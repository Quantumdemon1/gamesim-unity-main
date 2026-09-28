using System;
using System.Collections.Generic;
using System.Linq;
using static Gamesim.Simulation.Fx;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Threads (plan 31): the bond, the rivalry and the numbers, seeded from the cast at the first
    /// eviction nights. Their chapters are the catalogue's own plays and arcs, aimed at the thread's
    /// people; their climaxes read the season.
    /// </summary>
    public static partial class StoryCatalog
    {
        private static IEnumerable<ArcTemplate> ThreadArcs()
        {
            yield return TheBond();
            yield return TheRivalry();
            yield return TheNumbers();
        }

        /// <summary>A thread's one beat. A thread asks nothing itself; the beat is there because every cycle has one.</summary>
        private static BeatTemplate ThreadBeat(string title) => new BeatTemplate
        {
            id = "thread", surface = StorySurfaces.Npc, title = title,
            summary = "A story that runs all season.", text = "A story that runs all season.",
            npc = (c, y, options) => "goes-on",
            options = new[] { Opt("goes-on", "Goes on", null, null).Then(EpisodeEngine.Waits) },
        };

        /// <summary>A chapter aimed at one of the thread's parts.</summary>
        private static ThreadChapter Chapter(string arcId, string role, Func<EpisodeState, StorylineState, bool> landed = null, int attempts = 2) =>
            new ThreadChapter { arcId = arcId, focus = role == null ? null : (c, t) => new[] { t.Role(role) }, landed = landed, attempts = attempts };

        private static bool Gone(ContestantState who) => who == null || who.status != ContestantStatus.Active;

        /// <summary>The thread of this kind the season already has, if any.</summary>
        private static StorylineState ThreadOf(StoryContext c, string arcId) => c.state.storylines.FirstOrDefault(x => x.templateId == arcId);

        private static string RoleIn(StorylineState cycle, string role) => cycle?.cast.FirstOrDefault(r => r.role == role)?.contestantId;

        /// <summary>A juror's vote at the finale: for the player, against, or none cast.</summary>
        private static bool? VotedForPlayer(StoryContext c, string id)
        {
            var vote = c.state.votes.FirstOrDefault(v => v.voterId == id);
            return vote == null ? (bool?)null : vote.targetId == P(c);
        }

        // ---------------------------------------------------------------- the bond

        private static ArcTemplate TheBond() => new ArcTemplate
        {
            id = "thread-bond", lane = StoryLanes.Thread, eyebrow = "Your Story", title = "The Bond",
            origin = "native: plan 31, the bond thread: a friend who could become a ride-or-die or a showmance",
            rulesVersion = StoryRules.Threads, oncePerHeadliner = false, cooldownWeeks = 0, pairCooldownWeeks = 0,
            roles = new[] { Departed("FRIEND") },
            cast = c =>
            {
                // Your warmest houseguest both ways, who holds nothing much against you.
                var friend = Npcs(c).Where(x => c.Grudge(x.id, P(c)) < 40)
                    .OrderByDescending(x => Mutual(c, P(c), x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return friend == null ? null : Bind().With("FRIEND", friend.id).Headlining(friend.id);
            },
            beats = new[] { ThreadBeat("The Bond") },
            thread = new ThreadTemplate
            {
                kind = ThreadKinds.Bond, label = "Your bond with {FRIEND}",
                premise = "{FRIEND} could be the one person in this house you trust to the end.",
                chapters = new[]
                {
                    Chapter("settle-it", "FRIEND"),
                    Chapter("know-them", "FRIEND"),
                    Chapter("their-word", "FRIEND"),
                    Chapter("ride-or-die", "FRIEND", (s, x) => x.path.Any(p => p.optionId == "shake")),
                    Chapter("late-nights", "FRIEND", (s, x) => Bonds.Holds(s, s.playerId, RoleIn(x, "PARTNER"), BondKinds.Showmance)),
                },
                next = (c, t) => BondNext(c, t),
                // A friend production removed is gone for good; everyone else goes on to the jury's vote.
                climax = (c, t) => c.Find(t.Role("FRIEND"))?.status == ContestantStatus.Expelled ? ThreadEndings.Parted : null,
                finale = (c, t) =>
                {
                    var friend = c.Find(t.Role("FRIEND"));
                    if (friend == null) return ThreadEndings.Parted;
                    if (friend.status == ContestantStatus.Winner || friend.status == ContestantStatus.RunnerUp) return ThreadEndings.ToTheEnd;
                    var vote = VotedForPlayer(c, friend.id);
                    return vote == null ? ThreadEndings.Parted : vote.Value ? ThreadEndings.ForYou : ThreadEndings.AgainstYou;
                },
                endings =
                {
                    [ThreadEndings.ToTheEnd] = "You and {FRIEND} made it to the end together.",
                    [ThreadEndings.ForYou] = "{FRIEND} voted for you to win.",
                    [ThreadEndings.AgainstYou] = "In the end, {FRIEND} voted against you.",
                    [ThreadEndings.Parted] = "{FRIEND} was gone before the end, and the bond went with {FRIEND.them}.",
                    [ThreadEndings.Faded] = "You and {FRIEND} never became more than friendly.",
                    [ThreadEndings.LeftHouse] = "You left the house before your story with {FRIEND} was over.",
                },
                good = { ThreadEndings.ToTheEnd, ThreadEndings.ForYou },
            },
        };

        /// <summary>
        /// The bond's next chapters: mend a grudge first; get to know them; get their word; then the end
        /// itself, a final two or a showmance. A friend who has left has only the jury left.
        /// </summary>
        private static IEnumerable<string> BondNext(StoryContext c, StoryCycle t)
        {
            string friend = t.Role("FRIEND");
            if (Gone(c.Find(friend))) yield break;
            if (c.Grudge(friend, P(c)) >= 40) yield return "settle-it";
            if (!EpisodeEngine.ChapterLanded(t, "know-them")) yield return "know-them";
            if (!EpisodeEngine.ChapterLanded(t, "their-word")) yield return "their-word";
            if (!EpisodeEngine.ChapterLanded(t, "ride-or-die") && !EpisodeEngine.ChapterLanded(t, "late-nights"))
            {
                yield return "ride-or-die";
                yield return "late-nights";
            }
        }

        // ---------------------------------------------------------------- the rivalry

        private static ArcTemplate TheRivalry() => new ArcTemplate
        {
            id = "thread-rivalry", lane = StoryLanes.Thread, eyebrow = "Your Story", title = "The Rivalry",
            origin = "native: plan 31, the rival thread: a grudge that ends in peace or a showdown",
            rulesVersion = StoryRules.Threads, oncePerHeadliner = false, cooldownWeeks = 0, pairCooldownWeeks = 0,
            roles = new[] { Departed("RIVAL") },
            cast = c =>
            {
                // Whoever holds the most against you; failing that, the coldest both ways, if cold at all.
                // Never the bond's friend.
                string friend = RoleIn(ThreadOf(c, "thread-bond"), "FRIEND");
                var pool = Npcs(c).Where(x => x.id != friend).ToList();
                var rival = pool.Where(x => c.Grudge(x.id, P(c)) >= 40)
                                .OrderByDescending(x => c.Grudge(x.id, P(c))).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault()
                            ?? pool.Where(x => Mutual(c, P(c), x.id) < 0)
                                .OrderBy(x => Mutual(c, P(c), x.id)).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
                return rival == null ? null : Bind().With("RIVAL", rival.id).Headlining(rival.id);
            },
            beats = new[] { ThreadBeat("The Rivalry") },
            thread = new ThreadTemplate
            {
                kind = ThreadKinds.Rivalry, label = "Your rivalry with {RIVAL}",
                premise = "{RIVAL} has had it in for you from the start. It ends in peace or a showdown.",
                onPlayerExit = ThreadEndings.Lost,
                chapters = new[]
                {
                    Chapter("settle-it", "RIVAL"),
                    Chapter("stir-the-pot", "RIVAL"),
                },
                next = (c, t) => RivalryNext(c, t),
                // The showdown: whoever leaves first. Peace made before then is how it ends instead.
                climax = (c, t) =>
                {
                    var rival = c.Find(t.Role("RIVAL"));
                    if (!Gone(rival)) return null;
                    return EpisodeEngine.ChapterLanded(t, "settle-it") ? ThreadEndings.MadePeace : ThreadEndings.Won;
                },
                // Still here at the finale, the rival is the other finalist: the jury decides it.
                finale = (c, t) => c.Find(t.Role("RIVAL"))?.status == ContestantStatus.Winner ? ThreadEndings.Lost
                    : EpisodeEngine.ChapterLanded(t, "settle-it") ? ThreadEndings.MadePeace : ThreadEndings.Won,
                endings =
                {
                    [ThreadEndings.MadePeace] = "You and {RIVAL} made your peace.",
                    [ThreadEndings.Won] = "{RIVAL} left the house before you did.",
                    [ThreadEndings.Lost] = "{RIVAL} outlasted you.",
                    [ThreadEndings.Faded] = "{RIVAL} stopped mattering.",
                },
                good = { ThreadEndings.MadePeace, ThreadEndings.Won },
            },
        };

        /// <summary>
        /// The rivalry's next chapters: settle it; if that fails, turn the house on them. Peace made,
        /// or both tried, and all that is left is the showdown.
        /// </summary>
        private static IEnumerable<string> RivalryNext(StoryContext c, StoryCycle t)
        {
            if (Gone(c.Find(t.Role("RIVAL"))) || EpisodeEngine.ChapterLanded(t, "settle-it")) yield break;
            yield return "settle-it";
            if (EpisodeEngine.ChapterPlayed(t, "settle-it") || c.Grudge(t.Role("RIVAL"), P(c)) < 40) yield return "stir-the-pot";
        }

        // ---------------------------------------------------------------- the numbers

        private static ArcTemplate TheNumbers() => new ArcTemplate
        {
            id = "thread-numbers", lane = StoryLanes.Thread, eyebrow = "Your Story", title = "The Numbers",
            origin = "native: plan 31, the power line: an alliance to build and hold to the final four",
            rulesVersion = StoryRules.Threads, oncePerHeadliner = false, cooldownWeeks = 0, pairCooldownWeeks = 0,
            roles = new[] { Departed("ALLY1"), Optional("ALLY2") },
            cast = c =>
            {
                string rival = RoleIn(ThreadOf(c, "thread-rivalry"), "RIVAL");
                // Your own alliance, if you have one; failing that, the two warmest who could be one.
                var mine = c.state.alliances.Where(a => a.active && a.members.Contains(P(c)))
                    .OrderByDescending(a => a.members.Count).ThenBy(a => a.id, StringComparer.Ordinal).FirstOrDefault();
                var members = mine?.members.Where(id => id != P(c) && id != rival && !Gone(c.Find(id)))
                    .OrderByDescending(id => Mutual(c, P(c), id)).ThenBy(id => id, StringComparer.Ordinal).ToList();
                if (members == null || members.Count == 0)
                {
                    var warm = Npcs(c).Where(x => x.id != rival && c.Score(x.id, P(c)) >= 5)
                        .OrderByDescending(x => Mutual(c, P(c), x.id)).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
                    members = null;
                    for (int i = 0; i < warm.Count && members == null; i++)
                        for (int j = i + 1; j < warm.Count && members == null; j++)
                            if (Mutual(c, warm[i].id, warm[j].id) >= 0) members = new List<string> { warm[i].id, warm[j].id };
                }
                if (members == null || members.Count == 0) return null;
                return Bind().With("ALLY1", members[0]).With("ALLY2", members.Count > 1 ? members[1] : null).Headlining(members[0]);
            },
            beats = new[] { ThreadBeat("The Numbers") },
            thread = new ThreadTemplate
            {
                kind = ThreadKinds.Numbers, label = "The numbers",
                premise = "Votes that move together run this house. Yours start with {ALLY1}.",
                fades = false,
                chapters = new[]
                {
                    new ThreadChapter { arcId = "build-the-numbers", focus = (c, t) => new[] { t.Role("ALLY1"), t.Role("ALLY2") } },
                    Chapter("the-secret-alliance", null),
                    Chapter("stay-off-the-block", null, null, 99),
                    Chapter("the-favour", null, null, 99),
                },
                next = (c, t) => NumbersNext(c, t),
                climax = (c, t) =>
                {
                    var allies = new[] { t.Role("ALLY1"), t.Role("ALLY2") }.Where(id => id != null).ToList();
                    bool held = allies.Any(id => !Gone(c.Find(id)) && c.state.Allied(P(c), id));
                    // The climax is the final four; an alliance with nobody left in it has already ended.
                    if (c.state.Active.Count() <= 4) return held ? ThreadEndings.Held : ThreadEndings.Broken;
                    return allies.All(id => Gone(c.Find(id))) ? ThreadEndings.Broken : null;
                },
                finale = (c, t) => new[] { t.Role("ALLY1"), t.Role("ALLY2") }.Any(id => id != null
                    && (c.Find(id)?.status == ContestantStatus.Winner || c.Find(id)?.status == ContestantStatus.RunnerUp))
                    ? ThreadEndings.Held : ThreadEndings.Broken,
                endings =
                {
                    [ThreadEndings.Held] = "Your numbers carried you to the final four.",
                    [ThreadEndings.Broken] = "Your numbers came apart before the end.",
                    [ThreadEndings.LeftHouse] = "You left the house before your numbers could carry you.",
                },
                good = { ThreadEndings.Held },
            },
        };

        /// <summary>
        /// The numbers' next chapters: make the alliance one, if it is not; find the bloc against you;
        /// then keep yourself off the block and bank favours, week to week, until the final four.
        /// </summary>
        private static IEnumerable<string> NumbersNext(StoryContext c, StoryCycle t)
        {
            string a = t.Role("ALLY1"), b = t.Role("ALLY2");
            if (a != null && b != null && !Gone(c.Find(a)) && !Gone(c.Find(b))
                && !c.state.alliances.Any(x => x.active && x.members.Contains(P(c)) && x.members.Contains(a) && x.members.Contains(b)))
                yield return "build-the-numbers";
            yield return "the-secret-alliance";
            yield return "stay-off-the-block";
            yield return "the-favour";
        }
    }
}
