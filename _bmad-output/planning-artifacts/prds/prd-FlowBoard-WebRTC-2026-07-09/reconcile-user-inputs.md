# Input Reconciliation: PRD Finalize — FlowBoard Video Calls

Comparing all four original user inputs against `prd.md` and `addendum.md` (2026-07-09).

## Method

Every concrete fact, number, constraint, and scope item from the four inputs was checked against the PRD's Glossary, Features/FRs, Non-Goals, MVP Scope, NFRs, Success Metrics, Open Questions, and Assumptions Index, plus the addendum's technical-how content. Qualitative/tonal language (intent, stakes, feel) was checked separately from hard facts.

## Overall Finding

No hard contradictions and no dropped scope items. Every "must have," every discovery-time constraint, and every finalize-time resolution has a traceable home in the PRD (mostly 1:1, occasionally reworded but preserving meaning). The four confirmed `[ASSUMPTION]`s match exactly what the user approved. The professional-rigor ask (Input 2) is genuinely reflected structurally — nearly every FR carries a "Consequences (testable)" block, plus an Assumptions Index and resolved Open Questions section. The gaps below are softenings of *qualitative/emotional* framing, not missing requirements — exactly the kind of thing this reconciliation pass is meant to catch.

## Gaps / Softenings Found

### 1. "Mentor impressed" (emotional stake) flattened into a neutral comprehension metric
Input 3 §6 (verbatim): *"my mentor is impressed with both functionality and engineering quality."* This is an explicit emotional/subjective success bar, not just a functional one.
PRD's operationalization — SM-3 (§8): *"The codebase demonstrates clean separation of concerns... such that it can be walked through and understood by a mentor in a single review session."*
"Understood" is a much lower, more measurable bar than "impressed." The PRD's Vision (§1) does say "clean enough to walk a mentor through line by line," which gets closer, but nowhere does the doc preserve the *aspirational/pride* dimension of the original ask — it's been fully converted into an engineering-quality proxy. Worth flagging to the user: is "mentor understands the code in one sitting" an acceptable proxy for "mentor is impressed," or should a more evaluative bar be added (e.g., a specific quality signal the user associates with "impressed")?

### 2. "NO participant limit anywhere — nowhere" success bar narrowed to a 4-participant demo test
Input 3 §2 repeats the no-cap constraint three times with emphatic language ("NO participant limit anywhere — not hardcoded, not in settings, nowhere... zero cap... unlimited number of participants"), and Input 3 §6 frames success itself around *"an unrestricted, dynamic number of participants."*
FR-9 correctly preserves this as a code-level requirement ("No code path rejects a join due to Room size"). But the primary demo-validation metric, SM-1 (§8), only requires *"4 or more Participants"* to pass. Nothing in the Success Metrics actually exercises the "unrestricted/dynamic" property (e.g., a room growing past 5-6 without any rejection). This is a reasonable, practical demo bar — but it's a narrowing of the original's emphatic framing that the user should consciously sign off on, not one that happened silently by omission alone.

### 3. "Practice project" / personal-learning framing understated in favor of pure demonstration framing
Input 1 opens with *"This is project to practice WebRTC and real time communication"* — a personal-skill-building motivation. Input 2 recontextualizes this ("even though it's for learning, I want it built to a professional standard") but does not retract the learning framing.
PRD's JTBD (§2.1) states the primary job as: *"I need a working, non-trivial WebRTC application to demonstrate real-time systems engineering skill to a mentor and prospective employers."* This is entirely outward-facing (demonstration/portfolio) with no explicit trace of the original inward-facing "practice/learn" motivation. Vision §1 gestures at it via "prove out real-time communication engineering... to a professional standard," but the word "practice" / "learning" never appears. Low-stakes, but worth a conscious check: was dropping the learning-motivation framing intentional, or just a byproduct of Input 2 superseding Input 1 in the PM's read?

## Items Checked and Confirmed Present (no gap)

- Blazor Server / .NET 10 / single project / JS interop for WebRTC / SignalR `CallHub` / Google STUN (`stun:stun.l.google.com:19302`) / no other backend — all present verbatim in `addendum.md`.
- All "must have" scope bullets (room create/join by ID, multi-participant group calls, camera+mic streaming, mesh full visibility, screen sharing, all four call controls, adaptive grid) map 1:1 to FR-1 through FR-21.
- Mesh architecture, ~4-5 people informal target, no SFU, self-contained P2P — in addendum + §7 NFR Performance.
- Display name shown under tile, no accounts, arbitrary/generated room ID, implicit creation-on-first-use, ephemeral in-memory rooms — Glossary + FR-1/2/3/4.
- No participant cap in code/config — FR-9, Non-Goals §5, addendum.
- Per-pair STUN/NAT failure isolation with subtle non-blocking indicator, no crash — FR-10.
- No TURN in v1 — Glossary, FR-10 feature NFR, Non-Goals §5, addendum.
- Manual-only reconnection (no auto-reconnect) — Non-Goals §5. (Minor observation, not counted as a gap: there is no explicit FR stating "a manually rejoining participant succeeds via the normal join flow" — it's only inferable from FR-1/FR-2 plus the Non-Goals negative statement. Sufficient, but a single explicit sentence would remove any ambiguity.)
- Single active screen share, block-vs-replace resolved as "block," marked `[ASSUMPTION]` and confirmed — FR-12, Assumptions Index, matches Input 3 §3 exactly including the "keep it simple" instruction.
- Desktop-only Chrome/Edge/Firefox, mobile out of scope — §2.2, §7 NFR, Non-Goals §5.
- Localhost primary target, Azure nice-to-have, don't block v1, don't hardcode localhost assumptions — §6.2, addendum Deployment section, matches Input 4 point 3 near-verbatim.
- All six explicit non-goals from Input 3 §5 (chat, recording, auth/accounts, virtual backgrounds/blur, persistent history, TURN, mobile, auto-reconnect) — all present in §5, none dropped.
- Finalize resolution: denied camera/mic permission → joins anyway with camera-off placeholder + "media unavailable" indicator, never blocks — FR-22, matches Input 4 point 1 near-verbatim.
- Finalize resolution: connection-issue indicator is informational-only, no retry in v1 — FR-10 consequence, §9 Open Question #2.
- Finalize resolution: visual design deferred to UX (Sally/bmad-ux) — §9 Open Question #4, Assumptions Index note on FR-16.
- All four `[ASSUMPTION]` tags (FR-12 block-vs-replace, FR-14 share-ends-on-disconnect, FR-16 placeholder treatment, FR-21 screen-share-priority layout) are present, correctly tagged, and match the Assumptions Index's "all four confirmed" from Input 4.

## Recommendation

None of the three gaps above require reopening the PRD's functional content — they're calibration questions about how much of the original *emotional/motivational* framing should be made explicit rather than left as inferred context. Suggest surfacing gaps #1 and #2 to the user as a quick confirm/adjust before the PRD is marked final, since they involve the user's own success bar (not a PM inference). Gap #3 is lowest priority — cosmetic framing only.
