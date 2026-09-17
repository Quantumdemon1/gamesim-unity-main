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
                // Nomination and eviction are narrated by their own overlays and asserted below;
                // they stay in this map because it is also the set of beats the loop looks for.
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

                // Let both cards finish their entrance before reading or photographing them.
                // A fixed frame count is not enough: batchmode advances frames far faster than
                // real time, so 25 frames is a fraction of a 0.30s fade and the capture comes back
                // uniformly washed — which reads as a rendering defect rather than as a stopwatch
                // problem. Wait on the alpha itself.
                for (int frame = 0; frame < 25; frame++) yield return null;
                yield return SettleCeremonyCards();

                if (committed.kind == CeremonySting.NominationKind)
                {
                    // The nomination is narrated by the key ceremony rather than the strip, for the
                    // same reason the eviction is: the beat is the order the names come out in, and
                    // a strip that reports both at once is the thing being replaced. The guarantee
                    // is unchanged — the beat must announce itself and name who is on the block.
                    var keys = SceneComponents<KeyCeremony>().FirstOrDefault();
                    Assert.That(keys, Is.Not.Null, "The episode should stage a key ceremony at startup.");
                    Assert.That(keys.IsPlaying, Is.True, "The nomination beat should play the key ceremony.");
                    Assert.That(keys.ShowingBlock, Is.True,
                        "The ceremony should have reached the block by the time the cards have settled.");

                    var onTheBlock = keys.GetComponentsInChildren<TMP_Text>(true)
                        .Where(label => label.name == "Nominee")
                        .Select(label => label.text)
                        .ToArray();
                    Assert.That(onTheBlock, Has.Length.EqualTo(after.nominees.Count),
                        "The ceremony should show exactly the committed nominees.");
                    foreach (var id in after.nominees)
                        Assert.That(onTheBlock, Contains.Item(after.Find(id).name),
                            "The ceremony must name the houseguest the simulation actually nominated.");

                    // One key slot per houseguest who draws: everyone still playing except the Head
                    // of Household, who does not draw for their own safety. Structural, so the
                    // key-by-key stage cannot quietly stop happening — the settled capture only ever
                    // photographs the block, and a ceremony that jumped straight there would look
                    // identical in every frame this suite keeps.
                    int drawing = after.contestants.Count(actor =>
                        actor.status == ContestantStatus.Active && actor.id != after.hohId);
                    int slots = keys.GetComponentsInChildren<RectTransform>(true)
                        .Count(rect => rect.name.StartsWith("Key ", System.StringComparison.Ordinal));
                    Assert.That(slots, Is.EqualTo(drawing - after.nominees.Count),
                        "There should be one key slot per houseguest who keeps a key.");

                    Assert.That(keys.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0.5f),
                        "The ceremony should be visible at this point, not faded out.");
                }
                else if (committed.kind == CeremonySting.EvictionKind)
                {
                    // The eviction is narrated by the vote reveal instead of the strip, which would
                    // only flash underneath it and fade out mid-tally. The guarantee this test
                    // exists for is unchanged — the beat must announce itself, and name the person
                    // it happened to — but for this one beat the thing a player sees is the reveal.
                    var reveal = SceneComponents<VoteReveal>().FirstOrDefault();
                    Assert.That(reveal, Is.Not.Null, "The episode should stage a vote reveal at startup.");
                    Assert.That(reveal.IsPlaying, Is.True, "The eviction beat should play the vote reveal.");
                    Assert.That(reveal.ShowingResult, Is.True,
                        "The reveal should have reached its result by the time the cards have settled.");

                    var evicted = after.contestants.FirstOrDefault(actor =>
                        actor.status != ContestantStatus.Active
                        && before.contestants.Any(was => was.id == actor.id && was.status == ContestantStatus.Active));
                    Assert.That(evicted, Is.Not.Null, "An eviction commit should have removed somebody.");

                    var resultBanner = RevealText(reveal, "Result");
                    Assert.That(resultBanner, Is.Not.Null, "The reveal should carry a result banner.");
                    Assert.That(resultBanner.text, Does.Contain(evicted.name.ToUpperInvariant()),
                        "The reveal must name the houseguest the simulation actually evicted.");
                    Assert.That(reveal.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0.5f),
                        "The reveal should be visible at this point, not faded out.");
                }
                else
                {
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
                }

                yield return CaptureCeremony(committed.kind);
                outstanding.Remove(committed.kind);
            }

            Assert.That(outstanding, Is.Empty,
                "These ceremonies never showed a card during a played episode: " + string.Join(", ", outstanding));
        }

        /// <summary>
        /// Waits until every ceremony card that is playing has finished fading in.
        ///
        /// <para>Bounded, and it gives up rather than failing: a card that is already past its hold
        /// is a real thing to photograph, and blocking forever on one would turn a timing quirk into
        /// a hung suite.</para>
        /// </summary>
        private static IEnumerator SettleCeremonyCards()
        {
            // Generous, because the vote reveal is narrated over several seconds of unscaled time
            // and a batchmode frame is a couple of milliseconds. Bounded all the same: a card stuck
            // half-open should photograph badly, not hang the suite.
            const int limit = 6000;
            for (int frame = 0; frame < limit; frame++)
            {
                bool waiting = false;
                foreach (var group in Object.FindObjectsByType<CanvasGroup>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    // The reveal is worth photographing at its payoff, not mid-tally: a frame of
                    // "Revealing vote 2 of 5" says less about the screen than the result does.
                    var reveal = group.GetComponent<VoteReveal>();
                    if (reveal != null && reveal.IsPlaying && !reveal.ShowingResult) { waiting = true; continue; }

                    var keys = group.GetComponent<KeyCeremony>();
                    if (keys != null && keys.IsPlaying && !keys.ShowingBlock) { waiting = true; continue; }

                    if (reveal == null && keys == null
                        && group.GetComponent<CeremonySting>() == null
                        && group.GetComponent<CompetitionResult>() == null
                        && group.GetComponent<CeremonyTakeover>() == null) continue;
                    if (group.alpha > 0.02f && group.alpha < 0.99f) waiting = true;
                }
                if (!waiting) yield break;
                yield return null;
            }
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

        private static TMP_Text RevealText(VoteReveal reveal, string name) =>
            reveal.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label => label.name == name);

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
