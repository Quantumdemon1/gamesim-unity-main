using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A play (plan 30 §2): an arc with a goal the player can win or lose. The goal is checked
    /// against the season, never against the arc's own bookkeeping, so anything the player does
    /// that gets there counts: a beat's answer, a conversation, the windows' lobby.
    ///
    /// <para>A play's first beat is its offer (<see cref="PlayOptions.TakeItOn"/> or
    /// <see cref="PlayOptions.NotNow"/>). Once taken on, the play is decided the moment its goal
    /// is met, or at its <see cref="deadline"/> anchor if it never is (or sooner, the moment
    /// <see cref="failed"/> says it no longer can be): won, part-won or lost, each with its own consequences. The outcome is the cycle's ending, in the ledger's own
    /// words (<see cref="PlayEndings"/>), so a play needs no saved field of its own.</para>
    /// </summary>
    public sealed class PlayTemplate
    {
        /// <summary>What it pays: one of <see cref="PlayCurrencies"/>.</summary>
        public string currency;

        /// <summary>The goal in one sentence, with role tokens: what winning means.</summary>
        public string goal;

        /// <summary>The anchor whose Advance decides it if the goal has not been met by then.</summary>
        public string deadline;

        /// <summary>How far along it is, read from the season. Won the moment <c>have</c> reaches <c>need</c>.</summary>
        public Func<StoryContext, StoryCycle, PlayProgress> progress;

        /// <summary>
        /// Optional: whether the goal can no longer be met, which decides the play at once, as its
        /// deadline would. Null: only the deadline ends a play that is not won.
        /// </summary>
        public Func<StoryContext, StoryCycle, bool> failed;

        /// <summary>Consequences and the line that says what happened, per outcome. Role tokens throughout.</summary>
        public Fx[] won = Array.Empty<Fx>(), part = Array.Empty<Fx>(), lost = Array.Empty<Fx>();
        public string wonOutcome, partOutcome, lostOutcome;

        /// <summary>Whether a deadline that finds some progress is a part-win rather than a loss.</summary>
        public bool PartCounts => partOutcome != null;
    }

    /// <summary>A play's progress: <see cref="have"/> of <see cref="need"/>.</summary>
    public readonly struct PlayProgress
    {
        public readonly int have, need;
        public PlayProgress(int have, int need) { this.have = Math.Max(0, have); this.need = Math.Max(1, need); }
        public bool Met => have >= need;
        public static PlayProgress Done(bool done) => new PlayProgress(done ? 1 : 0, 1);
    }

    /// <summary>What a play pays (plan 30 §2): the owner's four currencies.</summary>
    public static class PlayCurrencies
    {
        public const string Trust = "trust", Intel = "intel", Alliance = "alliance", Power = "power";
        public static readonly string[] All = { Trust, Intel, Alliance, Power };
        public static bool IsKnown(string currency) => currency != null && Array.IndexOf(All, currency) >= 0;

        public static string Label(string currency)
        {
            switch (currency)
            {
                case Trust: return "Trust";
                case Intel: return "Intel";
                case Alliance: return "Alliance";
                case Power: return "Power";
                default: return "Play";
            }
        }
    }

    /// <summary>How a play ended: the ledger's outcome words, plus an offer turned down.</summary>
    public static class PlayEndings
    {
        public const string Won = "won", Part = "part", Lost = "lost", Declined = "declined";
        public static bool IsDecided(string ending) => ending == Won || ending == Part || ending == Lost;
    }

    /// <summary>A play offer's two options. Their ids are fixed so the engine can tell a play taken on.</summary>
    public static class PlayOptions
    {
        public const string TakeItOn = "take-it-on", NotNow = "not-now";
        public const string TakeItOnLabel = "Take it on", NotNowLabel = "Not now";
    }

    /// <summary>
    /// What a play changed, said to the player (plan 30 §4). One line per effect that the player
    /// would know about, taken from the effect that ran and the season after it ran, never a
    /// paraphrase. Promises, deals, alliances formed and lore already log their own lines, so they
    /// are left to those.
    /// </summary>
    public static class PlayReceipts
    {
        /// <summary>
        /// The receipts without the pacts the spreads granted. Under the leak rules an alliance spread
        /// gets no line this way: asked after the grant, the pact of the two the player now knows of may
        /// not be the one granted, so the engine passes what it resolved before (the overload below).
        /// </summary>
        public static IEnumerable<string> For(EpisodeState s, IEnumerable<StoryEffectState> applied) => For(s, applied, null);

        /// <summary>
        /// The same, with the pact each alliance spread granted under the leak rules, resolved before it
        /// was applied (<see cref="Knowledge.PactOfPair"/>), so the receipt names the pact the player was
        /// given and no other of the same two people. A spread with no pact here gets no line.
        /// </summary>
        public static IEnumerable<string> For(EpisodeState s, IEnumerable<StoryEffectState> applied,
            IReadOnlyDictionary<StoryEffectState, AllianceState> spread)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in applied ?? Enumerable.Empty<StoryEffectState>())
            {
                string line = Line(s, e, spread);
                if (line != null && seen.Add(line)) yield return line;
            }
        }

        private static string Line(EpisodeState s, StoryEffectState e, IReadOnlyDictionary<StoryEffectState, AllianceState> spread)
        {
            if (e == null) return null;
            var from = s.Find(e.fromId);
            var to = s.Find(e.toId);
            switch (e.kind)
            {
                case StoryEffects.Move:
                {
                    if (from == null || to == null || (!from.isPlayer && !to.isPlayer)) return null;
                    var other = from.isPlayer ? to : from;
                    if (Math.Abs(e.amount) < 0.5) return null;
                    return e.amount > 0 ? other.name + " warmed to you." : other.name + " cooled on you.";
                }
                case StoryEffects.Hook:
                    return from != null && from.isPlayer && to != null ? to.name + " owes you a favour." : null;
                case StoryEffects.HookSpend:
                    return from != null && from.isPlayer && to != null ? "You called in " + to.name + "'s favour." : null;
                case StoryEffects.Grudge:
                    // Receipts are only written for a play's effects, so a grudge between two others is the player's doing.
                    if (from == null || to == null || from.isPlayer) return null;
                    return to.isPlayer ? from.name + " is holding it against you." : from.name + " is holding it against " + to.name + ".";
                case StoryEffects.Ease:
                    return from != null && to != null && to.isPlayer ? from.name + " let some of it go." : null;
                case StoryEffects.Told:
                    if (from == null || to == null || !to.isPlayer || from.isPlayer) return null;
                    return e.type == StoryReceipts.TooNosy ? from.name + " knows you have been asking questions." : from.name + " found out.";
                case StoryEffects.Spread:
                {
                    // An alliance out in the open, or leaked to the player: say who is in it.
                    if (e.type != FactKinds.Alliance || from == null || to == null) return null;
                    bool toPlayer = e.thirdId == s.playerId || (e.thirdId == null && e.text == FactVisibility.Public);
                    if (!toPlayer) return null;
                    // Under the leak rules the receipt names the one pact the spread granted (one pair, one pact).
                    var alliance = AllianceLeaks.On(s) ? Granted(e, spread)
                        : s.alliances.FirstOrDefault(a => a.members.Contains(from.id) && a.members.Contains(to.id));
                    if (alliance == null || alliance.members.Contains(s.playerId)) return null;
                    return "You learned: " + Names(s, alliance.members) + " are working together.";
                }
                case StoryEffects.PhaseBonus:
                    return e.type == "competition" && e.amount > 0 ? "You go into the next competition with an edge." : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The pact an alliance spread granted under the leak rules: the one resolved before it was
        /// applied, where the engine kept it. Otherwise none: the engine keeps one for every spread that
        /// granted anything, and asked after the fact, the pact of the two the player now knows of can be
        /// another one of the same two people (a known pact listed before the one just granted).
        /// </summary>
        private static AllianceState Granted(StoryEffectState e, IReadOnlyDictionary<StoryEffectState, AllianceState> spread) =>
            spread != null && spread.TryGetValue(e, out var resolved) ? resolved : null;

        /// <summary>"Riley, Jo and Sam": names in the alliance's own order.</summary>
        private static string Names(EpisodeState s, IList<string> ids)
        {
            var names = ids.Select(id => s.Find(id)?.name).Where(n => n != null).ToList();
            if (names.Count <= 1) return names.FirstOrDefault() ?? "someone";
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last();
        }
    }
}
