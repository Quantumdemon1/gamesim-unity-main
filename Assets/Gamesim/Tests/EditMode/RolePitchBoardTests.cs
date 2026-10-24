using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Head of Household's pitch board (ROLE-MOMENTS-PLAN R5, §3.1), through the public engine. Under the role moments a
    /// nomination brings the player at most three pitches - the two they read warmest and the one they read coolest, by their
    /// own reading and nothing hidden - while every courting houseguest still courts; the board is answered in any order; a
    /// promise of safety moves nothing at the answer and pays +8 at the nominations to those left off the block, and naming
    /// one later breaks it under the promise rules; a no costs the card's tone's honest no, and eases the grudge of a pitcher
    /// the player nominates anyway, at the ceremony or as the replacement, unless they were Threatening; nominating clears the
    /// rest at no cost; and validation holds the board's size and numbers. A season without the role moments - every committed
    /// fixture - keeps every courting houseguest's pitch, one at a time, at the answers' old numbers. Pure simulation: the
    /// Unity-free subset runs it. <c>RolePitchBoardPersistenceTests</c> reloads the board through the shipping save store.
    /// </summary>
    public sealed class RolePitchBoardTests
    {
        private const string Courting = RoleTone.Role.Courting;

        // ------------------------------------------------------------------ the cap and the selection

        /// <summary>
        /// Under the role moments the board holds three of the seven houseguests who court the player in week one: the two the
        /// player reads warmest, then the one they read coolest, in that order - <see cref="HoHPitches.BoardPicks"/> of every
        /// one who came. Every one of them still courted: the season is the one without the role moments, draw for draw, the
        /// cards aside.
        /// </summary>
        [Test]
        public void UnderRolesTheBoardIsTheTwoWarmestAndTheCoolestAndEveryoneStillCourts()
        {
            var crowned = Crowned();
            var courters = Courters(crowned);
            Assert.That(courters, Has.Count.EqualTo(7), "Every houseguest courts the player in week one.");
            var engine = Dealt(crowned, out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var after = engine.Snapshot;
            Assert.That(after.replyCards.Select(c => c.fromId), Is.EqualTo(board), "The warmest, the next warmest, then the coolest.");
            Assert.That(HoHPitches.BoardPicks(after, courters), Is.EqualTo(board), "The board is the picks of everybody who came.");
            Assert.That(after.replyCards.All(c => c.kind == ReplyCards.Pitch && c.week == after.week), Is.True);

            var old = Shaped(crowned, board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            old.story.rulesVersion = StoryRules.Threads;
            var oldAfter = Apply(new EpisodeEngine(old), Command(old, EpisodeCommandKind.Advance));
            Assert.That(oldAfter.replyCards.Select(c => c.fromId), Is.EquivalentTo(courters), "Without the role moments every one of them pitches.");
            Assert.That(after.randomState, Is.EqualTo(oldAfter.randomState), "The board draws nothing.");
            var same = after.Clone();
            same.replyCards = oldAfter.replyCards.Select(c => c.Clone()).ToList();
            same.story.rulesVersion = StoryRules.Threads;
            Assert.That(Json(same), Is.EqualTo(Json(oldAfter)), "Every courting houseguest still courted: only the cards differ.");
        }

        /// <summary>
        /// The board reads the player's reading alone: moving how every houseguest reads the player, and each other - all
        /// alike, against the player's reading, or the board's three coldest of all - leaves the same three cards in the same
        /// order.
        /// </summary>
        [Test]
        public void HiddenWarmthNeverMovesTheBoard()
        {
            var crowned = Crowned();
            var engine = Dealt(crowned, out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var reference = Shaped(crowned, board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var views = new List<(string what, Func<string, double> view)>
            {
                ("every view at -100", id => -100), ("every view at 100", id => 100),
                // The coolest the warmest toward the player, the warmest the coldest: a ranking by their view turns the board over.
                ("against the reading", id => id == board[2] ? 100 : id == board[1] ? 0 : id == board[0] ? -100 : 50),
                ("the board coldest of all", id => board.Contains(id) ? -100 : 100),
            };
            foreach (var (what, view) in views)
            {
                var s = reference.Clone();
                int flip = 0;
                foreach (var edge in s.relationships.Where(r => r.fromId != s.playerId))
                    edge.score = edge.toId == s.playerId ? view(edge.fromId) : (flip++ % 2 == 0 ? 100 : -100);
                Assert.That(s.Active.Where(c => !c.isPlayer).Select(c => s.Score(s.playerId, c.id)),
                    Is.EqualTo(reference.Active.Where(c => !c.isPlayer).Select(c => reference.Score(reference.playerId, c.id))), what + ": the player's reading is untouched.");
                var after = Apply(new EpisodeEngine(s), Command(s, EpisodeCommandKind.Advance));
                Assert.That(after.replyCards.Select(c => c.fromId), Is.EqualTo(engine.Snapshot.replyCards.Select(c => c.fromId)),
                    what + ": the same three, in the same order.");
            }
        }

        /// <summary>
        /// The picks rank by the player's own reading: close first - an ally reads close whatever the score - then the player's
        /// own score, ties by id, the coolest the same way round; three or fewer are all kept, warmest first.
        /// </summary>
        [Test]
        public void ThePicksRankByThePlayersReadingAloneTiesById()
        {
            var s = Crowned();
            var ids = s.Active.Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            foreach (var id in ids) Set(s, s.playerId, id, 0);
            Assert.That(HoHPitches.BoardPicks(s, ids), Is.EqualTo(new[] { ids[0], ids[1], ids[2] }), "All level: by id.");
            Set(s, s.playerId, ids[5], 50);
            Set(s, s.playerId, ids[3], -20);
            Set(s, s.playerId, ids[4], -20);
            Assert.That(HoHPitches.BoardPicks(s, ids), Is.EqualTo(new[] { ids[5], ids[0], ids[3] }), "The warmest two, then the coolest, its tie by id.");
            s.alliances.Add(new AllianceState { id = "alliance-pitch-board", name = "The Board Pact", members = new List<string> { s.playerId, ids[6] }, active = true });
            Set(s, s.playerId, ids[6], -30);
            Assert.That(ReplyCards.ReadsAsClose(s, ids[6]), Is.True);
            Assert.That(HoHPitches.BoardPicks(s, ids), Is.EqualTo(new[] { ids[5], ids[6], ids[3] }),
                "An ally reads close whatever the score, ahead of everybody read neutral; between two read close, the score.");
            foreach (var edge in s.relationships.Where(r => r.toId == s.playerId)) edge.score = -100;
            Assert.That(HoHPitches.BoardPicks(s, ids), Is.EqualTo(new[] { ids[5], ids[6], ids[3] }), "How they read the player is never read.");
            Assert.That(HoHPitches.BoardPicks(s, new[] { ids[3], ids[0] }), Is.EqualTo(new[] { ids[0], ids[3] }), "Two are both kept, warmest first.");
            Assert.That(HoHPitches.BoardPicks(s, new[] { ids[3], ids[0], ids[5] }), Is.EqualTo(new[] { ids[5], ids[0], ids[3] }));
            Assert.That(HoHPitches.BoardPicks(s, new string[0]), Is.Empty);
        }

        /// <summary>
        /// A season without the role moments keeps E4 exactly: every courting houseguest pitches, one at a time - a later card
        /// is refused - a promise is +8 as it is made and a no -5, and the nominations pay nothing more.
        /// </summary>
        [Test]
        public void WithoutRolesEveryPitcherComesOneAtATimeAtTheOldNumbers()
        {
            var old = Crowned().Clone();
            old.story.rulesVersion = StoryRules.Threads;
            var engine = new EpisodeEngine(old);
            var s = Apply(engine, Command(old, EpisodeCommandKind.Advance));
            Assert.That(EpisodeEngine.RolesOn(s), Is.False);
            Assert.That(s.replyCards, Has.Count.EqualTo(7), "Every courting houseguest pitches.");
            var later = engine.Apply(Answer(s, s.replyCards[1].id, HoHPitches.HearKey));
            Assert.That((later.accepted, later.reason), Is.EqualTo((false, "Answer the houseguest currently speaking first.")));
            Assert.That(HoHPitches.Answerable(s, s.replyCards[1].id), Is.False);
            Assert.That(HoHPitches.Answerable(s, s.replyCards[0].id), Is.True);

            string promised = s.replyCards[0].fromId, refused = s.replyCards[1].fromId;
            s = Apply(engine, Answer(s, s.replyCards[0].id, HoHPitches.PromiseKey));
            Assert.That(Row(s, promised).toThem, Is.EqualTo(8));
            Assert.That(Events(s, promised, "reply").Select(e => e.impactScore), Is.EqualTo(new[] { Adjusted(s, 8) }), "+8 as the promise is made.");
            s = Apply(engine, Answer(s, s.replyCards[0].id, HoHPitches.TurnDownKey));
            Assert.That(Row(s, refused).toThem, Is.EqualTo(-5));
            Assert.That(Events(s, refused, "reply").Select(e => e.impactScore), Is.EqualTo(new[] { Adjusted(s, -5) }), "-5 for a no.");

            string other = s.Active.First(c => !c.isPlayer && c.id != promised && c.id != refused).id;
            var nominated = Apply(engine, Command(s, EpisodeCommandKind.Nominate, refused, other));
            Assert.That(nominated.replyCards, Is.Empty);
            Assert.That(Events(nominated, promised, "reply").Select(e => e.impactScore), Is.EqualTo(new[] { Adjusted(s, 8) }), "The nominations pay nothing more.");
            Assert.That(nominated.events.Any(e => e.text != null && e.text.Contains("stayed off your block")), Is.False);
        }

        // ------------------------------------------------------------------ any order

        /// <summary>Under the role moments any card on the board can be answered first, and each answer takes only its own card.</summary>
        [Test]
        public void UnderRolesAnyPitchOnTheBoardCanBeAnsweredFirst()
        {
            var engine = Dealt(Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            Assert.That(s.replyCards.All(c => HoHPitches.Answerable(s, c.id)), Is.True, "Every card on the board is the player's to answer.");
            s = Apply(engine, Answer(s, CardOf(s, board[2]).id, HoHPitches.HearKey));
            Assert.That(s.replyCards.Select(c => c.fromId), Is.EqualTo(new[] { board[0], board[1] }), "The last card first: only it leaves.");
            s = Apply(engine, Answer(s, CardOf(s, board[1]).id, HoHPitches.FeelOutKey));
            Assert.That(s.replyCards.Select(c => c.fromId), Is.EqualTo(new[] { board[0], board[1] }), "Feeling one out keeps its card.");
            s = Apply(engine, Answer(s, CardOf(s, board[1]).id, HoHPitches.TurnDownKey));
            s = Apply(engine, Answer(s, CardOf(s, board[0]).id, HoHPitches.PromiseKey));
            Assert.That(s.replyCards, Is.Empty);
            Assert.That(s.ledger.replies.Where(r => r.week == s.week && r.kind == ReplyCards.Pitch).Select(r => (r.fromId, r.replyKey)),
                Is.EqualTo(new[] { (board[2], HoHPitches.HearKey), (board[1], HoHPitches.FeelOutKey), (board[1], HoHPitches.TurnDownKey), (board[0], HoHPitches.PromiseKey) }));
        }

        // ------------------------------------------------------------------ promise safety

        /// <summary>
        /// Under the role moments a promise moves nothing as it is made: no change, no draw. At the nominations it pays +8 to the
        /// pitcher left off the block, and the player is told; the one nominated has their promise broken by it and is paid
        /// nothing; the card left unanswered clears with the others at no cost.
        /// </summary>
        [Test]
        public void APromisePaysAtTheNominationsOnlyThoseLeftOffTheBlock()
        {
            var engine = Dealt(Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            var before = s.Clone();
            s = Apply(engine, Answer(s, CardOf(s, board[0]).id, HoHPitches.PromiseKey));
            s = Apply(engine, Answer(s, CardOf(s, board[1]).id, HoHPitches.PromiseKey));
            foreach (string id in new[] { board[0], board[1] })
            {
                Assert.That(Row(s, id).toThem, Is.EqualTo(0), "Nothing recorded at the answer.");
                Assert.That(Events(s, id, "reply"), Is.Empty, "No warmth at the answer.");
                Assert.That(s.Score(s.playerId, id), Is.EqualTo(before.Score(before.playerId, id)));
                Assert.That(HoHPitches.HasSafetyPromise(s, id), Is.True, "The promise of safety is made, as always.");
            }
            Assert.That(s.randomState, Is.EqualTo(before.randomState), "A promise draws nothing at the answer.");

            string other = s.Active.First(c => !c.isPlayer && !board.Contains(c.id)).id;
            var nominated = Apply(engine, Command(s, EpisodeCommandKind.Nominate, board[1], other));
            Assert.That(nominated.replyCards, Is.Empty, "Nominating clears the rest.");
            Assert.That(Events(nominated, board[0], "reply").Select(e => (e.description, e.impactScore)),
                Is.EqualTo(new[] { ("Kept me off the block, as promised.", Adjusted(s, HoHPitches.KeptOffTheBlock)) }), "+8 for the promise kept at the nominations.");
            Assert.That(nominated.events.Count(e => e.text == nominated.Find(board[0]).name + " stayed off your block, as you promised."), Is.EqualTo(1));
            Assert.That(Events(nominated, board[1], "reply"), Is.Empty, "Nominated: nothing paid,");
            var broken = CommitmentReferences.Promises(nominated).Single(p => p.fromId == s.playerId && p.toId == board[1] && p.kind == PromiseKind.Safety);
            Assert.That((broken.status, broken.brokenById), Is.EqualTo((PromiseStatus.Broken, s.playerId)), "and the promise broken by the nomination.");
            Assert.That(Events(nominated, board[2], "reply"), Is.Empty, "Left unanswered: no cost.");
            Assert.That(nominated.ledger.replies.Count(r => r.week == s.week && r.fromId == board[2]), Is.EqualTo(0), "Silence writes no answer.");
        }

        /// <summary>
        /// Only a promise pays at the nominations: with all three left off the block, the promised pitcher is paid and told of,
        /// while one heard - or turned down - beside them keeps only what their answer moved, and one left unanswered is owed
        /// nothing; no line says either of them stayed off the block as promised.
        /// </summary>
        [TestCase(HoHPitches.HearKey)] [TestCase(HoHPitches.TurnDownKey)]
        public void OnlyAPromisePaysAtTheNominations(string answer)
        {
            var engine = Dealt(Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = Apply(engine, Answer(engine.Snapshot, CardOf(engine.Snapshot, board[0]).id, HoHPitches.PromiseKey));
            s = Apply(engine, Answer(s, CardOf(s, board[1]).id, answer));
            var answered = Events(s, board[1], "reply").Select(e => (e.description, e.impactScore)).ToList();
            Assert.That(answered, Has.Count.EqualTo(answer == HoHPitches.TurnDownKey ? 1 : 0), answer + ": what the answer itself moved.");

            var others = s.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            var nominated = Apply(engine, Command(s, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(nominated.nominees.Intersect(board), Is.Empty, "All three are left off the block.");
            Assert.That(Events(nominated, board[0], "reply").Select(e => (e.description, e.impactScore)),
                Is.EqualTo(new[] { ("Kept me off the block, as promised.", Adjusted(s, HoHPitches.KeptOffTheBlock)) }), "The promise kept pays.");
            Assert.That(Events(nominated, board[1], "reply").Select(e => (e.description, e.impactScore)), Is.EqualTo(answered),
                answer + ": the nominations pay nothing more.");
            Assert.That(Events(nominated, board[2], "reply"), Is.Empty, "Left unanswered: nothing either.");
            foreach (string id in board)
                Assert.That(nominated.events.Count(e => e.text == nominated.Find(id).name + " stayed off your block, as you promised."),
                    Is.EqualTo(id == board[0] ? 1 : 0), id == board[0] ? "The player is told of the promise kept," : "and of nothing else.");
        }

        /// <summary>
        /// A promised pitcher left off at the ceremony and then named as the replacement: the +8 was paid at the nominations,
        /// and naming them breaks the promise under the promise rules - in every commitment mode.
        /// </summary>
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void NamingAPromisedPitcherAsTheReplacementBreaksThePromise(int mode)
        {
            var engine = Dealt(Crowned(1, mode), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = Apply(engine, Answer(engine.Snapshot, CardOf(engine.Snapshot, board[0]).id, HoHPitches.PromiseKey));
            var others = s.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            s = Apply(engine, Command(s, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(Events(s, board[0], "reply").Count(e => e.description == "Kept me off the block, as promised."), Is.EqualTo(1));
            var decided = UseTheVeto(engine, others[0], board[0]);
            Assert.That(decided.nominees, Does.Contain(board[0]), "Named as the replacement.");
            var promise = CommitmentReferences.Promises(decided).Single(p => p.fromId == s.playerId && p.toId == board[0] && p.kind == PromiseKind.Safety);
            Assert.That((promise.status, promise.brokenById), Is.EqualTo((PromiseStatus.Broken, s.playerId)), "mode " + mode + ": naming them breaks the promise.");
            Assert.That(Events(decided, board[0], "reply").Count(e => e.description == "Kept me off the block, as promised."), Is.EqualTo(1),
                "The +8 was the nominations', paid once.");
        }

        // ------------------------------------------------------------------ turn them down

        /// <summary>
        /// Under the role moments a no costs what the card's tone stakes - 0 to -3, the stakes table - as it is given, in place of
        /// -5: recorded on the answer and charged through the engine's own change, or nothing at all for a Calculated card.
        /// </summary>
        [TestCase(RoleTone.Bitter)] [TestCase(RoleTone.Threatening)] [TestCase(RoleTone.Desperate)] [TestCase(RoleTone.Friendly)]
        [TestCase(RoleTone.Earnest)] [TestCase(RoleTone.Calculated)] [TestCase(RoleTone.Pragmatic)]
        public void ANoCostsTheCardsTone(string tone)
        {
            var engine = Dealt(Crowned(2), out var board, tone, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            double decline = RoleTone.Stakes(tone).decline;
            Assert.That(HoHPitches.TurnDownCost(s, board[0]), Is.EqualTo(decline));
            var after = Apply(engine, Answer(s, CardOf(s, board[0]).id, HoHPitches.TurnDownKey));
            Assert.That(Row(after, board[0]).toThem, Is.EqualTo(decline), tone + ": recorded on the answer.");
            Assert.That(Events(after, board[0], "reply").Select(e => e.impactScore), Is.EqualTo(decline == 0 ? new double[0] : new[] { Adjusted(s, decline) }),
                tone + ": " + decline + " with them.");
            Assert.That(decline, Is.GreaterThanOrEqualTo(-3).And.LessThanOrEqualTo(0), "Never the old -5.");
        }

        /// <summary>
        /// A pitcher the player turned down and then nominated takes it easier: their nomination grudge is thirty less - but not
        /// a Threatening one's. Heard and nominated, or in a season without the role moments, the grudge is whole. (A promised
        /// pitcher nominated holds the broken word's grudge besides, under the promise rules, as always.)
        /// </summary>
        [TestCase(RoleTone.Pragmatic, HoHPitches.TurnDownKey, true, 30)]
        [TestCase(RoleTone.Earnest, HoHPitches.TurnDownKey, true, 30)]
        [TestCase(RoleTone.Threatening, HoHPitches.TurnDownKey, true, 0)]
        [TestCase(RoleTone.Pragmatic, HoHPitches.HearKey, true, 0)]
        [TestCase(RoleTone.Pragmatic, HoHPitches.TurnDownKey, false, 0)]
        public void AnHonestNoEasesTheNominationGrudge(string tone, string answer, bool roles, double eased)
        {
            var crowned = Crowned();
            var board = Board(crowned);
            var s = Shaped(crowned, board, tone, RoleTone.Pragmatic, RoleTone.Pragmatic);
            if (!roles) s.story.rulesVersion = StoryRules.Threads;
            var engine = new EpisodeEngine(s);
            s = Apply(engine, Command(s, EpisodeCommandKind.Advance));
            Assert.That(RoleTone.Of(s, board[0], Courting), Is.EqualTo(tone));
            // Without the role moments the cards come one at a time: the ones before theirs are heard first.
            while (s.replyCards[0].fromId != board[0]) s = Apply(engine, Answer(s, s.replyCards[0].id, HoHPitches.HearKey));
            s = Apply(engine, Answer(s, CardOf(s, board[0]).id, answer));
            Assert.That(Grudges.Severity(s, board[0], s.playerId), Is.EqualTo(0), "No grudge before the nominations.");
            string other = s.Active.First(c => !c.isPlayer && !board.Contains(c.id)).id;
            var nominated = Apply(engine, Command(s, EpisodeCommandKind.Nominate, board[0], other));
            double whole = Grudges.Severity(nominated, other, nominated.playerId);
            Assert.That(whole, Is.EqualTo(70), "The nomination's grudge, whole, for the one who did not pitch.");
            Assert.That(Grudges.Severity(nominated, board[0], nominated.playerId), Is.EqualTo(70 - eased), tone + " " + answer + (roles ? "" : " without the role moments"));
        }

        /// <summary>
        /// A pitcher the player turned down and then named as the replacement takes that easier too, unless Threatening; in a
        /// season without the role moments the replacement's grudge is whole.
        /// </summary>
        [TestCase(RoleTone.Pragmatic, true, 30)]
        [TestCase(RoleTone.Threatening, true, 0)]
        [TestCase(RoleTone.Pragmatic, false, 0)]
        public void AnHonestNoEasesTheReplacementsGrudge(string tone, bool roles, double eased)
        {
            var crowned = Crowned();
            var board = Board(crowned);
            var start = Shaped(crowned, board, tone, RoleTone.Pragmatic, RoleTone.Pragmatic);
            if (!roles) start.story.rulesVersion = StoryRules.Threads;
            var engine = new EpisodeEngine(start);
            var s = Apply(engine, Command(start, EpisodeCommandKind.Advance));
            Assert.That(RoleTone.Of(s, board[0], Courting), Is.EqualTo(tone));
            while (s.replyCards[0].fromId != board[0]) s = Apply(engine, Answer(s, s.replyCards[0].id, HoHPitches.HearKey));
            s = Apply(engine, Answer(s, CardOf(s, board[0]).id, HoHPitches.TurnDownKey));
            var others = s.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            s = Apply(engine, Command(s, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(Grudges.Severity(s, board[0], s.playerId), Is.EqualTo(0), "Left off at the ceremony, no grudge yet.");
            var decided = UseTheVeto(engine, others[0], board[0]);
            Assert.That(decided.nominees, Does.Contain(board[0]));
            Assert.That(Grudges.Severity(decided, board[0], decided.playerId), Is.EqualTo(70 - eased), tone + ": named as the replacement.");
        }

        /// <summary>
        /// A pitcher turned down and nominated at the ceremony who stays on the block through a used veto takes the honest no
        /// once: the veto's ease is the replacement's alone - here another pitcher the player turned down.
        /// </summary>
        [Test]
        public void AnHonestNoEasesANomineeOnceThroughAUsedVeto()
        {
            var engine = Dealt(Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = Apply(engine, Answer(engine.Snapshot, CardOf(engine.Snapshot, board[0]).id, HoHPitches.TurnDownKey));
            s = Apply(engine, Answer(s, CardOf(s, board[1]).id, HoHPitches.TurnDownKey));
            string other = s.Active.First(c => !c.isPlayer && !board.Contains(c.id)).id;
            s = Apply(engine, Command(s, EpisodeCommandKind.Nominate, board[0], other));
            Assert.That(Grudges.Severity(s, board[0], s.playerId), Is.EqualTo(70 - HoHPitches.HonestNoEase), "Eased at the ceremony.");
            var decided = UseTheVeto(engine, other, board[1]);
            Assert.That(decided.nominees, Is.EquivalentTo(new[] { board[0], board[1] }), "The veto saved the other nominee; the replacement is the second pitcher.");
            Assert.That(Grudges.Severity(decided, board[0], decided.playerId), Is.EqualTo(70 - HoHPitches.HonestNoEase),
                "Still on the block after the veto, eased once and only once.");
            Assert.That(Grudges.Severity(decided, board[1], decided.playerId), Is.EqualTo(70 - HoHPitches.HonestNoEase), "The replacement's own ease.");
        }

        /// <summary>
        /// The honest no is this week's: a pitcher the player turned down in an earlier week of the board, heard this week and
        /// nominated, takes the ceremony's grudge whole.
        /// </summary>
        [Test]
        public void ANoFromAnEarlierWeekEasesNothing()
        {
            var crowned = Crowned(2);
            var board = Board(crowned);
            crowned.ledger.replies.Add(new ReplyRow { week = 1, cardId = "reply-pitch-1-" + board[0], kind = ReplyCards.Pitch, fromId = board[0],
                replyKey = HoHPitches.TurnDownKey, toThem = RoleTone.Stakes(RoleTone.Pragmatic).decline });
            Valid(crowned);
            var engine = Dealt(crowned, out board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = Apply(engine, Answer(engine.Snapshot, CardOf(engine.Snapshot, board[0]).id, HoHPitches.HearKey));
            Assert.That(HoHPitches.TurnedDownThisWeek(s, board[0]), Is.False, "Last week's no is not this week's.");
            Assert.That(Grudges.Severity(s, board[0], s.playerId), Is.EqualTo(0), "No grudge before the nominations.");
            string other = s.Active.First(c => !c.isPlayer && !board.Contains(c.id)).id;
            var nominated = Apply(engine, Command(s, EpisodeCommandKind.Nominate, board[0], other));
            Assert.That(Grudges.Severity(nominated, board[0], nominated.playerId), Is.EqualTo(70), "Heard this week: the nomination's grudge, whole.");
        }

        /// <summary>
        /// An honest no softens the nomination it went with and never forgives an earlier wrong: a pitcher who already held a
        /// grudge against the player stacks the nomination's at half its weight, up to the ceiling, and eases by thirty or by
        /// what this nomination added, whichever is less - at the ceremony and as the replacement.
        /// </summary>
        [TestCase(50, false, 55)]
        [TestCase(90, false, 90)]
        [TestCase(90, true, 90)]
        public void AnHonestNoEasesOnlyWhatTheNominationAdded(double held, bool asReplacement, double after)
        {
            var crowned = Crowned();
            var board = Board(crowned);
            Grudges.Add(crowned, board[0], crowned.playerId, held, GrudgeCauses.Story);
            Valid(crowned);
            var engine = Dealt(crowned, out board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = Apply(engine, Answer(engine.Snapshot, CardOf(engine.Snapshot, board[0]).id, HoHPitches.TurnDownKey));
            Assert.That(Grudges.Severity(s, board[0], s.playerId), Is.EqualTo(held), "The grudge they came with.");
            var others = s.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            if (asReplacement)
            {
                s = Apply(engine, Command(s, EpisodeCommandKind.Nominate, others[0], others[1]));
                Assert.That(Grudges.Severity(s, board[0], s.playerId), Is.EqualTo(held), "Left off at the ceremony: untouched.");
                s = UseTheVeto(engine, others[0], board[0]);
            }
            else s = Apply(engine, Command(s, EpisodeCommandKind.Nominate, board[0], others[0]));
            Assert.That(s.nominees, Does.Contain(board[0]));
            Assert.That(Grudges.Severity(s, board[0], s.playerId), Is.EqualTo(after),
                held + (asReplacement ? " named as the replacement" : " nominated") + ": eased by no more than the nomination added.");
        }

        // ------------------------------------------------------------------ the board's words

        /// <summary>
        /// A pitch's card says only public facts and the player's own: its chip and ask, the tone as one courting the Head of
        /// Household, the situation, the read, the line keyed on the card - naming their own recommendation - "Recommends:",
        /// what the answers stake and what a no and silence cost in words, and once felt out what they said. Moving every hidden
        /// view leaves every word the same.
        /// </summary>
        [Test]
        public void APitchCardSaysPublicFactsInWords()
        {
            var engine = Dealt(Crowned(), out var board, RoleTone.Threatening, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            var cards = RoleOffers.PitchBoard(s);
            Assert.That(cards.Select(c => c.speakerId), Is.EqualTo(board), "The board's cards, in its order.");
            for (int i = 0; i < cards.Count; i++)
            {
                var words = cards[i];
                var pitch = CardOf(s, board[i]);
                string tone = i == 0 ? RoleTone.Threatening : RoleTone.Pragmatic;
                Assert.That((words.offerId, words.role, words.moment, words.chip, words.chipLine, words.tone, words.situation, words.read),
                    Is.EqualTo((pitch.id, Courting, RoleQuotes.Pitch, RoleOffers.PitchChip, RoleOffers.PitchLine, tone, "Never nominated", RoleTone.ReadLine(s, board[i]))));
                Assert.That(words.quote, Is.EqualTo(RoleQuotes.Line(s, RoleQuotes.Pitch, tone, pitch.id, pitch.aboutId)));
                Assert.That(words.recommends, Is.EqualTo(pitch.aboutId == null ? null : "Recommends: " + s.Find(pitch.aboutId).name));
                Assert.That(words.stakes, Is.EqualTo(RoleOffers.PitchStakes(tone)));
                Assert.That(words.answers, Is.EqualTo(RoleOffers.PitchAnswers(tone)));
                Assert.That((words.terms, words.assessment), Is.EqualTo(((string)null, (string)null)));
                Assert.That(RoleOffers.Heading(words), Is.EqualTo(words.name + ", pitching to you, nomination pitch"));
                Assert.That(RoleOffers.Description(words), Does.StartWith("Tone: " + tone + ". Never nominated. " + words.read + "."));
                if (words.recommends != null) Assert.That(RoleOffers.Description(words), Does.Contain(words.recommends + "."));
            }
            Assert.That(RoleOffers.PitchStakes(RoleTone.Threatening), Does.Contain("still stings"));
            Assert.That(RoleOffers.PitchStakes(RoleTone.Pragmatic), Does.Contain("stings less"));

            string text = string.Join("\n", cards.Select(c => RoleOffers.Spoken(HoHPitches.HearKey, c)));
            var hidden = s.Clone();
            foreach (var edge in hidden.relationships.Where(r => r.fromId != hidden.playerId)) edge.score = edge.toId == hidden.playerId ? -100 : 100;
            Assert.That(string.Join("\n", RoleOffers.PitchBoard(hidden).Select(c => RoleOffers.Spoken(HoHPitches.HearKey, c))), Is.EqualTo(text),
                "No word reads a hidden view.");

            var felt = Apply(engine, Answer(s, CardOf(s, board[1]).id, HoHPitches.FeelOutKey));
            var assessed = RoleOffers.PitchCard(felt, CardOf(felt, board[1]));
            Assert.That(assessed.assessment, Is.Not.Null.And.EqualTo(HoHPitches.Assessment(felt, CardOf(felt, board[1]))), "Felt out, the card says what they said.");
            Assert.That(RoleOffers.Description(assessed), Does.Contain(assessed.assessment));
        }

        /// <summary>
        /// The director's gate (<see cref="RoleOffers.PitchBoardShows"/>, read by <c>EpisodeDirector.PitchBoardShows</c>): the
        /// nomination's screen and the Diary Room's nomination draw the board in place of the one pitch at a time only under the
        /// role moments, with the player Head of Household before the names and a pitch waiting. The same pitches without the
        /// role moments, another Head of Household, an answered board or the names made draw none.
        /// </summary>
        [Test]
        public void TheBoardShowsOnlyUnderRolesWhileThePlayersPitchesWait()
        {
            var engine = Dealt(Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            Assert.That(RoleOffers.PitchBoardShows(s), Is.True, "Under the role moments the player's pitches are a board.");

            var old = s.Clone();
            old.story.rulesVersion = StoryRules.Threads;
            Assert.That(HoHPitches.Available(old) && ReplyCards.Pending(old)?.kind == ReplyCards.Pitch, Is.True, "The same pitches wait without the role moments,");
            Assert.That(RoleOffers.PitchBoardShows(old), Is.False, "but they come one at a time, as they always did.");

            var someoneElses = s.Clone();
            someoneElses.hohId = board[0];
            Assert.That(RoleOffers.PitchBoardShows(someoneElses), Is.False, "Nobody pitches to another Head of Household.");

            var answered = s;
            foreach (string id in board) answered = Apply(engine, Answer(answered, CardOf(answered, id).id, HoHPitches.HearKey));
            Assert.That(answered.replyCards, Is.Empty);
            Assert.That(HoHPitches.OnTheBoard(answered), Is.True, "Still the board's week,");
            Assert.That(RoleOffers.PitchBoardShows(answered), Is.False, "but with every pitch answered there is no board to draw.");

            var others = s.Active.Where(c => !c.isPlayer && !board.Contains(c.id)).Select(c => c.id).Take(2).ToArray();
            var named = Apply(new EpisodeEngine(s), Command(s, EpisodeCommandKind.Nominate, others[0], others[1]));
            Assert.That(RoleOffers.PitchBoardShows(named), Is.False, "Once the names are made the board is gone.");
            Assert.That(RoleOffers.PitchBoardShows(null), Is.False);
        }

        /// <summary>The stakes and answers lines of every tone a pitcher can wear: at most fourteen words, no number, silence costing nothing.</summary>
        [Test]
        public void EveryPitchLineIsShortAndNumberFree()
        {
            foreach (string tone in RoleTone.TonesFor(Courting))
                foreach (string line in new[] { RoleOffers.PitchStakes(tone), RoleOffers.PitchAnswers(tone) })
                {
                    Assert.That(RoleOffers.WordCount(line), Is.LessThanOrEqualTo(RoleOffers.MostStakesWords), tone + ": " + line);
                    Assert.That(line.Any(char.IsDigit), Is.False, tone + ": " + line);
                }
            Assert.That(RoleOffers.PitchAnswers(RoleTone.Calculated), Is.EqualTo("Turn them down: costs nothing. Leave it unanswered: costs nothing."));
            Assert.That(RoleOffers.PitchAnswers(RoleTone.Bitter), Is.EqualTo("Turn them down: costs some goodwill. Leave it unanswered: costs nothing."));
            Assert.That(RoleOffers.PitchAnswers(null), Is.Null);
        }

        // ------------------------------------------------------------------ validation

        /// <summary>
        /// Validation holds the board: under the role moments no more than three pitchers in a nomination - waiting and answered
        /// together, and in the record of any week - and an answer records what the board moved: nothing for a promise or for
        /// hearing them, a tone's honest no for a no. A season without the role moments keeps the old numbers and size.
        /// </summary>
        [Test]
        public void ValidationHoldsTheBoardsSizeAndNumbers()
        {
            var engine = Dealt(Crowned(), out var board, RoleTone.Pragmatic, RoleTone.Pragmatic, RoleTone.Pragmatic);
            var s = engine.Snapshot;
            Valid(s);
            string fourth = s.Active.First(c => !c.isPlayer && !board.Contains(c.id)).id;
            var four = s.Clone();
            four.replyCards.Add(new ReplyCardState { id = "reply-pitch-" + s.week + "-" + fourth, week = s.week, kind = ReplyCards.Pitch, fromId = fourth });
            Invalid(four, "Invalid reply card data.", "A fourth card on the board.");
            var oldFour = four.Clone();
            oldFour.story.rulesVersion = StoryRules.Threads;
            Valid(oldFour);

            var answered = Apply(engine, Answer(s, CardOf(s, board[0]).id, HoHPitches.HearKey));
            var fourWithAnswer = answered.Clone();
            fourWithAnswer.replyCards.Add(new ReplyCardState { id = "reply-pitch-" + s.week + "-" + fourth, week = s.week, kind = ReplyCards.Pitch, fromId = fourth });
            Invalid(fourWithAnswer, "Invalid reply card data.", "Three waiting and one answered are four pitchers.");

            var promised = Apply(new EpisodeEngine(answered), Answer(answered, CardOf(answered, board[1]).id, HoHPitches.PromiseKey));
            Valid(promised);
            var warm = promised.Clone();
            warm.ledger.replies.Single(r => r.fromId == board[1] && r.replyKey == HoHPitches.PromiseKey).toThem = 8;
            Invalid(warm, "Invalid reply record data.", "A promise moves nothing at the answer.");
            var refused = Apply(new EpisodeEngine(answered), Answer(answered, CardOf(answered, board[1]).id, HoHPitches.TurnDownKey));
            Valid(refused);
            foreach (double toThem in new[] { -5.0, -4.0, 1.0 })
            {
                var wrong = refused.Clone();
                wrong.ledger.replies.Single(r => r.fromId == board[1] && r.replyKey == HoHPitches.TurnDownKey).toThem = toThem;
                Invalid(wrong, "Invalid reply record data.", "A no of " + toThem + " is no tone's.");
            }
            foreach (string tone in RoleTone.TonesFor(Courting))
            {
                var right = refused.Clone();
                right.ledger.replies.Single(r => r.fromId == board[1] && r.replyKey == HoHPitches.TurnDownKey).toThem = RoleTone.Stakes(tone).decline;
                Valid(right);
            }
            var oldPromise = promised.Clone();
            oldPromise.story.rulesVersion = StoryRules.Threads;
            Invalid(oldPromise, "Invalid reply record data.", "Without the role moments a promise recorded +8, as it always did.");

            // The record of an earlier week of the role moments: three pitchers at most.
            var later = Crowned(2);
            var npcs = later.Active.Where(c => !c.isPlayer).Select(c => c.id).Take(4).ToList();
            for (int i = 0; i < 3; i++) later.ledger.replies.Add(new ReplyRow { week = 1, cardId = "reply-pitch-1-" + npcs[i], kind = ReplyCards.Pitch, fromId = npcs[i], replyKey = HoHPitches.HearKey });
            Valid(later);
            later.ledger.replies.Add(new ReplyRow { week = 1, cardId = "reply-pitch-1-" + npcs[3], kind = ReplyCards.Pitch, fromId = npcs[3], replyKey = HoHPitches.HearKey });
            Invalid(later, "Invalid reply record data.", "Four pitchers in one week of the board.");
            later.story.rulesVersion = StoryRules.Threads;
            Valid(later);
        }

        // ------------------------------------------------------------------ fixtures

        private static readonly Dictionary<(int week, int mode, bool roles), EpisodeState> Walks = new Dictionary<(int, int, bool), EpisodeState>();

        /// <summary>
        /// A season at story rules Roles (or Threads) in commitment mode <paramref name="mode"/>, played through the public engine
        /// to week <paramref name="week"/>'s Head of Household competition with the player in the game, free to win it and in no
        /// pact; then crowned there, as <see cref="HoHPitchTests"/>' fixture crowns one. Not yet advanced: the advance deals the
        /// pitches.
        /// </summary>
        internal static EpisodeState Crowned(int week = 1, int mode = 2, bool roles = true)
        {
            if (!Walks.TryGetValue((week, mode, roles), out var held))
            {
                for (uint seed = 7401; seed < 7461 && held == null; seed++)
                {
                    var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                    ShippedRules.ApplyFresh(start);
                    start.story.rulesVersion = roles ? StoryRules.Roles : StoryRules.Threads;
                    start.unifiedCommitmentRulesVersion = mode == 2 ? UnifiedVoteFamilyValidation.Version : mode;
                    start.unifiedHearingRulesVersion = mode == 0 ? 0 : UnifiedCommitmentHearings.ProspectiveVersion;
                    var walk = new EpisodeEngine(start);
                    for (int i = 0; i < 400 && !AtTheHoH(walk.Snapshot, week); i++)
                    {
                        var step = walk.Apply(EpisodeEngineTests.NextCommand(walk.Snapshot));
                        Assert.That(step.accepted, Is.True, "seed " + seed + ": " + step.reason);
                        if (walk.Snapshot.week > week) break;
                    }
                    var s = walk.Snapshot;
                    if (!AtTheHoH(s, week) || s.previousHohId == s.playerId || s.Find(s.playerId).status != ContestantStatus.Active
                        || s.alliances.Any(a => a.members.Contains(s.playerId))
                        || !EpisodeEngine.CompetitionPlayers(s).Any(c => c.isPlayer)) continue;
                    held = s;
                }
                Assert.That(held, Is.Not.Null, "No seed reached week " + week + "'s Head of Household competition with the player free to win it.");
                Walks[(week, mode, roles)] = held;
            }
            var crowned = held.Clone();
            crowned.hohId = crowned.playerId;
            crowned.competitionResolved = true;
            crowned.competitionScores = EpisodeEngine.CompetitionPlayers(crowned)
                .Select(c => new CompetitionScore { contestantId = c.id, score = c.id == crowned.playerId ? 10 : 1 }).ToList();
            Valid(crowned);
            return crowned;
        }

        private static bool AtTheHoH(EpisodeState s, int week) => s.week == week && s.phase == EpisodePhase.HoH && !s.competitionResolved;

        /// <summary>Who courts the player once the nominations open: every houseguest in the game with no pact or safety deal with them.</summary>
        internal static List<string> Courters(EpisodeState crowned)
        {
            var nomination = crowned.Clone();
            nomination.phase = EpisodePhase.Nomination;
            return nomination.Active.Where(c => !c.isPlayer && NpcAgendas.Of(nomination, c.id)?.kind == Agendas.Court).Select(c => c.id).ToList();
        }

        /// <summary>
        /// The three courting houseguests the fixture puts on the board: the first three, by id, with nothing on the record the
        /// ladder reads - never on the block before, never wronged by the player.
        /// </summary>
        internal static string[] Board(EpisodeState crowned) =>
            Courters(crowned).Where(id => Clean(crowned, id)).OrderBy(id => id, StringComparer.Ordinal).Take(HoHPitches.BoardSize).ToArray();

        private static bool Clean(EpisodeState s, string id)
        {
            var who = s.Find(id);
            return !RoleTone.OnTheBlockBefore(s, who) && !RoleTone.YouNominatedThemBefore(s, who) && !RoleTone.YouBrokeYourWordBefore(s, id);
        }

        /// <summary>
        /// A copy of the crowned season with the board's three shaped: each made to wear its tone as one courting the player,
        /// the first read warmest, the second next and the third coolest, and every other houseguest read between them - far
        /// enough apart that the courts, which move the player's reading by a few points, never reorder them.
        /// </summary>
        internal static EpisodeState Shaped(EpisodeState crowned, string[] board, params string[] tones)
        {
            var s = crowned.Clone();
            foreach (string id in Courters(s).Where(id => !board.Contains(id))) Set(s, s.playerId, id, -15);
            var readings = new[] { 5.0, -5.0, -35.0 };
            for (int i = 0; i < board.Length; i++) Shape(s, board[i], tones[i], readings[i]);
            return s;
        }

        /// <summary><see cref="Shaped"/>, advanced into the nominations by the engine, which deals the board; each card wears its tone there.</summary>
        internal static EpisodeEngine Dealt(EpisodeState crowned, out string[] board, params string[] tones)
        {
            board = Board(crowned);
            Assert.That(board, Has.Length.EqualTo(HoHPitches.BoardSize));
            var s = Shaped(crowned, board, tones);
            var engine = new EpisodeEngine(s);
            var after = Apply(engine, Command(s, EpisodeCommandKind.Advance));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(HoHPitches.OnTheBoard(after), Is.True);
            Assert.That(after.replyCards.Select(c => c.fromId), Is.EqualTo(board), "The shaped three are the board, in its order.");
            for (int i = 0; i < board.Length; i++)
                Assert.That(RoleTone.Of(after, board[i], Courting), Is.EqualTo(tones[i]), board[i] + "'s card wears its tone.");
            return engine;
        }

        /// <summary>
        /// Makes a houseguest wear a tone as one courting the player (§3.0): a word the player broke last week (Bitter, from week
        /// two); Bold and Vengeful traits (Threatening); a week on the block before (Desperate, from week two); the player's own
        /// reading of them as close (Friendly); Warm (Earnest); Steady (Calculated); nothing at all (Pragmatic) - read by the
        /// player at <paramref name="reading"/> but for Friendly.
        /// </summary>
        private static void Shape(EpisodeState s, string id, string tone, double reading)
        {
            var who = s.Find(id);
            who.traits = new List<string>();
            Set(s, s.playerId, id, reading);
            switch (tone)
            {
                case RoleTone.Bitter:
                    s.deals.Add(new DealState
                    {
                        id = "deal-word-" + id, type = DealKind.InformationSharing, proposerId = s.playerId, recipientId = id,
                        status = DealStatus.Broken, week = 1, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
                        brokenById = s.playerId, settledWeek = 1,
                    });
                    break;
                case RoleTone.Threatening: who.traits = new List<string> { "Competitive", "Stubborn" }; break;
                case RoleTone.Desperate:
                    if (!who.nominationWeeks.Contains(1)) { who.nominationWeeks.Insert(0, 1); who.timesNominated++; }
                    break;
                case RoleTone.Friendly: Set(s, s.playerId, id, 30); break;
                case RoleTone.Earnest: who.traits = new List<string> { "Emotional" }; break;
                case RoleTone.Calculated: who.traits = new List<string> { "Analytical" }; break;
            }
        }

        /// <summary>
        /// From the nominations, on to the veto meeting with the player holding the veto as well, as R1's fixture hands it over;
        /// then the veto used on <paramref name="saved"/>, <paramref name="replacement"/> named in their place.
        /// </summary>
        private static EpisodeState UseTheVeto(EpisodeEngine engine, string saved, string replacement)
        {
            for (int i = 0; i < 50 && !(engine.Snapshot.phase == EpisodePhase.Veto && !engine.Snapshot.competitionResolved); i++)
                Apply(engine, EpisodeEngineTests.NextCommand(engine.Snapshot));
            var s = engine.Snapshot.Clone();
            Assert.That(s.phase, Is.EqualTo(EpisodePhase.Veto));
            s.vetoHolderId = s.playerId;
            if (!s.vetoPlayers.Contains(s.playerId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.playerId;
            s.competitionResolved = true;
            s.competitionScores = s.vetoPlayers.Select((id, i) => new CompetitionScore { contestantId = id, score = id == s.playerId ? 100 : i }).ToList();
            Valid(s);
            var meeting = new EpisodeEngine(s);
            var at = Apply(meeting, Command(s, EpisodeCommandKind.Advance));
            Assert.That(at.phase, Is.EqualTo(EpisodePhase.VetoMeeting));
            var decide = Command(at, EpisodeCommandKind.ResolveVeto, saved, replacement);
            decide.useVeto = true;
            return Apply(meeting, decide);
        }

        private static ReplyCardState CardOf(EpisodeState s, string fromId) => s.replyCards.Single(c => c.kind == ReplyCards.Pitch && c.fromId == fromId);

        private static ReplyRow Row(EpisodeState s, string fromId) =>
            s.ledger.replies.Single(r => r.week == s.week && r.kind == ReplyCards.Pitch && r.fromId == fromId && r.replyKey != HoHPitches.FeelOutKey);

        /// <summary>This week's ledger records of a type on the player's side of their pair with a houseguest.</summary>
        private static List<RelationshipEventState> Events(EpisodeState s, string id, string type) =>
            s.relationships.Where(r => r.fromId == s.playerId && r.toId == id).SelectMany(r => r.events).Where(e => e.type == type && e.week == s.week).ToList();

        /// <summary>A change the player makes, as the player's social stat scales it.</summary>
        private static double Adjusted(EpisodeState s, double delta) => WebRules.RelationshipDelta(delta, s.Find(s.playerId).stats.social, true);

        private static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);

        private static void Invalid(EpisodeState s, string error, string why)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.False, why);
            Assert.That(reason, Is.EqualTo(error), why);
        }

        internal static EpisodeState Apply(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + " " + command.targetId + " " + command.text + ": " + result.reason);
            return result.state;
        }

        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "role-pitch-" + kind + "-" + s.revision + "-" + target + "-" + text, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>The player's free answer to a pitch: one of its answers' keys, or feeling them out.</summary>
        internal static EpisodeCommand Answer(EpisodeState s, string cardId, string key) =>
            Command(s, EpisodeCommandKind.ReplyToHouseguest, cardId, text: key);
    }
}
