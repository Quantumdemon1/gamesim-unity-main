using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5e: the history views and the finale and jury readers read mode 2 as they read mode 1 - the juror's receipts
    /// and their lines (<see cref="FinaleQuestions"/>), the argument's moments (<see cref="FinalArgument"/>), the case's résumé
    /// (<see cref="FinalCaseResume"/>), the finalist's facts (<see cref="FinalistRead"/>), the jury house (<see cref="JuryHouseRead"/>),
    /// the verdict (<see cref="GameSense"/>) and the week (<see cref="YourWeek"/>). A canonical Vote row is a receipt, a moment or a
    /// scored chance only as its Rule2 group's owner, rebuilt from the archive (<see cref="UnifiedVoteHistory.Incidents"/>,
    /// <see cref="UnifiedVoteHistory.Fulfillments"/>), and a breach term counts it once per incident (the lead's decision D1); its
    /// receipt keeps mode 1's legacy dating (<see cref="CommitmentReferences.ReceiptWeek"/>).
    /// </summary>
    public sealed class ModeTwoFinaleReaderTests
    {
        /// <summary>
        /// The finale and jury readers at one of a walk's moments (<see cref="ModeTwoReaderParityTests"/>): the week, and - where no
        /// Rule2 group holds two rows (<paramref name="overlap"/>, the designed D1 difference) - the verdict, every houseguest's
        /// receipts, each saved question's receipt line, week and kicker, the argument's moments and its speech, the finalists'
        /// facts, the case's résumé and the jury house. A reader that throws must throw alike.
        /// </summary>
        internal static void CheckFinale(EpisodeState mode1, EpisodeState mode2, string where, bool overlap)
        {
            void Same(string reader, Func<EpisodeState, object> read) =>
                Assert.That(Read(mode2, read), Is.EqualTo(Read(mode1, read)), where + ": " + reader + " reads mode 2 as mode 1.");
            // Designed (V5e, the knowledge gate): mode 2 does not say the player kept a vote deal whose keeping tells a ballot they
            // cannot know (YourWeek.KeepingTellsTheirBallot); mode 1 says it. Compared with mode 2's not-known line in its place, only
            // where the gate holds on the season's facts (KeepingTellsAHiddenBallot).
            var one = Weeks(mode1); var two = Weeks(mode2);
            for (int w = 0; w < Math.Min(one.Count, two.Count); w++)
                for (int i = 0; i < Math.Min(one[w].word.Count, two[w].word.Count); i++)
                    if (KeepingTellsAHiddenBallot(mode1, one[w].week, one[w].word[i], two[w].word[i])) one[w].word[i] = two[w].word[i];
            // Its lines always; its Game Sense, which scores a Rule2 group once (D1), where no group holds two rows.
            if (overlap) foreach (var week in one.Concat(two)) week.sense = null;
            Assert.That(Json(two), Is.EqualTo(Json(one)), where + ": YourWeek reads mode 2 as mode 1, the knowledge gate aside.");
            if (overlap) return;
            Same("GameSense", s => GameSense.Evaluate(s));
            var others = mode1.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();
            Same("FinaleQuestions.Receipts", s => others.Select(id => FinaleQuestions.Receipts(s, id)).ToList());
            Same("a question's receipt", s => s.juryExchanges.Select(e => new object[] { FinaleQuestions.ReceiptLine(s, e), FinaleQuestions.ReceiptWeek(s, e),
                FinaleQuestions.Kicker(s, e), JuryHouseRead.ReceiptWeek(s, e) }).ToList());
            Same("FinalArgument", s => new object[] { FinalArgument.Moments(s), FinalArgument.Speech(s),
                FinalistRead.Jurors(s).SelectMany(juror => s.Active.Select(finalist => FinalArgument.Term(s, juror.id, finalist.id))).ToList() });
            Same("FinalistRead", s => s.Active.Where(c => !c.isPlayer).Select(c => FinalistRead.Read(s, c.id)).ToList());
            Same("FinalistRead's facts", s => others.Select(id => new object[] { FinalistRead.TowardYou(s, id), FinalistRead.RelationshipLine(s, id) }).ToList());
            Same("FinalCaseResume", s => FinalCaseResume.Read(s));
            Same("JuryHouseRead", s => JuryHouseRead.Read(s));
        }

        /// <summary>
        /// Whether mode 2's not-known line in place of mode 1's "You kept the ..." is the knowledge gate's, read on the season's facts:
        /// the same deal's words with the same houseguest; a ballot the player does not know that week; and no vote deal of that
        /// kind between them, kept that week, whose ending the player can know (<see cref="KnownBallots.DealOutcomeKnown"/>).
        /// </summary>
        internal static bool KeepingTellsAHiddenBallot(EpisodeState s, int week, YourWeek.Line kept, YourWeek.Line notKnown)
        {
            if (kept.kind != YourWeek.Kinds.Deal || kept.verdict != YourWeek.Verdicts.Kept || kept.byId != s.playerId
                || notKnown.kind != YourWeek.Kinds.Deal || notKnown.verdict != YourWeek.Verdicts.NotKnown || notKnown.aboutId != kept.aboutId
                || KnownBallots.Knows(s, week, kept.aboutId)) return false;
            string partner = kept.aboutId, whom = " with " + s.Find(partner)?.name;
            // YourWeek's own words: "You kept the {deal} with {name}." and "The {deal} with {name} is unresolved: ...".
            var type = new[] { DealKind.VoteTogether, DealKind.VoteSave, DealKind.VoteEvict, DealKind.Partnership }.SingleOrDefault(kind =>
                kept.text == "You kept the " + YourWeek.DealWords(kind) + whom + "."
                && notKnown.text == "The " + YourWeek.DealWords(kind) + whom + " is " + KnownBallots.Unresolved + ".");
            if (type == null) return false;
            var keptThatWeek = CommitmentReferences.Deals(s).Where(d => d.type == type && d.status == DealStatus.Fulfilled && d.settledWeek == week
                && ((d.proposerId == s.playerId && d.recipientId == partner) || (d.proposerId == partner && d.recipientId == s.playerId))).ToList();
            return keptThatWeek.Count > 0 && !keptThatWeek.Any(d => KnownBallots.DealOutcomeKnown(s, d));
        }

        /// <summary>The week a moment stands in and the one before it, as the recap builds them.</summary>
        private static List<YourWeek.Week> Weeks(EpisodeState s) =>
            Enumerable.Range(Math.Max(1, s.week - 1), Math.Min(2, s.week)).Select(week => YourWeek.Build(s, week)).ToList();

        /// <summary>A reader's answer as JSON, or what it threw.</summary>
        private static string Read(EpisodeState s, Func<EpisodeState, object> read)
        {
            try { return Json(read(s)); }
            catch (Exception error) { return "throws " + error.GetType().Name + ": " + error.Message; }
        }

        // ------------------------------------------------------------ the flip pair (pattern P3)

        /// <summary>The player-facing readers V5e moved, each as the player sees it of the flip pair's partner.</summary>
        private static object Reader(string reader, EpisodeState s, string partner)
        {
            switch (reader)
            {
                case "FinalistRead":
                    return new object[] { FinalistRead.Read(s, partner), FinalistRead.TowardYou(s, partner), FinalistRead.RelationshipLine(s, partner) };
                case "FinalCaseResume": return FinalCaseResume.Read(s);
                case "JuryHouseRead": return JuryHouseRead.ReadJuror(s, partner);
                // The notes the week shows: the known ones (YourWeek's sense). The season's verdict counts a hidden ending too,
                // by design (GameSense: "the verdict counts it either way"), and is not shown while the season runs.
                case "GameSense": return GameSense.Evaluate(s).notes.Where(note => note.known).ToList();
                case "YourWeek": return YourWeek.Build(s, s.week);
                case "ReceiptLine":
                    return FinaleQuestions.Receipts(s, partner).Select(receipt => FinaleQuestions.ReceiptLine(s, new JuryExchangeState
                        { questionerId = partner, finalistId = s.playerId, category = receipt.category, receiptKind = receipt.kind, receiptId = receipt.id })).ToList();
                default: throw new ArgumentException(reader);
            }
        }

        [TestCase("FinalistRead")] [TestCase("FinalCaseResume")] [TestCase("JuryHouseRead")]
        [TestCase("GameSense")] [TestCase("YourWeek")] [TestCase("ReceiptLine")]
        public void AFinaleReaderShowsNothingOfAHiddenBallot(string reader)
        {
            var pair = ModeTwoReaderSweep.Flip(false);
            pair.AssertBlind(reader, s => Reader(reader, s, pair.PartnerId));
        }

        [TestCase("FinalistRead")] [TestCase("FinalCaseResume")] [TestCase("JuryHouseRead")]
        [TestCase("GameSense")] [TestCase("YourWeek")] [TestCase("ReceiptLine")]
        public void AFinaleReaderShowsWhatThePlayerWasToldAndTheRevealJudged(string reader)
        {
            var pair = ModeTwoReaderSweep.Flip(true);
            pair.AssertControl(reader, s => Reader(reader, s, pair.PartnerId));
        }

        /// <summary>
        /// The designed difference the week's flip pair found (V5e, the knowledge gate): a vote deal is kept only where the partner's
        /// ballot kept it too, so mode 1's "You kept the vote-to-evict deal" tells a ballot the player cannot know. Mode 2 says the
        /// deal is not known yet, as it says of one the partner settled; mode 1 is unchanged.
        /// </summary>
        [Test]
        public void TheWeekDoesNotSayThePlayerKeptADealWhoseKeepingTellsAHiddenBallot()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string partner = voters[0], other = voters[1], x = s.nominees[0], y = s.nominees[1];
            s = ModeTwoReaderSweep.Strike(s, partner, DealKind.VoteEvict, x);
            // The partner keeps it; one other voter votes the other way, so the count does not tell whose ballot that was.
            var reveal = ModeTwoReaderSweep.Reveal(s, x, voters.ToDictionary(id => id, id => id == other ? y : x, StringComparer.Ordinal));
            int week = reveal.Mode2.week;
            Assert.That(KnownBallots.Knows(reveal.Mode2, week, partner), Is.False, "Fixture: the partner's ballot is hidden.");
            var one = YourWeek.Build(reveal.Mode1, week).word.Single(line => line.kind == YourWeek.Kinds.Deal && line.aboutId == partner);
            var two = YourWeek.Build(reveal.Mode2, week).word.Single(line => line.kind == YourWeek.Kinds.Deal && line.aboutId == partner);
            Assert.That((one.verdict, one.byId), Is.EqualTo((YourWeek.Verdicts.Kept, reveal.Mode1.playerId)), "Mode 1 says the player kept it.");
            Assert.That(two.verdict, Is.EqualTo(YourWeek.Verdicts.NotKnown), "Mode 2 says it is not known yet.");
            Assert.That(two.byId, Is.Null);
            Assert.That(Json(YourWeek.Build(reveal.Projection, week)), Is.EqualTo(Json(YourWeek.Build(reveal.Mode2, week))),
                "The projection of mode 1's season reads as mode 2's.");
            // The walks' comparison (CheckFinale) puts mode 2's line in mode 1's place only where the gate holds on the season's facts.
            Assert.That(KeepingTellsAHiddenBallot(reveal.Mode1, week, one, two), Is.True, "The gate holds here.");
            Assert.That(KeepingTellsAHiddenBallot(reveal.Mode1, week + 1, one, two), Is.False, "Not in a week the deal was not kept in.");
            var otherDeal = new YourWeek.Line { kind = two.kind, verdict = two.verdict, aboutId = partner,
                text = two.text.Replace(YourWeek.DealWords(DealKind.VoteEvict), YourWeek.DealWords(DealKind.VoteTogether)) };
            Assert.That(KeepingTellsAHiddenBallot(reveal.Mode1, week, one, otherDeal), Is.False, "Not for another deal's line.");
            var aboutOther = new YourWeek.Line { kind = two.kind, verdict = two.verdict, aboutId = other, text = two.text };
            Assert.That(KeepingTellsAHiddenBallot(reveal.Mode1, week, one, aboutOther), Is.False, "Not for another houseguest's line.");
        }

        /// <summary>
        /// The designed D1 difference in the finale: the partner breaks a vote-to-keep and a vote-to-evict with one ballot, which
        /// the player can tell from the count. Mode 1's finalist facts say two deals broken, and its verdict scores two; mode 2's
        /// say one breach, and score it once - the incident's owner (<see cref="UnifiedVoteHistory.Incidents"/>).
        /// </summary>
        [Test]
        public void OneBallotBreakingTwoDealsIsOneBreachOnTheFinalistsRecord()
        {
            var s = ModeTwoReaderSweep.Campaign();
            var voters = PinnedVoteSeason.NpcVoters(s).ToList();
            string partner = voters[0], x = s.nominees[0], y = s.nominees[1];
            s = ModeTwoReaderSweep.Strike(ModeTwoReaderSweep.Strike(s, partner, DealKind.VoteSave, x), partner, DealKind.VoteEvict, y);
            // Every houseguest votes x out, the partner too: the count says so, and both deals are the partner's breach.
            var reveal = ModeTwoReaderSweep.Reveal(s, y, voters.ToDictionary(id => id, id => x, StringComparer.Ordinal));
            int week = reveal.Mode2.week;
            Assert.That(KnownBallots.Knows(reveal.Mode2, week, partner), Is.True, "Fixture: the count tells the partner's ballot.");
            var incident = UnifiedVoteHistory.Incidents(reveal.Mode2).Single();
            Assert.That((incident.ActorId, incident.WrongedId, incident.EvidenceIds.Count), Is.EqualTo((partner, reveal.Mode2.playerId, 2)));
            Assert.That(FinalistRead.TowardYou(reveal.Mode1, partner).value, Does.Contain("Broke 2 deals with you"));
            Assert.That(FinalistRead.TowardYou(reveal.Mode2, partner).value, Does.Contain("Broke a deal with you").And.Not.Contain("2 deals"));
            Assert.That(FinalistRead.TowardYou(reveal.Projection, partner).value, Is.EqualTo(FinalistRead.TowardYou(reveal.Mode2, partner).value));
            List<GameSense.Note> Broken(EpisodeState state) => GameSense.Evaluate(state).notes.Where(note => note.text.Contains("a deal broken against you")).ToList();
            Assert.That(Broken(reveal.Mode1).Count, Is.EqualTo(2), "Mode 1 notes each deal.");
            Assert.That(Broken(reveal.Mode2).Select(note => note.rowId), Is.EqualTo(new[] { incident.OwnerId }), "Mode 2 notes the incident once, its owner's.");
            // The case's résumé is a history, read row by row in both modes (D1's inventories): both deals, as mode 1 lists them.
            Assert.That(FinalCaseResume.Read(reveal.Mode2).brokenAgainstYou, Is.EqualTo(2));
            Assert.That(Json(FinalCaseResume.Read(reveal.Projection)), Is.EqualTo(Json(FinalCaseResume.Read(reveal.Mode1))));
        }

        private static string Json(object value) => ModeTwoReaderSweep.Json(value);
    }
}
