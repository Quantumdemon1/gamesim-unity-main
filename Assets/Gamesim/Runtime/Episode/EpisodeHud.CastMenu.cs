using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>The card a chip opens over the cast strip, the name a test finds it by.</summary>
        public const string CastMenuName = "Cast menu";

        /// <summary>The card's four ways on, by the houseguest's first name: captions are how a control is found.</summary>
        public static string CastTalkCaption(string first) => "Talk to " + first;
        public static string CastProfileCaption(string first) => first + "'s profile";
        public static string CastDealsCaption(string first) => "Deals with " + first;
        public static string CastFollowCaption(string first, bool following) => (following ? "Stop following " : "Follow ") + first;

        private const float CastMenuWidth = 230f, CastMenuRow = 36f, CastMenuGap = 6f, CastMenuHead = 52f;

        /// <summary>
        /// The card over a houseguest's chip (<see cref="EpisodeDirector.PressCastChip"/>): who they
        /// are and their mood in words, then walk over and talk, their profile, what is between you,
        /// and the camera's follow. It stands on the chip it belongs to, kept inside the frame, and
        /// takes the keyboard as it opens.
        /// </summary>
        private void CastMenu(EpisodeState state, string id)
        {
            var actor = state?.Find(id);
            if (actor == null || canvas == null) return;
            // The chip it belongs to: the last of that name on the strip, since a rebuild in the same
            // frame leaves the old strip under the canvas until it is destroyed.
            Transform rail = null;
            for (int i = canvas.transform.childCount - 1; i >= 0 && rail == null; i--)
                if (canvas.transform.GetChild(i).name == CastRail.RootName && canvas.transform.GetChild(i).gameObject.activeSelf)
                    rail = canvas.transform.GetChild(i);
            RectTransform chip = null;
            if (rail != null)
                foreach (Transform child in rail)
                    if (child.name == actor.name) chip = (RectTransform)child;
            if (chip == null) return;

            string first = (actor.name ?? "").Split(' ')[0];
            float scale = FontScale, width = CastMenuWidth * scale;
            float height = (CastMenuHead + 4f * (CastMenuRow + CastMenuGap) + 8f) * scale;
            var frame = (RectTransform)canvas.transform;
            var corners = new Vector3[4];
            chip.GetWorldCorners(corners);
            // The chip's top-left, in the canvas's own units, measured from its bottom-left.
            Vector2 at = frame.InverseTransformPoint(corners[1]);
            at += frame.rect.size * .5f;
            float x = Mathf.Clamp(at.x, 12f, Mathf.Max(12f, frame.rect.width - width - 12f));
            float y = Mathf.Min(at.y + 8f, frame.rect.height - height - 12f);

            var menu = Chrome(CastMenuName, canvas.transform, UiTheme.Emphasis.Active);
            menu.anchorMin = menu.anchorMax = Vector2.zero;
            menu.pivot = Vector2.zero;
            menu.anchoredPosition = new Vector2(x, y);
            menu.sizeDelta = new Vector2(width, height);
            menu.SetAsLastSibling();

            var name = FixedText(menu, actor.name, 17, UiTheme.Heading, new Vector2(14f * scale, -8f * scale), new Vector2(width - 28f * scale, 22f * scale));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            string mood = EpisodeDirector.MoodLine(state, actor);
            if (mood != null)
                FixedText(menu, mood, 13, RelationshipWeb.MoodColour(actor.mood), new Vector2(14f * scale, -30f * scale), new Vector2(width - 28f * scale, 18f * scale));

            bool following = director.FollowedId == id;
            var rows = new (string caption, System.Action action)[]
            {
                (CastTalkCaption(first), () => director.TalkFromCastMenu(id)),
                (CastProfileCaption(first), () => director.ProfileFromCastMenu(id)),
                (CastDealsCaption(first), () => director.DealsFromCastMenu(id)),
                (CastFollowCaption(first, following), () => director.FollowFromCastMenu(id)),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var button = FixedButton(menu, rows[i].caption,
                    new Vector2(10f * scale, -(CastMenuHead + i * (CastMenuRow + CastMenuGap)) * scale),
                    new Vector2(width - 20f * scale, CastMenuRow * scale), rows[i].action);
                var words = button.GetComponentInChildren<TMP_Text>();
                if (words != null) { words.fontSize = 16; words.fontSizeMax = 16; }
            }
            // The keyboard goes to the card as it opens: the first way on. A rebuild while it is open
            // (a body finishing assembly) keeps whichever of its rows the keyboard had reached.
            bool onTheCard = false;
            foreach (var row in rows) onTheCard |= row.caption == preferredSelection;
            if (!onTheCard) preferredSelection = rows[0].caption;
            restoreSelection = true;
        }
    }
}
