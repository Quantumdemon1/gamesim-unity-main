using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Photographs the episode as a player meets it, one frame per beat.
    ///
    /// <para>This exists because the Scene view is a bad way to judge this project and quietly
    /// implies the opposite of the truth. The HUD, the ceremony cards, the interaction prompt, the
    /// name labels and — on the UMA cast — the bodies themselves are all constructed at runtime, so
    /// an editor window showing the committed scene shows an unlit set with six primitive stand-ins
    /// and no interface at all. Everything the slice is assessed on appears only once something is
    /// running.</para>
    ///
    /// <para>Not an assertion of quality. It renders what exists so a person can judge it, which is
    /// the same reason <c>Accessibility_ReportsHowLargeTheCastReadsOnScreen</c> reports a number and
    /// declines to grade it. The only thing failing here means is that a beat did not render.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The phases worth a frame. Others are transitional and photograph identically.</summary>
        private static readonly Dictionary<EpisodePhase, string> WalkthroughBeats =
            new Dictionary<EpisodePhase, string>
            {
                { EpisodePhase.HoH, "competition" },
                { EpisodePhase.Nomination, "nomination" },
                { EpisodePhase.VetoSelection, "veto-players" },
                { EpisodePhase.Veto, "veto-competition" },
                { EpisodePhase.VetoMeeting, "veto-meeting" },
                { EpisodePhase.Campaign, "campaign" },
                { EpisodePhase.Eviction, "eviction" },
                { EpisodePhase.Finished, "aftermath" },
            };

        [UnityTest]
        public IEnumerator Presentation_CapturesAWalkthroughOfTheEpisode()
        {
            if (!Application.isBatchMode) yield break;

            yield return SettleCast();

            // 00 — the literal first frame. The rig is authored well outside its own configured
            // distance and glides in, so this is not the 24 the camera decision was made about; it is
            // what the player actually sees first, which is a different question and worth its own
            // frame rather than a footnote.
            yield return Shoot("walkthrough-00-first-frame");

            // 01 — the same view once the rig has settled: the shipped default framing.
            yield return SettleCamera();
            yield return Shoot("walkthrough-01-arrival");
            LogBeat("arrival", director.Snapshot);

            // 02 — the same set close in, so the cast can be judged as characters rather than as the
            // 1.4%-of-frame silhouettes the default distance makes of them. Labelled, not smuggled:
            // this is not the framing the game starts at.
            SetCameraDistance(10f);
            for (int i = 0; i < 40; i++) yield return null;
            yield return Shoot("walkthrough-02-cast-close-up");
            SetCameraDistance(24f);
            yield return SettleCamera();

            // 03 — a conversation, which is where promises and alliances are actually made.
            var maya = SceneComponents<HouseNpc>().FirstOrDefault(npc => npc.Id == ContentCatalog.MayaId);
            if (maya != null)
            {
                yield return OpenNearbyNpc(maya);
                yield return SettlePanels();
                yield return Shoot("walkthrough-03-conversation");

                // 03b — the same conversation after a committed social action, which is the only
                // state in which the outcome chip exists. Capturing only the opening frame would
                // photograph a panel that can never show it.
                var talk = director.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .FirstOrDefault(button => button.IsActive()
                        && button.GetComponentsInChildren<TMPro.TMP_Text>(true)
                            .Any(label => label.text == "Spend time together"));
                if (talk != null)
                {
                    talk.onClick.Invoke();
                    yield return null; yield return null;
                    yield return SettlePanels();
                    yield return Shoot("walkthrough-03b-conversation-outcome");
                }

                director.ClosePanels();
                yield return null;
            }

            // 04 — the notebook: relationships, promises and the committed event log. The legibility
            // of this panel is what E3 is really asking about.
            yield return OpenNotebook();
            yield return SettlePanels();
            yield return Shoot("walkthrough-04-notebook");

            // 04b — the same panel scrolled to the room map. The notebook is taller than its
            // viewport, so the top frame photographs the graph and nothing below it. Scrolled to a
            // fixed fraction this would silently drift the moment the panel grows a paragraph, so
            // it scrolls to the element by name and photographs wherever that turns out to be.
            var scroll = director.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true)
                .FirstOrDefault(rect => rect.gameObject.activeInHierarchy);
            var map = director.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == Gamesim.Presentation.HouseMap.RootName);
            Assert.That(map, Is.Not.Null, "The notebook should carry the room map.");
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                float travel = scroll.content.rect.height - scroll.viewport.rect.height;
                if (travel > 1f)
                {
                    // anchoredPosition.y is negative going down the content.
                    float target = Mathf.Clamp01(1f - (-map.anchoredPosition.y) / travel);
                    scroll.verticalNormalizedPosition = target;
                }
                Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                yield return Shoot("walkthrough-04b-notebook-rooms");
            }

            director.ClosePanels();
            yield return null;

            // 05 onward — drive the episode with the same commands a player's clicks produce, and
            // photograph each phase the first time it is reached.
            var photographed = new HashSet<EpisodePhase>();
            int index = 5;
            for (int guard = 0; guard < 400; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;

                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;

                var phase = director.Snapshot.phase;
                if (!WalkthroughBeats.TryGetValue(phase, out var label)) continue;
                if (!photographed.Add(phase)) continue;

                // Let a ceremony card finish its entrance, so the beat is photographed staged rather
                // than mid-animation.
                for (int frame = 0; frame < 30; frame++) yield return null;
                yield return SettleCeremonyCards();

                yield return Shoot("walkthrough-" + index.ToString("00") + "-" + label);
                LogBeat(label, director.Snapshot);
                index++;
            }

            var final = director.Snapshot;
            if (final.phase == EpisodePhase.Finished && photographed.Add(EpisodePhase.Finished))
            {
                for (int frame = 0; frame < 30; frame++) yield return null;
                yield return SettleCeremonyCards();
                yield return Shoot("walkthrough-" + index.ToString("00") + "-aftermath");
                LogBeat("aftermath", final);
            }

            Assert.That(photographed, Is.Not.Empty,
                "The episode was driven to " + final.phase + " without reaching a single photographable beat.");
            Debug.Log("[Gamesim] walkthrough captured " + (photographed.Count + 4) + " frames, ending in " + final.phase + ".");
        }

        /// <summary>
        /// Reports the camera's actual distance from its pivot over the first several seconds of an
        /// untouched episode, against the distance the rig is configured for.
        ///
        /// <para>Written because the walkthrough captures disagreed with the record. The camera
        /// decision was taken on <c>HouseCameraRig.distance = 24</c>, but the opening frames measure
        /// 48 — and the earlier legibility pass recorded houseguests "42-54 metres from camera"
        /// without anyone noticing that a 24-unit rig cannot produce a 48-unit shot. Whether the rig
        /// converges on its own or simply never applies its own default changes what the cast-reads-
        /// small finding means, so this measures it rather than reasoning about it.</para>
        ///
        /// <para>No threshold: this reports, it does not grade.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Camera_ReportsStartupFramingOverTime()
        {
            yield return SettleCast();

            var configured = typeof(HouseCameraRig)
                .GetField("distance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(cameraRig);

            int[] marks = { 0, 15, 30, 60, 120, 240, 480 };
            int next = 0;
            for (int frame = 0; frame <= marks[marks.Length - 1]; frame++)
            {
                if (next < marks.Length && frame == marks[next])
                {
                    float actual = Vector3.Distance(
                        cameraRig.ViewCamera.transform.position, cameraRig.transform.position);
                    Debug.Log(string.Format(
                        "[Gamesim] startup framing · frame {0,3} · actual {1:F1} units · rig field {2}",
                        frame, actual, configured));
                    next++;
                }
                yield return null;
            }
        }

        /// <summary>
        /// Captures a frame and records the camera distance it was taken at.
        ///
        /// <para>The framing is the open design question these images exist to inform, so a frame
        /// whose distance is assumed rather than measured is worse than no frame — it would invite a
        /// judgement about the default from a picture that is not the default.</para>
        /// </summary>
        private IEnumerator Shoot(string name)
        {
            var camera = cameraRig.ViewCamera;
            float distance = Vector3.Distance(camera.transform.position, cameraRig.transform.position);
            Debug.Log(string.Format("[Gamesim] walkthrough frame · {0} · camera {1:F1} units from the rig pivot",
                name, distance));
            yield return CaptureFraming(name);
        }

        /// <summary>
        /// Waits until the camera rig stops moving, so a frame labelled as the default framing was
        /// actually taken at it.
        /// </summary>
        private IEnumerator SettleCamera()
        {
            const int limit = 600;
            float previous = -1f;
            int stable = 0;
            for (int frame = 0; frame < limit; frame++)
            {
                float distance = Vector3.Distance(
                    cameraRig.ViewCamera.transform.position, cameraRig.transform.position);
                stable = Mathf.Abs(distance - previous) < 0.01f ? stable + 1 : 0;
                if (stable >= 10) yield break;
                previous = distance;
                yield return null;
            }
        }

        /// <summary>
        /// Waits for every fading panel to finish arriving.
        ///
        /// <para>Photographing a panel one frame after opening it catches it mid-fade, and the
        /// resulting frame shows the set washing through copy that is nearly opaque in play — a
        /// defect that exists only in the photograph. The theme's <c>Surface</c> is alpha 250/255, so
        /// any translucency in a settled capture is real and anything else is this.</para>
        /// </summary>
        private IEnumerator SettlePanels()
        {
            const int limit = 120;
            for (int frame = 0; frame < limit; frame++)
            {
                Canvas.ForceUpdateCanvases();
                var fading = director.GetComponentsInChildren<CanvasGroup>(true)
                    .Where(group => group.gameObject.activeInHierarchy)
                    .Any(group => group.alpha > 0.01f && group.alpha < 0.99f);
                if (!fading && frame > 4) yield break;
                yield return null;
            }
        }

        /// <summary>
        /// Writes what the simulation actually holds at the moment of the frame, so a reviewer can
        /// check the picture against the state rather than inferring the state from the picture.
        /// </summary>
        private static void LogBeat(string label, EpisodeState state)
        {
            var active = state.contestants.Where(actor => actor.status == ContestantStatus.Active).ToArray();
            var evicted = state.contestants.Where(actor => actor.status == ContestantStatus.Evicted).ToArray();
            Debug.Log(string.Format(
                "[Gamesim] walkthrough · {0} · phase {1} · week {2} · {3} active ({4}){5} · {6} promises · {7} events",
                label, state.phase, state.week, active.Length,
                string.Join(", ", active.Select(actor => actor.name)),
                evicted.Length == 0 ? "" : " · evicted: " + string.Join(", ", evicted.Select(actor => actor.name)),
                state.promises == null ? 0 : state.promises.Count,
                state.events == null ? 0 : state.events.Count));
        }
    }
}
