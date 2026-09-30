using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>A ceremony screen's parts, by the names a test finds them by.</summary>
        public const string CeremonyTitleName = "Ceremony title", CeremonyFacesName = "Ceremony faces", ChipBagName = "Chip bag";

        /// <summary>One face on a ceremony screen: who, the role it carries this week, and the pill's colour.</summary>
        public struct CeremonyFace
        {
            public string Id, Role;
            public Color RoleColour;
            public CeremonyFace(string id, string role, Color colour) { Id = id; Role = role; RoleColour = colour; }
        }

        /// <summary>
        /// The head of a ceremony's screen (playtest, 2026-09-27): the ceremony's name large, in its
        /// colour, over one line of what is happening. The nomination and the veto's draw were a
        /// quiet card 300 high with the week's names in one line of text; the web gives each the
        /// whole screen (NominationContent, ChipDraw), and so does this.
        /// </summary>
        public void CeremonyTitle(string eyebrow, string title, string line, Color colour)
        {
            if (!string.IsNullOrEmpty(eyebrow)) Eyebrow(eyebrow, colour);
            var heading = FlowText(title, 34, colour);
            heading.gameObject.name = CeremonyTitleName;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
            if (!string.IsNullOrEmpty(line)) Paragraph(line);
        }

        /// <summary>
        /// A row of faces, as cards the width of <paramref name="cardWidth"/>: the photo, the role
        /// pill on its foot, the name under it. Nothing on them is pressed - the ceremony is the
        /// house's; the way on is the panel's own.
        ///
        /// <para>With a <paramref name="fitHeight"/>, the grid - its heading apart - takes no more
        /// than that many units: the cards shrink, as many to a row as the column holds, until every
        /// face fits in the height a one-screen step leaves them (PACK8-PASS-PLAN A3). Never below
        /// <see cref="FaceCardFloor"/>, where a name would stop reading; the names, the photos and the
        /// role pills are the ones a card always carries.</para>
        /// </summary>
        public void CeremonyFaces(string heading, IReadOnlyList<CeremonyFace> faces, float cardWidth = 150f, float fitHeight = 0f)
        {
            if (faces == null || faces.Count == 0 || content == null) return;
            if (!string.IsNullOrEmpty(heading)) Eyebrow(heading, UiTheme.Muted);
            float s = FontScale, width = cardWidth * s, gap = 14f * s;
            if (fitHeight > 0f) width = FittedFaceWidth(faces.Count, width, fitHeight, gap);
            float photo = width * 1.12f, height = photo + 50f * s;
            int columns = Mathf.Max(1, Mathf.FloorToInt((ContentWidth() + gap) / (width + gap)));
            columns = Mathf.Min(columns, faces.Count);
            int rows = Mathf.CeilToInt(faces.Count / (float)columns);
            var grid = new GameObject(CeremonyFacesName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(width, height);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperCenter;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * height + (rows - 1) * gap;
            var state = director != null ? director.Snapshot : null;
            foreach (var face in faces)
            {
                var actor = state != null ? state.Find(face.Id) : null;
                var card = Chrome("Face · " + (actor != null ? actor.name : face.Id), grid);
                var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(face.Id), actor, new Vector2(width - 12f * s, photo), 7);
                picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
                picture.pivot = new Vector2(.5f, 1f);
                picture.anchoredPosition = new Vector2(0f, -6f * s);
                if (!string.IsNullOrEmpty(face.Role))
                {
                    var pill = Panel("Role", picture, face.RoleColour, 4);
                    float pillWidth = Mathf.Clamp(face.Role.Length * 7.5f + 16f, 46f, width - 20f * s) * s;
                    Anchor(pill, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 5f * s), new Vector2(pillWidth, 17f * s));
                    pill.GetComponent<Image>().raycastTarget = false;
                    var word = FixedText(pill, face.Role, 11, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
                    word.alignment = TextAlignmentOptions.Center;
                }
                var name = NewText(card, actor != null ? actor.name : face.Id, 15, Paper);
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) name.font = semibold;
                name.alignment = TextAlignmentOptions.Top;
                AutoSize(name, 10);
                Anchor(name.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -(photo + 12f * s)), new Vector2(width - 10f * s, 34f * s));
            }
        }

        /// <summary>The narrowest a fitted face card goes, at the resting text size: a first name at 10 still reads under a photo this wide.</summary>
        public const float FaceCardFloor = 64f;

        /// <summary>
        /// The widest card, no wider than <paramref name="widest"/>, at which <paramref name="count"/>
        /// faces fit the column in <paramref name="fitHeight"/>: tried at every number of rows, as
        /// many to a row as that makes, because a second row of large faces can beat one row of
        /// small ones. A card is its photo (1.12 of its width) and a 50-unit foot.
        /// </summary>
        private float FittedFaceWidth(int count, float widest, float fitHeight, float gap)
        {
            float s = FontScale, best = 0f, column = ContentWidth();
            for (int rows = 1; rows <= count; rows++)
            {
                int columns = Mathf.CeilToInt(count / (float)rows);
                float byWidth = (column - (columns - 1) * gap) / columns;
                float byHeight = ((fitHeight - (rows - 1) * gap) / rows - 50f * s) / 1.12f;
                best = Mathf.Max(best, Mathf.Min(widest, byWidth, byHeight));
            }
            return Mathf.Max(best, FaceCardFloor * s);
        }

        /// <summary>The campaign's grid of the votes, the name a test finds it by.</summary>
        public const string CampaignVotersName = "Campaign voters";

        /// <summary>
        /// The votes, as the web's campaign draws them (EvictionInteractionStage's houseguest grid):
        /// each voter a card - the photo, the name, where the player stands with them - with a way to
        /// walk over and talk to them under it. The detail is the conversation, one press away.
        /// </summary>
        public void CampaignVoters(IReadOnlyList<string> ids, System.Action<string> talk, float cardWidth = 150f)
        {
            if (ids == null || ids.Count == 0 || content == null) return;
            float s = FontScale, width = cardWidth * s, photo = width * .9f, height = photo + 118f * s, gap = 14f * s;
            int columns = Mathf.Max(1, Mathf.Min(ids.Count, Mathf.FloorToInt((ContentWidth() + gap) / (width + gap))));
            int rows = Mathf.CeilToInt(ids.Count / (float)columns);
            var grid = new GameObject(CampaignVotersName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(width, height);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperCenter;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * height + (rows - 1) * gap;
            var state = director != null ? director.Snapshot : null;
            if (state == null) return;
            // Each voter's read, from the notebook's own sheet (STRATEGY-LOOP-PLAN.md section 2).
            var sheet = VoteRead.Read(state);
            foreach (var id in ids)
            {
                var actor = state.Find(id);
                if (actor == null) continue;
                var card = Chrome("Voter · " + actor.name, grid);
                var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(id), actor, new Vector2(width - 12f * s, photo), 7);
                picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
                picture.pivot = new Vector2(.5f, 1f);
                picture.anchoredPosition = new Vector2(0f, -6f * s);
                var name = FixedText(card, actor.name, 15, Paper, new Vector2(6f * s, -(photo + 10f * s)), new Vector2(width - 12f * s, 20f * s));
                name.alignment = TextAlignmentOptions.Center;
                var kind = RelationshipWeb.KindOf(state, id);
                var standing = FixedText(card, RelationshipWeb.StandingWord(kind), 12,
                    kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind),
                    new Vector2(6f * s, -(photo + 32f * s)), new Vector2(width - 12f * s, 16f * s));
                standing.alignment = TextAlignmentOptions.Center;
                var read = sheet.voters.FirstOrDefault(r => r.voterId == id);
                bool blank = read == null || (read.confidence == VoteRead.Unknown && read.saysId == null);
                var lean = FixedText(card, read == null ? "" : EpisodeDirector.ReadHeadline(state, read), 12, blank ? UiTheme.Muted : Paper,
                    new Vector2(6f * s, -(photo + 48f * s)), new Vector2(width - 12f * s, 16f * s));
                lean.alignment = TextAlignmentOptions.Center;
                lean.name = VoteReadLineName;
                string first = (actor.name ?? "").Split(' ')[0];
                string captured = id;
                var button = FixedButton(card, CastTalkCaption(first), new Vector2(8f * s, -(photo + 70f * s)),
                    new Vector2(width - 16f * s, 36f * s), () => talk(captured));
                var words = button.GetComponentInChildren<TMP_Text>();
                if (words != null) { words.fontSize = 15; words.fontSizeMax = 15; words.alignment = TextAlignmentOptions.Center; }
            }
        }

        /// <summary>
        /// The veto's chip bag (the web's ChipDraw): a bag with a chip for each seat still to be
        /// drawn, and how many that is.
        /// </summary>
        public void ChipBag(int toDraw, string line)
        {
            if (content == null) return;
            float s = FontScale;
            var row = new GameObject(ChipBagName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 150f * s;
            var bag = Panel("Bag", row, new Color(.55f, .33f, .12f, 1f), 26);
            Anchor(bag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f * s, -6f * s), new Vector2(118f * s, 138f * s));
            bag.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(bag, 26, new Color(.85f, .6f, .25f, 1f));
            for (int i = 0; i < Mathf.Clamp(toDraw, 0, 6); i++)
            {
                var chip = Panel("Chip", bag, UiTheme.Accent, 9);
                Anchor(chip, new Vector2(.5f, 0f), new Vector2(.5f, .5f),
                    new Vector2((i % 3 - 1) * 28f * s, (30f + (i / 3) * 30f) * s), new Vector2(22f * s, 22f * s));
                chip.GetComponent<Image>().raycastTarget = false;
            }
            var words = FixedText(row, line, 20, UiTheme.Paper, new Vector2(146f * s, -48f * s), new Vector2(Mathf.Max(160f, ContentWidth() - 160f * s), 60f * s));
            words.textWrappingMode = TextWrappingModes.Normal;
        }
    }
}
