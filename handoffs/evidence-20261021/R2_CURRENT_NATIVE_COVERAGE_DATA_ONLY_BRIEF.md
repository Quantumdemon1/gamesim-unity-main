# Current native coverage: data-only completion route

## Decision and limits

The existing strict native adapter can support complete coverage without admitting generic discovery code, provided aggregation checks actual XML occurrence multisets, disjoint batch ownership, historical floors and the native assembly's unfiltered declared testcasecount at one unchanged source/vendor/defines boundary. A declared count alone is not execution proof; selected total alone is not discovery proof. Any mismatch must fail closed.

Read-only review used only named CLOSED artifacts and the physical source snapshot from the focused portrait-owner run already audited in R2_PORTRAIT_SCENE_OWNER_NATIVE01_CLOSED_INDEPENDENT_AUDIT.md (B6276587…). No current project, vendor, Git, Editor or active native output was read. No compiler, Unity, discovery helper or project mutation was performed. Historical results below establish roster provenance, not a current full-suite pass.

Source snapshot S7D:

`D:/GamesimAcceptance/Logs/validation/r2-dialogue-pilot-portrait-queue-scene-owner-native01-20261021T181705426Z-0b356dd029774f88baaa2f4706e1f21a/snapshot`

The same compiled boundary reports Gamesim.EditModeTests.dll testcasecount 8462 in studioEdit.xml and Gamesim.PlayModeTests.dll testcasecount 1342 in generic06.xml and portraitScene.xml. All six subsequently CLOSED full52 XMLs independently declare 1342. Only selected 5/2/3 and 52 tests were executed in those runs; these declarations do not turn them into full suites.

## Main Play Mode: all 1342 accounted for

The exact historical baseline is the 1281-name main roster in R1_BOUNDED_PLAY_UMA_PARTITION_R2_V1.json, 371971 bytes, SHA256 6DD6FAB7C7ED6DF63D9BEC7EABAFB089424422C0401BE3AFACF6B05AC98B4551. Its six batches contain 196/195/196/253/222/219 leaves. The current total reconciles as:

`1281 historical + 44 distinct generic-dialogue additions + 14 real-dialogue additions + 3 scene-owner leaves = 1342`.

The full52 names file is 6075 bytes, SHA256 25FDED089FB4333583A410CDDAB9B0E0F5AD1DB0890C1ABC43F41B168907EB51. Eight of those 52 names already occur in the historical 1281 floor; adding all52 would double-count them. The full52 mapping array is `rows`, not `batches`.

The missing14 are actual native full names, not reconstructed parameter displays. They occur in:

`D:/GamesimAcceptance/Logs/validation/r2-dialogue-pilot-native07-20261020T160623150Z-b0956873daf9438b9812102532375320/play.xml`

SHA256 E3941B3B2812218D94BC35478AD84B676109F0A088B98D1E459705CB03114B9C. All14 historically passed; current source method declarations agree. Under namespace Gamesim.Tests.PlayMode they are:

- EpisodeDurableTransactionTests.DialogueEntryAfterReplacementAcknowledgementFailureInstallsOnce
- EpisodeDurableTransactionTests.DialogueEntryBeforeWriteFailurePreservesSourceFilesAndBothRandomStreams
- EpisodeDurableTransactionTests.DialogueMiddleAfterReplacementAcknowledgementFailureInstallsOnce
- EpisodeDurableTransactionTests.DialogueMiddleBeforeWriteFailurePreservesSourceFilesAndBothRandomStreams
- EpisodeDurableTransactionTests.DialogueTerminalAfterReplacementAcknowledgementFailureInstallsOnce
- EpisodeDurableTransactionTests.DialogueTerminalBeforeWriteFailurePreservesSourceFilesAndBothRandomStreams
- EpisodePlayModeTests.DialogueCampaign_RealConfrontationAtCaseAnswersTheQueueAndResumesAfterReload
- EpisodePlayModeTests.DialogueCampaign_RealConfrontationAtFinishAnswersTheQueueAndResumesAfterReload
- EpisodePlayModeTests.DialogueCampaign_RealGossipAtCaseAnswersVisiblyAndResumesAfterReload
- EpisodePlayModeTests.DialogueCampaign_RealGossipAtFinishAnswersVisiblyAndResumesAfterReload
- EpisodePlayModeTests.DialogueCampaign_RealIncomingOfferAcceptsAtCaseAndResumesAfterReload
- EpisodePlayModeTests.DialogueCampaign_RealIncomingOfferDeclinesAtFinishAndResumesAfterReload
- EpisodePlayModeTests.DialogueCampaign_VisiblePaidEntryFreeRepliesAndEveryNodeSurviveReload
- EpisodePlayModeTests.DialogueCampaign_WalkAwayResumeAndReloadRetireCapturedChoiceCallbacks

Source references relative to S7D/Assets/Gamesim/Tests/PlayMode: EpisodeDurableTransactionTests.DialogueCampaign.cs test attributes at 69/75/81/87/93/99; EpisodePlayModeTests.DialogueRealReplies.cs at 15/20/26/32; DialogueIncomingOffer.cs at 16/22; DialogueCampaign.cs at 17/67.

There are26 current native fixture classes: the old25 below plus CharacterPortraitSceneLifetimePlayModeTests. All use namespace Gamesim.Tests.PlayMode:

```text
CeremonyGlassPlayModeTests
CeremonyRevealPlayModeTests
CeremonyStingPlayModeTests
CharacterAppearanceProviderPlayModeTests
CharacterBodyProviderPlayModeTests
CharacterCastSlotEditingPlayModeTests
CharacterClonePlayModeTests
CharacterGarmentLifecyclePlayModeTests
CharacterOutfitPlayModeTests
CharacterProfileLibraryPlayModeTests
CharacterStudioRecoveryPlayModeTests
CompetitionApparatusPlayModeTests
CompetitionSurfacePlayModeTests
EpisodeDurableTransactionTests
EpisodePlayModeTests
GroundedFarewellPlumbingPlayModeTests
HouseNpcMotionPlayModeTests
HousePrototypeTests
HouseSeatPresentationPlayModeTests
HumanoidReactionPlumbingPlayModeTests
HumanoidTalkAndSeatPlayModeTests
JuryRevealPlayModeTests
NominationCardPlayModeTests
PresentationAccessibilityPlayModeTests
UiThemeGlassPlayModeTests
CharacterPortraitSceneLifetimePlayModeTests
```

Keep portrait3 in its own fresh process. Its exclusive-empty-state setup and global cleanup must not run before original generic lifecycle leaves in the same process. Do not make the very large EpisodePlayModeTests fixture a single native batch: its historical main-suite duration cannot safely fit the45-minute bound. Retain bounded known method/family partitions and explicit extra rosters, with a final actual1342-name union check.

## Edit Mode: 8462 accounted for, with one explicit changed contract

Historical XML:

`D:/GamesimAcceptance/Logs/validation/role-ap-r1-full-native01-20261019-20261019T202818910Z-f9b392d73075451eaf99e871d71d5c05/edit.xml`

7135406 bytes, SHA256 B15005B21FE10105BB56C3EBB905D69BFD553D85C56E50A668646B079F675350. Its8253 occurrences span321 fixtures, but only8204 distinct full names:49 names occur twice. Do not collapse those occurrences into a set. The data-only partition is r2-edit8253-historical-data-partition-next01/partition.json (2586882 bytes, SHA256 F179B2CDFDFB669BE408A454914C8E2C159C61156BC23B06D6E95F014969A424), whose `batches[].fixtures[].fullName` entries enumerate all321 exact fixture names and retain actual occurrence data. All321 classes still occur in S7D source; no historical fixture is missing.

Current334 fixtures are exactly those321 plus the following13, all under Gamesim.Tests.EditMode:

| Added fixture | Current source case declarations |
|---|---:|
|DialogueAdvanceAcceptedShapeLegacyTests|2|
|DialogueCampaignCapacityTests|11|
|DialogueCampaignDealClosureTests|2|
|DialogueCampaignEmotionalResponseTests|4|
|DialogueCampaignEngineTests|38|
|DialogueCampaignFreeOfferTests|4|
|DialogueCampaignRealReplySaveTests|4|
|DialogueCampaignRulesTests|101|
|DialogueCampaignSaveBoundsTests|5|
|DialoguePendingNpcTests|1|
|DialoguePendingSaveTests|15|
|DialogueCampaignV1RetainedReaderTests|11|
|DialogueCampaignV1WriterCaptureTests|1|
|Total|199|

The first11 fixtures have187 actual historical XML occurrences in native07/edit.xml; its other30 cases are the existing DialogueSchema29ShapeTests fixture. Retained reader11 and writer1 are additional fixtures. The source count for Rules is94 TestCase attributes plus7 Test attributes, not42 attribute-bearing lines; multiple attributes share lines. No parameterized full names were guessed from those literals.

Two existing fixtures add10 more cases:

- CharacterStudioShutdownTests adds2: PortraitQueueShutdownOutsidePlayRetiresOnlyItsOwnStudioCameraAndTarget at source135, and PortraitQueueShutdownOutsidePlayRetiresItsActualPendingRevision at197. Its native focused5 includes the old3.
- VotePleaAndCallTests adds8: EmotionalVotePleaKeepsTheSourceLeafAndOneWindowDebit has4 cases at112–116; EmotionalVotePleaDuplicateCannotSpendOrResolveAgain has1 at190–191; EmotionalVotePleaRefusalPreservesTheWholeStateAndRequest has3 at209–212. These8 explain the previously unresolved count delta. This review establishes source roster only, not a current native pass for them.

Thus `8253 + 199 + 2 + 8 = 8462`.

There is a required explicit floor migration, not a free subtraction. Old edit.xml has:

`Gamesim.Tests.EditMode.DialogueSchema29ShapeTests.FoundationRuleOneRefusesEvenWithCompleteFreshPrerequisites`

Native07/edit.xml and S7D source97 instead have:

`Gamesim.Tests.EditMode.DialogueSchema29ShapeTests.ExplicitRuleOneAcceptsEmptyStorageWithCompleteFreshPrerequisitesWithoutMutation`.

The fixture still has30 cases; its actual old/new name difference is exactly this pair. The dormant-foundation refusal was replaced by explicit pilot-rule admission. Aggregation must document and review that one-to-one semantic substitution, retaining the old evidence; an unchanged old-name matcher would properly refuse. Do not silently waive or lower the8253 occurrence floor.

Exclude non-native source-only members under `!UNITY_5_3_OR_NEWER`, including the extra ModeTwoReaderParityTests walk, ModeTwoSeasonSweepTests remainder/sample check and UnifiedVoteSettlementTests other14 season cases. BalanceLabTests and its partial source files are likewise non-Unity. Frozen28PhasePilotDiagnostics and Frozen28SaveStoreCostDiagnostics require GAMESIM_DIAGNOSTICS, absent from this normal native boundary. Source helper classes/comments are not fixtures.

The existing historical four-batch fixture ownership is1/106/106/108 fixtures, with172/2561/3324/2196 historical occurrences. OldSaveAcceptanceTests owns the first batch alone. Its estimated1281.7 leaf-seconds plus startup margin is still not a deadline guarantee. Add the new13 fixtures in disjoint bounded groups; newly added cases in old fixtures should be included automatically by fixture-prefix selection, not omitted by reusing only old leaf names. Recompute actual final totals, not just historical planning totals. Every native process still has a hard2700-second original-birth deadline and zero grace.

## UMA: 90 historical +38 triangle +1 graph =129

The same R1 partition JSON retains the90-case UMA floor and actual XML provenance. S7D has18 native fixture classes: the old16 plus UmaTriangleReaderFencePlayModeTests and UmaAnimatorGraphLifetimePlayModeTests. All use namespace Gamesim.Uma.Tests:

```text
UmaAppearanceCatalogPlayModeTests
UmaBodyTintPlayModeTests
UmaCastCostPlayModeTests
UmaCastLookPlayModeTests
UmaCastPlayModeTests
UmaColourPlayModeTests
UmaEmbracePlayModeTests
UmaExpressionsPlayModeTests
UmaFacingPlayModeTests
UmaFarewellReadinessDecisionPlayModeTests
UmaFarewellStancePlayModeTests
UmaGroundedFarewellPlayModeTests
UmaGrownPiecesPlayModeTests
UmaHouseGarmentFitCapturePlayModeTests
UmaHouseGarmentLifecyclePlayModeTests
UmaHouseGarmentPatternTests
UmaTriangleReaderFencePlayModeTests
UmaAnimatorGraphLifetimePlayModeTests
```

The exact38 triangle leaf names are already bound by the four actual CLOSED green XMLs reviewed in R2_TRIANGLE_READER_GREEN38_ACTUAL_XML_AND_COMPLETE_LOG_AUDIT01.md, not newly invented parameter displays. Its expected-name file3572 bytes has SHA256 6674E0080B0973342F36FC3EB0C22452AA0F1F129996230AD09958E090EC58C0. Historical graphLifecycle01.xml is7051 bytes, SHA256 1AF940210F8697A171CF734E02837A10601D3D6D2AE775CC6821BBF300E6D540 at:

`D:/GamesimAcceptance/Logs/validation/r2-dialogue-pilot-uma-graph-lifecycle-single-native01-20261021T141850180Z-fe525a47d946440cbbb366ecdfb228f3/graphLifecycle01.xml`.

It declares Gamesim.Uma.PlayModeTests.dll testcasecount129 while selecting only1: UmaAnimatorGraphLifetimePlayModeTests.ShippingReplacementAndTeardownRetireOnlyFixtureOwnedAnimatorGraphs. This is the last named CLOSED native UMA declaration inspected, not a fresh129-case execution at S7D. A future current UMA run must independently declare129 consistently and execute the exact129 occurrence union with the required GAMESIM_UMA/UNITY_INCLUDE_TESTS boundary.

## Minimal execution and acceptance contract

1. Keep the existing strict adapter and source/vendor/defines pins. Use a data-only disjoint fixture ownership roster for Edit and bounded historical Play/UMA partitions plus explicit additions. Do not admit a new discovery assembly solely to repeat data already present.
2. Escape exact fixture full names and anchor their prefixes. A conceptual fixture regex is `\AGamesim[.]Tests[.]EditMode[.]ExactFixture(?:[.]|\z)`. NUnit may match an ancestor fixture and then run all descendants; that behavior is useful only when fixture ownership is disjoint. Do not include a shared namespace/assembly ancestor selector in multiple batches. Parameterized or nested fixtures must use actual XML fixture identities, not a guessed suffix grammar. Historical Edit fixtures contain no parameterized/nested fixture suffixes, but future XML must still be checked.
3. Validate actual per-batch XML against its ownership and reviewed method roster. Reject unknown/unexpected leaves, absent expected occurrences, duplicate cross-batch ownership, skipped/explicit/not-runnable cases or failed setup. XML IDs need only be unique within a process and must not substitute for stable full-name occurrence multisets across processes.
4. Require constant native assembly testcasecount across all batches at the same source/compiled/defines boundary and exact aggregate occurrence totals8462/1342/129. Preserve historical floors and the explicit single shape-contract substitution; verify all new fixtures/method families. Counts alone cannot detect a removal-plus-addition swap.
5. Preserve the49 legitimate old Edit duplicate-fullname occurrences. Accidental additional duplicate execution is not extra coverage. Each occurrence must belong to exactly one batch even when its full name occurs twice inside a source fixture.
6. Accept only CLOSED captures with exact original-birth2700/zero-grace process custody, complete post-XML logs, source/tested/vendor map stability, approvals empty and preservation checks. Filtered-run summaries remain filtered/fullSuiteAcceptance=false; a separate reviewed aggregate can establish broad coverage without changing those receipts.
7. Keep lifecycle, historical-reader/save compatibility, source-fixture parity, desktop build,1080p/60 profiling and human playtests separate. A whole numeric native floor is not shipping or human acceptance, and old failed reader evidence must not be overwritten.

## Review-tool incident, resolved

A broad read-only PowerShell source regex outlived its initial shell wait and its session identifier was accidentally discarded. It was never a Unity/compiler process and made no file writes. Root was informed and held native launch. The remaining exact owned scan PID5040, parent23612, held birth2026-10-21T19:03:56.5516068Z, Codex runtime pwsh.exe, was independently revalidated by CIM signature and a held Process handle, then stopped without descendants; kernel closure confirmed, exit-1. A safe signature-only follow-up at19:08:07.0986394Z found zero matches for either owned census signature. The earlier first scan had already disappeared; no invented closure/exit receipt is claimed for it. Subsequent bounded line/XML queries completed in0.3–2 seconds and all shell results retained their session fields. No raw command line, credential or environment was printed. This incident is not native-test evidence and does not alter root's separately owned process/preservation gate.
