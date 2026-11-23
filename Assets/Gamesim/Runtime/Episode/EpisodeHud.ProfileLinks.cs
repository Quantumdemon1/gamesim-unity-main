using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// A houseguest's name on a reading page opens their profile (menu slice 3, the lead's ruling
    /// N6 ii): on the notes, Deals &amp; promises, the alliances and the relationship web, a name drawn
    /// as a row or a control is one press from everything the game shows about that person. The
    /// profile is a page reached from that page, so its way back names it, and Escape and B take
    /// it there. A link is underlined as well as tinted, so it never reads as a link by colour alone,
    /// and the keyboard's or the pad's focus on it lights a raised backing behind it.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The name of a name drawn as a link, then the houseguest's name.</summary>
        public const string ProfileLinkPrefix = "Profile link · ";

        /// <summary>The backing a link lights while it has the focus or the pointer.</summary>
        public const string LinkFocusName = "Link focus";

        /// <summary>
        /// Makes a name already drawn on a reading page a link to that houseguest's profile, opened
        /// from <paramref name="fromSection"/>. Nothing for the player's own name or with no director.
        /// </summary>
        private Button ProfileLink(TMP_Text name, ContestantState actor, string fromSection)
        {
            if (name == null || actor == null || actor.isPlayer || director == null) return null;
            string id = actor.id;
            name.gameObject.name = ProfileLinkPrefix + actor.name;
            name.fontStyle |= FontStyles.Underline;
            name.raycastTarget = true;
            // The backing sits just behind the name, as large as its box and a little more.
            var rect = name.rectTransform;
            var backing = new GameObject(LinkFocusName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            backing.SetParent(rect.parent, false);
            backing.SetSiblingIndex(rect.GetSiblingIndex());
            backing.anchorMin = rect.anchorMin; backing.anchorMax = rect.anchorMax; backing.pivot = rect.pivot;
            // Grown 6 units each side and 2 above and below, whichever corner the name is pinned by.
            backing.anchoredPosition = rect.anchoredPosition
                + new Vector2((rect.pivot.x * 2f - 1f) * 6f * FontScale, (rect.pivot.y * 2f - 1f) * 2f * FontScale);
            backing.sizeDelta = rect.sizeDelta + new Vector2(12f * FontScale, 4f * FontScale);
            var image = backing.GetComponent<Image>();
            image.raycastTarget = false;
            var button = Pressable(rect, () => director.ShowHouseguestProfile(id, fromSection));
            LightOnFocus(button, image);
            return button;
        }

        /// <summary>
        /// A press whose backing shows only while it is focused, hovered or pressed: the theme's raised
        /// surface, hidden at rest (a tint multiplies the backing's own colour, alpha included).
        /// </summary>
        private static void LightOnFocus(Button button, Image backing)
        {
            if (button == null || backing == null) return;
            backing.color = UiTheme.SurfaceRaised;
            // At rest before the press takes it: its tint then starts from hidden, never a 0.1 s flash per render.
            backing.canvasRenderer.SetAlpha(0f);
            button.targetGraphic = backing;
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = Color.white;
            colours.selectedColor = Color.white;
            colours.pressedColor = new Color(.85f, .85f, .85f, 1f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0f);
            button.colors = colours;
        }

        /// <summary>
        /// A card headed by a houseguest's name (the vote page's read, a card a voter) makes that name
        /// a link to their profile, opened from <paramref name="fromSection"/>. The eyebrow is the
        /// card's first line of text. Nothing for the player or a card with no text.
        /// </summary>
        public Button LinkCardEyebrow(RectTransform card, ContestantState actor, string fromSection)
        {
            if (card == null) return null;
            return ProfileLink(card.GetComponentInChildren<TMP_Text>(true), actor, fromSection);
        }

        /// <summary>
        /// The room map's people (the notebook's Who is where) as links: each name the map marks with
        /// <see cref="HouseMap.OccupantNamePrefix"/> and an id becomes a press that opens that
        /// houseguest's profile, from the room map. The player's own name is never marked.
        /// </summary>
        private void WireRoomProfiles(RectTransform map)
        {
            var state = director != null ? director.Snapshot : null;
            if (map == null || state == null) return;
            foreach (var name in map.GetComponentsInChildren<TMP_Text>(true)
                         .Where(t => t.name.StartsWith(HouseMap.OccupantNamePrefix, System.StringComparison.Ordinal)).ToList())
                ProfileLink(name, state.Find(name.name.Substring(HouseMap.OccupantNamePrefix.Length)), EpisodeDirector.NotebookSection.Rooms);
        }

        /// <summary>
        /// The relationship web's people as links (its allies and rivals rows, and the link under a
        /// chosen houseguest's head): each row the web marks for a profile becomes a press that opens
        /// it, from Relationships. Wired here, where the HUD's press and the director are.
        /// </summary>
        private void WireWebProfiles(RectTransform web, EpisodeState state)
        {
            if (web == null || state == null || director == null) return;
            foreach (var rect in web.GetComponentsInChildren<RectTransform>(true)
                         .Where(r => r.name.StartsWith(RelationshipWeb.ProfileRowPrefix, System.StringComparison.Ordinal)).ToList())
            {
                string id = rect.name.Substring(RelationshipWeb.ProfileRowPrefix.Length);
                var actor = state.Find(id);
                if (actor == null || actor.isPlayer || rect.GetComponent<Button>() != null) continue;
                var image = rect.GetComponent<Image>();
                if (image == null) image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = true;
                var button = Pressable(rect, () => director.ShowHouseguestProfile(id, EpisodeDirector.NotebookSection.Network));
                LightOnFocus(button, image);
            }
        }
    }
}
