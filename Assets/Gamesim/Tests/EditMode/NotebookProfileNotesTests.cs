using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Backlog item 21 (b): a houseguest's profile shows the alliance-call notes the notes card shows
    /// - a bloc call of yours they followed or ignored, a war room's plan you backed that they were
    /// with or not - each once, and still leaves out what its other blocks say (the promises between
    /// you, your memories). It chooses among the card's own lines, so it never says more than the
    /// card. Also in the dotnet subset.
    /// </summary>
    public sealed class NotebookProfileNotesTests
    {
        private static EpisodeState Season(uint seed = 21)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            s.strategyRulesStartWeek = 1; s.blocRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableAgency(s);
            s.week = 4;
            return s;
        }

        private static HouseguestNotes.Note Note(int week, string kind, string text, string brief = null) =>
            new HouseguestNotes.Note { week = week, kind = kind, text = text, brief = brief };

        [Test]
        public void ACallTheyFollowed_IsOnTheProfile_AndThePromiseBetweenYouIsNot()
        {
            var s = Season();
            var others = s.contestants.Where(c => !c.isPlayer).ToList();
            var a = others[0]; var c = others[2];
            string first = a.name.Split(' ')[0];
            s.promises.Add(new PromiseState { id = "p1", fromId = a.id, toId = s.playerId, kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 2 });
            s.ledger.calls.Add(new BlocCallRow { week = 3, allianceId = "alliance-x", callerId = s.playerId, targetId = c.id, followed = new List<string> { a.id } });
            s.alliances.Add(new AllianceState { id = "alliance-x", name = "The Pact", members = new List<string> { s.playerId, a.id }, active = true });
            s.ledger.alliances.Add(new AllianceRow { id = "alliance-x", startedWeek = 2 });
            s.memories.Add(new MemoryState { ownerId = s.playerId, subjectId = a.id, week = 1, text = first + " talked about me to somebody in week 1.", isPrivate = true });

            var card = HouseguestNotes.For(s, a.id);
            var shown = ProfileNotes.For(s, a.id);
            string call = first + " followed your call to evict " + c.name;
            Assert.That(card.Select(n => n.text), Has.Some.EqualTo(call), "The notes card says the call.");
            Assert.That(shown.Select(n => n.text).Count(t => t == call), Is.EqualTo(1), "The profile says it, once.");
            Assert.That(shown.Select(n => n.text), Has.None.StartsWith(first + " promised you"), "The promise is the profile's records', not said again.");
            Assert.That(shown.Any(n => n.kind == HouseguestNotes.Kinds.Memory), Is.False, "Memories are the profile's own block.");
            // Each reading builds its notes anew, so they are compared as the lines they print.
            var cardLines = card.Select(n => n.ToString()).ToList();
            var shownLines = shown.Select(n => n.ToString()).ToList();
            Assert.That(shownLines, Is.SubsetOf(cardLines), "Nothing the notes card does not say.");
            Assert.That(shownLines, Is.EqualTo(cardLines.Where(shownLines.Contains).ToList()), "In the card's order.");
        }

        [Test]
        public void AnIgnoredCallABetrayalOfTheSameWeekTells_IsSaidOnce()
        {
            var ignored = Note(3, HouseguestNotes.Kinds.Word, "Ana ignored your call to evict Jordan Lee", "Ignored your call");
            var betrayal = Note(3, HouseguestNotes.Kinds.Pact, "Ana Reyes ignored your call to evict Jordan Lee despite The Pact", "Turned on your pact");
            var shown = ProfileNotes.Shown(new List<HouseguestNotes.Note> { ignored, betrayal });
            Assert.That(shown, Is.EqualTo(new[] { betrayal }), "The betrayal already says the call.");

            var later = Note(4, HouseguestNotes.Kinds.Pact, "Ana Reyes ignored your call to evict Jordan Lee despite The Pact", "Turned on your pact");
            Assert.That(ProfileNotes.Shown(new List<HouseguestNotes.Note> { later, ignored }), Is.EqualTo(new[] { later, ignored }),
                "A betrayal of another week is another call.");
            var other = Note(3, HouseguestNotes.Kinds.Pact, "Ana Reyes ignored your call to evict Sam Park despite The Pact", "Turned on your pact");
            Assert.That(ProfileNotes.Shown(new List<HouseguestNotes.Note> { ignored, other }), Is.EqualTo(new[] { ignored, other }),
                "A betrayal over another target is another call.");
        }

        [Test]
        public void APlanTheyWereWith_IsKept_AndPromisesAndMemoriesAreLeftOut()
        {
            var plan = Note(5, HouseguestNotes.Kinds.Word, "Ana wasn't with the plan you backed to evict Jordan Lee", "Not with your plan");
            var theirs = Note(4, HouseguestNotes.Kinds.Word, "Ana promised you safety · kept", ProfileNotes.TheirWordBrief + "kept");
            var yours = Note(4, HouseguestNotes.Kinds.Word, "You promised Ana a final two · active", ProfileNotes.YourWordBrief + "active");
            var followed = Note(3, HouseguestNotes.Kinds.Word, "Ana followed your call to evict Sam Park", "Followed your call");
            var memory = Note(2, HouseguestNotes.Kinds.Memory, "Ana kept her word.");
            var read = Note(2, HouseguestNotes.Kinds.Read, "You read Ana: warm on you", "Read: warm on you");
            var shown = ProfileNotes.Shown(new List<HouseguestNotes.Note> { plan, theirs, yours, followed, memory, read });
            Assert.That(shown, Is.EqualTo(new[] { plan, followed, read }));
            Assert.That(ProfileNotes.IsPromise(theirs) && ProfileNotes.IsPromise(yours), Is.True);
            Assert.That(ProfileNotes.IsPromise(plan) || ProfileNotes.IsPromise(followed), Is.False);
        }
    }
}
