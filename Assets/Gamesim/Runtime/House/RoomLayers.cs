using System;
using System.Collections.Generic;
using System.Text;

namespace Gamesim.House
{
    /// <summary>
    /// The house's eight room layers (ROOM-SCOPE-PLAN §3.2 b): 20 Living, 21 Kitchen, 22 Bedroom,
    /// 23 Private, 24 Yard, 25 HoH, 26 Nomination, 27 Games, named in ProjectSettings/TagManager.asset.
    ///
    /// <para>A room's furniture and dressing go on its layer so a camera can leave the room out of its
    /// drawing with one bit of its culling mask. Only collider-free objects ever move there, so no
    /// physics query can hit anything on a room layer: a layer is physics as well as drawing, and
    /// <see cref="HouseLayers"/>' Pick and Sight masks do include bits 20 to 27. A body's renderer
    /// objects (RS4) must stay collider-free for the same reason. Layers 30 and 31 stay the tests' own
    /// (a capture guard and the garment fit's isolation layer), so the scope writes bits 20 to 27 and
    /// nothing else.</para>
    ///
    /// <para>RS1 names the layers and puts nothing on them; RS2a applies the classification. RS4 puts a
    /// hidden body's renderer objects on its walker room's layer and writes the main camera's and the
    /// live feed's masks (<see cref="ScopeMask"/>, <see cref="OwnRoomMask"/>).</para>
    /// </summary>
    public static class RoomLayers
    {
        public const int First = 20;
        public const int Count = 8;
        public const int Living = 20, Kitchen = 21, Bedroom = 22, Private = 23, Yard = 24, HoH = 25, Nomination = 26, Games = 27;

        /// <summary>Bits 20 to 27: the only bits of a camera's culling mask the scope writes.</summary>
        public const int Mask = ((1 << Count) - 1) << First;

        // Private, so no caller can rewrite an entry and leave IndexOf, LayerOf, RoomOf and the
        // TagManager names disagreeing; the lists below read them.
        private static readonly string[] roomIds = { "Living", "Kitchen", "Bedroom", "Private", "Yard", "HoH", "Nomination", "Games" };
        private static readonly string[] layerNames =
        {
            "Room Living", "Room Kitchen", "Room Bedroom", "Room Private", "Room Yard", "Room HoH", "Room Nomination", "Room Games",
        };

        /// <summary>The room ids by index (layer minus <see cref="First"/>): the room query's eight, in its order.</summary>
        public static readonly IReadOnlyList<string> RoomIds = Array.AsReadOnly(roomIds);

        /// <summary>Each room layer's name in ProjectSettings/TagManager.asset, by index.</summary>
        public static readonly IReadOnlyList<string> LayerNames = Array.AsReadOnly(layerNames);

        /// <summary>The room's index, 0 to 7, or -1 for anything that is not one of the eight.</summary>
        public static int IndexOf(string roomId)
        {
            if (roomId == null) return -1;
            for (int i = 0; i < Count; i++)
                if (string.Equals(roomIds[i], roomId, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>The room's layer, or -1.</summary>
        public static int LayerOf(string roomId)
        {
            int index = IndexOf(roomId);
            return index < 0 ? -1 : First + index;
        }

        /// <summary>The room a layer stands for, or null for a layer that is not a room's.</summary>
        public static string RoomOf(int layer) => IsRoomLayer(layer) ? roomIds[layer - First] : null;

        public static bool IsRoomLayer(int layer) => layer >= First && layer < First + Count;

        /// <summary>
        /// Whether a camera with this culling mask draws a renderer on this layer, as the room scope reads
        /// it (§3.2 f, D13): not on a room layer the mask leaves out; on every other layer yes, whatever the
        /// mask says, because the scope hides things only by moving them to a room layer. The pick filter
        /// (<see cref="HouseScopePick"/>) asks it of every renderer a hit's owner has drawing.
        /// </summary>
        public static bool Draws(int mask, int layer) => !IsRoomLayer(layer) || (mask & (1 << layer)) != 0;

        /// <summary>
        /// Whether two culling masks draw the same rooms: bits 20 to 27 alike, whatever the rest (a
        /// capture's guard bit, the tests' layer 30). The hover picks again when they differ (RS2b), since
        /// what the camera draws is what the pointer can point at.
        /// </summary>
        public static bool SameRooms(int mask, int other) => ((mask ^ other) & Mask) == 0;

        /// <summary>
        /// Whether a culling mask draws every room: all of bits 20 to 27, whatever the rest. The room scope
        /// hides nothing then - a renderer on any room layer is drawn (<see cref="Draws"/>) - so a still
        /// pointer over a still camera is over what it was, and the hover need not look again (RS2b). With
        /// a room left out, what the camera draws can change with no change of mask (a piece of dressing
        /// gone to the shell, a body hidden in a doorway), so the hover looks again every frame.
        /// </summary>
        public static bool DrawsEveryRoom(int mask) => (mask & Mask) == Mask;

        /// <summary>What <see cref="ScopeMask"/> keeps while no scope has a camera's room bits: nothing to write back.</summary>
        public const int NothingKept = -1;

        /// <summary>
        /// The main camera's culling mask under a view (§3.2 c; RS4, which takes the mask write by D16):
        /// the plan's <c>(mask &amp; ~RoomBits) | ScopeBits</c>, with the scope's bits only among the room
        /// bits the camera had when the scope began. <paramref name="kept"/> records those on the first
        /// call that scopes and carries them between calls; the whole house, or no room, writes them
        /// back once and lets them go (<see cref="NothingKept"/>). Bits 20 to 27 are written whole from
        /// the view on every call, so a stale mask put back over the scope's - a capture's save and
        /// restore across a move - is the view's again at the next call; a room bit something else
        /// cleared before the scope began stays clear throughout and after. Nothing outside 20 to 27 is
        /// ever touched (the capture guard on 31, the tests' 30).
        /// </summary>
        public static int ScopeMask(int mask, RoomSet viewed, ref int kept)
        {
            if (viewed.IsWholeHouse || viewed.IsEmpty)
            {
                if (kept == NothingKept) return mask;
                mask = (mask & ~Mask) | (kept & Mask);
                kept = NothingKept;
                return mask;
            }
            if (kept == NothingKept) kept = mask & Mask;
            return (mask & ~Mask) | (viewed.CullingBits & kept);
        }

        /// <summary>The room bits a scope that keeps <paramref name="kept"/> has out of the mask under a view: none for the whole house or nothing kept.</summary>
        public static int TakenOut(int kept, RoomSet viewed)
            => kept == NothingKept || viewed.IsWholeHouse || viewed.IsEmpty ? 0 : kept & Mask & ~viewed.CullingBits;

        /// <summary>
        /// The live feed camera's mask (§3.2 c, D10): the shell and its own room's layer, every other
        /// room's left out; every room for a feed that names none. Only bits 20 to 27 change.
        /// </summary>
        public static int OwnRoomMask(int mask, RoomSet own) => own.IsEmpty ? mask | Mask : (mask & ~Mask) | own.CullingBits;
    }

    /// <summary>
    /// A set of the house's rooms, one bit each in <see cref="RoomLayers"/> order, so a set is also
    /// the culling bits that draw it (<see cref="CullingBits"/>). What the camera rig reports a move
    /// is about, and what the director's scope says is drawn. All eight is the whole house.
    /// </summary>
    public readonly struct RoomSet : IEquatable<RoomSet>
    {
        private const int AllBits = (1 << RoomLayers.Count) - 1;

        /// <summary>Bit i is room i of <see cref="RoomLayers.RoomIds"/>.</summary>
        public readonly int Bits;

        private RoomSet(int bits) { Bits = bits & AllBits; }

        public static RoomSet None => default;
        public static RoomSet WholeHouse => new RoomSet(AllBits);
        public static RoomSet FromBits(int bits) => new RoomSet(bits);
        /// <summary>The room at <paramref name="index"/>, or none for an index that is not a room's.</summary>
        public static RoomSet Of(int index) => index >= 0 && index < RoomLayers.Count ? new RoomSet(1 << index) : default;
        public static RoomSet Of(string roomId) => Of(RoomLayers.IndexOf(roomId));
        public static RoomSet Of(string first, string second) => Of(first).With(Of(second));

        public bool IsEmpty => Bits == 0;
        public bool IsWholeHouse => Bits == AllBits;

        public int Count
        {
            get
            {
                int count = 0;
                for (int bits = Bits; bits != 0; bits &= bits - 1) count++;
                return count;
            }
        }

        public bool Contains(int index) => index >= 0 && index < RoomLayers.Count && (Bits & (1 << index)) != 0;
        public bool Contains(string roomId) => Contains(RoomLayers.IndexOf(roomId));
        public RoomSet With(RoomSet other) => new RoomSet(Bits | other.Bits);

        /// <summary>The rooms' bits in a camera's culling mask: bits 20 to 27 only.</summary>
        public int CullingBits => Bits << RoomLayers.First;

        /// <summary>The one room's id when the set holds exactly one, otherwise null.</summary>
        public string Single
        {
            get
            {
                if (Bits == 0 || (Bits & (Bits - 1)) != 0) return null;
                for (int i = 0; i < RoomLayers.Count; i++)
                    if (Bits == 1 << i) return RoomLayers.RoomIds[i];
                return null;
            }
        }

        public bool Equals(RoomSet other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is RoomSet other && Equals(other);
        public override int GetHashCode() => Bits;
        public static bool operator ==(RoomSet a, RoomSet b) => a.Bits == b.Bits;
        public static bool operator !=(RoomSet a, RoomSet b) => a.Bits != b.Bits;

        /// <summary>For messages: "the whole house", "no room", or the rooms' ids.</summary>
        public override string ToString()
        {
            if (IsWholeHouse) return "the whole house";
            if (IsEmpty) return "no room";
            var text = new StringBuilder();
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                if (!Contains(i)) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(RoomLayers.RoomIds[i]);
            }
            return text.ToString();
        }
    }
}
