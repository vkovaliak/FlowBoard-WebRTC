---
stepsCompleted: [step-01-validate-prerequisites, requirements-confirmed, step-02-design-epics, step-03-create-stories, step-04-final-validation]
inputDocuments:
  - '{planning_artifacts}/prds/prd-FlowBoard-WebRTC-2026-07-09/prd.md'
  - '{planning_artifacts}/prds/prd-FlowBoard-WebRTC-2026-07-09/addendum.md'
  - '{planning_artifacts}/ux-designs/ux-FlowBoard-WebRTC-2026-07-09/DESIGN.md'
  - '{planning_artifacts}/ux-designs/ux-FlowBoard-WebRTC-2026-07-09/EXPERIENCE.md'
  - '{planning_artifacts}/architecture/architecture-FlowBoard-WebRTC-2026-07-09/ARCHITECTURE-SPINE.md'
---

# FlowBoard Video Calls - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for FlowBoard Video Calls, decomposing the requirements from the PRD, UX Design spine pair, and Architecture spine into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1: A user can enter a Display Name and a Room ID and submit to enter a Room.
FR-2: The system creates a new, empty Room automatically the first time any Room ID is submitted, and routes subsequent submissions of that same Room ID into the same Room.
FR-3: A user can click a "Generate Room ID" control to populate the Room ID field with a system-generated random ID.
FR-4: A Room exists only in server memory and only while at least one Participant is connected; ceases to exist the instant it becomes empty.
FR-5: The system establishes a direct Peer Connection between every pair of Participants currently in a Room (full mesh).
FR-6: Each Participant's browser captures their camera and microphone and publishes those tracks on every one of their Peer Connections.
FR-7: Each Participant renders the incoming audio and video stream from every other connected Participant, updating live.
FR-8: When a Participant joins or leaves a Room, the system establishes or tears down only the Peer Connections involving that Participant, leaving all other active Peer Connections undisturbed.
FR-9: The system imposes no maximum on the number of Participants in a Room, in code or configuration.
FR-10: If ICE/STUN negotiation fails between one specific pair of Participants, that pair's Peer Connection fails in isolation; all other Peer Connections continue functioning normally.
FR-11: A Participant can start sharing their screen, window, or tab; the shared content is sent to every other Participant.
FR-12: While one Participant's Screen Share is active, the Share Screen control is disabled for all other Participants until the active share ends.
FR-13: The sharing Participant can stop their Screen Share at any time, after which all other Participants' views return to the standard Grid.
FR-14: If the sharing Participant disconnects while their Screen Share is active, the share ends automatically for all other Participants.
FR-15: A Participant can mute and unmute their own microphone; other Participants see a mute indicator on that Participant's tile while muted.
FR-16: A Participant can turn their camera off and back on; while off, other Participants see a placeholder instead of a frozen or blank video frame.
FR-17: A Participant can start and stop their Screen Share from the same control bar.
FR-18: A Participant can hang up, which closes all of that Participant's Peer Connections and returns them to the room-entry screen; other Participants see that Participant's tile removed.
FR-19: The Grid's arrangement (rows/columns) is computed from the current number of Participants, without requiring a page reload.
FR-20: The Grid updates in real time as Participants join or leave.
FR-21: While a Screen Share is active, the Grid switches to a large main view of the shared screen with participant video tiles reduced to a thumbnail strip.
FR-22: If a Participant denies the camera/microphone permission prompt, they still join the Room rather than being blocked; shown with the camera-off placeholder plus a "media unavailable" indicator, and can still see/hear others.

### NonFunctional Requirements

NFR1 (Security): Browser media capture APIs require a secure context — the app must run under `https://` for any non-`localhost` origin. Not required for the localhost v1 target; becomes a hard constraint the moment the app is reached from any other origin.
NFR2 (Performance / Mesh scaling): Each Participant's upload bandwidth/CPU cost scales linearly with Room size (N-1 outgoing streams) — an inherent Mesh characteristic. Smooth performance expected up to ~4-5 Participants; graceful degradation beyond that is acceptable, never artificially blocked.
NFR3 (Reliability / fault isolation): A failure in any single Peer Connection or Participant's media pipeline must never crash, freeze, or degrade any other Participant's connections.
NFR4 (Browser compatibility): Current stable desktop Chrome, Edge, Firefox only. No mobile browser support or testing required in v1.
NFR5 (Code quality): Clean separation of concerns (Blazor UI components / JS interop layer / CallHub-Signaling layer / WebRTC peer-connection management) is a first-class, reviewable requirement.

### Additional Requirements

- No starter/scaffold template: Architecture specifies a from-scratch ASP.NET Core Blazor Server (.NET 10) project via the standard SDK template — no third-party starter. This is Epic 1 Story 1, per explicit user instruction and the PRD addendum.
- Signaling-Plane / Media-Plane Separation paradigm (AD-1): C#/Blazor Server never touches a `RTCPeerConnection` or `MediaStream`; JS never holds product state. All interop crosses via the named Signaling & Interop Contract only.
- Deterministic offerer rule (AD-2): the newly joining participant always offers; existing participants always answer. Includes a lexicographic-connectionId tie-break as defense-in-depth.
- CallHub is an opaque relay (AD-3): `SendOffer`/`SendAnswer`/`SendIceCandidate` forward payloads verbatim to a specific target connection, never broadcast, never inspected server-side.
- RoomRegistry (AD-4): single `ConcurrentDictionary<connectionId, ParticipantRecord>` (no separate reverse-lookup dict), with atomic compound operations `AddAndSnapshot`, `RemoveAndGetRemaining`, `SetSharingState` — each a single coarse lock around the whole compound step. `OnDisconnectedAsync` is the sole teardown path. A `pagehide` JS hook proactively closes the hub connection on refresh/navigate to minimize the ghost-tile window. Disconnect is terminal — no reconnection support (matches PRD's no-auto-reconnect non-goal).
- No participant cap anywhere in code/config (AD-5), matching FR-9.
- One local `cameraStream` per session, fanned out via `addTrack` to every Peer Connection; mute/camera-off toggle `track.enabled`, never per-connection (AD-6).
- Screen share is additive via a distinct `screenStream` object added with `addTrack` (never `replaceTrack`); receiver disambiguates camera vs. screen by first-vs-second distinct stream id per connection (AD-7).
- Track-completeness corollary: every new Peer Connection is created with ALL currently-active local tracks (camera + active screen share, if any) — not just camera — so a participant joining mid-share still sees the share.
- Renegotiation corollary: for any mid-call track change, the participant whose track set changed is always the renegotiation offerer.
- Per-pair connection lifecycle isolation (AD-8): tracked in `Map<connectionId, RTCPeerConnection>`; every removal path pairs `pc.close()` with Map-entry deletion atomically — never one without the other.
- Full Signaling & Interop Contract is fixed in the architecture spine: CallHub server methods (`JoinRoom`, `SendOffer`, `SendAnswer`, `SendIceCandidate`, `SetMuteState`, `SetCameraState`, `SetSharingState`), CallHub client callbacks (`ReceiveOffer`, `ReceiveAnswer`, `ReceiveIceCandidate`, `ParticipantJoined`, `ParticipantLeft`, `ParticipantMuteChanged`, `ParticipantCameraChanged`, `ScreenShareStateChanged`), JS interop functions (`initializeCall`, `toggleMic`, `toggleCamera`, `startScreenShare`, `stopScreenShare`, `retryMediaPermission`, `hangUp`), and C# `[JSInvokable]` callbacks (`OnMediaPermissionResult`, `OnConnectionIssue`). Stories must use these exact names/signatures, not invent new ones.
- STUN config lives in `appsettings.json` under `WebRtc`, bound via `IOptions<WebRtcOptions>`; passed to JS as a parameter, never hardcoded.
- No authentication/authorization layer (PRD Non-Goal) — any connectionId reaching a hub method is trusted for its own room's operations.
- Explicitly out of scope for this epic/story breakdown (Architecture Deferred, matches PRD Non-Goals): TURN server, multi-instance/horizontal scaling of RoomRegistry, automated test strategy, Azure-specific deployment/secrets/CI-CD, a specific logging/observability framework beyond `ILogger`.
- Project structure fixed by the Structural Seed: `Components/Pages` (RoomEntry, CallView), `Components/Call` (VideoTile, ControlBar, ScreenShareLayout), `Hubs/CallHub.cs`, `Services/RoomRegistry.cs`, `Models/` (ParticipantDto, SignalPayload), `Configuration/WebRtcOptions.cs`, `wwwroot/js/` (interop.js, webrtc-mesh.js, media.js).

### UX Design Requirements

UX-DR1: Implement the full DESIGN.md token system in CSS (or a `:root` custom-property sheet) — dark theme (`background #1A1A1A`, `surface #242424`, `surface-raised #2E2E2E`), teal primary accent (`#2DD4BF`) reserved exclusively for the primary action and active/on toggle states, destructive red (`#EF4444`) reserved exclusively for Hang Up, warning amber (`#F5A623`) reserved exclusively for the Connection Issue Badge, the 6-hue avatar palette with paired `-foreground` tokens for AA-contrast initials text, system sans-serif type ramp (display/body/label/caption) plus a dedicated monospace role for the Room ID, `rounded` scale (sm/md/lg/xl/full), and the 4-based `spacing` scale including the `grid-min-tile-width` (180px) and `narrow-window-breakpoint` (1200px) layout tokens.
UX-DR2: Build the Room Entry surface per `mockups/room-entry.html`: centered card (~400px max-width), Display Name input, Room ID input (monospace), Generate Room ID secondary button, Create/Join primary button disabled until both fields are non-empty, Enter-key submission.
UX-DR3: Build the adaptive video Grid (Call View) per `mockups/call-view-grid.html`, implementing the FR-19/FR-20 breakpoints (1 / 2 / 3-4 / 5+) and the narrow-window min-tile-width/scroll behavior from EXPERIENCE.md's Responsive & Platform table.
UX-DR4: Build the Video Tile component: live video or Avatar Placeholder, always-present Name Label Chip (bottom-left overlay), Mute Indicator (top-right overlay, icon-only, shown only while muted), Connection Issue Badge (amber, top-right overlay, offset from Mute Indicator when both present).
UX-DR5: Build the Avatar Placeholder: initials (1-2 letters) on the deterministic per-name avatar-hue background with its paired foreground token; identical rendering for voluntary camera-off and FR-22 permission-denied, distinguished only by an additional "media unavailable" caption/indicator on the permission-denied case.
UX-DR6: Build the floating pill Control Bar (fixed order: Mic, Camera, Share Screen, Hang Up) per DESIGN.md.Components — Control Buttons toggle on/off with icon swap and recessed (not alarmed) off-state styling; Hang Up is always a filled destructive-red circle; Share Screen shows a disabled state (40% opacity) with a tooltip naming the active sharer, keyboard-focus-triggered as well as hover-triggered (concrete accessibility requirement, not optional).
UX-DR7: Build the screen-share-priority layout per `mockups/call-view-screenshare.html`: large main view of the shared screen + right-edge Thumbnail Strip of all Participant tiles (including the sharer's own camera view), driven by the `ScreenShareStateChanged` callback.
UX-DR8: Build the in-call Room ID display (top of viewport, monospace, copy-to-clipboard affordance) so a Participant can re-share the Room ID after already joining.
UX-DR9: Implement the Accessibility Floor: WCAG 2.1 AA target; `aria-label` on every icon-only Control Bar button reflecting current state; alt text distinguishing camera-off vs. permission-denied Avatar Placeholders; visible focus rings at AA contrast; Tab order matching visual/reading order on Room Entry; Connection Issue Badge and Mute Indicator never color-only (icon + color together).
UX-DR10: Implement all named State Patterns from EXPERIENCE.md: Room Entry idle/submitting, Waiting alone, Connecting to a peer (brief connecting affordance before live video), Active call, Screen sharing active, Camera off, Media permission denied (with retry-then-fallback-hint per the architecture's `retryMediaPermission`/`OnMediaPermissionResult` contract), Connection issue per-pair (purely informational, no retry control), Participant leaves (silent, no toast), Total local disconnect (explicitly out of scope — defer to Blazor Server's default circuit-disconnected UI).
UX-DR11: Apply the Voice and Tone microcopy rules verbatim where they apply (e.g. "Waiting for others to join…", "Camera and microphone unavailable", "Connection issue with {Display Name}") — flat, factual, present tense; no exclamation points or forced enthusiasm anywhere in the product.

### FR Coverage Map

| Requirement | Epic |
| --- | --- |
| FR-1, FR-2, FR-3, FR-4 | Epic 1 — Room Entry & Session Lifecycle |
| FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, FR-15, FR-16, FR-18, FR-19, FR-20, FR-22 | Epic 2 — Mesh Video & Audio Calling |
| FR-11, FR-12, FR-13, FR-14, FR-17, FR-21 | Epic 3 — Screen Sharing |
| NFR1 (HTTPS), NFR4 (Browser compatibility), NFR5 (Code quality / plane separation) | Epic 1 (foundational, enforced throughout) |
| NFR2, NFR3 (Mesh performance, fault isolation) | Epic 2 |
| UX-DR1 (tokens), UX-DR2 (Room Entry), UX-DR8 (in-call Room ID display), UX-DR9 (accessibility floor, foundational), UX-DR11 (voice/tone, applied throughout) | Epic 1 |
| UX-DR3, UX-DR4, UX-DR5, UX-DR10 | Epic 2 |
| UX-DR6 (Control Bar — Share Screen specifics), UX-DR7 (screen-share layout) | Epic 3 |

## Epic List

### Epic 1: Room Entry & Session Lifecycle
Users can open the app, create or join a room by ID (typed or generated), and see themselves waiting in that room; the room correctly comes into existence and cleans itself up.
**FRs covered:** FR-1, FR-2, FR-3, FR-4

### Epic 2: Mesh Video & Audio Calling
Users in the same room see and hear every other participant live, with working mute/camera-off/hang-up controls and a grid that adapts as people join or leave — including graceful handling of denied permissions and isolated per-pair connection failures.
**FRs covered:** FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, FR-15, FR-16, FR-18, FR-19, FR-20, FR-22

### Epic 3: Screen Sharing
Any participant can share their screen with the group; everyone sees a clear main-view-plus-thumbnails layout, and sharing cleanly starts, stops, and recovers from a disconnect.
**FRs covered:** FR-11, FR-12, FR-13, FR-14, FR-17, FR-21

## Epic 1: Room Entry & Session Lifecycle

Users can open the app, create or join a room by ID (typed or generated), and see themselves waiting in that room; the room correctly comes into existence and cleans itself up.

### Story 1.1: Scaffold the Blazor Server Project

As a developer,
I want a properly structured, buildable Blazor Server (.NET 10) project with the architecture's folder layout in place,
So that every subsequent feature has a consistent, correct foundation.

**Acceptance Criteria:**

**Given** no project exists yet
**When** the scaffold is created
**Then** a new ASP.NET Core Blazor Server project targeting .NET 10 exists at the repository root, builds with `dotnet build` with zero errors, and runs with `dotnet run` serving a default page

**Given** the architecture's Structural Seed
**When** the scaffold is complete
**Then** the following folders exist: `Components/Pages`, `Components/Call`, `Hubs/`, `Services/`, `Models/`, `Configuration/`, `wwwroot/js/`

**Given** the project must not depend on a third-party starter template
**When** reviewing the scaffold
**Then** the `.csproj` shows only the standard ASP.NET Core Blazor Server SDK template, no extra starter-template package references

**And** `appsettings.json` exists with an empty `WebRtc` section placeholder, ready for Story 1.3

### Story 1.2: Apply the FlowBoard Design Token Foundation

As a developer,
I want the DESIGN.md token system available as CSS custom properties across the app,
So that every future component can consume consistent colors, typography, spacing, and radii without redefining them.

**Acceptance Criteria:**

**Given** DESIGN.md's frontmatter tokens
**When** the global stylesheet is added
**Then** all color, typography, rounded, and spacing tokens are defined as CSS custom properties in a single site-wide stylesheet

**Given** the dark-mode-first brand
**When** any page loads
**Then** `--color-background`/`--color-foreground` are applied to the document body by default

**And** the monospace typography role is available as a utility class, ready for the Room ID input/display

### Story 1.3: Build the RoomRegistry and CallHub Signaling Backbone

As a developer,
I want a working, atomic in-memory RoomRegistry and a CallHub clients can connect to and join a room through,
So that Room Entry has a real backend to submit to.

**Acceptance Criteria:**

**Given** the architecture's AD-4
**When** RoomRegistry is implemented
**Then** it exposes exactly `AddAndSnapshot`, `RemoveAndGetRemaining`, and `SetSharingState` as its only mutation entry points, backed by a single `ConcurrentDictionary<string, ParticipantRecord>`, each method wrapped in one lock around its whole compound operation

**Given** a client connects to `/callHub` and calls `JoinRoom(roomId, displayName)`
**When** the call completes
**Then** the caller is added to RoomRegistry, receives the existing-participants snapshot, and every other participant in that room receives `ParticipantJoined`

**Given** a client's SignalR connection closes (graceful or timeout)
**When** `OnDisconnectedAsync` fires
**Then** RoomRegistry removes that connectionId atomically and every remaining participant receives `ParticipantLeft`

**Given** the architecture's AD-5
**When** `JoinRoom` is called on a room that already has participants
**Then** no size/capacity check ever rejects the join, regardless of room size

**And** `WebRtcOptions` is bound via `IOptions<WebRtcOptions>` from `appsettings.json`'s `WebRtc` section, and `SendOffer`/`SendAnswer`/`SendIceCandidate` exist with the exact Signaling & Interop Contract signatures, forwarding verbatim to `Clients.Client(targetConnectionId)` (functional WebRTC wiring lands in Epic 2 — this story proves the relay and config plumbing work)

### Story 1.4: Room Entry Screen — Create or Join a Room

As a user,
I want to enter a display name and a room ID (typed or generated) and submit it,
So that I land in a room, whether it's new or already exists.

**Acceptance Criteria:**

**Given** the Room Entry screen matches `mockups/room-entry.html`
**When** a user loads the app
**Then** they see a centered card with Display Name input, monospace Room ID input, "Generate Room ID" button, and "Create / Join" button

**Given** both fields are empty
**When** the page loads
**Then** Create/Join is disabled (FR-1)

**Given** a user clicks "Generate Room ID"
**When** the click completes
**Then** the Room ID field fills with a random ID at least 6 alphanumeric characters (FR-3)

**Given** a user submits a Room ID never used before
**When** `JoinRoom` completes
**Then** a new Room is implicitly created with the user as its sole participant (FR-2)

**Given** a user submits an already-active Room ID
**When** `JoinRoom` completes
**Then** the user joins that existing Room alongside its current participants (FR-2)

**Given** a user is the sole participant
**When** the screen renders
**Then** it shows "Waiting for others to join…" with the Room ID visible in monospace and a copy-to-clipboard affordance (UX-DR8)

**And** when all Participants leave a Room, it ceases to exist server-side (FR-4) — verified by a subsequent join to the same Room ID producing a fresh, empty Room, not resumed state

**And** `Tab` order moves Display Name → Room ID → Generate Room ID → Create/Join, and every input/button shows a visible focus ring at AA contrast against the dark background (UX-DR9)

## Epic 2: Mesh Video & Audio Calling

Users in the same room see and hear every other participant live, with working mute/camera-off/hang-up controls and a grid that adapts as people join or leave — including graceful handling of denied permissions and isolated per-pair connection failures.

### Story 2.1: Local Media Capture, Self Preview, and Permission Handling

As a user,
I want the app to access my camera and microphone and show my own live preview,
So that I can confirm my camera/mic work before anyone else joins — even if I deny access.

**Acceptance Criteria:**

**Given** a user has just joined a Room (Epic 1)
**When** `initializeCall` runs
**Then** `getUserMedia()` is called exactly once, producing one `cameraStream`, and the user's own Video Tile renders that live camera/mic feed (AD-6, FR-6)

**Given** the user allows the permission prompt
**When** the stream resolves
**Then** their own tile shows live video with no placeholder

**Given** the user denies the permission prompt
**When** `OnMediaPermissionResult(false)` fires
**Then** the user still lands in the Room (not blocked), their tile shows the Avatar Placeholder with a "media unavailable" indicator distinct from voluntary camera-off (FR-22, UX-DR5)

**And** clicking the (still-enabled) Mic/Camera control calls `retryMediaPermission()`; on success `OnMediaPermissionResult(true)` swaps the tile back to live video, on repeated failure an inline "Enable camera/mic access in your browser settings" hint appears

**And** the Avatar Placeholder carries `alt` text that distinguishes voluntary camera-off from permission-denied for screen reader users, matching the sighted-user distinction (UX-DR9)

### Story 2.2: Two-Participant Mesh Call Establishment

As a user,
I want to see and hear the other participant live when I join a room they're already in (or they join mine),
So that we're in a working call together.

**Acceptance Criteria:**

**Given** Participant B is already in a Room and Participant A joins
**When** `JoinRoom` returns B in the existing-participants snapshot
**Then** A creates an `RTCPeerConnection` to B and is the SDP offerer (AD-2); B only ever answers

**Given** A's offer is sent via `SendOffer`
**When** it reaches B via `ReceiveOffer`
**Then** B creates its own `RTCPeerConnection`, answers via `SendAnswer`, and ICE candidates trickle both directions via `SendIceCandidate`/`ReceiveIceCandidate`

**Given** the connection reaches "connected"
**When** both sides render
**Then** each sees the other's live video/audio in a Video Tile with the correct Name Label Chip (FR-5, FR-7)

**And** in the vanishingly rare case both sides believe they're the offerer (AD-2's documented defense-in-depth scenario), the participant with the lexicographically greater connectionId keeps its offer and the other discards its outbound offer and answers instead

### Story 2.3: N-Way Mesh with Dynamic Join/Leave and Adaptive Grid

As a user,
I want every participant who joins or leaves to seamlessly appear or disappear for everyone, with the layout adapting automatically,
So that a group call of any size just works without manual refresh.

**Acceptance Criteria:**

**Given** a Room already has 2+ connected participants
**When** a new participant joins
**Then** they become the offerer to every existing participant (AD-2) and every existing pairwise connection is left completely untouched (AD-8) — no glitch, drop, or renegotiation between unrelated pairs (FR-8)

**Given** no participant cap exists anywhere in code or config (AD-5, FR-9)
**When** any number of participants join the same Room
**Then** the join always succeeds — verified with 6+ simultaneous participants in one Room (validates PRD SM-4)

**Given** the Grid renders per participant count
**When** the count crosses 1 / 2 / 3-4 / 5+
**Then** the layout adapts per DESIGN.md/EXPERIENCE.md breakpoints (FR-19), and reflows live within a couple of seconds of any join or leave (FR-20, UX-DR3)

**And** below the narrow-window breakpoint at 5+ participants, the Grid holds `grid-min-tile-width` as a floor and scrolls rather than shrinking tiles further

### Story 2.4: Per-Pair Connection Failure Isolation

As a user,
I want one participant's bad connection to me to never affect my connections to everyone else,
So that a single network hiccup doesn't break the whole call for me.

**Acceptance Criteria:**

**Given** ICE/STUN negotiation fails for one specific pair
**When** that `RTCPeerConnection`'s state degrades
**Then** only that pair's tile(s) show the Connection Issue Badge (amber, purely informational, no retry control) — every other connection in the Room continues functioning normally (FR-10, NFR3)

**Given** AD-8's mandatory close-then-delete pairing
**When** a connection is removed for any reason (failure, participant left)
**Then** `pc.close()` and the Map-entry deletion happen together — never one without the other, verified by no lingering active media/bandwidth after removal

**And** this isolation holds structurally (each `RTCPeerConnection` in its own Map entry, per AD-8) — not by any special-case error handling

**And** the Connection Issue Badge is never a color-only signal — it pairs a distinct icon with the amber color, so the state is legible without color perception (UX-DR9)

### Story 2.5: Call Controls — Mute, Camera Toggle, and Hang Up

As a user,
I want to mute my mic, turn my camera off, and hang up, with everyone else seeing my state change immediately,
So that I control my own presence in the call.

**Acceptance Criteria:**

**Given** a user clicks Mute
**When** `toggleMic()` sets `track.enabled = false` on the shared `cameraStream` and calls `SetMuteState`
**Then** every other participant's view of that tile shows the Mute Indicator via `ParticipantMuteChanged` (FR-15)

**Given** a user clicks Camera off
**When** `toggleCamera()` and `SetCameraState` fire
**Then** every other participant's view swaps that tile to the Avatar Placeholder via `ParticipantCameraChanged` — not a frozen frame (FR-16, UX-DR5)

**Given** a user clicks Hang Up
**When** `hangUp()` runs
**Then** all of that user's `RTCPeerConnection`s close (AD-8 close-then-delete), the JS SignalR connection stops (triggering `OnDisconnectedAsync`/`RemoveAndGetRemaining` from Epic 1), the user returns to Room Entry, and every remaining participant's Grid removes that tile immediately (FR-18)

**And** all three Control Bar buttons show clear active/inactive visual states per DESIGN.md.Components (UX-DR6)

**And** every icon-only Control Bar button carries an `aria-label` reflecting its current state (e.g. "Mute microphone" / "Unmute microphone", not a static label), and the Mute Indicator pairs its icon with a label/state rather than relying on the mute icon's shape alone (UX-DR9)

## Epic 3: Screen Sharing

Any participant can share their screen with the group; everyone sees a clear main-view-plus-thumbnails layout, and sharing cleanly starts, stops, and recovers from a disconnect.

### Story 3.1: Start and Stop Screen Sharing

As a user,
I want to share my screen and have everyone else see it in a clear layout, with the option disabled for others while I'm sharing,
So that I can show something to the whole group without confusion about who's presenting.

**Acceptance Criteria:**

**Given** a user clicks Share Screen
**When** `startScreenShare()` runs
**Then** `getDisplayMedia()` produces a `screenStream` distinct from `cameraStream`, its track is added via `addTrack(track, screenStream)` — never `replaceTrack()` — to every existing `RTCPeerConnection`, and `SetSharingState(true)` broadcasts `ScreenShareStateChanged(sharerId)` (FR-11, AD-7)

**Given** the track addition triggers `onnegotiationneeded`
**When** renegotiation runs
**Then** the sharer is the offerer on every affected connection (Renegotiation corollary) and every other participant's `cameraStream` tracks remain untouched and still flowing (AD-6)

**Given** `ScreenShareStateChanged` names an active sharer
**When** every other participant's Call View updates
**Then** it switches to the screen-share-priority layout — large main view of the share plus a right-edge Thumbnail Strip of all camera tiles including the sharer's own (FR-21, UX-DR7) — and their Share Screen control becomes disabled with a tooltip naming the sharer (FR-12, UX-DR6)

**Given** the sharer clicks Stop Sharing
**When** `stopScreenShare()` runs
**Then** the screen track is removed via the same renegotiation pattern, `SetSharingState(false)` broadcasts `ScreenShareStateChanged(null)`, every Call View reflows back to the standard Grid, and Share Screen re-enables for everyone (FR-13, FR-17)

**And** the disabled Share Screen tooltip triggers on keyboard focus as well as mouse hover, so a `Tab`-only user sees "{Sharer Display Name} is sharing their screen" without a mouse (UX-DR9, concrete requirement per EXPERIENCE.md's Accessibility Floor)

### Story 3.2: Screen Share Visibility for Late Joiners

As a user joining a call already in progress,
I want to immediately see an active screen share if one is happening,
So that I don't miss what's being presented just because I joined a moment late.

**Acceptance Criteria:**

**Given** Participant F is actively sharing their screen when Participant D joins the Room
**When** D creates its `RTCPeerConnection` to F (D is the offerer per AD-2)
**Then** that connection is created with ALL of F's currently-active local tracks — camera *and* the active screen track — not just camera (Track-completeness corollary)

**Given** D's connection to F includes both streams from creation
**When** D's `ontrack` handler receives them
**Then** D correctly routes the first distinct stream to the camera tile and the second distinct stream to the screen-share main view, without needing F to stop and restart sharing (AD-7 receiver-side disambiguation)

**And** D's Call View renders the screen-share-priority layout immediately on connecting to F, matching every other participant's view

### Story 3.3: Screen Share Ends Automatically on Sharer Disconnect

As a user,
I want an active screen share to end cleanly for everyone if the sharer suddenly disconnects,
So that no one is left staring at a frozen or orphaned share.

**Acceptance Criteria:**

**Given** the active sharer's connection is torn down (hang-up, closed tab, or connection loss)
**When** `RemoveAndGetRemaining` processes that disconnect
**Then** `CallHub` includes the prior sharing state in that removal and broadcasts `ScreenShareStateChanged(null)` to every remaining participant (FR-14, AD-7)

**Given** that broadcast arrives
**When** every remaining Call View updates
**Then** it reflows back to the standard Grid exactly as if the sharer had clicked Stop Sharing — same code path, not a special case (FR-13/FR-14 consistency)

**And** this is verified with the disconnecting participant being both the sharer and mid-renegotiation with a separate, simultaneous late joiner (Story 3.2's scenario), confirming no orphaned or half-negotiated state is left behind
