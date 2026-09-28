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

            hud.Heading("CURRENT LOCATION  ·  " + here.Name.ToUpperInvariant());
            hud.Paragraph(CompanyLine(here, false));
        }

        /// <summary>
        /// Who else is in a room, in the notebook's own words: "You have this room to yourself." or
        /// "2 houseguests here: ..." - by full name in the notebook, first names in the status line.
        /// </summary>
        private static string CompanyLine(HouseMap.Room room, bool firstNames)
        {
            var others = (room.Occupants ?? new List<HouseMap.Occupant>()).Where(person => !person.IsPlayer)
                .Select(person => firstNames ? person.Name.Split(' ')[0] : person.Name).ToArray();
            return others.Length == 0
                ? "You have this room to yourself."
                : others.Length + (others.Length == 1 ? " houseguest here: " : " houseguests here: ") + string.Join(", ", others);
        }

        /// <summary>The player's room and who else is in it, in the status line's words; null when the house cannot say.</summary>
        private string ArrivalLine()
        {
            var here = HouseOccupancy(projected)
                .FirstOrDefault(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            if (string.IsNullOrEmpty(here.Name)) return null;
            return "In the " + RoomLabels.InSentence(here.Name) + ".  " + CompanyLine(here, true);
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

        private List<HouseMap.Room> HouseOccupancy(EpisodeState state) => HouseOccupancy(state, out _);

        /// <summary>
        /// Who is standing in which room, right now, by nearest room marker: one entry for each
        /// houseguest in the house, found through the body the director bound to them.
        ///
        /// <para>It used to scan every character body in the scene, spare ones included, and look
        /// each up by the id it carried - so a switched-off spare that still held an old id could
        /// list someone twice, and a houseguest whose body did not resolve was silently dropped.
        /// Now it walks the roster: everyone in the house is either in exactly one room or in
        /// <paramref name="unplaced"/>, which says only that the house has no body to read for them.</para>
        /// </summary>
        private List<HouseMap.Room> HouseOccupancy(EpisodeState state, out List<HouseMap.Occupant> unplaced)
        {
            var rooms = new List<HouseMap.Room>();
            unplaced = new List<HouseMap.Occupant>();
            if (state == null) return rooms;
            // The finalists stay in the house when the season ends; everyone else in it is Active.
            var inHouse = state.contestants.Where(c => c.status == ContestantStatus.Active
                || c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp).ToList();

            var markers = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
                .OrderBy(marker => marker.RoomName, StringComparer.Ordinal)
                .ToArray();
            HouseMap.Occupant Person(ContestantState actor) =>
                new HouseMap.Occupant(actor.id, actor.name, CharacterPortraits.Get(actor), actor.id == state.playerId, actor);
            if (markers.Length == 0) { foreach (var actor in inHouse) unplaced.Add(Person(actor)); return rooms; }

            var occupants = new Dictionary<string, List<HouseMap.Occupant>>();
            foreach (var marker in markers) occupants[marker.RoomName] = new List<HouseMap.Occupant>();

            foreach (var actor in inHouse)
            {
                var body = BodyFor(actor.id);
                if (body == null) { unplaced.Add(Person(actor)); continue; }

                HouseRoomMarker nearest = null;
                float best = float.MaxValue;
                foreach (var marker in markers)
                {
                    float distance = (marker.transform.position - body.position).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance; nearest = marker;
                }
                occupants[nearest.RoomName].Add(Person(actor));
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
            hud.Heading("THE STORY SO FAR", UiTheme.Heading);
            hud.Eyebrow("PREVIOUSLY ON GAMESIM", UiTheme.Muted);
            hud.Mark(NotebookSection.Story);
            // The plays page (plan 30 §4): what the player is chasing, then how the finished ones went;
            // then the season's threads (plan 31), running and ended.
            PlaysBlock(state, finished: true);
            ThreadsBlock(state, finished: true);

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
                foreach (var entry in entries) hud.Paragraph(StoryText.Log(state, entry));
            }
        }

        /// <summary>
        /// Opens the notebook, if needed, and scrolls to a section.
        ///
        /// <para>Turning a page of an open notebook is just that. Anything else - the overview, a
        /// conversation, the house activities, the diary, the episode screen - is closed first, the
        /// way <see cref="OpenJournal"/> closes it. The rail used to set the notebook open over
        /// whatever was up: from the overview, the overview stayed on behind the page with its
        /// camera shot and its room chips, and the rail kept "Overview" lit over "Houseguests";
        /// from the house activities, the activities stayed on screen while the rail lit the page
        /// that had been asked for.</para>
        /// </summary>
        public void ShowNotebookSection(string section)
        {
            // The rail's last entry is not a page: it is the house itself, from above.
            if (section == OverviewSection) { ToggleOverview(); return; }
            bool onlyTheNotebook = journalOpen && !overviewOpen && !houseActivitiesOpen && focusedNpc == null
                && !phaseOpen && !settingsOpen && !diaryOpen && !challengeActive;
            if (!onlyTheNotebook) { OpenNotebookAt(section); return; }
            journalSection = section;
            // The rail's Houseguests entry is the directory, from a profile as from anywhere else.
            profileId = null;
            hud.RequestScrollTo(section);
            Render();
        }

        /// <summary>What a redesigned notebook page says of itself in its head.</summary>
        private (string Title, string Subtitle) NotebookPageHead(string section)
        {
            if (section == NotebookSection.People && ProfileId != null)
                return ("Houseguest profile", "A focused view of the same permitted records \u2014 not access to private thoughts");
            if (section == NotebookSection.Rooms) return ("Who is where", "Room directory \u00b7 where everyone in the house is right now");
            if (section == NotebookSection.People) return ("Houseguests", "People, their status, and your own recorded trust in each \u2014 separate at a glance");
            if (section == NotebookSection.Votes) return ("The vote", "Recorded eviction results, and only the ballot information available to your character");
            if (section == NotebookSection.Notes) return ("Your notes", "What your character has on each houseguest — their word, what they told you, what you read, and what they put to you");
            return (null, null);
        }

        /// <summary>Which rooms the room directory shows. View state: never saved, reset when the notebook opens.</summary>
        private HouseMap.Filter roomsFilter;

        /// <summary>
        /// The room directory (Refinement Kit 6's "Who is where"): filters and the house's count over
        /// the room cards, and the house activities at the foot.
        /// </summary>
        private void RenderNotebookRooms(EpisodeState state)
        {
            var rooms = HouseOccupancy(state, out var unplaced);
            if (roomsFilter == HouseMap.Filter.Unplaced && unplaced.Count == 0) roomsFilter = HouseMap.Filter.All;
            var tabs = new List<(string, bool, Action)>
            {
                ("All rooms", roomsFilter == HouseMap.Filter.All, () => { roomsFilter = HouseMap.Filter.All; Render(); }),
                ("Occupied", roomsFilter == HouseMap.Filter.Occupied, () => { roomsFilter = HouseMap.Filter.Occupied; Render(); }),
            };
            if (unplaced.Count > 0)
                tabs.Add(("Location unavailable", roomsFilter == HouseMap.Filter.Unplaced, () => { roomsFilter = HouseMap.Filter.Unplaced; Render(); }));
            hud.FilterRow("Room filters", tabs, HouseMap.Summary(rooms, unplaced));
            hud.Mark(NotebookSection.Rooms);
            hud.HouseMapPanel(rooms, unplaced, roomsFilter);
            hud.NotebookFooter("Read from where everyone is standing right now. The house shows everyone; an empty room is empty.",
                "House activities", OpenHouseActivities);
        }

        /// <summary>Which houseguests the directory lists. View state, like the room filter.</summary>
        private enum PeopleFilter { All, Active, Jury }
        private PeopleFilter peopleFilter;
        /// <summary>What is typed in the directory's search. Held here so a repaint keeps it.</summary>
        private string peopleSearch = string.Empty;
        /// <summary>Whose profile the Houseguests page is showing, or null for the directory.</summary>
        private string profileId;

        /// <summary>The houseguest whose profile is on screen, or null when it is not.</summary>
        public string ProfileId => journalOpen && journalSection == NotebookSection.People ? profileId : null;

        /// <summary>
        /// Opens a houseguest's profile on the Houseguests page. It is a view of that page - the
        /// rail keeps Houseguests lit, and "Back to Houseguests" returns to the directory as it was
        /// left, its filter and search intact.
        /// </summary>
        public void ShowHouseguestProfile(string contestantId)
        {
            var actor = Snapshot != null ? Snapshot.Find(contestantId) : null;
            if (actor == null || actor.isPlayer) return;
            if (!journalOpen || journalSection != NotebookSection.People) OpenNotebookAt(NotebookSection.People, scroll: false);
            profileId = contestantId;
            hud.RequestScrollTo(NotebookSection.People);
            Render();
        }

        /// <summary>Back from a profile to the directory.</summary>
        public void BackToHouseguests()
        {
            if (profileId == null) return;
            profileId = null;
            hud.RequestScrollTo(NotebookSection.People);
            Render();
        }

        /// <summary>Why a conversation has nothing to choose outside free time, in the house's own word for it.</summary>
        public const string ConversationUnavailableLine = "Conversation unavailable during this ceremony.";

        /// <summary>
        /// Who holds what this week, as the episode screen says it: "HoH: X  ·  Veto holder: Y  ·
        /// Nominees: A and B", each part only when it is so. It was three paragraphs, a third of the
        /// panel before anything could be done. Null when nobody holds anything yet.
        /// </summary>
        public static string HouseStatus(EpisodeState state)
        {
            if (state == null) return null;
            var parts = new List<string>();
            if (state.hohId != null && state.Find(state.hohId) != null) parts.Add("HoH: " + state.Find(state.hohId).name);
            if (state.vetoHolderId != null && state.Find(state.vetoHolderId) != null) parts.Add("Veto holder: " + state.Find(state.vetoHolderId).name);
            var nominees = state.nominees.Select(id => state.Find(id)).Where(c => c != null).Select(c => c.name).ToList();
            if (nominees.Count > 0) parts.Add("Nominees: " + string.Join(" and ", nominees));
            return parts.Count > 0 ? string.Join("  \u00b7  ", parts) : null;
        }

        /// <summary>A houseguest's place in the season, as one word.</summary>
        public static string StatusWord(ContestantStatus status)
        {
            switch (status)
            {
                case ContestantStatus.Active: return "Active";
                case ContestantStatus.Jury: return "Jury";
                case ContestantStatus.Evicted: return "Evicted";
                case ContestantStatus.Winner: return "Winner";
                case ContestantStatus.RunnerUp: return "Runner-up";
                case ContestantStatus.Expelled: return "Removed";
                default: return status.ToString();
            }
        }

        /// <summary>
        /// Your trust in someone as the notebook prints it: the committed score, whole and signed.
        /// It is the player's own reading, so it is shown as the number it is - no band name is
        /// derived from it here.
        /// </summary>
        public static string TrustFigure(double score) => score.ToString("+0;-0;0");

        private static Color TrustTint(double score)
        {
            double whole = Math.Round(score, MidpointRounding.AwayFromZero);
            return whole > 0 ? UiTheme.Allied : whole < 0 ? UiTheme.Conflict : UiTheme.Muted;
        }

        /// <summary>
        /// The houseguest directory (Kit 6's preview 03): All, Active and Jury, a search, and a
        /// row a houseguest in columns - or, with one chosen, their profile (preview 07).
        /// </summary>
        private void RenderNotebookPeople(EpisodeState state)
        {
            var chosen = profileId != null ? state.Find(profileId) : null;
            if (chosen != null && !chosen.isPlayer) { RenderHouseguestProfile(state, chosen); return; }
            profileId = null;

            var others = state.contestants.Where(c => !c.isPlayer).ToList();
            int active = others.Count(c => c.status == ContestantStatus.Active);
            int jury = others.Count(c => c.status == ContestantStatus.Jury);
            // The counts are in the captions so they cannot be confused with a row's status word:
            // a pill reading "Active" and a row reading "Active" would be the same control by name.
            var tabs = new List<(string, bool, Action)>
            {
                ("All \u00b7 " + others.Count, peopleFilter == PeopleFilter.All, () => { peopleFilter = PeopleFilter.All; Render(); }),
                ("Active \u00b7 " + active, peopleFilter == PeopleFilter.Active, () => { peopleFilter = PeopleFilter.Active; Render(); }),
                ("Jury \u00b7 " + jury, peopleFilter == PeopleFilter.Jury, () => { peopleFilter = PeopleFilter.Jury; Render(); }),
            };
            var row = hud.FilterRow("Houseguest filters", tabs);
            hud.SearchBox(row, "Search houseguests", peopleSearch, words => peopleSearch = words ?? string.Empty);
            hud.Mark(NotebookSection.People);

            var shown = others.Where(c => peopleFilter == PeopleFilter.All
                || (peopleFilter == PeopleFilter.Active ? c.status == ContestantStatus.Active : c.status == ContestantStatus.Jury));
            var entries = shown.Select(c =>
            {
                string id = c.id;
                double score = state.Score(state.playerId, id);
                return new EpisodeHud.RosterEntry
                {
                    Name = c.name, Detail = CardLine(c), Status = StatusWord(c.status),
                    Trust = TrustFigure(score), TrustTint = TrustTint(score), Character = c,
                    Open = () => ShowHouseguestProfile(id),
                };
            }).ToList();
            string empty = peopleFilter == PeopleFilter.Jury ? "No one is on the jury yet."
                : peopleFilter == PeopleFilter.Active ? "No other houseguest is still in the house."
                : "There are no other houseguests.";
            hud.RosterTable(entries, peopleSearch, empty);
            hud.NotebookFooter("Trust is your character\u2019s view. Mood, jury status, and relationships are not the same field.",
                "House activities", OpenHouseActivities);
        }

        /// <summary>
        /// A houseguest's profile: the card they came in with, your trust in them, what you
        /// remember of them, and the records you hold that involve them. The profile is a closer
        /// look at what the notebook already knows - nothing on it is private to them.
        /// </summary>
        private void RenderHouseguestProfile(EpisodeState state, ContestantState actor)
        {
            string first = FirstName(actor.name);
            double score = state.Score(state.playerId, actor.id);
            bool inHouse = actor.status == ContestantStatus.Active || actor.status == ContestantStatus.Winner
                || actor.status == ContestantStatus.RunnerUp;
            var facts = new List<string>();
            if (actor.age > 0) facts.Add(actor.age.ToString());
            if (!string.IsNullOrEmpty(actor.occupation)) facts.Add(actor.occupation);
            var view = new EpisodeHud.ProfileView
            {
                Name = actor.name,
                Archetype = actor.archetype,
                Facts = facts.Count > 0 ? string.Join(" \u00b7 ", facts) : null,
                Hometown = string.IsNullOrEmpty(actor.hometown) ? null : "From " + actor.hometown,
                About = string.IsNullOrEmpty(actor.bio) ? null : actor.bio,
                Status = "Status: " + StatusWord(actor.status),
                // Mood is what the house can see of someone; a juror is not in the house to be seen.
                Mood = inHouse ? MoodLine(state, actor) : null,
                Trust = TrustFigure(score),
                TrustTint = TrustTint(score),
                TrustNote = "This is your character\u2019s relationship value, not " + first + "\u2019s private opinion of you.",
                Character = actor,
                Traits = actor.traits != null ? actor.traits.Where(t => !string.IsNullOrEmpty(t)).ToList() : new List<string>(),
                Memories = state.memories
                    .Where(m => m.ownerId == state.playerId && m.subjectId == actor.id && !string.IsNullOrEmpty(m.text))
                    .OrderByDescending(m => m.week).Take(3)
                    .Select(m => "Week " + m.week + " \u00b7 " + m.text).ToList(),
                Records = ProfileRecords(state, actor, first),
                Back = BackToHouseguests,
            };
            hud.HouseguestProfile(view);
            hud.Mark(NotebookSection.People);
        }

        /// <summary>
        /// A houseguest's mood as the house can read it, with whom it is about when everybody saw
        /// why (<see cref="EpisodeEngine.MoodTarget"/>): "Mood: Angry at Jordan", "Mood: Upset at
        /// you". Null when there is no mood to read.
        /// </summary>
        public static string MoodLine(EpisodeState state, ContestantState actor)
        {
            if (state == null || actor == null || string.IsNullOrEmpty(actor.mood)) return null;
            string target = EpisodeEngine.MoodTarget(state, actor.id, out _);
            var about = target != null ? state.Find(target) : null;
            if (about == null) return "Mood: " + actor.mood;
            string name = about.id == state.playerId ? "you" : (about.name ?? "").Split(' ')[0];
            return "Mood: " + actor.mood + " at " + name;
        }

        /// <summary>
        /// The records the player holds that involve someone: the season's ceremony counts, which
        /// the whole house watched, and the promises, alliances and declarations the player was a
        /// party to. Nothing between two other people.
        /// </summary>
        private List<string> ProfileRecords(EpisodeState state, ContestantState actor, string first)
        {
            var lines = new List<string>
            {
                "Head of Household " + Times(actor.hohWins) + " \u00b7 Veto " + Times(actor.vetoWins)
                    + " \u00b7 Nominated " + Times(actor.timesNominated),
            };
            int personal = 0;
            foreach (var promise in state.promises.Where(p => (p.fromId == state.playerId && p.toId == actor.id)
                         || (p.fromId == actor.id && p.toId == state.playerId)))
            {
                bool mine = promise.fromId == state.playerId;
                lines.Add("Week " + promise.week + ": " + (mine ? "You promised " + first : first + " promised you")
                    + " " + RelationshipWeb.PromiseWord(promise.kind) + " \u00b7 " + PromiseStanding(promise.status));
                personal++;
            }
            foreach (var alliance in state.alliances.Where(a => a.members.Contains(state.playerId) && a.members.Contains(actor.id)))
            {
                lines.Add("You are both in " + alliance.name + (alliance.active ? " \u00b7 active" : " \u00b7 ended"));
                personal++;
            }
            foreach (var oath in state.loyaltyOaths.Where(o => (o.playerId == state.playerId && o.targetId == actor.id)
                         || (o.playerId == actor.id && o.targetId == state.playerId)))
            {
                lines.Add("Week " + oath.week + ": " + (oath.playerId == state.playerId
                    ? "You declared loyalty to " + first : first + " declared loyalty to you"));
                personal++;
            }
            if (personal == 0) lines.Add("No promises, alliances or declarations between you.");
            return lines;
        }

        private static string Times(int count) => count == 1 ? "once" : count + " times";

        private static string FirstName(string name) =>
            string.IsNullOrEmpty(name) ? string.Empty : name.Split(' ')[0];

        /// <summary>Which half of the vote page is showing. View state, like the filters.</summary>
        private enum VotesTab { Results, Ballots, Read }
        private VotesTab votesTab;

        /// <summary>
        /// The vote page (Kit 6's preview 04): the season's eviction results in one tab and the
        /// ballots made public at each reveal in the other, read from the public record by
        /// <see cref="VoteRecords"/>.
        ///
        /// <para>It read the ballot box, which the engine empties when the next week begins - so
        /// from week two's first competition it told a season that had evicted someone that
        /// "Nobody has voted yet this season." What it says when there is nothing to show now
        /// depends on why: no eviction yet, a record that has rolled out of the log, or a vote
        /// under way whose ballots are still private.</para>
        /// </summary>
        private void RenderNotebookVotes(EpisodeState state)
        {
            var book = VoteRecords.Read(state);
            // The read has a tab of its own while there is a vote to read; outside a campaign the
            // tab is not there, and a page left on it opens on the results.
            bool readable = VoteRead.Available(state);
            if (!readable && votesTab == VotesTab.Read) votesTab = VotesTab.Results;
            var tabs = new List<(string, bool, Action)>
            {
                ("Eviction results", votesTab == VotesTab.Results, () => { votesTab = VotesTab.Results; Render(); }),
                ("Known ballots", votesTab == VotesTab.Ballots, () => { votesTab = VotesTab.Ballots; Render(); }),
            };
            if (readable) tabs.Add((EpisodeHud.VoteReadTabCaption, votesTab == VotesTab.Read, () => { votesTab = VotesTab.Read; Render(); }));
            hud.FilterRow("Vote tabs", tabs);
            // The mark exists in every state: it is what the rail scrolls to, and an absent one is
            // the difference between an empty page and no page at all.
            hud.Mark(NotebookSection.Votes);
            if (votesTab == VotesTab.Read) RenderVoteRead(state);
            else if (votesTab == VotesTab.Results) RenderEvictionResults(book);
            else RenderKnownBallots(state, book);
            hud.NotebookFooter("Every eviction ballot is made public when its vote is revealed. Until then the only ballot you know is your own.",
                "House activities", OpenHouseActivities);
        }

        /// <summary>
        /// The read (STRATEGY-LOOP-PLAN.md section 2): the whip count and one card per voter, built
        /// from the vote model's own terms and stripped of what the player has not learned. A read
        /// is what you have learned, not a ballot; the lock line says so.
        /// </summary>
        private void RenderVoteRead(EpisodeState state)
        {
            var sheet = VoteRead.Read(state);
            var first = state.Find(sheet.nomineeIds[0]);
            var second = state.Find(sheet.nomineeIds[1]);
            int voters = sheet.voters.Count + (EpisodeEngine.Voters(state).Any(v => v.isPlayer) ? 1 : 0);
            var lines = new List<string>
            {
                "Evict " + first.name + " " + sheet.evictFirst + " \u00b7 Evict " + second.name + " " + sheet.evictSecond + " \u00b7 Unknown " + sheet.unknown,
                voters + (voters == 1 ? " vote" : " votes") + " this week; a tie goes to the Head of Household.",
            };
            string headline = sheet.predictedEvicteeId != null ? "The house is leaning " + state.Find(sheet.predictedEvicteeId).name
                : sheet.unknown == sheet.voters.Count ? "No read on the house yet" : "Too close to call";
            hud.RecordCard(EpisodeHud.WhipCountName, "THIS WEEK \u00b7 THE READ", headline, UiTheme.Paper, lines);
            foreach (var read in sheet.voters)
            {
                var voter = state.Find(read.voterId);
                if (voter == null) continue;
                bool blank = read.confidence == VoteRead.Unknown && read.saysId == null;
                hud.RecordCard(EpisodeHud.VoteReadCardPrefix + voter.name, voter.name.ToUpperInvariant(), ReadHeadline(state, read),
                    blank ? UiTheme.Muted : UiTheme.Paper, ReadLines(state, read));
            }
            hud.LockNote(EpisodeHud.VotesPrivacyName, "A read is what you have learned, not a ballot. Ask them straight, read them, listen in, or hear it from an ally.");
        }

        /// <summary>What the read says of a voter, in a line: their lean and how sure it is, or what they said, or nothing yet.</summary>
        public static string ReadHeadline(EpisodeState state, VoteRead.VoterRead read)
        {
            if (read.confidence == VoteRead.Unknown)
                return read.saysId != null ? "Says: evict " + state.Find(read.saysId).name : "No read yet";
            string lean = read.confidence == VoteRead.Torn ? "Torn"
                : (read.confidence == VoteRead.Firm ? "Firm: evict " : "Leaning: evict ") + state.Find(read.leaningId).name
                    + (read.exact ? " by " + read.knownMargin.ToString("0") : "");
            // What they said is worth a word when it is not what the read says.
            return read.saysId != null && read.saysId != read.leaningId ? lean + " \u00b7 says " + state.Find(read.saysId).name : lean;
        }

        private static List<string> ReadLines(EpisodeState state, VoteRead.VoterRead read)
        {
            var lines = new List<string>();
            foreach (var claim in read.claims)
                lines.Add((claim.source == ClaimSource.Told ? "Told you: evict " : claim.source == ClaimSource.Overheard ? "Overheard: evict " : "An ally heard: evict ")
                    + state.Find(claim.targetId).name + " (week " + claim.week + ")");
            if (read.knownTerms.Count > 0) lines.Add("You know: " + string.Join(", ", read.knownTerms.Select(TermWords)) + ".");
            if (read.unknownTerms > 0)
                lines.Add(read.unknownTerms == 1 ? "1 thing you don't know could change this." : read.unknownTerms + " things you don't know could change this.");
            return lines;
        }

        private static string TermWords(string code)
        {
            switch (code)
            {
                case "relationship": return "how they see the nominees";
                case "alliance": return "their alliances";
                case "blocPressure": return "their alliance votes as one";
                case "deal": return "their deals";
                case "grudge": return "a grudge";
                case "bond": return "a bond";
                case "history": return "your history with them";
                case "obligation": return "your deal with them";
                case "plea": return "your plea to them";
                default: return code;
            }
        }

        private void RenderEvictionResults(VoteRecords.Book book)
        {
            if (book.VoteInProgress)
            {
                var own = book.OwnPendingBallot;
                hud.RecordCard(EpisodeHud.VotesInProgressName, "THIS WEEK \u00b7 VOTE IN PROGRESS",
                    own != null ? "Your ballot is recorded." : "The house is voting.", UiTheme.Paper,
                    new[] { own != null
                        ? "You voted to evict " + own.TargetName + ". The others are private until the reveal."
                        : "Every ballot is private until the reveal." });
            }
            foreach (var record in book.Records)
            {
                string headline;
                Color ink = UiTheme.Paper;
                var lines = new List<string>();
                if (!record.Complete)
                {
                    headline = "The result is no longer in your notebook";
                    ink = UiTheme.Muted;
                    lines.Add("This week's ballots survive under Known ballots. Without the result, no tally is given.");
                }
                else
                {
                    if (record.EvictedIsPlayer) { headline = "You were evicted"; ink = UiTheme.Conflict; }
                    else headline = record.EvictedName != null ? record.EvictedName + " was evicted" : "An eviction is recorded";
                    if (record.FinalDecision) lines.Add("Decided by the final Head of Household, not by a vote.");
                    else if (record.Counts.Count > 0)
                        lines.Add("Votes to evict: " + string.Join(" \u00b7 ", record.Counts.Select(c => c.Name + " " + c.Votes)));
                    if (record.TieBroken) lines.Add("The vote was tied; the Head of Household broke the tie.");
                    if (!record.FinalDecision && record.Ballots.Count > 0)
                        lines.Add(record.Ballots.Count + (record.Ballots.Count == 1 ? " ballot" : " ballots") + " made public at the reveal, under Known ballots.");
                }
                hud.RecordCard(EpisodeHud.VoteRecordPrefix + record.Week,
                    "WEEK " + record.Week + (record.FinalDecision ? " \u00b7 FINAL DECISION" : " \u00b7 EVICTION"), headline, ink, lines);
            }
            if (book.Records.Count == 0)
            {
                if (book.NoEvictionYet)
                    hud.EmptyState("No vote records", PackArt.KitEmptyVotes, "No eviction results yet.",
                        "No one has been evicted this season. Each result is recorded here when its vote is revealed.");
                else
                    hud.EmptyState("No vote records", PackArt.KitEmptyVotes, "No recorded results available.",
                        "Your notebook no longer holds the record of the season's earlier votes. Individual votes are not known.");
            }
            else if (book.Missing > 0)
                hud.LockNote("Missing vote records", (book.Missing == 1 ? "One earlier eviction is" : book.Missing + " earlier evictions are")
                    + " no longer in your notebook's record.");
        }

        private void RenderKnownBallots(EpisodeState state, VoteRecords.Book book)
        {
            bool any = false;
            var own = book.OwnPendingBallot;
            if (book.VoteInProgress && own != null)
            {
                hud.BallotCard(EpisodeHud.VotesInProgressName, "THIS WEEK \u00b7 BEFORE THE REVEAL", new[]
                {
                    new EpisodeHud.BallotLine { Voter = state.Find(state.playerId), Words = "You voted to evict " + own.TargetName },
                });
                any = true;
            }
            foreach (var record in book.Records.Where(r => r.Ballots.Count > 0))
            {
                string eyebrow = "WEEK " + record.Week;
                if (record.Complete && record.EvictedName != null)
                    eyebrow += " \u00b7 " + (record.EvictedIsPlayer ? "YOU WERE EVICTED" : record.EvictedName.ToUpperInvariant() + " EVICTED");
                hud.BallotCard(EpisodeHud.BallotsPrefix + record.Week, eyebrow, record.Ballots.Select(ballot => new EpisodeHud.BallotLine
                {
                    Voter = ballot.VoterId != null ? state.Find(ballot.VoterId) : null,
                    Words = BallotWords(ballot),
                    Reason = ballot.Reason,
                    Tag = ballot.TieBreak ? "Head of Household's tie-break" : null,
                }).ToList());
                any = true;
            }
            if (!any)
                hud.EmptyState("No known ballots", PackArt.KitEmptyVotes,
                    book.NoEvictionYet ? "No ballots are known yet." : "No ballots are recorded.",
                    "Ballots are made public when each eviction vote is revealed.");
            hud.LockNote(EpisodeHud.VotesPrivacyName, "Unknown individual ballots remain private.");
        }

        /// <summary>"Maya Hassan voted to evict Casey Wilson", with "you" where it is the player.</summary>
        private static string BallotWords(VoteRecords.Ballot ballot)
        {
            if (ballot.ByPlayer) return "You voted to evict " + (ballot.AgainstPlayer ? "yourself" : ballot.TargetName);
            return ballot.VoterName + " voted to evict " + (ballot.AgainstPlayer ? "you" : ballot.TargetName);
        }

        /// <summary>Opens the notebook on a section as every panel opens: everything else closed, the house paused.</summary>
        private void OpenNotebookAt(string section, bool scroll = true)
        {
            PauseNpcSocialForPanel(); ClosePanels();
            journalSection = section; journalOpen = true; roomsFilter = HouseMap.Filter.All;
            peopleFilter = PeopleFilter.All; peopleSearch = string.Empty; profileId = null; votesTab = VotesTab.Results; notesFilter = NotesFilter.Everyone;
            player.SetInputEnabled(false); cameraRig.ControlsEnabled = false;
            if (scroll) hud.RequestScrollTo(section);
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
            if (you != null && you.status == ContestantStatus.Expelled)
            {
                int removed = state.story?.removals.FirstOrDefault(r => r.contestantId == you.id)?.week ?? state.week;
                return "Production removed you from the house in week " + removed + ". You take no seat on the jury."
                    + " The house plays on; you can still watch every ceremony and read the notebook.";
            }
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
            while ((voteReveal != null && voteReveal.IsPlaying) || JuryRevealPlaying || walkingOutId != null
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
            // Back to the page it was reached from: OpenJournal opens on the notes.
            string section = journalSection;
            var committed = Snapshot;
            OpenRecap(() => weeklyRecap.Review(committed, week,
                fromNotebook ? () => OpenNotebookAt(section) : (Action)ClosePanels));
        }

        /// <summary>
        /// Opens the season report on the committed season.
        ///
        /// <para>It reads <see cref="Snapshot"/> rather than the projection, for the reason every
        /// other presentation surface here does: a projected result is not a fact, and the last
        /// screen of a season is the worst possible place to show an outcome the save does not
        /// hold.</para>
        /// </summary>
        public void ShowSeasonReport()
        {
            if (seasonReport == null) return;
            var committed = Snapshot;
            // Every way on from the top of the report; each closes it first. Close redraws the
            // house behind it, whose music the report had silenced.
            seasonReport.Show(committed, id =>
            {
                var actor = committed.Find(id);
                return actor == null ? null : CharacterPortraits.Get(actor);
            }, OpenJournal, CareerNow(), NewSeason, OpenMainMenu, Render);
            // The final stats are one of the screens the music rule silences, and nothing repaints
            // when the report opens: a HUD button only runs its action, and the panel left under
            // the report keeps the house paused. Asked here, or the season's track plays on.
            ApplyMusic();
        }
    }
}
