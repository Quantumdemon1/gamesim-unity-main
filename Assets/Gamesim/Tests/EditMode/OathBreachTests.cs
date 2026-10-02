using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What happens to a loyalty declaration that is broken (X12), played through the engine: a
    /// nomination or an evicting vote between the player and the houseguest they declared loyalty to
    /// breaks it, whichever of the two does it; the house is told in a line of the log anybody can
    /// read; the declaration is gone; and most of the house thinks less of whoever broke it.
    ///
    /// <para>That is what the conversation and the notebook now say (EpisodeDirector.OathOfferLine,
    /// OathRecordedLine, OathNotebookNote; ConversationGroupsTests pins the words). The old line said
    /// a declaration "does not bind" the houseguest, while a houseguest who nominated the player was
    /// named an oath-breaker in front of everybody.</para>
    /// </summary>
    public sealed class OathBreachTests
    {
        private const string BrokenKind = "loyalty_oath_broken";

        /// <summary>
        /// A six-house the player has declared loyalty to <paramref name="partnerOf"/>'s choice in,
        /// shaped by <paramref name="shape"/>. Everybody but the partner is neutral on the player and
        /// on the partner, so no witness has a side to take and each reacts as a neutral one does.
        /// </summary>
        private static (EpisodeState state, string partner) Declared(System.Func<List<string>, string> partnerOf,
            System.Action<EpisodeState, List<string>, string> shape)
        {
            var state = ContentCatalog.Create(7);
            var npcs = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            string partner = partnerOf(npcs);
            foreach (var row in state.relationships.Where(r => r.fromId != partner && r.fromId != state.playerId
                         && (r.toId == state.playerId || r.toId == partner)))
                row.score = 0;
            state.shownOathMilestones.Add(partner);
            state.loyaltyOaths.Add(new WebOathRecord { playerId = state.playerId, targetId = partner, week = state.week, timestamp = state.nextSequence++ });
            shape(state, npcs, partner);
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            return (state, partner);
        }

        /// <summary>The first houseguest at the head of the house, the next holding the veto, already settled, and the block as given.</summary>
        private static void AtCampaign(EpisodeState state, string hoh, string vetoHolder, params string[] block)
        {
            state.phase = EpisodePhase.Campaign;
            state.hohId = hoh;
            state.nominees = block.ToList();
            state.vetoHolderId = vetoHolder;
            state.vetoPlayers = new List<string> { hoh, vetoHolder }.Concat(block).Distinct()
                .Concat(state.Active.Select(c => c.id)).Distinct().Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).ToList();
            state.vetoResolved = true;
        }

        private static void Score(EpisodeState state, string from, string to, double score)
        {
            var row = state.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) state.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static void Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string text = null)
        {
            var state = engine.Snapshot;
            var result = engine.Apply(new EpisodeCommand
            {
                id = "oath-breach-" + state.revision, actorId = state.playerId, expectedRevision = state.revision,
                expectedPhase = state.phase, kind = kind, targetId = target, text = text,
            });
            Assert.That(result.accepted, Is.True, kind + ": " + result.reason);
        }

        /// <summary>
        /// The breach as the copy describes it: the declaration gone, one line about it that nobody
        /// is kept from, naming the breaker and how, and most witnesses thinking less of the breaker.
        /// </summary>
        private static void AssertBrokenInFrontOfTheHouse(EpisodeState before, EpisodeState after, string breaker, string wronged, string how)
        {
            Assert.That(after.loyaltyOaths.Any(o => o.playerId == after.playerId), Is.False, "The declaration is broken, and gone.");
            var line = after.events.Skip(before.events.Count).Where(e => e.kind == BrokenKind).ToList();
            Assert.That(line, Has.Count.EqualTo(1), "The breach is logged once.");
            Assert.That(line[0].audienceIds, Is.Empty, "and the whole house hears it: the line has no audience to keep anybody out.");
            Assert.That(line[0].text, Is.EqualTo(after.Find(breaker).name + " broke their loyalty oath to " + after.Find(wronged).name + " " + how + "!"));
            var witnesses = before.Active.Where(c => c.id != breaker && c.id != wronged).Select(c => c.id).ToList();
            int colder = witnesses.Count(w => after.Score(w, breaker) < before.Score(w, breaker));
            Assert.That(colder * 2, Is.GreaterThan(witnesses.Count),
                "Most of the house thinks less of whoever broke it: " + colder + " of " + witnesses.Count + ".");
        }

        [Test]
        public void AHouseguestWhoVotesThePlayerOutBreaksThePlayersDeclarationInFrontOfTheHouse()
        {
            var (state, partner) = Declared(npcs => npcs[3], (s, npcs, p) =>
            {
                AtCampaign(s, npcs[0], npcs[2], s.playerId, npcs[1]);
                // The partner votes on eviction night, and has every reason to send the player home.
                Score(s, p, s.playerId, -100);
                Score(s, p, npcs[1], 100);
            });
            var engine = new EpisodeEngine(state);
            Apply(engine, EpisodeCommandKind.Advance);
            Apply(engine, EpisodeCommandKind.SubmitEvictionSpeech, text: "");
            Apply(engine, EpisodeCommandKind.Advance);
            var before = engine.Snapshot;
            Assert.That(before.evictionStage, Is.EqualTo(EvictionStage.Voting), "The house is about to vote.");
            Assert.That(WebEvictionVoting.EvaluateNative(before, partner).selectedNomineeId, Is.EqualTo(before.playerId),
                "The fixture's partner votes the player out.");
            Apply(engine, EpisodeCommandKind.Advance);
            var after = engine.Snapshot;
            Assert.That(after.evictionResolved, Is.True, "The votes are revealed.");
            AssertBrokenInFrontOfTheHouse(before, after, partner, after.playerId, "by voting to evict them");
        }

        [Test]
        public void AHeadOfHouseholdWhoNominatesThePlayerBreaksThePlayersDeclarationInFrontOfTheHouse()
        {
            var (state, partner) = Declared(npcs => npcs[0], (s, npcs, p) =>
            {
                s.phase = EpisodePhase.Nomination;
                s.hohId = p;
                s.nominees = new List<string>();
                // The partner holds the house and ranks the player first to go up.
                foreach (var other in npcs.Where(id => id != p)) Score(s, p, other, 50);
                Score(s, p, s.playerId, -100);
            });
            var engine = new EpisodeEngine(state);
            var before = engine.Snapshot;
            Apply(engine, EpisodeCommandKind.Advance);
            var after = engine.Snapshot;
            Assert.That(after.nominees, Does.Contain(after.playerId), "The partner nominates the player.");
            AssertBrokenInFrontOfTheHouse(before, after, partner, after.playerId, "by nominating them");
        }

        [Test]
        public void ThePlayerWhoVotesTheirPartnerOutBreaksItFromTheOtherSide()
        {
            var (state, partner) = Declared(npcs => npcs[1], (s, npcs, p) => AtCampaign(s, npcs[0], npcs[3], p, npcs[2]));
            var engine = new EpisodeEngine(state);
            Apply(engine, EpisodeCommandKind.Advance);
            Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(engine.Snapshot.evictionStage, Is.EqualTo(EvictionStage.Voting), "The nominees have spoken and the house votes.");
            Apply(engine, EpisodeCommandKind.CastVote, partner);
            var before = engine.Snapshot;
            Apply(engine, EpisodeCommandKind.Advance);
            var after = engine.Snapshot;
            Assert.That(after.evictionResolved, Is.True, "The votes are revealed.");
            AssertBrokenInFrontOfTheHouse(before, after, after.playerId, partner, "by voting to evict them");
        }
    }
}
