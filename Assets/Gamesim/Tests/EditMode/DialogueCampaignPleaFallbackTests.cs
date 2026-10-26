using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Owner, 2026-10-25: never lose the plea (DIALOGUE-PLAN §5.5). In a season on dialogue rules 1 the desperate plea for
    /// the player's own vote opens the campaign conversation when it can; while it cannot - a house moment waiting on an
    /// answer, another conversation open, the occasion used - the press is the one-shot plea a season without dialogue
    /// makes, by the same Lobby command, and draws exactly what that season draws. Rules 1 used to lock the plea instead
    /// (the panel's Emotional button, locked with CanStart's reason as its tag), in about one nominated Campaign week in
    /// two (P1(a)); these tests pin the fallback that replaced the lock.
    ///
    /// <para>The witness is a pair of seasons from one seed, walked in step by the plain public walk: the shipped season
    /// enrolled on rules 1 by the literal, as every rules-1 test enrols it (DIALOGUE-PLAN §2; the chip's season is that
    /// start, field for field, ShippedRulesTests), and the same shipped season left on rules 0, the chip off. Past the
    /// enrolment nothing is assigned by hand: no nominee, counter, roll, house moment or store row.</para>
    /// </summary>
    public sealed class DialogueCampaignPleaFallbackTests
    {
        private const string HouseMoment = "Answer the existing house moment first.";

        private sealed class Witness
        {
            internal uint Seed;
            internal EpisodeState On, Off;
            /// <summary>Two voters whose conversation waits on the open house moment, and who could each hear a plea.</summary>
            internal string Voter, Other;
        }

        private static Witness found;

        private sealed class SavedFields : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type type) =>
                type.GetFields(BindingFlags.Public | BindingFlags.Instance).Cast<MemberInfo>().ToList();
        }

        private static readonly IContractResolver SavedFieldContracts = new SavedFields();

        /// <summary>The saved DTO surface, the dialogue rules version aside: the one field the chip writes.</summary>
        private static string Json(EpisodeState s)
        {
            var copy = s.Clone();
            copy.dialogueRulesVersion = 0;
            return JsonConvert.SerializeObject(copy, new JsonSerializerSettings { ContractResolver = SavedFieldContracts });
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value, new JsonSerializerSettings { ContractResolver = SavedFieldContracts });

        private static EpisodeState Start(uint seed, bool dialogue)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero);
            if (dialogue) fresh.dialogueRulesVersion = 1;
            return fresh;
        }

        private static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);

        /// <summary>Voters the player could plead with now, as the panel offers them: StrategyRules.CanLobby for the player's own vote.</summary>
        private static List<string> Pleadable(EpisodeState s) => s.Active
            .Where(c => !c.isPlayer && StrategyRules.CanLobby(s, c.id, LobbyAsk.Vote, s.playerId, out _)).Select(c => c.id).ToList();

        private static string Refusal(EpisodeState s, string npc) => DialogueRules.CanStart(s, npc, out string why) ? null : why;

        /// <summary>
        /// The first nominated Campaign of seeds 1 to 24 (houses of eight, 400 commands each) with a house moment open, two
        /// actions left and two voters whose conversation waits on it; both seasons checked equal at every step on the way.
        /// </summary>
        private static Witness Find()
        {
            if (found == null) found = Search();
            return new Witness { Seed = found.Seed, On = found.On.Clone(), Off = found.Off.Clone(), Voter = found.Voter, Other = found.Other };
        }

        private static Witness Search()
        {
            for (uint seed = 1; seed <= 24; seed++)
            {
                var on = new EpisodeEngine(Start(seed, true));
                var off = new EpisodeEngine(Start(seed, false));
                for (int step = 0; step < 400; step++)
                {
                    var s = on.Snapshot;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId)?.status != ContestantStatus.Active) break;
                    Assert.That((off.Snapshot.revision, off.Snapshot.randomState), Is.EqualTo((s.revision, s.randomState)),
                        "seed " + seed + " step " + step + ": the two seasons walk in step.");
                    if (s.phase == EpisodePhase.Campaign && !s.evictionResolved && s.nominees.Contains(s.playerId) && Remaining(s) >= 2
                        && s.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)))
                    {
                        var waiting = Pleadable(s).Where(id => Refusal(s, id) == HouseMoment).ToList();
                        if (waiting.Count >= 2)
                        {
                            Assert.That(Json(off.Snapshot), Is.EqualTo(Json(s)), "seed " + seed + ": the chip changes nothing before the plea.");
                            TestContext.Out.WriteLine("Plea fallback witness: seed " + seed + ", week " + s.week + " campaign, revision " + s.revision
                                + ", " + waiting.Count + " voters waiting on the house moment.");
                            return new Witness { Seed = seed, On = s.Clone(), Off = off.Snapshot.Clone(), Voter = waiting[0], Other = waiting[1] };
                        }
                    }
                    var next = EpisodeEngineTests.NextCommand(s);
                    var a = on.Apply(next);
                    if (!a.accepted) break;
                    var b = off.Apply(next);
                    Assert.That(b.accepted, Is.True, "seed " + seed + ": the chip-off season refused what the chip-on one took: " + next.kind + ": " + b.reason);
                }
            }
            Assert.Fail("No nominated Campaign of 24 seeds met an open house moment with two actions left and two voters waiting on it.");
            return null;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string tag) => new EpisodeCommand
        {
            id = "plea-fallback-" + tag + "-" + s.revision, actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        /// <summary>The panel's Emotional press for the player's own vote, as it commits it when there is no conversation: the very command a season without dialogue commits.</summary>
        private static EpisodeCommand Plea(EpisodeState s, string voter)
        {
            var c = Command(s, EpisodeCommandKind.Lobby, "plea");
            c.targetId = voter; c.secondTargetId = s.playerId; c.text = LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional);
            return c;
        }

        private static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var before = engine.Snapshot;
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, "week " + before.week + " " + before.phase + " " + command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            return result.state;
        }

        /// <summary>Every house moment open now, answered with its own neutral exit; the same commands in both seasons, which stay equal.</summary>
        private static void AnswerTheHouse(EpisodeEngine on, EpisodeEngine off)
        {
            for (int answer = 0; answer < 24; answer++)
            {
                var s = on.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent, "answer");
                c.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(o => o.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False, "The neutral exit costs nothing.");
                    c.secondTargetId = lapse.optionId;
                }
                else c.text = item.choices.First().label;
                Accepted(on, c);
                if (off != null)
                {
                    Accepted(off, c);
                    Assert.That(Json(off.Snapshot), Is.EqualTo(Json(on.Snapshot)), "Answering the house draws the same in both seasons.");
                }
            }
            Assert.Fail("The house's moments did not close within 24 neutral answers.");
        }

        [Test]
        public void WhileAHouseMomentIsOpenTheDesperatePleaIsTheChipOffSeasonsPlea()
        {
            var w = Find();
            var on = new EpisodeEngine(w.On);
            var off = new EpisodeEngine(w.Off);
            var s = on.Snapshot;
            Assert.That(DialogueRules.On(s), Is.True, "The chip's season plays dialogue rules 1.");
            Assert.That(DialogueRules.On(off.Snapshot), Is.False, "and its twin plays none.");

            // The conversation cannot start, and the plea is not taken away: the press is the one-shot plea, saying why.
            Assert.That(DialogueRules.TryBuildEntryCommand(s, w.Voter, out _), Is.False, "The house moment holds the conversation.");
            Assert.That(DialogueRules.PleaWithoutConversation(s, w.Voter, out string why), Is.True,
                "owner, 2026-10-25: never lose the plea. A refused conversation leaves the one-shot plea.");
            Assert.That(why, Is.EqualTo(HouseMoment), "in CanStart's own words.");
            Assert.That(DialogueRules.PleaWithoutConversation(off.Snapshot, w.Voter, out why), Is.False, "Without dialogue the plea is the plea, as ever.");
            Assert.That(why, Is.Null);
            Assert.That(Pleadable(off.Snapshot), Does.Contain(w.Voter), "The chip-off season offers this plea,");
            Assert.That(Remaining(off.Snapshot), Is.EqualTo(Remaining(s)), "for the same action.");

            var press = Plea(s, w.Voter);
            var withChip = Accepted(on, press);
            var withoutChip = Accepted(off, press);
            Assert.That(Json(withChip), Is.EqualTo(Json(withoutChip)), "The press draws exactly what the chip-off season's draws: the whole saved state.");
            Assert.That(withChip.randomState, Is.EqualTo(withoutChip.randomState), "the same main-stream roll,");
            var lobby = withChip.lobbies.Last();
            Assert.That((lobby.week, lobby.phase, lobby.deciderId, lobby.ask, lobby.subjectId, lobby.approach),
                Is.EqualTo((s.week, EpisodePhase.Campaign, w.Voter, LobbyAsk.Vote, s.playerId, LobbyApproach.Emotional)), "the desperate plea itself,");
            Assert.That(Json(lobby), Is.EqualTo(Json(withoutChip.lobbies.Last())), "with the same answer and influence,");
            Assert.That(Json(withChip.relationships), Is.EqualTo(Json(withoutChip.relationships)), "the same relationships,");
            Assert.That(Json(withChip.storylines), Is.EqualTo(Json(withoutChip.storylines)), "the same storylines,");
            Assert.That(Json(withChip.houseEvents), Is.EqualTo(Json(withoutChip.houseEvents)), "the same house events,");
            Assert.That(Json(withChip.unifiedCommitments), Is.EqualTo(Json(withoutChip.unifiedCommitments)), "and the same commitments.");
            Assert.That(Remaining(withChip), Is.EqualTo(Remaining(s) - 1), "It cost its one action.");
            Assert.That(withChip.dialogue.episodes, Is.Empty, "No conversation that time.");
            Assert.That(StrategyRules.AlreadyAsked(withChip, w.Voter), Is.True, "and the voter has heard the plea.");

            // The conversation is not spent: once the moment is answered it is offered again, to a voter not yet pleaded with.
            AnswerTheHouse(on, off);
            var answered = on.Snapshot;
            Assert.That(Remaining(answered), Is.GreaterThanOrEqualTo(1));
            Assert.That(DialogueRules.CanStart(answered, w.Other, out why), Is.True, why);
            Assert.That(DialogueRules.PleaWithoutConversation(answered, w.Other, out _), Is.False, "With the house free the press is the conversation again.");
            Assert.That(DialogueRules.TryBuildEntryCommand(answered, w.Other, out var entry), Is.True);
            entry.id = "plea-fallback-entry";
            var talking = Accepted(on, entry);
            Assert.That(DialogueRules.Active(talking)?.npcId, Is.EqualTo(w.Other), "The conversation opens.");
            Assert.That(Remaining(talking), Is.EqualTo(Remaining(answered) - 1), "for the plea's one action.");
            Assert.That(Pleadable(off.Snapshot), Does.Contain(w.Other), "where the chip-off season offers the plain plea.");
        }

        [Test]
        public void WhenTheConversationCanStartTheDesperatePleaStartsIt()
        {
            var w = Find();
            var on = new EpisodeEngine(w.On);
            AnswerTheHouse(on, new EpisodeEngine(w.Off));
            var s = on.Snapshot;
            Assert.That(s.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)), Is.False, "The house is free.");
            Assert.That(DialogueRules.CanStart(s, w.Voter, out string why), Is.True, why);
            Assert.That(DialogueRules.PleaWithoutConversation(s, w.Voter, out why), Is.False, "The press is the conversation's entry,");
            Assert.That(why, Is.Null);
            Assert.That(DialogueRules.TryBuildEntryCommand(s, w.Voter, out var entry), Is.True, "and the panel can build it.");
            entry.id = "plea-fallback-entry";
            var talking = Accepted(on, entry);
            var episode = talking.dialogue.episodes.Single();
            Assert.That(episode.npcId, Is.EqualTo(w.Voter));
            Assert.That(DialogueRules.Cycle(talking, episode).beatId, Is.EqualTo(DialogueCampaignV1.Case), "open at its case.");
            Assert.That(Remaining(talking), Is.EqualTo(Remaining(s) - 1), "It cost the plea's one action.");
            Assert.That(StrategyRules.AlreadyAsked(talking, w.Voter), Is.False, "and is no plea yet.");
        }

        /// <summary>The house answered and the first voter's conversation open, paid with one of the witness's two actions.</summary>
        private static EpisodeState Talking(EpisodeEngine on, Witness w)
        {
            AnswerTheHouse(on, null);
            Assert.That(DialogueRules.TryBuildEntryCommand(on.Snapshot, w.Voter, out var entry), Is.True);
            entry.id = "plea-fallback-entry";
            var talking = Accepted(on, entry);
            Assert.That(DialogueRules.Active(talking)?.npcId, Is.EqualTo(w.Voter));
            Assert.That(Remaining(talking), Is.GreaterThanOrEqualTo(1));
            return talking;
        }

        /// <summary>
        /// Every refusal leaves the plea, not only the house moment's. With another conversation still open the other voter's
        /// desperate plea is the plain Lobby command, accepted for one action, and the open conversation stays where it was.
        /// </summary>
        [Test]
        public void AnotherOpenConversationLeavesTheOneShotPlea()
        {
            var w = Find();
            var on = new EpisodeEngine(w.On);
            var talking = Talking(on, w);
            var episode = DialogueRules.Active(talking);
            string node = DialogueRules.Cycle(talking, episode).beatId;
            Assert.That(DialogueRules.PleaWithoutConversation(talking, w.Other, out string why), Is.True);
            Assert.That(why, Is.EqualTo("Finish the active dialogue first."));
            var pleaded = Accepted(on, Plea(talking, w.Other));
            var lobby = pleaded.lobbies.Last();
            Assert.That((lobby.deciderId, lobby.ask, lobby.subjectId, lobby.approach), Is.EqualTo((w.Other, LobbyAsk.Vote, talking.playerId, LobbyApproach.Emotional)));
            Assert.That(Remaining(pleaded), Is.EqualTo(Remaining(talking) - 1), "for its one action.");
            Assert.That(DialogueRules.Active(pleaded)?.id, Is.EqualTo(episode.id), "The open conversation is untouched,");
            Assert.That(DialogueRules.Cycle(pleaded, DialogueRules.Active(pleaded)).beatId, Is.EqualTo(node), "at its node,");
            Assert.That(pleaded.dialogue.episodes, Has.Count.EqualTo(1), "and no second one began.");
        }

        /// <summary>A conversation that ended without a plea used its occasion: the voter's desperate plea is then the plain plea.</summary>
        [Test]
        public void AUsedOccasionLeavesTheOneShotPlea()
        {
            var w = Find();
            var on = new EpisodeEngine(w.On);
            var talking = Talking(on, w);
            var cycle = DialogueRules.Cycle(talking, DialogueRules.Active(talking));
            Assert.That(DialogueRules.TryBuildChoiceCommand(talking, cycle.eventId, DialogueCampaignV1.Leave, out var leave), Is.True);
            leave.id = "plea-fallback-leave";
            var left = Accepted(on, leave);
            Assert.That(DialogueRules.Active(left), Is.Null, "Left there,");
            Assert.That(StrategyRules.AlreadyAsked(left, w.Voter), Is.False, "with no plea made.");
            Assert.That(DialogueRules.PleaWithoutConversation(left, w.Voter, out string why), Is.True);
            Assert.That(why, Is.EqualTo("This campaign occasion has already been used."));
            Assert.That(Pleadable(left), Does.Contain(w.Voter), "The plea is still the player's to make,");
            var again = Accepted(on, Plea(left, w.Voter));
            var lobby = again.lobbies.Last();
            Assert.That((lobby.deciderId, lobby.ask, lobby.subjectId, lobby.approach), Is.EqualTo((w.Voter, LobbyAsk.Vote, left.playerId, LobbyApproach.Emotional)));
            Assert.That(Remaining(again), Is.EqualTo(Remaining(left) - 1), "for its one action,");
            Assert.That(again.dialogue.episodes, Has.Count.EqualTo(1), "and no conversation that time.");
        }

        [Test]
        public void OutsideADialogueSeasonOrTheCampaignThereIsNoFallbackToSpeakOf()
        {
            var w = Find();
            Assert.That(DialogueRules.PleaWithoutConversation(w.Off, w.Voter, out string why), Is.False, "Rules 0: the plea is the plea.");
            Assert.That(why, Is.Null);
            var nomination = Start(w.Seed, true);
            Assert.That(nomination.phase, Is.Not.EqualTo(EpisodePhase.Campaign));
            Assert.That(DialogueRules.PleaWithoutConversation(nomination, nomination.Active.First(c => !c.isPlayer).id, out why), Is.False,
                "Before the Campaign there is no conversation to fall back from.");
            Assert.That(DialogueRules.PleaWithoutConversation(null, w.Voter, out why), Is.False);
        }
    }
}
