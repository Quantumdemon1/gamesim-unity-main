# Wave C E3 — deliberately choose what to share

Implemented on isolated `codex/wave-c-economy` above nominee-question commit
`060859af2e6e8307b8f71fdc9276ee378e019abf`. No integration merge, live promotion,
native run or desktop acceptance. The separate schema22 QA candidate remains
frozen at eda while its full NoUMA suite owns the acceptance copy.

## Source, scope and saved-season boundary

The approved `Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md`, E3, requires a picker
for what to share. Original web `player-action-reducer.ts:462-499` accepts an
`intelContent` description and records the exchange; native `ShareInformation`
previously always copied the latest player-owned memory about somebody other
than the recipient. This is the approved native targeting extension. It is NOT
an exact port of the web's5-10 warmth or one-step information-deal fulfillment:
the native existing3-base warmth and commitment-deal lifecycle are unchanged.

The existing unreleased schema23 fresh-season `economyRulesVersion` boundary
enables the selector. No serialized field, command kind, schema/envelope version,
migration or activation-on-load change. Supported v1-v22 saves remain rules0 and
preserve their original one-step share and ignored `secondTargetId` behavior.
Fresh rules use that existing command field for a bounded exact-memory reference.
An invalid explicit reference fails; it never selects a different memory.

One deliberate fresh-season hardening also applies to the old no-reference
entry point: its latest memory comes from `KnownBallots.PlayerMemories`, so an
omitted reference cannot bypass privacy filtering. It uses the same bounded
receipt writer. Ordinary short, visible latest-memory shares remain byte-for-byte
equivalent to the old generic command. Legacy rules retain the old policy,
including its historical edge behavior, for deterministic saved-season compatibility.

## Every material change

- `InformationShareChoice` provides immutable snapshot options, newest first,
  from the player's visible memories only. Memories about the recipient, NPC-owned
  records and receipts revealing unknown ballots are not options. It exposes all
  eligible records within the existing valid store bound, never just recent ones.
- Each reference binds session, player, recipient, revision, phase, original list
  index and every memory field with length-framed SHA256 input over exact UTF-16
  code units. References are canonical and at most80 characters. They are record
  identities, NOT credentials or cryptographic authorization. Eligibility is
  independently rechecked against authoritative state before resolving; even a
  correctly hashed forbidden record is rejected. Arbitrary command text is ignored.
  The resolved memory is a detached copy. No preview rolls or mutates state.
- `EpisodeEngine.ShareInformation` resolves explicit references before drawing,
  changing goodwill or spending. It uses the existing single action,3-base warmth,
  reciprocal draw, private recipient memory and player/recipient event audience.
  Other memories, NPC knowledge, source records and public events are not changed.
- A receipt keeps the existing "Heard from you: " attribution. If adding it would
  exceed the existing2000-character memory limit, fresh rules create a bounded
  excerpt with an ellipsis, without splitting a surrogate pair. The UI previews
  exactly that receipt and warns when it is shortened; the original remains intact.
  No save limit or validation check is relaxed.
- `EpisodeDirector.InformationShare` replaces only the fresh-season share row
  with a choose/review/confirm flow. The original caption and warmth tag remain.
  Six choices per page, numbered distinctly even for duplicate names/texts; every
  page is reachable. Review shows the complete source and exact recipient receipt
  as literal non-rich text. Empty knowledge has a clear message and an exit.
- Cancel stays pinned outside scrolling and is the initial keyboard selection
  on open, page change and review. The existing pointer hold protects replacement.
  Back, paging, cancel and review never submit a command. Only confirmation does.
- Snapshot/view identity, load generation, session/player/phase/revision, focused
  recipient and active component bound callbacks. Repainting/replacing the screen
  retires old controls; closing panels or any accepted command forgets the choice.
  An old opener cannot reopen a canceled view; an old confirmation cannot act after
  Back, another review, cancellation/reopening, load or scene replacement. Authority
  is consumed before durable persistence, so failed saves cannot disclose a result
  or make a retained click an automatic retry. No UI state is saved.
-54 new pure/Edit cases cover all cast sizes3-16, every eligible memory, privacy
  gates including correctly hashed forbidden inputs, canonical/forged/stale/cross-
  recipient references, detached records, all decision windows, identical texts,
  culture independence, UTF-16 identity, long receipts, existing memory caps, replay,
  duplicate delivery, action budgets, empty knowledge and old-season compatibility.
-10 new Play tests cover real conversation entry, older-memory selection, exact
  preview/save matching, stale controls, all30 normal memories across five pages
  at both text sizes with actual keyboard traversal, long literal text, empty state,
  Escape/intervening commands, reload/scene replacement, locked isolated saves,
  campaign/legacy adapters and the post-nomination window. Authored/compiled only.
- Pure inclusion and floors: Edit2713 / Play932 / UMA77 / pure1721. Combining eda
  must retain its11 Edit cases for a combined floor2724. Four new metadata GUIDs
  are unique; no portable GAMESIM_UMA define, scene, imported asset or vendor change.

## Closed evidence and limits

Evidence root: `D:/CodexGamesimEvidence/integration-20261004`.

- Initial pure01:47/48. The new test used JSON.NET's default collection-population
  behavior, appending to the DTO's initialized window counters. It now explicitly
  replaces collections for its pure replay check. No production serialization or
  validation was changed. Failure retained. Focused pure02 passed49, before the
  five final window cases and stronger hostile-reference assertions.
- Full pure03 passed1716 before final window coverage; final pure04 passed1721,
  zero failures/skipped execution results. Explicit report methods are separate.
  `wave-c-share-pure-04/wave-c-share-full.trx`, SHA256
  `0451cabe3a581a31e4b77b38719fd9d6c7049dcfe850dc5f2ff5752e23e90e19`.
  All54 new sharing cases pass in that final subset.
- Final offline05:8/8 assemblies, zero errors. Output
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/40f3d737711649109d697cf5e13eaf70`;
  `wave-c-share-offline-05.log`, SHA256
  `da7293bcd50d75b0798c3f81101cfa83ec26fc90aecbc6227a9453e3cf5294b2`.
  Earlier01/02 passed before final coverage;03 preceded a documentation-comment
  correction,04 preceded the locked-save test's deliberate fresh retry. All retained.
  Pure source was unchanged after pure04. References were
  read from the idle UMA cache; neither native acceptance copy was written by this work.
- Root manual review caught and corrected a tag mismatch and an undersized long-text
  fixture before native execution. Independent reviewers remain unavailable; no
  independent review or source-drift attestation is claimed.
- Explicit regression reports:3/3 passed;54 unchanged legacy-golden seasons,
 54 commitment walks and480 paired-policy seasons completed. Artifact
  `wave-c-share-measurement-01/wave-c-share-season-regressions.trx`, SHA256
  `e4cf2fa3778838720dc0c95583d40b7d82a3607911e94b4fdfa1a56a23e4d07f`.
  These existing walkers do not select the new sharing reference; its behavior
  is covered by the54 dedicated cases. Game Sense gaps remain11.14/9.96, not a
  balance-acceptance pass. No source fixtures or expected goldens were rewritten.

E3's three requested targeting features are now implemented on this isolated branch,
not integrated or accepted. Their new runtime tests must run after the frozen QA
candidate releases Unity ownership; then complete combined NoUMA/UMA suites and a
matching desktop build. The currently running native suite contains none of E1/E3.

E2, E4 and E5 remain, followed by all four owner-required Wave D additions. Asset
completion, shipping visual/accessibility checks, actual1920x1080 at60 FPS on the
GTX1060 and human acceptance remain distinct. Optional cloud/AI/accounts remain
unconfigured. Live edits, scenes, recovery assets, saves and previous builds are
untouched; nothing was pushed or published.
