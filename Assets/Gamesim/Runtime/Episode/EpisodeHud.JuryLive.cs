using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Jury questioning as a live event (ENDGAME-PLAN F5, mockup 35), the part that needs no saved
    /// field (F5a): the juror and their question on the left, the answers in the centre - the
    /// controls they have always been, under the captions they have always had, in the same order -
    /// and the season's receipt on the right: what the record holds between the player and the
    /// juror asking (the jury house's read of them). One to a row at the larger text.
    ///
    /// <para>The receipt is evidence the player holds, never the answer: the saved answer key is
    /// read only after the answer is committed, for the reaction line, which is the engine's own
    /// note. No saved row holds a juror's words, so the receipt quotes the record, not the juror.
    /// History questions and five responses are F5b's, under schema 21.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The parts a test finds by name.</summary>
        public const string JuryLiveName = "Jury live", JuryAskerColumnName = "Jury asker", JuryAnswerColumnName = "Jury answers",
            JuryReceiptColumnName = "Season receipt", JuryReactionName = "Jury reaction", JuryHintName = "Jury hint";

        public const string JuryHintWords = "The jury is listening.";

        /// <summary>The five responses' column, the receipt's line, and the final case's parts (ENDGAME-PLAN F4b/F5b).</summary>
        public const string JuryResponsesName = "Jury responses", JuryReceiptLineName = "Jury receipt",
            FinalCaseResumeName = "Your season résumé", FinalCaseThemesName = "Final case narratives", FinalCaseMomentsName = "Final case moments";

        /// <summary>
        /// The responses offered for a history question, one to a row: the caption, the words the
        /// player would say, and the risk. The locked argument's own response comes first, a read at
        /// render that is never saved; nothing on a tile says which one lands.
        /// </summary>
        private void ResponseTiles(EpisodeState state, JuryExchangeState exchange)
        {
            var offered = FinaleQuestions.Offered(exchange.category, exchange.receiptKind).ToList();
            string first = FinaleQuestions.FirstFor(state.finalArgument?.theme);
            offered = offered.OrderBy(response => response == first ? 0 : 1).ToList();
            Tiles(JuryResponsesName, offered.Select(response => new MoveTile
            {
                Caption = FinaleQuestions.Caption(response), Description = FinaleQuestions.Line(exchange.category, response),
                Corner = RiskTag(FinaleQuestions.Risk(response)), CornerTint = RiskTint(FinaleQuestions.Risk(response)), Glyph = "chat",
                Choose = () => director.AnswerJury(response),
            }).ToList(), TileStyle.List);
        }

        /// <summary>
        /// The three columns. <paramref name="controls"/> draws the centre - the answers, the tone
        /// questions, the recorded answers and Continue - exactly as the panel always drew them.
        /// </summary>
        private void JuryLive(EpisodeState state, JuryExchangeState exchange, ContestantState questioner, ContestantState finalist, Action controls)
        {
            float s = FontScale, width = ContentWidth(), gap = 18f * s;
            bool beside = width >= 900f && FontScale <= 1.05f;
            var row = new GameObject(JuryLiveName, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = beside
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            float room = beside ? width - 2f * gap : width;
            float left = beside ? room * .26f : width, centre = beside ? room * .44f : width, right = beside ? room * .30f : width;

            bool playerAnswers = exchange.finalistId == state.playerId;
            // The one on the spot: the juror asking the player, or the finalist the player asks.
            var face = playerAnswers ? questioner : finalist;

            PushContent(LiveColumn(JuryAskerColumnName, row, left), left);
            if (face != null)
            {
                var photoRow = new GameObject("Photo row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                photoRow.SetParent(content, false);
                var size = new Vector2(96f, 118f) * s;
                photoRow.GetComponent<LayoutElement>().minHeight = size.y;
                var photo = HudPrimitives.RectPortrait(photoRow, "Photo", CharacterPortraits.Get(face), face, size, 10);
                photo.anchorMin = photo.anchorMax = new Vector2(.5f, 1f);
                photo.pivot = new Vector2(.5f, 1f); photo.anchoredPosition = Vector2.zero;
                var name = FlowText(face.name, 18, Paper);
                name.alignment = TextAlignmentOptions.Center;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) name.font = semibold;
                if (playerAnswers)
                    FlowText("Leads with " + WebJuryQuestioning.GetPrimaryTrait(face.traits), 12, UiTheme.Muted).alignment = TextAlignmentOptions.Center;
            }
            if (!string.IsNullOrEmpty(exchange.question))
                FlowText(exchange.question, 21, Accent).gameObject.name = "Jury question";
            PopContent();

            PushContent(LiveColumn(JuryAnswerColumnName, row, centre), centre);
            controls();
            string reaction = Reaction(state, exchange, questioner);
            if (reaction != null)
            {
                bool impressed = exchange.category != null ? FinaleQuestions.Landed(state, exchange) : exchange.answerChoice == exchange.correctChoice;
                var line = FlowText(reaction, 15, impressed ? UiTheme.Allied : UiTheme.Conflict);
                line.name = JuryReactionName;
            }
            PopContent();

            PushContent(LiveColumn(JuryReceiptColumnName, row, right), right);
            if (playerAnswers) Receipt(state, exchange);
            else FinalistReceipt(state, exchange.finalistId);
            PopContent();
        }

        private RectTransform LiveColumn(string name, RectTransform row, float width)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            column.SetParent(row, false);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f * FontScale;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var element = column.GetComponent<LayoutElement>();
            element.minWidth = 0f; element.preferredWidth = width; element.flexibleWidth = width;
            return column;
        }

        /// <summary>
        /// The engine's own note for a committed answer, rebuilt from the saved exchange so it
        /// survives a reload: "{juror} was impressed by your response during jury questioning." or
        /// "...unconvinced...". Null until the player finalist has answered.
        /// </summary>
        public static string Reaction(EpisodeState state, JuryExchangeState exchange, ContestantState questioner)
        {
            if (exchange == null || !exchange.completed || exchange.finalistId != state.playerId) return null;
            // A history question's note, from what was saved, as the engine wrote it.
            if (exchange.category != null)
                return FinaleQuestions.Note(questioner?.name ?? "Unknown housemate", FinaleQuestions.Landed(state, exchange));
            bool choice(string key) => key == "A" || key == "B";
            if (!choice(exchange.answerChoice) || !choice(exchange.correctChoice)) return null;
            if (string.IsNullOrEmpty(exchange.questionerId) || exchange.questionerId == exchange.finalistId) return null;
            return WebJuryQuestioning.EvaluateChoice(new WebJuryQuestion { correctIs = exchange.correctChoice }, exchange.answerChoice,
                exchange.questionerId, questioner?.name ?? "Unknown housemate", exchange.finalistId).note;
        }

        /// <summary>The season's receipt for the juror asking: where they stand with the player and the dated lines between them.</summary>
        private void Receipt(EpisodeState state, JuryExchangeState exchange)
        {
            string jurorId = exchange.questionerId;
            ReceiptEyebrow("SEASON RECEIPT");
            // A history question's own receipt first (ENDGAME-PLAN F5b): the row it was built from.
            if (exchange.category != null)
            {
                string line = exchange.category == FinaleQuestions.Comparison ? "No receipt: they are weighing you against the other finalist."
                    : FinaleQuestions.ReceiptLine(state, exchange) ?? "The record no longer holds the row this question came from.";
                var receipt = FlowText(line, 14, Paper);
                receipt.name = JuryReceiptLineName;
            }
            var read = JuryHouseRead.ReadJuror(state, jurorId);
            var band = FlowText(read.band.ToUpperInvariant(), 14, BandTint(read.band));
            band.name = JurorBandName;
            band.characterSpacing = 4f;
            FlowText(read.reason, 13, Paper);
            var lines = read.highlights.Skip(Math.Max(0, read.highlights.Count - 4)).Concat(read.knows.Take(2)).ToList();
            if (lines.Count == 0) FlowText("Nothing on the record between you.", 13, UiTheme.Muted);
            foreach (var line in lines) FlowText(line, 13, UiTheme.Muted);
        }

        /// <summary>For a player juror: what they know of the finalist they are asking, the finalist cards' first facts.</summary>
        private void FinalistReceipt(EpisodeState state, string finalistId)
        {
            var actor = state.Find(finalistId);
            ReceiptEyebrow("WHAT YOU KNOW OF " + FinalistRead.FirstName(actor?.name).ToUpperInvariant());
            var read = FinalistRead.Read(state, finalistId);
            if (read == null) return;
            foreach (var fact in new[] { read.resume, read.relationship, read.agreement, read.alliances })
            {
                FlowText(fact.label.ToUpperInvariant(), 11, UiTheme.Muted).characterSpacing = 3f;
                FlowText(fact.value, 13, Paper);
            }
        }

        private void ReceiptEyebrow(string words)
        {
            var eyebrow = FlowText(words, 12, Accent);
            eyebrow.characterSpacing = 6f;
        }
    }
}
