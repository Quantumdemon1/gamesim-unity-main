# Combined candidate offline compilation

Completed 2026-10-21 04:12 UTC after the GameSim Dev owner corrected the two
ConversationBaseline calls. This is one fresh NoUMA replay of current
`D:/GamesimConsolidation` sources with all nine staged competition and house
postimages. No Unity operation or shared project write was performed.

| Assembly | Compiler result | Warnings |
| --- | --- | --- |
| Gamesim.Simulation | Passed, zero errors | 1 |
| Gamesim.Runtime | Passed, zero errors | 0 |
| Gamesim.Editor | Passed, zero errors | 13 |
| Gamesim.PlayModeTests | Passed, zero errors | 53 |
| Gamesim.EditModeTests | Passed, zero errors | 6 |

No diagnostic names a staged file. The earlier two CS0103 errors are absent.
This run compiled fresh dependency DLLs locally and required each dependent
assembly to use those successful outputs. It did not reuse the owner's native
test results or Gamesim assembly outputs.

The 1,252 current source inputs had identical hashes before and after the replay.
All nine original staged files remained unchanged, and the external source
snapshots match their recorded hashes. `before.json` records those inputs;
`result.json` records assembly output hashes and diagnostic counts. Full compiler
logs and response files are under `assemblies/cd0624fd60f540bfb428ae46b58579ed/`.

This establishes offline C# compilation against the existing Unity 6000.6 NoUMA
reference-cache configuration. It does not execute Unity tests, import FBX files,
validate native meshes/materials, measure runtime performance, check UMA assemblies,
or reevaluate assembly-definition declarations. Native acceptance remains pending.

Earlier sealed receipts describe the evidence available at their creation and
were not altered. This directory is additional evidence for a new handoff version.
