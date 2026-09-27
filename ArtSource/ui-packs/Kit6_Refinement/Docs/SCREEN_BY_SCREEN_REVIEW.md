# GameSim — UI review of the remaining menu screens

## Scope and evidence

This pass covers the current screenshots of the conversation-availability panel, Who is where, Houseguests, The vote, and the private Diary Room. It also includes companion layouts for a private reflection confirmation, a houseguest profile, and a knowledge-limited relationship view. The latter three are extensions of screens discussed earlier, not claims that additional mechanics already exist.

The supplied images—not source-code inspection—are the evidence for this review. A screen that looks wrong does not prove a particular controller is broken. Suggested new labels, filters, disclosures, and dimensions are design proposals; retain existing tested captions in the first skin-only pass.

**Frozen project boundaries:** keep URP 17.6, the pinned baked-night solution and `Lighting_TheEpisodeSceneShipsItsBakedNight`; keep uGUI; keep existing `UiTheme` authoritative; keep the normal full HUD with Live Feed, Recent Events and the House Vibe / weekly-information widget. Do not turn on compact HUD to make this pass look less crowded. These assets do not change scenes, shaders, materials, lighting, or game rules.

The previous illustrated style guide contained approximate colors and sample text. It is not a replacement specification for the actual project's semantic palette. Likewise, generated mockups that invent mood, ballots, jury knowledge, or social rewards are not behavior specifications.

## What has improved already

The current build has a recognizable left navigation rail, a bottom cast strip, neutral mood labels, readable phase/status bars, and a front-facing Diary Room camera. Keep these gains. The remaining problem is not absence of a global theme: it is applying one generic translucent scrolling-window pattern to different information tasks.

There are three page types to implement deliberately:

- **Contextual interaction:** a compact message near a person, with the world still usable.
- **Notebook page:** an opaque, readable information workspace with stable heading, filters, content area and footer.
- **Private cinematic view:** a character camera plus a readable private-record panel, with decision confirmation separated from read-only browsing.

## 1. Conversation unavailable — Emma panel

Source: `02ca5c33-735b-4fe8-afd9-7f20fc4425c5.png`.

### Current reading

The drawer is tall relative to its short message. Most of its lower half is unused. The content repeats that the ceremony must finish. `Neutral -9 · 5 actions left` mixes a mood, a signed relationship number, and a global resource in one phrase. The world remains visible, which is worth retaining. The foreground avatar/prop intersection and bright lamp are separate scene/animation issues; changing UI sprites cannot fix them.

### Proposed composition

A content-sized card with an 80–96 reference-unit portrait, the name and pronouns, then distinct chips for **Mood: Neutral** and **Your trust: −9**. Keep the short in-character quote. Add one factual availability line: **Conversation unavailable during this ceremony.** Do not add a fictitious action price or spend a social action when the interaction is refused.

Use a card around 500–560 units wide. Its height should follow the content within a safe-area clamp, rather than stretch to the viewport height. Prefer a position beside the interaction or in a bottom corner, not in front of a face. Close remains the same existing command. The global `Go to episode screen` action can remain reachable in the navigation; the small card does not need a second large route list.

When the interaction becomes available, the existing gameplay controller—not a UI timer—must enable the available conversation choices. Do not invent a displayed countdown if the game has none. A non-modal card must not consume all mouse input; a genuinely modal choice must block underlying input intentionally.

### Assets

`card_fill`, `card_edge_rest`, `portrait_mask`, `portrait_ring`, `pill_fill`, `ic_chat`, `ic_lock`, `speech_tail` (optional). Apply focus styling to a focused action only. No permanent cyan halo on the whole unavailable panel.

### Acceptance

No duplicated global action count; no blank lower half at the normal text scale; no negative trust value presented as the mood; moving, dismissing, or receiving an unavailable response does not mutate the game state. Preserve the exact tested close caption during the skin-only pass.

## 2. Who is where — room directory

Source: `aa64df88-3c70-4f58-a5e8-3a74ffc0bf8c.png`.

### Current reading

Every row has a bright cyan glow, including empty rooms. The room content is tiny relative to the width and height of each row. Avatar dots do not provide readable names. The user must scroll past large, mostly empty rectangles to understand a small number of locations.

### Proposed composition

Use two columns of room cards, plus an optional narrow area for unknown locations at the 1920×1080 reference size. Inside each room card place a room glyph, human-readable room label, known-occupant count, and portrait/name tokens. Put empty-room cards lower or collapse them via an explicit filter; do not silently change which rooms exist.

A useful starting size is 576×202 with 24 units between cards. These are proposals, not a reason to overwrite your Canvas Scaler. At narrower effective widths, the layout should become one column or an approved dense list rather than shrinking all type.

**Knowledge boundary:** `No known occupants` and `Empty` are not automatically interchangeable. If the current data is an authoritative permitted occupancy list, `Empty` is valid. If the player only has partial information, use `No known occupants` or `Location unavailable`. Only show a “last seen” timestamp when it is actually recorded. Do not add hidden NPC coordinates, exact activities, or occupancy capacities from guesses.

The preview's room assignments are explicitly illustrative fixtures. They must not be copied into production state. It preserves the idea of two people in a bedroom and three in a living room without claiming to reconstruct each miniature portrait's identity from the screenshot.

### Assets

`card_fill`, `card_edge_rest`, `selection_stripe`, `portrait_mask`, `pill_fill`, `ic_bed`, `ic_sofa`, `ic_kitchen`, `ic_gamepad`, `ic_location`, `ic_location_unknown`, `room_no_known_occupants`.

### Acceptance

Resting empty rows do not glow. Every permitted located houseguest appears once. Unknown locations have their own state. Counts reconcile with the active season roster and knowledge rules, not with the number of visible thumbnails after filtering. Room selection may reveal an existing inspect/follow action; do not imply teleportation or a new click-to-reveal power.

## 3. Houseguests directory and profile

Source: `d311f442-f578-41d9-b622-987b2fe025f4.png`.

### Current reading

The list compresses name, active/jury state, and relationship number into one sentence. Metadata is much smaller than the surrounding space permits. The `Overview` navigation row is highlighted while the content says `HOUSEGUESTS`. This is a visible mismatch to investigate, not proof of a particular routing bug.

### Proposed composition

Keep a readable roster list, but create stable columns: **portrait/name**, **season status**, **your trust**, and **profile action**. At 1920×1080, a row height around 76–88 with a 56–72 portrait supports readable text without turning the screen into giant cards. Use a single focus/selection cue on the active row. Place Search and All / Active / Jury controls above the list only if those filters are part of the approved presenter change.

The selected navigation state and page title must derive from the same current view identifier. Do not keep Overview highlighted as a generic notebook fallback. The generic `House activities` row should become a secondary contextual action rather than the headline of every page, while preserving its actual callback.

The preview carries over the visible numbers: Alex +12, Emma −9, Jordan −4, Casey −4, Riley −12, Quinn −24. These are **your** relationship values. They are not automatically the other person's feelings. Do not invent “Hostile”, “Dangerous”, or “Ally” thresholds from signed numbers. A diverging trust bar is appropriate only after the actual score domain and zero semantics are confirmed; otherwise the signed number is clearer and safer.

A selected-row detail page can show the existing archetype, age, occupation, public status and permitted history. Missing competition records are `Not recorded` or absent—not invented zero wins. Do not create a fictional biography to fill a dossier. Use the approved portrait asset or an actual avatar render, with a consistent crop; screenshot crops in these previews are not high-resolution character replacements.

### Assets

`card_fill`, `card_edge_rest`, `card_edge_focus`, `selection_stripe`, `portrait_mask`, `pill_fill`, `ic_person`, `ic_people`, `ic_search`, `ic_filter`, `ic_chevron_right`. The optional meter is separated into `meter_track`, `meter_fill_rect`, `meter_zero_tick`; it contains no drawn value.

### Acceptance

Names are not truncated at the supported text size; Active and Jury are distinct from mood and trust; keyboard focus is visible; the active navigation label matches the page; filtering does not drop the player or juror records incorrectly; pointer hit regions do not change during a skin-only patch.

## 4. The vote — results, privacy and empty states

Source: `6d351c5c-efc1-4153-9eb8-0a6e2430c4ac.png`.

### Current reading

The page is a very large blank window containing “Nobody has voted yet this season.” In the broader supplied captures the HUD is Week 2, 7/8 remain, and another screen labels Jordan Jury. This is a **state/copy consistency question to verify**. The images do not establish whether the cause is a missing ledger, a knowledge restriction, different snapshots, or loading behavior.

### Proposed composition

Separate **recorded eviction results** from **known individual ballots**. Do not place private ballot intentions under an unconditional “How the house voted” title. The UI should never reveal a ballot solely because an omniscient engine can read it.

For the state shown, use a designed empty treatment: a modest ballot/archive illustration, **No vote records available**, and a short explanation that this is the notebook's available information. Keep an existing route such as House activities as a secondary action. Do not show a bright “Refresh” action unless a refresh operation actually exists.

Three states need different wording:

| Backing state | Player-facing copy | UI treatment |
|---|---|---|
| Authoritatively no eviction result yet | No eviction results yet. | Quiet empty illustration |
| Event may have occurred but records/ballots are unavailable | No recorded results available. / Individual votes are not known. | Privacy or record-availability explanation |
| Failed loading or invalid data | Could not load vote records. | Error treatment and a real retry action, if supported |

A populated history entry should show its week/event, the recorded result, the tally only when present, and any permitted known ballots in a disclosure. Do not fill missing tally numbers. Sources and timestamps may be shown only when recorded. Keep jury support and future vote intentions elsewhere.

### Assets

`votes_no_records`, `records_load_error`, `ic_ballot`, `ic_archive`, `ic_lock`, `ic_refresh`, `card_fill`, `card_edge_rest`, `timeline_dot`, `timeline_ring`, `divider_v`.

### Acceptance

The message is driven by an explicit record/knowledge/loading state, not merely an empty list; a record does not expose hidden ballots; Week 2 does not by itself force a invented Week 1 vote result; empty, unavailable and failure are not the same visual state.

## 5. Private Diary Room — overview, memories and decision confirmation

Source: `4a817e7a-96a8-4ab8-8fda-763fe755cb83.png`.

### Current reading

The move to a front-facing camera is a substantial improvement. The right-hand text is still dense and narrow. It puts pending-decision information, implementation qualifications, preparation, persona, jury record and memories in one continuous reading column. The bottom hint still says to walk into the private room while already inside it. The avatar's open-mouth pose and chair geometry are not UI-asset problems.

### Proposed composition

Give the existing character camera approximately 55–60% of the content width and the record panel the rest. Retain the current in-engine view instead of using an unrelated photoreal model. Use three clearly separated sections or tabs: **Your record**, **Memories**, **Pending decision**. These reorganize current information; they do not introduce public confessional mechanics.

Keep the current no-pending state explicit: **No private decision pending.** The summary cards can show the values already present: study preparation 0/5, diary persona Neutral, zero recorded reflections, and recorded jury impression −2 across 1 juror. The last number must always be labeled as a record, **not a vote prediction**. A private impression ledger is not an omniscient survey of jurors' thoughts.

Move the full preparation and ledger qualifications into a keyboard-accessible disclosure, such as How this record works. Preserve them there verbatim from the live controller; do not simplify away important limitations. The screenshot's rule that preparation is used only for weekly simulated HOH/Veto—not precision play or final HOH—still applies. Opening, reading, closing or changing a tab must not grant points, change trust, create reflections, or advance ceremonies.

Use a readable quote panel for the actual memory being viewed. Match the avatar name to the current player; several earlier generated mockups mislabeled portraits, and those errors must not become bindings. Correct the context hint to the active state. Do not keep an “Enter private diary room” instruction on the inside view.

### Confirmation companion

When there is a real pending decision, use a separate review with the answer on the left and exact controller-provided effects on the right. Keep **Confirm private reflection** and **Back to reflection (discard answer)** visible outside scrolling content. Confirm once; do not apply the side effects once per frame or on selection. The supplied companion preview is a layout example based on the previously supplied remorseful-answer wording, not a claim that the latest capture has a pending choice.

### Assets

`panel_fill`, `card_fill`, `pill_fill`, `button_fill`, `button_edge_focus`, `private_no_decision`, `ic_diary`, `ic_note`, `ic_book`, `ic_lock`, `ic_jury`, `ic_info`, `ic_check`, `ic_arrow_back`.

### Acceptance

No state changes from opening/reading; no stale enter prompt; preparation and ledger limitations remain discoverable; only actual pending decisions get confirmation controls; cancel discards and restores the prior view; double-click does not double-commit; the world remains protected by the existing mode/input contract.

## 6. Relationship view — additional companion

This screen appeared in earlier captures. The new companion enlarges the portrait network without inventing friendship/alliance edges. A graph with little known information should look sparse. “Unknown” is not “neutral”, a directed trust link is not mutual, and jury/outside-house state is distinct from an active in-house node.

Use the selected player, known nodes, a clear legend and a side panel for the selected recorded relation. Hide nonexistent edges rather than decorating the screen with invented colored lines. The preview deliberately uses no confirmed-alliance edges.

## Production sequence

1. Skin-only patch: default rows become quiet, surfaces become readable, state icons are separated. Preserve captions, callbacks and RectTransforms.
2. One notebook page at a time: approve a layout diff, then intentionally update the affected rect snapshots. Do not delete or broadly rebase tests.
3. Conversation availability and Diary Room state copy: test unavailable/no-pending paths explicitly.
4. Vote record states: trace the backing data before choosing the final empty copy.
5. Only after these are correct, add short focus/fade motion and optional camera/portrait-stage polish in a separate change.

The main lesson is **format information according to the task**. A compact social message, a location directory, a history ledger and a private decision review should not all look like the same scrollable developer window.
