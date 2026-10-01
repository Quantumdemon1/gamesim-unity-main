using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The alliances page's furniture (ACTIONS-DEALS-ALLIANCES-PLAN V3): a heading over each half of
    /// the page with its count, and a Refinement Kit 6 card a pact - an eyebrow, the headline, the
    /// members as faces with a word under each name, then the card's lines of record. The words are
    /// the director's, read from <see cref="AllianceRead"/>; these only lay them out.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The page's cards, a pact of the player's and one they know of, named by what they show.</summary>
        public const string AllianceCardPrefix = "Alliance · ", SuspectedCardPrefix = "Suspected alliance · ";

        /// <summary>
        /// A card of one of the player's pacts: its name and its id, so two pacts that share a name
        /// ("The Riley Pact", ended, and another since) are two cards a test can tell apart. The
        /// card itself shows the name alone.
        /// </summary>
        public static string AllianceCardName(string name, string id) => AllianceCardPrefix + name + " · " + id;

        /// <summary>One face on a pact's card, named for the houseguest.</summary>
        public const string PactFacePrefix = "Pact face · ";
        /// <summary>The page's two empty states, by name.</summary>
        public const string NoAlliancesName = "No alliances", NoSuspectedName = "No known alliances";

        /// <summary>One face on a pact's card.</summary>
        public sealed class PactFace
        {
            public ContestantState Actor;
            /// <summary>A word under the name: the player's own reading of them, or where they are now.</summary>
            public string Note;
            public Color NoteTint;
            /// <summary>A smaller line under that - how they answered the player's calls - or null.</summary>
            public string Detail;
            /// <summary>Out of the house: the face is drawn dimmed and its ring quiet.</summary>
            public bool Away;
        }

        /// <summary>A heading over one half of a page: letterspaced words, how many at the right, and a rule under both.</summary>
        public RectTransform PageSectionHeading(string name, string words, int count)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth(), line = 15f * 1.35f * s + 2f;
            var row = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var label = FixedText(row, words, 15, UiTheme.Muted, new Vector2(0f, -10f * s), new Vector2(width - 64f * s, line));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.characterSpacing = 6f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var tally = FixedText(row, count.ToString(), 15, Accent, Vector2.zero, new Vector2(56f * s, line));
            if (semibold != null) tally.font = semibold;
            tally.alignment = TextAlignmentOptions.Right;
            Anchor(tally.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0f, -10f * s), new Vector2(56f * s, line));
            var rule = HudPrimitives.Fill("Heading rule", row, UiTheme.Edge(UiTheme.Emphasis.Interactive), 1);
            Anchor(rule, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(width, 1f));
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 10f * s + line + 10f * s;
            return row;
        }

        /// <summary>
        /// One pact: the eyebrow in the card's tint, the headline, the members as faces in rows that
        /// wrap to the page's width - each with the word under the name and the line under that -
        /// and then the card's lines of record, each wrapped to as many lines as it needs.
        /// </summary>
        public RectTransform PactCard(string name, string eyebrow, Color eyebrowTint, string headline, IList<PactFace> faces, IList<string> lines)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth(), pad = 20f * s, inner = width - 2f * pad;
            var card = HudPrimitives.KitCard(name, content, false, 14f);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            float y = 16f * s;
            if (!string.IsNullOrEmpty(eyebrow))
            {
                var label = FixedText(card, eyebrow, 13, eyebrowTint, new Vector2(pad, -y), new Vector2(inner, 13f * 1.4f * s + 2f));
                if (semibold != null) label.font = semibold;
                label.characterSpacing = 4f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                y += 26f * s;
            }
            y = PlacedCopy(card, headline, 21, UiTheme.Weight.SemiBold, Paper, pad, y, inner) + 10f * s;

            if (faces != null && faces.Count > 0)
            {
                float cell = 128f * s, gap = 10f * s, disc = 52f * s;
                float nameBox = 14f * 1.35f * s + 2f, noteBox = 12f * 1.35f * s + 2f, detailBox = 11f * 1.35f * s + 2f;
                bool details = faces.Any(face => !string.IsNullOrEmpty(face.Detail));
                float height = disc + 4f * s + 6f * s + nameBox + noteBox + (details ? detailBox : 0f);
                int columns = Mathf.Max(1, Mathf.FloorToInt((inner + gap) / (cell + gap)));
                for (int i = 0; i < faces.Count; i++)
                {
                    var face = faces[i];
                    float x = pad + (i % columns) * (cell + gap), top = y + (i / columns) * (height + gap);
                    var holder = new GameObject(PactFacePrefix + (face.Actor != null ? face.Actor.name : ""), typeof(RectTransform)).GetComponent<RectTransform>();
                    holder.SetParent(card, false);
                    Anchor(holder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -top), new Vector2(cell, height));
                    var rim = HudPrimitives.Portrait(holder, face.Actor != null ? CharacterPortraits.Get(face.Actor) : null,
                        face.Away ? UiTheme.Outline : face.NoteTint, disc, 2f * s, face.Away, face.Actor);
                    rim.name = "Face";
                    Anchor(rim, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, rim.sizeDelta);
                    float under = rim.sizeDelta.y + 6f * s;
                    var who = FixedText(holder, face.Actor != null ? FinalistRead.FirstName(face.Actor.name) : "", 14,
                        face.Away ? UiTheme.Muted : Paper, new Vector2(0f, -under), new Vector2(cell, nameBox));
                    if (semibold != null) who.font = semibold;
                    who.alignment = TextAlignmentOptions.Top;
                    who.textWrappingMode = TextWrappingModes.NoWrap;
                    who.overflowMode = TextOverflowModes.Ellipsis;
                    under += nameBox;
                    var note = FixedText(holder, face.Note ?? "", 12, face.NoteTint, new Vector2(0f, -under), new Vector2(cell, noteBox));
                    note.alignment = TextAlignmentOptions.Top;
                    note.textWrappingMode = TextWrappingModes.NoWrap;
                    under += noteBox;
                    if (!string.IsNullOrEmpty(face.Detail))
                    {
                        var detail = FixedText(holder, face.Detail, 11, UiTheme.Muted, new Vector2(0f, -under), new Vector2(cell, detailBox));
                        detail.alignment = TextAlignmentOptions.Top;
                        detail.textWrappingMode = TextWrappingModes.NoWrap;
                    }
                }
                int rows = (faces.Count + columns - 1) / columns;
                y += rows * height + (rows - 1) * gap + 10f * s;
            }

            if (lines != null)
                foreach (var line in lines.Where(line => !string.IsNullOrEmpty(line)))
                    y = PlacedCopy(card, line, 16, UiTheme.Weight.Regular, UiTheme.Muted, pad, y, inner) + 4f * s;
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 12f * s;
            return card;
        }
    }
}
