// FlowBoard Video Calls -- the sole JS<->C# interop boundary (Architecture AD-1).
// The media plane (JS) talks to the signaling plane (CallHub) directly via its
// own SignalR connection, per the Design Paradigm -- never routed through C#.
//
// Story 1.4 scope: room join + roster plumbing only. Epic 2 (Story 2.1)
// extends this same initializeCall function with getUserMedia/local media
// capture rather than introducing a new entry point -- no RTCPeerConnection
// or media code exists in this file yet, by design.

let connection = null;

function initializeCall(dotNetRef, roomId, displayName, iceServers) {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/callHub")
        .build();

    connection.on("ParticipantJoined", function (participant) {
        dotNetRef.invokeMethodAsync("OnParticipantJoined", participant);
    });

    connection.on("ParticipantLeft", function (connectionId) {
        dotNetRef.invokeMethodAsync("OnParticipantLeft", connectionId);
    });

    // AD-4: a disconnect is terminal -- no automatic reconnection is
    // attempted anywhere in this module (matches PRD's no-auto-reconnect
    // non-goal). pagehide proactively closes the connection on refresh/
    // navigate so OnDisconnectedAsync fires promptly server-side instead of
    // waiting on passive SignalR timeout detection.
    window.addEventListener("pagehide", function () {
        if (connection) {
            connection.stop();
        }
    });

    return connection.start()
        .then(function () {
            return connection.invoke("JoinRoom", roomId, displayName);
        })
        .then(function (existingParticipants) {
            existingParticipants.forEach(function (participant) {
                dotNetRef.invokeMethodAsync("OnParticipantJoined", participant);
            });
        });
}
