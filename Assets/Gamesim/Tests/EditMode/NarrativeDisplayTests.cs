using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class NarrativeDisplayTests
    {
        private static EpisodeState Cast(string pronouns)
        {
            var state = ContentCatalog.Create(91);
            var npcs = state.contestants.Where(c => !c.isPlayer).ToArray();
            npcs[0].name = "Alex Chen"; npcs[0].pronouns = pronouns;
            npcs[1].name = "Quinn Martinez";
            return state;
        }

        private static ContestantState Teller(EpisodeState state) => state.contestants.First(c => !c.isPlayer);
        private static ContestantState Subject(EpisodeState state) => state.contestants.Where(c => !c.isPlayer).Skip(1).First();

        /// <summary>
        /// The repaired intel lines use the pronouns every other line of the season uses (<see cref="StoryPeople.Pronouns"/>: the
        /// engine's own sentences and a story card's tokens) - the consolidation check's PE-9. A set the creator does not offer,
        /// or none, reads as that rule reads it ("they" unless it starts "she" or "he"), never as the name where the rest of the
        /// season says "they".
        /// </summary>
        [TestCase("he/him", "he", "He", "thinks", "is", "does")]
        [TestCase("she/her", "she", "She", "thinks", "is", "does")]
        [TestCase("they/them", "they", "They", "think", "are", "do")]
        [TestCase(null, "they", "They", "think", "are", "do")]
        [TestCase("", "they", "They", "think", "are", "do")]
        [TestCase("ze/zir", "they", "They", "think", "are", "do")]
        [TestCase("he/they", "he", "He", "thinks", "is", "does")]
        [TestCase("he", "he", "He", "thinks", "is", "does")]
        [TestCase("SHE/HER", "she", "She", "thinks", "is", "does")]
        [TestCase(" SHE/HER ", "they", "They", "think", "are", "do")]
        public void HistoricalIntelReadsUseChosenPronounsAndAgreementWithoutChangingRecords(string pronouns,
            string lower, string upper, string thinks, string be, string does)
        {
            var state = Cast(pronouns);
            Assert.That(lower, Is.EqualTo(StoryPeople.Pronouns(Teller(state)).they), "The pronoun every other line of the season uses.");
            var readings = new[]
            {
                ("is solid with", be + " solid with Quinn Martinez."),
                ("does not trust", does + " not trust Quinn Martinez."),
                ("is still working out", be + " still figuring Quinn Martinez out.")
            };
            var inputs = new List<(string saved, string displayed)>();
            foreach (var (oldReading, answer) in readings)
            {
                inputs.Add(("You asked Alex Chen what they think of Quinn Martinez. They " + oldReading + " Quinn Martinez.",
                    "You asked Alex Chen what " + lower + " " + thinks + " of Quinn Martinez. " + upper + " " + answer));
                inputs.Add(("You asked Alex Chen what they had been hearing. They " + oldReading + " Quinn Martinez.",
                    "You asked Alex Chen what " + lower + " had been hearing. " + upper + " " + answer));
                inputs.Add(("Alex Chen told me in week 7 that they " + oldReading + " Quinn Martinez.",
                    "Alex Chen told me in week 7 that " + lower + " " + answer));
            }
            foreach (var (saved, _) in inputs)
            {
                state.events.Add(new EpisodeEvent { sequence = state.nextSequence++, kind = "information", text = saved,
                    audienceIds = new List<string> { state.playerId, Teller(state).id } });
                state.memories.Add(new MemoryState { ownerId = state.playerId, subjectId = Teller(state).id,
                    week = 7, isPrivate = true, text = saved });
            }
            string snapshot = JsonConvert.SerializeObject(state);
            uint random = state.randomState;
            foreach (var (saved, displayed) in inputs)
            {
                Assert.That(NarrativeDisplay.Render(state, saved), Is.EqualTo(displayed), saved);
                Assert.That(NarrativeDisplay.Render(state, displayed), Is.EqualTo(displayed), "Reading a display line twice is stable.");
            }
            Assert.That(NarrativeDisplay.Render(state, "You asked Alex Chen what they had been hearing."),
                Is.EqualTo("You asked Alex Chen what " + lower + " had been hearing."));
            Assert.That(state.randomState, Is.EqualTo(random));
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(snapshot), "Events, memories, identity and random state stay byte-identical.");
        }

        [Test]
        public void AmbiguousOrUnresolvableNamesLeaveHistoricalTextUntouched()
        {
            var state = Cast("he/him");
            string line = "You asked Alex Chen what they think of Quinn Martinez. They is solid with Quinn Martinez.";
            var duplicate = Teller(state).Clone();
            duplicate.id = "duplicate-teller"; duplicate.pronouns = "she/her";
            state.contestants.Add(duplicate);
            Assert.That(NarrativeDisplay.Render(state, line), Is.EqualTo(line));
            Assert.That(NarrativeDisplay.Render(state, "You asked Alex Chen what they had been hearing."),
                Is.EqualTo("You asked Alex Chen what they had been hearing."));
            state.contestants.Remove(duplicate);
            duplicate = Subject(state).Clone(); duplicate.id = "duplicate-subject";
            state.contestants.Add(duplicate);
            Assert.That(NarrativeDisplay.Render(state, line), Is.EqualTo(line));
            state.contestants.Remove(duplicate);
            Subject(state).name = "Renamed houseguest";
            Assert.That(NarrativeDisplay.Render(state, line), Is.EqualTo(line), "An obsolete name is never guessed.");
        }

        [TestCase("They is still working out Quinn Martinez.")]
        [TestCase("You asked Alex Chen what they think of Quinn Martinez. They is solid with Quinn Martinez. Saved locally.")]
        [TestCase("You asked Alex Chen what they think of Quinn Martinez. They is completely against Quinn Martinez.")]
        [TestCase("Alex Chen told me in week 07 that they is solid with Quinn Martinez.")]
        [TestCase("Alex Chen told me in week -1 that they is solid with Quinn Martinez.")]
        [TestCase("Someone quoted: You asked Alex Chen what they think of Quinn Martinez. They is solid with Quinn Martinez.")]
        public void UnrecognisedTextIsNeverRewritten(string line)
        {
            Assert.That(NarrativeDisplay.Render(Cast("he/him"), line), Is.EqualTo(line));
        }

        [Test]
        public void NullAndEmptyInputAndMissingStateAreSafe()
        {
            Assert.That(NarrativeDisplay.Render(null, null), Is.Null);
            Assert.That(NarrativeDisplay.Render(Cast("he/him"), ""), Is.Empty);
            const string line = "You asked Alex Chen what they had been hearing.";
            Assert.That(NarrativeDisplay.Render(null, line), Is.EqualTo(line));
            Assert.That(NarrativeDisplay.Render(new EpisodeState(), line), Is.EqualTo(line));
        }

        [TestCase("he/him", "he", "him", "likes", "keeps", "knows", "wants", "owes")]
        [TestCase("she/her", "she", "her", "likes", "keeps", "knows", "wants", "owes")]
        [TestCase("they/them", "they", "them", "like", "keep", "know", "want", "owe")]
        [TestCase(null, "they", "them", "like", "keep", "know", "want", "owe")]
        [TestCase("ze/zir", "they", "them", "like", "keep", "know", "want", "owe")]
        [TestCase("he/they", "he", "him", "likes", "keeps", "knows", "wants", "owes")]
        public void KnownCatalogueClausesAgreeOnlyAtDisplayTime(string pronouns, string subject, string obj,
            string likes, string keeps, string knows, string wants, string owes)
        {
            var state = Cast(pronouns);
            var cast = new[] { "CONFIDANT", "HOH", "SUBJECT", "NOMINEE" }
                .Select(role => new StoryRoleState { role = role, contestantId = Teller(state).id }).ToArray();
            // One card, one pronoun rule (PE-9): a corrected sentence names the houseguest as the card's other sentences do.
            Assert.That((subject, obj), Is.EqualTo((StoryText.FillDisplay(state, "{HOH.they}", cast), StoryText.FillDisplay(state, "{HOH.them}", cast))),
                "The pronouns the card's other sentences fill in.");
            var templates = new[]
            {
                ("Let {CONFIDANT} think what {CONFIDANT.they} likes.", "Let Alex Chen think what " + subject + " " + likes + "."),
                ("Promise {HOH} you will keep {HOH.them} safe next week if {HOH.they} keep you safe now.",
                    "Promise Alex Chen you will keep " + obj + " safe next week if " + subject + " " + keeps + " you safe now."),
                ("{HOH} would not commit, and now {HOH.they} know how worried you are.",
                    "Alex Chen would not commit, and now " + subject + " " + knows + " how worried you are."),
                ("Ask {SUBJECT} what {SUBJECT.they} want out of this game.", "Ask Alex Chen what " + subject + " " + wants + " out of this game."),
                ("Help {NOMINEE} through the vote so that {NOMINEE.they} owe you one.", "Help Alex Chen through the vote so that " + subject + " " + owes + " you one.")
            };
            string snapshot = JsonConvert.SerializeObject(state);
            foreach (var (template, expected) in templates)
                Assert.That(StoryText.FillDisplay(state, template, cast), Is.EqualTo(expected), template);
            Assert.That(StoryText.Neutral(templates[0].Item1), Is.EqualTo("Let a houseguest think what they likes."),
                "Historical neutral fallback bytes stay unchanged.");
            Assert.That(StoryText.Neutral(templates[1].Item1), Is.EqualTo("Promise a houseguest you will keep them safe next week if they keep you safe now."));
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(snapshot));
            Assert.That(StoryPeople.Pronouns(Teller(state)).they, Is.EqualTo(subject), "The existing public pronoun fallback is unchanged.");
        }

        /// <summary>
        /// A singular teller, so the correction shows in the words: the exact authored sentence agrees ("he wants"), while the same
        /// words inside other text keep the plain fill's ("he want") - "they want" would read alike either way.
        /// </summary>
        [Test]
        public void CustomNamesAreLiteralAndCatalogueCorrectionRequiresAnExactTemplate()
        {
            var state = Cast("he/him");
            Teller(state).name = "{HOH}";
            var cast = new[]
            {
                new StoryRoleState { role = "SUBJECT", contestantId = Teller(state).id },
                new StoryRoleState { role = "HOH", contestantId = Subject(state).id }
            };
            const string exact = "Ask {SUBJECT} what {SUBJECT.they} want out of this game.";
            Assert.That(StoryText.Fill(state, exact, cast), Is.EqualTo("Ask {HOH} what he want out of this game."),
                "Fixture: the plain fill keeps the authored verb.");
            string corrected = StoryText.FillDisplay(state, exact, cast);
            Assert.That(corrected, Is.EqualTo("Ask {HOH} what he wants out of this game."), "Names are never recursively treated as story tokens.");
            string quoted = StoryText.FillDisplay(state, "A quotation: " + exact, cast);
            Assert.That(quoted, Is.EqualTo("A quotation: Ask {HOH} what he want out of this game."), "Only the exact authored sentence gets the correction.");
            Assert.That(quoted, Is.Not.EqualTo("A quotation: " + corrected), "The quotation is not the corrected sentence.");
        }

        [Test]
        public void CanonicalPlayOutcomeCompositionAndSavedBytesRemainUnchanged()
        {
            var state = Cast("he/him");
            var cast = new[] { new StoryRoleState { role = "HOH", contestantId = Teller(state).id } };
            const string template = "{HOH} would not commit, and now {HOH.they} know how worried you are.";
            const string canonical = "Alex Chen would not commit, and now he know how worried you are.";
            Assert.That(StoryText.Fill(state, template, cast), Is.EqualTo(canonical), "The engine's existing composition API retains its exact output.");
            var entry = new EpisodeEvent { sequence = state.nextSequence++, kind = StoryLog.Play, text = "Stay Off the Block: " + canonical,
                audienceIds = new List<string> { state.playerId } };
            state.events.Add(entry);
            string snapshot = JsonConvert.SerializeObject(state);
            Assert.That(StoryText.FillDisplay(state, template, cast), Is.EqualTo("Alex Chen would not commit, and now he knows how worried you are."));
            Assert.That(entry.text, Is.EqualTo("Stay Off the Block: " + canonical));
            Assert.That(StoryText.Log(state, entry), Is.EqualTo(entry.text), "A historical play receipt is not parsed as a story outcome.");
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(snapshot));
        }
    }
}
