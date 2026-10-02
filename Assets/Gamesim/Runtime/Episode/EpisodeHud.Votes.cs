using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The vote page's cards (Refinement Kit 6): an eviction result a week, the ballots the player
    /// knows of each reveal with one line counting the ones they do not, and the line that says what
    /// stays private. The words are the director's; these only lay them out.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        public const string VoteRecordPrefix = "Vote record · Week ";
        public const string BallotsPrefix = "Ballots · Week ";
        public const string VotesInProgressName = "Vote in progress";
        public const string VotesPrivacyName = "Ballot privacy";

        /// <summary>One known ballot: who cast it, what it was, and the reason they gave in public.</summary>
        public sealed class BallotLine
        {
            public ContestantState Voter;
            public string Words, Reason, Tag;
        }

        /// <summary>A week's eviction result: an eyebrow, a headline, and the lines of record under it.</summary>
        public RectTransform RecordCard(string name, string eyebrow, string headline, Color ink, IList<string> lines)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth(), pad = 20f * s, inner = width - 2f * pad;
            var card = HudPrimitives.KitCard(name, content, false, 14f);
            float y = CardEyebrow(card, eyebrow, pad, 16f * s, inner);
            y = PlacedCopy(card, headline, 21, UiTheme.Weight.SemiBold, ink, pad, y, inner) + 6f * s;
            if (lines != null)
                foreach (var line in lines)
                    y = PlacedCopy(card, line, 16, UiTheme.Weight.Regular, UiTheme.Muted, pad, y, inner) + 4f * s;
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 12f * s;
            return card;
        }

        /// <summary>A week's known ballots, a line each with the voter's face and their public reason.</summary>
        public RectTransform BallotCard(string name, string eyebrow, IList<BallotLine> ballots)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth(), pad = 20f * s, face = 36f * s;
            float left = pad + face + 14f * s, inner = width - left - pad;
            var card = HudPrimitives.KitCard(name, content, false, 14f);
            float y = CardEyebrow(card, eyebrow, pad, 16f * s, width - 2f * pad) + 4f * s;
            foreach (var ballot in ballots)
            {
                var rim = HudPrimitives.Portrait(card, ballot.Voter != null ? CharacterPortraits.Get(ballot.Voter) : null,
                    UiTheme.Outline, face, 2f * s, false, ballot.Voter);
                rim.name = "Voter";
                Anchor(rim, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -y), rim.sizeDelta);
                float bottom = PlacedCopy(card, ballot.Words, 18, UiTheme.Weight.Medium, Paper, left, y, inner);
                string under = ballot.Reason != null ? "“" + ballot.Reason + "”" : null;
                if (!string.IsNullOrEmpty(ballot.Tag)) under = under != null ? ballot.Tag + " · " + under : ballot.Tag;
                if (!string.IsNullOrEmpty(under))
                    bottom = PlacedCopy(card, under, 15, UiTheme.Weight.Regular, UiTheme.Muted, left, bottom + 2f * s, inner);
                y = Mathf.Max(bottom, y + rim.sizeDelta.y) + 12f * s;
            }
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 4f * s;
            return card;
        }

        /// <summary>A quiet line with the kit's lock: what the page cannot show, and why.</summary>
        public RectTransform LockNote(string name, string words) => IconNote(name, PackArt.KitIconLock, words);

        private RectTransform IconNote(string name, string icon, string words, int textSize = 16)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth();
            var row = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            float x = 0f;
            if (KitGlyph(row, icon, UiTheme.Muted, new Vector2(0, 1), new Vector2(0f, -8f * s), 20f * s) != null) x = 30f * s;
            float bottom = PlacedCopy(row, words, textSize, UiTheme.Weight.Regular, UiTheme.Muted, x, 6f * s, width - x);
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = bottom + 6f * s;
            return row;
        }

        private float CardEyebrow(RectTransform card, string eyebrow, float x, float y, float width)
        {
            if (string.IsNullOrEmpty(eyebrow)) return y;
            float s = FontScale;
            var label = FixedText(card, eyebrow, 13, UiTheme.Muted, new Vector2(x, -y), new Vector2(width, 18f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.characterSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return y + 24f * s;
        }
    }
}
