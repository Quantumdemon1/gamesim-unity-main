using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Sticky relationships: ride-or-die, confidant, nemesis, showmance. They change only through
    /// beats, never on a wobbling score, and each has named readers: the vote, the veto save, the
    /// nomination sort, the final HoH's choice and the jury.
    /// </summary>
    public static class Bonds
    {
        private static bool Pair(BondState b, string x, string y) =>
            (b.aId == x && b.bId == y) || (b.aId == y && b.bId == x);

        /// <summary>A bond of this kind between two people that still holds, or null.</summary>
        public static BondState Between(EpisodeState state, string a, string b, string kind) =>
            state?.story?.bonds.FirstOrDefault(x => x.kind == kind && Pair(x, a, b) && BondStatus.Holds(x.status));

        public static bool Holds(EpisodeState state, string a, string b, string kind) => Between(state, a, b, kind) != null;

        /// <summary>A showmance partner or a ride-or-die: somebody you do not put up and do not vote out.</summary>
        public static bool Partner(EpisodeState state, string a, string b) =>
            Holds(state, a, b, BondKinds.Showmance) || Holds(state, a, b, BondKinds.RideOrDie);

        public static bool Nemesis(EpisodeState state, string a, string b) => Holds(state, a, b, BondKinds.Nemesis);

        /// <summary>Somebody's showmance partner, if they have one.</summary>
        public static string ShowmancePartner(EpisodeState state, string id)
        {
            var bond = state?.story?.bonds.FirstOrDefault(x => x.kind == BondKinds.Showmance && BondStatus.Holds(x.status)
                                                               && (x.aId == id || x.bId == id));
            return bond == null ? null : bond.aId == id ? bond.bId : bond.aId;
        }

        /// <summary>Forms a bond, or updates the status of one that exists.</summary>
        public static BondState Form(EpisodeState state, string a, string b, string kind, string status, string cycleId)
        {
            if (state?.story == null || a == b || !BondKinds.IsKnown(kind)) return null;
            var existing = Between(state, a, b, kind);
            if (existing != null) { existing.status = status; return existing; }
            if (state.story.bonds.Count >= 64) return null;
            var bond = new BondState
            {
                id = "bond-" + state.nextSequence++, kind = kind, aId = a, bId = b, status = status,
                sinceWeek = state.week, endedWeek = 0, cycleId = cycleId,
            };
            state.story.bonds.Add(bond);
            return bond;
        }

        /// <summary>Ends a bond. A betrayal is recorded as one, because the jury reads it differently from a quiet end.</summary>
        public static void End(EpisodeState state, string a, string b, string kind, bool betrayed)
        {
            var bond = Between(state, a, b, kind);
            if (bond == null) return;
            bond.status = betrayed ? BondStatus.Betrayed : BondStatus.Ended;
            bond.endedWeek = state.week;
        }

        /// <summary>
        /// Somebody left the house: their bonds move to "apart". A showmance partner on the jury
        /// still reads as a kept or a betrayed showmance.
        /// </summary>
        public static void Apart(EpisodeState state, string id)
        {
            if (state?.story == null) return;
            foreach (var bond in state.story.bonds.Where(x => (x.aId == id || x.bId == id) && BondStatus.Holds(x.status)))
                bond.status = BondStatus.Apart;
        }

        /// <summary>Everybody production removed simply loses their bonds.</summary>
        public static void EndAll(EpisodeState state, string id)
        {
            if (state?.story == null) return;
            foreach (var bond in state.story.bonds.Where(x => (x.aId == id || x.bId == id) && BondStatus.Holds(x.status)))
            {
                bond.status = BondStatus.Ended;
                bond.endedWeek = state.week;
            }
        }

        /// <summary>Whether a bond between these two ended in a betrayal.</summary>
        public static bool Betrayed(EpisodeState state, string a, string b, string kind) =>
            state?.story?.bonds.Any(x => x.kind == kind && Pair(x, a, b) && x.status == BondStatus.Betrayed) == true;
    }

    /// <summary>
    /// Who knows what. Facts are born only from things that already happen - an alliance forming,
    /// a couple, a broken word, a strike, a story outcome - and reach the player only through what
    /// the player does or witnesses.
    /// </summary>
    public static class Knowledge
    {
        /// <summary>A fact about something a cycle did, found by kind.</summary>
        public static HouseFactState Of(EpisodeState state, string kind, string refId) =>
            state?.story?.facts.FirstOrDefault(f => f.kind == kind && f.refId == refId);

        public static bool Knows(HouseFactState fact, string who) =>
            fact != null && (fact.visibility == FactVisibility.Public || fact.knowers.Contains(who));

        /// <summary>Records a fact with its first knowers: whoever did it, whoever it was about, and anyone named.</summary>
        public static HouseFactState Create(EpisodeState state, string kind, string actorId, string subjectId, string visibility,
            StorylineState cycle, string extraKnower = null)
        {
            if (state?.story == null || !FactKinds.IsKnown(kind)) return null;
            // A couple is known by its showmance bond, which is how the nomination sort finds it;
            // every other story fact by the cycle that made it.
            string refId = kind == FactKinds.Couple ? StoryConsumers.CoupleRef(state, actorId, subjectId) ?? cycle?.id : cycle?.id;
            var existing = refId == null ? null : Of(state, kind, refId);
            if (existing != null)
            {
                if (FactVisibility.IsKnown(visibility)) existing.visibility = Wider(existing.visibility, visibility);
                AddKnower(state, existing, extraKnower);
                return existing;
            }
            MakeRoom(state);
            var fact = new HouseFactState
            {
                id = "fact-" + state.nextSequence++, kind = kind, actorId = actorId, subjectId = subjectId, refId = refId,
                visibility = FactVisibility.IsKnown(visibility) ? visibility : FactVisibility.Private, week = state.week,
            };
            foreach (var id in new[] { actorId, subjectId, extraKnower }) AddKnower(state, fact, id);
            if (fact.visibility == FactVisibility.Public)
                foreach (var c in state.Active) AddKnower(state, fact, c.id);
            state.story.facts.Add(fact);
            return fact;
        }

        /// <summary>An alliance's private fact: its members are the knowers.</summary>
        public static void AllianceFormed(EpisodeState state, AllianceState alliance)
        {
            if (state?.story == null || alliance == null) return;
            if (state.story.facts.Any(f => f.kind == FactKinds.Alliance && f.refId == alliance.id)) return;
            MakeRoom(state);
            var fact = new HouseFactState
            {
                id = "fact-" + state.nextSequence++, kind = FactKinds.Alliance, actorId = alliance.members.FirstOrDefault(),
                subjectId = alliance.members.Skip(1).FirstOrDefault(), refId = alliance.id, visibility = FactVisibility.Private,
                week = state.week,
            };
            foreach (var member in alliance.members) AddKnower(state, fact, member);
            state.story.facts.Add(fact);
        }

        /// <summary>
        /// A deal broken as house knowledge (ACTIONS-DEALS-ALLIANCES-PLAN C8): the one who broke it its
        /// actor, the one it was broken against its subject, the deal its reference - one fact a deal -
        /// out as a whisper, so the house's own gossip carries it (<see cref="Spread"/>), and known at
        /// first to the two of them and nobody else: the deal was struck between them, and an act the
        /// house watches (a nomination, the veto meeting, the final choice) shows the house the act, not
        /// the deal it broke. The engine writes one for a deal the player breaks by such an act, under
        /// the commitment rules where the house keeps knowledge (<see cref="YourWord.On"/>), and never for
        /// a breach a ballot decided. <see cref="YourWord"/> reads them.
        /// </summary>
        public static HouseFactState BrokenWord(EpisodeState state, DealState deal, string breakerId, string wrongedId)
        {
            if (state?.story == null || deal?.id == null || string.IsNullOrEmpty(breakerId) || string.IsNullOrEmpty(wrongedId)
                || breakerId == wrongedId) return null;
            var existing = Of(state, FactKinds.BrokenWord, deal.id);
            if (existing != null) return existing;
            MakeRoom(state);
            var fact = new HouseFactState
            {
                id = "fact-" + state.nextSequence++, kind = FactKinds.BrokenWord, actorId = breakerId, subjectId = wrongedId,
                refId = deal.id, visibility = FactVisibility.Whispered, week = state.week,
            };
            AddKnower(state, fact, breakerId);
            AddKnower(state, fact, wrongedId);
            state.story.facts.Add(fact);
            return fact;
        }

        /// <summary>How many facts the house keeps: validation's bound.</summary>
        public const int Ceiling = 128;

        /// <summary>
        /// Makes room for one more fact when the house's list is full (X14). Before the commitment rules
        /// the oldest went, whatever it was - and a private alliance's fact dropped that way left its pact
        /// with no record, which <see cref="AllianceVisibleTo"/> reads as known to everyone (the legacy
        /// rule), so every voter counted a pact nobody had told them of. A season without the rules still
        /// drops the oldest, so it plays as it did.
        ///
        /// <para>Under the rules the oldest fact that nothing keeps goes: never an alliance's, and never
        /// the player's broken word, which their reading reads (<see cref="YourWord"/>) and which must not
        /// change without a word. Where every fact is one of those, the oldest fact of a pact that no
        /// longer stands goes - no voter weighs an ended pact - then the oldest alliance fact of any, and
        /// the player's broken word only when nothing else is left. None of that is reached in a season:
        /// every pact leaves one fact and every deal the player breaks in front of the house one, and a
        /// season makes nowhere near a hundred and twenty-eight of them.</para>
        /// </summary>
        public static void MakeRoom(EpisodeState state)
        {
            var facts = state?.story?.facts;
            if (facts == null || facts.Count < Ceiling) return;
            int at = 0;
            if (EpisodeEngine.CommitmentRulesOn(state))
            {
                at = facts.FindIndex(f => !KeptWhenFull(state, f));
                if (at < 0) at = facts.FindIndex(f => f.kind == FactKinds.Alliance && !state.alliances.Any(a => a.id == f.refId && a.active));
                if (at < 0) at = facts.FindIndex(f => f.kind == FactKinds.Alliance);
                if (at < 0) at = 0;
            }
            facts.RemoveAt(at);
        }

        /// <summary>A fact a full list keeps under the commitment rules: an alliance's (X14), or the player's broken word (C8).</summary>
        private static bool KeptWhenFull(EpisodeState state, HouseFactState fact) =>
            fact.kind == FactKinds.Alliance || (fact.kind == FactKinds.BrokenWord && fact.actorId == state.playerId);

        /// <summary>
        /// Whether an evaluator can see an alliance. <b>The legacy rule:</b> an alliance with no fact
        /// record is known to everyone, so a save that never reached the knowledge rules - and every
        /// alliance formed before them - counts exactly as it always did. Only an alliance formed
        /// past the boundary, which gets a private fact at birth, is hidden from non-knowers.
        /// </summary>
        public static bool AllianceVisibleTo(EpisodeState state, AllianceState alliance, string evaluatorId)
        {
            if (alliance == null) return false;
            if (!EpisodeEngine.StoryAt(state, StoryRules.Bonds)) return true;
            if (alliance.members.Contains(evaluatorId)) return true;
            var fact = Of(state, FactKinds.Alliance, alliance.id);
            return fact == null || Knows(fact, evaluatorId);
        }

        public static void AddKnower(EpisodeState state, HouseFactState fact, string who)
        {
            if (fact == null || string.IsNullOrEmpty(who) || fact.knowers.Contains(who)) return;
            if (state.Find(who) == null || fact.knowers.Count >= state.contestants.Count) return;
            fact.knowers.Add(who);
        }

        /// <summary>Widens a fact's visibility; a public fact is known to everyone in the house.</summary>
        public static void MakeKnown(EpisodeState state, HouseFactState fact, string visibility)
        {
            if (state?.story == null || fact == null) return;
            fact.visibility = Wider(fact.visibility, FactVisibility.IsKnown(visibility) ? visibility : FactVisibility.Public);
            if (fact.visibility == FactVisibility.Public)
                foreach (var c in state.Active) AddKnower(state, fact, c.id);
        }

        /// <summary>Makes a cycle's fact of a kind more widely known.</summary>
        public static void Publicise(EpisodeState state, StorylineState cycle, string kind, string visibility)
        {
            if (state?.story == null || cycle == null) return;
            var fact = ForCycle(state, cycle, kind);
            if (fact == null) return;
            fact.visibility = Wider(fact.visibility, FactVisibility.IsKnown(visibility) ? visibility : FactVisibility.Public);
            if (fact.visibility == FactVisibility.Public)
                foreach (var c in state.Active) AddKnower(state, fact, c.id);
        }

        /// <summary>A cycle's fact of a kind: by the cycle's id, or for a couple by whoever of its cast is in one.</summary>
        public static HouseFactState ForCycle(EpisodeState state, StorylineState cycle, string kind)
        {
            if (state?.story == null || cycle == null) return null;
            var fact = Of(state, kind, cycle.id);
            if (fact != null || kind != FactKinds.Couple) return fact;
            var ids = cycle.cast.Select(r => r.contestantId).ToList();
            return state.story.facts.LastOrDefault(f => f.kind == FactKinds.Couple
                && (ids.Contains(f.actorId) || ids.Contains(f.subjectId))
                && (f.actorId == state.playerId || f.subjectId == state.playerId || (ids.Contains(f.actorId) && ids.Contains(f.subjectId))));
        }

        private static string Wider(string current, string wanted) =>
            Array.IndexOf(FactVisibility.All, wanted) > Array.IndexOf(FactVisibility.All, current) ? wanted : current;

        /// <summary>
        /// The one-hop rumour mill (M5): once per anchor, each NPC knower of a whispered fact may
        /// tell their warmest non-knower. The odds are the engine's own promise-witness odds - 0.8
        /// allied with the person it hurts, 0.6 close to them, 0.3 close to whoever it is about,
        /// 0.2 otherwise - scaled by how sociable and how discreet the teller is. Keyed, and at most
        /// one new knower per fact per pass, so it spreads like gossip rather than a broadcast.
        /// </summary>
        public static List<(HouseFactState fact, string listener)> Spread(EpisodeState state, string anchor)
        {
            var told = new List<(HouseFactState, string)>();
            if (!EpisodeEngine.StoryAt(state, StoryRules.Bonds)) return told;
            foreach (var fact in state.story.facts.Where(f => f.visibility == FactVisibility.Whispered || f.visibility == FactVisibility.Known)
                         .OrderBy(f => f.id, StringComparer.Ordinal).ToList())
            {
                foreach (var tellerId in fact.knowers.OrderBy(id => id, StringComparer.Ordinal).ToList())
                {
                    var teller = state.Find(tellerId);
                    if (teller == null || teller.isPlayer || teller.status != ContestantStatus.Active) continue;
                    var listener = state.Active.Where(c => c.id != tellerId && !fact.knowers.Contains(c.id))
                        .OrderByDescending(c => state.Score(tellerId, c.id)).ThenBy(c => c.id, StringComparer.Ordinal).FirstOrDefault();
                    if (listener == null) continue;
                    double odds = state.Allied(listener.id, fact.subjectId) ? 0.8
                        : state.Score(listener.id, fact.subjectId ?? "") > 50 ? 0.6
                        : state.Score(listener.id, fact.actorId ?? "") > 50 ? 0.3 : 0.2;
                    var axes = Personality.Of(teller);
                    odds *= Math.Max(0.2, 1 + 0.1 * (axes.Sociable - axes.Honest));
                    if (!StoryRandom.Chance(state, "w" + state.week + ":" + anchor + ":rumour:" + fact.id + ":" + tellerId, odds)) continue;
                    AddKnower(state, fact, listener.id);
                    told.Add((fact, listener.id));
                    break;
                }
            }
            return told;
        }
    }

    /// <summary>A favour owed: one use, spent into a promise or a deal.</summary>
    public static class Hooks
    {
        public static HookState Create(EpisodeState state, string holderId, string overId, string factId)
        {
            if (state?.story == null || holderId == overId) return null;
            if (state.story.hooks.Count >= 32) state.story.hooks.RemoveAll(h => h.spent);
            if (state.story.hooks.Count >= 32) return null;
            var hook = new HookState { id = "hook-" + state.nextSequence++, holderId = holderId, overId = overId, factId = factId, week = state.week };
            state.story.hooks.Add(hook);
            return hook;
        }

        /// <summary>An unspent favour the holder has over somebody.</summary>
        public static HookState Unspent(EpisodeState state, string holderId, string overId) =>
            state?.story?.hooks.FirstOrDefault(h => !h.spent && h.holderId == holderId && h.overId == overId);

        public static bool Has(EpisodeState state, string holderId, string overId) => Unspent(state, holderId, overId) != null;

        public static void Spend(EpisodeState state, string holderId, string overId)
        {
            var hook = Unspent(state, holderId, overId);
            if (hook != null) hook.spent = true;
        }

        public static void Void(EpisodeState state, string id)
        {
            if (state?.story == null) return;
            foreach (var hook in state.story.hooks.Where(h => !h.spent && (h.holderId == id || h.overId == id))) hook.spent = true;
        }
    }

    /// <summary>
    /// Production as a character: the conduct ladder, Have-Nots, and removal.
    ///
    /// <para><b>The ladder is signposted and never rolled.</b> A conduct option always strikes, is
    /// labelled before it is chosen, and needs a second press to confirm. A private Diary Room
    /// warning, then a penalty - a Have-Not week and sitting out the next HoH - then removal, and
    /// removal only in a post-eviction Social window with four or more in the house, at most once a
    /// season. Big Brother USA has removed a handful of houseguests in more than twenty-five
    /// seasons, each for a clear act; this keeps it that rare and that deliberate.</para>
    /// </summary>
    public static class Production
    {
        public static ConductState For(EpisodeState state, string id, bool create)
        {
            var row = state?.story?.conduct.FirstOrDefault(c => c.contestantId == id);
            if (row != null || !create || state?.story == null) return row;
            row = new ConductState { contestantId = id };
            state.story.conduct.Add(row);
            return row;
        }

        public static int Strikes(EpisodeState state, string id) => For(state, id, false)?.strikes ?? 0;

        /// <summary>Whether somebody is one of this week's Have-Nots: the house's own list (<see cref="HaveNots"/>), the only one.</summary>
        public static bool IsHaveNot(EpisodeState state, string id) => HaveNots.Is(state, id);

        /// <summary>Whether somebody sits out this week's HoH competition as a penalty.</summary>
        public static bool SitsOut(EpisodeState state, string id) =>
            state.phase == EpisodePhase.HoH && For(state, id, false)?.sitsOutWeek == state.week && state.week > 0;

        /// <summary>
        /// Whether production could remove somebody now: a post-eviction Social window, four or more
        /// in the house so at least three remain, nobody removed yet this season - and never in a
        /// house of six or fewer, where one removal would be a third of the season.
        /// </summary>
        public static bool RemovalWindow(EpisodeState state) =>
            state.contestants.Count > 6
            && state.phase == EpisodePhase.Social && state.evictionResolved && state.Active.Count() >= 4
            && state.story.removals.Count == 0 && string.IsNullOrEmpty(state.story.pendingRemovalId);

        /// <summary>
        /// A strike, and the rung of the ladder it lands on. Returns the rung: 1 a warning, 2 a
        /// penalty, 3 a removal (or another penalty when there is no window). At most one strike a
        /// person a week: a second in the same week is the same incident.
        /// </summary>
        public static int Strike(EpisodeState state, string id, string reason)
        {
            var who = state.Find(id);
            if (who == null || who.status != ContestantStatus.Active || StoryPeople.IsRealPerson(who)) return 0;
            var row = For(state, id, true);
            if (row.lastStrikeWeek == state.week && row.strikes > 0) return 0;
            row.strikes = Math.Min(10, row.strikes + 1);
            row.lastStrikeWeek = state.week;
            row.cleanWeeks = 0;
            if (!string.IsNullOrEmpty(reason) && row.reasons.Count < 10) row.reasons.Add(reason.Length > 100 ? reason.Substring(0, 100) : reason);
            bool lenient = who.isPlayer && state.story.productionStrictness == StoryRules.Lenient;
            if (row.strikes >= 3 && !lenient && RemovalWindow(state)) { Pending(state, id); return 3; }
            if (row.strikes >= 2) { Penalise(state, id); return 2; }
            return 1;
        }

        /// <summary>
        /// The penalty rung: a Have-Not week and sitting out the next HoH, unless the field would fall
        /// to two. The Have-Not week is the house's own: the next Head of Household competition names
        /// them whatever it says, as the veto's punishment does, so the week they sit out is the week
        /// they spend on slop. A season that plays without Have-Nots keeps the sit-out alone.
        /// </summary>
        public static void Penalise(EpisodeState state, string id)
        {
            if (HaveNots.Apply(state) && state.Find(id)?.status == ContestantStatus.Active
                && !state.punishedHaveNots.Contains(id) && state.punishedHaveNots.Count < 16)
                state.punishedHaveNots.Add(id);
            var row = For(state, id, true);
            // The next HoH competition is next week's; at four or fewer the field would be too small.
            if (state.Active.Count() > 4) row.sitsOutWeek = Math.Min(state.week + 1, 101);
        }

        /// <summary>
        /// Puts somebody on slop now, through the house's own list: this week's, until the next Head
        /// of Household names new ones. What a Have-Not costs is the house's (<see cref="HaveNots"/>):
        /// the veto's point, and a player's conversation. Nothing in a season without Have-Nots.
        /// </summary>
        public static void MakeHaveNot(EpisodeState state, string id)
        {
            var who = state?.Find(id);
            if (who == null || who.status != ContestantStatus.Active || !HaveNots.Apply(state) || IsHaveNot(state, id)) return;
            if (state.haveNots.Count >= 16) return;
            state.haveNots.Add(id);
            Personality.AdjustStress(who, 1);
        }

        public static void Pending(EpisodeState state, string id)
        {
            if (!RemovalWindow(state) || state.Find(id)?.status != ContestantStatus.Active) return;
            state.story.pendingRemovalId = id;
        }

        /// <summary>
        /// A week older: three clean weeks clear a strike (four after pushing back in the Diary
        /// Room). The Have-Not week is the house's, and ends when it names the next.
        /// </summary>
        public static void Age(EpisodeState state)
        {
            if (state?.story == null) return;
            foreach (var row in state.story.conduct)
            {
                if (row.lastStrikeWeek < state.week - 1) row.cleanWeeks = Math.Min(100, row.cleanWeeks + 1);
                if (row.cleanWeeks >= 3 + row.pushedBack && row.strikes > 0) { row.strikes--; row.cleanWeeks = 0; row.pushedBack = 0; }
            }
        }
    }
}
