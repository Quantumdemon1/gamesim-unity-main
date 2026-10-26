#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;
using R2 = Gamesim.Tests.EditMode.DialogueRules2Season;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Rules 2's appeal takes in V2's citation (DIALOGUE-PLAN §5.1): on the one retained genuine public-rescue source
    /// DialoguePublicRescueV2Tests reads (week 5, the voter used the veto on the player in week 2), a season enrolled on
    /// dialogue rules 2 offers "Remind them of the rescue" at the reply, and citing it keeps the public record as its
    /// power copy, then makes the final plea the Strategic plea's roll. The carrier is read through
    /// <see cref="Application.dataPath"/>, so this file runs in the editor and under the Mono harness, not the dotnet subset.
    ///
    /// <para>The only edits are the declared enrolment (rules 2) and, from the schema-30 bump, the schema field the bump's
    /// own upgrade writes (dialogue adds no field, DIALOGUE-PLAN §6). Below schema 30 the test holds rules 2's refusal.</para>
    /// </summary>
    public sealed class DialogueRules2PublicRescueTests
    {
        private const string OriginalSha = "D3E0C73F5A10BCD85ED37766AE462B6435E588DA08D7E2FFAADF7C70F5BD4D71";
        private static readonly JsonSerializerSettings Fields = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, DateParseHandling = DateParseHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64, ContractResolver = new PublicFieldsOnly()
        };
        private EpisodeState carried;
        private string voter;

        private sealed class PublicFieldsOnly : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) =>
                type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => base.CreateProperty(f, MemberSerialization.Fields)).ToList();
        }

        [OneTimeSetUp]
        public void LoadTheRetainedPublicRescueSource()
        {
            string path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/DialoguePublicRescueV2/preentry-carrier.json");
            Assert.That(File.Exists(path), Is.True, "The reviewed immutable carrier; no generation fallback.");
            var carrier = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
            byte[] bytes = Convert.FromBase64String((string)carrier["base64"]);
            using (var hash = SHA256.Create())
                Assert.That(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""), Is.EqualTo(OriginalSha));
            carried = JsonConvert.DeserializeObject<EpisodeState>(new UTF8Encoding(false, true).GetString(bytes), Fields);
            Assert.That(carried.dialogueRulesVersion, Is.Zero);
            Assert.That(carried.week, Is.EqualTo(5));
            voter = EpisodeEngine.Voters(carried).Single(p => !p.isPlayer).id;
            var rescue = carried.ledger.power.Single(p => p.week == 2);
            Assert.That(rescue.vetoHolderId, Is.EqualTo(voter)); Assert.That(rescue.savedId, Is.EqualTo(carried.playerId));
        }

        private EpisodeState Enrolled()
        {
            var s = carried.Clone();
            s.dialogueRulesVersion = DialogueRules.Rules2;
            s.schemaVersion = R2.Start(1).schemaVersion; // The live schema: what the bump's upgrade writes, with no dialogue field.
            Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
            return s;
        }

        [Test]
        public void TheReplyOffersTheRescueAndCitingItKeepsThePublicRecordForTheStrategicPlea()
        {
            if (R2.HeldBelowItsSchema()) return;
            var s = Enrolled();
            var engine = new EpisodeEngine(s);
            var entered = R2.Accepted(engine, R2.Entry(s, voter));
            var episode = R2.Conversation(entered, voter);
            var cycle = R2.Cycle(entered, episode);
            Assert.That(DialogueCampaignWord.OffersRescue(cycle.beatId), Is.True, "The voter's rescue is a fact the player holds.");
            Assert.That(DialogueRules.TryReadPublicRescue(entered, cycle.eventId, out var view), Is.True, "The HUD's public line.");
            Assert.That(view.HolderId, Is.EqualTo(voter)); Assert.That(view.SavedId, Is.EqualTo(s.playerId)); Assert.That(view.Week, Is.EqualTo(2));
            var cited = R2.Accepted(engine, R2.Choice(entered, DialogueCampaignWord.CiteRescue));
            var row = R2.Conversation(cited, voter).evidence.Single();
            Assert.That(row.kind, Is.EqualTo(DialogueCampaignWord.PowerEvidence));
            Assert.That(R2.Json(row.power), Is.EqualTo(R2.Json(s.ledger.power.Single(p => p.week == 2))), "An exact copy of the public record.");
            Assert.That(row.memory, Is.Null); Assert.That(row.statementId, Is.Null);
            var pleaded = R2.Accepted(engine, R2.Choice(cited, DialogueCampaignWord.Plea));
            Assert.That(pleaded.lobbies.Single(l => l.week == pleaded.week && l.deciderId == voter).approach, Is.EqualTo(LobbyApproach.Strategic));
            Assert.That(R2.Remaining(pleaded), Is.EqualTo(R2.Remaining(s) - 1));
        }

        [Test]
        public void AWordReplacesTheRescueAndTheRescueIsNeverCitedBesideIt()
        {
            if (R2.HeldBelowItsSchema()) return;
            var s = Enrolled();
            var engine = new EpisodeEngine(s);
            var asked = R2.Accepted(engine, R2.AskVote(s, voter));
            var claim = asked.ledger.claims.LastOrDefault(k => k.week == asked.week && k.voterId == voter && k.source == ClaimSource.Told);
            var entered = R2.Accepted(engine, R2.Entry(asked, voter));
            var episode = R2.Conversation(entered, voter);
            var reply = entered.houseEvents.Single(e => e.id == R2.Cycle(entered, episode).eventId);
            bool word = claim != null && claim.targetId != entered.playerId;
            Assert.That(reply.choices.Any(c => c.optionId == DialogueCampaignWord.CiteWord), Is.EqualTo(word));
            Assert.That(reply.choices.Any(c => c.optionId == DialogueCampaignWord.CiteRescue), Is.EqualTo(!word),
                "One fact at a node: their own word when they gave one to keep the player, the rescue otherwise.");
        }
    }
}
#endif
