// FlowBoard Video Calls -- WebRTC mesh orchestration.
// Owns every RTCPeerConnection (Architecture AD-1, AD-8) and the offer/
// answer/ICE exchange over the CallHub (AD-2, AD-3). C# never sees any of
// this -- only primitive roster events cross the interop boundary
// (handled in interop.js/CallView.razor, not here).

let hubConnection = null;
let localConnectionId = null;
let dotNetObjectRef = null;
const peerConnections = new Map(); // AD-8: connectionId -> RTCPeerConnection
const remoteStreams = new Map(); // connectionId -> camera MediaStream, for re-attaching after a tile re-render
const remoteFirstStreamId = new Map(); // connectionId -> the FIRST distinct stream.id seen (AD-7: that one is always the camera stream)
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

    if (pc && pc._negotiated) {
        // Renegotiation (e.g. screen-share start/stop, Renegotiation
        // corollary) -- NOT glare. Glare can only happen before the initial
        // handshake completes; this is an already-connected pair exchanging
        // a fresh offer/answer round over the SAME pc. Reuse it -- creating
        // a new RTCPeerConnection here would discard the existing tracks
        // and ICE state for no reason.
        answerOffer(pc, callerConnectionId, sdpJson);
        return;
    }

    if (pc) {
        // Glare: we already hold a not-yet-negotiated connection to this
        // peer, meaning we must have also sent them an offer. AD-2's atomic
        // join snapshot should make this unreachable in practice -- this is
        // defense-in-depth for a bug in that guarantee, not the expected path.
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
    answerOffer(pc, callerConnectionId, sdpJson);
}

/// Shared by the initial-connection answerer path and the renegotiation
/// path -- setRemoteDescription/createAnswer/setLocalDescription/SendAnswer
/// is identical either way; only whether a NEW pc was just created differs,
/// and that's already handled by the caller.
function answerOffer(pc, callerConnectionId, sdpJson) {
    const offer = JSON.parse(sdpJson);
    pc.setRemoteDescription(offer)
        .then(function () {
            return pc.createAnswer();
        })
        .then(function (answer) {
            return pc.setLocalDescription(answer);
        })
        .then(function () {
            pc._negotiated = true;
            return hubConnection.invoke("SendAnswer", callerConnectionId, JSON.stringify(pc.localDescription));
        });
}

function handleReceiveAnswer(callerConnectionId, sdpJson) {
    const pc = peerConnections.get(callerConnectionId);
    if (!pc) {
        return;
    }

    const answer = JSON.parse(sdpJson);
    pc.setRemoteDescription(answer).then(function () {
        pc._negotiated = true;
    });
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

    // Becomes true once the FIRST offer/answer round completes (set in
    // handleReceiveAnswer and answerOffer). Distinguishes "this
    // onnegotiationneeded/ReceiveOffer is the initial handshake" (handled
    // manually by createOffererConnection/handleReceiveOffer) from "this is
    // a LATER renegotiation" (screen-share start/stop) -- see below.
    pc._negotiated = false;

    // AD-6: fan out the single shared cameraStream's tracks to this
    // connection -- never a fresh getUserMedia call per connection.
    const stream = getCameraStream();
    if (stream) {
        stream.getTracks().forEach(function (track) {
            pc.addTrack(track, stream);
        });
    }

    // Track-completeness corollary: a brand-new connection must include
    // EVERY currently-active local track, not just camera -- so a late
    // joiner sees an in-progress screen share without the sharer having to
    // stop and restart. Added second, after camera, on purpose: AD-7's
    // receiver-side disambiguation identifies the camera stream as
    // whichever distinct stream.id it observes FIRST for this peer, so
    // camera must always be added before screen here.
    const activeScreenStream = getScreenStream();
    if (activeScreenStream) {
        activeScreenStream.getTracks().forEach(function (track) {
            pc.addTrack(track, activeScreenStream);
        });
    }

    pc.onicecandidate = function (event) {
        if (event.candidate) {
            hubConnection.invoke("SendIceCandidate", peerId, JSON.stringify(event.candidate));
        }
    };

    // AD-7 receiver-side disambiguation: the FIRST distinct stream.id seen
    // for this peerId is always the camera stream (guaranteed present from
    // connection creation, above); any LATER, different stream.id is the
    // screen share. No signaling metadata needed -- just arrival order per
    // peer, tracked in remoteFirstStreamId.
    pc.ontrack = function (event) {
        var stream = event.streams[0];
        var firstId = remoteFirstStreamId.get(peerId);
        if (!firstId) {
            remoteFirstStreamId.set(peerId, stream.id);
            attachRemoteStream(peerId, stream);
        } else if (stream.id === firstId) {
            attachRemoteStream(peerId, stream);
        } else {
            attachRemoteScreenStream(peerId, stream);
        }
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

    // Fires automatically the first time tracks are added (during THIS
    // function, for the initial camera tracks) and again whenever the
    // track set changes later (screen-share addTrack/removeTrack). The
    // initial firing is ignored -- createOffererConnection/handleReceiveOffer
    // already handle that handshake manually -- so only a POST-negotiation
    // firing (screen share) reaches the renegotiation logic below. Per the
    // Renegotiation corollary, whoever changed their track set is the
    // offerer: onnegotiationneeded only fires on the side that actually
    // called addTrack/removeTrack, so this is automatically the sharer's
    // connection, never the other participants'.
    pc.onnegotiationneeded = function () {
        if (!pc._negotiated) {
            return;
        }
        pc.createOffer()
            .then(function (offer) {
                return pc.setLocalDescription(offer);
            })
            .then(function () {
                return hubConnection.invoke("SendOffer", peerId, JSON.stringify(pc.localDescription));
            });
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

/// AD-7: the second, distinct stream.id for a given peerId is always the
/// screen share (never a fresh getUserMedia/getDisplayMedia signal needed
/// server-side -- purely a receiving-side arrival-order convention).
function attachRemoteScreenStream(peerId, stream) {
    const videoEl = document.getElementById("screen-share-video");
    if (videoEl) {
        videoEl.srcObject = stream;
    } else {
        // ScreenShareLayout.razor only renders once ScreenShareStateChanged
        // arrives from the hub -- same render race as every other
        // stream-attach path in this file.
        setTimeout(function () { attachRemoteScreenStream(peerId, stream); }, 200);
    }
}

/// Story 3.1: adds the local sharer's screen track to EVERY existing
/// RTCPeerConnection via addTrack (never replaceTrack, AD-7) -- each
/// addTrack call independently fires that connection's own
/// onnegotiationneeded, so each pair renegotiates on its own, in isolation
/// (AD-8), not as one shared operation.
function addScreenTrackToAllConnections(screenStream) {
    const track = screenStream.getVideoTracks()[0];
    peerConnections.forEach(function (pc) {
        pc.addTrack(track, screenStream);
    });
}

/// Story 3.1 stop path: removeTrack (not addTrack(null) or anything
/// track-mutating) so the track is cleanly detached per connection --
/// triggers the SAME onnegotiationneeded renegotiation as starting did.
function removeScreenTrackFromAllConnections(screenStream) {
    const track = screenStream.getVideoTracks()[0];
    peerConnections.forEach(function (pc) {
        const sender = pc.getSenders().find(function (s) { return s.track === track; });
        if (sender) {
            pc.removeTrack(sender);
        }
    });
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
    remoteFirstStreamId.delete(connectionId);
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
