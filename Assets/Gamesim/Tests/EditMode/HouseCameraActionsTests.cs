using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Camera Phase 3 (MASTER-PLAN §3.E): the rig's controls are one Input Actions map with a
    /// binding for the mouse and keyboard and one for a gamepad on every control that has both,
    /// and the exported asset, when it exists, is that map.
    /// </summary>
    public sealed class HouseCameraActionsTests
    {
        [Test]
        public void TheMapCarriesEveryActionInBothControlSchemes()
        {
            var asset = HouseCameraActions.BuildAsset();
            try
            {
                var map = asset.FindActionMap(HouseCameraActions.MapName, throwIfNotFound: true);
                Assert.That(map.actions.Select(a => a.name), Is.EquivalentTo(HouseCameraActions.ActionNames));
                Assert.That(asset.controlSchemes.Select(s => s.name),
                    Is.EquivalentTo(new[] { HouseCameraActions.KeyboardMouseScheme, HouseCameraActions.GamepadScheme }));

                foreach (var action in map.actions)
                {
                    var groups = action.bindings.Where(b => !b.isComposite).SelectMany(b => (b.groups ?? "").Split(';')).ToArray();
                    Assert.That(groups, Is.Not.Empty, action.name + " must be bound to something.");
                    Assert.That(groups.All(g => g == HouseCameraActions.KeyboardMouseScheme || g == HouseCameraActions.GamepadScheme),
                        action.name + ": every binding belongs to one of the two schemes.");
                }
                // The controls a gamepad player needs all have a pad binding; the pointer's position
                // and the wheel's notches are the mouse's alone, by their nature.
                foreach (var name in new[] { "OrbitRate", "Pan", "ZoomRate", "Recenter", "Next", "Previous" })
                    Assert.That(map.FindAction(name).bindings.Any(b => (b.groups ?? "").Contains(HouseCameraActions.GamepadScheme)),
                        name + " must have a gamepad binding.");
                foreach (var name in new[] { "Orbit", "Pan", "Drag", "Zoom", "ZoomRate", "Recenter", "Point" })
                    Assert.That(map.FindAction(name).bindings.Any(b => (b.groups ?? "").Contains(HouseCameraActions.KeyboardMouseScheme)),
                        name + " must have a keyboard-and-mouse binding.");

                // The shortcuts: every one of the director's keys has a gamepad button beside it.
                var shortcuts = asset.FindActionMap(HouseCameraActions.ShortcutsMapName, throwIfNotFound: true);
                Assert.That(shortcuts.actions.Select(a => a.name), Is.EquivalentTo(HouseCameraActions.ShortcutNames));
                foreach (var action in shortcuts.actions)
                {
                    Assert.That(action.bindings.Any(b => (b.groups ?? "").Contains(HouseCameraActions.KeyboardMouseScheme)), action.name + ": a key.");
                    Assert.That(action.bindings.Any(b => (b.groups ?? "").Contains(HouseCameraActions.GamepadScheme)), action.name + ": a gamepad button.");
                }
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TheWrapperFindsEveryActionAndOwnsTheAssetItBuilt()
        {
            using (var actions = new HouseCameraActions())
            {
                Assert.That(actions.Orbit.name, Is.EqualTo("Orbit"));
                Assert.That(actions.Drag.bindings.Any(b => b.path == "<Mouse>/middleButton"), "Middle-drag is the drag's modifier.");
                Assert.That(actions.Zoom.bindings.Single(b => !b.isComposite).path, Is.EqualTo("<Mouse>/scroll/y"));
                actions.Enable();
                Assert.That(actions.Pan.enabled, Is.True);
                actions.Disable();
                Assert.That(actions.Pan.enabled, Is.False);
            }
        }

        [Test]
        public void AnAssetMissingAnActionFailsLoudly()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                asset.AddActionMap(HouseCameraActions.MapName).AddAction("Orbit", InputActionType.Value);
                Assert.That(() => new HouseCameraActions(asset), Throws.Exception,
                    "A hand-edited asset that lost an action must fail at the rig's first frame, not leave a control dead.");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TheExportedAssetWhenPresentIsTheCodeMap()
        {
            var exported = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(Gamesim.Editor.HouseCameraActionsExport.AssetPath);
            if (exported == null) Assert.Ignore("No exported camera actions asset; the rig builds the map in code.");
            var map = exported.FindActionMap(HouseCameraActions.MapName);
            Assert.That(map, Is.Not.Null, "The exported asset must carry the Camera map.");
            Assert.That(map.actions.Select(a => a.name), Is.EquivalentTo(HouseCameraActions.ActionNames),
                "The export is derived from the code; re-run Gamesim/U07/Export the camera actions.");
            var shortcuts = exported.FindActionMap(HouseCameraActions.ShortcutsMapName);
            Assert.That(shortcuts, Is.Not.Null, "The exported asset must carry the Shortcuts map too.");
            Assert.That(shortcuts.actions.Select(a => a.name), Is.EquivalentTo(HouseCameraActions.ShortcutNames));
        }
    }
}
