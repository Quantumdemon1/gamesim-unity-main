using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>The eight things a houseguest can decide to do with a social turn.</summary>
    public enum NpcActionKind
    {
        Talk,
        AllianceProposal,
        Promise,
        AllianceMeeting,
        SpreadInfo,
        Confront,
        Campaign,
        Eavesdrop,
    }

    /// <summary>
    /// Houseguests spending their own social turns.
    ///
    /// <para>The house could already talk — the conversation scheduler has always paired people off
    /// and given them a topic — and after the previous phase it could ally and promise. What it
    /// could not do was anything else. Nobody had ever started a rumour, picked a fight, listened at
    /// a door or campaigned for their own life. A houseguest who can be lied to but cannot lie is
    /// not playing the same game as the player, and the player's vocabulary had just doubled.</para>
    ///
    /// <para>Ported from the reference build's <c>npc-social-behavior.ts</c>: each social phase every
    /// houseguest generates a ranked list of intents and executes the top
    /// <see cref="ActionsPerSocialPhase"/>.</para>
    ///
    /// <para><b>This is the first NPC pass that spends the season's randomness</b>, and it does so
    /// for one reason: <c>selectTalkTarget</c> is weighted sampling rather than argmax. The source is
    /// explicit that this is what stops a houseguest talking to their closest friend every single
    /// time, and a deterministic port of it would quietly delete the variety the system exists to
    /// produce. So targets are drawn through <see cref="EpisodeState.randomState"/>, and the pass is
    /// gated on the same rules boundary the alliance and promise passes use.</para>
    /// </summary>
    public static class NpcSocialActions
    {
        /// <summary>The source's <c>NPC_ACTIONS_PER_SOCIAL_PHASE</c>.</summary>
        public const int ActionsPerSocialPhase = 3;

        // ---------------------------------------------------------------- what the numbers are

        /// <summary>An ordinary conversation, worth what the player's own <c>Talk</c> is worth.</summary>
        public const double TalkImpact = 4;

        /// <summary>
        /// Touching base with an alliance: the source's <c>holdAllianceMeeting</c>, which writes a
        /// decaying <c>alliance_meeting</c> event worth three in both directions between every pair
        /// of members.
        ///
        /// <para>Marked authored here for a while, because the design document schedules alliance
        /// upkeep at priority 50 and never says what it is worth. Reading <c>alliance-system.ts</c>
        /// settled it: the guess and the source agree exactly.</para>
        /// </summary>
        public const double AllianceMeetingImpact = 3;

        /// <summary>The source's interaction table: <c>rumor_spread</c>, and it fades.</summary>
        public const double RumorImpact = -15;

        /// <summary>The source's <c>confrontation</c>. Notably milder than <c>attacked</c> at −20.</summary>
        public const double ConfrontationImpact = -8;

        /// <summary>The source's <c>campaign</c>: begging works a little.</summary>
        public const double CampaignImpact = 5;

        /// <summary>What being caught listening costs, matching the player's own eavesdrop.</summary>
        public const double CaughtEavesdroppingImpact = -8;

        /// <summary>Below this, somebody is a person you would pick a fight with.</summary>
        public const double ConfrontationLine = 0;

        // ---------------------------------------------------------------- personality

        private static readonly NpcActionKind[] Default =
            { NpcActionKind.Talk, NpcActionKind.AllianceProposal, NpcActionKind.Promise };

        /// <summary>
        /// Which verbs come naturally to a houseguest, from the source's <c>getTraitPreferences</c>.
        ///
        /// <para><b>Two of the source's ten traits do not exist in this project, and nine of this
        /// project's seventeen are not among the source's ten.</b> <c>Paranoid</c> and <c>Floater</c>
        /// are carried here anyway — unreachable today, correct if the trait table ever grows — while
        /// <c>Charming</c>, <c>Funny</c>, <c>Manipulative</c>, <c>Impulsive</c>, <c>Deceptive</c>,
        /// <c>Introverted</c>, <c>Stubborn</c>, <c>Flexible</c> and <c>Intuitive</c> fall to the
        /// default. Six of the twenty-four shipped houseguests lead with one of those, so a quarter of
        /// the cast plays the default repertoire. Inventing repertoires for them would be authoring a
        /// personality system rather than porting one, so the gap is left visible instead.</para>
        /// </summary>
        public static IReadOnlyList<NpcActionKind> Repertoire(string trait)
        {
            switch (trait)
            {
                case "Strategic":
                    return new[] { NpcActionKind.AllianceProposal, NpcActionKind.SpreadInfo, NpcActionKind.Talk };
                case "Loyal":
                    return new[] { NpcActionKind.AllianceMeeting, NpcActionKind.Promise, NpcActionKind.Talk };
                case "Sneaky":
                    return new[] { NpcActionKind.SpreadInfo, NpcActionKind.Eavesdrop, NpcActionKind.Talk, NpcActionKind.AllianceProposal };
                case "Social":
                    return new[] { NpcActionKind.Talk, NpcActionKind.AllianceMeeting, NpcActionKind.Promise };
                case "Competitive":
                    return new[] { NpcActionKind.AllianceProposal, NpcActionKind.Confront, NpcActionKind.Talk, NpcActionKind.SpreadInfo };
                case "Paranoid":
                    return new[] { NpcActionKind.Eavesdrop, NpcActionKind.Talk, NpcActionKind.SpreadInfo };
                case "Emotional":
                    return new[] { NpcActionKind.Talk, NpcActionKind.Confront, NpcActionKind.Promise, NpcActionKind.AllianceMeeting };
                case "Floater":
                    return new[] { NpcActionKind.Talk, NpcActionKind.AllianceMeeting };
                case "Confrontational":
                    return new[] { NpcActionKind.Confront, NpcActionKind.Talk, NpcActionKind.SpreadInfo };
                case "Analytical":
                    return new[] { NpcActionKind.AllianceProposal, NpcActionKind.Eavesdrop, NpcActionKind.SpreadInfo, NpcActionKind.Talk };
                default:
                    return Default;
            }
        }

        /// <summary>The trait a houseguest leads with, which is the only one the source reads.</summary>
        public static string LeadTrait(ContestantState npc) =>
            npc != null && npc.traits != null && npc.traits.Count > 0 ? npc.traits[0] : null;

        // ---------------------------------------------------------------- the weekly pass

        /// <summary>
        /// The social week: every houseguest takes up to three turns.
        ///
        /// <para>Alliances and promises are attempted first and unconditionally, whatever the
        /// houseguest's traits, because that is the source's shape: it generates those intents for
        /// everybody and scores them at 80 and 70, above every other verb. Personality breaks ties and
        /// decides what fills the turns left over, which for most houseguests is two of the three. A
        /// <c>Floater</c> still proposes alliances in the reference build, and still does here.</para>
        /// </summary>
        public static void Settle(EpisodeState state)
        {
            if (!NpcSocialState.AutonomyHasBegun(state)) return;
            NpcAlliances.Dissolve(state);

            // Cast order throughout, so the same house always plays the same week.
            var joined = new HashSet<string>(StringComparer.Ordinal);
            var met = new HashSet<string>(StringComparer.Ordinal);
            var turn = Turn.Season(state);

            foreach (var npc in state.contestants
                         .Where(c => c.status == ContestantStatus.Active && !c.isPlayer)
                         .ToList())
            {
                // A Have-Not is on slop and a cot: two turns this week, not three.
                int turns = EpisodeEngine.StoryAt(state, StoryRules.Production) && Production.IsHaveNot(state, npc.id)
                    ? ActionsPerSocialPhase - 1 : ActionsPerSocialPhase;
                int taken = 0;
                if (NpcAlliances.TryPropose(state, npc.id, joined)) taken++;
                if (taken < turns && NpcPromises.TryGive(state, npc.id)) taken++;
                // Under agency the first free turn goes to the agenda (NPC-AGENCY-PLAN.md §4).
                if (taken < turns && PursueOn(state, npc, turn) != null) taken++;

                foreach (var kind in Repertoire(LeadTrait(npc)))
                {
                    if (taken >= turns) break;
                    // Both were already attempted above, at the priority the source gives them.
                    if (kind == NpcActionKind.AllianceProposal || kind == NpcActionKind.Promise) continue;
                    if (Perform(state, npc, kind, met, turn) != null) taken++;
                }
            }
        }

        /// <summary>
        /// One beat of the all-week cadence (WAVE-D-NPC-PACTS-PLAN §4.3, D2): one houseguest's next
        /// success on <see cref="Settle"/>'s ladder, spread over the week, or null when nothing on it is left
        /// for them to do. The ladder is Settle's order - a pact (one new pact a houseguest a week), a word
        /// (once a week), the agenda's pursuit (once a week; courting the Head of Household is a pursuit of its
        /// own, see <see cref="PursuitSpent"/>), then the repertoire, each kind once a week and a pact's meeting
        /// once - and this week's acts are its memory
        /// (<see cref="NpcSocialState.acts"/>), so the week's limits hold across beats, windows and reloads.
        ///
        /// <para>Every draw comes from <paramref name="roll"/>, the beat's keyed stream, never the season's.
        /// <paramref name="playerFree"/> leaves the player out of every target, listener and subject a verb
        /// would choose - a rung that is about them is skipped, and no reply card or gossip discovery follows;
        /// <paramref name="positional"/> lets a word on the vote or for safety be given, which belongs to
        /// the window the block is set in. Returns the act, without the id, room, window or tick the engine
        /// gives it; it moves what the verb moves and records nothing of its own.</para>
        /// </summary>
        public static NpcActState Beat(EpisodeState state, string npcId, Func<double> roll, bool playerFree, bool positional)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            var npc = state.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active) return null;
            var week = state.npcSocial.acts.Where(a => a != null && a.week == state.week).ToList();
            var mine = week.Where(a => a.actorId == npcId).ToList();
            // A reply card lives in free time and the campaign (validation's rule): a beat after the HoH or the
            // nominations that reaches the player says its line without one.
            var turn = new Turn { roll = roll, playerFree = playerFree, cards = state.phase == EpisodePhase.Social || state.phase == EpisodePhase.Campaign };

            // A pact, at most one new one a houseguest a week, either side of it, tried at their first beat of the
            // week (EpisodeEngine.PactWindow) as the weekly pass tried it once.
            var joined = new HashSet<string>(week.Where(a => a.kind == NpcActKinds.Pact)
                .SelectMany(a => new[] { a.actorId, a.partnerId }).Where(id => id != null), StringComparer.Ordinal);
            if (EpisodeEngine.Window(state) == EpisodeEngine.PactWindow(state, npcId) && NpcAlliances.TryPropose(state, npcId, joined))
            {
                var formed = state.alliances[state.alliances.Count - 1];
                return new NpcActState { kind = NpcActKinds.Pact, actorId = npcId, partnerId = formed.members.FirstOrDefault(id => id != npcId) };
            }
            // A word, once a week.
            if (!mine.Any(a => a.kind == NpcActKinds.Promise) && NpcPromises.TryGive(state, npcId, positional, out string toId))
                return new NpcActState { kind = NpcActKinds.Promise, actorId = npcId, partnerId = toId };
            // The agenda's pursuit, once a week, so a fixed target cannot compound.
            if (!PursuitSpent(state, npcId, mine))
            {
                var pursued = PursueOn(state, npc, turn);
                if (pursued != null) return pursued;
            }
            // The repertoire: each kind once a week, a pact's meeting once.
            var met = new HashSet<string>(week.Where(a => a.kind == NpcActKinds.Meet && a.subjectId != null).Select(a => a.subjectId), StringComparer.Ordinal);
            foreach (var kind in Repertoire(LeadTrait(npc)))
            {
                if (kind == NpcActionKind.AllianceProposal || kind == NpcActionKind.Promise) continue;
                if (mine.Any(a => a.kind == ActKind(kind))) continue;
                var done = Perform(state, npc, kind, met, turn);
                if (done != null) return done;
            }
            return null;
        }

        /// <summary>
        /// Whether a houseguest's pursuit for the week is spent (D2's ladder): by the agenda they hold now, a
        /// court spends courting the Head of Household, and building, holding or hunting spends the rest. The
        /// weekly pass courted as the nominations opened and still built, held or hunted on its own turn; a court
        /// that spent the whole rung took the week's building from most of the house, which cooled about a
        /// fifth by week four (acceptance (b)'s drift).
        /// </summary>
        public static bool PursuitSpent(EpisodeState state, string npcId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return PursuitSpent(state, npcId, state.npcSocial.acts.Where(a => a != null && a.week == state.week && a.actorId == npcId));
        }

        private static bool PursuitSpent(EpisodeState state, string npcId, IEnumerable<NpcActState> mine)
        {
            bool courting = NpcAgendas.Of(state, npcId)?.kind == Agendas.Court;
            return mine.Any(a => NpcActKinds.IsPursuit(a.kind) && (a.kind == NpcActKinds.Court) == courting);
        }

        /// <summary>A repertoire verb's act kind.</summary>
        private static string ActKind(NpcActionKind kind)
        {
            switch (kind)
            {
                case NpcActionKind.Talk: return NpcActKinds.Talk;
                case NpcActionKind.AllianceProposal: return NpcActKinds.Pact;
                case NpcActionKind.Promise: return NpcActKinds.Promise;
                case NpcActionKind.AllianceMeeting: return NpcActKinds.Meet;
                case NpcActionKind.SpreadInfo: return NpcActKinds.Rumour;
                case NpcActionKind.Confront: return NpcActKinds.Confront;
                case NpcActionKind.Eavesdrop: return NpcActKinds.Eavesdrop;
                default: return NpcActKinds.Campaign;
            }
        }

        /// <summary>
        /// Where a pass's draws come from and whom it may reach: the season's own stream with the player in
        /// reach for the weekly pass, a single verb and the campaign, as they always were; a beat's keyed
        /// stream under D2, with the player left out where the beat is player-free.
        /// </summary>
        private sealed class Turn
        {
            public Func<double> roll;
            public bool playerFree;
            /// <summary>Whether a reply card can be put to the player: always for the season's passes, which run in free time and the campaign, the only windows a card lives in; for a beat, only there.</summary>
            public bool cards = true;

            public double Roll() => roll();

            public static Turn Season(EpisodeState state) => new Turn { roll = () => EpisodeEngine.Roll(state) };
        }

        /// <summary>A reply card for the player where the pass can put one; a beat in a window no card lives in says its line without one.</summary>
        private static void Offer(EpisodeState state, Turn turn, string kind, string fromId, string aboutId)
        {
            if (turn.cards) ReplyCards.Offer(state, kind, fromId, aboutId);
        }

        /// <summary>
        /// The campaign pass, which belongs to campaigning rather than to the social week.
        ///
        /// <para>The source's campaign branch fires when <c>npc.isNominated</c>, and a houseguest is
        /// only ever nominated between the veto meeting and the vote. It is also the largest
        /// situational boost in the whole priority table — a nominee begging the veto holder outranks
        /// everything else in the system — so it is run on its own rather than competing for a social
        /// turn against verbs that were never going to beat it.</para>
        /// </summary>
        public static void Campaign(EpisodeState state)
        {
            if (!NpcSocialState.AutonomyHasBegun(state) || state.evictionResolved) return;
            var turn = Turn.Season(state);

            foreach (var npc in state.contestants
                         .Where(c => c.status == ContestantStatus.Active && !c.isPlayer
                                     && state.nominees.Contains(c.id))
                         .ToList())
            {
                // Under agency, the voters worth the visit: the persuadable, closest to torn first
                // (NPC-AGENCY-PLAN.md §5.2). With nobody persuadable, or without agency, the old
                // order, one visit.
                var visits = EpisodeEngine.PersuadableVoters(state, npc.id).Take(EpisodeEngine.CampaignVisits).ToList();
                if (visits.Count == 0)
                {
                    var voter = EpisodeEngine.Voters(state)
                        // Desperation reads as urgency: the veto holder first, then the player, then
                        // whoever is left, in cast order.
                        .OrderByDescending(candidate => candidate.id == state.vetoHolderId)
                        .ThenByDescending(candidate => candidate.isPlayer)
                        .ThenBy(candidate => candidate.id, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (voter == null) continue;
                    visits.Add(voter);
                }

                foreach (var voter in visits)
                {
                    Act(state, npc.id, voter.id, CampaignImpact,
                        npc.name + " campaigned to stay", "campaign", turn);
                    // Under the all-week rules (D2) the visit is one of the week's acts, recorded with no draw.
                    if (EpisodeEngine.AllWeekOn(state))
                        EpisodeEngine.RecordAct(state, NpcActKinds.Campaign, state.week + "-" + Windows.AfterVeto + "-campaign-" + npc.id + "-" + voter.id,
                            npc.id, voter.id, null);
                    if (!voter.isPlayer) continue;
                    EpisodeEngine.Log(state, "campaign",
                        npc.name + " came to you asking to stay this week.", state.playerId);
                    ReplyCards.Offer(state, ReplyCards.Plea, npc.id, state.nominees.FirstOrDefault(id => id != npc.id));
                    // Where the reply cards do not play, past the story boundary the plea is a story moment.
                    EpisodeEngine.StoryCampaignedTo(state, npc.id);
                }
            }
        }

        // ---------------------------------------------------------------- the verbs

        /// <summary>
        /// One houseguest doing one specific thing, or declining to because there is nobody to do it
        /// to. Returns whether a turn was actually spent.
        /// </summary>
        public static bool Perform(EpisodeState state, string npcId, NpcActionKind kind) =>
            Perform(state, state.Find(npcId), kind, new HashSet<string>(StringComparer.Ordinal), Turn.Season(state)) != null;

        /// <summary>One verb; the act it was, or null where it found nobody to do it to.</summary>
        private static NpcActState Perform(EpisodeState state, ContestantState npc, NpcActionKind kind, ISet<string> met, Turn turn)
        {
            if (npc == null || npc.status != ContestantStatus.Active) return null;
            switch (kind)
            {
                case NpcActionKind.Talk: return Talk(state, npc, turn);
                case NpcActionKind.AllianceProposal:
                    return NpcAlliances.TryPropose(state, npc.id, null)
                        ? new NpcActState { kind = NpcActKinds.Pact, actorId = npc.id, partnerId = state.alliances[state.alliances.Count - 1].members.FirstOrDefault(id => id != npc.id) }
                        : null;
                case NpcActionKind.Promise:
                    return NpcPromises.TryGive(state, npc.id, true, out string toId)
                        ? new NpcActState { kind = NpcActKinds.Promise, actorId = npc.id, partnerId = toId } : null;
                case NpcActionKind.AllianceMeeting: return Meet(state, npc, met, turn);
                case NpcActionKind.SpreadInfo: return SpreadInfo(state, npc, turn);
                case NpcActionKind.Confront: return Confront(state, npc, turn);
                case NpcActionKind.Eavesdrop: return Eavesdrop(state, npc, turn);
                // Campaigning is not a social turn; it has its own pass, at its own moment.
                default: return null;
            }
        }

        private static NpcActState Talk(EpisodeState state, ContestantState npc, Turn turn)
        {
            var target = WeightedTarget(state, npc.id, turn);
            if (target == null) return null;

            Act(state, npc.id, target.id, EpisodeEngine.TalkWarmth(state, npc.id, target.id),
                npc.name + " spent time with " + Named(state, target), "talk", turn);
            if (target.isPlayer)
                EpisodeEngine.Log(state, "conversation", npc.name + " sought you out this week.", state.playerId);
            return new NpcActState { kind = NpcActKinds.Talk, actorId = npc.id, partnerId = target.id };
        }

        /// <summary>
        /// The agenda's turn (NPC-AGENCY-PLAN.md §4): a houseguest building, holding or courting
        /// spends it with the person their agenda points at, and a hunt spends it with a pact-mate,
        /// against the threat. Survive and Reign have their moments elsewhere (the campaign, the
        /// nominations) and a drifter has nobody in mind, so neither spends a turn here. Nothing
        /// without agency, where there is no agenda.
        /// </summary>
        private static bool Pursue(EpisodeState state, ContestantState npc) => PursueOn(state, npc, Turn.Season(state)) != null;

        /// <summary>The same on a pass's stream; the act it was, or null.</summary>
        private static NpcActState PursueOn(EpisodeState state, ContestantState npc, Turn turn)
        {
            var agenda = NpcAgendas.Of(state, npc.id);
            var partner = agenda?.partnerId == null ? null : state.Find(agenda.partnerId);
            if (partner == null || partner.status != ContestantStatus.Active || !NpcAgendas.StillWorking(state, npc.id, agenda)) return null;
            // A player-free beat skips a pursuit of the player, or about them, and moves down the ladder.
            if (turn.playerFree && (partner.isPlayer || (agenda.kind == Agendas.Hunt && agenda.targetId == state.playerId))) return null;
            switch (agenda.kind)
            {
                case Agendas.Build:
                case Agendas.Court:
                case Agendas.Hold:
                    Act(state, npc.id, partner.id, EpisodeEngine.TalkWarmth(state, npc.id, partner.id),
                        npc.name + " spent time with " + Named(state, partner), "talk", turn);
                    if (partner.isPlayer)
                        EpisodeEngine.Log(state, "conversation", npc.name + " sought you out this week.", state.playerId);
                    return new NpcActState
                    {
                        kind = agenda.kind == Agendas.Build ? NpcActKinds.Build : agenda.kind == Agendas.Court ? NpcActKinds.Court : NpcActKinds.Hold,
                        actorId = npc.id, partnerId = partner.id,
                    };
                case Agendas.Hunt:
                    var threat = state.Find(agenda.targetId);
                    if (threat == null || threat.status != ContestantStatus.Active || threat.id == partner.id) return null;
                    Act(state, npc.id, partner.id, EpisodeEngine.TalkWarmth(state, npc.id, partner.id),
                        npc.name + " and " + Named(state, partner) + " talked about " + Named(state, threat), "talk", turn);
                    // Under the commitment rules (R0, X6) the player's view of the threat is the player's
                    // own: told the threat has to go, they read it in the log and their view of the
                    // threat does not move. Before them it took the hunt's weight, which could end a pact
                    // of theirs with the threat without a word. And a hunt with the player as its threat
                    // moves the pact-mate's view of the player, not the player's view of the pact-mate:
                    // the player was not there.
                    string heardHunt = "What " + Named(state, partner) + " heard from " + npc.name + " about " + Named(state, threat);
                    if (!EpisodeEngine.CommitmentRulesOn(state))
                        Act(state, partner.id, threat.id, EpisodeEngine.HuntImpact, heardHunt, "rumor", turn);
                    else if (threat.isPlayer)
                        EpisodeEngine.HeardAbout(state, partner.id, EpisodeEngine.HuntImpact, heardHunt, "rumor");
                    else if (!partner.isPlayer)
                        Act(state, partner.id, threat.id, EpisodeEngine.HuntImpact, heardHunt, "rumor", turn);
                    if (partner.isPlayer)
                        EpisodeEngine.Log(state, "information", npc.name + " told you " + threat.name + " has to go.", state.playerId);
                    // Talk about the player reaches them as a rumour does: the reference's roll, from the strategy windows.
                    if (threat.isPlayer && StrategyRules.Apply(state) && turn.Roll() < ReplyCards.GossipDiscoveryChance)
                    {
                        EpisodeEngine.Log(state, "gossip", "You found out " + npc.name + " has been talking about you to "
                            + partner.name + ".", state.playerId);
                        Offer(state, turn, ReplyCards.Gossip, npc.id, partner.id);
                    }
                    return new NpcActState { kind = NpcActKinds.Hunt, actorId = npc.id, partnerId = partner.id, subjectId = threat.id };
            }
            return null;
        }

        /// <summary>
        /// Courting the Head of Household as the nominations open (NPC-AGENCY-PLAN.md §4): a word
        /// with them, worth what a talk is worth, which their ranking of the house feels. Public for
        /// the engine's nomination hook.
        /// </summary>
        public static void Court(EpisodeState state, ContestantState npc, ContestantState hoh)
        {
            if (npc == null || hoh == null || npc.id == hoh.id) return;
            Act(state, npc.id, hoh.id, EpisodeEngine.TalkWarmth(state, npc.id, hoh.id),
                npc.name + " courted " + Named(state, hoh) + " before the nominations", "talk", Turn.Season(state));
            if (hoh.isPlayer)
                EpisodeEngine.Log(state, "conversation", npc.name + " came to see you before the nominations.", state.playerId, npc.id);
            if (hoh.isPlayer) HoHPitches.Offer(state, npc.id);
        }

        /// <summary>
        /// Alliance upkeep, once per alliance per week.
        ///
        /// <para>The source guards this with <c>lastMeetingWeek</c> on the alliance itself. That is a
        /// stored field, and this pass runs exactly once a week — so the pass <i>is</i> the week, and a
        /// set that lives as long as it says the same thing without a schema version.</para>
        ///
        /// <para>Alliances the player belongs to are skipped. A houseguest convening a meeting the
        /// player is a member of and never hears about would move the player's standing for reasons
        /// they have no way to see.</para>
        /// </summary>
        private static NpcActState Meet(EpisodeState state, ContestantState npc, ISet<string> met, Turn turn)
        {
            var alliance = NpcAlliances.ActiveAlliancesFor(state, npc.id)
                .Where(pact => !met.Contains(pact.id)
                               && !pact.members.Contains(state.playerId)
                               && pact.members.Count(id => state.Find(id)?.status == ContestantStatus.Active) >= 2)
                .OrderBy(pact => pact.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (alliance == null) return null;

            met.Add(alliance.id);
            var present = alliance.members
                .Where(id => state.Find(id)?.status == ContestantStatus.Active)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < present.Count; i++)
                for (int j = i + 1; j < present.Count; j++)
                    Act(state, present[i], present[j], AllianceMeetingImpact,
                        alliance.name + " met in week " + state.week, "alliance-meeting", turn);
            // The act names the pact by its id (D2's decision 3), which nothing shows.
            return new NpcActState { kind = NpcActKinds.Meet, actorId = npc.id, partnerId = present.FirstOrDefault(id => id != npc.id), subjectId = alliance.id };
        }

        /// <summary>
        /// Starting something about somebody.
        ///
        /// <para>The listener is drawn the way a conversation partner is drawn; the subject is not
        /// drawn at all. It is whoever this houseguest reads as the biggest threat, which is the whole
        /// reason <see cref="ThreatAssessment"/> exists — people talk about the person they are
        /// worried about, not a name pulled out of a hat.</para>
        ///
        /// <para>The damage lands between the listener and the subject rather than on the storyteller,
        /// and no entry is written for the storyteller at all. Nobody in the room knows where it came
        /// from; that is what makes it a rumour rather than an accusation.</para>
        /// </summary>
        private static NpcActState SpreadInfo(EpisodeState state, ContestantState npc, Turn turn)
        {
            var listener = WeightedTarget(state, npc.id, turn);
            if (listener == null) return null;

            string subjectId = ThreatAssessment.RankedTargets(state, npc.id)
                .FirstOrDefault(id => id != listener.id && id != npc.id && !(turn.playerFree && id == state.playerId));
            if (subjectId == null) return null;

            var subject = state.Find(subjectId);
            // Under the commitment rules (R0, X8) a rumour told to the player says what was said, and
            // moves only the player's own view of its subject: the subject heard nothing. A rumour
            // about the player moves only the listener's view of them: the player heard nothing.
            // Before the rules both went through the engine's two-way path, and a rumour to the
            // player said only that something was said.
            bool rules = EpisodeEngine.CommitmentRulesOn(state);
            if (listener.isPlayer && rules)
                EpisodeEngine.HeardFrom(state, npc.id, subjectId, RumorImpact, subject.name + " is the biggest threat in this house");
            else if (subject.isPlayer && rules)
                EpisodeEngine.HeardAbout(state, listener.id, RumorImpact,
                    "Something " + Named(state, listener) + " heard about " + Named(state, subject), "rumor");
            else
            {
                Act(state, listener.id, subjectId, RumorImpact,
                    "Something " + Named(state, listener) + " heard about " + Named(state, subject), "rumor", turn);
                if (listener.isPlayer)
                    EpisodeEngine.Log(state, "information",
                        npc.name + " told you something about " + subject.name + ".", state.playerId);
            }
            // A rumour about the player reaches them three times in ten, from the strategy windows -
            // the reference's roll, drawn only when it could matter, so an older season spends nothing.
            if (subject.isPlayer && StrategyRules.Apply(state) && turn.Roll() < ReplyCards.GossipDiscoveryChance)
            {
                EpisodeEngine.Log(state, "gossip", "You found out " + npc.name + " has been talking about you to "
                    + listener.name + ".", state.playerId);
                Offer(state, turn, ReplyCards.Gossip, npc.id, listener.id);
            }
            // Where the reply cards do not play, talk about the player gets back to them as a story moment.
            if (subject.isPlayer) EpisodeEngine.StoryGossipedAbout(state, npc.id, listener.id);
            return new NpcActState { kind = NpcActKinds.Rumour, actorId = npc.id, partnerId = listener.id, subjectId = subjectId };
        }

        /// <summary>
        /// Picking a fight, with whoever they like least — and only if they actually dislike them.
        /// Nobody confronts a house they are on good terms with. Past the story system's grudge
        /// rules, somebody they hold a real grudge against (forty or more) comes first: a
        /// nomination, a broken word or a betrayal is what people actually confront each other
        /// about. With no grudges held, this is exactly the old choice.
        /// </summary>
        private static NpcActState Confront(EpisodeState state, ContestantState npc, Turn turn)
        {
            // A player-free beat picks its fight among the houseguests.
            var house = state.Active.Where(other => other.id != npc.id && !(turn.playerFree && other.isPlayer)).ToList();
            var target = EpisodeEngine.StoryAt(state, StoryRules.Grudges)
                ? house.Where(other => Grudges.Severity(state, npc.id, other.id) >= 40)
                    .OrderByDescending(other => Grudges.Severity(state, npc.id, other.id))
                    .ThenBy(other => other.id, StringComparer.Ordinal)
                    .FirstOrDefault()
                : null;
            target = target ?? house
                .Where(other => state.Score(npc.id, other.id) < ConfrontationLine)
                .OrderBy(other => state.Score(npc.id, other.id))
                .ThenBy(other => other.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target == null) return null;

            Act(state, npc.id, target.id, ConfrontationImpact,
                npc.name + " had words with " + Named(state, target), "confrontation", turn);
            if (target.isPlayer)
            {
                EpisodeEngine.Log(state, "confrontation",
                    npc.name + " confronted you in front of the house.", state.playerId);
                Offer(state, turn, ReplyCards.Confrontation, npc.id, null);
                // Where the reply cards do not play, past the story boundary it is a story moment to answer.
                EpisodeEngine.StoryConfronted(state, npc.id);
            }
            return new NpcActState { kind = NpcActKinds.Confront, actorId = npc.id, partnerId = target.id };
        }

        /// <summary>
        /// Listening to a conversation they were not part of.
        ///
        /// <para>Worth nothing numerically when it works — the source's interaction table scores
        /// <c>eavesdrop</c> at zero, because what it produces is knowledge rather than goodwill. So the
        /// whole outcome is a private memory the listener owns, and the season's ledger records
        /// nothing at all.</para>
        ///
        /// <para>Never on the player, in either role. A houseguest overhearing the player would be
        /// knowledge the player has no way to learn they lost, and the player's own eavesdropping is a
        /// social action they choose to spend.</para>
        /// </summary>
        private static NpcActState Eavesdrop(EpisodeState state, ContestantState npc, Turn turn)
        {
            var others = state.Active.Where(c => !c.isPlayer && c.id != npc.id).ToList();
            if (others.Count < 2) return null;

            // Drawn before the outcome roll, exactly as the player's own eavesdrop draws it: the
            // conversation was happening whether or not they got away with listening to it.
            var first = Draw(others, turn);
            others.Remove(first);
            var second = Draw(others, turn);
            var act = new NpcActState { kind = NpcActKinds.Eavesdrop, actorId = npc.id, partnerId = first.id, subjectId = second.id };

            if (turn.Roll() >= EpisodeEngine.EavesdropSuccessChance)
            {
                Act(state, first.id, npc.id, CaughtEavesdroppingImpact,
                    first.name + " caught " + npc.name + " listening in", "eavesdrop", turn);
                return act;
            }

            double between = state.Score(first.id, second.id);
            string reading = between >= 25 ? "sounded close"
                : between <= -25 ? "sounded like they cannot stand each other"
                : "sounded careful with each other";
            EpisodeEngine.Remember(state, npc.id, first.id, "I overheard " + first.name + " and "
                + second.name + " in week " + state.week + ". They " + reading + ".", true);
            return act;
        }

        // ---------------------------------------------------------------- internals

        /// <summary>
        /// The source's <c>selectTalkTarget</c>: weighted sampling on relationship, not argmax.
        ///
        /// <para>Its own note is that NPCs "gravitate to friends but still surprise, which is what
        /// makes the house feel alive". The player carries six tenths of the weight everyone else
        /// does, applied after the floor rather than before it — the source's order, and it matters
        /// for anyone the houseguest is already at the floor with.</para>
        /// </summary>
        private static ContestantState WeightedTarget(EpisodeState state, string npcId, Turn turn)
        {
            // Cast order, which is the order the source sweeps in. It decides where a cumulative
            // sweep lands, so it is part of the result rather than presentation. A player-free beat
            // leaves the player out of the pool.
            var pool = state.Active.Where(other => other.id != npcId && !(turn.playerFree && other.isPlayer)).ToList();
            if (pool.Count == 0) return null;

            var weights = pool
                .Select(other => Math.Max(1, state.Score(npcId, other.id) + 60) * (other.isPlayer ? 0.6 : 1))
                .ToList();
            double remaining = turn.Roll() * weights.Sum();
            for (int i = 0; i < pool.Count; i++)
            {
                remaining -= weights[i];
                if (remaining <= 0) return pool[i];
            }
            return pool[0];
        }

        private static ContestantState Draw(IList<ContestantState> pool, Turn turn) =>
            pool[Math.Min(pool.Count - 1, (int)Math.Floor(turn.Roll() * pool.Count))];

        /// <summary>
        /// One houseguest's act on another.
        ///
        /// <para>Acts involving the player go through the engine's own relationship path, so they move
        /// the player's arcs and can raise a loyalty milestone exactly as a player-initiated act does.
        /// Acts between two houseguests do not, and that is not an optimisation — see
        /// <see cref="RelationshipLedger.Move"/> for why an arc must not be fed by a conversation the
        /// player was never part of.</para>
        /// </summary>
        private static void Act(EpisodeState state, string from, string to, double delta,
            string note, string type, Turn turn)
        {
            if (from == state.playerId || to == state.playerId)
            {
                // The pass's own stream: the season's for the weekly pass, a beat's keyed one under D2.
                EpisodeEngine.ChangeOnStream(state, from, to, delta, turn.roll, note, type);
                return;
            }
            RelationshipLedger.Move(state, from, to, delta);
            RelationshipLedger.Record(state, from, to, type, delta, note);
        }

        private static string Named(EpisodeState state, ContestantState who) =>
            who != null && who.isPlayer ? "you" : who == null ? "somebody" : who.name;
    }
}
