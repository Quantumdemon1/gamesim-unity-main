using System;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The house answering what the player does in it: the room goes dim while the player sleeps
    /// in it, and a friend comes to keep them company in the hot tub.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>How far the room's fill light drops for a sleeper, and how long it takes to get there.</summary>
        public const float SleepDimTo = .35f, SleepDimSeconds = 1.5f;

        private Light sleepLight;
        private float sleepLightBase, sleepDim;

        /// <summary>The fill light a sleeping player has dimmed, while it is dimmed; null otherwise.</summary>
        public Light DimmedForSleep => sleepLight;

        private HouseActivityLease companionActivity;
        private string companionId, companionMessage;
        private float nextCompanionLook;
        private bool companionLeaving;

        /// <summary>
        /// Housemates the house has taken out of the tub during this soak - for a conversation, or a
        /// pairing that came to nothing. They are not asked back until the player gets out: asked
        /// back, the next scan takes them again, and they are in and out every few seconds.
        /// </summary>
        private readonly System.Collections.Generic.HashSet<string> companionTaken = new System.Collections.Generic.HashSet<string>();

        /// <summary>The houseguest keeping the player company at the furniture, while one is.</summary>
        public string CompanionId => companionActivity != null && !companionActivity.Released ? companionId : null;

        /// <summary>
        /// Company in the hot tub. Once the player has settled, the housemate they get on with best
        /// - by the season's own scores, the id breaking a tie, and never somebody who dislikes
        /// them - walks over, changes and takes the other seat, and gets out when the player does.
        ///
        /// <para>Presentation only, like everything else the player does in the house: it is a
        /// furniture lease, not a conversation, and the house's own schedule outranks it. A
        /// conversation the season calls for takes the companion out of the tub as it would take
        /// them off a lounger; somebody the schedule has already paired off is never asked.</para>
        /// </summary>
        private void TickCompanion()
        {
            bool soaking = IsPlayerHouseActivityActive && playerActivityInHouse && playerActivityKind == HouseFurnitureActivity.Soak;
            if (companionActivity != null)
            {
                if (companionActivity.Released)
                {
                    // Out without being asked: the house wanted them.
                    if (!companionLeaving && soaking && companionId != null) companionTaken.Add(companionId);
                    EndCompanion();
                    return;
                }
                var playerPose = player != null ? player.GetComponent<HouseFurniturePose>() : null;
                if (!soaking || playerPose == null || playerPose.Ending) LetCompanionOut();
                return;
            }
            if (!soaking) { companionTaken.Clear(); return; }
            if (npcMeetings == null || projected == null || !NpcCanAdvance) return;
            var seat = player.GetComponent<HouseSeatPresentation>();
            if (seat == null || !seat.Settled || Time.unscaledTime < nextCompanionLook) return;
            nextCompanionLook = Time.unscaledTime + 2f;
            var mine = playerActivity.Anchor;
            var other = HouseInteractionAnchors.InScene(gameObject.scene).FirstOrDefault(anchor => anchor != mine
                && anchor.VenueId == mine.VenueId && anchor.transform.parent == mine.transform.parent && npcMeetings.ActivityAnchorAvailable(anchor));
            if (other == null) return;
            var state = projected;
            // Liking goes both ways here: the scores are one reading per direction, and somebody
            // the player likes can still have no time for them.
            foreach (var friend in state.Active.Where(c => !c.isPlayer && !companionTaken.Contains(c.id)
                             && state.Score(state.playerId, c.id) >= 0 && state.Score(c.id, state.playerId) >= 0
                             && !state.npcSocial.pending.Any(row => row.firstId == c.id || row.secondId == c.id))
                         .OrderByDescending(c => state.Score(state.playerId, c.id)).ThenBy(c => c.id, StringComparer.Ordinal))
            {
                var npc = housemates.FirstOrDefault(actor => actor != null && actor.Id == friend.id && actor.gameObject.activeInHierarchy);
                if (npc == null || npcMeetings.TryGetActivity(friend.id, out _)) continue;
                if (!npcMeetings.TryReserveActivity(friend.id, other, out var lease, out _)) continue;
                BeginFurniturePose(npc.gameObject, lease, HouseFurnitureActivity.Soak, float.PositiveInfinity);
                if (lease.Released) continue;
                companionActivity = lease; companionId = friend.id; companionLeaving = false;
                DressHousemate(friend.id);
                message = companionMessage = friend.name + " joins you in the hot tub.";
                Render();
                return;
            }
        }

        /// <summary>
        /// The player is getting out, so the companion does too - climbing out as the player does,
        /// not jumping from the water to the deck once the player is already out.
        /// </summary>
        private void LetCompanionOut()
        {
            if (companionLeaving) return;
            companionLeaving = true;
            var npc = housemates?.FirstOrDefault(actor => actor != null && actor.Id == companionId);
            var pose = npc != null ? npc.GetComponent<HouseFurniturePose>() : null;
            // The pose's own finish releases the lease, and the next tick ends the company.
            if (pose != null && pose.Active) pose.RequestFinish();
            else EndCompanion();
        }

        private void EndCompanion()
        {
            if (companionActivity != null && !companionActivity.Released) npcMeetings?.ReleaseActivity(companionActivity);
            companionActivity = null; companionLeaving = false;
            var leaving = companionId; companionId = null;
            if (leaving != null) DressHousemate(leaving);
            // "X joins you in the hot tub" is not left saying so once they have gone.
            if (companionMessage != null && message == companionMessage)
            {
                message = "";
                if (IsReady && isActiveAndEnabled) Render();
            }
            companionMessage = null;
        }

        /// <summary>
        /// A housemate in the clothes the moment calls for: swimwear while they keep the player
        /// company in the water, the phase's otherwise - changed behind the body the house can see.
        /// </summary>
        private void DressHousemate(string id)
        {
            if (projected == null) return;
            var npcs = projected.contestants.Where(c => !c.isPlayer).ToArray();
            for (int i = 0; i < housemates.Length && i < npcs.Length; i++)
            {
                if (housemates[i] == null || npcs[i].id != id) continue;
                var presentation = housemates[i].GetComponent<CharacterPresentation>();
                var dressed = HousemateDressed(npcs[i]);
                bool changing = id == CompanionId || (presentation != null && (presentation.IsChangingOutfit
                    || presentation.AppearanceSnapshot?.activeOutfit == CharacterOutfits.Swimwear));
                (changing ? CharacterPresentation.Dress(housemates[i].gameObject, dressed, Palette(i))
                    : CharacterPresentation.Attach(housemates[i].gameObject, dressed, Palette(i)))?.SetReducedMotion(reducedMotion);
                return;
            }
        }

        /// <summary>What a housemate wears now: swimwear as the player's company in the water, the phase's set otherwise.</summary>
        private Gamesim.Simulation.ContestantState HousemateDressed(Gamesim.Simulation.ContestantState model)
        {
            if (model.id != CompanionId) return CharacterOutfits.ForPhase(model, projected.phase);
            return CharacterOutfits.ForContext(WithWardrobe(model), CharacterOutfits.Swimwear);
        }

        /// <summary>
        /// Dims the fill light of the room the player is asleep in, and brings it back when they
        /// get up. The fills are Mixed lights under an indirect-only bake, so this takes down the
        /// direct light and leaves the baked bounce where it was: the room is dimmer, not dark.
        /// The light's own value is kept and put back exactly.
        /// </summary>
        private void TickSleepLight()
        {
            var seat = player != null ? player.GetComponent<HouseSeatPresentation>() : null;
            bool asleep = IsPlayerHouseActivityActive && playerActivityKind == HouseFurnitureActivity.Sleep
                && seat != null && seat.Settled && seat.Mode == HouseAnchorPose.Lie;
            if (asleep && sleepLight == null && playerActivity?.Anchor != null)
            {
                string name = playerActivity.Anchor.RoomId + " fill";
                sleepLight = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>())
                    .FirstOrDefault(light => light.name == name && light.isActiveAndEnabled);
                if (sleepLight != null) { sleepLightBase = sleepLight.intensity; sleepDim = 0f; }
            }
            if (sleepLight == null) return;
            bool still = cameraRig != null && cameraRig.ReducedMotion;
            sleepDim = still ? (asleep ? 1f : 0f)
                : Mathf.MoveTowards(sleepDim, asleep ? 1f : 0f, Time.unscaledDeltaTime / SleepDimSeconds);
            sleepLight.intensity = sleepLightBase * Mathf.Lerp(1f, SleepDimTo, Mathf.SmoothStep(0f, 1f, sleepDim));
            if (!asleep && sleepDim <= 0f) { sleepLight.intensity = sleepLightBase; sleepLight = null; }
        }
    }
}
