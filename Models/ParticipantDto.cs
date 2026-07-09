namespace FlowBoardVideoCalls.Models;

/// <summary>
/// The wire-facing shape of a Participant, crossing the CallHub boundary
/// (JoinRoom's snapshot return value, ParticipantJoined). ConnectionId is
/// SignalR's own Context.ConnectionId -- the canonical peer identifier
/// end-to-end (Architecture Consistency Conventions), never re-keyed.
/// </summary>
public sealed record ParticipantDto(
    string ConnectionId,
    string DisplayName,
    bool IsSharing);
