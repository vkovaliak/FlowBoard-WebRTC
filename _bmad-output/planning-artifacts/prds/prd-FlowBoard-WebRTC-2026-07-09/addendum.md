# Addendum: FlowBoard Video Calls

Technical-how content volunteered during PRD discovery, preserved here for the downstream architecture document. None of this is a product requirement in itself — it's the mechanism the PRD's capabilities (see `prd.md` §4) will be built on.

## Technology Stack

- **Blazor Server, .NET 10** — single project, no separate backend/API service, no separate frontend SPA.
- **WebRTC** for peer-to-peer audio/video/screen transport, implemented via JavaScript + Blazor JS Interop, since Blazor has no native WebRTC API.
- **SignalR**, via a dedicated `CallHub` in the same project, used purely for Signaling (see Glossary: Signaling) — room membership tracking and relaying offers/answers/ICE candidates. SignalR never carries media.
- **STUN only**: Google's public STUN server (`stun:stun.l.google.com:19302`) for ICE candidate/NAT-traversal assistance. No TURN relay (see `prd.md` §5 Non-Goals).
- The app is **not connected to any other backend** — no database, no external API dependencies.

## Architecture: Mesh Topology

- Full mesh: every Participant holds a direct Peer Connection to every other Participant in the Room (N-1 connections per Participant in an N-person Room).
- Deliberately no SFU/media server — chosen to keep the app self-contained and to specifically practice/demonstrate P2P WebRTC mechanics (peer connection lifecycle, ICE negotiation, Signaling orchestration) rather than server-side media routing.
- The user explicitly does not want a coded participant cap (see `prd.md` FR-9) even though Mesh has a well-known practical scaling ceiling (~4-5 participants is the informally targeted comfortable range, per typical consumer bandwidth/CPU). This is documented as an inherent technology characteristic in `prd.md` §7, not enforced anywhere in code.

## CallHub Responsibilities (Signaling)

The `CallHub` (SignalR hub) must support multiple peers per room, at minimum:
- Track which connection IDs belong to which Room ID.
- Relay SDP offers/answers between specific peer pairs (not broadcast Room-wide) as new Participants join, so mesh connections can be established pairwise.
- Relay ICE candidates between specific peer pairs.
- Notify existing Room members when a Participant joins or leaves, so peer connections can be dynamically opened/torn down (realizes `prd.md` FR-8).

Exact hub method signatures, group management approach, and reconnection-on-hub-disconnect behavior are architecture-level decisions, not specified here.

## Deployment

- **Primary v1 target: localhost.** The demo is expected to run locally.
- **Azure deployment is a nice-to-have**, not a blocker for v1 completion. The architecture should avoid decisions that would preclude a later Azure deployment (e.g., avoid hardcoding `localhost` assumptions where trivially avoidable), but no deployment pipeline, secrets management, or hosting config work is in scope for MVP.
- HTTPS is required by browser media APIs (`getUserMedia`) for any non-`localhost` origin — relevant only if/when Azure deployment is pursued (see `prd.md` §7 NFRs).

## Scaffolding

- No existing Blazor project exists yet. Project scaffolding itself is planned as the first implementation story under the BMad Method epics/stories workflow, not something this PRD needs to specify further.
