---
title: FlowBoard Video Calls
status: final
created: 2026-07-09
updated: 2026-07-09
---

# PRD: FlowBoard Video Calls

## 0. Document Purpose

This PRD defines the product requirements for FlowBoard Video Calls, a browser-based, peer-to-peer group video calling application. It is written for the downstream workflow owners who will turn this into an architecture, epics/stories, and code: primarily the solo builder (kovaliak) acting as architect and developer, using the BMad Method pipeline. The document is Glossary-anchored (§3) — every capitalized domain term used elsewhere in this PRD is defined once there, with no synonyms introduced downstream. Features are grouped with Functional Requirements (FRs) nested underneath and numbered globally (§4). Technical implementation decisions (Blazor Server structure, SignalR hub design, WebRTC/STUN mechanics) are captured separately in `addendum.md` in this workspace and are treated as constraints an architecture document will formalize, not requirements themselves.

## 1. Vision

FlowBoard Video Calls is a self-contained group video calling application built to demonstrate professional-grade engineering of real-time, peer-to-peer communication. A participant opens the app, types or generates a room identifier, enters a display name, and is in a live video call within seconds — no account, no install, no signup. Anyone else who enters the same room ID joins the same call. Every participant sees and hears every other participant, any participant can share their screen for the group, and the video layout reflows automatically as people come and go. Underneath, every media stream flows directly between participants' browsers over WebRTC — there is no media server relaying video, and the app's own backend (a lightweight SignalR hub) is only ever used to help peers find each other, never to touch the media itself.

What this project is *for* is as important as what it *does*: it exists to prove out real-time communication engineering — mesh topology peer connection management, signaling orchestration, and JS-interop-mediated browser media APIs — to a professional standard, in a codebase clean and well-structured enough to be read and reviewed line by line.

## 2. Target User

### 2.1 Jobs To Be Done

- **As the builder**, I need a working, non-trivial WebRTC application to demonstrate real-time systems engineering skill as a portfolio piece, and to genuinely practice and master WebRTC and real-time communication engineering — this is the primary job this project serves.
- **As a call participant**, when I want to talk face-to-face with a small group without scheduling overhead, installs, or accounts, I want to share a room ID and be in a call within seconds.
- **As a call participant**, when I'm explaining something visual (a document, a design, a bug), I want to show my screen to everyone else in the room without leaving the call.
- **As a call participant**, when I join a room, I want to immediately understand who else is present and control my own audio/video/screen presence without hunting for controls.

### 2.2 Non-Users (v1)

- Mobile browser users (desktop-only in v1).
- Users on restrictive/symmetric NAT networks that require a TURN relay to connect (no TURN server in v1 — see §5).
- Users who need chat, recording, or persistent call history — this is a live-call-only tool.
- Enterprise users needing authentication, access control, or audit trails — there are no accounts in v1; anyone with the room ID can join.

### 2.3 Key User Journeys

- **UJ-1. Maria starts an ad-hoc call with two teammates.**
  - **Persona + context:** Maria wants to talk through a design decision with two teammates right now, without a scheduling tool.
  - **Entry state:** Unauthenticated, first visit to the app, on desktop Chrome.
  - **Path:** Maria opens the app, types her display name "Maria," clicks "Generate Room ID," gets a short code, and shares it over Slack. She clicks "Create / Join." Her camera and mic permission prompts appear; she allows both and lands in the room alone, seeing her own video tile. Her two teammates paste the code into the same field, enter their names, and join.
  - **Climax:** As each teammate joins, their video tile appears in Maria's grid within a second or two, and the grid reflows from 1 to 2 to 3 tiles automatically. She hears them as soon as they're connected.
  - **Resolution:** All three are now in a live 3-way call, each seeing and hearing the other two, each free to mute, toggle camera, or share their screen.
  - **Edge case:** If a fourth teammate tries to join with a typo'd room ID, they instead create a brand-new (empty) room and wonder where everyone is — this is expected v1 behavior, not an error state (realizes FR-2).

- **UJ-2. Alex shares his screen to walk through a bug.**
  - **Persona + context:** Alex is already in a live call with three others and needs to show a stack trace in his editor.
  - **Entry state:** Already connected in an active room, camera on, unmuted.
  - **Path:** Alex clicks "Share Screen," the browser's native screen/window picker appears, he selects his editor window.
  - **Climax:** All other participants' views shift to a large main view of Alex's shared screen with the participant video tiles reduced to a thumbnail strip, so the shared content is legible (realizes FR-21 [ASSUMPTION]).
  - **Resolution:** Alex finishes explaining, clicks "Stop Sharing," and everyone's layout reflows back to the standard participant grid.
  - **Edge case:** If a teammate tries to start their own screen share while Alex is still sharing, their "Share Screen" button is disabled with a tooltip explaining only one share is active at a time (realizes FR-12 [ASSUMPTION]).

- **UJ-3. Jordan joins a call already in progress.**
  - **Persona + context:** Jordan is running a few minutes late to a call that Maria, a teammate, and Alex are already on.
  - **Entry state:** Unauthenticated, has the room ID from a calendar invite link.
  - **Path:** Jordan enters the room ID and a display name, clicks "Create / Join."
  - **Climax:** Without any of the three existing participants doing anything, new peer connections are established between Jordan and each of them; Jordan's video tile appears in their grids and theirs appear in his, all without interrupting the existing connections between Maria, the teammate, and Alex.
  - **Resolution:** The call is now a 4-way mesh, grid adapted to 4 tiles for everyone (realizes FR-8, FR-19).
  - **Edge case:** If Jordan's connection to just one of the three participants fails to establish (e.g., a restrictive network on Jordan's end), Jordan still connects successfully to the other two; the failed pair shows a subtle "connection issue" indicator on both ends instead of blocking the whole call (realizes FR-10).

## 3. Glossary

- **Room** — A named, ephemeral space identified by a Room ID where Participants exchange media. Exists only in server memory for as long as at least one Participant is present; ceases to exist the instant it becomes empty. Has no owner/host role — all Participants are equal.
- **Room ID** — A string identifier for a Room, either typed freely by a user or produced by the random ID generator. Not validated against any reserved list; entering a nonexistent Room ID creates that Room.
- **Participant** — A single browser session connected to a Room, represented by a Display Name and a video tile.
- **Display Name** — Free-text name a Participant enters before joining a Room; shown under their video tile to all other Participants. Not unique, not persisted, not tied to any account.
- **Mesh (Mesh Topology)** — The peer connection topology in which every Participant in a Room holds a direct Peer Connection to every other Participant (full graph). No media passes through a server.
- **Peer Connection** — A single direct WebRTC connection between exactly two Participants, carrying that pair's audio, video, and (when active) Screen Share streams.
- **Signaling** — The out-of-band exchange of connection setup information (offers, answers, ICE candidates) between Participants, relayed through the CallHub. Signaling never carries media.
- **CallHub** — The application's SignalR hub, responsible for Room membership tracking and relaying Signaling messages between Participants in the same Room.
- **STUN** — A public server (Google's `stun:stun.l.google.com:19302`) that helps a Participant's browser discover its own public-facing network address for use in ICE candidate negotiation. Used in v1; no TURN relay is used.
- **ICE Candidate** — A network address/route a Participant's browser proposes during Peer Connection setup; pairs of Participants negotiate the best working route using these.
- **Screen Share** — A Participant's shared screen, window, or tab, sent as an additional video stream over their existing Peer Connections to every other Participant. Only one Participant's Screen Share can be active in a Room at a time.
- **Call Controls** — The set of actions a Participant can take on their own presence in a call: mute/unmute microphone, camera on/off, start/stop Screen Share, hang up.
- **Grid (Video Grid)** — The on-screen layout arranging all current Participants' video tiles, which reflows automatically as Participants join, leave, or as Screen Share starts/stops.

## 4. Features

### 4.1 Room Creation & Joining

**Description:** The entry point to the app. A user provides a Display Name and a Room ID (typed or generated) to enter a Room; the system transparently creates the Room if it doesn't yet exist. No accounts, no pre-registration. Realizes UJ-1, UJ-3.

**Functional Requirements:**

#### FR-1: Enter a call with a name and room ID

A user can enter a Display Name and a Room ID and submit to enter a Room. Realizes UJ-1.

**Consequences (testable):**
- Submission is blocked (with inline validation) if Display Name or Room ID is empty.
- On successful submission, the user is taken to the call view for that Room.

#### FR-2: Implicit room creation

The system creates a new, empty Room automatically the first time any Room ID is submitted, and routes subsequent submissions of that same Room ID into the same Room. Realizes UJ-1.

**Consequences (testable):**
- Two users submitting the same Room ID within the Room's lifetime land in the same call together.
- A user submitting a Room ID no one has used yet becomes the sole Participant of a newly created Room.

#### FR-3: Random Room ID generation

A user can click a "Generate Room ID" control to populate the Room ID field with a system-generated random ID, without needing to invent one. Realizes UJ-1.

**Consequences (testable):**
- Generated IDs are at least 6 alphanumeric characters, short enough to read aloud or paste into a chat message, while remaining random enough that two independent clicks are exceedingly unlikely to collide.

#### FR-4: Ephemeral room lifecycle

A Room exists only in server memory and only while at least one Participant is connected to it; the moment the last Participant leaves (via hang up, closed tab, or dropped connection), the Room and all its state cease to exist.

**Consequences (testable):**
- Re-submitting a Room ID after all Participants have left creates a brand-new, empty Room rather than resuming any prior state.
- No Room data survives a server restart.

**Out of Scope:**
- Room passwords, private/unlisted rooms, or any access control beyond knowing the Room ID.

### 4.2 Multi-Participant Video & Audio (Mesh Calling)

**Description:** The core calling experience. Every Participant in a Room streams their camera and microphone directly to every other Participant over a full Mesh of Peer Connections, orchestrated via Signaling through the CallHub. Realizes UJ-1, UJ-3.

**Functional Requirements:**

#### FR-5: Full mesh peer connection establishment

The system establishes a direct Peer Connection between every pair of Participants currently in a Room. Realizes UJ-1, UJ-3.

**Consequences (testable):**
- In a Room of N Participants, each Participant holds exactly N-1 active Peer Connections.
- Peer Connection setup uses the CallHub for Signaling and the configured STUN server for ICE candidate gathering.

#### FR-6: Local media capture and publish

Each Participant's browser captures their camera and microphone (via JS interop to browser media APIs) and publishes those tracks on every one of their Peer Connections.

**Consequences (testable):**
- A camera/microphone permission prompt is shown on first join; declining is handled without crashing the app — the Participant still joins with camera/mic unavailable rather than being blocked (see FR-22).

#### FR-7: Remote media rendering

Each Participant renders the incoming audio and video stream from every other connected Participant in their Room, updating live.

**Consequences (testable):**
- A Participant hears and sees every other currently-connected Participant with no manual action required.

#### FR-8: Dynamic join/leave without disruption

When a Participant joins or leaves a Room, the system establishes or tears down only the Peer Connections involving that Participant, leaving all other active Peer Connections in the Room undisturbed. Realizes UJ-3.

**Consequences (testable):**
- Existing Participants' audio/video to each other does not glitch, drop, or renegotiate when an unrelated Participant joins or leaves.

#### FR-9: No participant cap

The system imposes no maximum on the number of Participants in a Room, in code or configuration. Any number of Participants may join a Room and the Mesh scales to connect all of them.

**Consequences (testable):**
- No code path rejects a join due to Room size.
- Documented separately (§7 NFRs) that Mesh has an inherent, non-enforced practical performance ceiling — this is a property of the technology, not a product limit.

#### FR-10: Independent per-pair failure handling

If ICE/STUN negotiation fails between one specific pair of Participants, that pair's Peer Connection fails in isolation; all other Peer Connections in the Room continue functioning normally. Realizes UJ-3.

**Consequences (testable):**
- A failed pair shows a subtle, non-blocking "connection issue" indicator on the affected tile(s) for both Participants in that pair.
- The indicator is purely informational in v1 — no retry action is offered. `[NOTE FOR PM: manual/automatic retry is a reasonable v2 addition, deferred here to keep v1 scope tight.]`
- No exception or failure in one Peer Connection prevents, interrupts, or crashes any other Peer Connection or the Room as a whole.

**Feature-specific NFRs:**
- No TURN relay is used in v1; pairs that cannot establish a direct or STUN-assisted path are expected to fail per FR-10 rather than connect (see §5 Non-Goals).

### 4.3 Screen Sharing

**Description:** Any Participant can share their screen, a specific window, or a browser tab with everyone else in the Room, using the browser's native screen-capture picker. Only one Screen Share is active in a Room at a time. Realizes UJ-2.

**Functional Requirements:**

#### FR-11: Start screen share

A Participant can start sharing their screen, window, or tab; the shared content is sent to every other Participant in the Room over their existing Peer Connections. Realizes UJ-2.

**Consequences (testable):**
- All other Participants begin seeing the shared content within a couple of seconds of the share starting, without needing to take any action.

#### FR-12: Single active screen share `[ASSUMPTION]`

While one Participant's Screen Share is active, the "Share Screen" control is disabled for all other Participants until the active share ends. Realizes UJ-2.

**Consequences (testable):**
- A second Participant attempting to share while another's share is active cannot start one; the control is visibly disabled with an explanatory tooltip rather than silently failing.

`[ASSUMPTION — confirmed: Chosen "block" over "replace" as the simpler, lower-risk implementation for concurrent share requests; user reviewed and approved this choice.]`

#### FR-13: Stop screen share

The sharing Participant can stop their Screen Share at any time, after which all other Participants' views return to the standard participant Grid. Realizes UJ-2.

**Consequences (testable):**
- Stopping a share re-enables the "Share Screen" control for all other Participants.

#### FR-14: Screen share ends on disconnect `[ASSUMPTION]`

If the sharing Participant disconnects (hang up, closed tab, or dropped connection) while their Screen Share is active, the share ends automatically for all other Participants, equivalent to them clicking Stop.

**Consequences (testable):**
- No other Participant is left rendering a stale/frozen shared-screen frame after the sharer has left the Room.

`[ASSUMPTION — confirmed: inferred as the only sane behavior given FR-4's ephemeral Room model; user reviewed and approved.]`

**Out of Scope:**
- Audio capture of the shared screen/tab (system audio sharing).
- Annotating or drawing over the shared screen.

### 4.4 Call Controls

**Description:** Each Participant controls their own presence in the call — audio, video, screen, and departure — through a consistent, always-visible control bar. Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-15: Mute/unmute microphone

A Participant can mute and unmute their own microphone; all other Participants see a mute indicator on that Participant's tile while muted.

**Consequences (testable):**
- Toggling mute stops/resumes the Participant's outgoing audio track without dropping or renegotiating their Peer Connections.

#### FR-16: Camera on/off

A Participant can turn their camera off and back on; while off, other Participants see a placeholder (e.g., initials/avatar) on that Participant's tile instead of a frozen or blank video frame. `[ASSUMPTION — confirmed: placeholder treatment inferred, no visual design input was provided; exact visual design deferred to the UX workflow per §9.]`

**Consequences (testable):**
- Turning the camera off stops the outgoing video track (not just visually hides it) so no stale frame is ever shown to others.

#### FR-22: Join despite denied camera/microphone permission
*(Numbered 22, appended during Finalize; placed here rather than at the end since it shares FR-16's placeholder mechanism.)*

If a Participant denies the camera/microphone permission prompt, they still join the Room rather than being blocked; they are shown with the same placeholder treatment as camera-off (above), with a clear indicator that their media is unavailable, and can still see and hear all other Participants. Realizes UJ-1.

**Consequences (testable):**
- Denying permission never prevents a Participant from entering a Room.
- Other Participants see this Participant's tile using the FR-16 placeholder, distinguishable from a voluntary camera-off state via the "media unavailable" indicator.

#### FR-17: Screen share controls

A Participant can start and stop their Screen Share from the same control bar. Realizes §4.3 (FR-11 through FR-14).

#### FR-18: Hang up

A Participant can hang up, which closes all of that Participant's Peer Connections and returns them to the room-entry screen (§4.1); all other Participants immediately see that Participant's tile removed from their Grid.

**Consequences (testable):**
- Hanging up triggers Room-emptiness evaluation per FR-4 if it was the last remaining Participant.
- Remaining Participants' connections to each other are unaffected (realizes FR-8).

### 4.5 Adaptive Grid Layout

**Description:** The video Grid automatically arranges all current Participants' tiles to make efficient use of screen space, and reflows live as the Room's composition changes. Realizes UJ-1, UJ-3.

**Functional Requirements:**

#### FR-19: Layout adapts to participant count

The Grid's arrangement (rows/columns) is computed from the current number of Participants in the Room, without requiring a page reload. Realizes UJ-3.

**Consequences (testable):**
- Grid arrangement visibly changes at natural breakpoints as Participant count grows (e.g., 1, 2, 3-4, 5+); exact legibility bounds (minimum tile size, aspect ratio) are left to the UX workflow handoff (§9 item 4).

#### FR-20: Live reflow on join/leave

The Grid updates in real time as Participants join or leave, without manual refresh. Realizes UJ-3, FR-8.

**Consequences (testable):**
- A tile appears within a couple of seconds of a new Participant's media connecting, and is removed immediately when a Participant leaves.

#### FR-21: Screen-share-priority layout `[ASSUMPTION]`

While a Screen Share is active, the Grid switches to a large main view of the shared screen with participant video tiles reduced to a thumbnail strip, rather than treating the Screen Share as just another equally-sized Grid tile. Realizes UJ-2.

**Consequences (testable):**
- The shared screen occupies the dominant portion of the viewport whenever a Screen Share is active, at any Participant count.

`[ASSUMPTION — confirmed: a uniform grid would make shared screen content illegible once several participants are present; prioritized layout inferred as necessary for the feature to be usable, user reviewed and approved.]`

## 5. Non-Goals (Explicit)

FlowBoard Video Calls will **not**, in v1:
- Provide text chat or messaging within a call.
- Record calls or any portion of them.
- Support user accounts, authentication, or any form of access control beyond knowledge of the Room ID.
- Provide virtual backgrounds, background blur, or any video filters.
- Persist call history, room lists, or any data beyond a Room's live in-memory state.
- Include a TURN relay server — Participants on networks that block direct/STUN-assisted connections will experience a failed Peer Connection with that specific peer (FR-10), not a working call. `[NOTE FOR PM: revisit if the demo network turns out to need it.]`
- Support mobile browsers — v1 targets desktop Chrome, Edge, and Firefox only.
- Automatically reconnect a Participant after a network drop or page refresh — rejoining is a manual action in v1.
- Enforce any participant limit — this is a deliberate non-limit, not an oversight (see FR-9).

## 6. MVP Scope

### 6.1 In Scope
- Room creation/joining by arbitrary or generated Room ID, with a Display Name (§4.1).
- Unlimited-participant Mesh video/audio calling with per-pair failure isolation (§4.2).
- Single-active-participant screen sharing (§4.3).
- Mute/unmute, camera on/off, screen share start/stop, hang up (§4.4).
- Adaptive, live-reflowing Grid layout with screen-share-priority view (§4.5).
- Scaffolding the Blazor Server project itself (first implementation story, per addendum.md).

### 6.2 Out of Scope for MVP
- Everything listed in §5 Non-Goals.
- Azure (or any) deployment, including secrets/config management for it — localhost is the v1 target. **Resolved:** address deployment config only if actually pursued post-MVP; the only present-day obligation is not hardcoding `localhost` assumptions that would needlessly block it later (see addendum.md). `[NOTE FOR PM: nice-to-have if time allows.]`

## 7. Cross-Cutting Non-Functional Requirements

- **Security:** Browser media capture APIs (`getUserMedia`) require a secure context — the app must run under `https://` for any non-`localhost` origin. HTTPS is not required for the primary localhost demo target but is a hard constraint the moment the app is reached from any other origin (e.g., Azure deployment).
- **Performance (Mesh scaling characteristic):** Because every Participant streams to every other Participant directly (Mesh), each Participant's upload bandwidth and CPU cost scale linearly with Room size (N-1 outgoing streams). This is an inherent property of Mesh topology, not a defect. Smooth performance is expected on typical consumer hardware/bandwidth up to roughly 4-5 Participants; beyond that, degradation (dropped frames, increased CPU/fan noise, bandwidth contention) is expected and acceptable — the system must not artificially block it (FR-9), but the PRD does not claim large-Room performance as a goal.
- **Reliability (fault isolation):** A failure in any single Peer Connection (FR-10) or any single Participant's media pipeline must never crash, freeze, or degrade any other Participant's connections in the Room.
- **Browser compatibility:** Current stable versions of desktop Chrome, Edge, and Firefox. No mobile browser support or testing required in v1.
- **Code quality:** Given this project's purpose as a portfolio/demo piece (§1, §2.1), a clean separation of concerns — Blazor UI components, the JS interop layer, the CallHub/Signaling layer, and WebRTC peer-connection management — is treated as a first-class, reviewable requirement, not a nice-to-have (see SM-3).

## 8. Success Metrics

**Primary**
- **SM-1**: A live demo call with 4 or more Participants runs start-to-finish with working camera, audio, and screen sharing, and all four Call Controls functioning correctly, with no crashes. Validates FR-5 through FR-18.
- **SM-2**: The Grid layout visibly and correctly adapts as Participants join and leave during a single live demo, including correct screen-share-priority reflow. Validates FR-19, FR-20, FR-21.

**Secondary**
- **SM-3**: The codebase demonstrates professional-grade, clean, well-structured code — clear separation of concerns (Blazor components / JS interop / CallHub / peer-connection management) — that is easy to read and review.
- **SM-4**: The join path successfully connects participants well past the Mesh's comfortable range (6 or more in a single Room) — media quality may visibly degrade, but no code or configuration path ever rejects a join due to Room size. Validates FR-9.

**Counter-metrics (do not optimize)**
- **SM-C1**: Do not build toward larger-than-mesh-appropriate participant counts (e.g., do not introduce an SFU/media server, do not over-engineer bandwidth optimization) in pursuit of "no participant cap" (FR-9). SM-4 proves the join path stays open, not that large Rooms must perform well — the absence of a cap is a scope simplification, not a scale target. Counterbalances FR-9 / SM-4.

## 9. Open Questions

All Discovery-time open questions were resolved during Finalize; none remain blocking. Resolutions:

1. **Resolved (FR-22):** Denied camera/mic permission does not block joining — Participant joins with the FR-16 placeholder plus a "media unavailable" indicator, and can still see/hear others.
2. **Resolved (FR-10):** The "connection issue" indicator is purely informational in v1, no retry action. `[NOTE FOR PM: retry is a reasonable v2 addition.]`
3. **Resolved (§6.2):** No Azure deployment config/secrets work in v1 scope; only obligation now is not hardcoding `localhost` assumptions that would block a later deployment.
4. **Deferred to UX workflow:** Exact visual design (colors, spacing, iconography, grid/control-bar look) is intentionally left open here. **Owner:** user, via the `bmad-ux` workflow (Sally) as the next step after this PRD. **Revisit condition:** before or during epics/stories creation, so UX output can inform story acceptance criteria.

## 10. Assumptions Index

All four assumptions below were reviewed and confirmed by the user during Finalize (2026-07-09):

- §2.3 UJ-2 / §4.3 FR-12: Concurrent screen-share requests are **blocked** (button disabled for others) rather than the new share **replacing** the active one — chosen as the simpler implementation. **Confirmed.**
- §4.3 FR-14: An active Screen Share ends automatically for everyone if the sharing Participant disconnects — inferred from the ephemeral Room model (FR-4). **Confirmed.**
- §4.4 FR-16: Camera-off state (and FR-22 denied-permission state) shows an initials/avatar placeholder rather than a blank or frozen frame — inferred; exact visual treatment deferred to the UX workflow. **Confirmed.**
- §4.5 FR-21: An active Screen Share switches the Grid to a "main view + thumbnail strip" layout rather than treating it as a uniform Grid tile — inferred as necessary for usability at higher Participant counts. **Confirmed.**
