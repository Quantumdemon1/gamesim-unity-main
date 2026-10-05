# Wave C E3 — choose the nominee you ask about

Implemented above purchase-picker commit `b24816080388ba5f4553ad7e594d9fccd125cfbf`
on the isolated `codex/wave-c-economy` branch. Not merged, promoted live, run in Unity,
built for desktop or accepted. The separate schema22 integration remains frozen at
`eda7312ae5c5772b822f4e145da8268ce7de1a63` while its full NoUMA run owns the test copy.

## Authority and compatibility

The approved `Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md`, Wave C E3, specifies
"Ask what they think of {nominee}" using the same roll. This is a native design
extension, not a claim of exact original-web UI parity. The original
`src/components/game-phases/social/InformationExchangeDialog.tsx:111-138` asks for
general intel and supplies the source's two-to-four relationship improvement.
The native `EpisodeEngine.AskForIntel` already records a private standing/memory
about a randomly selected housemate, using the improvement roll to select them.

This increment uses `secondTargetId` on that existing command, only under the
unreleased schema23 fresh-season economy boundary (`EconomyRulesOn`). Schema23's
shape, migration, save envelope, validation and discriminator values are unchanged.
Previously supported v1-v22 saves retain discriminator0 and ignore that payload
exactly as before. A delayed/inactive week boundary does too. Old saved histories
are never rewritten. An omitted/empty target still follows the unchanged generic
question path in both old and new seasons. No activation on load or web import.

## Every material change

- `NomineeIntel.Targets` exposes only current public nominee identities, in the
  post-nomination windows and Campaign. It excludes the listener, the player,
  inactive/unknown identities and duplicates. Old nominees retained during free
  time or nominations are not offered. Final-three play has no regular block.
  Opening/reading this list does not read hidden relationships, draw or commit.
- The engine validates an explicit eligible nominee before any roll. Invalid
  choices fail atomically, never fall back to another person. A valid choice
  spends the same one action, takes the same trust/reciprocity rolls, and records
  the same kind of private earned standing and memory. The selected subject and
  question copy change; no ballot intention, global disclosure or story roll is
  added. The original generic question remains available.
- `EpisodeDirector.NomineeIntel` adds one folded person picker through shared
  `AskRows`, so both ordinary LEARN and "what you came to ask" use it. Existing
  portrait/trust cards show the player's own reading, not the answer for free.
  Captions name the chosen nominee. Existing keyboard ring and scroll layout apply.
- Render-local object identity, loaded-world generation, session/player/phase/
  revision, current focused listener, active component and open picker bound the
  callback. Cancel/reopen, another picker, panel/scene replacement, reload and
  another commit retire old controls. Any render retires the old view authority,
  including rendering a different screen. Closing the panel closes this picker.
  Choice authority is consumed before durable `Commit`; a failed save cannot
  reveal a speculative answer or authorize a retained click to retry later.
- 28 new pure/Edit cases cover every listener/eligible nominee in casts4-16,
  final-three absence, phase gates, immutable source state, no hidden read during
  selection, invalid/self/foreign/stale commands, private audiences, exact roll and
  relationship parity, duplicate delivery and each post-nomination window's cost.
- Eight new Play tests cover both nominee buttons and exact durable answers,
  cancellation/reopening/switching/stale openers, actual keyboard traversal at
  both text sizes in a16-person house, Escape/intervening commands, same-revision
  reload and scene replacement, locked isolated saves, legacy/move-in absence and
  the post-nomination window. They are compiled, not executed.
- Pure-test inclusion and suite floors are raised: Edit2659 / Play922 / UMA77 /
  pure1667. Four new C# metadata GUIDs are unique. Combining the separate eda QA
  commit must retain its11 Edit cases: combined floor2670, not either branch's
  lower floor. Portable ProjectSettings still contains no GAMESIM_UMA define.

## Closed checks and outstanding gates

Evidence root: `D:/CodexGamesimEvidence/integration-20261004`.

- Focused pure02:28/28, zero failures/skips;
  `wave-c-intel-pure-02/wave-c-nominee-intel.trx`, SHA256
  `5de98521af45e3332968de4a472baca81dc9754913f8df483035ff32094da7e6`.
  Initial pure01 was24/25: the new fixture incorrectly constructed a regular
  campaign with three active people, which existing validation correctly rejects.
  The fixture now uses legal regular casts4-16 and separately asserts final-three
  absence. No production validation or assertion was disabled; failure retained.
- Full Unity-free subset:1667/1667, zero failed/skipped execution results;
  `wave-c-intel-pure-03/wave-c-intel-full.trx`, SHA256
  `8cbc405be5bde4772bbed2c91531822dd572d8683246feb9f6a338c6a2b0d39d`.
  Explicit report methods are intentionally outside this subset, run separately.
- Final offline02:8/8 assemblies, zero errors, output
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/d9b9dd9961bb4983a7c318bc44b65283`;
  `wave-c-intel-offline-02.log`, SHA256
  `8aab72cdfc55a699188d5f8627e20202ace65449d061804dbd20712e99a69210`.
  Offline01 also passed but predates the final render/Escape/keyboard refinements.
  References were read from the idle UMA cache, never changed in the running copy.
- Explicit regression reports:3/3, zero failures/skips;54 unchanged legacy-golden
  seasons,54 commitment-enabled walks,480 paired-policy seasons complete.
  `wave-c-intel-measurement-01/wave-c-intel-season-regressions.trx`, SHA256
  `8601ac0bafcf22dafd128a50efb569a40e2a544ac744d55eb8212eec030b399e`.
  These existing walks do not choose the new targeted question; its coverage is
  the28 dedicated cases. Game Sense gaps remain11.14 legacy /9.96 E1, below the
  report's aspirational20. This is regression evidence, not targeted-strategy
  measurement, final balance or human acceptance. No goldens were updated.
- Root manual review only: independent agents are still unavailable due usage
  quota. No independent review or source-drift attestation is claimed here.

The separately running native suite does NOT contain E1 or either E3 increment.
Required next gates are focused new native tests, combined full NoUMA/UMA suites,
a separately verified matching desktop build and graphical UI/accessibility review.
No scene, art, vendor package, ordinary save, baseline build, live source or remote
was changed by this increment. No MCP call was made into the running test copy.

E3's explicit sharing picker still remains; E2, E4, E5 and all four owner-required
Wave D additions remain. Asset completion,1920x1080 at60 FPS on the GTX1060, and
human E1-E5 acceptance remain separate gates, not implications of these tests.
