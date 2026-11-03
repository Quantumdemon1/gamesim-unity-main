using System;
using UnityEditor;

namespace Gamesim.Editor
{
    /// <summary>
    /// Fixed import settings for the competition kit's 39 parts under <c>Art/CompetitionKit</c>
    /// (<c>ArtSource/competition-apparatus/build_competition_art.py</c>).
    ///
    /// <para>The parts are Blender-authored like everything under <c>Art/Authored</c> and export the same
    /// way (metres, -Z forward, Y up, baked), so the geometry settings are the props' own: no import
    /// scale, the axis conversion baked once here, not readable, no collider. They are not props,
    /// though. Each is a centred unit block whose vertices a named instrument part borrows and scales
    /// (<c>CompetitionApparatusMeshKit</c>), so it stands on nothing, and keeping it out of
    /// <c>Art/Authored</c> keeps the props' floor contract (<c>AuthoredAssetImportTests</c>) free of an
    /// exception. Two settings differ because of what a part is: the apparatus is built at run time
    /// and never lightmapped, so no second UV set is generated; and the apparatus owns every
    /// material it draws, so a part imports none.</para>
    /// </summary>
    public sealed class CompetitionApparatusMeshKitImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Gamesim/Art/CompetitionKit";

        public override uint GetVersion() => 1;

        public static bool IsKitPart(string path) => path != null
            && path.StartsWith(Folder + "/", StringComparison.Ordinal)
            && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsKitPart(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.generateSecondaryUV = false;
        }
    }
}
