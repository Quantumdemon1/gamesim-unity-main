using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The privacy sentinel over the screens (UI-UX-PASS-PLAN B0): on a week whose count the player
    /// knows but whose ballots they can only partly place, the notebook's vote page, its story and
    /// notes, the diary room's memories, a houseguest's profile, the weekly recap on every tab and
    /// the HUD's Recent events never print a voter the player cannot place beside the nominee they
    /// voted against, nor the engine's verdict line that would tell it; the Known ballots page shows
    /// the player's own, what they were told and what the count proves, each by its basis, and how
    /// many they cannot place; and the recap's vote breakdown shows the count and only the known
    /// faces. Captured at both text sizes: notebook-known-ballots and recap-vote-breakdown.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The verbs a line says a ballot with, after the voter's name in the same sentence (the subset's BallotPrivacyTests has the same rule).</summary>
        private static readonly string[] PrivacyBallotVerbs = { "voted", "votes", "is voting", "broke", "kept", "honoured", "followed", "ignored", "lied", "defected", "fell out" };

        /// <summary>Lines that are a record of what was said, never a ballot read, by the words they open with: a voter's own stated lean on a whip count, and the claims the player gathered - an ally's own word to the pact included (C6).</summary>
        private static readonly string[] PrivacyAccountPrefixes = { "Says: evict", "Overheard", "Told the pact" };

        /// <summary>
        /// An eight-house walked to its first reveal with the player among the voters: the player
        /// asked two voters where their heads were - one told the truth, one lied - and a third
        /// promised them a vote they did not cast; the engine judged all three at the reveal, and
        /// wrote the broken promise to the player's memories, where it tells that third ballot. The
        /// count does not place the third voter, so that memory waits for the ballot, and at least
        /// one ballot stays the voter's own.
        /// </summary>
        private static EpisodeState PartlyKnownReveal()
        {
            for (uint seed = 50; seed < 90; seed++)
            {
                var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                var engine = new EpisodeEngine(state);
                bool voting = false;
                for (int guard = 0; guard < 80 && !voting; guard++)
                {
                    var now = engine.Snapshot;
                    voting = now.phase == EpisodePhase.Eviction && now.evictionStage == EvictionStage.Voting && now.votes.Count == 0
                        && EpisodeEngine.Voters(now).Any(v => v.isPlayer) && EpisodeEngine.Voters(now).Count(v => !v.isPlayer) >= 3;
                    if (voting) break;
                    if (!engine.Apply(NextCommand(now)).accepted) break;
                }
                if (!voting) continue;
                var s = engine.Snapshot;
                var voters = EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Take(3).ToArray();
                string first = EpisodeEngine.ProjectBallot(s, voters[0].id).selectedNomineeId;
                s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = voters[0].id, targetId = first, source = ClaimSource.Told });
                s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = voters[1].id, targetId = s.nominees.First(id => id != EpisodeEngine.ProjectBallot(s, voters[1].id).selectedNomineeId), source = ClaimSource.Told });
                // A vote promise from the third voter for the nominee they will not vote against: the
                // engine settles it broken at the reveal and writes the verdict to the player's memories.
                string third = EpisodeEngine.ProjectBallot(s, voters[2].id).selectedNomineeId;
                s.promises.Add(new PromiseState { id = "sentinel-promise", fromId = voters[2].id, toId = s.playerId, targetId = s.nominees.First(id => id != third),
                    kind = PromiseKind.Vote, status = PromiseStatus.Active, week = s.week, expiresWeek = s.week });
                engine = new EpisodeEngine(s);
                for (int guard = 0; guard < 20 && !engine.Snapshot.evictionResolved; guard++)
                {
                    var command = NextCommand(engine.Snapshot);
                    if (command.kind == EpisodeCommandKind.CastVote) command.targetId = engine.Snapshot.nominees[0];
                    if (!engine.Apply(command).accepted) break;
                }
                var revealed = engine.Snapshot;
                if (!revealed.evictionResolved) continue;
                var sheet = KnownBallots.Read(revealed, revealed.week);
                if (sheet.Unknown == 0 || sheet.ballots.Count(b => b.basis == KnownBallots.Basis.Told) < 2 || sheet.Knows(voters[2].id)) continue;
                if (!revealed.memories.Any(m => m.ownerId == revealed.playerId && m.subjectId == voters[2].id
                        && KnownBallots.TellsAnUnknownBallot(revealed, voters[2].id, m.text, m.week))) continue;
                return revealed;
            }
            Assert.Fail("No eight-house from seed 50 reached a reveal with two told claims, a broken promise the player cannot place and a ballot still unknown.");
            return null;
        }

        private static List<(int week, string voterId, string targetId)> PrivateBallots(EpisodeState s)
        {
            var ballots = new List<(int, string, string)>();
            foreach (var line in s.events.Where(e => e.kind == "vote-reveal" && e.audienceIds.Count == 1 && e.audienceIds[0] != s.playerId))
            {
                var read = KnownBallots.ReadRevealLine(s, line.text, null);
                if (read?.voterId != null && read.targetId != null) ballots.Add((line.week, read.voterId, read.targetId));
            }
            return ballots;
        }

        /// <summary>
        /// Whether a line says the voter's ballot: in any sentence of it, the voter's name as the
        /// sentence's subject - the first houseguest it names - followed by a ballot verb, with the
        /// nominee it went against named anywhere in the line. The words that say a ballot is not
        /// known (KnownBallots.Unresolved) are not a ballot said.
        /// </summary>
        private static bool PrivacyNamesTheBallot(EpisodeState s, string line, string voterId, string targetId)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string voter = s.Find(voterId)?.name, target = s.Find(targetId)?.name;
            if (voter == null || target == null) return false;
            if (PrivacyAccountPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))) return false;
            string said = line.Replace(KnownBallots.Unresolved, string.Empty);
            string targetWord = targetId == s.playerId ? "you" : FinalistRead.FirstName(target);
            if (said.IndexOf(targetWord, StringComparison.Ordinal) < 0) return false;
            foreach (var sentence in said.Split(new[] { ". ", "? ", "! ", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int at = sentence.IndexOf(voter, StringComparison.Ordinal);
                if (at < 0) continue;
                if (s.contestants.Any(c => !string.IsNullOrEmpty(c.name) && sentence.IndexOf(c.name, StringComparison.Ordinal) is int first && first >= 0 && first < at)) continue;
                string after = sentence.Substring(at + voter.Length);
                if (PrivacyBallotVerbs.Any(verb => after.IndexOf(" " + verb, StringComparison.Ordinal) >= 0)) return true;
            }
            return false;
        }

        private List<string> VisibleLines() =>
            director.GetComponentsInChildren<TMP_Text>(true).Where(label => label.gameObject.activeInHierarchy)
                .SelectMany(label => label.text.Split('\n')).ToList();

        /// <summary>A line as the engine wrote it, without the "Week 3: " or "Week 3 · " a screen dates it with.</summary>
        private static string PrivacyUnprefixed(string line)
        {
            if (line == null || !line.StartsWith("Week ", StringComparison.Ordinal)) return line;
            int colon = line.IndexOf(": ", StringComparison.Ordinal), dot = line.IndexOf(" · ", StringComparison.Ordinal);
            int cut = colon >= 0 && (dot < 0 || colon < dot) ? colon + 2 : dot >= 0 ? dot + 3 : -1;
            return cut >= 0 ? line.Substring(cut) : line;
        }

        /// <summary>The week a line is dated to by its "Week 3: " or "Week 3 · ", or null where it carries none.</summary>
        private static int? PrivacyWeekOf(string line)
        {
            if (line == null || !line.StartsWith("Week ", StringComparison.Ordinal)) return null;
            int end = 5;
            while (end < line.Length && char.IsDigit(line[end])) end++;
            return end > 5 && end < line.Length && (line[end] == ':' || line[end] == ' ') ? int.Parse(line.Substring(5, end - 5)) : (int?)null;
        }

        /// <summary>The player's memories that tell a ballot the player cannot place: the engine's own verdict lines, which every memory reader withholds until the ballot is known.</summary>
        private static List<MemoryState> WithheldMemories(EpisodeState s) =>
            s.memories.Where(m => m.ownerId == s.playerId && KnownBallots.TellsAnUnknownBallot(s, m.subjectId, m.text, m.week)).ToList();

        private void AssertNoUnknownBallotOnScreen(EpisodeState s, IEnumerable<(int week, string voterId, string targetId)> unknown, string where)
        {
            var lines = VisibleLines();
            var withheld = WithheldMemories(s);
            foreach (var (week, voterId, targetId) in unknown)
                foreach (var line in lines)
                {
                    // A line dated to another week is that week's: held against that week's ballots.
                    if (PrivacyWeekOf(line) is int dated && dated != week) continue;
                    Assert.That(PrivacyNamesTheBallot(s, line, voterId, targetId), Is.False,
                        where + ": " + s.Find(voterId).name + "'s ballot (week " + week + ") is not the player's to know, and the screen says it: \"" + line + "\"");
                    Assert.That(KnownBallots.TellsAnUnknownBallot(s, voterId, PrivacyUnprefixed(line), week), Is.False,
                        where + ": " + s.Find(voterId).name + "'s ballot (week " + week + ") is not the player's to know, and the screen tells its verdict: \"" + line + "\"");
                }
            foreach (var memory in withheld)
                Assert.That(lines.Any(line => line.Contains(memory.text)), Is.False,
                    where + ": a memory that tells a ballot the player cannot place is on the screen: \"" + memory.text + "\"");
        }

        [UnityTest]
        public IEnumerator BallotPrivacy_TheNotebookTheRecapAndTheHudNameOnlyKnownBallots()
        {
            var state = PartlyKnownReveal();
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            // The house's own clock must not write over the fixture: a tick after the load logs
            // events of its own, and enough of them roll the reveal's lines off the log the sentinel
            // reads, which is what a filtered run showed.
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return SkipReveals();
            director.ClosePanels();
            yield return null;
            var shown = director.Snapshot;
            int week = shown.week;
            var sheet = KnownBallots.Read(shown, week);
            var privateLines = PrivateBallots(shown);
            var unknown = privateLines.Where(b => !sheet.Knows(b.voterId)).ToList();
            Assert.That(unknown, Is.Not.Empty, "A ballot the player cannot place, or the sentinel guards nothing: " + privateLines.Count
                + " private lines in week " + week + " (the fixture had " + PrivateBallots(state).Count + "), " + sheet.Unknown + " unknown on the sheet, "
                + shown.events.Count + " events on the log.");
            var known = sheet.ballots.Where(b => b.Known && b.voterId != shown.playerId).ToList();
            Assert.That(known, Is.Not.Empty, "A ballot the player can place.");
            var withheld = WithheldMemories(shown);
            Assert.That(withheld, Is.Not.Empty, "A memory that tells a ballot the player cannot place, or the memory readers guard nothing.");
            Assert.That(KnownBallots.PlayerMemories(shown).Select(m => m.text), Has.None.EqualTo(withheld[0].text), "The one list every memory reader prints leaves it out.");

            // The diary room: the confessional's caption under the player's name is their latest
            // memory they may know, and the Memories tab their reflections.
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption(EpisodeDirector.DiaryMemoriesTabCaption).onClick.Invoke();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            AssertNoUnknownBallotOnScreen(shown, unknown, "The diary room's memories");
            var latest = KnownBallots.PlayerMemories(shown).LastOrDefault();
            if (latest != null)
                Assert.That(VisibleLines(), Has.Some.EqualTo("Week " + latest.week + ": " + latest.text), "The reflections read the memories the player may know.");
            director.ClosePanels();
            yield return WaitForDiaryExit();
            yield return null;

            // A houseguest's profile: the latest three memories the player may know of them.
            director.ShowHouseguestProfile(withheld[0].subjectId);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            AssertNoUnknownBallotOnScreen(shown, unknown, "The profile of " + shown.Find(withheld[0].subjectId).name);
            director.ClosePanels();
            yield return null;

            // The HUD over the house: Recent events reads the house's record, where the count rides
            // the public eviction line and never a ballot. The record is pinned rather than the
            // card, whose lines after a reveal may have scrolled the eviction off it.
            Canvas.ForceUpdateCanvases();
            AssertNoUnknownBallotOnScreen(shown, unknown, "The HUD");
            var gone = shown.events.Single(e => e.week == week && e.kind == "eviction");
            Assert.That(gone.audienceIds, Is.Empty, "The count is the house's,");
            Assert.That(gone.text, Does.Contain(" the jury. By a vote of ").Or.Contain(" the jury. By a single vote."), "on the eviction line.");

            foreach (bool larger in new[] { false, true })
            {
                director.SetLargeText(larger);
                yield return null;
                string size = larger ? " at the large text" : "";

                // The notebook: the vote page, the Known ballots tab, the story so far, the notes.
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes);
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                AssertNoUnknownBallotOnScreen(shown, unknown, "Eviction results" + size);
                Assert.That(VisibleLines(), Has.Some.Contains(sheet.Known + " of " + sheet.ballots.Count + " ballots known"), "The results card counts the known ballots.");
                ButtonWithCaption("Known ballots").onClick.Invoke();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                var lines = VisibleLines();
                AssertNoUnknownBallotOnScreen(shown, unknown, "Known ballots" + size);
                Assert.That(lines, Has.Some.StartsWith("You voted to evict "), "The player's own ballot,");
                Assert.That(lines, Has.Some.EqualTo(KnownBallots.Basis.Word(KnownBallots.Basis.Own)), "by its basis;");
                foreach (var ballot in known)
                {
                    string words = shown.Find(ballot.voterId).name + " voted to evict " + (ballot.targetId == shown.playerId ? "you" : shown.Find(ballot.targetId).name);
                    Assert.That(lines, Has.Some.EqualTo(words), "a known ballot" + size + ": " + words);
                    Assert.That(lines, Has.Some.StartsWith(KnownBallots.Basis.Word(ballot.basis)), "tagged by its basis: " + ballot.basis);
                }
                Assert.That(lines, Has.Some.EqualTo(VoteRecords.UnknownBallotsLine(sheet.Unknown)), "and one line counting the ballots the player cannot place,");
                Assert.That(lines, Has.None.EqualTo("A ballot you do not know"), "never a line a slot.");
                Assert.That(lines, Has.Some.Contains("remain private"));
                Assert.That(lines, Has.None.Contains("made public"));
                AssertDecisionCopyFits(ActiveRect(EpisodeHud.NotebookHeaderName).parent as RectTransform);
                if (Application.isBatchMode) yield return CaptureFraming(larger ? "notebook-known-ballots-large" : "notebook-known-ballots");

                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
                yield return Frames(2);
                AssertNoUnknownBallotOnScreen(shown, unknown, "The story so far" + size);
                director.ShowNotebookSection(EpisodeDirector.NotebookSection.Notes);
                yield return Frames(2);
                AssertNoUnknownBallotOnScreen(shown, unknown, "Your notes" + size);
                director.ClosePanels();
                yield return null;

                // The weekly recap, every tab; the vote breakdown captured.
                var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
                screen.FontScale = larger ? 1.2f : 1f;
                yield return null;
                screen.Show(shown, () => { });
                yield return Frames(2);
                for (int tab = 0; tab < WeeklyRecapScreen.TabCaptions.Length; tab++)
                {
                    ButtonWithCaption(WeeklyRecapScreen.TabCaptions[tab]).onClick.Invoke();
                    yield return Frames(2);
                    Canvas.ForceUpdateCanvases();
                    AssertNoUnknownBallotOnScreen(shown, unknown, WeeklyRecapScreen.TabCaptions[tab] + size);
                    if (WeeklyRecapScreen.TabCaptions[tab] == "Vote breakdown")
                    {
                        var words = VisibleLines();
                        Assert.That(words.Any(line => line.Contains("By a vote of ") || line.Contains("By a single vote")), Is.True, "The count.");
                        Assert.That(words.Count(line => line.Contains(" ballots you do not know") || line.Contains("1 ballot you do not know")), Is.GreaterThan(0), "and how many are unknown.");
                        AssertDecisionCopyFits(LastActive(WeeklyRecapScreen.TabBodyName));
                        if (Application.isBatchMode) yield return CaptureFraming(larger ? "recap-vote-breakdown-large" : "recap-vote-breakdown");
                    }
                }
                ButtonWithCaption("Week overview").onClick.Invoke();
                yield return Frames(2);
                Canvas.ForceUpdateCanvases();
                var overview = VisibleLines();
                Assert.That(overview.Any(line => line.StartsWith("TO EVICT ", StringComparison.Ordinal)), Is.True, "THE VOTE card's count.");
                foreach (var ballot in known) Assert.That(overview, Has.Some.StartsWith(KnownBallots.Basis.Word(ballot.basis)), "A known face carries its basis: " + ballot.basis);
                screen.Hide();
                yield return null;
            }
            director.SetLargeText(false);
            // Reading changes nothing of the record: the sheet reads the same (the subset pins that
            // the reader itself writes nothing; opening a panel may bank an NPC tick of its own).
            var again = KnownBallots.Read(director.Snapshot, week);
            Assert.That(again.ballots.Select(b => b.voterId + ":" + b.targetId + ":" + b.basis), Is.EqualTo(sheet.ballots.Select(b => b.voterId + ":" + b.targetId + ":" + b.basis)));
        }
    }
}
