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
    /// <summary>The notebook, the recaps and the season report: what the player reads rather than does.</summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// Who is standing in which room, right now, by nearest room marker.
        ///
        /// <para>Scene-local, like the diary-room lookup: a second loaded house must not contribute
        /// markers to this one. Read from live transforms rather than from the simulation, because
        /// the simulation does not model position — where a houseguest is standing is a fact about
        /// the scene, and claiming otherwise would be inventing state.</para>
        /// </summary>
        /// <summary>
        /// The room the player is standing in and who else is in it — the web build's "Current
        /// Location" card.
        ///
        /// <para>Read from the same occupancy the notebook's house map uses, which derives each
        /// houseguest's room from where their body actually is rather than from a stored field. So
        /// this cannot disagree with the map, and it cannot claim someone is nearby who is not.</para>
        ///
        /// <para>It earns its place on the social screen because the decision being made there is
        /// who to talk to, and that was previously answerable only by opening the notebook or
        /// turning the camera.</para>
        /// </summary>
        private void CurrentLocation(EpisodeState state)
        {
            var here = HouseOccupancy(state)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return;

            var others = here.Occupants.Where(person => !person.IsPlayer).Select(person => person.Name).ToArray();
            hud.Heading("CURRENT LOCATION  ·  " + here.Name.ToUpperInvariant());
            hud.Paragraph(others.Length == 0
                ? "You have this room to yourself."
                : others.Length + (others.Length == 1 ? " houseguest here: " : " houseguests here: ") + string.Join(", ", others));
        }

        /// <summary>
        /// "The Diplomat · 31 · Mediator", or as much of it as the save actually holds.
        /// </summary>
        public static string CardLine(ContestantState actor)
        {
            if (actor == null) return null;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(actor.archetype)) parts.Add(actor.archetype);
            if (actor.age > 0) parts.Add(actor.age.ToString());
            if (!string.IsNullOrEmpty(actor.occupation)) parts.Add(actor.occupation);
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        private List<HouseMap.Room> HouseOccupancy(EpisodeState state)
        {
            var rooms = new List<HouseMap.Room>();
            if (state == null) return rooms;

            var markers = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
                .OrderBy(marker => marker.RoomName, StringComparer.Ordinal)
                .ToArray();
            if (markers.Length == 0) return rooms;

            var occupants = new Dictionary<string, List<HouseMap.Occupant>>();
            foreach (var marker in markers) occupants[marker.RoomName] = new List<HouseMap.Occupant>();

            foreach (var visual in gameObject.scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<CharacterPresentation>(true)))
            {
                var actor = state.Find(visual.CharacterId);
                if (actor == null || actor.status != ContestantStatus.Active) continue;

                HouseRoomMarker nearest = null;
                float best = float.MaxValue;
                foreach (var marker in markers)
                {
                    float distance = (marker.transform.position - visual.transform.position).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance; nearest = marker;
                }
                if (nearest == null) continue;

                occupants[nearest.RoomName].Add(new HouseMap.Occupant(actor.id, actor.name,
                    CharacterPortraits.Get(actor),
                    actor.id == state.playerId, actor));
            }

            foreach (var marker in markers) rooms.Add(new HouseMap.Room(marker.RoomName, occupants[marker.RoomName]));
            return rooms;
        }

        /// <summary>
        /// The season so far, grouped by week and read forwards.
        ///
        /// <para>The record was a flat reverse-chronological list of the last thirty-five committed
        /// lines. That is a log, and a log is the right thing for debugging and the wrong thing for
        /// remembering a story: it opens on the most recent line, gives no indication which week
        /// anything belongs to, and reads backwards, so cause follows effect down the page.</para>
        ///
        /// <para>Grouped and forwards, it reads as what happened. Nothing is invented and nothing is
        /// paraphrased — every line is the text the simulation committed, filtered by the same
        /// audience rule as everywhere else.</para>
        /// </summary>
        private void RenderStorySoFar(EpisodeState state)
        {
            hud.Heading("THE STORY SO FAR", UiTheme.Gold);
            hud.Eyebrow("PREVIOUSLY ON BIG BROTHER", UiTheme.Gold);
            hud.Mark(NotebookSection.Story);

            var visible = state.events
                .Where(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                // Phase markers are scaffolding for the engine, not events in the story.
                .Where(e => e.kind != "phase")
                .ToList();
            if (visible.Count == 0) { hud.Paragraph("Nothing has happened yet."); return; }

            // The most recent weeks, oldest first inside each. A long season would otherwise push
            // this week off the bottom of a panel that opens at the top.
            const int weeks = 3;
            int newest = visible.Max(e => e.week);
            int oldest = Mathf.Max(1, newest - (weeks - 1));
            if (oldest > 1) hud.Paragraph("Earlier weeks are in the save; the last " + weeks + " are shown here.");

            for (int week = oldest; week <= newest; week++)
            {
                var entries = visible.Where(e => e.week == week).ToList();
                if (entries.Count == 0) continue;
                hud.Heading(week == newest ? "Week " + week + " · this week" : "Week " + week, UiTheme.Gold);
                foreach (var entry in entries) hud.Paragraph(entry.text);
            }
        }

        /// <summary>Opens the notebook, if needed, and scrolls to a section.</summary>
        public void ShowNotebookSection(string section)
        {
            // The rail's last entry is not a page: it is the house itself, from above.
            if (section == OverviewSection) { ToggleOverview(); return; }
            journalOpen = true;
            phaseOpen = false; settingsOpen = false; diaryOpen = false;
            hud.RequestScrollTo(section);
            Render();
        }

        /// <summary>
        /// Whether the player is out of the game and the season is running on without them.
        ///
        /// <para>The web game stores this as a sticky <c>isSpectatorMode</c> flag set when the
        /// evicted houseguest is the player. Here it is derived from the player's status instead,
        /// which is equivalent — a juror never returns to the house — and avoids adding a field to
        /// the save schema and a migration to go with it.</para>
        ///
        /// <para>The finale is excluded: once the season is over everyone is a spectator, and the
        /// report carries its own badge for someone who watched from the jury.</para>
        /// </summary>
        public static bool Spectating(EpisodeState state)
        {
            if (state == null || state.phase == EpisodePhase.Finished) return false;
            var you = state.Find(state.playerId);
            return you != null && you.status != ContestantStatus.Active;
        }

        /// <summary>The line under the spectator caption: when they went, and what is left.</summary>
        private static string SpectatorDetail(EpisodeState state)
        {
            var you = state.Find(state.playerId);
            int week = you != null && you.nominationWeeks != null && you.nominationWeeks.Count > 0
                ? you.nominationWeeks[you.nominationWeeks.Count - 1]
                : state.week;
            string seat = you != null && you.status == ContestantStatus.Jury
                ? "You are on the jury, and you will vote for the winner."
                : "You were evicted before jury, so you have no vote in the finale.";
            return "You were evicted in week " + week + ". " + seat
                + " The house plays on; you can still watch every ceremony and read the notebook.";
        }

        /// <summary>
        /// Opens the season report on the committed season.
        ///
        /// <para>It reads <see cref="Snapshot"/> rather than the projection, for the reason every
        /// other presentation surface here does: a projected result is not a fact, and the last
        /// screen of a season is the worst possible place to show an outcome the save does not
        /// hold.</para>
        /// </summary>
        /// <summary>
        /// Opens the week's recap once the eviction has finished being narrated.
        ///
        /// <para>A season that has just ended does not get one: the finale plays, and
        /// <see cref="SeasonReport"/> is the screen that closes it. A recap in front of the winner
        /// would be a summary of the week interrupting the end of the season.</para>
        /// </summary>
        private void QueueWeeklyRecap(int week)
        {
            if (weeklyRecap == null || week < 1) return;
            if (recapWait != null) StopCoroutine(recapWait);
            recapWait = StartCoroutine(OpenWeeklyRecap(week, Snapshot.revision));
        }

        /// <summary>
        /// Phases where there is no next week to continue to.
        ///
        /// <para>The last regular eviction is still an eviction, so it queues a recap like any
        /// other — and then the season walks straight into the endgame while the vote reveal is
        /// still playing. A recap that opened there would be a summary of the week laid over the
        /// jury questioning that replaced it.</para>
        /// </summary>
        private static bool SeasonIsEnding(EpisodePhase phase) =>
            phase == EpisodePhase.FinalEviction || phase == EpisodePhase.JuryQuestioning
            || phase == EpisodePhase.FinalSpeeches || phase == EpisodePhase.Jury
            || phase == EpisodePhase.Finished;

        private IEnumerator OpenWeeklyRecap(int week, int queuedAt)
        {
            // Nothing here is timed. It waits on the cards' own state, so reduced motion and
            // batchmode — where those beats collapse to nothing — cost exactly one frame.
            yield return null;
            while ((voteReveal != null && voteReveal.IsPlaying)
                   || (takeover != null && takeover.IsPlaying))
                yield return null;

            recapWait = null;
            var committed = Snapshot;
            if (SeasonIsEnding(committed.phase)) yield break;
            if (seasonReport != null && seasonReport.IsShowing) yield break;
            // The player has already moved on. Nothing commits while the reveal plays unless
            // somebody pressed something, and a recap that arrives after the next decision has been
            // taken is an interruption rather than a summary.
            if (committed.revision != queuedAt) yield break;
            // ClosePanels is what dismissing does: it puts the player back in the house and
            // repaints, which hiding the screen on its own would not.
            OpenRecap(() => weeklyRecap.Show(committed, ClosePanels));
        }

        /// <summary>
        /// Puts the recap up the way every other full-screen panel goes up.
        ///
        /// <para>Taking the house away is explicit here, not a consequence of rendering: a render
        /// redraws the HUD and nothing else, and the one call that gates movement on
        /// <see cref="IsPanelOpen"/> lives in <c>Project</c>, which only runs when a command
        /// commits. Showing a scrim without this leaves the player walking around behind it.</para>
        /// </summary>
        private void OpenRecap(Action show)
        {
            PauseNpcSocialForPanel();
            // Without a render: the panel being cleared is replaced in the same breath, and it
            // also hides the recap, so a repaint here would draw a screen about to be reopened.
            ClosePanelsInternal(false);
            show();
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            Render();
        }

        /// <summary>
        /// Opens a played week's recap for review. The player's own choice, not a beat.
        ///
        /// <para>Dismissing puts them back where they came from. Reached from the notebook it
        /// reopens the notebook, because a control that closes the screen it was pressed on makes
        /// the player navigate back to it every time they check a second week.</para>
        /// </summary>
        public void ReviewWeek(int week)
        {
            if (weeklyRecap == null) return;
            bool fromNotebook = journalOpen;
            var committed = Snapshot;
            OpenRecap(() => weeklyRecap.Review(committed, week,
                fromNotebook ? (Action)OpenJournal : ClosePanels));
        }

        public void ShowSeasonReport()
        {
            if (seasonReport == null) return;
            var committed = Snapshot;
            seasonReport.Show(committed, id =>
            {
                var actor = committed.Find(id);
                return actor == null ? null : CharacterPortraits.Get(actor);
            }, OpenJournal, CareerNow());
        }
    }
}
