using FlowBoardVideoCalls.Models;
using FlowBoardVideoCalls.Services;
using Microsoft.AspNetCore.SignalR;

namespace FlowBoardVideoCalls.Hubs;

/// <summary>
/// Signaling-plane relay and Room membership hub (Architecture AD-3, AD-4).
/// Never inspects or holds a WebRTC object -- see Signaling &amp; Interop
/// Contract in ARCHITECTURE-SPINE.md for the complete, exact method/callback
/// set this hub implements. Room broadcasts use SignalR's own Group
/// mechanism (one group per Room, named by roomId).
/// </summary>
public sealed class CallHub : Hub
{
    private readonly RoomRegistry _roomRegistry;

    public CallHub(RoomRegistry roomRegistry)
    {
        _roomRegistry = roomRegistry;
    }

    /// <summary>Calls RoomRegistry.AddAndSnapshot; returns existing participants (AD-4).</summary>
    public async Task<IReadOnlyList<ParticipantDto>> JoinRoom(string roomId, string displayName)
    {
        var existing = _roomRegistry.AddAndSnapshot(Context.ConnectionId, roomId, displayName);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var joined = new ParticipantDto(Context.ConnectionId, displayName, IsSharing: false);
        await Clients.OthersInGroup(roomId).SendAsync("ParticipantJoined", joined);

        return existing.Select(ToDto).ToList();
    }

    /// <summary>Opaque relay (AD-3) -- forwards verbatim, never inspects the SDP.</summary>
    public Task SendOffer(string targetConnectionId, string sdp) =>
        Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", Context.ConnectionId, sdp);

    /// <summary>Opaque relay (AD-3) -- forwards verbatim, never inspects the SDP.</summary>
    public Task SendAnswer(string targetConnectionId, string sdp) =>
        Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", Context.ConnectionId, sdp);

    /// <summary>Opaque relay (AD-3) -- forwards verbatim, never inspects the ICE candidate.</summary>
    public Task SendIceCandidate(string targetConnectionId, string candidateJson) =>
        Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidateJson);

    /// <summary>Broadcasts ParticipantMuteChanged to the caller's room.</summary>
    public async Task SetMuteState(bool isMuted)
    {
        var roomId = _roomRegistry.TryGetRoomId(Context.ConnectionId);
        if (roomId is null)
        {
            return;
        }

        await Clients.OthersInGroup(roomId).SendAsync("ParticipantMuteChanged", Context.ConnectionId, isMuted);
    }

    /// <summary>Broadcasts ParticipantCameraChanged -- a separate event from mute.</summary>
    public async Task SetCameraState(bool isCameraOn)
    {
        var roomId = _roomRegistry.TryGetRoomId(Context.ConnectionId);
        if (roomId is null)
        {
            return;
        }

        await Clients.OthersInGroup(roomId).SendAsync("ParticipantCameraChanged", Context.ConnectionId, isCameraOn);
    }

    /// <summary>Calls RoomRegistry.SetSharingState; broadcasts ScreenShareStateChanged (AD-7).</summary>
    public async Task SetSharingState(bool isSharing)
    {
        var (roomId, _) = _roomRegistry.SetSharingState(Context.ConnectionId, isSharing);

        var activeSharerConnectionId = isSharing ? Context.ConnectionId : null;
        await BroadcastSharingState(roomId, activeSharerConnectionId);
    }

    /// <summary>Calls RoomRegistry.RemoveAndGetRemaining; broadcasts ParticipantLeft (AD-4).
    /// Sole teardown path -- there is no separate LeaveRoom method. If the
    /// disconnecting participant was the active sharer (AD-7), a disconnect
    /// ends the share exactly like an explicit Stop Sharing click -- reusing
    /// BroadcastSharingState (the same call SetSharingState makes) rather
    /// than a parallel code path, so remaining Call Views reflow through the
    /// identical ScreenShareStateChanged(null) handler either way (FR-14).</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (roomId, _, wasSharing) = _roomRegistry.RemoveAndGetRemaining(Context.ConnectionId);

        if (roomId is not null)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ParticipantLeft", Context.ConnectionId);

            if (wasSharing)
            {
                await BroadcastSharingState(roomId, null);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Task BroadcastSharingState(string roomId, string? sharerConnectionId) =>
        Clients.Group(roomId).SendAsync("ScreenShareStateChanged", sharerConnectionId);

    private static ParticipantDto ToDto(ParticipantRecord record) =>
        new(record.ConnectionId, record.DisplayName, record.IsSharing);
}
