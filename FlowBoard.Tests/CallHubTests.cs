using FlowBoardVideoCalls.Hubs;
using FlowBoardVideoCalls.Models;
using FlowBoardVideoCalls.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace FlowBoard.Tests;

/// <summary>
/// CallHub tests (AD-3 opaque relay, AD-4 membership orchestration, AD-7
/// disconnect-ends-share). RoomRegistry is left real -- it's exercised
/// directly in RoomRegistryTests, and CallHub's own job here is purely
/// orchestration over it. Only the SignalR plumbing (IHubCallerClients /
/// IClientProxy / HubCallerContext / IGroupManager) is mocked, via Hub's own
/// public settable Clients/Context/Groups properties -- the standard
/// pattern for unit-testing a Hub without a live connection.
/// </summary>
public class CallHubTests
{
    /// <summary>Everything needed to drive one CallHub method call as a
    /// specific connectionId and inspect exactly which client/group proxy
    /// received which message afterward.</summary>
    private sealed class HubHarness
    {
        public required CallHub Hub { get; init; }
        public required Mock<IHubCallerClients> Clients { get; init; }
        public required Mock<IGroupManager> Groups { get; init; }
        // Client(id) returns ISingleClientProxy specifically, not the plain
        // IClientProxy that Group/OthersInGroup return -- ISingleClientProxy
        // extends IClientProxy, so its mock's .Object still satisfies either
        // return type.
        public required Dictionary<string, Mock<ISingleClientProxy>> ClientProxies { get; init; }
        public required Dictionary<string, Mock<IClientProxy>> GroupProxies { get; init; }
        public required Dictionary<string, Mock<IClientProxy>> OthersInGroupProxies { get; init; }

        public Mock<ISingleClientProxy> ClientProxyFor(string connectionId) => ClientProxies[connectionId];
        public Mock<IClientProxy> GroupProxyFor(string roomId) => GroupProxies[roomId];
        public Mock<IClientProxy> OthersInGroupProxyFor(string roomId) => OthersInGroupProxies[roomId];

        public static void AssertSent<TProxy>(Mock<TProxy> proxy, string method, params object?[] args)
            where TProxy : class, IClientProxy =>
            proxy.Verify(
                p => p.SendCoreAsync(method, It.Is<object?[]>(a => a.SequenceEqual(args)), It.IsAny<CancellationToken>()),
                Times.Once);

        public void AssertNeverBroadcast() =>
            Clients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
    }

    private static HubHarness CreateHub(RoomRegistry registry, string connectionId)
    {
        var clientProxies = new Dictionary<string, Mock<ISingleClientProxy>>();
        var groupProxies = new Dictionary<string, Mock<IClientProxy>>();
        var othersInGroupProxies = new Dictionary<string, Mock<IClientProxy>>();

        static Mock<TProxy> ProxyFor<TProxy>(Dictionary<string, Mock<TProxy>> store, string key)
            where TProxy : class, IClientProxy
        {
            if (store.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var proxy = new Mock<TProxy>();
            proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            store[key] = proxy;
            return proxy;
        }

        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Client(It.IsAny<string>()))
            .Returns((string id) => ProxyFor(clientProxies, id).Object);
        clients.Setup(c => c.Group(It.IsAny<string>()))
            .Returns((string roomId) => ProxyFor(groupProxies, roomId).Object);
        clients.Setup(c => c.OthersInGroup(It.IsAny<string>()))
            .Returns((string roomId) => ProxyFor(othersInGroupProxies, roomId).Object);

        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns(connectionId);

        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hub = new CallHub(registry)
        {
            Clients = clients.Object,
            Context = context.Object,
            Groups = groups.Object,
        };

        return new HubHarness
        {
            Hub = hub,
            Clients = clients,
            Groups = groups,
            ClientProxies = clientProxies,
            GroupProxies = groupProxies,
            OthersInGroupProxies = othersInGroupProxies,
        };
    }

    // --- JoinRoom (AD-4) ---

    [Fact]
    public async Task JoinRoom_FirstParticipant_ReturnsEmptySnapshot()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "A");

        var existing = await harness.Hub.JoinRoom("room-1", "Alice");

        Assert.Empty(existing);
    }

    [Fact]
    public async Task JoinRoom_SecondParticipant_ReturnsSnapshotWithFirstParticipant()
    {
        var registry = new RoomRegistry();
        var bHarness = CreateHub(registry, "B");
        await bHarness.Hub.JoinRoom("room-1", "Bob");

        var aHarness = CreateHub(registry, "A");
        var existing = await aHarness.Hub.JoinRoom("room-1", "Alice");

        var participant = Assert.Single(existing);
        Assert.Equal("B", participant.ConnectionId);
        Assert.Equal("Bob", participant.DisplayName);
    }

    [Fact]
    public async Task JoinRoom_Caller_IsAddedToTheSignalRGroupForTheRoom()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "A");

        await harness.Hub.JoinRoom("room-1", "Alice");

        harness.Groups.Verify(g => g.AddToGroupAsync("A", "room-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_ExistingParticipant_ReceivesParticipantJoinedWithCorrectDto()
    {
        var registry = new RoomRegistry();
        var bHarness = CreateHub(registry, "B");
        await bHarness.Hub.JoinRoom("room-1", "Bob");

        var aHarness = CreateHub(registry, "A");
        await aHarness.Hub.JoinRoom("room-1", "Alice");

        var expectedDto = new ParticipantDto("A", "Alice", IsSharing: false);
        HubHarness.AssertSent(aHarness.OthersInGroupProxyFor("room-1"), "ParticipantJoined", expectedDto);
    }

    [Fact]
    public async Task JoinRoom_ManyParticipants_NeverRejectsRegardlessOfRoomSize()
    {
        // AD-5/FR-9: no participant cap anywhere in JoinRoom or RoomRegistry.
        var registry = new RoomRegistry();
        const int participantCount = 30;

        for (var i = 0; i < participantCount; i++)
        {
            var harness = CreateHub(registry, $"conn-{i}");
            var existing = await harness.Hub.JoinRoom("room-1", $"User{i}");
            Assert.Equal(i, existing.Count);
        }

        Assert.Equal(participantCount, registry.GetParticipants("room-1").Count);
    }

    // --- SendOffer / SendAnswer / SendIceCandidate (AD-3: opaque relay) ---

    [Fact]
    public async Task SendOffer_ForwardsVerbatimToTargetConnectionIdOnly()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "A");

        await harness.Hub.SendOffer("B", "{\"type\":\"offer\",\"sdp\":\"opaque-sdp\"}");

        HubHarness.AssertSent(harness.ClientProxyFor("B"), "ReceiveOffer", "A", "{\"type\":\"offer\",\"sdp\":\"opaque-sdp\"}");
        harness.AssertNeverBroadcast(); // never Clients.Group / Clients.Others -- AD-3
    }

    [Fact]
    public async Task SendAnswer_ForwardsVerbatimToTargetConnectionIdOnly()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "B");

        await harness.Hub.SendAnswer("A", "{\"type\":\"answer\",\"sdp\":\"opaque-sdp\"}");

        HubHarness.AssertSent(harness.ClientProxyFor("A"), "ReceiveAnswer", "B", "{\"type\":\"answer\",\"sdp\":\"opaque-sdp\"}");
        harness.AssertNeverBroadcast();
    }

    [Fact]
    public async Task SendIceCandidate_ForwardsVerbatimToTargetConnectionIdOnly()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "A");

        await harness.Hub.SendIceCandidate("B", "{\"candidate\":\"opaque-ice\"}");

        HubHarness.AssertSent(harness.ClientProxyFor("B"), "ReceiveIceCandidate", "A", "{\"candidate\":\"opaque-ice\"}");
        harness.AssertNeverBroadcast();
    }

    // --- OnDisconnectedAsync (AD-4, AD-7, FR-14) ---

    [Fact]
    public async Task OnDisconnectedAsync_RemovesParticipant_RemainingReceiveParticipantLeft()
    {
        var registry = new RoomRegistry();
        var bHarness = CreateHub(registry, "B");
        await bHarness.Hub.JoinRoom("room-1", "Bob");
        var aHarness = CreateHub(registry, "A");
        await aHarness.Hub.JoinRoom("room-1", "Alice");

        await aHarness.Hub.OnDisconnectedAsync(null);

        HubHarness.AssertSent(aHarness.OthersInGroupProxyFor("room-1"), "ParticipantLeft", "A");
        Assert.Null(registry.TryGetRoomId("A"));
    }

    [Fact]
    public async Task OnDisconnectedAsync_UnknownConnectionId_DoesNotBroadcastOrThrow()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "never-joined");

        var exception = await Record.ExceptionAsync(() => harness.Hub.OnDisconnectedAsync(null));

        Assert.Null(exception);
        harness.Clients.Verify(c => c.OthersInGroup(It.IsAny<string>()), Times.Never);
        harness.AssertNeverBroadcast();
    }

    [Fact]
    public async Task OnDisconnectedAsync_ActiveSharerDisconnects_BroadcastsScreenShareStateChangedNull()
    {
        // FR-14: a disconnect mid-share is treated identically to that
        // participant clicking Stop -- same BroadcastSharingState call.
        var registry = new RoomRegistry();
        var aHarness = CreateHub(registry, "A");
        await aHarness.Hub.JoinRoom("room-1", "Alice");
        await aHarness.Hub.SetSharingState(true);

        await aHarness.Hub.OnDisconnectedAsync(null);

        HubHarness.AssertSent(aHarness.GroupProxyFor("room-1"), "ScreenShareStateChanged", (string?)null);
    }

    [Fact]
    public async Task OnDisconnectedAsync_NonSharerDisconnects_DoesNotBroadcastScreenShareStateChanged()
    {
        var registry = new RoomRegistry();
        var aHarness = CreateHub(registry, "A");
        await aHarness.Hub.JoinRoom("room-1", "Alice");

        await aHarness.Hub.OnDisconnectedAsync(null);

        aHarness.Clients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
    }

    // --- SetSharingState (AD-7) ---

    [Fact]
    public async Task SetSharingState_StartSharing_BroadcastsScreenShareStateChangedWithCallerId()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "A");
        await harness.Hub.JoinRoom("room-1", "Alice");

        await harness.Hub.SetSharingState(true);

        HubHarness.AssertSent(harness.GroupProxyFor("room-1"), "ScreenShareStateChanged", "A");
    }

    [Fact]
    public async Task SetSharingState_StopSharing_BroadcastsScreenShareStateChangedWithNull()
    {
        var registry = new RoomRegistry();
        var harness = CreateHub(registry, "A");
        await harness.Hub.JoinRoom("room-1", "Alice");
        await harness.Hub.SetSharingState(true);

        await harness.Hub.SetSharingState(false);

        HubHarness.AssertSent(harness.GroupProxyFor("room-1"), "ScreenShareStateChanged", (string?)null);
    }
}
