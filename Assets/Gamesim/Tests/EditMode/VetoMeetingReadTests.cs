using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What the staged veto meeting's screen says (PACK8-PASS-PLAN C1): who came off the block and
    /// who went up, read from the committed state and the block before the commit, in lines built
    /// from those ids and never from the event the commit logged - which named an NPC holder who
    /// saved themselves twice ("Emma Brown saves Emma Brown"). Unity-free, so the dotnet subset runs
    /// it (Tools/SimulationTests).
    /// </summary>
    public sealed class VetoMeetingReadTests
    {
        /// <summary>The veto meeting on the default cast: an NPC Head of Household, the next two NPCs on the block, the fourth holding the veto.</summary>
        private static EpisodeState AtTheMeeting(uint seed = 41)
        {
            var s = ContentCatalog.Create(seed);
            var npcs = Npcs(s);
            s.phase = EpisodePhase.VetoMeeting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).ToList();
            return s;
        }

        private static List<string> Npcs(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static string Name(EpisodeState s, string id) => s.Find(id).name;

        /// <summary>The block after a used veto, as the engine leaves it: the saved nominee off, the replacement on the end.</summary>
        private static void Replace(EpisodeState s, string saved, string replacement)
        {
            s.nominees.Remove(saved);
            s.nominees.Add(replacement);
        }

        [Test]
        public void AUsedVetoNamesWhoCameOffTheBlockAndWhoWentUp()
        {
            var s = AtTheMeeting();
            var npcs = Npcs(s);
            var before = s.nominees.ToList();
            string saved = before[0], kept = before[1], replacement = npcs[4];
            Replace(s, saved, replacement);

            var meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.used, Is.True);
            Assert.That(meeting.savedId, Is.EqualTo(saved));
            Assert.That(meeting.replacementId, Is.EqualTo(replacement));
            Assert.That(meeting.HasReplacement, Is.True);
            Assert.That(meeting.blockBefore, Is.EqualTo(before), "The block before the meeting, in the order it was named.");
            Assert.That(meeting.finalBlock, Is.EqualTo(new[] { kept, replacement }), "The block that goes to the vote.");
            Assert.That(meeting.holderOnTheBlock, Is.False);
            Assert.That(meeting.introLine, Is.EqualTo(Name(s, s.vetoHolderId) + " holds the Golden Power of Veto."));
            Assert.That(meeting.questionLine, Is.EqualTo("Will " + Name(s, s.vetoHolderId) + " use the veto?"));
            Assert.That(meeting.decisionHeadline, Is.EqualTo(VetoMeetingRead.UsedHeadline));
            Assert.That(meeting.decisionLine, Is.EqualTo(Name(s, s.vetoHolderId) + " uses the veto on " + Name(s, saved) + "."));
            Assert.That(meeting.replacementLine, Is.EqualTo(Name(s, s.hohId) + " names " + Name(s, replacement) + " as the replacement nominee."));
            Assert.That(meeting.outcomeLine, Is.EqualTo(VetoMeetingRead.UsedLine));

            // Read by who changed, never by where they stand in the list: a block written in
            // another order names the same two.
            s.nominees = new List<string> { replacement, kept };
            meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.savedId, Is.EqualTo(saved));
            Assert.That(meeting.replacementId, Is.EqualTo(replacement));
        }

        [Test]
        public void AnUnusedVetoKeepsTheBlockAndNamesNobody()
        {
            var s = AtTheMeeting();
            var before = s.nominees.ToList();

            var meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.used, Is.False);
            Assert.That(meeting.savedId, Is.Null);
            Assert.That(meeting.replacementId, Is.Null);
            Assert.That(meeting.HasReplacement, Is.False, "No replacement page when nobody went up.");
            Assert.That(meeting.finalBlock, Is.EqualTo(before));
            Assert.That(meeting.decisionHeadline, Is.EqualTo(VetoMeetingRead.NotUsedHeadline));
            Assert.That(meeting.decisionLine, Is.EqualTo(Name(s, s.vetoHolderId) + " does not use the veto."));
            Assert.That(meeting.replacementLine, Is.Null);
            Assert.That(meeting.outcomeLine, Is.EqualTo(VetoMeetingRead.NotUsedLine));
        }

        /// <summary>The copy bug the screen must not repeat: an NPC holder who saves themselves is said with their reflexive, never their name twice.</summary>
        [TestCase("she/her", "herself")]
        [TestCase("he/him", "himself")]
        [TestCase("they/them", "themselves")]
        public void AHolderWhoSavesThemselvesIsSaidWithTheirReflexive(string pronouns, string reflexive)
        {
            var s = AtTheMeeting();
            var npcs = Npcs(s);
            var before = s.nominees.ToList();
            string holder = before[0];
            s.vetoHolderId = holder;
            s.Find(holder).pronouns = pronouns;
            Replace(s, holder, npcs[4]);

            var meeting = VetoMeetingRead.Read(s, before);
            string name = Name(s, holder);
            Assert.That(meeting.holderOnTheBlock, Is.True, "The holder sat on the block.");
            Assert.That(meeting.savedId, Is.EqualTo(holder));
            Assert.That(meeting.decisionLine, Is.EqualTo(name + " uses the veto on " + reflexive + "."));
            foreach (var line in meeting.Lines)
                Assert.That(line.Split(new[] { name }, System.StringSplitOptions.None).Length - 1, Is.LessThanOrEqualTo(1),
                    "Nobody is named twice in one line: " + line);
        }

        [Test]
        public void ThePlayerIsSpokenToInTheSecondPerson()
        {
            // The player holds the veto and saves a houseguest.
            var s = AtTheMeeting();
            var npcs = Npcs(s);
            var before = s.nominees.ToList();
            s.vetoHolderId = s.playerId;
            Replace(s, before[0], npcs[4]);
            var meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.introLine, Is.EqualTo("You hold the Golden Power of Veto."));
            Assert.That(meeting.questionLine, Is.EqualTo("Will you use the veto?"));
            Assert.That(meeting.decisionLine, Is.EqualTo("You use the veto on " + Name(s, before[0]) + "."));

            // The player holds it from the block and saves themselves.
            s = AtTheMeeting();
            npcs = Npcs(s);
            s.nominees = new List<string> { s.playerId, npcs[2] };
            before = s.nominees.ToList();
            s.vetoHolderId = s.playerId;
            Replace(s, s.playerId, npcs[4]);
            meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.decisionLine, Is.EqualTo("You use the veto on yourself."));
            Assert.That(meeting.holderOnTheBlock, Is.True);

            // The player holds it and keeps the block.
            s = AtTheMeeting();
            before = s.nominees.ToList();
            s.vetoHolderId = s.playerId;
            Assert.That(VetoMeetingRead.Read(s, before).decisionLine, Is.EqualTo("You do not use the veto."));

            // A houseguest saves the player, and the player as Head of Household names the replacement.
            s = AtTheMeeting();
            npcs = Npcs(s);
            s.hohId = s.playerId;
            s.nominees = new List<string> { npcs[0], npcs[1] };
            before = s.nominees.ToList();
            s.vetoHolderId = npcs[2];
            Replace(s, npcs[0], npcs[4]);
            meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.replacementLine, Is.EqualTo("You name " + Name(s, npcs[4]) + " as the replacement nominee."));

            // A houseguest saves the player off the block.
            s = AtTheMeeting();
            npcs = Npcs(s);
            s.nominees = new List<string> { s.playerId, npcs[2] };
            before = s.nominees.ToList();
            Replace(s, s.playerId, npcs[4]);
            meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.decisionLine, Is.EqualTo(Name(s, s.vetoHolderId) + " uses the veto on you."));

            // The player goes up as the replacement.
            s = AtTheMeeting();
            before = s.nominees.ToList();
            Replace(s, before[0], s.playerId);
            meeting = VetoMeetingRead.Read(s, before);
            Assert.That(meeting.replacementLine, Is.EqualTo(Name(s, s.hohId) + " names you as the replacement nominee."));
        }

        /// <summary>
        /// Through the engine itself: the player holds the veto and uses it, the engine's Head of
        /// Household names the replacement, and the card reads the commit - never its sentence.
        /// </summary>
        [Test]
        public void TheCardReadsTheCommitAndNeverItsSentence()
        {
            var s = AtTheMeeting();
            s.vetoHolderId = s.playerId;
            var before = s.nominees.ToList();
            var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ResolveVeto);
            command.useVeto = true;
            command.targetId = before[0];
            command.secondTargetId = EpisodeEngine.ReplacementCandidates(s).First().id;
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            var committed = result.state;

            var meeting = VetoMeetingRead.Read(committed, before);
            Assert.That(meeting.savedId, Is.EqualTo(before[0]));
            Assert.That(meeting.replacementId, Is.EqualTo(committed.nominees.Except(before).Single()), "Whoever the engine's Head of Household named.");
            Assert.That(meeting.finalBlock, Is.EqualTo(committed.nominees));
            string sentence = committed.events.Last(entry => entry.kind == "veto").text;
            foreach (var line in meeting.Lines)
                Assert.That(line, Is.Not.EqualTo(sentence).And.Not.Contain(sentence), "The engine's sentence on the card.");
        }

        [Test]
        public void WithoutTheBlockBeforeTheMeetingNothingIsSaidToHaveChanged()
        {
            var s = AtTheMeeting();
            var npcs = Npcs(s);
            Replace(s, s.nominees[0], npcs[4]);
            var meeting = VetoMeetingRead.Read(s, null);
            Assert.That(meeting.used, Is.False, "Nothing says the veto was used.");
            Assert.That(meeting.blockBefore, Is.EqualTo(meeting.finalBlock));
            Assert.That(VetoMeetingRead.Read(s, new List<string>()).used, Is.False);
        }

        [Test]
        public void EveryFaceIsNamedOnceAndReadingChangesNothing()
        {
            var s = AtTheMeeting();
            var npcs = Npcs(s);
            var before = s.nominees.ToList();
            s.vetoHolderId = before[0];
            Replace(s, before[0], npcs[4]);
            string json = JsonConvert.SerializeObject(s);
            uint random = s.randomState;

            var meeting = VetoMeetingRead.Read(s, before);
            var people = meeting.People.ToList();
            Assert.That(people, Is.Unique);
            Assert.That(people, Is.EquivalentTo(new[] { s.hohId, before[0], before[1], npcs[4] }),
                "The Head of Household, the block before and after, and the holder once though they sat on the block.");
            Assert.That(JsonConvert.SerializeObject(s), Is.EqualTo(json), "Reading the meeting must not change the state.");
            Assert.That(s.randomState, Is.EqualTo(random), "or draw from its generator.");
        }
    }
}
