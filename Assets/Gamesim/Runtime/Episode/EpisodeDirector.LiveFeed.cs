using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The live feed's subject and caption (VISUAL-TARGET.md phase V5): where the second camera
    /// looks, and what the card under it says.
    ///
    /// <para>The caption names the room and how many stand in it - which the notebook's "who is
    /// where" already tells anyone - and never who is saying what, unless the player is standing
    /// there to hear it. That is the witnessed caption's own rule, and the feed defers to it rather
    /// than opening a second door into a conversation across the house.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public const string LiveFeedCardName = "Live feed";
        /// <summary>How often the feed re-aims and re-captions; the subject moves slowly.</summary>
        public const float LiveFeedAimSeconds = 0.5f;

        private LiveFeed liveFeed;
        private string lastCeremonyKind;
        private string liveFeedCaption = "";
        private float liveFeedNextAim;
        private LiveFeed subscribedLiveFeed;

        /// <summary>How long the feeds hold after production steps in.</summary>
        public const float FeedsHoldingSeconds = 12f;
        private float feedsHoldingUntil = -1f;

        /// <summary>Whether the feeds are on their holding card: production has just stepped in (plan §5.1).</summary>
        public bool LiveFeedHolding => feedsHoldingUntil > 0f && Time.unscaledTime < feedsHoldingUntil;

        /// <summary>Cuts the feeds to their holding card for a while.</summary>
        private void HoldFeeds() => feedsHoldingUntil = Time.unscaledTime + FeedsHoldingSeconds;

        /// <summary>The feed's picture, for the card; null before the feed exists.</summary>
        public Texture LiveFeedTexture => liveFeed != null ? liveFeed.Texture : null;
        public string LiveFeedCaption => liveFeedCaption;
        public bool LiveFeedPaused => liveFeed != null && liveFeed.Paused;

        private void TickLiveFeed()
        {
            if (liveFeed == null || !IsReady) return;
            if (subscribedLiveFeed != liveFeed)
            {
                if (subscribedLiveFeed != null) subscribedLiveFeed.FrameCaptured -= PublishLiveFeed;
                subscribedLiveFeed = liveFeed;
                liveFeed.FrameCaptured += PublishLiveFeed;
            }
            liveFeed.Paused = IsPanelOpen;
            if (hud != null) { hud.SetLiveFeedHolding(LiveFeedHolding); hud.SetLiveFeedPaused(liveFeed.Paused); }
            // Keep the subject, words and pixels together while another activity owns the view.
            if (liveFeed.Paused) return;
            if (Time.unscaledTime < liveFeedNextAim) return;
            liveFeedNextAim = Time.unscaledTime + LiveFeedAimSeconds;
            if (!TryGetLiveFeedSubject(out var at, out var caption)) { liveFeed.Clear(); return; }
            // The reverse angle of the viewer's own camera: a second camera, not the same one smaller.
            liveFeed.Aim(at, (cameraRig != null ? cameraRig.Yaw : 0f) + 180f, caption);
        }

        private void PublishLiveFeed(string caption)
        {
            if (caption == liveFeedCaption) return;
            liveFeedCaption = caption;
            if (hud != null) hud.SetLiveFeedCaption(caption);
        }

        /// <summary>
        /// Where the feed looks and what its caption says: a story moment being acted out in a room
        /// (plan §5.1: the feed prefers the scene, and names nobody); failing that the most recent
        /// NPC conversation that has physically formed; failing that the room of the last ceremony;
        /// failing that the player's room. False only when the house has no rooms to speak of.
        /// </summary>
        public bool TryGetLiveFeedSubject(out Vector3 at, out string caption)
        {
            at = Vector3.zero;
            caption = "";
            if (projected == null) return false;
            var markers = RoomMarkers();
            if (markers.Length == 0) return false;

            var staged = StagedRoom(projected, out bool meeting);
            var scene = staged != null ? markers.FirstOrDefault(m => m.RoomName == staged) : null;
            if (scene != null)
            {
                at = scene.transform.position;
                caption = RoomLabels.Title(scene.RoomName) + (meeting ? " \u00b7 THE HOUSE IS GATHERING" : " \u00b7 SOMETHING'S GOING ON");
                return true;
            }

            HouseMeetingLease latest = null;
            long latestSequence = long.MinValue;
            if (projected.npcSocial != null && npcMeetings != null)
                foreach (var pending in projected.npcSocial.pending)
                {
                    if (pending.sequence <= latestSequence) continue;
                    if (!npcPendingWorld.TryGetValue(pending.sequence, out var lease) || !npcMeetings.ValidateArrivedPair(lease, out _)) continue;
                    latest = lease;
                    latestSequence = pending.sequence;
                }

            HouseRoomMarker room;
            if (latest != null)
            {
                at = (latest.FirstSlot + latest.SecondSlot) * 0.5f;
                room = NearestMarker(markers, at);
                bool witnessed = ReferenceEquals(npcShownLease, latest) && !string.IsNullOrEmpty(ObservedNpcConversation);
                caption = witnessed ? ObservedNpcConversation : RoomCaption(room);
                return true;
            }

            var ceremonyRoom = lastCeremonyKind != null ? CeremonyRoom(lastCeremonyKind) : null;
            room = ceremonyRoom != null ? markers.FirstOrDefault(m => m.RoomName == ceremonyRoom) : null;
            if (room == null && player != null) room = NearestMarker(markers, player.transform.position);
            if (room == null) return false;
            at = room.transform.position;
            caption = RoomCaption(room);
            return true;
        }

        /// <summary>"KITCHEN · 2 HOUSEGUESTS": the room as the set names it, and the notebook's count for it.</summary>
        private string RoomCaption(HouseRoomMarker room)
        {
            int count = 0;
            foreach (var entry in WhoIsWhere())
                if (entry.Name == room.RoomName) { count = entry.Occupants.Count; break; }
            return RoomLabels.Title(room.RoomName) + " · " + count + (count == 1 ? " HOUSEGUEST" : " HOUSEGUESTS");
        }

        private static HouseRoomMarker NearestMarker(HouseRoomMarker[] markers, Vector3 at)
        {
            HouseRoomMarker nearest = null;
            float best = float.MaxValue;
            foreach (var marker in markers)
            {
                float distance = (marker.transform.position - at).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = marker;
            }
            return nearest;
        }
    }
}
