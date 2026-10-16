using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Caption reading time (PLAN A, A10): the settings' "Caption time" reaches the ceremony's strip
    /// and the witnessed conversation's line, and that line, once shown, stays up for its reading
    /// time when the pair walks out of sight - while a panel, a load or the world pausing still takes
    /// it down at once. The reveals the player paces are not on this clock.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Captions_TheCaptionTimeSettingReachesTheStripAndIsAControlInTheSettings()
        {
            var sting = SceneComponents<CeremonySting>().Single();
            Assert.That(director.CaptionTimeSetting, Is.EqualTo(CaptionTime.Standard), "A fresh session reads at the standard time.");
            Assert.That(sting.CaptionTime, Is.EqualTo(CaptionTime.Standard));
            director.OpenSettings();
            yield return null; yield return null;
            ButtonWithCaption(CaptionTiming.StandardCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.CaptionTimeSetting, Is.EqualTo(CaptionTime.Longer), "The control moves to the longer time,");
            Assert.That(sting.CaptionTime, Is.EqualTo(CaptionTime.Longer), "and the strip reads at it.");
            Assert.That(ShowsControl(CaptionTiming.LongerCaption), Is.True, "The control names the time in use.");
            Assert.That(ShowsControl(CaptionTiming.StandardCaption), Is.False);
            ButtonWithCaption(CaptionTiming.LongerCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.CaptionTimeSetting, Is.EqualTo(CaptionTime.Standard), "and back.");
            Assert.That(sting.CaptionTime, Is.EqualTo(CaptionTime.Standard));
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The witnessed line holds for its reading time once shown: let go while it is still being
        /// read, it stays; past its time it goes; a hide takes it at once. Under "Longer" it holds half
        /// as long again. A line of its own, so the house's world tick cannot show or hide it meanwhile.
        /// </summary>
        [UnityTest]
        public IEnumerator Captions_AWitnessedLineStaysUpForItsReadingTimeOnceShown()
        {
            var holder = new GameObject("Caption time probe");
            try
            {
                var caption = holder.AddComponent<HouseConversationCaption>();
                caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
                // "Maya Hassan and Riley Johnson are discussing the game.": nine words, 3.9 s.
                Assert.That(caption.HoldSeconds, Is.EqualTo(1.2f + 0.3f * 9).Within(1e-4f));
                caption.Release();
                yield return null;
                Assert.That(caption.CurrentText, Is.Not.Empty, "Let go at once, the line stays to be read.");
                Assert.That(caption.IsReleasing, Is.True);
                yield return new WaitForSecondsRealtime(caption.HoldSeconds - 1f);
                Assert.That(caption.CurrentText, Is.Not.Empty, "Still up a second before its time is out.");
                yield return new WaitForSecondsRealtime(1.3f);
                Assert.That(caption.CurrentText, Is.Empty, "and gone once it has been read.");

                // Shown again, a release after its time is up takes it at once.
                caption.Show("Maya Hassan", "Riley Johnson", "gossip", 1f);
                yield return new WaitForSecondsRealtime(caption.HoldSeconds + .2f);
                Assert.That(caption.CurrentText, Is.Not.Empty, "Still witnessed, it stays as long as the pair is in sight.");
                caption.Release();
                Assert.That(caption.CurrentText, Is.Empty, "Read already: let go, it goes.");

                // A hide is at once, read or not: a panel, a load or a paused house.
                caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
                caption.Release();
                caption.Hide();
                Assert.That(caption.CurrentText, Is.Empty, "A hide never waits.");
                Assert.That(caption.IsReleasing, Is.False);

                // Half as long again under "Longer", from the next line shown.
                caption.CaptionTime = CaptionTime.Longer;
                caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
                Assert.That(caption.HoldSeconds, Is.EqualTo((1.2f + 0.3f * 9) * 1.5f).Within(1e-4f));
                caption.Hide();
            }
            finally { Object.Destroy(holder); }
            yield return null;
        }

        /// <summary>
        /// The same hold in the house itself (D3): a pair witnessed at their venue, then the player
        /// put in another room. The director lets the line go rather than taking it down - only a
        /// panel, a load or the house stopping takes it down at once - so it stays up, worded as it
        /// was, until its reading time from when it was first shown is up, offers no Listen in
        /// meanwhile, and goes after. The component's own test above cannot see this: the house's
        /// per-frame witness check took the line down the frame the pair left sight.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Captions_AWitnessedLineInTheHouseStaysForItsReadingTimeAfterThePairLeavesSight()
        {
            var fixture = NpcPendingRuntimeFixture();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            yield return WaitForNpcRuntimeBinding();
            NpcInvoke("SetNpcWorldPaused", false);
            NpcInvoke("RestorePendingNpcMeetings");
            var leases = NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld");
            Assert.That(leases.ContainsKey(1), Is.True, "The saved conversation reserves its venue.");
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");

            // The director ticks its own world every frame; ticking it again here would run the
            // reunion clock at double speed and cancel the meeting mid-walk.
            float deadline = Time.realtimeSinceStartup + 40f;
            while (!director.IsNpcConversationPhysicallyReady(1) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsNpcConversationPhysicallyReady(1), Is.True, "The pair must reach their venue. " + director.NpcAutonomyDiagnostic);
            Assert.That(leases.TryGetValue(1, out var lease), Is.True);

            // Within earshot: in the pair's room, a few metres off, in sight of both.
            var bodies = SceneComponents<HouseNpc>();
            var middle = (bodies.Single(npc => npc.Id == lease.FirstId).transform.position
                + bodies.Single(npc => npc.Id == lease.SecondId).transform.position) * .5f;
            bool placed = false;
            for (int ring = 0; ring < 3 && !placed; ring++)
                for (int direction = 0; direction < 16 && !placed; direction++)
                {
                    float angle = direction * Mathf.PI / 8f;
                    var candidate = middle + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (2f + ring);
                    if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask) || !player.Agent.Warp(hit.position)) continue;
                    player.Agent.ResetPath();
                    Physics.SyncTransforms();
                    placed = coordinator.CanWitness(player, lease);
                }
            Assert.That(placed, Is.True, "Somewhere in the " + lease.RoomId + " in sight of the pair.");

            var caption = NpcRead<HouseConversationCaption>("npcCaption");
            deadline = Time.realtimeSinceStartup + 3f;
            while (string.IsNullOrEmpty(director.ObservedNpcConversation) && Time.realtimeSinceStartup < deadline) yield return null;
            string line = director.ObservedNpcConversation;
            // Read in the frame the line came up: it holds from here.
            float shownAt = Time.unscaledTime, holdFor = caption.HoldSeconds;
            Assert.That(line, Is.Not.Empty, "Witnessed, the pair's line comes up.");
            Assert.That(holdFor, Is.GreaterThan(1.5f), "A line of words takes a while to read: " + line);
            yield return null; yield return null;
            Assert.That(NearbyWanted(), Is.True, "While the pair is witnessed, the Nearby card offers Listen in.");

            // Out of sight: another room, nobody in it, well away from the pair.
            Vector3? away = null;
            foreach (var marker in ActiveRoomMarkers().Where(m => m.RoomName != lease.RoomId && m.RoomName != "HoH" && m.RoomName != "Private"))
            {
                if (!NavMesh.SamplePosition(marker.transform.position, out var hit, 1.5f, player.Agent.areaMask)) continue;
                string there = RoomAt(hit.position, player.Agent.radius);
                if (there == null || there == lease.RoomId || Apart(hit.position, middle) < 8f) continue;
                if (bodies.Any(npc => npc.gameObject.activeInHierarchy && RoomAt(npc.transform) == there)) continue;
                away = hit.position;
                break;
            }
            Assert.That(away.HasValue, Is.True, "Another room with nobody in it.");
            Assert.That(player.Agent.Warp(away.Value), Is.True);
            player.Agent.ResetPath();
            Physics.SyncTransforms();
            Assert.That(coordinator.CanWitness(player, lease), Is.False, "From there the pair is out of sight.");
            Assert.That(Time.unscaledTime - shownAt, Is.LessThan(holdFor - .5f), "The pair leaves sight while the line is still being read.");

            int frames = 0;
            do
            {
                yield return null;
                frames++;
                string at = " (" + (Time.unscaledTime - shownAt).ToString("0.00") + " s of " + holdFor.ToString("0.00") + ", frame " + frames + ")";
                Assert.That(director.ObservedNpcConversation, Is.EqualTo(line), "Out of sight, the line stays up to be read" + at + ".");
                Assert.That(caption.IsReleasing, Is.True, "It is only waiting out its reading time" + at + ".");
                Assert.That(NearbyWanted(), Is.False, "and offers no Listen in meanwhile" + at + ",");
                Assert.That(ControlCarrying(EpisodeHud.ListenInCaption), Is.Null, "nor any control to listen in with" + at + ".");
            }
            while (Time.unscaledTime < shownAt + holdFor - .5f);

            while (Time.unscaledTime < shownAt + holdFor + .3f) yield return null;
            Assert.That(director.ObservedNpcConversation, Is.Empty, "and once it has been read, it goes.");
            Assert.That(NearbyWanted(), Is.False);
        }

        /// <summary>Whether the director has the Nearby card up this frame (a Pull can hold its place on screen).</summary>
        private bool NearbyWanted()
        {
            var field = typeof(EpisodeHud).GetField("nearbyWanted", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing Nearby seam.");
            return (bool)field.GetValue(NpcRead<EpisodeHud>("hud"));
        }
    }
}
