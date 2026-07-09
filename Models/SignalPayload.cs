namespace FlowBoardVideoCalls.Models;

/// <summary>
/// Documents the opaque JSON wire shapes CallHub relays verbatim (AD-3) --
/// CallHub never deserializes into these types; they cross as plain strings
/// on SendOffer/SendAnswer/SendIceCandidate. Mirrors the browser's native
/// RTCSessionDescriptionInit / RTCIceCandidateInit shapes (Consistency
/// Conventions), for reference only.
/// </summary>
public static class SignalPayload
{
    // SDP offer/answer (client-constructed, never parsed server-side):
    //   { "type": "offer" | "answer", "sdp": string }

    // ICE candidate (client-constructed, never parsed server-side):
    //   { "candidate": string, "sdpMid": string | null, "sdpMLineIndex": number | null }
}
