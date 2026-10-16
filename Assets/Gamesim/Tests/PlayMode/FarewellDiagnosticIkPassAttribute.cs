using System;
using System.Collections;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Runs one farewell IK probe with an IK pass on the humanoid controller's base layer. The shipping
    /// controller has none (m_IKPass 0, pinned by GroundedFarewellAnimationTests): the farewell plants its
    /// feet by holding its stance, and an IK pass on every humanoid every frame is a cost nothing needs. The
    /// passive, calibrated and dynamic IK probes read and set foot goals in OnAnimatorIK, which Unity calls
    /// only on a layer with the pass, so they need it for as long as they run.
    ///
    /// A copy of the controller cannot stand in for it: the farewell plays only on the Resources handle's
    /// own base controller or an override of it (CharacterPresentation.HasFarewellTake), and the probes
    /// assert that lineage. So this outer test action owns the pass for one test: before the fixture's
    /// set-up builds a body it turns the pass on in the controller object in memory, and after the
    /// fixture's tear-down has destroyed the bodies it turns it off again, puts the object's dirty flag back
    /// as it found it, and asserts the asset file's bytes are what they were. Nothing is written to disk,
    /// and no shipping test runs with the pass.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FarewellDiagnosticIkPassAttribute : NUnitAttribute, IOuterUnityTestAction
    {
        public const string ControllerPath = "Assets/Gamesim/Art/Characters/GamesimHumanoid.controller";
        public const string HandlePath = "Animation/GamesimHumanoid";

#if UNITY_EDITOR
        private UnityEditor.Animations.AnimatorController controller;
        private string fileBefore;
        private bool wasDirty;
#endif

        public IEnumerator BeforeTest(ITest test)
        {
#if UNITY_EDITOR
            controller = null;
            var shipping = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
            Assert.That(shipping, Is.Not.Null, "IK_PASS_CONTROLLER " + ControllerPath);
            var handle = Resources.Load<AnimatorOverrideController>(HandlePath);
            Assert.That(handle != null && handle.runtimeAnimatorController == shipping, Is.True,
                "IK_PASS_LINEAGE: the bodies play the Resources handle's base, and that is the controller given the pass.");
            var layers = shipping.layers;
            Assert.That(layers.Length, Is.EqualTo(1), "IK_PASS_SINGLE_LAYER");
            Assert.That(layers[0].iKPass, Is.False,
                "IK_PASS_SHIPPING_OFF: the shipping controller has no IK pass (or an earlier probe left this one on).");
            fileBefore = FileHash();
            wasDirty = UnityEditor.EditorUtility.IsDirty(shipping);
            controller = shipping; // From here on AfterTest restores, whatever this test does.
            layers[0].iKPass = true;
            controller.layers = layers;
            Assert.That(controller.layers[0].iKPass, Is.True, "IK_PASS_ON");
            Debug.Log("[Gamesim] FAREWELL-DIAGNOSTIC-IK-PASS on test=" + test.Name + " controller=" + controller.name
                + " persisted=false");
#else
            Assert.Fail("The farewell IK probes turn the IK pass on in the editor; a player cannot.");
#endif
            yield break;
        }

        public IEnumerator AfterTest(ITest test)
        {
#if UNITY_EDITOR
            if (controller == null) yield break; // BeforeTest refused before it turned anything on.
            var restored = controller; controller = null;
            var layers = restored.layers;
            layers[0].iKPass = false;
            restored.layers = layers;
            if (!wasDirty) UnityEditor.EditorUtility.ClearDirty(restored);
            Assert.That(restored.layers[0].iKPass, Is.False, "IK_PASS_RESTORED_OFF");
            Assert.That(UnityEditor.EditorUtility.IsDirty(restored), Is.EqualTo(wasDirty), "IK_PASS_DIRTY_FLAG_RESTORED");
            Assert.That(FileHash(), Is.EqualTo(fileBefore), "IK_PASS_FILE_UNCHANGED: the probe wrote nothing to " + ControllerPath);
            Debug.Log("[Gamesim] FAREWELL-DIAGNOSTIC-IK-PASS off test=" + test.Name + " fileUnchanged=true");
#endif
            yield break;
        }

#if UNITY_EDITOR
        private static string FileHash()
        {
            var bytes = System.IO.File.ReadAllBytes(System.IO.Path.GetFullPath(ControllerPath));
            using (var algorithm = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "");
        }
#endif
    }
}
