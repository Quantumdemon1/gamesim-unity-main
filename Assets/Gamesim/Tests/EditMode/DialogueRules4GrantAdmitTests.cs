#if !UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The rows the house writes at its veto decision after an open conversation lapsed (DIALOGUE-PLAN D5a): only the
    /// week's occasion decision and this week's nominee grant join the command's expected delta, and nothing does below
    /// dialogue rules 4. The rule is internal, so only the Unity-free subset reaches it (no InternalsVisibleTo).
    /// </summary>
    public sealed class DialogueRules4GrantAdmitTests
    {
        private const int Week = 2;

        private static DialogueVetoOccasionState Occasion(int week) => new DialogueVetoOccasionState
            { week = week, holderId = "holder", hohId = "hoh", participants = new List<string> { "holder" }, nomineesAtResult = new List<string> { "a", "b" } };

        private static DialogueRoleGrantState Grant(int week, string status) => new DialogueRoleGrantState
        {
            id = DialogueRules.GrantId(week, DialogueRules.NomineeRole), ownerId = "player", week = week, role = DialogueRules.NomineeRole,
            sourceKind = DialogueRules.FromNominations, status = status, sourceNominees = new List<string> { "player", "b" },
        };

        /// <summary>The command's expected delta before the decision, and the candidate the house then wrote.</summary>
        private static (DialogueState expected, EpisodeState state) Decided(int rules)
        {
            var state = DialogueRules4Season.Start(1);
            state.week = Week;
            state.dialogueRulesVersion = rules;
            var expected = new DialogueState();
            expected.vetoOccasions.Add(Occasion(Week));
            expected.grants.Add(Grant(Week - 1, DialogueRules.GrantOpen));
            state.dialogue = expected.Clone();
            state.dialogue.vetoOccasions[0].decision = new DialogueVetoDecisionState { used = true, savedId = "b", replacementId = "player" };
            state.dialogue.grants.Add(Grant(Week, DialogueRules.GrantOpen));
            // Rows the house never writes at its decision: a stray occasion and an earlier week's grant turned used.
            state.dialogue.vetoOccasions.Add(Occasion(Week + 1));
            state.dialogue.grants[0].status = DialogueRules.GrantUsed;
            Assert.That(DialogueRules.VetoQuestionOn(state), Is.EqualTo(rules >= DialogueRules.Rules4), "The season's rules level.");
            return (expected, state);
        }

        [Test]
        public void OnlyTheDecisionAndThisWeeksGrantAreAdmitted()
        {
            var (expected, state) = Decided(DialogueRules.Rules4);
            EpisodeEngine.AdmitTheHousesRows(expected, state);
            Assert.That(expected.vetoOccasions.Count, Is.EqualTo(1), "No stray occasion joins.");
            var decision = expected.vetoOccasions[0].decision;
            Assert.That(decision, Is.Not.Null, "The week's decision joins.");
            Assert.That(decision.used && decision.savedId == "b" && decision.replacementId == "player", Is.True);
            Assert.That(decision, Is.Not.SameAs(state.dialogue.vetoOccasions[0].decision), "A copy, not the candidate's row.");
            Assert.That(expected.grants.Select(g => g.id).ToList(), Is.EqualTo(new List<string>
                { DialogueRules.GrantId(Week - 1, DialogueRules.NomineeRole), DialogueRules.GrantId(Week, DialogueRules.NomineeRole) }));
            Assert.That(expected.grants[0].status, Is.EqualTo(DialogueRules.GrantOpen), "An earlier week's grant stays as expected.");
            Assert.That(expected.grants[1].status, Is.EqualTo(DialogueRules.GrantOpen));
            Assert.That(expected.grants[1], Is.Not.SameAs(state.dialogue.grants[1]));
        }

        [Test]
        public void BelowRulesFourNothingIsAdmitted()
        {
            var (expected, state) = Decided(DialogueRules.Rules3);
            EpisodeEngine.AdmitTheHousesRows(expected, state);
            Assert.That(expected.vetoOccasions.Count, Is.EqualTo(1));
            Assert.That(expected.vetoOccasions[0].decision, Is.Null);
            Assert.That(expected.grants.Count, Is.EqualTo(1));
            Assert.That(expected.grants[0].status, Is.EqualTo(DialogueRules.GrantOpen));
        }
    }
}
#endif
