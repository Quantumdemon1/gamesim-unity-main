# NPC agency: trait affinity, agendas and a house that comes to you

The owner's ask (2026-09-28): *"Crusader Kings has traits that naturally align or synergize with
other traits and ones universally liked or disliked depending on the traits of the NPC, try to
model something similar with npc to npc interactions, work on improving the intelligence of NPC
and make them proactive agents."*

What prompted it is measurable. Across every real standalone season run for the strategy loop
(STRATEGY-LOOP-PLAN.md), houseguests formed **zero** pacts among themselves. The warmest standing
between two houseguests was 17 against a pact floor of 25 both ways, because the only thing that
ever warmed two houseguests to each other was the NPC world's conversations (−3 to +3 apiece) and
the weekly pass's talks (+4, to a partner drawn at random by warmth). Nothing about *who* they were
entered into it: two loyal competitors and a loyal competitor beside a sneaky analyst started, and
stayed, at nought. The house was a set of interchangeable bodies with different verbs.

This plan gives each houseguest a temperament that other temperaments take to or grate against, a
weekly agenda that says what they are trying to do and with whom, and the decisions to act on it,
including toward the player. Built in three parts (R6a, R6b, R6c), all keyed to one new rule
boundary so that every season saved before it keeps its outcomes.

The story session's constraints (their message of 2026-09-28, accepted as design targets):

1. **Pacing holds.** `StoryPacingTests.PacingReport` before and after: NPC showmances 0.5-1.5 a
   season with fictional NPCs and 0 in All-Stars; seasons with a pile-on at most 25%; pariah-free
   seasons at least 80%, and the twelve-house's 45% not lower; seasons with an NPC removal at most
   10%.
2. **Blocs stay legible.** At most one NPC-only pact per three NPCs in the house at a time, and
   each NPC in one such pact at most. Enforced in the pass, not left to the numbers (§3.4).
3. **Cold pairs stay.** Clashes are as valuable as affinities: kitchen-blowup and cold-shoulder
   want NPC grudges of 40 or more, or mutual warmth of −10 or below.
4. **Warmth toward the player is this plan's to model.** The Bond, Their Word (10), Build the
   Numbers (12) and Ride or Die (mutual 35) cast on it; the house's mean view of the player is
   reported before and after.

---

## 1. The trait table (`TraitAffinity`)

Two tables over the seventeen traits `WebTraits` knows, in the manner of Crusader Kings' trait
opinions, and one function.

**Reception** is what a trait earns from the house at large, whoever is looking:

| liked | | disliked | |
|---|---|---|---|
| Loyal | +2 | Manipulative | −2 |
| Funny | +2 | Deceptive | −2 |
| Charming | +2 | Sneaky | −2 |
| Social | +1 | Confrontational | −2 |
| Flexible | +1 | Stubborn | −1 |
| | | Impulsive | −1 |

**Affinity** is what an observer's trait makes of a trait in somebody else, and it is directional.
Kinship runs through most of it (like warms to like, +1; the loyal to the loyal, +2), with the
exceptions that make a house: schemers see rivals in each other (Manipulative on Manipulative −1),
the intuitive see through the deceptive (−2), the loyal cannot abide them (−2), the flexible and
the stubborn grate (−2/−1), the analytical and the impulsive (−2), the emotional and the
confrontational (−2), the introverted and the social (−1 each way), and the one symmetric enmity,
the bully and the sneak (Confrontational and Sneaky, −2 each way). A manipulator looks kindly on
the loyal and the emotional (+1: easy marks); the loyal do not return it. Every trait has a row, so
a houseguest the player authored has opinions as surely as a card from the roster.

**Compatibility(observer, other)** is the sum, over the other's traits, of what each earns from
anyone plus what each of the observer's traits makes of it. Over the shipped casts it runs from −4
to +5 one way and from −8 to +9 both ways added: in the regular twelve Avery and Sam (+9) and Emma
and Riley (+8) are kindred, Blake and Taylor (−8, −4 each way) oil and water, Jordan and Taylor and
Blake and Quinn (−7) close behind; the house takes to Sam, Alex, Casey, Avery and Maya and is wary
of Taylor, Blake, Jordan and Quinn. In All-Stars, Cody and Xavier (+9), Chelsie and Tyler (+7); Jun
and Rachel (−8), Dan and Derrick (−5, two schemers). `Describe` gives the words: kindred (4+), easy
company (2+), neither here nor there, grating (−2), oil and water (−4).

Pure data and a pure function. The season decides when it applies (§3), so the table can be
retuned without a schema.

## 2. The boundary

`EpisodeState.agencyRulesStartWeek` (schema 20; 0 for never). `EpisodeEngine.EnableAgency(s,
fromWeek = 1)` and `AgencyOn(s)`, exactly as the read, the levers and the week are keyed. The
director enables it for every season it starts, from week one; the web importer from the week
after the save's; recorded fixtures never reach it. `FrozenEpisodeV19` freezes the week's shape,
`UpgradeV19ToV20` adds the one field at week + 1, `StripSchema20` composes downward. No new
command kinds: the house comes to the player through the surfaces that exist.

**First impressions.** `EnableAgency` on a season that has not begun (week 1, no command accepted)
seeds the standings: every NPC's view of every other houseguest, the player included, moves by
**3 × Compatibility**. A kindred pair starts at about +15 each way, an oil-and-water pair at about
−12, and a Manipulative, Deceptive persona walks in read at −6 to −12 by most of the house while a
Funny, Charming one is liked before they say a word. The player's own view of everybody stays at
nought: what you think of them is yours to decide. Scores only, no ledger events, so trust (which
reads the ledger) starts neutral as it always did.

## 3. Warmth by temperament (R6a)

All under `AgencyOn`.

1. **The NPC world's conversations.** A completed conversation's delta (the web's −3..+3 by
   topic) carries a bias of the pair's mutual compatibility (both ways added) over six, rounded and
   clamped to ±1. Kindred pairs never come out of a chat colder; oil-and-water pairs rarely warmer.
2. **The weekly talk.** `NpcSocialActions.Talk` is worth 4 plus half the speaker's compatibility
   with the listener, clamped to ±2: between 2 and 6. The same when the listener is the player.
3. **Desire.** `NpcAlliances.Desire` adds **2 × Compatibility** (up to about ±10), so who
   houseguests want to work with follows who they take to, on top of the web's shared-threat and
   warmth terms.
4. **The pact cap** (story constraint 2). The weekly pass proposes no pact when the proposer or the
   partner is already in an active NPC-only pact, or when the house already holds
   ⌊active NPCs / 3⌋ of them. A story's own pact (`FormFromStory`) and the player's are outside
   the cap. `MaximumEach` stays for them.

Everything the story session builds on cold pairs is left standing: the seeded clashes sit at
mutual −10 or below from week one for the coldest pairs, and the conversation bias keeps them there.

## 4. Agendas (R6b)

`NpcAgendas.Of(state, npcId)`: a pure reading of what a houseguest is trying to do this week, in
priority order, with the person it points at (`Partner`):

| agenda | when | points at |
|---|---|---|
| **Survive** | on the block (or the backdoor's target) while the vote is open | the most persuadable voter (§5.2) |
| **Reign** | the Head of Household | their closest ally, to consult |
| **Court** | somebody else is HoH and they have no claim on them (no pact, no safety deal) | the Head of Household |
| **Hunt** | in a pact and there is a common threat (`NpcDeals.CommonThreat` with a pact-mate) | a pact-mate, to coordinate |
| **Build** | in no NPC pact and somebody would have them (`Desire` highest among those they would propose to, the player included) | the prospect |
| **Hold** | in a pact | a pact-mate |
| **Drift** | none of the above | nobody: the weighted draw, as before |

Consumers:

1. **The weekly pass** (`NpcSocialActions.Settle`): the first free turn goes to the agenda. Build,
   Court and Hold talk to the partner; Hunt talks the threat over with the partner (a talk between
   them, and a word against the threat worth −6 with the listener); the rest of the turns play the
   trait repertoire as they did. A talk aimed at the player is logged as it always was ("sought you
   out"). A builder whose prospect is already warm enough for a pact (25) has built it and spends
   the week as before; the pact pass takes over when the house has room. Without that stop, the
   unpaired majority of a capped house piled warmth on one person all season (the warmest pair
   ended a season at 164 both ways added, against 30 before).
2. **The NPC world's pairs** (`EpisodeDirector.PlanNpcApproaches`): when a scan is due, each idle
   houseguest tries their agenda's partner before the next idle body in cast order. The engine
   supplies `NpcAgendas.PreferredPartner`; the director only orders its loop by it.
3. **Reads.** `ReadPerson` on a houseguest adds their agenda to what the read says, in the read's
   own words ("Riley is looking for a partner"; "Quinn is courting the Head of Household"; "Sam
   wants Taylor out"). Gaining information about what people are *doing*, not only what they think
   of you.

## 5. Intelligence (R6b)

1. **Nominations weigh threat, and the bloc's read of it.** Under agency `NominationWeight`
   subtracts 0.4 × `ThreatAssessment.Total(hoh, id)`, and when the Head of Household has
   pact-mates, 0.2 × the mean of their totals. A competition beast at threat 60 reads as 24 points
   colder; a Head of Household consults their alliance. The replacement follows the same weight.
   (Today an NPC HoH nominates whoever they like least, threat or no threat.)
2. **Nominees campaign to the persuadable.** Under agency the campaign pass sends a nominee to the
   two voters worth the visit: not those allied with the other nominee, not those sure to keep
   them (warmth margin of 20 or more) or sure to evict them (−20 or less); the closest to torn
   first. The player is a voter on the same terms and gets the plea card as now. With nobody
   persuadable, the old order (veto holder, player, cast).
3. **The house asks the player to work with it.** A houseguest whose Build agenda points at the
   player puts an alliance invitation to them through the deal ladder (a new rung under agency:
   `WouldPropose(npc, player)`, so warmth 25 and desire 25), and a Hunt whose pact-mate is the
   player already arrives as a target agreement. Accepting the invitation forms the pact
   (`AllyThroughInvitation`), as the reference does. No new surface: the offers panel shows it.

## 6. Later (R6c)

- Voters: a voter whose agenda is Hunt votes with the pact's target; Survive's persuasion moves the
  torn (a campaign as a plea with the reference's roll).
- The Head of Household's target as a whispered fact the read and the listen-in can find.
- The People tab: the agenda word beside each houseguest, once read.
- Game Sense: an invitation welcomed or refused as an opportunity row.

## 7. Tests and measures

- `TraitAffinityTests`: every trait has a row and a reception, kindred close and clashing far,
  directional, the shipped casts spread.
- `NpcAgencyTests` (Unity-free): first impressions seeded once and never on an imported save; the
  cap; desire follows compatibility; the talk and the conversation bias; each agenda's trigger and
  partner; the persuadable campaign; nominations weigh threat; the invitation rung; the read's
  agenda line.
- `PersistenceV20MigrationTests`; the V19 tests reworked around a current schema of 20.
- `NpcAgencyHarnessTests.AgencyReport` (explicit): over 48 seasons, NPC-only pacts a season, the
  warmest and coldest mutual standings, cold pairs (mutual −10 or below) at week 3, the house's
  mean view of the player, and how the agendas distribute.
- `StoryPacingTests.PacingReport` and `GameSenseHarnessTests.GameSenseReport` before and after,
  with `StorySeasonTests.StorySeason` enabling agency as the director does (one line, the story
  session's file, told to them).
