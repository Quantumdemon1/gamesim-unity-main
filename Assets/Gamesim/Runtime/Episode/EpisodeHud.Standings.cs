using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>The results phase's FINAL STANDINGS as the kit's rows (UI-UX-PASS-PLAN C0).</summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>A standings row's name, followed by its rank: "Standing row 1" is the winner's.</summary>
        public const string StandingRowName = "Standing row";
        /// <summary>The row's parts, for the tests that read them.</summary>
        public const string StandingRankName = "Standing rank", StandingPortraitName = "Standing portrait",
            StandingNameName = "Standing name", StandingWordName = "Standing word";
        /// <summary>The word at the end of the winner's row.</summary>
        public const string StandingWinnerWord = "WINNER";

        /// <summary>
        /// The committed standings as rows: the rank, the face in a ring, the name, and the winner's
        /// row in gold with the winner's badge on their face - the crown, or the veto's medal - and
        /// the word at its end. No score: the engine's composite number is not something anybody in
        /// the house sees, and the order is the whole result (UI-UX-PASS-PLAN decision 12). The
        /// rows take the standings the result card was given, so the two never disagree.
        /// </summary>
        public void CompetitionStandings(IList<CompetitionResult.Standing> standings, HudPrimitives.RoleMark winnersMark)
        {
            if (content == null || standings == null) return;
            float s = FontScale;
            float face = Mathf.Round(40f * s), rowHeight = Mathf.Round(54f * s), width = ContentWidth();
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            for (int i = 0; i < standings.Count; i++)
            {
                var entry = standings[i];
                var row = Panel(StandingRowName + " " + (i + 1), content, entry.IsWinner
                    ? new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .16f)
                    : new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .55f), UiTheme.ControlRadius);
                row.GetComponent<Image>().raycastTarget = false;
                var element = row.gameObject.AddComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = rowHeight;
                row.sizeDelta = new Vector2(width, rowHeight);
                // The winner in gold; you, if you did not win, in the accent - found at a glance.
                if (entry.IsWinner) UiTheme.AddBorder(row, UiTheme.ControlRadius, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .55f));
                else if (entry.IsPlayer) UiTheme.AddBorder(row, UiTheme.ControlRadius, UiTheme.Edge(UiTheme.Emphasis.Active));

                var rank = NewText(row, (i + 1).ToString(), 14, UiTheme.Muted);
                rank.name = StandingRankName;
                rank.alignment = TextAlignmentOptions.MidlineLeft;
                rank.textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(rank.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12f, 0f), new Vector2(26f * s, rowHeight));

                var rim = HudPrimitives.Portrait(row, entry.Portrait, entry.IsWinner ? UiTheme.Gold : entry.IsPlayer ? UiTheme.Accent : UiTheme.Outline,
                    face, 2f * s, false, entry.Character);
                rim.name = StandingPortraitName;
                rim.anchorMin = rim.anchorMax = new Vector2(0f, .5f); rim.pivot = new Vector2(.5f, .5f);
                rim.anchoredPosition = new Vector2(40f * s + face * .5f, 0f);
                if (entry.IsWinner) HudPrimitives.AddRoleMark(rim, winnersMark, face);

                float x = 40f * s + face + 12f * s;
                float wordWidth = entry.IsWinner ? 84f * s : 0f;
                var name = NewText(row, HudPrimitives.WithYou(entry.Name, entry.IsPlayer)
                    + (string.IsNullOrEmpty(entry.Note) ? "" : "  ·  " + entry.Note), 16, entry.IsWinner ? UiTheme.Gold : Paper);
                name.name = StandingNameName;
                if (entry.IsWinner && semibold != null) name.font = semibold;
                name.alignment = TextAlignmentOptions.MidlineLeft;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                AutoSize(name, 11);
                Anchor(name.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(x, 0f),
                    new Vector2(Mathf.Max(60f, width - x - wordWidth - 14f), rowHeight));
                if (!entry.IsWinner) continue;

                var word = NewText(row, StandingWinnerWord, 11, UiTheme.Gold);
                word.name = StandingWordName;
                if (semibold != null) word.font = semibold;
                word.characterSpacing = 3f;
                word.alignment = TextAlignmentOptions.MidlineRight;
                word.textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(word.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-14f, 0f), new Vector2(wordWidth, rowHeight));
            }
        }
    }
}
