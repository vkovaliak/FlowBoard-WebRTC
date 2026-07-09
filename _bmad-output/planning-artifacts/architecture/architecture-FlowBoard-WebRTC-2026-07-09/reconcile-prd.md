# PRD ↔ Architecture Spine Reconciliation

**Inputs compared:**
- `prd.md` (FR-1–FR-22, §7 NFRs)
- `addendum.md` (technical constraints)
- `ARCHITECTURE-SPINE.md` (AD-1–AD-8, Stack, Structural Seed, Capability Map, Deferred)

**Scope checked:** 22 Functional Requirements, 5 Cross-Cutting NFRs (§7), ~12 addendum technical-constraint statements.

---

## Headline finding (the quiet one)

**PRD §7 Security NFR — HTTPS/secure-context requirement — has no home anywhere in the spine.**

The PRD states plainly: *"the app must run under `https://` for any non-`localhost` origin... a hard constraint the moment the app is reached from any other origin (e.g., Azure deployment)."* `addendum.md`'s Deployment section repeats it almost verbatim and explicitly cross-references it back to §7.

The spine's `Deferred` section has a bullet for exactly the triggering condition — *"Azure-specific deployment: hosting config, secrets management, CI/CD... this spine's choices (config via `IOptions`, no hardcoded `localhost`) deliberately don't block it"* — and even echoes the *other* half of the same addendum sentence (don't hardcode `localhost`), but never mentions HTTPS/secure-context at all. No AD, no Stack entry, no Deferred note. A story-writer implementing Azure deployment later has nothing in this document telling them `getUserMedia`/`getDisplayMedia` will silently fail over plain HTTP. This is a constraint that was present in two source documents, echoed by its "sibling" (localhost non-hardcoding), and still dropped. Recommend adding a one-line Deferred bullet (or an explicit non-scope note) parallel to the Azure-deployment bullet.

---

## Gaps found

### 1. FR-3 (Random Room ID generation) — no architectural home
No AD, Structural Seed entry, Capability Map row, or Deferred note mentions ID generation. `RoomEntry.razor` is documented only as "room ID + display name entry" (i.e., the input surface), not as the owner of the generate-a-random-ID behavior. It's unclear from the spine alone whether this is a trivial client-side (JS `crypto.randomUUID`) or server-side (Blazor C# method) concern — a genuine, if small, invention gap for whoever implements FR-3.

### 2. FR-14 (Screen share ends automatically on sharer disconnect) — coverage is inferred, not stated
AD-7 and the Renegotiation corollary define how a screen share starts/stops via **explicit** offer/answer renegotiation when the sharer clicks Stop. Nothing in the spine addresses the **disconnect** path: when the sharer's browser/tab closes mid-share, does `ScreenShareLayout.razor`'s "active sharer" state get explicitly cleared, or does it rely solely on the peer connections closing (AD-8) to implicitly unwind the UI? The mechanism is plausible by combination of AD-4 (`OnDisconnectedAsync` teardown) + AD-8 (independent per-pair lifecycle), but no AD or Deferred note states the layout-revert behavior explicitly, unlike the deliberate, named treatment FR-12 got in AD-7. Worth a one-line addition to AD-7 or a Deferred flag, since it's an `[ASSUMPTION]`-tagged FR in the PRD (i.e., already flagged as needing explicit confirmation once).

### 3. PRD §7 Browser compatibility NFR — no explicit home (minor)
"Current stable Chrome, Edge, Firefox desktop only; no mobile support/testing" isn't named in the Stack table, an AD, or Deferred. This is likely low-risk since the spine relies only on standard `RTCPeerConnection`/`getUserMedia`/`getDisplayMedia` APIs with no browser-specific shims called out — but strictly, no document states "we target these three browsers and no others," so there's no architectural trigger to reject/flag mobile user-agents or polyfill for older browsers if that ever comes up in a story. Low severity; flagging per completeness.

### 4. Addendum: "reconnection-on-hub-disconnect behavior" — addendum explicitly calls this out as undecided; spine answers it only implicitly
`addendum.md` says verbatim: *"reconnection-on-hub-disconnect behavior are architecture-level decisions, not specified here."* The spine never states the decision explicitly. It's inferable (AD-4's `OnDisconnectedAsync` is the sole teardown path + PRD's explicit Non-Goal of no auto-reconnect ⇒ a hub disconnect is terminal, user must manually re-enter via `RoomEntry.razor`) but the addendum asked the architecture to make this call and the spine never states it as a rule anywhere (no AD, no Deferred bullet). Low severity — the answer is derivable, but a story-writer has to do the deriving themselves rather than read it off the page.

---

## Everything else: clean bill

- **FR-1, FR-2, FR-4–FR-13, FR-15–FR-22:** each has a clear, non-contradictory architectural home — either a named AD (AD-1 through AD-8 + Renegotiation corollary), a Structural Seed component, or a Capability Map row. Notably strong: FR-8/FR-10 (AD-8), FR-9 (AD-5), FR-12 (AD-7, with an explicit note on why enforcement is UI-only), FR-4 (AD-4 + the "Room-emptiness race" Deferred note), FR-18 (AD-4's no-separate-`LeaveRoom` rule matches FR-18's hang-up behavior exactly).
- **No FR was found to have spine coverage that *contradicts* its actual requirement.** FR-16's "stops the outgoing video track... no stale frame" language is worth a passing note: AD-6 implements mute/camera-off via `track.enabled = false` rather than literally stopping the track. In practice this satisfies the FR's intent (no stale/frozen frame — receivers get silence/black, not a frozen last frame) and is the correct, standard WebRTC technique, so this is **not** flagged as a contradiction, just an implementation-vs-wording nuance worth a sentence if a reviewer wants zero ambiguity.
- **Addendum Technology Stack, Mesh Topology, and CallHub Responsibilities sections:** all four CallHub bullets (track connection IDs per room, pairwise offer/answer relay, pairwise ICE relay, join/leave notification) map 1:1 onto AD-3, AD-4, and the two sequence diagrams. STUN-only/no-TURN is in the Stack table and the Deferred section. No-SFU/mesh-only design is the spine's stated paradigm. All match with no contradiction.
- **PRD §7 Performance and Reliability NFRs:** both explicitly covered — Performance via AD-5 + the Mesh Topology framing in Design Paradigm + the Deferred "Multi-instance scaling" note; Reliability via AD-8's explicit fault-isolation guarantee.
- **PRD §7 Code quality NFR:** this is effectively the spine's entire reason for being (Signaling-Plane/Media-Plane separation, AD-1, Structural Seed) — best-covered NFR in the set.
