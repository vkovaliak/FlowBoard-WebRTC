---
name: FlowBoard Video Calls — UX -> Architecture Reconciliation
type: reconciliation-report
created: 2026-07-09
inputs:
  - '{planning_artifacts}/ux-designs/ux-FlowBoard-WebRTC-2026-07-09/EXPERIENCE.md'
  - '{planning_artifacts}/ux-designs/ux-FlowBoard-WebRTC-2026-07-09/DESIGN.md'
  - '{planning_artifacts}/architecture/architecture-FlowBoard-WebRTC-2026-07-09/ARCHITECTURE-SPINE.md'
---

# UX Spine -> Architecture Spine Reconciliation

## Method

Checked every EXPERIENCE.md State Pattern, the Screen-share Component Pattern, the Media-permission-denied Component Pattern, and DESIGN.md's Components section against the architecture's stated JS<->C# callback contract:

> "JS tells C# about state changes worth rendering (**participant joined/left, mute changed, connection issue**)." (Design Paradigm section)

This is the only place the architecture enumerates *which* callback events exist. Everything else (Consistency Conventions' `On`-prefixed naming rule, the Structural Seed, the three sequence diagrams, and the Deferred section) was checked for whether it names or implies any *additional* callback beyond these three.

## Finding 1 (primary gap): No callback for "screen share started/stopped, and by whom"

**Affects:** EXPERIENCE.md State Patterns → "Screen sharing active"; Component Patterns → "Screen-share layout" and "Share Screen control"; DESIGN.md → Control Button disabled state.

The architecture's only named JS->C# callback types are `participant joined/left`, `mute changed`, `connection issue`. There is no fourth named callback for "a screen share started" / "a screen share stopped" / "who is sharing."

This matters because, unlike video rendering (which JS owns end-to-end via `<video>` elements it manages directly), the **layout decision** — whether `CallView.razor` renders the standard Grid or swaps to `ScreenShareLayout.razor` — is a Blazor-side branch (per the Structural Seed, `ScreenShareLayout.razor` is a distinct Razor component, and the Screen-share sequence diagram says "UI switches to screen-share-priority layout"). That branch has to be driven by C# state, which means JS must tell C# "sharing started, sharer = X" and "sharing stopped" as a discrete event. This event is never named anywhere in the spine (not in the Design Paradigm callback list, not in Consistency Conventions' naming examples, not in the Structural Seed, not in Deferred).

Same gap re-surfaces for the DESIGN.md/EXPERIENCE.md "Share Screen control" disabled state: the tooltip copy is "{Other Display Name} is sharing their screen" — rendering that tooltip requires C# to know *which* participant is sharing, not just a boolean. No callback in the contract carries a sharer identity.

**Verdict:** plausible in principle (the interop boundary is generically bidirectional and AD-1 permits "primitive state... bool flags" to cross), but as specified, this is a real hole — the single most concrete missing data path in the spine. Screen sharing active/inactive and sharer identity have no named route from JS to C#.

## Finding 2: AD-7 delivers the sending-side guarantee, but receiver-side track disambiguation is unaddressed

**Affects:** EXPERIENCE.md Component Patterns → "Screen-share layout" (camera tiles stay visible during share) vs. AD-7.

AD-7 (additive screen track via `addTrack()`, never `replaceTrack()`) is the correct mechanism for keeping the camera track alive and flowing during a share — this is necessary and it is what EXPERIENCE.md needs (thumbnail strip showing live camera tiles while the main view shows the screen). On the sending side, AD-7 is sufficient.

What AD-7 does not address: on the **receiving** peer's `RTCPeerConnection`, two video tracks now arrive (camera + screen) via `ontrack`. The spine does not specify how the receiving JS determines which of the two incoming video tracks is the camera and which is the screen, in order to route one to the main view and the other to the thumbnail strip. WebRTC's `ontrack` doesn't inherently label tracks semantically; this needs some convention (track/transceiver ordering, `track.label`, a small metadata signal, etc.) that the spine is silent on. This is not a fundamental blocker — AD-7 makes the correct data available — but the routing logic that turns "two video tracks" into "this one is main view, this one is thumbnail" has no specified mechanism, and is worth flagging before implementation, not after.

## Finding 3: "Media permission denied" retry path — outbound action is fine, inbound result callback is unnamed

**Affects:** EXPERIENCE.md State Patterns → "Media permission denied"; Component Patterns → same row (Mic/Camera stay clickable, click retries `getUserMedia`).

- **Outbound (C# -> JS, "user clicked Mic/Camera, retry permission"):** architecturally sound by extension of the existing pattern — "C# tells JS about user actions (mute, hang up, share)" is a non-exhaustive illustrative list, and a "retry permission" action fits the same `IJSRuntime.InvokeVoidAsync` -> `interop.js` -> `media.js` (`getUserMedia`) shape already established for mute/hang-up/share. No new architecture decision is strictly required here.
- **Inbound (JS -> C#, "retry succeeded" vs. "still blocked, show the inline hint"):** this result has to reach C# for the UI to swap the Avatar Placeholder back to live video, or to render the "Enable camera/mic access in your browser settings" hint — and there is no named callback for it. The Deferred section's closing line is the only textual support: *"the interop contract guarantees such failures surface to C# via a callback (AD-1), but the exact user-facing treatment beyond PRD FR-22's permission-denied case is left to story-level detail."* That sentence asserts a callback exists for the **initial** denial, but doesn't name one for the **retry outcome**, and doesn't cover the "permanently blocked, no prompt reappears" branch EXPERIENCE.md explicitly calls out. Weak, not-zero coverage — flagged as under-specified rather than absent.

## Finding 4 (minor): "Camera off" is folded into "mute changed" without being named

**Affects:** EXPERIENCE.md State Patterns → "Camera off (voluntary)"; DESIGN.md → Video Tile / Avatar Placeholder / Mute Indicator.

The callback list says `mute changed`, singular, with no separate `camera changed` entry, even though Mic and Camera are two independent Control Bar toggles governing two independent tracks (AD-6 covers both audio and video via the same `track.enabled = false` mechanism, so the underlying data plane is fine). Whether the single "mute changed" callback is meant to carry both audio-mute and video-mute (camera-off) state, or whether camera-off needs its own callback to drive the Avatar Placeholder swap, is not disambiguated. Low severity — the fix is naming, not new plumbing — but worth calling out since Video Tile's Avatar-Placeholder-vs-live-video branch is a Razor-side decision (per Component Patterns: "Renders the Participant's live video, or the Avatar Placeholder if camera is off/unavailable"), so it does need *some* callback, and it's not clear which one.

## Finding 5: Connection Issue Badge — no gap

**Affects:** DESIGN.md Components → Connection Issue Badge; EXPERIENCE.md State Patterns → "Connection issue (per-pair)."

This is the one state/component with unambiguous, directly-named support: `connection issue` is one of the three explicitly named callback types, and AD-8 (per-pair connection lifecycle fully independent) gives it a clean, isolated data path — one Map entry's ICE-failure state maps to exactly one tile's badge, with no risk of cross-contamination into other pairs' state. No gap.

## Findings not flagged as gaps

- **Waiting alone** — no callback needed; it's the absence of any `ParticipantJoined` events. Fine.
- **Connecting to a peer** (tile appears, pulse, then swaps to live video) — `ParticipantJoined` covers tile-appears; the pulse->live-video swap itself is plausibly pure JS/DOM work (media plane "owns... all `<video>` rendering" per Design Paradigm), not requiring a C# round-trip at all. Fine, though the spine never explicitly states the stream-to-DOM-element wiring mechanism (e.g., how JS finds *which* tile's video element belongs to which participant) — noted for implementation, not scored as a UX-capability gap.
- **Active call / Grid re-flow** — driven by `ParticipantJoined`/`participant left`, consistent with AD-4/AD-8. Fine.
- **Participant leaves** — directly named (`participant joined/left`). Fine.
- **Total local disconnect** — EXPERIENCE.md explicitly puts this out of scope and defers to Blazor Server's own default circuit-disconnected UI; the architecture makes no competing claim. Consistent, no gap.

## Summary Table

| EXPERIENCE.md / DESIGN.md requirement | Architecture support | Verdict |
|---|---|---|
| Waiting alone | Implicit (no join events) | OK |
| Connecting to a peer | `ParticipantJoined` + presumed JS-only DOM swap | OK (mechanism unstated but plausible) |
| Active call / grid re-flow | `ParticipantJoined`/left, AD-4/AD-8 | OK |
| Screen sharing active (layout swap) | **No named callback** | **GAP** |
| Share Screen tooltip (sharer identity) | **No named callback carrying identity** | **GAP** |
| Camera tiles stay visible during share (AD-7) | AD-7 covers send side; receive-side track disambiguation unspecified | **PARTIAL GAP** |
| Camera off (voluntary) | "mute changed" — ambiguous whether it covers camera track too | **MINOR GAP (naming)** |
| Media permission denied (initial) | Deferred section asserts a callback exists | OK, thinly specified |
| Media permission denied (retry outcome) | No named callback for retry result / permanently-blocked branch | **GAP** |
| Connection issue (per-pair) | Named callback + AD-8 isolation | OK |
| Participant leaves | Named callback | OK |
| Total local disconnect | Explicitly out of scope both sides | OK (consistent) |

## Bottom line

The architecture's callback contract is capable of realizing most of the UX spine, and gets the hardest media-plane problem (screen share without dropping camera video) structurally right via AD-7. The load-bearing gap is that **screen-share state (active/inactive + sharer identity)** has no named path from JS to C#, despite being required to drive a Razor-level layout branch (`CallView` vs `ScreenShareLayout`) and a tooltip that names the sharer. Two secondary gaps — the permission-denied retry-result callback, and the camera-off vs. mic-mute naming ambiguity — are both fixable by naming an additional callback event rather than by any structural rework; they don't undermine the paradigm, just the completeness of its one enumerated callback list.
