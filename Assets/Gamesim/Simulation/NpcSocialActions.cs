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

            foreach (var npc in state.contestants
                         .Where(c => c.status == ContestantStatus.Active && !c.isPlayer)
                         .ToList())
            {
                int taken = 0;
                if (NpcAlliances.TryPropose(state, npc.id, joined)) taken++;
                if (taken < ActionsPerSocialPhase && NpcPromises.TryGive(state, npc.id)) taken++;

                foreach (var kind in Repertoire(LeadTrait(npc)))
                {
                    if (taken >= ActionsPerSocialPhase) break;
                    // Both were already attempted above, at the priority the source gives them.
                    if (kind == NpcActionKind.AllianceProposal || kind == NpcActionKind.Promise) continue;
                    if (Perform(state, npc, kind, met)) taken++;
                }
            }
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

            foreach (var npc in state.contestants
                         .Where(c => c.status == ContestantStatus.Active && !c.isPlayer
                                     && state.nominees.Contains(c.id))
                         .ToList())
            {
                var voter = EpisodeEngine.Voters(state)
                    // Desperation reads as urgency: the veto holder first, then the player, then
                    // whoever is left, in cast order.
                    .OrderByDescending(candidate => candidate.id == state.vetoHolderId)
                    .ThenByDescending(candidate => candidate.isPlayer)
                    .ThenBy(candidate => candidate.id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (voter == null) continue;

                Act(state, npc.id, voter.id, CampaignImpact,
                    npc.name + " campaigned to stay", "campaign");
                if (voter.isPlayer)
                    EpisodeEngine.Log(state, "campaign",
                        npc.name + " came to you asking to stay this week.", state.playerId);
            }
        }

        // ---------------------------------------------------------------- the verbs

        /// <summary>
        /// One houseguest doing one specific thing, or declining to because there is nobody to do it
        /// to. Returns whether a turn was actually spent.
        /// </summary>
        public static bool Perform(EpisodeState state, string npcId, NpcActionKind kind) =>
            Perform(state, state.Find(npcId), kind, new HashSet<string>(StringComparer.Ordinal));

        private static bool Perform(EpisodeState state, ContestantState npc, NpcActionKind kind, ISet<string> met)
        {
            if (npc == null || npc.status != ContestantStatus.Active) return false;
            switch (kind)
            {
                case NpcActionKind.Talk: return Talk(state, npc);
                case NpcActionKind.AllianceProposal: return NpcAlliances.TryPropose(state, npc.id, null);
                case NpcActionKind.Promise: return NpcPromises.TryGive(state, npc.id);
                case NpcActionKind.AllianceMeeting: return Meet(state, npc, met);
                case NpcActionKind.SpreadInfo: return SpreadInfo(state, npc);
                case NpcActionKind.Confront: return Confront(state, npc);
                case NpcActionKind.Eavesdrop: return Eavesdrop(state, npc);
                // Campaigning is not a social turn; it has its own pass, at its own moment.
                default: return false;
            }
        }

        private static bool Talk(EpisodeState state, ContestantState npc)
        {
            var target = WeightedTarget(state, npc.id);
            if (target == null) return false;

            Act(state, npc.id, target.id, TalkImpact,
                npc.name + " spent time with " + Named(state, target), "talk");
            if (target.isPlayer)
                EpisodeEngine.Log(state, "conversation", npc.name + " sought you out this week.", state.playerId);
            return true;
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
        private static bool Meet(EpisodeState state, ContestantState npc, ISet<string> met)
        {
            var alliance = NpcAlliances.ActiveAlliancesFor(state, npc.id)
                .Where(pact => !met.Contains(pact.id)
                               && !pact.members.Contains(state.playerId)
                               && pact.members.Count(id => state.Find(id)?.status == ContestantStatus.Active) >= 2)
                .OrderBy(pact => pact.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (alliance == null) return false;

            met.Add(alliance.id);
            var present = alliance.members
                .Where(id => state.Find(id)?.status == ContestantStatus.Active)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < present.Count; i++)
                for (int j = i + 1; j < present.Count; j++)
                    Act(state, present[i], present[j], AllianceMeetingImpact,
                        alliance.name + " met in week " + state.week, "alliance-meeting");
            return true;
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
        private static bool SpreadInfo(EpisodeState state, ContestantState npc)
        {
            var listener = WeightedTarget(state, npc.id);
            if (listener == null) return false;

            string subjectId = ThreatAssessment.RankedTargets(state, npc.id)
                .FirstOrDefault(id => id != listener.id && id != npc.id);
            if (subjectId == null) return false;

            var subject = state.Find(subjectId);
            Act(state, listener.id, subjectId, RumorImpact,
                "Something " + Named(state, listener) + " heard about " + Named(state, subject), "rumor");
            if (listener.isPlayer)
                EpisodeEngine.Log(state, "information",
                    npc.name + " told you something about " + subject.name + ".", state.playerId);
            return true;
        }

        /// <summary>
        /// Picking a fight, with whoever they like least — and only if they actually dislike them.
        /// Nobody confronts a house they are on good terms with.
        /// </summary>
        private static bool Confront(EpisodeState state, ContestantState npc)
        {
            var target = state.Active
                .Where(other => other.id != npc.id && state.Score(npc.id, other.id) < ConfrontationLine)
                .OrderBy(other => state.Score(npc.id, other.id))
                .ThenBy(other => other.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target == null) return false;

            Act(state, npc.id, target.id, ConfrontationImpact,
                npc.name + " had words with " + Named(state, target), "confrontation");
            if (target.isPlayer)
                EpisodeEngine.Log(state, "confrontation",
                    npc.name + " confronted you in front of the house.", state.playerId);
            return true;
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
        private static bool Eavesdrop(EpisodeState state, ContestantState npc)
        {
            var others = state.Active.Where(c => !c.isPlayer && c.id != npc.id).ToList();
            if (others.Count < 2) return false;

            // Drawn before the outcome roll, exactly as the player's own eavesdrop draws it: the
            // conversation was happening whether or not they got away with listening to it.
            var first = Draw(state, others);
            others.Remove(first);
            var second = Draw(state, others);

            if (EpisodeEngine.Roll(state) >= EpisodeEngine.EavesdropSuccessChance)
            {
                Act(state, first.id, npc.id, CaughtEavesdroppingImpact,
                    first.name + " caught " + npc.name + " listening in", "eavesdrop");
                return true;
            }

            double between = state.Score(first.id, second.id);
            string reading = between >= 25 ? "sounded close"
                : between <= -25 ? "sounded like they cannot stand each other"
                : "sounded careful with each other";
            EpisodeEngine.Remember(state, npc.id, first.id, "I overheard " + first.name + " and "
                + second.name + " in week " + state.week + ". They " + reading + ".", true);
            return true;
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
        private static ContestantState WeightedTarget(EpisodeState state, string npcId)
        {
            // Cast order, which is the order the source sweeps in. It decides where a cumulative
            // sweep lands, so it is part of the result rather than presentation.
            var pool = state.Active.Where(other => other.id != npcId).ToList();
            if (pool.Count == 0) return null;

            var weights = pool
                .Select(other => Math.Max(1, state.Score(npcId, other.id) + 60) * (other.isPlayer ? 0.6 : 1))
                .ToList();
            double remaining = EpisodeEngine.Roll(state) * weights.Sum();
            for (int i = 0; i < pool.Count; i++)
            {
                remaining -= weights[i];
                if (remaining <= 0) return pool[i];
            }
            return pool[0];
        }

        private static ContestantState Draw(EpisodeState state, IList<ContestantState> pool) =>
            pool[Math.Min(pool.Count - 1, (int)Math.Floor(EpisodeEngine.Roll(state) * pool.Count))];

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
            string note, string type)
        {
            if (from == state.playerId || to == state.playerId)
            {
                EpisodeEngine.Change(state, from, to, delta, note, type);
                return;
            }
            RelationshipLedger.Move(state, from, to, delta);
            RelationshipLedger.Record(state, from, to, type, delta, note);
        }

        private static string Named(EpisodeState state, ContestantState who) =>
            who != null && who.isPlayer ? "you" : who == null ? "somebody" : who.name;
    }
}
