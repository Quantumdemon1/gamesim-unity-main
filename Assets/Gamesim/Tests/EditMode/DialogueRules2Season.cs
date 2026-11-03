using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Genuine seasons on dialogue rules 2 (DIALOGUE-PLAN D2) for the tests that play one: the shipped season enrolled by
    /// the literal 2, walked by the plain public walk to a campaign where the player is nominated, the house's open asks
    /// answered with their own neutral exits. Past the enrolment nothing is assigned by hand.
    ///
    /// <para><b>Rules 2 exists only from schema 30</b> (decision 7): a rules-2 season at schema 29 is refused, and while
    /// new seasons were written at 29 no test could play one. So every test that plays one starts with
    /// <see cref="HeldBelowItsSchema"/>: below schema 30 it asserts that refusal everywhere a season could meet it
    /// (validation, the engine, the rules' own gate) and the test ends there; from the pooled bump on it returns false and
    /// the test plays the conversation, as it does at the live 30 since D2's merge. The conversation arm was first proven
    /// when D2 was built, ahead of the bump, on a scratch copy of this tree with the live schema moved to 30, exactly as the
    /// bump moves it (the D2 report records the run); D2 lands after the bump (DIALOGUE-PLAN §6 item 5). Nothing here reads <see cref="DialogueRules.Offered"/>: the chip stays on
    /// rules 1 until rules 2 freezes.</para>
    /// </summary>
    internal static class DialogueRules2Season
    {
        internal const string Template = DialogueCampaignWord.TemplateId;

        private sealed class SavedFields : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type type) =>
                type.GetFields(BindingFlags.Public | BindingFlags.Instance).Cast<MemberInfo>().ToList();
        }
        private static readonly JsonSerializerSettings Fields = new JsonSerializerSettings
        {
            ContractResolver = new SavedFields(), TypeNameHandling = TypeNameHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace, FloatParseHandling = FloatParseHandling.Double
        };

        internal static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None, Fields);
        internal static JObject Tree(EpisodeState s) => JObject.Parse(Json(s));
        internal static EpisodeState RoundTrip(EpisodeState s) => JsonConvert.DeserializeObject<EpisodeState>(Json(s), Fields);

        /// <summary>The shipped house of a seed, before its first command, enrolled on dialogue rules 2 by its literal.</summary>
        internal static EpisodeState Start(uint seed, int houseSize = 8)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = houseSize }, seed);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero, "A shipped season starts on dialogue rules 0.");
            fresh.dialogueRulesVersion = DialogueRules.Rules2;
            return fresh;
        }

        /// <summary>Whether a new season is written at a schema that defines rules 2.</summary>
        internal static bool AtItsSchema => Start(1).schemaVersion >= DialogueRules.Rules2Schema;

        /// <summary>
        /// Below schema 30, asserts that a rules-2 season is refused everywhere a season meets it, and returns true: the
        /// calling test ends there. At 30 or later it returns false and the test plays its conversation.
        /// </summary>
        internal static bool HeldBelowItsSchema()
        {
            var fresh = Start(1);
            if (fresh.schemaVersion >= DialogueRules.Rules2Schema) return false;
            Assert.That(DialogueRules.Admits(DialogueRules.Rules2, fresh.schemaVersion), Is.False);
            Assert.That(DialogueRules.On(fresh), Is.False, "Below schema 30 rules 2 is never on.");
            Assert.That(EpisodeValidation.TryValidate(fresh, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Dialogue rules are not enabled in this build."),
                "Refused in the words the frozen schema-29 reader uses, so the two agree.");
            Assert.That(DialogueRules.TryValidate(fresh, out error), Is.False);
            Assert.That(error, Is.EqualTo("Dialogue rules version 2 needs schema 30 or later."));
            Assert.That(DialogueRules.CanStart(fresh, fresh.contestants.First(c => !c.isPlayer).id, out error), Is.False);
            var refused = Assert.Throws<ArgumentException>(() => new EpisodeEngine(fresh));
            Assert.That(refused.Message, Does.Contain("Dialogue rules are not enabled in this build."));
            return true;
        }

        internal sealed class Campaign
        {
            internal uint Seed;
            internal EpisodeState State;
            internal string[] Voters;
        }

        /// <summary>
        /// The first campaign in seeds 1 to 24 (first weeks, 96 commands each) where the player is nominated, the asks are
        /// answered, and at least two voters can be talked to with two actions left. Returned as a detached copy.
        /// </summary>
        internal static Campaign FindCampaign()
        {
            for (uint seed = 1; seed <= 24; seed++)
            {
                var engine = new EpisodeEngine(Start(seed));
                for (int step = 0; step < 96; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase == EpisodePhase.Campaign)
                    {
                        if (!s.nominees.Contains(s.playerId)) break;
                        CloseAsks(engine);
                        s = engine.Snapshot;
                        var voters = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .Where(id => DialogueRules.CanStart(s, id, out _)).ToArray();
                        if (voters.Length < 2 || Remaining(s) < 2) break;
                        TestContext.Out.WriteLine("Rules-2 campaign: seed " + seed + ", week 1, revision " + s.revision + ".");
                        return new Campaign { Seed = seed, State = s.Clone(), Voters = voters };
                    }
                    Accepted(engine, EpisodeEngineTests.NextCommand(s));
                }
            }
            Assert.Fail("No rules-2 season of 24 met a nominated first-week campaign with two voters to talk to.");
            return null;
        }

        /// <summary>The house's open asks, answered with their own neutral exits: a conversation waits for them.</summary>
        internal static void CloseAsks(EpisodeEngine engine)
        {
            for (int answer = 0; answer < 24; answer++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || !item.contentId.StartsWith(Template + ":", StringComparison.Ordinal), Is.True,
                    "Cleanup answers the house's asks, never the conversation under test.");
                var c = Command(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                c.targetId = item.id;
                if (item.IsStory) c.secondTargetId = item.lapseOptionId; else c.text = item.choices.First().label;
                Accepted(engine, c);
            }
            Assert.Fail("The house's asks did not close within 24 neutral answers.");
        }

        internal static int Remaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);

        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-r2-" + s.revision + "-" + kind,
            actorId = s.playerId, kind = kind, expectedRevision = s.revision, expectedPhase = s.phase,
        };

        /// <summary>The entry the HUD builds for a voter: the public plan, never a hand-made payload.</summary>
        internal static EpisodeCommand Entry(EpisodeState s, string voter)
        {
            Assert.That(DialogueRules.TryBuildEntryCommand(s, voter, out var plan), Is.True, "The HUD's own entry plan.");
            Assert.That(plan.text, Is.EqualTo(Template));
            plan.id = "dialogue-r2-entry-" + s.revision + "-" + voter;
            return plan;
        }

        /// <summary>The choice the HUD builds for an option of the open conversation's current node.</summary>
        internal static EpisodeCommand Choice(EpisodeState s, string option)
        {
            var active = DialogueRules.Active(s);
            Assert.That(active, Is.Not.Null, "A conversation is open.");
            var cycle = DialogueRules.Cycle(s, active);
            Assert.That(DialogueRules.TryBuildChoiceCommand(s, cycle.eventId, option, out var plan), Is.True,
                "The HUD's own plan for " + option + " at " + cycle.beatId + ".");
            plan.id = "dialogue-r2-choice-" + s.revision + "-" + option;
            return plan;
        }

        internal static EpisodeCommand AskVote(EpisodeState s, string voter)
        {
            var c = Command(s, EpisodeCommandKind.AskVote);
            c.targetId = voter;
            return c;
        }

        internal static EpisodeCommand Plea(EpisodeState s, string voter, string approach)
        {
            var c = Command(s, EpisodeCommandKind.Lobby);
            c.targetId = voter; c.secondTargetId = s.playerId; c.text = LobbyAsk.Encode(LobbyAsk.Vote, approach);
            return c;
        }

        internal static EpisodeState Accepted(EpisodeEngine engine, EpisodeCommand command)
        {
            var source = engine.Snapshot;
            string sourceBytes = Json(source), request = Json(command);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, "week " + source.week + " " + source.phase + " " + command.kind + ": " + result.reason);
            Assert.That(Json(source), Is.EqualTo(sourceBytes), "The detached source is not a candidate workspace.");
            Assert.That(Json(command), Is.EqualTo(request), "Admission does not rewrite the caller's payload.");
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            return result.state;
        }

        internal static string Refused(EpisodeEngine engine, EpisodeCommand command)
        {
            string before = Json(engine.Snapshot);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.False, command.kind + " was accepted.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "A refusal changes nothing: no roll, no action, no row.");
            return result.reason;
        }

        internal static DialogueEpisodeState Conversation(EpisodeState s, string voter) =>
            s.dialogue.episodes.Single(e => e.npcId == voter && e.week == s.week);

        internal static StorylineState Cycle(EpisodeState s, DialogueEpisodeState e) => s.storylines.Single(c => c.id == e.id);

        internal static string Suffix(DialogueResponseState response) => response.responseKey.Substring(Template.Length + 1);

        /// <summary>Every relationship row as "from>to" to its score, for exact before/after comparisons.</summary>
        internal static Dictionary<string, double> Scores(EpisodeState s) =>
            s.relationships.ToDictionary(r => r.fromId + ">" + r.toId, r => r.score, StringComparer.Ordinal);
    }
}
