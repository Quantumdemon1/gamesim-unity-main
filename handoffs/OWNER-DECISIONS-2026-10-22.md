# Owner decisions, 22 October 2026

Binding for every agent working on Gamesim (Claude and Codex). These answer the open questions
from the 21 Oct handoff (`HANDOFF-2026-10-21-codex.md`) against the owner's 15-17 Oct decisions
(`HANDOFF-2026-10-18-claude.md`, section 3). Where the owner delegated a decision ("use your
judgment"), the call made under that delegation is marked as such.

## 1. Push everything; the props and the real-person research go in too (owner)

- Everything that exists only on the PC is committed and pushed: `D:/GamesimConsolidation`
  (HEAD `f1b93b1c` plus its uncommitted changes), the props batch, the All-Star research and
  campaign documents, the Frozen28 reader archive and the handoffs. `PC-PUSH-BRIEF-2026-10-22.md`
  says how.
- The props and the research are stored now and integrated into the game later:
  - the props batch under `ArtSource/props/`, outside `Assets/`, so Unity imports nothing until
    the batch is admitted through the asset pipeline (step 4 of the plan);
  - the research under `Research/AllStars/`.
- Still never committed, because it is someone else's licensed content or local machine state:
  - Asset Store and purchased packs;
  - UMA (`Assets/UMA`, `Assets/UMA.meta`, `Assets/UMAProjectData`) and the HouseGarments assets
    an editor generates from it;
  - `ProjectSettings/ProjectSettings.asset` (it holds the local `GAMESIM_UMA` define), the Inter-*
    SDF fonts, `Assets/_Recovery` and the URP global settings;
  - `Library/`, `Temp/`, `Logs/`, `Builds/` and test evidence trees (XML, logs, captures).
- The repository is public: everything pushed, the research about real people included, can be
  read by anyone.

## 2. PR #26 is merged (delegated)

PR #26 (the verified 18 Oct baseline: EditMode 8073/8073, PlayMode 1273/1273, UMA 90/90, Windows
build, season walk) was merged with a merge commit on 22 Oct as `66a9f377`. `main` now holds it.
PR #25 was already closed as included. Later work lands through new PRs on top of `main`.

## 3. Codex's paperwork stays dropped (owner, reaffirmed)

No hash pinning of sources, indexes or receipts, no sealed programmes, custody ceremonies or
permission documents. A step is qualified by a plain full run of the suites it touches plus one
independent review. Each qualified step is committed and pushed before the next begins; nothing
waits in an uncommitted tree.

## 4. The save gate before schema 29 and role/AP (delegated)

- The owner's 15 Oct old-save acceptance (committed fixture saves per save version and rules
  combination, `OldSaveAcceptanceTests`; `Assets/Plans/OLD-SAVE-ACCEPTANCE.md`) is the save gate,
  and it is done. It must stay green in every full EditMode run, including on the dialogue V2 source.
- Before schema 29 ships:
  1. land the Frozen28 reader through the real save paths, following the landing steps in
     `OLD-SAVE-ACCEPTANCE.md`;
  2. add an ordinary test that calls `TryRecoverBackup` on the old fixtures (today's tests check
     `TryLoad` and backup creation only);
  3. run a record-mode sweep of the owner's real saves, on copies, never the originals.
- Codex's "funded F" historical-parity procedure is not a gate. Its substantive check becomes an
  ordinary regression test in the dialogue work: a public Cite at AP 0 must not deliver a paid
  plea's effect for free, and seasons on the old rules must play as before.

## 5. Schema 29 (delegated)

Schema 29 is claimed by the main line, for the Frozen28 freeze together with the role/AP records,
consistent with the owner's earlier confirmation that no other chat reserves it
(`R2_INTEGRATION_20261018.md`). It comes before the R3/R4 persisted records, not at R5. Any other
session that needs a schema takes the next free number after this claim.

**Addendum, 22 Oct: the dialogue V2 storage is this schema 29 (delegated call, confirmed by the
owner).** The dialogue V2 source (`9d87b59e`, PR #28) already writes schema 29: `dialogueRulesVersion`
and a `dialogue` store (`DialogueState`: role grants, time receipts, veto occasions and dialogue
episodes) on the root, and `conditionalVeto` on each commitment row. The main line accepted that
shape as the schema 29 claimed here, and the owner confirmed it the same day. It is the role,
dialogue and time contract of `ROLE_DIALOGUE_ECONOMY_20261012.md`, not speculative fields. The
Frozen28 freeze is on the real save path: every schema 1 to 28 save goes through
`UpgradeV28ToV29`, which runs `FrozenEpisodeV28.Validate` before it adds the off, empty and null
literals. Shipping seasons stay on dialogue rules 0 with empty storage. Schema 29 freezes when
PR #28 merges; any persisted field after that takes schema 30, including R3/R4 records that need
more than this store. Section 4's gate still applies before PR #28 merges.

## 6. How long a season may take (owner)

The owner: "no game should take longer than 45 minutes with 8 players; with 12, a longer limit
makes sense."

- Read as a target for playing a season: an 8-player season should be playable in 45 minutes or
  less; a 12-player season may run longer, up to about two hours. It belongs in the playtest
  protocol and in the pacing and balance measurements.
- It is not a deadline on test-suite processes. The audited pipeline's per-suite deadlines stand,
  and the 2,700-second zero-grace rule on native test runs is withdrawn. If the owner meant test
  runs, this item is corrected.

## Standing decisions, unchanged (15-17 Oct)

- The farewell is a noncontact beat; the original 3.8 s Embrace stays tested.
- UMA bodies only for the cast; never a non-UMA fallback, stand-in or test cast.
- Desktop target: GTX 1060 at 1920x1080, 60 FPS.
- Three first-time human testers.
- Furniture and props are authored in Blender. Whether the delivered props batch meets that is
  checked when it is admitted.
- Story-arcs' own "schema 14" saves stay refused.
- Full test gates and independent review for every landed slice.
