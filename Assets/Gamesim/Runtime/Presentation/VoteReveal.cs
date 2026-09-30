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
    ///
    /// <para>It plays in one of two frames: the HUD's card in the middle of the screen, or - for the
    /// ceremony cut scenes - the living room's own screen (<see cref="ScreenSurface"/>), the same
    /// card at the screen's shape with the two faces and their counts large enough to read from
    /// the sofa. Every child keeps its name in both.</para>
    ///
    /// <para>The screen's frame is a broadcast's vote board as well (MOCKUP-PASS-PLAN M18): the
    /// faces and counts stand at the sides, and between them a roster of the voters fills in a row
    /// a vote, each row naming the nominee that voter evicts. No row says EVICT or KEEP against
    /// whoever is leaving: a row's colour is the side of the nominee it names, fixed when the card
    /// opens, so the board reads the same whoever the result turns out to be. At the result the
    /// board gives way to three lines read like the host's - the count, the name, and that they are
    /// evicted. The HUD's card has none of this and is drawn as it always was.</para>
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
            /// <summary>Who cast it, by id; null where the caller did not say.</summary>
            public readonly string VoterId;
            public readonly string VoterName;
            public readonly string TargetId;

            /// <summary>
            /// The Head of Household's deciding vote in a tie. It is not one of the house's votes: the
            /// engine casts it only on a tie and leaves it out of the tally, and so does the card.
            /// </summary>
            public readonly bool TieBreak;

            /// <summary>
            /// The voter as they look, for their face on the screen's roster; null draws the row's
            /// plain ground where the face would be. The HUD's card draws no voters' faces.
            /// </summary>
            public readonly ContestantState Character;

            public Ballot(string voterName, string targetId, bool tieBreak = false)
                : this(null, voterName, targetId, tieBreak, null) { }

            public Ballot(string voterId, string voterName, string targetId, bool tieBreak, ContestantState character)
            {
                VoterId = voterId; VoterName = voterName; TargetId = targetId; TieBreak = tieBreak;
                Character = character?.Clone();
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

        /// <summary>
        /// Each beat as the card reaches it - the card up, a vote on the board, the beat before the
        /// last, a tie and the vote that breaks it, the result, the card down - so the house can cut
        /// to the hot seats and act each out in time with the screen. A skip reports every vote it
        /// puts up at once, marked as skipped.
        /// </summary>
        public event System.Action<CeremonyBeat> BeatReached;

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
        private Frame frame;
        private ScreenSurface surface;

        // The screen's board (MOCKUP-PASS-PLAN M18), null on the HUD: the layer the tally and the
        // roster stand on, a row per ballot the house cast and one for the deciding vote, and the
        // result block that takes the board's place when the count is read.
        private RectTransform board, tieRow, resultBlock, resultTally, resultGlow;
        private CanvasGroup boardGroup, blockGroup;
        private readonly List<RectTransform> ballotRows = new List<RectTransform>();
        private TMP_Text resultLead, resultCount, resultOther, resultName, resultLine;
        private float resultShownAt;

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

        /// <summary>How long each vote holds at the pace played, in seconds of the card's clock.</summary>
        public float VoteHoldSeconds => VoteHold;

        /// <summary>The beat before the house's last vote, at the pace played.</summary>
        public float LastVoteBeatSeconds => LastVoteWait;

        /// <summary>How long the result holds once it is read, at the pace played.</summary>
        public float ResultHoldSeconds => CeremonyPacing.ResultHold(pace);

        /// <summary>Who is leaving, as the card was told; null before it plays.</summary>
        public string EvictedId => evictedId;

        /// <summary>The screen the card is playing on, or null while it plays on the HUD.</summary>
        public ScreenSurface Surface => playing ? surface : null;

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
        /// rather than named. With a <paramref name="screen"/> the card plays on that screen's face
        /// instead of the HUD.
        /// </summary>
        public bool Play(int week, IList<Nominee> block, IList<Ballot> votes, string evicted, bool reducedMotion,
            CeremonyPace pace = CeremonyPace.Suspenseful, string hohName = null, bool hohIsPlayer = false,
            bool evictedIsPlayer = false, ScreenSurface screen = null)
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
            surface = screen;
            frame = screen != null ? Frame.OnScreen() : Frame.Hud(Mathf.Max(0.5f, FontScale));

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
            Beat(new CeremonyBeat(CeremonyBeatKind.Opened));
            return true;
        }

        public void Cancel()
        {
            bool was = playing;
            playing = false;
            if (group != null) group.alpha = 0f;
            if (column != null) column.gameObject.SetActive(false);
            if (scrim != null) scrim.gameObject.SetActive(false);
            if (was) Beat(new CeremonyBeat(CeremonyBeatKind.Closed, -1, evictedId, !ShowingResult));
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

            // On the screen the board hands over to the result block, on the card's clock so a
            // sped-up count hands over sooner. Reduced motion swapped them at once in Result.
            if (ShowingResult && boardGroup != null && blockGroup != null && !reduced)
            {
                float swap = Eased(Mathf.Clamp01((elapsed - resultShownAt) / ScreenBoard.ResultFade));
                boardGroup.alpha = 1f - swap;
                blockGroup.alpha = swap;
            }
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

        private void Beat(CeremonyBeat beat) => BeatReached?.Invoke(beat);

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
            for (int b = before; b < count; b++) Beat(new CeremonyBeat(CeremonyBeatKind.VoteShown, b, house[b].TargetId, !announce));

            for (int i = 0; i < nominees.Count; i++)
                counts[i].text = HouseVotes(nominees[i].Id, count).ToString();

            for (int i = 0; i < dots.Count; i++)
                dots[i].GetComponent<Image>().color = i < count ? UiTheme.Danger
                    : pending && i == dots.Count - 1 ? UiTheme.Gold : UiTheme.Outline;

            // The screen's roster: a row for every ballot on the board, none for the one held back.
            for (int b = 0; b < ballotRows.Count; b++) ballotRows[b].gameObject.SetActive(b < count);

            progress.text = pending ? "One vote left"
                : count == 0 ? (house.Count == 1 ? "1 vote to reveal" : house.Count + " votes to reveal")
                : "Revealing vote " + count + " of " + house.Count;
            if (pending) Beat(new CeremonyBeat(CeremonyBeatKind.LastVotePending, house.Count - 1));
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
            Beat(new CeremonyBeat(CeremonyBeatKind.TieCalled));
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
            // And the roster's gold row, under the house's: nothing held a place for it until now.
            if (tieRow != null) tieRow.gameObject.SetActive(true);
            progress.text = hohIsPlayer ? "You have cast the deciding vote."
                : "The Head of Household has cast the deciding vote.";
            Beat(new CeremonyBeat(CeremonyBeatKind.TieBroken, -1, tieBreak?.TargetId, !announce));
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
            if (resultBlock != null) ReadTheResultOnTheScreen();
            Raise(HouseAudio.Cue.Eviction);
            Beat(new CeremonyBeat(CeremonyBeatKind.ResultShown, -1, evictedId, elapsed < ResultAt));
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

        /// <summary>
        /// The screen's result block: the board fades and the result is read in lines, the way the
        /// host reads it - by how many votes or by whose, the first name, and that they are evicted.
        /// The count is the house's alone, as the board's was, so a tie-break is credited to the Head
        /// of Household rather than counted; the evictee's figure is red and the other nominee's is
        /// in their side's colour; an evicted player is addressed under their name, the way a host
        /// says it to them ("YOU ARE EVICTED."). The Host line and the banner at the foot keep their
        /// words.
        /// </summary>
        private void ReadTheResultOnTheScreen()
        {
            int leaving = nominees.FindIndex(n => n.Id == evictedId);
            string first = FirstName(evictedName).ToUpperInvariant();
            bool named = leaving >= 0 && first.Length > 0;
            bool counted = named && !tieBreak.HasValue;

            resultLead.text = !named ? "THE HOUSE HAS VOTED."
                : tieBreak.HasValue ? "BY THE HEAD OF HOUSEHOLD'S VOTE" : "BY A VOTE OF";
            if (counted)
            {
                int other = leaving == 0 ? 1 : 0;
                resultCount.text = HouseVotes(evictedId, house.Count).ToString();
                resultOther.text = HouseVotes(nominees[other].Id, house.Count).ToString();
                resultOther.color = Side(other);
            }
            resultName.text = first;
            resultLine.text = evictedIsPlayer ? "YOU ARE EVICTED." : "IS EVICTED.";

            // Stacked in the middle of the band the board leaves, with only the lines this result has:
            // a tie-break has no count to give, and a result without a name has only its lead.
            var lines = new List<(RectTransform Rect, float Height)> { (resultLead.rectTransform, ScreenBoard.LeadH) };
            if (counted) lines.Add((resultTally, ScreenBoard.TallyH));
            if (named)
            {
                lines.Add((resultName.rectTransform, ScreenBoard.NameH));
                lines.Add((resultLine.rectTransform, ScreenBoard.LineH));
            }
            resultTally.gameObject.SetActive(counted);
            resultName.gameObject.SetActive(named);
            resultLine.gameObject.SetActive(named);
            if (resultGlow != null) resultGlow.gameObject.SetActive(named);

            float total = -ScreenBoard.Stack;
            foreach (var line in lines) total += line.Height + ScreenBoard.Stack;
            float y = ScreenBoard.BandTop - (ScreenBoard.BandTop - ScreenBoard.BandBottom - total) * 0.5f;
            foreach (var line in lines)
            {
                Place(line.Rect, ScreenBoard.BlockWidth, line.Height, y);
                if (line.Rect == resultName.rectTransform && resultGlow != null)
                    Place(resultGlow, ScreenBoard.GlowWidth, ScreenBoard.GlowHeight, y + (ScreenBoard.GlowHeight - line.Height) * 0.5f);
                y -= line.Height + ScreenBoard.Stack;
            }

            resultBlock.gameObject.SetActive(true);
            resultShownAt = elapsed;
            // Reduced motion swaps them at once; otherwise Update crossfades them on the card's clock.
            boardGroup.alpha = reduced ? 0f : 1f;
            blockGroup.alpha = reduced ? 1f : 0f;
        }

        /// <summary>
        /// Each nominee's colour on the screen's board, by their place on the block and so fixed when
        /// the card opens: the jury reveal's blue, and its pink lifted toward paper. Neither is the
        /// eviction's red, which is kept for the result, so nothing on the board is coloured by who
        /// is leaving.
        ///
        /// <para>The colour is also the word on a roster row's chip ('EVICT CASEY'), which is body
        /// text on the pack's dark red evict chip. The jury's pink reads at 4.2:1 there, under the
        /// 4.5 floor, so the right side is lifted until it clears the floor on the art and on the
        /// drawn chip alike (UiThemeContrastTests). It is one colour a side, so a chip still wears
        /// exactly the colour of the figure it names.</para>
        /// </summary>
        public static Color Side(int nominee) => nominee == 0 ? UiTheme.Accent : RightSide;

        private static readonly Color RightSide = Color.Lerp(UiTheme.Flirt, UiTheme.Paper, 0.35f);

        /// <summary>
        /// The ground of a roster row's chip when the pack's art is missing: the eviction's red, faint.
        /// The chip's word is measured on it as well as on the art (UiThemeContrastTests).
        /// </summary>
        public static Color DrawnChipGround => new Color(UiTheme.Danger.r, UiTheme.Danger.g, UiTheme.Danger.b, 0.22f);

        private static string FirstName(string name) =>
            string.IsNullOrEmpty(name) ? string.Empty : name.Split(' ')[0];

        /// <summary>
        /// The card's frame: how big its parts are and where they sit, in canvas units, with the type
        /// sizes that fit their boxes (Inter's line is 1.21 of its size; every box here is at least
        /// 1.3). The HUD's is the 880-wide card in the middle of the screen, scaled by the large-text
        /// preference; the screen's is the screen's whole face at 3:2, the faces and their counts
        /// large enough to read from the sofa.
        ///
        /// <para>A nominee's column is <see cref="Slot"/> wide and stands <see cref="ColumnX"/> either
        /// side of the middle. On the HUD the two columns touch, so that is half a slot; on the screen
        /// they stand at the sides with the roster between them.</para>
        /// </summary>
        private readonly struct Frame
        {
            public readonly bool Screen;
            public readonly float Width, Height, Portrait, Slot, ColumnX;
            public readonly float EyebrowY, EyebrowH, EyebrowPt, TitleY, TitleH, TitlePt, RimY, NameY, NameH, NamePt,
                CountY, CountH, CountPt, CaptionY, CaptionH, CaptionPt, VersusSize, VersusY, VersusPt, DotsY, DotsH, Pip, Step,
                TieMarkY, TieMarkW, TieMarkH, TieMarkDx, TiePt, ProgressY, ProgressH, ProgressPt, HostY, HostH, HostPt,
                BannerY, BannerW, BannerH, BannerPt, ControlsY, ControlsH, ControlsPt, GlassX, GlassY, Ring;

            private Frame(bool screen, float width, float height, float portrait, float slot, float eyebrowY, float eyebrowH,
                float eyebrowPt, float titleY, float titleH, float titlePt, float rimY, float nameY, float nameH, float namePt,
                float countY, float countH, float countPt, float captionY, float captionH, float captionPt, float versusSize,
                float versusY, float versusPt, float dotsY, float dotsH, float pip, float step, float tieMarkY, float tieMarkW,
                float tieMarkH, float tieMarkDx, float tiePt, float progressY, float progressH, float progressPt, float hostY,
                float hostH, float hostPt, float bannerY, float bannerW, float bannerH, float bannerPt, float controlsY,
                float controlsH, float controlsPt, float glassX, float glassY, float ring, float columnX = float.NaN)
            {
                Screen = screen; Width = width; Height = height; Portrait = portrait; Slot = slot;
                ColumnX = float.IsNaN(columnX) ? slot * 0.5f : columnX;
                EyebrowY = eyebrowY; EyebrowH = eyebrowH; EyebrowPt = eyebrowPt; TitleY = titleY; TitleH = titleH; TitlePt = titlePt;
                RimY = rimY; NameY = nameY; NameH = nameH; NamePt = namePt; CountY = countY; CountH = countH; CountPt = countPt;
                CaptionY = captionY; CaptionH = captionH; CaptionPt = captionPt; VersusSize = versusSize; VersusY = versusY; VersusPt = versusPt;
                DotsY = dotsY; DotsH = dotsH; Pip = pip; Step = step; TieMarkY = tieMarkY; TieMarkW = tieMarkW; TieMarkH = tieMarkH;
                TieMarkDx = tieMarkDx; TiePt = tiePt; ProgressY = progressY; ProgressH = progressH; ProgressPt = progressPt;
                HostY = hostY; HostH = hostH; HostPt = hostPt; BannerY = bannerY; BannerW = bannerW; BannerH = bannerH; BannerPt = bannerPt;
                ControlsY = controlsY; ControlsH = controlsH; ControlsPt = controlsPt; GlassX = glassX; GlassY = glassY; Ring = ring;
            }

            /// <summary>The HUD's card: 880 wide, the two faces 104 across at the standard text size.</summary>
            public static Frame Hud(float s)
            {
                const float portrait = 104f;
                float top = 100f + portrait;
                return new Frame(false, 880f * s, (top + 296f) * s, portrait * s, 300f * s,
                    0f, 22f * s, 15f * s, -26f * s, 60f * s, 48f * s, -100f * s,
                    -(top + 14f) * s, 24f * s, 19f * s, -(top + 36f) * s, 72f * s, 58f * s, -(top + 104f) * s, 18f * s, 12f * s,
                    62f * s, -(100f + portrait * 0.4f) * s, 22f * s, -(top + 130f) * s, 20f * s, 11f * s, 20f * s,
                    -(top + 60f) * s, 56f * s, 24f * s, 70f * s, 12f * s, -(top + 156f) * s, 22f * s, 16f * s,
                    -(top + 184f) * s, 26f * s, 18f * s, -(top + 218f) * s, 560f * s, 44f * s, 22f * s,
                    -(top + 272f) * s, 18f * s, 12f * s, 34f * s, 26f * s, 4f * s);
            }

            /// <summary>
            /// The living room's screen: its whole face, the two faces 160 across at the sides with
            /// their counts 84 high under them, the roster between them (<see cref="ScreenBoard"/>),
            /// and a small VS disc over the roster. The HOH chip goes under the figure it marks,
            /// since beside it would run off the face. The foot is as it was.
            /// </summary>
            public static Frame OnScreen() => new Frame(true, ScreenSurface.ReferenceWidth, ScreenSurface.ReferenceHeight, 160f, 220f,
                -16f, 36f, 26f, -52f, 84f, 64f, -176f,
                -354f, 40f, 28f, -394f, 110f, 84f, float.NaN, 0f, 0f,
                56f, -174f, 22f, -590f, 40f, 24f, 40f,
                -508f, 96f, 40f, 0f, 26f, -636f, 36f, 26f,
                -676f, 40f, 30f, -724f, 720f, 56f, 40f,
                -782f, 18f, 13f, 0f, 0f, 6f, 470f);
        }

        /// <summary>
        /// The screen's vote board (MOCKUP-PASS-PLAN M18), in canvas units on the 1200 × 800 face.
        /// The band under the title runs from the top of the faces to the pips; the roster fills its
        /// middle under the VS disc, one column up to seven rows and two past that - thirteen at a
        /// full house - and the result block is centred in the whole band once the board has gone.
        /// Every label box is at least 1.3 times its type: Inter draws nothing in a box under 1.21.
        /// </summary>
        private static class ScreenBoard
        {
            /// <summary>'THE VOTE', under the title.</summary>
            public const float VoteY = -134f, VoteH = 34f, VotePt = 24f, VoteWidth = 420f;

            /// <summary>The band the board stands in, and the result block after it.</summary>
            public const float BandTop = -176f, BandBottom = -584f;

            /// <summary>The roster: its top, its rows, and one column or two.</summary>
            public const float RosterY = -242f, Row = 44f, Gap = 5f, OneWide = 520f, TwoWide = 322f, Between = 16f;
            public const int RowsInAColumn = 7;

            /// <summary>A row's parts: the face at its left, the voter's name, the chip at its right.</summary>
            public const float Inset = 8f, Face = 32f, VoterPt = 19f, VoterH = 28f, ChipOne = 180f, ChipTwo = 140f,
                ChipH = 30f, ChipPt = 15f;

            /// <summary>
            /// The pack art's corners in canvas units, and how deep the art's glow sits inside its
            /// image in pixels (UiPackCatalogue's body inset: 40 for the strip, 8 for the chip), so a
            /// frame is drawn out past its rect by that much and the visible edge lands on the rect.
            /// </summary>
            public const float StripBorder = 16f, StripInset = 40f, ChipBorder = 12f, ChipInset = 8f;

            /// <summary>The result block's lines, the gap between them, and the glow behind the name.</summary>
            public const float BlockWidth = 1100f, LeadH = 40f, LeadPt = 30f, TallyH = 84f, TallyPt = 64f, NameH = 196f,
                NamePt = 150f, LineH = 52f, LinePt = 40f, Stack = 6f, GlowWidth = 1000f, GlowHeight = 260f;

            /// <summary>How long the board takes to hand over to the result, on the card's clock.</summary>
            public const float ResultFade = 0.4f;
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            var canvas = GetComponent<Canvas>();
            if (surface != null) surface.Mount(canvas);
            else ScreenSurface.Unmount(canvas);

            // The screen's board is built afresh for a screen and not at all for the HUD.
            board = null; tieRow = null; resultBlock = null; resultTally = null; resultGlow = null;
            boardGroup = null; blockGroup = null;
            resultLead = null; resultCount = null; resultOther = null; resultName = null; resultLine = null;
            ballotRows.Clear();

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

            var f = frame;
            float width = f.Width, portrait = f.Portrait;
            column.sizeDelta = new Vector2(width, f.Height);
            CardGlass(column, f.GlassX, f.GlassY);

            eyebrow = HudPrimitives.Label("Week", column, f.EyebrowPt, UiTheme.Muted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 14f;
            Place(eyebrow.rectTransform, width, f.EyebrowH, f.EyebrowY);

            // A broadcast's fast-forward bug in the card's corner, up only while the count is sped up.
            speedMark = HudPrimitives.Label("Speed", column, f.EyebrowPt, UiTheme.Glow, TextAlignmentOptions.Right);
            speedMark.text = CeremonyTakeover.SpeedCaption;
            Place(speedMark.rectTransform, width * 0.2f, f.EyebrowH, f.EyebrowY, width * 0.39f);
            speedMark.gameObject.SetActive(false);

            title = HudPrimitives.Label("Title", column, f.TitlePt, UiTheme.Paper, TextAlignmentOptions.Center);
            title.text = "LIVE EVICTION";
            // The bold cut, lit from above, as the ceremony cards set their titles.
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 2f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Color.white, Color.white, UiTheme.Glow, UiTheme.Glow);
            Place(title.rectTransform, width, f.TitleH, f.TitleY);

            // On the screen the tally stands on a layer of its own, so the result can fade the whole
            // board - faces, names, counts, the VS disc, the pips and the roster - in one, and keep
            // every piece of it. On the HUD the pieces stand on the column, as they always have.
            var tally = column;
            if (f.Screen)
            {
                var heading = HudPrimitives.Label("Vote heading", column, ScreenBoard.VotePt, UiTheme.Glow, TextAlignmentOptions.Center);
                heading.text = "THE VOTE";
                heading.characterSpacing = 12f;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) heading.font = semibold;
                Place(heading.rectTransform, ScreenBoard.VoteWidth, ScreenBoard.VoteH, ScreenBoard.VoteY);

                board = Layer("Board", column);
                boardGroup = Faded(board, 1f);
                tally = board;
            }

            // The two columns, with the tally between them.
            float slot = f.Slot;
            for (int i = 0; i < nominees.Count; i++)
            {
                float x = (i == 0 ? -1f : 1f) * f.ColumnX;

                var rim = HudPrimitives.Portrait(tally, nominees[i].Portrait, UiTheme.Danger, portrait, f.Ring, false, nominees[i].Character);

                // Both faces here are on the block, so both carry the target the web build uses.
                HudPrimitives.AddRoleMark(rim, HudPrimitives.RoleMark.Nominee, portrait);
                rim.anchorMin = new Vector2(.5f, 1f); rim.anchorMax = new Vector2(.5f, 1f); rim.pivot = new Vector2(.5f, 1f);
                rim.anchoredPosition = new Vector2(x, f.RimY);
                rims.Add(rim);

                var name = HudPrimitives.Label("Nominee", tally, f.NamePt, UiTheme.Paper, TextAlignmentOptions.Center);
                name.text = nominees[i].Name;
                // A side column on the screen is narrower than a name can be: drawn smaller on one
                // line, not wrapped, and marked as shortened if it still does not fit.
                if (f.Screen) SmallerThenShortened(name, f.NamePt * 0.7f);
                Place(name.rectTransform, slot, f.NameH, f.NameY, x);

                // On the screen each figure is in its nominee's side colour, the colour of the roster's
                // chips that name them; on the HUD it is the paper white it always was.
                var count = HudPrimitives.Label("Votes", tally, f.CountPt, f.Screen ? Side(i) : UiTheme.Paper, TextAlignmentOptions.Center);
                count.text = "0";
                // Taller than the figure's line: Inter's line is 1.21 of its size, and a 58-point
                // figure in a 66 box was truncated whole - the tally counted to nothing on screen.
                Place(count.rectTransform, slot, f.CountH, f.CountY, x);
                counts.Add(count);

                if (!f.Screen)
                {
                    var caption = HudPrimitives.Label("Votes caption", tally, f.CaptionPt, UiTheme.Muted, TextAlignmentOptions.Center);
                    caption.text = "VOTES";
                    caption.characterSpacing = 8f;
                    Place(caption.rectTransform, slot, f.CaptionH, f.CaptionY, x);
                }
            }

            // A filled red disc with the word inside it, not red lettering on the ground. The web
            // build makes this the one solid mark between the two faces, and it is what stops the
            // eye reading the pair as a row of portraits rather than as an opposition.
            var versusDisc = HudPrimitives.Disc("Versus disc", tally, UiTheme.Danger);
            Place(versusDisc, f.VersusSize, f.VersusSize, f.VersusY);

            var versus = HudPrimitives.Label("Versus", versusDisc, f.VersusPt, UiTheme.OnColor(UiTheme.Danger),
                TextAlignmentOptions.Center);
            versus.text = "VS";
            versus.rectTransform.anchorMin = Vector2.zero;
            versus.rectTransform.anchorMax = Vector2.one;
            versus.rectTransform.sizeDelta = Vector2.zero;
            versus.rectTransform.anchoredPosition = Vector2.zero;

            // One pip per ballot the house cast, filling as the votes come in.
            dotRow = new GameObject("Dots", typeof(RectTransform)).GetComponent<RectTransform>();
            dotRow.SetParent(tally, false);
            Place(dotRow, width, f.DotsH, f.DotsY);

            float pip = f.Pip, step = f.Step;
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
                    float x = (named == 0 ? -1f : 1f) * f.ColumnX;
                    tieMark = HudPrimitives.Fill("Tie-break vote", tally, UiTheme.Gold, UiTheme.ControlRadius);
                    // Beside the figure, level with its middle, clear of a two-digit count (under it
                    // on the screen, where beside the outer column would run off the face).
                    Place(tieMark, f.TieMarkW, f.TieMarkH, f.TieMarkY, x + f.TieMarkDx);
                    var chip = HudPrimitives.Label("Tie-break", tieMark, f.TiePt, UiTheme.OnColor(UiTheme.Gold),
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

            if (f.Screen)
            {
                BuildRoster(board, bold);
                BuildResultBlock(column, bold);
            }

            progress = HudPrimitives.Label("Progress", column, f.ProgressPt, UiTheme.Muted, TextAlignmentOptions.Center);
            Place(progress.rectTransform, width, f.ProgressH, f.ProgressY);

            // The host's line: whose vote breaks a tie, and then the result, the way the format reads
            // it out.
            host = HudPrimitives.Label("Host", column, f.HostPt, UiTheme.Paper, TextAlignmentOptions.Center);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) host.font = medium;
            // One line, drawn smaller rather than cut: a long name would wrap the verdict onto a second
            // line the box truncates, and "...Jordan Taylor, you" is not a result.
            host.enableAutoSizing = true; host.fontSizeMax = host.fontSize; host.fontSizeMin = f.HostPt * 0.66f;
            Place(host.rectTransform, width, f.HostH, f.HostY);

            banner = HudPrimitives.Fill("Result banner", column, UiTheme.Danger, UiTheme.ControlRadius);
            Place(banner, f.BannerW, f.BannerH, f.BannerY);
            bannerText = HudPrimitives.Label("Result", banner, f.BannerPt, UiTheme.Ink, TextAlignmentOptions.Center);
            bannerText.rectTransform.anchorMin = Vector2.zero;
            bannerText.rectTransform.anchorMax = Vector2.one;
            bannerText.rectTransform.offsetMin = Vector2.zero;
            bannerText.rectTransform.offsetMax = Vector2.zero;
            banner.gameObject.SetActive(false);

            // What the card answers to: the count can be sped up, or skipped to the result. The keys
            // named follow the device last used on the card.
            controls = HudPrimitives.Label("Controls", column, f.ControlsPt, UiTheme.Muted, TextAlignmentOptions.Center);
            controls.text = CeremonyTakeover.ControlsFor(usingPad);
            Place(controls.rectTransform, width, f.ControlsH, f.ControlsY);
        }

        /// <summary>
        /// The screen's roster: a row for each of the house's ballots in the order they were cast,
        /// and a gold one for the Head of Household's deciding vote after them. Every row is built
        /// hidden and put up as its vote is read. The shape comes from the house's ballots alone -
        /// one column up to seven rows, two past that - and the deciding vote takes the next free
        /// place, so neither the columns nor a place held open say a tie is coming.
        /// </summary>
        private void BuildRoster(RectTransform parent, TMP_FontAsset bold)
        {
            int rows = house.Count;
            bool split = rows > ScreenBoard.RowsInAColumn;
            int perColumn = split ? (rows + 1) / 2 : ScreenBoard.RowsInAColumn;
            float rowWidth = split ? ScreenBoard.TwoWide : ScreenBoard.OneWide;
            float chipWidth = split ? ScreenBoard.ChipTwo : ScreenBoard.ChipOne;

            var roster = new GameObject("Roster", typeof(RectTransform)).GetComponent<RectTransform>();
            roster.SetParent(parent, false);
            Place(roster, split ? 2f * rowWidth + ScreenBoard.Between : rowWidth,
                ScreenBoard.RowsInAColumn * (ScreenBoard.Row + ScreenBoard.Gap) - ScreenBoard.Gap, ScreenBoard.RosterY);

            int total = rows + (tieBreak.HasValue ? 1 : 0);
            for (int b = 0; b < total; b++)
            {
                bool deciding = b == rows;
                var ballot = deciding ? tieBreak.Value : house[b];
                // The house's rows fill the first column and then the second; the deciding vote goes
                // under the last of them, in the second column when there are two.
                int lane = !split ? 0 : deciding ? 1 : b / perColumn;
                int place = !split ? b : deciding ? rows - perColumn : b % perColumn;
                float x = split ? (lane == 0 ? -1f : 1f) * (rowWidth + ScreenBoard.Between) * 0.5f : 0f;

                var row = BallotRow(roster, ballot, rowWidth, chipWidth, deciding, bold);
                Place(row, rowWidth, ScreenBoard.Row, -place * (ScreenBoard.Row + ScreenBoard.Gap), x);
                if (deciding) tieRow = row;
                else ballotRows.Add(row);
            }
        }

        /// <summary>
        /// One roster row, hidden: the strip, the voter's face and name, and a chip naming the nominee
        /// they evict in that nominee's side colour. The chip is the pack's evict chip on every row -
        /// every ballot here is a vote to evict somebody - and the name on it says whom.
        /// </summary>
        private RectTransform BallotRow(RectTransform parent, Ballot ballot, float width, float chipWidth, bool deciding,
            TMP_FontAsset bold)
        {
            var row = new GameObject("Ballot", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            Framed("Ballot strip", row, PackArt.VoteRevealStrip, ScreenBoard.StripBorder, ScreenBoard.StripInset,
                UiTheme.SurfaceRaised, UiTheme.Hairline);
            // The deciding vote's row is edged in gold, the Head of Household's colour on this card,
            // over the strip's own edge.
            if (deciding) UiTheme.AddBorder(row, UiTheme.ControlRadius, UiTheme.Gold);

            var face = HudPrimitives.RectPortrait(row, "Ballot face", null, ballot.Character,
                new Vector2(ScreenBoard.Face, ScreenBoard.Face), 6);
            Pin(face, 0f, ScreenBoard.Inset, ScreenBoard.Face, ScreenBoard.Face);
            // With no look to bind there is no face to draw: the frame's ground shows, not a white square.
            if (ballot.Character == null)
            {
                var blank = face.GetComponentInChildren<RawImage>();
                if (blank != null) blank.enabled = false;
            }

            float nameX = ScreenBoard.Inset * 2f + ScreenBoard.Face;
            var voter = HudPrimitives.Label("Ballot voter", row, ScreenBoard.VoterPt, deciding ? UiTheme.Gold : UiTheme.Paper,
                TextAlignmentOptions.Left);
            voter.text = ballot.VoterName ?? string.Empty;
            SmallerThenShortened(voter, ScreenBoard.VoterPt * 0.7f);
            Pin(voter.rectTransform, 0f, nameX, width - nameX - chipWidth - ScreenBoard.Inset * 2f, ScreenBoard.VoterH);

            int named = nominees.FindIndex(n => n.Id == ballot.TargetId);
            var chip = new GameObject("Ballot chip", typeof(RectTransform)).GetComponent<RectTransform>();
            chip.SetParent(row, false);
            Pin(chip, 1f, -ScreenBoard.Inset, chipWidth, ScreenBoard.ChipH);
            Framed("Ballot chip art", chip, PackArt.VoteChipEvict, ScreenBoard.ChipBorder, ScreenBoard.ChipInset,
                DrawnChipGround, UiTheme.Danger);
            var target = HudPrimitives.Label("Ballot target", chip, ScreenBoard.ChipPt, named >= 0 ? Side(named) : UiTheme.Paper,
                TextAlignmentOptions.Center);
            if (bold != null) target.font = bold;
            target.characterSpacing = 4f;
            target.text = named >= 0 ? "EVICT " + FirstName(nominees[named].Name).ToUpperInvariant() : "EVICT";
            SmallerThenShortened(target, ScreenBoard.ChipPt * 0.7f);
            target.rectTransform.anchorMin = Vector2.zero;
            target.rectTransform.anchorMax = Vector2.one;
            target.rectTransform.offsetMin = new Vector2(ScreenBoard.Inset, 0f);
            target.rectTransform.offsetMax = new Vector2(-ScreenBoard.Inset, 0f);

            row.gameObject.SetActive(false);
            return row;
        }

        /// <summary>
        /// The screen's result block, built empty and hidden over the board: nothing on it is written
        /// until the result is read (<see cref="ReadTheResultOnTheScreen"/>), so the card holds
        /// nothing about the result before then.
        /// </summary>
        private void BuildResultBlock(RectTransform parent, TMP_FontAsset bold)
        {
            resultBlock = Layer("Result block", parent);
            blockGroup = Faded(resultBlock, 0f);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);

            // The name's glow first, so the name draws over it.
            var red = UiTheme.Pack(PackArt.GlowRed);
            if (red != null)
            {
                var glow = new GameObject("Result glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                glow.rectTransform.SetParent(resultBlock, false);
                glow.sprite = red;
                glow.color = new Color(1f, 1f, 1f, .55f);
                glow.raycastTarget = false;
                resultGlow = glow.rectTransform;
            }

            resultLead = HudPrimitives.Label("Result lead", resultBlock, ScreenBoard.LeadPt, UiTheme.Paper, TextAlignmentOptions.Center);
            if (semibold != null) resultLead.font = semibold;
            resultLead.characterSpacing = 12f;

            // '{n} TO {m}': the figures either side of a centred TO, each in its own colour.
            resultTally = new GameObject("Result tally", typeof(RectTransform)).GetComponent<RectTransform>();
            resultTally.SetParent(resultBlock, false);
            const float half = 70f, figure = 400f;
            resultCount = TallyPart("Result count", resultTally, TextAlignmentOptions.Right, 1f, -half, figure, UiTheme.Danger, bold);
            var to = TallyPart("Result to", resultTally, TextAlignmentOptions.Center, .5f, 0f, half * 2f, UiTheme.Paper, bold);
            to.text = "TO";
            resultOther = TallyPart("Result other count", resultTally, TextAlignmentOptions.Left, 0f, half, figure, UiTheme.Paper, bold);

            resultName = HudPrimitives.Label("Result name", resultBlock, ScreenBoard.NamePt, UiTheme.Danger, TextAlignmentOptions.Center);
            if (bold != null) resultName.font = bold;
            resultName.characterSpacing = 2f;
            // A long first name is drawn smaller before anything else gives: it is the one word the
            // frame is for.
            SmallerThenShortened(resultName, ScreenBoard.NamePt * 0.6f);

            resultLine = HudPrimitives.Label("Result line", resultBlock, ScreenBoard.LinePt, UiTheme.Paper, TextAlignmentOptions.Center);
            if (semibold != null) resultLine.font = semibold;
            resultLine.characterSpacing = 8f;

            resultBlock.gameObject.SetActive(false);
        }

        /// <summary>
        /// Keeps <paramref name="label"/> to one line, drawn smaller down to <paramref name="least"/>
        /// when its words are wider than its box. Unwrapped, so the shrinking answers to the width:
        /// a wrapped chip could settle on two cramped lines instead.
        ///
        /// <para>A name can run to a hundred characters, and one still too wide at the floor ends in
        /// an ellipsis. The label's own truncation would drop the glyphs that do not fit with no mark,
        /// and a cut name reads as a whole, different one.</para>
        /// </summary>
        private static void SmallerThenShortened(TMP_Text label, float least)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = least;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        /// <summary>One part of the result's count line, as tall as the line, at <paramref name="x"/> from its middle.</summary>
        private static TMP_Text TallyPart(string name, RectTransform parent, TextAlignmentOptions alignment, float pivotX, float x,
            float width, Color colour, TMP_FontAsset bold)
        {
            var label = HudPrimitives.Label(name, parent, ScreenBoard.TallyPt, colour, alignment);
            if (bold != null) label.font = bold;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(.5f, 0f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(pivotX, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
            return label;
        }

        /// <summary>A layer stretched over <paramref name="parent"/>: a piece placed on it sits where it would on the parent.</summary>
        private static RectTransform Layer(string name, RectTransform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A group to fade <paramref name="rect"/> by, that takes no input: the card never does.</summary>
        private static CanvasGroup Faded(RectTransform rect, float alpha)
        {
            var fade = rect.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = alpha;
            fade.interactable = false;
            fade.blocksRaycasts = false;
            return fade;
        }

        /// <summary>
        /// A pack frame behind <paramref name="host"/>'s other children: the sprite sliced at
        /// <paramref name="border"/> units a side and drawn out past the rect by the depth of the
        /// art's glow (<paramref name="inset"/> of its pixels), so the visible edge lands on the rect.
        /// Without the pack, the drawn card in <paramref name="fallback"/> with an <paramref name="edge"/>
        /// hairline, flush with the rect.
        /// </summary>
        private static Image Framed(string name, RectTransform host, string path, float border, float inset, Color fallback, Color edge)
        {
            var art = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = art.rectTransform;
            rect.SetParent(host, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            art.raycastTarget = false;
            float overhang = 0f;
            if (UiTheme.PackSliced(art, path, border))
            {
                var sprite = art.sprite;
                float authored = Mathf.Max(1f, Mathf.Max(sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w));
                overhang = inset * border / authored;
            }
            else
            {
                UiTheme.Style(art, fallback, UiTheme.ControlRadius);
                UiTheme.AddBorder(rect, UiTheme.ControlRadius, edge);
            }
            rect.offsetMin = new Vector2(-overhang, -overhang);
            rect.offsetMax = new Vector2(overhang, overhang);
            return art;
        }

        /// <summary>Sets <paramref name="rect"/> at its parent's left (0) or right (1) edge, level with the middle.</summary>
        private static void Pin(RectTransform rect, float side, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(side, .5f);
            rect.anchorMax = new Vector2(side, .5f);
            rect.pivot = new Vector2(side, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// The mockups' glass ground behind the card's column (VISUAL-TARGET.md §4, mockup-08 and
        /// -10): the night background at 85 %, a cyan hairline on the edge and a soft glow outside
        /// it. Built as the column's first child so every piece of the ceremony draws over it, and
        /// stretched to the column so it grows with the large-text preference. On a screen it is
        /// the screen's own ground, flush with the face.
        /// </summary>
        private static RectTransform CardGlass(RectTransform column, float marginX, float marginY)
        {
            var glass = HudPrimitives.Fill("Card glass", column, UiTheme.GlassFill, UiTheme.GlassRadius);
            glass.SetAsFirstSibling();
            glass.anchorMin = Vector2.zero;
            glass.anchorMax = Vector2.one;
            glass.offsetMin = new Vector2(-marginX, -marginY);
            glass.offsetMax = new Vector2(marginX, marginY);
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
