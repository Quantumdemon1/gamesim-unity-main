using Gamesim.Simulation;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The live eviction: two nominees, the votes revealed one at a time, and the result.
    ///
    /// <para>The eviction is the only beat in the week whose outcome the player cannot already
    /// infer, and the simulation commits the whole thing in a single frame — every vote, the tally
    /// and the eviction, all at once. Reporting that as one sentence in the status line threw away
    /// the only suspense the format has. This spends it: the counts climb one vote at a time from a
    /// result that is already decided and already saved.</para>
    ///
    /// <para>Nothing here is a decision. The votes are read from committed state, so the reveal
    /// cannot disagree with the save, and skipping it — by reloading, or by the card being cancelled
    /// for a scene change — costs a presentation and never an outcome.</para>
    ///
    /// <para>It tells the count the way the format does. The figures are the house's votes and
    /// nothing else: the Head of Household votes only to break a tie, the engine leaves that vote out
    /// of its tally, and a card that counted it with the house's drew a 2-2 tie as 3-2. A tie is
    /// announced as a tie, the deciding vote is its own mark, and nothing on the card hints at either
    /// before the house's last vote is read. It is paced like the key ceremony
    /// (<see cref="CeremonyPacing"/>), with a beat before the house's last vote, and it can be sped
    /// up or skipped to the result the same way.</para>
    ///
    /// <para>Same two rules as the other overlays: no <see cref="GraphicRaycaster"/> and no
    /// raycasting graphic, and it ends on a timer rather than on acknowledgement, so an automated
    /// season is never held up behind it. The presses that speed it up and skip it are read straight
    /// off the devices.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoteReveal : MonoBehaviour
    {
        /// <summary>
        /// How long past its fade the card must have been up, in real seconds, before a press moves it
        /// on - the key ceremony's delay, on the wall clock so a sped-up count does not shorten it.
        /// </summary>
        private const float ReadDelay = 0.35f;

        /// <summary>One committed ballot, as the card needs it.</summary>
        public readonly struct Ballot
        {
            public readonly string VoterName;
            public readonly string TargetId;

            /// <summary>
            /// The Head of Household's deciding vote in a tie. It is not one of the house's votes: the
            /// engine casts it only on a tie and leaves it out of the tally, and so does the card.
            /// </summary>
            public readonly bool TieBreak;

            public Ballot(string voterName, string targetId, bool tieBreak = false)
            {
                VoterName = voterName; TargetId = targetId; TieBreak = tieBreak;
            }
        }

        /// <summary>A nominee on the block.</summary>
        public readonly struct Nominee
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Texture Portrait;
            public readonly ContestantState Character;

            public Nominee(string id, string name, Texture portrait, ContestantState character = null)
            {
                Id = id; Name = name; Portrait = portrait; Character = character?.Clone();
            }
        }

        /// <summary>
        /// A sound the reveal wants played as it reaches a beat: the vote's sound for each ballot put on
        /// the board - the Head of Household's included - and the eviction's when the result is read.
        /// The card makes no sound of its own; the director plays these, so the result is not heard
        /// before the count reaches it. A skip asks only for the result's.
        /// </summary>
        public event System.Action<HouseAudio.Cue> CueRequested;

        private CanvasGroup group;
        private RectTransform column, scrim, dotRow, banner, tiePip, tieMark;
        private TMP_Text eyebrow, title, progress, bannerText, host, controls, speedMark;
        private readonly List<RectTransform> dots = new List<RectTransform>();
        private readonly List<TMP_Text> counts = new List<TMP_Text>();
        private readonly List<RectTransform> rims = new List<RectTransform>();
        private List<Nominee> nominees = new List<Nominee>();
        private List<Ballot> house = new List<Ballot>();
        private Ballot? tieBreak;
        private string evictedId, evictedName, hohName;
        private CeremonyPace pace = CeremonyPace.Suspenseful;
        private float elapsed, upFor, speed = 1f;
        private int shown = -1;
        private bool playing, reduced, hohIsPlayer, evictedIsPlayer, lastVotePending, tieCalled, tieBroken, usingPad;

        public float FontScale { get; set; } = 1f;
        public bool IsPlaying => playing;

        /// <summary>True once every ballot is on the board and the result banner is up.</summary>
        public bool ShowingResult => banner != null && banner.gameObject.activeSelf;

        /// <summary>The pace the reveal was played at.</summary>
        public CeremonyPace Pace => pace;

        /// <summary>How fast the card's clock runs: 1, or <see cref="CeremonyPacing.SpeedUp"/> while the player has sped it up.</summary>
        public float SpeedMultiplier => speed;

        /// <summary>How many ballots are on the board: the house's so far, and the Head of Household's once it is cast.</summary>
        public int VotesShown => Mathf.Max(0, shown) + (tieBroken ? 1 : 0);

        /// <summary>
        /// How long the reveal takes at its own speed, start to finish: the fade, the block, a hold on
        /// each of the house's votes, the beat before the last of them, the tie and the vote that
        /// breaks it when there is one, the result, and the fade out.
        /// </summary>
        public float Duration => ResultAt + CeremonyPacing.ResultHold(pace) + CeremonyPacing.FadeOut;

        /// <summary>When the house's first vote is read, on the card's clock.</summary>
        private float RevealStart => CeremonyPacing.FadeIn + CeremonyPacing.VoteIntro(pace);

        /// <summary>How long each vote holds before the next.</summary>
        private float VoteHold => CeremonyPacing.PerVote(pace, house.Count);

        /// <summary>The extra wait before the house's last vote; none when the house cast none.</summary>
        private float LastVoteWait => house.Count > 0 ? CeremonyPacing.LastVoteBeat(pace) : 0f;

        /// <summary>When the beat before the house's last vote begins: the vote before it has had its hold.</summary>
        private float LastVoteCalled => RevealStart + (house.Count - 1) * VoteHold;

        /// <summary>When the house's count is complete: its last vote has had its hold too.</summary>
        private float HouseEnd => RevealStart + house.Count * VoteHold + LastVoteWait;

        /// <summary>When the Head of Household's vote is cast, after the tie has been announced.</summary>
        private float TieBreakAt => HouseEnd + CeremonyPacing.TieBeat(pace);

        /// <summary>When the result is read: after the house's count, or after the deciding vote has had a vote's hold.</summary>
        private float ResultAt => tieBreak.HasValue ? TieBreakAt + VoteHold : HouseEnd;

        public static VoteReveal Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Vote Reveal",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            if (owner != null && owner.scene.IsValid()) SceneManager.MoveGameObjectToScene(root, owner.scene);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the takeover's 100: the eviction replaces the generic ceremony card rather than
            // stacking with it.
            canvas.sortingOrder = 110;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return root.AddComponent<VoteReveal>();
        }

        /// <summary>
        /// Plays the reveal at <paramref name="pace"/>. Needs exactly two nominees and at least one
        /// ballot; anything else is a shape this card cannot narrate, and it declines rather than
        /// drawing a broken tally. <paramref name="hohName"/> is who breaks a tie, and the two flags
        /// say when the Head of Household or the evicted houseguest is the player, who is spoken to
        /// rather than named.
        /// </summary>
        public bool Play(int week, IList<Nominee> block, IList<Ballot> votes, string evicted, bool reducedMotion,
            CeremonyPace pace = CeremonyPace.Suspenseful, string hohName = null, bool hohIsPlayer = false,
            bool evictedIsPlayer = false)
        {
            if (block == null || block.Count != 2 || votes == null || votes.Count == 0) return false;

            nominees = new List<Nominee>(block);
            // Reveal in a stable order, and never one that leaks the result: committed order is the
            // order the house voted, which is what a broadcast shows. The Head of Household's vote is
            // held back from the house's for the tie it breaks, wherever it sits in the list.
            house = new List<Ballot>();
            tieBreak = null;
            foreach (var ballot in votes)
            {
                if (!ballot.TieBreak) house.Add(ballot);
                else if (!tieBreak.HasValue) tieBreak = ballot;
            }
            evictedId = evicted;
            evictedName = nominees.Find(n => n.Id == evicted).Name;
            this.pace = pace;
            this.hohName = hohName;
            this.hohIsPlayer = hohIsPlayer;
            this.evictedIsPlayer = evictedIsPlayer;

            Build();
            reduced = reducedMotion;
            elapsed = 0f;
            upFor = 0f;
            shown = -1;
            lastVotePending = false;
            tieCalled = false;
            tieBroken = false;
            // Every reveal starts at its own pace; speeding one up is for that one.
            SetSpeed(1f);
            playing = true;

            eyebrow.text = "WEEK " + Mathf.Max(1, week);
            banner.gameObject.SetActive(false);
            host.text = string.Empty;
            Tally(0, false, false);

            column.gameObject.SetActive(true);
            scrim.gameObject.SetActive(true);
            group.alpha = reduced ? 1f : 0f;
            return true;
        }

        public void Cancel()
        {
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
        }

        /// <summary>
        /// True once the card has been up long enough to have been read; after that a press moves it
        /// on. A press already in flight when the card appears - the Enter that cast the last ballot -
        /// must not skip a count nobody has seen.
        /// </summary>
        private bool Dismissable => upFor >= CeremonyPacing.FadeIn + ReadDelay;

        private void Update()
        {
            if (!playing) return;
            // The house's click guard, which the key ceremony and the takeover already kept. This card
            // never stamped it, so a click on the tally was a click on the floor behind it as well.
            CeremonyOverlays.Showing();
            upFor += Time.unscaledDeltaTime;
            FollowDevice();

            // The key ceremony's controls, read off the devices for the same reason: speed the count
            // up or back to its own pace, skip straight to the result - every vote on the board and
            // the result read, giving up the count's order and never the result - and a skip on the
            // result ends the card.
            if (Dismissable && CeremonyTakeover.SpeedPressed()) SetSpeed(speed > 1f ? 1f : CeremonyPacing.SpeedUp);
            if (Dismissable && CeremonyTakeover.SkipPressed())
            {
                if (ShowingResult) { Cancel(); return; }
                elapsed = Mathf.Max(elapsed, ResultAt);
                Tally(house.Count, false, false);
                if (tieBreak.HasValue) { CallTie(); BreakTie(false); }
                Result();
                return;
            }

            elapsed += Time.unscaledDeltaTime * speed;
            float resultEnd = ResultAt + CeremonyPacing.ResultHold(pace);

            // Fade.
            if (reduced) group.alpha = 1f;
            else if (elapsed < CeremonyPacing.FadeIn) group.alpha = Eased(elapsed / CeremonyPacing.FadeIn);
            else if (elapsed < resultEnd) group.alpha = 1f;
            else
            {
                float exit = (elapsed - resultEnd) / CeremonyPacing.FadeOut;
                if (exit >= 1f) { Cancel(); return; }
                group.alpha = 1f - Eased(exit);
            }

            // Reduced motion keeps the timings: they are reading time, not movement.
            if (reduced && elapsed >= Duration) { Cancel(); return; }

            // How many of the house's ballots are on the board by now, and whether the last of them
            // is being held back.
            int due = VotesDue(elapsed);
            bool pending = LastVotePendingAt(elapsed);
            if (!tieCalled && !ShowingResult && (due != shown || pending != lastVotePending)) Tally(due, pending, true);

            if (tieBreak.HasValue && elapsed >= HouseEnd && !tieCalled) CallTie();
            if (tieBreak.HasValue && elapsed >= TieBreakAt && !tieBroken) BreakTie(true);
            if (elapsed >= ResultAt && !ShowingResult) Result();
        }

        private static float Eased(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>How many of the house's ballots are on the board at <paramref name="t"/> on the card's clock.</summary>
        private int VotesDue(float t)
        {
            if (house.Count == 0 || t < RevealStart) return 0;
            int due = Mathf.Min(house.Count, Mathf.FloorToInt((t - RevealStart) / VoteHold) + 1);
            // The last vote keeps the others' rhythm and then waits its beat on top of it.
            if (due == house.Count && t < LastVoteCalled + LastVoteWait) due--;
            return due;
        }

        /// <summary>Whether <paramref name="t"/> falls in the beat before the house's last vote.</summary>
        private bool LastVotePendingAt(float t) =>
            house.Count > 0 && LastVoteWait > 0f && t >= LastVoteCalled && t < LastVoteCalled + LastVoteWait;

        /// <summary>Runs the card's clock at <paramref name="value"/>, and says so in the corner while it is fast.</summary>
        private void SetSpeed(float value)
        {
            speed = value;
            if (speedMark != null) speedMark.gameObject.SetActive(speed > 1f);
        }

        /// <summary>The key hints follow the device last used on the card, as the key ceremony's do.</summary>
        private void FollowDevice()
        {
            var pad = CeremonyTakeover.PadUsed();
            if (!pad.HasValue || pad.Value == usingPad) return;
            usingPad = pad.Value;
            if (controls != null) controls.text = CeremonyTakeover.ControlsFor(usingPad);
        }

        private void Raise(HouseAudio.Cue cue) => CueRequested?.Invoke(cue);

        /// <summary>How many of the house's first <paramref name="count"/> ballots name <paramref name="id"/>.</summary>
        private int HouseVotes(string id, int count)
        {
            int votes = 0;
            for (int b = 0; b < count && b < house.Count; b++) if (house[b].TargetId == id) votes++;
            return votes;
        }

        /// <summary>
        /// Redraws the board for the first <paramref name="count"/> of the house's ballots - or, while
        /// <paramref name="pending"/>, with the last of them held back. The figures count the house's
        /// votes and nothing else. <paramref name="announce"/> asks for the vote's sound once for every
        /// ballot newly on the board.
        /// </summary>
        private void Tally(int count, bool pending, bool announce)
        {
            int before = Mathf.Max(0, shown);
            shown = count;
            lastVotePending = pending;
            if (announce) for (int b = before; b < count; b++) Raise(HouseAudio.Cue.Vote);

            for (int i = 0; i < nominees.Count; i++)
                counts[i].text = HouseVotes(nominees[i].Id, count).ToString();

            for (int i = 0; i < dots.Count; i++)
                dots[i].GetComponent<Image>().color = i < count ? UiTheme.Danger
                    : pending && i == dots.Count - 1 ? UiTheme.Gold : UiTheme.Outline;

            progress.text = pending ? "One vote left"
                : count == 0 ? (house.Count == 1 ? "1 vote to reveal" : house.Count + " votes to reveal")
                : "Revealing vote " + count + " of " + house.Count;
        }

        /// <summary>
        /// The tie announced: the house's count, level, and whose vote settles it. Nothing on the card
        /// hinted at this before now - the Head of Household's mark is not drawn until the tie is
        /// called - so a tie is news when it is read.
        /// </summary>
        private void CallTie()
        {
            tieCalled = true;
            int first = HouseVotes(nominees[0].Id, house.Count);
            int second = HouseVotes(nominees[1].Id, house.Count);
            progress.text = "The vote is tied, " + first + " to " + second + ".";
            host.text = hohIsPlayer ? "As Head of Household, you must break the tie."
                : string.IsNullOrEmpty(hohName) ? "The Head of Household must break the tie."
                : "As Head of Household, " + hohName + " must break the tie.";
            if (tiePip != null)
            {
                tiePip.gameObject.SetActive(true);
                tiePip.GetComponent<Image>().color = UiTheme.Outline;
            }
        }

        /// <summary>
        /// The Head of Household's vote, as its own mark: the gold pip set apart from the house's row,
        /// and an "HOH" chip beside the figure of the nominee it names. The figures do not move - it
        /// is not one of the house's votes.
        /// </summary>
        private void BreakTie(bool announce)
        {
            tieBroken = true;
            if (announce) Raise(HouseAudio.Cue.Vote);
            if (tiePip != null)
            {
                tiePip.gameObject.SetActive(true);
                tiePip.GetComponent<Image>().color = UiTheme.Gold;
            }
            if (tieMark != null) tieMark.gameObject.SetActive(true);
            progress.text = hohIsPlayer ? "You have cast the deciding vote."
                : "The Head of Household has cast the deciding vote.";
        }

        /// <summary>The result stage: the block dims apart from whoever is leaving, and the host reads it.</summary>
        private void Result()
        {
            banner.gameObject.SetActive(true);
            bannerText.text = string.IsNullOrEmpty(evictedName)
                ? "The house has voted"
                : evictedName.ToUpperInvariant() + "  ·  EVICTED";

            for (int i = 0; i < nominees.Count; i++)
            {
                bool leaving = nominees[i].Id == evictedId;
                rims[i].GetComponent<Image>().color = leaving ? UiTheme.Danger : UiTheme.Outline;
                counts[i].color = leaving ? UiTheme.Danger : UiTheme.Muted;
            }
            progress.text = "All votes are in";
            host.text = Verdict();
            Raise(HouseAudio.Cue.Eviction);
        }

        /// <summary>
        /// The host's line, the way the format reads it: the house's count and the name, or whose vote
        /// decided it. The count is the house's alone - a tie-break says so instead of adding itself
        /// to it - and an evicted player is spoken to rather than named.
        /// </summary>
        private string Verdict()
        {
            string evictee = evictedIsPlayer ? "you have been evicted."
                : string.IsNullOrEmpty(evictedName) ? null
                : evictedName + ", you have been evicted.";
            if (evictee == null) return "The house has voted.";
            if (tieBreak.HasValue) return "By the Head of Household's tie-breaking vote, " + evictee;
            if (house.Count == 1)
            {
                string voter = string.IsNullOrEmpty(house[0].VoterName) ? "One houseguest" : house[0].VoterName;
                return voter + " cast the sole vote to evict. " + char.ToUpperInvariant(evictee[0]) + evictee.Substring(1);
            }
            string otherId = nominees[0].Id == evictedId ? nominees[1].Id : nominees[0].Id;
            return "By a vote of " + HouseVotes(evictedId, house.Count) + " to " + HouseVotes(otherId, house.Count)
                + ", " + evictee;
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            if (column != null)
            {
                foreach (Transform child in column) Destroy(child.gameObject);
                dots.Clear(); counts.Clear(); rims.Clear();
            }
            else
            {
                group = GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                // Dimmed and vignetted, not blacked out: the eviction is announced in the living
                // room, and the room - with everyone in it reacting - stays behind the tally. The
                // near-opaque scrim this was made the one beat the whole house exists for play in
                // front of a black wall.
                scrim = HudPrimitives.Fill("Scrim", root,
                    new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.55f), 1);
                scrim.anchorMin = Vector2.zero; scrim.anchorMax = Vector2.one;
                scrim.offsetMin = Vector2.zero; scrim.offsetMax = Vector2.zero;
                scrim.GetComponent<Image>().raycastTarget = false;
                HudPrimitives.Vignette(scrim);

                column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
                column.SetParent(root, false);
                column.anchorMin = new Vector2(.5f, .5f);
                column.anchorMax = new Vector2(.5f, .5f);
                column.pivot = new Vector2(.5f, .5f);
            }

            float scale = Mathf.Max(0.5f, FontScale);
            float portrait = 104f * scale;
            // Tall enough for the host's line and the controls under the banner.
            column.sizeDelta = new Vector2(880f * scale, (100f + portrait + 296f) * scale);
            CardGlass(column, scale);

            eyebrow = HudPrimitives.Label("Week", column, 15f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 14f;
            Place(eyebrow.rectTransform, 880f * scale, 22f * scale, 0f);

            // A broadcast's fast-forward bug in the card's corner, up only while the count is sped up.
            speedMark = HudPrimitives.Label("Speed", column, 14f * scale, UiTheme.Glow, TextAlignmentOptions.Right);
            speedMark.text = CeremonyTakeover.SpeedCaption;
            Place(speedMark.rectTransform, 170f * scale, 22f * scale, 0f, 345f * scale);
            speedMark.gameObject.SetActive(false);

            title = HudPrimitives.Label("Title", column, 48f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            title.text = "LIVE EVICTION";
            // The bold cut, lit from above, as the ceremony cards set their titles.
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 2f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Color.white, Color.white, UiTheme.Glow, UiTheme.Glow);
            Place(title.rectTransform, 880f * scale, 60f * scale, -26f * scale);

            // The two columns, with the tally between them.
            float slot = 300f * scale;
            for (int i = 0; i < nominees.Count; i++)
            {
                float x = (i == 0 ? -1f : 1f) * slot * 0.5f;

                var rim = HudPrimitives.Portrait(column, nominees[i].Portrait, UiTheme.Danger, portrait, 4f * scale, false, nominees[i].Character);

                // Both faces here are on the block, so both carry the target the web build uses.
                HudPrimitives.AddRoleMark(rim, HudPrimitives.RoleMark.Nominee, portrait);
                rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
                rim.anchoredPosition = new Vector2(x, -100f * scale);
                rims.Add(rim);

                var name = HudPrimitives.Label("Nominee", column, 19f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
                name.text = nominees[i].Name;
                Place(name.rectTransform, slot, 24f * scale, -(100f + portrait + 14f) * scale, x);

                var count = HudPrimitives.Label("Votes", column, 58f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
                count.text = "0";
                // Taller than the figure's line: Inter's line is 1.21 of its size, and a 58-point
                // figure in a 66 box was truncated whole - the tally counted to nothing on screen.
                Place(count.rectTransform, slot, 72f * scale, -(100f + portrait + 36f) * scale, x);
                counts.Add(count);

                var caption = HudPrimitives.Label("Votes caption", column, 12f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
                caption.text = "VOTES";
                caption.characterSpacing = 8f;
                Place(caption.rectTransform, slot, 18f * scale, -(100f + portrait + 104f) * scale, x);
            }

            // A filled red disc with the word inside it, not red lettering on the ground. The web
            // build makes this the one solid mark between the two faces, and it is what stops the
            // eye reading the pair as a row of portraits rather than as an opposition.
            float versusSize = 62f * scale;
            var versusDisc = HudPrimitives.Disc("Versus disc", column, UiTheme.Danger);
            Place(versusDisc, versusSize, versusSize, -(100f + portrait * 0.4f) * scale);

            var versus = HudPrimitives.Label("Versus", versusDisc, 22f * scale, UiTheme.OnColor(UiTheme.Danger),
                TextAlignmentOptions.Center);
            versus.text = "VS";
            versus.rectTransform.anchorMin = Vector2.zero;
            versus.rectTransform.anchorMax = Vector2.one;
            versus.rectTransform.sizeDelta = Vector2.zero;
            versus.rectTransform.anchoredPosition = Vector2.zero;

            // One pip per ballot the house cast, filling as the votes come in.
            dotRow = new GameObject("Dots", typeof(RectTransform)).GetComponent<RectTransform>();
            dotRow.SetParent(column, false);
            Place(dotRow, 880f * scale, 20f * scale, -(100f + portrait + 130f) * scale);

            float pip = 11f * scale, step = 20f * scale;
            float first = -(house.Count - 1) * step * 0.5f;
            for (int i = 0; i < house.Count; i++)
            {
                var dot = HudPrimitives.Disc("Pip", dotRow, UiTheme.Outline);
                dot.anchorMin = new Vector2(.5f, .5f); dot.anchorMax = new Vector2(.5f, .5f); dot.pivot = new Vector2(.5f, .5f);
                dot.anchoredPosition = new Vector2(first + i * step, 0f);
                dot.sizeDelta = new Vector2(pip, pip);
                dots.Add(dot);
            }

            // The Head of Household's vote, when there is one, is its own mark: a larger gold pip set
            // apart from the house's row, and a chip beside the figure of the nominee it names. Neither
            // is drawn until the tie is called - an empty place waiting at the end of the row, or a
            // row centred around one, would say a tie was coming.
            tiePip = null;
            tieMark = null;
            if (tieBreak.HasValue)
            {
                tiePip = HudPrimitives.Disc("Tie-break pip", dotRow, UiTheme.Outline);
                tiePip.anchorMin = new Vector2(.5f, .5f); tiePip.anchorMax = new Vector2(.5f, .5f); tiePip.pivot = new Vector2(.5f, .5f);
                tiePip.anchoredPosition = new Vector2(first + (house.Count - 1) * step + step * 1.6f, 0f);
                tiePip.sizeDelta = new Vector2(pip * 1.45f, pip * 1.45f);
                tiePip.gameObject.SetActive(false);

                int named = nominees.FindIndex(n => n.Id == tieBreak.Value.TargetId);
                if (named >= 0)
                {
                    float x = (named == 0 ? -1f : 1f) * slot * 0.5f;
                    tieMark = HudPrimitives.Fill("Tie-break vote", column, UiTheme.Gold, UiTheme.ControlRadius);
                    // Beside the figure, level with its middle, clear of a two-digit count.
                    Place(tieMark, 56f * scale, 24f * scale, -(100f + portrait + 60f) * scale, x + 70f * scale);
                    var chip = HudPrimitives.Label("Tie-break", tieMark, 12f * scale, UiTheme.OnColor(UiTheme.Gold),
                        TextAlignmentOptions.Center);
                    if (bold != null) chip.font = bold;
                    chip.text = "HOH";
                    chip.rectTransform.anchorMin = Vector2.zero;
                    chip.rectTransform.anchorMax = Vector2.one;
                    chip.rectTransform.offsetMin = Vector2.zero;
                    chip.rectTransform.offsetMax = Vector2.zero;
                    tieMark.gameObject.SetActive(false);
                }
            }

            progress = HudPrimitives.Label("Progress", column, 16f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, 880f * scale, 22f * scale, -(100f + portrait + 156f) * scale);

            // The host's line: whose vote breaks a tie, and then the result, the way the format reads
            // it out.
            host = HudPrimitives.Label("Host", column, 18f * scale, UiTheme.Paper, TextAlignmentOptions.Center);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) host.font = medium;
            // One line, drawn smaller rather than cut: a long name would wrap the verdict onto a second
            // line the box truncates, and "...Jordan Taylor, you" is not a result.
            host.enableAutoSizing = true; host.fontSizeMax = host.fontSize; host.fontSizeMin = 12f * scale;
            Place(host.rectTransform, 880f * scale, 26f * scale, -(100f + portrait + 184f) * scale);

            banner = HudPrimitives.Fill("Result banner", column, UiTheme.Danger, UiTheme.ControlRadius);
            Place(banner, 560f * scale, 44f * scale, -(100f + portrait + 218f) * scale);
            bannerText = HudPrimitives.Label("Result", banner, 22f * scale, UiTheme.Ink, TextAlignmentOptions.Center);
            bannerText.rectTransform.anchorMin = Vector2.zero;
            bannerText.rectTransform.anchorMax = Vector2.one;
            bannerText.rectTransform.offsetMin = Vector2.zero;
            bannerText.rectTransform.offsetMax = Vector2.zero;
            banner.gameObject.SetActive(false);

            // What the card answers to: the count can be sped up, or skipped to the result. The keys
            // named follow the device last used on the card.
            controls = HudPrimitives.Label("Controls", column, 12f * scale, UiTheme.Muted, TextAlignmentOptions.Center);
            controls.text = CeremonyTakeover.ControlsFor(usingPad);
            Place(controls.rectTransform, 880f * scale, 18f * scale, -(100f + portrait + 272f) * scale);
        }

        /// <summary>
        /// The mockups' glass ground behind the card's column (VISUAL-TARGET.md §4, mockup-08 and
        /// -10): the night background at 85 %, a cyan hairline on the edge and a soft glow outside
        /// it. Built as the column's first child so every piece of the ceremony draws over it, and
        /// stretched to the column so it grows with the large-text preference.
        /// </summary>
        private static RectTransform CardGlass(RectTransform column, float scale)
        {
            var glass = HudPrimitives.Fill("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            glass.SetAsFirstSibling();
            glass.anchorMin = Vector2.zero;
            glass.anchorMax = Vector2.one;
            glass.offsetMin = new Vector2(-34f * scale, -26f * scale);
            glass.offsetMax = new Vector2(34f * scale, 26f * scale);
            UiTheme.Glass(glass, UiTheme.GlassRadius);
            return glass;
        }

        private static void Place(RectTransform rect, float width, float height, float y, float x = 0f)
        {
            rect.anchorMin = new Vector2(.5f, 1f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
