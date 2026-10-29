using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The pilot's second content version on screen (DIALOGUE-PLAN D1): "Cite their public rescue", offered when the
    /// voter used the veto to save the player in an earlier week whose reveal completed. Until now only EditMode drove
    /// it (DialoguePublicRescueV2Tests). Here the HUD does, from the same one retained public-route source, through
    /// the player's real controls: the conversation category, the vote ask, "Desperate plea", the citation and the
    /// plea, with a reload at each node. The source is the EditMode suite's own immutable carrier, read as it reads it,
    /// and the only edit is the declared pilot opt-in to dialogue rules 1.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string CiteCarrierSha = "D3E0C73F5A10BCD85ED37766AE462B6435E588DA08D7E2FFAADF7C70F5BD4D71";

        [UnityTest]
        public IEnumerator DialogueCampaign_CitingThePublicRescueShowsTheRecordAndMakesTheStrategicPleaAcrossReloads()
        {
            yield return InstallDialogueCiteCampaign();
            var before = director.Snapshot;
            var rescue = before.ledger.power.Single(p => p.week == 2);
            string record = "Week 2: " + before.Find(rescue.vetoHolderId).name + " used the veto to save "
                + before.Find(rescue.savedId).name + "; " + before.Find(rescue.replacementId).name + " went on the block.";
            Assert.That(ShownText(), Does.Not.Contain(record), "The record is the conversation's, not the house's.");

            yield return OpenDialogueUiEntry(); // The real category, the vote ask, then "Desperate plea".
            var entered = director.Snapshot;
            Assert.That(entered.revision, Is.EqualTo(before.revision + 1), "One command opened the conversation.");
            Assert.That(entered.windowActions[Windows.AfterVeto], Is.EqualTo(before.windowActions[Windows.AfterVeto] + 1), "It cost the plea's one action.");
            var episode = entered.dialogue.episodes.Single();
            var cycle = entered.storylines.Single(c => c.id == episode.id);
            Assert.That(cycle.templateId, Is.EqualTo(DialogueCampaignV2.TemplateId), "The public rescue makes this voter's conversation the citing one.");
            Assert.That(episode.contentVersion, Is.EqualTo(DialogueCampaignV2.ContentVersion));
            AssertCiteNode(episode.id, DialogueCampaignV2.Case, 1, record);
            Assert.That(ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.AppealLabel)), Is.Not.Null, "The personal appeal stays the other way on.");

            yield return ReloadDialogueUiBytes();
            yield return TalkTo(dialogueUiVoter);
            AssertCiteNode(episode.id, DialogueCampaignV2.Case, 1, record);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV2.CiteLabel)).onClick.Invoke();
            yield return Frames(2);
            var cited = director.Snapshot;
            Assert.That(cited.revision, Is.EqualTo(entered.revision + 1));
            Assert.That(cited.windowActions, Is.EqualTo(entered.windowActions), "The citation is free.");
            Assert.That(cited.randomState, Is.EqualTo(entered.randomState), "The citation draws nothing.");
            Assert.That(cited.lobbies.Count, Is.EqualTo(entered.lobbies.Count), "The citation is no plea.");
            var evidence = cited.dialogue.episodes.Single().evidence.Single();
            Assert.That(evidence.kind, Is.EqualTo("power"));
            Assert.That(evidence.power.week, Is.EqualTo(2));
            Assert.That(cited.dialogue.episodes.Single().responses.Last().responseKey, Is.EqualTo(DialogueCampaignV2.TemplateId + "/explain-that-record"));
            AssertCiteNode(episode.id, DialogueCampaignV2.FinishRecord, 2, record);

            yield return ReloadDialogueUiBytes();
            yield return TalkTo(dialogueUiVoter);
            AssertCiteNode(episode.id, DialogueCampaignV2.FinishRecord, 2, record);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.PleaLabel)).onClick.Invoke();
            yield return Frames(2);
            var closed = director.Snapshot;
            Assert.That(closed.revision, Is.EqualTo(cited.revision + 1));
            Assert.That(closed.windowActions, Is.EqualTo(cited.windowActions), "The plea the entry paid for costs nothing more.");
            Assert.That(DialogueRules.Active(closed), Is.Null, "The plea closes the conversation.");
            var path = closed.storylines.Single(c => c.id == episode.id);
            Assert.That(path.path.Select(p => p.optionId), Is.EqualTo(new[] { DialogueCampaignV1.OpenCase, DialogueCampaignV2.Cite, DialogueCampaignV1.Plea }));
            Assert.That(path.endingId, Is.EqualTo("answered"));
            Assert.That(closed.lobbies.Count, Is.EqualTo(cited.lobbies.Count + 1), "The plea the entry funded, delivered once.");
            var lobby = closed.lobbies.Last();
            Assert.That((lobby.deciderId, lobby.week, lobby.phase), Is.EqualTo((dialogueUiVoter, closed.week, EpisodePhase.Campaign)));
            Assert.That((lobby.ask, lobby.subjectId, lobby.approach), Is.EqualTo((LobbyAsk.Vote, closed.playerId, LobbyApproach.Strategic)),
                "A cited plea is the Strategic plea's roll, as the one-click Strategic plea's.");
            Assert.That(closed.dialogue.episodes.Single().responses.Last().responseKey, Is.EqualTo(DialogueCampaignV2.TemplateId + "/vote-" + lobby.response));
            Assert.That(ShownText(), Does.Not.Contain(record), "The record goes with the conversation.");
            yield return ReloadDialogueUiBytes();
            AssertEquivalent(closed, director.Snapshot);
        }

        /// <summary>
        /// The conversation at <paramref name="node"/> after <paramref name="steps"/> choices, on screen: the public record
        /// drawn once, in words, its continuation pressable, and the house's other conversation not taken from it.
        /// </summary>
        private void AssertCiteNode(string episodeId, string node, int steps, string record)
        {
            var state = director.Snapshot;
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
            Assert.That(DialogueRules.Active(state)?.id, Is.EqualTo(episodeId));
            var cycle = state.storylines.Single(c => c.id == episodeId);
            Assert.That(cycle.beatId, Is.EqualTo(node));
            Assert.That(cycle.path.Count, Is.EqualTo(steps));
            Assert.That(DialogueRules.TryReadPublicRescue(state, cycle.eventId, out var view), Is.True, "The record reads at this node.");
            Assert.That(view.Week, Is.EqualTo(2));
            Assert.That(ShownText(), Does.Contain(record), "The public record is drawn, in words.");
            Assert.That(ButtonWithCaption(EpisodeHud.EventChoiceCaption(node == DialogueCampaignV2.Case
                ? DialogueCampaignV2.CiteLabel : DialogueCampaignV1.PleaLabel)), Is.Not.Null, "The citing path's next step is pressable.");
        }

        /// <summary>
        /// The EditMode suite's retained public-route source (week 5's campaign; Alex, who used the veto to save the player
        /// in week 2, its only voter), read as that suite reads it - the reviewed immutable carrier, its bytes and hash
        /// checked, its public fields only - with the declared pilot opt-in and nothing else, then saved and loaded by the
        /// director as any season is.
        /// </summary>
        private IEnumerator InstallDialogueCiteCampaign()
        {
            HoldTheHouseForTheFixture();
            string path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/DialoguePublicRescueV2/preentry-carrier.json");
            Assert.That(File.Exists(path), Is.True, "The reviewed carrier; no generation fallback.");
            var carrier = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
            Assert.That((string)carrier["kind"], Is.EqualTo("immutable-diagnostic-public-field-dto-carrier-not-savewire"));
            byte[] bytes = Convert.FromBase64String((string)carrier["base64"]);
            using (var sha = SHA256.Create())
                Assert.That(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""), Is.EqualTo(CiteCarrierSha));
            var fixture = JsonConvert.DeserializeObject<EpisodeState>(new UTF8Encoding(false, true).GetString(bytes), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, DateParseHandling = DateParseHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace, FloatParseHandling = FloatParseHandling.Double,
                MaxDepth = 64, ContractResolver = new CitePublicFieldsOnly(),
            });
            Assert.That((fixture.week, fixture.phase, fixture.dialogueRulesVersion), Is.EqualTo((5, EpisodePhase.Campaign, 0)));
            fixture.dialogueRulesVersion = 1; // The declared pilot opt-in, as every pilot fixture's; no role, AP, RNG or history edit.
            Assert.That(EpisodeValidation.TryValidate(fixture, out string error), Is.True, error);
            var voters = EpisodeEngine.Voters(fixture).Where(c => !c.isPlayer).ToArray();
            Assert.That(voters, Has.Length.EqualTo(1));
            dialogueUiVoter = voters[0].id;
            Assert.That(fixture.ledger.power.Single(p => p.week == 2).vetoHolderId, Is.EqualTo(dialogueUiVoter));
            Assert.That(fixture.ledger.power.Single(p => p.week == 2).savedId, Is.EqualTo(fixture.playerId));
            Assert.That(DialogueRules.CanStart(fixture, dialogueUiVoter, out error), Is.True, error);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return SkipReveals();
            AssertEquivalent(fixture, director.Snapshot);
            AssertDialogueUiDisk(director.Snapshot);
        }

        private sealed class CitePublicFieldsOnly : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization) =>
                type.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => base.CreateProperty(f, MemberSerialization.Fields)).ToList();
        }
    }
}
