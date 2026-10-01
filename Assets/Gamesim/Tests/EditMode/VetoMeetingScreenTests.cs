using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What the veto meeting's screens read from committed state (PACK8-PASS-PLAN B3): which way the
    /// week's veto went, from the ledger's power row or, for a season saved before the ledger kept
    /// one, from who was named this week and is no longer on the block; and the line the Head of
    /// Household reads over the replacement's candidates, in the holder's own reflexive when they
    /// are saving themselves.
    /// </summary>
    public sealed class VetoMeetingScreenTests
    {
        /// <summary>The meeting with a houseguest holding the veto from the block: the first houseguest is Head of Household, the next two nominated, the first of them holding it.</summary>
        private static EpisodeState AtTheMeeting(uint seed, string holderPronouns = null)
        {
            var state = ContentCatalog.Create(seed);
            var npcs = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            state.phase = EpisodePhase.VetoMeeting;
            state.hohId = npcs[0];
            state.nominees = new List<string> { npcs[1], npcs[2] };
            foreach (var id in state.nominees) state.Find(id).nominationWeeks.Add(state.week);
            state.vetoHolderId = npcs[1];
            state.vetoPlayers = state.Active.Select(c => c.id).ToList();
            if (holderPronouns != null) state.Find(npcs[1]).pronouns = holderPronouns;
            return state;
        }

        [Test]
        public void Outcome_ReadsTheWeeksPowerRowAndFallsBackToWhoWasNamed()
        {
            var before = AtTheMeeting(71);
            string holder = before.vetoHolderId, kept = before.nominees[1];
            Assert.That(EpisodeDirector.VetoMeetingOutcome(before).Used, Is.False, "Nothing is used before the meeting.");
            var engine = new EpisodeEngine(before);
            var result = engine.Apply(EpisodeEngineTests.Command(before, EpisodeCommandKind.Advance));
            Assert.That(result.accepted, Is.True, result.reason);
            var after = engine.Snapshot;
            string replacement = after.nominees.Single(id => id != kept);

            var outcome = EpisodeDirector.VetoMeetingOutcome(after);
            Assert.That(outcome.Used, Is.True, "The holder on the block saved themselves.");
            Assert.That(outcome.HolderId, Is.EqualTo(holder));
            Assert.That(outcome.SavedId, Is.EqualTo(holder));
            Assert.That(outcome.ReplacementId, Is.EqualTo(replacement));

            // A season saved before the ledger kept the week's power says the same from who was named.
            after.ledger.power.Clear();
            var legacy = EpisodeDirector.VetoMeetingOutcome(after);
            Assert.That((legacy.Used, legacy.SavedId, legacy.ReplacementId), Is.EqualTo((true, holder, replacement)),
                "Named this week and off the block is the saved one; the engine puts the replacement at the block's end.");
        }

        [Test]
        public void Outcome_AKeptBlockIsAVetoNotUsed()
        {
            var state = AtTheMeeting(72);
            // The player holds it this time, and keeps the block.
            state.vetoHolderId = state.playerId;
            var engine = new EpisodeEngine(state);
            var keep = EpisodeEngineTests.Command(state, EpisodeCommandKind.ResolveVeto);
            keep.useVeto = false;
            var result = engine.Apply(keep);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = engine.Snapshot;
            var outcome = EpisodeDirector.VetoMeetingOutcome(after);
            Assert.That((outcome.Used, outcome.SavedId, outcome.ReplacementId), Is.EqualTo((false, (string)null, (string)null)));
            after.ledger.power.Clear();
            Assert.That(EpisodeDirector.VetoMeetingOutcome(after).Used, Is.False, "Both still on the block: nobody was saved.");
        }

        [Test]
        public void ReplacementLine_SaysWhoseSaveItIsInTheHoldersOwnWords()
        {
            foreach (var (pronouns, reflexive) in new[] { ("she/her", "herself"), ("he/him", "himself"), ("they/them", "themselves") })
            {
                var state = AtTheMeeting(73, pronouns);
                string holder = state.Find(state.vetoHolderId).name;
                Assert.That(EpisodeDirector.VetoReplacementLine(state, state.vetoHolderId),
                    Is.EqualTo(holder + " is using the veto on " + reflexive + ". Name the replacement nominee."), pronouns);
                string other = state.nominees[1];
                Assert.That(EpisodeDirector.VetoReplacementLine(state, other),
                    Is.EqualTo(holder + " is using the veto on " + state.Find(other).name + ". Name the replacement nominee."),
                    "Somebody else's save names them.");
            }
            var none = AtTheMeeting(74);
            Assert.That(EpisodeDirector.VetoReplacementLine(none, "nobody"), Is.Null, "No saved houseguest, no line.");
        }
    }
}
