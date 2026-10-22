using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// A place the player and one houseguest have been sent to talk (PACK8-PASS-PLAN C3): a talk
    /// spot's two seats, or a venue's two marks. Runtime only. It names no topic, score or
    /// outcome, it is never saved, and it is let go the moment the conversation it was for ends.
    /// </summary>
    public sealed class HouseTalkSpot
    {
        public string NpcId { get; }
        /// <summary>The place's own venue id: a talk spot's, or one of the six venues' own.</summary>
        public string SpotId { get; }
        public string RoomId { get; }
        /// <summary>Whether the two sit: a spot on furniture. Otherwise they stand and face each other.</summary>
        public bool Seated { get; }
        /// <summary>The houseguest's seat or mark, and the player's.</summary>
        public HouseInteractionAnchor NpcPlace { get; }
        public HouseInteractionAnchor PlayerPlace { get; }
        /// <summary>Where the player's root waits while the body sits or stands at its place: its approach, on the floor.</summary>
        public Vector3 PlayerStands { get; }
        /// <summary>Whether the house has let the place go.</summary>
        public bool Released { get; internal set; }
        /// <summary>The houseguest's route token while the place is held: opaque, like a meeting lease's.</summary>
        public string Token { get; }
        internal HouseTalkSpot(string npcId, string spotId, string roomId, bool seated, HouseInteractionAnchor npcPlace,
            HouseInteractionAnchor playerPlace, Vector3 playerStands, string token)
        {
            NpcId = npcId; SpotId = spotId; RoomId = roomId; Seated = seated; NpcPlace = npcPlace; PlayerPlace = playerPlace;
            PlayerStands = playerStands; Token = token;
        }
    }

    /// <summary>
    /// The player's half of the talk spots: the nearest free place in the houseguest's room, then
    /// in the player's, the houseguest sent there on a token of its own. Like the ceremony's and
    /// the opening's borrows it is exempt from the house's pause - the house pauses while a panel
    /// is up and outside free time, and the person the player asked over still has to get there -
    /// and like a meeting it gives way to nothing but its own end: a stage, the opening or the walk
    /// out cannot start over it, and the director lets it go before anything else can want them.
    /// </summary>
    public sealed partial class HouseMeetingCoordinator
    {
        private sealed class TalkEntry
        {
            public HouseTalkSpot spot;
            public Venue venue;
            public Actor actor;
            public Vector3 npcPosition, npcApproach, playerPosition, playerApproach;
        }
        private TalkEntry talk;

        /// <summary>The place the player's talk holds, or null.</summary>
        public HouseTalkSpot Talk => talk != null ? talk.spot : null;

        /// <summary>
        /// Sends the houseguest to the best free place to talk with the player: in their room first,
        /// then in the player's, the shortest pair of walks with a seat counted three metres
        /// shorter (<see cref="SpotScore"/>), each place measured as a meeting's is - on the floor
        /// of its room, clear of the rooms' destinations, both bodies with room to stand, complete
        /// routes - and its two places inside <see cref="HouseConversationSpots.PlayerReach"/>. False,
        /// and nobody disturbed, when there is none or the houseguest is anybody else's.
        /// </summary>
        public bool TryReserveTalkSpot(string npcId, HousePlayerController player, out HouseTalkSpot spot, out string reason)
        {
            spot = null;
            if (disposed || !IsReady || HasCompetitionStage || HasOpeningStage || HasCeremonyStage || departing != null || talk != null)
                return Fail(out reason, "The house is not free for a conversation spot.");
            if (player == null || player.gameObject.scene != rooms.Scene || player.Agent == null || !player.Agent.enabled
                || !player.Agent.isOnNavMesh || npcId == null || !actors.TryGetValue(npcId, out var actor) || !eligible.Contains(npcId)
                || actor.npc == null || actor.motion == null || !actor.motion.IsBound)
                return Fail(out reason, "Nobody is free to walk to a conversation spot.");
            // A conversation, a stage or a walk out has them; furniture or a stroll gives way, as it does to a meeting.
            if (actor.motion.LeaseId != null && !ActivityOwnsMotion(actor.motion) && !WanderOwnsMotion(actor.motion) && !ActOwnsMotion(actor.motion))
                return Fail(out reason, actor.npc.DisplayName + " is busy.");
            var body = actor.npc.GetComponent<CapsuleCollider>();
            if (body == null) return Fail(out reason, actor.npc.DisplayName + " has no body to walk.");
            var order = new List<string>(2);
            if (rooms.TryLocate(actor.npc.transform.position, body.radius, out var theirs)) order.Add(theirs);
            if (rooms.TryLocate(player.transform.position, player.Agent.radius, out var yours) && !order.Contains(yours)) order.Add(yours);
            foreach (string room in order)
            {
                Venue best = null; bool npcFirst = true; float bestScore = float.PositiveInfinity;
                Vector3 playerAt = default;
                foreach (var venue in venues)
                {
                    if (venue.room != room || !venue.Valid(rooms.Scene) || PlaceTaken(venue)) continue;
                    for (int slot = 0; slot < 2; slot++)
                    {
                        if (!MeasureTalk(venue, slot == 0, actor, body, player, out var standing, out float walk)) continue;
                        float score = SpotScore(walk, venue.seated);
                        if (score >= bestScore - .0001f) continue; // Stable authored order on ties.
                        best = venue; npcFirst = slot == 0; bestScore = score; playerAt = standing;
                    }
                }
                if (best == null) continue;
                // Only now is anybody disturbed: off their furniture or their stroll, and on their way.
                YieldActivity(npcId); YieldWander(npcId); YieldAct(npcId);
                if (actor.motion.LeaseId != null) return Fail(out reason, actor.npc.DisplayName + " is busy.");
                var npcPlace = npcFirst ? best.a : best.b;
                var playerPlace = npcFirst ? best.b : best.a;
                string token = "talk:" + System.Guid.NewGuid().ToString("N");
                actor.motion.SetPaused(false);
                if (!actor.motion.TryReserveAndPath(token, npcPlace.Approach))
                {
                    actor.motion.SetPaused(paused);
                    return Fail(out reason, actor.npc.DisplayName + " has no route to " + best.id
                        + (actor.motion.LastRouteFailure != null ? " (" + actor.motion.LastRouteFailure + ")" : "") + ".");
                }
                spot = new HouseTalkSpot(npcId, best.id, best.room, best.seated, npcPlace, playerPlace, playerAt, token);
                talk = new TalkEntry
                {
                    spot = spot, venue = best, actor = actor,
                    npcPosition = npcPlace.Position, npcApproach = npcPlace.Approach,
                    playerPosition = playerPlace.Position, playerApproach = playerPlace.Approach,
                };
                LastFailure = null; reason = null;
                return true;
            }
            return Fail(out reason, "No conversation spot in their room or yours has two clear places.");
        }

        /// <summary>
        /// One way round a place: the houseguest at one end and the player at the other, each
        /// approach on its room's floor and clear of the rooms' destinations, both bodies with room
        /// to stand there, far enough apart to stand and near enough to talk, and complete routes
        /// for both. <paramref name="walk"/> is the two routes' length.
        /// </summary>
        private bool MeasureTalk(Venue venue, bool npcFirst, Actor actor, CapsuleCollider body, HousePlayerController player,
            out Vector3 playerAt, out float walk)
        {
            playerAt = default; walk = 0f;
            var npcPlace = npcFirst ? venue.a : venue.b;
            var playerPlace = npcFirst ? venue.b : venue.a;
            float playerRadius = player.Agent.radius;
            if (!rooms.TrySampleFloor(npcPlace.Approach, body.radius, filter, .25f, out var npcAt, out var npcRoom)
                || !rooms.TrySampleFloor(playerPlace.Approach, playerRadius, filter, .25f, out playerAt, out var playerRoom)
                || npcRoom != venue.room || playerRoom != venue.room || !ClearsDestinations(npcAt) || !ClearsDestinations(playerAt))
                return false;
            float apart = HorizontalSquared(npcAt, playerAt);
            if (apart < Mathf.Pow(body.radius + playerRadius + .2f, 2)
                || apart > HouseConversationSpots.PlayerReach * HouseConversationSpots.PlayerReach
                || !rooms.HasCapsuleClearance(npcAt, body.radius, body.height, actor.npc.transform)
                || !rooms.HasCapsuleClearance(playerAt, playerRadius, player.Agent.height, player.transform)) return false;
            if (!MeasurePath(actor.motion.Agent, npcAt, firstPath, out float theirs) || !player.TryMeasureRoute(playerAt, out float yours))
                return false;
            walk = theirs + yours;
            return HouseRoomQuery.Finite(walk);
        }

        /// <summary>Whether the houseguest stands on their place: on their mark and stopped, whoever is against them - the place is theirs.</summary>
        public bool TalkArrived(HouseTalkSpot spot) =>
            TalkHolds(spot) && talk.actor.motion != null && talk.actor.motion.IsOnMark(spot.Token);

        /// <summary>Whether this is the place the house holds for the player's talk.</summary>
        public bool TalkHolds(HouseTalkSpot spot) => !disposed && spot != null && talk != null && ReferenceEquals(talk.spot, spot);

        /// <summary>Whether the place is still held and whole: the houseguest bound and on its route, the place where it was.</summary>
        public bool TalkValid(HouseTalkSpot spot)
        {
            if (!TalkHolds(spot) || !eligible.Contains(talk.actor.id) || !ValidActor(talk.actor, spot.Token) || !talk.venue.Valid(rooms.Scene))
                return false;
            return (spot.NpcPlace.Position - talk.npcPosition).sqrMagnitude <= .0025f
                && (spot.NpcPlace.Approach - talk.npcApproach).sqrMagnitude <= .0025f
                && (spot.PlayerPlace.Position - talk.playerPosition).sqrMagnitude <= .0025f
                && (spot.PlayerPlace.Approach - talk.playerApproach).sqrMagnitude <= .0025f;
        }

        /// <summary>Lets the place go, if it is this one: the houseguest gets up at their seat and is the house's again.</summary>
        public void ReleaseTalk(HouseTalkSpot spot)
        {
            if (spot != null && talk != null && ReferenceEquals(talk.spot, spot)) EndTalk();
        }

        private void EndTalk()
        {
            if (talk == null) return;
            var entry = talk;
            talk = null;
            entry.spot.Released = true;
            var motion = entry.actor.motion;
            if (motion == null) return;
            // Up at the seat, as a conversation's end gets its pair up (Retire).
            StandUp(motion);
            motion.Release(entry.spot.Token);
            string id = entry.actor.id;
            motion.SetPaused(paused && !OpeningHoldsActor(id) && !CeremonyHoldsActor(id) && !DepartureHoldsActor(id) && !WanderHoldsActor(id));
        }

        private void RetireInvalidTalk()
        {
            if (talk != null && !TalkValid(talk.spot)) EndTalk();
        }

        private bool TalkHoldsActor(string id) => talk != null && talk.actor.id == id;

        private bool TalkOwnsMotion(HouseNpcMotion motion) =>
            talk != null && talk.actor.motion == motion && motion.LeaseId == talk.spot.Token;
    }
}
