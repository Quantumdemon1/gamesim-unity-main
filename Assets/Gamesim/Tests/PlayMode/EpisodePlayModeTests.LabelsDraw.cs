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
    /// Every label on screen draws its copy.
    ///
    /// <para>The HUD's labels truncate, and TextMesh Pro truncates a line whole when its box is
    /// shorter than the font's line. Inter's line is 1.21 of its size; the default font's was not,
    /// so boxes sized at 1.15 or 1.2 of the text drew their words until the HUD moved to Inter and
    /// then drew nothing at all - the ceremony sting's headline, the eviction tally's figures - with
    /// the text property still saying what it should, which is all the other tests read.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Fails on any label with copy that draws not a single character: the scene's, or one screen's under <paramref name="root"/>.</summary>
        private static void AssertEveryLabelDraws(string where, Transform root = null)
        {
            Canvas.ForceUpdateCanvases();
            var labels = root != null ? root.GetComponentsInChildren<TMP_Text>()
                : Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var empty = labels
                .Where(label => label.enabled && label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text))
                .Where(label =>
                {
                    label.ForceMeshUpdate(true);
                    var info = label.textInfo;
                    return !info.characterInfo.Take(info.characterCount).Any(glyph => glyph.isVisible);
                })
                .Select(label => "'" + label.text + "' (" + label.name + " under " + (label.transform.parent != null ? label.transform.parent.name : "-")
                    + "; font " + (label.font != null ? label.font.name : "none") + " " + label.fontSize.ToString("0.#")
                    + ", box " + label.rectTransform.rect.size.ToString("0")
                    + ", chars " + label.textInfo.characterCount + ", truncated " + label.isTextTruncated + ")")
                .ToArray();
            Assert.That(empty, Is.Empty, where + ": copy that draws nothing: " + string.Join(" | ", empty));
        }

        /// <summary>The same sweep over one part of the screen: every label with copy under <paramref name="root"/> draws some of it.</summary>
        private static void AssertEveryLabelDraws(RectTransform root, string where)
        {
            Canvas.ForceUpdateCanvases();
            var empty = root.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.enabled && label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text))
                .Where(label =>
                {
                    label.ForceMeshUpdate(true);
                    var info = label.textInfo;
                    return !info.characterInfo.Take(info.characterCount).Any(glyph => glyph.isVisible);
                })
                .Select(label => "'" + label.text + "' (" + label.name + ", box " + label.rectTransform.rect.size.ToString("0") + ")")
                .ToArray();
            Assert.That(empty, Is.Empty, where + ": copy that draws nothing: " + string.Join(" | ", empty));
        }

        [UnityTest]
        public IEnumerator Labels_EveryScreenAndCardDrawsItsCopy()
        {
            yield return null;
            AssertEveryLabelDraws("The house");

            director.OpenMainMenu();
            yield return null; yield return null;
            AssertEveryLabelDraws("The main menu");
            director.CloseMainMenu();
            yield return null;

            director.OpenJournal();
            yield return null; yield return null;
            AssertEveryLabelDraws("The notebook");
            director.ClosePanels();
            yield return null;

            yield return OpenDiaryFixturePanel();
            AssertEveryLabelDraws("The diary");
            director.ClosePanels();
            yield return null;

            // A season's cards, each checked the first time it is up, through to the eviction's tally.
            var seen = new HashSet<string>();
            var reveal = SceneComponents<VoteReveal>().Single();
            for (int guard = 0; guard < 400 && !seen.Contains("reveal"); guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                director.Submit(NextCommand(before));
                yield return null;
                foreach (var (name, up) in new[]
                {
                    ("takeover", SceneComponents<CeremonyTakeover>().Any(card => card.IsPlaying)),
                    ("keys", SceneComponents<KeyCeremony>().Any(card => card.IsPlaying)),
                    ("result", SceneComponents<CompetitionResult>().Any(card => card.IsPlaying)),
                    ("reveal", reveal.IsPlaying),
                })
                {
                    if (!up || !seen.Add(name)) continue;
                    // The reveal counts its ballots in over seconds; its figures are up from the start.
                    AssertEveryLabelDraws("The " + name + " card");
                }
            }
            Assert.That(seen, Does.Contain("reveal"), "The season never reached an eviction's tally.");
        }
    }
}
