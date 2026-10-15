using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Camera Phase 3 (MASTER-PLAN §3.E) and PLAN A's A1: the game's controls are one Input Actions
    /// asset - the camera, the house's shortcuts, the ceremony cards, the competitions, the season
    /// report and the prototype's dialogue - with a binding for the mouse and keyboard and one for a
    /// gamepad on every control that has both, and the exported asset is that asset, binding for
    /// binding.
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

                foreach (var action in asset.actionMaps.SelectMany(each => each.actions))
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
                // OrbitRate has keys since PLAN A, A7: Q turns the camera left and C right, as the stick does.
                Assert.That(HouseCameraActions.BindingWords(map.FindAction("OrbitRate"), HouseCameraActions.KeyboardMouseScheme), Is.EqualTo("Q/C"));
                foreach (var name in new[] { "Orbit", "OrbitRate", "Pan", "Drag", "Zoom", "ZoomRate", "Recenter", "Point" })
                    Assert.That(map.FindAction(name).bindings.Any(b => (b.groups ?? "").Contains(HouseCameraActions.KeyboardMouseScheme)),
                        name + " must have a keyboard-and-mouse binding.");

                // The shortcuts: every one of the director's keys has a gamepad button beside it.
                // Back is the pad's alone (PLAN A, A2): the keyboard's back is Escape, which is Menu.
                var shortcuts = asset.FindActionMap(HouseCameraActions.ShortcutsMapName, throwIfNotFound: true);
                Assert.That(shortcuts.actions.Select(a => a.name), Is.EquivalentTo(HouseCameraActions.ShortcutNames));
                foreach (var action in shortcuts.actions)
                {
                    bool key = action.bindings.Any(b => (b.groups ?? "").Contains(HouseCameraActions.KeyboardMouseScheme));
                    Assert.That(key, Is.EqualTo(action.name != "Back"), action.name + (action.name == "Back" ? ": no key; Escape is Menu." : ": a key."));
                    Assert.That(action.bindings.Any(b => (b.groups ?? "").Contains(HouseCameraActions.GamepadScheme)), action.name + ": a gamepad button.");
                }
                Assert.That(shortcuts.FindAction("Back").bindings.Select(b => b.path), Is.EqualTo(new[] { "<Gamepad>/buttonEast" }),
                    "Back is the pad's B and nothing else, separate from Menu's Start.");
                Assert.That(shortcuts.FindAction("Menu").bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/escape", "<Gamepad>/start" }));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TheAsset_HoldsEveryMapWithItsActionsInOrder()
        {
            var asset = HouseCameraActions.BuildAsset();
            try
            {
                Assert.That(asset.actionMaps.Select(map => map.name), Is.EqualTo(HouseCameraActions.Maps.Select(map => map.Key)),
                    "The camera, the shortcuts, the ceremony cards, the competitions, the report and the prototype's dialogue, in that order.");
                foreach (var entry in HouseCameraActions.Maps)
                    Assert.That(asset.FindActionMap(entry.Key, throwIfNotFound: true).actions.Select(action => action.name), Is.EqualTo(entry.Value), entry.Key);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TheNewMaps_HaveAPadWayWhereverAKeyHasOne()
        {
            var asset = HouseCameraActions.BuildAsset();
            try
            {
                // What each map's actions are bound to, by scheme: (keyboard and mouse, gamepad).
                var expected = new Dictionary<string, (bool Keys, bool Pad)>
                {
                    { "Ceremony/Skip", (true, true) }, { "Ceremony/Speed", (true, true) },
                    // The opening's Space; a pad presses its focused Continue, through the UI module.
                    { "Ceremony/Advance", (true, false) },
                    { "Competition/Up", (true, true) }, { "Competition/Right", (true, true) },
                    { "Competition/Down", (true, true) }, { "Competition/Left", (true, true) },
                    // The classic reaction game's Space; a pad presses the focused target.
                    { "Competition/Hit", (true, false) },
                    { "Competition/Hold", (true, true) }, { "Competition/Pause", (true, true) }, { "Competition/Back", (true, true) },
                    { "Competition/Undo", (true, true) }, { "Competition/FocusNext", (true, true) },
                    { "Competition/FocusPrevious", (true, true) }, { "Competition/Confirm", (true, true) },
                    { "Report/PageUp", (true, true) }, { "Report/PageDown", (true, true) },
                    { "Report/Home", (true, true) }, { "Report/End", (true, true) },
                    // The right stick scrolls; the d-pad and the left stick walk the ring.
                    { "Report/Scroll", (false, true) }, { "Report/Walk", (false, true) },
                    // The prototype's numbered replies; its buttons take the pad.
                    { "Dialogue/Reply1", (true, false) }, { "Dialogue/Reply2", (true, false) }, { "Dialogue/Reply3", (true, false) },
                };
                var seen = new List<string>();
                foreach (var mapName in new[] { HouseCameraActions.CeremonyMapName, HouseCameraActions.CompetitionMapName,
                    HouseCameraActions.ReportMapName, HouseCameraActions.DialogueMapName })
                    foreach (var action in asset.FindActionMap(mapName, throwIfNotFound: true).actions)
                    {
                        string key = mapName + "/" + action.name;
                        seen.Add(key);
                        Assert.That(expected.ContainsKey(key), Is.True, key + " is not in this test's table.");
                        bool keys = action.bindings.Any(b => !b.isComposite && (b.groups ?? "").Contains(HouseCameraActions.KeyboardMouseScheme));
                        bool pad = action.bindings.Any(b => !b.isComposite && (b.groups ?? "").Contains(HouseCameraActions.GamepadScheme));
                        Assert.That(keys, Is.EqualTo(expected[key].Keys), key + ": a key.");
                        Assert.That(pad, Is.EqualTo(expected[key].Pad), key + ": a pad button.");
                    }
                Assert.That(seen, Is.EquivalentTo(expected.Keys));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TabIsFoldedIntoNextAndPrevious_AndTheEmoteHasAPadButton()
        {
            using (var actions = new HouseCameraActions())
            {
                Assert.That(actions.Next.bindings.Any(b => b.path == "<Keyboard>/tab"), "Tab is a binding of Next.");
                var previous = actions.Previous.bindings.ToList();
                int composite = previous.FindIndex(b => b.isComposite && b.path == "OneModifier");
                Assert.That(composite, Is.GreaterThanOrEqualTo(0), "Shift+Tab is a binding of Previous.");
                Assert.That(previous[composite + 1].path, Is.EqualTo("<Keyboard>/shift"));
                Assert.That(previous[composite + 2].path, Is.EqualTo("<Keyboard>/tab"));
                Assert.That(HouseCameraActions.BindingWords(actions.Previous, HouseCameraActions.KeyboardMouseScheme), Is.EqualTo("[ / Shift+Tab"));
                Assert.That(HouseCameraActions.BindingWords(actions.Emote, HouseCameraActions.KeyboardMouseScheme), Is.EqualTo("G"));
                Assert.That(HouseCameraActions.BindingWords(actions.Emote, HouseCameraActions.GamepadScheme), Is.EqualTo("D-pad right"));
                Assert.That(HouseCameraActions.BindingWords(actions.Skip, HouseCameraActions.GamepadScheme), Is.EqualTo("A / B"));
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
                // Each property is its own map's action of that name.
                var properties = new[]
                {
                    (actions.Emote, HouseCameraActions.ShortcutsMapName + "/Emote"),
                    (actions.Back, HouseCameraActions.ShortcutsMapName + "/Back"),
                    (actions.Skip, HouseCameraActions.CeremonyMapName + "/Skip"),
                    (actions.Speed, HouseCameraActions.CeremonyMapName + "/Speed"),
                    (actions.Advance, HouseCameraActions.CeremonyMapName + "/Advance"),
                    (actions.CompetitionUp, HouseCameraActions.CompetitionMapName + "/Up"),
                    (actions.CompetitionRight, HouseCameraActions.CompetitionMapName + "/Right"),
                    (actions.CompetitionDown, HouseCameraActions.CompetitionMapName + "/Down"),
                    (actions.CompetitionLeft, HouseCameraActions.CompetitionMapName + "/Left"),
                    (actions.CompetitionHit, HouseCameraActions.CompetitionMapName + "/Hit"),
                    (actions.CompetitionHold, HouseCameraActions.CompetitionMapName + "/Hold"),
                    (actions.CompetitionPause, HouseCameraActions.CompetitionMapName + "/Pause"),
                    (actions.CompetitionBack, HouseCameraActions.CompetitionMapName + "/Back"),
                    (actions.CompetitionUndo, HouseCameraActions.CompetitionMapName + "/Undo"),
                    (actions.CompetitionFocusNext, HouseCameraActions.CompetitionMapName + "/FocusNext"),
                    (actions.CompetitionFocusPrevious, HouseCameraActions.CompetitionMapName + "/FocusPrevious"),
                    (actions.CompetitionConfirm, HouseCameraActions.CompetitionMapName + "/Confirm"),
                    (actions.ReportPageUp, HouseCameraActions.ReportMapName + "/PageUp"),
                    (actions.ReportPageDown, HouseCameraActions.ReportMapName + "/PageDown"),
                    (actions.ReportHome, HouseCameraActions.ReportMapName + "/Home"),
                    (actions.ReportEnd, HouseCameraActions.ReportMapName + "/End"),
                    (actions.ReportScroll, HouseCameraActions.ReportMapName + "/Scroll"),
                    (actions.ReportWalk, HouseCameraActions.ReportMapName + "/Walk"),
                    (actions.Reply1, HouseCameraActions.DialogueMapName + "/Reply1"),
                    (actions.Reply2, HouseCameraActions.DialogueMapName + "/Reply2"),
                    (actions.Reply3, HouseCameraActions.DialogueMapName + "/Reply3"),
                };
                foreach (var property in properties)
                {
                    Assert.That(property.Item1, Is.Not.Null, property.Item2);
                    Assert.That(property.Item1.actionMap.name + "/" + property.Item1.name, Is.EqualTo(property.Item2));
                }
                actions.Enable();
                Assert.That(actions.Pan.enabled, Is.True);
                Assert.That(actions.Skip.enabled && actions.CompetitionBack.enabled && actions.ReportScroll.enabled, Is.True,
                    "Enabling the wrapper enables every map: a card, a board and the report read it whatever is on screen.");
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

        /// <summary>
        /// The scene's rig loads the exported asset, so a binding that differs between it and the
        /// code is a control that differs between the game and every test that builds the map. Every
        /// map, action and binding, in order, and both schemes; only the ids, which the export
        /// generates, may differ.
        /// </summary>
        [Test]
        public void TheExportedAsset_IsTheCodeAsset_BindingForBinding()
        {
            var exported = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(Gamesim.Editor.HouseCameraActionsExport.AssetPath);
            Assert.That(exported, Is.Not.Null, "The scene's rig loads " + Gamesim.Editor.HouseCameraActionsExport.AssetPath + "; it must exist.");
            var built = HouseCameraActions.BuildAsset();
            try
            {
                Assert.That(Describe(exported), Is.EqualTo(Describe(built)),
                    "The export is derived from the code; re-run Gamesim/U07/Export the camera actions.");
                Assert.That(() => new HouseCameraActions(exported), Throws.Nothing, "The wrapper finds every action in the export.");
            }
            finally
            {
                Object.DestroyImmediate(built);
            }
        }

        private static List<string> Describe(InputActionAsset asset)
        {
            var lines = new List<string>();
            foreach (var map in asset.actionMaps)
            {
                lines.Add("map " + map.name);
                foreach (var action in map.actions)
                    lines.Add("  action " + action.name + " " + action.type + " " + (action.expectedControlType ?? ""));
                foreach (var binding in map.bindings)
                    lines.Add("  binding " + binding.action + " '" + binding.name + "' " + binding.path + " [" + binding.groups + "]"
                        + (binding.isComposite ? " composite" : "") + (binding.isPartOfComposite ? " part" : ""));
            }
            foreach (var scheme in asset.controlSchemes)
                lines.Add("scheme " + scheme.name + " " + scheme.bindingGroup + " "
                    + string.Join(",", scheme.deviceRequirements.Select(device => device.controlPath + (device.isOptional ? "?" : ""))));
            return lines;
        }
    }
}
