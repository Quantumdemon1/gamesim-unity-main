using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The finale's vote: the two finalists, the jury read one juror at a time, and the winner.
    ///
    /// <para>The engine decides the season in one commit - every juror's ballot, the count and the
    /// winner - and the port used to announce it with a card that named the winner at once. This
    /// spends it the way the reference does (<c>JuryVoteReveal.tsx</c>): "{n} votes to win", each
    /// juror's vote put under their face in turn, and confetti the moment a finalist has enough.</para>
    ///
    /// <para><b>Three changes, all toward honesty.</b> The jurors are read in an order dealt from the
    /// seed, as the keys are, rather than in the cast order the reference reads them in (its own
    /// comment: "could shuffle for drama"). A pause comes before any vote that <i>could</i> decide
    /// it, whoever it names, so the pause gives nothing away. And the count the card ends on is the
    /// jury's whole count: the reference stops reading at the deciding vote and reports the partial
    /// tally as the result; here the rest are put up with the winner, and the host reads the real
    /// figures. A tie is called as a tie and settled by the house's tie rule, as the engine settles it.</para>
    ///
    /// <para>Same rules as the other reveals: read from committed state, no raycasts, a timer rather
    /// than acknowledgement, and the key ceremony's controls to speed it up or skip to the result.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JuryReveal : MonoBehaviour
    {
        private const float ReadDelay = 0.35f;

        /// <summary>
        /// The least room left between the card's glass and each edge of the canvas when the card is
        /// fitted to it: a little more than the glass's halo (<see cref="UiTheme.GlowWidth"/>), so the
        /// halo stays whole too.
        /// </summary>
        private const float EdgeRoom = 12f;

        /// <summary>The show's name, as the host says it at the end.</summary>
        public const string ShowName = "Gamesim: The House";

        /// <summary>
        /// The runner-up's steel, as the season report wears it: not the gold of the win, and not
        /// either finalist's own tint, which the chips carry until the result.
        /// </summary>
        private static readonly Color Steel = new Color(.68f, .77f, .86f);

        /// <summary>
        /// The small line over the count at the result (MOCKUP-PASS-PLAN M4): "BY A VOTE OF" over
        /// "5 TO 2", a split jury called as a tie, and a jury of one said as the one vote it is.
        /// Public so the winner's screen can say it the same way.
        /// </summary>
        public static string CountEyebrow(int forWinner, int forOther) =>
            forWinner + forOther == 1 ? "WITH THE JURY'S ONLY VOTE"
            : forWinner == forOther ? "THE JURY IS TIED"
            : "BY A VOTE OF";

        /// <summary>The jury's whole count as the headline says it, the winner's votes first: "5 TO 2".</summary>
        public static string CountLine(int forWinner, int forOther) => forWinner + " TO " + forOther;

        /// <summary>A finalist, as the card needs them.</summary>
        public readonly struct Finalist
        {
            public readonly string Id, Name;
            public readonly Texture Portrait;
            public readonly ContestantState Character;
            public readonly bool IsPlayer;

            public Finalist(string id, string name, Texture portrait, ContestantState character = null, bool isPlayer = false)
            {
                Id = id; Name = name; Portrait = portrait; Character = character?.Clone(); IsPlayer = isPlayer;
            }
        }

        /// <summary>One juror and the finalist they voted for.</summary>
        public readonly struct Juror
        {
            public readonly string Id, Name, TargetId;
            public readonly Texture Portrait;
            public readonly ContestantState Character;

            public Juror(string id, string name, string targetId, Texture portrait = null, ContestantState character = null)
            {
                Id = id; Name = name; TargetId = targetId; Portrait = portrait; Character = character?.Clone();
            }
        }

        /// <summary>The beat's sounds, for the director to play: a vote's for each juror read, and the finale's for the winner.</summary>
        public event System.Action<HouseAudio.Cue> CueRequested;

        private CanvasGroup group;
        private RectTransform column, scrim, jurorRow, banner;
        private TMP_Text eyebrow, title, progress, countEyebrow, countLine, host, bannerText, controls, speedMark;
        private readonly List<TMP_Text> counts = new List<TMP_Text>(), chipTexts = new List<TMP_Text>();
        private readonly List<RectTransform> rims = new List<RectTransform>(), jurorRims = new List<RectTransform>(), chips = new List<RectTransform>();
        private readonly List<float> readAt = new List<float>();
        private List<Finalist> finalists = new List<Finalist>();
        private List<Juror> jurors = new List<Juror>();
        private ConfettiBurst confetti;
        private string winnerId;
        private int majority, decidedAt = -1, shown = -1, seed;
        private CeremonyPace pace = CeremonyPace.Suspenseful;
        private float elapsed, upFor, speed = 1f;
        private bool playing, reduced, usingPad, tied, resultShown;
        // The card's whole size with its glass, at full size, and the canvas size it was last fitted to.
        private Vector2 cardExtent, fittedTo;

        public float FontScale { get; set; } = 1f;
        public bool IsPlaying => playing;
        public bool ShowingResult => resultShown;
        public CeremonyPace Pace => pace;
        public float SpeedMultiplier => speed;

        /// <summary>How many jurors' votes are on the board.</summary>
        public int VotesShown => Mathf.Max(0, shown);

        /// <summary>How many votes win it: more than half the jury.</summary>
        public int Majority => majority;

        /// <summary>The count at which a finalist reached a majority, or 0 for a tied jury.</summary>
        public int DecidingVote => decidedAt + 1;

        /// <summary>Whether the reveal is holding a beat before the next vote, because it could decide it.</summary>
        public bool HoldingForDecidingVote { get; private set; }

        /// <summary>Whether confetti is in the air.</summary>
        public bool Celebrating => confetti != null && confetti.Throwing;

        /// <summary>The whole reveal at its own speed: the fade, the finalists, each vote and its beats, the winner, and the fade out.</summary>
        public float Duration => ResultAt + CeremonyPacing.WinnerHold(pace) + CeremonyPacing.FadeOut;

        private float RevealStart => CeremonyPacing.FadeIn + CeremonyPacing.JuryIntro(pace);
        private float Hold => CeremonyPacing.PerJuror(pace, jurors.Count);

        /// <summary>When the winner is called: a hold after the deciding vote, or after a tie has been called.</summary>
        private float ResultAt
        {
            get
            {
                if (jurors.Count == 0) return RevealStart;
                if (!tied) return readAt[decidedAt] + Hold;
                return readAt[jurors.Count - 1] + Hold + CeremonyPacing.TieBeat(pace);
            }
        }

        public static JuryReveal Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Jury Reveal",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // With the other reveals, over the takeover's 100.
            canvas.sortingOrder = 110;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            return root.AddComponent<JuryReveal>();
        }

        /// <summary>
        /// Plays the reveal. Needs exactly two finalists, a jury of at least one, and a winner who is
        /// one of the finalists; anything else is declined, and the caller's generic card plays. The
        /// jurors are read in the order given.
        /// </summary>
        public bool Play(IList<Finalist> pair, IList<Juror> jury, string winner, bool reducedMotion,
            CeremonyPace pace = CeremonyPace.Suspenseful, int seed = 0)
        {
            if (pair == null || pair.Count != 2 || jury == null || jury.Count == 0) return false;
            if (winner != pair[0].Id && winner != pair[1].Id) return false;
            foreach (var juror in jury) if (juror.TargetId != pair[0].Id && juror.TargetId != pair[1].Id) return false;

            finalists = new List<Finalist>(pair);
            jurors = new List<Juror>(jury);
            winnerId = winner;
            this.pace = pace;
            this.seed = seed;
            majority = jurors.Count / 2 + 1;
            Schedule();

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            upFor = 0f;
            shown = -1;
            resultShown = false;
            HoldingForDecidingVote = false;
            SetSpeed(1f);
            playing = true;

            eyebrow.text = "THE FINALE";
            banner.gameObject.SetActive(false);
            host.text = string.Empty;
            Board(0, true);

            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            group.alpha = reduced ? 1f : 0f;
            return true;
        }

        public void Cancel()
        {
            playing = false;
            if (confetti != null) confetti.Clear();
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
        }

        /// <summary>
        /// When each vote is read, and which one decides it. A beat comes before any vote with a
        /// finalist a vote from winning - wherever that vote goes - so it never tells.
        /// </summary>
        private void Schedule()
        {
            readAt.Clear();
            decidedAt = -1;
            tied = false;
            int first = 0, second = 0;
            float t = RevealStart;
            for (int i = 0; i < jurors.Count; i++)
            {
                if (i > 0) t += Hold;
                if (first == majority - 1 || second == majority - 1) t += CeremonyPacing.DecidingBeat(pace);
                readAt.Add(t);
                if (jurors[i].TargetId == finalists[0].Id) first++; else second++;
                if (decidedAt < 0 && (first >= majority || second >= majority)) decidedAt = i;
            }
            tied = decidedAt < 0;
        }

        private bool Dismissable => upFor >= CeremonyPacing.FadeIn + ReadDelay;

        private void Update()
        {
            if (!playing) return;
            CeremonyOverlays.Showing();
            upFor += Time.unscaledDeltaTime;
            FollowDevice();
            FitToCanvas();

            if (Dismissable && CeremonyTakeover.SpeedPressed()) SetSpeed(speed > 1f ? 1f : CeremonyPacing.SpeedUp);
            if (Dismissable && CeremonyTakeover.SkipPressed())
            {
                if (resultShown) { Cancel(); return; }
                elapsed = Mathf.Max(elapsed, ResultAt);
                Result();
                return;
            }

            elapsed += Time.unscaledDeltaTime * speed;
            float end = ResultAt + CeremonyPacing.WinnerHold(pace);
            if (reduced) group.alpha = 1f;
            else if (elapsed < CeremonyPacing.FadeIn) group.alpha = Eased(elapsed / CeremonyPacing.FadeIn);
            else if (elapsed < end) group.alpha = 1f;
            else
            {
                float exit = (elapsed - end) / CeremonyPacing.FadeOut;
                if (exit >= 1f) { Cancel(); return; }
                group.alpha = 1f - Eased(exit);
            }
            if (reduced && elapsed >= Duration) { Cancel(); return; }

            if (!resultShown)
            {
                int due = VotesDue(elapsed);
                if (due != shown) Board(due, false);
                // In the beat before a vote that could decide it: past the last vote's hold, short of the next.
                HoldingForDecidingVote = due < jurors.Count && Leading(due) == majority - 1
                    && elapsed < readAt[due] && elapsed >= (due == 0 ? RevealStart : readAt[due - 1] + Hold);
                if (tied && due == jurors.Count && elapsed >= readAt[jurors.Count - 1] + Hold && progress.text != TieLine())
                    progress.text = TieLine();
                if (elapsed >= ResultAt) Result();
            }
        }

        private static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>How many votes are on the board at <paramref name="t"/>: those whose time has come, never past the deciding vote until the winner is called.</summary>
        private int VotesDue(float t)
        {
            int due = 0;
            while (due < readAt.Count && t >= readAt[due]) due++;
            return tied ? due : Mathf.Min(due, decidedAt + 1);
        }

        /// <summary>The higher of the two tallies after the first <paramref name="count"/> votes.</summary>
        private int Leading(int count) => Mathf.Max(Votes(finalists[0].Id, count), Votes(finalists[1].Id, count));

        private int Votes(string id, int count)
        {
            int votes = 0;
            for (int i = 0; i < count && i < jurors.Count; i++) if (jurors[i].TargetId == id) votes++;
            return votes;
        }

        private void SetSpeed(float value)
        {
            speed = value;
            if (speedMark != null) speedMark.gameObject.SetActive(speed > 1f);
        }

        /// <summary>
        /// Shrinks the card, glass and all, to a canvas too short or too narrow to hold it. The
        /// canvas keeps about 1600 by 900's area whatever the screen's shape, so a screen wider than
        /// 16:9 leaves it less height - about 780 units at 21:9 - and at large text, with a full jury
        /// and the count's headline, the card wants about 900. Every label shrinks with its box, so
        /// each still draws; the scrim and the confetti keep the whole canvas. Checked each frame,
        /// because the window can change shape during the reveal, and because a canvas attached this
        /// frame is only sized by its scaler as it first draws.
        /// </summary>
        private void FitToCanvas()
        {
            if (column == null) return;
            var room = ((RectTransform)transform).rect.size;
            if (room.x <= 0f || room.y <= 0f || room == fittedTo) return;
            fittedTo = room;
            float fit = Mathf.Min(1f,
                Mathf.Max(1f, room.x - 2f * EdgeRoom) / cardExtent.x,
                Mathf.Max(1f, room.y - 2f * EdgeRoom) / cardExtent.y);
            column.localScale = new Vector3(fit, fit, 1f);
        }

        private void FollowDevice()
        {
            var pad = CeremonyTakeover.PadUsed();
            if (!pad.HasValue || pad.Value == usingPad) return;
            usingPad = pad.Value;
            if (controls != null) controls.text = CeremonyTakeover.ControlsFor(usingPad);
        }

        private void Raise(HouseAudio.Cue cue) => CueRequested?.Invoke(cue);

        /// <summary>Puts the first <paramref name="count"/> votes on the board: a chip under each juror read, and the tallies.</summary>
        private void Board(int count, bool silent)
        {
            int before = Mathf.Max(0, shown);
            shown = count;
            if (!silent) for (int i = before; i < count; i++) Raise(HouseAudio.Cue.Vote);
            for (int i = 0; i < finalists.Count; i++) counts[i].text = Votes(finalists[i].Id, count).ToString();
            for (int i = 0; i < jurors.Count; i++)
            {
                chips[i].gameObject.SetActive(i < count);
                // The next face to be read, ringed in gold as the reference rings it.
                jurorRims[i].GetComponent<Image>().color = i == count && !resultShown ? UiTheme.Gold : UiTheme.Outline;
            }
            progress.text = count == 0 ? majority + (majority == 1 ? " vote" : " votes") + " to win"
                : Leading(count) == majority - 1 && count < jurors.Count ? "One vote could decide it"
                : "Reading vote " + count + " of " + jurors.Count;
        }

        private string TieLine() =>
            "The jury is tied, " + Votes(finalists[0].Id, jurors.Count) + " to " + Votes(finalists[1].Id, jurors.Count) + ".";

        /// <summary>The winner: every vote on the board, the real count, the host's line and the confetti.</summary>
        private void Result()
        {
            if (resultShown) return;
            HoldingForDecidingVote = false;
            Board(jurors.Count, true);
            resultShown = true;
            foreach (var rim in jurorRims) rim.GetComponent<Image>().color = UiTheme.Outline;
            var winner = finalists.Find(f => f.Id == winnerId);
            banner.gameObject.SetActive(true);
            bannerText.text = (winner.IsPlayer ? "YOU" : winner.Name.ToUpperInvariant()) + "  ·  WINNER";
            for (int i = 0; i < finalists.Count; i++)
            {
                bool won = finalists[i].Id == winnerId;
                rims[i].GetComponent<Image>().color = won ? UiTheme.Gold : UiTheme.Outline;
                counts[i].color = won ? UiTheme.Gold : UiTheme.Muted;
            }
            // Each vote in the colour of how it went: gold for the winner's, steel for the other's.
            // Only now: until the result a chip wears its finalist's own tint, which says nothing
            // about who wins.
            for (int i = 0; i < jurors.Count; i++)
            {
                var tint = jurors[i].TargetId == winnerId ? UiTheme.Gold : Steel;
                chips[i].GetComponent<Image>().color = tint;
                chipTexts[i].color = UiTheme.OnColor(tint);
            }
            progress.text = tied ? TieLine() : "All votes are in";
            // The count as a headline over the host's line, the winner's votes first.
            string otherId = finalists[0].Id == winnerId ? finalists[1].Id : finalists[0].Id;
            int forWinner = Votes(winnerId, jurors.Count), forOther = Votes(otherId, jurors.Count);
            countEyebrow.text = CountEyebrow(forWinner, forOther);
            countLine.text = CountLine(forWinner, forOther);
            countEyebrow.gameObject.SetActive(true);
            countLine.gameObject.SetActive(true);
            host.text = Verdict(winner);
            Raise(HouseAudio.Cue.Finale);
            if (!reduced && confetti != null) confetti.Throw(seed);
        }

        /// <summary>The host's line: the jury's whole count and the name, or the tie and the house's rule.</summary>
        private string Verdict(Finalist winner)
        {
            string crowned = winner.IsPlayer ? "you are the winner of " + ShowName + "!"
                : winner.Name + ", you are the winner of " + ShowName + "!";
            if (tied) return "Under the house's tie rule, the win goes to " + (winner.IsPlayer ? "you" : winner.Name) + ".";
            if (jurors.Count == 1) return "With the jury's only vote, " + crowned;
            string otherId = finalists[0].Id == winnerId ? finalists[1].Id : finalists[0].Id;
            return "By a vote of " + Votes(winnerId, jurors.Count) + " to " + Votes(otherId, jurors.Count) + ", " + crowned;
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            if (column != null)
            {
                foreach (Transform child in column) Destroy(child.gameObject);
                counts.Clear(); rims.Clear(); jurorRims.Clear(); chips.Clear(); chipTexts.Clear();
            }
            else
            {
                group = GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                scrim = HudPrimitives.Fill("Scrim", root, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.6f), 1);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;
                HudPrimitives.Vignette(scrim);

                column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
                column.SetParent(root, false);
                column.anchorMin = column.anchorMax = column.pivot = new Vector2(.5f, .5f);

                // Over the card, so it falls in front of the faces.
                confetti = ConfettiBurst.Attach(root);
            }

            float scale = Mathf.Max(0.5f, FontScale);
            float portrait = 110f * scale;
            float face = Mathf.Min(52f, 760f / Mathf.Max(1, jurors.Count) - 10f) * scale;
            float step = face + 12f * scale;
            // Grown by the count's headline at the result (64 units), which sits between the
            // progress line and the host's.
            column.sizeDelta = new Vector2(900f * scale, (100f + portrait + 150f + face + 254f) * scale);
            var glass = HudPrimitives.Fill("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            glass.SetAsFirstSibling();
            glass.anchorMin = Vector2.zero; glass.anchorMax = Vector2.one;
            glass.offsetMin = new Vector2(-34f * scale, -26f * scale); glass.offsetMax = new Vector2(34f * scale, 26f * scale);
            UiTheme.Glass(glass, UiTheme.GlassRadius);
            cardExtent = column.sizeDelta + new Vector2(68f, 52f) * scale;

            eyebrow = HudPrimitives.Label("Eyebrow", column, 15f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 14f;
            Place(eyebrow.rectTransform, 900f * scale, 22f * scale, 0f);

            speedMark = HudPrimitives.Label("Speed", column, 14f * scale, UiTheme.Glow, TextAlignmentOptions.Right);
            speedMark.text = CeremonyTakeover.SpeedCaption;
            Place(speedMark.rectTransform, 170f * scale, 22f * scale, 0f, 355f * scale);
            speedMark.gameObject.SetActive(false);

            title = HudPrimitives.Label("Title", column, 48f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            title.text = "THE FINAL VOTE";
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 2f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Color.white, Color.white, UiTheme.Gold, UiTheme.Gold);
            Place(title.rectTransform, 900f * scale, 60f * scale, -26f * scale);

            float slot = 320f * scale;
            for (int i = 0; i < finalists.Count; i++)
            {
                float x = (i == 0 ? -1f : 1f) * slot * 0.5f;
                var rim = HudPrimitives.Portrait(column, finalists[i].Portrait, Tint(i), portrait, 4f * scale, false, finalists[i].Character);
                rim.anchorMin = rim.anchorMax = rim.pivot = new Vector2(.5f, 1f);
                rim.anchoredPosition = new Vector2(x, -100f * scale);
                rims.Add(rim);

                var name = HudPrimitives.Label("Finalist", column, 19f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
                name.text = HudPrimitives.WithYou(finalists[i].Name, finalists[i].IsPlayer);
                Place(name.rectTransform, slot, 24f * scale, -(100f + portrait + 14f) * scale, x);

                var count = HudPrimitives.Label("Votes", column, 58f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
                count.text = "0";
                // Inter's line is 1.21 of its size: a 58-point figure needs more than a 66 box.
                Place(count.rectTransform, slot, 72f * scale, -(100f + portrait + 36f) * scale, x);
                counts.Add(count);
            }

            // The jury, a face each, read left to right in the order dealt.
            jurorRow = new GameObject("Jury", typeof(RectTransform)).GetComponent<RectTransform>();
            jurorRow.SetParent(column, false);
            Place(jurorRow, 900f * scale, face + 60f * scale, -(100f + portrait + 120f) * scale);
            float left = -(jurors.Count - 1) * step * 0.5f;
            for (int i = 0; i < jurors.Count; i++)
            {
                float x = left + i * step;
                var rim = HudPrimitives.Portrait(jurorRow, jurors[i].Portrait, UiTheme.Outline, face, 3f * scale, false, jurors[i].Character);
                rim.anchorMin = rim.anchorMax = rim.pivot = new Vector2(.5f, 1f);
                rim.anchoredPosition = new Vector2(x, 0f);
                jurorRims.Add(rim);

                var name = HudPrimitives.Label("Juror", jurorRow, 11f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
                name.text = FirstName(jurors[i].Name);
                name.enableAutoSizing = true; name.fontSizeMax = name.fontSize; name.fontSizeMin = 8f * scale;
                // At 1.3 times the text's size: Inter draws nothing in a box under 1.21 of it.
                Place(name.rectTransform, step, 16f * scale, -(face + 8f * scale), x);

                int finalist = jurors[i].TargetId == finalists[0].Id ? 0 : 1;
                var chip = HudPrimitives.Fill("Vote", jurorRow, Tint(finalist), UiTheme.ControlRadius);
                Place(chip, step - 4f * scale, 20f * scale, -(face + 26f * scale), x);
                var chipText = HudPrimitives.Label("Vote for", chip, 11f * scale, UiTheme.OnColor(Tint(finalist)), TextAlignmentOptions.Center);
                if (bold != null) chipText.font = bold;
                chipText.text = FirstName(finalists[finalist].Name);
                chipText.enableAutoSizing = true; chipText.fontSizeMax = chipText.fontSize; chipText.fontSizeMin = 7f * scale;
                chipText.rectTransform.anchorMin = Vector2.zero; chipText.rectTransform.anchorMax = Vector2.one;
                chipText.rectTransform.offsetMin = Vector2.zero; chipText.rectTransform.offsetMax = Vector2.zero;
                chip.gameObject.SetActive(false);
                chips.Add(chip);
                chipTexts.Add(chipText);
            }

            progress = HudPrimitives.Label("Progress", column, 16f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, 900f * scale, 22f * scale, -(100f + portrait + 130f + face + 64f) * scale);

            // The count as a headline, "BY A VOTE OF" over "5 TO 2" (MOCKUP-PASS-PLAN M4). Both
            // wait for the result: before it they would tell. Each box is at least 1.3 times its
            // text's size, since Inter draws nothing in a box under 1.21 of it.
            countEyebrow = HudPrimitives.Label("Count eyebrow", column, 13f * scale, UiTheme.Gold, TextAlignmentOptions.Center);
            countEyebrow.characterSpacing = 12f;
            Place(countEyebrow.rectTransform, 900f * scale, 18f * scale, -(100f + portrait + 130f + face + 88f) * scale);
            countEyebrow.gameObject.SetActive(false);

            countLine = HudPrimitives.Label("Count", column, 36f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            if (bold != null) countLine.font = bold;
            countLine.characterSpacing = 2f;
            countLine.enableVertexGradient = true;
            countLine.colorGradient = new VertexGradient(Color.white, Color.white, UiTheme.Gold, UiTheme.Gold);
            Place(countLine.rectTransform, 900f * scale, 48f * scale, -(100f + portrait + 130f + face + 106f) * scale);
            countLine.gameObject.SetActive(false);

            host = HudPrimitives.Label("Host", column, 18f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) host.font = medium;
            host.enableAutoSizing = true; host.fontSizeMax = host.fontSize; host.fontSizeMin = 12f * scale;
            Place(host.rectTransform, 900f * scale, 26f * scale, -(100f + portrait + 130f + face + 156f) * scale);

            banner = HudPrimitives.Fill("Result banner", column, UiTheme.Gold, UiTheme.ControlRadius);
            Place(banner, 560f * scale, 44f * scale, -(100f + portrait + 130f + face + 190f) * scale);
            bannerText = HudPrimitives.Label("Result", banner, 22f * scale, UiTheme.Ink, TextAlignmentOptions.Center);
            if (bold != null) bannerText.font = bold;
            bannerText.rectTransform.anchorMin = Vector2.zero; bannerText.rectTransform.anchorMax = Vector2.one;
            bannerText.rectTransform.offsetMin = Vector2.zero; bannerText.rectTransform.offsetMax = Vector2.zero;
            banner.gameObject.SetActive(false);

            controls = HudPrimitives.Label("Controls", column, 12f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            controls.text = CeremonyTakeover.ControlsFor(usingPad);
            Place(controls.rectTransform, 900f * scale, 18f * scale, -(100f + portrait + 130f + face + 244f) * scale);

            // Fitted afresh: this jury and this text size make a card of their own size.
            column.localScale = Vector3.one;
            fittedTo = Vector2.zero;
            FitToCanvas();
        }

        /// <summary>Each finalist's colour, carried by their votes' chips: the reference's blue and red, in the house's tones.</summary>
        private static Color Tint(int finalist) => finalist == 0 ? UiTheme.Accent : UiTheme.Flirt;

        private static string FirstName(string name) =>
            string.IsNullOrEmpty(name) ? string.Empty : name.Split(' ')[0];

        private static void Place(RectTransform rect, float width, float height, float y, float x = 0f)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
