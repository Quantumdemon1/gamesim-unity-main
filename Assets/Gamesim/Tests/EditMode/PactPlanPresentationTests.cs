using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The war room on screen, its pure half (WAVE-D-NPC-PACTS-PLAN D3-S4): which conversation draws an
    /// open plan's card, the answers it offers and their new captions, the meeting row's pill, and no call
    /// rows for a pact of three or more. The panel itself, its fit and its captures, is the PlayMode half
    /// (EpisodePlayModeTests.WarRoom.cs); the engine's is PactPlanTests.
    /// </summary>
    public sealed class PactPlanPresentationTests
    {
        private static List<string> NpcIds(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        [Test]
        public void TheAnswersAreOfferedAsThePlanStands()
        {
            var open = PactPlanTests.Opened(out var pact);
            var npcs = NpcIds(open);
            string Name(string id) => open.Find(id).name;
            Assert.That(EpisodeDirector.PlanAnswers(open, pact), Is.EqualTo(new[]
            {
                (EpisodeDirector.PlanAgreeCaption(Name(npcs[1])), npcs[1]),
                (EpisodeDirector.PlanCounterCaption(Name(npcs[2])), npcs[2]),
                (EpisodeDirector.PlanLieLowCaption, (string)null),
            }), "The plan, the other nominee once, or lying low.");

            var split = PactPlanTests.Opened(out var splitPact, split: true);
            Assert.That(EpisodeDirector.PlanAnswers(split, splitPact), Is.EqualTo(new[]
            {
                (EpisodeDirector.PlanSettleCaption(Name(npcs[1])), npcs[1]),
                (EpisodeDirector.PlanSettleCaption(Name(npcs[2])), npcs[2]),
                (EpisodeDirector.PlanLieLowCaption, (string)null),
            }), "A split is settled on either nominee.");

            // The player on the block as the plan: only pushing for the other and lying low.
            var onTheBlock = open.Clone();
            onTheBlock.nominees = new List<string> { onTheBlock.playerId, npcs[2] };
            foreach (var say in onTheBlock.ledger.plans[0].says) say.targetId = onTheBlock.playerId;
            Assert.That(EpisodeDirector.PlanAnswers(onTheBlock, onTheBlock.alliances.Single(a => a.id == PactPlanTests.PactId)), Is.EqualTo(new[]
            {
                (EpisodeDirector.PlanCounterCaption(Name(npcs[2])), npcs[2]),
                (EpisodeDirector.PlanLieLowCaption, (string)null),
            }), "Never going with a plan to evict the player.");

            // Nobody left with a say: lying low alone.
            var silent = open.Clone();
            var gone = silent.alliances.Single(a => a.id == PactPlanTests.PactId);
            silent.ledger.plans[0].says.Clear();
            Assert.That(EpisodeDirector.PlanAnswers(silent, gone), Is.EqualTo(new[] { (EpisodeDirector.PlanLieLowCaption, (string)null) }));
        }

        [Test]
        public void TheCardIsDrawnForAMemberWhoWasThereWhileThePlanIsOpen()
        {
            var open = PactPlanTests.Opened(out var pact);
            var npcs = NpcIds(open);
            Assert.That(EpisodeDirector.PlanCardPact(open, npcs[3])?.id, Is.EqualTo(PactPlanTests.PactId), "Through whoever it was held through,");
            Assert.That(EpisodeDirector.PlanCardPact(open, npcs[4])?.id, Is.EqualTo(PactPlanTests.PactId), "or anybody else who was there.");
            Assert.That(EpisodeDirector.PlanCardPact(open, npcs[0]), Is.Null, "Not with somebody outside the pact.");
            var off = open.Clone(); off.pactPlanRulesStartWeek = 0;
            Assert.That(EpisodeDirector.PlanCardPact(off, npcs[3]), Is.Null, "Never without the rules.");
            var answered = PactPlanTests.Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], null, PactPlanTests.PactId);
            Assert.That(answered.accepted, Is.True, answered.reason);
            Assert.That(EpisodeDirector.PlanCardPact(answered.state, npcs[3]), Is.Null, "Answered, the card is gone.");
        }

        [Test]
        public void TheCaptionsAreNewAndNoneBeginsAnother()
        {
            var open = PactPlanTests.Opened(out var pact);
            var names = open.contestants.Select(c => c.name).ToList();
            var ours = names.SelectMany(n => new[] { EpisodeDirector.PlanAgreeCaption(n), EpisodeDirector.PlanSettleCaption(n), EpisodeDirector.PlanCounterCaption(n) })
                .Concat(new[] { EpisodeDirector.PlanLieLowCaption }).ToList();
            Assert.That(ours.Distinct().Count(), Is.EqualTo(ours.Count), "Unique among themselves.");
            var theirs = names.SelectMany(n => new[]
                {
                    EpisodeHud.CallTheVoteCaption(PactPlanTests.PactName, n), EpisodeHud.CounterAcceptCaption(n), EpisodeHud.CounterDeclineCaption(n),
                    EpisodeHud.MendFencesCaption(n), EpisodeDirector.PromiseToEvictCaption(n), EpisodeHud.VoteDealCaption(DealKind.VoteEvict, n),
                })
                .Concat(new[] { EpisodeDirector.AllianceMeetingCaption, EpisodeDirector.AllianceMeetCaption, EpisodeHud.DealAcceptCaption, EpisodeHud.DealDeclineCaption })
                .ToList();
            foreach (string caption in ours)
                Assert.That(theirs.Any(other => other.StartsWith(caption, System.StringComparison.Ordinal) || caption.StartsWith(other, System.StringComparison.Ordinal)),
                    Is.False, "'" + caption + "' is nobody else's words.");
        }

        [Test]
        public void TheMeetingRowSaysAPlanWhereItWouldBeOne()
        {
            var s = PactPlanTests.Decided(out var pact);
            Assert.That(EpisodeDirector.AllianceMeetingTag(s, pact), Is.EqualTo(EpisodeDirector.WarmthTag + " · " + EpisodeDirector.PlanTag));
            var off = s.Clone(); off.pactPlanRulesStartWeek = 0;
            Assert.That(EpisodeDirector.AllianceMeetingTag(off, off.alliances.Single(a => a.id == PactPlanTests.PactId)),
                Is.EqualTo(EpisodeDirector.WarmthTag + " · " + EpisodeDirector.LearnTag), "Without the rules, C6's pill.");
            var free = PactPlanTests.FreeTime(true, out var trio);
            Assert.That(EpisodeDirector.AllianceMeetingTag(free, trio), Is.EqualTo(EpisodeDirector.WarmthTag), "No plan in free time.");
        }

        [Test]
        public void APactOfThreeOffersNoCallRows()
        {
            var calls = typeof(EpisodeDirector).GetMethod("Calls", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(calls, Is.Not.Null, "EpisodeDirector.Calls draws the call rows.");
            var s = PactPlanTests.Decided(out var pact);
            var npc = s.Find(NpcIds(s)[3]);
            int Rows(EpisodeState state) => ((System.Collections.ICollection)calls.Invoke(null, new object[] { state, npc })).Count;
            Assert.That(Rows(s), Is.Zero, "Under the war rooms a pact of three settles its call when it meets.");
            var off = s.Clone(); off.pactPlanRulesStartWeek = 0;
            Assert.That(Rows(off), Is.EqualTo(2), "Without them, a row for each nominee, as ever.");
            var pair = s.Clone();
            pair.alliances.Single(a => a.id == PactPlanTests.PactId).members.Remove(NpcIds(s)[4]);
            Assert.That(Rows(pair), Is.EqualTo(2), "A pair calls as ever.");
        }
    }
}
