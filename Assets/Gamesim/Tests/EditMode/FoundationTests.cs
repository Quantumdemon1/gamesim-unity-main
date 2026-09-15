using System.Linq;
using Gamesim.Bootstrap;
using Gamesim.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class FoundationTests
    {
        [Test]
        public void FoundationIdentifiers_AreStable()
        {
            Assert.That(FoundationInfo.ProductName, Is.EqualTo("Gamesim"));
            Assert.That(FoundationInfo.FoundationVersion, Is.EqualTo("U01"));
        }

        [Test]
        public void BootstrapScene_IsFirstEnabledBuildScene()
        {
            Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(FoundationInfo.BootstrapScenePath));
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
        }

        [Test]
        public void BootstrapScene_ContainsRequiredEntryPoint()
        {
            var bootstrapScene = EditorSceneManager.OpenPreviewScene(FoundationInfo.BootstrapScenePath);

            try
            {
                var roots = bootstrapScene.GetRootGameObjects();
                var bootstraps = roots.SelectMany(root => root.GetComponentsInChildren<GamesimBootstrap>(true)).ToArray();
                Assert.That(bootstraps, Has.Length.EqualTo(1));
                Assert.That(bootstraps[0].FoundationVersion, Is.EqualTo("U01"));
                var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true));
                Assert.That(cameras.Any(camera => camera.CompareTag("MainCamera")), Is.True);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(bootstrapScene);
            }
        }
    }
}
