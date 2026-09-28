using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The read's side of the engine (STRATEGY-LOOP-PLAN.md §2): asking a voter straight, reading a
    /// person, what an overheard conversation and an account teach, the ledger rows they write, and
    /// the reveal judging every claim.
    ///
    /// <para>Asking and reading are free, like answering an offer: a question is not an action, and
    /// the read is the play. Each is once per person per week.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The odds a read lands: a base, more for an Intuitive or Analytical player, more again for somebody you are close to.</summary>
        public const double ReadBaseChance = 0.35, ReadTraitBonus = 0.35, ReadWarmthBonus = 0.15;
        /// <summary>Of the reads that miss, the share the person notices.</summary>
        public const double ReadNoticeShare = 0.3;
        /// <summary>What a lie about the vote costs the liar with the player, at the reveal.</summary>
        public const double VoteLieCost = -10;

        /// <summary>
        /// Switches the read rules on for a season from <paramref name="fromWeek"/>. The director
        /// calls this for every season it starts; the save migration and the importer do the same
        /// from the week after a save's, so the week it was in plays under the rules it was played under.
        /// </summary>
        public static void EnableRead(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.readRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>
        /// Whether the read rules run this week. Under them every alliance is a private fact at
        /// birth, whoever formed it, so a voter weighs only the alliances it knows of and the
        /// player learns them through the read. Before them, an alliance without a fact is known
        /// to everyone, as it always was.
        /// </summary>
        public static bool ReadRulesOn(EpisodeState s) => s != null && s.readRulesStartWeek >= 1 && s.week >= s.readRulesStartWeek;

        /// <summary>An alliance just formed becomes a private fact its members know, where the read rules run.</summary>
        public static void AllianceFormedUnderRead(EpisodeState s, AllianceState alliance)
        {
            if (ReadRulesOn(s)) Knowledge.AllianceFormed(s, alliance);
        }

        /// <summary>
        /// A voter's ballot as the state stands now: the same evaluator and, under the bloc rules,
        /// the same seeded round the vote itself will run, so a projection equals the ballot for as
        /// long as nothing changes. Never persisted; the ballot is cast at the vote.
        /// </summary>
        public static WebVoteEvaluation ProjectBallot(EpisodeState s, string voterId) =>
            UsesBlocVoting(s) ? WebNativeEvictionRound.Evaluate(s, new[] { voterId }).evaluations.Single()
                : WebEvictionVoting.EvaluateNative(s, voterId);

        /// <summary>
        /// "Where's your head at on the vote?" They answer with their leaning, or a lie, or nothing.
        /// The claim is the record; no memory is written, because a memory naming "you" counts for
        /// the player in every ballot they are on the block for (the web's memory term matches names).
        /// </summary>
        private static void AskVote(EpisodeState s, EpisodeCommand c)
        {
            Require(VoteRead.Available(s), "There is no vote to ask about yet.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var target = s.Find(c.targetId);
            Require(target != null && target.status == ContestantStatus.Active && !target.isPlayer && Voters(s).Any(v => v.id == target.id),
                "Ask somebody who votes this week.");
            Require(!AskedThisWeek(s, target.id), "You already asked " + target.name + " this week.");
            double view = s.Score(target.id, s.playerId);
            // A strategist who is not close to you keeps it to themselves. No roll: nothing to keep.
            if (target.traits.Contains("Strategic") && !target.traits.Contains("Loyal") && view < 25)
            {
                // The week's ask all the same: on the record as one, or a free question could be put again and again.
                AddStanding(s, target.id, s.playerId, ClaimSource.Deflected, 0);
                Log(s, "vote-read", target.name + " wouldn't say where their vote is. \"Ask me after.\"", s.playerId, target.id);
                return;
            }
            var truth = ProjectBallot(s, target.id);
            double honesty = target.traits.Contains("Loyal") ? 1 : target.traits.Contains("Sneaky") ? 0.25
                : Math.Max(0.2, Math.Min(0.95, 0.5 + view / 100));
            bool honest = Roll(s) < honesty;
            string stated = honest ? truth.selectedNomineeId : s.nominees.First(id => id != truth.selectedNomineeId);
            SeasonLedger.Append(s.ledger, s.ledger.claims, new ClaimRow { week = s.week, voterId = target.id, targetId = stated, source = ClaimSource.Told });
            Log(s, "vote-read", "You asked " + target.name + " where their head is at. \"" + Name(s, stated) + ".\"", s.playerId, target.id);
        }

        /// <summary>Reading a person: on a hit, how they see you becomes known; on a miss, they may notice.</summary>
        private static void ReadPerson(EpisodeState s, EpisodeCommand c)
        {
            Require(s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign, "Read people during free time or the campaign.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var target = s.Find(c.targetId);
            Require(target != null && target.status == ContestantStatus.Active && !target.isPlayer, "Read an active housemate.");
            Require(!ReadThisWeek(s, target.id), "You already read " + target.name + " this week.");
            var player = s.Find(s.playerId);
            double chance = ReadBaseChance
                + (player.traits.Contains("Intuitive") || player.traits.Contains("Analytical") ? ReadTraitBonus : 0)
                + (s.Score(s.playerId, target.id) >= 25 ? ReadWarmthBonus : 0);
            double roll = Roll(s);
            if (roll < chance)
            {
                double view = s.Score(target.id, s.playerId);
                AddStanding(s, target.id, s.playerId, ClaimSource.Read, view);
                string band = view >= 25 ? "warm on you" : view <= -25 ? "cold on you" : "not sure about you";
                Log(s, "vote-read", "You read " + target.name + ": they're " + band + ".", s.playerId, target.id);
                return;
            }
            // A miss is still the week's attempt: on the record as one, so it cannot be tried again
            // until it lands, which a free action otherwise invites.
            AddStanding(s, target.id, s.playerId, ClaimSource.Missed, 0);
            // The top of the miss range is the miss they notice: one roll, either way.
            if (roll >= 1 - (1 - chance) * ReadNoticeShare)
            {
                WriteScore(s, target.id, s.playerId, -3);
                Log(s, "vote-read", target.name + " noticed you sizing them up, and didn't love it.", s.playerId, target.id);
            }
            else Log(s, "vote-read", "You couldn't get a read on " + target.name + ".", s.playerId, target.id);
        }

        /// <summary>Whether the player has already asked this voter where their head is at this week, answered or not.</summary>
        public static bool AskedThisWeek(EpisodeState s, string npcId) =>
            s.ledger.claims.Any(k => k.week == s.week && k.voterId == npcId && k.source == ClaimSource.Told)
            || s.ledger.standings.Any(r => r.week == s.week && r.fromId == npcId && r.toId == s.playerId && r.source == ClaimSource.Deflected);

        /// <summary>Whether the player has already read this houseguest this week, hit or miss.</summary>
        public static bool ReadThisWeek(EpisodeState s, string npcId) =>
            s.ledger.standings.Any(r => r.week == s.week && r.fromId == npcId && r.toId == s.playerId && (r.source == ClaimSource.Read || r.source == ClaimSource.Missed));

        /// <summary>Records a standing the player learned, for the read.</summary>
        private static void AddStanding(EpisodeState s, string fromId, string toId, string source, double score) =>
            SeasonLedger.Append(s.ledger, s.ledger.standings, new StandingRow { week = s.week, fromId = fromId, toId = toId, source = source, score = score });

        /// <summary>
        /// What an overheard conversation says about the vote, when both are voting this week: the
        /// first speaker's leaning, as an overheard claim. Empty when there is no vote to talk about.
        /// </summary>
        private static string OverheardVote(EpisodeState s, ContestantState first, ContestantState second)
        {
            if (!VoteRead.Available(s)) return "";
            var voters = Voters(s).Select(v => v.id).ToList();
            if (!voters.Contains(first.id) || !voters.Contains(second.id)) return "";
            var truth = ProjectBallot(s, first.id);
            SeasonLedger.Append(s.ledger, s.ledger.claims, new ClaimRow { week = s.week, voterId = first.id, targetId = truth.selectedNomineeId, source = ClaimSource.Overheard });
            return " They talked about the vote: " + first.name + " is voting out " + Name(s, truth.selectedNomineeId) + ".";
        }

        /// <summary>The player's ballot, with the read it was cast on: who the whip count said would go.</summary>
        private static void RecordPlayerBallot(EpisodeState s, string targetId)
        {
            var sheet = VoteRead.Read(s);
            SeasonLedger.Append(s.ledger, s.ledger.ballots, new BallotRow { week = s.week, voterId = s.playerId, targetId = targetId, readBefore = sheet.predictedEvicteeId });
        }

        /// <summary>
        /// The reveal judges every claim of the week: kept, or lied. A lie told to the player's face
        /// costs the liar with them, is remembered, and is in the log for the jury to read later.
        /// The player's ballot row learns whether its read was right.
        /// </summary>
        private static void SettleVoteRead(EpisodeState s, string evicted)
        {
            foreach (var claim in s.ledger.claims.Where(k => k.week == s.week && k.status == ClaimStatus.Open).ToArray())
            {
                var ballot = s.votes.FirstOrDefault(v => v.voterId == claim.voterId);
                if (ballot == null) continue;
                claim.status = ballot.targetId == claim.targetId ? ClaimStatus.Kept : ClaimStatus.Lied;
                if (claim.status != ClaimStatus.Lied || claim.source != ClaimSource.Told) continue;
                string liar = Name(s, claim.voterId);
                Change(s, s.playerId, claim.voterId, VoteLieCost, liar + " lied to you about the vote", "vote-lie");
                Remember(s, s.playerId, claim.voterId, liar + " told me in week " + s.week + " they would vote out " + Name(s, claim.targetId)
                    + ", and voted out " + Name(s, ballot.targetId) + ".", true);
                Log(s, "vote-lie", liar + " told you " + Name(s, claim.targetId) + " and voted " + Name(s, ballot.targetId) + ".", s.playerId, claim.voterId);
            }
            foreach (var row in s.ledger.ballots.Where(b => b.week == s.week && b.voterId == s.playerId))
                row.correct = row.readBefore != null && row.readBefore == evicted;
        }

        /// <summary>
        /// The reason a ballot gives at the reveal, with the terms the player knows allowed to speak:
        /// the web's public reasons, plus a grudge, a bond or an alliance the player has learned
        /// of. A bloc's plan is never spoken, known or not: coordination is the bloc's business, and
        /// a voter who followed it gives another reason. The evaluation itself is untouched; only
        /// the sentence chosen for the log changes, so a vote the player saw coming is explained by
        /// what they saw.
        /// </summary>
        public static string ExplainKnown(EpisodeState s, WebVoteEvaluation evaluation)
        {
            var selected = evaluation.nomineeEvaluations.FirstOrDefault(n => n.nomineeId == evaluation.selectedNomineeId);
            var saved = evaluation.nomineeEvaluations.FirstOrDefault(n => n.nomineeId == evaluation.savedNomineeId);
            if (selected == null || saved == null) return WebEvictionVoting.ExplainNative(s, evaluation);
            var codes = saved.factors
                .Where(f => f.code != "blocPressure"
                    && (f.visibility == "public" || f.visibility == "playerKnown" || VoteRead.FactorKnown(s, evaluation.voterId, saved.nomineeId, f)))
                .Select(f => new { f.code, contribution = f.value - (selected.factors.FirstOrDefault(x => x.code == f.code)?.value ?? 0) })
                .Where(f => f.contribution > 0.01).OrderByDescending(f => f.contribution).Select(f => f.code).Take(3).ToList();
            if (codes.SequenceEqual(evaluation.publicReasonCodes)) return WebEvictionVoting.ExplainNative(s, evaluation);
            var told = new WebVoteEvaluation
            {
                voterId = evaluation.voterId, nomineeEvaluations = evaluation.nomineeEvaluations,
                selectedNomineeId = evaluation.selectedNomineeId, savedNomineeId = evaluation.savedNomineeId,
                margin = evaluation.margin, confidence = evaluation.confidence,
                publicReasonCodes = codes, privateReasonCodes = evaluation.privateReasonCodes,
            };
            return WebEvictionVoting.ExplainNative(s, told);
        }
    }
}
