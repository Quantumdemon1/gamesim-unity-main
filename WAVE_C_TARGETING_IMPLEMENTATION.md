# Wave C E3 — explicit extra-action purchase target

Implemented on the isolated `codex/wave-c-economy` branch above E1 commit
`a17429a3d4ad006188f136f6cee301bce335af9b`. Not merged into the frozen schema22
integration candidate, not promoted live, not natively accepted. No new Unity process was
launched for this work while the separate `g22n3` regression run owns the test copy.

## Source and scope

The original web `src/contexts/reducers/reducers/player-action-reducer.ts:324-340` accepts
a pre-picked active non-player `targetId` for `buy_action_point` / `random_one`, falling
back to a random housemate only when absent/invalid. The native `EpisodeEngine.BuyActionPoint`
already implements that contract. Both native UI adapters omitted the target. This increment
adds the missing deliberate choice without changing the engine, its fallback, costs, ceiling,
RNG policy, command types, durable transaction pipeline, schema23 or saved fields.

This is the burn-bridge portion of E3, not completion of E3 or Wave C. The subsequent targeted
nominee question increment is recorded in `WAVE_C_NOMINEE_INTEL_IMPLEMENTATION.md` (not native
accepted). The sharing picker follows in `WAVE_C_SHARING_IMPLEMENTATION.md`, also not native
accepted. E2, E4, E5 and all required Wave D remain.

## Material changes

- `ExtraActionPurchaseChoice` is a transient read-only model, not serialized state. It lists every
  active non-player (up to15 at the supported maximum cast), only in Social/Campaign while the
  active player has purchases left. It binds session, player, phase and revision, checks that a
  selected target remains eligible, and draws no random number or command on open/cancel.
- `EpisodeDirector.ExtraActionPurchase` draws the shared scrollable choice screen. Costs are
  explained before selection, Cancel stays pinned, initial keyboard focus is on Cancel, and the
  existing real-time pointer guard protects screen replacement. Rows use existing portraits and
  the player's own trust/alliance readings, not hidden NPC knowledge.
- The budget board and `HouseMoves` now open this screen. That covers free time, houseguest
  screens, expanded campaigning and legacy/final-three free time without separate purchase
  implementations. Existing opener captions remain stable; copy now explains the explicit choice.
- A fresh choice object is the view's authority. Canceled/reopened callbacks, changed revisions,
  changed world/load generations, inactive/destroyed directors and closed panels cannot spend.
  Only selection submits the existing targeted command through `Commit` and durable persistence.
  View authority is consumed before submission; failed disk writes cannot be retried by an old
  click. Closing panels or accepting any command retires the view. Loading saves persists none
  of this UI state. No preview mutates the simulation or user save.
- 27 new pure/Edit cases cover all cast sizes3-16, phase/status/ceiling eligibility, immutable
  targets, session/revision/phase/player changes, inactive targets, every offered engine target
  in3/8/16-person casts, unchanged unrelated goodwill and duplicate-command safety.
- 10 new Play cases cover actual UI adapters, exact persisted chosen outcomes, canceled and stale
  clicks, same-slot reload and scene replacement, final-three choices,16-person keyboard navigation
  at both text sizes, and a real locked isolated save. They are authored, not yet executed.
- Four new C# metadata GUIDs are unique in the branch. Native floors now Edit2631 / Play914 / UMA77;
  pure floor1639. The separate eda relay QA commit is not in this branch yet: when combining it,
  retain its11 additional Edit cases (combined floor2642), not either branch's lower floor.

## Evidence and limits

Evidence root: `D:/CodexGamesimEvidence/integration-20261004`.

- `wave-c-targeting-pure-01/wave-c-targeted-purchase.trx`:27 passed, zero failures/skips.
  SHA256 `b3fb4532568e98e790e832f5884eb099dac7ea525ccc618e2dfc14c85c0e49b4`.
- `wave-c-targeting-pure-02/wave-c-targeting-full.trx`:1639 passed, zero failures/skips.
  SHA256 `4a7381fd0b4e1e59d184443625d9b21672f319262bb50ca08ad8d0eedaae49a3`.
  This is the Unity-free subset, not native runtime or desktop evidence. Explicit report runs
  are separate, not implicitly included in the1639 count.
- Initial `wave-c-targeting-offline-01.log` failed on the new test's mistaken `rngState` field
  name; the source DTO uses `randomState`. The failure is retained. Compilation02 passed8/8
  before later generation/reload coverage; compilation03 passed8/8 before a timing-test adjustment.
  Final `wave-c-targeting-offline-04.log` passed8/8 assemblies, zero errors, output
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/ee01b4602d4341d3b129bf4dbbc08857`;
  log SHA256 `34b6e4ce32653033710947afb2ebbe4361193f4851266247fa2dc4def3444c6f`.
- `wave-c-targeting-measurement-01/wave-c-targeting-season-regressions.trx`: all3 explicit checks
  passed (54 unchanged legacy-golden seasons,54 commitment-enabled walks,480 paired-policy seasons).
  SHA256 `5b38e7068c13920e7047015cb632545ae2bea40028d67b7f07cd504d2d890b9b`.
  Old expected values were not changed. These are scripted measurements, not human balance acceptance.
- Root manual review checked all callers, command and save authority, state installation/closure,
  stale callback identities, optional-body frame timing and the preserved source fallback.
  Independent agents remain unavailable; this is not independent review.

No scene, material, imported asset, vendor package, ordinary save, baseline build, live file or
remote repository was modified by this increment. Required next gates: native focused10 cases
and compatibility cases after the frozen candidate finishes,
complete combined native suites, matching desktop build and real UI/visual/accessibility checks.
Performance and human acceptance remain separate; no AAA/completion claim follows from these tests.
