using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The veto meeting, staged (PACK8-PASS-PLAN C1, decision 5): the house takes its seats in the
    /// living room, the block as it stood before the meeting in the red chairs, the holder and the
    /// Head of Household on the U's two middle base seats facing them, everybody else on the U in
    /// cast order. The meeting plays on the living room's screen (CeremonyTakeover's screen path),
    /// and the camera cuts with its pages: the wide over the U, the screen, the holder, each
    /// nominee in turn, the push-in on the red chairs, the decision, the Head of Household and the
    /// replacement, and the block that goes to the vote.
    ///
    /// <para>Presentation and nothing else, as every stage is: the meeting is decided and saved by
    /// the commit before the house is summoned, nothing here writes state, and nobody is seated by
    /// what the meeting decided - the replacement sits wherever cast order puts them, so the room
    /// gives nothing away before the screen does. The block before the commit is the only thing
    /// the stage is handed beyond the committed state, in memory, for this one meeting.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// The veto meeting's card for a stage: the meeting on the set's screen when the stage has
        /// one, and the generic card on the HUD when it has none - which a stage asks for only once
        /// it has let the house go, so the generic card's framing and reactions never run under it.
        /// </summary>
        private bool TryStageVetoMeeting(EpisodeState committed, string text, HashSet<string> wasActive, HashSet<string> wasNominated,
            IReadOnlyList<string> blockBefore)
        {
            if (takeover == null) return false;
            return TryBeginCeremonyStage(CeremonySting.VetoKind, committed,
                screen => screen != null
                    ? takeover.PlayVetoMeeting(VetoMeetingScriptFor(committed, blockBefore), reducedMotion, ceremonyPace, screen)
                    : PlayGenericCeremonyCard(committed, CeremonySting.VetoKind, text, wasActive, wasNominated),
                blockBefore);
        }

        /// <summary>
        /// What the veto meeting's card says, read from the committed state and the block before
        /// the commit (<see cref="VetoMeetingRead"/>), with a face for everyone it names. Never the
        /// event's sentence. Public so a test can play the card on a screen of its own.
        /// </summary>
        public static VetoMeetingScript VetoMeetingScriptFor(EpisodeState committed, IReadOnlyList<string> blockBefore)
        {
            var meeting = VetoMeetingRead.Read(committed, blockBefore);
            var faces = new List<VetoMeetingScript.Face>();
            foreach (var id in meeting.People)
            {
                var actor = committed.Find(id);
                if (actor != null) faces.Add(new VetoMeetingScript.Face(actor.id, actor.name, CharacterPortraits.Get(actor), actor));
            }
            return new VetoMeetingScript(meeting, faces);
        }

        private sealed partial class CeremonyStage
        {
            /// <summary>The block as it stood before the veto meeting's commit, in the order it was named; empty for any other ceremony.</summary>
            private readonly List<string> blockBefore = new List<string>();

            /// <summary>The veto meeting's block before its commit, or the committed block when the caller had none to give.</summary>
            private List<string> VetoBlock => blockBefore.Count > 0 ? blockBefore : (state.nominees ?? new List<string>());

            /// <summary>Whom the meeting is about, and the card waits for: the block before it, and the holder.</summary>
            private IEnumerable<string> VetoPrincipals =>
                VetoBlock.Concat(string.IsNullOrEmpty(state.vetoHolderId) ? Enumerable.Empty<string>() : new[] { state.vetoHolderId }).Distinct();

            /// <summary>
            /// The veto meeting's places (decision 5): the block before the meeting in the red chairs
            /// by id, the player included and a holder on the block too; the holder on the U's first
            /// gallery seat - the base's middle, facing the red chairs across the low table - and the
            /// Head of Household on the next; everyone else in cast order, standing marks only past
            /// the seats, as the eviction does. Nothing is keyed to the outcome.
            /// </summary>
            private bool AssignVeto(List<string> active, out string reason)
            {
                reason = null;
                var scene = director.gameObject.scene;
                var hot = CeremonySeating.Anchors(scene, CeremonySeating.HotSeat);
                var gallery = CeremonySeating.Anchors(scene, CeremonySeating.GallerySeat);
                var sofa = CeremonySeating.Anchors(scene, CeremonySeating.SofaSeat);
                var marks = CeremonySeating.Anchors(scene, CeremonySeating.LivingMark);
                if (hot.Count < 2) { reason = "no red chairs for the veto meeting's block"; return false; }
                int hotSeat = 0;
                foreach (var id in VetoBlock)
                    if (active.Contains(id) && hotSeat < hot.Count) Place(id, hot[hotSeat++]);
                if (gallery.Count > 0) galleryFocus = (hot[0].Position + hot[1].Position) * 0.5f;

                int mark = 0;
                // A standing mark nobody could be sent to is passed over, not handed out, as the eviction's are.
                HouseInteractionAnchor NextMark()
                {
                    while (mark < marks.Count)
                    {
                        var candidate = marks[mark++];
                        if (director.npcMeetings == null || director.player == null
                            || director.npcMeetings.CanStandAt(candidate.Position, director.player.transform, out var why)) return candidate;
                        Debug.Log("Ceremony stage (" + Kind + "): " + candidate.VenueId + " " + candidate.Slot + " is passed over: " + why);
                    }
                    return null;
                }
                int gallerySeat = 0, sofaSeat = 0;
                void Seat(string id)
                {
                    if (string.IsNullOrEmpty(id) || placeOf.ContainsKey(id) || !active.Contains(id)) return;
                    if (gallerySeat < gallery.Count) { Place(id, gallery[gallerySeat++]); return; }
                    if (sofaSeat < sofa.Count) { Place(id, sofa[sofaSeat++]); return; }
                    var next = NextMark();
                    if (next != null) Place(id, next);
                }
                Seat(state.vetoHolderId);
                Seat(state.hohId);
                foreach (var id in active) Seat(id);
                return true;
            }

            /// <summary>
            /// The takeover's beats, when they are the veto meeting's on this stage's screen. The
            /// takeover plays other cards too - a story's fallout, the endgame's - and a card on the
            /// HUD frame is not this stage's to cut with.
            /// </summary>
            private void OnTakeoverBeat(CeremonyBeat beat)
            {
                var card = director.takeover;
                if (card == null || card.MeetingScreen != Screen) return;
                if (!Active || Step == CeremonyStageStep.Summons) return;
                OnVetoBeat(beat);
            }

            /// <summary>
            /// The meeting's pages acted out: the screen, then the holder as the house turns to them;
            /// the question on the screen, each nominee in turn looking to the holder, and the push-in
            /// on the red chairs while the decision is pending; the decision on the screen, then the
            /// saved nominee's relief or the block's look at each other and down; the Head of
            /// Household looking to the replacement, who looks down as the house turns to them; and
            /// the block that goes to the vote on the screen. A page reached by a skip is the screen
            /// alone: the order is given up, the outcome is the card's.
            /// </summary>
            private void OnVetoBeat(CeremonyBeat beat)
            {
                var pace = director.takeover != null ? director.takeover.MeetingPace : director.ceremonyPace;
                var block = VetoBlock.Where(id => placeOf.ContainsKey(id)).ToList();
                string holder = !string.IsNullOrEmpty(state.vetoHolderId) && placeOf.ContainsKey(state.vetoHolderId) ? state.vetoHolderId : null;
                switch (beat.Kind)
                {
                    case CeremonyBeatKind.Opened:
                    {
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        float intro = CeremonyPacing.FadeIn + CeremonyPacing.VetoIntro(pace);
                        float question = CeremonyPacing.VetoQuestion(pace);
                        if (holder != null)
                            Schedule(intro * 0.45f, () =>
                            {
                                Cut(SeatShot(holder));
                                director.TurnHeads(state, holder, block);
                            });
                        Schedule(intro, () => Cut(Screen.Shot(CutSeconds)));
                        // Each nominee in turn, waiting on the holder's word; a holder on the block is not
                        // waiting on themselves. Then the two of them together, the camera pushing in.
                        var waiting = block.Where(id => id != holder).ToList();
                        float each = question * 0.3f;
                        for (int i = 0; i < waiting.Count; i++)
                        {
                            string nominee = waiting[i];
                            Schedule(intro + question * 0.12f + each * i, () => Waiting(nominee, holder, each));
                        }
                        if (block.Count == 2)
                            Schedule(intro + question * 0.72f, () => PushInOnThePair(block[0], block[1], question * 0.28f));
                        break;
                    }
                    case CeremonyBeatKind.VetoDecided:
                    {
                        ClearCues();
                        Hush();
                        Cut(Screen.Shot(CutSeconds));
                        if (beat.Skipped) break;
                        float decision = CeremonyPacing.VetoDecision(pace);
                        string saved = beat.SubjectId;
                        if (saved != null && placeOf.ContainsKey(saved))
                            Schedule(decision * 0.4f, () =>
                            {
                                Cut(SeatShot(saved));
                                Relieved(saved, holder);
                                director.TurnHeads(state, saved, new[] { saved });
                            });
                        else if (saved == null && block.Count == 2)
                        {
                            string first = block[0], second = block[1];
                            Schedule(decision * 0.4f, () =>
                            {
                                Cut(PairShot(first, second));
                                Glance(first, second, 1.2f);
                                Glance(second, first, 1.2f);
                            });
                            Schedule(decision * 0.7f, () => { LookDown(first, 2f); LookDown(second, 2f); });
                        }
                        // The next page on the screen: the replacement's, or the block that goes to the vote.
                        Schedule(decision, () => Cut(Screen.Shot(CutSeconds)));
                        break;
                    }
                    case CeremonyBeatKind.ReplacementNamed:
                    {
                        ClearCues();
                        Cut(Screen.Shot(CutSeconds));
                        if (beat.Skipped) break;
                        float naming = CeremonyPacing.VetoReplacement(pace);
                        string replacement = beat.SubjectId;
                        string hoh = !string.IsNullOrEmpty(state.hohId) && placeOf.ContainsKey(state.hohId) ? state.hohId : null;
                        if (hoh != null)
                            Schedule(naming * 0.25f, () =>
                            {
                                Cut(SeatShot(hoh));
                                Glance(hoh, replacement, 1.5f);
                            });
                        if (replacement != null && placeOf.ContainsKey(replacement))
                            Schedule(naming * 0.55f, () =>
                            {
                                Cut(SeatShot(replacement));
                                LookDown(replacement, 4f);
                                director.TurnHeads(state, replacement, new[] { replacement });
                            });
                        Schedule(naming, () => Cut(Screen.Shot(CutSeconds)));
                        break;
                    }
                    case CeremonyBeatKind.Closed:
                        ClearCues();
                        Hush();
                        Release();
                        break;
                }
            }

            /// <summary>Whoever the meeting has set talking, so nobody is left mid-word when it moves on or ends.</summary>
            private readonly HashSet<string> talking = new HashSet<string>();

            /// <summary>A nominee waiting on the holder's word, from their chair: the cut to them, a look to the holder, and the lips moving - body language, never a line.</summary>
            private void Waiting(string nominee, string holder, float seconds)
            {
                Cut(SeatShot(nominee));
                var visual = Visual(nominee);
                if (visual == null) return;
                var towards = holder != null ? director.BodyFor(holder) : null;
                if (towards != null) visual.LookAt(towards, seconds + 0.4f);
                // The player's own body never speaks for them.
                if (nominee == state.playerId) return;
                visual.SetTalking(true);
                talking.Add(nominee);
                Schedule(seconds, () => Quiet(nominee));
            }

            private void Quiet(string id)
            {
                if (!talking.Remove(id)) return;
                var visual = Visual(id);
                if (visual != null) visual.SetTalking(false);
            }

            /// <summary>Nobody the meeting set talking is still talking: the decision is on the screen, or the house is let go.</summary>
            private void Hush()
            {
                foreach (var id in talking.ToList()) Quiet(id);
            }

            /// <summary>The push-in on the red chairs while the decision is pending, as the vote's is before its last ballot.</summary>
            private void PushInOnThePair(string first, string second, float seconds)
            {
                var wide = PairShot(first, second);
                Cut(wide);
                var near = PairShot(first, second);
                near.Distance = Mathf.Max(0.9f, wide.Distance - 0.7f);
                near.Seconds = Mathf.Max(0.4f, seconds - 0.1f);
                Schedule(0.05f, () => director.cameraRig.MoveTo(near));
            }

            /// <summary>Relief in the red chair: a look to the holder and, where the body has it, the seated fist pump.</summary>
            private void Relieved(string id, string holder)
            {
                var visual = Visual(id);
                if (visual == null) return;
                var towards = holder != null && holder != id ? director.BodyFor(holder) : null;
                if (towards != null) visual.LookAt(towards, 2.5f);
                if (visual.SupportsSeated(CharacterPresentation.Reaction.Saved)) visual.React(CharacterPresentation.Reaction.Saved);
            }

            /// <summary>A look from one place to another houseguest.</summary>
            private void Glance(string from, string to, float seconds)
            {
                var visual = Visual(from);
                var towards = to != null ? director.BodyFor(to) : null;
                if (visual != null && towards != null) visual.LookAt(towards, seconds);
            }

        }
    }
}
