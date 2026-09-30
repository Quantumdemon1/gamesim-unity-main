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
    ///
    /// <para>The mockup pass (MOCKUP-PASS M11, mockup 55) framed it: the one on the spot in a card
    /// of their own beside the question in the jury's panel, with the receipt's kicker over it;
    /// the responses as compact rows that fit, four or five; a leaner receipt with the juror's
    /// greyed photo, its week, and the week's count beside the line; a framed footer; and the ways
    /// on in one thin row. Every caption and every name a test finds a part by is unchanged.</para>
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

        /// <summary>The mockup pass's parts (MOCKUP-PASS M11), by the names a test finds them by.</summary>
        public const string JuryAskerCardName = "Jury asker card", JuryQuestionPanelName = "Jury question panel", JuryKickerName = "Jury kicker",
            JuryReceiptCardName = "Jury receipt card", JuryReceiptTallyName = "Jury receipt tally", JuryRecapName = "Jury recap headline",
            JuryReactNoteName = "Jury react note", JuryFooterName = "Jury footer", JuryWaysOnName = "Jury ways on";

        /// <summary>The line under the receipt until the answer is in; the engine's own note takes over after.</summary>
        public const string JuryReactWords = "The jury will react after your answer.";

        /// <summary>The asker's photo, the receipt's photo, and the narrowest the question's panel may be beside the card, at the resting text size.</summary>
        private const float AskerPhotoWidth = 150f, AskerPhotoHeight = 185f, ReceiptPhotoWidth = 56f, ReceiptPhotoHeight = 68f,
            QuestionPanelMinWidth = 170f;

        /// <summary>The ways on's thin row: its height, its captions' size, and the column it needs to stand three abreast.</summary>
        private const float WaysOnHeight = 44f, WaysOnRowWidth = 720f;
        private const int WaysOnCaptionSize = 16;

        /// <summary>
        /// The responses offered for a history question, one to a compact row: the caption, the
        /// words the player would say, and the risk. The locked argument's own response comes
        /// first, a read at render that is never saved; nothing on a tile says which one lands.
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
            }).ToList(), TileStyle.Compact);
        }

        /// <summary>
        /// The three columns. <paramref name="controls"/> draws the centre - the answers, the tone
        /// questions, the recorded answers and Continue - exactly as the panel always drew them.
        /// <paramref name="asks"/> is the line the panel used to open with ("Casey asks you"), and
        /// the question's number and count were its heading; both are the asker's card's now.
        /// </summary>
        private void JuryLive(EpisodeState state, JuryExchangeState exchange, ContestantState questioner, ContestantState finalist,
            int number, int count, string asks, Action controls)
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
            // The asker's card and the question side by side take the larger share (MOCKUP-PASS M11).
            float left = beside ? room * .38f : width, centre = beside ? room * .36f : width, right = beside ? room * .26f : width;

            bool playerAnswers = exchange.finalistId == state.playerId;
            // The one on the spot: the juror asking the player, or the finalist the player asks.
            var face = playerAnswers ? questioner : finalist;
            string kicker = playerAnswers && exchange.category != null ? FinaleQuestions.Kicker(state, exchange) : null;

            PushContent(LiveColumn(JuryAskerColumnName, row, left), left);
            AskerAndQuestion(exchange, face, (playerAnswers ? "Juror" : "Finalist") + " · Question " + number + " of " + count,
                asks, playerAnswers, kicker, left);
            PopContent();

            PushContent(LiveColumn(JuryAnswerColumnName, row, centre), centre);
            controls();
            FitActionRows(content, centre);
            string reaction = Reaction(state, exchange, questioner);
            if (reaction != null)
            {
                bool impressed = exchange.category != null ? FinaleQuestions.Landed(state, exchange) : exchange.answerChoice == exchange.correctChoice;
                var line = FlowText(reaction, 15, impressed ? UiTheme.Allied : UiTheme.Conflict);
                line.name = JuryReactionName;
            }
            PopContent();

            PushContent(LiveColumn(JuryReceiptColumnName, row, right), right);
            if (playerAnswers) Receipt(state, exchange, questioner);
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
        /// Grows each action row in a column to the lines its caption needs. The old seasons' A and
        /// B answers and the player juror's questions are whole sentences at 20. The centre column
        /// is about a third of the row, and a row one line tall showed only their first line. The
        /// caption's words and size are untouched; only the row's height changes.
        /// </summary>
        private void FitActionRows(RectTransform column, float width)
        {
            foreach (Transform child in column)
            {
                var element = child.GetComponent<LayoutElement>();
                var caption = child.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.transform.parent == child);
                if (child.GetComponent<Button>() == null || element == null || caption == null) continue;
                // The caption is stretched over its row, inset on every side.
                var box = caption.rectTransform;
                float inner = width - box.offsetMin.x + box.offsetMax.x, ends = box.offsetMin.y - box.offsetMax.y;
                float need = Mathf.Ceil(caption.GetPreferredValues(caption.text, inner, 0f).y) + ends + 4f * FontScale;
                element.minHeight = Mathf.Max(element.minHeight, need);
            }
        }

        /// <summary>
        /// The one on the spot in a card of their own - the photo, the name, who they are this
        /// question and its number, what a juror leads with, and who asks whom - beside the
        /// question in the jury's own panel, with the receipt's kicker in bold over it (MOCKUP-PASS
        /// M11, decision 39). The heading and the line the panel opened with are the card's, word
        /// for word. One under the other where the column is too narrow for both.
        /// </summary>
        private void AskerAndQuestion(JuryExchangeState exchange, ContestantState face, string role, string asks, bool juror, string kicker, float width)
        {
            // The card's sides are narrow so its lines have nearly the photo's width: "Juror ·
            // Question 14 of 14" is the longest, in a house of sixteen.
            float s = FontScale, gap = 12f * s, pad = 8f * s, ends = 12f * s;
            var photo = new Vector2(AskerPhotoWidth, AskerPhotoHeight) * s;
            float cardWidth = photo.x + 2f * pad;
            bool question = !string.IsNullOrEmpty(exchange.question) || kicker != null;
            bool pair = question && width >= cardWidth + gap + QuestionPanelMinWidth * s;
            var row = new GameObject("Jury asker row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = pair
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            // Beside it, the card keeps its width and the panel takes the rest, as tall as the card.
            layout.childForceExpandWidth = !pair; layout.childForceExpandHeight = pair;

            var card = HudPrimitives.KitCard(JuryAskerCardName, row, false, 12f);
            UiTheme.AddGlow(card, 12);
            // The card's edge and glow stay stretched over it: the layout lays out only its lines.
            foreach (Transform decoration in card) decoration.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            float cardSpan = pair ? cardWidth : width;
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.minWidth = pair ? cardWidth : 0f; element.preferredWidth = cardSpan; element.flexibleWidth = pair ? 0f : 1f;
            var lines = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int inset = Mathf.RoundToInt(pad), rim = Mathf.RoundToInt(ends);
            lines.padding = new RectOffset(inset, inset, rim, rim);
            lines.spacing = 3f * s; lines.childAlignment = TextAnchor.UpperCenter;
            lines.childControlWidth = lines.childControlHeight = true;
            lines.childForceExpandWidth = true; lines.childForceExpandHeight = false;
            PushContent(card, cardSpan - 2f * pad);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (face != null)
            {
                var photoRow = new GameObject("Photo row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                photoRow.SetParent(content, false);
                photoRow.GetComponent<LayoutElement>().minHeight = photo.y;
                var picture = HudPrimitives.RectPortrait(photoRow, "Photo", CharacterPortraits.Get(face), face, photo, 10);
                picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
                picture.pivot = new Vector2(.5f, 1f); picture.anchoredPosition = Vector2.zero;
                var name = FlowText(face.name, 18, Paper);
                name.alignment = TextAlignmentOptions.Center;
                if (semibold != null) name.font = semibold;
            }
            var who = FlowText(role, 13, Accent);
            who.alignment = TextAlignmentOptions.Center;
            // What a juror's questions come from; a finalist is not asking.
            if (juror && face != null)
                FlowText("Leads with " + WebJuryQuestioning.GetPrimaryTrait(face.traits), 12, UiTheme.Muted).alignment = TextAlignmentOptions.Center;
            FlowText(asks, 13, Paper).alignment = TextAlignmentOptions.Center;
            PopContent();

            if (!question) return;
            float panelWidth = pair ? width - cardWidth - gap : width;
            var panel = new GameObject(JuryQuestionPanelName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement))
                .GetComponent<RectTransform>();
            panel.SetParent(row, false);
            var ground = panel.GetComponent<Image>();
            ground.raycastTarget = false;
            if (!UiTheme.PackSliced(ground, PackArt.JuryQuestionPanel, 12f)) UiTheme.Style(ground, UiTheme.SurfaceRaised, 12);
            var size = panel.GetComponent<LayoutElement>();
            size.minWidth = 0f; size.preferredWidth = panelWidth; size.flexibleWidth = 1f;
            var words = panel.GetComponent<VerticalLayoutGroup>();
            int side = Mathf.RoundToInt(18f * s), edge = Mathf.RoundToInt(16f * s);
            words.padding = new RectOffset(side, side, edge, edge);
            words.spacing = 6f * s; words.childAlignment = TextAnchor.MiddleLeft;
            words.childControlWidth = words.childControlHeight = true;
            words.childForceExpandWidth = true; words.childForceExpandHeight = false;
            PushContent(panel, panelWidth - 2f * side);
            if (kicker != null)
            {
                // The receipt in a few words, in bold, over the saved question it was asked from.
                var over = FlowText(kicker, 13, UiTheme.Gold);
                over.name = JuryKickerName;
                over.fontStyle = FontStyles.Bold;
            }
            if (!string.IsNullOrEmpty(exchange.question))
                FlowText(exchange.question, 20, Paper).gameObject.name = "Jury question";
            PopContent();
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

        /// <summary>
        /// The season's receipt for the juror asking: the row the question came from as a card, the
        /// week's recap headline when it says more than the card does, where they stand with the
        /// player, and at most two dated lines between them that are not the receipt's own week.
        /// Until the answer is in, a line says the jury will react to it.
        /// </summary>
        private void Receipt(EpisodeState state, JuryExchangeState exchange, ContestantState questioner)
        {
            string jurorId = exchange.questionerId;
            ReceiptEyebrow("SEASON RECEIPT");
            int? week = null;
            // A history question's own receipt first (ENDGAME-PLAN F5b): the row it was built from.
            if (exchange.category != null)
            {
                bool comparison = exchange.category == FinaleQuestions.Comparison;
                week = comparison ? null : FinaleQuestions.ReceiptWeek(state, exchange);
                string line = comparison ? "No receipt: they are weighing you against the other finalist."
                    : FinaleQuestions.ReceiptLine(state, exchange) ?? "The record no longer holds the row this question came from.";
                ReceiptCard(line, comparison ? null : questioner, week, comparison ? null : FinaleQuestions.ReceiptTally(state, exchange));
                // The week's recap headline, unquoted, only under a receipt about that week's vote
                // whose line does not say who went (review correction 19). The recap is the
                // player's own, and public.
                if (week != null && FinaleQuestions.RecapAdds(state, exchange))
                {
                    var recap = WeeklyRecap.Build(state, week.Value);
                    if (recap.evicted != null) FlowText(recap.Headline, 13, UiTheme.Muted).name = JuryRecapName;
                }
            }
            var read = JuryHouseRead.ReadJuror(state, jurorId);
            var band = FlowText(read.band.ToUpperInvariant(), 14, BandTint(read.band));
            band.name = JurorBandName;
            band.characterSpacing = 4f;
            FlowText(read.reason, 13, Paper);
            // Leaner than the jury house's card: the receipt's own week is not said twice, and two
            // dated lines at most.
            string said = week == null ? null : "Week " + week.Value + " · ";
            var dated = read.highlights.Where(entry => said == null || !entry.StartsWith(said, StringComparison.Ordinal)).ToList();
            var lines = dated.Skip(Math.Max(0, dated.Count - 2)).Concat(read.knows.Take(2)).ToList();
            if (lines.Count == 0) FlowText("Nothing on the record between you.", 13, UiTheme.Muted);
            foreach (var line in lines) FlowText(line, 13, UiTheme.Muted);
            if (!exchange.completed) IconNote(JuryReactNoteName, PackArt.KitIconJury, JuryReactWords, 13);
        }

        /// <summary>
        /// The receipt as a card of the record: the juror's photo, greyed as a thing past, with the
        /// week on its foot; the receipt's line in bold, found by its name and word for word what
        /// <see cref="FinaleQuestions.ReceiptLine"/> says; and the week's count in a text of its own
        /// beside the line, never inside it. A comparison has no row, and draws its line alone.
        /// </summary>
        private void ReceiptCard(string line, ContestantState juror, int? week, string tally)
        {
            float s = FontScale, width = ContentWidth();
            var card = new GameObject(JuryReceiptCardName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            card.SetParent(content, false);
            float x = 0f, tall = 0f;
            if (juror != null)
            {
                var size = new Vector2(ReceiptPhotoWidth, ReceiptPhotoHeight) * s;
                var photo = HudPrimitives.RectPortrait(card, "Receipt photo", CharacterPortraits.Get(juror), juror, size, 6);
                Anchor(photo, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, size);
                var face = photo.GetComponentInChildren<RawImage>();
                if (face != null) face.color = new Color(.6f, .65f, .7f, 1f);
                if (week != null)
                {
                    // The week on the photo's foot, inside its rounded mask.
                    var foot = Panel("Week band", photo, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .8f), 0);
                    foot.GetComponent<Image>().raycastTarget = false;
                    foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f); foot.pivot = new Vector2(.5f, 0f);
                    foot.offsetMin = Vector2.zero; foot.offsetMax = new Vector2(0f, 16f * s);
                    var words = FixedText(foot, "WEEK " + week.Value, 10, Paper, Vector2.zero, new Vector2(size.x, 16f * s));
                    Stretch(words.rectTransform, 2f, 0f, 2f, 0f);
                    words.alignment = TextAlignmentOptions.Center;
                    words.textWrappingMode = TextWrappingModes.NoWrap;
                    words.characterSpacing = 2f;
                    var bold = UiTheme.Font(UiTheme.Weight.SemiBold);
                    if (bold != null) words.font = bold;
                    AutoSize(words, 8);
                }
                x = size.x + 10f * s; tall = size.y;
            }
            TMP_Text count = null;
            float countWidth = 0f;
            if (!string.IsNullOrEmpty(tally))
            {
                count = NewText(card, tally, 14, UiTheme.Muted);
                count.name = JuryReceiptTallyName;
                count.textWrappingMode = TextWrappingModes.NoWrap;
                count.alignment = TextAlignmentOptions.TopRight;
                countWidth = Mathf.Ceil(count.GetPreferredValues(count.text).x) + 4f * s;
                Anchor(count.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(countWidth, 20f * s));
            }
            float lineWidth = Mathf.Max(60f * s, width - x - (count != null ? countWidth + 6f * s : 0f));
            var receipt = NewText(card, line, 14, Paper);
            receipt.name = JuryReceiptLineName;
            receipt.fontStyle = FontStyles.Bold;
            float lineHeight = Mathf.Ceil(receipt.GetPreferredValues(receipt.text, lineWidth, 0f).y) + 4f * s;
            Anchor(receipt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, 0f), new Vector2(lineWidth, lineHeight));
            var element = card.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = Mathf.Max(tall, lineHeight);
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

        /// <summary>
        /// The questioning's footer, framed, with the kit's info mark: what to take care over, the
        /// hint the panel has always carried, and that the questions are public and no answer is a
        /// vote - the line the panel used to open with (MOCKUP-PASS M11). The line keeps the hint's name.
        /// </summary>
        private void JuryFooter(string words)
        {
            if (content == null) return;
            float s = FontScale, width = ContentWidth(), pad = 14f * s;
            var card = HudPrimitives.KitCard(JuryFooterName, content, false, 10f);
            float x = pad;
            if (KitGlyph(card, PackArt.KitIconInfo, Accent, new Vector2(0, 1), new Vector2(pad, -10f * s), 20f * s) != null) x += 30f * s;
            var line = NewText(card, words, 14, UiTheme.Muted);
            line.name = JuryHintName;
            float inner = width - x - pad;
            float height = Mathf.Ceil(line.GetPreferredValues(line.text, inner, 0f).y) + 4f * s;
            Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -10f * s), new Vector2(inner, height));
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = Mathf.Max(height, 20f * s) + 20f * s;
        }

        /// <summary>
        /// Opens the questioning's ways on: one thin row after the live layout (MOCKUP-PASS M11) -
        /// skip the rest, then the doors the director adds, the final case and the jury house, in
        /// that order, under the captions they have always had. One under another where the column
        /// is too narrow for three abreast. The rows until <see cref="EndWaysOn"/> are its.
        /// </summary>
        private RectTransform BeginWaysOn()
        {
            float s = FontScale, width = ContentWidth();
            bool abreast = width >= WaysOnRowWidth * s;
            var ways = new GameObject(JuryWaysOnName, typeof(RectTransform)).GetComponent<RectTransform>();
            ways.SetParent(content, false);
            HorizontalOrVerticalLayoutGroup layout = abreast
                ? (HorizontalOrVerticalLayoutGroup)ways.gameObject.AddComponent<HorizontalLayoutGroup>()
                : ways.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f * s;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = abreast;
            PushContent(ways, width);
            return ways;
        }

        /// <summary>
        /// Closes the ways on: each row thin, its caption a size down - its words are the control's
        /// and are not touched - and, abreast, an equal share of the row whatever its caption.
        /// </summary>
        private void EndWaysOn(RectTransform ways)
        {
            PopContent();
            if (ways == null) return;
            bool abreast = ways.GetComponent<HorizontalLayoutGroup>() != null;
            foreach (Transform child in ways)
            {
                var element = child.GetComponent<LayoutElement>();
                if (element == null) continue;
                element.minHeight = WaysOnHeight * FontScale;
                if (abreast) { element.preferredWidth = 0f; element.flexibleWidth = 1f; }
                var caption = child.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.transform.parent == child);
                if (caption != null) caption.fontSize = Mathf.RoundToInt(WaysOnCaptionSize * FontScale);
            }
        }
    }
}
