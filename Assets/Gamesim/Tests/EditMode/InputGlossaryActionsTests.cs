using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The glossary held to the actions map (PLAN A, A0): every action the house's asset carries has
    /// exactly one row, every row that names the asset names an action in it, and each row's key and
    /// pad words are what that action is bound to. The rows for the UI module name one of its own
    /// actions; the pointer and typing rows stand for the direct reads the scan allows.
    /// </summary>
    public sealed class InputGlossaryActionsTests
    {
        [Test]
        public void EveryAction_HasOneRow_AndEveryRowHasAnAction()
        {
            var asset = HouseCameraActions.BuildAsset();
            try
            {
                var missing = new List<string>();
                foreach (var map in asset.actionMaps)
                    foreach (var action in map.actions)
                    {
                        int rows = InputGlossary.Rows.Count(row => row.Map == map.name && row.Action == action.name);
                        if (rows != 1) missing.Add(map.name + "/" + action.name + " has " + rows + " glossary rows");
                    }
                Assert.That(missing, Is.Empty, string.Join("\n", missing));

                foreach (var row in InputGlossary.Rows.Where(row => !row.OutsideTheAsset))
                {
                    var map = asset.FindActionMap(row.Map);
                    Assert.That(map, Is.Not.Null, row + ": the map is not in the asset.");
                    Assert.That(map.FindAction(row.Action), Is.Not.Null, row + ": the action is not in its map.");
                }
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }

            // Not a using: DefaultInputActions.Dispose calls Object.Destroy, which Unity refuses in edit
            // mode with an error log that fails the test. The asset is destroyed the edit-mode way.
            var module = new DefaultInputActions();
            try
            {
                var ui = module.asset.FindActionMap("UI", throwIfNotFound: true);
                foreach (var row in InputGlossary.Rows.Where(row => row.Map == InputGlossary.InterfaceMap))
                    Assert.That(ui.FindAction(row.Action), Is.Not.Null, row + ": the event system's module has no such action.");
            }
            finally
            {
                Object.DestroyImmediate(module.asset);
            }
        }

        [Test]
        public void EveryRow_SaysTheKeysAndButtonsItsActionIsBoundTo()
        {
            using (var actions = new HouseCameraActions())
            {
                var wrong = new List<string>();
                foreach (var row in InputGlossary.Rows.Where(row => !row.OutsideTheAsset))
                {
                    var action = actions.Asset.FindActionMap(row.Map, throwIfNotFound: true).FindAction(row.Action, throwIfNotFound: true);
                    string keys = HouseCameraActions.BindingWords(action, HouseCameraActions.KeyboardMouseScheme);
                    string pad = HouseCameraActions.BindingWords(action, HouseCameraActions.GamepadScheme);
                    if (keys != row.Keyboard) wrong.Add(row + " says keys '" + row.Keyboard + "', the map binds '" + keys + "'");
                    if (pad != row.Pad) wrong.Add(row + " says pad '" + row.Pad + "', the map binds '" + pad + "'");
                }
                Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
            }
        }
    }
}
