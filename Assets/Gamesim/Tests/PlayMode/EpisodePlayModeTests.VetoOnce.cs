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
    /// card ("Veto Meeting") and the status line under both (the sentence again, "Saved locally."),
    /// over an episode screen redrawn with the sentence a fourth time. The strip is on the kit now;
    /// on the HUD frame the card has the frame to itself, the chrome aside as under the endgame's
    /// cards, and a press that moves the card on takes its strip with it; the status line stands down
    /// while the strip or the card says what it says; and every screen and card calls it the veto
    /// meeting. The living room's screen names the replacement on the block that goes to the vote,
    /// once the Head of Household has named them there, and never on a page before.
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
        /// How many labels on screen this frame say <paramref name="sentence"/>: the strip's and the
        /// card's while they play, and the HUD's while it is drawn - not stepped aside for a reveal or
        /// a cinematic.
        /// </summary>
        private int TimesOnScreen(string sentence)
        {
            var labels = new List<TMP_Text>();
            var strip = Strip();
            if (strip.IsPlaying) labels.AddRange(strip.GetComponentsInChildren<TMP_Text>());
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            if (takeover.IsPlaying) labels.AddRange(takeover.GetComponentsInChildren<TMP_Text>());
            var hud = Hud;
            if (hud != null && hud.IsVisible && !hud.IsHeldForReveal && !hud.IsCinematic) labels.AddRange(director.GetComponentsInChildren<TMP_Text>());
            return labels.Count(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text) && label.text.Contains(sentence));
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
        /// card and the strip reporting it play together with the chrome aside, as under the endgame's
        /// cards: every frame from the commit on - the waits included - the strip and the status line
        /// are never up together, the line is never up under the card, the chrome never comes back
        /// while the card is up, and the meeting's sentence is never on screen twice. The line comes
        /// back with the director's words once both are gone. A press that moves the card on early
        /// takes the strip with it. The strip and the card give the meeting one name, and nothing else
        /// on screen gives it another. A frame of the card and the strip for the look sheet, in a
        /// batch run.
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
            string sentence = result.state.events.Last(entry => entry.kind == CeremonySting.VetoKind).text;

            // The frame the commit lands in, before the director has ticked: the render the commit
            // made was made under the hold, with the line down.
            Assert.That(director.IsCeremonyStaged, Is.False, "Nothing is staged.");
            Assert.That(takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind && takeover.Surface == null, Is.True,
                "The meeting's card plays on the HUD frame,");
            Assert.That(strip.PlayingKind, Is.EqualTo(CeremonySting.VetoKind), "the strip reports it,");
            Assert.That(Hud.IsHeldForReveal, Is.True, "the chrome stands aside under the card, as under the endgame's cards,");
            Assert.That(director.CeremonyCardAnnouncing, Is.True);
            Assert.That(Hud.IsStatusUnderCard, Is.True);
            Assert.That(StatusLineShowing(), Is.False, "the status line with it,");
            Assert.That(TimesOnScreen(sentence), Is.EqualTo(1), "and the meeting's sentence is on screen once: the strip's.");
            var headline = strip.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Sting headline");
            var title = takeover.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Takeover title");
            Assert.That(headline.text, Is.EqualTo("VETO MEETING"), "The strip names the veto meeting,");
            Assert.That(title.text.ToUpperInvariant(), Is.EqualTo(headline.text), "and the card the same meeting.");
            AssertTheMeetingHasOneName("The commit's frame");

            // Every frame, the waits' included.
            int frames = 0, together = 0, underCard = 0, unheld = 0, twice = 0;
            void Sample()
            {
                frames++;
                bool line = StatusLineShowing();
                if (line && strip.IsPlaying) together++;
                if (line && takeover.IsPlaying) underCard++;
                if (takeover.IsPlaying && !Hud.IsHeldForReveal) unheld++;
                if ((strip.IsPlaying || takeover.IsPlaying) && TimesOnScreen(sentence) > 1) twice++;
            }
            IEnumerator Sampled(float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until) { Sample(); yield return null; }
            }

            if (used && Application.isBatchMode)
            {
                // Past both entrances, inside the strip's hold.
                yield return Sampled(0.45f);
                Assert.That(strip.IsPlaying && takeover.IsPlaying, Is.True, "The strip and the card are up for their frame.");
                yield return CaptureFraming("veto-meeting-sting", settle: false, inspect: frame =>
                {
                    Assert.That(strip.IsPlaying, Is.True, "The strip is up as its frame is taken,");
                    Assert.That(Hud.IsHeldForReveal, Is.True, "the chrome aside,");
                    Assert.That(LastActive("Status"), Is.Null, "and the status line down through the capture's own render.");
                });
            }
            else if (!used)
            {
                // A press moves the card on once it has been read: on the HUD frame the strip goes with it.
                yield return Sampled(0.8f);
                Assert.That(takeover.IsPlaying && strip.IsPlaying, Is.True, "The card and the strip are still up to be pressed away.");
                takeover.Cancel();
                Sample();
                yield return null;
                Assert.That(strip.IsPlaying, Is.False, "A press that moves the meeting's card on takes its strip with it,");
                Assert.That(Hud.IsHeldForReveal, Is.False, "and gives the chrome back.");
            }

            float by = Time.realtimeSinceStartup + 6f;
            while ((strip.IsPlaying || takeover.IsPlaying) && Time.realtimeSinceStartup < by)
            {
                Sample();
                yield return null;
            }
            Assert.That(strip.IsPlaying || takeover.IsPlaying, Is.False, "The strip and the card go on their own clocks.");
            Assert.That(together, Is.Zero, "The strip and the status line were up together on " + together + " of " + frames + " frames.");
            Assert.That(underCard, Is.Zero, "The status line was up under the meeting's card on " + underCard + " of " + frames + " frames.");
            Assert.That(unheld, Is.Zero, "The chrome came back under the meeting's card on " + unheld + " of " + frames + " frames.");
            Assert.That(twice, Is.Zero, "The meeting's sentence was on screen twice on " + twice + " of " + frames + " frames.");

            // Back the frame after they are gone, saying what the commit said.
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the card is down,");
            Assert.That(Hud.IsStatusUnderCard, Is.False, "and the status line with it,");
            Assert.That(StatusLineShowing(), Is.True, "on screen,");
            Assert.That(LastActive("Status").GetComponentsInChildren<TMP_Text>().Single().text, Is.EqualTo(director.StatusMessage),
                "with the director's line.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(result.state.revision), "The cards commit nothing.");

            // The episode screen after the meeting names it as the cards did.
            yield return OpenStation();
            yield return null;
            Assert.That(Words(LastActive("Phase band")), Does.Contain(EpisodeDirector.PhaseTitle(EpisodePhase.VetoMeeting)), "The band says VETO MEETING.");
            Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("The Veto Meeting Is Over"));
            AssertTheMeetingHasOneName("The episode screen after the meeting");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The meeting committed from the open episode screen, on the HUD frame: the screen redraws as
        /// the meeting's outcome under the card - "The Veto Meeting Is Over", the meeting's sentence
        /// and VETO USED - beside the strip that says the sentence (the review's M1). So the chrome
        /// stands aside for as long as the card is up, as it does under the endgame's cards, and the
        /// sentence is on screen once, the strip's; once the card is down the screen is back as it
        /// was, still open.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoOnce_CommittedFromTheEpisodeScreenTheCardHasTheFrameAndTheSentenceIsSaidOnce()
        {
            yield return InstallStrategySeason(84, state => AtVetoMeeting(state, playerHolds: true));
            HoldTheHouseForTheFixture();
            director.CeremonyStages = false;
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            yield return OpenStation();
            yield return null;
            var before = director.Snapshot;
            Assert.That(director.IsPhasePanelOpen, Is.True, "The episode screen is open on the holder's decision.");
            ButtonWithCaption(EpisodeDirector.VetoSaveCaption(before, before.nominees[0])).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.vetoResolved && after.revision == before.revision + 1, Is.True, "One press uses the veto.");
            string sentence = after.events.Last(entry => entry.kind == CeremonySting.VetoKind).text;
            Assert.That(director.IsPhasePanelOpen, Is.True, "The episode screen stays open under the card,");
            Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("The Veto Meeting Is Over"),
                "redrawn as the meeting's outcome,");
            Assert.That(takeover.IsPlaying && takeover.PlayingKind == CeremonySting.VetoKind && takeover.Surface == null, Is.True,
                "the meeting's card over it on the HUD frame,");
            Assert.That(Hud.IsHeldForReveal, Is.True, "and the chrome aside while the card is up.");
            Assert.That(TimesOnScreen(sentence), Is.EqualTo(1), "The meeting's sentence is on screen once: the strip's.");

            int frames = 0, unheld = 0, twice = 0;
            float by = Time.realtimeSinceStartup + 6f;
            while (takeover.IsPlaying && Time.realtimeSinceStartup < by)
            {
                frames++;
                if (!Hud.IsHeldForReveal) unheld++;
                if (TimesOnScreen(sentence) > 1) twice++;
                yield return null;
            }
            Assert.That(takeover.IsPlaying, Is.False, "The card goes on its own clock.");
            Assert.That(unheld, Is.Zero, "The chrome came back under the card on " + unheld + " of " + frames + " frames.");
            Assert.That(twice, Is.Zero, "The meeting's sentence was on screen twice on " + twice + " of " + frames + " frames while the card was up.");
            yield return null;
            Assert.That(Hud.IsHeldForReveal, Is.False, "The chrome is back once the card is down,");
            Assert.That(director.IsPhasePanelOpen, Is.True, "the episode screen with it,");
            Assert.That(ActiveRect(EpisodeHud.CeremonyTitleName).GetComponent<TMP_Text>().text, Is.EqualTo("The Veto Meeting Is Over"), "as it was.");
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
        /// screen's frame keeps its own type, so both read the same). Every frame is watched and each
        /// page is read a frame after it turns; the replacement's page and the last must be read, and
        /// an earlier page a long frame skipped past is checked wherever it was seen.
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
            // The suspenseful pace: each page holds about three seconds.
            Assert.That(takeover.PlayVetoMeeting(script, false, CeremonyPace.Suspenseful, screen), Is.True,
                "The meeting plays on the living room's screen.");
            try
            {
                var root = takeover.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Veto meeting");
                var read = new HashSet<CeremonyTakeover.MeetingPage>();
                CeremonyTakeover.MeetingPage? last = null;
                float by = Time.realtimeSinceStartup + takeover.MeetingDuration + 2f;
                while (takeover.PlayingMeeting && !read.Contains(CeremonyTakeover.MeetingPage.Final) && Time.realtimeSinceStartup < by)
                {
                    var page = takeover.Page;
                    // A page is read the frame after it turned, once the last page's faces - destroyed at
                    // the end of the frame it turned in - are gone, and read once.
                    if (page.HasValue && page == last && read.Add(page.Value))
                    {
                        var badges = MeetingBadgesByName(takeover);
                        string where = page.Value + " page" + (larger ? " at the larger text" : "");
                        switch (page.Value)
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
                    }
                    last = page;
                    yield return null;
                }
                Assert.That(read, Does.Contain(CeremonyTakeover.MeetingPage.Replacement), "The replacement's page was read: " + string.Join(", ", read));
                Assert.That(read, Does.Contain(CeremonyTakeover.MeetingPage.Final), "The block that goes to the vote was read: " + string.Join(", ", read));
            }
            finally
            {
                takeover.Cancel();
                takeover.FontScale = 1f;
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

        /// <summary>
        /// The look sheet's frames of the replacement on the living room's screen, as a staged meeting
        /// plays them (the review's m4): the replacement's page and the block that goes to the vote,
        /// each on the stage's own cut to the screen with the lens kept clear of the house
        /// (UI-UX-PASS-PLAN K0), and no body in the card as it is taken. Batch runs only.
        /// </summary>
        [UnityTest]
        public IEnumerator VetoOnce_CapturesTheReplacementOnTheStagedScreen()
        {
            if (!Application.isBatchMode) yield break;
            yield return InstallStagedSeason(85, state => AtVetoMeeting(state, playerHolds: true));
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            var command = NextCommand(director.Snapshot);
            Assert.That(command.kind, Is.EqualTo(EpisodeCommandKind.ResolveVeto), "The player holds the veto and decides.");
            Assert.That(command.useVeto, Is.True, "and uses it: a replacement is named.");
            var result = director.Submit(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(director.IsCeremonyStaged, Is.True, "The veto meeting is staged in the house.");
            director.SkipCeremonySummons();
            yield return WaitFor(() => takeover.PlayingMeeting, 3f, "the meeting plays once the summons is skipped");
            var screen = takeover.Surface;
            foreach (var (page, frameName) in new[]
                     {
                         (CeremonyTakeover.MeetingPage.Replacement, "ceremony-stage-veto-replacement"),
                         (CeremonyTakeover.MeetingPage.Final, "ceremony-stage-veto-final"),
                     })
            {
                yield return WaitFor(() => takeover.Page == page, takeover.MeetingDuration + 3f, "the meeting turns to its " + page + " page");
                // Taken at once, on the cut the page arrived with: the stage cuts to the Head of
                // Household a quarter of the replacement's page in, and the card is bound already.
                yield return CaptureTheScreen(screen, frameName, frame =>
                {
                    Assert.That(takeover.Page, Is.EqualTo(page), frameName + ": the " + page + " page is up as its frame is taken.");
                    AssertNoBodyStandsInTheCard(takeover, screen, frameName, "Veto meeting");
                }, waitForFaces: false);
            }
            takeover.Cancel();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
            Assert.That(director.Snapshot.revision, Is.EqualTo(result.state.revision), "The stage commits nothing.");
        }
    }
}
