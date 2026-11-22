using System;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Overheard, on screen (plan 32 §I2 and §I5, slice S2b; story rules 12 only). The house's watch: a conversation the
    /// player witnesses for <see cref="NoticeHoldSeconds"/> is noticed (a hunch), and one on a listening topic the player
    /// stays within <see cref="ListenReachMetres"/> of for <see cref="ListenHoldSeconds"/> is listened in on, free. The
    /// Nearby card's "Listen in" commits the Listen at once, and the listen tiles walk the player over to the nearest
    /// conversation they can listen in on. Every commit is the engine's witness command (S2a), refused there on its own
    /// terms (<see cref="EpisodeEngine.OverhearRefusal"/>), and checked against the committed season before it is sent.
    /// Below 12 nothing here runs and the house plays as before. Nothing here is saved: the watch's clocks are transient.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>How long a witnessed conversation is held before it is noticed, and lingered on before it is heard (§I2).</summary>
        public const float NoticeHoldSeconds = 2f, ListenHoldSeconds = 5f;
        /// <summary>How close a lingering player stands to one of the pair to listen in (§I2).</summary>
        public const float ListenReachMetres = 3.5f;

        private long overhearWatched;
        private double overhearHeld, overhearListenHeld;
        private bool overhearNoticed, overhearListened;

        /// <summary>The last Notice or Listen the house sent the engine. A read for explicit QA (PortVerification.Autonomy.cs).</summary>
        public EpisodeCommand LastOverhearCommand { get; private set; }

        /// <summary>Whether overhearing plays in this season: story rules 12 or above.</summary>
        public bool OverhearFree => projected != null && Overhearing.On(projected);

        /// <summary>The conversation being witnessed: the sequence of the lease the caption is over, or 0.</summary>
        public long WitnessedConversation
        {
            get
            {
                if (npcShownLease == null) return 0;
                foreach (var pair in npcPendingWorld) if (ReferenceEquals(pair.Value, npcShownLease)) return pair.Key;
                return 0;
            }
        }

        /// <summary>At 12, the Nearby card's offer: the conversation being witnessed can be listened in on now (§I5).</summary>
        private bool FreeListenOnOffer
        {
            get
            {
                long sequence = WitnessedConversation;
                return sequence > 0 && Overhearing.Refusal(projected, sequence, true) == null;
            }
        }

        /// <summary>The Nearby card's foot at 12: free, and the catch risk for the pair being witnessed.</summary>
        public string NearbyListenFoot
        {
            get
            {
                var conversation = Overhearing.Pending(projected, WitnessedConversation);
                return Overhearing.NearbyFoot(projected, conversation?.firstId, conversation?.secondId);
            }
        }

        /// <summary>The watch's guards: the acts' watch's own, without the acts (EpisodeDirector.AllWeek.cs).</summary>
        private bool OverhearWatchAllowed => IsReady && !blockedRecovery && !IsPanelOpen && !challengeActive && !CeremonyOverlays.OnScreen
            && !TourIsUp && !FrontDoorUp && playerIsActive && !overviewOpen && !durableCommitInProgress && !diaryOpen
            && !LookingAwayFromThePlayer;

        private void ForgetOverhearWatch()
        {
            overhearWatched = 0; overhearHeld = overhearListenHeld = 0;
            overhearNoticed = overhearListened = false;
        }

        /// <summary>
        /// The house's watch, on the world's 10 Hz samples (EpisodeDirector.NpcSocial.cs): the witnessed conversation is
        /// noticed once held <see cref="NoticeHoldSeconds"/>, and heard once the player has stayed within
        /// <see cref="ListenReachMetres"/> of the pair for <see cref="ListenHoldSeconds"/> on a listening topic. A new
        /// conversation, or none, starts the clocks again; a conversation already noticed or heard is not sent twice.
        /// </summary>
        private void WatchOverhearing(double elapsed)
        {
            if (!OverhearFree || !OverhearWatchAllowed || double.IsNaN(elapsed) || elapsed < 0) { ForgetOverhearWatch(); return; }
            long sequence = WitnessedConversation;
            var conversation = Overhearing.Pending(projected, sequence);
            if (conversation == null) { ForgetOverhearWatch(); return; }
            if (sequence != overhearWatched)
            {
                ForgetOverhearWatch();
                overhearWatched = sequence;
                return;
            }
            elapsed = Math.Min(elapsed, 1d);
            overhearHeld += elapsed;
            if (!overhearNoticed && overhearHeld + .0001 >= NoticeHoldSeconds)
            {
                overhearNoticed = true;
                SubmitOverhear(sequence, false);
            }
            if (overhearListened || !Overhearing.ListenTopic(conversation.topic)) return;
            if (!WithinListenReach(conversation)) { overhearListenHeld = 0; return; }
            overhearListenHeld += elapsed;
            if (overhearListenHeld + .0001 < ListenHoldSeconds) return;
            overhearListened = true;
            SubmitOverhear(sequence, true);
        }

        /// <summary>Whether the player stands within <see cref="ListenReachMetres"/> of one of the pair, across the floor.</summary>
        private bool WithinListenReach(NpcConversationState conversation)
        {
            var one = BodyFor(conversation.firstId);
            var two = BodyFor(conversation.secondId);
            if (one == null || two == null || player == null) return false;
            var at = player.transform.position;
            return Mathf.Min(Across(at, one.position), Across(at, two.position)) <= ListenReachMetres;
        }

        /// <summary>
        /// Sends a Notice or a Listen on this conversation, against the committed season, as the act watch sends a sighting:
        /// only while the engine would take it. False when it was not sent or not accepted.
        /// </summary>
        private bool SubmitOverhear(long sequence, bool listen)
        {
            var current = Snapshot;
            if (current == null || Overhearing.Refusal(current, sequence, listen) != null) return false;
            var conversation = Overhearing.Pending(current, sequence);
            var command = new EpisodeCommand
            {
                id = Guid.NewGuid().ToString("N"), actorId = current.playerId, kind = EpisodeCommandKind.WitnessNpcAct,
                expectedPhase = current.phase, expectedRevision = current.revision,
                targetId = conversation.firstId, secondTargetId = conversation.secondId,
                text = EpisodeEngine.ConversationText(sequence, listen),
            };
            LastOverhearCommand = command;
            return Submit(command).accepted;
        }

        /// <summary>At 12, the Nearby card's "Listen in": the Listen on the conversation being witnessed, at once (§I5).</summary>
        private void ListenInFree()
        {
            long sequence = WitnessedConversation;
            if (sequence <= 0) return;
            if (SubmitOverhear(sequence, true) && overhearWatched == sequence) overhearListened = true;
        }

        /// <summary>
        /// At 12, the listen tiles' choice (§I5): the panel closes and the player walks over to the nearest conversation
        /// they can listen in on, on the talk click's route (a spot near the first of the pair with a clear line to them),
        /// where lingering or the Nearby card's "Listen in" hears it. Nothing is committed here. With nobody to listen
        /// to, or no route, it says so and nothing moves.
        /// </summary>
        public void WalkOverToListen()
        {
            if (!OverhearFree || !IsReady || blockedRecovery || challengeActive || !playerIsActive || player == null) return;
            var state = projected;
            string refusal = Overhearing.ListenLeft(state) ? null : "You have listened in enough this week.";
            if (refusal == null)
            {
                var here = player.transform.position;
                foreach (var conversation in Overhearing.Listenable(state, c => PairDistance(c, here)))
                {
                    var npc = housemates?.FirstOrDefault(h => h != null && h.Id == conversation.firstId);
                    if (npc == null || !TryApproach(npc, out var approach)) continue;
                    ClosePanels();
                    CancelTravel();
                    if (!TryTravel(approach)) break;
                    message = "Heading over to listen in on " + state.Find(conversation.firstId)?.name
                        + " and " + state.Find(conversation.secondId)?.name;
                    Render();
                    return;
                }
                refusal = Overhearing.NobodyToListenTo;
            }
            message = refusal;
            BeginSystemNotice();
            Render();
        }

        /// <summary>How far the player stands from a conversation's pair in the house's world, or infinity where it has none.</summary>
        private float PairDistance(NpcConversationState conversation, Vector3 here)
        {
            if (!npcPendingWorld.ContainsKey(conversation.sequence)) return float.PositiveInfinity;
            var one = BodyFor(conversation.firstId);
            var two = BodyFor(conversation.secondId);
            if (one == null || two == null) return float.PositiveInfinity;
            return Mathf.Min(Across(here, one.position), Across(here, two.position));
        }
    }
}
