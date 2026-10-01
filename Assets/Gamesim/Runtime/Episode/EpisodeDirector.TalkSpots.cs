using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Talking somewhere (PACK8-PASS-PLAN C3). Asked to talk to a houseguest - a click on them, the
    /// cast strip's Talk, a voter's or a house card's - the player and they go to the nearest free
    /// talk spot in their room, then in the player's: two seats on the couches or at the long table,
    /// or a venue's two marks. Both arrive, the panel opens, and then they sit, or turn to each
    /// other. Closing the panel gets both up at their seats and lets the place go.
    ///
    /// <para>Presentation only: it commits nothing, saves nothing and draws on no generator, and
    /// the conversation it opens is the one <see cref="TryOpenNpc"/> always opened. Anything that
    /// goes wrong opens that conversation where they are instead, standing, as every conversation
    /// opened before the spots: no free place, a walk that runs out of time or stops short, a
    /// second press on the same houseguest. Reduced motion never walks to a spot, and a batch run
    /// does not unless asked (<see cref="TalkSpotsInBatchRuns"/>), so the audited walk and every
    /// test that opens a conversation directly see what they always saw.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private HouseTalkSpot talkSpot;
        /// <summary>Whether the conversation has opened at the spot: from then on the panel, not the walk, decides when it ends.</summary>
        private bool talkSpotOpen;
        private int talkSpotResends;
        private HouseSeatPresentation talkSpotPlayerSeat;

        /// <summary>How long the walk to a spot may take before the conversation opens where they are.</summary>
        private const float TalkSpotTimeout = 20f;
        /// <summary>How near the player's root must be to its place to have arrived: a little over the agent's stopping slack.</summary>
        private const float TalkSpotArrival = .4f;
        /// <summary>How often a walk that stopped short of its place is sent on before the conversation opens where they are.</summary>
        private const int TalkSpotResends = 3;

        /// <summary>For tests: walk to the talk spots even in a batch run, where a conversation otherwise opens where the houseguest is, as the stages and the walk out are skipped.</summary>
        public bool TalkSpotsInBatchRuns { get; set; }

        /// <summary>The place the player and a houseguest are walking to or talking at, by its venue id, or null. A read for tests.</summary>
        public string TalkSpotId => talkSpot != null ? talkSpot.SpotId : null;

        /// <summary>Whether that place is two seats. A read for tests.</summary>
        public bool TalkSpotSeated => talkSpot != null && talkSpot.Seated;

        /// <summary>The place itself, while one is held: its seats or marks, and where the player's root waits. A read for tests.</summary>
        public HouseTalkSpot CurrentTalkSpot => talkSpot;

        /// <summary>
        /// The places in a room where two people stand to talk: the standing venues' marks. For the
        /// story session's scenes (its ScenePlaces), should it want them; nothing here uses it.
        /// </summary>
        public IReadOnlyList<Vector3> StandingPlaces(string room) => HouseConversationSpots.StandingPlaces(gameObject.scene, room);

        private bool TalkSpotsAllowed => !reducedMotion && (!Application.isBatchMode || TalkSpotsInBatchRuns);

        /// <summary>
        /// Sends the player and the houseguest to a place to talk, when the house can: the house's
        /// world up and free, nothing else holding the player, and a free place they can both reach.
        /// False, with nothing changed, when it cannot, and the caller opens the conversation the
        /// old way.
        /// </summary>
        private bool TryWalkToTalkSpot(HouseNpc npc)
        {
            if (!TalkSpotsAllowed || npc == null || player == null || npcMeetings == null || !npcMeetings.IsReady || npcWorldFailed
                || npcDiagnosticsSuspended || player.HasActivityOwner || OpeningOwnsHouse || IsCeremonyStaged || walkingOutId != null
                || competitionArenaStaging || projected == null || projected.Find(npc.Id)?.status != ContestantStatus.Active) return false;
            // A new errand replaces a walk to a spot still under way, as it replaces any other walk.
            if (talkSpot != null && !talkSpotOpen) EndTalkSpot();
            if (talkSpot != null || !npcMeetings.TryReserveTalkSpot(npc.Id, player, out var spot, out _)) return false;
            // The player's half is the walk to a houseguest aimed at a place instead of a person, so
            // a floor click, the screen, the diary or a panel call it off as they always did.
            if (!player.TryTravelTo(spot.PlayerStands)) { npcMeetings.ReleaseTalk(spot); return false; }
            CancelTravel();
            talkSpot = spot; talkSpotOpen = false; talkSpotResends = 0;
            headingToNpcId = npc.Id;
            headingToNpcAt = npc.transform.position;
            headingToNpcDeadline = Time.unscaledTime + TalkSpotTimeout;
            cameraRig?.FocusSubject(player.transform, false);
            message = "Heading over to " + npc.DisplayName;
            Render();
            return true;
        }

        /// <summary>
        /// Each frame, before the walk to a houseguest: a conversation opened at a spot lasts as long
        /// as the panel on that houseguest, and a walk to one whose errand was dropped without being
        /// cancelled is let go.
        /// </summary>
        private void TickTalkSpot()
        {
            if (talkSpot == null) return;
            if (talkSpotOpen)
            {
                if (focusedNpc == null || focusedNpc.Id != talkSpot.NpcId) EndTalkSpot();
                return;
            }
            if (headingToNpcId != talkSpot.NpcId) EndTalkSpot();
        }

        /// <summary>The walk to a spot: both on their places, the conversation opens; anything else, it opens where they are.</summary>
        private void TickWalkToTalkSpot()
        {
            var spot = talkSpot;
            var npc = housemates.FirstOrDefault(actor => actor != null && actor.Id == spot.NpcId && actor.gameObject.activeInHierarchy);
            if (npc == null || npcMeetings == null || !npcMeetings.TalkValid(spot) || Time.unscaledTime > headingToNpcDeadline)
            { FallBackFromTalkSpot(npc); return; }
            bool playerThere = Across(player.transform.position, spot.PlayerStands) <= TalkSpotArrival;
            if (player.HasArrived && !playerThere)
            {
                // Stopped short - somebody in the way, the path cut - and sent on again, a few times.
                if (++talkSpotResends > TalkSpotResends || !player.TryTravelTo(spot.PlayerStands)) FallBackFromTalkSpot(npc);
                return;
            }
            if (playerThere && player.HasArrived && npcMeetings.TalkArrived(spot)) OpenAtTalkSpot(npc);
        }

        /// <summary>
        /// Both are there: the conversation opens, and then each takes their place - into the seats,
        /// the player's root waiting on its approach as a houseguest's does, or turned to each other.
        /// A conversation that cannot open opens where they are instead.
        /// </summary>
        private void OpenAtTalkSpot(HouseNpc npc)
        {
            var spot = talkSpot;
            player.StopHere();
            // Open first, so the CancelTravel the walk ends with leaves the spot to the panel.
            talkSpotOpen = true;
            if (!TryOpenNpc(npc.Id)) { talkSpotOpen = false; FallBackFromTalkSpot(npc); return; }
            CancelTravel();
            var visual = npc.GetComponent<CharacterPresentation>();
            var mine = player.GetComponent<CharacterPresentation>();
            if (spot.Seated)
            {
                var meetings = npcMeetings;
                // Explicitly: a missing component is Unity's fake null in the editor, which ?? keeps.
                var seat = npc.GetComponent<HouseSeatPresentation>();
                if (seat == null) seat = npc.gameObject.AddComponent<HouseSeatPresentation>();
                seat.Begin(spot.NpcPlace, () => meetings != null && meetings.TalkHolds(spot));
                var mySeat = player.GetComponent<HouseSeatPresentation>();
                if (mySeat == null) mySeat = player.gameObject.AddComponent<HouseSeatPresentation>();
                mySeat.Begin(spot.PlayerPlace, () => ReferenceEquals(talkSpot, spot) && talkSpotOpen);
                if (mySeat.Active) talkSpotPlayerSeat = mySeat;
                if (visual != null) visual.SetFacing(spot.NpcPlace.Facing);
                if (mine != null) mine.SetFacing(spot.PlayerPlace.Facing);
            }
            else
            {
                if (visual != null) visual.SetFacing(FacingToward(npc.transform.position, player.transform.position));
                if (mine != null) mine.SetFacing(FacingToward(player.transform.position, npc.transform.position));
            }
        }

        /// <summary>
        /// The spot is let go and the conversation opens where they are, standing, as it did before
        /// the spots: at once when they are close enough, otherwise after the old walk over to them.
        /// </summary>
        private void FallBackFromTalkSpot(HouseNpc npc)
        {
            EndTalkSpot();
            if (npc == null) { GiveUpOnWalk(null); return; }
            if (CanTalk(npc) && TryOpenNpc(npc.Id)) { CancelTravel(); player.StopHere(); return; }
            if (!TryApproach(npc, out var approach) || !player.TryRunTo(approach)) { GiveUpOnWalk(npc); return; }
            headingToNpcId = npc.Id;
            headingToNpcAt = npc.transform.position;
            headingToNpcDeadline = Time.unscaledTime + WalkTimeout;
        }

        /// <summary>
        /// Lets the place go: both up at their seats - the seat's own stand-up, which outlives the
        /// place - their headings their own again, and the houseguest back to the house.
        /// </summary>
        private void EndTalkSpot()
        {
            var spot = talkSpot;
            if (spot == null) return;
            talkSpot = null; talkSpotOpen = false; talkSpotResends = 0;
            if (talkSpotPlayerSeat != null && talkSpotPlayerSeat.Active) talkSpotPlayerSeat.RequestExit();
            talkSpotPlayerSeat = null;
            var mine = player != null ? player.GetComponent<CharacterPresentation>() : null;
            if (mine != null) mine.SetFacing(float.NaN);
            var body = BodyFor(spot.NpcId);
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual != null) visual.SetFacing(float.NaN);
            if (npcMeetings != null) npcMeetings.ReleaseTalk(spot);
        }

        private static float FacingToward(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
    }
}
