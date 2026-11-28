using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Dialogue rules 2's foundation as data (DIALOGUE-PLAN §2, slice D2): the version as a bound over a closed set, each
    /// version at the schema that introduced it, the per-version bounds, the entry's funding and its tick, and the closed
    /// content registry with the vote-word tree's shape. None of it plays a season, so all of it holds at any schema.
    /// </summary>
    public sealed class DialogueRules2LadderTests
    {
        [Test]
        public void TheVersionIsABoundOverAClosedSetEachAtTheSchemaThatIntroducedIt()
        {
            // Repinned at D5b-1, which defines rules 5 (the HoH consultation), switched off: the chip still writes 4.
            Assert.That(DialogueRules.Version, Is.EqualTo(5), "The highest version this build defines: 5 since D5b-1 defined the HoH consultation.");
            Assert.That(Enumerable.Range(-2, 9).Where(DialogueRules.IsDefined), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(DialogueRules.IsDefined(DialogueRules.Version + 1), Is.False);
            Assert.That(DialogueRules.SchemaFor(0), Is.EqualTo(29));
            Assert.That(DialogueRules.SchemaFor(1), Is.EqualTo(29));
            Assert.That(DialogueRules.SchemaFor(2), Is.EqualTo(30), "Decision 7: rules 2 is defined only from schema 30.");
            foreach (int schema in new[] { 28, 29, 30, 31 })
            {
                Assert.That(DialogueRules.Admits(0, schema), Is.EqualTo(schema >= 29));
                Assert.That(DialogueRules.Admits(1, schema), Is.EqualTo(schema >= 29), "Rules 1 is on at 29 and every later schema.");
                Assert.That(DialogueRules.Admits(2, schema), Is.EqualTo(schema >= 30));
                Assert.That(DialogueRules.Admits(3, schema), Is.EqualTo(schema >= 30), "Rules 3 is defined from 30, as rules 2 is (D3).");
                Assert.That(DialogueRules.Admits(4, schema), Is.EqualTo(schema >= 30), "Rules 4 is defined from 30, as rules 2 and 3 are (D4).");
                Assert.That(DialogueRules.Admits(5, schema), Is.EqualTo(schema >= 30), "Rules 5 is defined from 30, as rules 2 to 4 are (D5b-1).");
                Assert.That(DialogueRules.Admits(DialogueRules.Version + 1, schema), Is.False, "An undefined version is never admitted.");
                Assert.That(DialogueRules.Admits(-1, schema), Is.False);
            }
        }

        /// <summary>
        /// The chip writes the pinned <see cref="DialogueRules.Offered"/>: rules 4 since D5c moved it from rules 3 (which D3
        /// froze), and that move freezes rules 4 as D4, D5a and D5c built it (§2: a version freezes the first time a shipped
        /// path can write it). It is a defined version a new season's schema admits, never the bound by reference, and the
        /// shipped default stays on 0 until D7. Re-pinned and renamed from TheChipWritesRules2AndTheDefaultStaysOn0 by D3,
        /// and from TheChipWritesRules3AndTheDefaultStaysOn0 by D5c.
        /// </summary>
        [Test]
        public void TheChipWritesRules4AndTheDefaultStaysOn0()
        {
            Assert.That(DialogueRules.Offered, Is.EqualTo(4), "The chip writes rules 4 since D5c.");
            var chip = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8, CampaignConversations = true }, 7);
            ShippedRules.ApplyFresh(chip, new SeasonBuilder.Choice { HouseSize = 8, CampaignConversations = true });
            Assert.That(chip.dialogueRulesVersion, Is.EqualTo(4), "The chip's season is on rules 4 since D5c.");
            Assert.That(DialogueRules.Admits(chip.dialogueRulesVersion, chip.schemaVersion), Is.True, "A new season's schema admits it.");
            Assert.That(DialogueRules.On(chip), Is.True);
            var plain = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7);
            ShippedRules.ApplyFresh(plain);
            Assert.That(plain.dialogueRulesVersion, Is.Zero);
        }

        /// <summary>
        /// At schema 29 a season on rules 2 or 3 is refused. The rules' own validator says why, at any live schema: it reads
        /// the season's schema, never the build's. Whole-state validation refuses it in the words it always used, which are
        /// the frozen schema-29 reader's; that half reads the live schema, so it is asserted only while new seasons are
        /// written at 29. From the pooled bump the live validator refuses schema 29 itself, and that wording pin moves to the
        /// frozen reader's tests (DIALOGUE-PLAN §6 item 7), so this case stays green across the bump. Since D2's merge new
        /// seasons are written at 30, the whole-state half no longer runs, and
        /// FrozenEpisodeV29IntegrationTests.RulesTwoAtTwentyNineIsRefusedInTheFrozenReadersWordsBeforeAnyLift holds those words.
        /// </summary>
        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void AtSchema29ALaterVersionIsRefusedInTheFrozenReadersWords(int version)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3);
            ShippedRules.ApplyFresh(s);
            bool writtenAt29 = s.schemaVersion == 29;
            s.schemaVersion = DialogueRules.Rules2Schema - 1;
            s.dialogueRulesVersion = version;
            Assert.That(DialogueRules.On(s), Is.False);
            Assert.That(DialogueRules.TryValidate(s, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Dialogue rules version " + version + " needs schema 30 or later."), "Rules 3 is defined from 30 since D3, as rules 2 is.");
            // The plea's fallback is one helper for every version (owner, 2026-10-25: never lose the plea): below its schema
            // rules 2 is not on, so even in a Campaign it has no rules-2 fallback to give and no open talk to point to.
            var campaign = s.Clone(); campaign.phase = EpisodePhase.Campaign;
            string voter = campaign.contestants.First(c => !c.isPlayer).id;
            Assert.That(DialogueRules.PleaWithoutConversation(campaign, voter, out string reason), Is.False);
            Assert.That(reason, Is.Null);
            Assert.That(DialogueRules.PleaInOpenConversation(campaign, voter), Is.False);
            if (!writtenAt29) return;
            Assert.That(EpisodeValidation.TryValidate(s, out error), Is.False);
            Assert.That(error, Is.EqualTo("Dialogue rules are not enabled in this build."));
        }

        [Test]
        public void EachVersionKeepsItsOwnBounds()
        {
            Assert.That(DialogueRules.MaximumSteps, Is.EqualTo(3), "Rules 1's bound keeps its meaning.");
            Assert.That(DialogueRules.MaximumEpisodes, Is.EqualTo(100), "Every reader of the storyline ceiling keeps it.");
            // Rules 3 (D3), rules 4 (D4) and rules 5 (D5b-1, repinned there) keep rules 2's bounds; 6 is undefined.
            Assert.That(new[] { 0, 1, 2, 3, 4, 5, 6 }.Select(DialogueRules.StepsFor), Is.EqualTo(new[] { 0, 3, 5, 5, 5, 5, 0 }));
            Assert.That(new[] { 0, 1, 2, 3, 4, 5, 6 }.Select(DialogueRules.ConversationBudget), Is.EqualTo(new[] { 0, 100, 32, 32, 32, 32, 0 }));
            Assert.That(new[] { 0, 1, 2, 3, 4, 5, 6 }.Select(DialogueRules.EvidenceRows), Is.EqualTo(new[] { 0, 1, 3, 3, 3, 3, 0 }));
            Assert.That(DialogueRules.Rules2StorylineCeiling, Is.EqualTo(92), "Never past the story pool's own stop (§5.5).");
        }

        [Test]
        public void TheActiveConversationReservesItsOwnVersionsPath()
        {
            var witness = DialoguePreviewWitness.Find().Conversation;
            var active = DialogueRules.Active(witness);
            Assert.That(active, Is.Not.Null);
            int used = witness.houseEvents.Count(e => e.cycleId == active.id);
            Assert.That(DialogueRules.ReservedEvents(witness), Is.EqualTo(3 - used), "Rules 1: three, less those used.");
            witness.dialogueRulesVersion = 2;
            Assert.That(DialogueRules.ReservedEvents(witness), Is.EqualTo(5 - used), "Rules 2: five, less those used.");
        }

        /// <summary>
        /// The tick an entry spends is keyed by how it was paid (the fix ROLE-MOMENTS-PLAN §6.1 asks of the rules' tick key):
        /// an action entry owns its window tick, a grant entry none, so the two never collide in one window. A grant funds
        /// nothing below rules 4, which D4 defines; no conversation is paid by a grant until D5a writes one (the store refuses every
        /// grant row until then).
        /// </summary>
        [Test]
        public void TheEntryTickIsKeyedByHowTheEntryWasPaid()
        {
            var action = new DialogueEntryReceiptState { kind = DialogueRules.ActionEntry, windowActionBefore = 0, windowActionAfter = 1 };
            var grant = new DialogueEntryReceiptState { kind = DialogueRules.GrantEntry, grantId = "dialogue-grant-7", windowActionBefore = 1, windowActionAfter = 1 };
            Assert.That(DialogueRules.EntryTick(action, 3, Windows.AfterVeto), Is.EqualTo("3:" + Windows.AfterVeto + ":1"));
            Assert.That(DialogueRules.EntryTick(grant, 3, Windows.AfterVeto), Is.Null, "A grant spends no window tick.");
            Assert.That(DialogueRules.EntryGrant(grant), Is.EqualTo("grant:dialogue-grant-7"));
            Assert.That(DialogueRules.EntryGrant(action), Is.Null);
            Assert.That(DialogueRules.EntryTick(null, 3, Windows.AfterVeto), Is.Null);
            // Repinned at D5b-1: rules 5 (the HoH grant) funds by action or grant as rules 4 does; 6 is undefined.
            for (int version = -1; version <= 6; version++)
            {
                Assert.That(DialogueRules.AcceptsFunding(version, DialogueRules.ActionEntry), Is.EqualTo(version >= 1 && version <= 5));
                Assert.That(DialogueRules.AcceptsFunding(version, DialogueRules.GrantEntry), Is.EqualTo(version == 4 || version == 5), "Rules 4 is the first that may fund by grant.");
                Assert.That(DialogueRules.AcceptsFunding(version, "wallet"), Is.False);
            }
            Assert.That(DialogueRules.GrantFundingFrom, Is.EqualTo(4));
        }

        [Test]
        public void TheContentRegistryIsClosedAndEveryReadIsFresh()
        {
            string word = DialogueCampaignWord.TemplateId;
            var first = DialogueRules.ContentFor(2, word, 1);
            var second = DialogueRules.ContentFor(2, word, 1);
            Assert.That(first, Is.Not.Null);
            Assert.That(ReferenceEquals(first, second), Is.False, "A fresh definition on every read, never shared authority.");
            Assert.That(first.beats.Select(b => b.id), Is.EqualTo(second.beats.Select(b => b.id)));
            Assert.That(DialogueRules.ContentFor(2, word, 2), Is.Null, "An unknown content version.");
            Assert.That(DialogueRules.ContentFor(2, DialogueCampaignV1.TemplateId, 1), Is.Null, "Rules 2 never plays rules 1's pairs.");
            Assert.That(DialogueRules.ContentFor(2, DialogueCampaignV2.TemplateId, 2), Is.Null);
            Assert.That(DialogueRules.ContentFor(1, word, 1), Is.Null, "Rules 1 never plays rules 2's tree.");
            Assert.That(DialogueRules.ContentFor(1, DialogueCampaignV1.TemplateId, 1), Is.Not.Null);
            Assert.That(DialogueRules.ContentFor(1, DialogueCampaignV2.TemplateId, 2), Is.Not.Null);
            Assert.That(DialogueRules.ContentFor(3, word, 1), Is.Null, "Rules 3 plays its own trees, never rules 2's (D3).");
            Assert.That(DialogueRules.ContentFor(DialogueRules.Version + 1, word, 1), Is.Null, "An undefined rules version has no content.");
            Assert.That(DialogueRules.ContentFor(0, word, 1), Is.Null);
            Assert.That(DialogueRules.EntryTemplateFor(2), Is.EqualTo(word));
            Assert.That(DialogueRules.EntryTemplateFor(1), Is.EqualTo(DialogueCampaignV1.TemplateId));
            Assert.That(DialogueRules.EntryTemplateFor(0), Is.Null);
            Assert.That(ReferenceEquals(StoryCatalog.Find(word), StoryCatalog.Find(word)), Is.False);
            Assert.That(StoryCatalog.All.Any(t => DialogueRules.IsDialogueTemplate(t.id)), Is.False, "Never an ambient-pool arc.");
            Assert.That(new[] { DialogueCampaignV1.TemplateId, DialogueCampaignV2.TemplateId, word }.All(DialogueRules.IsDialogueTemplate), Is.True);
        }

        /// <summary>
        /// The ROLE doc's shape (:90) and the plan's bounds (§2): at most four options a node, every node's exit the neutral
        /// "Leave it there", at most three consequential choices on any path, and every path within rules 2's five steps.
        /// Each reply node is the one its answer and the facts held draw; every reply except the exit has its odds.
        /// </summary>
        [Test]
        public void TheVoteWordTreeKeepsTheRoleDocsShape()
        {
            var tree = StoryCatalog.Find(DialogueCampaignWord.TemplateId);
            Assert.That(tree.beats.Select(b => b.id).Distinct().Count(), Is.EqualTo(tree.beats.Length));
            Assert.That(tree.beats.Length, Is.EqualTo(1 + 14 + 3), "The open, fourteen replies and three finishes.");
            foreach (var beat in tree.beats)
            {
                Assert.That(beat.options.Length, Is.InRange(2, 4), beat.id);
                Assert.That(beat.options.Select(o => o.id).Distinct().Count(), Is.EqualTo(beat.options.Length), beat.id);
                Assert.That(beat.lapse, Is.EqualTo(DialogueCampaignWord.Leave));
                Assert.That(beat.options.Last().id, Is.EqualTo(DialogueCampaignWord.Leave), beat.id + ": the exit, last, on every node.");
                Assert.That(beat.options.Last().next, Is.EqualTo("end:neutral"));
                Assert.That(beat.closes, Is.EqualTo(StoryAnchors.EvictionEve));
                Assert.That(beat.surface, Is.EqualTo(StorySurfaces.Conversation));
                foreach (var option in beat.options)
                {
                    Assert.That(option.next == DialogueCampaignWord.ByAnswer || option.next == DialogueCampaignWord.ByReaction
                        || option.next.StartsWith("end:", StringComparison.Ordinal), Is.True, beat.id + "/" + option.id);
                    Assert.That(option.costsAction || option.conduct || option.pick != null || option.check >= 0, Is.False,
                        "A reply is free and draws on its own coin, never a check.");
                    Assert.That(HouseEventRisk.IsKnown(option.risk), Is.True);
                    if (option.risk != HouseEventRisk.Low) Assert.That(option.description, Does.Contain("It can backfire."));
                }
            }
            var open = tree.Beat(DialogueCampaignWord.Open);
            Assert.That(open.options.Select(o => o.id), Is.EqualTo(new[] { DialogueCampaignWord.OpenCase, DialogueCampaignWord.Leave }));
            Assert.That(open.options[0].next, Is.EqualTo(DialogueCampaignWord.ByAnswer));
            foreach (string answer in DialogueCampaignWord.Answers)
                foreach (bool rescue in new[] { false, true })
                    foreach (bool lie in new[] { false, true })
                    {
                        string id = DialogueCampaignWord.ReplyBeat(answer, rescue, lie);
                        var reply = tree.Beat(id);
                        Assert.That(reply, Is.Not.Null, id);
                        Assert.That(DialogueCampaignWord.AnswerOf(id), Is.EqualTo(answer));
                        var ids = reply.options.Select(o => o.id).ToArray();
                        Assert.That(ids.Contains(DialogueCampaignWord.CiteWord), Is.EqualTo(answer == DialogueCampaignWord.Word));
                        Assert.That(ids.Contains(DialogueCampaignWord.CiteRescue), Is.EqualTo(rescue && answer != DialogueCampaignWord.Word));
                        Assert.That(ids.Contains(DialogueCampaignWord.CiteLie), Is.EqualTo(lie));
                        Assert.That(ids.Contains(DialogueCampaignWord.Press), Is.EqualTo(!lie));
                        Assert.That(ids.Contains(DialogueCampaignWord.AskConcern), Is.EqualTo(answer == DialogueCampaignWord.Concern));
                        foreach (var option in reply.options.Take(reply.options.Length - 1))
                        {
                            Assert.That(option.next, Is.EqualTo(DialogueCampaignWord.ByReaction));
                            Assert.That(DialogueCampaignWord.TryOdds(option.id, out double warm, out double sour), Is.True, option.id);
                            Assert.That(warm, Is.GreaterThan(0)); Assert.That(sour, Is.GreaterThan(0)); Assert.That(warm + sour, Is.LessThanOrEqualTo(1));
                        }
                    }
            foreach (string reaction in DialogueCampaignWord.Reactions)
            {
                var finish = tree.Beat(DialogueCampaignWord.FinishBeat(reaction));
                Assert.That(finish.options.Select(o => o.id), Is.EqualTo(new[] { DialogueCampaignWord.Plea, DialogueCampaignWord.KeepOpen, DialogueCampaignWord.Leave }));
                Assert.That(finish.options[0].next, Is.EqualTo("end:answered"));
            }
            Assert.That(DialogueCampaignWord.Reactions.Select(DialogueCampaignWord.Delta), Is.EqualTo(new[] { 3, 0, -3 }));
            foreach (string exit in new[] { DialogueCampaignWord.Leave, DialogueCampaignWord.Plea, DialogueCampaignWord.KeepOpen, DialogueCampaignWord.OpenCase })
                Assert.That(DialogueCampaignWord.TryOdds(exit, out _, out _), Is.False, exit + " draws nothing.");
            // Every path the tree can take, walked: the open's case leads to any reply its answer and the facts held can draw,
            // a reply to any finish its reaction can draw, and an "end:" closes the path. A consequential choice is one that
            // leads anywhere but a neutral ending: the paid entry, a reply that draws a reaction, the plea.
            int steps = DialogueRules.StepsFor(DialogueRules.Rules2), longest = 0, mostChoices = 0, paths = 0;
            var reached = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<string> Targets(string next) =>
                next == DialogueCampaignWord.ByAnswer ? DialogueCampaignWord.ReplyBeats()
                : next == DialogueCampaignWord.ByReaction ? DialogueCampaignWord.Reactions.Select(DialogueCampaignWord.FinishBeat)
                : new[] { next };
            void Walk(string id, int depth, int choices)
            {
                var beat = tree.Beat(id);
                Assert.That(beat, Is.Not.Null, "Every edge leads to a node of the tree: " + id + ".");
                Assert.That(depth, Is.LessThanOrEqualTo(steps), "Every path within rules 2's five steps, so none loops: " + id + ".");
                reached.Add(id);
                foreach (var option in beat.options)
                {
                    int made = choices + (option.next == "end:neutral" ? 0 : 1);
                    Assert.That(made, Is.LessThanOrEqualTo(3), "At most three consequential choices on a path: " + id + "/" + option.id + ".");
                    if (option.next.StartsWith("end:", StringComparison.Ordinal))
                    {
                        paths++; longest = Math.Max(longest, depth); mostChoices = Math.Max(mostChoices, made);
                        continue;
                    }
                    foreach (string target in Targets(option.next)) Walk(target, depth + 1, made);
                }
            }
            Walk(DialogueCampaignWord.Open, 1, 0);
            Assert.That(reached, Is.EquivalentTo(tree.beats.Select(b => b.id)), "Every node is on some path.");
            TestContext.Out.WriteLine("Vote-word tree: " + paths + " paths, the longest " + longest + " nodes, at most "
                + mostChoices + " consequential choices.");
            Assert.That(longest, Is.EqualTo(3), "Content version 1: the open, a reply, a finish.");
            Assert.That(mostChoices, Is.EqualTo(3), "The entry, the reply and the plea.");
        }

        /// <summary>
        /// Captions are a contract (hud-button-caption-is-a-contract): every new answer has a caption of its own, used by no
        /// other story, house event or conversation; only the four shared acts keep rules 1's words.
        /// </summary>
        [Test]
        public void EveryNewAnswerHasACaptionOfItsOwn()
        {
            var tree = StoryCatalog.Find(DialogueCampaignWord.TemplateId);
            var shared = new[] { DialogueCampaignV1.OpenLabel, DialogueCampaignV1.LeaveLabel, DialogueCampaignV1.PleaLabel, DialogueCampaignV1.KeepLabel };
            var labels = tree.beats.SelectMany(b => b.options).Select(o => o.label).Distinct().ToArray();
            var fresh = labels.Except(shared).ToArray();
            Assert.That(fresh, Is.EquivalentTo(new[] { DialogueCampaignWord.CiteWordLabel, DialogueCampaignWord.CiteRescueLabel,
                DialogueCampaignWord.CiteLieLabel, DialogueCampaignWord.AppealLabel, DialogueCampaignWord.AskConcernLabel, DialogueCampaignWord.PressLabel }));
            var elsewhere = new HashSet<string>(StoryCatalog.All.SelectMany(t => t.beats).SelectMany(b => b.options).Select(o => o.label), StringComparer.Ordinal);
            foreach (var template in new[] { StoryCatalog.Find(DialogueCampaignV1.TemplateId), StoryCatalog.Find(DialogueCampaignV2.TemplateId) })
                foreach (var option in template.beats.SelectMany(b => b.options)) elsewhere.Add(option.label);
            foreach (string label in fresh) Assert.That(elsewhere.Contains(label), Is.False, label + " is already another control's caption.");
        }
    }
}
