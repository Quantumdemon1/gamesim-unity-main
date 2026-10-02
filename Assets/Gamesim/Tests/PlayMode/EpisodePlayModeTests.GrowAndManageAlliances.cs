using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Growing and managing the player's pacts on screen (ACTIONS-DEALS-ALLIANCES-PLAN C5), in a house
    /// that plays the commitment rules: "Bring {name} into {pact}" in BARGAIN for each of the player's
    /// pacts the houseguest is not in, carrying the player's read of the one asked under the one note
    /// that says whose read it is, and locked under the reason at four; none for an ally of the
    /// player's, and every one locked under one line for somebody the player has soured on; "Leave
    /// {pact}" a row a pact where two are shared, the pill saying when a pact goes on without the
    /// player, and "Leave our alliance" naming the one pact otherwise; "Rename {pact}…" opening the names
    /// on offer for a pact the player founded, free, and locked under the reason once used that week.
    /// Without the rules none of it is offered. Each press commits exactly what the engine's command
    /// does. The longest captions - a pact of four named for its people, renamed to the longest of the
    /// web's names - fit at both text sizes. Photographed in a batch run at both text sizes, on a 16:9
    /// frame and a 4:3 one, as 'conversation-pact-bring-in', 'conversation-pact-leave',
    /// 'conversation-pact-rename', 'conversation-pact-longest' and 'conversation-pact-longest-ask'. The
    /// engine's half is GrowAndManageAlliancesTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string GrowPactId = "alliance-grow-screen", GrowPactName = "The Screen Pact";
        private const string PairPactId = "alliance-pair-screen", PairPactName = "The Pair";
        private const string TrioPactId = "alliance-trio-screen", TrioPactName = "The Trio";

        /// <summary>The houseguests of a talking house, in cast order: who the fixtures below put where.</summary>
        private static List<ContestantState> HouseNpcs(EpisodeState state) => state.Active.Where(actor => !actor.isPlayer).ToList();

        /// <summary>Whether a houseguest has a body in the house to talk to.</summary>
        private bool HasBody(string id) => SceneComponents<HouseNpc>().Any(actor => actor.Id == id && actor.gameObject.activeInHierarchy);

        /// <summary>
        /// The rules on, and a pact of the player's with the last houseguest, who would welcome the first
        /// in it: they dislike the same two others, and the member thinks the world of them. The first,
        /// warm on the player, is the one asked.
        /// </summary>
        private static void PactToGrow(EpisodeState state)
        {
            EpisodeEngine.EnableCommitments(state);
            var npcs = HouseNpcs(state);
            var asked = npcs[0];
            var member = npcs[npcs.Count - 1];
            state.alliances.Add(new AllianceState
            {
                id = GrowPactId, name = GrowPactName, active = true, members = new List<string> { state.playerId, member.id },
            });
            Reading(state, member.id, asked.id, 100);
            foreach (var threat in new[] { npcs[1], npcs[2] })
            {
                Reading(state, member.id, threat.id, -60);
                Reading(state, asked.id, threat.id, -60);
            }
            Reading(state, asked.id, state.playerId, 100);
            Reading(state, state.playerId, asked.id, 30);
        }

        /// <summary>The rules on, and a pact of four in the house - the player and the second to fourth houseguests - that nobody more can join.</summary>
        private static void PactOfFour(EpisodeState state)
        {
            EpisodeEngine.EnableCommitments(state);
            var npcs = HouseNpcs(state);
            state.alliances.Add(new AllianceState
            {
                id = GrowPactId, name = GrowPactName, active = true, members = new List<string> { state.playerId, npcs[1].id, npcs[2].id, npcs[3].id },
            });
        }

        /// <summary>The player and the first houseguest in two pacts: a pair, and a trio with the second.</summary>
        private static void TwoPactsShared(EpisodeState state)
        {
            var npcs = HouseNpcs(state);
            state.alliances.Add(new AllianceState { id = PairPactId, name = PairPactName, active = true, members = new List<string> { state.playerId, npcs[0].id } });
            state.alliances.Add(new AllianceState { id = TrioPactId, name = TrioPactName, active = true, members = new List<string> { state.playerId, npcs[0].id, npcs[1].id } });
        }

        /// <summary>
        /// The rules on, the two pacts shared with the first houseguest, a third pact with the fourth, and
        /// the player soured on the fifth: the second houseguest is an ally (in the trio), and the fifth is
        /// somebody the player cannot vouch for.
        /// </summary>
        private static void AllyAndSoured(EpisodeState state)
        {
            EpisodeEngine.EnableCommitments(state);
            TwoPactsShared(state);
            var npcs = HouseNpcs(state);
            state.alliances.Add(new AllianceState
            {
                id = GrowPactId, name = GrowPactName, active = true, members = new List<string> { state.playerId, npcs[3].id },
            });
            Reading(state, state.playerId, npcs[4].id, NpcAlliances.SourLine - 1);
        }

        /// <summary>
        /// The longest captions C5 draws (review m7): the rules on, a pact of four the player founded,
        /// named the house's way for the three with the longest first names ("The Riley, Jo and Sam
        /// Pact"), and a pair with the first of them, so both of that member's leave rows name their
        /// pacts. Its names on offer end with the longest of the web's, "The Hidden Council".
        /// </summary>
        private static void LongestPacts(EpisodeState state)
        {
            EpisodeEngine.EnableCommitments(state);
            var ids = HouseNpcs(state)
                .OrderByDescending(npc => npc.name.Split(' ')[0].Length).ThenBy(npc => npc.id, System.StringComparer.Ordinal)
                .Take(3).Select(npc => npc.id).ToList();
            state.alliances.Add(new AllianceState
            {
                id = GrowPactId, name = PactNames.HouseName(state, ids), active = true,
                members = new List<string> { state.playerId }.Concat(ids).ToList(),
            });
            state.alliances.Add(new AllianceState { id = PairPactId, name = PairPactName, active = true, members = new List<string> { state.playerId, ids[0] } });
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator GrowAndManage_UnderTheRulesABringInRowAsksSomebodyIntoAPact()
        {
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";
                yield return InstallTalkingHouse(8, false, PactToGrow);
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var asked = HouseNpcs(director.Snapshot)[0];
                Assert.That(HasBody(asked.id), Is.True, "The one asked has a body to talk to.");
                yield return TalkTo(asked.id);
                string where = "A conversation with " + asked.name + " under the commitment rules" + size;
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var open = director.Snapshot;
                Assert.That(EpisodeEngine.CommitmentRulesOn(open), Is.True, where + ": the house plays the commitment rules.");
                var pact = open.alliances.Single(a => a.id == GrowPactId);
                string caption = EpisodeDirector.BringInCaption(asked.name, pact.name);
                var row = ButtonWithCaption(caption);
                Assert.That(TagOn(row), Is.EqualTo(EpisodeDirector.VerbTag(EpisodeCommandKind.BringIntoAlliance) + " · " + KnownOdds.Alliance(open, asked.id).word),
                    where + ": what it is for, and the player's read of the one asked - never the roll's number, nothing of the members' say.");
                var notes = LiveRects(EpisodeHud.OddsNoteName);
                Assert.That(notes, Has.Count.EqualTo(1), where + ": one note says whose read the chances are,");
                Assert.That(notes[0].parent, Is.SameAs(row.transform.parent), where + ": in the row's column,");
                Assert.That(notes[0].GetSiblingIndex(), Is.LessThan(row.transform.GetSiblingIndex()), where + ": above it.");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-pact-bring-in" + (larger ? "-large" : ""), where, null);

                if (!larger)
                {
                    // One press, one command: exactly what the engine's asking commits.
                    var before = director.Snapshot;
                    var expected = new EpisodeEngine(before).Apply(new EpisodeCommand
                    {
                        id = "bring-in-expectation", actorId = before.playerId, kind = EpisodeCommandKind.BringIntoAlliance,
                        targetId = asked.id, secondTargetId = GrowPactId, expectedRevision = before.revision, expectedPhase = before.phase,
                    });
                    Assert.That(expected.accepted, Is.True, expected.reason);
                    ButtonWithCaption(caption).onClick.Invoke();
                    yield return null;
                    var after = director.Snapshot;
                    Assert.That(after.revision, Is.EqualTo(before.revision + 1), "One press, one command.");
                    Assert.That(after.randomState, Is.EqualTo(expected.state.randomState), "It drew what the asking draws,");
                    Assert.That(after.alliances.Single(a => a.id == GrowPactId).members, Is.EqualTo(expected.state.alliances.Single(a => a.id == GrowPactId).members),
                        "left the pact as the asking left it,");
                    Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before) + 1), "and spent the action.");
                    Assert.That(after.alliances.Single(a => a.id == GrowPactId).members[0], Is.EqualTo(after.playerId), "The founder is still first.");
                }
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);

            // A pact with four in the house: the row is locked under the reason and commits nothing.
            yield return InstallTalkingHouse(8, false, PactOfFour);
            yield return SettleCast();
            var outsider = HouseNpcs(director.Snapshot)[0];
            Assert.That(HasBody(outsider.id), Is.True);
            yield return TalkTo(outsider.id);
            const string full = "A conversation at a pact of four";
            Assert.That(director.IsConversationOpen, Is.True, full + ": the conversation opens.");
            var held = director.Snapshot;
            var four = held.alliances.Single(a => a.id == GrowPactId);
            var locked = FindButton(EpisodeDirector.BringInCaption(outsider.name, four.name));
            Assert.That(locked.interactable, Is.False, full + ": the row is drawn locked,");
            AssertDirectlyOver(LiveLabel(EpisodeEngine.BringInRefusal(held, outsider.id, four)), locked, full + ": under the reason");
            int revision = director.Snapshot.revision;
            locked.onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), full + ": and it commits nothing.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator GrowAndManage_UnderTheRulesALeaveNamesItsPactAndAPactOfThreeGoesOnWithoutThePlayer()
        {
            yield return InstallTalkingHouse(8, false, state =>
            {
                EpisodeEngine.EnableCommitments(state);
                TwoPactsShared(state);
            });
            yield return SettleCast();
            var npcs = HouseNpcs(director.Snapshot);
            Assert.That(HasBody(npcs[0].id), Is.True);
            yield return TalkTo(npcs[0].id);
            const string where = "A conversation with the partner in two pacts";
            Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
            Assert.That(ButtonWithCaptionOrNull("Leave our alliance"), Is.Null, where + ": with two pacts between them, no row says 'our alliance'.");
            var pair = ButtonWithCaption(EpisodeDirector.LeavePactCaption(PairPactName));
            var trio = ButtonWithCaption(EpisodeDirector.LeavePactCaption(TrioPactName));
            Assert.That(TagOn(pair), Is.EqualTo(EpisodeDirector.VerbTag(EpisodeCommandKind.LeaveAlliance)), where + ": leaving the pair costs what a leave costs;");
            Assert.That(TagOn(trio), Is.EqualTo(EpisodeDirector.LeaveGoesOnTag), where + ": leaving the trio too, and it goes on without the player.");
            AssertConversationFits(false, where);
            AssertTagsHaveRoom(where);
            yield return CaptureConversation("conversation-pact-leave", where, null);

            int revision = director.Snapshot.revision;
            ButtonWithCaption(EpisodeDirector.LeavePactCaption(TrioPactName)).onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "One press, one command.");
            Assert.That(after.alliances.Single(a => a.id == TrioPactId).members, Is.EqualTo(new[] { npcs[0].id, npcs[1].id }), "The trio goes on without the player,");
            Assert.That(after.alliances.Single(a => a.id == TrioPactId).active, Is.True);
            Assert.That(after.alliances.Single(a => a.id == PairPactId).active, Is.True, "and the pair is untouched.");

            // One pact between them now: the row is the one it always was, and it names the pair.
            var only = ButtonWithCaption("Leave our alliance");
            Assert.That(TagOn(only), Is.EqualTo(EpisodeDirector.VerbTag(EpisodeCommandKind.LeaveAlliance)), "A pact of two: it ends.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.LeavePactCaption(PairPactName)), Is.Null, "One pact, one row.");
            revision = after.revision;
            only.onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision + 1));
            Assert.That(director.Snapshot.alliances.Single(a => a.id == PairPactId).active, Is.False, "'Leave our alliance' leaves the pair, which ends.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator GrowAndManage_UnderTheRulesTheFounderRenamesAPactFromTheNamesOnOffer()
        {
            foreach (bool larger in new[] { false, true })
            {
                yield return InstallTalkingHouse(8, false, state =>
                {
                    EpisodeEngine.EnableCommitments(state);
                    state.alliances.Add(new AllianceState
                    {
                        id = GrowPactId, name = GrowPactName, active = true, members = new List<string> { state.playerId, HouseNpcs(state)[0].id },
                    });
                });
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var partner = HouseNpcs(director.Snapshot)[0];
                Assert.That(HasBody(partner.id), Is.True);
                yield return TalkTo(partner.id);
                string where = "A conversation with a partner in a pact the player founded" + (larger ? " at the larger text" : "");
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                var open = director.Snapshot;
                var pact = open.alliances.Single(a => a.id == GrowPactId);
                string verb = EpisodeDirector.RenamePickerCaption(pact.name);
                var names = PactNames.For(open, pact);
                Assert.That(names, Is.Not.Empty, where + ": there are names on offer.");
                Assert.That(TagOn(ButtonWithCaption(verb)), Is.EqualTo(EpisodeDirector.FreeTag), where + ": free.");
                foreach (string name in names)
                    Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.RenameCaption(pact.name, name)), Is.Null, where + ": '" + name + "' waits behind the row.");
                yield return PressRow(verb);
                Assert.That(director.ConversationPicker, Is.EqualTo(verb), where + ": the row opens the names on offer.");
                foreach (string name in names)
                    Assert.That(ButtonWithCaption(EpisodeDirector.RenameCaption(pact.name, name)), Is.Not.Null, where + ": '" + name + "' is on offer.");
                AssertConversationFits(larger, where + " with its names open");
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-pact-rename" + (larger ? "-large" : ""), where, null);

                if (!larger)
                {
                    var before = director.Snapshot;
                    string chosen = names.Last();
                    ButtonWithCaption(EpisodeDirector.RenameCaption(pact.name, chosen)).onClick.Invoke();
                    yield return null;
                    var after = director.Snapshot;
                    Assert.That(after.revision, Is.EqualTo(before.revision + 1), "One press, one command.");
                    Assert.That(after.alliances.Single(a => a.id == GrowPactId).name, Is.EqualTo(chosen), "The pact has its new name,");
                    Assert.That(after.randomState, Is.EqualTo(before.randomState), "for no roll,");
                    Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)), "and no action.");
                    Assert.That(director.ConversationPicker, Is.Null, "Chosen, the names shut.");
                    // Once a week: the row stands locked under the reason, and commits nothing.
                    var locked = FindButton(EpisodeDirector.RenamePickerCaption(chosen));
                    Assert.That(locked.interactable, Is.False, "Renamed this week, the row is locked");
                    AssertDirectlyOver(LiveLabel(EpisodeEngine.RenameRefusal(after, after.alliances.Single(a => a.id == GrowPactId))), locked, "under the reason");
                    locked.onClick.Invoke();
                    yield return null;
                    Assert.That(director.Snapshot.revision, Is.EqualTo(after.revision), "and commits nothing.");
                }
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator GrowAndManage_UnderTheRulesAnAllyIsAskedIntoNoOtherPactAndTheSouredAreLockedUnderOneLine()
        {
            yield return InstallTalkingHouse(8, false, AllyAndSoured);
            yield return SettleCast();
            var npcs = HouseNpcs(director.Snapshot);

            // An ally - in the trio - is asked into none of the player's other pacts: no row, locked or open.
            Assert.That(HasBody(npcs[1].id), Is.True, "The ally has a body to talk to.");
            yield return TalkTo(npcs[1].id);
            const string ally = "A conversation with an ally in the trio";
            Assert.That(director.IsConversationOpen, Is.True, ally + ": the conversation opens.");
            foreach (string pact in new[] { PairPactName, GrowPactName })
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.BringInCaption(npcs[1].name, pact)), Is.Null, ally + ": no row asks them into " + pact + ".");
            director.ClosePanels();
            yield return null;

            // Somebody the player has soured on: a row for each pact, every one locked, under one line
            // about them, said once, over the first.
            Assert.That(HasBody(npcs[4].id), Is.True, "The soured-on houseguest has a body to talk to.");
            yield return TalkTo(npcs[4].id);
            string soured = "A conversation with " + npcs[4].name + ", whom the player has soured on";
            Assert.That(director.IsConversationOpen, Is.True, soured + ": the conversation opens.");
            var rows = new[] { PairPactName, TrioPactName, GrowPactName }
                .Select(pact => FindButton(EpisodeDirector.BringInCaption(npcs[4].name, pact))).ToList();
            foreach (var row in rows) Assert.That(row.interactable, Is.False, soured + ": every row is drawn locked.");
            AssertDirectlyOver(LiveLabel(EpisodeEngine.CannotVouchRefusal(npcs[4].name)), rows[0], soured + ": under one line, over the first row");
            AssertConversationFits(false, soured);
            int revision = director.Snapshot.revision;
            rows[0].onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), soured + ": and a press commits nothing.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator GrowAndManage_UnderTheRulesTheLongestPactCaptionsFit()
        {
            foreach (bool larger in new[] { false, true })
            {
                string size = larger ? " at the larger text" : "";
                yield return InstallTalkingHouse(8, false, LongestPacts);
                yield return SettleCast();
                yield return ApplyTextSize(larger);
                var open = director.Snapshot;
                var four = open.alliances.Single(a => a.id == GrowPactId);
                var partner = open.Find(four.members[1]);
                Assert.That(HasBody(partner.id), Is.True, "The partner in both pacts has a body to talk to.");

                // A member of both: each leave row names its pact, and the four's names are open, the
                // longest of the web's among them.
                yield return TalkTo(partner.id);
                string where = "A conversation with " + partner.name + ", in a pact of four and a pair" + size;
                Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
                string leaveFour = EpisodeDirector.LeavePactCaption(four.name);
                Assert.That(TagOn(ButtonWithCaption(leaveFour)), Is.EqualTo(EpisodeDirector.LeaveGoesOnTag), where + ": '" + leaveFour + "', which goes on without the player;");
                Assert.That(TagOn(ButtonWithCaption(EpisodeDirector.LeavePactCaption(PairPactName))), Is.EqualTo(EpisodeDirector.VerbTag(EpisodeCommandKind.LeaveAlliance)),
                    where + ": the pair's own row.");
                string verb = EpisodeDirector.RenamePickerCaption(four.name);
                yield return PressRow(verb);
                Assert.That(director.ConversationPicker, Is.EqualTo(verb), where + ": the four's names are open.");
                string longest = EpisodeDirector.RenameCaption(four.name, "The Hidden Council");
                Assert.That(ButtonWithCaption(longest), Is.Not.Null, where + ": '" + longest + "' is on offer.");
                AssertConversationFits(larger, where + " with its names open");
                AssertTagsHaveRoom(where);
                yield return CaptureConversation("conversation-pact-longest" + (larger ? "-large" : ""), where, null);
                director.ClosePanels();
                yield return null;

                // Outside both, the houseguest with the longest name: asked into the four, locked under the
                // reason; into the pair, open.
                var outsider = HouseNpcs(open).Where(npc => !four.members.Contains(npc.id))
                    .OrderByDescending(npc => npc.name.Length).ThenBy(npc => npc.id, System.StringComparer.Ordinal).First();
                Assert.That(HasBody(outsider.id), Is.True, "The one asked has a body to talk to.");
                yield return TalkTo(outsider.id);
                string asking = "A conversation with " + outsider.name + ", outside both" + size;
                Assert.That(director.IsConversationOpen, Is.True, asking + ": the conversation opens.");
                var held = director.Snapshot;
                var locked = FindButton(EpisodeDirector.BringInCaption(outsider.name, four.name));
                Assert.That(locked.interactable, Is.False, asking + ": the four's row is drawn locked,");
                AssertDirectlyOver(LiveLabel(EpisodeEngine.BringInRefusal(held, outsider.id, held.alliances.Single(a => a.id == GrowPactId))), locked,
                    asking + ": under the reason");
                Assert.That(ButtonWithCaption(EpisodeDirector.BringInCaption(outsider.name, PairPactName)), Is.Not.Null, asking + ": and the pair's is open.");
                AssertConversationFits(larger, asking);
                AssertTagsHaveRoom(asking);
                yield return CaptureConversation("conversation-pact-longest-ask" + (larger ? "-large" : ""), asking, null);
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        [UnityTest]
        public IEnumerator GrowAndManage_WithoutTheRulesNoneOfItIsOffered()
        {
            yield return InstallTalkingHouse(8, false, state =>
            {
                TwoPactsShared(state);
                state.alliances.Add(new AllianceState
                {
                    id = GrowPactId, name = GrowPactName, active = true, members = new List<string> { state.playerId, HouseNpcs(state)[3].id },
                });
            });
            yield return SettleCast();
            var npcs = HouseNpcs(director.Snapshot);
            Assert.That(EpisodeEngine.CommitmentRulesOn(director.Snapshot), Is.False, "A house without the commitment rules.");
            yield return TalkTo(npcs[0].id);
            Assert.That(director.IsConversationOpen, Is.True);
            Assert.That(ButtonWithCaption("Leave our alliance"), Is.Not.Null, "The one row it always had, with two pacts between them,");
            foreach (string caption in new[]
            {
                EpisodeDirector.LeavePactCaption(PairPactName), EpisodeDirector.LeavePactCaption(TrioPactName),
                EpisodeDirector.RenamePickerCaption(PairPactName), EpisodeDirector.RenamePickerCaption(TrioPactName),
                EpisodeDirector.BringInCaption(npcs[0].name, GrowPactName),
            })
                Assert.That(ButtonWithCaptionOrNull(caption), Is.Null, "and nothing new: '" + caption + "'.");
            director.ClosePanels();
            yield return null;
        }
    }
}
