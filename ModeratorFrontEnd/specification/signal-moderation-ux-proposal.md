# Moderator Overlay + Bout Annotation Tagging — UX Proposal (Draft v0.2)

> Status: **DRAFT / EXPLORATORY** — companion to
> [bout-spec.md](bout-spec.md). This document proposes UX options; it does not
> replace the normative contracts in the bout spec. Where it introduces new data
> or APIs, those additions are proposals for §6b.2 and §6b.4 of the bout spec.
> Grounded in a 2026-09-16 request to (a) improve the moderator evidence
> **overlay** and (b) add **bout annotation tagging** for moderators.

## 1. Purpose & scope

Two related goals:

1. **Evolve the Moderator Workbench overlay.** Today's overlay (bout-spec §8.1)
   pivots almost entirely on *who reported* an annotation (human vs model, per-reporter
   lanes). That is necessary but insufficient. Moderators also need to see, at a
   glance:

   - which annotations are **already moderated/confirmed** vs still **unverified**;
   - when a single timeframe contains **mixed species that split into two bouts**
     (e.g. a humpback bout and a Biggs/transient bout overlapping in time);
   - when annotations **change under them** — a label correction (humpback →
     transient) that can move a bout boundary — both on the bout they are actively
     editing and on bouts they have previously worked.

1. **Add bout annotation tagging alongside post-closure bout approval** — primarily as
   an inline mode of the M2 Moderator Workbench overlay, with dedicated queue/compare
   views retained as alternatives (§6). As soon as a bout begins, a moderator can
   open it and tag or correct its approximately 3-second annotations, including
   annotations not yet assigned to a species/source bout, without leaving the
   overlay; final approval remains available once the bout is closed. A confirmed
   annotation becomes authoritative; later reporter/model input is preserved as a
   separate proposal rather than overwriting it. Whether the API also hard-locks
   confirmed annotations is unresolved pending the override policy in U-3. The flow
   is **time-first** (default 2-minute contextual viewing span, zoomable) and supports
   select annotation → listen → tag/confirm, followed by bout approval. Approximately
   3-second annotations are the preferred review unit. When a detection has no child
   annotations (as with a human-reported detection), the 60-second detection remains
   selectable and taggable as the fallback review unit so it can become a Signal and
   reach bout approval.

The main overlay is also **pivotable**: a one-tap control flips linked projections
of the same annotations between a *who reported* view (per-reporter lanes) and a
*what is here* view (species/source lanes) — see §4.0.

In v1, selecting, tagging, correcting, confirming, and changing annotation labels are
moderator actions integrated into the main overlay (§5). A separate overlay for
non-moderator reporters reviewing non-live data is out of scope; it may be
explored later if a concrete requirement emerges.

Out of scope here: the bout generation algorithm (bout-spec §3–§6), notification
delivery mechanics (bout-spec §5.2 and §8.1), and storage-engine choice
(bout-spec §6b.5).

## 2. Terminology used here (delta over bout-spec §2)

| Term | Meaning in this document |
| ---- | ------------------------ |
| **Annotation** | The preferred fine-grained interval shown for moderator tagging: an approximately 3-second interval with proposed or confirmed label(s). |
| **Detection-only review unit** | A 1-minute detection with no child annotations, including a human-reported detection. It is shown as a selectable parent-window row and may be tagged or confirmed directly; it is not synthesized into a false 3-second annotation. |
| **Proposed annotation** | A reporter's or model's unconfirmed approximately 3-second interval and tag(s) before a moderator acts. |
| **Confirmed annotation** | An annotation whose tag(s) a moderator has confirmed. It is authoritative; reporters and models may submit new proposals but never overwrite the moderator decision. Hard-lock enforcement is an unresolved alternative (U-3). |
| **Moderation state** | Per-annotation review status (see §7): `unmoderated`, `confirmed`, `false_positive`, `needs_confirmation`. Distinct from the *bout* `status` in bout-spec §5.2.1, which may be `rejected`. A label change is an audited action, not a persistent moderation state. |
| **Detection review state** | Status used only by detection-only records: `unreviewed`, `confirmed`, `false_positive`, `unknown`, matching bout-spec §6b.2. It is not persisted as annotation `moderation_state`; UI glyphs map `unreviewed → □` and `unknown → ?`. |
| **Signal** | A tagged annotation or detection that a moderator counts as present, matching bout-spec §2. Annotation confirmation and detection-only confirmation are distinct persistence paths that can both produce Signals. |
| **Reporter identity** | A `reporters` row describing the human or model that authored source evidence. A human reporter has a unique nullable `user_id` link to an authenticated user; the server derives this mapping and never accepts it from review request JSON. |
| **Non-target/source label** | A valid controlled-vocabulary tag describing what was actually heard when the proposed target label was false (for example, `srkw → seal`). A `false_positive` decision requires at least one such tag; it is never represented by an empty tag set. |
| **Bout approval** | The explicit **Publish bout** transition for a completed candidate, matching bout-spec §5.2.1. It is separate from tagging or confirming evidence, and it is the only transition that can notify subscribers. |
| **Watcher** | A moderator who is working, or previously worked, a bout and should be alerted when its evidence changes (§8). |

## 3. Why the current overlay is not enough

The currently proposed overlay
![moderator workbench](images/moderator-workbench.svg)
encodes **reporter identity** with lane + color (Reporter A human / Reporter B
model). That answers "who said this?" but not:

- **"Is this annotation trustworthy yet?"** — confirmed vs unverified is invisible; a
  moderator can build a bout on evidence that no human has vetted.
- **"Are there really two things here?"** — a minute with both humpback and
  transient calls looks like one busy lane, not two overlapping bouts.
- **"Did something move?"** — if another moderator changes the label on a nearby
  annotation, the boundary can shift silently; there is no change alert.

The options below add these three encodings **without** throwing away the
per-reporter view that already works.

> **Baseline rendering convention (matches the live site).** The sonogram always shows the
> sound waveform with bright **bursts** where energy is detected. An annotation is
> drawn as an **empty outlined rectangle around** its burst — never a solid fill —
> so the waveform stays visible inside the box. Encoding rides on the box's
> *outline and corner glyph* (color = annotation label, outline pattern = reporting
> source, corner glyph = moderation state), not on an opaque fill. Two annotation
> labels in one timeframe are two overlapping boxes. M1 intentionally tests a
> state-fill alternative only in the lower swimlane markers; spectrogram boxes
> remain translucent enough to preserve waveform visibility.

---

## 4. Moderator overlay — proposed options

Each option is evaluated against the three needs: **(M-state)** show moderation
status, **(Species)** show mixed-species/two-bout overlap, **(Alerts)** show
changes.

### 4.0 Baseline capability — “Who” and “What” as separate, combinable layers

The main interface treats **Who (source)** and **What (species)** as two
*independent layers*, not a single either/or view, because they answer different
questions and routinely co-occur (a vessel *and* residents in the same minute):

- **What · annotation label set** is drawn as **outline color segments** — one
  color for a single label; multiple ordered colors for simultaneous labels such
  as `{srkw, vessel}`.
- **Who · reporting source** is drawn as the **outline pattern** — solid for human,
  dashed for model, and dotted when source is unknown or unavailable.
- **Moderation state** is drawn as a **corner glyph** — `□` unmoderated, `✓`
  confirmed, `×` false positive, and `?` needs confirmation.

Detection-only rows reuse these shapes but retain their distinct status names:
`□` means `unreviewed` and `?` means `unknown`. Every marker tooltip, focus label,
and details panel names both the review-unit type and full state text; glyph shape
alone is never used to distinguish annotation from detection state.

Each layer has its **own on/off control**, and the default shows **both at the same
time**. The swimlane toggle only changes how linked projections of the same
underlying annotations are grouped by reporting source or annotation label; it
never changes the encoding rules.

![Who (reporting source) and What (annotation label) shown together as outline pattern and color, with corner moderation glyphs and a toggle that only regroups the lower bars](images/overlay-who-what-layers.svg)

**The toggle is for the lower bars.** Below the spectrogram, linked projections of
the same annotations are laid out as grouped **swimlane rows**. A one-tap toggle
sets whether those *lower bars* are grouped **by Who** (per-reporter lanes) or
**by What** (species lanes). The toggle only regroups the projections; it does
**not** turn off either upper layer, and it preserves zoom, playback, selection,
and the underlying evidence.

```text
Layers:  [x] What · annotation label (color)   [x] Who · source (outline)
Lower bars grouping:  (•) by Who   ( ) by What        ⟵ toggle
```

![Lower-bar grouping toggle: linked projections of the same annotations re-grouped between per-reporter lanes and species lanes](images/signal-overlay-pivot.svg)

**Encoding channels (shared baseline vocabulary).** M2, M3, and the moderator
editing controls (§5) reuse this channel assignment, so the four concerns never
fight over the same visual variable and can be shown simultaneously. M1 is the
explicit alternative that moves source/type into the lane grouping and adds
state fill while retaining the same corner glyphs.

| Concern | Channel |
| ------- | ------- |
| **What** · species/source tag set | **one or more ordered outline-color segments**; lane projections use the lane color plus `+N` |
| **Who** · reporting source | **outline pattern** (solid = human, dashed = model, dotted = unknown/unavailable) and/or lane |
| **Moderation state** | **corner glyph** (`□` unmoderated, `✓` confirmed, `×` false positive, `?` needs confirmation) |
| **Change alerts** | **motion** (pulse) + minimap ✦ |

The box interior stays empty so the waveform burst remains visible. A changed
label uses its new annotation-label color segment(s) and current moderation glyph;
change history and review alerts remain in the audit panel/minimap rather than
consuming another marker channel.

#### Multi-tag rendering and selection

Only species/source tags that can determine bout membership participate in the
color encoding; metadata tags such as pod, call type, and `high-value` remain in
the details panel. The controlled vocabulary supplies a stable `display_order`
and color for each species/source tag, with tag id as the tie-breaker.

- **Spectrogram:** one annotation always renders as one box, even when it has
  several species/source tags. For one tag, the full outline uses that tag's
  color. For two or three tags, the perimeter is divided into equal contiguous
  color segments in canonical order. Every segment retains the annotation's
  solid/dashed/dotted reporting-source pattern. For more than three tags, show
  the first two color segments plus a neutral overflow segment and a `+N` badge;
  hover, keyboard focus, and the details panel expose the complete ordered tag
  set. An annotation with no species/source tag uses the neutral Unassigned
  outline.
- **Species/source lanes:** the same annotation is projected once into every
  applicable lane. Each projection uses that lane tag's color and shows `+N`
  when the annotation has other species/source tags. Source pattern and
  moderation glyph are identical on every projection. Reporter-grouped lanes
  show one marker using the same segmented outline as the spectrogram box.
- **Linked selection:** the spectrogram box and all lane projections share one
  `annotation_id`. Selecting, hovering, or focusing any representation
  highlights every representation and opens one selection record containing the
  complete tag set. Multi-select de-duplicates by `annotation_id`, so a
  `{srkw, vessel}` annotation contributes one item, not two. Confirm and Change
  actions always submit the complete resulting tag set; acting from one species
  lane does not silently remove the annotation's other tags.

Color is never the only accessible label: focus/hover text and the details panel
name the full tag set, while source pattern and moderation glyph retain their
existing non-color channels.

Option M3 generalizes the lower-bar grouping to a third choice (group by moderation
state) in addition to Who/What.

### Option M1 — Status fill on toggleable lanes (evolutionary)

This option intentionally departs from the §4.0 outline-only baseline for lower
swimlane markers. Its fill is supplemental; the corner glyph remains the
portable moderation-state encoding shared with every other view.

Keep the existing annotation overlay, but let moderators toggle its lanes between
**reporting source** and **annotation label**. Do not add another per-marker label
indicator: the active lane grouping already communicates that dimension. Add moderation
state as a marker fill, reinforced by the shared corner glyph: neutral/`□`
unmoderated, green/`✓` confirmed, red/`×` false positive, and amber/`?` needs
confirmation. An annotation's state fill and glyph persist across regrouping when the
toggle moves it to a different lane.

Keep **change alerts via a pulse + count chip** on markers whose underlying
annotation changed since the panel opened.

```text
Group by source
Human  ┤  [□]   [✓]       [?]
Model  ┤    [□]   [×]

Toggle to group by annotation label — the same state fills and glyphs move lanes
Humpback  ┤  [□]   [✓]
Transient ┤    [□]   [×]   [?]
```

![Option M1 wireframe: toggleable source or annotation-label lanes with persistent moderation-state fills, corner glyphs, and a change pulse with count chip](images/moderator-overlay-m1.svg)

#### M1 strengths

- Smallest change; reuses muscle memory and existing components.
- Cheapest to build; incremental rollout.

#### M1 tradeoffs

- Only the active grouping is structural; comparing source and annotation label at the
  same time requires toggling.
- The two-bout/mixed-species case is still cramped when source lanes are active;
  overlap becomes structural only after switching to annotation-label lanes.
- State fill must retain the corner glyph for colorblind accessibility.

Handles: M-state ✔ · Species ~ (weak) · Alerts ✔

### Option M2 — Species-first swimlanes (re-pivot)

Make **species/source** a primary grouping (flip from the normative reporter
default at any time, §4.0). Each species gets its own lane band
(Humpback, Biggs/transient, Vessel, …). Within a band, reporting source remains
the marker's solid/dashed/dotted outline and moderation remains its corner glyph.
Two overlapping bouts render as two clearly separated bands, each with its **own**
boundary handles.

As an optional focus control, add a **Bout limits** selector with **All annotation
labels** (default) or one annotation label/species. Selecting a label shows only that
label's bout boundaries and handles; all overlapping annotations and swimlanes remain
visible. This reduces competing boundary lines without hiding evidence or
changing the active annotation-label/source swimlane pivot.

```text
 Humpback   ┤  ┌✓┐ ─────────── bout #1 boundary ──────────  │ solid=human
 Transient  ┤        ┌□┐--┌✓┐-- ─ bout #2 boundary ───────  │ dashed=model
 Unassigned ┤   ┌?┐··     (needs annotation label)           │ dotted=unknown source
```

![Option M2-A wireframe: species-first swimlanes with overlapping bouts, bout-limit filtering, an audited Force merge bouts option, shared moderation-state legend, change minimap, and inline moderation actions](images/moderator-overlay-species.svg)

#### Option M2-A — Inline annotation tagging mode (recommended v1)

Add annotation tagging directly to the M2 overlay rather than navigating to a
separate console. Selecting one or more proposed annotation markers opens a
persistent action bar below the swimlanes with **Listen**, **Tag**, **Confirm
tags**, and **Change labels**. The
spectrogram, species lanes, bout boundaries, minimap, and current zoom remain in
place. The action bar remains available under either §4.0 pivot; switching to the
species pivot exposes the M2 lanes but is not required to tag evidence. Annotations
without a species/source assignment appear in **Unassigned**
and can be tagged with the same controls. A detection with no child annotations
appears in a **Detection-only** row spanning its parent window and uses the same
**Listen**, **Tag**, **Confirm tags**, and **Change labels** actions at detection
scope. **Publish bout** remains a separate bout action after evidence review;
confirming either review-unit type counts it as a Signal but does not approve the
bout.

The candidate enters the Moderator Workbench queue and becomes available for
evidence review as soon as qualifying activity is detected. It remains marked
**active** until the automatic 15-minute no-detection gap closes; closure enables
bout approval/publish without moving it to a second queue or changing its id.
After an action, the marker updates in place to the shared corner-glyph styling
and the next unmoderated annotation may be selected. Group selection uses the same
action bar and requires an explicit audited revision for any confirmed member
or false-positive member (§5.2).

#### M2 strengths

- The mixed-species / two-bout timeframe is **immediately legible** — the split is
  structural, not inferred.
- Overlay aligns with bout identity (species + node + time), so boundaries read
  naturally per species.
- Moderation state is still visible per annotation.

#### M2 tradeoffs

- Comparing the *same reporter* across species is harder (reporter is now
  secondary).
- More vertical lanes; an "Unassigned/ambiguous" holding lane is required for
  annotations whose species is not yet determined.
- An Unassigned annotation does not contribute to a bout until it receives at
  least one applicable species/source tag.

Handles: M-state ✔ · Species ✔ (strong) · Alerts ✔

### Option M3 — Layered canvas with switchable pivot + facet toggles (power tool)

One spectrogram; the §4.0 pivot control is extended to a **third option** —
(Reporter | Species | Moderation state) — and the moderator can toggle facets on/off. Confirmed annotations
render with a checkmark. A **minimap ribbon** above the timeline shows annotation
density and change markers across the whole session, so alerts are visible even
off-screen.

When **Moderation state** is the active grouping, lower-bar projections are placed
in Unmoderated, Confirmed, False positive, or Needs confirmation lanes. The same
corner glyph remains on every projection regardless of grouping, so changing the
pivot does not remove the accessible state cue.

```text
Pivot: (•)Annotation label ( )Source ( )State    Facets: [x]confirmed [x]unmoderated [ ]false positive [x]needs confirmation
minimap ▁▂▅█▅▂▁  ← change ✦ at 08:04 ─────────────────────────────────
[ main spectrogram + overlay rendered per current pivot ]
```

![Option M3 wireframe: switchable primary pivot (Reporter | Species | Moderation state), facet toggles hiding annotation classes, and a minimap ribbon surfacing off-screen changes](images/moderator-overlay-m3.svg)

#### M3 strengths

- Most flexible; supports all three needs and future ones (e.g. confidence,
  call-type) by adding facets rather than redesigns.
- Each moderator tunes the view to the task (triage vs boundary work).
- Minimap makes off-screen changes discoverable.

#### M3 tradeoffs

- Highest complexity and build cost; needs excellent defaults or it overwhelms.
- Discoverability/consistency risk (two moderators may see different pivots).
- Harder to document and QA.

Handles: M-state ✔ · Species ✔ · Alerts ✔ (strong)

### Moderator overlay — comparison

| Option | M-state | Species / two-bout | Alerts | Build cost | Overload risk |
| ------ | :-----: | :----------------: | :----: | :--------: | :-----------: |
| M1 evolutionary | Good | Weak | Good | Low | High |
| M2 species-first | Good | **Strong** | Good | Medium | Medium |
| M3 layered/switchable | Good | Strong | **Strong** | High | Medium‑High |

**Recommendation:** ship M2 as the primary mixed-species review mode and adopt
M3's minimap ribbon as the alert mechanism layered on top. The initial v1 pivot
remains **Reporter**, matching bout-spec §11; making M2 the default requires an
explicit amendment to that normative decision. Keep M1's corner-glyph vocabulary
as the shared moderation-state language across every moderator view.

---

## 5. Moderator annotation editing in the overlay (v1)

Annotation editing is part of the Moderator Workbench overlay in §4, not a separate
surface. A moderator stays in the same time-aligned spectrogram and species/source
lanes while inspecting, selecting, listening to, tagging, confirming, or changing
annotation labels. The current pivot, zoom, playback position, and bout
boundaries remain visible throughout the action.

### 5.1 Single-annotation editing

A moderator selects an approximately 3-second annotation box or its aligned lane marker. The details panel
shows the current and proposed tags, reporter/model provenance, confidence,
moderation state, audio interval, and review history. The moderator can **Listen**,
**Confirm tags** or **Change labels** without leaving the overlay. Confirmed
decisions are authoritative; subsequent corrections use an explicit audited
moderator action (§7).

### 5.2 Group selection and tagging

Moderators can rubber-band a time range, Shift-click annotations, use a checkbox list,
or choose **Select all in view**. A selection summary shows the count, time span,
current tags, and moderation states. **Listen to selection** plays only those
intervals, and **Change selection** applies one tag-set edit to the whole group.
Annotations with a prior terminal decision (`confirmed` or `false_positive`) are
identified before submission and require an explicit audited revision; no member
is silently skipped.

This interaction supports the worked existing-bout corrections in §8.1–§8.2 and
  the bout annotation-tagging options in §6. On phone, the same controls use the stacked
Workbench layout and sticky moderator action bar described in bout-spec §8.2.

### 5.3 Create a boundary-adjacent annotation

When continuous audio reveals relevant activity just outside the current bout,
the moderator can drag across the spectrogram to create an approximately
3-second annotation and assign its label. This is the fine-grained correction
action; when child annotations exist, the one-minute parent detection is not a
second selectable or editable review item. The new annotation records moderator
provenance and is stored under a moderator-authored detection that references the
stable media asset for the selected node/time window (§7.1). It is never appended
under a model's or another reporter's detection. If no detection exists at that
time, Save atomically creates the moderator detection parent and, when necessary,
pins a media asset from the feed segment before creating the annotation. Save is
disabled if the selected interval cannot be linked to stable audio. The new
annotation enters the same preview and atomic bout-recomputation flow as a label
change (§8). The moderator can therefore grow, join, or split a bout from audible
evidence that the model did not annotate without duplicating source evidence or
losing its parent/media linkage.

### 5.4 Detection-only fallback review

When a detection has no child annotations, the Workbench renders its full parent
window in a dedicated **Detection-only** row. The moderator can select it, listen
to the shared audio, assign or correct detection tags, and confirm whether it is
present. The details panel shows reporter provenance, detection confidence,
description, tags, and review history. Confirmation makes the tagged detection a
Signal under bout-spec §2; it can seed or support the applicable bout and remains
available when that bout is approved. If child annotations exist, the UI uses
those annotations instead and does not duplicate the parent detection as another
review item.

Non-moderator tagging or correction of historical annotations is not a v1
requirement. Reporters and models continue to supply source evidence through the
existing ingestion paths; a future reporter-facing review experience requires a
separate product decision.

---

## 6. Bout Annotation Tagging options (moderator-only)

The recommended v1 option is **M2-A**, the inline tagging mode in §4. It supports
the core **see → select annotation → listen → tag/confirm** loop without a separate console
or workflow. The dedicated concepts below remain alternatives for unusually large
backlogs or focused reconciliation. In every option, confirming annotation tags
makes that moderator decision authoritative (§7); it does not imply an exclusive
hard lock or approve the bout. The default **inspection span** is 2 minutes,
zoomable out to hours or in to seconds.

![Dedicated bout annotation-tagging alternative (S2 timeline scrubber): zoom presets, selectable annotation blocks, listen/tag/confirm/change-label actions, authoritative confirmation, candidate lifecycle, and downstream feedback](images/signal-approval-console.svg)

### Shared requirements (all options)

- **No waiting (KPI1):** evidence moderation and candidate preparation MUST become
  available as soon as qualifying activity is detected. No design may wait for
  the 15-minute no-detection gap, bout closure, or another timer before exposing
  the active candidate and its evidence for moderator action. Later evidence may
  extend or recompute its boundaries without blocking initial review. Publication
  and notification remain disabled until closure and explicit **Publish bout**.
- **Initial inspection framing:** when opened from a model report whose parent
  window is `[D.start, D.end]` (60 seconds in v1), show
  `[D.start − 30 s, D.end + 30 s]`. This symmetric 2-minute viewport is centered
  on the parent window and exposes its full 60 seconds plus 30 seconds of audio
  before and after it. Do not draw the
  parent detection as a selectable interval when child annotations exist. For a
  detection-only record, draw the parent window in the dedicated Detection-only
  row and allow it to be selected. If no report is selected, fit the bout period
  with 30-second shoulders and a minimum 2-minute span.
- **Why not a shifted 1-minute viewport:** showing 30 seconds before the parent
  window and only its first half hides its latter half and guarantees a pan.
  That trades one boundary blind spot for another and increases the chance that
  the moderator confirms without reviewing all model evidence.
- **Boundary framing:** **Fit start boundary** shows the active boundary with the
  full 15-minute exterior gap and at least 1 minute inside the bout
  (`[start − 15 min, start + 1 min]`). **Fit end boundary** mirrors it
  (`[end − 1 min, end + 15 min]`). These dynamic 16-minute views are distinct
  from the 2-minute inspection default and expose `satisfied`, `unknown`, or
  `violated` boundary evidence without representing detections as editable
  objects.
- **Time axis with zoom:** default 2-minute window; zoom presets 10 s / 2 min /
  15 min / 1 h; horizontal scrub with audio kept in sync. Zooming or panning
  away from a fitted view is unrestricted.
- **Select:** one annotation, a rubber-band range, a checkbox list, or "all unmoderated in
  view." A detection-only record is selected as one 60-second unit; selections
  MUST NOT combine annotation and detection scopes in one confirmation action.
- **Multi-select tagging (first-class):** a single **Confirm tags** or **Change labels**
  applies to the **entire selection** in one action, with a
  keyboard/gesture shortcut for "confirm all in view." Annotations with a prior
  terminal decision are flagged before submission. To retain one in the batch,
  the moderator must choose an audited action and provide that item's
  `override_reason`; otherwise it must be removed from the selection. The server
  never silently skips it. This is the primary throughput lever and MUST be
  available in every option and on phone.
- **Listen:** scoped playback of the selected annotation/region (or sequential
  playback across a multi-selection).
- **Tag/confirm:** confirm existing tags or change the label/species; the chosen label
  applies to the whole selection. False positives are corrected to the appropriate
  non-target/source label. Edits write to the main database (§7), not a side store.
- **Authority on confirm:** reporter/model ingestion creates separate proposals
  and may not overwrite a confirmed decision. A moderator may revise it through
  an explicit audited action. Hard-lock enforcement remains an alternative in U-3.
- **Active and closed candidates:** a candidate enters the one review queue on
  its first qualifying detection. Moderators may review its evidence immediately
  but do not need to work in real time. The automatic 15-minute no-detection gap
  changes the same candidate from active to approval-ready. A candidate supported
  only by confirmed detection-level Signals follows the same lifecycle and
  exposes those records before **Publish bout**.
- **Encoding for M2-A and dedicated views follows §4.0:** ordered outline-color
  segments encode the species/source tag set, solid/dashed/dotted outline encodes
  reporting source, and the corner glyph encodes moderation state. Species/source
  lane projections use their lane color plus `+N` and remain linked by
  `annotation_id`; reporter-grouped and spectrogram markers use the segmented
  outline. These semantics are consistent in M2-A, dedicated alternatives, and
  the phone layout. M1's optional state fill remains confined to its lower
  swimlane markers.

### Option S1 — Time-rail triage inbox (list-centric)

Left: chronological, keyboard-navigable list of unmoderated annotations grouped by time.
Right: detail pane with the 2-minute contextual spectrogram, audio, tag editor, and
Confirm tags / Change labels. Optimized for **backlog throughput**.

![Option S1 wireframe: chronological keyboard-driven annotation inbox with a selected-item detail pane, 2-minute contextual spectrogram, boundary-fit controls, scoped playback, tag editor, and batch actions](images/annotation-triage-inbox.svg)

#### S1 strengths

- Fastest per-item throughput; familiar inbox pattern; excellent keyboard flow.
- Great for clearing historical backlog (KPI 2).

#### S1 tradeoffs

- Weak spatial/temporal context; clustering and overlaps are hard to see.
- Zoom is per-item, not continuous.

### Option S2 — Continuous timeline scrubber (timeline-centric) — dedicated alternative

A horizontally scrolling spectrogram at a 2-minute default viewing span; annotations are selectable
blocks. Here, **horizontally scrolling** means that time moves beneath a fixed,
centered playhead. Dragging the spectrogram, overview window, or scrubber changes
the timestamp under the center marker; tapping an annotation recenters its time
under that marker. During playback, the spectrogram advances behind the centered
playhead; the position marker does not drift across the viewport. Zoom changes the span around
the same centered timestamp. Select one/many → listen → tag → confirm inline.
Confirmed blocks show a checkmark and remain authoritative when later
reporter/model proposals arrive.

```text
[◀ 08:02:30 ───── | 08:03:30 | ───── 08:04:30 ▶]   zoom: 10s · [2m] · 15m · 1h
spectrogram ░▒▓ annotations: ┌□┐unmoderated  ┌✓┐confirmed  ┌×┐false positive  ┌?┐needs confirmation
select ▢▢  →  ( Listen )  ( Label: transient ▾ )  ( Confirm tags )  ( Change labels )
```

![Timeline scrubber mechanics: overview window to move/resize the span, spectrogram moving beneath a fixed centered playhead, zoom around that timestamp, and selection persisting while scrubbing](images/signal-approval-scrubber.svg)

#### S2 strengths

- Shows the whole parent minute plus symmetric context without requiring a pan.
- Strong temporal context; batch-confirm consecutive annotations; overlaps visible.
- Same timeline/audio/spectrogram components as the Workbench (reuse).

#### S2 tradeoffs

- Per-item confirmation can be slower than S1 for a huge undifferentiated backlog.
- Needs a good multi-select and "confirm selection" affordance.

### Option S3 — Proposed-vs-confirmed compare view (reconciliation)

Two aligned tracks: **model-proposed** annotations above, **human-proposed** below;
the moderator confirms the correct interpretation or writes a corrected tag.
Emphasis on reconciling proposed tags for the same annotation.

#### S3 strengths

- Best for disagreements and for generating clean **model-eval / retraining**
  labels (§9).
- Makes reconciliation between proposed annotation tags explicit.

#### S3 tradeoffs

- Narrower use; heavier UI; overkill for simple, uncontested confirmations.

### Annotation-tagging views — comparison

| Option | Time-first / zoom | Throughput | Context | Model-feedback value | Build cost |
| ------ | :---------------: | :--------: | :-----: | :------------------: | :--------: |
| M2-A inline tagging | **Strong** | Medium-High | **Highest** | Medium | Low-Med |
| S1 triage inbox | Partial | **High** | Low | Medium | Low‑Med |
| S2 timeline scrubber | **Strong** | Medium‑High | **High** | Medium | Medium |
| S3 compare view | Strong | Medium | High | **High** | High |

**Recommendation:** build **M2-A as the primary v1 annotation-tagging workflow** within the
Moderator Workbench. Consider **S1** later for backlog blitzes and **S3** for
focused reconciliation; S2 is the dedicated-surface alternative if inline M2
cannot meet throughput needs. Every option sends annotation decisions through
`POST /api/v1/annotation-reviews` (§7.1); detection-only decisions continue to
use `POST /api/v1/detections/{id}/reviews` and are never translated into
annotation reviews.

### Responsive phone view (all options)

The annotation-tagging workflow has a phone layout so moderators can clear unmoderated annotations
away from a desk. For M2-A this is the Workbench's stacked mobile layout, not a
separate destination. It is not a shrunk desktop grid; it is a single vertical
flow that keeps the same review contract.

![Bout annotation-tagging console — phone layout: zoom presets, tap/drag multi-select, checkbox selection list, scoped listen, and a sticky Confirm-all / Change labels bar](images/signal-approval-console-mobile.svg)

1. A compact header shows node, "live" state, and the pending count.
2. Zoom presets (10 s / **2 min** / 15 min / 1 h) sit above a pinch-zoomable,
   scrubbable spectrogram; tapping a block selects it.
3. **Multi-select is first-class on phone:** tap blocks, drag a marquee, use the
   checkbox list, or "Select all in view." A selection summary shows the count and
   time span.
4. **Listen** plays the selection (scoped or sequential); a single **Tag** applies
   to all selected.
5. A **sticky bottom bar** exposes **Confirm tags (N)** and **Change labels**;
  either action confirms the whole selection; members with a prior terminal
  decision require an explicit audited revision and are never silently skipped.
6. Candidates that become approval-ready update the queue badge without losing
  the current selection, zoom, or playback position.

Minimum target 390 CSS-pixel viewport; controls ≥ 44×44 CSS px; horizontal
scrolling limited to the time-aligned spectrogram. Desktop and phone operate on the
same annotations and server state (mirrors bout-spec §8.2).

### Annotation-tagging flow

```mermaid
flowchart LR
  I[Detections and optional ~3-second annotations<br/>arrive from reporters/models] --> U{Child annotations?}
  U -- yes --> A[Review annotation units]
  U -- no --> D[Review 60-second detection unit]
  A --> E[Evidence available in active bout]
  D --> E
  E --> R{Moderator evidence action}
  E --> Q[15-minute gap closes;<br/>same candidate becomes approval-ready]
  R -- confirm existing tags --> C[Confirmed Signal + authoritative<br/>membership unchanged]
  R -- change label/species --> K[Corrected + authoritative]
  C -. if annotation origin is model .-> F[(Model-feedback store)]
  K --> B[Recompute affected bouts]
  K -. if annotation origin is model .-> F
  C --> P[Publish closed candidate separately]
  B --> P
  Q --> P
```

---

## 7. Proposed data-model additions

These extend the canonical relational model in **bout-spec §6b.2**. All additions
are UTC, append-only where they carry decisions, and preserve provenance.

### 7.1 Annotation persistence, provenance, and moderation state

Add an explicit, queryable state to each approximately 3-second annotation.
One-minute detections do not receive this annotation moderation state. Detection-
only records retain their existing detection-level review state and append-only
review history; implementations MUST expose that state through the fallback in
§5.4. Prefer a dedicated column plus an append-only review history over a mutable
flag for annotations.

| Field | Table | Type | Purpose |
| ----- | ----- | ---- | ------- |
| `detection_id` | `annotations` | FK → detections, required | Parent observation. For moderator-created intervals, references a moderator-authored detection sharing the selected media asset; it never references a model's or another reporter's detection. |
| `origin` | `annotations` | enum | `reporter` \| `model` \| `moderator`; records how the interval entered the system independently of its current labels or moderation state. |
| `created_by` | `annotations` | FK → user, nullable | Required for `origin = moderator`; identifies the moderator who drew the interval. Null is allowed for imported/model intervals whose actor is represented by the parent detection's `reporter_id`. |
| `created_at` | `annotations` | timestamptz | Creation time, distinct from the acoustic interval time. |
| `idempotency_key` | `annotations` | string, nullable unique | Required for moderator-created intervals so a retried Save cannot duplicate the interval or its generated parent. |
| `revision` | `annotations` | bigint | Starts at `0` and increments once for each committed annotation review; compared with `expected_revision` to prevent lost updates. |
| `moderation_state` | `annotations` | enum | `unmoderated` \| `confirmed` \| `false_positive` \| `needs_confirmation`. Drives the corner glyph. Label corrections remain append-only `annotation_reviews` actions rather than a display state. |
| `annotation_confirmed_tags` | new join table | rows | `{annotation_review_id, annotation_id, tag_id}`. The rows attached to the latest review are the authoritative set a moderator affirmed; supports multiple simultaneous species/sources and makes prior sets reconstructable. |
| `annotation_proposals` | new append-only table | rows | `{id, annotation_id, reporter_id, confidence, created_at}`. Stores source proposals without rewriting the annotation or a moderator decision. The current proposal before confirmation is the newest row by `(created_at, id)`. |
| `annotation_proposal_tags` | new join table | rows | `{proposal_id, tag_id}`. Stores each proposal's complete normalized tag set; model feedback snapshots these values rather than reading mutable detection tags. |

`media_asset_id` is not duplicated on `annotations`: it resolves through
`annotations.detection_id → detections.media_asset_id`. The annotation's absolute
interval is `detections.timestamp + start_offset_s/end_offset_s`. Both offsets
MUST fall within the parent detection duration and its referenced media asset.

**Create operation.** `POST /api/v1/annotations` accepts `feed_id`, absolute UTC
`start`/`end`, tag ids, and an `idempotency_key`, plus an optional
`media_asset_id`. When it is absent, `feed_id` plus `start`/`end` is the complete
feed-segment locator and the server pins that interval from the feed archive. The server derives
`created_by` from the authenticated moderator and resolves that user to the
moderator's reporter identity; neither provenance value is accepted from request
JSON. In one transaction the server:

1. resolves an existing `media_assets` row containing the complete interval or
  creates one with stable `audio_uri`/`spectrogram_uri` values;
2. finds or creates a detection for the moderator's reporter identity and that
  media window, setting `reporter_id` to the moderator reporter,
  `media_asset_id` to the resolved asset, `source_system = orcasite`, and
  `confidence = null`;
3. creates the child annotation with `origin = moderator`, `created_by`,
  `created_at`, relative offsets, and the request idempotency key; and
4. writes its initial `annotation_reviews` confirmation and
  `annotation_confirmed_tags`, then recomputes affected bouts as the same atomic
  save described in §8.

This path applies whether other model/human detections share that media window or
no detection exists there. Existing source detections and their child annotations
remain unchanged. If the server cannot resolve or pin media containing the whole
interval, the transaction fails and no detection, annotation, review, or bout
change is committed.

**Annotation review operation.** `POST /api/v1/annotation-reviews` is the single
write endpoint for annotation confirmation, label changes, false-positive
classification, and deferral in M2-A and S1-S3. Its body contains a request-level
`idempotency_key` and a non-empty `items` array. Each item contains:

- `annotation_id`;
- `expected_revision` for optimistic concurrency;
- `action`: `confirm` | `change` | `false_positive` | `needs_confirmation`;
- `tag_ids`: the complete resulting tag set, including unchanged simultaneous
  species/sources; and
- `override_reason`, required when revising a prior terminal decision
  (`confirmed` or `false_positive`); and
- optional `comment`.

The server derives `actor` and `at` from the authenticated moderator and server
clock. A one-item request and a multi-selection use the same endpoint and
semantics. Re-confirming an already-confirmed annotation with an identical tag set
is rejected as a no-op. Before writing, the server validates authorization, every
annotation and tag id, every expected revision, required override reasons, a
non-empty controlled-vocabulary replacement tag set for `false_positive`, and the
complete post-change bout set for actions that alter tags. Under the v1 append-only
baseline, any moderator authorized to review annotations may revise a prior
terminal decision when `override_reason` is present. It then commits one `annotation_reviews`
row per item, the corresponding `annotation_confirmed_tags`, eligible
model-feedback rows under §7.4, affected bout recomputations, and `change_events`
in one database transaction. A selected human- or moderator-originated annotation
requires no `model_feedback` row and does not make the batch incomplete. A
confirmation of an unmoderated or deferred annotation still changes its
moderation state, creates the review and confirmed-tag rows, and increments its
revision. Because its tag values are unchanged, it does not recompute bouts or
emit a boundary-change event.

The batch is all-or-nothing: one invalid, stale, or unauthorized item rejects the
whole request with no writes. A revision conflict returns `409 Conflict` with
per-item problem details; validation failures use the shared API error envelope.
Replaying a completed request-level `idempotency_key` returns the original result
without adding review rows. The response returns each annotation's new revision
and moderation state plus previews/identifiers for every affected bout.

`POST /api/v1/annotations` invokes this same review command internally for the
initial confirmation of a moderator-created interval, within the creation
transaction described above. Detection-only records have no annotation id and
therefore use `POST /api/v1/detections/{id}/reviews`; a request MUST NOT mix
detection and annotation review units.

`annotation_reviews` (append-only): `{id, annotation_id,
prior_state, new_state, prior_tag_ids[], new_tag_ids[], actor, at, comment,
override_reason}`. The current state and authoritative tag set come from the
newest review and its `annotation_confirmed_tags` rows; `actor` and `at` provide
the confirmation identity/time, and history is never mutated.

> A confirmed annotation is not restricted to one species/source. For example,
> one approximately 3-second interval may be authoritatively tagged with both
> `srkw` and `vessel` and contribute to two overlapping bouts. A correction replaces the
> applicable member of the confirmed tag set rather than overwriting the whole
> set (for example, `{orca, vessel}` → `{seal, vessel}`).

<!-- Separate implementation notes. -->

> Existing detection-level reviews are surfaced only for detection-only records
> and are not migrated into annotation decisions: a 1-minute review cannot
> reliably determine the state or tags of each approximately 3-second interval.
> Annotation state is populated from annotation-specific evidence or moderator
> review. A false positive still carries the appropriate non-target/source label
> for training and provenance.

<!-- Separate implementation notes. -->

> **Moderator editing and confirmed authority (§5).** A moderator confirmation or
> correction writes `annotation_reviews` and the corresponding
> `annotation_confirmed_tags`; it never rewrites source evidence from a reporter or
> model. Group edits create one auditable review per selected annotation. Every
> authorized moderator may revise a confirmed or false-positive annotation with
> an `override_reason` in the v1 baseline. A stronger API hard lock is not part of
> this baseline; see U-3.

### 7.2 Species assignment & multi-bout membership (for the species-first overlay)

Bout membership is projected in two stages. First, derive each annotation's
**effective species/source tags** from its latest authoritative
`annotation_confirmed_tags`; an annotation with no moderator review uses its
current proposed tags. Then derive the parent detection's applicable tag set:

- when the detection has child annotations, use the union of their effective
  species/source tags; the parent detection's broad/proposed tags do not add an
  independent membership after annotation-level evidence exists; and
- when the detection has no child annotations, use the tags from its current
  detection-level review (§5.4).

For each applicable tag, the parent detection appears once in the corresponding
single-source candidate/published bout. Thus `{srkw, vessel}` on one annotation,
or across two annotations under one parent, puts that detection into both bouts
without cloning it. Removing `srkw` from one annotation removes the parent from
the SRKW bout only when no other child annotation under that parent still has an
effective `srkw` tag; its vessel membership is unaffected.

| Field | Table | Type | Purpose |
| ----- | ----- | ---- | ------- |
| Species/source lanes | derived from `annotation_confirmed_tags` (or proposed tags before confirmation) | projection | An annotation appears in every applicable species/source lane (Option M2); each projection carries the same `annotation_id`, uses its lane color plus `+N`, and participates in linked, de-duplicated selection. An annotation with no applicable tag renders in "Unassigned." This is not a singular stored field. |
| `candidate_bout_detections` / `bout_detections` | existing join tables | rows | Canonical bout evidence and the only membership used for boundary clustering. The same `detection_id` may occur once in each applicable single-source bout. |
| `annotation_bout_membership` | new join table | rows | `{annotation_id, bout_id, role}` — traces which child evidence caused its parent detection to qualify for each bout. It supports explanation and lane projection but never supplies timestamps to the 15-minute boundary algorithm. |

### 7.3 Change tracking & watchers (for alerts)

| Field / table | Type | Purpose |
| ------------- | ---- | ------- |
| `change_events` (new) | rows | `{id, entity_type: bout\|annotation, entity_id, change_type, from jsonb, to jsonb, actor, at, affected_bout_ids[], operation_payload jsonb}`. For annotation label changes, `from`/`to` are complete `{tag_ids: [...]}` snapshots; for bout changes they are complete prior/result `{id, start, end, type, tag_ids, detection_ids}` snapshots. `operation_payload` is validated against the schema for `change_type`; it is `null` for changes needing no operation-specific fields. One row per annotation-label or bout change that may affect bout membership; drives change alerts. Annotation confirmation is audited in `annotation_reviews` but does not trigger bout recomputation. |
| `bout_watch` (new) | rows | `{bout_id, user_id, reason: working\|worked, created_at}`. Populated when a moderator claims/edits a bout, and retained after publish so prior reviewers can be alerted later. |
| `bout_lineage` (new) | rows | `{predecessor_bout_id, successor_bout_id, reason: split\|automatic_merge\|force_merge, change_event_id}`. One row links each retired predecessor to each applicable successor. Split and merge results always receive fresh bout IDs while retired source IDs remain traceable for audit and links. |

For `change_type = force_merge`, `entity_type` is `bout`, `entity_id` is the
resulting bout id, and `operation_payload` has the required shape:

```json
{
  "reason": "moderator-entered non-empty text",
  "source_bout_ids": ["bout-a", "bout-b"],
  "result_bout_id": "bout-c",
  "gap_duration_s": 1140,
  "source_timeframes": [
    { "bout_id": "bout-a", "start": "...", "end": "..." },
    { "bout_id": "bout-b", "start": "...", "end": "..." }
  ],
  "result_timeframe": { "start": "...", "end": "..." }
}
```

`source_bout_ids` contains at least two unique ids in chronological order;
`affected_bout_ids` contains those ids plus `result_bout_id`. The server computes
`gap_duration_s` from the persisted adjacent source timeframes rather than
accepting it from the client. The transaction creates one `bout_lineage` row per
source with `successor_bout_id = result_bout_id`, `reason = force_merge`, and the
same `change_event_id`. Database/API validation requires the payload ids,
`entity_id`, affected ids, and lineage rows to agree, so the override can be
reconstructed from the event even if bout boundaries later change.

### 7.4 Model-feedback fields

`model_feedback` is a comparison between a model proposal and the moderator's
terminal decision, not a general audit table. Create one row for a reviewed
annotation only when `annotations.origin = model` and the review action is
`confirm`, `change`, or `false_positive`. (`false_positive` is stored as
`decision = change` with the moderator's non-target/source tag set.) Do not create
a row for `needs_confirmation`, because there is no terminal training label, or
for `origin = reporter|moderator`, because no model proposal, reporter, label set,
or confidence exists to compare. Those annotations remain fully represented by
`annotation_reviews` and `annotation_confirmed_tags`.

| Field | Table | Type | Purpose |
| ----- | ----- | ---- | ------- |
| `model_feedback` | new table | rows | `{id, annotation_id, annotation_review_id, model_reporter_id, model_tag_ids[], model_confidence, moderator_tag_ids[], decision: confirm\|change, at, export_state}`. `annotation_id` MUST reference an `origin = model` annotation; `annotation_review_id` uniquely links the terminal moderator decision so retries cannot duplicate feedback. Stores the complete proposed and moderator tag sets for retraining/evaluation. |
| `export_state` | `model_feedback` | enum | `pending` \| `exported` \| `excluded`. Lets ambiguous/unknown items be withheld from training. |

For an atomic batch, feedback cardinality is therefore zero or one row per
selected annotation, determined independently for each item by the rule above.
All required feedback rows commit in the same transaction as their annotation
reviews; failure to create any required model row rolls back the whole batch.
The absence of an inapplicable row for human or moderator evidence is valid and
does not roll back the batch.

---

## 8. Change propagation & alerts

Confirming an annotation affirms its existing tags. It does **not** change the
annotation's bout membership or boundaries, and therefore never merges or grows a
bout. A label change first recomputes the parent detection's applicable tag set
under §7.2, then reruns bout-spec R1-R5 over the affected parent detections. Child
annotation timestamps and offsets never participate in 15-minute gap arithmetic.
A label change can affect bouts in either direction:

- **Remove a species/source tag from an annotation** → remove its parent detection
  from that source's bout only if no sibling annotation still supports the tag.
  Removing an outermost parent detection may **shrink** the bout; removing an
  interior parent detection may expose a detection gap of at least 15 minutes and
  **split** it.
- **Change the label on an annotation from another bout/source to the label for this
  bout** → its parent detection joins this bout if it was not already a member; that
  detection may fill a former gap so two bouts **merge**, or extend the nearest
  boundary so the bout **grows**.
- **Change species** (humpback → transient) → the annotation leaves one species bout
  and joins another only where the parent's before/after applicable tag sets change;
  **both** bouts' detection membership and boundaries may move.

> **Interior split example.** An SRKW bout has parent detections timestamped at
> minutes 1, 10, and 20. The minute-10 parent is an SRKW member only because one
> child annotation has effective tag `srkw`; none of its sibling annotations do.
> The initial adjacent **detection** gaps are 9 minutes (`10 − 1`) and 10 minutes
> (`20 − 10`), both under 15 minutes. Changing that child's tag to `seal` removes
> the minute-10 parent from SRKW membership. The remaining SRKW detections are 19
> minutes apart (`20 − 1`), so the generator retires the original bout and creates
> two successor bouts. If a sibling annotation still carried `srkw`, the parent
> would remain an SRKW member and no split would occur. The annotation's 3-second
> offset is never used in either result.

Design:

1. Every label change that changes a parent detection's applicable tag set writes
  a `change_events` row with `affected_bout_ids`. Confirmation still writes its
  normal audit and, for model-originated annotations, its eligible model-feedback
  record, but creates no bout-boundary change.
2. Before Save is enabled, the generator computes the complete resulting set of
  candidate and published bouts, including membership, boundaries, type, tags,
  and any split or merge. The UI previews those results alongside the label
  change. If the result cannot satisfy the bout timeframe invariant, Save remains
  disabled; the moderator cannot create an intermediate illegal state.
3. Saving commits the annotation review, membership changes, recomputed bouts, and
  `change_events` audit rows in one transaction. No intermediate boundary state
  is persisted. Updating an already-published bout is audited but does not send a
  subscriber notification or publish a candidate.
  If one bout splits into two, the original bout is retired and both resulting
  bouts receive new IDs; neither child inherits the original ID. A merge likewise
  retires every source bout and creates one new ID. `bout_lineage` links each
  retired predecessor to its new successor or successors.
4. **Active editors** of an affected bout see an inline banner ("Evidence changed
  — boundary updated") with optimistic-concurrency handling and a before/after
  diff.
5. **Watchers** with `bout_watch.reason = worked` get an inbox/notification badge:
  "A bout you reviewed changed," linking to a diff of before/after. This behavior
  and the proposed table are one recommendation and are adopted or deferred
  together.

### 8.1 Existing-bout correction: orca annotation → seal

A reviewer opens an existing orca bout from the Bouts page or from an "A bout you
reviewed changed" alert. The Workbench opens in **review existing bout** mode and
keeps the bout boundary visible while the species overlay shows each member
annotation. The reviewer selects the misidentified orca annotation and listens to its
exact interval; the details panel shows its current `orca` identification,
reporter/model provenance, confidence, moderation state, and review history.

The reviewer chooses **Change**, selects `seal`, and sees an inline preview before
confirming:

- the annotation moves from the orca lane to the seal lane;
- the annotation leaves the orca bout and joins or seeds the applicable seal bout (potentially resulting in merging two existing seal bouts);
- every affected bout is listed with its current and resulting boundaries; and
- any title, type, tag, split, or merge consequence is called out explicitly.

Confirming writes an `annotation_reviews` entry (`orca` → `seal`), keeps the annotation
authoritative while later reporter/model input remains separate, records model
feedback only when the selected annotation is model-originated, updates every
affected bout to the previewed valid result, and emits one `change_events` row
naming those bouts in the same transaction. There is no second boundary-acceptance
step. Correcting the annotation does not publish a candidate bout or notify
subscribers; publication remains the only notification gate (bout-spec §5.2.1).

### 8.2 Existing-bout correction: unidentified opening annotations → ship

A reviewer opens an existing bout and zooms to the small unidentified annotations at
its beginning. They drag a marquee over the run, then add or remove individual
annotations by Shift-click on desktop or checkboxes on phone. The selection summary
shows the count, total time span, current tags and moderation states. **Listen to
selection** plays the intervals in sequence so the reviewer can verify that the
whole group has the same ship source before changing it.

The reviewer chooses **Change selection**, identifies the source as **ship**
(canonical tag `vessel`), and receives one confirmation screen for the batch. The
screen lists every annotation that will change and requires an `override_reason`
for each item with a prior terminal decision; an unauthorized or stale item rejects the whole
batch and is never silently skipped. The preview moves the selected annotations into a vessel lane and
shows the resulting anthrophony/vessel bout. If those annotations anchored the
original bout's start, it also shows that boundary moving to the first remaining
annotation and displays both bouts before and after the change.

Confirming creates one append-only `annotation_reviews` record per selected annotation,
corresponding `change_events`, eligible `model_feedback` rows only for
model-originated selections, and an audit link that groups them as one batch
action. Human- and moderator-originated selections produce no model-feedback row.
The same transaction updates every affected candidate or published bout to its
previewed valid state. If any required result cannot be validated, none of the
batch is saved. The update never publishes a candidate
bout or sends a subscriber notification.

```mermaid
stateDiagram-v2
  [*] --> unmoderated
  unmoderated --> confirmed: confirm existing tags
  unmoderated --> false_positive: change to non-target/source label
  unmoderated --> needs_confirmation: defer for more evidence
  needs_confirmation --> confirmed: confirm tags
  needs_confirmation --> false_positive: change to non-target/source label
  confirmed --> confirmed: change label (audited)
  confirmed --> false_positive: change to non-target/source label (audited)
  confirmed --> needs_confirmation: defer for more evidence (audited)
  false_positive --> confirmed: revise label (audited)
  false_positive --> needs_confirmation: defer for more evidence (audited)
  false_positive --> [*]
  confirmed --> [*]
```

### 8.3 Activity resumes after closure: conditional force merge

Ordinary bout creation exposes the active bout for moderation as soon as qualifying
activity is detected; it does not wait for the 15-minute no-detection gap. That gap
only determines when the bout closes. If relevant activity resumes after closure,
the new activity immediately starts a separate bout under bout-spec R1.

**Force merge bouts is not part of the v1 baseline unless bout-spec R1 is amended
with the normative exception described in U-5.** If adopted, the Workbench may
offer this action when a moderator determines that adjacent bouts on the same node
and with the same species/source represent one continuous real-world event.

Force merge is not normal boundary recomputation: it is an intentional exception
to the 15-minute gap invariant. The confirmation preview shows both source bouts,
the intervening gap, and the resulting combined timeframe. The client submits
only the ordered source bout ids and a non-empty moderator reason; the server
computes the gap from persisted source timeframes. Saving atomically creates the
merged bout, retires and lineage-links the source bouts, and appends one
`change_type = force_merge` `change_events` row with the typed payload in §7.3.
The authenticated actor and server time are stored on the event; its payload
preserves the moderator reason, ordered source ids and timeframes, server-computed
gap duration, and resulting bout id/timeframe. The same transaction writes the
corresponding `force_merge` lineage rows. It does not notify subscribers or
republish automatically. Different-node or different-species/source bouts cannot
be force-merged.

---

## 9. Recent high-value bouts

Moderators need a quick way to revisit strong examples so they can calibrate what
clear, useful annotations look and sound like. Add a **Recent bouts** view that uses
the same bout cards, timeline, audio player, and species/source overlays as the
Workbench. It is a review and learning surface, not another moderation queue.

### 9.1 Ranking and filters

The default **High value first** order is deterministic and transparent:

1. Bouts carrying the moderator-assigned `high-value` tag appear first.
2. Within the high-value group, bouts are ordered newest first.
3. Remaining bouts follow, also newest first.

Model confidence does not automatically make a bout high value and is not folded
into a hidden quality score. Moderators can switch to **Newest first** and filter
by node, species/source, date, moderator, and `high-value` status. Each result
shows the bout title, node, time, tags, contributing annotation count, reviewer, and a
compact spectrogram with a play action.

### 9.2 Mark high value while reviewing

While creating or updating a bout, a moderator can toggle a star action labeled
**High Value** in the bout details panel. Turning it on adds the canonical
`high-value` bout tag; turning it off removes that tag. This remains a pending
edit until the moderator chooses **Save bout changes**, so they can adjust several
fields or continue inspecting the current spectrogram range before committing.
Save applies the pending metadata together and appends `{who, when, field: tags,
from, to}` to the bout's `review_history`. It preserves the current zoom,
playhead, and selection. Reporters and models may see the saved tag but cannot set
or remove it.

The control includes a short optional note for why the example is valuable, such
as clear signal-to-noise ratio, representative call type, unusual species, or a
useful correction example. The note is displayed in the Recent bouts view but is
not part of ranking. Store it as nullable `bouts.high_value_note`; saving a change
adds a `review_history` entry for that field alongside the tag change. Applying or
removing `high-value` does not publish the bout or notify subscribers.

![Moderator Workbench editing an existing bout with the High Value star enabled, an optional quality note, explicit Save bout changes action, and publish-independent behavior](images/mark-bout-high-value.svg)

### 9.3 Sample recent-bouts view

![Recent bouts desktop view with high-value-first ranking, filters, spectrogram previews, playback, quality notes, and transparent ordering](images/recent-high-value-bouts.svg)

Selecting a row opens the existing bout in read-only review mode by default.
Moderators can choose **Edit bout** to enter the correction workflow in §8.1–§8.2,
while other viewers can inspect and play the confirmed annotations without changing
them.

![Recent high-value bouts mobile view with stable card dimensions, large playback controls, filters, and the same explicit ranking](images/recent-high-value-bouts-mobile.svg)

## 10. Open issues

Add these to bout-spec **Appendix A** if adopted.

| ID | Open question | Why it matters / proposed direction |
| -- | ------------- | ----------------------------------- |
| U-1 | **Atomic recompute failure handling:** if the system cannot compute a valid post-change bout set, what recovery detail should the moderator see? | Direction decided: preview the automatic result and disable Save until the label change and all affected bouts can commit atomically. Confirmation alone never changes a boundary. Open detail: error/retry presentation. |
| U-3 | **Confirmed-annotation write policy:** is append-only authority sufficient (reporter/model input becomes a separate proposal and moderators revise through audited actions), or should the API additionally hard-lock confirmed records? If hard locking is adopted, who may override it and with what precedence? | The baseline in this proposal uses append-only authority to match the bout spec's guidance to avoid hard locking. A padlock/`locked` field MUST NOT be implemented unless stakeholders choose the stronger alternative and define moderator override, takeover, and audit behavior. |
| U-4 | **What is fed back to models, and when?** For model-originated annotations, which confirmed/corrected labels are exported, in what format and cadence? Should human- or moderator-originated annotations later enter a separate generalized training-example pipeline? | Drives retraining quality (KPI 3). Proposed v1: export only `model_feedback` where `export_state = pending` and decision ∈ {confirm, change}. Human/moderator evidence, including moderator-created boundary-adjacent intervals, remains in annotation audit storage and is not represented as model feedback. |
| U-5 | **Force-merge policy:** should the audited force-merge exception have a maximum gap or require a second moderator? | Queue timing is decided: KPI1 requires the active candidate to enter moderation immediately, without waiting for its 15-minute closing gap. Resumed same-node, same-species/source activity creates a new bout immediately. Force merge remains disabled in the v1 baseline unless bout-spec R1 is amended with a narrow normative exception (§8.3). |

## 11. Recommendation summary

- **Moderator overlay:** make the **Reporter ⇄ Species pivot toggle** (§4.0) a
  baseline control in the main interface; retain the normative **Reporter**
  default and make the **species pivot / M2 (species-first swimlanes)** the primary
  mixed-species mode, layered with **M3's minimap** for change alerts; adopt a single moderation-state
  vocabulary (`□` unmoderated, `✓` confirmed, `×` false positive, `?` needs
  confirmation) shared everywhere. M1's optional state fills are not part of the
  shared baseline.
- **Moderator editing:** integrate single-annotation and group tagging directly into
  the §4 overlay; preserve timeline context and require audited revisions for
  confirmed annotations. Preserve a detection-level fallback for records with no
  child annotations so human detections can become tagged Signals and support
  bout approval. A separate non-moderator historical-data overlay is out of scope
  for v1.
- **Bout evidence review:** build **M2-A inline annotation tagging** in the main
  pivotable overlay as the primary v1 workflow. The Reporter pivot loads by
  default; M2's species pivot is the recommended mixed-species task view, and the
  tagging action bar remains available in either pivot. The workflow is time-first,
  with a symmetric 2-minute contextual viewing span, dedicated start/end boundary framing, zoomable,
  select/listen/tag/confirm, **first-class multi-select
  tagging**, authoritative annotation confirmation, separate bout publication,
  immediate queue intake with closure-gated publication, and a responsive phone layout with a sticky Confirm
  tags / Change labels bar. Keep S1–S3 as optional specialized
  views, not required navigation.
- **Data:** add per-annotation `moderation_state` + confirmation fields, the
  many-to-many `annotation_confirmed_tags` relation, `annotation_bout_membership`,
  `change_events`, `bout_watch`, and `model_feedback`, extending bout-spec §6b.2;
  do not add a `locked` field unless U-3 resolves in favor of hard enforcement.
- **Loop safety:** preview label-change effects, then atomically update all
  affected bouts so the timeframe invariant holds. If U-5 is adopted, permit the
  audited same-node, same-species/source **Force merge bouts** exception;
  alert active editors and prior watchers, and feed a clean supervised set back
  to models.
- **Quality calibration:** add a Recent bouts view ordered by moderator-assigned
  `high-value` first, with newest-first ordering inside each group and no opaque
  confidence-derived quality score.
