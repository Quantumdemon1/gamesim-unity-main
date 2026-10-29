using System;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Optional authored geometry for the existing named instrument parts. Each mesh is authored
    /// in its reference dimensions, then normalized offline to the existing centred unit box.
    /// The apparatus retains its transforms, materials, readouts, fitting and mesh registration.
    /// These are borrowed asset references: an apparatus must never destroy or modify them.
    /// </summary>
    public sealed class CompetitionApparatusMeshKit : ScriptableObject
    {
        public const string ResourceName = "Gamesim/Competition/ApparatusMeshKit";

        // Serialized keys: append new kinds instead of reordering existing values.
        public enum PartKind
        {
            StanceRail = 0, ConsoleLeg = 1, ConsoleHousing = 2, RecessedDisplay = 3,
            AwardHeader = 4, MemoryTile = 5, SignalTarget = 6, ResponsePad = 7,
            RigUpright = 8, RigBrace = 9, GripHandle = 10, GripTrack = 11,
            GripRemaining = 12, CounterweightGuide = 13, Counterweight = 14,
            GripTick = 15, TrayPedestal = 16, TrayBed = 17, TrayBackRail = 18,
            TraySideRail = 19, Die = 20, LetterTile = 21, ProgressBacking = 22
        }

        [Serializable]
        public sealed class Entry
        {
            public CompetitionApparatus.Family family;
            public PartKind partKind;
            public Vector3 referenceDimensions;
            public Mesh mesh;
        }

        public Entry[] entries = Array.Empty<Entry>();

        /// <summary>
        /// Resolve only an unambiguous, compatible part. No fit-to-bounds scaling is performed:
        /// a part authored for a different aspect ratio falls back to the original block.
        /// Imported meshes need not retain a CPU-readable vertex copy in the shipped game.
        /// </summary>
        public bool TryGetMesh(CompetitionApparatus.Family family, PartKind partKind,
            Vector3 referenceDimensions, out Mesh mesh)
        {
            mesh = null;
            if (entries == null || !PositiveFinite(referenceDimensions)) return false;
            Entry found = null;
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.family != family || entry.partKind != partKind) continue;
                if (found != null) return false;
                found = entry;
            }
            if (found == null || !PositiveFinite(found.referenceDimensions)
                || !Matches(found.referenceDimensions, referenceDimensions) || found.mesh == null
                || found.mesh.vertexCount < 3 || found.mesh.subMeshCount != 1
                || found.mesh.GetTopology(0) != MeshTopology.Triangles || found.mesh.GetIndexCount(0) < 3) return false;

            Bounds bounds = found.mesh.bounds;
            if (!Finite(bounds.center) || !PositiveFinite(bounds.size)
                || !Matches(bounds.center, Vector3.zero, .0001f)
                || !Matches(bounds.size, Vector3.one, .0001f)) return false;
            Vector3 min = bounds.min, max = bounds.max;
            const float limit = .5001f;
            if (min.x < -limit || min.y < -limit || min.z < -limit
                || max.x > limit || max.y > limit || max.z > limit) return false;
            mesh = found.mesh;
            return true;
        }

        private static bool Matches(Vector3 a, Vector3 b, float tolerance = .00001f)
            => Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance
                && Mathf.Abs(a.z - b.z) <= tolerance;

        private static bool PositiveFinite(Vector3 value)
            => Finite(value) && value.x > 0 && value.y > 0 && value.z > 0;

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
