using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Plays (plan 30 §2): deciding them, and what the HUD reads of them. A play is a story cycle
    /// whose template carries a <see cref="PlayTemplate"/>; everything it needs is already saved on
    /// the cycle (its path says whether it was taken on, its ending how it went), so plays add no
    /// field to the save.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Whether a play has been taken on: its offer answered with Take it on.</summary>
        public static bool TakenOn(StorylineState cycle) =>
            cycle?.path != null && cycle.path.Any(p => p.optionId == PlayOptions.TakeItOn);

        /// <summary>How far along a play is now, read from the season. Nothing for a cycle that is not a play.</summary>
        public static PlayProgress ProgressOf(EpisodeState s, StorylineState cycle)
        {
            var template = StoryCatalog.Find(cycle?.templateId);
            if (s == null || template?.play?.progress == null) return new PlayProgress(0, 1);
            return template.play.progress(new StoryContext(s, CurrentAnchor(s)), new StoryCycle(cycle, template));
        }

        /// <summary>
        /// Decides every play taken on whose goal is met and, at its deadline, every one whose goal
        /// is not. Between anchors (<paramref name="anchor"/> null) only a met goal decides: a play
        /// is never lost before its deadline comes.
        /// </summary>
        internal static void DecidePlays(EpisodeState s, string anchor)
        {
            if (!StoryOn(s)) return;
            foreach (var cycle in RunningCycles(s).ToList())
            {
                var template = StoryCatalog.Find(cycle.templateId);
                var play = template?.play;
                if (play?.progress == null || !TakenOn(cycle)) continue;
                var view = new StoryCycle(cycle, template);
                var progress = play.progress(new StoryContext(s, anchor ?? CurrentAnchor(s)), view);
                if (progress.Met) Decide(s, view, PlayEndings.Won);
                else if (anchor != null && anchor == play.deadline)
                    Decide(s, view, progress.have > 0 && play.PartCounts ? PlayEndings.Part : PlayEndings.Lost);
            }
        }

        /// <summary>A play's outcome: its consequences, its line, what changed, and the cycle's end.</summary>
        private static void Decide(EpisodeState s, StoryCycle cycle, string ending)
        {
            var play = cycle.template.play;
            var effects = ending == PlayEndings.Won ? play.won : ending == PlayEndings.Part ? play.part : play.lost;
            string outcome = ending == PlayEndings.Won ? play.wonOutcome : ending == PlayEndings.Part ? play.partOutcome : play.lostOutcome;
            var applied = new List<StoryEffectState>();
            foreach (var fx in effects ?? Array.Empty<Fx>())
            {
                var effect = ResolveFx(s, cycle, fx);
                if (effect == null) continue;
                ApplyStoryEffect(s, effect, cycle.record, null, cycle.Id + ":" + ending, 0, false);
                applied.Add(effect);
            }
            EndCycle(s, cycle.record, ending);
            if (!string.IsNullOrEmpty(outcome))
                Log(s, StoryLog.Play, cycle.template.title + ": " + StoryText.Fill(s, outcome, cycle.record.cast), s.playerId);
            Receipts(s, applied);
        }

        /// <summary>What the player learns a play changed: one line each, only to the player.</summary>
        private static void Receipts(EpisodeState s, IEnumerable<StoryEffectState> applied)
        {
            foreach (var line in PlayReceipts.For(s, applied)) Log(s, StoryLog.Receipt, line, s.playerId);
        }

        /// <summary>Whether this ending turned a play down for the first time: it may be offered once more.</summary>
        private static bool FirstRefusal(EpisodeState s, StorylineState cycle, ArcTemplate template, string ending) =>
            template.play != null && ending == PlayEndings.Declined
            && !s.storylines.Any(x => x != cycle && x.templateId == template.id && x.endingId == PlayEndings.Declined);

        // ---------------------------------------------------------------- what the HUD reads

        /// <summary>One play as the player sees it: the offer, the chase, or how it went.</summary>
        public sealed class PlayView
        {
            public string cycleId, arcId, title, currency, deadline;
            /// <summary>The goal with names.</summary>
            public string goal;
            public bool takenOn;
            public PlayProgress progress;
            /// <summary>Null while it runs; one of <see cref="PlayEndings"/> once it has ended.</summary>
            public string ending;
            /// <summary>The line that says how it went, with names, once decided.</summary>
            public string outcome;
            /// <summary>The open beat waiting on the player, if any: the offer, or the next step.</summary>
            public string openEventId;
            public int week;
        }

        /// <summary>
        /// The season's plays the player has been offered: running ones first (newest first), then
        /// the ones that ended, newest first. Turned-down offers are left out once they end.
        /// </summary>
        public static List<PlayView> Plays(EpisodeState s)
        {
            var views = new List<PlayView>();
            if (s?.storylines == null) return views;
            foreach (var cycle in s.storylines)
            {
                var template = StoryCatalog.Find(cycle.templateId);
                var play = template?.play;
                if (play == null || cycle.endingId == PlayEndings.Declined) continue;
                bool running = StorylineStatus.Running(cycle.status);
                string outcome = cycle.endingId == PlayEndings.Won ? play.wonOutcome
                    : cycle.endingId == PlayEndings.Part ? play.partOutcome
                    : cycle.endingId == PlayEndings.Lost ? play.lostOutcome : null;
                views.Add(new PlayView
                {
                    cycleId = cycle.id, arcId = template.id, title = template.title, currency = play.currency,
                    deadline = play.deadline, goal = StoryText.Fill(s, play.goal, cycle.cast),
                    takenOn = TakenOn(cycle), progress = running ? ProgressOf(s, cycle) : default,
                    ending = running ? null : cycle.endingId,
                    outcome = outcome == null ? null : StoryText.Fill(s, outcome, cycle.cast),
                    openEventId = s.houseEvents.FirstOrDefault(e => e.cycleId == cycle.id && !e.resolved)?.id,
                    week = cycle.week,
                });
            }
            return views.OrderByDescending(v => v.ending == null).ThenByDescending(v => v.week)
                .ThenByDescending(v => v.cycleId, StringComparer.Ordinal).ToList();
        }
    }
}
