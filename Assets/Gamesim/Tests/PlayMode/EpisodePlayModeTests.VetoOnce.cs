using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
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
    /// The veto meeting, once (UI-UX-PASS-PLAN V0, decision 14). A veto meeting was announced three
    /// times at once under three names: the strip ("VETO CEREMONY" over the meeting's sentence), the
    /// card ("Veto Meeting") and the status line under both (the sentence again, "Saved locally.").
    /// The strip is on the kit now; the status line stands down while the strip or the meeting's card
    /// is up, so the strip and the line are never on screen together; and every screen and card calls
    /// it the veto meeting. The living room's screen names the replacement on the block that goes to
    /// the vote, once the Head of Household has named them there, and never on a page before.
    ///
    /// <para>Every wait is on the real clock or on the cards' own state, never a count of frames: the
    /// cards run on the unscaled clock, and a batchmode frame is a fraction of a millisecond.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static CeremonySting Strip() => SceneComponents<CeremonySting>().Single();

        /// <summary>
        /// Whether the status line is on screen: this render's line up, with words in it, on a HUD that
        /// is drawn - neither stepped aside for a reveal nor for a cinematic. A line fading in counts.
        /// </summary>
        private bool StatusLineShowing()
        {
            var hud = Hud;
            if (hud == null || !hud.IsVisible || hud.IsHeldForReveal || hud.IsCinematic) return false;
            var status = LastActive("Status");
            return status != null && status.GetComponentsInChildren<TMP_Text>().Any(label => !string.IsNullOrWhiteSpace(label.text));
        }

        /// <summary>
        /// No words on screen call the veto meeting anything else: nothing on the HUD, the strip, the
        /// card or the living room's screen says "veto ceremony" or "power of veto meeting".
        /// </summary>
        private void AssertTheMeetingHasOneName(string where)
        {
            var shown = director.GetComponentsInChildren<TMP_Text>()
                .Concat(Strip().GetComponentsInChildren<TMP_Text>())
                .Concat(SceneComponents<CeremonyTakeover>().Single().GetComponentsInChildren<TMP_Text>())
                .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text));
            foreach (var label in shown)
            {
                string words = label.text.ToLowerInvariant();
                Assert.That(words.Contains("veto ceremony") || words.Contains("power of veto meeting"), Is.False,
                    where + ": '" + label.text + "' (" + label.name + ") calls the veto meeting by another name.");
            }
        }

        /// <summary>
        /// On the HUD frame - a batch run, reduced motion, a house that cannot be staged - the meeting's
        /// card and the strip reporting it play together, and the status line stands down from the
        /// render the commit makes: on no frame are the strip and the line up together, the line is
        /// never up under the card, and it comes back with the commit's words once both are gone. A
        /// press that takes the card away early leaves the line down until the strip has gone as well.
        /// The strip and the card give the meeting one name, and nothing else on screen gives it
        /// another. A frame of the card and the strip for the look sheet, in a batch run.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoOnce_OnTheHudFrameTheMeetingIsAnnouncedOnceUnderOneName([Values(true, false)] bool used)
        {
            yield return InstallStrategySeason(used ? 81u : 82u, state => AtVetoMeeting(state, playerHolds: true));
            HoldTheHouseForTheFixture();
            // The card on the HUD frame in any run: an interactive run would stage the meeting.
            director.CeremonyStages = false;
            var before = director.Snapshot;
            var block = before.nominees.ToList();
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var strip = Strip();
            var command = NextCommand(before);
            Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto), "The player holds the veto and decides.");
            Assert.That(command.secondTargetId, Is.Not.Null, "A replacement is there to be named.");
            command.useVeto = used;
            command.targetId = used ? block[0] : null;
            var result = director.Submit(command);
            Assert.That(result.accepted, Is.True, result.reason);

            // The frame the commit lands in, before the director has ticked: the render the commit
            // made built the line down.
            Assert.That(director.IsCeremonyStaged, Is.False, "Nothing is staged.");
            Assert.That(takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind && takeover.Surface == null, Is.True,
                "The meeting's card plays on the HUD frame,");
            Assert.That(strip.PlayingKind, Is.EqualTo(CeremonySting.VetoKind), "the strip reports it,");
            Assert.That(director.CeremonyCardAnnouncing, Is.True);
            Assert.That(Hud.IsStatusUnderCard, Is.True);
            Assert.That(StatusLineShowing(), Is.False, "and the status line is down from the render the commit made.");
            var headline = strip.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Sting headline");
            var title = takeover.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Takeover title");
            Assert.That(headline.text, Is.EqualTo("VETO MEETING"), "The strip names the veto meeting,");
            Assert.That(title.text.ToUpperInvariant(), Is.EqualTo(headline.text), "and the card the same meeting.");
            AssertTheMeetingHasOneName("The commit's frame");

            if (used && Application.isBatchMode)
            {
                // Past both entrances, inside the strip's hold.
                yield return RealSeconds(0.45f);
                Assert.That(strip.IsPlaying && takeover.IsPlaying, Is.True, "The strip and the card are up for their frame.");
                yield return CaptureFraming("veto-meeting-sting", settle: false, inspect: frame =>
                {
                    Assert.That(strip.IsPlaying, Is.True, "The strip is up as its frame is taken,");
                    Assert.That(LastActive("Status"), Is.Null, "and the status line down through the capture's own render.");
                });
            }
            else if (!used)
            {
                // A press takes the card away once it has been read; the strip runs on its own clock.
                yield return RealSeconds(0.8f);
                Assert.That(takeover.IsPlaying, Is.True, "The card is still up to be pressed away.");
                takeover.Cancel();
                Assert.That(strip.IsPlaying, Is.True, "The strip outlives the card it reports.");
            }

            // Every frame until both are gone.
            int frames = 0, together = 0, underCard = 0;
            float by = Time.realtimeSinceStartup + 6f;
            while ((strip.IsPlaying || takeover.IsPlaying) && Time.realtimeSinceStartup < by)
            {
                frames++;
                bool line = StatusLineShowing();
                if (line && strip.IsPlaying) together++;
                if (line && takeover.IsPlaying) underCard++;
                yield return null;
            }
            Assert.That(strip.IsPlaying || takeover.IsPlaying, Is.False, "The strip and the card go on their own clocks.");
            Assert.That(together, Is.Zero, "The strip and the status line were up together on " + together + " of " + frames + " frames.");
            Assert.That(underCard, Is.Zero, "The status line was up under the meeting's card on " + underCard + " of " + frames + " frames.");

            // Back the frame after they are gone, saying what the commit said.
            yield return null;
            Assert.That(Hud.IsStatusUnderCard, Is.False, "The status line comes back once the strip and the card are down,");
            Assert.That(StatusLineShowing(), Is.True, "on screen,");
            Assert.That(LastActive("Status").GetComponentsInChildren<TMP_Text>().Single().text, Is.EqualTo(director.StatusMessage),
                "with the director's line.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(result.state.revision), "The cards commit nothing.");

            // The episode screen after the meeting names it as the cards did.
            yield return OpenStation();
            yield return null;
            Assert.That(director.GetComponentsInChildren<TMP_Text>().Any(label => label.gameObject.activeInHierarchy
                && label.text == EpisodeDirector.PhaseTitle(EpisodePhase.VetoMeeting)), Is.True, "The band says VETO MEETING.");
            Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("The Veto Meeting Is Over"));
            AssertTheMeetingHasOneName("The episode screen after the meeting");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// Staged, the meeting is announced by the strip as the house takes its seats and then by its
        /// card on the living room's screen, under the one name, while the chrome - the status line
        /// with it - is aside. Skipped as fast as a player can skip it, the stage lets the house go
        /// while the summons' strip is still up; the line waits for the strip, so the two are never up
        /// together, and then comes back.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoOnce_AStagedMeetingSkippedAtOnceNeverShowsTheStripAndTheLineTogether()
        {
            yield return InstallStagedSeason(83, state => AtVetoMeeting(state, playerHolds: true));
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var strip = Strip();
            var command = NextCommand(director.Snapshot);
            Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto), "The player holds the veto and decides.");
            Assert.That(command.useVeto, Is.True, "and uses it: a replacement is named.");
            var result = director.Submit(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(director.IsCeremonyStaged, Is.True, "The veto meeting is staged in the house.");
            Assert.That(strip.PlayingKind, Is.EqualTo(CeremonySting.VetoKind), "The strip says the house is taking its seats,");
            var headline = strip.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Sting headline");
            Assert.That(headline.text, Is.EqualTo("VETO MEETING"), "under the meeting's one name.");
            Assert.That(StatusLineShowing(), Is.False, "The chrome is aside for the summons, and the status line with it.");
            AssertTheMeetingHasOneName("The summons");

            int frames = 0, together = 0;
            bool Sample()
            {
                frames++;
                if (strip.IsPlaying && StatusLineShowing()) together++;
                return true;
            }

            // Past the guard that keeps the committing press from skipping the summons, then the skip.
            float until = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < until && Sample()) yield return null;
            director.SkipCeremonySummons();
            float by = Time.realtimeSinceStartup + 3f;
            while (!takeover.PlayingMeeting && Time.realtimeSinceStartup < by && Sample()) yield return null;
            Assert.That(takeover.PlayingMeeting, Is.True, "The meeting plays on the living room's screen once the summons is skipped.");
            var screenTitle = takeover.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Meeting title");
            Assert.That(screenTitle.text, Is.EqualTo(headline.text), "The screen titles the meeting as the strip names it.");
            AssertTheMeetingHasOneName("The meeting on the screen");

            // The card closed at once, as a press closes it: the house is let go with the strip still up.
            takeover.Cancel();
            by = Time.realtimeSinceStartup + 6f;
            while ((strip.IsPlaying || director.IsCeremonyStaged) && Time.realtimeSinceStartup < by && Sample()) yield return null;
            Assert.That(director.IsCeremonyStaged, Is.False, "The stage lets the house go.");
            Assert.That(strip.IsPlaying, Is.False, "The strip goes on its own clock.");
            Assert.That(together, Is.Zero, "The strip and the status line were up together on " + together + " of " + frames + " frames.");
            yield return Frames(2);
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back,");
            Assert.That(StatusLineShowing(), Is.True, "and the status line with it.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(result.state.revision), "The stage commits nothing.");
            Assert.That(director.NpcAutonomyDiagnostic, Is.Null, "The house's world outlives the meeting.");
        }

        /// <summary>The faces on the meeting's page, by the name on each, and the words on their badges in order. Read a frame after the page turned, when the last page's faces are gone.</summary>
        private static Dictionary<string, List<string>> MeetingBadgesByName(CeremonyTakeover takeover)
        {
            var faces = new Dictionary<string, List<string>>();
            foreach (var face in MeetingFacesUp(takeover))
            {
                var labels = face.GetComponentsInChildren<TMP_Text>();
                string name = labels.First(label => label.name == "Name").text;
                faces[name] = labels.Where(label => label.name == "Badge text").Select(label => label.text).ToList();
            }
            return faces;
        }

        /// <summary>
        /// Every badge on the meeting's page is the kit's pill: Pack 9's status tag where the pack is
        /// in, a box at least 1.3 times its type, inside the photo it is pinned to, every letter of its
        /// word drawn.
        /// </summary>
        private static void AssertTheMeetingsPillsAreTheKits(CeremonyTakeover takeover, string where)
        {
            Canvas.ForceUpdateCanvases();
            var tag = UiTheme.Pack(PackArt.Pack9NominationCeremonyStatusTagFill);
            var corners = new Vector3[4];
            var words = takeover.GetComponentsInChildren<TMP_Text>().Where(label => label.name == "Badge text").ToList();
            Assert.That(words, Is.Not.Empty, where + " has badges.");
            foreach (var word in words)
            {
                var chip = (RectTransform)word.transform.parent;
                var photo = (RectTransform)chip.parent;
                float type = word.enableAutoSizing ? word.fontSizeMax : word.fontSize;
                Assert.That(chip.rect.height, Is.GreaterThanOrEqualTo(type * 1.3f - .01f),
                    where + ": '" + word.text + "' is in a pill " + chip.rect.height.ToString("0.#") + " high for " + type.ToString("0.#") + " points.");
                Assert.That(word.rectTransform.rect.height, Is.GreaterThanOrEqualTo(type * 1.3f - .01f), where + ": '" + word.text + "' has a box its type fits.");
                word.ForceMeshUpdate(true);
                int drawn = word.textInfo.characterInfo.Take(word.textInfo.characterCount).Count(glyph => glyph.isVisible);
                Assert.That(drawn, Is.EqualTo(word.text.Count(letter => !char.IsWhiteSpace(letter))), where + ": every letter of '" + word.text + "' is drawn.");
                Assert.That(word.isTextTruncated, Is.False, where + ": '" + word.text + "' is not cut.");
                chip.GetWorldCorners(corners);
                var box = photo.rect;
                foreach (var corner in corners)
                {
                    var at = photo.InverseTransformPoint(corner);
                    Assert.That(at.x >= box.xMin - .5f && at.x <= box.xMax + .5f && at.y >= box.yMin - .5f && at.y <= box.yMax + .5f, Is.True,
                        where + ": '" + word.text + "''s pill stands inside the photo it is pinned to.");
                }
                if (tag != null)
                    Assert.That(chip.GetComponent<Image>().sprite, Is.SameAs(tag), where + ": '" + word.text + "' is on the kit's pill.");
            }
        }

        /// <summary>
        /// The living room's screen names the replacement once the Head of Household has named them,
        /// and never before (UI-UX-PASS-PLAN V0): the replacement has no face on the holder's page, the
        /// question or the decision; the replacement's page puts REPLACEMENT on theirs; and the block
        /// that goes to the vote keeps it there, under ON THE BLOCK, where the two faces on it read
        /// ON THE BLOCK alike. Every pill is the kit's and draws its word, at both text sizes (the
        /// screen's frame keeps its own type, so both read the same). Frames of the replacement's page
        /// and of the block for the look sheet, in a batch run.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoOnce_TheScreenNamesTheReplacementOnceTheyAreNamed([Values(false, true)] bool larger)
        {
            // The house held still, so nobody walks into the screen's frame between its pages.
            HoldTheHouseForTheFixture();
            yield return null;
            var scene = director.gameObject.scene;
            var state = director.Snapshot;
            CeremonySeating.Ensure(scene, state.Active.Count());
            Assert.That(ScreenSurface.TryFind(scene, "Living", out var screen), Is.True, "The living room has its ceremony screen.");
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            // The holder, off the block, saves the first nominee; the Head of Household puts the fifth up.
            var before = new List<string> { npcs[1], npcs[2] };
            state.hohId = npcs[0];
            state.vetoHolderId = npcs[3];
            state.nominees = new List<string> { npcs[2], npcs[4] };
            var script = EpisodeDirector.VetoMeetingScriptFor(state, before);
            Assert.That(script.Meeting.HasReplacement, Is.True, "The veto was used and somebody went up.");
            string replacement = state.Find(npcs[4]).name, kept = state.Find(npcs[2]).name, saved = state.Find(npcs[1]).name;
            Assert.That(script.Meeting.replacementId, Is.EqualTo(npcs[4]));

            var takeover = SceneComponents<CeremonyTakeover>().Single();
            takeover.FontScale = larger ? 1.2f : 1f;
            // The suspenseful pace where the look sheet photographs the pages, which then hold for seconds.
            bool capture = Application.isBatchMode && !larger;
            Assert.That(takeover.PlayVetoMeeting(script, false, capture ? CeremonyPace.Suspenseful : CeremonyPace.Quick, screen), Is.True,
                "The meeting plays on the living room's screen.");
            try
            {
                var root = takeover.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Veto meeting");
                float budget = takeover.MeetingDuration + 2f;
                var pages = new[]
                {
                    CeremonyTakeover.MeetingPage.Intro, CeremonyTakeover.MeetingPage.Question, CeremonyTakeover.MeetingPage.Decision,
                    CeremonyTakeover.MeetingPage.Replacement, CeremonyTakeover.MeetingPage.Final,
                };
                foreach (var page in pages)
                {
                    yield return WaitFor(() => takeover.Page == page, budget, "the meeting turns to its " + page + " page");
                    // A frame on: the faces the last page drew go at the end of the frame it turned in.
                    yield return null;
                    Assert.That(takeover.Page, Is.EqualTo(page), page + " is still up a frame after it turned.");
                    var badges = MeetingBadgesByName(takeover);
                    string where = page + " page" + (larger ? " at the larger text" : "");
                    switch (page)
                    {
                        case CeremonyTakeover.MeetingPage.Replacement:
                            Assert.That(badges.ContainsKey(replacement), Is.True, where + ": the replacement's face is up once they are named.");
                            Assert.That(badges[replacement], Is.EqualTo(new[] { CeremonyTakeover.ReplacementBadge }), where + ": they wear the REPLACEMENT pill,");
                            Assert.That(badges[saved], Is.EqualTo(new[] { CeremonyTakeover.SavedBadge }), where + ": beside the one they replace.");
                            break;
                        case CeremonyTakeover.MeetingPage.Final:
                            Assert.That(badges.Keys, Is.EquivalentTo(new[] { kept, replacement }), where + ": the block that goes to the vote.");
                            Assert.That(badges[kept], Is.EqualTo(new[] { CeremonyTakeover.OnTheBlockBadge }), where + ": the nominee who stayed is on the block,");
                            Assert.That(badges[replacement], Is.EqualTo(new[] { CeremonyTakeover.OnTheBlockBadge, CeremonyTakeover.ReplacementBadge }),
                                where + ": and the replacement is on it as the replacement - the screen frame's REPLACEMENT pill.");
                            break;
                        default:
                            // Before the naming: no pill, and no face, for whoever is about to go up.
                            Assert.That(badges.Values.SelectMany(words => words), Has.None.EqualTo(CeremonyTakeover.ReplacementBadge),
                                where + ": nobody is the replacement before the Head of Household names them.");
                            Assert.That(badges.ContainsKey(replacement), Is.False, where + ": the replacement is not on the screen before they are named.");
                            break;
                    }
                    AssertTheMeetingsPillsAreTheKits(takeover, where);
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>().Where(each => !string.IsNullOrWhiteSpace(each.text)))
                    {
                        float font = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                        Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(font * 1.3f - 0.01f),
                            where + ": '" + label.text + "' (" + label.name + ") has a box " + label.rectTransform.rect.height.ToString("0.0")
                            + " high for a " + font.ToString("0.#") + " font.");
                    }
                    AssertEveryLabelDraws(root, where);
                    if (!capture || (page != CeremonyTakeover.MeetingPage.Replacement && page != CeremonyTakeover.MeetingPage.Final)) continue;
                    // The screen's frame as a staged meeting shows it: the chrome aside, as a stage
                    // holds it, so the frame is the screen's and not the HUD's over it.
                    Hud.HoldForReveal(true);
                    string frameName = page == CeremonyTakeover.MeetingPage.Replacement ? "ceremony-stage-veto-replacement" : "ceremony-stage-veto-final";
                    yield return CaptureTheScreen(screen, frameName,
                        frame => Assert.That(takeover.Page, Is.EqualTo(page), frameName + ": the " + page + " page is up as its frame is taken."),
                        waitForFaces: false);
                    Hud.HoldForReveal(false);
                }
            }
            finally
            {
                takeover.Cancel();
                takeover.FontScale = 1f;
                Hud.HoldForReveal(false);
            }
            yield return Frames(2);
        }

        /// <summary>
        /// A meeting that keeps the block names nobody as a replacement on the screen: the block that
        /// goes to the vote is the block that was, ON THE BLOCK alike, with no REPLACEMENT pill.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoOnce_AMeetingThatKeepsTheBlockPutsNobodyUpAsAReplacement()
        {
            yield return null;
            var scene = director.gameObject.scene;
            var state = director.Snapshot;
            CeremonySeating.Ensure(scene, state.Active.Count());
            Assert.That(ScreenSurface.TryFind(scene, "Living", out var screen), Is.True, "The living room has its ceremony screen.");
            var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
            state.hohId = npcs[0];
            state.vetoHolderId = npcs[3];
            state.nominees = new List<string> { npcs[1], npcs[2] };
            var script = EpisodeDirector.VetoMeetingScriptFor(state, state.nominees.ToList());
            Assert.That(script.Meeting.used, Is.False, "The veto was not used.");
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            Assert.That(takeover.PlayVetoMeeting(script, false, CeremonyPace.Quick, screen), Is.True, "The meeting plays on the living room's screen.");
            try
            {
                takeover.SkipToResult();
                Assert.That(takeover.Page, Is.EqualTo(CeremonyTakeover.MeetingPage.Final), "A skip turns to the block that goes to the vote.");
                yield return null;
                var badges = MeetingBadgesByName(takeover);
                Assert.That(badges.Keys, Is.EquivalentTo(state.nominees.Select(id => state.Find(id).name)), "The block that goes to the vote is the block that was,");
                foreach (var pair in badges)
                    Assert.That(pair.Value, Is.EqualTo(new[] { CeremonyTakeover.OnTheBlockBadge }), pair.Key + " is on the block, and nobody is a replacement.");
                AssertTheMeetingsPillsAreTheKits(takeover, "The block kept");
            }
            finally { takeover.Cancel(); }
            yield return Frames(2);
        }
    }
}
