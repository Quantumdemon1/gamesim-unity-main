using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The verdict (STRATEGY-LOOP-PLAN.md §5): Game Sense, 0-100, with three faces. The game's own
    /// referee, deterministic and seeded by nothing: every point traces to a ledger row the player
    /// can open. Competitions are wins against the odds the player had; strategy is what was done
    /// with power, reads, deals and the chances the season offered, weighed against what was
    /// offered; social is how the house sees the player, not how the player sees the house.
    /// A quiet winner who took nothing scores below a loser who took and won most of their chances:
    /// that is the "not all wins are equal" the owner asked for.
    /// </summary>
    public static class GameSense
    {
        public const string Competitions = "competitions", Strategy = "strategy", Social = "social";
        /// <summary>The faces' weights in the number: strategy is the game, the other two are half of it between them.</summary>
        public const double CompetitionWeight = 0.3, StrategyWeight = 0.4, SocialWeight = 0.3;
        /// <summary>Where a face starts before the ledger says anything: the middle.</summary>
        public const double Base = 50;

        /// <summary>One scored item: a face, its points, the words, and the ledger row it came from.</summary>
        public sealed class Note
        {
            public string face, text, rowKind, rowId;
            public double points;
            public int week;

            /// <summary>
            /// Whether the player could already know everything the note rests on, mid-season: a
            /// row that is public (the power, the count) or the player's own (their ballot and read,
            /// their deals, pleas, plays and calls). False for a note that rests on what the house
            /// keeps to itself: the odds a competition was won against (every competitor's strength),
            /// an NPC's backdoor plan, how the house sees the player, why a pact ended, a grudge, a
            /// bond, a houseguest's memory. False until a note's writer says otherwise, so a note
            /// added later stays out of the weekly recap until somebody has decided it can be known.
            /// The verdict at the end counts every note either way; the weekly recap shows only the
            /// known ones (<see cref="YourWeek"/>).
            /// </summary>
            public bool known;
        }

        public sealed class Report
        {
            public int score, competitions, strategy, social;
            public List<Note> notes = new List<Note>();
            /// <summary>The three moments that made the difference, and the three chances missed, each a row in words.</summary>
            public List<Note> moments = new List<Note>(), missed = new List<Note>();
        }

        public static Report Evaluate(EpisodeState s)
        {
            var report = new Report();
            if (s?.ledger == null) return report;
            var notes = report.notes;
            ScoreCompetitions(s, notes);
            ScoreStrategy(s, notes);
            ScoreSocial(s, notes);
            report.competitions = Face(notes, Competitions);
            report.strategy = Face(notes, Strategy);
            report.social = Face(notes, Social);
            report.score = (int)Math.Round(report.competitions * CompetitionWeight + report.strategy * StrategyWeight + report.social * SocialWeight);
            report.moments = notes.Where(n => n.points > 0 && n.rowKind != "standing").OrderByDescending(n => n.points).ThenBy(n => n.week).Take(3).ToList();
            report.missed = notes.Where(n => n.points < 0).OrderBy(n => n.points).ThenBy(n => n.week).Take(3).ToList();
            return report;
        }

        /// <summary>A face from these notes: the middle, moved by every note of that face, held to 0-100.</summary>
        public static int Face(IEnumerable<Note> notes, string face) =>
            (int)Math.Round(Math.Max(0, Math.Min(100, Base + notes.Where(n => n.face == face).Sum(n => n.points))));

        private static void Add(List<Note> notes, string face, double points, string text, string rowKind, string rowId, int week, bool known = false) =>
            notes.Add(new Note { face = face, points = Math.Round(points, 1), text = text, rowKind = rowKind, rowId = rowId, week = week, known = known });

        // ---------------------------------------------------------------- competitions

        /// <summary>Wins against the odds, clutch wins, a throw that served a purpose; losing when nominated with a full effort, a throw that put you up.</summary>
        private static void ScoreCompetitions(EpisodeState s, List<Note> notes)
        {
            foreach (var row in s.ledger.competitions)
            {
                var power = s.ledger.power.FirstOrDefault(p => p.week == row.week);
                bool onTheBlock = power != null && (power.savedId == s.playerId || power.nominees.Contains(s.playerId));
                bool final = row.kind.StartsWith("FinalHoH", StringComparison.Ordinal);
                string what = final ? "the final HoH" : row.kind == "Veto" ? "the veto" : "the HoH";
                string odds = "one in " + Math.Max(1, Math.Round(1 / Math.Max(0.02, row.expectedWin))).ToString("0");
                // The odds are every competitor's strength, which nobody in the house is shown: a
                // note weighed by them is not known until the season's end.
                switch (row.entry)
                {
                    case CompetitionEntry.Played:
                    case CompetitionEntry.Assist:
                        if (row.placement == 1)
                        {
                            double points = (1 - row.expectedWin) * 30 + (onTheBlock && row.kind == "Veto" ? 10 : 0) + (final ? 10 : 0);
                            Add(notes, Competitions, points, "Week " + row.week + ": you won " + what + " against " + odds + " odds"
                                + (onTheBlock && row.kind == "Veto" ? ", from the block" : "") + ".", "competition", row.week + ":" + row.kind, row.week, known: false);
                        }
                        else if (onTheBlock && row.kind == "Veto" && row.performance >= 0.75)
                            Add(notes, Competitions, -8, "Week " + row.week + ": you gave the veto everything from the block and finished " + Ordinal(row.placement) + " of " + row.field + ".",
                                "competition", row.week + ":" + row.kind, row.week, known: true);
                        else
                            Add(notes, Competitions, -row.expectedWin * 6, "Week " + row.week + ": " + Ordinal(row.placement) + " of " + row.field + " in " + what + ".",
                                "competition", row.week + ":" + row.kind, row.week, known: false);
                        break;
                    case CompetitionEntry.Thrown:
                        bool nominatedAfter = power != null && (power.nominees.Contains(s.playerId) || power.replacementId == s.playerId);
                        Add(notes, Competitions, nominatedAfter ? -12 : 8, "Week " + row.week + ": you threw " + what
                            + (nominatedAfter ? ", and went up that week." : ", and stayed off the block."), "competition", row.week + ":" + row.kind, row.week, known: true);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- strategy

        /// <summary>
        /// Power held and used, nominations avoided, backdoors made or dodged, the read's accuracy,
        /// deals and pleas and plays taken and won, votes with the majority, and the chances let pass.
        /// </summary>
        private static void ScoreStrategy(EpisodeState s, List<Note> notes)
        {
            var you = s.Find(s.playerId);
            var scoredSafety = ScoredSafetyOutcomes(s);
            foreach (var power in s.ledger.power.Where(p => p.evicteeId != null))
            {
                string id = "power:" + power.week;
                bool nominated = power.nominees.Contains(s.playerId) || power.savedId == s.playerId;
                // The week's power is public, and the player's own plan is theirs.
                if (power.hohId == s.playerId)
                {
                    Add(notes, Strategy, 4, "Week " + power.week + ": you held the Head of Household.", "power", id, power.week, known: true);
                    if (power.backdoorTargetId != null)
                        Add(notes, Strategy, power.backdoorResult == "made" ? 10 : -4, "Week " + power.week + ": your backdoor of " + Name(s, power.backdoorTargetId)
                            + (power.backdoorResult == "made" ? " was made." : power.backdoorResult == "survived" ? " went up and survived." : " never went up."), "power", id, power.week, known: true);
                }
                else if (power.evicteeId != s.playerId)
                {
                    if (!nominated) Add(notes, Strategy, 3 + (you != null && you.hohWins + you.vetoWins > 0 ? 2 : 0), "Week " + power.week + ": you stayed off the block.", "power", id, power.week, known: true);
                    else if (power.savedId == s.playerId) Add(notes, Strategy, 6, "Week " + power.week + ": you were on the block, and the veto took you off.", "power", id, power.week, known: true);
                    else Add(notes, Strategy, -4, "Week " + power.week + ": you were on the block.", "power", id, power.week, known: true);
                }
                if (power.vetoHolderId == s.playerId && power.vetoUsed && power.savedId != s.playerId)
                    Add(notes, Strategy, s.alliances.Any(a => a.members.Contains(s.playerId) && a.members.Contains(power.savedId ?? "")) ? 6 : 2,
                        "Week " + power.week + ": you used the veto on " + Name(s, power.savedId) + ".", "power", id, power.week, known: true);
                // Another Head of Household's plan is theirs: the house saw who went up, not what was meant.
                if (power.backdoorTargetId == s.playerId && power.hohId != s.playerId)
                    Add(notes, Strategy, power.backdoorResult == "made" ? -12 : 8, "Week " + power.week + ": a backdoor was planned for you"
                        + (power.backdoorResult == "made" ? ", and it worked." : ", and you dodged it."), "power", id, power.week, known: false);
                // The player's own ballot and the read it was cast on, against the public count.
                var ballot = s.ledger.ballots.LastOrDefault(b => b.week == power.week && b.voterId == s.playerId);
                if (ballot != null)
                {
                    if (ballot.readBefore != null)
                        Add(notes, Strategy, ballot.correct ? 5 : -5, "Week " + power.week + ": your whip count said " + Name(s, ballot.readBefore) + " would go, and "
                            + (ballot.correct ? "they did." : Name(s, power.evicteeId) + " went."), "ballot", "ballot:" + power.week, power.week, known: true);
                    Add(notes, Strategy, ballot.targetId == power.evicteeId ? 2 : -1, "Week " + power.week + ": you voted "
                        + (ballot.targetId == power.evicteeId ? "with the house." : "against the house."), "ballot", "ballot:" + power.week, power.week, known: true);
                }
                if (power.evicteeId == s.playerId && (ballot == null || ballot.readBefore != power.evicteeId))
                    Add(notes, Strategy, -6, "Week " + power.week + ": you went out without seeing it coming.", "power", id, power.week, known: true);
            }
            // The player's own chances: what they were offered and what they did with it, an offer
            // left in a conversation included.
            foreach (var chance in s.ledger.opportunities)
            {
                string when = "Week " + chance.week + ": ";
                switch (chance.kind)
                {
                    case OpportunityKinds.Read:
                        if (chance.response == OpportunityResponse.Ignored) Add(notes, Strategy, -2, when + "a vote to read, and you asked nobody.", "opportunity", chance.id, chance.week, known: true);
                        break;
                    case OpportunityKinds.Deal:
                        // A canonical row's outcome scores once per group (ScoredSafetyOutcomes, mode 2's Vote groups too),
                        // and only a Safety one is dated by its settlement: a Vote row keeps mode 1's week (vote family V5e).
                        var canonical = CommitmentReferences.FindCanonical(s, chance.id);
                        if (canonical != null && (chance.outcome == OpportunityOutcome.Won || chance.outcome == OpportunityOutcome.Lost)
                            && !scoredSafety.Contains(chance.id)) break;
                        if (canonical != null && canonical.kind != UnifiedCommitments.Safety) canonical = null;
                        // Under the commitment rules (C0, X3) a deal the other side broke is not the
                        // player's to answer for: on the record, and it costs them nothing.
                        var brokenAgainst = chance.outcome == OpportunityOutcome.Lost && EpisodeEngine.CommitmentRulesOn(s)
                            ? CommitmentReferences.FindDeal(s, chance.id) : null;
                        if (brokenAgainst != null && (brokenAgainst.status != DealStatus.Broken || Breaches.Broke(s, brokenAgainst, s.playerId))) brokenAgainst = null;
                        // How a vote deal ended is a ballot the player may not know - a voting bloc broken
                        // by both of them included - so a note on its ending waits for the weekly recap
                        // until they do. The verdict counts it either way.
                        var settled = CommitmentReferences.FindDeal(s, chance.id);
                        bool endingKnown = settled == null || KnownBallots.DealOutcomeKnown(s, settled);
                        int outcomeWeek = canonical != null && canonical.settledWeek > 0 ? canonical.settledWeek : chance.week;
                        string outcomeWhen = "Week " + outcomeWeek + ": ";
                        if (chance.outcome == OpportunityOutcome.Won) Add(notes, Strategy, 4, outcomeWhen + "a deal kept (" + Source(chance) + ").", "opportunity", chance.id, outcomeWeek, known: endingKnown);
                        else if (brokenAgainst != null) Add(notes, Strategy, 0, outcomeWhen + "a deal broken against you (" + Source(chance) + ").", "opportunity", chance.id, outcomeWeek,
                            known: KnownBallots.DealOutcomeKnown(s, brokenAgainst));
                        else if (chance.outcome == OpportunityOutcome.Lost) Add(notes, Strategy, -5, outcomeWhen + "a deal broken (" + Source(chance) + ").", "opportunity", chance.id, outcomeWeek, known: endingKnown);
                        else if (chance.response == OpportunityResponse.Expired) Add(notes, Strategy, -1, when + "an offer left on the table (" + Source(chance) + ").", "opportunity", chance.id, chance.week, known: true);
                        else if (chance.response == OpportunityResponse.Taken) Add(notes, Strategy, 1, when + "a deal made (" + Source(chance) + ").", "opportunity", chance.id, chance.week, known: true);
                        break;
                    case OpportunityKinds.Plea:
                    case OpportunityKinds.Lobby:
                        Add(notes, Strategy, chance.outcome == OpportunityOutcome.Won ? 3 : chance.note != null && chance.note.EndsWith(LobbyResponse.Hostile, StringComparison.Ordinal) ? -3 : -1,
                            when + "a plea " + (chance.outcome == OpportunityOutcome.Won ? "heard" : "turned down") + " (" + Source(chance) + ").", "opportunity", chance.id, chance.week, known: true);
                        break;
                    case OpportunityKinds.Play:
                        if (chance.outcome == OpportunityOutcome.Won) Add(notes, Strategy, 6, when + "a play won (" + Source(chance) + ").", "opportunity", chance.id, chance.week, known: true);
                        else if (chance.outcome == OpportunityOutcome.Part) Add(notes, Strategy, 3, when + "a play half won (" + Source(chance) + ").", "opportunity", chance.id, chance.week, known: true);
                        else if (chance.outcome == OpportunityOutcome.Lost) Add(notes, Strategy, -2, when + "a play lost (" + Source(chance) + ").", "opportunity", chance.id, chance.week, known: true);
                        else if (chance.response == OpportunityResponse.Expired || chance.response == OpportunityResponse.Ignored)
                            Add(notes, Strategy, -2, when + "a play let pass (" + Source(chance) + ").", "opportunity", chance.id, chance.week, known: true);
                        break;
                }
            }
            // The player's calls: who said they were in at the call, and whether the target went. A call a war
            // room's plan made (WAVE-D-NPC-PACTS-PLAN D3-L1) names nobody as defected - a dissenter is never
            // flagged - so it counts every voter at the plan who is not with it instead, and says it as the plan
            // the player went with, or pushed their way (D3-S7).
            foreach (var call in s.ledger.calls)
            {
                var power = s.ledger.power.FirstOrDefault(p => p.week == call.week);
                var plan = s.ledger.plans?.FirstOrDefault(p => p != null && p.week == call.week && p.allianceId == call.allianceId);
                int against = plan != null ? PactPlans.NotFollowing(s, plan).Count : call.defected.Count;
                double points = call.followed.Count * 2 - against + (power != null && power.evicteeId == call.targetId ? 6 : 0);
                string what = plan == null ? "you called the vote in your alliance; " + call.followed.Count + " followed, "
                    : (plan.stance == PactPlanStance.Countered ? "your alliance went with your push; " : "you went with your alliance's plan; ")
                      + call.followed.Count + " went with it, ";
                Add(notes, Strategy, points, "Week " + call.week + ": " + what + against + " did not"
                    + (power != null && power.evicteeId == call.targetId ? ", and " + Name(s, call.targetId) + " went." : "."), "call", call.allianceId + ":" + call.week, call.week, known: true);
            }
        }

        /// <summary>
        /// The canonical deal chances whose outcome scores: one per group, its owner's where the owner has one, else the first
        /// by id. Safety's incidents and receipts wherever Safety is canonical; in mode 2 the Vote family's Rule2 groups too
        /// (vote family V5e, the lead's decision D1) - a row in two groups (a deal both broke, a bloc both kept) scores in the
        /// one the player's own word made.
        /// </summary>
        private static HashSet<string> ScoredSafetyOutcomes(EpisodeState s)
        {
            if (!UnifiedCommitments.SafetyAuthorityOn(s)) return new HashSet<string>(StringComparer.Ordinal);
            var groups = new Dictionary<string, (string key, string owner)>(StringComparer.Ordinal);
            foreach (var incident in UnifiedCommitmentHistory.Breaches(s))
                foreach (string id in incident.EvidenceIds) groups[id] = ("broken:" + incident.EffectKey, incident.EffectOwnerId);
            foreach (var receipt in UnifiedCommitmentHistory.Fulfillments(s))
                foreach (string id in receipt.EvidenceIds) groups[id] = ("kept:" + receipt.EffectOwnerId, receipt.EffectOwnerId);
            foreach (var group in UnifiedVoteHistory.Incidents(s).Concat(UnifiedVoteHistory.Fulfillments(s))
                         .OrderBy(group => group.ActorId == s.playerId ? 1 : 0))
                foreach (string id in group.EvidenceIds)
                    groups[id] = ((group.Kept ? "vote-kept\u0001" : "vote-broken\u0001") + group.Week.ToString(CultureInfo.InvariantCulture)
                        + "\u0001" + group.ActorId + "\u0001" + group.WrongedId, group.OwnerId);
            var rows = UnifiedCommitmentHistory.Records(s)
                .Concat(UnifiedVoteStore.On(s) ? UnifiedVoteHistory.Records(s) : Array.Empty<UnifiedCommitmentState>())
                .ToDictionary(row => row.id, StringComparer.Ordinal);
            return new HashSet<string>(s.ledger.opportunities.Where(chance => chance.kind == OpportunityKinds.Deal
                && groups.ContainsKey(chance.id) && rows.TryGetValue(chance.id, out var row)
                && ((row.status == DealStatus.Broken && chance.outcome == OpportunityOutcome.Lost)
                    || (row.status == DealStatus.Fulfilled && chance.outcome == OpportunityOutcome.Won)))
                .GroupBy(chance => groups[chance.id].key, StringComparer.Ordinal)
                .Select(group => group.OrderBy(chance => chance.id == groups[chance.id].owner ? 0 : 1)
                    .ThenBy(chance => chance.id, StringComparer.Ordinal).First().id), StringComparer.Ordinal);
        }

        // ---------------------------------------------------------------- social

        /// <summary>The house's view of the player (and each juror's as they left), alliances that held or turned, bonds, grudges left standing, lies caught.</summary>
        private static void ScoreSocial(EpisodeState s, List<Note> notes)
        {
            var views = new List<double>();
            foreach (var npc in s.contestants.Where(c => !c.isPlayer))
            {
                var departed = s.ledger.standings.LastOrDefault(r => r.source == ClaimSource.Juror && r.fromId == npc.id && r.toId == s.playerId);
                views.Add(npc.status == ContestantStatus.Active || npc.status == ContestantStatus.Winner || npc.status == ContestantStatus.RunnerUp || departed == null
                    ? s.Score(npc.id, s.playerId) : departed.score);
            }
            // The social face is the house's view of the player, which nobody is shown while the
            // season runs: only a pact that still stands, or one that ended because somebody left the
            // house, rests on something public. Why a pact soured, a bond, a grudge and a houseguest's
            // memory are the house's own.
            if (views.Count > 0)
            {
                double mean = views.Average();
                Add(notes, Social, mean * 0.5, "How the house sees you, on average: " + (mean >= 0 ? "+" : "") + mean.ToString("0") + ".", "standing", "house", s.week, known: false);
            }
            foreach (var row in s.ledger.alliances)
            {
                var alliance = s.alliances.FirstOrDefault(a => a.id == row.id);
                if (alliance == null || !alliance.members.Contains(s.playerId)) continue;
                string others = string.Join(", ", alliance.members.Where(id => id != s.playerId).Select(id => Name(s, id)));
                if (row.endedWeek == 0) Add(notes, Social, 4, "Your alliance with " + others + " held to the end.", "alliance", row.id, row.startedWeek, known: true);
                // Under the commitment rules (C2) a pact that ended in a betrayal the player can know of:
                // a member turned on it in its life, and the player's own record says so.
                else if (row.why.EndsWith("/betrayed", StringComparison.Ordinal)
                    && alliance.members.Any(id => id != s.playerId && Allegiance.KnownBetrayals(s, id)
                        .Any(e => e.week >= Allegiance.StartWeek(s, alliance) && e.week <= row.endedWeek)))
                    Add(notes, Social, -6, "Week " + row.endedWeek + ": your alliance with " + others + " ended in a betrayal.", "alliance", row.id, row.endedWeek, known: true);
                else if (row.why.EndsWith("/turned", StringComparison.Ordinal)) Add(notes, Social, -6, "Week " + row.endedWeek + ": your alliance with " + others + " turned on you.", "alliance", row.id, row.endedWeek, known: false);
                else if (row.why.EndsWith("/soured", StringComparison.Ordinal)) Add(notes, Social, -3, "Week " + row.endedWeek + ": your alliance with " + others + " soured.", "alliance", row.id, row.endedWeek, known: false);
                else if (row.endedWeek - row.startedWeek >= 3) Add(notes, Social, 2, "Your alliance with " + others + " lasted " + (row.endedWeek - row.startedWeek) + " weeks.", "alliance", row.id, row.endedWeek,
                    known: row.why.EndsWith("/left-house", StringComparison.Ordinal));
            }
            if (s.story != null)
            {
                foreach (var bond in s.story.bonds.Where(b => b.endedWeek == 0 && (b.aId == s.playerId || b.bId == s.playerId)))
                {
                    string other = bond.aId == s.playerId ? bond.bId : bond.aId;
                    if (bond.kind == BondKinds.Nemesis) Add(notes, Social, -4, Name(s, other) + " is your nemesis.", "bond", bond.id, bond.sinceWeek, known: false);
                    else Add(notes, Social, 4, Name(s, other) + " is your " + bond.kind.Replace('-', ' ') + ".", "bond", bond.id, bond.sinceWeek, known: false);
                }
                foreach (var grudge in s.story.grudges.Where(g => g.targetId == s.playerId && g.severity >= 20))
                    Add(notes, Social, -3, Name(s, grudge.holderId) + " still holds " + (grudge.cause ?? "a grudge") + " against you.", "grudge", grudge.holderId, grudge.originWeek, known: false);
            }
            int caught = s.memories.Count(m => m.subjectId == s.playerId && m.text.Contains("found out what you were telling"));
            if (caught > 0) Add(notes, Social, -5 * caught, caught == 1 ? "A lie of yours was found out." : caught + " lies of yours were found out.", "memory", "lies", s.week, known: false);
        }

        private static string Source(OpportunityRow chance) => string.IsNullOrEmpty(chance.source) ? chance.kind : chance.source.Split(':')[0].Replace('_', ' ').Replace('-', ' ');

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? id ?? "somebody";

        private static string Ordinal(int n) => n <= 0 ? "nowhere" : n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";
    }
}
