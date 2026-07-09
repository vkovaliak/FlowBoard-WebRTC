// FlowBoard Video Calls -- WebRTC mesh orchestration.
// Owns every RTCPeerConnection (Architecture AD-1, AD-8) and the offer/
// answer/ICE exchange over the CallHub (AD-2, AD-3). C# never sees any of
// this -- only primitive roster events cross the interop boundary
// (handled in interop.js/CallView.razor, not here).

let hubConnection = null;
let localConnectionId = null;
let dotNetObjectRef = null;
const peerConnections = new Map(); // AD-8: connectionId -> RTCPeerConnection
const remoteStreams = new Map(); // connectionId -> MediaStream, for re-attaching after a tile re-render
let rtcConfiguration = { iceServers: [] };

/// Registers CallHub listeners for the signaling messages this module owns.
/// Called once join + local media capture have both settled (see
/// interop.js) so createPeerConnection's cameraStream lookup is never
/// called before getUserMedia has resolved one way or the other.
function initializeMesh(connection, dotNetRef, iceServers) {
    hubConnection = connection;
    localConnectionId = connection.connectionId;
    dotNetObjectRef = dotNetRef;
    rtcConfiguration = { iceServers: iceServers || [] };

    hubConnection.on("ReceiveOffer", function (callerConnectionId, sdpJson) {
        handleReceiveOffer(callerConnectionId, sdpJson);
    });

    hubConnection.on("ReceiveAnswer", function (callerConnectionId, sdpJson) {
        handleReceiveAnswer(callerConnectionId, sdpJson);
    });

    hubConnection.on("ReceiveIceCandidate", function (callerConnectionId, candidateJson) {
        handleReceiveIceCandidate(callerConnectionId, candidateJson);
    });

    // AD-8: every removal path pairs pc.close() with Map-entry deletion --
    // a participant leaving is one such path, not just failure/hang-up.
    hubConnection.on("ParticipantLeft", function (connectionId) {
        closeAndRemovePeer(connectionId);
    });
}

/// The joiner is always the offerer to every participant already in the
/// room (AD-2). Called once, right after JoinRoom's snapshot is known.
/// N participants means N independent createOffererConnection calls, each
/// writing only its own Map entry (AD-8) -- there is no shared mutable state
/// between pairs, so joining a 6th participant can never glitch, drop, or
/// renegotiate the 5 connections already established between the others
/// (FR-8). No participant-count branching exists here or anywhere in this
/// file, by construction (AD-5, FR-9) -- this loop runs identically whether
/// existingConnectionIds has 1 entry or 50.
function connectToExistingPeers(existingConnectionIds) {
    existingConnectionIds.forEach(function (peerId) {
        createOffererConnection(peerId);
    });
}

function createOffererConnection(peerId) {
    const pc = createPeerConnection(peerId);
    peerConnections.set(peerId, pc);

    pc.createOffer()
        .then(function (offer) {
            return pc.setLocalDescription(offer);
        })
        .then(function () {
            return hubConnection.invoke("SendOffer", peerId, JSON.stringify(pc.localDescription));
        });
}

function handleReceiveOffer(callerConnectionId, sdpJson) {
    let pc = peerConnections.get(callerConnectionId);

    if (pc) {
        // Glare: we already hold a connection to this peer, meaning we must
        // have also sent them an offer. AD-2's atomic join snapshot should
        // make this unreachable in practice -- this is defense-in-depth for
        // a bug in that guarantee, not the expected path.
        if (localConnectionId > callerConnectionId) {
            // We are the lexicographically greater id -- keep our own
            // outbound offer, ignore theirs.
            return;
        }

        // They are greater -- discard our outbound offer/connection
        // (AD-8: close-then-delete) and answer theirs instead.
        closeAndRemovePeer(callerConnectionId);
    }

    pc = createPeerConnection(callerConnectionId);
    peerConnections.set(callerConnectionId, pc);

    const offer = JSON.parse(sdpJson);
    pc.setRemoteDescription(offer)
        .then(function () {
            return pc.createAnswer();
        })
        .then(function (answer) {
            return pc.setLocalDescription(answer);
        })
        .then(function () {
            return hubConnection.invoke("SendAnswer", callerConnectionId, JSON.stringify(pc.localDescription));
        });
}

function handleReceiveAnswer(callerConnectionId, sdpJson) {
    const pc = peerConnections.get(callerConnectionId);
    if (!pc) {
        return;
    }

    const answer = JSON.parse(sdpJson);
    pc.setRemoteDescription(answer);
}

function handleReceiveIceCandidate(callerConnectionId, candidateJson) {
    const pc = peerConnections.get(callerConnectionId);
    if (!pc) {
        return;
    }

    const candidate = JSON.parse(candidateJson);
    pc.addIceCandidate(candidate);
}

function createPeerConnection(peerId) {
    const pc = new RTCPeerConnection(rtcConfiguration);

    // AD-6: fan out the single shared cameraStream's tracks to this
    // connection -- never a fresh getUserMedia call per connection.
    const stream = getCameraStream();
    if (stream) {
        stream.getTracks().forEach(function (track) {
            pc.addTrack(track, stream);
        });
    }

    pc.onicecandidate = function (event) {
        if (event.candidate) {
            hubConnection.invoke("SendIceCandidate", peerId, JSON.stringify(event.candidate));
        }
    };

    pc.ontrack = function (event) {
        attachRemoteStream(peerId, event.streams[0]);
    };

    // FR-10/AD-8: this handler is a closure over exactly one peerId and one
    // pc -- it never iterates peerConnections, so a failure here cannot
    // touch any other pair. A "failed"/"disconnected" ICE state does NOT
    // close or remove the Map entry (that would contradict "purely
    // informational, no retry action" -- PRD FR-10): it only reports the
    // badge state. Removal only ever happens via closeAndRemovePeer, called
    // from the two paths that already exist (ParticipantLeft, glare
    // discard) -- never from here.
    pc.oniceconnectionstatechange = function () {
        var state = pc.iceConnectionState;
        var hasIssue = state === "failed" || state === "disconnected";
        if (dotNetObjectRef) {
            dotNetObjectRef.invokeMethodAsync("OnConnectionIssue", peerId, hasIssue);
        }
    };

    return pc;
}

function attachRemoteStream(peerId, stream) {
    remoteStreams.set(peerId, stream);
    attachRemoteStreamToElement(peerId, stream);
}

function attachRemoteStreamToElement(peerId, stream) {
    const videoEl = document.getElementById("remote-video-" + peerId);
    if (videoEl) {
        videoEl.srcObject = stream;
    } else {
        // The Video Tile for this participant may not have rendered yet --
        // ParticipantJoined (drives Blazor's render) and ontrack (drives
        // this) are two independent async paths with no guaranteed
        // ordering. A short poll is a deliberate, minimal fix for this
        // story's scope.
        setTimeout(function () { attachRemoteStreamToElement(peerId, stream); }, 200);
    }
}

/// Called from interop.js's ParticipantCameraChanged handler when a remote
/// participant turns their camera back ON. Their <video> element was just
/// torn down and recreated by Blazor (HasMedia flipped false -> true) --
/// ontrack only fires once per track, not on every DOM re-render, so
/// nothing else re-attaches srcObject to the new element. Same underlying
/// bug as the local camera-toggle fix in media.js/interop.js, same fix
/// shape: re-attach from a stored reference instead of relying on a
/// one-time event.
function reattachRemoteStream(peerId) {
    const stream = remoteStreams.get(peerId);
    if (stream) {
        attachRemoteStreamToElement(peerId, stream);
    }
}

/// Touches exactly one Map entry -- every other participant's connection to
/// every other participant is untouched (AD-8), regardless of room size.
function closeAndRemovePeer(connectionId) {
    const pc = peerConnections.get(connectionId);
    if (pc) {
        pc.close();
        peerConnections.delete(connectionId); // AD-8: close-then-delete, always paired
    }
    remoteStreams.delete(connectionId);
}

/// hangUp() (interop.js) calls this for a full teardown. Reuses the same
/// close-then-delete helper per entry -- AD-8's discipline holds even for
/// "close everything," not just single-peer removal. Keys are snapshotted
/// with Array.from before iterating, since deleting from a Map while
/// iterating it directly is unsafe.
function closeAllPeerConnections() {
    Array.from(peerConnections.keys()).forEach(function (connectionId) {
        closeAndRemovePeer(connectionId);
    });
}
