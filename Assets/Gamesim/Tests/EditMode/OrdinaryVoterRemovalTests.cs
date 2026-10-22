using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// An ordinary voter removed by production in the week of their last vote (D1 V1b, the vote plan's
    /// section 3). Production removes only in a post-eviction Social window, so a removal always comes
    /// after that week's reveal: the removed voter cast a ballot that week and keeps their seat in its
    /// frame. Under the commitment rules the player's record of that week says so
    /// (KnownBallots' reconstruction counts a same-week removal as present, as the completed-reveal
    /// check already did); without them it reads as it always did.
    ///
    /// <para>Each witness is a constructed season (<see cref="PinnedVoteSeason"/>): the player an
    /// ordinary voter in week W (2 or later, so a week-before removal is a real negative control), a
    /// real VoteEvict deal between the player and V when V is an NPC, the week's NPC ballots pinned
    /// after the real batch so V alone votes against the player's nominee (unknown to the player), the
    /// player's real ballot and the real reveal. The removal itself is production's own ladder: on a
    /// detached snapshot of the post-reveal Social, V's conduct row is put at two strikes with the last
    /// one the week before, and the public Production.Strike decides the third and sets the pending
    /// removal; the real Social Advance then expels V before the week turns. Nothing writes a claim,
    /// ballot record, removal row or status.</para>
    /// </summary>
    public sealed class OrdinaryVoterRemovalTests
    {
        [TestCase(0, false)] [TestCase(1, false)] [TestCase(0, true)] [TestCase(1, true)]
        public void ARemovedOrdinaryVoterKeepsTheirSeatInTheirLastVote(int mode, bool player)
        {
            var w = Witness(mode, player, true);
            var after = w.Expelled;
            var removal = after.story.removals.Single(row => row.contestantId == w.Voter);
            Assert.That(removal.week, Is.EqualTo(w.Week), "Expel writes the removal in the week of the vote, before the week turns.");
            Assert.That(after.week, Is.EqualTo(w.Week + 1));
            Assert.That(after.Find(w.Voter).status, Is.EqualTo(ContestantStatus.Expelled));
            var frame = w.Frames.Single(item => item.week == w.Week);
            Assert.That(frame.ballots.Any(ballot => ballot.voterId == w.Voter), Is.True, "V's ballot is in the genuine frame.");
            Assert.That(UnifiedVoteCompletedReveal.TryValidate(after, frame, out var error), Is.True, error);
            var earlier = after.Clone();
            earlier.story.removals.Single(row => row.contestantId == w.Voter).week = w.Week - 1;
            Assert.That(UnifiedVoteCompletedReveal.TryValidate(earlier, frame, out error), Is.False,
                "A removal the week before would have left V out of the frame.");

            // The leak comes first, so the NPC cases pin it on their own rather than behind the voter list.
            if (!player)
            {
                Assert.That(KnownBallots.Knows(after, w.Week, w.Voter), Is.False, "V's ballot stays unknown to the player.");
                var deal = after.deals.Single(row => row.id == w.DealId);
                Assert.That(deal.status, Is.EqualTo(DealStatus.Broken)); Assert.That(deal.brokenById, Is.EqualTo(w.Voter));
                Assert.That(KnownBallots.DealOutcomeKnown(after, deal), Is.EqualTo(KnownBallots.Knows(after, w.Week, w.Voter)),
                    "The removal does not tell the player how V's deal ended.");
                Assert.That(KnownBallots.DealOutcomeKnown(w.Pending, w.Pending.deals.Single(row => row.id == w.DealId)), Is.False,
                    "Nor did the live box before it.");
            }
            var sheet = KnownBallots.Read(after, w.Week);
            Assert.That(sheet.voters, Does.Contain(w.Voter), "The week's voters as the record says include the removed voter.");
            Assert.That(sheet.voters.Count, Is.EqualTo(sheet.tally.Sum()), "The voter list is exact again.");
        }

        [TestCase(false)] [TestCase(true)]
        public void TheProjectedModeTwoSeasonPassesTheWholeCoreAroundTheRemoval(bool player)
        {
            var w = Witness(1, player, true);
            foreach (var point in new[] { w.Pending, w.Expelled, w.Later })
            {
                // The archive as it stood at the point: every regular reveal its power rows record.
                var projected = PinnedVoteSeason.Project(point, w.Owners, w.Frames.Where(frame =>
                    point.ledger.power.Any(row => row.week == frame.week && row.tally.Count == 2)));
                Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(projected, out var error), Is.True,
                    "week " + point.week + " " + point.phase + ": " + error);
                // Flipped at vote family V6: public validation takes mode 2 to the same complete core (it refused it until V6).
                Assert.That(EpisodeValidation.TryValidate(projected, out error), Is.True, "A public season since V6. " + error);
                if (!player)
                {
                    var deal = CommitmentReferences.FindDeal(projected, w.DealId);
                    Assert.That(deal, Is.Not.Null); Assert.That(deal.status, Is.EqualTo(DealStatus.Broken));
                    Assert.That(KnownBallots.DealOutcomeKnown(projected, deal), Is.EqualTo(KnownBallots.Knows(projected, w.Week, w.Voter)),
                        "week " + point.week + " " + point.phase + ": the removal does not tell the player how V's deal ended.");
                    Assert.That(KnownBallots.Knows(projected, w.Week, w.Voter), Is.False);
                }
                Assert.That(KnownBallots.Read(projected, w.Week).voters, Does.Contain(w.Voter));
            }
        }

        [Test]
        public void WithoutTheCommitmentRulesTheRemovalWeekReadsAsTheRecordedSeasonsDid()
        {
            var w = Witness(0, false, false);
            var after = w.Expelled;
            Assert.That(EpisodeEngine.CommitmentRulesOn(after), Is.False);
            Assert.That(after.story.removals.Single(row => row.contestantId == w.Voter).week, Is.EqualTo(w.Week));
            // The recorded reader, kept for seasons without the rules: a removal counts only from the
            // week after its own, so the week's list is short by the removed voter.
            var sheet = KnownBallots.Read(after, w.Week);
            Assert.That(sheet.voters, Does.Not.Contain(w.Voter));
            Assert.That(sheet.voters.Count, Is.EqualTo(sheet.tally.Sum() - 1));
            Assert.That(KnownBallots.DealOutcomeKnown(after, after.deals.Single(row => row.id == w.DealId)), Is.True);
        }

        /// <summary>
        /// A season imported without the rules in the removal week, so its rules start the week after
        /// (the importer's start), keeps reading that week as it did once the rules come on: the gate is
        /// the vote week's own, like the deal week's in DealResolution.AcceptedOffer, so a ballot
        /// outcome the player was already shown is not taken back.
        /// </summary>
        [Test]
        public void ARemovalWeekBeforeTheRulesFirstWeekKeepsItsReadingOnceTheRulesComeOn()
        {
            var w = Witness(0, false, false);
            var recorded = w.Expelled;
            var imported = recorded.Clone();
            EpisodeEngine.EnableCommitments(imported, w.Week + 1);
            Assert.That(imported.commitmentRulesStartWeek, Is.EqualTo(w.Week + 1));
            Assert.That(EpisodeEngine.CommitmentRulesOn(imported), Is.True, "The rules are on in the week after the removal.");
            Assert.That(KnownBallots.Read(imported, w.Week).voters, Is.EqualTo(KnownBallots.Read(recorded, w.Week).voters));
            Assert.That(KnownBallots.Read(imported, w.Week).voters, Does.Not.Contain(w.Voter));
            Assert.That(KnownBallots.DealOutcomeKnown(imported, imported.deals.Single(row => row.id == w.DealId)), Is.True,
                "What the player was shown before the rules stays shown.");
        }

        /// <summary>
        /// Vote family V5e: a voter production removed is never a juror. The season walked on to the jury's questions: no question
        /// is theirs, the jury readers leave them out, and in the mode-2 projection every Vote row they were a party to reads as
        /// ended - none binds, none waits on the player.
        /// </summary>
        [Test]
        public void AnExpelledVoterAsksNoQuestionAndTheirVoteRowsReadAsEnded()
        {
            var w = Witness(1, false, true);
            var walk = new PinnedVoteSeason(w.Seed, w.Later.Clone());
            walk.Frames.AddRange(w.Frames.Select(frame => frame.Clone()));
            foreach (var pair in w.Owners) walk.Owners.Add(pair.Key, pair.Value.Clone());
            for (int step = 0; step < 1024; step++)
            {
                var s = walk.State;
                if (s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.Jury || s.phase == EpisodePhase.Finished) break;
                if (s.Find(s.playerId).status == ContestantStatus.Active && PinnedVoteSeason.OpenVote(s) && s.nominees.Contains(s.playerId))
                { walk.PlayPinnedVote(s.nominees.First(id => id != s.playerId)); continue; }
                walk.Step(ModeTwoReaderSweep.Next(s));
            }
            var jury = walk.State;
            Assert.That(jury.phase == EpisodePhase.JuryQuestioning || jury.phase == EpisodePhase.Jury, Is.True, "Fixture: the jury sits (" + jury.phase + ").");
            Assert.That(walk.Supported, Is.True, walk.FirstUnsupported);
            Assert.That(jury.Find(w.Voter).status, Is.EqualTo(ContestantStatus.Expelled));
            Assert.That(jury.juryExchanges, Is.Not.Empty, "Fixture: the jury asks its questions.");
            Assert.That(jury.juryExchanges.Select(e => e.questionerId), Does.Not.Contain(w.Voter), "The expelled voter asks nothing.");
            var projected = PinnedVoteSeason.Project(jury, walk.Owners, walk.Frames);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(projected, out var error), Is.True, error);
            Assert.That(FinalistRead.Jurors(projected).Select(c => c.id), Does.Not.Contain(w.Voter));
            Assert.That(JuryHouseRead.Read(projected).jurors.Select(j => j.id), Does.Not.Contain(w.Voter));
            var theirs = projected.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote
                && (row.makerId == w.Voter || row.beneficiaryId == w.Voter)).ToList();
            Assert.That(theirs.Select(row => row.id), Does.Contain(w.DealId), "Fixture: the deal they broke is a canonical Vote row.");
            Assert.That(theirs.Where(row => DealStatus.Binds(row.status) || row.status == DealStatus.Proposed).Select(row => row.id), Is.Empty,
                "Every Vote row of theirs has ended.");
            Assert.That(CommitmentReferences.Deals(projected).Where(d => d.proposerId == w.Voter || d.recipientId == w.Voter)
                .Where(d => DealStatus.Binds(d.status) || d.status == DealStatus.Proposed).Select(d => d.id), Is.Empty, "The views read them ended.");
            Assert.That(NpcDeals.Pending(projected).Where(d => d.proposerId == w.Voter), Is.Empty, "None waits on the player.");
        }

        private sealed class RemovalWitness
        {
            internal uint Seed;
            internal int Week;
            internal string Voter, DealId;
            internal EpisodeState Pending, Expelled, Later;
            internal List<UnifiedVoteRevealState> Frames;
            internal Dictionary<string, ProspectiveVoteOwner> Owners;
        }
        private static readonly Dictionary<string, RemovalWitness> Cache = new Dictionary<string, RemovalWitness>();
        private static readonly Dictionary<string, string> Failures = new Dictionary<string, string>();

        private static RemovalWitness Witness(int mode, bool player, bool rules)
        {
            string key = mode + ":" + player + ":" + rules;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            if (Failures.TryGetValue(key, out var failed)) { Assert.Fail(failed); return null; }
            var misses = new List<string>();
            for (uint seed = 1; seed <= 32; seed++)
            {
                var found = Build(mode, player, rules, seed, out string miss);
                if (found != null)
                {
                    TestContext.Out.WriteLine("Removal witness " + key + ": seed " + seed + ", week " + found.Week + ", voter " + found.Voter
                        + " (" + misses.Count + " earlier seeds missed).");
                    Cache.Add(key, found); return found;
                }
                misses.Add("seed=" + seed + ": " + miss);
            }
            string failure = "No constructed ordinary-voter removal " + key + " within seeds 1..32:\n" + string.Join("\n", misses);
            Failures.Add(key, failure); Assert.Fail(failure); return null;
        }

        private static RemovalWitness Build(int mode, bool player, bool rules, uint seed, out string miss)
        {
            var fresh = PinnedVoteSeason.Fresh(seed, mode);
            if (!rules) fresh.commitmentRulesStartWeek = 0;
            var walk = new PinnedVoteSeason(seed, fresh);
            string voter = null, deal = null; int week = 0;
            while (voter == null)
            {
                // The vote week: the player an ordinary voter beside at least two NPC voters, week 2 or later.
                int tried = week;
                if (!WalkTo(walk, s => s.week > tried && s.week >= 2 && s.pendingDiary == null && s.phase == EpisodePhase.Campaign
                        && PinnedVoteSeason.PlayerVotes(s) && PinnedVoteSeason.NpcVoters(s).Count() >= 2, out miss)) return null;
                var campaign = walk.State; week = campaign.week;
                if (player) { voter = campaign.playerId; break; }
                // The player's real VoteEvict deal about the first nominee, with the first ordinal NPC voter who takes it.
                foreach (string id in PinnedVoteSeason.NpcVoters(campaign).OrderBy(id => id, StringComparer.Ordinal))
                {
                    var s = walk.State;
                    if (EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s) < 1) break;
                    if (!PlayerDeals.CanPropose(s, id, DealKind.VoteEvict, s.nominees[0], out _)) continue;
                    var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.ProposeDeal);
                    command.id = "removal-deal-" + seed + "-" + s.revision + "-" + id;
                    command.targetId = id; command.secondTargetId = s.nominees[0]; command.text = DealKind.VoteEvict;
                    var result = walk.Apply(command);
                    string created = "deal-player-" + s.nextSequence;
                    if (result.accepted && result.state.deals.Any(row => row.id == created && row.status == DealStatus.Active))
                    { voter = id; deal = created; break; }
                }
            }

            // The vote: V alone against the player's nominee, everybody else with the player.
            if (!WalkTo(walk, PinnedVoteSeason.OpenVote, out miss)) return null;
            Assert.That(walk.State.week, Is.EqualTo(week));
            walk.RunNpcBatch();
            var box = walk.State;
            string x = box.nominees[0], y = box.nominees[1];
            var npc = PinnedVoteSeason.NpcVoters(box).OrderBy(id => id, StringComparer.Ordinal).ToList();
            string dissent = player ? npc[0] : voter;
            walk.PinCast(npc.ToDictionary(id => id, id => id == dissent ? y : x, StringComparer.Ordinal));
            walk.CastVote(x);
            var revealed = walk.Reveal();
            var power = revealed.ledger.power.Single(row => row.week == week);
            Assert.That(power.tally, Is.EqualTo(new List<int> { npc.Count, 1 }), "The designed count.");
            Assert.That(KnownBallots.Knows(revealed, week, dissent), Is.False, "The lone dissent is unknown to the player.");
            if (deal != null) Assert.That(revealed.deals.Single(row => row.id == deal).status, Is.EqualTo(DealStatus.Broken), "V broke the deal at the reveal.");

            // To the post-reveal Social, with no reflection pending.
            if (!WalkTo(walk, s => s.week == week && s.phase == EpisodePhase.Social && s.evictionResolved && s.pendingDiary == null, out miss)) return null;
            var social = walk.State;
            if (social.Find(voter).status != ContestantStatus.Active || !Production.RemovalWindow(social))
            { miss = "week " + week + ": no removal window for " + voter; return null; }

            // Production's own ladder decides the removal.
            var pending = walk.State;
            var conduct = Production.For(pending, voter, true);
            conduct.strikes = 2; conduct.lastStrikeWeek = week - 1;
            Assert.That(Production.Strike(pending, voter, "fixture"), Is.EqualTo(3), "The real threshold returns the removal rung.");
            Assert.That(pending.story.pendingRemovalId, Is.EqualTo(voter));
            walk = Rebuild(walk, pending);
            var expelled = walk.Step(EpisodeEngineTests.Command(walk.State, EpisodeCommandKind.Advance));
            Assert.That(expelled.story.pendingRemovalId, Is.Null);

            // A later week: the next week's completed reveal, or the first state past that week.
            if (!WalkTo(walk, s => s.week > week + 1 || s.week == week + 1 && s.phase == EpisodePhase.Eviction && s.evictionResolved,
                    out miss)) return null;
            if (!walk.Supported) { miss = "an actual birth this observer cannot order: " + walk.FirstUnsupported; return null; }
            miss = null;
            return new RemovalWitness { Seed = seed, Week = week, Voter = voter, DealId = deal, Pending = pending, Expelled = expelled,
                Later = walk.State, Frames = walk.Frames.Select(frame => frame.Clone()).ToList(),
                Owners = walk.Owners.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal) };
        }

        /// <summary>The same walk from a detached snapshot, rebuilt through public validation; its frames and owners carry over.</summary>
        private static PinnedVoteSeason Rebuild(PinnedVoteSeason walk, EpisodeState detached)
        {
            var next = new PinnedVoteSeason(walk.Seed, detached);
            next.Frames.AddRange(walk.Frames.Select(frame => frame.Clone()));
            foreach (var pair in walk.Owners) next.Owners.Add(pair.Key, pair.Value.Clone());
            next.Supported = walk.Supported; next.FirstUnsupported = walk.FirstUnsupported;
            next.Accepted = walk.Accepted;
            return next;
        }

        /// <summary>
        /// Plays the walk's next commands until <paramref name="until"/> holds: the player competes at no
        /// effort, and a vote with the player on the block is pinned against the other nominee.
        /// </summary>
        private static bool WalkTo(PinnedVoteSeason walk, Func<EpisodeState, bool> until, out string miss)
        {
            while (walk.Accepted < 512)
            {
                var s = walk.State;
                if (until(s)) { miss = null; return true; }
                if (s.phase == EpisodePhase.Finished || s.phase == EpisodePhase.Jury || s.Active.Count() <= 3)
                { miss = "not reached before the end game (week " + s.week + ", " + s.phase + ")"; return false; }
                if (s.Find(s.playerId).status == ContestantStatus.Active && PinnedVoteSeason.OpenVote(s) && s.nominees.Contains(s.playerId))
                { walk.PlayPinnedVote(s.nominees.First(id => id != s.playerId)); continue; }
                var command = EpisodeEngineTests.NextCommand(s);
                if (command.kind == EpisodeCommandKind.Compete) command.performance = 0;
                walk.Step(command);
            }
            miss = "not reached within 512 accepted commands";
            return false;
        }
    }
}
