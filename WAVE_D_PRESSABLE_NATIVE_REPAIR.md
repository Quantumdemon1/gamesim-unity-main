# Native stale/disabled button dispatch repair

2026-10-05 UTC. Source increment above isolated WaveC `f5b58df3`. No live promotion.

## Actual failed native evidence

Frozen integration3ed g24n1/root97056 CLOSED1 at2026-10-05T17:25:09.9578844Z.
Native Edit3643/3643passed. Play executed992cases:991passed,1error,0skipped,
Unity exit2. The failing authored case was
`HoHPitches_FeelingOutIsFreeDeterministicDurableAndCannotBeRepeatedAfterReload`.
At its line145, directly invoking the disabled post-reload assessment control
called `EpisodeHud.Pressable`'s unguarded `action()` with a null delegate.
This is a real UI lifecycle defect, not an infrastructure-only failure.
Play XML SHA256`d2a40f46d18af5c6af67a57d7f4f9b550917b73cde9476527136817a632a5b52`.
Terminal source drift, cleanup errors and unowned descendants are all empty.
Its terminal/native artifacts remain retained. Never poll/restart root97056.

## Every material change

- `EpisodeHud.Radial.cs`: shared press dispatch checks the captured button still
  exists, is active and is interactable before invoking its nullable action.
  Direct or retained UnityEvents cannot bypass the real button's eligibility.
  Existing director command bounds/save-before-publication rules remain required.
- New `EpisodePlayModeTests.Pressable.cs` and unique GUID:3 native lifecycle cases
  verify disabled/inactive/destroyed controls, missing callbacks, and disabled
  CanvasGroups. The original failing HoH test and all its assertions are intact.
- Play floor raised991 in WaveC,995 in future combined integration retainingQA4.
  Other combined floors remain3826Edit/77UMA/2581pure.

## Verification boundary

Root78975/offline01 CLOSED0:8fresh assemblies,0errors; output
`4278e26c485e411ea1ece52809ae792d`.
Root38875/pure01 CLOSED0:2581regular passed,0failed/0skipped;13pre-existing
Explicit reports unselected. These do not execute the new native lifecycle cases.
Before01 source image952C#/asmdef SHA256
`747e256f92329a20974889c85e843f3a5b77cdaf2124627498097fe7593d8fd0`.
Root source review only; no new independent-agent review was obtained.

Read-only MCP after the closed native owner verified the intended C project:
EpisodeHouse,21roots,loaded/notdirty; editor idle/notcompiling/updating; current
Console0messages/errors/warnings. No scene/editor mutation was performed.
The replacement combined native run must prove the original failing case and
all995Play cases, plus3826Edit and separately77UMA at the same accepted source.
No build, visual, performance, human or enabled-rule acceptance is claimed.

Liveb25 source edits, scenes/recoveries, settings, normal saves, fixtures, retained
builds and snapshots are untouched. No purchases/accounts/cloud/AI/publication/
credentials/access/deployment changed. All four WaveD systems and actual
1080p60 GTX1060 acceptance remain required.
