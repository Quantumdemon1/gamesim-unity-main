using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Threads (plan 31): seeding a season's stories, running their chapters, and ending them at
    /// their climax or a stated end. A thread asks nothing itself - its chapters do - so it never
    /// spends the airtime; its chapters spend it like any story.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The last week a season may still seed a thread: its first eviction nights.</summary>
        public const int ThreadSeedLastWeek = 3;

        /// <summary>A thread plays at most this many chapters, which keeps its record inside a cycle's path.</summary>
        public const int ThreadChaptersMax = 12;

        /// <summary>Weeks a thread with nothing it could play waits before it fades - unless all that is left is its climax.</summary>
        public const int ThreadFadeWeeks = 4;

        private const string ChapterStep = "chapter:", BeginStep = "begin", EndStep = "end";

        /// <summary>
        /// Seeds the season's threads at its first eviction nights: every kind the cast can carry, each
        /// at most once, in the catalogue's order (the bond, the rivalry, the numbers). A kind the cast
        /// cannot carry yet - no alliance to speak of in week one - is tried again the next eviction
        /// night, through <see cref="ThreadSeedLastWeek"/>.
        /// </summary>
        internal static void SeedThreads(EpisodeState s, string anchor)
        {
            if (!StoryAt(s, StoryRules.Threads) || anchor != StoryAnchors.EvictionNight || s.week > ThreadSeedLastWeek) return;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            var ctx = new StoryContext(s, anchor);
            foreach (var template in StoryCatalog.All.Where(t => t.thread != null))
            {
                if (s.storylines.Count(x => x.lane == StoryLanes.Thread) >= StoryLanes.MaxThreads) return;
                if (s.storylines.Any(x => x.templateId == template.id)) continue;
                var binding = template.cast?.Invoke(ctx);
                if (binding == null || template.roles.Any(r => !r.optional && binding.Get(r.key) == null)) continue;
                BeginThread(s, template, binding);
            }
        }

        private static void BeginThread(EpisodeState s, ArcTemplate template, ArcBinding binding)
        {
            var cycle = new StorylineState
            {
                id = Mint(s, "story"),
                templateId = template.id,
                category = template.eyebrow ?? template.lane,
                title = template.title,
                status = StorylineStatus.Active,
                week = s.week,
                endedWeek = 0,
                beatId = template.beats[0].id,
                lane = template.lane,
                variant = 0,
                cast = template.roles.Where(r => binding.Get(r.key) != null)
                    .Select(r => new StoryRoleState { role = r.key, contestantId = binding.Get(r.key) }).ToList(),
            };
            s.storylines.Add(cycle);
            int sequence = s.nextSequence;
            Log(s, StoryLog.Thread, ThreadLabel(s, cycle, template) + ": " + StoryText.Fill(s, template.thread.premise, cycle.cast), s.playerId);
            AddStep(cycle, BeginStep, null, StoryResults.Plain, s.week, sequence);
        }

        /// <summary>A thread's name with its people: "Your bond with Alex".</summary>
        private static string ThreadLabel(EpisodeState s, StorylineState cycle, ArcTemplate template) =>
            StoryText.Fill(s, template.thread.label ?? template.title, cycle.cast);

        /// <summary>
        /// A thread at an anchor: a chapter under way is left to run; one that has ended is closed; then
        /// its climax, a chapter the house already started for its people, or the next chapter that
        /// will cast; and a thread with nothing it could play for a month fades.
        /// </summary>
        internal static void ThreadPulse(EpisodeState s, StoryCycle thread, string anchor)
        {
            var rules = thread.template.thread;
            var ctx = new StoryContext(s, anchor);
            bool underWay = false;
            foreach (var step in thread.record.path.Where(p => IsChapter(p) && p.result == StoryResults.Plain).ToList())
            {
                var chapter = s.storylines.FirstOrDefault(x => x.id == step.optionId);
                if (chapter != null && StorylineStatus.Running(chapter.status)) { underWay = true; continue; }
                CloseChapter(s, thread, step, chapter);
            }

            // The climax comes at its fixed point whatever chapter is under way; that chapter plays out on its own.
            string ending = rules.climax?.Invoke(ctx, thread);
            if (ending != null) { EndThread(s, thread, ending); return; }
            if (underWay) return;

            var candidates = (rules.next?.Invoke(ctx, thread) ?? Enumerable.Empty<string>())
                .Select(rules.Chapter).Where(c => c != null && Attempts(thread.record, c.arcId) < c.attempts).ToList();
            if (candidates.Count == 0) return; // All that is left is its climax.
            if (AdoptChapter(s, thread, candidates)) return;
            if (Chapters(thread.record) < ThreadChaptersMax)
                foreach (var chapter in candidates)
                    if (TryStartChapter(s, thread, chapter, anchor)) return;

            int lastActive = Math.Max(thread.record.week, thread.record.path.Count == 0 ? 0 : thread.record.path.Max(p => p.week));
            if (rules.fades && lastActive + ThreadFadeWeeks < s.week) EndThread(s, thread, ThreadEndings.Faded);
        }

        private static bool IsChapter(StoryStepState step) =>
            step?.beatId != null && step.beatId.StartsWith(ChapterStep, StringComparison.Ordinal);

        private static string ChapterArc(StoryStepState step) => step.beatId.Substring(ChapterStep.Length);

        private static int Chapters(StorylineState thread) => thread.path.Count(IsChapter);

        private static int Attempts(StorylineState thread, string arcId) =>
            thread.path.Count(p => IsChapter(p) && ChapterArc(p) == arcId);

        /// <summary>Whether a thread has played this chapter and it went the thread's way.</summary>
        public static bool ChapterLanded(StoryCycle thread, string arcId) =>
            thread?.record?.path != null && thread.record.path.Any(p => IsChapter(p) && ChapterArc(p) == arcId && p.result == StoryResults.Success);

        /// <summary>Whether a thread has played this chapter at all, however it went.</summary>
        public static bool ChapterPlayed(StoryCycle thread, string arcId) =>
            thread?.record?.path != null && thread.record.path.Any(p => IsChapter(p) && ChapterArc(p) == arcId && p.result != StoryResults.Plain);

        /// <summary>
        /// A chapter the house already started for the thread's people - the play pool's offer about
        /// them - is the thread's chapter: a thread owns its people's stories.
        /// </summary>
        private static bool AdoptChapter(EpisodeState s, StoryCycle thread, List<ThreadChapter> candidates)
        {
            var ctx = new StoryContext(s, CurrentAnchor(s));
            var linked = new HashSet<string>(s.storylines.Where(x => x.lane == StoryLanes.Thread)
                .SelectMany(x => x.path).Where(IsChapter).Select(p => p.optionId).Where(id => id != null), StringComparer.Ordinal);
            foreach (var chapter in candidates)
            {
                var focus = chapter.focus?.Invoke(ctx, thread);
                var running = s.storylines.Where(x => x.templateId == chapter.arcId && StorylineStatus.Running(x.status)
                                                      && x.week >= thread.record.week && !linked.Contains(x.id)
                                                      && (focus == null || x.cast.Any(r => focus.Contains(r.contestantId))))
                    .OrderBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                if (running == null) continue;
                OpenChapter(s, thread, running);
                return true;
            }
            return false;
        }

        /// <summary>Starts a chapter aimed at the thread's people, if the arc casts them here and the house has room for it.</summary>
        private static bool TryStartChapter(EpisodeState s, StoryCycle thread, ThreadChapter chapter, string anchor)
        {
            var arc = StoryCatalog.Find(chapter.arcId);
            if (arc == null || arc.thread != null || !StoryAt(s, arc.rulesVersion)) return false;
            // A chapter comes when its arc would: at one of its own anchors.
            if (arc.startAnchors.Length > 0 && Array.IndexOf(arc.startAnchors, anchor) < 0) return false;
            var focus = chapter.focus?.Invoke(new StoryContext(s, anchor), thread);
            var binding = Castable(s, new StoryContext(s, anchor, focus: focus), arc);
            if (binding == null) return false;
            var cycle = StartCycle(s, arc, binding, anchor);
            if (cycle == null) return false;
            OpenChapter(s, thread, cycle);
            return true;
        }

        private static void OpenChapter(EpisodeState s, StoryCycle thread, StorylineState chapter)
        {
            var arc = StoryCatalog.Find(chapter.templateId);
            int sequence = s.nextSequence;
            Log(s, StoryLog.Thread, ThreadLabel(s, thread.record, thread.template) + ": chapter " + (Chapters(thread.record) + 1)
                + ", " + (arc?.title ?? chapter.title) + ".", s.playerId);
            AddStep(thread.record, ChapterStep + chapter.templateId, chapter.id, StoryResults.Plain, s.week, sequence);
        }

        /// <summary>A chapter that has ended: whether it went the thread's way, written on its step.</summary>
        private static void CloseChapter(EpisodeState s, StoryCycle thread, StoryStepState step, StorylineState chapter)
        {
            string arcId = ChapterArc(step);
            var definition = thread.template.thread.Chapter(arcId);
            bool landed = chapter != null && (definition?.landed?.Invoke(s, chapter) ?? chapter.endingId == PlayEndings.Won);
            bool never = chapter == null || chapter.endingId == PlayEndings.Declined || chapter.endingId == "left-house" || chapter.endingId == "stale";
            string result = landed ? StoryResults.Success : never ? StoryResults.Lapsed : StoryResults.Backfire;
            var arc = StoryCatalog.Find(arcId);
            int sequence = s.nextSequence;
            Log(s, StoryLog.Thread, ThreadLabel(s, thread.record, thread.template) + ": " + (arc?.title ?? arcId)
                + (landed ? " went your way." : never ? " never came to anything." : " did not go your way."), s.playerId);
            step.result = result;
            step.week = s.week;
            step.logSequence = sequence;
        }

        /// <summary>A thread's end: its line, its last step, and the cycle closed.</summary>
        internal static void EndThread(EpisodeState s, StoryCycle thread, string ending)
        {
            if (!StorylineStatus.Running(thread.record.status)) return;
            var rules = thread.template.thread;
            string line = rules.endings.TryGetValue(ending ?? string.Empty, out var text) ? text : null;
            int sequence = s.nextSequence;
            if (line != null) Log(s, StoryLog.Thread, ThreadLabel(s, thread.record, thread.template) + ": " + StoryText.Fill(s, line, thread.record.cast), s.playerId);
            AddStep(thread.record, EndStep, ending, rules.good.Contains(ending ?? string.Empty) ? StoryResults.Success : StoryResults.Backfire,
                s.week, line != null ? sequence : 0);
            EndCycle(s, thread.record, ending ?? ThreadEndings.Faded);
        }

        /// <summary>
        /// The finale (plan 31): every thread still running ends, reading the jury it was heading for.
        /// A chapter still under way at the end simply stops counting.
        /// </summary>
        internal static void StoryFinale(EpisodeState s)
        {
            if (!StoryAt(s, StoryRules.Threads)) return;
            foreach (var cycle in RunningCycles(s).Where(x => x.lane == StoryLanes.Thread).ToList())
            {
                var template = StoryCatalog.Find(cycle.templateId);
                if (template?.thread == null) { EndCycle(s, cycle, "done"); continue; }
                var thread = new StoryCycle(cycle, template);
                string ending = template.thread.finale?.Invoke(new StoryContext(s, CurrentAnchor(s)), thread) ?? ThreadEndings.Faded;
                EndThread(s, thread, ending);
            }
        }

        // ---------------------------------------------------------------- what the HUD reads

        /// <summary>A thread as the player sees it: its people, its chapters and, once it has ended, how.</summary>
        public sealed class ThreadView
        {
            public string cycleId, arcId, kind, title, label, premise;
            public int week;
            /// <summary>Null while it runs; one of <see cref="ThreadEndings"/> once it has ended.</summary>
            public string ending;
            /// <summary>The line its ending wrote, with names.</summary>
            public string outcome;
            public bool good;
            public List<ThreadChapterView> chapters = new List<ThreadChapterView>();
        }

        /// <summary>One chapter of a thread: its play or arc and how it went (<see cref="ThreadChapterResults"/>).</summary>
        public sealed class ThreadChapterView
        {
            public string arcId, title, cycleId, result;
        }

        /// <summary>The season's threads, running first, in the order they began.</summary>
        public static List<ThreadView> Threads(EpisodeState s)
        {
            var views = new List<ThreadView>();
            if (s?.storylines == null) return views;
            foreach (var cycle in s.storylines.Where(x => x.lane == StoryLanes.Thread))
            {
                var template = StoryCatalog.Find(cycle.templateId);
                if (template?.thread == null) continue;
                bool running = StorylineStatus.Running(cycle.status);
                string ending = running ? null : cycle.endingId;
                var view = new ThreadView
                {
                    cycleId = cycle.id, arcId = template.id, kind = template.thread.kind, title = template.title,
                    label = ThreadLabel(s, cycle, template), premise = StoryText.Fill(s, template.thread.premise, cycle.cast),
                    week = cycle.week, ending = ending,
                    outcome = ending != null && template.thread.endings.TryGetValue(ending, out var line) ? StoryText.Fill(s, line, cycle.cast) : null,
                    good = ending != null && template.thread.good.Contains(ending),
                };
                foreach (var step in cycle.path.Where(IsChapter))
                    view.chapters.Add(new ThreadChapterView
                    {
                        arcId = ChapterArc(step), cycleId = step.optionId,
                        title = StoryCatalog.Find(ChapterArc(step))?.title ?? ChapterArc(step),
                        result = step.result == StoryResults.Plain ? ThreadChapterResults.Open
                            : step.result == StoryResults.Success ? ThreadChapterResults.Landed
                            : step.result == StoryResults.Lapsed ? ThreadChapterResults.Never : ThreadChapterResults.Missed,
                    });
                views.Add(view);
            }
            return views.OrderByDescending(v => v.ending == null).ThenBy(v => v.week).ThenBy(v => v.cycleId, StringComparer.Ordinal).ToList();
        }
    }
}
