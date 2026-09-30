using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What the player can know about a finalist at the Final 3 (ENDGAME-PLAN F2): the
    /// comparison in the window at three and the cards on the final Head of Household's decision.
    ///
    /// <para>Evidence, never the model's leaning (the plan's second principle). Every fact is
    /// something the player saw, was part of, or was told, and carries how sure the player can be
    /// of it: <see cref="Confirmed"/> for the public record and anything the player was party to,
    /// <see cref="Suspected"/> for what they heard, <see cref="Unknown"/> where there is nothing
    /// to go on. The jury model's own terms - a juror's score toward a finalist, gameplay respect,
    /// the story's jury term, the jury sentiment - are never read here, and grudges are not
    /// either: a grudge has no visibility, so it is the model's input, not the player's
    /// knowledge. Bitterness comes from the public record instead: who put whom on the block.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator, and it
    /// lives in the simulation so the Unity-free subset can test it.</para>
    /// </summary>
    public static class FinalistRead
    {
        public const string Confirmed = "Confirmed", Suspected = "Suspected", Unknown = "Unknown";
        public const string Strong = "Strong", Moderate = "Moderate", Light = "Light";
        /// <summary>A juror's lean toward a finalist, as far as the player can tell.</summary>
        public const string Support = "support", Bitter = "bitter", Uncertain = "uncertain";
        /// <summary>Why a juror the player has evidence both ways about is uncertain.</summary>
        public const string MixedSignals = "mixed signals";
        /// <summary>A learned standing this warm or this cold says something; the engine's own bands for a told or overheard read.</summary>
        public const double WarmStanding = 25, ColdStanding = -25;
        /// <summary>The player's own standing bands, as <c>RelationshipWeb.KindOf</c> draws them.</summary>
        public const double FriendThreshold = 15, DistrustThreshold = -15, RivalThreshold = -40;

        /// <summary>One line of a finalist's card: what it is about, what is known, and how sure.</summary>
        public sealed class Fact
        {
            public string label, value, certainty;
            public Fact(string label, string value, string certainty) { this.label = label; this.value = value; this.certainty = certainty; }
        }

        /// <summary>One juror's lean toward the finalist and why, or <see cref="Uncertain"/>.</summary>
        public sealed class JurorLean
        {
            public string jurorId, lean, certainty, reason;
            /// <summary>The juror's first name, as the card lists them under their count.</summary>
            public string name;
        }

        public sealed class Finalist
        {
            public string id;
            /// <summary>Their own line, the one they introduced themselves with (<see cref="WebIntroductions.IntroLine"/>).</summary>
            public string line;
            public string grade;
            public int wins;
            public Fact resume, relationship, agreement, alliances, support, bitterness, uncertain;
            public List<JurorLean> jurors = new List<JurorLean>();
            /// <summary>The facts a card shows when they are not the finalist's seven: a juror's case (<see cref="JurorCase"/>).</summary>
            public List<Fact> caseFacts;
            public IEnumerable<Fact> Facts => (IEnumerable<Fact>)caseFacts ?? new[] { resume, relationship, agreement, alliances, support, bitterness, uncertain };

            /// <summary>
            /// The player's own score toward them, the value the cast strip's bar is drawn from. The
            /// card draws it as that bar and never prints it: it is the player's reading, not theirs.
            /// </summary>
            public double standing;
            /// <summary>The line under the player's standing, from the record only (<see cref="RelationshipLine"/>), or null.</summary>
            public string standingLine;
            /// <summary>The parts of the final Head of Household they won, 1 and 2, in order.</summary>
            public List<int> finalPartsWon = new List<int>();
            /// <summary>Eviction votes they sat on the block through and stayed (<see cref="VotesSurvived"/>).</summary>
            public int votesSurvived;
            /// <summary>Whether they are the final Head of Household on the record (<see cref="FinalHeadOfHousehold"/>).</summary>
            public bool finalHead;
        }

        /// <summary>The jurors, as the engine's jury vote counts one: Jury, or Evicted on an older save. Never the player.</summary>
        public static List<ContestantState> Jurors(EpisodeState s) =>
            s.contestants.Where(c => c.id != s.playerId && (c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted)).ToList();

        /// <summary>The other finalists the player is weighing, in cast order: everyone still in the house but the player.</summary>
        public static List<ContestantState> Others(EpisodeState s) => s.Active.Where(c => c.id != s.playerId).ToList();

        public static Finalist Read(EpisodeState s, string finalistId)
        {
            var actor = s.Find(finalistId);
            if (actor == null) return null;
            var read = new Finalist { id = actor.id };
            read.line = WebIntroductions.IntroLine(unchecked((int)s.seed), actor.id, actor.name, actor.traits);

            read.wins = Wins(s, actor);
            read.grade = Grade(s, actor);
            read.resume = new Fact("Competition record", read.grade + " · " + Record(s, actor), Confirmed);
            if (s.finalPart1WinnerId == actor.id) read.finalPartsWon.Add(1);
            if (s.finalPart2WinnerId == actor.id) read.finalPartsWon.Add(2);
            read.votesSurvived = VotesSurvived(s, actor.id);
            read.finalHead = FinalHeadOfHousehold(s) == actor.id;
            read.relationship = new Fact("Your relationship", StandingWord(s, actor.id), Confirmed);
            read.standing = s.Score(s.playerId, actor.id);
            read.standingLine = RelationshipLine(s, actor.id);
            // The player is party to any agreement there is; with none there is nothing to go on.
            string agreement = Agreement(s, actor.id);
            read.agreement = agreement != null ? new Fact("Final 2 agreement", agreement, Confirmed) : new Fact("Final 2 agreement", "None with you", Unknown);
            read.alliances = Alliances(s, actor.id);

            foreach (var juror in Jurors(s)) read.jurors.Add(Lean(s, juror.id, actor.id));
            read.support = Count("Known jury support", read.jurors.Where(j => j.lean == Support).ToList(), s);
            read.bitterness = Count("Jury bitterness", read.jurors.Where(j => j.lean == Bitter).ToList(), s);
            int unsure = read.jurors.Count(j => j.lean == Uncertain);
            read.uncertain = new Fact("Uncertain jurors", unsure + " of " + read.jurors.Count, Unknown);
            return read;
        }

        // ------------------------------------------------------------ the record

        /// <summary>Competition wins on the record: Heads of Household (the final one included), vetoes, and the final Head of Household's first two parts.</summary>
        public static int Wins(EpisodeState s, ContestantState actor) =>
            actor.hohWins + actor.vetoWins + (s.finalPart1WinnerId == actor.id ? 1 : 0) + (s.finalPart2WinnerId == actor.id ? 1 : 0);

        /// <summary>
        /// The record against the house's: Strong for three wins or more, or two when nobody in the
        /// season has more; Light for none; Moderate between. Public, so certain.
        /// </summary>
        public static string Grade(EpisodeState s, ContestantState actor)
        {
            int wins = Wins(s, actor);
            if (wins == 0) return Light;
            int most = s.contestants.Max(c => Wins(s, c));
            return wins >= 3 || (wins >= 2 && wins >= most) ? Strong : Moderate;
        }

        /// <summary>"HoH 2 · Veto 1 · Nominated 3", with a final part won when there is one.</summary>
        public static string Record(EpisodeState s, ContestantState actor)
        {
            string record = "HoH " + actor.hohWins + " · Veto " + actor.vetoWins + " · Nominated " + actor.timesNominated;
            if (s.finalPart1WinnerId == actor.id) record += " · Won Final HoH Part 1";
            if (s.finalPart2WinnerId == actor.id) record += " · Won Final HoH Part 2";
            return record;
        }

        /// <summary>
        /// Where the player stands with them in one word, from the player's own outbound reading -
        /// the words and thresholds <c>RelationshipWeb.StandingWord(KindOf)</c> uses, kept here so
        /// the simulation can say it (an EditMode test holds the two together).
        /// </summary>
        public static string StandingWord(EpisodeState s, string otherId)
        {
            if (string.IsNullOrEmpty(otherId) || otherId == s.playerId) return "Neutral";
            if (s.Allied(s.playerId, otherId)) return "Allied";
            double score = s.Score(s.playerId, otherId);
            if (score >= FriendThreshold) return "Friendly";
            if (score <= RivalThreshold) return "Hostile";
            if (score <= DistrustThreshold) return "Wary";
            return "Neutral";
        }

        // ------------------------------------------------------------ the Final 2 agreement

        /// <summary>The player's Final 2 deal with them that still binds (Active), or null. What the final eviction settles.</summary>
        public static DealState BindingDeal(EpisodeState s, string otherId) =>
            s.deals.LastOrDefault(d => d.type == DealKind.FinalTwo && d.status == DealStatus.Active && Between(d.proposerId, d.recipientId, s.playerId, otherId));

        /// <summary>The player's own Final 2 promise to them that still stands, or null: the engine settles the Head of Household's promises at the final eviction.</summary>
        public static PromiseState BindingPromise(EpisodeState s, string otherId) =>
            s.promises.LastOrDefault(p => p.kind == PromiseKind.FinalTwo && p.status == PromiseStatus.Active && p.fromId == s.playerId && p.toId == otherId);

        /// <summary>Whether the player is bound to take them: a Final 2 deal that is Active, or the player's own Final 2 promise that stands.</summary>
        public static bool BoundTo(EpisodeState s, string otherId) => BindingDeal(s, otherId) != null || BindingPromise(s, otherId) != null;

        /// <summary>
        /// The Final 2 agreement between the player and them in words, what binds first - the
        /// order the final eviction settles them in: a deal that binds, then the player's own
        /// promise that stands; then an offer still waiting; then the latest deal or promise of
        /// any standing, either way. Null when there has never been one.
        /// </summary>
        public static string Agreement(EpisodeState s, string otherId)
        {
            var binding = BindingDeal(s, otherId);
            if (binding != null) return "Deal: " + HouseguestNotes.DealStanding(binding.status, binding.proposerId == otherId);
            if (BindingPromise(s, otherId) != null) return "You promised: " + HouseguestNotes.PromiseStanding(PromiseStatus.Active);
            var deals = s.deals.Where(d => d.type == DealKind.FinalTwo && Between(d.proposerId, d.recipientId, s.playerId, otherId)).ToList();
            var waiting = deals.LastOrDefault(d => d.status == DealStatus.Proposed);
            if (waiting != null) return "Deal: " + HouseguestNotes.DealStanding(waiting.status, waiting.proposerId == otherId);
            var promises = s.promises.Where(p => p.kind == PromiseKind.FinalTwo && Between(p.fromId, p.toId, s.playerId, otherId)).ToList();
            var promise = promises.LastOrDefault(p => p.status == PromiseStatus.Active) ?? promises.LastOrDefault();
            // Their own promise to the player still standing says more than a dead deal does.
            if (promise != null && promise.status == PromiseStatus.Active)
                return "They promised: " + HouseguestNotes.PromiseStanding(promise.status);
            var deal = deals.LastOrDefault();
            if (deal != null) return "Deal: " + HouseguestNotes.DealStanding(deal.status, deal.proposerId == otherId);
            if (promise != null)
                return (promise.fromId == s.playerId ? "You promised: " : "They promised: ") + HouseguestNotes.PromiseStanding(promise.status);
            return null;
        }

        /// <summary>
        /// The player's Final 2 deals and promises with someone other than the two finalists - a
        /// juror by now - that the final eviction breaks whichever finalist the player takes: the
        /// engine settles every binding Final 2 deal and every standing Final 2 promise of the Head
        /// of Household's, and only the one taken is kept. First names.
        /// </summary>
        public static List<string> BrokenEitherWay(EpisodeState s)
        {
            var finalists = new HashSet<string>(Others(s).Select(c => c.id));
            var names = new List<string>();
            foreach (var deal in s.deals.Where(d => d.type == DealKind.FinalTwo && d.status == DealStatus.Active
                && (d.proposerId == s.playerId || d.recipientId == s.playerId)))
            {
                string partner = deal.proposerId == s.playerId ? deal.recipientId : deal.proposerId;
                if (!finalists.Contains(partner) && !names.Contains(FirstName(s, partner))) names.Add(FirstName(s, partner));
            }
            foreach (var promise in s.promises.Where(p => p.kind == PromiseKind.FinalTwo && p.status == PromiseStatus.Active && p.fromId == s.playerId))
                if (!finalists.Contains(promise.toId) && !names.Contains(FirstName(s, promise.toId))) names.Add(FirstName(s, promise.toId));
            return names;
        }

        private static bool Between(string a, string b, string x, string y) => (a == x && b == y) || (a == y && b == x);

        // ------------------------------------------------------------ alliances

        /// <summary>
        /// How the player knows of an alliance: Confirmed when they are in it or it is public,
        /// Suspected when they only heard of it, null when they do not know of it at all. An
        /// alliance with no fact is unknown to the player, as the vote read has it.
        /// </summary>
        public static string AllianceCertainty(EpisodeState s, AllianceState alliance)
        {
            if (alliance == null) return null;
            if (alliance.members.Contains(s.playerId)) return Confirmed;
            var fact = Knowledge.Of(s, FactKinds.Alliance, alliance.id);
            if (fact == null) return null;
            if (fact.visibility == FactVisibility.Public) return Confirmed;
            return Knowledge.Knows(fact, s.playerId) ? Suspected : null;
        }

        private static Fact Alliances(EpisodeState s, string finalistId)
        {
            var known = new List<(AllianceState alliance, string certainty)>();
            foreach (var alliance in s.alliances.Where(a => a.members.Contains(finalistId)))
            {
                string certainty = AllianceCertainty(s, alliance);
                if (certainty != null) known.Add((alliance, certainty));
            }
            if (known.Count == 0) return new Fact("Known alliances", "None known", Unknown);
            var withYou = known.Where(k => k.alliance.members.Contains(s.playerId) && k.alliance.active).ToList();
            var parts = new List<string>();
            if (withYou.Count > 0) parts.Add("With you: " + string.Join(", ", withYou.Select(k => k.alliance.name)));
            int others = known.Count - withYou.Count;
            if (others > 0) parts.Add(others + (others == 1 ? " other" : " others") + " you know of");
            string sure = known.All(k => k.certainty == Confirmed) ? Confirmed : Suspected;
            return new Fact("Known alliances", string.Join(" · ", parts), sure);
        }

        // ------------------------------------------------------------ the jury

        /// <summary>
        /// What the player can tell of one juror's lean toward a finalist. For: an alliance they
        /// shared that the player knows of, however it ended (why an alliance ended is worked out
        /// from scores the player never sees, so it cannot be the player's evidence), a couple the
        /// player knows of, or a warm standing the player learned. Against: the finalist put them
        /// on the block (the ledger's power rows, a veto save included), or a reveal proved the
        /// finalist voted to evict them (a claim kept, or a lie exposed when the ballot went to the
        /// other nominee), or a cold standing the player learned. Both, or neither: uncertain.
        /// </summary>
        public static JurorLean Lean(EpisodeState s, string jurorId, string finalistId)
        {
            string forCertainty = null, forReason = null, againstCertainty = null, againstReason = null;
            void For(string certainty, string reason)
            {
                if (forCertainty == Confirmed) return;
                if (forCertainty == null || certainty == Confirmed) { forCertainty = certainty; forReason = reason; }
            }
            void Against(string certainty, string reason)
            {
                if (againstCertainty == Confirmed) return;
                if (againstCertainty == null || certainty == Confirmed) { againstCertainty = certainty; againstReason = reason; }
            }

            foreach (var alliance in s.alliances.Where(a => a.members.Contains(jurorId) && a.members.Contains(finalistId)))
            {
                string certainty = AllianceCertainty(s, alliance);
                if (certainty != null) For(certainty, "shared an alliance");
            }
            string couple = StoryConsumers.CoupleRef(s, jurorId, finalistId);
            if (couple != null)
            {
                var fact = Knowledge.Of(s, FactKinds.Couple, couple);
                if (fact != null && fact.visibility == FactVisibility.Public) For(Confirmed, "a showmance");
                else if (Knowledge.Knows(fact, s.playerId)) For(Suspected, "a showmance");
            }
            var standing = s.ledger?.standings?.LastOrDefault(row => row.fromId == jurorId && row.toId == finalistId
                && (row.source == ClaimSource.Told || row.source == ClaimSource.Overheard));
            if (standing != null && standing.score >= WarmStanding) For(Suspected, "you heard they were close");
            if (standing != null && standing.score <= ColdStanding) Against(Suspected, "you heard they did not trust them");

            if (s.ledger?.power != null && s.ledger.power.Any(p => p.hohId == finalistId && (p.nominees.Contains(jurorId) || p.replacementId == jurorId || p.savedId == jurorId)))
                Against(Confirmed, "nominated them");
            if (s.ledger?.claims != null && s.ledger.claims.Any(c => c.voterId == finalistId && VotedAgainst(s, c) == jurorId))
                Against(Confirmed, "voted to evict them");

            string name = FirstName(s, jurorId);
            if (forCertainty != null && againstCertainty == null)
                return new JurorLean { jurorId = jurorId, name = name, lean = Support, certainty = forCertainty, reason = forReason };
            if (againstCertainty != null && forCertainty == null)
                return new JurorLean { jurorId = jurorId, name = name, lean = Bitter, certainty = againstCertainty, reason = againstReason };
            return new JurorLean { jurorId = jurorId, name = name, lean = Uncertain, certainty = Unknown,
                reason = forCertainty != null ? MixedSignals : "nothing to go on" };
        }

        /// <summary>
        /// Who a claim's voter was proven at the reveal to have voted to evict: the claimed target
        /// when the claim was kept; when it was a lie, the other nominee on that week's final block.
        /// Null while the claim is open, or when the week's block is not on the record.
        /// </summary>
        private static string VotedAgainst(EpisodeState s, ClaimRow claim)
        {
            if (claim.status == ClaimStatus.Kept) return claim.targetId;
            if (claim.status != ClaimStatus.Lied) return null;
            var week = s.ledger?.power?.LastOrDefault(p => p.week == claim.week && p.nominees.Count == 2 && p.nominees.Contains(claim.targetId));
            return week?.nominees.FirstOrDefault(n => n != claim.targetId);
        }

        private static Fact Count(string label, List<JurorLean> leans, EpisodeState s)
        {
            if (leans.Count == 0) return new Fact(label, "None known", Unknown);
            string names = string.Join(", ", leans.Select(l => FirstName(s, l.jurorId) + " (" + l.reason + ")"));
            string certainty = leans.All(l => l.certainty == Confirmed) ? Confirmed : Suspected;
            return new Fact(label, leans.Count + (leans.Count == 1 ? " juror: " : " jurors: ") + names, certainty);
        }

        private static string FirstName(EpisodeState s, string id) => FirstName(s.Find(id)?.name ?? id);

        /// <summary>
        /// The name a line calls someone by: the first word that is not a title ("Dr. Will Kirby"
        /// is Will), as the cast screen's CastSelect.FirstName has it.
        /// </summary>
        public static string FirstName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var words = name.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            return words.FirstOrDefault(word => !word.EndsWith(".", System.StringComparison.Ordinal)) ?? words[0];
        }

        // ------------------------------------------------------------ the player on the jury

        /// <summary>
        /// A finalist's case as the player on the jury can read it (ENDGAME-PLAN F6): the record,
        /// the player's own relationship with them, any Final 2 agreement between them, the
        /// alliances the player knew, and what the finalist did to the player where the house could
        /// see it. The other jurors' leans are not shown: the vote is the player's own.
        /// </summary>
        public static Finalist JurorCase(EpisodeState s, string finalistId)
        {
            var read = Read(s, finalistId);
            if (read == null) return null;
            read.caseFacts = new List<Fact> { read.resume, read.relationship, read.agreement, read.alliances, TowardYou(s, finalistId) };
            return read;
        }

        /// <summary>
        /// What a finalist did to the player on the record: put them on the block as Head of
        /// Household (a veto save included), named them the replacement, used the veto to put them
        /// up, evicted them, broke a deal with them (<see cref="BrokeADealWithYou"/>) or a promise
        /// to them. A voting block that fell apart was both of theirs, so it is said as that. Public
        /// or the player's own, so certain; nothing, Unknown.
        /// </summary>
        public static Fact TowardYou(EpisodeState s, string finalistId)
        {
            string player = s.playerId;
            var acts = new List<(int week, string text)>();
            if (s.ledger?.power != null)
                foreach (var p in s.ledger.power)
                {
                    bool final = p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null;
                    if (p.evicteeId == player && p.hohId == finalistId && final) { acts.Add((p.week, "evicted you at the final eviction")); continue; }
                    if (final) continue;
                    if (p.hohId == finalistId && p.replacementId == player) acts.Add((p.week, "named you the replacement"));
                    else if (p.hohId == finalistId && (p.nominees.Contains(player) || p.savedId == player)) acts.Add((p.week, "nominated you"));
                    else if (p.vetoHolderId == finalistId && p.vetoUsed && p.replacementId == player) acts.Add((p.week, "used the veto to put you up"));
                }
            var broken = s.deals.Where(d => d.status == DealStatus.Broken && Between(d.proposerId, d.recipientId, player, finalistId)).ToList();
            int deals = broken.Count(d => BrokeADealWithYou(s, d, finalistId));
            bool block = broken.Any(d => d.type == DealKind.VoteTogether);
            int promises = s.promises.Count(p => p.status == PromiseStatus.Broken && p.fromId == finalistId && p.toId == player);
            var parts = acts.OrderBy(a => a.week).Select(a => "Week " + a.week + ": " + a.text).ToList();
            if (deals > 0) parts.Add(deals == 1 ? "Broke a deal with you" : "Broke " + deals + " deals with you");
            if (block) parts.Add("Your voting block fell apart");
            if (promises > 0) parts.Add(promises == 1 ? "Broke a promise to you" : "Broke " + promises + " promises to you");
            return parts.Count == 0 ? new Fact("What they did to you", "Nothing on the record", Unknown)
                : new Fact("What they did to you", string.Join(" · ", parts), Confirmed);
        }

        /// <summary>Whether a broken deal between the player and a finalist was the finalist's doing (<see cref="DealBreaker"/>).</summary>
        public static bool BrokeADealWithYou(EpisodeState s, DealState deal, string finalistId) =>
            finalistId != null && finalistId != s.playerId && DealBreaker(s, deal) == finalistId;

        /// <summary>
        /// Who broke a broken deal the player was party to, as the player can tell it, or null. The
        /// engine judges each kind on one act (<see cref="DealResolution"/>) and names who did it;
        /// the player reads the same acts off the record. A nomination deal (safety, target) broke
        /// on the first ceremony in its term where one of them, as Head of Household, put the other
        /// up. A veto deal broke on the first veto in its term that one of them held and left the
        /// other on the block. A vote deal broke at the vote on its target: the player knows their
        /// own ballot, so a deal broken where they kept it was the other's. A Final 2 deal is
        /// settled by the final Head of Household. A voting block is settled by both at once, so by
        /// neither; anything else, by nobody the record names.
        /// </summary>
        public static string DealBreaker(EpisodeState s, DealState deal)
        {
            string player = s.playerId;
            if (deal == null || deal.status != DealStatus.Broken || (deal.proposerId != player && deal.recipientId != player)) return null;
            string other = deal.proposerId == player ? deal.recipientId : deal.proposerId;
            var rows = s.ledger?.power ?? new List<PowerRow>();
            bool Final(PowerRow p) => p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null;
            int last = deal.expiresWeek >= deal.week ? deal.expiresWeek : int.MaxValue;
            var term = rows.Where(p => p.week >= deal.week && p.week <= last && !Final(p)).OrderBy(p => p.week).ToList();
            switch (deal.type)
            {
                case DealKind.SafetyAgreement:
                case DealKind.TargetAgreement:
                    foreach (var p in term)
                    {
                        if (p.hohId == other && PutUp(p, player)) return other;
                        if (p.hohId == player && PutUp(p, other)) return player;
                    }
                    return null;
                case DealKind.VetoUse:
                    foreach (var p in term)
                    {
                        if (p.vetoHolderId == other && LeftOnTheBlock(p, player)) return other;
                        if (p.vetoHolderId == player && LeftOnTheBlock(p, other)) return player;
                    }
                    return null;
                case DealKind.VoteSave:
                case DealKind.VoteEvict:
                    foreach (var p in term.Where(p => p.tally.Count > 0 && p.nominees.Contains(deal.targetId)))
                    {
                        var ballot = s.ledger.ballots.FirstOrDefault(b => b.week == p.week && b.voterId == player);
                        if (ballot == null) return other;
                        bool evicted = ballot.targetId == deal.targetId;
                        return (deal.type == DealKind.VoteEvict ? evicted : !evicted) ? other : player;
                    }
                    return null;
                case DealKind.FinalTwo:
                    var chose = rows.Where(Final).OrderBy(p => p.week).LastOrDefault();
                    return chose != null && (chose.hohId == player || chose.hohId == other) ? chose.hohId : null;
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------ the card's lines (MOCKUP-PASS M8)

        /// <summary>
        /// The line under the player's standing with a finalist (mockup 59), from the record only:
        /// the alliance the two of them are in and the week it began, the deals between them that
        /// were kept, and what the finalist did to the player where the house could see it
        /// (<see cref="TowardYou"/>). Never how the finalist feels about the player: that is a score
        /// the player does not see. Null when the record holds nothing between them.
        /// </summary>
        public static string RelationshipLine(EpisodeState s, string finalistId)
        {
            if (string.IsNullOrEmpty(finalistId) || finalistId == s.playerId || s.Find(finalistId) == null) return null;
            var parts = new List<string>();
            // The week an alliance began is on the ledger; one without a row is said by the standing word already.
            var weeks = s.alliances.Where(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(finalistId))
                .Select(a => s.ledger?.alliances?.FirstOrDefault(row => row.id == a.id)?.startedWeek ?? 0)
                .Where(week => week > 0).ToList();
            if (weeks.Count > 0) parts.Add("Allied since week " + weeks.Min());
            int kept = s.deals.Count(d => d.status == DealStatus.Fulfilled && Between(d.proposerId, d.recipientId, s.playerId, finalistId));
            if (kept > 0) parts.Add(kept == 1 ? "Kept a deal with you" : "Kept " + kept + " deals with you");
            var acts = TowardYou(s, finalistId);
            if (acts.certainty == Confirmed) parts.Add(acts.value);
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        /// <summary>
        /// How many eviction votes they sat on the block through and stayed (MOCKUP-PASS decision
        /// 32): the weeks on the record with a tally, them on the block at the vote, and somebody
        /// else evicted. A ceremony and its count are public, so this is too.
        /// </summary>
        public static int VotesSurvived(EpisodeState s, string id) =>
            s.ledger?.power == null || string.IsNullOrEmpty(id) ? 0
                : s.ledger.power.Count(p => p.tally.Count > 0 && p.nominees.Contains(id) && p.evicteeId != null && p.evicteeId != id);

        /// <summary>
        /// The final Head of Household once they have chosen, on the record: the last Head of
        /// Household's own power row, the one with an evictee and neither a vote nor a veto. Null
        /// before the final eviction is decided, so the crown the jury's cards give them (decision
        /// 33) never lands on anybody early.
        /// </summary>
        public static string FinalHeadOfHousehold(EpisodeState s)
        {
            if (s.phase != EpisodePhase.JuryQuestioning && s.phase != EpisodePhase.FinalSpeeches
                && s.phase != EpisodePhase.Jury && s.phase != EpisodePhase.Finished) return null;
            return s.ledger?.power?.LastOrDefault(p => p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null)?.hohId;
        }

        /// <summary>Whether a week's Head of Household put somebody up: nominated (a veto save included) or named the replacement.</summary>
        internal static bool PutUp(PowerRow p, string id) => p.nominees.Contains(id) || p.savedId == id || p.replacementId == id;

        /// <summary>Whether somebody was on the block at the veto and the holder did not take them off.</summary>
        internal static bool LeftOnTheBlock(PowerRow p, string id) => p.nominees.Contains(id) && p.replacementId != id && p.savedId != id;

        // ------------------------------------------------------------ the decision

        /// <summary>
        /// What taking <paramref name="takeId"/> to the Final 2 means - and so evicting
        /// <paramref name="cutId"/> - in lines read from the same evidence: the agreements the
        /// final eviction settles, the record, and the jurors the player knows about.
        /// </summary>
        public static List<string> IfYouTake(EpisodeState s, string takeId, string cutId)
        {
            var lines = new List<string>();
            var take = s.Find(takeId);
            var cut = s.Find(cutId);
            if (take == null || cut == null) return lines;
            string takeName = FirstName(s, takeId), cutName = FirstName(s, cutId);
            if (BoundTo(s, takeId)) lines.Add("Honours your Final 2 deal with " + takeName + ".");
            if (BoundTo(s, cutId)) lines.Add("Breaks your Final 2 deal with " + cutName + ".");
            var read = Read(s, takeId);
            if (read.grade == Strong) lines.Add(takeName + " sits beside you with a strong competition record.");
            else if (read.grade == Light) lines.Add(takeName + " has no competition wins to argue with.");
            // The jury that will vote: today's, and the finalist cut, who joins it.
            var incoming = Lean(s, cutId, takeId);
            var jury = read.jurors.Concat(new[] { incoming }).ToList();
            int support = jury.Count(j => j.lean == Support), bitter = jury.Count(j => j.lean == Bitter);
            if (support > 0) lines.Add(support + (support == 1 ? " juror you know of is" : " jurors you know of are") + " close to " + takeName + ".");
            if (bitter > 0) lines.Add(bitter + (bitter == 1 ? " juror has" : " jurors have") + " reason to hold a grudge against " + takeName + ".");
            if (support == 0 && bitter == 0) lines.Add("You know little about how the jury sees " + takeName + ".");
            lines.Add(incoming.lean == Bitter ? cutName + " joins the jury with reason to hold a grudge against " + takeName + " (" + incoming.reason + ")."
                : incoming.lean == Support ? cutName + " joins the jury close to " + takeName + " (" + incoming.reason + ")."
                : cutName + " joins the jury.");
            return lines;
        }
    }
}
