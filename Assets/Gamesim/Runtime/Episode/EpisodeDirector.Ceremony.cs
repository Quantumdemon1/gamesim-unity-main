using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>The ceremonies: who stands on a takeover card, how a vote is revealed, and the reactions the bodies act out.</summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// The faces a ceremony card should show, read from committed state rather than from the
        /// event sentence.
        ///
        /// <para>Every subject is checked against the same audience rule the card text already
        /// passes. A portrait is a stronger disclosure than a name — it says unambiguously who,
        /// where prose can be vague — so the rail and this list stay inside what the player is
        /// entitled to know.</para>
        /// </summary>
        private List<CeremonyTakeover.Subject> CeremonySubjects(
            EpisodeState state, string kind, HashSet<string> wasActive)
        {
            var subjects = new List<CeremonyTakeover.Subject>();
            if (state == null) return subjects;

            void Add(string id, string badge)
            {
                var actor = state.Find(id);
                if (actor == null) return;
                subjects.Add(new CeremonyTakeover.Subject(actor.name, badge,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
            }

            switch (kind)
            {
                case CeremonySting.NominationKind:
                case CeremonySting.VetoKind:
                    if (state.nominees != null)
                        foreach (var id in state.nominees) Add(id, "NOMINATED");
                    break;
                case CeremonySting.EvictionKind:
                    // Whoever stopped being active during this commit. Usually one person; the
                    // loop rather than a Single() because a double eviction would still be true.
                    foreach (var actor in state.contestants)
                        if (actor.status != ContestantStatus.Active && wasActive != null && wasActive.Contains(actor.id))
                            Add(actor.id, "EVICTED");
                    break;
                case CeremonySting.WinnerKind:
                    Add(state.winnerId, "WINNER");
                    Add(state.runnerUpId, "RUNNER-UP");
                    break;
            }
            return subjects;
        }

        /// <summary>
        /// What the competition was for, named from the phase the command was issued in.
        ///
        /// <para>Read from the pre-commit phase rather than parsed out of the event sentence: the
        /// committed phase has already advanced to whatever comes next, and picking the words back
        /// out of "Competition winner: X · Skill." would couple the card to copy.</para>
        /// </summary>
        private static string AwardTitle(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.HoH: return "Head of Household";
                case EpisodePhase.Veto: return "Power of Veto";
                case EpisodePhase.FinalHoHPart1: return "Final HoH · Part 1";
                case EpisodePhase.FinalHoHPart2: return "Final HoH · Part 2";
                case EpisodePhase.FinalHoHPart3: return "Final HoH · Part 3";
                default: return "Competition";
            }
        }

        /// <summary>Who is playing for the veto, badged by what they are defending.</summary>
        private List<CeremonyTakeover.Subject> VetoField(EpisodeState state)
        {
            var field = new List<CeremonyTakeover.Subject>();
            if (state?.vetoPlayers == null) return field;
            foreach (var id in state.vetoPlayers)
            {
                var actor = state.Find(id);
                if (actor == null) continue;
                string badge = actor.id == state.hohId ? "HOH"
                    : state.nominees != null && state.nominees.Contains(actor.id) ? "NOMINATED"
                    : null;
                field.Add(new CeremonyTakeover.Subject(actor.name, badge,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
            }
            return field;
        }

        private static string NameOf(EpisodeState state, string id) => state?.Find(id)?.name;

        /// <summary>
        /// Everyone who draws a key and keeps it: still playing, not the Head of Household, not on
        /// the block. The HoH does not draw for their own safety.
        /// </summary>
        private List<KeyCeremony.Person> SafeHouseguests(EpisodeState state)
        {
            var people = new List<KeyCeremony.Person>();
            if (state?.contestants == null) return people;
            foreach (var actor in state.contestants)
            {
                if (actor.status != ContestantStatus.Active) continue;
                if (actor.id == state.hohId) continue;
                if (state.nominees != null && state.nominees.Contains(actor.id)) continue;
                people.Add(Person(state, actor.id));
            }
            return people;
        }

        private List<KeyCeremony.Person> NominatedHouseguests(EpisodeState state)
        {
            var people = new List<KeyCeremony.Person>();
            if (state?.nominees == null) return people;
            foreach (var id in state.nominees) if (state.Find(id) != null) people.Add(Person(state, id));
            return people;
        }

        private static KeyCeremony.Person Person(EpisodeState state, string id)
        {
            var actor = state.Find(id);
            return new KeyCeremony.Person(actor.id, actor.name,
                CharacterPortraits.Get(
                    CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id))));
        }

        /// <summary>The two people on the block, with their faces, for the eviction reveal.</summary>
        private List<VoteReveal.Nominee> EvictionBlock(EpisodeState state)
        {
            var block = new List<VoteReveal.Nominee>();
            if (state?.nominees == null) return block;
            foreach (var id in state.nominees)
            {
                var actor = state.Find(id);
                if (actor == null) continue;
                block.Add(new VoteReveal.Nominee(actor.id, actor.name,
                    CharacterPortraits.Get(
                        CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)))));
            }
            return block;
        }

        /// <summary>
        /// The committed ballots, in the order the house cast them.
        ///
        /// <para>Read from state rather than re-derived, so the card counts to the same total the
        /// save holds. It carries who voted and for whom — both already public at the reveal, which
        /// is the moment the engine logs them as <c>vote-reveal</c> events.</para>
        /// </summary>
        private static List<VoteReveal.Ballot> EvictionBallots(EpisodeState state)
        {
            var ballots = new List<VoteReveal.Ballot>();
            if (state?.votes == null) return ballots;
            foreach (var vote in state.votes)
            {
                var voter = state.Find(vote.voterId);
                ballots.Add(new VoteReveal.Ballot(voter?.name ?? "A housemate", vote.targetId));
            }
            return ballots;
        }

        /// <summary>Whoever stopped being active during this commit, or null.</summary>
        /// <summary>
        /// The houseguests a ceremony is about act it out: the nominees when the keys are turned,
        /// whoever came off the block (and whoever replaced them) at the veto ceremony, the evicted
        /// on the vote, the winner at the end. A body plays its clip only when its controller has
        /// one and it is standing; the beat is recorded on every body regardless.
        /// </summary>
        private void ReactToCeremony(EpisodeState state, string kind, HashSet<string> wasActive, HashSet<string> wasNominated)
        {
            if (state == null) return;
            switch (kind)
            {
                case CeremonySting.NominationKind:
                    foreach (var id in state.nominees ?? new List<string>()) React(id, CharacterPresentation.Reaction.Nominated);
                    TurnHeads(state, (state.nominees ?? new List<string>()).FirstOrDefault(), state.nominees);
                    break;
                case CeremonySting.VetoKind:
                    foreach (var id in wasNominated)
                        if (state.nominees == null || !state.nominees.Contains(id)) React(id, CharacterPresentation.Reaction.Saved);
                    foreach (var id in state.nominees ?? new List<string>())
                        if (!wasNominated.Contains(id)) React(id, CharacterPresentation.Reaction.Nominated);
                    break;
                case CeremonySting.EvictionKind:
                    foreach (var actor in state.contestants)
                        if (actor.status != ContestantStatus.Active && wasActive.Contains(actor.id))
                            React(actor.id, CharacterPresentation.Reaction.Evicted);
                    TurnHeads(state, EvictedThisCommit(state, wasActive), null);
                    break;
                case CeremonySting.WinnerKind:
                    React(state.winnerId, CharacterPresentation.Reaction.Won);
                    TurnHeads(state, state.winnerId, null);
                    break;
            }
        }

        /// <summary>How long the room looks at the ceremony's subject: the card's strip and a beat after.</summary>
        public const float HeadTurnSeconds = 7f;

        /// <summary>
        /// The crowd turns to look (V6): every other body in the house turns its head toward the
        /// ceremony's subject for the card's length, except the subjects themselves, who are
        /// acting the beat out. Heads only: the bodies stay where the venues put them.
        /// </summary>
        private void TurnHeads(EpisodeState state, string towardId, ICollection<string> except)
        {
            if (string.IsNullOrEmpty(towardId)) return;
            var target = BodyFor(towardId);
            if (target == null) return;
            foreach (var actor in state.contestants)
            {
                if (actor.id == towardId || actor.status != ContestantStatus.Active) continue;
                if (except != null && except.Contains(actor.id)) continue;
                var body = BodyFor(actor.id);
                var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
                if (visual != null) visual.LookAt(target, HeadTurnSeconds);
            }
        }

        private void React(string id, CharacterPresentation.Reaction kind)
        {
            if (string.IsNullOrEmpty(id)) return;
            CharacterPresentation visual = null;
            if (housemates != null)
                foreach (var npc in housemates)
                    if (npc != null && npc.Id == id) { visual = npc.GetComponent<CharacterPresentation>(); break; }
            if (visual == null && player != null && projected != null && id == projected.playerId)
                visual = player.GetComponent<CharacterPresentation>();
            if (visual != null) visual.React(kind);
        }

        private static string EvictedThisCommit(EpisodeState state, HashSet<string> wasActive)
        {
            if (state?.contestants == null || wasActive == null) return null;
            foreach (var actor in state.contestants)
                if (actor.status != ContestantStatus.Active && wasActive.Contains(actor.id)) return actor.id;
            return null;
        }
    }
}
