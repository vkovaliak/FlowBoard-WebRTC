# PRD Quality Review — FlowBoard Video Calls

## Overall verdict

This PRD is unusually disciplined for its stakes: the thesis (prove professional-grade P2P WebRTC engineering to a mentor) is stated once and every feature, NFR, and success metric traces back to it without drift into scope theater. Trade-offs are named honestly (Mesh scaling ceiling, no-TURN failure mode, block-vs-replace screen share), and the Assumptions/Open-Questions machinery round-trips cleanly. The only real soft spots are a handful of FR consequences that lean on unquantified adjectives ("exceedingly unlikely," "legible") and one broken section cross-reference (FR-6 → "§8" should be §9) — neither threatens the PRD's usefulness for the next workflow steps.

## Decision-readiness — strong

Trade-offs are surfaced with what was given up, not smoothed over. FR-12's `[ASSUMPTION — confirmed: Chosen "block" over "replace" as the simpler, lower-risk implementation...]` names the alternative and why it lost. §5 Non-Goals states plainly that no-TURN means "Participants on networks that block direct/STUN-assisted connections will experience a failed Peer Connection... not a working call" — not softened into "may experience degraded connectivity." §7's performance NFR admits degradation "beyond [4-5 participants]... is expected and acceptable" rather than claiming the system handles scale gracefully. `[NOTE FOR PM]` callouts land at real deferred tensions (TURN revisit tied to mentor-demo network risk, §5; retry-on-failure deferred to v2, FR-10) rather than at safe checkpoints. §9 Open Questions are genuinely resolved-with-reasoning rather than rhetorical.

### Findings
- **low** Stale cross-reference (§4.2 FR-6) — "declining is handled... or is shown a clear message — see Open Questions §8" points to the wrong section; Open Questions is §9. *Fix:* change "§8" to "§9" (also listed under Mechanical notes).

## Substance over theater — strong

No persona theater: three UJs (Maria, Alex, Jordan), each with a named protagonist that drives specific FRs (UJ-3 → FR-8, FR-19, FR-10's edge case), well under the four-persona ceiling. The "As the builder" JTBD (§2.1) is an unusual but honest admission that the primary job is portfolio demonstration — this is substance, not a swapped-in template line, since it's what actually shapes SM-3 and the Non-Goals list. Vision (§1) is specific to this product's actual constraints (mesh, no media server, JS-interop-mediated browser APIs, mentor walkthrough) — it would not paste cleanly into another video-calling PRD. NFRs in §7 carry product-specific thresholds (STUN/HTTPS constraint tied to origin type, "~4-5 Participants" performance ceiling) rather than generic "must be scalable/secure" boilerplate.

## Strategic coherence — strong

The thesis — demonstrate professional-grade P2P real-time engineering, not build a scalable product — is stated in §1 and enforced downstream. SM-C1 is a genuine counter-metric that guards the thesis directly: "Do not build toward larger-than-mesh-appropriate participant counts... in pursuit of 'no participant cap' (FR-9)." Success metrics (SM-1, SM-2) are demo-completion criteria, not activity metrics (no DAU/MAU-style stand-ins), which fits a single-user portfolio artifact rather than a product with an engagement thesis. Feature order (§4.1–4.5) follows the call lifecycle the thesis needs demonstrated (join → mesh → share → control → layout), not an arbitrary backlog.

## Done-ness clarity — adequate

Most FRs carry testable consequences with concrete, checkable behavior (e.g., FR-5: "each Participant holds exactly N-1 active Peer Connections"; FR-8: "does not glitch, drop, or renegotiate"; FR-22: "Denying permission never prevents a Participant from entering a Room"). A few consequences fall back on unquantified adjectives that weaken testability at review/QA time.

### Findings
- **low** Unquantified randomness bound (§4.1 FR-3) — "Generated IDs are sufficiently random/short that two independent clicks are exceedingly unlikely to collide" gives no length/character-set/entropy figure to test against. *Fix:* state a concrete ID length/charset (even loosely, e.g. "6 alphanumeric characters") so the consequence is checkable.
- **low** Subjective legibility bound (§4.5 FR-19) — "remaining legible at each" breakpoint has no defined criterion (tile minimum size, aspect ratio) for what counts as legible. *Fix:* either accept this as a UX-workflow handoff item (cross-reference §9 item 4) or add a minimum-tile-size bound.
- **low** Soft timing language repeated across FR-11 and FR-20 ("within a couple of seconds") is consistent at least, and reasonable for this stakes level — noted only because downstream story acceptance criteria will need to convert this to a hard number eventually; not a fix required now.

## Scope honesty — strong

§5 Non-Goals is extensive and does real work (nine explicit exclusions, each stated as a decision, e.g. TURN, mobile, auto-reconnect, participant cap). All four `[ASSUMPTION]` tags (FR-12, FR-14, FR-16, FR-21) are inline and re-indexed identically in §10, each marked "Confirmed" with the user's review noted. Open-items density is low (0 blocking open questions, 4 confirmed assumptions, 3 `[NOTE FOR PM]` callouts) — appropriate for a Finalize-stage PRD at this project's stakes; nothing reads as inflated to look thorough.

## Downstream usability — strong

This is a chain-top PRD (→ UX → architecture → stories) and the traceability holds up. Glossary (§3) terms are used consistently capitalized across UJs, FRs, and NFRs (Room, Room ID, Participant, Display Name, Mesh, Peer Connection, Signaling, CallHub, STUN, ICE Candidate, Screen Share, Call Controls, Grid) with no synonym drift found. UJs each have a named protagonist carrying context inline (Maria/Alex/Jordan), not floating. FR IDs are unique and all cross-references resolve except the one noted above.

### Findings
- **low** FR-22 is numbered out of sequence (§4.2) — it appears physically after FR-10 but is numbered 22, ahead of FR-11–FR-21 which follow it in later sections (§4.3–4.5). Not a gap or duplicate, but a downstream tool or reader walking FRs in ID order will jump 1→10, 22, then back to 11. *Fix:* renumber for sequential continuity, or leave as-is but note it was appended during Finalize (harmless if intentional, but worth a one-line note in the doc).

## Shape fit — strong

Consumer-facing, meaningful-UX product with a small, real user base (ad-hoc small-group calls) — three UJs with named protagonists is the right amount of formalization, not UJ-density overkill for a single-operator tool. Solo/portfolio calibration is respected throughout: no compliance, auth, or multi-stakeholder scaffolding was force-fit in, and NFRs stay scoped to what actually matters (secure-context/HTTPS constraint, Mesh performance ceiling, fault isolation, code-quality-as-NFR for the mentor walkthrough) rather than generic enterprise boilerplate. The addendum.md split (technical-how kept separate from product requirements) is itself good shape discipline for a chain-top PRD headed into an architecture phase.

## Mechanical notes

- **Broken cross-reference:** §4.2 FR-6 references "Open Questions §8" — Open Questions is actually §9 (Success Metrics is §8). The correct citation appears two paragraphs later at FR-16 ("§9"), so this is a one-off slip, not systemic.
- **ID ordering:** FR-22 (§4.2) is numbered ahead of FR-11–FR-21 despite appearing earlier in document order — full ID set 1–22 is present with no true gaps or duplicates, just a non-monotonic placement suggesting a late Finalize-stage addition.
- **Assumptions Index roundtrip:** Clean. All four inline `[ASSUMPTION]` tags (FR-12, FR-14, FR-16, FR-21) are indexed in §10 with matching content and confirmation status; no index entries lack an inline counterpart.
- **UJ protagonist naming:** Clean. UJ-1 (Maria), UJ-2 (Alex), UJ-3 (Jordan) each carry context inline (relationship to the call, entry state, edge case).
- **Glossary drift:** None found. Capitalized terms (Room, Room ID, Participant, Display Name, Mesh, Peer Connection, Signaling, CallHub, STUN, ICE Candidate, Screen Share, Call Controls, Grid) are used consistently; no synonyms substituted downstream.
- **Required sections:** All present and appropriately scoped for a chain-top solo-project PRD (Document Purpose, Vision, Target User incl. Non-Users, Glossary, Features/FRs, Non-Goals, MVP Scope, Cross-Cutting NFRs, Success Metrics incl. counter-metric, Open Questions, Assumptions Index).
