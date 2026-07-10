// FlowBoard Video Calls -- local media capture (Architecture AD-6, AD-1).
// Owns the single cameraStream for this Call View session. C# never receives
// or holds a MediaStream reference -- only the granted/denied boolean crosses
// via the OnMediaPermissionResult [JSInvokable] callback.
//
// getUserMedia() is called exactly once automatically at session start
// (captureLocalMedia, invoked from initializeCall). retryMediaPermission()
// is a second, explicitly user-initiated call after a prior denial -- an
// architecture-sanctioned exception (named in the Signaling & Interop
// Contract), not a violation of AD-6's "exactly once" rule, which exists to
// prevent redundant per-peer-connection capture, not to forbid a retry path.

let cameraStream = null;

function attachLocalVideoElement() {
    var videoEl = document.getElementById("local-video");
    if (videoEl && cameraStream) {
        videoEl.srcObject = cameraStream;
    } else if (cameraStream) {
        // The <video> element only renders once C# sets HasMedia=true in
        // response to OnMediaPermissionResult -- which this function's
        // first call always precedes. Retry until Blazor has rendered it,
        // the same pattern webrtc-mesh.js's attachRemoteStream already uses
        // for the equivalent remote-tile race.
        setTimeout(attachLocalVideoElement, 100);
    }
}

async function captureLocalMedia(dotNetRef) {
    try {
        cameraStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        attachLocalVideoElement();
        await dotNetRef.invokeMethodAsync("OnMediaPermissionResult", true);
    } catch (err) {
        cameraStream = null;
        await dotNetRef.invokeMethodAsync("OnMediaPermissionResult", false);
    }
}

function retryMediaPermission(dotNetRef) {
    return captureLocalMedia(dotNetRef);
}

/// Exposes the shared cameraStream to webrtc-mesh.js (AD-6: one stream,
/// fanned out to every RTCPeerConnection) without that module needing to
/// know how or when it was captured.
function getCameraStream() {
    return cameraStream;
}

/// Mute/camera-off are implemented as track.enabled flips on the ONE shared
/// cameraStream (AD-6) -- never per-connection, never a fresh getUserMedia
/// call. interop.js's toggleMic/toggleCamera call these, then invoke the
/// hub method; track manipulation stays here since this module owns the
/// stream.
function isMicMuted() {
    if (!cameraStream) {
        return false;
    }
    var tracks = cameraStream.getAudioTracks();
    return tracks.length > 0 && !tracks[0].enabled;
}

function setMicMuted(muted) {
    if (!cameraStream) {
        return;
    }
    cameraStream.getAudioTracks().forEach(function (t) { t.enabled = !muted; });
}

function isCameraOn() {
    if (!cameraStream) {
        return false;
    }
    var tracks = cameraStream.getVideoTracks();
    return tracks.length > 0 && tracks[0].enabled;
}

function setCameraOn(on) {
    if (!cameraStream) {
        return;
    }
    cameraStream.getVideoTracks().forEach(function (t) { t.enabled = on; });
}

/// hangUp() (interop.js) calls this to release the camera/mic hardware.
/// Distinct from setMicMuted/setCameraOn (which just flip track.enabled,
/// keeping the stream alive for a future toggle back on) -- this actually
/// stops the tracks, since the session is ending.
function stopLocalMedia() {
    if (cameraStream) {
        cameraStream.getTracks().forEach(function (t) { t.stop(); });
        cameraStream = null;
    }
}

// --- Screen sharing (AD-7). screenStream is a SEPARATE MediaStream object
// from cameraStream -- never the same object, never mixed tracks -- so the
// receiving side's AD-7 disambiguation (first stream seen = camera, second
// distinct stream = screen) has two genuinely distinct stream identities to
// tell apart. ---

let screenStream = null;

async function captureScreenShare() {
    screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true });
    return screenStream;
}

function getScreenStream() {
    return screenStream;
}

/// interop.js's stopScreenShare() calls this AFTER removing the track from
/// every RTCPeerConnection -- stopping the track first would leave a
/// dangling sender on each pc.
function stopScreenShareCapture() {
    if (screenStream) {
        screenStream.getTracks().forEach(function (t) { t.stop(); });
        screenStream = null;
    }
}

