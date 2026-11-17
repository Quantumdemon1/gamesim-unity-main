# Brief for a session on the owner's PC: the Ring Tower's old-save cell

Written 2026-11-17 by the cloud session (claude/nifty-rubin-bv3yur), for the owner's ask: "add the Ring Tower test save".
OLD-SAVE-ACCEPTANCE.md §7 records the gap ("Competition rules 5 has no cell yet").

## Why it has to be written on the PC

- Competition rules 5, the Ring Tower (5d1daabf, PR #77), is what `ShippedRules.ApplyFresh` writes for every new season.
  A built season stays at 4 (`SeasonBuilder.NewState`). No fixture written by a rules-5 build exists, so a shipped season
  saved today has no cell.
- The cloud session built `Tools/OldSaves/Recorder.cs` under .NET 10 with Newtonsoft 13.0.2. It refused six existing
  Mono-written fixtures: "Save checksum does not match". That is the Mono double handling `SaveJson.ParseObject` notes
  (F9). The cell has to come from the editor's Mono, through `Tools/OldSaves/build.sh`.

## The steps

Write it the way cells 2.12 and 2.13 were written.

1. **Build the recorder** at a main head at or after 5dab738e, with `Rules.Shipped.cs`.
2. **Generate a fresh shipped Regular season of 8** with the plain walker, saved at a point after the season's first
   Ring Tower. The first Ring Tower always falls in weeks 1-3, so pick a seed and a stop point whose walk has played one.
3. **Record it** with that build, which is its own reference.
4. **Add:**
   - the manifest row;
   - the fixture's save, replay and their metas;
   - the `OldSaveAcceptanceTests` expectations for the new cell;
   - a control: the same file resealed at competition rules 4 plays otherwise or is refused, whichever the code does.
5. **Record it in OLD-SAVE-ACCEPTANCE.md:**
   - add the section-2 entry, numbered next after 2.13;
   - turn the §7 bullet into "has its cell".
6. **Qualify:** run a full EditMode run with the new cases, raise the floor to what ran, get one independent review, and
   open a draft PR.

No owner decision is needed. This adds a test save for rules already on main.
