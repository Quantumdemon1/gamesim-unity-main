# Completing the season

What the Unity port already does against the web game's documented flow, and what is actually
missing.

## The headline finding

**The middle of the game is done. Both ends are not.**

The weekly cycle, the endgame and every underlying system are ported and working — a headless season
already runs to a decided winner (acceptance A4). What is missing is the way in and the way out: the
game cannot be started except by pressing Play on a scene, and it ends with two sentences of text.

## Gap map, against `GameSim-Game-Flow.md`

| Doc section | Web game | Unity port | Verdict |
| --- | --- | --- | --- |
| 2. Home / sign-in / onboarding | Auth, routing, leaderboard | `MainMenu`: Continue / New season / Settings / Quit | **Done**, minus accounts, which are out of scope for a single-player desktop build |
| 3. Season setup (3 steps) | Player creation, 8 stats, 2 traits, cast pick, All-Stars, house size 8 | Cast screen: 24-person pool, both rosters, category filters, house size 3–12, default 8 | **Mostly done.** No free-form character creation — you pick a card rather than authoring stats |
| 4. Opening sequences | Intro, house entry, walk-in, tutorial, meet & greet | Tutorial only (`HouseTutorial`) | **Four of five missing** |
| 5–6. Weekly cycle | HoH → Nomination → Draw → Veto → Ceremony → Eviction → Social | All present | **Done** |
| 7. Endgame F4 / F3 / F2 | Final HoH, three rounds | `FinalHoHPart1/2/3`, `FinalEviction` | **Done** |
| 8. Jury questioning and finale | Questioning, speeches, jury vote | `JuryQuestioning`, `FinalSpeeches`, jury vote | **Done** |
| 9. Final stats screen | Winner display, your journey, houseguest grid, season table, standings, player stats | `SeasonReport` — all six sections | **Done** |
| 10. Underlying systems | Relationships, alliances, deals, threat, storylines, gossip, persona, jury sentiment, autosave | All present and substantial | **Done** |
| 11. Spectator mode | Auto-on when evicted, season plays to the end | Season continues and input is gated; nothing tells the player | **Half done — see below** |

The systems column is worth dwelling on, because it is the expensive half and it is finished:
alliances, promises, relationship decay, gossip and witnesses, diary-room persona, jury sentiment,
threat ranking and storyline arcs are all real code with tests behind them.

## The six-contestant constraint

`EpisodeValidation` rejects any state whose cast is not exactly six:

```csharp
if (s.contestants.Count != 6) return Fail(out error, "The house format requires six stored contestants.");
```

The web game defaults to eight and lets the player choose. **Setup cannot ship without relaxing
this**, and relaxing it touches validation, the veto lineup rule, save migration and a large number
of tests. It is the single biggest structural difference between the two games and it is why setup
is sequenced late here rather than first, despite being first in the player's experience.

## Order of work, and why

**Phase 1 — the season finale report.** *Building now.*

Highest value per unit of risk, and the clearest current defect: a player finishes a whole season
and is told "Winner: X. Runner-up: Y." in two lines of body text. Everything a full report needs is
already in `EpisodeState` — `hohWins`, `vetoWins`, `timesNominated`, `nominationWeeks`, `status`,
and an `events` log whose entries carry a `week` and types for `competition`, `nomination`, `veto`
and `eviction`, so the week-by-week table can be reconstructed rather than newly recorded. **It
needs no simulation change at all**, which means it cannot regress the season.

**Phase 2 — spectator mode.** Smaller than it first looked, and worth stating precisely because the
first version of this document got it wrong.

The *mechanism* is already there. The engine marks an evicted player `Jury` and carries on; it
already guards for an inactive player (`Require(!s.Active.Any(x => x.isPlayer) || …)` before final
speeches). The director tracks `playerIsActive`, disables player input and the diary room, blocks
social approaches, and opens the phase panel so the season can still be advanced. Tests already
construct a season in which the player is a juror.

What is missing is **the acknowledgement**. Nothing on screen says the player has been evicted or
that they are now watching. Control simply stops responding, which is the difference between an
ending and a bug. So Phase 2 is a presentation change — an eviction moment for the player, a
standing spectator badge, and a decision about whether remaining phases auto-advance — rather than
the systems work it appeared to be.

**Phase 3 — main menu and season setup.** *Season setup built; menu still open.*

Done in three steps, in the opposite order from the one planned here, because the validation work
turned out to be the thing everything else was waiting on:

1. `EpisodeValidation` stopped demanding exactly six (`MinimumCast` 3, `MaximumCast` 16), and the
   caps derived from six — relationships, memories, votes, scores — were rewritten against the cast
   size. That exposed the veto lineup rule hiding inside the old one: six *seats*, not "everyone".
2. `CastTemplates` — a 24-person pool across two rosters, with `WebTraits` supplying the stats so a
   houseguest cast from the pool and the same person in the shipped scenario are built by one
   formula. `SeasonBuilder` turns a choice into a season without touching the generator, so a built
   season replays like any other.
3. `CastSelect` — the "Choose Your Houseguest" screen: roster tabs, category chips, the card grid,
   a house-size stepper, and a start that stages to a new slot while keeping the old one.

4. `MainMenu` — the front door. It opens on a real launch, offers Continue only when there is
   something on disk to continue, and hands off to the cast screen for a new season. A run started
   with an explicit save root keeps the old behaviour and reaches the house directly, because a
   menu nothing asked for would block every test and the standalone verification; those drive it
   through `OpenMainMenu` instead, and do.

**Still open.** Setup is a *pick*, not the web's character creator — no free-form name, stat
allocation or trait selection. `WebTraits.CreateStats` already holds the arithmetic a creator would
need, and `CastTemplates.ToContestant` is the shape it would produce.

**Phase 4 — opening sequences.** Intro, house entry, walk-in, meet and greet. Presentation only,
and the least load-bearing: the game is complete without them, it just does not open well.

## What not to port

The web game's home page, sign-in, account creation, onboarding and cloud leaderboard exist to serve
a hosted multi-user web app. A single-player desktop build has no accounts, so these become one
main menu with New Season / Continue / Settings / Quit. The "unranked local season" notice on the
web stats screen exists because those results are not cloud-verified; here every season is local, so
the notice has nothing to contrast with and is dropped.
