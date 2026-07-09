---
name: FlowBoard Video Calls
status: final
created: 2026-07-09
updated: 2026-07-09
sources:
  - {planning_artifacts}/prds/prd-FlowBoard-WebRTC-2026-07-09/prd.md
---

# FlowBoard Video Calls — Experience Spine

## Foundation

Desktop web only (Chrome, Edge, Firefox current stable — per PRD §2.2 Non-Users; no mobile browser support in v1). Blazor Server with JavaScript/JS-interop for all WebRTC and browser media API access. No inherited UI component library — this spine and `DESIGN.md` are the from-scratch contract. `DESIGN.md` is the visual identity reference; this file is the behavioral contract. Single-tenant per Room — a Participant belongs to exactly one Room at a time, and Rooms have no persistent identity beyond their live in-memory session (PRD Glossary: Room, Participant).

## Information Architecture

| Surface | Reached from | Purpose |
|---|---|---|
| Room Entry | App load / after Hang Up | Enter a Display Name and Room ID (typed or generated) to create or join a Room. |
| Call View | Successful submission from Room Entry | The live call: adaptive video Grid, floating Control Bar, and (conditionally) the screen-share layout when a Screen Share is active. |

Two surfaces, no navigation chrome between them beyond the join/leave transition — Hang Up always returns to Room Entry (PRD FR-18). No settings, no history, no account surface (PRD §5 Non-Goals). Single-level: no surface stacks on top of another.

→ Composition reference: `mockups/room-entry.html`, `mockups/call-view-grid.html`, `mockups/call-view-screenshare.html`. Spine wins on conflict.

## Voice and Tone

Microcopy. Brand voice and aesthetic posture live in `DESIGN.md.Brand & Style`.

| Do | Don't |
|---|---|
| "Enter a room ID or generate one" | "Let's get you connected! 🎉" |
| "Camera and microphone unavailable" | "Oops! We couldn't access your camera." |
| "Connection issue with {Display Name}" | "Uh-oh, something went wrong with this peer!" |
| "Waiting for others to join…" | "You're all alone here! Invite some friends." |
| Flat, factual, present tense | Exclamation points, apologies, or forced enthusiasm anywhere |

`[ASSUMPTION: voice rules inferred from Brand & Style's "developer-tool, not corporate-suite" posture — not explicitly specified by user.]`

## Component Patterns

Behavioral. Visual specs live in `DESIGN.md.Components`.

| Component | Use | Behavioral rules |
|---|---|---|
| Primary Button | Room Entry (Create/Join) | Disabled until both Display Name and Room ID are non-empty (PRD FR-1). Enter key in either text field submits if the form is valid (see Interaction Primitives). |
| Secondary Button | Room Entry (Generate Room ID) | On click, overwrites whatever is currently in the Room ID field with a system-generated ID (PRD FR-3). Never disabled. |
| Room ID Input | Room Entry; Room ID display | Free text on Room Entry (typed or filled by Generate Room ID). Read-only + copy-to-clipboard affordance in its Call View incarnation (see "Room ID display" row below). Same visual token (`{components.room-id-input}`) governs both. |
| Video Tile | Call View Grid | Renders the Participant's live video, or the Avatar Placeholder if camera is off/unavailable. Always carries the Name Label Chip. Carries the Mute Indicator only while that Participant is muted. Carries the Connection Issue Badge only while PRD FR-10's failure state applies to that pair. Tile size and grid position are computed purely from current Participant count (PRD FR-19) — no manual resize or drag. |
| Name Label Chip | Video Tile (every tile, always) | Displays the Participant's Display Name, unconditionally present regardless of camera/mute/connection state — the one label that's never hidden. |
| Mute Indicator | Video Tile (while muted) | Appears only while that Participant is muted; disappears immediately on unmute. Icon-only, no text, no color change (mute is a normal state — see DESIGN.md.Components). |
| Avatar Placeholder | Video Tile (camera off or FR-22 permission-denied) | Initials (1-2 letters) derived from Display Name, on a deterministic per-name color from `DESIGN.md.colors.avatar-*`. Camera-off and permission-denied render identically *except* the "media unavailable" indicator (see State Patterns) is present only for permission-denied — a viewer can tell "chose to turn camera off" from "camera never worked" at a glance. |
| Control Bar | Call View (always visible, floating) | Fixed order: Mic, Camera, Share Screen, Hang Up. Each of Mic/Camera/Share Screen is a two-state toggle (on/off) except Share Screen, which has a third disabled state (see below). No control in the bar requires a confirmation dialog to activate — Hang Up included, matching PRD FR-18 exactly (no "are you sure" step, even in a non-empty Room). |
| Control Button | Control Bar (Mic, Camera) | Single click/tap toggles on/off; icon swaps to reflect new state (e.g. mic → mic-off). No press-and-hold, no drag, no confirmation. |
| Share Screen control | Control Bar | Enabled + togglable when no Screen Share is active in the Room, or when the current Participant is the one sharing (clicking again stops it). Disabled (40% opacity, non-interactive) with a tooltip "{Other Display Name} is sharing their screen" whenever another Participant's Screen Share is active (PRD FR-12). |
| Hang Up Button | Control Bar | Single click ends the call for that Participant only (PRD FR-18) and returns them to Room Entry — immediate, no confirmation step, matching the Control Bar row above and PRD FR-18 exactly. |
| Screen-share layout | Call View, while a Screen Share is active | Grid collapses into a large main view (the shared screen) plus a Thumbnail Strip of all Participant video tiles, positioned along the right edge of the viewport. `[ASSUMPTION: right-edge placement chosen over bottom — keeps the strip visible without competing with the Control Bar, which already occupies the bottom edge; not specified by user.]` Thumbnail Strip tiles carry the same Name Label / Mute Indicator / Connection Issue Badge as full Grid tiles, at reduced size. |
| Room ID display | Call View (top of viewport, small) | The current Room's ID, rendered in `{typography.mono}`, with a copy-to-clipboard affordance so a Participant can invite others mid-call. `[ASSUMPTION: not specified by user — inferred as necessary, since UJ-1 requires sharing the Room ID with teammates and there was no other stated mechanism to retrieve it once already in a call.]` |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| Room Entry, idle | Room Entry | Empty form, Create/Join disabled. |
| Room Entry, submitting | Room Entry → Call View transition | No loading UI beyond the browser's own native camera/mic permission prompt — that prompt *is* the only visible gap between clicking Create/Join and landing in Call View. `[ASSUMPTION: no separate app-level loading state specified; the browser prompt is treated as sufficient feedback.]` |
| Waiting alone | Call View | Single, large Video Tile (own camera). `display` text overlay or adjacent: "Waiting for others to join…" `[ASSUMPTION: exact copy/placement inferred.]` Room ID display visible for sharing. |
| Connecting to a peer | Call View | New Participant's Video Tile appears in the Grid immediately at join (per PRD FR-8/FR-20) showing a brief connecting affordance (e.g. a subtle pulse or spinner on the tile) until their media stream resolves, then swaps to live video. `[ASSUMPTION: connecting micro-state not specified by user; PRD only requires the tile to "appear within a couple of seconds" — this fills the gap between tile-appears and video-resolves so the tile is never a jarring blank.]` |
| Active call | Call View | Standard Grid per PRD FR-19 breakpoints (1 / 2 / 3-4 / 5+). |
| Screen sharing active | Call View | Screen-share layout (see Component Patterns) for every Participant, including the sharer (sharer sees their own share in the main view too, so they can confirm what others see). `[ASSUMPTION: sharer's own view treatment inferred.]` |
| Camera off (voluntary) | Video Tile | Avatar Placeholder, no additional indicator beyond the placeholder itself (PRD FR-16). |
| Media permission denied | Video Tile | Avatar Placeholder + a small "media unavailable" caption/icon distinguishing it from voluntary camera-off (PRD FR-22). `[ASSUMPTION: exact caption copy is "Camera/mic unavailable" per Voice and Tone table above.]` The affected Participant's own Mic/Camera Control Bar buttons stay clickable rather than hard-disabled: clicking attempts to re-request the browser permission prompt; if the browser has permanently blocked it and no prompt reappears, the button shows an inline hint — "Enable camera/mic access in your browser settings." `[ASSUMPTION — to verify during implementation: kept deliberately simple, no blocked-state detection beyond try-then-fallback-hint.]` |
| Connection issue (per-pair) | Video Tile | Connection Issue Badge (amber) appears on the affected Participant's tile; everything else in the Grid is unaffected (PRD FR-10). Purely informational — no retry control, per PRD §9 item 2. |
| Participant leaves | Call View | That Participant's tile is removed and the Grid re-flows immediately (PRD FR-20); no "participant left" toast or notification. `[ASSUMPTION: no leave notification specified; kept silent to match the flat, non-intrusive Voice and Tone.]` |
| Total local disconnect | Call View | Out of this spine's scope. A dropped Blazor Server circuit (distinct from a single peer's FR-10 connection issue) is an infrastructure-level failure — PRD §5 explicitly makes automatic reconnection a non-goal, so this spine deliberately does not design a UI for it; whatever Blazor Server's own default disconnected-circuit UI shows is accepted as-is for v1. |

## Interaction Primitives

Mouse/click and standard form interaction only — no keyboard-shortcut layer specified or assumed (this is a small-group calling tool, not a power-user productivity surface; PRD carries no such requirement).

- **Room Entry:** `Tab` moves between Display Name → Room ID → Generate Room ID → Create/Join, in that order. `Enter` in either text field submits if the form is valid.
- **Control Bar:** each control is a single click/tap to toggle. No press-and-hold, no drag.
- **Screen share picker:** native browser picker (outside this product's UI control) — Share Screen button click triggers it; the product has no say in that dialog's appearance.
- **Tooltips:** triggered on both hover and keyboard focus on the Control Bar (e.g. disabled Share Screen) — see Accessibility Floor for the concrete requirement.

## Accessibility Floor

Behavioral. Visual contrast lives in `DESIGN.md` (all color pairs chosen to meet WCAG AA against their respective backgrounds).

- WCAG 2.1 AA target across both surfaces (PRD did not specify a WCAG level; AA assumed as the professional-grade default). `[ASSUMPTION]`
- Every icon-only Control Bar button carries an `aria-label` reflecting current state (e.g. "Mute microphone" / "Unmute microphone", not a static label) so screen readers announce the action, not just the icon.
- Camera-off and permission-denied Avatar Placeholders carry alt text distinguishing the two states for screen reader users, mirroring the sighted-user distinction in Component Patterns.
- Focus rings use `{colors.focus-ring}` at AA contrast against `{colors.background}` and `{colors.surface}`; `Tab` order on Room Entry follows visual/reading order.
- Connection Issue Badge and Mute Indicator are never color-only signals — each pairs a distinct icon with its color, so the state is legible without color perception.
- **Tooltips must be keyboard-accessible, not hover-only.** The disabled Share Screen tooltip (and any other tooltip in the product) triggers on `:focus` as well as `:hover`, so a `Tab`-only user sees "{Other Display Name} is sharing their screen" without needing a mouse. This is a concrete implementation requirement, not a nice-to-have.

## Responsive & Platform

Desktop web, but browser window width still varies (laptop windowed vs. external monitor fullscreen). The video Grid's column/row count is driven primarily by Participant count (PRD FR-19), not viewport width — but tile minimum size is enforced so the Grid never renders unusably small tiles at high Participant counts in a narrow window: below `{spacing.grid-min-tile-width}`, the Grid scrolls vertically rather than shrinking tiles further. `[ASSUMPTION: minimum-tile-width-before-scroll behavior invented to satisfy PRD FR-19's deferred "exact legibility bounds" — PRD §9 item 4 explicitly punted this to this UX pass. Both thresholds below are now named tokens in DESIGN.md (Layout & Spacing) rather than one-off numbers.]`

| Condition | Behavior |
|---|---|
| Ample width (typical laptop/desktop, ≥ `{spacing.narrow-window-breakpoint}`) | Grid renders per the 1 / 2 / 3-4 / 5+ breakpoints at comfortable tile size. |
| Narrow window (< `{spacing.narrow-window-breakpoint}`) at low Participant count (≤4) | Grid reflows to fewer columns, tiles remain full-height. |
| Narrow window at high Participant count (5+) | Grid holds `{spacing.grid-min-tile-width}` as a floor and scrolls vertically rather than shrinking tiles below legibility. |

## Key Flows

Mirrors PRD §2.3 named journeys; each flow below adds the visual/interaction detail the PRD's behavioral narrative didn't specify.

### Flow 1 — Maria starts an ad-hoc call (mirrors PRD UJ-1)

1. Maria lands on Room Entry: a centered card on the near-black background, Display Name and Room ID inputs, Generate Room ID (secondary) and Create/Join (primary, teal) buttons.
2. She types "Maria," clicks Generate Room ID — the Room ID input fills with a monospace code. She clicks Create/Join.
3. Browser's native permission prompt appears for camera/mic; she allows both.
4. She lands in Call View alone: one large Video Tile of herself, "Waiting for others to join…" visible, Room ID shown at the top in monospace with a copy affordance.
5. **Climax:** as her two teammates join, their tiles appear one at a time (brief connecting pulse, then live video), and the Grid smoothly re-flows from 1 → 2 → 3 tiles. She never has to do anything for this to happen.
6. Resolution: 3-way Grid, Control Bar floating at the bottom, all four controls active and available.

Failure: a fourth teammate mistypes the Room ID and clicks Create/Join — per PRD FR-2, this silently creates a brand-new, empty Room rather than surfacing a "room not found" error. They land in Call View alone, see no one, and have to notice the mismatch themselves (e.g. by comparing Room IDs with the group). No error state exists for this by design (PRD UJ-1 edge case) — it is expected v1 behavior, not a bug.

### Flow 2 — Alex shares his screen (mirrors PRD UJ-2)

1. Alex, already in a 4-way call, clicks Share Screen in the Control Bar.
2. Browser's native picker appears; he selects his editor window.
3. **Climax:** every Participant's Call View — including Alex's own — switches to the screen-share layout: Alex's editor fills the large main view, the other three Participants' tiles (plus Alex's own small self-view) form a Thumbnail Strip along the right edge. The other three Participants' Share Screen buttons are now visibly disabled with a tooltip naming Alex.
4. Alex clicks Stop Sharing; every Call View reflows back to the standard Grid, Share Screen re-enables for everyone.

Failure: a teammate tries to start their own Screen Share while Alex is still sharing (PRD UJ-2 edge case). Their Share Screen button is already disabled per the Share Screen control row in Component Patterns — clicking does nothing beyond the tooltip they'd already see on hover/focus; no error message, no toast, because the disabled state itself is the whole affordance.

### Flow 3 — Jordan joins with denied permissions (extends PRD UJ-3 with FR-22)

This flow uses UJ-3's protagonist and entry state but walks the FR-22 permission-denied scenario rather than UJ-3's own PRD-stated edge case (a single failed peer connection, FR-10) — that scenario is walked separately below, since both are real, distinct things that can happen to Jordan on the same join.

1. Jordan enters the Room ID and a Display Name, clicks Create/Join.
2. The browser's permission prompt appears; Jordan clicks Block (perhaps on a locked-down work laptop).
3. **Climax:** Jordan still lands in Call View — not an error screen. Their own tile (and everyone else's view of them) shows the Avatar Placeholder with the "media unavailable" caption, distinguishable from a teammate who simply turned their camera off. Jordan can still see and hear every other Participant, and their microphone-off state means the other three see Jordan's Mute Indicator too (audio is unavailable, which the UI treats the same as muted).
4. Resolution: Jordan participates fully via listening/watching. Jordan's Mic and Camera controls remain clickable (not hard-disabled): clicking either attempts to re-request the browser's permission prompt. `[ASSUMPTION — to verify during implementation: if the browser has permanently blocked the permission (no prompt reappears on request), the control instead shows a short inline hint — "Enable camera/mic access in your browser settings." Kept deliberately simple for v1: the product does not attempt to detect *which* blocked-state it's in, it just tries the re-request and falls back to the hint if nothing happens.]`

Failure (PRD UJ-3's actual edge case, FR-10): Jordan's permissions are granted fine, but their connection to just one of the three existing Participants fails to establish (e.g. a restrictive network on Jordan's end). Jordan still connects successfully to the other two — the failed pair shows the Connection Issue Badge (amber) on the affected tile for both Jordan and that one Participant, purely informational, no retry control (State Patterns → "Connection issue (per-pair)"). The rest of the call, including Jordan's other two connections, is entirely unaffected.
