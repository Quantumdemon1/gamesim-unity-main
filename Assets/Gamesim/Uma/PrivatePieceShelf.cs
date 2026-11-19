using System;
using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gamesim.Uma
{
    /// <summary>
    /// Wardrobe pieces a copy of the game holds that source control never sees: the slots, overlays and
    /// wardrobe recipes in Resources/Gamesim/Private (and their catalog entries in
    /// Resources/Gamesim/CharacterCatalog/Private, which the catalog's own Resources.LoadAll already reads).
    /// Both folders are ignored by git, and UMA's project index (a tracked asset) never lists them, so they are
    /// put on the index here, at run time, before the catalog reads a recipe.
    ///
    /// <para>Each asset is registered once per index and per play session, with UMA's run-time entry point
    /// (<c>ProcessNewItem</c>, which never marks the index dirty). An asset named like one the index already
    /// holds is left out with a warning: it would otherwise take that name's place for every houseguest.
    /// With both folders empty, or absent, nothing changes. Outside play mode nothing is registered, so an
    /// editor-time authoring pass never writes a private piece into the tracked index; and in the editor every
    /// piece registered here is taken off the index again when play stops, before anything could save it.</para>
    /// </summary>
    public static class PrivatePieceShelf
    {
        /// <summary>The Resources path of the shelf: Assets/Gamesim/Uma/Resources/Gamesim/Private.</summary>
        public const string Folder = "Gamesim/Private";

        private sealed class Shelved
        {
            public Type Type;
            public string Name;
            public Object Asset;
        }

        private static readonly List<Shelved> shelved = new List<Shelved>();
        private static UMAAssetIndexer shelvedIn;
        private static bool loaded;

        /// <summary>How many assets this play session put on UMA's index (the shelf's and any registered directly).</summary>
        public static int Count => shelved.Count;

        // Domain reload is off when play starts in the editor: last session's registrations are withdrawn
        // (they should already be, when play stopped) and the shelf is read again on first use.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
#if UNITY_EDITOR
            WithdrawAll();
#endif
            shelved.Clear();
            shelvedIn = null;
            loaded = false;
        }

        // As play starts, and only when the shelf holds something: everything that reads UMA's index in the session
        // (a catalog, a test that notes the index's length first) then finds the shelf already on it. An empty shelf
        // never touches the index here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAtStart()
        {
            if (Resources.LoadAll<UMAWardrobeRecipe>(Folder).Length == 0 && Resources.LoadAll<SlotDataAsset>(Folder).Length == 0
                && Resources.LoadAll<OverlayDataAsset>(Folder).Length == 0) return;
            Ensure(UMAAssetIndexer.Instance);
        }

        /// <summary>
        /// Puts the shelf's slots, overlays and wardrobe recipes on <paramref name="index"/>, in that order (a
        /// recipe names its slots and overlays), the first time it is asked for that index in a play session.
        /// Returns how many were added: 0 when the shelf is empty, already done, or not in play mode.
        /// </summary>
        public static int Ensure(UMAAssetIndexer index)
        {
            if (index == null || !Application.isPlaying) return 0;
            if (loaded && ReferenceEquals(index, shelvedIn)) return 0;
            loaded = true;
            var found = new List<Object>();
            found.AddRange(Resources.LoadAll<SlotDataAsset>(Folder));
            found.AddRange(Resources.LoadAll<OverlayDataAsset>(Folder));
            found.AddRange(Resources.LoadAll<UMAWardrobeRecipe>(Folder));
            if (found.Count == 0) { shelvedIn = index; return 0; }
            return Register(index, found).Count;
        }

        /// <summary>
        /// Registers each of <paramref name="pieces"/> that UMA indexes and that the index does not hold yet, and
        /// returns those it added. An asset already on the index under its own name is skipped silently; one whose
        /// name the index holds for another asset is skipped with a warning. Only in play mode.
        /// </summary>
        public static IReadOnlyList<Object> Register(UMAAssetIndexer index, IEnumerable<Object> pieces)
        {
            var added = new List<Object>();
            if (index == null || pieces == null || !Application.isPlaying) return added;
            if (!ReferenceEquals(index, shelvedIn))
            {
#if UNITY_EDITOR
                WithdrawAll();
#endif
                shelved.Clear();
                shelvedIn = index;
            }
            foreach (var piece in pieces)
            {
                if (piece == null || !index.IsIndexedType(piece.GetType())) continue;
                string name = AssetItem.GetEvilName(piece);
                if (index.GetAssetDictionary(piece.GetType()).TryGetValue(name, out var known) && known != null)
                {
                    if (!ReferenceEquals(known.Item, piece))
                        Debug.LogWarning("[Gamesim.Uma] A private piece is named like an asset UMA already holds and is left out: " + name);
                    continue;
                }
                index.ProcessNewItem(piece, false, true);
                shelved.Add(new Shelved { Type = piece.GetType(), Name = name, Asset = piece });
                added.Add(piece);
            }
#if UNITY_EDITOR
            if (added.Count > 0) Watch();
#endif
            return added;
        }

#if UNITY_EDITOR
        private static bool watching;

        private static void Watch()
        {
            if (watching) return;
            watching = true;
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                if (change == UnityEditor.PlayModeStateChange.EnteredEditMode) WithdrawAll();
            };
        }

        /// <summary>
        /// Takes <paramref name="pieces"/> off the index this shelf registered them on, where they are still the
        /// asset under their name, without marking the index dirty; the index's length is restored when nothing
        /// was added after them. Editor only (UMA's removal is): a built game never saves its index.
        /// </summary>
        public static void Withdraw(IEnumerable<Object> pieces)
        {
            if (pieces == null || shelvedIn == null) return;
            var taking = new HashSet<Object>(pieces);
            var leaving = shelved.FindAll(item => taking.Contains(item.Asset));
            Remove(leaving);
        }

        private static void WithdrawAll() => Remove(new List<Shelved>(shelved));

        private static void Remove(List<Shelved> leaving)
        {
            var index = shelvedIn;
            if (index == null || leaving.Count == 0) return;
            foreach (var item in leaving)
            {
                shelved.Remove(item);
                if (item.Asset == null || !index.IsIndexedType(item.Type)) continue;
                if (!index.GetAssetDictionary(item.Type).TryGetValue(item.Name, out var known) || known == null) continue;
                if (!ReferenceEquals(known._SerializedItem, item.Asset)) continue;
                index.RemoveAsset(item.Type, item.Name, refresh: false);
            }
            var list = index.SerializedItems;
            while (list.Count > 0 && list[list.Count - 1] == null) list.RemoveAt(list.Count - 1);
        }
#endif
    }
}
