using System;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The opening's front door, authored (the art brief's A4; decision D9, an original scripted
    /// door): the frame, a leaf and its handle from ArtSource/setpieces/bb_set_frontdoor_*.py. The
    /// door set builds its jambs, crown and lintel from <see cref="frame"/> and each leaf from
    /// <see cref="leaf"/> and <see cref="handle"/>, wearing its own materials, one to a submesh by the
    /// slot names here; and the primitives it always built when the kit is missing.
    ///
    /// <para>Built by <c>Gamesim.Editor.OpeningDoorKitAssets</c>, never by hand.</para>
    /// </summary>
    public sealed class OpeningDoorKit : ScriptableObject
    {
        public const string ResourceName = "Sets/OpeningDoorKit";

        public Mesh frame, leaf, handle;
        /// <summary>The authored material each submesh was exported with, in submesh order.</summary>
        public string[] frameSlots = Array.Empty<string>(), leafSlots = Array.Empty<string>(), handleSlots = Array.Empty<string>();

        private static OpeningDoorKit current;

        /// <summary>The kit, or null on a clone without it - the door set then builds its primitives.</summary>
        public static OpeningDoorKit Current
        {
            get
            {
                if (current == null) current = Resources.Load<OpeningDoorKit>(ResourceName);
                return current;
            }
        }

        /// <summary>The material for each of a mesh's submeshes, by the slot's authored name: <paramref name="pick"/> chooses.</summary>
        public static Material[] Wear(string[] slots, Mesh mesh, Func<string, Material> pick)
        {
            int count = mesh != null ? mesh.subMeshCount : 0;
            var materials = new Material[count];
            for (int i = 0; i < count; i++)
                materials[i] = pick(slots != null && i < slots.Length ? slots[i] ?? "" : "");
            return materials;
        }
    }
}
