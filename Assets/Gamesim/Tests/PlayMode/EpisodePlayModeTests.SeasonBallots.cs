using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// The season report's tapes on screen (UI-UX-PASS-PLAN J0; decision 8): at the finale, "How
    /// the house voted" draws a card for every eviction - who went, the count in the house's words,
    /// each side's voters by face and name under the nominee they voted to evict, the Head of
    /// Household's tie-break on its own row, a lie marked LIED with what the voter said - every
    /// label drawn whole in a box Inter draws in, at both text sizes, on the 4:3 batch canvas and the
    /// 16:9 frame the captures photograph. Before the finale the same section is sealed: on the B0
    /// sentinel's fixture, with a ballot the player cannot place, the report shows the sealed line
    /// and no ballot, and the sentinel's rule holds over the whole report. Captures
    /// season-report-ballots and season-report-ballots-large.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// An eight-house played to its finale in the engine, with what the B0 sentinel's fixture
        /// gives the reveal to judge: before every vote the player is told two ballots - one true,
        /// one a lie - and overhears a third voter name the nominee they will not vote against. The
        /// first season from seed 1 whose tapes are whole, hold a lie and break a tie, or failing a
        /// tie the first whole one with a lie.
        /// </summary>
        private static EpisodeState TapesSeason()
        {
            EpisodeState withoutTie = null;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed));
                bool played = true;
                for (int guard = 0; guard < 400 && engine.Snapshot.phase != EpisodePhase.Finished && played; guard++)
                {
                    var now = engine.Snapshot;
                    if (now.phase == EpisodePhase.Eviction && now.evictionStage == EvictionStage.Voting && !now.evictionResolved && now.votes.Count == 0 && now.nominees.Count == 2)
                    {
                        var voters = EpisodeEngine.Voters(now).Where(v => !v.isPlayer).ToArray();
                        string Other(string id) => now.nominees.First(nominee => nominee != id);
                        if (voters.Length >= 1)
                            now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[0].id, targetId = EpisodeEngine.ProjectBallot(now, voters[0].id).selectedNomineeId, source = ClaimSource.Told });
                        if (voters.Length >= 2)
                            now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[1].id, targetId = Other(EpisodeEngine.ProjectBallot(now, voters[1].id).selectedNomineeId), source = ClaimSource.Told });
                        if (voters.Length >= 3)
                            now.ledger.claims.Add(new ClaimRow { week = now.week, voterId = voters[2].id, targetId = Other(EpisodeEngine.ProjectBallot(now, voters[2].id).selectedNomineeId), source = ClaimSource.Overheard });
                        engine = new EpisodeEngine(now);
                    }
                    played = engine.Apply(NextCommand(engine.Snapshot)).accepted;
                }
                var season = engine.Snapshot;
                if (season.phase != EpisodePhase.Finished) continue;
                var tapes = SeasonBallots.Read(season);
                if (tapes.Count < 3 || !tapes.All(week => week.Complete) || !tapes.Any(week => week.rows.Any(row => row.Lied))) continue;
                if (tapes.Any(week => week.TieBreak != null)) return season;
                if (withoutTie == null) withoutTie = season;
            }
            Assert.That(withoutTie, Is.Not.Null, "No eight-house from seed 1 finished with whole tapes and a lie on them.");
            return withoutTie;
        }

        /// <summary>
        /// The same season as a long one ends: the first house week the record can no longer
        /// place whole once its ballots' lines have rolled off the log and its claims are gone, so
        /// the table counts the rest and names whose they are.
        /// </summary>
        private static EpisodeState TapesWithAWeekForgotten(EpisodeState season)
        {
            foreach (var power in season.ledger.power.Where(p => p.tally.Count == 2))
            {
                var forgotten = season.Clone();
                forgotten.events.RemoveAll(e => e.week == power.week && e.kind == "vote-reveal");
                forgotten.ledger.claims.RemoveAll(k => k.week == power.week);
                if (SeasonBallots.Read(forgotten).Any(week => !week.Complete && week.unplaced.Count > 0)) return forgotten;
            }
            Assert.Fail("No week of the fixture is left unplaced without its lines and claims.");
            return null;
        }

        /// <summary>Whether <paramref name="inner"/> lies inside <paramref name="outer"/>, measured in <paramref name="outer"/>'s own space, so a camera canvas's tilt is no part of it.</summary>
        private static bool TapesInside(RectTransform outer, RectTransform inner)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(outer, inner);
            var room = outer.rect;
            return bounds.min.x >= room.xMin - .5f && bounds.max.x <= room.xMax + .5f && bounds.min.y >= room.yMin - .5f && bounds.max.y <= room.yMax + .5f;
        }

        private static string TapesLabel(RectTransform root, string name) =>
            root.GetComponentsInChildren<TMP_Text>().Where(label => label.name == name).Select(label => label.text).FirstOrDefault();

        /// <summary>The voter's name on a ballot row, as the report draws it.</summary>
        private static string TapesVoter(EpisodeState state, SeasonBallots.Row ballot) =>
            HudPrimitives.WithYou(state.Find(ballot.voterId).name, ballot.voterId == state.playerId);

        /// <summary>
        /// The tapes on the report now open, against the reader: a card a week in order, each with
        /// its eyebrow, who went and the count; each side's heading and its voters in order, a lie
        /// marked exactly where the reader marks one, with what was said under it, and the ballots
        /// the record no longer places counted on their side and named under the card; the tie-break
        /// on its own row, and the final eviction's one vote; every label drawn whole in a box at
        /// least 1.3 times its type and inside the section; every row inside its card, every card
        /// inside the section. Returns how many lies it found marked.
        /// </summary>
        private int AssertTheTapesOnTheReport(EpisodeState state, List<SeasonBallots.Week> tapes, string where)
        {
            Canvas.ForceUpdateCanvases();
            var section = ReportPart(SeasonReport.HouseBallotsName);
            Assert.That(section, Is.Not.Null, where + ": the report reads the tapes.");
            Assert.That(ReportLabels().Count(text => text == SeasonBallots.Heading.ToUpperInvariant()), Is.EqualTo(1), where + ": under one heading.");
            string intro = tapes.All(week => week.Complete) ? SeasonBallots.Intro : SeasonBallots.Intro + " " + SeasonBallots.IntroIncomplete;
            Assert.That(TapesLabel(section, "Intro"), Is.EqualTo(intro), where + ": the intro says what the marks are, and whether the record is whole.");
            Assert.That(LabelsUnder(section), Has.None.EqualTo(SeasonBallots.SealedLine), where + ": open, not sealed.");
            var cards = section.Cast<Transform>().OfType<RectTransform>().Where(rect => rect.name.StartsWith(SeasonReport.BallotWeekName + " ")).ToList();
            Assert.That(cards.Select(card => card.name), Is.EqualTo(tapes.Select(week => SeasonReport.BallotWeekName + " " + week.week)), where + ": a card an eviction, in order.");
            int lies = 0;
            foreach (var week in tapes)
            {
                string at = where + ", week " + week.week + ": ";
                var card = cards.Single(rect => rect.name == SeasonReport.BallotWeekName + " " + week.week);
                Assert.That(TapesInside(section, card), Is.True, at + "the card is inside the section.");
                Assert.That(TapesLabel(card, "Eyebrow"), Is.EqualTo(SeasonBallots.Eyebrow(week)), at + "the eyebrow,");
                Assert.That(TapesLabel(card, "Evicted"), Is.EqualTo(SeasonBallots.Title(state, week)), at + "who went,");
                Assert.That(TapesLabel(card, "Count"), Is.EqualTo(SeasonBallots.CountWords(state, week)), at + "and the count.");
                var rows = card.GetComponentsInChildren<RectTransform>().Where(rect => rect.name == SeasonReport.BallotRowName).ToList();
                foreach (var row in rows) Assert.That(TapesInside(card, row), Is.True, at + "every row inside its card.");
                if (week.final)
                {
                    Assert.That(rows, Has.Count.EqualTo(1), at + "the final eviction is one vote,");
                    Assert.That(TapesLabel(rows[0], "Name"), Is.EqualTo(TapesVoter(state, week.FinalChoice)), at + "the last Head of Household's,");
                    Assert.That(rows[0].Find(SeasonReport.SoleVoteMarkName), Is.Not.Null, at + "marked the sole vote.");
                    continue;
                }
                var sides = card.Cast<Transform>().OfType<RectTransform>().Where(rect => rect.name == SeasonReport.BallotSideName).ToList();
                var nominees = week.Sides.ToList();
                Assert.That(sides, Has.Count.EqualTo(2), at + "two sides, a nominee each.");
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(TapesLabel(sides[i], "Side"), Is.EqualTo(SeasonBallots.SideHeading(state, week, nominees[i])), at + "the one who went first.");
                    var ballots = week.Side(nominees[i]).ToList();
                    var drawn = sides[i].Cast<Transform>().OfType<RectTransform>().Where(rect => rect.name == SeasonReport.BallotRowName).ToList();
                    Assert.That(drawn.Select(row => TapesLabel(row, "Name")), Is.EqualTo(ballots.Select(ballot => TapesVoter(state, ballot))),
                        at + "each voter under the nominee they voted to evict.");
                    for (int b = 0; b < ballots.Count; b++)
                    {
                        var mark = drawn[b].Find(SeasonReport.LieMarkName);
                        Assert.That(mark != null, Is.EqualTo(ballots[b].Lied), at + TapesVoter(state, ballots[b]) + " is marked a lie exactly where the reader marks one.");
                        if (!ballots[b].Lied)
                        {
                            Assert.That(TapesLabel(drawn[b], SeasonReport.LieLineName), Is.Null);
                            continue;
                        }
                        lies++;
                        Assert.That(mark.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(SeasonBallots.LieWord));
                        Assert.That(TapesLabel(drawn[b], SeasonReport.LieLineName), Is.EqualTo(SeasonBallots.LieWords(state, ballots[b])), at + "with what they said.");
                    }
                    // The ballots against this nominee the record no longer places, counted and never guessed.
                    int missing = week.MissingAgainst(nominees[i]);
                    Assert.That(TapesLabel(sides[i], SeasonReport.MissingBallotsName),
                        Is.EqualTo(missing > 0 ? SeasonBallots.MissingSideWords(missing) : ballots.Count == 0 ? "No votes." : null), at + "the side's count of what is not on the record.");
                }
                var breakers = rows.Where(row => row.Find(SeasonReport.TieBreakMarkName) != null).ToList();
                Assert.That(breakers, Has.Count.EqualTo(week.TieBreak != null ? 1 : 0), at + "the tie-break on a row of its own, and only in a tie.");
                if (week.TieBreak != null)
                {
                    Assert.That(breakers[0].parent, Is.SameAs(card), at + "outside the sides, as outside the count.");
                    Assert.That(TapesLabel(breakers[0], "Name"), Is.EqualTo(TapesVoter(state, week.TieBreak)));
                }
                Assert.That(TapesLabel(card, SeasonReport.NotOnRecordName), Is.EqualTo(SeasonBallots.MissingWords(state, week)), at + "whose ballots the record no longer holds, where it knows.");
            }
            Assert.That(lies, Is.EqualTo(tapes.Sum(week => week.rows.Count(row => row.Lied))), where + ": every lie on the tapes is marked.");

            // Every label draws its words, whole, in a box Inter draws in, inside the section.
            AssertEveryLabelDraws(section, where + "'s tapes");
            foreach (var label in section.GetComponentsInChildren<TMP_Text>())
            {
                float size = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(size * 1.3f - .5f),
                    where + ": '" + label.text + "' (" + label.name + ") has a box Inter draws in: " + label.rectTransform.rect.height.ToString("0.#") + " for " + size + ".");
                Assert.That(label.isTextTruncated, Is.False, where + ": '" + label.text + "' is cut.");
            }
            AssertDecisionCopyFits(section);
            return lies;
        }

        /// <summary>
        /// The tapes at the finale, at both text sizes, on the 4:3 batch canvas and on the 16:9
        /// frame (the report laid out for it, as a 16:9 screen lays it out): every eviction against
        /// the reader, the lies marked, every label whole. On the batch canvas, the same season as a
        /// long one ends - a week whose lines the log no longer holds - counts what it cannot place
        /// and names whose. Photographed on the 16:9 frame, scrolled to the section.
        /// </summary>
        [UnityTest]
        public IEnumerator SeasonReport_HowTheHouseVotedReadsEveryBallotWholeAtBothSizesOnBothFrames()
        {
            HoldTheHouseForTheFixture();
            var state = TapesSeason();
            var tapes = SeasonBallots.Read(state);
            var forgotten = TapesWithAWeekForgotten(state);
            var forgottenTapes = SeasonBallots.Read(forgotten);
            var canvas = Report().GetComponent<Canvas>();
            var camera = cameraRig.ViewCamera;
            foreach (bool larger in new[] { false, true })
            {
                string where = "A week forgotten, on the batch canvas" + (larger ? " at the larger text" : " at resting text");
                Report().FontScale = larger ? 1.2f : 1f;
                yield return null;
                Report().Show(forgotten, _ => null, null);
                yield return null;
                AssertTheTapesOnTheReport(forgotten, forgottenTapes, where);
                var section = ReportPart(SeasonReport.HouseBallotsName);
                Assert.That(LabelsUnder(section).Count(text => text.StartsWith("Not on the record: ")), Is.GreaterThan(0), where + ": whose ballots are not on the record, named.");
                Report().Hide();
            }
            foreach (bool wide in new[] { false, true })
                foreach (bool larger in new[] { false, true })
                {
                    string where = (wide ? "On the 16:9 frame" : "On the batch canvas") + (larger ? " at the larger text" : " at resting text");
                    Report().FontScale = larger ? 1.2f : 1f;
                    var previousTarget = camera.targetTexture;
                    RenderTexture frame = null;
                    if (wide)
                    {
                        // The report laid out for the frame the captures photograph, as a 16:9
                        // screen lays it out: its canvas drawn by the view camera into 1600 by 900.
                        frame = new RenderTexture(1600, 900, 24);
                        camera.targetTexture = frame;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
                    }
                    try
                    {
                        yield return Frames(2);
                        Canvas.ForceUpdateCanvases();
                        if (wide)
                        {
                            var room = ((RectTransform)Report().transform).rect;
                            Assert.That(room.width / room.height, Is.EqualTo(16f / 9f).Within(.02f), where + ": laid out on the 16:9 frame, not " + room.size + ".");
                        }
                        Report().Show(state, _ => null, null);
                        yield return null;
                        Assert.That(AssertTheTapesOnTheReport(state, tapes, where), Is.GreaterThan(0), where + ": a lie on the tapes, marked.");

                        if (wide && Application.isBatchMode)
                        {
                            // The tapes in the window: the heading at its top, the first weeks under it.
                            var scroll = Report().GetComponentsInChildren<ScrollRect>().Last();
                            var section = ReportPart(SeasonReport.HouseBallotsName);
                            float travel = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
                            scroll.StopMovement();
                            scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, Mathf.Clamp(-section.anchoredPosition.y - 48f, 0f, travel));
                            yield return CaptureFraming(larger ? "season-report-ballots-large" : "season-report-ballots");
                        }
                        Report().Hide();
                    }
                    finally
                    {
                        if (wide)
                        {
                            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                            camera.targetTexture = previousTarget;
                            frame.Release();
                            Object.Destroy(frame);
                        }
                    }
                }
            Report().FontScale = 1f;
            yield return null;
        }

        /// <summary>
        /// Before the finale the tapes are sealed (decision 8 opens them at the finale, and no
        /// sooner). On the B0 sentinel's fixture - a reveal whose count the player knows and whose
        /// ballots they can only partly place - the report, opened as the director opens it, draws
        /// the section as its sealed line alone, with no week, no face and no ballot, and the
        /// sentinel's rule holds over the whole screen at both text sizes: no voter the player
        /// cannot place beside the nominee they voted against, and no verdict that tells it.
        /// </summary>
        [UnityTest]
        public IEnumerator BallotPrivacy_TheSeasonReportKeepsTheTapesSealedBeforeTheFinale()
        {
            var state = PartlyKnownReveal();
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return SkipReveals();
            director.ClosePanels();
            yield return null;
            var shown = director.Snapshot;
            Assert.That(shown.phase, Is.Not.EqualTo(EpisodePhase.Finished), "A season still being played.");
            var sheet = KnownBallots.Read(shown, shown.week);
            var unknown = PrivateBallots(shown).Where(b => !sheet.Knows(b.voterId)).ToList();
            Assert.That(unknown, Is.Not.Empty, "A ballot the player cannot place, or the sentinel guards nothing.");
            Assert.That(SeasonBallots.Read(shown), Is.Empty, "The reader is sealed,");

            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the large text" : "";
                director.SetLargeText(larger);
                yield return null;
                director.ShowSeasonReport();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(director.IsSeasonReportOpen, Is.True);
                var section = ReportPart(SeasonReport.HouseBallotsName);
                Assert.That(section, Is.Not.Null, "and the report's section with it" + size + ":");
                Assert.That(LabelsUnder(section), Is.EqualTo(new[] { SeasonBallots.SealedLine }), "its sealed line alone" + size + ",");
                Assert.That(section.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == SeasonReport.BallotRowName
                    || rect.name == SeasonReport.BallotSideName || rect.name.StartsWith(SeasonReport.BallotWeekName + " ")), Is.False, "with no week and no ballot" + size + ".");
                AssertEveryLabelDraws(section, "The sealed tapes" + size);
                AssertNoUnknownBallotOnScreen(shown, unknown, "The season report before the finale" + size);
                director.ClosePanels();
                yield return null;
                Assert.That(director.IsSeasonReportOpen, Is.False);
            }
            director.SetLargeText(false);
            yield return null;
        }
    }
}
