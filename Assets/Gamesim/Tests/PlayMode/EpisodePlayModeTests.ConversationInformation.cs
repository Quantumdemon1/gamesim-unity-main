using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallInformationHouse(bool lands = true, bool fresh = true, bool campaign = false, int size = 8)
        {
            yield return InstallTalkingHouse(size, campaign, s =>
            {
                EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableLevers(s);
                s.economyRulesVersion = fresh ? 1 : 0;
                // Pick a first draw on the same side of BOTH existing source gates.
                for (uint seed = 1; seed < 1000; seed++)
                {
                    double draw = new SeededRandom(seed).NextDouble();
                    if (lands ? draw > WebSocialVocabulary.MeetingFloor : draw <= WebSocialVocabulary.DiscussGameFloor)
                    { s.randomState = seed; break; }
                }
            });
        }
        private static int AiringReads(EpisodeState s) => s.events.Count(e =>
            e.kind == ConversationIntentRules.AiringBacked || e.kind == ConversationIntentRules.AiringOpposed);

        [UnityTest]
        public IEnumerator ConversationInformation_OpenGameButtonEarnsOpinionsOnceAndReloadsTheExactCommit()
        {
            yield return InstallInformationHouse(); yield return TalkTo(ContentCatalog.MayaId);
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.DiscussGameCaption)), Is.EqualTo(EpisodeDirector.LearnTag + " · " + EpisodeDirector.RiskTag));
            var before = director.Snapshot; var click = ButtonWithCaption(EpisodeHud.DiscussGameCaption).onClick;
            click.Invoke(); var after = director.Snapshot;
            Assert.That(after.ledger.standings.Count - before.ledger.standings.Count, Is.EqualTo(2));
            Assert.That(after.memories.Count(m => m.ownerId == after.playerId && m.subjectId == ContentCatalog.MayaId), Is.GreaterThanOrEqualTo(2));
            AssertIntentCommand(before, EpisodeCommandKind.DiscussGame, ContentCatalog.MayaId);
            click.Invoke(); AssertIntentDurable(after);
            director.LoadNow(); HoldTheHouseForTheFixture(); AssertIntentDurable(after);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationInformation_OpenGameBackfireSpendsOnceButDisclosesNothing()
        {
            yield return InstallInformationHouse(lands: false); yield return TalkTo(ContentCatalog.MayaId);
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.DiscussGameCaption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(after.ledger.standings.Count, Is.EqualTo(before.ledger.standings.Count));
            Assert.That(after.windowActions[Windows.AfterEviction], Is.EqualTo(before.windowActions[Windows.AfterEviction] + 1));
            AssertIntentCommand(before, EpisodeCommandKind.DiscussGame, ContentCatalog.MayaId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationInformation_FailedSavePublishesNeitherOpinionsNorMeetingReads()
        {
            yield return InstallInformationHouse(); yield return TalkTo(ContentCatalog.MayaId);
            var before = director.Snapshot; byte[] bytes = File.ReadAllBytes(director.SavePath);
            using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
                ButtonWithCaption(EpisodeHud.DiscussGameCaption).onClick.Invoke();
            AssertIntentDurable(before); Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            director.ClosePanels(); yield return OpenStation();
            before = director.Snapshot; bytes = File.ReadAllBytes(director.SavePath);
            using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None))
                ButtonWithCaption(EpisodeHud.AirLaundryCaption).onClick.Invoke();
            AssertIntentDurable(before); Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator ConversationInformation_BoardAiringPersistsEachReactionAndOldClickHasNoAuthority()
        {
            yield return InstallInformationHouse(); yield return OpenStation();
            Assert.That(director.IsFreeTimeBoard, Is.True);
            var button = ButtonWithCaption(EpisodeHud.AirLaundryCaption);
            Assert.That(button.GetComponentsInChildren<TMP_Text>().Any(t => t.text == EpisodeDirector.AiringRiskLabel(director.Snapshot)), Is.True);
            var before = director.Snapshot; var click = button.onClick; click.Invoke();
            var after = director.Snapshot;
            Assert.That(AiringReads(after) - AiringReads(before), Is.EqualTo(after.Active.Count() - 1));
            AssertIntentCommand(before, EpisodeCommandKind.HouseMeeting, null, EpisodeEngine.AirDirtyLaundry);
            foreach (var npc in after.Active.Where(c => !c.isPlayer))
                Assert.That(HouseguestNotes.For(after, npc.id).Any(n => n.kind == HouseguestNotes.Kinds.Read && n.text.Contains("airing")), Is.True);
            click.Invoke(); AssertIntentDurable(after);
            director.LoadNow(); HoldTheHouseForTheFixture(); AssertIntentDurable(after);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationInformation_HouseguestScreenUsesTheSameAiringWithAnHonestPayoffDescription()
        {
            yield return InstallInformationHouse(); yield return OpenStation();
            director.OpenHouseguestScreen(ContentCatalog.MayaId); yield return null;
            Assert.That(director.HouseguestScreenFor, Is.EqualTo(ContentCatalog.MayaId));
            var button = ButtonWithCaption(EpisodeHud.AirLaundryCaption);
            Assert.That(button.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("No vote is promised")), Is.True);
            var before = director.Snapshot; button.onClick.Invoke();
            Assert.That(AiringReads(director.Snapshot), Is.EqualTo(before.Active.Count() - 1));
            AssertIntentCommand(before, EpisodeCommandKind.HouseMeeting, null, EpisodeEngine.AirDirtyLaundry);
        }

        [UnityTest]
        public IEnumerator ConversationInformation_CampaignAiringUsesItsOwnWindowAndTheSameReads()
        {
            yield return InstallInformationHouse(campaign: true); yield return OpenStation();
            ButtonWithCaption(EpisodeDirector.CampaignMoreCaption).onClick.Invoke(); yield return null;
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.AirLaundryCaption).onClick.Invoke();
            var after = director.Snapshot;
            Assert.That(AiringReads(after), Is.EqualTo(before.Active.Count() - 1));
            Assert.That(after.windowActions[Windows.AfterVeto], Is.EqualTo(before.windowActions[Windows.AfterVeto] + 1));
            Assert.That(after.windowActions[Windows.AfterEviction], Is.EqualTo(before.windowActions[Windows.AfterEviction]));
            AssertIntentCommand(before, EpisodeCommandKind.HouseMeeting, null, EpisodeEngine.AirDirtyLaundry);
        }

        [UnityTest]
        public IEnumerator ConversationInformation_BackfiredAiringLeavesNoReadsAndLegacyTagsDoNotAdvertiseInformation()
        {
            yield return InstallInformationHouse(lands: false); yield return OpenStation();
            var before = director.Snapshot; ButtonWithCaption(EpisodeHud.AirLaundryCaption).onClick.Invoke();
            Assert.That(AiringReads(director.Snapshot), Is.Zero);
            AssertIntentCommand(before, EpisodeCommandKind.HouseMeeting, null, EpisodeEngine.AirDirtyLaundry);
            yield return InstallInformationHouse(fresh: false); yield return TalkTo(ContentCatalog.MayaId);
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.DiscussGameCaption)), Is.EqualTo(EpisodeDirector.VerbTag(EpisodeCommandKind.DiscussGame)));
            Assert.That(EpisodeDirector.AiringRiskLabel(director.Snapshot), Is.EqualTo("High risk"));
            before = director.Snapshot; ButtonWithCaption(EpisodeHud.DiscussGameCaption).onClick.Invoke();
            Assert.That(director.Snapshot.ledger.standings.Count, Is.EqualTo(before.ledger.standings.Count));
            AssertIntentCommand(before, EpisodeCommandKind.DiscussGame, ContentCatalog.MayaId);
        }

        [UnityTest]
        public IEnumerator ConversationInformation_InformationControlsRemainKeyboardReachableAtBothTextSizes()
        {
            yield return InstallInformationHouse(size: 16);
            foreach (bool large in new[] { false, true })
            {
                yield return ApplyTextSize(large); yield return TalkTo(ContentCatalog.MayaId);
                Assert.That(TagOn(ButtonWithCaption(EpisodeHud.DiscussGameCaption)), Is.EqualTo(EpisodeDirector.LearnTag + " · " + EpisodeDirector.RiskTag));
                yield return AssertKeyboardRing("Information conversation " + large, ModalRoot);
                director.ClosePanels(); yield return OpenStation();
                Assert.That(ButtonWithCaption(EpisodeHud.AirLaundryCaption).IsInteractable(), Is.True);
                yield return AssertKeyboardRing("Information house moves " + large, ModalRoot);
            }
        }

        [UnityTest]
        public IEnumerator ConversationInformation_NotebookReadFilterShowsTheCapturedAiringReactions()
        {
            yield return InstallInformationHouse(); yield return OpenStation();
            ButtonWithCaption(EpisodeHud.AirLaundryCaption).onClick.Invoke();
            var after = director.Snapshot;
            director.ClosePanels(); director.ShowNotebookSection(EpisodeDirector.NotebookSection.Notes);
            yield return null; yield return null;
            ButtonWithCaption("Your reads").onClick.Invoke(); yield return null; yield return null;
            string visible = NotebookText();
            foreach (var reaction in after.events.Where(e => e.kind == ConversationIntentRules.AiringBacked || e.kind == ConversationIntentRules.AiringOpposed))
                Assert.That(visible, Does.Contain(reaction.text));
            AssertIntentDurable(after);
        }
    }
}
