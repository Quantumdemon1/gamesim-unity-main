using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The overview (VISUAL-TARGET.md phase V5): the whole house from above through a
    /// near-orthographic lens, every room named where it is, the cast still walking under it.
    ///
    /// <para>A camera mode, not a panel. Nothing pauses, the HUD stays, the player can still be
    /// walked, and the first touch of the camera hands it back - so <see cref="IsPanelOpen"/> is
    /// untouched and the houseguests keep their own business going, which is the point of looking
    /// down on the house at all.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The rail entry and section name that mean the overview rather than a notebook page.</summary>
        public const string OverviewSection = "Overview";
        /// <summary>The rig's far pitch: the dollhouse's own steepest look, made whole-house.</summary>
        public const float OverviewPitch = 62f;
        /// <summary>Half the frame's height in metres; the house is forty deep, seen at a tilt.</summary>
        public const float OverviewOrthographicSize = 22f;
        public const float OverviewSeconds = 1.2f;
        /// <summary>
        /// How far toward the camera the overview looks from the house's centre, in metres. Centred,
        /// the near rooms fell behind the status line and the strip, and so did their chips; this
        /// raises the house about sixty pixels, into the band between the top bar and the status
        /// line, which is where mockup-03 draws its labelled rooms.
        /// </summary>
        public const float OverviewLift = 3.3f;

        /// <summary>The briefing's way back to the map, and the map's way back to the briefing (STRATEGY-LOOP-PLAN.md §4.6).</summary>
        public const string ShowMapCaption = "Show the map";
        public const string ShowBriefingCaption = "Show the briefing";

        private bool overviewOpen, briefingOpen;
        private RoomLabels roomLabels;

        public bool IsOverview => overviewOpen;
        /// <summary>
        /// The strategic briefing (the owner's screen #3) standing over the labelled house. The rail's
        /// Overview row opens the overview with it; "Show the map" puts it away and leaves the map -
        /// the chips and the floor still a way there - and "Show the briefing" brings it back.
        /// </summary>
        public bool IsBriefing => overviewOpen && briefingOpen;

        /// <summary>Opens the overview with its briefing, or leaves the overview when it is up. False when the house cannot be looked at right now.</summary>
        public bool ToggleOverview()
        {
            if (overviewOpen) { EndOverview(); return false; }
            return ShowBriefing();
        }

        /// <summary>The overview with the briefing over it: what the rail's Overview row opens.</summary>
        public bool ShowBriefing()
        {
            if (!ShowOverview()) return false;
            briefingOpen = true;
            Render();
            return true;
        }

        /// <summary>Puts the briefing away and leaves the map: the overview stays, its chips and floor a way there.</summary>
        public void HideBriefing()
        {
            if (!briefingOpen) return;
            briefingOpen = false;
            Render();
        }

        /// <summary>The overview as the house's map, with no briefing over it.</summary>
        public bool ShowOverview()
        {
            if (!IsReady || blockedRecovery || challengeActive || cameraRig == null) return false;
            if (IsPanelOpen) ClosePanels();
            overviewOpen = true;
            briefingOpen = false;
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = cameraRig.HouseCenter - Quaternion.Euler(0f, cameraRig.Yaw, 0f) * Vector3.forward * OverviewLift,
                Distance = cameraRig.FarthestDistance, Pitch = OverviewPitch, KeepYaw = true,
                Orthographic = true, OrthographicSize = OverviewOrthographicSize, Seconds = OverviewSeconds,
            });
            if (roomLabels == null) roomLabels = RoomLabels.Attach(gameObject);
            // Each chip is a way there: the overview is the house's map.
            roomLabels.Show(RoomMarkers(), largeText ? 1.2f : 1f, PressTravelBeacon, cameraRig.ViewCamera);
            Render();
            return true;
        }

        public void EndOverview()
        {
            if (!overviewOpen) return;
            LeaveOverview();
            if (cameraRig != null) cameraRig.ReleaseShot(OverviewSeconds);
            Render();
        }

        /// <summary>The state without the camera: the panel path and the input path both come through here.</summary>
        private void LeaveOverview()
        {
            overviewOpen = false;
            briefingOpen = false;
            if (roomLabels != null) roomLabels.Hide();
        }

        /// <summary>Camera input ends the overview from the rig's side; the chips and the column follow it out.</summary>
        private void TickOverview()
        {
            if (!overviewOpen || cameraRig == null || cameraRig.HasShot) return;
            LeaveOverview();
            Render();
        }

        /// <summary>The rooms and who stands in each, for the overview's column; the notebook's own reckoning.</summary>
        public IList<HouseMap.Room> WhoIsWhere() => HouseOccupancy(projected);

        private HouseRoomMarker[] RoomMarkers() => gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
            .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
            .OrderBy(marker => marker.RoomName, StringComparer.Ordinal)
            .ToArray();
    }
}
