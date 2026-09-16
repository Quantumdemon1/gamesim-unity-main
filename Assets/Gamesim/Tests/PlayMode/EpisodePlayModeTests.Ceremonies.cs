using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Plays a real episode and proves the ceremony cards fire inside it.
    ///
    /// The card has its own unit tests, but those build a sting in isolation and tell it what to
    /// say. This drives the actual episode through nomination, veto and eviction with the same
    /// commands a player's clicks produce, and checks that each beat puts the right headline on
    /// screen carrying the text the simulation actually committed. That is the difference between
    /// "the component works" and "the episode uses it".
    ///
    /// Each beat is also rendered to a PNG in the headless harness, so the staging can be reviewed
    /// rather than taken on trust.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Presentation_CeremonyCardsFireDuringAPlayedEpisode()
        {
            var expected = new Dictionary<string, string>
            {
                { CeremonySting.NominationKind, "NOMINATION CEREMONY" },
                { CeremonySting.VetoKind, "VETO CEREMONY" },
                { CeremonySting.EvictionKind, "EVICTION" },
            };
            var outstanding = new HashSet<string>(expected.Keys);

            var sting = SceneComponents<CeremonySting>().FirstOrDefault();
            Assert.That(sting, Is.Not.Null, "The episode should stage a ceremony sting at startup.");
            var group = sting.GetComponent<CanvasGroup>();

            for (int guard = 0; guard < 400 && outstanding.Count > 0; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;

                // Where the log ended before this command. A ceremony is frequently not the last
                // line a commit writes — an eviction is followed by a vote-reveal per voter — so
                // looking only at the final entry misses the beat entirely.
                int known = before.events.Count;
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;

                var after = director.Snapshot;
                var committed = after.events.Skip(known).LastOrDefault(entry =>
                    outstanding.Contains(entry.kind)
                    && (entry.audienceIds.Count == 0 || entry.audienceIds.Contains(after.playerId)));
                if (committed == null) continue;

                // Let the card finish its entrance before reading or photographing it.
                for (int frame = 0; frame < 25; frame++) yield return null;

                var headline = StingText(sting, "Sting headline");
                var detail = StingText(sting, "Sting detail");
                Assert.That(headline, Is.Not.Null, "The card should be built by the time it plays.");
                Assert.That(headline.text, Is.EqualTo(expected[committed.kind]),
                    "The " + committed.kind + " beat should announce itself as " + expected[committed.kind] + ".");
                Assert.That(detail.text, Is.EqualTo(committed.text),
                    "The card must carry the text the simulation committed, not a paraphrase.");
                Assert.That(group.alpha, Is.GreaterThan(0.5f),
                    "The card should be visible at this point, not faded out.");

                AssertCardCoversNoChrome(sting);
                yield return CaptureCeremony(committed.kind);
                outstanding.Remove(committed.kind);
            }

            Assert.That(outstanding, Is.Empty,
                "These ceremonies never showed a card during a played episode: " + string.Join(", ", outstanding));
        }

        /// <summary>
        /// The card renders on a canvas above the HUD, so anything it covers is invisible while it
        /// is up even though it stays clickable — and it is up during the most consequential moments
        /// in the episode. It must not sit over the chrome.
        /// </summary>
        private void AssertCardCoversNoChrome(CeremonySting sting)
        {
            var card = sting.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Card");
            var cardRect = ScreenRect(card);

            foreach (var name in new[] { "Brand", "Objective", "Navigation", "Exploration controls", "Status" })
            {
                var panel = director.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
                if (panel == null) continue;
                Assert.That(cardRect.Overlaps(ScreenRect(panel)), Is.False,
                    "The ceremony card " + cardRect + " covers '" + name + "' " + ScreenRect(panel) + ".");
            }
        }

        private static TMP_Text StingText(CeremonySting sting, string name) =>
            sting.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label => label.name == name);

        /// <summary>
        /// Renders the set plus every overlay — HUD and card together — into an image.
        /// <see cref="ScreenCapture"/> returns black in batchmode, so the canvases are pointed at a
        /// camera with a render target instead, and put back afterwards.
        /// </summary>
        private IEnumerator CaptureCeremony(string kind)
        {
            if (!Application.isBatchMode) yield break;

            const int width = 1600, height = 900;
            var camera = cameraRig.ViewCamera;
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .ToArray();

            var texture = new RenderTexture(width, height, 24);
            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                foreach (var canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
                }

                camera.targetTexture = texture;
                Canvas.ForceUpdateCanvases();
                yield return null;
                camera.Render();

                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();

                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, "..", "ceremony-" + kind + "-in-episode.png"));
                System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Gamesim] Ceremony '" + kind + "' captured in a played episode -> " + path);
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                foreach (var canvas in overlays) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Object.Destroy(readback);
                texture.Release();
                Object.Destroy(texture);
            }
        }
    }
}
