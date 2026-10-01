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
    /// notes, the weekly recap on every tab and the HUD's Recent events never print a voter the
    /// player cannot place beside the nominee they voted against; the Known ballots page shows the
    /// player's own, what they were told and what the count proves, each by its basis, and an
    /// unknown slot for the rest; and the recap's vote breakdown shows the count and only the known
    /// faces. Captured at both text sizes: notebook-known-ballots and recap-vote-breakdown.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The verbs a line says a ballot with, right after the voter's name (the subset's BallotPrivacyTests has the same rule).</summary>
        private static readonly string[] PrivacyBallotVerbs = { "voted", "votes", "is voting", "broke", "kept", "honoured", "followed", "ignored", "lied", "defected", "fell out" };

        private static readonly string[] PrivacyAccounts =
        {
            "told you", "Overheard", "An ally heard", "wouldn't say", "at the call", "your call", "put a ", "(about ", "agreed", "waiting on",
            KnownBallots.Unresolved, "Says: evict", "promised", "proposed", "offered", "Trust", "whip count", " to win", "jury",
            "pact", "agreement", "commitment", "final two", "Safety promise", "FinalTwo promise", "AllianceLoyalty promise", "Information promise",
        };

        /// <summary>
        /// An eight-house walked to its first reveal with the player among the voters: the player
        /// asked two voters where their heads were - one told the truth, one lied - and the engine
        /// judged both at the reveal. The count is not unanimous, so at least one ballot stays the
        /// voter's own.
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
                var voters = EpisodeEngine.Voters(s).Where(v => !v.isPlayer).Take(2).ToArray();
                string first = EpisodeEngine.ProjectBallot(s, voters[0].id).selectedNomineeId;
                s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = voters[0].id, targetId = first, source = ClaimSource.Told });
                s.ledger.claims.Add(new ClaimRow { week = s.week, voterId = voters[1].id, targetId = s.nominees.First(id => id != EpisodeEngine.ProjectBallot(s, voters[1].id).selectedNomineeId), source = ClaimSource.Told });
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
                if (sheet.Unknown == 0 || sheet.ballots.Count(b => b.basis == KnownBallots.Basis.Told) < 2) continue;
                return revealed;
            }
            Assert.Fail("No eight-house from seed 50 reached a reveal with two told claims and a ballot still unknown.");
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

        private static bool PrivacyNamesTheBallot(EpisodeState s, string line, string voterId, string targetId)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string voter = s.Find(voterId)?.name, target = s.Find(targetId)?.name;
            if (voter == null || target == null) return false;
            int at = line.IndexOf(voter, StringComparison.Ordinal);
            if (at < 0) return false;
            string targetWord = targetId == s.playerId ? "you" : FinalistRead.FirstName(target);
            if (line.IndexOf(targetWord, StringComparison.Ordinal) < 0) return false;
            if (PrivacyAccounts.Any(account => line.IndexOf(account, StringComparison.Ordinal) >= 0)) return false;
            string after = line.Substring(at + voter.Length);
            int stop = after.IndexOf('.');
            if (stop >= 0) after = after.Substring(0, stop);
            return PrivacyBallotVerbs.Any(verb => after.IndexOf(" " + verb, StringComparison.Ordinal) >= 0);
        }

        private List<string> VisibleLines() =>
            director.GetComponentsInChildren<TMP_Text>(true).Where(label => label.gameObject.activeInHierarchy)
                .SelectMany(label => label.text.Split('\n')).ToList();

        private void AssertNoUnknownBallotOnScreen(EpisodeState s, IEnumerable<(int week, string voterId, string targetId)> unknown, string where)
        {
            var lines = VisibleLines();
            foreach (var (week, voterId, targetId) in unknown)
                foreach (var line in lines)
                    Assert.That(PrivacyNamesTheBallot(s, line, voterId, targetId), Is.False,
                        where + ": " + s.Find(voterId).name + "'s ballot (week " + week + ") is not the player's to know, and the screen says it: \"" + line + "\"");
        }

        [UnityTest]
        public IEnumerator BallotPrivacy_TheNotebookTheRecapAndTheHudNameOnlyKnownBallots()
        {
            var state = PartlyKnownReveal();
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            yield return SkipReveals();
            director.ClosePanels();
            yield return null;
            var shown = director.Snapshot;
            int week = shown.week;
            var sheet = KnownBallots.Read(shown, week);
            var unknown = PrivateBallots(shown).Where(b => !sheet.Knows(b.voterId)).ToList();
            Assert.That(unknown, Is.Not.Empty, "A ballot the player cannot place, or the sentinel guards nothing.");
            var known = sheet.ballots.Where(b => b.Known && b.voterId != shown.playerId).ToList();
            Assert.That(known, Is.Not.Empty, "A ballot the player can place.");

            // The HUD over the house: Recent events carries the count, never a ballot.
            Canvas.ForceUpdateCanvases();
            AssertNoUnknownBallotOnScreen(shown, unknown, "The HUD");
            Assert.That(VisibleLines().Any(line => line.StartsWith("By a vote of ", StringComparison.Ordinal) || line.StartsWith("By a single vote", StringComparison.Ordinal)),
                Is.True, "The count is on the HUD's Recent events.");

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
                Assert.That(lines.Count(line => line == "A ballot you do not know"), Is.EqualTo(sheet.Unknown), "and an unknown slot for each the player cannot place.");
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
                        Assert.That(words.Any(line => line.StartsWith("By a vote of ", StringComparison.Ordinal) || line.StartsWith("By a single vote", StringComparison.Ordinal)), Is.True, "The count.");
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
