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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Camera Phase 2 (MASTER-PLAN §3.E): a portrait on the cast rail follows that houseguest, a
    /// ring rides under whoever is followed and a chip names them, and Tab cycles the house when
    /// no panel is open - and does nothing to the camera when one is, because inside a panel Tab
    /// belongs to the keyboard ring.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Follow_ACastRailPortraitFollowsThatHouseguestWithARingAndAChip()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            var rail = director.GetComponentsInChildren<RectTransform>().First(t => t.name == CastRail.RootName);
            var entry = rail.Cast<Transform>().Select(t => t.GetComponent<Button>()).First(b => b != null && b.name == maya.DisplayName);
            entry.onClick.Invoke();
            // Reference equality throughout: NUnit would otherwise compare two Transforms as the
            // collections of children they enumerate, and every body has the same children.
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(maya.transform), "The camera follows the portrait's houseguest.");
            yield return null;
            var ring = GameObject.Find(FollowRing.RingName);
            Assert.That(ring, Is.Not.Null, "A ring marks who is followed.");
            Assert.That(Vector3.Distance(new Vector3(ring.transform.position.x, 0, ring.transform.position.z),
                new Vector3(maya.transform.position.x, 0, maya.transform.position.z)), Is.LessThan(0.05f), "under their feet");
            var chip = director.GetComponentsInChildren<RectTransform>().FirstOrDefault(t => t.name == EpisodeHud.FollowChipName);
            Assert.That(chip, Is.Not.Null, "and a chip names them");
            Assert.That(chip.GetComponentInChildren<TMP_Text>().text, Does.Contain(maya.DisplayName.ToUpperInvariant()));

            entry.onClick.Invoke();
            yield return null;
            Assert.That(cameraRig.FocusedSubject, Is.Null, "The same portrait again lets go.");
            Assert.That(GameObject.Find(FollowRing.RingName), Is.Null, "and the ring goes with it");
            Assert.That(director.GetComponentsInChildren<RectTransform>().Any(t => t.name == EpisodeHud.FollowChipName), Is.False, "and so does the chip");
        }

        [UnityTest]
        public IEnumerator Follow_BracketsCycleTheHouseOutsideAPanelAndLeaveTheCameraAloneInsideOne()
        {
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            director.ClosePanels();
            yield return null; yield return null;
            var active = director.Snapshot.contestants.Where(c => c.status == ContestantStatus.Active).Select(c => c.id).ToList();
            Assume.That(active.Count, Is.GreaterThan(2));

            yield return PressKey(Key.RightBracket);
            var first = cameraRig.FocusedSubject;
            Assert.That(first, Is.Not.Null, "] starts following the first active houseguest.");
            yield return PressKey(Key.RightBracket);
            var second = cameraRig.FocusedSubject;
            Assert.That(second, Is.Not.Null.And.Not.SameAs(first), "] again moves to the next.");
            yield return PressKey(Key.LeftBracket);
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(first), "[ goes back.");

            // Tab is the same cycle for a mouse player who clicked the house and so has no control
            // focused. The HUD keeps a control focused whenever it can, so this only holds when it
            // did not take the focus back in the meantime.
            cameraRig.ClearSubject();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                yield return PressKey(Key.Tab);
                Assert.That(cameraRig.FocusedSubject, Is.Not.Null, "With nothing focused, Tab cycles the house too.");
                cameraRig.ClearSubject();
            }

            director.OpenSettings();
            yield return null;
            var before = cameraRig.FocusedSubject;
            yield return PressKey(Key.RightBracket);
            yield return PressKey(Key.Tab);
            Assert.That(ReferenceEquals(cameraRig.FocusedSubject, before), Is.True, "Inside a panel neither ] nor Tab touches the camera.");
            director.ClosePanels();
            cameraRig.ClearSubject();
        }
    }
}
