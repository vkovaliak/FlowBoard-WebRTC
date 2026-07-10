using FlowBoardVideoCalls.Services;

namespace FlowBoard.Tests;

/// <summary>
/// RoomRegistry tests (AD-4: single source of truth for Room membership,
/// mutated only through atomic compound operations). RoomRegistry has no
/// dependencies to mock -- these exercise the real singleton directly.
/// </summary>
public class RoomRegistryTests
{
    // --- AddAndSnapshot (AD-4) ---

    [Fact]
    public void AddAndSnapshot_FirstParticipantInRoom_ReturnsEmptySnapshot()
    {
        var registry = new RoomRegistry();

        var existing = registry.AddAndSnapshot("conn-1", "room-1", "Alice");

        Assert.Empty(existing);
    }

    [Fact]
    public void AddAndSnapshot_SecondParticipantInRoom_SnapshotContainsFirstParticipant()
    {
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");

        var existing = registry.AddAndSnapshot("conn-2", "room-1", "Bob");

        var participant = Assert.Single(existing);
        Assert.Equal("conn-1", participant.ConnectionId);
        Assert.Equal("Alice", participant.DisplayName);
        Assert.Equal("room-1", participant.RoomId);
    }

    [Fact]
    public void AddAndSnapshot_ParticipantInDifferentRoom_NotIncludedInSnapshot()
    {
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");
        registry.AddAndSnapshot("conn-2", "room-2", "Carol"); // different room entirely

        var existing = registry.AddAndSnapshot("conn-3", "room-1", "Bob");

        var participant = Assert.Single(existing);
        Assert.Equal("conn-1", participant.ConnectionId);
    }

    [Fact]
    public void AddAndSnapshot_NewParticipant_IsRetrievableViaGetParticipants()
    {
        var registry = new RoomRegistry();

        registry.AddAndSnapshot("conn-1", "room-1", "Alice");

        var participants = registry.GetParticipants("room-1");
        var participant = Assert.Single(participants);
        Assert.Equal("conn-1", participant.ConnectionId);
        Assert.False(participant.IsSharing);
    }

    // --- RemoveAndGetRemaining (AD-4, FR-4) ---

    [Fact]
    public void RemoveAndGetRemaining_OneOfTwoParticipants_ReturnsCorrectRemaining()
    {
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");
        registry.AddAndSnapshot("conn-2", "room-1", "Bob");

        var (roomId, remaining, _) = registry.RemoveAndGetRemaining("conn-1");

        Assert.Equal("room-1", roomId);
        var participant = Assert.Single(remaining);
        Assert.Equal("conn-2", participant.ConnectionId);
    }

    [Fact]
    public void RemoveAndGetRemaining_LastParticipant_RoomBecomesEmpty()
    {
        // FR-4: removing the last participant leaves nothing behind for
        // that roomId -- an empty room is just the absence of any record
        // with that RoomId, not a separate cleanup step (AD-4).
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");

        var (roomId, remaining, _) = registry.RemoveAndGetRemaining("conn-1");

        Assert.Equal("room-1", roomId);
        Assert.Empty(remaining);
        Assert.Empty(registry.GetParticipants("room-1"));
    }

    [Fact]
    public void RemoveAndGetRemaining_UnknownConnectionId_ReturnsNullRoomIdAndEmptyRemaining()
    {
        var registry = new RoomRegistry();

        var (roomId, remaining, wasSharing) = registry.RemoveAndGetRemaining("never-joined");

        Assert.Null(roomId);
        Assert.Empty(remaining);
        Assert.False(wasSharing);
    }

    [Fact]
    public void RemoveAndGetRemaining_RemovedConnectionId_NoLongerResolvesARoom()
    {
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");

        registry.RemoveAndGetRemaining("conn-1");

        Assert.Null(registry.TryGetRoomId("conn-1"));
    }

    // --- SetSharingState (AD-7) ---

    [Fact]
    public void SetSharingState_SetTrue_MarksParticipantSharing()
    {
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");

        registry.SetSharingState("conn-1", true);

        var participant = Assert.Single(registry.GetParticipants("room-1"));
        Assert.True(participant.IsSharing);
    }

    [Fact]
    public void SetSharingState_SetFalseAfterTrue_ClearsSharingState()
    {
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");
        registry.SetSharingState("conn-1", true);

        registry.SetSharingState("conn-1", false);

        var participant = Assert.Single(registry.GetParticipants("room-1"));
        Assert.False(participant.IsSharing);
    }

    [Fact]
    public void SetSharingState_UnknownConnectionId_ThrowsInvalidOperationException()
    {
        var registry = new RoomRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.SetSharingState("never-joined", true));
    }

    // --- Sharing state carried through removal (AD-7, FR-14) ---

    [Fact]
    public void RemoveAndGetRemaining_ActiveSharerRemoved_ReturnsWasSharingTrue()
    {
        // FR-14: CallHub.OnDisconnectedAsync relies on this flag alone to
        // decide whether a disconnect must also clear the active share.
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");
        registry.SetSharingState("conn-1", true);

        var (_, _, wasSharing) = registry.RemoveAndGetRemaining("conn-1");

        Assert.True(wasSharing);
    }

    [Fact]
    public void RemoveAndGetRemaining_NonSharerRemoved_ReturnsWasSharingFalse()
    {
        var registry = new RoomRegistry();
        registry.AddAndSnapshot("conn-1", "room-1", "Alice");

        var (_, _, wasSharing) = registry.RemoveAndGetRemaining("conn-1");

        Assert.False(wasSharing);
    }

    // --- No participant cap, anywhere (AD-5, FR-9) ---

    [Fact]
    public void AddAndSnapshot_TenPlusParticipants_AllSucceedWithGrowingSnapshot()
    {
        var registry = new RoomRegistry();
        const int participantCount = 25;

        for (var i = 0; i < participantCount; i++)
        {
            var existing = registry.AddAndSnapshot($"conn-{i}", "room-1", $"User{i}");
            Assert.Equal(i, existing.Count); // never rejected -- count simply grows by one each time
        }

        Assert.Equal(participantCount, registry.GetParticipants("room-1").Count);
    }

    // --- Thread-safety / atomicity (AD-4: the glare-race protection AD-2 depends on) ---

    [Fact]
    public async Task AddAndSnapshot_ManyConcurrentJoins_NeverCorruptsStateOrThrows()
    {
        // The critical guarantee AD-2's deterministic-offerer rule depends
        // on: the atomic add-then-snapshot step means two simultaneous
        // joiners can never both observe the other as "not here yet."
        var registry = new RoomRegistry();
        const int participantCount = 50;

        var tasks = Enumerable.Range(0, participantCount)
            .Select(i => Task.Run(() => registry.AddAndSnapshot($"conn-{i}", "room-1", $"User{i}")))
            .ToArray();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));

        Assert.Null(exception);
        var finalParticipants = registry.GetParticipants("room-1");
        Assert.Equal(participantCount, finalParticipants.Count);
        Assert.Equal(participantCount, finalParticipants.Select(p => p.ConnectionId).Distinct().Count());
    }

    [Fact]
    public async Task AddAndRemove_ManyConcurrentJoinsAndLeaves_NeverCorruptsStateOrThrows()
    {
        var registry = new RoomRegistry();
        const int initialCount = 50;

        for (var i = 0; i < initialCount; i++)
        {
            registry.AddAndSnapshot($"conn-{i}", "room-1", $"User{i}");
        }

        // Half of the original participants leave while a fresh batch joins,
        // all concurrently -- exercises AddAndSnapshot and
        // RemoveAndGetRemaining fighting over the same coarse lock at once.
        var removeTasks = Enumerable.Range(0, initialCount / 2)
            .Select(i => (Task)Task.Run(() => registry.RemoveAndGetRemaining($"conn-{i}")));
        var addTasks = Enumerable.Range(initialCount, initialCount / 2)
            .Select(i => (Task)Task.Run(() => registry.AddAndSnapshot($"conn-{i}", "room-1", $"User{i}")));

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(removeTasks.Concat(addTasks)));

        Assert.Null(exception);
        var finalParticipants = registry.GetParticipants("room-1");
        Assert.Equal(initialCount, finalParticipants.Count); // half the original survivors + the new joiners
        Assert.Equal(initialCount, finalParticipants.Select(p => p.ConnectionId).Distinct().Count());
    }
}
