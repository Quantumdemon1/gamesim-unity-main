using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Turns each authored body under <c>Art/Authored/Characters/Generic/</c> into the prefab the
    /// runtime loads for it, the way the six shipped bodies are set up: the model's own hierarchy
    /// at the root, an <see cref="Animator"/> on it with <c>GamesimCharacter.controller</c> and the
    /// model's Generic avatar, no root motion, culled to transform updates.
    ///
    /// <para>The file name is the template id: <c>bb_char_dan_gheesling.fbx</c> becomes
    /// <c>Resources/GamesimCharacters/dan-gheesling.prefab</c>, because the export checklist allows
    /// no hyphen in a bb_ name and a template id has one. Re-runnable: an existing prefab is
    /// replaced, never duplicated.</para>
    /// </summary>
    public static class AuthoredCharacterPrefabs
    {
        public const string Folder = AuthoredAssetImporter.Root + "Characters/Generic/";
        public const string PrefabFolder = "Assets/Gamesim/Resources/GamesimCharacters/";
        public const string Prefix = "bb_char_";

        /// <summary>The appearance id a body file stands for, or null when the file is not a body.</summary>
        public static string AppearanceIdOf(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var id = fileName.Substring(Prefix.Length).Replace('_', '-');
            return id.Length == 0 ? null : id;
        }

        [MenuItem("Gamesim/U07/Build the authored character prefabs")]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AuthoredClipWiring.Controller);
            if (controller == null) throw new InvalidOperationException("No controller at " + AuthoredClipWiring.Controller);
            var models = AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (models.Length == 0) throw new InvalidOperationException("No authored bodies under " + Folder);
            foreach (var path in models)
                Debug.Log("[Gamesim] character prefab · " + Build(path, controller));
            AssetDatabase.SaveAssets();
        }

        public static string Build(string modelPath, RuntimeAnimatorController controller)
        {
            var id = AppearanceIdOf(Path.GetFileNameWithoutExtension(modelPath));
            if (id == null) throw new InvalidOperationException(modelPath + " is not a bb_char_ body.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException("No model at " + modelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null) throw new InvalidOperationException(modelPath + " imported without an avatar; is it under Characters/Generic?");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = id;
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                Directory.CreateDirectory(PrefabFolder);
                var prefabPath = PrefabFolder + id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("The prefab could not be saved at " + prefabPath);
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
