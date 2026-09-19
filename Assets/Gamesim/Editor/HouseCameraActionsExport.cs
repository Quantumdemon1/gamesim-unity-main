using System.IO;
using Gamesim.House;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Writes the camera's action map, as the code builds it, to an Input Actions asset that can be
    /// rebound in the editor and assigned on the rig. The code stays the truth: re-running this
    /// after a change to <see cref="HouseCameraActions.BuildAsset"/> overwrites the file.
    /// </summary>
    public static class HouseCameraActionsExport
    {
        public const string AssetPath = "Assets/Gamesim/Input/HouseCamera.inputactions";

        [MenuItem("Gamesim/U07/Export the camera actions")]
        public static void Export()
        {
            var asset = HouseCameraActions.BuildAsset();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                File.WriteAllText(AssetPath, asset.ToJson());
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Gamesim] camera actions exported to " + AssetPath);
        }
    }
}
