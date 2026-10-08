using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Two copies of one season before its first reveal: the public mode-1 game and its exact mode-2 twin,
    /// for driving one command through both (vote family V2/V3). The mode-1 copy is walked with real public
    /// commands; the twin is <see cref="PinnedVoteSeason.Project"/> of it - every actual Vote promise and
    /// deal moved into its canonical row with the owner its identity names - and is reached only through
    /// the internal engine seam (<see cref="ProspectiveVoteFacade.Engine"/>).
    ///
    /// <para>The constructed facts a fixture may add are the ones the negotiation fixtures add: a
    /// relationship score, the season's next draw (<see cref="Draw"/>), the sequence a keyed coin reads,
    /// and rows filed the way their owners file them. Each is applied to the mode-1 copy and rebuilt through
    /// public validation before the twin is projected, so both copies hold the same facts.</para>
    ///
    /// <para>Before the first reveal only: until the reveal publishes its archive frame (vote family V4) a
    /// mode-2 reveal is refused by the core, so the twin cannot be carried past one.</para>
    /// </summary>
    internal static class ProspectiveVoteTwins
    {
        /// <summary>
        /// The staged fixtures' season walked with real public commands until <paramref name="until"/>
        /// holds, or null when it never does before the first reveal. The player competes all out for the
        /// titles named in <paramref name="allOut"/>; everything else is <see cref="EpisodeEngineTests.NextCommand"/>.
        /// </summary>
        internal static EpisodeState Walk(uint seed, Func<EpisodeState, bool> until, params EpisodePhase[] allOut)
        {
            var engine = new EpisodeEngine(PinnedVoteSeason.Fresh(seed));
            for (int step = 0; step < 400; step++)
            {
                var s = engine.Snapshot;
                if (Revealed(s)) return null;
                if (until(s)) return s;
                var command = EpisodeEngineTests.NextCommand(s);
                if (command.kind == EpisodeCommandKind.Compete && allOut.Contains(s.phase)) command.performance = 1;
                if (!engine.Apply(command).accepted) return null;
            }
            return null;
        }

        /// <summary>Whether a regular reveal has been counted: its power row holds the tally.</summary>
        internal static bool Revealed(EpisodeState s) => s.evictionResolved || s.ledger.power.Any(p => p.tally.Count > 0);

        /// <summary>The first seed in 1..32 whose walk <paramref name="build"/> accepts, or a failure naming every miss.</summary>
        internal static EpisodeState Find(string what, Func<uint, EpisodeState> build)
        {
            var misses = new List<string>();
            for (uint seed = 1; seed <= 32; seed++)
            {
                var s = build(seed);
                if (s != null)
                {
                    TestContext.Out.WriteLine(what + ": seed " + seed + ".");
                    return s;
                }
                misses.Add(seed.ToString());
            }
            Assert.Fail("No seed in 1..32 gives " + what + " (tried " + string.Join(", ", misses) + ").");
            return null;
        }

        /// <summary>Asserts a constructed mode-1 copy still passes public validation, and returns it.</summary>
        internal static EpisodeState Valid(EpisodeState s)
        {
            Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
            return s;
        }

        /// <summary>The exact mode-2 twin of a mode-1 copy before its first reveal, checked by the complete core.</summary>
        internal static EpisodeState Twin(EpisodeState mode1)
        {
            Assert.That(mode1.unifiedCommitmentRulesVersion, Is.EqualTo(1));
            Assert.That(Revealed(mode1), Is.False, "A twin is projected before the first reveal only.");
            var twin = PinnedVoteSeason.Project(mode1, Owners(mode1), Array.Empty<UnifiedVoteRevealState>());
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(twin, out var error), Is.True, error);
            return twin;
        }

        /// <summary>
        /// The owner each actual Vote row's identity names, before the first reveal: bound in its own week,
        /// first judged at this week's reveal; an unanswered offer is unbound.
        /// </summary>
        internal static Dictionary<string, ProspectiveVoteOwner> Owners(EpisodeState s)
        {
            var owners = new Dictionary<string, ProspectiveVoteOwner>(StringComparer.Ordinal);
            foreach (var promise in s.promises.Where(row => row.kind == PromiseKind.Vote))
                owners.Add(promise.id, new ProspectiveVoteOwner {
                    Origin = promise.id.StartsWith("promise-npc-", StringComparison.Ordinal) ? UnifiedCommitments.NpcPromise
                        : promise.targetId == null ? UnifiedCommitments.StoryPromise : UnifiedCommitments.PlayerPromise,
                    BindingWeek = promise.week, FirstWeek = promise.week });
            foreach (var deal in s.deals.Where(row => KnownBallots.IsVoteDeal(row.type)))
            {
                string origin = PinnedVoteSeason.DealOrigin(s, deal);
                Assert.That(origin, Is.Not.Null, "Every actual Vote deal names its owner: " + deal.id);
                bool unbound = origin == UnifiedCommitments.NpcOffer && deal.status != DealStatus.Active;
                owners.Add(deal.id, new ProspectiveVoteOwner { Origin = origin,
                    BindingWeek = unbound ? 0 : s.week, FirstWeek = unbound ? 0 : s.week });
            }
            return owners;
        }

        /// <summary>One command through the public mode-1 engine and through the seam on the twin.</summary>
        internal static (CommandResult legacy, CommandResult prospective) Both(EpisodeState mode1, EpisodeCommand command)
        {
            var twin = Twin(mode1);
            string before = PinnedVoteSeason.Json(twin);
            var legacy = new EpisodeEngine(mode1).Apply(command);
            var engine = ProspectiveVoteFacade.Engine(twin);
            var prospective = engine.Apply(command);
            Assert.That(PinnedVoteSeason.Json(twin), Is.EqualTo(before), "The seam never touches the state it was given.");
            if (!prospective.accepted) Assert.That(PinnedVoteSeason.Json(engine.Snapshot), Is.EqualTo(before), "A refusal installs nothing.");
            else Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(prospective.state, out var error), Is.True, error);
            return (legacy, prospective);
        }

        /// <summary>
        /// The mode-2 command's result is the mode-1 result with its Vote rows moved to their canonical owners:
        /// the same ids, sequence, draws, lines, scores, memories and ledger, field for field.
        /// </summary>
        internal static void AssertParity(EpisodeState legacy, EpisodeState prospective, string what, params string[] readerGaps)
        {
            var expected = Normal(PinnedVoteSeason.Project(legacy, Owners(legacy), Array.Empty<UnifiedVoteRevealState>()));
            var actual = Normal(prospective);
            // A reader not yet moved to the canonical rows (vote family V5) may word a line differently. Each
            // gap is named by its event kind, must actually differ, and differs in that line's text only.
            foreach (string kind in readerGaps)
            {
                var left = ((JArray)expected["events"]).Where(e => (string)e["kind"] == kind).ToList();
                var right = ((JArray)actual["events"]).Where(e => (string)e["kind"] == kind).ToList();
                Assert.That(right.Count, Is.EqualTo(left.Count), what + ": the " + kind + " lines are the same lines.");
                Assert.That(left.Where((e, i) => (string)e["text"] != (string)right[i]["text"]), Is.Not.Empty,
                    what + ": the named reader gap '" + kind + "' no longer differs - remove it from this case.");
                foreach (var e in left.Concat(right)) e["text"] = "(reader gap: " + kind + ")";
            }
            var differing = expected.Properties().Select(p => p.Name)
                .Where(name => !JToken.DeepEquals(expected[name], actual[name])).ToList();
            Assert.That(differing, Is.Empty, what + ": the mode-2 command must equal the mode-1 command with its Vote rows canonical. "
                + string.Join(" ", differing.Select(name => FirstDifference(name, expected[name], actual[name]))));
        }

        private static string FirstDifference(string name, JToken expected, JToken actual)
        {
            if (expected is JArray left && actual is JArray right)
                for (int i = 0; i < Math.Max(left.Count, right.Count); i++)
                    if (i >= left.Count || i >= right.Count || !JToken.DeepEquals(left[i], right[i]))
                        return name + "[" + i + "]: mode 1 " + (i < left.Count ? left[i].ToString(Newtonsoft.Json.Formatting.None) : "none")
                            + " / mode 2 " + (i < right.Count ? right[i].ToString(Newtonsoft.Json.Formatting.None) : "none") + ".";
            return name + ": mode 1 " + expected?.ToString(Newtonsoft.Json.Formatting.None) + " / mode 2 " + actual?.ToString(Newtonsoft.Json.Formatting.None) + ".";
        }

        private static JObject Normal(EpisodeState s)
        {
            var copy = s.Clone();
            copy.unifiedCommitments = copy.unifiedCommitments.OrderBy(row => row.id, StringComparer.Ordinal).ToList();
            return JObject.FromObject(copy);
        }

        /// <summary>A command from the player, at the state's own revision and phase.</summary>
        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null,
            string text = null, string tag = null) => new EpisodeCommand
        {
            id = "vote-v3-" + kind + "-" + s.revision + "-" + (tag ?? target), actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
            targetId = target, secondTargetId = second, text = text,
        };

        /// <summary>One way of the relationship, as the negotiation fixtures set it.</summary>
        internal static void Set(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>A season's next draw that says yes, or no, to a chance in percent.</summary>
        internal static uint Draw(bool yes, double chance)
        {
            for (uint n = 1; n < 10000; n++)
            {
                uint state = n * 2654435761u;
                if ((new SeededRandom(state).NextDouble() * 100 < chance) == yes) return state;
            }
            Assert.Fail("No draw says " + (yes ? "yes" : "no") + " to " + chance + ".");
            return 0;
        }

        /// <summary>What a refusal must leave as it was: nothing spent, drawn, minted or written.</summary>
        internal static void AssertUntouched(EpisodeState before, CommandResult result, string what)
        {
            Assert.That(result.accepted, Is.False, what + ": refused.");
            Assert.That((result.state.revision, result.state.randomState, result.state.nextSequence),
                Is.EqualTo((before.revision, before.randomState, before.nextSequence)), what + ": nothing spent, drawn or minted.");
            Assert.That(PinnedVoteSeason.Json(result.state), Is.EqualTo(PinnedVoteSeason.Json(before)), what + ": nothing written.");
        }
    }
}
