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

        /// <summary>
        /// A card of the public record of past games (ALL-STAR-HISTORIES-PLAN §14.F.2), named for its
        /// fact's id ("Past game · bb16-hitmen"), and one of its faces, named for the houseguest: names
        /// of their own, so no count of the pacts' cards or faces ever takes one in.
        /// </summary>
        public const string PastGameCardPrefix = "Past game · ", PastGameFacePrefix = "Past game face · ";
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
        /// Whether a face's word is the player's own reading of a member in the house (the alliances
        /// page's Friendly, Neutral, Wary or Hostile), which the trust chip says instead (menu slice 3).
        /// Where someone is now, for a member out of the house or a past game's, stays in words.
        /// </summary>
        private static bool TrustChipFor(PactFace face) =>
            face != null && !face.Away && face.Actor != null && !face.Actor.isPlayer
            && (face.Note == AllianceRead.Friendly || face.Note == AllianceRead.Neutral
                || face.Note == AllianceRead.Wary || face.Note == AllianceRead.Hostile);

        /// <summary>A face's rim: where the chip reads trust, the chip's own band tint, so the page says it one way.</summary>
        private Color RimTint(PactFace face)
        {
            var snapshot = director != null ? director.Snapshot : null;
            if (!TrustChipFor(face) || snapshot == null) return face.NoteTint;
            return TrustChip.Tint(TrustChip.BandOf(snapshot.Score(snapshot.playerId, face.Actor.id)));
        }

        /// <summary>
        /// One pact: the eyebrow in the card's tint, the headline, the members as faces in rows that
        /// wrap to the page's width - each with the word under the name and the line under that -
        /// and then the card's lines of record, each wrapped to as many lines as it needs. Each face is
        /// named <paramref name="facePrefix"/> and the houseguest's name.
        /// </summary>
        public RectTransform PactCard(string name, string eyebrow, Color eyebrowTint, string headline, IList<PactFace> faces, IList<string> lines,
            string facePrefix = PactFacePrefix)
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
                // A member's reading is the trust chip (menu slice 3), at the HUD's 14 floor: its row is as tall as that needs.
                bool chips = faces.Any(TrustChipFor);
                float nameBox = 14f * 1.35f * s + 2f, noteBox = (chips ? 14f * 1.3f + 2f : 12f * 1.35f) * s + 2f, detailBox = 11f * 1.35f * s + 2f;
                bool details = faces.Any(face => !string.IsNullOrEmpty(face.Detail));
                float height = disc + 4f * s + 6f * s + nameBox + noteBox + (details ? detailBox : 0f);
                int columns = Mathf.Max(1, Mathf.FloorToInt((inner + gap) / (cell + gap)));
                for (int i = 0; i < faces.Count; i++)
                {
                    var face = faces[i];
                    float x = pad + (i % columns) * (cell + gap), top = y + (i / columns) * (height + gap);
                    var holder = new GameObject(facePrefix + (face.Actor != null ? face.Actor.name : ""), typeof(RectTransform)).GetComponent<RectTransform>();
                    holder.SetParent(card, false);
                    Anchor(holder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -top), new Vector2(cell, height));
                    var rim = HudPrimitives.Portrait(holder, face.Actor != null ? CharacterPortraits.Get(face.Actor) : null,
                        face.Away ? UiTheme.Outline : RimTint(face), disc, 2f * s, face.Away, face.Actor);
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
                    var snapshot = director != null ? director.Snapshot : null;
                    if (TrustChipFor(face) && snapshot != null)
                    {
                        // Your reading of a member in the house is your trust in them, drawn one way on every reading page.
                        var chip = TrustChip.Build(holder, snapshot.Score(snapshot.playerId, face.Actor.id), Mathf.Round(14f * s), s, false);
                        chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(.5f, 1);
                        chip.anchoredPosition = new Vector2(0f, -under);
                    }
                    else
                    {
                        var note = FixedText(holder, face.Note ?? "", 12, face.NoteTint, new Vector2(0f, -under), new Vector2(cell, noteBox));
                        note.alignment = TextAlignmentOptions.Top;
                        note.textWrappingMode = TextWrappingModes.NoWrap;
                    }
                    under += noteBox;
                    // A member's face is a link to their profile, opened from the alliances (menu slice 3, N6 ii).
                    if (face.Actor != null && !face.Actor.isPlayer && director != null && snapshot != null && snapshot.Find(face.Actor.id) != null)
                    {
                        var press = holder.gameObject.AddComponent<Image>();
                        press.raycastTarget = true;
                        string id = face.Actor.id;
                        LightOnFocus(Pressable(holder, () => director.ShowHouseguestProfile(id, EpisodeDirector.NotebookSection.Alliances)), press);
                    }
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
