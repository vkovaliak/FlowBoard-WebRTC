// FlowBoard Video Calls -- the sole JS<->C# interop boundary (Architecture AD-1).
// The media plane (JS) talks to the signaling plane (CallHub) directly via its
// own SignalR connection, per the Design Paradigm -- never routed through C#.

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

    connection.on("ParticipantMuteChanged", function (connectionId, isMuted) {
        dotNetRef.invokeMethodAsync("OnParticipantMuteChanged", connectionId, isMuted);
    });

    connection.on("ParticipantCameraChanged", function (connectionId, isCameraOn) {
        // Their <video> element was just recreated by Blazor (HasMedia
        // false -> true) -- ontrack won't fire again for an already-flowing
        // track, so re-attach explicitly from the stored stream reference.
        if (isCameraOn) {
            reattachRemoteStream(connectionId);
        }
        dotNetRef.invokeMethodAsync("OnParticipantCameraChanged", connectionId, isCameraOn);
    });

    // AD-4: a disconnect is terminal -- no automatic reconnection is
    // attempted anywhere in this module (matches PRD's no-auto-reconnect
    // non-goal). pagehide proactively closes the connection on refresh/
    // navigate so OnDisconnectedAsync fires promptly server-side instead of
    // waiting on passive SignalR timeout detection. Also releases local
    // media and closes every peer connection -- the same cleanup hangUp()
    // does explicitly, but for the tab-closed/navigated-away path rather
    // than the red-button path.
    window.addEventListener("pagehide", function () {
        closeAllPeerConnections();
        stopLocalMedia();
        if (connection) {
            connection.stop();
        }
    });

    var mediaPromise = captureLocalMedia(dotNetRef);

    var joinPromise = connection.start()
        .then(function () {
            return connection.invoke("JoinRoom", roomId, displayName);
        })
        .then(function (existingParticipants) {
            existingParticipants.forEach(function (participant) {
                dotNetRef.invokeMethodAsync("OnParticipantJoined", participant);
            });
            return existingParticipants;
        });

    // Mesh setup (registering ReceiveOffer/ReceiveAnswer/ReceiveIceCandidate
    // and offering to existing peers) waits for BOTH the join and the local
    // media attempt to settle. This is deliberate: createPeerConnection
    // looks up the cameraStream via media.js, and without this join point
    // that lookup could race ahead of getUserMedia resolving -- on either
    // side of a pair (this client as the joining offerer, or as an
    // already-present answerer receiving someone else's offer).
    return Promise.all([joinPromise, mediaPromise]).then(function (results) {
        var existingParticipants = results[0];
        initializeMesh(connection, dotNetRef, iceServers);
        connectToExistingPeers(existingParticipants.map(function (p) {
            return p.connectionId;
        }));
    });
}

/// Flips track.enabled on the shared cameraStream's audio track (AD-6,
/// media.js owns the stream) and reports the new state to every other
/// participant via SetMuteState (FR-15). Never touches any RTCPeerConnection
/// directly -- the same track object is already referenced by every peer
/// connection's sender, so toggling it once is enough for all of them.
function toggleMic() {
    var newMuted = !isMicMuted();
    setMicMuted(newMuted);
    return connection.invoke("SetMuteState", newMuted);
}

/// Flips track.enabled on the shared cameraStream's video track (AD-6) and
/// reports the new state via SetCameraState (FR-16).
function toggleCamera() {
    var newCameraOn = !isCameraOn();
    setCameraOn(newCameraOn);
    if (newCameraOn) {
        // Turning back on: Blazor just recreated <video id="local-video">
        // (HasMedia false -> true) as a brand-new DOM element with no
        // srcObject. Re-attach explicitly -- the same fix shape as the
        // original local-video race, applied to the toggle-back-on case.
        attachLocalVideoElement();
    }
    return connection.invoke("SetCameraState", newCameraOn);
}

/// Closes every RTCPeerConnection (AD-8 close-then-delete, via
/// webrtc-mesh.js's closeAllPeerConnections), releases the camera/mic
/// hardware, then stops the SignalR connection -- which triggers
/// OnDisconnectedAsync/RemoveAndGetRemaining server-side (Epic 1), notifying
/// every remaining participant via ParticipantLeft (FR-18). Navigation back
/// to Room Entry is a C# concern (NavigationManager), not this function's
/// job -- CallView.razor does that after this resolves.
function hangUp() {
    closeAllPeerConnections();
    stopLocalMedia();
    if (connection) {
        return connection.stop();
    }
    return Promise.resolve();
}
