using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The player's word (ACTIONS-DEALS-ALLIANCES-PLAN V1): every commitment the player is a party
    /// to - the promises they made and were made, the deals they proposed, accepted or answered,
    /// their loyalty oaths and the calls they made in an alliance - each with who it is with, what
    /// it binds, until when, where it stands and how it ended.
    ///
    /// <para>Read from the season's own records and nothing else. A deal between two houseguests, a
    /// score, a ballot not yet revealed is never here, and an ending is said as the player was told
    /// it: a promise is only ever broken by whoever gave it, an oath's breach is on the player's own
    /// record with the other party, and who broke a deal is said only where the record can tell it
    /// (<see cref="FinalistRead.DealBreaker"/>). Elsewhere a broken deal is "broken", blaming nobody:
    /// deals do not record who broke them yet.</para>
    ///
    /// <para><see cref="WouldBreak"/> is the dry run behind the decision screens' warnings: what an
    /// intended nomination, veto, ballot or final choice would break. The nomination, the veto and
    /// the final choice are run through a throwaway engine on a copy of the season, so a warning and
    /// the verdict the commit then reaches cannot disagree; nothing is written to the season the
    /// copy came from, nothing is committed, and the only random stream advanced is the copy's. The
    /// ballot is judged by the rules instead (<see cref="ByTheRules"/>): the engine settles a ballot
    /// only at the reveal, after drawing every other houseguest's private ballot, and a warning that
    /// waited for those would be one built from ballots the player cannot see.</para>
    /// </summary>
    public static class CommitmentsRead
    {
        /// <summary>What a commitment is.</summary>
        public static class Kinds
        {
            public const string Promise = "promise", Deal = "deal", Oath = "oath", Call = "call";
            public static readonly string[] All = { Promise, Deal, Oath, Call };
        }

        /// <summary>How a commitment ended, or that it has not.</summary>
        public static class Outcomes
        {
            public const string Open = "open", Kept = "kept", Broken = "broken", Lapsed = "lapsed";
            public static readonly string[] All = { Open, Kept, Broken, Lapsed };
        }

        /// <summary>One commitment the player is a party to, as the page says it.</summary>
        public sealed class Commitment
        {
            /// <summary><see cref="Kinds"/>, and the record's own id: a promise's, a deal's, an oath's or a call's.</summary>
            public string kind, id;
            /// <summary>Who it is with: the other party, or for a call the member it asked.</summary>
            public string withId;
            /// <summary>A third houseguest it names - a deal's target, a vote promise's, a call's - or null.</summary>
            public string aboutId;
            /// <summary>Whether it is the player's own word: their promise, their proposal, their oath, their call.</summary>
            public bool yours;
            /// <summary>What it is, in a few words: "Safety deal you proposed", "Your promise of safety".</summary>
            public string title;
            /// <summary>What it binds, in words.</summary>
            public string binds;
            /// <summary>The week it was made, or 0 where the record cannot say.</summary>
            public int week;
            /// <summary>The last week it binds by its own record, or 0 for never.</summary>
            public int untilWeek;
            /// <summary>Its term in words: "this week", "until week 5", "never expires".</summary>
            public string term;
            /// <summary>Where it stands, in the record's own words: "agreed", "waiting on you", "broken by them".</summary>
            public string status;
            /// <summary><see cref="Outcomes"/>.</summary>
            public string outcome;
            /// <summary>Who broke it, where the record can say; null otherwise.</summary>
            public string brokenById;
            /// <summary>The week it was settled, where the record can say; 0 otherwise.</summary>
            public int settledWeek;

            public bool IsOpen => outcome == Outcomes.Open;
        }

        // ------------------------------------------------------------ the reader

        /// <summary>
        /// Every commitment the player is a party to: promises, then deals, then oaths, then calls,
        /// each in the order the season made them. Empty for no season or no player.
        /// </summary>
        public static List<Commitment> Of(EpisodeState s)
        {
            var found = new List<Commitment>();
            if (s == null || string.IsNullOrEmpty(s.playerId) || s.Find(s.playerId) == null) return found;
            AddPromises(s, found);
            AddDeals(s, found);
            AddOaths(s, found);
            AddCalls(s, found);
            return found;
        }

        /// <summary>The player's commitments with one houseguest.</summary>
        public static List<Commitment> With(EpisodeState s, string id) =>
            Of(s).Where(c => c.withId == id).ToList();

        /// <summary>The line the Your word page gives a commitment: what it is, what it binds, until when, and where it stands.</summary>
        public static string Line(Commitment c) =>
            c == null ? null : c.title + ": " + c.binds + " · " + c.term + " · " + c.status;

        /// <summary>
        /// A commitment's term in words, from the week it is read in: "never expires", "this week",
        /// "until week 5", "week 3 only" for a week that has passed. A deal's own week can pass while
        /// it still binds - the house lets deals lapse only when it next settles them - and the term
        /// says so rather than claim it has ended.
        /// </summary>
        public static string Term(EpisodeState s, Commitment c)
        {
            if (c == null) return null;
            if (c.untilWeek <= 0) return "never expires";
            int now = s != null ? s.week : c.untilWeek;
            if (c.untilWeek == now) return "this week";
            if (c.untilWeek > now) return "until week " + c.untilWeek;
            // An offer nobody has answered binds nobody yet; it is still there to be answered.
            if (c.IsOpen) return "until week " + c.untilWeek + (Waiting(c) ? ", still open" : ", still binding");
            return c.week == c.untilWeek ? "week " + c.untilWeek + " only" : "until week " + c.untilWeek;
        }

        private static bool Waiting(Commitment c) =>
            c.kind == Kinds.Deal && (c.status == "waiting on you" || c.status == "waiting on them");

        private static void AddPromises(EpisodeState s, List<Commitment> into)
        {
            string player = s.playerId;
            foreach (var p in s.promises)
            {
                if (p == null || (p.fromId != player && p.toId != player)) continue;
                bool yours = p.fromId == player;
                string other = yours ? p.toId : p.fromId;
                if (other == player || s.Find(other) == null) continue;
                var c = new Commitment
                {
                    kind = Kinds.Promise, id = p.id, withId = other, yours = yours,
                    aboutId = p.kind == PromiseKind.Vote && s.Find(p.targetId) != null ? p.targetId : null,
                    title = (yours ? "Your " : "Their ") + PromiseNoun(p.kind),
                    week = p.week, untilWeek = Math.Max(0, p.expiresWeek),
                    outcome = PromiseOutcome(p.status),
                    // A promise is one-sided: only whoever gave it can break it, and the engine
                    // breaks it only on their act (a nomination, a ballot, the final choice).
                    brokenById = p.status == PromiseStatus.Broken ? p.fromId : null,
                };
                c.binds = PromiseBinds(s, p, yours);
                c.status = p.status == PromiseStatus.Broken ? (yours ? "broken by you" : "broken by them")
                    : p.status == PromiseStatus.Expired ? "lapsed" : HouseguestNotes.PromiseStanding(p.status);
                c.term = Term(s, c);
                into.Add(c);
            }
        }

        private static void AddDeals(EpisodeState s, List<Commitment> into)
        {
            string player = s.playerId;
            foreach (var d in s.deals)
            {
                if (d == null || (d.proposerId != player && d.recipientId != player)) continue;
                bool yours = d.proposerId == player;
                string other = yours ? d.recipientId : d.proposerId;
                if (other == player || s.Find(other) == null) continue;
                var c = new Commitment
                {
                    kind = Kinds.Deal, id = d.id, withId = other, yours = yours,
                    aboutId = DealKind.NamesATarget(d.type) && s.Find(d.targetId) != null ? d.targetId : null,
                    title = Capitalise(DealNoun(d.type)) + (yours ? " you proposed" : " they offered"),
                    week = d.week, untilWeek = Math.Max(0, d.expiresWeek),
                    outcome = DealOutcome(d.status),
                };
                if (d.status == DealStatus.Broken) c.brokenById = FinalistRead.DealBreaker(s, d);
                c.binds = DealBinds(s, d);
                c.status = DealStatusWord(d, yours, c.brokenById, player);
                c.term = Term(s, c);
                into.Add(c);
            }
        }

        /// <summary>The words an oath's breach is recorded under on the player's arc with the other party (<see cref="WebLoyaltyOaths"/>).</summary>
        private const string OathBreachReason = "Broke loyalty oath by ";

        private static void AddOaths(EpisodeState s, List<Commitment> into)
        {
            string player = s.playerId;
            bool playerIn = s.Find(player)?.status == ContestantStatus.Active;
            foreach (var o in s.loyaltyOaths)
            {
                if (o == null || (o.playerId != player && o.targetId != player)) continue;
                string other = o.playerId == player ? o.targetId : o.playerId;
                var them = s.Find(other);
                if (them == null || other == player) continue;
                // An oath binds while both are in the house to nominate or vote: once either has
                // left without it breaking, it was never broken, and it never will be.
                bool live = playerIn && them.status == ContestantStatus.Active;
                var c = new Commitment
                {
                    kind = Kinds.Oath, id = OathId(o), withId = other, yours = o.playerId == player,
                    title = o.playerId == player ? "Your loyalty oath" : "Their loyalty oath",
                    binds = OathBinds, week = o.week, untilWeek = 0,
                    outcome = live ? Outcomes.Open : Outcomes.Kept,
                    status = live ? "standing" : "never broken",
                };
                c.term = Term(s, c);
                into.Add(c);
            }
            // A broken oath leaves the list. Its breach stays on the player's own arc with the other
            // party, in the words the oath's rule wrote it: "Broke loyalty oath by nominating you in
            // week 4" when they broke it, the player's victim named when the player did.
            if (s.relationshipArcs == null) return;
            foreach (var arc in s.relationshipArcs)
            {
                if (arc?.weeklyHistory == null || arc.npcId == player || s.Find(arc.npcId) == null) continue;
                foreach (var entry in arc.weeklyHistory)
                {
                    if (entry?.reason == null || !entry.reason.StartsWith(OathBreachReason, StringComparison.Ordinal)) continue;
                    bool theirs = entry.reason.EndsWith(" you in week " + entry.week, StringComparison.Ordinal);
                    bool vote = entry.reason.StartsWith(OathBreachReason + "voting", StringComparison.Ordinal);
                    var c = new Commitment
                    {
                        kind = Kinds.Oath, id = "oath-broken:" + arc.npcId + ":" + entry.week, withId = arc.npcId, yours = true,
                        title = "Your loyalty oath", binds = OathBinds, week = 0, untilWeek = 0,
                        outcome = Outcomes.Broken, brokenById = theirs ? arc.npcId : player, settledWeek = entry.week,
                        status = (theirs ? "broken by them" : "broken by you") + (vote ? " with a vote" : " with a nomination")
                            + " in week " + entry.week,
                    };
                    c.term = Term(s, c);
                    into.Add(c);
                }
            }
        }

        private const string OathBinds = "never to nominate or vote out each other";

        /// <summary>An oath's id on this page: the declaration it is, by who made it and to whom.</summary>
        public static string OathId(WebOathRecord o) => o == null ? null : "oath:" + o.playerId + ":" + o.targetId;

        private static void AddCalls(EpisodeState s, List<Commitment> into)
        {
            string player = s.playerId;
            if (s.ledger?.calls == null) return;
            foreach (var call in s.ledger.calls)
            {
                if (call == null || call.callerId != player) continue;
                var pact = s.alliances.FirstOrDefault(a => a.id == call.allianceId);
                string pactName = pact != null && !string.IsNullOrEmpty(pact.name) ? pact.name : "your alliance";
                // A call binds that week's vote, and the vote is over once its eviction is.
                bool revealed = call.week < s.week || (call.week == s.week && s.evictionResolved);
                var members = (call.followed ?? new List<string>()).Concat(call.defected ?? new List<string>()).Distinct().ToList();
                foreach (var member in members)
                {
                    if (member == player || s.Find(member) == null) continue;
                    bool followed = call.followed != null && call.followed.Contains(member);
                    var c = new Commitment
                    {
                        kind = Kinds.Call, id = "call:" + call.allianceId + ":" + call.week + ":" + member,
                        withId = member, aboutId = s.Find(call.targetId) != null ? call.targetId : null, yours = true,
                        title = "Your call in " + pactName,
                        binds = "their vote to evict " + First(s, call.targetId),
                        week = call.week, untilWeek = call.week,
                        outcome = !followed ? Outcomes.Broken : revealed ? Outcomes.Kept : Outcomes.Open,
                        brokenById = followed ? null : member,
                        settledWeek = !followed || revealed ? call.week : 0,
                        status = !followed ? "ignored it" : revealed ? "followed it" : "with you",
                    };
                    c.term = Term(s, c);
                    into.Add(c);
                }
            }
        }

        private static string PromiseOutcome(PromiseStatus status)
        {
            switch (status)
            {
                case PromiseStatus.Fulfilled: return Outcomes.Kept;
                case PromiseStatus.Broken: return Outcomes.Broken;
                case PromiseStatus.Expired: return Outcomes.Lapsed;
                default: return Outcomes.Open;
            }
        }

        private static string DealOutcome(string status)
        {
            switch (status)
            {
                case DealStatus.Fulfilled: return Outcomes.Kept;
                case DealStatus.Broken: return Outcomes.Broken;
                case DealStatus.Declined:
                case DealStatus.Expired: return Outcomes.Lapsed;
                default: return Outcomes.Open;
            }
        }

        /// <summary>A promise's kind as the page and the warnings name it.</summary>
        public static string PromiseNoun(PromiseKind kind)
        {
            switch (kind)
            {
                case PromiseKind.Safety: return "promise of safety";
                case PromiseKind.Vote: return "vote promise";
                case PromiseKind.FinalTwo: return "final two promise";
                case PromiseKind.AllianceLoyalty: return "promise of alliance loyalty";
                case PromiseKind.Information: return "promise of information";
                default: return "promise";
            }
        }

        private static string PromiseBinds(EpisodeState s, PromiseState p, bool yours)
        {
            switch (p.kind)
            {
                case PromiseKind.Safety: return yours ? "not to nominate them" : "not to nominate you";
                case PromiseKind.Vote:
                    string whose = yours ? "your vote" : "their vote";
                    return s.Find(p.targetId) != null ? whose + " to evict " + First(s, p.targetId) : whose;
                case PromiseKind.FinalTwo: return yours ? "to take them to the final two" : "to take you to the final two";
                case PromiseKind.AllianceLoyalty: return "not to nominate an ally";
                case PromiseKind.Information: return "to share information";
                default: return "their word";
            }
        }

        /// <summary>A deal's kind as the page and the warnings name it, in lower case: "safety deal", "veto deal".</summary>
        public static string DealNoun(string type)
        {
            switch (type)
            {
                case DealKind.TargetAgreement: return "target deal";
                case DealKind.SafetyAgreement: return "safety deal";
                case DealKind.VoteTogether: return "voting-block deal";
                case DealKind.VoteSave: return "vote-to-save deal";
                case DealKind.VoteEvict: return "vote-to-evict deal";
                case DealKind.VetoUse: return "veto deal";
                case DealKind.InformationSharing: return "information deal";
                case DealKind.FinalTwo: return "final two deal";
                case DealKind.AllianceInvite: return "alliance invitation";
                default: return "partnership";
            }
        }

        private static string DealBinds(EpisodeState s, DealState d)
        {
            bool named = s.Find(d.targetId) != null;
            switch (d.type)
            {
                case DealKind.TargetAgreement: return named ? "to put " + First(s, d.targetId) + " up, not each other" : "to put the same person up";
                case DealKind.SafetyAgreement: return "not to nominate each other";
                case DealKind.VoteTogether: return "to vote the same way";
                case DealKind.VoteSave: return named ? "to vote to keep " + First(s, d.targetId) : "to vote to keep somebody";
                case DealKind.VoteEvict: return named ? "to vote to evict " + First(s, d.targetId) : "to vote to evict somebody";
                // The recipient is the one asked, and the veto is asked of whoever holds it.
                case DealKind.VetoUse: return d.recipientId == s.playerId ? "to use the veto on them" : "to use the veto on you";
                case DealKind.InformationSharing: return "to share what you each hear";
                case DealKind.FinalTwo: return "to take each other to the final two";
                case DealKind.AllianceInvite: return "to join forces in an alliance";
                default: return "to work together";
            }
        }

        private static string DealStatusWord(DealState d, bool yours, string brokenBy, string player)
        {
            switch (d.status)
            {
                case DealStatus.Proposed: return yours ? "waiting on them" : "waiting on you";
                case DealStatus.Fulfilled: return "honoured";
                case DealStatus.Broken:
                    if (brokenBy == player) return "broken by you";
                    if (brokenBy != null) return "broken by them";
                    // Both of them settle a voting block at once; anything else is broken, blaming nobody.
                    return d.type == DealKind.VoteTogether ? "fell apart" : "broken";
                case DealStatus.Declined: return yours ? "they declined" : "you declined";
                case DealStatus.Expired: return "lapsed";
                default: return "agreed";
            }
        }

        // ------------------------------------------------------------ the dry run

        /// <summary>The decisions the screens warn about.</summary>
        public static class DecisionKinds
        {
            public const string Nominate = "nominate", Veto = "veto", Vote = "vote", FinalEviction = "final-eviction";
        }

        /// <summary>What part of a decision breaks a commitment, for the warning's words.</summary>
        public static class Acts
        {
            public const string Nominate = "nominate", Save = "save", Decline = "decline", Replace = "replace", Vote = "vote", Take = "take";
        }

        /// <summary>A decision the player is about to make, in the engine's own terms.</summary>
        public sealed class Decision
        {
            public string kind;
            /// <summary>
            /// The two nominees; the veto's saved nominee and replacement; the ballot's target; the
            /// finalist the final Head of Household evicts.
            /// </summary>
            public string firstId, secondId;
            public bool useVeto;

            public static Decision Nominate(string first, string second) =>
                new Decision { kind = DecisionKinds.Nominate, firstId = first, secondId = second };

            /// <summary>The veto: used to save <paramref name="saved"/>, with <paramref name="replacement"/> named in their place by a player Head of Household; or not used.</summary>
            public static Decision Veto(bool use, string saved = null, string replacement = null) =>
                new Decision { kind = DecisionKinds.Veto, useVeto = use, firstId = use ? saved : null, secondId = use ? replacement : null };

            public static Decision Vote(string target) => new Decision { kind = DecisionKinds.Vote, firstId = target };

            /// <summary>The final Head of Household evicting <paramref name="evicted"/>, and so taking the other finalist.</summary>
            public static Decision FinalEviction(string evicted) => new Decision { kind = DecisionKinds.FinalEviction, firstId = evicted };
        }

        /// <summary>A commitment a decision would break, and the part of the decision that breaks it.</summary>
        public sealed class Breach
        {
            /// <summary><see cref="Kinds"/>, and the id <see cref="Of"/> gives the commitment.</summary>
            public string kind, id;
            /// <summary>Who it is with; who it names, where it names somebody.</summary>
            public string withId, aboutId;
            /// <summary>The deal's type, or the promise's kind by name; null for an oath.</summary>
            public string subtype;
            /// <summary><see cref="Acts"/>, and the houseguest that part of the decision is about: the nominee, the one saved, the replacement, the ballot's target, the finalist taken. Null for not using the veto.</summary>
            public string act, causeId;
        }

        /// <summary>
        /// Every commitment of the player's that <paramref name="decision"/> would break, judged as
        /// the engine judges it. Empty when it breaks nothing, and when the decision is not one the
        /// player can make now. Never writes to <paramref name="state"/>.
        /// </summary>
        public static List<Breach> WouldBreak(EpisodeState state, Decision decision)
        {
            var none = new List<Breach>();
            if (state == null || decision == null || state.Find(state.playerId) == null) return none;
            if (decision.kind == DecisionKinds.Vote) return ByTheRules(state, decision);
            var command = CommandFor(state, decision);
            if (command == null) return none;
            CommandResult result;
            try
            {
                // The engine clones what it is given, and so does every command it applies: the
                // season this was read from is never touched, and the copy's stream is its own.
                result = new EpisodeEngine(state).Apply(command);
            }
            catch (Exception)
            {
                // A season the engine will not hold, or a run it cannot finish, is judged by the
                // rules instead: a warning is a reading, and a reading must never take a screen down.
                return ByTheRules(state, decision);
            }
            if (result == null || !result.accepted || result.state == null) return none;
            return Settled(state, result.state, decision);
        }

        private static EpisodeCommand CommandFor(EpisodeState s, Decision d)
        {
            var command = new EpisodeCommand
            {
                id = "dry-run:" + d.kind + ":" + s.revision, actorId = s.playerId,
                expectedRevision = s.revision, expectedPhase = s.phase,
            };
            switch (d.kind)
            {
                case DecisionKinds.Nominate:
                    command.kind = EpisodeCommandKind.Nominate;
                    command.targetId = d.firstId; command.secondTargetId = d.secondId;
                    return command;
                case DecisionKinds.Veto:
                    command.kind = EpisodeCommandKind.ResolveVeto;
                    command.useVeto = d.useVeto;
                    command.targetId = d.useVeto ? d.firstId : null;
                    command.secondTargetId = d.useVeto ? d.secondId : null;
                    return command;
                case DecisionKinds.FinalEviction:
                    command.kind = EpisodeCommandKind.FinalEvict;
                    command.targetId = d.firstId;
                    return command;
                default:
                    return null;
            }
        }

        /// <summary>What the engine's run broke of the player's own word: the records that were standing before it and broken after.</summary>
        private static List<Breach> Settled(EpisodeState before, EpisodeState after, Decision d)
        {
            var breaches = new List<Breach>();
            string player = before.playerId;
            var deals = after.deals.ToDictionary(x => x.id, StringComparer.Ordinal);
            foreach (var deal in before.deals)
            {
                if (deal.status != DealStatus.Active || (deal.proposerId != player && deal.recipientId != player)) continue;
                if (!deals.TryGetValue(deal.id, out var now) || now.status != DealStatus.Broken) continue;
                if (!SettledByThePlayer(before, d, deal.type)) continue;
                breaches.Add(DealBreach(before, deal, d));
            }
            var promises = after.promises.ToDictionary(x => x.id, StringComparer.Ordinal);
            foreach (var promise in before.promises)
            {
                if (promise.status != PromiseStatus.Active || promise.fromId != player) continue;
                if (!promises.TryGetValue(promise.id, out var now) || now.status != PromiseStatus.Broken) continue;
                breaches.Add(PromiseBreach(before, promise, d));
            }
            foreach (var oath in before.loyaltyOaths.Where(o => o.playerId == player || o.targetId == player))
                if (!after.loyaltyOaths.Any(o => o.playerId == oath.playerId && o.targetId == oath.targetId && o.week == oath.week && o.timestamp == oath.timestamp))
                    breaches.Add(OathBreach(before, oath, d));
            return breaches;
        }

        /// <summary>
        /// Whether a deal of this type is settled by the player's own part in the decision, not by a
        /// houseguest's: the player's nominations as Head of Household (a replacement among them),
        /// the player's own veto, the player's own final choice. A houseguest Head of Household's
        /// replacement is theirs, and whom they would name is not the player's to know.
        /// </summary>
        private static bool SettledByThePlayer(EpisodeState s, Decision d, string type)
        {
            bool nomination = type == DealKind.SafetyAgreement || type == DealKind.TargetAgreement;
            switch (d.kind)
            {
                case DecisionKinds.Nominate: return nomination;
                case DecisionKinds.Veto: return (type == DealKind.VetoUse && s.vetoHolderId == s.playerId) || (nomination && s.hohId == s.playerId);
                case DecisionKinds.FinalEviction: return type == DealKind.FinalTwo;
                default: return false;
            }
        }

        /// <summary>
        /// The same judgement made by the rules the engine applies, on a copy: the nomination's,
        /// the veto's and the final choice's verdicts from <see cref="DealResolution"/>, the promise
        /// checks of the nomination, the reveal and the final eviction, and the oath's. The ballot
        /// is judged only on the player's own ballot: a voting block is settled by both partners'
        /// ballots at once, and the partner's is private until the reveal, so the dry run never
        /// warns about one.
        /// </summary>
        public static List<Breach> ByTheRules(EpisodeState state, Decision decision)
        {
            var breaches = new List<Breach>();
            if (state == null || decision == null || state.Find(state.playerId) == null) return breaches;
            var s = state.Clone();
            string player = s.playerId;
            switch (decision.kind)
            {
                case DecisionKinds.Nominate:
                    if (s.phase != EpisodePhase.Nomination || s.hohId != player || s.nominees.Count > 0) return breaches;
                    var pair = new List<string> { decision.firstId, decision.secondId };
                    if (decision.firstId == decision.secondId || pair.Any(id => !EpisodeEngine.NominationCandidates(s).Any(c => c.id == id))) return breaches;
                    foreach (var verdict in DealResolution.Verdicts(s, DealResolution.Nominates, player, pair))
                        if (verdict.status == DealStatus.Broken) breaches.Add(DealBreach(s, verdict.deal, decision));
                    NominationRules(s, pair, decision, breaches);
                    return breaches;
                case DecisionKinds.Veto:
                    if (s.phase != EpisodePhase.VetoMeeting || s.vetoResolved || (s.vetoHolderId != player && s.hohId != player)) return breaches;
                    // The decisions the engine would take, and no others: a houseguest's save as it
                    // stands, a used veto only where it can be used, a player's replacement from those
                    // who can go up.
                    if (s.vetoHolderId != player && (!decision.useVeto || decision.firstId != EpisodeEngine.NpcVetoSave(s))) return breaches;
                    if (decision.useVeto)
                    {
                        var candidates = EpisodeEngine.ReplacementCandidates(s).Select(c => c.id).ToList();
                        if (EpisodeEngine.VetoIsLockedAtFinalFour(s) || !s.nominees.Contains(decision.firstId ?? "") || candidates.Count == 0) return breaches;
                        if (s.hohId == player && !candidates.Contains(decision.secondId ?? "")) return breaches;
                    }
                    if (s.vetoHolderId == player)
                        foreach (var verdict in DealResolution.Verdicts(s, DealResolution.Vetoes, player, s.nominees.ToList(),
                                     decision.useVeto ? decision.firstId : null, decision.useVeto))
                            if (verdict.status == DealStatus.Broken) breaches.Add(DealBreach(s, verdict.deal, decision));
                    if (decision.useVeto && s.hohId == player && !string.IsNullOrEmpty(decision.secondId))
                    {
                        var named = new List<string> { decision.secondId };
                        foreach (var verdict in DealResolution.Verdicts(s, DealResolution.Nominates, player, named))
                            if (verdict.status == DealStatus.Broken) breaches.Add(DealBreach(s, verdict.deal, decision));
                        NominationRules(s, named, decision, breaches);
                    }
                    return breaches;
                case DecisionKinds.Vote:
                    return BallotRules(s, decision, breaches);
                case DecisionKinds.FinalEviction:
                    if (s.phase != EpisodePhase.FinalEviction || s.hohId != player) return breaches;
                    string selected = Taken(s, decision.firstId);
                    if (selected == null) return breaches;
                    foreach (var verdict in DealResolution.Verdicts(s, DealResolution.Selects, player, selectedId: selected))
                        if (verdict.status == DealStatus.Broken) breaches.Add(DealBreach(s, verdict.deal, decision));
                    foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.FinalTwo && p.fromId == player))
                        if (promise.toId != selected) breaches.Add(PromiseBreach(s, promise, decision));
                    return breaches;
                default:
                    return breaches;
            }
        }

        /// <summary>
        /// The nomination's promise and oath checks for each name in order, as the engine runs them:
        /// a promise of safety to the one named, a promise of alliance loyalty when the one named is
        /// an ally, and an oath between the two. Each record breaks once.
        /// </summary>
        private static void NominationRules(EpisodeState s, List<string> named, Decision decision, List<Breach> breaches)
        {
            string player = s.playerId;
            var promisesBroken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var nominee in named)
            {
                foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.fromId == player
                             && ((p.kind == PromiseKind.Safety && p.toId == nominee) || (p.kind == PromiseKind.AllianceLoyalty && s.Allied(player, nominee)))))
                    if (promisesBroken.Add(promise.id)) breaches.Add(PromiseBreach(s, promise, decision, nominee));
                var oath = s.loyaltyOaths.FirstOrDefault(o => (o.playerId == player && o.targetId == nominee) || (o.playerId == nominee && o.targetId == player));
                if (oath != null && !breaches.Any(b => b.kind == Kinds.Oath && b.id == OathId(oath))) breaches.Add(OathBreach(s, oath, decision));
            }
        }

        /// <summary>
        /// The ballot, judged on the player's own: every standing vote promise of theirs that names
        /// somebody else, an oath with the nominee, and - under the levers - a vote deal the
        /// player's ballot alone breaks (DealResolution.VoteDeal reads only the ballots it is given).
        /// </summary>
        private static List<Breach> BallotRules(EpisodeState s, Decision decision, List<Breach> breaches)
        {
            string player = s.playerId, target = decision.firstId;
            if (string.IsNullOrEmpty(target) || !s.nominees.Contains(target) || target == player) return breaches;
            foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.Vote && p.fromId == player))
                if (promise.targetId != target) breaches.Add(PromiseBreach(s, promise, decision));
            var oath = s.loyaltyOaths.FirstOrDefault(o => (o.playerId == player && o.targetId == target) || (o.playerId == target && o.targetId == player));
            if (oath != null) breaches.Add(OathBreach(s, oath, decision));
            if (!EpisodeEngine.LeverRulesOn(s)) return breaches;
            var ballot = new List<VoteState> { new VoteState { voterId = player, targetId = target, reason = "dry run" } };
            foreach (var deal in s.deals.Where(d => d.status == DealStatus.Active && (d.proposerId == player || d.recipientId == player)))
            {
                if (deal.type != DealKind.VoteSave && deal.type != DealKind.VoteEvict) continue;
                if (DealResolution.VoteDeal(deal, ballot, s.nominees, out string actor) == DealStatus.Broken && actor == player)
                    breaches.Add(DealBreach(s, deal, decision));
            }
            return breaches;
        }

        /// <summary>The finalist the final Head of Household takes by evicting <paramref name="evicted"/>: the third of the three.</summary>
        private static string Taken(EpisodeState s, string evicted)
        {
            var others = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            if (others.Count != 2 || !others.Contains(evicted ?? "")) return null;
            return others.First(id => id != evicted);
        }

        private static Breach DealBreach(EpisodeState s, DealState deal, Decision d)
        {
            string partner = DealResolution.Partner(deal, s.playerId);
            var breach = new Breach
            {
                kind = Kinds.Deal, id = deal.id, withId = partner, subtype = deal.type,
                aboutId = DealKind.NamesATarget(deal.type) ? deal.targetId : null,
            };
            switch (d.kind)
            {
                case DecisionKinds.Nominate: breach.act = Acts.Nominate; breach.causeId = partner; break;
                case DecisionKinds.Veto:
                    if (deal.type == DealKind.VetoUse) { breach.act = d.useVeto ? Acts.Save : Acts.Decline; breach.causeId = d.useVeto ? d.firstId : null; }
                    else { breach.act = Acts.Replace; breach.causeId = d.secondId; }
                    break;
                case DecisionKinds.Vote: breach.act = Acts.Vote; breach.causeId = d.firstId; break;
                case DecisionKinds.FinalEviction: breach.act = Acts.Take; breach.causeId = Taken(s, d.firstId); break;
            }
            return breach;
        }

        private static Breach PromiseBreach(EpisodeState s, PromiseState promise, Decision d, string nominee = null)
        {
            var breach = new Breach
            {
                kind = Kinds.Promise, id = promise.id, withId = promise.toId, subtype = promise.kind.ToString(),
                aboutId = promise.kind == PromiseKind.Vote ? promise.targetId : null,
            };
            switch (d.kind)
            {
                case DecisionKinds.Nominate:
                    breach.act = Acts.Nominate;
                    breach.causeId = nominee ?? (promise.kind == PromiseKind.Safety ? promise.toId
                        : new[] { d.firstId, d.secondId }.FirstOrDefault(id => id != null && s.Allied(s.playerId, id)));
                    break;
                case DecisionKinds.Veto: breach.act = Acts.Replace; breach.causeId = d.secondId; break;
                case DecisionKinds.Vote: breach.act = Acts.Vote; breach.causeId = d.firstId; break;
                case DecisionKinds.FinalEviction: breach.act = Acts.Take; breach.causeId = Taken(s, d.firstId); break;
            }
            return breach;
        }

        private static Breach OathBreach(EpisodeState s, WebOathRecord oath, Decision d)
        {
            string other = oath.playerId == s.playerId ? oath.targetId : oath.playerId;
            return new Breach
            {
                kind = Kinds.Oath, id = OathId(oath), withId = other, causeId = other,
                act = d.kind == DecisionKinds.Veto ? Acts.Replace : d.kind == DecisionKinds.Vote ? Acts.Vote : Acts.Nominate,
            };
        }

        // ------------------------------------------------------------ the warning's words

        /// <summary>
        /// The warning for a screen: every commitment the breaches name, with the choices that
        /// break it - "Nominating Alex breaks your safety deal with him." "Saving Bea or not using
        /// the veto breaks your veto deal with Alex." Commitments broken by the same choices share a
        /// sentence. Null when nothing breaks.
        /// </summary>
        public static string Warning(EpisodeState s, IEnumerable<Breach> breaches)
        {
            var list = breaches?.Where(b => b != null).ToList();
            if (s == null || list == null || list.Count == 0) return null;
            var found = new List<Broken>();
            foreach (var breach in list)
            {
                string key = breach.kind + ":" + breach.id;
                var entry = found.FirstOrDefault(e => e.Key == key);
                if (entry == null) found.Add(entry = new Broken { Key = key, First = breach });
                string lead = breach.act + ":" + breach.causeId;
                if (!entry.Leads.Any(l => l.act + ":" + l.causeId == lead)) entry.Leads.Add(breach);
            }
            var sentences = new List<string>();
            foreach (var group in found.GroupBy(e => string.Join("|", e.Leads.Select(l => l.act + ":" + l.causeId))))
            {
                var leads = group.First().Leads;
                // The one houseguest a pronoun can stand for: the only one the choice is about.
                string them = leads.Count == 1 ? leads[0].causeId : null;
                string lead = JoinWith(leads.Select(l => Lead(s, l.act, l.causeId)).ToList(), "or");
                var phrases = group.Select(e => Phrase(s, e.First, them)).Distinct().ToList();
                sentences.Add(Capitalise(lead) + " breaks " + JoinWith(phrases, "and") + ".");
            }
            return string.Join(" ", sentences);
        }

        private sealed class Broken
        {
            public string Key;
            public Breach First;
            public readonly List<Breach> Leads = new List<Breach>();
        }

        private static string Lead(EpisodeState s, string act, string causeId)
        {
            switch (act)
            {
                case Acts.Nominate: return "nominating " + First(s, causeId);
                case Acts.Save: return "saving " + First(s, causeId);
                case Acts.Decline: return "not using the veto";
                case Acts.Replace: return "naming " + First(s, causeId) + " as the replacement";
                case Acts.Vote: return "voting to evict " + First(s, causeId);
                case Acts.Take: return "taking " + First(s, causeId) + " to the Final 2";
                default: return "this choice";
            }
        }

        private static string Phrase(EpisodeState s, Breach b, string them)
        {
            string who = Who(s, b.withId, them);
            switch (b.kind)
            {
                case Kinds.Deal:
                    bool named = s.Find(b.aboutId) != null;
                    if (b.subtype == DealKind.TargetAgreement && named) return "your deal with " + who + " to put " + Who(s, b.aboutId, them) + " up";
                    if (b.subtype == DealKind.VoteSave && named) return "your deal with " + who + " to keep " + Who(s, b.aboutId, them);
                    if (b.subtype == DealKind.VoteEvict && named) return "your deal with " + who + " to evict " + Who(s, b.aboutId, them);
                    return "your " + DealNoun(b.subtype) + " with " + who;
                case Kinds.Promise:
                    if (b.subtype == PromiseKind.Vote.ToString())
                        return s.Find(b.aboutId) != null ? "your promise to " + who + " to vote out " + Who(s, b.aboutId, them) : "your vote promise to " + who;
                    return Enum.TryParse(b.subtype, out PromiseKind kind) ? "your " + PromiseNoun(kind) + " to " + who : "your promise to " + who;
                case Kinds.Oath:
                    return "your loyalty oath to " + who;
                default:
                    return "your word to " + who;
            }
        }

        /// <summary>A houseguest as a warning says them: by the pronoun when the choice is about them alone, by first name otherwise.</summary>
        private static string Who(EpisodeState s, string id, string them)
        {
            var who = s.Find(id);
            if (who == null) return "somebody";
            if (id == them) return StoryPeople.Pronouns(who).them;
            return First(s, id);
        }

        private static string First(EpisodeState s, string id)
        {
            var who = s?.Find(id);
            return who == null ? "somebody" : FinalistRead.FirstName(who.name);
        }

        /// <summary>"a", "a or b", "a, b or c".</summary>
        private static string JoinWith(IList<string> items, string word)
        {
            if (items.Count == 0) return string.Empty;
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.Take(items.Count - 1)) + " " + word + " " + items[items.Count - 1];
        }

        private static string Capitalise(string words) =>
            string.IsNullOrEmpty(words) ? words : char.ToUpperInvariant(words[0]) + words.Substring(1);
    }
}
