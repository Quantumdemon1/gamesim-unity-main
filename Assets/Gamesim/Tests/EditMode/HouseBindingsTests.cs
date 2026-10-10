using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Rebinding on the actions themselves (PLAN A, A5): every target a player may rebind finds its
    /// binding in the game's actions asset - an action's own, or one part of its keyboard composite,
    /// whose shipped key is the one the words on screen were written with - and a choice lands on
    /// that binding and no other: the pan's up on WASD's W and not the arrows' up, the zoom's out on
    /// the keyboard's - and not the pad's trigger.
    /// </summary>
    public sealed class HouseBindingsTests
    {
        /// <summary>A store holding the one kept text, as a playtest root's file would.</summary>
        private sealed class KeptBindings : IPreferenceStore
        {
            private readonly string json;
            public KeptBindings(string json) { this.json = json; }
            public PreferenceRoute Route => PreferenceRoute.RootFile;
            public bool HasKey(string key) => key == KeyBindings.PreferenceKey;
            public int GetInt(string key, int fallback) => fallback;
            public string GetString(string key, string fallback) => key == KeyBindings.PreferenceKey ? json : fallback;
            public void SetInt(string key, int value) { }
            public void SetString(string key, string value) { }
            public void Save() { }
        }

        [TearDown]
        public void EveryBindingAsShipped() => HouseBindings.Load(DefaultPreferences.Instance);

        [Test]
        public void EveryTarget_FindsItsBinding_AndACameraKeysShippedKeyIsTheAssets()
        {
            using (var actions = new HouseCameraActions())
            {
                foreach (var target in KeyBindings.Targets)
                {
                    var action = actions.Asset.FindAction(target.Path);
                    Assert.That(action, Is.Not.Null, target.Path + " is in the asset.");
                    foreach (var scheme in new[] { KeyBindings.KeyboardScheme, KeyBindings.GamepadScheme })
                    {
                        int index = HouseBindings.BindingIndex(action, scheme, target.Part);
                        if (!target.Takes(scheme)) continue;
                        Assert.That(index, Is.GreaterThanOrEqualTo(0), target.Name + " has a binding on " + scheme + ".");
                        var binding = action.bindings[index];
                        Assert.That(binding.isPartOfComposite, Is.EqualTo(target.Part != null), target.Name + ": " + (target.Part != null ? "a composite's part" : "the action's own binding"));
                        if (target.Part != null)
                        {
                            Assert.That(binding.name, Is.EqualTo(target.Part).IgnoreCase, target.Name + "'s part.");
                            Assert.That(binding.path, Is.EqualTo(KeyBindings.ShippedPath(target)),
                                target.Name + " answers to the key the words on screen were written with.");
                        }
                    }
                    if (!target.OnPad)
                        Assert.That(target.Takes(KeyBindings.GamepadScheme), Is.False, target.Name + " is the keyboard's alone: the pad uses its sticks and triggers.");
                }
            }
        }

        [Test]
        public void AChoiceLandsOnItsOwnBinding_AndTheShippedOnesStay()
        {
            HouseBindings.Load(new KeptBindings(
                "{\"Camera/Pan#Up@Keyboard&Mouse\":\"<Keyboard>/i\",\"Camera/ZoomRate#Negative@Keyboard&Mouse\":\"<Keyboard>/z\","
                + "\"Camera/OrbitRate#Right@Keyboard&Mouse\":\"<Keyboard>/v\",\"Shortcuts/Notebook@Keyboard&Mouse\":\"<Keyboard>/k\"}"));
            Assert.That(HouseBindings.Current.Count, Is.EqualTo(4), "Every choice in the kept text is one a player may make.");
            using (var actions = new HouseCameraActions())
            {
                HouseBindings.Apply(actions);
                string[] PathsOf(InputAction action) => action.bindings.Select(binding => binding.effectivePath).ToArray();
                Assert.That(PathsOf(actions.Pan), Does.Contain("<Keyboard>/i").And.Not.Contain("<Keyboard>/w"), "The pan's up is I,");
                Assert.That(PathsOf(actions.Pan), Does.Contain("<Keyboard>/upArrow").And.Contain("<Keyboard>/a"), "and the arrows and the rest of WASD are as shipped.");
                Assert.That(PathsOf(actions.ZoomRate), Does.Contain("<Keyboard>/z").And.Not.Contain("<Keyboard>/minus"), "The zoom out is Z,");
                Assert.That(PathsOf(actions.ZoomRate), Does.Contain("<Gamepad>/leftTrigger"), "and the pad's trigger is untouched.");
                Assert.That(PathsOf(actions.OrbitRate), Does.Contain("<Keyboard>/v").And.Contain("<Keyboard>/q").And.Not.Contain("<Keyboard>/c"), "The orbit's right is V.");
                Assert.That(PathsOf(actions.Notebook), Does.Contain("<Keyboard>/k").And.Contain("<Gamepad>/select"), "An action's own binding as before.");

                var live = HouseBindings.LiveBindings(actions);
                Assert.That(live.Any(binding => binding.Action == "Pan" && binding.Part == "Up" && binding.Path == "<Keyboard>/i"), Is.True,
                    "The conflict check reads the part by its name.");
                Assert.That(KeyBindings.Check(live, KeyBindings.FindTarget("Camera", "Pan", "Left"), KeyBindings.KeyboardScheme, "<Keyboard>/i"),
                    Is.EqualTo("I is already Pan up's. Pan left keeps its key."), "One part of the pan is refused another's key.");
                Assert.That(KeyBindings.Check(live, KeyBindings.FindTarget("Camera", "Pan", "Up"), KeyBindings.KeyboardScheme, "<Keyboard>/leftArrow"),
                    Is.EqualTo("Left is already Pan left's. Pan up keeps its key."), "and the arrows' parts are bindings too.");

                // Back as shipped: every override goes, the part's included.
                HouseBindings.Load(DefaultPreferences.Instance);
                HouseBindings.Apply(actions);
                Assert.That(PathsOf(actions.Pan), Does.Contain("<Keyboard>/w").And.Not.Contain("<Keyboard>/i"));
                Assert.That(PathsOf(actions.ZoomRate), Does.Contain("<Keyboard>/minus"));
            }
        }
    }
}
