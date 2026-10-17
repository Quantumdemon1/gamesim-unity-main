using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator ConversationExchangeHud_UrgentStatusOwnsTheBandUntilTheCurrentNearbyBarCanReturn()
        {
            director.ClosePanels();
            yield return null;
            var hud = director.GetComponent<EpisodeHud>();
            var parts = hud.GetComponentsInChildren<RectTransform>(true);
            var status = parts.Single(rect => rect.name == "Status");
            var nearbyCard = parts.Single(rect => rect.name == EpisodeHud.NearbyCardName);
            var nearbyBar = parts.Single(rect => rect.name == EpisodeHud.SpeechBarName);

            hud.SetStatusGuidanceVisible(true, notice: true);
            hud.SetNearby(true);
            Assert.That(status.gameObject.activeInHierarchy, Is.True, "The urgent notice keeps the status band.");
            Assert.That(nearbyCard.gameObject.activeInHierarchy, Is.True, "The current Nearby decision remains available.");
            Assert.That(nearbyBar.gameObject.activeInHierarchy, Is.False, "Requesting Nearby cannot cover an urgent notice.");

            hud.SetStatusGuidanceVisible(true, notice: false);
            Assert.That(nearbyCard.gameObject.activeInHierarchy, Is.True);
            Assert.That(nearbyBar.gameObject.activeInHierarchy, Is.True, "Clearing the notice restores the existing Nearby request.");
            Assert.That(status.gameObject.activeInHierarchy, Is.False, "The ordinary guidance yields the band to Nearby.");

            hud.SetStatusGuidanceVisible(true, notice: true);
            Assert.That(status.gameObject.activeInHierarchy, Is.True);
            Assert.That(nearbyBar.gameObject.activeInHierarchy, Is.False, "A notice also wins when the Nearby bar was already visible.");
        }

        [UnityTest]
        public IEnumerator ConversationExchangeHud_CurrentSpeakerAndBothControlsSurviveTheNextBeat()
        {
            yield return SettleCast();
            var npc = SceneComponents<HouseNpc>().Single(actor => actor.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(npc);
            var hud = director.GetComponent<EpisodeHud>();
            var state = director.Snapshot;
            int continued = 0, skipped = 0;
            const string playerLine = "I want us to look out for each other.";
            const string npcLine = "Let me see how this week goes first.";

            void DrawNpc()
            {
                continued++;
                hud.Begin(state, "", false, true);
                hud.RenderConversationExchange(npc.Id, npcLine, false, true,
                    () => continued++, () => skipped++);
            }

            hud.Begin(state, "", false, true);
            hud.RenderConversationExchange(state.playerId, playerLine, true, false, DrawNpc, () => skipped++);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            string playerName = state.Find(state.playerId).name;
            AssertExchangeSpeaker(hud, state.playerId, playerName == "You" ? "You" : playerName + " (You)", playerLine);
            var next = ButtonWithCaption(EpisodeHud.ConversationContinueCaption);
            var skip = ButtonWithCaption(EpisodeHud.ConversationSkipCaption);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(next.gameObject),
                "A spoken beat opens on the way to read the next one.");
            Assert.That(next.navigation.selectOnDown, Is.SameAs(skip), "Both controls are in the keyboard and pad ring.");
            next.onClick.Invoke();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertExchangeSpeaker(hud, npc.Id, state.Find(npc.Id).name, npcLine);
            Assert.That(hud.ActivityContent.GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains(playerLine)), Is.False,
                "Only the current spoken beat is shown.");
            next = ButtonWithCaption(EpisodeHud.ConversationContinueCaption);
            skip = ButtonWithCaption(EpisodeHud.ConversationSkipCaption);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(next.gameObject),
                "Continue keeps its focus across the change of speaker.");
            skip.onClick.Invoke();
            Assert.That(continued, Is.EqualTo(1));
            Assert.That(skipped, Is.EqualTo(1));
            AssertEquivalent(state, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator ConversationExchangeHud_LargestTextKeepsControlsVisibleOnWideAndCompactFrames()
        {
            yield return SettleCast();
            var npc = SceneComponents<HouseNpc>().Single(actor => actor.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(npc);
            var hud = director.GetComponent<EpisodeHud>();
            var canvas = hud.GetComponentsInChildren<Canvas>().Single(item => item.name == "Gamesim Episode HUD");
            var root = (RectTransform)canvas.transform;
            var oldMode = canvas.renderMode;
            var oldPosition = root.position;
            var oldRotation = root.rotation;
            var oldScale = root.localScale;
            var oldSize = root.sizeDelta;
            float oldFont = hud.FontScale;
            bool oldCompact = hud.Compact;
            string longLine = string.Join(" ", Enumerable.Repeat(
                "I need to know that we can still talk honestly when this house puts pressure on us.", 8));
            try
            {
                hud.FontScale = TextSizes.Scale(TextSize.Largest);
                foreach (var (wide, high, compact) in new[] { (1600, 900, false), (2560, 1080, false), (1024, 768, true) })
                {
                    float match = FrameMatch.For(wide, high, .5f);
                    float factor = Mathf.Pow(wide / EpisodeHud.ReferenceWidth, 1f - match)
                        * Mathf.Pow(high / EpisodeHud.ReferenceHeight, match);
                    canvas.renderMode = RenderMode.WorldSpace;
                    root.position = new Vector3(wide * .5f, high * .5f, 0f);
                    root.rotation = Quaternion.identity;
                    root.localScale = new Vector3(factor, factor, 1f);
                    root.sizeDelta = new Vector2(wide / factor, high / factor);
                    hud.Compact = compact;
                    hud.Begin(director.Snapshot, "", false, true);
                    hud.RenderConversationExchange(npc.Id, longLine, false, true, () => { }, () => { });
                    yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();

                    string frame = wide + "x" + high + (compact ? " compact" : "");
                    var column = hud.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == EpisodeHud.ConversationColumnName);
                    var footer = hud.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == EpisodeHud.ConversationExchangeFooterName);
                    var scroll = column.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                    var controls = footer.GetComponentsInChildren<UnityEngine.UI.Button>()
                        .Where(button => button.IsActive() && button.IsInteractable()).ToArray();
                    var next = controls.Single(button => button.GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == EpisodeHud.ConversationContinueCaption));
                    var skip = controls.Single(button => button.GetComponentsInChildren<TMP_Text>()
                        .Any(label => label.text == EpisodeHud.ConversationSkipCaption));
                    foreach (var control in new[] { next, skip })
                        Assert.That(TourEncloses(TourWorldRect(root), TourWorldRect((RectTransform)control.transform)), Is.True,
                            frame + ": '" + control.name + "' stays inside the simulated canvas.");
                    Assert.That(TourEncloses(TourWorldRect(column), TourWorldRect(footer)), Is.True, frame + ": controls stay inside their column.");
                    Assert.That(TourOverlap(TourWorldRect(footer), TourWorldRect(scroll.viewport)), Is.LessThan(1f),
                        frame + ": reading and controls have separate space.");
                    Assert.That(scroll.viewport.rect.height, Is.GreaterThan(80f), frame + ": the spoken line retains reading space.");
                    Assert.That(TourOverlap(TourWorldRect((RectTransform)next.transform), TourWorldRect((RectTransform)skip.transform)), Is.LessThan(1f),
                        frame + ": neither control covers the other.");
                    foreach (var label in footer.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing, Is.False, frame + ": '" + label.text + "' fits at the largest text size.");
                    }
                    var spoken = hud.ActivityContent.GetComponentsInChildren<TMP_Text>().Single(label => label.name == EpisodeHud.ConversationExchangeLineName);
                    spoken.ForceMeshUpdate();
                    Assert.That(spoken.isTextOverflowing, Is.False, frame + ": a long line grows the scroll content instead of clipping.");
                    var was = TourWorldRect(footer);
                    scroll.verticalNormalizedPosition = 0f;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(TourWorldRect(footer), Is.EqualTo(was), frame + ": scrolling does not move the controls.");
                }
            }
            finally
            {
                hud.FontScale = oldFont;
                hud.Compact = oldCompact;
                canvas.renderMode = oldMode;
                root.position = oldPosition;
                root.rotation = oldRotation;
                root.localScale = oldScale;
                root.sizeDelta = oldSize;
                RenderHudForTheCurrentCanvas();
            }
        }

        /// <summary>
        /// The phase step lights the decision while one is up and falls back to the current objective.
        ///
        /// <para>The stand-in decision lives on a canvas of the test's own. The HUD empties its canvas
        /// on every rebuild, whoever owns the children (EpisodeHud.Begin), and on the UMA cast it
        /// rebuilds whenever a body finishes assembling, so a stand-in hung under the HUD was destroyed
        /// mid-test on the UMA build only (r1, 2026-10-16). The rebuild after the panel closes is forced
        /// here, the same Render a body arrival makes, so both builds prove the fallback survives one.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_PhaseStepPrefersTheDecisionAndFallsBackToTheCurrentObjective()
        {
            director.ClosePanels();
            yield return SettleCast();
            var tour = DirectorTour();
            Assert.That(TourChrome("Objective"), Is.Not.Null);
            var host = new GameObject("Tour test panel host", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(host, director.gameObject.scene);
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Episode panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(host.transform, false);
            panel.sizeDelta = new Vector2(700f, 300f);
            try
            {
                tour.Show(name => name == "Episode panel" ? panel : TourChrome(name));
                while (tour.StepIndex < 5) { tour.Next(); yield return null; }
                Assert.That(tour.CurrentAnchors, Is.EqualTo(new[] { "Episode panel" }),
                    "The current decision is the phase step's first choice.");
                panel.gameObject.SetActive(false);
                RenderHudForTheCurrentCanvas();
                // Longer than the tour's .25 s re-lookup, so the step has looked again after the rebuild.
                yield return TourRealSeconds(.3f);
                Assert.That(tour.CurrentAnchors, Is.EqualTo(new[] { "Objective" }),
                    "A closed panel points to the current objective, never the latest status report, "
                    + "and a HUD rebuild while the step is up still points at the current objective.");
                Assert.That(panel != null, Is.True, "The HUD's rebuild leaves a canvas it does not own alone.");
                Assert.That(TourText(tour, "Body"), Does.Contain("A phase is the part of the week"));
                Assert.That(TourText(tour, "Body"), Does.Contain("action has already happened"));
            }
            finally
            {
                tour.Skip();
                if (host != null) Object.Destroy(host);
            }
        }

        private void AssertExchangeSpeaker(EpisodeHud hud, string speakerId, string expectedName, string line)
        {
            var words = hud.ActivityContent.GetComponentsInChildren<TMP_Text>();
            Assert.That(words.Select(label => label.text), Does.Contain(expectedName));
            Assert.That(words.Count(label => label.name == EpisodeHud.ConversationExchangeLineName), Is.EqualTo(1));
            Assert.That(words.Single(label => label.name == EpisodeHud.ConversationExchangeLineName).text, Is.EqualTo("\"" + line + "\""));
            var speaker = hud.ActivityContent.Find("Speaker");
            Assert.That(speaker, Is.Not.Null);
            var face = speaker.GetComponentInChildren<UnityEngine.UI.RawImage>();
            Assert.That(face.GetComponent<CharacterPortraitBinding>(), Is.Not.Null, "The actual speaker's portrait remains bound while it loads.");
            Assert.That(face.texture, Is.SameAs(CharacterPortraits.Get(director.Snapshot.Find(speakerId))));
        }
    }
}
