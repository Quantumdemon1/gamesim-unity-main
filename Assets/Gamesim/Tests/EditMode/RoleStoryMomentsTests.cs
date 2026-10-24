using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The story moments of the role moments (ROLE-MOMENTS-PLAN R6, §3.4a, §3.5, §4.1, §5.5), through the public engine. The pawn
    /// conversation: a houseguest Head of Household who names the player as a nominee may call them up and say they are only a
    /// pawn, in any week, on a keyed coin and once per Head of Household a season, with six in the house and room to ask there and
    /// then - the backdoor's own "Just a Pawn" in a backdoor week, its plan quiet on a no, and the pawn conversation in any other,
    /// with the same card, neither holding the Game lane - and under the role moments taking their word gives the player's own
    /// back, and getting it in writing costs the window's seat. Quiet tears: a nominee
    /// found crying in the living room, at most once a week, never over the veto board nor in week one, taking The Fallout's
    /// place. A role moment weighs on the pool's "nothing" as much as it weighs itself; the pool starts nothing new at 92
    /// storylines and the rules nothing at 100. A season without the role moments - every committed fixture - draws its pools,
    /// its cards and its choices exactly as before, and a season at story rules Roles without the rules the moments are built on
    /// casts none of them. Pure simulation: the Unity-free subset runs it.
    /// </summary>
    public sealed class RoleStoryMomentsTests
    {
        private const string PawnTalk = "the-pawn-talk", Backdoor = "the-backdoor", Tears = "quiet-tears";

        // ------------------------------------------------------------------ the gate

        /// <summary>
        /// A season at story rules Roles without the strategy windows - <see cref="EpisodeEngine.RolesOn"/> false - plays to its
        /// finish casting neither of R6's arcs: no pool at any anchor holds one, no pawn conversation comes, The Fallout keeps its
        /// weight, and every Just a Pawn it draws is the shipped card. The template pool filters on a template's version, so
        /// <see cref="EpisodeEngine.Castable"/> asks <see cref="EpisodeEngine.RolesOn"/> of every Roles arc.
        /// </summary>
        [Test]
        public void AStoryElevenSeasonWithoutTheRoleMomentsCastsNoneOfThem()
        {
            var start = StorySeasonTests.StorySeason(4u, 8);
            start.story.rulesVersion = StoryRules.Roles;
            Valid(start);
            var engine = new EpisodeEngine(start);
            int pools = 0;
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                Assert.That(EpisodeEngine.RolesOn(s), Is.False);
                Assert.That(EpisodeEngine.PawnTalkComes(s), Is.False);
                foreach (string anchor in RoleStoryPools.Anchors)
                {
                    var pool = EpisodeEngine.PoolCandidates(s, new StoryContext(s, anchor));
                    pools += pool.Count;
                    Assert.That(pool.Where(c => c.template.rulesVersion >= StoryRules.Roles).Select(c => c.template.id), Is.Empty, "week " + s.week + " " + anchor);
                    Assert.That(EpisodeEngine.Castable(s, new StoryContext(s, anchor), StoryCatalog.Find(Tears)), Is.Null);
                }
                Assert.That(StoryCatalog.Find("the-fallout").weight(new StoryContext(s, StoryAnchors.BlockSet), new ArcBinding()), Is.EqualTo(10));
                Assert.That(engine.Apply(RoleStoryPools.Next(s)).accepted, Is.True, "week " + s.week + " " + s.phase);
            }
            var end = engine.Snapshot;
            Assert.That(end.phase, Is.EqualTo(EpisodePhase.Finished));
            Assert.That(pools, Is.GreaterThan(0), "The walk drew on its pools.");
            Assert.That(end.storylines.Select(x => x.templateId), Has.None.EqualTo(Tears).And.None.EqualTo(PawnTalk));
            Assert.That(end.houseEvents.Where(e => e.contentId != null && e.contentId.EndsWith(":just-a-pawn", StringComparison.Ordinal))
                .SelectMany(e => e.choices.Select(c => c.optionId)), Has.None.EndsWith("-roles"), "Only the shipped card.");
        }

        /// <summary>
        /// Castable asks the role moments of every Roles arc, StartStory goes through it, and the pawn conversation's coin is read
        /// only under them: the same state with the commitment rules off - story rules Roles, nothing else changed - casts no
        /// quiet tears and calls nobody up, where with them it does both.
        /// </summary>
        [Test]
        public void WithoutTheRulesTheMomentsAreBuiltOnNothingOfR6Casts()
        {
            var tears = TearsCastable(StoryAnchors.NomsSet);
            var arc = StoryCatalog.Find(Tears);
            Assert.That(EpisodeEngine.Castable(tears, new StoryContext(tears, StoryAnchors.NomsSet), arc), Is.Not.Null);
            var off = tears.Clone();
            off.commitmentRulesStartWeek = 0;
            Assert.That(EpisodeEngine.RolesOn(off), Is.False);
            Assert.That(off.story.rulesVersion, Is.EqualTo(StoryRules.Roles));
            Assert.That(arc.cast(new StoryContext(off, StoryAnchors.NomsSet)), Is.Not.Null, "Its cast would cast: Castable is the gate.");
            Assert.That(EpisodeEngine.Castable(off, new StoryContext(off, StoryAnchors.NomsSet), arc), Is.Null);
            Assert.That(EpisodeEngine.StartStory(off, Tears, StoryAnchors.NomsSet), Is.False);
            Assert.That(EpisodeEngine.PoolCandidates(off, new StoryContext(off, StoryAnchors.NomsSet)).Select(c => c.template.id), Has.None.EqualTo(Tears));

            var named = Unasked(PawnWeek(backdoor: false, coin: true));
            Assert.That(EpisodeEngine.PawnTalkComes(named), Is.True, "The names said, on a yes, under the role moments.");
            named.commitmentRulesStartWeek = 0;
            Assert.That(EpisodeEngine.PawnTalkComes(named), Is.False, "Never without them, whatever the coin.");
        }

        // ------------------------------------------------------------------ old pools

        /// <summary>
        /// The pools of a season without the role moments are the pools they were: four recorded seasons - three shipped (mode 2
        /// twice, mode 0) and one story season under the older rules - walked to their finish, every anchor's castable arcs, their
        /// weights and casts read at every step, and every storyline and card the walk drew, hash to what HEAD (45d2f015) recorded
        /// before R6. The Roles arcs never cast there, so the pools see the same candidates and the same total, "nothing" weighs
        /// what it did - read at every anchor of every step beside the hash, which sees only the draws - The Fallout weighs what it
        /// did, and the draws land where they did.
        /// </summary>
        [Test]
        public void OldSeasonsDrawTheirPoolsExactlyAsBefore()
        {
            var recorded = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "shipped-2101", "d3fed206ab096ac0eae840c76db846ca5d6ff6860617565e928fe39b5c6eb88c" },
                { "shipped-2102", "292684190876ca9a4e31040bfa2163f2434b013385027359d3f18829f55c5e84" },
                { "shipped-mode0-2103", "6d896d9b6b775babba5b3e8de3a6387b0cbd364be55d56d555684c2e8ccb8b6a" },
                { "story-10-3", "d2a0e1093d327454cce6b8ee5e4e6384d8698efdb5e986922e2c6d2560922182" },
            };
            foreach (var (name, start) in RoleStoryPools.OldSeasons())
            {
                Assert.That(start.story.rulesVersion, Is.LessThanOrEqualTo(StoryRules.Threads), name);
                Assert.That(RoleStoryPools.Fingerprint(start), Is.EqualTo(recorded[name]), name);
            }
        }

        /// <summary>The Fallout weighs nothing under the role moments - quiet tears takes its place - and what it always weighed otherwise.</summary>
        [Test]
        public void TheFalloutWeighsNothingOnlyUnderTheRoleMoments()
        {
            var fallout = StoryCatalog.Find("the-fallout");
            var roles = TearsCastable(StoryAnchors.NomsSet);
            var binding = new ArcBinding();
            Assert.That(fallout.weight(new StoryContext(roles, StoryAnchors.BlockSet), binding), Is.Zero);
            var threads = roles.Clone();
            threads.story.rulesVersion = StoryRules.Threads;
            Assert.That(fallout.weight(new StoryContext(threads, StoryAnchors.BlockSet), binding), Is.EqualTo(10));
            var off = roles.Clone();
            off.commitmentRulesStartWeek = 0;
            Assert.That(fallout.weight(new StoryContext(off, StoryAnchors.BlockSet), binding), Is.EqualTo(10), "Story rules Roles alone is not the role moments.");
            Assert.That(fallout.rulesVersion, Is.EqualTo(StoryRules.Grudges));
            Assert.That(StoryCatalog.Find(Tears).weight(new StoryContext(roles, StoryAnchors.BlockSet), binding), Is.EqualTo(10), "Quiet tears weighs The Fallout's 10.");
        }

        /// <summary>
        /// A role moment that casts also weighs on "nothing" (§5.5): quiet tears' 10 is added to the reach rules' 40, so a moment
        /// comes with (o + r) / (o + 2r + 40) - the role moments mostly take the place of other moments. Without them "nothing" is 40.
        /// </summary>
        [Test]
        public void ARoleMomentAlsoWeighsOnNothing()
        {
            var s = TearsCastable(StoryAnchors.NomsSet);
            var ctx = new StoryContext(s, StoryAnchors.NomsSet);
            var pool = EpisodeEngine.PoolCandidates(s, ctx);
            Assert.That(pool.Where(c => c.template.rulesVersion >= StoryRules.Roles).Select(c => (c.template.id, c.weight)), Is.EqualTo(new[] { (Tears, 10.0) }));
            Assert.That(EpisodeEngine.PoolNothing(s, StoryAnchors.NomsSet, pool), Is.EqualTo(40 + 10));
            var others = pool.Where(c => c.template.rulesVersion < StoryRules.Roles).ToList();
            Assert.That(EpisodeEngine.PoolNothing(s, StoryAnchors.NomsSet, others), Is.EqualTo(40), "Only a Roles arc adds to it.");
            // Nor an arc of the milestone just below, were one ever in a pool: no thread has a start anchor today, so only this says so.
            var thread = StoryCatalog.All.First(t => t.rulesVersion == StoryRules.Threads);
            Assert.That(EpisodeEngine.PoolNothing(s, StoryAnchors.NomsSet, others.Append((thread, new ArcBinding(), 10.0))), Is.EqualTo(40), "A Threads arc adds nothing.");
            Assert.That(EpisodeEngine.PoolNothing(s, StoryAnchors.EvictionNight, pool), Is.EqualTo(25 + 10));
        }

        /// <summary>
        /// Quiet tears is in the house's pool at the nominations, the veto competition and the block, weighing 10 - and at no other
        /// anchor of the week.
        /// </summary>
        [Test]
        public void QuietTearsIsInThePoolAtItsThreeAnchors()
        {
            foreach (string anchor in new[] { StoryAnchors.NomsSet, StoryAnchors.VetoWon, StoryAnchors.BlockSet })
            {
                var s = TearsCastable(anchor);
                Assert.That(EpisodeEngine.PoolCandidates(s, new StoryContext(s, anchor)).Where(c => c.template.id == Tears).Select(c => c.weight),
                    Is.EqualTo(new[] { 10.0 }), anchor);
                foreach (string other in new[] { StoryAnchors.HohCrowned, StoryAnchors.EvictionEve, StoryAnchors.EvictionNight })
                    Assert.That(EpisodeEngine.PoolCandidates(s, new StoryContext(s, other)).Select(c => c.template.id), Has.None.EqualTo(Tears), other);
            }
        }

        // ------------------------------------------------------------------ the pawn conversation

        /// <summary>
        /// In a week no backdoor is planned, a houseguest Head of Household who names the player calls them up on the coin's yes:
        /// the pawn conversation starts as the names are said, its opener the backdoor's, and its card is put to the player at once
        /// - "Just a Pawn", in the HoH room, closing at the veto competition, four answers. On a no nobody calls them up. The coin
        /// is the story's keyed stream: the season's own draws nothing for it.
        /// </summary>
        [Test]
        public void ThePawnConversationComesOnTheCoinInAWeekWithoutABackdoor()
        {
            var yes = PawnWeek(backdoor: false, coin: true);
            var s = yes.after;
            Assert.That(EpisodeEngine.BackdoorPlanned(s), Is.Null);
            var cycle = s.storylines.Single(x => x.templateId == PawnTalk);
            Assert.That(cycle.week, Is.EqualTo(s.week));
            Assert.That(cycle.cast.Select(r => (r.role, r.contestantId)), Is.EqualTo(new[] { ("HOH", yes.hohId) }));
            Assert.That(cycle.path.Select(p => (p.beatId, p.optionId, p.result)), Is.EqualTo(new[] { ("plan", "pawn", StoryResults.Npc) }));
            var card = JustAPawn(s);
            Assert.That(card.cycleId, Is.EqualTo(cycle.id));
            Assert.That(card.contentId, Is.EqualTo(PawnTalk + ":just-a-pawn"));
            Assert.That((card.title, card.venue, card.surface, card.closesAnchor, card.lapseOptionId),
                Is.EqualTo(("Just a Pawn", StoryVenues.HohRoom, StorySurfaces.Conversation, StoryAnchors.VetoWon, "say-nothing")));
            Assert.That(card.choices.Select(c => c.label), Is.EqualTo(new[] { "Take their word for it", "Get it in writing", "Call it what it is", "Say nothing" }));
            Assert.That(card.involvedIds, Is.EqualTo(new[] { yes.hohId }));
            Assert.That(EpisodeEngine.PawnTalkHad(s, yes.hohId), Is.True);

            // The same names said with the conversation already had: the season's stream is where it was.
            var had = yes.before.Clone();
            had.storylines.Add(Earlier(PawnTalk, yes.hohId, "pawn"));
            var without = Apply(new EpisodeEngine(had), Command(had, EpisodeCommandKind.Advance));
            Assert.That(without.storylines.Count(x => x.templateId == PawnTalk), Is.EqualTo(1), "Only the earlier one.");
            Assert.That(s.randomState, Is.EqualTo(without.randomState), "The coin and the card draw nothing from the season's stream.");

            // The same Head of Household, the same coin, the player left off the block: nobody calls them up.
            var safe = Shaped(yes.walked, yes.hohId, false, false, out _);
            var spared = Apply(new EpisodeEngine(safe), Command(safe, EpisodeCommandKind.Advance));
            Assert.That(spared.nominees, Does.Not.Contain(spared.playerId));
            Assert.That(EpisodeEngine.PawnTalkComes(spared), Is.False);
            Assert.That(spared.storylines.Select(x => x.templateId), Has.None.EqualTo(PawnTalk));

            var no = PawnWeek(backdoor: false, coin: false, drawn: false);
            Assert.That(no.after.nominees, Does.Contain(no.after.playerId));
            Assert.That(no.after.storylines.Select(x => x.templateId), Has.None.EqualTo(PawnTalk));
            Assert.That(JustAPawn(no.after), Is.Null, "On a no nobody calls them up.");
        }

        /// <summary>
        /// In a backdoor week the backdoor's own plan takes the same coin: on a yes its Just a Pawn is the card and no pawn
        /// conversation starts beside it; on a no the plan goes quiet - the backdoor runs, and nobody calls the player up. A
        /// season without the role moments calls every pawn up, as it always did.
        /// </summary>
        [Test]
        public void InABackdoorWeekThePlanTakesTheSameCoin()
        {
            var yes = PawnWeek(backdoor: true, coin: true);
            var plan = yes.after.storylines.Single(x => x.templateId == Backdoor && x.week == yes.after.week);
            Assert.That(plan.path.First().optionId, Is.EqualTo("pawn"));
            Assert.That(JustAPawn(yes.after).contentId, Is.EqualTo(Backdoor + ":just-a-pawn"));
            Assert.That(yes.after.storylines.Select(x => x.templateId), Has.None.EqualTo(PawnTalk), "One conversation, never two.");

            var no = PawnWeek(backdoor: true, coin: false, drawn: false);
            var quiet = no.after.storylines.Single(x => x.templateId == Backdoor && x.week == no.after.week);
            Assert.That(quiet.path.First().optionId, Is.EqualTo("quiet"));
            Assert.That(EpisodeEngine.BackdoorPlanned(no.after), Is.EqualTo(no.target), "The plan stands.");
            Assert.That(JustAPawn(no.after), Is.Null);
            Assert.That(no.after.storylines.Select(x => x.templateId), Has.None.EqualTo(PawnTalk));

            var old = PawnWeek(backdoor: true, coin: false, roles: false);
            Assert.That(old.after.storylines.Single(x => x.templateId == Backdoor && x.week == old.after.week).path.First().optionId, Is.EqualTo("pawn"),
                "Without the role moments the coin is never read.");
        }

        /// <summary>
        /// The pawn conversation looks the same with and without a backdoor behind it (§5.3): the same Head of Household naming the
        /// same player in the same week, once with a plan and once without, draws the same card - eyebrow, arc and beat title,
        /// narrative, venue, surface, closing anchor, who it names and stages, and every answer's caption, words, risk, approach,
        /// lock, cost and consequence - and the storyline the player sees running has the same name.
        /// </summary>
        [Test]
        public void ThePawnConversationReadsTheSameWithAndWithoutABackdoor()
        {
            foreach (var walked in AtTheHoH())
                foreach (string hoh in Contenders(walked).Where(id => Coin(walked, id)))
                {
                    var backdoor = Named(walked, hoh, backdoor: true, out string target);
                    var plain = Named(walked, hoh, backdoor: false, out _);
                    if (backdoor == null || plain == null || EpisodeEngine.BackdoorPlanned(backdoor) != target || EpisodeEngine.BackdoorPlanned(plain) != null) continue;
                    var withPlan = JustAPawn(backdoor);
                    var without = JustAPawn(plain);
                    if (withPlan == null || without == null) continue;
                    Assert.That(withPlan.contentId, Is.EqualTo(Backdoor + ":just-a-pawn"));
                    Assert.That(without.contentId, Is.EqualTo(PawnTalk + ":just-a-pawn"));
                    Assert.That(Card(plain, without), Is.EqualTo(Card(backdoor, withPlan)));
                    Assert.That(Card(backdoor, withPlan), Does.Contain(backdoor.Find(hoh).name), "The card names the Head of Household.");
                    Assert.That(Card(backdoor, withPlan), Does.Not.Contain(backdoor.Find(target).name), "Never the target.");
                    return;
                }
            Assert.Fail("No Head of Household's coin said yes in both variants.");
        }

        /// <summary>
        /// At most once per Head of Household a season, whichever variant came before: an earlier conversation with this Head of
        /// Household - a pawn conversation, or a backdoor plan that called the player up - keeps the next one away, coin or no;
        /// one with another Head of Household, or a plan that went quiet, does not. The backdoor's plan reads the same record.
        /// </summary>
        [Test]
        public void ThePawnConversationComesOncePerHeadOfHouseholdASeason()
        {
            var pawn = PawnWeek(backdoor: false, coin: true);
            string other = pawn.before.Active.First(c => !c.isPlayer && c.id != pawn.hohId).id;
            foreach (var (record, comes) in new[]
            {
                (Earlier(PawnTalk, pawn.hohId, "pawn"), false),
                (Earlier(Backdoor, pawn.hohId, "pawn"), false),
                (Earlier(PawnTalk, other, "pawn"), true),
                (Earlier(Backdoor, pawn.hohId, "quiet"), true),
            })
            {
                var s = pawn.before.Clone();
                s.storylines.Add(record);
                Valid(s);
                var after = Apply(new EpisodeEngine(s), Command(s, EpisodeCommandKind.Advance));
                string what = record.templateId + " " + record.path[0].optionId + (record.cast[0].contestantId == pawn.hohId ? " with them" : " with another");
                Assert.That(after.storylines.Any(x => x.templateId == PawnTalk && x.week == after.week), Is.EqualTo(comes), what);
                Assert.That(JustAPawn(after) != null, Is.EqualTo(comes), what);
            }

            var backdoor = PawnWeek(backdoor: true, coin: true);
            var again = backdoor.before.Clone();
            again.storylines.Add(Earlier(PawnTalk, backdoor.hohId, "pawn"));
            var quiet = Apply(new EpisodeEngine(again), Command(again, EpisodeCommandKind.Advance));
            Assert.That(quiet.storylines.Single(x => x.templateId == Backdoor && x.week == quiet.week).path.First().optionId, Is.EqualTo("quiet"),
                "Had once already, the backdoor's plan goes quiet on a yes.");
            Assert.That(JustAPawn(quiet), Is.Null);
        }

        /// <summary>
        /// The twins: a season without the role moments draws exactly the shipped four answers - shipped ids, captions, words,
        /// no seat - and stages the whole plan; under the role moments the twins stand in their place under the same captions,
        /// taking their word saying what it now asks, getting it in writing costing the seat, and only the Head of Household on
        /// the card. Four answers either way.
        /// </summary>
        [Test]
        public void TheTwinsAreHiddenInOldSeasonsAndShownUnderTheRoleMoments()
        {
            var old = PawnWeek(backdoor: true, coin: false, roles: false);
            var shipped = JustAPawn(old.after);
            // The saved words are name-free (StoryText.Neutral); the card names the Head of Household as it draws them.
            Assert.That(shipped.choices.Select(c => (c.optionId, c.label, c.description, c.costsAction)), Is.EqualTo(new[]
            {
                ("take-word", "Take their word for it", "Accept a houseguest's promise of safety.", false),
                ("in-writing", "Get it in writing", "Make a houseguest commit: a safety pact, not a promise.", false),
                ("call-it", "Call it what it is", "\"If I'm a pawn, who's the target?\"", false),
                ("say-nothing", "Say nothing", "Nod and go back downstairs.", false),
            }));
            Assert.That(StoryText.Description(old.after, shipped, shipped.choices[0]), Is.EqualTo("Accept " + old.after.Find(old.hohId).name + "'s promise of safety."));
            Assert.That(shipped.choices[0].effects.Select(e => e.kind), Is.EqualTo(new[] { StoryEffects.Promise }));
            Assert.That(shipped.involvedIds, Is.EquivalentTo(old.after.storylines.Single(x => x.id == shipped.cycleId).cast.Select(r => r.contestantId)),
                "The shipped card stages the whole plan.");
            Assert.That(shipped.involvedIds, Has.Count.EqualTo(3));

            var roles = PawnWeek(backdoor: true, coin: true);
            var twins = JustAPawn(roles.after);
            Assert.That(twins.choices.Select(c => (c.optionId, c.label, c.costsAction)), Is.EqualTo(new[]
            {
                ("take-word-roles", "Take their word for it", false),
                ("in-writing-roles", "Get it in writing", true),
                ("call-it", "Call it what it is", false),
                ("say-nothing", "Say nothing", false),
            }));
            Assert.That(twins.choices[0].description, Is.EqualTo("Accept a houseguest's promise of safety, and promise them yours through next week."));
            Assert.That(twins.choices.Skip(1).Select(c => c.description), Is.EqualTo(shipped.choices.Skip(1).Select(c => c.description)),
                "Only taking their word says more.");
            Assert.That(twins.involvedIds, Is.EqualTo(new[] { roles.hohId }));
            Assert.That(twins.cast.Select(r => r.role), Is.EqualTo(new[] { "HOH" }));
        }

        /// <summary>
        /// Taking their word, under the role moments: the Head of Household's promise of safety, the player's own back to them
        /// through next week, and +3 with them - in every commitment mode, through whichever store holds a safety promise.
        /// </summary>
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void TakingTheirWordGivesYourWordBackThroughNextWeek(int mode)
        {
            var pawn = PawnWeek(backdoor: false, coin: true, mode: mode);
            var s = pawn.after;
            var card = JustAPawn(s);
            var after = Apply(pawn.engine, Progress(s, card.id, "take-word-roles"));
            var theirs = CommitmentReferences.Promises(after).Where(p => p.kind == PromiseKind.Safety && p.week == s.week && p.status == PromiseStatus.Active
                && new[] { p.fromId, p.toId }.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { pawn.hohId, s.playerId }.OrderBy(id => id, StringComparer.Ordinal))).ToList();
            Assert.That(theirs.Select(p => (p.fromId, p.toId, p.expiresWeek)), Is.EquivalentTo(new[]
            {
                (pawn.hohId, s.playerId, s.week + 1),
                (s.playerId, pawn.hohId, s.week + 1),
            }), "mode " + mode);
            Assert.That(after.Score(s.playerId, pawn.hohId), Is.Not.EqualTo(s.Score(s.playerId, pawn.hohId)), "+3 with them, as they take it.");
            Assert.That(JustAPawn(s).choices.Single(c => c.optionId == "take-word-roles").effects
                .Single(e => e.kind == StoryEffects.Move).amount, Is.EqualTo(3));
            Assert.That(after.houseEvents.Single(e => e.id == card.id).resolved, Is.True);
        }

        /// <summary>
        /// Getting it in writing costs the window's seat under the role moments: refused with no seat left - nothing struck - and
        /// with one, the safety pact as shipped and a seat spent. The shipped answer costs nothing, as it always did.
        /// </summary>
        [Test]
        public void GettingItInWritingCostsTheWindowsSeat()
        {
            var pawn = PawnWeek(backdoor: false, coin: true);
            var s = pawn.after;
            var card = JustAPawn(s);
            Assert.That(card.choices.Single(c => c.optionId == "in-writing-roles").locked, Is.False, "The player is Analytical.");
            int spent = EpisodeEngine.SocialActionsSpent(s);
            var written = Apply(new EpisodeEngine(s), Progress(s, card.id, "in-writing-roles"));
            Assert.That(EpisodeEngine.SocialActionsSpent(written), Is.EqualTo(spent + 1));
            Assert.That(CommitmentReferences.Deals(written).Count(d => d.type == DealKind.SafetyAgreement && d.proposerId == pawn.hohId
                && d.recipientId == s.playerId && d.status == DealStatus.Active && d.week == s.week), Is.EqualTo(1));

            var engine = new EpisodeEngine(s);
            var spentOut = SpendTheWindow(engine);
            var refused = engine.Apply(Progress(spentOut, card.id, "in-writing-roles"));
            Assert.That((refused.accepted, refused.reason), Is.EqualTo((false, "You have no actions left this week.")));
            Assert.That(CommitmentReferences.Deals(engine.Snapshot).Count(d => d.type == DealKind.SafetyAgreement && d.proposerId == pawn.hohId
                && d.recipientId == s.playerId && d.week == s.week), Is.Zero, "Nothing struck.");

            var old = PawnWeek(backdoor: true, coin: false, roles: false);
            Assert.That(JustAPawn(old.after).choices.Single(c => c.optionId == "in-writing").costsAction, Is.False);
        }

        /// <summary>
        /// Calling it out in a week with no backdoor costs what it always did and earns the truth about whether a plan exists:
        /// "There's no one else. One of you two is going home." - never which nominee they would rather lose, and no warning to
        /// carry to anybody. In a backdoor week it names the target and moves on to the warning, as shipped.
        /// </summary>
        [Test]
        public void CallingItOutWithoutABackdoorSaysThereIsNoOneElse()
        {
            var pawn = PawnWeek(backdoor: false, coin: true);
            var s = pawn.after;
            var card = JustAPawn(s);
            var after = Apply(pawn.engine, Progress(s, card.id, "call-it"));
            Assert.That(after.Score(pawn.hohId, s.playerId) - s.Score(pawn.hohId, s.playerId), Is.EqualTo(-6).Within(1e-9));
            var line = after.events.Last(e => e.kind == StoryLog.Outcome);
            Assert.That(StoryText.Log(after, line), Is.EqualTo("Just a Pawn: " + s.Find(pawn.hohId).name
                + " stopped pretending. \"There's no one else. One of you two is going home.\""));
            var cycle = after.storylines.Single(x => x.templateId == PawnTalk);
            Assert.That((cycle.nextAnchor, StorylineStatus.Running(cycle.status)), Is.EqualTo(((string)null, true)), "It waits, and nobody is warned.");
            Assert.That(EpisodeEngine.OpenStoryBeats(after).Where(e => e.cycleId == cycle.id), Is.Empty);
            // It runs on to eviction night, as the backdoor does: the storylines the player sees running never tell them apart.
            var week = new EpisodeEngine(after);
            for (int i = 0; i < 60 && week.Snapshot.phase != EpisodePhase.Campaign; i++) Apply(week, EpisodeEngineTests.NextCommand(week.Snapshot));
            Assert.That(week.Snapshot.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(StorylineStatus.Running(week.Snapshot.storylines.Single(x => x.id == cycle.id).status), Is.True, "Still running at the block.");
            for (int i = 0; i < 60 && week.Snapshot.phase != EpisodePhase.Social; i++) Apply(week, EpisodeEngineTests.NextCommand(week.Snapshot));
            var ended = week.Snapshot.storylines.Single(x => x.id == cycle.id);
            Assert.That(StorylineStatus.Running(ended.status), Is.False);
            Assert.That(ended.endingId, Is.EqualTo(week.Snapshot.Find(week.Snapshot.playerId).status == ContestantStatus.Active ? "done" : "left-house"));

            var backdoor = PawnWeek(backdoor: true, coin: true);
            var named = Apply(backdoor.engine, Progress(backdoor.after, JustAPawn(backdoor.after).id, "call-it"));
            Assert.That(StoryText.Log(named, named.events.Last(e => e.kind == StoryLog.Outcome)), Does.Contain(named.Find(backdoor.target).name));
            Assert.That(named.storylines.Single(x => x.templateId == Backdoor && x.week == named.week).nextAnchor, Is.EqualTo(StoryAnchors.Conversation));
        }

        /// <summary>
        /// The pawn conversation shares the airtime (2 asks a week, one open), and its card never waits past its moment (R6's
        /// review: a card put off never asks its pulse again, and one put off with the week's asks spent surfaced at the next
        /// week's nominations, naming last week's Head of Household). With another ask still open, or the week's two asks spent,
        /// as the names are said, nobody is called up: the backdoor's plan goes quiet and stands, no pawn conversation starts, and
        /// nothing is had, so that Head of Household may call the player up another week. A season without the role moments calls
        /// the backdoor's pawn up as it always did, its card waiting for the veto competition.
        /// </summary>
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void WithoutRoomToAskAtTheNamesNobodyIsCalledUp(bool backdoor, bool open)
        {
            var pawn = PawnWeek(backdoor, coin: true);
            var s = Crowded(pawn.before, pawn.hohId, open);
            var after = Apply(new EpisodeEngine(s), Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.nominees, Does.Contain(after.playerId), "The names are said.");
            Assert.That(after.storylines.Select(x => x.templateId), Has.None.EqualTo(PawnTalk), "No pawn conversation.");
            Assert.That(after.houseEvents.Where(e => e.contentId != null && e.contentId.EndsWith(":just-a-pawn", StringComparison.Ordinal)), Is.Empty);
            Assert.That(EpisodeEngine.PawnTalkHad(after, pawn.hohId), Is.False, "Nothing had: they may call the player up another week.");
            if (!backdoor) return;
            Assert.That(EpisodeEngine.BackdoorPlanned(after), Is.EqualTo(pawn.target), "The plan stands.");
            Assert.That(after.storylines.Single(x => x.templateId == Backdoor && x.week == after.week).path.Select(p => p.optionId), Is.EqualTo(new[] { "quiet" }));

            var old = PawnWeek(backdoor: true, coin: false, roles: false);
            var shipped = Crowded(old.before, old.hohId, open);
            var named = Apply(new EpisodeEngine(shipped), Command(shipped, EpisodeCommandKind.Advance));
            var plan = named.storylines.Single(x => x.templateId == Backdoor && x.week == named.week);
            Assert.That(plan.path.Select(p => p.optionId), Is.EqualTo(new[] { "pawn" }), "Without the role moments the pawn is called up.");
            Assert.That(JustAPawn(named), Is.Null, "Its card waits.");
            Assert.That((plan.beatId, plan.nextAnchor, plan.nextWeek), Is.EqualTo(("just-a-pawn", StoryAnchors.VetoWon, named.week)));
        }

        /// <summary>
        /// Nobody is called up in week one before its eviction night (R6's review): the story's first ask comes then, and a card put
        /// off at week one's names surfaced after the eviction, still saying "before the veto players are drawn". Under the role
        /// moments a Head of Household whose coin says yes names the player and calls nobody up - the backdoor's plan quiet, no pawn
        /// conversation, nothing had - and the week plays on through its eviction night with no Just a Pawn. Without the role
        /// moments the backdoor's pawn is called up and its card waits, as shipped.
        /// </summary>
        [TestCase(false)] [TestCase(true)]
        public void NobodyIsCalledUpInWeekOne(bool backdoor)
        {
            var pawn = PawnWeek(backdoor, coin: true, drawn: false, week: 1);
            var s = pawn.after;
            Assert.That((s.week, EpisodeEngine.BeforeTheFirstAsk(s)), Is.EqualTo((1, true)));
            Assert.That(s.storylines.Select(x => x.templateId), Has.None.EqualTo(PawnTalk));
            Assert.That(EpisodeEngine.PawnTalkHad(s, pawn.hohId), Is.False);
            if (backdoor)
                Assert.That(s.storylines.Single(x => x.templateId == Backdoor && x.week == 1).path.Select(p => p.optionId), Is.EqualTo(new[] { "quiet" }));
            int steps = 0;
            for (; steps < 120 && pawn.engine.Snapshot.week == 1 && pawn.engine.Snapshot.phase != EpisodePhase.Finished; steps++)
            {
                Assert.That(pawn.engine.Snapshot.houseEvents.Where(e => e.contentId != null && e.contentId.EndsWith(":just-a-pawn", StringComparison.Ordinal)),
                    Is.Empty, "Week one, " + pawn.engine.Snapshot.phase + ": no Just a Pawn.");
                Apply(pawn.engine, EpisodeEngineTests.NextCommand(pawn.engine.Snapshot));
            }
            Assert.That(pawn.engine.Snapshot.week, Is.EqualTo(2), "The week played through to its end.");
            if (!backdoor) return;

            var old = PawnWeek(backdoor: true, coin: false, roles: false, drawn: false, week: 1);
            var plan = old.after.storylines.Single(x => x.templateId == Backdoor && x.week == 1);
            Assert.That(plan.path.Select(p => p.optionId), Is.EqualTo(new[] { "pawn" }), "Without the role moments the pawn is called up.");
            Assert.That(JustAPawn(old.after), Is.Null, "Its card waits.");
            Assert.That((plan.beatId, plan.nextAnchor), Is.EqualTo(("just-a-pawn", StoryAnchors.VetoWon)));
        }

        /// <summary>
        /// Named second is named (R6's review): a Head of Household who names the player as the second of the week's first two
        /// nominees calls them up as one who names them first does, in either variant.
        /// </summary>
        [TestCase(false)] [TestCase(true)]
        public void ThePlayerNamedSecondIsCalledUpToo(bool backdoor)
        {
            var pawn = PawnWeek(backdoor, coin: true, second: true);
            var s = pawn.after;
            Assert.That((s.nominees.Count, s.nominees[0] != s.playerId, s.nominees[1]), Is.EqualTo((2, true, s.playerId)));
            Assert.That(JustAPawn(s).contentId, Is.EqualTo((backdoor ? Backdoor : PawnTalk) + ":just-a-pawn"));
            Assert.That(EpisodeEngine.PawnTalkHad(s, pawn.hohId), Is.True);
        }

        /// <summary>
        /// Six in the house at least (R6's review), the backdoor's own floor: with six the conversation may come, with five nobody
        /// is called up, coin or no - at the final four there is one voter and no tie-break for a safety pact to weigh in.
        /// </summary>
        [Test]
        public void ThePawnConversationNeedsSixInTheHouse()
        {
            var s = Unasked(PawnWeek(backdoor: false, coin: true));
            var bystanders = s.Active.Where(c => !c.isPlayer && c.id != s.hohId && !s.nominees.Contains(c.id)).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var who in bystanders)
                if (s.Active.Count() > EpisodeEngine.PawnTalkMinActive) who.status = ContestantStatus.Evicted;
            Assert.That(s.Active.Count(), Is.EqualTo(6));
            Assert.That(EpisodeEngine.PawnTalkComes(s), Is.True, "Six: they may.");
            bystanders.First(c => c.status == ContestantStatus.Active).status = ContestantStatus.Evicted;
            Assert.That(s.Active.Count(), Is.EqualTo(5));
            Assert.That(EpisodeEngine.PawnTalkComes(s), Is.False, "Five: nobody is called up.");
        }

        /// <summary>
        /// Under the role moments neither variant holds the Game lane (R6's review): each runs to eviction night, so in the very
        /// weeks the player is on the block a running backdoor or pawn conversation kept out Call in the Favour and Final Plea. With
        /// either running - its card answered - and a favour owed, Call in the Favour casts at the block; any other Game-lane arc
        /// running still keeps it out. A season without the role moments keeps its backdoor in the lane, as it always did, and only
        /// the lane keeps the favour out there.
        /// </summary>
        [Test]
        public void UnderTheRoleMomentsNeitherVariantHoldsTheGameLane()
        {
            var favour = StoryCatalog.Find("call-in-the-favour");
            foreach (bool backdoor in new[] { false, true })
            {
                var pawn = PawnWeek(backdoor, coin: true);
                var s = Owed(Apply(pawn.engine, Progress(pawn.after, JustAPawn(pawn.after).id, "say-nothing")));
                var running = s.storylines.Single(x => x.templateId == (backdoor ? Backdoor : PawnTalk) && x.week == s.week);
                Assert.That((running.lane, StorylineStatus.Running(running.status)), Is.EqualTo((StoryLanes.Game, true)));
                Assert.That(EpisodeEngine.Castable(s, new StoryContext(s, StoryAnchors.BlockSet), favour), Is.Not.Null, backdoor ? "The backdoor running." : "The pawn conversation running.");
                // Every other Game-lane arc still holds the lane: a secret got out, its card already gone.
                var held = s.Clone();
                string card = RoleVetoBoardTests.StoryBeat(held, "loose-lips", "it-got-out", "ALLY", held.Active.First(c => !c.isPlayer && c.id != held.hohId).id,
                    false, "lane-held-" + backdoor, "A moment.");
                held.houseEvents.RemoveAll(e => e.id == card);
                Assert.That(EpisodeEngine.Castable(held, new StoryContext(held, StoryAnchors.BlockSet), favour), Is.Null, "Another Game-lane arc holds the lane.");
            }

            var old = PawnWeek(backdoor: true, coin: false, roles: false);
            var shipped = Owed(Apply(old.engine, Progress(old.after, JustAPawn(old.after).id, "say-nothing")));
            Assert.That(EpisodeEngine.Castable(shipped, new StoryContext(shipped, StoryAnchors.BlockSet), favour), Is.Null, "The backdoor holds the lane.");
            var ended = shipped.Clone();
            ended.storylines.Single(x => x.templateId == Backdoor && x.week == ended.week).status = StorylineStatus.Completed;
            Assert.That(EpisodeEngine.Castable(ended, new StoryContext(ended, StoryAnchors.BlockSet), favour), Is.Not.Null, "Only the lane kept it out.");
        }

        // ------------------------------------------------------------------ quiet tears

        /// <summary>
        /// Who put them there decides the answers. A houseguest Head of Household's nominee can be comforted, and at the block sat
        /// with a while; a Head of Household player can only say it was not personal, on a check. Pretending not to see is always
        /// there. Never more than four.
        /// </summary>
        [Test]
        public void TheAnswersTurnOnWhoPutThemThere()
        {
            var seen = new HashSet<string>();
            foreach (bool playerHoh in new[] { false, true })
                foreach (string anchor in new[] { StoryAnchors.NomsSet, StoryAnchors.VetoWon, StoryAnchors.BlockSet })
                {
                    var found = TearsCard(playerHoh, anchor, out _);
                    if (found == null) continue;
                    var s = found.Snapshot;
                    var card = OpenTears(s);
                    seen.Add((playerHoh ? "hoh " : "npc ") + anchor);
                    var expected = playerHoh ? new[] { "not-personal", "pretend" }
                        : anchor == StoryAnchors.BlockSet ? new[] { "comfort", "sit-a-while", "pretend" } : new[] { "comfort", "pretend" };
                    Assert.That(card.choices.Select(c => c.optionId), Is.EqualTo(expected), (playerHoh ? "player HoH" : "NPC HoH") + " at " + anchor);
                    Assert.That(card.choices.Select(c => c.label), Is.SubsetOf(new[] { "Comfort them", "Tell them it wasn't personal", "Sit with them a while", "Pretend you didn't see" }));
                    Assert.That(card.choices.Count, Is.LessThanOrEqualTo(4));
                    Assert.That(StoryText.Narrative(s, card), Is.EqualTo("You spot " + s.Find(card.involvedIds.Single()).name + " crying quietly in the living room."));
                    Assert.That((card.venue, card.lapseOptionId), Is.EqualTo((StoryVenues.Living, "pretend")));
                    string subject = card.cast.Single(r => r.role == "SUBJECT").contestantId;
                    Assert.That(s.nominees, Does.Contain(subject));
                    Assert.That(subject, Is.Not.EqualTo(s.hohId).And.Not.EqualTo(s.playerId));
                }
            Assert.That(seen, Does.Contain("npc " + StoryAnchors.NomsSet).And.Contain("npc " + StoryAnchors.BlockSet), "Seen: " + string.Join(", ", seen));
            // Each answer's own rule, read as the card is drawn, for each Head of Household at each anchor.
            var npcHoh = TearsCastable(StoryAnchors.NomsSet);
            var playerHohState = npcHoh.Clone();
            playerHohState.hohId = playerHohState.playerId;
            var beat = StoryCatalog.Find(Tears).Beat("tears");
            foreach (var (state, who) in new[] { (npcHoh, "npc"), (playerHohState, "hoh") })
                foreach (string anchor in new[] { StoryAnchors.NomsSet, StoryAnchors.VetoWon, StoryAnchors.BlockSet })
                {
                    var shown = beat.options.Where(o => o.showIf == null || o.showIf(new StoryContext(state, anchor), null)).Select(o => o.id).ToList();
                    var expected = who == "hoh" ? new[] { "not-personal", "pretend" }
                        : anchor == StoryAnchors.BlockSet ? new[] { "comfort", "sit-a-while", "pretend" } : new[] { "comfort", "pretend" };
                    Assert.That(shown, Is.EqualTo(expected), who + " at " + anchor);
                }
            Assert.That(seen.Any(x => x.StartsWith("hoh ", StringComparison.Ordinal)), Is.True, "Seen: " + string.Join(", ", seen));
        }

        /// <summary>
        /// Comforting them: +5 with them and a memory that names nobody - "Comforted me in week 2." - as the player's own line of
        /// the moment names nobody; and when a houseguest Head of Household put them up, the keyed even chance that the Head of
        /// Household noticed, -3. Both ways of the chance, each where its key says.
        /// </summary>
        [Test]
        public void ComfortingThemLeavesANamelessMemoryAndTheHeadOfHouseholdMayNotice()
        {
            var noticed = new HashSet<bool>();
            foreach (var engine in new[] { StoryAnchors.NomsSet, StoryAnchors.VetoWon, StoryAnchors.BlockSet }.SelectMany(anchor => TearsCards(playerHoh: false, anchor)))
            {
                var s = engine.Snapshot;
                var card = OpenTears(s);
                string subject = card.cast.Single(r => r.role == "SUBJECT").contestantId, hoh = s.hohId;
                bool seen = StoryRandom.Chance(s, "w" + s.week + ":tears-seen:" + subject, 0.5);
                var comfort = card.choices.Single(c => c.optionId == "comfort");
                Assert.That(comfort.effects.Any(e => e.kind == StoryEffects.View && e.fromId == hoh && e.toId == s.playerId && e.amount == -3), Is.EqualTo(seen));
                var after = Apply(engine, Progress(s, card.id, "comfort"));
                Assert.That(after.Score(hoh, s.playerId) - s.Score(hoh, s.playerId), Is.EqualTo(seen ? -3 : 0).Within(1e-9));
                // +5 with them, as they take it: the story's reception of a warm approach, as every story option's.
                Assert.That(after.Score(s.playerId, subject), Is.Not.EqualTo(s.Score(s.playerId, subject)));
                var theirs = after.memories.Where(m => m.ownerId == subject && m.subjectId == s.playerId && m.week == s.week && m.text.StartsWith("Comforted", StringComparison.Ordinal)).ToList();
                Assert.That(theirs.Select(m => m.text), Is.EqualTo(new[] { "Comforted me in week " + s.week + "." }));
                var mine = after.memories.Where(m => m.ownerId == s.playerId && m.subjectId == subject && m.text.StartsWith("Quiet Tears", StringComparison.Ordinal)).ToList();
                Assert.That(mine.Select(m => m.text), Is.EqualTo(new[] { "Quiet Tears: Comfort them." }));
                foreach (var memory in theirs.Concat(mine))
                    foreach (var who in after.contestants)
                        Assert.That(memory.text, Does.Not.Contain(who.name), "A memory names nobody.");
                noticed.Add(seen);
                if (noticed.Count == 2) break;
            }
            Assert.That(noticed, Is.EquivalentTo(new[] { false, true }), "Both ways of the chance.");
        }

        /// <summary>
        /// Telling them it was not personal is a check at the shown odds, on the beat's own keyed roll: on success their grudge
        /// against the player eases by 25, and +2 as they take it; on a backfire -3, and the grudge stands. Both ways, the same
        /// card with the roll its id keys landing each side of the odds.
        /// </summary>
        [Test]
        public void TellingThemItWasntPersonalIsACheck()
        {
            var engine = new[] { StoryAnchors.NomsSet, StoryAnchors.VetoWon, StoryAnchors.BlockSet }
                .SelectMany(anchor => TearsCards(playerHoh: true, anchor)).FirstOrDefault();
            Assert.That(engine, Is.Not.Null, "No week brought a Head of Household player's nominee to tears.");
            var drawn = engine.Snapshot;
            var card = OpenTears(drawn);
            string subject = card.cast.Single(r => r.role == "SUBJECT").contestantId;
            var choice = card.choices.Single(c => c.optionId == "not-personal");
            Assert.That((choice.checkBase, choice.subjectId), Is.EqualTo((50.0, subject)));
            Assert.That(choice.effects.Select(e => (e.kind, e.fromId, e.toId, e.amount)), Is.EqualTo(new[]
            {
                (StoryEffects.Ease, subject, drawn.playerId, 25.0), (StoryEffects.Move, drawn.playerId, subject, 2.0),
            }));
            Assert.That(choice.backfire.Select(e => (e.kind, e.fromId, e.toId, e.amount)), Is.EqualTo(new[] { (StoryEffects.Move, drawn.playerId, subject, -3.0) }));
            double grudge = Grudges.Severity(drawn, subject, drawn.playerId);
            Assert.That(grudge, Is.GreaterThan(25), "They resent the nomination.");
            int chance = StoryOdds.Chance(drawn, card, choice);
            Assert.That(chance, Is.InRange(1, 99));
            foreach (bool success in new[] { true, false })
            {
                var s = drawn.Clone();
                var item = OpenTears(s);
                for (int n = 0; StoryRandom.Unit(s, item.id + ":not-personal:check") * 100 < chance != success; n++)
                {
                    Assert.That(n, Is.LessThan(64));
                    item.id = "house-event-tears-" + n;
                    s.storylines.Single(x => x.id == item.cycleId).eventId = item.id;
                }
                var after = Apply(new EpisodeEngine(s), Progress(s, item.id, "not-personal"));
                Assert.That(Grudges.Severity(after, subject, s.playerId), Is.EqualTo(success ? grudge - 25 : grudge).Within(1e-9), success ? "success" : "backfire");
                if (!success) Assert.That(after.Score(s.playerId, subject), Is.LessThan(s.Score(s.playerId, subject)), "A backfire is -3, which no reception touches.");
                Assert.That(after.storylines.Single(x => x.id == item.cycleId).path.Last().result, Is.EqualTo(success ? StoryResults.Success : StoryResults.Backfire));
            }
        }

        /// <summary>
        /// Sitting with them a while, at the block: the window's seat, then Comfort's +5 and memory and a favour they owe the player;
        /// with no seat left it is refused and nothing is owed.
        /// </summary>
        [Test]
        public void SittingWithThemAWhileCostsTheSeatAndLeavesAFavourOwed()
        {
            var engine = TearsCard(playerHoh: false, StoryAnchors.BlockSet, out _);
            Assert.That(engine, Is.Not.Null, "No block brought quiet tears.");
            var s = engine.Snapshot;
            var card = OpenTears(s);
            string subject = card.cast.Single(r => r.role == "SUBJECT").contestantId;
            Assert.That(card.choices.Single(c => c.optionId == "sit-a-while").costsAction, Is.True);
            int spent = EpisodeEngine.SocialActionsSpent(s);
            var sat = Apply(new EpisodeEngine(s), Progress(s, card.id, "sit-a-while"));
            Assert.That(EpisodeEngine.SocialActionsSpent(sat), Is.EqualTo(spent + 1));
            Assert.That(Hooks.Has(sat, s.playerId, subject), Is.True, "They owe the player a favour.");
            Assert.That(sat.memories.Count(m => m.ownerId == subject && m.subjectId == s.playerId && m.text == "Comforted me in week " + s.week + "."), Is.EqualTo(1));
            Assert.That(sat.Score(s.playerId, subject), Is.Not.EqualTo(s.Score(s.playerId, subject)), "+5, as they take it.");

            var spentOut = SpendTheWindow(engine);
            var refused = engine.Apply(Progress(spentOut, card.id, "sit-a-while"));
            Assert.That((refused.accepted, refused.reason), Is.EqualTo((false, "You have no actions left this week.")));
            Assert.That(Hooks.Has(engine.Snapshot, s.playerId, subject), Is.EqualTo(Hooks.Has(s, s.playerId, subject)), "Nothing owed.");
        }

        /// <summary>
        /// Quiet tears comes at most once a week across its three anchors: once it has come, it rests the week - the only thing
        /// keeping it away at the next anchor, the person's and the pair's rests and the airtime aside - and comes again from next week.
        /// </summary>
        [Test]
        public void QuietTearsComesAtMostOnceAWeek()
        {
            foreach (var engine in TearsCards(playerHoh: false, StoryAnchors.NomsSet))
            {
                var s = engine.Snapshot;
                var card = OpenTears(s);
                var after = Apply(engine, Progress(s, card.id, "pretend"));
                if (!EpisodeEngine.AirtimeFor(after, StorySurfaces.Scene)) continue;
                var rested = after.Clone();
                rested.story.cooldowns.RemoveAll(c => c.key.StartsWith("star:", StringComparison.Ordinal) || c.key.StartsWith("pair:", StringComparison.Ordinal));
                var arc = StoryCatalog.Find(Tears);
                foreach (string anchor in new[] { StoryAnchors.VetoWon, StoryAnchors.BlockSet })
                    Assert.That(EpisodeEngine.Castable(rested, new StoryContext(rested, anchor), arc), Is.Null, anchor + ": it has come this week.");
                var fresh = rested.Clone();
                fresh.story.cooldowns.RemoveAll(c => c.key == "tpl:" + Tears);
                Assert.That(EpisodeEngine.Castable(fresh, new StoryContext(fresh, StoryAnchors.VetoWon), arc), Is.Not.Null, "Only its own rest kept it away.");
                Assert.That(rested.story.cooldowns.Single(c => c.key == "tpl:" + Tears).untilWeek, Is.EqualTo(s.week + 1), "It comes again next week.");
                return;
            }
            Assert.Fail("No week had airtime left after quiet tears.");
        }

        /// <summary>
        /// Quiet tears shares the airtime: with another ask open it does not cast, and with it answered it does.
        /// </summary>
        [Test]
        public void QuietTearsWaitsForAirtime()
        {
            var s = TearsCastable(StoryAnchors.NomsSet).Clone();
            var arc = StoryCatalog.Find(Tears);
            string npc = s.Active.First(c => !c.isPlayer && c.id != s.hohId && !s.nominees.Contains(c.id)).id;
            string open = RoleVetoBoardTests.StoryBeat(s, "the-lobby", "lobby", "LOBBYIST", npc, false, "tears-airtime", "A moment.");
            s.storylines.Single(x => x.id == "story-role-tears-airtime").lane = StoryLanes.Game;
            Valid(s);
            Assert.That(EpisodeEngine.Castable(s, new StoryContext(s, StoryAnchors.NomsSet), arc), Is.Null, "Another ask is open.");
            s.houseEvents.Single(e => e.id == open).resolved = true;
            s.houseEvents.Single(e => e.id == open).outcome = "It passed.";
            if (EpisodeEngine.AirtimeFor(s, StorySurfaces.Scene))
                Assert.That(EpisodeEngine.Castable(s, new StoryContext(s, StoryAnchors.NomsSet), arc), Is.Not.Null, "Answered, there is room.");
            else Assert.That(EpisodeEngine.Castable(s, new StoryContext(s, StoryAnchors.NomsSet), arc), Is.Null, "The week's two asks are spent.");
        }

        /// <summary>
        /// Never over the veto board: while the player holds the veto with asks waiting on it, quiet tears does not cast at the
        /// veto competition - one interruption at a time - and once the board is answered it may.
        /// </summary>
        [Test]
        public void QuietTearsNeverComesOverTheVetoBoard()
        {
            var engine = RoleVetoBoardTests.Board(RoleTone.Pragmatic, out string first, out string second);
            var s = engine.Snapshot;
            var arc = StoryCatalog.Find(Tears);
            Assert.That(RoleOffers.BoardAsks(s), Is.Not.Empty);
            Assert.That(arc.cast(new StoryContext(s, StoryAnchors.VetoWon)), Is.Null, "The board is up.");
            Assert.That(EpisodeEngine.Castable(s, new StoryContext(s, StoryAnchors.VetoWon), arc), Is.Null);
            Assert.That(s.houseEvents.Where(e => e.contentId == Tears + ":tears" && e.week == s.week && e.closesAnchor == StoryAnchors.BlockSet), Is.Empty,
                "Nothing came at the veto competition.");
            Assert.That(arc.cast(new StoryContext(s, StoryAnchors.BlockSet)), Is.Not.Null, "The board is the veto competition's alone.");
            var answered = s;
            foreach (var ask in RoleOffers.BoardAsks(s).ToList())
                answered = RoleVetoBoardTests.Apply(engine, RoleVetoBoardTests.Answer(answered, ask.id, false));
            Assert.That(RoleOffers.BoardAsks(answered), Is.Empty);
            Assert.That(arc.cast(new StoryContext(answered, StoryAnchors.VetoWon)), Is.Not.Null, "Answered, the board is down.");
        }

        /// <summary>
        /// Not in week one before its eviction night: the nominees are there and the cast would cast, but the story's first-ask
        /// rule keeps every arc but the first night's away - and a season walked under the role moments never draws it in week one.
        /// </summary>
        [Test]
        public void QuietTearsNeverComesInWeekOneBeforeItsEvictionNight()
        {
            var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7611u);
            ShippedRules.ApplyFresh(start);
            start.story.rulesVersion = StoryRules.Roles;
            var engine = new EpisodeEngine(start);
            for (int i = 0; i < 200 && !(engine.Snapshot.week == 1 && engine.Snapshot.nominees.Count == 2 && engine.Snapshot.phase == EpisodePhase.Nomination); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That((s.week, s.phase, s.nominees.Count), Is.EqualTo((1, EpisodePhase.Nomination, 2)));
            Assert.That(EpisodeEngine.BeforeTheFirstAsk(s), Is.True);
            var arc = StoryCatalog.Find(Tears);
            if (s.nominees.Any(id => id != s.playerId && id != s.hohId))
                Assert.That(arc.cast(new StoryContext(s, StoryAnchors.NomsSet)), Is.Not.Null);
            foreach (string anchor in arc.startAnchors)
                Assert.That(EpisodeEngine.Castable(s, new StoryContext(s, anchor), arc), Is.Null, anchor);
        }

        /// <summary>
        /// Seasons walked under the role moments to their finish: every step valid; quiet tears comes, never in week one and at
        /// most once a week; The Fallout never comes; no Head of Household calls the player up as a pawn twice; and every pawn
        /// card is put to the player as the names are said, in its own week, after week one, with six or more in the house - none
        /// waits for later, and no call goes without its card.
        /// </summary>
        [Test]
        public void SeasonsUnderTheRoleMomentsKeepTheirMomentsInBounds()
        {
            int tears = 0, pawns = 0;
            // 7601, 7602 and 7682 call the player up: a backdoor at six in the house, a pawn conversation, and two backdoors.
            foreach (uint seed in new uint[] { 7601, 7602, 7682, 7621, 7622, 7623, 7624, 7625, 7626, 7627, 7628 })
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = StoryRules.Roles;
                var engine = new EpisodeEngine(start);
                var cards = new HashSet<string>();
                for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    foreach (var card in s.houseEvents.Where(e => e.contentId != null && e.contentId.EndsWith(":just-a-pawn", StringComparison.Ordinal) && cards.Add(e.id)))
                    {
                        var cycle = s.storylines.Single(x => x.id == card.cycleId);
                        string where = "seed " + seed + " week " + s.week + " " + s.phase + ": ";
                        Assert.That((card.week, s.week, s.phase, s.nominees.Count), Is.EqualTo((cycle.week, cycle.week, EpisodePhase.Nomination, 2)), where + "as the names are said.");
                        Assert.That(s.week, Is.GreaterThan(1), where + "never in week one.");
                        Assert.That(s.Active.Count(), Is.GreaterThanOrEqualTo(EpisodeEngine.PawnTalkMinActive), where + "six or more in the house.");
                    }
                    var step = engine.Apply(RoleStoryPools.Next(s));
                    Assert.That(step.accepted, Is.True, "seed " + seed + " week " + s.week + " " + s.phase + ": " + step.reason);
                }
                var end = engine.Snapshot;
                Assert.That(end.phase, Is.EqualTo(EpisodePhase.Finished), "seed " + seed);
                var cried = end.storylines.Where(x => x.templateId == Tears).ToList();
                tears += cried.Count;
                Assert.That(cried.Where(x => x.week == 1), Is.Empty, "seed " + seed + ": never in week one.");
                Assert.That(cried.GroupBy(x => x.week).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, "seed " + seed + ": once a week.");
                Assert.That(end.storylines.Where(x => x.templateId == "the-fallout"), Is.Empty, "seed " + seed + ": quiet tears takes The Fallout's place.");
                var called = end.storylines.Where(x => (x.templateId == PawnTalk || x.templateId == Backdoor)
                    && x.path.Any(p => p.beatId == "plan" && p.optionId == "pawn")).ToList();
                var calls = called.GroupBy(x => x.cast.Single(r => r.role == "HOH").contestantId);
                Assert.That(calls.Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, "seed " + seed + ": once per Head of Household.");
                Assert.That(called.Where(x => !end.houseEvents.Any(e => e.cycleId == x.id && e.contentId.EndsWith(":just-a-pawn", StringComparison.Ordinal))).Select(x => x.id),
                    Is.Empty, "seed " + seed + ": every call has its card.");
                pawns += called.Count;
            }
            Assert.That(tears, Is.GreaterThan(0), "Quiet tears came in some season.");
            Assert.That(pawns, Is.GreaterThan(0), "A Head of Household called the player up in some season.");
        }

        /// <summary>
        /// Quiet tears' subject is a houseguest nominee, never the Head of Household nor the veto's holder, by a keyed pick weighted
        /// by max(1, Warm + 1): across many seasons' keys, a warm nominee (Warm 3, weight 4) is picked four times as often as a cold
        /// one, whose negative Warm still weighs one.
        /// </summary>
        [Test]
        public void TheSubjectIsANomineeWeightedToTheWarm()
        {
            var s = TearsCastable(StoryAnchors.NomsSet).Clone();
            var nominees = s.nominees.Where(id => id != s.playerId).ToList();
            Assert.That(nominees, Has.Count.EqualTo(2));
            s.Find(nominees[0]).traits = new List<string> { "Emotional", "Loyal" };
            s.Find(nominees[1]).traits = new List<string> { "Manipulative", "Confrontational" };
            Assert.That(Personality.Of(s.Find(nominees[0])).Warm, Is.EqualTo(3));
            Assert.That(Personality.Of(s.Find(nominees[1])).Warm, Is.EqualTo(-2));
            var cast = StoryCatalog.Find(Tears).cast;
            int warm = 0, samples = 2000;
            for (int i = 0; i < samples; i++)
            {
                s.seed = (uint)(90000 + i);
                string subject = cast(new StoryContext(s, StoryAnchors.NomsSet)).Get("SUBJECT");
                Assert.That(nominees, Does.Contain(subject));
                if (subject == nominees[0]) warm++;
            }
            Assert.That(warm / (double)samples, Is.EqualTo(0.8).Within(0.04), "4 to 1.");
            // Never the Head of Household, were they somehow on the block.
            var odd = s.Clone();
            odd.nominees = new List<string> { odd.hohId, nominees[1] };
            for (int i = 0; i < 50; i++)
            {
                odd.seed = (uint)(91000 + i);
                Assert.That(cast(new StoryContext(odd, StoryAnchors.NomsSet)).Get("SUBJECT"), Is.EqualTo(nominees[1]));
            }
            // Nor the nominee who has just won the veto (R6's review): they hold their own way off the block.
            var holder = s.Clone();
            holder.vetoHolderId = nominees[0];
            Assert.That(RoleOffers.BoardAsks(holder), Is.Empty);
            for (int i = 0; i < 50; i++)
            {
                holder.seed = (uint)(92000 + i);
                Assert.That(cast(new StoryContext(holder, StoryAnchors.VetoWon)).Get("SUBJECT"), Is.EqualTo(nominees[1]));
            }
        }

        // ------------------------------------------------------------------ storyline capacity

        /// <summary>
        /// Under the role moments the pool starts nothing new at 92 storylines, any arc alike, keeping the last eight for the arcs
        /// the rules start; a season without them casts there as it always did.
        /// </summary>
        [Test]
        public void UnderTheRoleMomentsThePoolStopsAtNinetyTwo()
        {
            var s = TearsCastable(StoryAnchors.NomsSet);
            var ctx = new StoryContext(s, StoryAnchors.NomsSet);
            var arc = StoryCatalog.Find(Tears);
            var below = Padded(s, EpisodeEngine.PoolStorylineCeiling - 1);
            Assert.That(EpisodeEngine.Castable(below, new StoryContext(below, StoryAnchors.NomsSet), arc), Is.Not.Null, "91 storylines: it casts.");
            var at = Padded(s, EpisodeEngine.PoolStorylineCeiling);
            Assert.That(EpisodeEngine.Castable(at, new StoryContext(at, StoryAnchors.NomsSet), arc), Is.Null, "92: nothing new.");
            Assert.That(EpisodeEngine.PoolCandidates(s, ctx), Is.Not.Empty);
            Assert.That(EpisodeEngine.PoolCandidates(at, new StoryContext(at, StoryAnchors.NomsSet)), Is.Empty, "Nothing at all.");

            var threads = s.Clone();
            threads.story.rulesVersion = StoryRules.Threads;
            var old = EpisodeEngine.PoolCandidates(threads, new StoryContext(threads, StoryAnchors.NomsSet)).Select(c => c.template.id).ToList();
            Assert.That(old, Is.Not.Empty, "Something casts here without the role moments.");
            var oldAt = Padded(threads, EpisodeEngine.PoolStorylineCeiling);
            Assert.That(EpisodeEngine.PoolCandidates(oldAt, new StoryContext(oldAt, StoryAnchors.NomsSet)).Select(c => c.template.id), Is.EqualTo(old),
                "A season without the role moments casts at 92 as it always did.");
        }

        /// <summary>
        /// The arcs the rules start stop only at 100, validation's bound: at 99 the pawn conversation starts and the season holds
        /// 100; at 100 the names are still said and nothing starts, where it would have taken the season past what validation
        /// holds it to and refused the command.
        /// </summary>
        [Test]
        public void UnderTheRoleMomentsTheRulesStartNothingAtOneHundred()
        {
            var pawn = PawnWeek(backdoor: false, coin: true);
            var room = Padded(pawn.before, EpisodeEngine.StorylineCeiling - 1);
            var started = Apply(new EpisodeEngine(room), Command(room, EpisodeCommandKind.Advance));
            Assert.That(started.storylines.Count(x => x.templateId == PawnTalk), Is.EqualTo(1));
            Assert.That(started.storylines, Has.Count.EqualTo(EpisodeEngine.StorylineCeiling));

            var full = Padded(pawn.before, EpisodeEngine.StorylineCeiling);
            var named = Apply(new EpisodeEngine(full), Command(full, EpisodeCommandKind.Advance));
            Assert.That(named.nominees, Does.Contain(named.playerId), "The names are said.");
            Assert.That(named.storylines, Has.Count.EqualTo(EpisodeEngine.StorylineCeiling));
            Assert.That(named.storylines.Select(x => x.templateId), Has.None.EqualTo(PawnTalk));
            Assert.That(EpisodeEngine.PoolStorylineCeiling, Is.EqualTo(92));
        }

        // ------------------------------------------------------------------ fixtures

        private sealed class Pawn
        {
            public string hohId, target;
            public EpisodeState walked, before;
            public EpisodeEngine engine;
            public EpisodeState after;
        }

        private static readonly Dictionary<(int mode, bool roles, int week), List<EpisodeState>> Walks = new Dictionary<(int, bool, int), List<EpisodeState>>();

        /// <summary>
        /// Seasons at story rules Roles (or at Threads, the shipped rules, without <paramref name="roles"/>) in commitment mode
        /// <paramref name="mode"/>, played through the public engine to week <paramref name="week"/>'s Head of Household
        /// competition (week two's unless asked), unresolved, with the player in the game. Four seeds.
        /// </summary>
        private static List<EpisodeState> AtTheHoH(int mode = 2, bool roles = true, int week = 2)
        {
            if (Walks.TryGetValue((mode, roles, week), out var held)) return held;
            var found = new List<EpisodeState>();
            for (uint seed = 7601; seed < 7661 && found.Count < 4; seed++)
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(start);
                start.story.rulesVersion = roles ? StoryRules.Roles : StoryRules.Threads;
                start.unifiedCommitmentRulesVersion = mode == 2 ? UnifiedVoteFamilyValidation.Version : mode;
                start.unifiedHearingRulesVersion = mode == 0 ? 0 : UnifiedCommitmentHearings.ProspectiveVersion;
                var walk = new EpisodeEngine(start);
                for (int i = 0; i < 400 && !AtHoH(walk.Snapshot, week); i++)
                {
                    var step = walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot));
                    Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                    if (walk.Snapshot.week > week) break;
                }
                var s = walk.Snapshot;
                if (!AtHoH(s, week) || s.Find(s.playerId).status != ContestantStatus.Active) continue;
                found.Add(s);
            }
            Assert.That(found, Is.Not.Empty, "No seed reached week " + week + "'s Head of Household competition with the player in the game.");
            Walks[(mode, roles, week)] = found;
            return found;
        }

        private static bool AtHoH(EpisodeState s, int week) => s.week == week && s.phase == EpisodePhase.HoH && !s.competitionResolved;

        /// <summary>The houseguests in this Head of Household competition, by id.</summary>
        private static List<string> Contenders(EpisodeState atHoh) =>
            EpisodeEngine.CompetitionPlayers(atHoh).Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).ToList();

        /// <summary>The week's coin for the pawn conversation with this Head of Household: §3.4a's key, on the story's stream.</summary>
        private static bool Coin(EpisodeState s, string hohId) => StoryRandom.Chance(s, "w" + s.week + ":pawn-talk:" + hohId, 0.5);

        /// <summary>The competition handed to <paramref name="winnerId"/>, as the pitch board's fixture crowns the player.</summary>
        private static EpisodeState CrownedBy(EpisodeState atHoh, string winnerId)
        {
            var s = atHoh.Clone();
            s.hohId = winnerId;
            s.competitionResolved = true;
            s.competitionScores = EpisodeEngine.CompetitionPlayers(s)
                .Select(c => new CompetitionScore { contestantId = c.id, score = c.id == winnerId ? 10 : 1 }).ToList();
            Valid(s);
            return s;
        }

        /// <summary>
        /// <paramref name="hohId"/> crowned and the season advanced into the nominations, then shaped: the player Analytical and
        /// Confrontational, so every answer of Just a Pawn is open to them; the Head of Household honest and warm - or, for a
        /// backdoor, a planner with a competition winner at the foot of their reading - and the player next above that, or out
        /// of it altogether. With <paramref name="second"/> the player's fellow nominee sits below them, so the player is named
        /// second. The names are not yet said.
        /// </summary>
        private static EpisodeState Shaped(EpisodeState atHoh, string hohId, bool backdoor, bool playerUp, out string target, bool second = false)
        {
            var crowned = CrownedBy(atHoh, hohId);
            var s = Apply(new EpisodeEngine(crowned), Command(crowned, EpisodeCommandKind.Advance)).Clone();
            Assert.That((s.phase, s.nominees.Count), Is.EqualTo((EpisodePhase.Nomination, 0)));
            s.Find(hohId).traits = backdoor ? new List<string> { "Strategic", "Analytical" } : new List<string> { "Loyal", "Social" };
            s.Find(s.playerId).traits = new List<string> { "Analytical", "Confrontational" };
            var others = s.Active.Where(c => !c.isPlayer && c.id != hohId).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var other in others) Set(s, hohId, other.id, 60);
            Set(s, hohId, s.playerId, playerUp ? -60 : 100);
            target = null;
            if (backdoor)
            {
                target = others[0].id;
                Set(s, hohId, target, -100);
                others[0].stats.competition = 10;
                Set(s, hohId, others[1].id, second ? -90 : -20);
            }
            else
            {
                Set(s, hohId, others[0].id, second ? -100 : -20);
                if (!playerUp) Set(s, hohId, others[1].id, -20);
            }
            Valid(s);
            return s;
        }

        /// <summary><see cref="Shaped"/> with the player up, the names said by the engine; null where the season would not take it.</summary>
        private static EpisodeState Named(EpisodeState atHoh, string hohId, bool backdoor, out string target)
        {
            var s = Shaped(atHoh, hohId, backdoor, true, out target);
            var result = new EpisodeEngine(s).Apply(Command(s, EpisodeCommandKind.Advance));
            return result.accepted && result.state.nominees.Contains(result.state.playerId) ? result.state : null;
        }

        /// <summary>
        /// Week two's nominations (or <paramref name="week"/>'s), a houseguest Head of Household naming the player - second where
        /// <paramref name="second"/> asks it: with or without a backdoor planned, on the coin's yes or no, its card put to the
        /// player where <paramref name="drawn"/> asks it. The engine is after the names.
        /// </summary>
        private static Pawn PawnWeek(bool backdoor, bool coin, int mode = 2, bool roles = true, bool drawn = true, int week = 2, bool second = false)
        {
            foreach (var walked in AtTheHoH(mode, roles, week))
                foreach (string hoh in Contenders(walked))
                {
                    if (roles && Coin(walked, hoh) != coin) continue;
                    var before = Shaped(walked, hoh, backdoor, true, out string target, second);
                    var engine = new EpisodeEngine(before);
                    var result = engine.Apply(Command(before, EpisodeCommandKind.Advance));
                    if (!result.accepted) continue;
                    var after = result.state;
                    bool named = second ? after.nominees.Count == 2 && after.nominees[1] == after.playerId : after.nominees.Contains(after.playerId);
                    if (!named || EpisodeEngine.BackdoorPlanned(after) != target) continue;
                    if (drawn && JustAPawn(after) == null) continue;
                    return new Pawn { hohId = hoh, target = target, walked = walked, before = before, engine = engine, after = after };
                }
            Assert.Fail("No week-" + week + " nominations put the player up " + (second ? "second " : "") + (backdoor ? "with" : "without")
                + " a backdoor on a coin of " + coin + " (mode " + mode + ", roles " + roles + ").");
            return null;
        }

        /// <summary>
        /// The pawn conversation taken back out of the state after the names: the names said, nothing called up yet, and the room
        /// to ask its card used there - the state <see cref="EpisodeEngine.PawnTalkComes"/> was read on.
        /// </summary>
        private static EpisodeState Unasked(Pawn pawn)
        {
            var s = pawn.after.Clone();
            var cycle = s.storylines.Single(x => x.templateId == PawnTalk && x.week == s.week);
            s.storylines.Remove(cycle);
            s.houseEvents.RemoveAll(e => e.cycleId == cycle.id);
            Assert.That(EpisodeEngine.AirtimeFor(s, StorySurfaces.Conversation), Is.True, "The room to ask the card took.");
            return s;
        }

        /// <summary>
        /// The state before the names with no room to ask there: another ask still open (a beat that closes at no anchor), or the
        /// week's two asks spent.
        /// </summary>
        private static EpisodeState Crowded(EpisodeState before, string hohId, bool open)
        {
            var s = before.Clone();
            string npc = s.Active.First(c => !c.isPlayer && c.id != hohId).id;
            if (open) RoleVetoBoardTests.StoryBeat(s, "the-fallout", "fallout", "NOMINEE", npc, false, "airtime-open", "A moment.");
            else
                foreach (string name in new[] { "airtime-one", "airtime-two" })
                {
                    string id = RoleVetoBoardTests.StoryBeat(s, "the-fallout", "fallout", "NOMINEE", npc, false, name, "A moment.");
                    var spent = s.houseEvents.Single(e => e.id == id);
                    spent.resolved = true;
                    spent.outcome = "It passed.";
                }
            Valid(s);
            Assert.That(EpisodeEngine.AirtimeFor(s, StorySurfaces.Conversation), Is.False);
            return s;
        }

        /// <summary>A copy with a favour owed to the player by every houseguest who votes this week.</summary>
        private static EpisodeState Owed(EpisodeState state)
        {
            var s = state.Clone();
            foreach (var voter in EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToList())
                Assert.That(Hooks.Create(s, s.playerId, voter.id, null), Is.Not.Null);
            Valid(s);
            return s;
        }

        /// <summary>The Just a Pawn card waiting on the player, either variant, or null.</summary>
        private static HouseEventState JustAPawn(EpisodeState s) =>
            s.houseEvents.FirstOrDefault(e => !e.resolved && e.contentId != null && e.contentId.EndsWith(":just-a-pawn", StringComparison.Ordinal));

        /// <summary>An earlier week's conversation with this Head of Household: a finished storyline whose plan took the route given.</summary>
        private static StorylineState Earlier(string templateId, string hohId, string route) => new StorylineState
        {
            id = "story-earlier-" + templateId + "-" + hohId + "-" + route, templateId = templateId, category = "The Backdoor", title = "The Backdoor",
            status = StorylineStatus.Completed, week = 1, endedWeek = 1, beatId = route == "pawn" ? "just-a-pawn" : "plan", lane = StoryLanes.Game,
            endingId = "done", cast = new List<StoryRoleState> { new StoryRoleState { role = "HOH", contestantId = hohId } },
            path = new List<StoryStepState> { new StoryStepState { beatId = "plan", optionId = route, result = StoryResults.Npc, week = 1, logSequence = 0 } },
        };

        /// <summary>Everything the player can see of a card, in words, and the storyline's name: not its ids.</summary>
        private static string Card(EpisodeState s, HouseEventState item)
        {
            var cycle = s.storylines.Single(x => x.id == item.cycleId);
            var lines = new List<string>
            {
                StoryText.Eyebrow(item), StoryText.Title(item), StoryText.ArcOf(item)?.title, StoryText.Narrative(s, item), item.title, item.narrative,
                item.venue, item.surface, item.closesAnchor, item.lapseOptionId, item.week.ToString(CultureInfo.InvariantCulture),
                cycle.title, cycle.category, cycle.lane,
                string.Join(",", item.involvedIds), string.Join(",", item.cast.Select(r => r.role + "=" + r.contestantId)),
            };
            lines.AddRange(item.choices.Select(c => string.Join("|", c.optionId, c.label, StoryText.Description(s, item, c), c.risk, c.approach, c.locked,
                c.lockReason, c.costsAction, c.checkBase.ToString(CultureInfo.InvariantCulture), c.pickPerson, c.glyph, c.lapse,
                string.Join(";", c.effects.Concat(c.backfire).Select(e => e.kind + ":" + e.fromId + ":" + e.toId + ":" + e.type + ":"
                    + e.amount.ToString(CultureInfo.InvariantCulture))))));
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Spends what is left of the open window on plain conversations, through the engine, and returns the state with nothing
        /// left to spend.
        /// </summary>
        private static EpisodeState SpendTheWindow(EpisodeEngine engine)
        {
            var s = engine.Snapshot;
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            for (int i = 0; EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s); i++)
            {
                Assert.That(i, Is.LessThan(12), "The window would not spend out.");
                s = Apply(engine, Command(s, EpisodeCommandKind.Talk, npcs[i % npcs.Count]));
            }
            return s;
        }

        /// <summary>The open quiet tears card.</summary>
        private static HouseEventState OpenTears(EpisodeState s) =>
            s.houseEvents.Single(e => !e.resolved && e.contentId == Tears + ":tears");

        /// <summary>
        /// Week two's quiet tears card at <paramref name="anchor"/>, each that the seeds and Heads of Household reach: a houseguest
        /// Head of Household who left the player off the block, or the player as Head of Household, the names said, then the
        /// week played to the anchor; the card the pool drew there, or one <see cref="EpisodeEngine.StartStory"/> starts. Each
        /// engine is on a state with the card open.
        /// </summary>
        private static IEnumerable<EpisodeEngine> TearsCards(bool playerHoh, string anchor)
        {
            foreach (var walked in AtTheHoH())
            {
                var hohs = playerHoh ? (EpisodeEngine.CompetitionPlayers(walked).Any(c => c.isPlayer) ? new List<string> { walked.playerId } : new List<string>())
                    : Contenders(walked);
                foreach (string hoh in hohs)
                {
                    var engine = AtTheNominations(walked, hoh);
                    if (engine == null || !Reach(engine, anchor)) continue;
                    var s = engine.Snapshot;
                    if (s.houseEvents.Any(e => !e.resolved && e.contentId == Tears + ":tears" && e.week == s.week)) { yield return engine; continue; }
                    var started = s.Clone();
                    if (!EpisodeEngine.StartStory(started, Tears, anchor)) continue;
                    yield return new EpisodeEngine(started);
                }
            }
        }

        private static EpisodeEngine TearsCard(bool playerHoh, string anchor, out EpisodeState s)
        {
            var engine = TearsCards(playerHoh, anchor).FirstOrDefault();
            s = engine?.Snapshot;
            return engine;
        }

        /// <summary>
        /// A state at the nominations where quiet tears would cast but has not come: a houseguest Head of Household's two nominees,
        /// the player safe, before anything starts it.
        /// </summary>
        private static EpisodeState TearsCastable(string anchor)
        {
            foreach (var walked in AtTheHoH())
                foreach (string hoh in Contenders(walked))
                {
                    var engine = AtTheNominations(walked, hoh);
                    if (engine == null || !Reach(engine, anchor)) continue;
                    var s = engine.Snapshot;
                    if (s.storylines.Any(x => x.templateId == Tears)) continue;
                    if (EpisodeEngine.Castable(s, new StoryContext(s, anchor), StoryCatalog.Find(Tears)) != null) return s;
                }
            Assert.Fail("No week-two state where quiet tears would cast at " + anchor + ".");
            return null;
        }

        /// <summary>
        /// The names said in week two: by <paramref name="hohId"/> as a houseguest Head of Household who leaves the player off the
        /// block, or by the player as Head of Household naming the first two houseguests. Null where the season would not take it.
        /// </summary>
        private static EpisodeEngine AtTheNominations(EpisodeState atHoh, string hohId)
        {
            if (hohId == atHoh.playerId)
            {
                var crowned = CrownedBy(atHoh, hohId);
                var engine = new EpisodeEngine(crowned);
                var open = Apply(engine, Command(crowned, EpisodeCommandKind.Advance));
                var pool = EpisodeEngine.NominationCandidates(open).Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).ToList();
                var named = engine.Apply(Command(open, EpisodeCommandKind.Nominate, pool[0], pool[1]));
                return named.accepted ? engine : null;
            }
            var s = Shaped(atHoh, hohId, false, false, out _);
            var npc = new EpisodeEngine(s);
            var result = npc.Apply(Command(s, EpisodeCommandKind.Advance));
            return result.accepted && !result.state.nominees.Contains(result.state.playerId) ? npc : null;
        }

        /// <summary>Plays the week on by the test policy to where <paramref name="anchor"/> has just been crossed; false where it went elsewhere.</summary>
        private static bool Reach(EpisodeEngine engine, string anchor)
        {
            var phase = anchor == StoryAnchors.VetoWon ? EpisodePhase.VetoMeeting : anchor == StoryAnchors.BlockSet ? EpisodePhase.Campaign : EpisodePhase.Nomination;
            int week = engine.Snapshot.week;
            for (int i = 0; i < 60 && engine.Snapshot.phase != phase; i++)
                if (!engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted) return false;
            var s = engine.Snapshot;
            return s.phase == phase && s.week == week && s.Find(s.playerId).status == ContestantStatus.Active;
        }

        /// <summary>A copy with finished storylines added until it holds <paramref name="count"/>.</summary>
        private static EpisodeState Padded(EpisodeState s, int count)
        {
            var padded = s.Clone();
            for (int i = 0; padded.storylines.Count < count; i++)
                padded.storylines.Add(new StorylineState
                {
                    id = "story-padding-" + i, templateId = "the-lobby", title = "The Lobby", category = "This Week", status = StorylineStatus.Completed,
                    week = 1, endedWeek = 1, beatId = "lobby", lane = StoryLanes.Moment, endingId = "done",
                });
            Valid(padded);
            return padded;
        }

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);

        private static EpisodeState Apply(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + " " + command.targetId + " " + command.secondTargetId + ": " + result.reason);
            return result.state;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "role-story-" + kind + "-" + s.revision + "-" + target + "-" + second, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static EpisodeCommand Progress(EpisodeState s, string eventId, string optionId) =>
            Command(s, EpisodeCommandKind.ProgressStoryline, eventId, optionId);
    }

    /// <summary>
    /// The old seasons' pools, recorded (ROLE-MOMENTS-PLAN R6): seasons without the role moments walked to their finish by the
    /// test policy, every anchor's castable arcs read at every step with their weights and casts, and every storyline and card
    /// the walk drew, hashed. Built from what HEAD before R6 already had, so the hash HEAD recorded is the hash R6 must keep.
    /// </summary>
    internal static class RoleStoryPools
    {
        internal static readonly string[] Anchors =
        {
            StoryAnchors.HohCrowned, StoryAnchors.NomsSet, StoryAnchors.VetoWon, StoryAnchors.BlockSet,
            StoryAnchors.EvictionEve, StoryAnchors.EvictionNight,
        };

        internal static IEnumerable<(string name, EpisodeState start)> OldSeasons()
        {
            foreach (uint seed in new uint[] { 2101, 2102 })
            {
                var shipped = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(shipped);
                yield return ("shipped-" + seed, shipped);
            }
            var mode0 = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 2103u);
            ShippedRules.ApplyFresh(mode0);
            mode0.unifiedCommitmentRulesVersion = 0;
            mode0.unifiedHearingRulesVersion = 0;
            yield return ("shipped-mode0-2103", mode0);
            yield return ("story-10-3", StorySeasonTests.StorySeason(3u, 10));
        }

        internal static string Fingerprint(EpisodeState start)
        {
            var text = new StringBuilder();
            var engine = new EpisodeEngine(start);
            for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                text.Append('|').Append(s.week).Append(':').Append(s.phase).Append(':').Append(s.revision);
                if (EpisodeEngine.StoryOn(s))
                    foreach (string anchor in Anchors)
                    {
                        text.Append('@').Append(anchor).Append('=');
                        var ctx = new StoryContext(s, anchor);
                        var pool = new List<(ArcTemplate template, ArcBinding binding, double weight)>();
                        foreach (var template in StoryCatalog.All)
                        {
                            if (Array.IndexOf(template.startAnchors, anchor) < 0) continue;
                            var binding = EpisodeEngine.Castable(s, ctx, template);
                            if (binding == null) continue;
                            double weight = template.weight == null ? 10 : template.weight(ctx, binding);
                            text.Append(template.id).Append(':').Append(weight.ToString("0.###", CultureInfo.InvariantCulture)).Append(':')
                                .Append(string.Join(",", binding.roles.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Key + "=" + r.Value)))
                                .Append(';');
                            if (template.play == null && weight > 0) pool.Add((template, binding, weight));
                        }
                        // And "nothing" weighs what it always did beside them (R6's review: the hash sees the draws, not the
                        // weight they were drawn against): the reach rules' 40, 25 at eviction night; 60 and 40 before them.
                        bool reach = EpisodeEngine.StoryAt(s, StoryRules.Reach);
                        Assert.That(EpisodeEngine.PoolNothing(s, anchor, pool),
                            Is.EqualTo(anchor == StoryAnchors.EvictionNight ? (reach ? 25 : 40) : (reach ? 40 : 60)), "week " + s.week + " " + s.phase + " " + anchor);
                    }
                var command = Next(s);
                var result = engine.Apply(command);
                Assert.That(result.accepted, Is.True, "week " + s.week + " " + s.phase + ": " + result.reason);
            }
            var end = engine.Snapshot;
            Assert.That(end.phase, Is.EqualTo(EpisodePhase.Finished));
            text.Append("#end:").Append(end.randomState).Append(':').Append(end.nextSequence).Append(':').Append(end.revision);
            foreach (var cycle in end.storylines)
                text.Append("#story:").Append(cycle.id).Append(':').Append(cycle.templateId).Append(':').Append(cycle.week).Append(':')
                    .Append(cycle.endedWeek).Append(':').Append(cycle.endingId).Append(':')
                    .Append(string.Join(",", cycle.path.Select(p => p.beatId + "/" + p.optionId + "/" + p.result + "/" + p.week)));
            foreach (var item in end.houseEvents)
                text.Append("#event:").Append(item.id).Append(':').Append(item.contentId).Append(':').Append(item.chosenIndex)
                    .Append(':').Append(string.Join(",", item.choices.Select(c => c.optionId)));
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(b => b.ToString("x2")));
        }

        /// <summary>The test policy, the finale's answers chosen from what it offers.</summary>
        internal static EpisodeCommand Next(EpisodeState state)
        {
            var command = EpisodeEngineTests.NextCommand(state);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(state))
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.finalistId == state.playerId)
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).First();
            }
            return command;
        }
    }
}
