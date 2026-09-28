using System;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Reach (plan 30 P4, <see cref="StoryRules.Reach"/>): the fixes that let arcs a season could
    /// never cast come round. Each holds from the reach rules on and leaves a season stamped earlier
    /// exactly as it was; StorySeasonTests.ReachReport sweeps the whole catalogue with a skilled player.
    /// </summary>
    public sealed class StoryReachTests
    {
        private static EpisodeState Apply(EpisodeState s, EpisodeCommand c)
        {
            var result = new EpisodeEngine(s).Apply(c);
            Assert.That(result.accepted, Is.True, c.kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>A real history played with the story off to the first moment this holds, the story switched on there.</summary>
        private static EpisodeState At(Func<EpisodeState, bool> where, uint seed = 31, int rules = StoryRules.Current)
        {
            var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed));
            for (int i = 0; i < 800 && !where(engine.Snapshot); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(where(s), Is.True, "The fixture reaches its moment.");
            Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active), "The fixture's player is in the house.");
            EpisodeEngine.EnableStory(s, s.week);
            s.story.rulesVersion = rules;
            return s;
        }

        private static bool AfterEviction(EpisodeState s, int week) =>
            s.week >= week && s.phase == EpisodePhase.Social && s.evictionResolved && s.pendingDiary == null;

        private static void Score(EpisodeState s, string from, string to, double value) =>
            s.relationships.First(r => r.fromId == from && r.toId == to).score = value;

        private static void Both(EpisodeState s, string a, string b, double value) { Score(s, a, b, value); Score(s, b, a, value); }

        private static StorylineState Cycle(EpisodeState s, string arcId) => s.storylines.Last(x => x.templateId == arcId);

        private static EpisodeState Answer(EpisodeState s, string arcId, string optionId)
        {
            var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProgressStoryline);
            command.targetId = s.houseEvents.Single(e => e.cycleId == Cycle(s, arcId).id && !e.resolved).id;
            command.secondTargetId = optionId;
            return Apply(s, command);
        }

        /// <summary>Everybody's alliances with the player, and every houseguests' pact, gone: a house with no blocs.</summary>
        private static void NoBlocs(EpisodeState s)
        {
            foreach (var alliance in s.alliances) alliance.active = false;
        }

        [Test]
        public void ABeatPutOffAtEvictionNightTriesAgainAtTheNextHeadOfHousehold([Values(true, false)] bool reach)
        {
            var s = At(x => AfterEviction(x, 2), rules: reach ? StoryRules.Reach : StoryRules.Plays);
            Assert.That(EpisodeEngine.RetryAnchor(s, StoryAnchors.EvictionNight),
                Is.EqualTo(reach ? StoryAnchors.HohCrowned : StoryAnchors.SocialClose),
                reach ? "The close of the social window fires nothing: the beat tries again at the next HoH."
                      : "A season on the rules before reach keeps its old retry, stale as it was.");
            Assert.That(EpisodeEngine.RetryAnchor(s, StoryAnchors.BlockSet), Is.EqualTo(StoryAnchors.EvictionEve), "Every other anchor is unchanged.");
        }

        [Test]
        public void PowerShift_UnderTheReachRulesTwoHouseguestsAreABloc([Values(true, false)] bool reach)
        {
            var s = At(x => AfterEviction(x, 3), rules: reach ? StoryRules.Reach : StoryRules.Plays);
            NoBlocs(s);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            var pact = NpcAlliances.FormFromStory(s, new[] { npcs[0].id, npcs[1].id }.ToList());
            Grudges.Add(s, npcs[0].id, s.playerId, 60, GrudgeCauses.Story);
            // Nobody else has a reason to form a bloc of their own.
            foreach (var npc in npcs.Skip(1)) Grudges.Ease(s, npc.id, s.playerId, 100);
            Assert.That(EpisodeEngine.StartStory(s, "power-shift", StoryAnchors.EvictionNight), Is.EqualTo(reach),
                reach ? "A pair with a leader who holds a grudge is a bloc." : "Before the reach rules a bloc needed three, and no houseguest pact ever had them.");
            if (!reach) return;
            var cycle = Cycle(s, "power-shift");
            Assert.That(new[] { "LEADER", "MEMBER" }.Select(role => cycle.cast.Single(r => r.role == role).contestantId),
                Is.EquivalentTo(pact.members), "The pair is the bloc: one leads it, the other is in it.");
        }

        [Test]
        public void PowerShift_TheDiscoveryMakesTheBlocAndShowsItToYou()
        {
            var s = At(x => AfterEviction(x, 3));
            NoBlocs(s);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var npc in npcs) Grudges.Ease(s, npc.id, s.playerId, 100);
            string leader = npcs[0].id, member = npcs[1].id;
            Grudges.Add(s, leader, s.playerId, 60, GrudgeCauses.Story);
            foreach (var npc in npcs.Skip(1)) Both(s, leader, npc.id, 0);
            Both(s, leader, member, 25);
            Assert.That(EpisodeEngine.StartStory(s, "power-shift", StoryAnchors.EvictionNight), Is.True, "A bloc forming casts.");
            Assert.That(s.alliances.Any(a => a.active && a.members.Contains(leader) && a.members.Contains(member)), Is.False,
                "Nothing is formed before the scene plays.");
            s = Answer(s, "power-shift", "stay-out");
            var bloc = s.alliances.Single(a => a.active && a.members.Contains(leader) && a.members.Contains(member));
            Assert.That(bloc.members.Contains(s.playerId), Is.False, "It is their bloc, not yours.");
            Assert.That(Knowledge.AllianceVisibleTo(s, bloc, s.playerId), Is.True, "You watched it form, so the read counts it.");
        }

        [Test]
        public void ALeakOfAnAllianceEverybodyCanSeeLeavesItPublic()
        {
            var s = At(x => AfterEviction(x, 3));
            NoBlocs(s);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var npc in npcs) Grudges.Ease(s, npc.id, s.playerId, 100);
            // A pact from before the read rules: no fact, so everybody knows of it.
            var pact = new AllianceState { id = "alliance-public", name = "The Open Pact", active = true, members = new[] { npcs[0].id, npcs[1].id }.ToList() };
            s.alliances.Add(pact);
            Grudges.Add(s, npcs[0].id, s.playerId, 60, GrudgeCauses.Story);
            Assert.That(EpisodeEngine.StartStory(s, "power-shift", StoryAnchors.EvictionNight), Is.True);
            s = Answer(s, "power-shift", "stay-out");
            var after = s.alliances.Single(a => a.id == pact.id);
            Assert.That(Knowledge.AllianceVisibleTo(s, after, s.playerId), Is.True);
            Assert.That(Knowledge.AllianceVisibleTo(s, after, npcs[2].id), Is.True,
                "Under the reach rules one more person hearing of a public alliance does not make it a secret from the rest.");
        }

        [Test]
        public void WalkingInOnTwoHouseguestsWhoLikeEachOtherIsWhispering([Values(true, false)] bool reach)
        {
            var s = At(x => AfterEviction(x, 2), rules: reach ? StoryRules.Reach : StoryRules.Plays);
            NoBlocs(s);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            Both(s, npcs[0].id, npcs[1].id, 25);
            Assert.That(StoryCatalog.ProximityCast(s, "walked-in-whispering", npcs[0].id, npcs[1].id) != null, Is.EqualTo(reach),
                reach ? "Twenty-five both ways is close enough to whisper." : "Before the reach rules only sixty whispered.");
            Assert.That(StoryCatalog.ProximityCast(s, "walked-in-arguing", npcs[0].id, npcs[1].id) != null, Is.EqualTo(!reach),
                "A walk-in is one or the other.");
        }

        [Test]
        public void UnderAgencyItTakesThePactThresholdToWhisper([Values(true, false)] bool agency)
        {
            var s = At(x => AfterEviction(x, 2));
            NoBlocs(s);
            if (agency) EpisodeEngine.EnableAgency(s, s.week);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            Both(s, npcs[0].id, npcs[1].id, 22);
            Assert.That(StoryCatalog.ProximityCast(s, "walked-in-whispering", npcs[0].id, npcs[1].id) != null, Is.EqualTo(!agency),
                agency ? "Where warm pairs are common, twenty-two is not close enough to whisper." : "Without agency, twenty whispers.");
        }

        [Test]
        public void TheBlockNamesTheNomineeStillInTheHouse([Values(true, false)] bool reach)
        {
            var s = At(x => AfterEviction(x, 2) && x.nominees.Count == 2 && !x.nominees.Contains(x.playerId) && x.hohId != x.playerId,
                rules: reach ? StoryRules.Reach : StoryRules.Plays);
            string gone = s.nominees.Single(id => s.Find(id).status != ContestantStatus.Active);
            string stayed = s.nominees.Single(id => s.Find(id).status == ContestantStatus.Active);
            // The evictee first, as the block often lists them: the legacy roles took the first name.
            s.nominees = new[] { gone, stayed }.ToList();
            bool started = EpisodeEngine.StartStory(s, "legacy-house-divide", StoryAnchors.EvictionNight);
            Assert.That(started, Is.EqualTo(reach),
                reach ? "The situation is about the nominee still here." : "Before the reach rules it named the evictee, and could not be cast.");
            if (reach) Assert.That(Cycle(s, "legacy-house-divide").cast.Single(r => r.role == "NOMINEE").contestantId, Is.EqualTo(stayed));
        }

        [Test]
        public void RideOrDie_ThirtyFiveBothWaysIsAClosestAlly([Values(true, false)] bool reach)
        {
            var s = At(x => AfterEviction(x, 3), rules: reach ? StoryRules.Reach : StoryRules.Plays);
            NoBlocs(s);
            var npcs = s.Active.Where(c => !c.isPlayer).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            foreach (var npc in npcs) Both(s, s.playerId, npc.id, 0);
            s.promises.RemoveAll(p => p.kind == PromiseKind.FinalTwo);
            s.alliances.Add(new AllianceState { id = "alliance-close", name = "A Pact", active = true, members = new[] { s.playerId, npcs[0].id }.ToList() });
            Both(s, s.playerId, npcs[0].id, 40);
            Assert.That(EpisodeEngine.StartStory(s, "ride-or-die", StoryAnchors.EvictionNight), Is.EqualTo(reach),
                reach ? "An ally at forty both ways is close enough to ask for the end." : "Before the reach rules it took fifty.");
        }

        [Test]
        public void WhatTheyLeftOut_KnowingSomebodyWellIsTrustEnough([Values(true, false)] bool reach)
        {
            var s = At(x => AfterEviction(x, 2), rules: reach ? StoryRules.Reach : StoryRules.Plays);
            var npcs = s.Active.Where(c => !c.isPlayer && !StoryPeople.IsRealPerson(s, c.id) && Lore.Facet(s, c.id, Lore.Facets.Secret) != null)
                .OrderBy(c => c.id, StringComparer.Ordinal).ToList();
            Assert.That(npcs, Is.Not.Empty, "The fixture has somebody with a secret.");
            var subject = npcs[0];
            // Three things learned, one past small talk - what Know Them is for - and nothing deeper.
            var known = Lore.FactsOf(s, subject.id).Where(f => f.depth <= 2 && f.facet != Lore.Facets.Secret)
                .OrderByDescending(f => f.depth).ThenBy(f => f.id, StringComparer.Ordinal).Take(3).ToList();
            Assert.That(known.Count, Is.EqualTo(3));
            Assert.That(known.Any(f => f.depth == 2), Is.True);
            foreach (var fact in known) Lore.Learn(s, fact.id);
            foreach (var npc in npcs.Skip(1)) Lore.Learn(s, Lore.Facet(s, npc.id, Lore.Facets.Secret).id);
            Assert.That(EpisodeEngine.StartStory(s, "what-they-left-out", StoryAnchors.EvictionNight), Is.EqualTo(reach),
                reach ? "Knowing them well is trust enough to be told the rest." : "Before the reach rules it took a deep conversation or a bond.");
        }

        [Test]
        public void TheHouseTurnsMayAlsoComeAtTheFinalBlock([Values(true, false)] bool reach)
        {
            var s = At(x => AfterEviction(x, 2), rules: reach ? StoryRules.Reach : StoryRules.Plays);
            var arc = StoryCatalog.Find("the-house-turns");
            Assert.That(arc.startAnchors, Does.Contain(StoryAnchors.BlockSet));
            var binding = new ArcBinding();
            Assert.That(arc.weight(new StoryContext(s, StoryAnchors.BlockSet), binding) > 0, Is.EqualTo(reach),
                "The final block weighs only under the reach rules; before them the house turns on eviction eve alone.");
            Assert.That(arc.weight(new StoryContext(s, StoryAnchors.EvictionEve), binding), Is.GreaterThan(0));
        }

        [Test]
        public void KnowThem_AConversationNeverTeachesWhatOnlyTrustReveals()
        {
            var s = At(x => AfterEviction(x, 2));
            var subject = s.Active.Where(c => !c.isPlayer && !StoryPeople.IsRealPerson(s, c.id))
                .OrderByDescending(c => Lore.FactsOf(s, c.id).Count()).ThenBy(c => c.id, StringComparer.Ordinal).First();
            foreach (var other in s.Active.Where(c => !c.isPlayer && c.id != subject.id && Lore.FactsOf(s, c.id).Any()))
                Lore.Learn(s, Lore.FactsOf(s, other.id).First().id);
            Score(s, s.playerId, subject.id, 10);
            Assert.That(EpisodeEngine.StartStory(s, "know-them", StoryAnchors.EvictionNight), Is.True, "Know Them casts.");
            s = Answer(s, "know-them", PlayOptions.TakeItOn);
            // Everything a good conversation reaches is known already; only the deep things are left.
            foreach (var fact in Lore.FactsOf(s, subject.id).Where(f => f.depth <= 2 && f.facet != Lore.Facets.Secret)) Lore.Learn(s, fact.id);
            int before = Lore.Learned(s, subject.id).Count;
            s.houseEvents.Single(e => e.cycleId == Cycle(s, "know-them").id && !e.resolved)
                .choices.Single(c => c.optionId == "ask-about-home").checkBase = -1;
            s = Answer(s, "know-them", "ask-about-home");
            Assert.That(Lore.Learned(s, subject.id).Count, Is.EqualTo(before),
                "The unforgivable thing and the secret keep their own ways in: a chat, however good, does not reach them.");
        }
    }
}
