using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Episode button and a decision of the player's own (UI-UX plan Q2; menu review change 7):
    /// while their nominations, the veto meeting, their ballot or the final choice waits, the rail's
    /// Episode button opens the episode screen where they stand; free time, and every beat with
    /// nothing of theirs to decide, still walks them to the nomination room. The decisions are read
    /// from the season without changing it, and the director asks before it walks.
    /// </summary>
    public sealed class WaitingDecisionTests
    {
        private static EpisodeState House(int size = 8, uint seed = 41)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            s.week = 3;
            return s;
        }

        private static List<string> Npcs(EpisodeState s) => s.contestants.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static EpisodeState Nominations(bool playerHoh)
        {
            var s = House();
            s.phase = EpisodePhase.Nomination;
            s.hohId = playerHoh ? s.playerId : Npcs(s)[0];
            s.nominees = new List<string>();
            return s;
        }

        private static EpisodeState Meeting(bool playerHolds, bool playerHeads, bool holderOnTheBlock = false)
        {
            var s = House();
            var npcs = Npcs(s);
            s.phase = EpisodePhase.VetoMeeting;
            s.vetoResolved = false;
            s.hohId = playerHeads ? s.playerId : npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = playerHolds ? s.playerId : holderOnTheBlock ? npcs[1] : npcs[3];
            return s;
        }

        private static EpisodeState Ballot(bool playerNominated = false)
        {
            var s = House();
            var npcs = Npcs(s);
            s.phase = EpisodePhase.Eviction;
            s.evictionResolved = false;
            s.evictionStage = EvictionStage.Voting;
            s.hohId = npcs[0];
            s.nominees = new List<string> { playerNominated ? s.playerId : npcs[1], npcs[2] };
            return s;
        }

        private static EpisodeState FinalThree()
        {
            var s = House(6, 31);
            s.week = 9;
            foreach (var juror in s.contestants.Where(c => !c.isPlayer).Take(3)) juror.status = ContestantStatus.Jury;
            s.phase = EpisodePhase.FinalEviction;
            s.hohId = s.playerId;
            return s;
        }

        [Test]
        public void EachOfThePlayersFourDecisionsWaitsAndReadingItChangesNothing()
        {
            var heads = Meeting(playerHolds: false, playerHeads: true, holderOnTheBlock: true);
            Assert.That(EpisodeEngine.NpcVetoSave(heads), Is.Not.Null, "The fixture: a houseguest holding the veto on the block saves themselves.");
            var cases = new (string what, EpisodeState state, WaitingDecision.Kind kind)[]
            {
                ("the Head of Household's nominations", Nominations(playerHoh: true), WaitingDecision.Kind.Nominations),
                ("the veto meeting, the player holding the veto", Meeting(playerHolds: true, playerHeads: false), WaitingDecision.Kind.VetoMeeting),
                ("the veto meeting, the Head of Household naming a replacement", heads, WaitingDecision.Kind.VetoMeeting),
                ("the eviction ballot", Ballot(), WaitingDecision.Kind.Ballot),
                ("the final Head of Household's choice", FinalThree(), WaitingDecision.Kind.FinalChoice),
            };
            foreach (var (what, state, kind) in cases)
            {
                uint stream = state.randomState;
                int revision = state.revision, events = state.events.Count;
                Assert.That(WaitingDecision.For(state), Is.EqualTo(kind), what);
                Assert.That(WaitingDecision.Waits(state), Is.True, what);
                Assert.That(state.randomState, Is.EqualTo(stream), what + ": nothing draws from the season's stream.");
                Assert.That(state.revision, Is.EqualTo(revision), what + ": the season is not changed.");
                Assert.That(state.events.Count, Is.EqualTo(events), what + ": nothing is written to the season.");
            }
        }

        [Test]
        public void NothingWaitsInFreeTimeOrOnSomebodyElsesDecision()
        {
            var named = Nominations(playerHoh: true);
            named.nominees = Npcs(named).Skip(1).Take(2).ToList();
            var resolved = Meeting(playerHolds: true, playerHeads: false);
            resolved.vetoResolved = true;
            var cast = Ballot();
            cast.votes.Add(new VoteState { voterId = cast.playerId, targetId = cast.nominees[0] });
            var reflection = Nominations(playerHoh: true);
            reflection.pendingDiary = new DiaryPromptState { id = "reflection", week = reflection.week };
            var evicted = Ballot();
            evicted.Find(evicted.playerId).status = ContestantStatus.Evicted;
            var competition = House();
            competition.phase = EpisodePhase.HoH;
            var cases = new (string what, EpisodeState state, bool competitionRunning)[]
            {
                ("free time", House(), false),
                ("a fresh season's first free time", SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7), false),
                ("a houseguest's nominations", Nominations(playerHoh: false), false),
                ("the nominations already named", named, false),
                ("a houseguest's veto meeting", Meeting(playerHolds: false, playerHeads: false), false),
                ("the veto meeting already held", resolved, false),
                ("the ballot already cast", cast, false),
                ("the ballot with the player on the block", Ballot(playerNominated: true), false),
                ("a competition", competition, false),
                ("the final choice while a competition runs", FinalThree(), true),
                ("the nominations under a private reflection", reflection, false),
                ("a player out of the house", evicted, false),
            };
            foreach (var (what, state, running) in cases)
            {
                Assert.That(WaitingDecision.For(state, running), Is.EqualTo(WaitingDecision.Kind.None), what);
                Assert.That(WaitingDecision.Waits(state, running), Is.False, what);
            }
            Assert.That(WaitingDecision.For(null), Is.EqualTo(WaitingDecision.Kind.None), "No season.");
        }

        /// <summary>
        /// The director's wiring, read from its source: the Episode button asks for a waiting decision
        /// before it looks for the nomination room; the open reads the decision without the season's
        /// stream, flushes the house's due time as every panel does, and never travels; the rail and the
        /// compact objective's Episode buttons both reach it.
        /// </summary>
        [Test]
        public void TheEpisodeButtonOpensAWaitingDecisionBeforeItWalks()
        {
            string episode = Path.Combine(GamesimRoot(), "Runtime", "Episode");
            string destinations = Code(Path.Combine(episode, "EpisodeDirector.Destinations.cs"));
            string go = Between(destinations, "void GoToPlayDestination(", "public bool OpenWaitingDecision(");
            int ask = go.IndexOf("if (!competition && OpenWaitingDecision()) return;", StringComparison.Ordinal);
            Assert.That(ask, Is.GreaterThanOrEqualTo(0), "The Episode button asks for a waiting decision.");
            Assert.That(ask, Is.LessThan(go.IndexOf("Destination(competition)", StringComparison.Ordinal)), "before it looks for the room,");
            Assert.That(ask, Is.LessThan(go.IndexOf("GoToStation()", StringComparison.Ordinal)), "and before it walks.");

            string open = Between(destinations, "public bool OpenWaitingDecision(", "private string StationRoomId(");
            Assert.That(open, Does.Contain("WaitingDecision.Waits(projected, challengeActive)"), "It opens only for a decision of the player's own.");
            int flush = open.IndexOf("PauseNpcSocialForPanel();", StringComparison.Ordinal);
            Assert.That(flush, Is.GreaterThanOrEqualTo(0), "The house's due time is flushed as for every panel,");
            Assert.That(flush, Is.LessThan(open.IndexOf("phaseOpen = true;", StringComparison.Ordinal)), "before the screen opens.");
            foreach (var travel in new[] { "TryTravel(", "GoToStation(", "GoToRoom", "TryWarpTo(" })
                Assert.That(open, Does.Not.Contain(travel), "Opening the decision travels nowhere: " + travel);

            string chrome = Code(Path.Combine(episode, "EpisodeHud.Chrome.cs"));
            Assert.That(Regex.Matches(chrome, @"EpisodeTravelCaption\s*,[^;]*?director\.GoToPlayDestination\(false\)", RegexOptions.Singleline).Count, Is.EqualTo(2),
                "The rail's Episode row and the compact objective's Episode button both reach the same way in.");
        }

        /// <summary>The text between two markers.</summary>
        private static string Between(string code, string from, string to)
        {
            int start = code.IndexOf(from, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "'" + from + "' is in the file.");
            int end = code.IndexOf(to, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "'" + to + "' follows '" + from + "'.");
            return code.Substring(start, end - start);
        }

        /// <summary>A file's code with its comments blanked, so a note about the walk is not the walk.</summary>
        private static string Code(string path)
        {
            Assert.That(File.Exists(path), Is.True, path + " exists.");
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(text, @"//[^\n]*", " ");
        }

        private static string GamesimRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "Gamesim"));
#else
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                if (Directory.Exists(Path.Combine(candidate, "Runtime"))) return Path.GetFullPath(candidate);
            }
            Assert.Fail("Assets/Gamesim was not found above " + AppContext.BaseDirectory);
            return null;
#endif
        }
    }
}
