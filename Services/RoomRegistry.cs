using System.Collections.Concurrent;

namespace FlowBoardVideoCalls.Services;

/// <summary>
/// RoomRegistry-internal membership record (Architecture AD-4). Not a wire
/// type -- see Models.ParticipantDto for what actually crosses the CallHub
/// boundary.
/// </summary>
public sealed class ParticipantRecord
{
    public required string ConnectionId { get; init; }
    public required string RoomId { get; init; }
    public required string DisplayName { get; init; }
    public bool IsSharing { get; set; }
}

/// <summary>
/// Singleton, in-memory source of truth for Room membership (Architecture
/// AD-4). Backed by exactly one ConcurrentDictionary keyed by connectionId --
/// no separate reverse-lookup dictionary, so there is no second writer to
/// fall out of sync with the first. The three compound operations below are
/// its only mutation entry points, each guarded by one coarse lock around the
/// whole compound step (add-then-snapshot, remove-then-snapshot,
/// mutate-then-snapshot) so two concurrent callers can never observe a
/// half-applied state. A single coarse lock is deliberate: room sizes and
/// join/leave frequency at this project's scale don't justify per-room
/// fine-grained locking (Rule of Three applies if that ever changes).
/// </summary>
public sealed class RoomRegistry
{
    private readonly ConcurrentDictionary<string, ParticipantRecord> _participants = new();
    private readonly object _lock = new();

    /// <summary>
    /// Adds the caller to <paramref name="roomId"/> and returns the
    /// existing-participants snapshot as one atomic step. CallHub.JoinRoom
    /// calls this exactly once -- never Add and a separate GetParticipants as
    /// two steps -- so two simultaneous joiners can never both observe the
    /// other as "not here yet" (the precondition AD-2's deterministic
    /// offerer rule depends on).
    /// </summary>
    public IReadOnlyList<ParticipantRecord> AddAndSnapshot(string connectionId, string roomId, string displayName)
    {
        lock (_lock)
        {
            var existing = GetParticipantsNoLock(roomId);

            _participants[connectionId] = new ParticipantRecord
            {
                ConnectionId = connectionId,
                RoomId = roomId,
                DisplayName = displayName,
            };

            return existing;
        }
    }

    /// <summary>
    /// Removes the caller and returns who's left in their Room, atomically --
    /// plus whether the removed participant was the active sharer (AD-7), so
    /// CallHub.OnDisconnectedAsync can clear the share as part of the same
    /// atomic step that removed them, rather than a second, separately-timed
    /// lookup. CallHub.OnDisconnectedAsync is the sole caller and the sole
    /// teardown path -- there is no separate LeaveRoom method.
    /// </summary>
    public (string? RoomId, IReadOnlyList<ParticipantRecord> Remaining, bool WasSharing) RemoveAndGetRemaining(string connectionId)
    {
        lock (_lock)
        {
            if (!_participants.TryRemove(connectionId, out var removed))
            {
                return (null, Array.Empty<ParticipantRecord>(), false);
            }

            var remaining = GetParticipantsNoLock(removed.RoomId);
            return (removed.RoomId, remaining, removed.IsSharing);
        }
    }

    /// <summary>
    /// Sets the caller's sharing flag and returns their Room's current peers,
    /// atomically. Used by the screen-share start/stop flow (AD-7).
    /// </summary>
    public (string RoomId, IReadOnlyList<ParticipantRecord> Peers) SetSharingState(string connectionId, bool isSharing)
    {
        lock (_lock)
        {
            if (!_participants.TryGetValue(connectionId, out var record))
            {
                throw new InvalidOperationException($"Connection '{connectionId}' is not a member of any Room.");
            }

            record.IsSharing = isSharing;
            return (record.RoomId, GetParticipantsNoLock(record.RoomId));
        }
    }

    /// <summary>A filtered read over the single dictionary -- no lock required.</summary>
    public IReadOnlyList<ParticipantRecord> GetParticipants(string roomId) => GetParticipantsNoLock(roomId);

    /// <summary>Read-only lookup of which Room a connectionId currently belongs to.
    /// Not a mutation entry point -- exists so CallHub methods that take no
    /// roomId parameter (SetMuteState, SetCameraState) can find the caller's
    /// Room to broadcast to.</summary>
    public string? TryGetRoomId(string connectionId) =>
        _participants.TryGetValue(connectionId, out var record) ? record.RoomId : null;

    private List<ParticipantRecord> GetParticipantsNoLock(string roomId) =>
        _participants.Values.Where(p => p.RoomId == roomId).ToList();
}
