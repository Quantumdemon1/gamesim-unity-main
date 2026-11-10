#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Tests.EditMode
{
    internal static partial class BalanceLab
    {
        /// <summary>
        /// R7's A1 record of one week of a season (R7 build plan C1; the lead's decisions Q1 and Q2): what the season put to the
        /// player, what the player answered, the week's role classes and its minutes. Kept beside the season like the
        /// diagnostics' records and, like them, never in the row the goldens hash (<see cref="Row"/>); a part carries it
        /// (<see cref="BalanceLabParts.Record"/>, stamp v10). Every count is the lab's own reading of states the season showed:
        /// it moves nothing, so a season plays the same with it.
        ///
        /// <para><b>The format.</b> A part's season has <c>promptsByWeek</c>, an object keyed by the week number ("1", "2", ...)
        /// with an entry for every week from 1 to the season's last - a week the player spent out of the house reads zeros - and
        /// each entry has every field below, every dictionary every key:</para>
        /// <list type="bullet">
        /// <item><c>week</c>: the week.</item>
        /// <item><c>playerHoh</c>, <c>playerVetoHolder</c>, <c>playerNominated</c>: the role-week classes, as the autopsy's week row
        /// reads them (<see cref="SeasonAutopsy.WeekRow"/>): the player was Head of Household; won the veto; was named, at the
        /// ceremony or as the replacement. A role week is a week with any of them.</item>
        /// <item><c>vetoBoardCards</c>: the veto meeting's cards put to the player, counted at the week's first VetoMeeting state:
        /// <see cref="RoleOffers.BoardAsks"/> - at any story rules the nominees' veto asks (under Blame the week's proposed veto
        /// commitments to the player, which its rows drew), and under Roles the Head of Household's keep-the-block ask too.</item>
        /// <item><c>keepTheBlockCards</c>: of those, the Head of Household's keep-the-block asks (<see cref="NpcDeals.IsKeepTheBlockId"/>), 0 or 1.</item>
        /// <item><c>pitchCards</c>: the pitch cards put to the player as Head of Household, counted at the week's first Nomination
        /// state: under Blame the whole queue, under Roles the board <see cref="HoHPitches.HoldTheBoard"/> kept.</item>
        /// <item><c>storyAsks</c>: the story's asks, counted as the story pacing report counts them (one a storyline and anchor;
        /// the first night's card, summons and plays aside, <see cref="StoryPacingTests.Pace.Watch"/>), by the storyline's
        /// template: <c>quiet-tears</c>, <c>the-pawn-talk</c>, and <c>other</c> - the backdoor's own Just a Pawn among them,
        /// since its template is the backdoor's. A season's storyAsks summed over every week are its pace's asks.</item>
        /// <item><c>answers</c>: the player's answers, by class: <c>veto-ask</c>, <c>keep-the-block</c> and <c>other-offer</c>
        /// (RespondToDeal, by the offer's id); <c>pitch</c> and <c>other-card</c> (ReplyToHouseguest, by the card's kind; feeling
        /// a pitcher out answers nothing); <c>quiet-tears</c>, <c>the-pawn-talk</c> and <c>other-story</c> (ProgressStoryline,
        /// by the storyline's template).</item>
        /// <item><c>words</c>: of the answers, the words the player gave: <c>veto-ask</c> and <c>keep-the-block</c> accepted, and
        /// <c>pitch</c> a promise of safety.</item>
        /// <item><c>minutes</c>: the week's minutes in the house, the projections' own sum (<see cref="Projection.WeekMinutes"/>:
        /// the ceremonies, the competitions played, the NPC budget's free roam and 30 s a decision, every answer a decision);
        /// 0 for a week the player was out.</item>
        /// </list>
        /// <para>The prompts shown in a week, the lead's Q2 measure, are <c>vetoBoardCards + pitchCards</c> plus the storyAsks.</para>
        /// </summary>
        internal sealed class WeekPrompts
        {
            public int week;
            public bool playerHoh, playerVetoHolder, playerNominated;
            public int vetoBoardCards, keepTheBlockCards, pitchCards;
            public SortedDictionary<string, int> storyAsks = PromptWatch.Zeros(PromptWatch.StoryClasses);
            public SortedDictionary<string, int> answers = PromptWatch.Zeros(PromptWatch.AnswerClasses);
            public SortedDictionary<string, int> words = PromptWatch.Zeros(PromptWatch.WordClasses);
            public double minutes;

            /// <summary>The prompts shown this week (decision Q2): the veto board's cards, the pitch cards and the story's asks.</summary>
            internal int Shown => vetoBoardCards + pitchCards + storyAsks.Values.Sum();
        }

        /// <summary>
        /// R7's A1 watch over one season (<see cref="WeekPrompts"/>): it reads each accepted command and the state it left, and
        /// writes only the season's <see cref="SeasonRun.promptsByWeek"/>.
        /// </summary>
        internal sealed class PromptWatch
        {
            /// <summary>The story's ask classes, by the storyline's template (<see cref="WeekPrompts.storyAsks"/>).</summary>
            internal const string QuietTears = "quiet-tears", PawnTalk = EpisodeEngine.PawnTalkArc, OtherStory = "other";
            /// <summary>The answers' classes (<see cref="WeekPrompts.answers"/>).</summary>
            internal const string VetoAsk = "veto-ask", KeepTheBlock = "keep-the-block", OtherOffer = "other-offer", Pitch = "pitch", OtherCard = "other-card",
                OtherStoryAnswer = "other-story";

            internal static readonly string[] StoryClasses = { QuietTears, PawnTalk, OtherStory };
            internal static readonly string[] AnswerClasses = { VetoAsk, KeepTheBlock, OtherOffer, Pitch, OtherCard, QuietTears, PawnTalk, OtherStoryAnswer };
            internal static readonly string[] WordClasses = { VetoAsk, KeepTheBlock, Pitch };

            internal static SortedDictionary<string, int> Zeros(IEnumerable<string> keys) =>
                new SortedDictionary<string, int>(keys.ToDictionary(k => k, k => 0, StringComparer.Ordinal), StringComparer.Ordinal);

            // The watch's own notes: beats already seen, asks already counted (the pace watch's rule), and the weeks whose
            // veto meeting and nominations were counted.
            private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal), asked = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<int> meetings = new HashSet<int>(), nominations = new HashSet<int>();

            /// <summary>The season's record of a week, made when first asked for.</summary>
            internal static WeekPrompts Week(SeasonRun run, int week)
            {
                if (!run.promptsByWeek.TryGetValue(week, out var record)) run.promptsByWeek[week] = record = new WeekPrompts { week = week };
                return record;
            }

            /// <summary>
            /// A state the season reached after an accepted command: the week's first veto meeting counts its board, a player Head
            /// of Household's first nominations count their pitches, and every story ask new to the watch counts by its template.
            /// </summary>
            internal void Look(EpisodeState s, SeasonRun run)
            {
                if (s.phase == EpisodePhase.VetoMeeting && meetings.Add(s.week))
                {
                    var asks = RoleOffers.BoardAsks(s);
                    var w = Week(run, s.week);
                    w.vetoBoardCards += asks.Count;
                    w.keepTheBlockCards += asks.Count(d => NpcDeals.IsKeepTheBlockId(d.id));
                }
                if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId && nominations.Add(s.week))
                    Week(run, s.week).pitchCards += s.replyCards.Count(c => c != null && c.kind == ReplyCards.Pitch && c.week == s.week);
                // The story pacing report's own count (StoryPacingTests.Pace.Watch), the ask kept by its template.
                foreach (var beat in EpisodeEngine.OpenStoryBeats(s))
                {
                    if (!seen.Add(beat.id)) continue;
                    var arc = StoryCatalog.Find(s.storylines.FirstOrDefault(x => x.id == beat.cycleId)?.templateId);
                    if (arc?.id == "first-night" || beat.surface == StorySurfaces.Summons || arc?.play != null) continue;
                    if (!asked.Add(beat.week + "|" + beat.cycleId + "|" + beat.closesAnchor)) continue;
                    Week(run, beat.week).storyAsks[StoryClass(arc?.id)]++;
                }
            }

            /// <summary>An accepted command, read on the state it was applied to: an answer counts by its class, a word given as well.</summary>
            internal void Answer(EpisodeCommand used, EpisodeState before, SeasonRun run)
            {
                string kind = null;
                bool word = false;
                switch (used.kind)
                {
                    case EpisodeCommandKind.RespondToDeal:
                        kind = NpcDeals.IsKeepTheBlockId(used.targetId) ? KeepTheBlock
                            : used.targetId != null && used.targetId.StartsWith(NpcDeals.VetoAskPrefix, StringComparison.Ordinal) ? VetoAsk : OtherOffer;
                        word = kind != OtherOffer && string.Equals((used.text ?? "").Trim(), EpisodeEngine.AcceptDeal, StringComparison.OrdinalIgnoreCase);
                        break;
                    case EpisodeCommandKind.ReplyToHouseguest:
                        var card = before.replyCards.FirstOrDefault(c => c != null && c.id == used.targetId);
                        string key = (used.text ?? "").Trim();
                        if (card?.kind == ReplyCards.Pitch)
                        {
                            if (key == HoHPitches.FeelOutKey) return;
                            kind = Pitch;
                            word = key == HoHPitches.PromiseKey;
                        }
                        else kind = OtherCard;
                        break;
                    case EpisodeCommandKind.ProgressStoryline:
                        var beat = EpisodeEngine.OpenStoryBeats(before).FirstOrDefault(b => b.id == used.targetId);
                        string template = before.storylines.FirstOrDefault(x => beat != null && x.id == beat.cycleId)?.templateId;
                        kind = template == QuietTears ? QuietTears : template == PawnTalk ? PawnTalk : OtherStoryAnswer;
                        break;
                }
                if (kind == null) return;
                var w = Week(run, before.week);
                w.answers[kind]++;
                if (word) w.words[kind]++;
            }

            /// <summary>
            /// The season's end, after its autopsy and ceremonies: a record for every week from 1 to its last, each with the
            /// autopsy's role-week flags and, for a week the player was in the house, its minutes.
            /// </summary>
            internal static void Close(SeasonRun run)
            {
                var a = run.autopsy;
                if (a == null) return;
                int last = Math.Max(a.weeks, run.promptsByWeek.Keys.DefaultIfEmpty(0).Max());
                for (int week = 1; week <= last; week++) Week(run, week);
                foreach (var row in a.weeksPlayed)
                {
                    var w = Week(run, row.week);
                    w.playerHoh |= row.playerHoh;
                    w.playerVetoHolder |= row.playerVetoHolder;
                    w.playerNominated |= row.playerNominated;
                }
                foreach (int week in BalanceLabReports.WeeksInTheHouse(run))
                {
                    var p = BalanceLabReports.WeekParts(run, week);
                    Week(run, week).minutes = Math.Round(Projection.WeekMinutes(p.ceremony, p.competition, p.freeRoam, p.decisions), 4);
                }
            }

            private static string StoryClass(string templateId) => templateId == QuietTears ? QuietTears : templateId == PawnTalk ? PawnTalk : OtherStory;
        }
    }
}
#endif
