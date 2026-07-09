---
name: FlowBoard Video Calls
status: final
created: 2026-07-09
updated: 2026-07-09
description: Dark-mode-first, developer-tool-polished visual identity for a peer-to-peer group video calling app. Custom CSS — no inherited UI component library (Blazor Server has none built in; none was specified upstream).
colors:
  background: '#1A1A1A'
  surface: '#242424'
  surface-raised: '#2E2E2E'
  border: '#3A3A3A'
  foreground: '#F2F2F2'
  foreground-muted: '#A3A3A3'
  primary: '#2DD4BF'
  primary-foreground: '#0A2320'
  primary-hover: '#5EEADE'
  destructive: '#EF4444'
  destructive-foreground: '#FFFFFF'
  destructive-hover: '#F87171'
  warning: '#F5A623'
  focus-ring: '#2DD4BF'
  avatar-1: '#F87171'
  avatar-1-foreground: '#3A0E0E'
  avatar-2: '#FBBF24'
  avatar-2-foreground: '#3A2A00'
  avatar-3: '#34D399'
  avatar-3-foreground: '#06301F'
  avatar-4: '#60A5FA'
  avatar-4-foreground: '#06213A'
  avatar-5: '#C084FC'
  avatar-5-foreground: '#290644'
  avatar-6: '#F472B6'
  avatar-6-foreground: '#3A0821'
  connection-issue-foreground: '#1A1300'
typography:
  display:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    fontSize: 28px
    fontWeight: '600'
    lineHeight: '1.25'
  body:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    fontSize: 15px
    fontWeight: '400'
    lineHeight: '1.5'
  label:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    fontSize: 13px
    fontWeight: '500'
    lineHeight: '1.3'
  caption:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.3'
  mono:
    fontFamily: "'Cascadia Code', 'SF Mono', Consolas, monospace"
    fontSize: 15px
    fontWeight: '500'
    lineHeight: '1.4'
    letterSpacing: '0.02em'
rounded:
  sm: 6px
  md: 10px
  lg: 14px
  xl: 20px
  full: 9999px
  DEFAULT: 10px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 20px
  '6': 24px
  '8': 32px
  '10': 40px
  '12': 48px
  '16': 64px
  grid-min-tile-width: 180px
  narrow-window-breakpoint: 1200px
components:
  primary-button:
    background: '{colors.primary}'
    foreground: '{colors.primary-foreground}'
    radius: '{rounded.md}'
    hover: '{colors.primary-hover}'
  secondary-button:
    background: '{colors.surface-raised}'
    foreground: '{colors.foreground}'
    border: '{colors.border}'
    radius: '{rounded.md}'
  room-id-input:
    background: '{colors.surface}'
    foreground: '{colors.foreground}'
    border: '{colors.border}'
    radius: '{rounded.md}'
    font: '{typography.mono}'
  video-tile:
    background: '{colors.surface}'
    radius: '{rounded.lg}'
    border: '{colors.border}'
  control-bar:
    background: '{colors.surface-raised}'
    radius: '{rounded.full}'
    border: '{colors.border}'
  control-button:
    radius: '{rounded.full}'
    foreground-active: '{colors.foreground}'
    background-active: '{colors.surface}'
    background-toggled-off: '{colors.destructive}'
  hang-up-button:
    background: '{colors.destructive}'
    foreground: '{colors.destructive-foreground}'
    hover: '{colors.destructive-hover}'
    radius: '{rounded.full}'
  name-label-chip:
    background: 'rgba(0, 0, 0, 0.55)'
    foreground: '{colors.foreground}'
    radius: '{rounded.sm}'
    font: '{typography.label}'
  mute-indicator:
    background: 'rgba(0, 0, 0, 0.55)'
    foreground: '{colors.foreground}'
    radius: '{rounded.full}'
  connection-issue-badge:
    background: '{colors.warning}'
    foreground: '{colors.connection-issue-foreground}'
    radius: '{rounded.full}'
  avatar-placeholder:
    radius: '{rounded.full}'
    font: '{typography.display}'
---

## Brand & Style

FlowBoard Video Calls reads as a tool a developer built for developers — the aesthetic Google Meet would have if it were a side project on someone's dark-themed desktop. Near-black surfaces, one calm accent color, no chrome for chrome's sake. Every visual decision optimizes for the thing that's actually happening: people's faces and a shared screen, not the app around them. `[ASSUMPTION: no Blazor UI component library (e.g. MudBlazor, Radzen) was specified upstream, so this spine assumes plain custom CSS — no inherited system defaults to lean on. If a component library is adopted during implementation, this DESIGN.md becomes the brand-layer delta on top of it.]`

## Colors

- **Background (`#1A1A1A`)** — the app's base surface. Near-black per spec, not pure black — pure black crushes video thumbnails and reads harsher than intended.
- **Surface (`#242424`)** and **Surface Raised (`#2E2E2E`)** — two steps of dark-mode elevation. Surface is video tiles and inputs; Surface Raised is anything that floats above the call (the control bar, tooltips). `[ASSUMPTION: two-step elevation scale inferred — user specified only the base background color.]`
- **Primary — Teal (`#2DD4BF`)** — the single accent. Used for the primary action (Create/Join), active/toggled-on control states, focus rings, and the Generate Room ID action. `[ASSUMPTION: user said "calm blue or teal" and left the choice to me — teal chosen because it reads distinctly from the ubiquitous blue of every other video-calling product (Meet, Teams, Zoom) while staying calm and desaturated, reinforcing the "developer-tool, not corporate-suite" posture from Brand & Style.]`
- **Destructive — Red (`#EF4444`)** — exclusively the Hang Up button and its hover state. Never used elsewhere; red must mean exactly one thing in this product.
- **Warning — Amber (`#F5A623`)** — exclusively the per-tile "connection issue" indicator (FR-10). Deliberately distinct from both Primary and Destructive so a viewer never confuses "this peer connection is degraded" with "this is a destructive action" or "this is active/selected." `[ASSUMPTION: color not specified by user; amber chosen as the conventional "caution, not failure" signal.]`
- **Avatar palette (6 hues, each with a paired `-foreground`)** — background colors for the initials/avatar placeholder (FR-16, FR-22), assigned deterministically by hashing each Participant's Display Name so the same name always gets the same color within a session. Each `avatar-N` background has a matching `avatar-N-foreground` — a darkened tone of the same hue — for the initials text, keeping every pairing at AA contrast without falling back to a single generic dark/light text color. `[ASSUMPTION: entirely inferred — needed so multiple camera-off participants remain visually distinguishable from each other, not specified by user.]`
- **Connection Issue foreground (`#1A1300`)** — the text/icon color used inside the amber Connection Issue Badge; a near-black tone of the same warning hue, kept as a distinct token (`{colors.connection-issue-foreground}`) rather than a literal so it stays traceable if the badge's content ever changes from an icon to text.
- **Foreground (`#F2F2F2`)** / **Foreground Muted (`#A3A3A3`)** — primary text and secondary/label text respectively, both meeting WCAG AA contrast against `{colors.background}` and `{colors.surface}`.

## Typography

System sans-serif throughout (`system-ui` stack) — no webfont load, no FOUT, consistent with the "developer-tool, not marketing site" posture and with the user's explicit "sans-serif system fonts" instruction.

One deliberate departure: **Room ID uses a monospace role (`{typography.mono}`)** everywhere it appears (input field, any on-screen display of the current room's ID). `[ASSUMPTION: not specified by user — monospace chosen because a Room ID is a value people read character-by-character and copy/paste, and monospace is the conventional signal for "this is a precise string, not prose," consistent with the product's developer-tool identity.]`

Four other roles: `display` (room-entry headline, empty-state text), `body` (general UI text), `label` (name chips, button text), `caption` (secondary hints, tooltips).

## Layout & Spacing

4-based spacing scale (`{spacing.1}` through `{spacing.16}`). Room-entry is a single centered card, max-width ~400px, vertically centered in the viewport — per the user's "clean and centered" instruction. The call view has no fixed max-width; the video Grid fills the available viewport below a slim top bar (if any chrome is needed) and above the floating control bar.

Grid gutters use `{spacing.3}` (12px) between tiles at all Participant counts — tight enough to feel like one continuous surface, loose enough that adjacent tiles never look merged.

Two named layout tokens govern the Grid's narrow-window behavior (EXPERIENCE.md → Responsive & Platform): `{spacing.grid-min-tile-width}` (180px) is the floor below which a tile stops shrinking and the Grid scrolls instead, and `{spacing.narrow-window-breakpoint}` (1200px) is the viewport width below which that floor starts to matter at high Participant counts.

## Elevation & Depth

Flat by default — dark-mode-first products read best with minimal shadow (shadows read as light-mode artifacts on near-black surfaces). The one elevated element is the floating control bar, which sits above the video Grid with a soft `0 4px 16px rgba(0,0,0,0.4)` shadow so it reads as a persistent overlay, not part of the Grid. `[ASSUMPTION: shadow treatment inferred; not specified by user.]`

## Shapes

Rounded corners throughout per spec: video tiles at `{rounded.lg}` (14px — soft enough to feel modern, not so soft it looks bubbly), buttons and inputs at `{rounded.md}` (10px), the control bar itself and all icon buttons within it at `{rounded.full}` (pill/circle — reinforces "these are the live controls," visually distinct from the rectangular video Grid beneath).

## Components

- **Primary Button** (Create/Join) — `{colors.primary}` fill, `{colors.primary-foreground}` text, `{rounded.md}`. The one filled-teal element on the room-entry screen.
- **Secondary Button** (Generate Room ID) — `{colors.surface-raised}` fill, bordered, `{rounded.md}` — visually subordinate to the primary action.
- **Room ID Input** — monospace, `{colors.surface}` fill, bordered, `{rounded.md}`.
- **Video Tile** — `{colors.surface}` fill, `{rounded.lg}`, houses the video element (or Avatar Placeholder), the Name Label Chip (bottom-left overlay), and conditionally the Mute Indicator and Connection Issue Badge (both top-right overlay, stacked if both apply).
- **Name Label Chip** — semi-transparent black overlay (`rgba(0,0,0,0.55)`) so it stays legible over any video content, `{rounded.sm}`, positioned bottom-left inset within the tile.
- **Mute Indicator** — small circular icon-only chip, same semi-transparent treatment, positioned top-right inset. Icon swap only (mic / mic-off) — no color change, since mute is a normal state, not a warning.
- **Connection Issue Badge** — small circular `{colors.warning}` dot/icon, top-right inset, offset from the Mute Indicator when both are present (mute indicator sits closer to center, badge sits at the true corner). `[ASSUMPTION: stacking order inferred — not specified.]`
- **Avatar Placeholder** — fills the tile where video would be; a large centered initials label (`{typography.display}`, one or two letters from the Display Name) in that avatar's paired `{colors.avatar-N-foreground}` on its `{colors.avatar-N}` background, `{rounded.lg}` matching the tile.
- **Control Bar** — floating pill container, `{colors.surface-raised}`, `{rounded.full}`, horizontally centered at the bottom of the call view, containing four controls in a fixed order: the Mic and Camera Control Buttons, the Share Screen control, and the Hang Up Button.
- **Control Button** (Mic / Camera / Share Screen) — circular icon button. Default/"on" state: `{colors.foreground}` icon on transparent-within-bar background. Toggled-off state (muted / camera off): icon swaps (e.g. mic → mic-off) and background becomes `{colors.surface}` so the "off" state is visually recessed rather than alarmed — off is a normal, reversible state, not an error. Disabled state (Share Screen, when another Participant is sharing): 40% opacity, no hover affordance, paired with a tooltip (EXPERIENCE.md defines the copy).
- **Hang Up Button** — visually distinct from the other three: filled `{colors.destructive}` circle at all times (never just an outline/icon-only like its neighbors), so it reads as categorically different — the one irreversible action in the bar.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Use `{colors.primary}` (teal) only for the primary action and active/on toggle states | Use teal for anything destructive or cautionary |
| Use `{colors.destructive}` (red) only for Hang Up | Introduce a second red element anywhere else in the product |
| Use `{colors.warning}` (amber) only for the connection-issue indicator | Reuse amber for any other "attention" state — it must stay a single, learnable signal |
| Keep the control bar flat/pill-shaped, floating above the Grid | Dock the control bar as part of the Grid layout — it must always read as an overlay |
| Let video content fill tiles edge-to-edge inside `{rounded.lg}` | Add padding around video inside a tile — the video *is* the tile |
| Use monospace for the Room ID, everywhere it appears | Use monospace for Display Names or any other free text |
