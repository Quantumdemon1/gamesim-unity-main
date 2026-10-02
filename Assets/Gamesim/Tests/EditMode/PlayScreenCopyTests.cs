using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The copy and the knowledge in the play screens (UI-UX-PASS-PLAN D0, decisions 18 and 19):
    /// what a plain conversation prints and where a houseguest's goal is learned, whose nomination
    /// a memory names, how a voting bloc is spelled, and a walk-in's room in words. Nothing of
    /// Unity's, so the Unity-free subset runs it.
    /// </summary>
    public sealed class PlayScreenCopyTests
    {
        private static EpisodeState Apply(EpisodeEngine engine, EpisodeCommandKind kind, string targetId)
        {
            var command = EpisodeEngineTests.Command(engine.Snapshot, kind);
            command.targetId = targetId;
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, kind + ": " + result.reason);
            return result.state;
        }

        /// <summary>
        /// A Talk's line says what happened and never the houseguest's motive. The motive is their
        /// cast-template goal, a lore facet the player is meant to learn; one plain conversation
        /// used to print it to Recent events, the toast and the recap (the play sweep's row 7).
        /// </summary>
        [TestCase(4u)] [TestCase(11u)] [TestCase(27u)]
        public void ATalkSaysWhatHappenedAndNeverTheirMotive(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Social), "A season opens in free time.");
            foreach (var target in engine.Snapshot.Active.Where(c => !c.isPlayer).Take(2).ToList())
            {
                Assert.That(target.motive, Is.Not.Null.And.Not.Empty, "The template gives everybody a motive.");
                int count = engine.Snapshot.events.Count;
                var after = Apply(engine, EpisodeCommandKind.Talk, target.id);
                var added = after.events.Skip(count).ToList();
                var line = added.Single(e => e.kind == "conversation");
                Assert.That(line.text, Is.EqualTo("You and " + target.name + " talked about the game."));
                Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, target.id }), "The line is the pair's.");
                foreach (var e in added) Assert.That(e.text, Does.Not.Contain(target.motive), e.kind + " names the motive.");
                Assert.That(after.memories.Any(m => m.ownerId == target.id && m.subjectId == after.playerId
                    && m.text == "We spent time talking in week " + after.week + "."), Is.True, "Their memory of it is as it was.");
            }
        }

        /// <summary>With the story on a Talk still teaches what it opens onto, and never the goal.</summary>
        [Test]
        public void WithTheStoryOnATalkNeverTeachesTheGoal()
        {
            var s = ContentCatalog.Create(4);
            EpisodeEngine.EnableStory(s, 1);
            var maya = s.Find(ContentCatalog.MayaId);
            var goal = Lore.Facet(s, maya.id, Lore.Facets.Goal);
            Assert.That(goal, Is.Not.Null, "Maya's sheet carries her goal.");
            var engine = new EpisodeEngine(s);
            int count = engine.Snapshot.events.Count;
            var after = Apply(engine, EpisodeCommandKind.Talk, maya.id);
            foreach (var e in after.events.Skip(count))
            {
                Assert.That(e.text, Does.Not.Contain(maya.motive), e.kind + " names the motive.");
                Assert.That(e.text, Does.Not.Contain(goal.text), e.kind + " names the goal.");
            }
            Assert.That(Lore.Knows(after, goal.id), Is.False, "Spending time together never teaches the goal.");
        }

        /// <summary>
        /// The goal is still learned where it was (decision 19): game talk at rapport 3 reveals it
        /// first, a story's reveal can hand it over, and the plain conversation never opens onto it.
        /// </summary>
        [Test]
        public void TheGoalIsStillLearnedWhereItWas()
        {
            var s = ContentCatalog.Create(4);
            EpisodeEngine.EnableStory(s, 1);
            var maya = s.Find(ContentCatalog.MayaId);
            var goal = Lore.Facet(s, maya.id, Lore.Facets.Goal);
            Assert.That(goal, Is.Not.Null, "Maya's sheet carries her goal.");
            Assert.That(goal.text, Is.EqualTo("A dependable voting partnership, and not one promise she cannot keep."));
            Assert.That(Lore.FacetsFor(EpisodeCommandKind.Talk), Does.Not.Contain(Lore.Facets.Goal), "Spending time together does not open onto the goal;");
            foreach (var kind in new[] { EpisodeCommandKind.DiscussGame, EpisodeCommandKind.StrategicDiscussion, EpisodeCommandKind.InviteUp, EpisodeCommandKind.AllianceMeet })
                Assert.That(Lore.FacetsFor(kind), Does.Contain(Lore.Facets.Goal), kind + " does.");
            s.story.contacts.Add(new ContactState { npcId = maya.id, rapport = 3 });
            Assert.That(Lore.NextReveal(s, maya.id, EpisodeCommandKind.DiscussGame, false)?.facet, Is.EqualTo(Lore.Facets.Goal),
                "At rapport 3 game talk reveals the goal first.");
            Assert.That(Lore.NextReveal(s, maya.id, EpisodeCommandKind.Talk, false)?.facet, Is.Not.EqualTo(Lore.Facets.Goal));
            Assert.That(Lore.Learn(s, goal.id), Is.True, "A reveal teaches it.");
            Assert.That(Lore.Learned(s, maya.id).Select(f => f.id), Does.Contain(goal.id));
        }

        /// <summary>
        /// A nomination is read with the Head of Household's name: the diary's chair shows the
        /// player's latest memory as its caption, and "Nominated me in week 2." had no subject (the
        /// play sweep's row 38). The engine's text stays nameless - WebEvictionVoting.Memory matches
        /// a memory to a nominee by name, so a named line would move recorded seasons' ballots -
        /// and MemoryWords.Said names the subject the memory carries.
        /// </summary>
        [TestCase(4u)] [TestCase(9u)]
        public void ANominationIsRememberedWithTheHeadOfHouseholdsName(uint seed)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int guard = 0; guard < 60 && engine.Snapshot.nominees.Count < 2; guard++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var s = engine.Snapshot;
            Assert.That(s.nominees, Has.Count.EqualTo(2), "The season reaches its first nominations.");
            var hoh = s.Find(s.hohId);
            Assert.That(hoh, Is.Not.Null);
            foreach (var nominee in s.nominees)
            {
                var memory = s.memories.LastOrDefault(m => m.ownerId == nominee && m.subjectId == s.hohId && m.text == "Nominated me in week " + s.week + ".");
                Assert.That(memory, Is.Not.Null, s.Find(nominee).name + "'s memory stays nameless: the vote evaluator matches memories by nominee name.");
                Assert.That(memory.isPrivate, Is.False, "A nomination is public.");
                Assert.That(MemoryWords.Said(s, memory), Is.EqualTo(hoh.name + " nominated me in week " + s.week + "."), "The player reads it with the Head of Household's name.");
            }
            // The player as Head of Household is "You" to the houseguest they nominated.
            var byYou = new MemoryState { ownerId = s.nominees[0], subjectId = s.playerId, text = "Nominated me in week 3.", week = 3 };
            Assert.That(MemoryWords.Said(s, byYou), Is.EqualTo("You nominated me in week 3."));
            // Any other memory reads as it was written, a copy of the line included.
            foreach (string text in new[] { "We spent time talking in week 1.", "Heard from you: Nominated me in week 1.", "" })
                Assert.That(MemoryWords.Said(s, new MemoryState { ownerId = s.playerId, subjectId = s.nominees[0], text = text, week = 1 }), Is.EqualTo(text), text);
            Assert.That(MemoryWords.Said(s, new MemoryState { ownerId = s.playerId, subjectId = null, text = "Nominated me in week 1.", week = 1 }),
                Is.EqualTo("Nominated me in week 1."), "No subject, no name to say.");
            Assert.That(MemoryWords.Said(s, null), Is.Null);
        }

        /// <summary>
        /// A walk-in says the room in words: the command carries the house's room id, and the
        /// card and the toast read "are in Living, mid-argument" (the play sweep's row 46).
        /// </summary>
        [Test]
        public void AWalkInSaysTheRoomInWords()
        {
            var state = ContentCatalog.Create(5);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            string Narrative(string room) => HouseEventSources.Proximity(state, cast[0].id, cast[1].id, room, 1).narrative;
            Assert.That(Narrative("Living"), Does.StartWith(cast[0].name + " and " + cast[1].name + " are in the living room,"));
            Assert.That(Narrative("HoH"), Does.Contain(" are in the HoH suite,"), "Capitals that mean something keep them.");
            Assert.That(Narrative("Games"), Does.Contain(" are in the game room,"));
            Assert.That(Narrative("the kitchen"), Does.Contain(" are in the kitchen,"), "Words already said stay as they came.");
            Assert.That(Narrative(null), Does.Contain(" are in the house,"));
            Assert.That(Narrative("Living"), Does.Not.Contain("in Living"));
            Assert.That(RoomWords.Where("  "), Is.EqualTo("the house"));
            foreach (var room in RoomWords.Rooms)
            {
                Assert.That(RoomWords.IsRoom(room), Is.True, room);
                Assert.That(RoomWords.Where(room), Is.EqualTo("the " + RoomWords.InSentence(room)));
                Assert.That(RoomWords.InSentence(room), Is.Not.Empty.And.Not.Contain("the "), room + " reads without its article.");
            }
            Assert.That(RoomWords.InSentence("Living"), Is.EqualTo("living room"));
            Assert.That(RoomWords.InSentence("HoH"), Is.EqualTo("HoH suite"), "Not \"hoh suite\".");
            Assert.That(RoomWords.InSentence("Yard"), Is.EqualTo("competition yard"));
            Assert.That(RoomWords.IsRoom("Attic"), Is.False);
            Assert.That(RoomWords.Name("Attic"), Is.EqualTo("Attic"));
        }

        /// <summary>
        /// A voting bloc is spelled as one (the play sweep's row 31), on the caption that proposes
        /// it and in every line the engine writes; a line written under the reference's "voting
        /// block" still reads as the deal it was.
        /// </summary>
        [Test]
        public void AVotingBlocIsSpelledAsOneAndOldLinesStillRead()
        {
            Assert.That(DealKind.Title(DealKind.VoteTogether), Is.EqualTo("Voting Bloc"));
            Assert.That(YourWeek.DealWords(DealKind.VoteTogether), Is.EqualTo("voting bloc"));
            Assert.That(DealKind.Titles(DealKind.VoteTogether), Is.EqualTo(new[] { "Voting Bloc", "Voting Block" }));
            foreach (var kind in DealKind.All.Where(k => k != DealKind.VoteTogether))
                Assert.That(DealKind.Titles(kind), Is.EqualTo(new[] { DealKind.Title(kind) }), kind + " has one spelling.");
            var s = ContentCatalog.Create(6);
            var npc = s.contestants.First(c => !c.isPlayer);
            string you = s.Find(s.playerId).name;
            foreach (var spelling in new[] { "voting bloc", "voting block" })
            {
                Assert.That(YourWeek.ReadDeal(s, you + " and " + npc.name + " fell out over their " + spelling + ".", npc.id,
                    out string actor, out string type, out bool kept), Is.True, spelling);
                Assert.That((actor, type, kept), Is.EqualTo(((string)null, DealKind.VoteTogether, false)), spelling);
                Assert.That(YourWeek.ReadDeal(s, npc.name + " honoured a " + spelling + " with " + you + ".", npc.id,
                    out actor, out type, out kept), Is.True, spelling);
                Assert.That((actor, type, kept), Is.EqualTo((npc.id, DealKind.VoteTogether, true)), spelling);
            }
        }
    }
}
