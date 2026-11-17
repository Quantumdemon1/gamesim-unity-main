using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 2, part b: the cast screen's one start commits exactly what its two bright buttons
    /// did - "Start this season" and the details' "Play as" - for the same choices, so a season
    /// started the new way is byte-identical to one started the old way. The old commits are
    /// written out here as the code had them; the seasons are built from one seed and compared.
    /// </summary>
    public sealed class MenuOneWayBackFrontPlayModeTests
    {
        private GameObject owner;
        private const uint Seed = 0x5eed2b01u;

        [UnityTearDown]
        public IEnumerator TearDown() { if (owner != null) Object.Destroy(owner); yield return null; }

        private CastSelect Open(System.Action<SeasonBuilder.Choice> start)
        {
            owner = new GameObject("Menu slice 2 cast test");
            var cast = CastSelect.Attach(owner);
            cast.Show(start, () => { }, (_, __) => { });
            return cast;
        }

        private static void Click(Component root, string caption)
        {
            var buttons = root.GetComponentsInChildren<Button>()
                .Where(button => button.gameObject.activeInHierarchy
                    && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption)).ToArray();
            Assert.That(buttons, Has.Length.EqualTo(1), caption);
            buttons[0].onClick.Invoke();
        }

        /// <summary>The old commit, as CastSelect.Commit had it before menu slice 2, for the default roster and size.</summary>
        private static SeasonBuilder.Choice OldCommit(string pickedId, CharacterDraft authored)
        {
            var choice = new SeasonBuilder.Choice
            {
                Roster = CastTemplates.Roster.Regular,
                PlayerTemplateId = pickedId,
                HouseSize = SeasonBuilder.ClampHouseSize(CastTemplates.Roster.Regular, SeasonBuilder.DefaultHouseSize),
                CustomHouseguests = new System.Collections.Generic.List<CharacterProfile>(),
                CampaignConversations = false,
            };
            var chosen = pickedId == null ? null : CastTemplates.Find(pickedId);
            if (authored != null) choice.Authored = authored.Copy();
            else if (chosen != null)
            {
                choice.Authored = CharacterDraft.FromAppearance(chosen);
                var catalog = (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
                if (catalog != null) choice.Authored.Appearance = catalog.Materialize(choice.Authored.Appearance);
            }
            return choice;
        }

        /// <summary>A season built from <paramref name="choice"/> on the test's seed, as its saved words.</summary>
        private static string Season(SeasonBuilder.Choice choice)
        {
            var season = SeasonBuilder.Create(choice, Seed);
            CharacterAppearanceSnapshots.Materialize(season);
            return JsonUtility.ToJson(season);
        }

        private static void AssertSameSeason(SeasonBuilder.Choice now, SeasonBuilder.Choice then, string how)
        {
            Assert.That(now, Is.Not.Null, how + ": the start committed.");
            Assert.That(now.PlayerTemplateId, Is.EqualTo(then.PlayerTemplateId), how);
            Assert.That(now.Authored?.Name, Is.EqualTo(then.Authored?.Name), how);
            Assert.That(now.Authored?.Appearance?.ContentKey(), Is.EqualTo(then.Authored?.Appearance?.ContentKey()), how);
            Assert.That(Season(now), Is.EqualTo(Season(then)), how + ": the season is byte-identical to the old way's.");
        }

        /// <summary>A pick, then the one start: what "Play as" and "Start this season" both committed for a pick with no draft.</summary>
        [UnityTest]
        public IEnumerator TheStartAfterAPickCommitsWhatPlayAsDid()
        {
            SeasonBuilder.Choice committed = null;
            var cast = Open(value => committed = value);
            yield return null;
            var emma = CastTemplates.Find("emma-brown");
            Click(cast, emma.Name);
            yield return null;
            Click(cast, FrontDoorWords.StartAs("Emma"));
            AssertSameSeason(committed, OldCommit(emma.Id, null), "Start as Emma");
        }

        /// <summary>Nobody picked: the start is the newcomer's, as "Start this season" was.</summary>
        [UnityTest]
        public IEnumerator TheStartWithNobodyPickedCommitsWhatStartThisSeasonDid()
        {
            SeasonBuilder.Choice committed = null;
            var cast = Open(value => committed = value);
            yield return null;
            Click(cast, FrontDoorWords.StartAsNewcomer);
            AssertSameSeason(committed, OldCommit(null, null), "Start as a newcomer");
            Assert.That(committed.Authored, Is.Null, "The unaffiliated newcomer, as before.");
        }

        /// <summary>
        /// A houseguest built from somebody else and brought back over a pick: the start names and
        /// commits the built one, as "Start this season" did; pressing the picked card picks it back,
        /// and the start then commits what "Play as" did. Both old commits stay reachable.
        /// </summary>
        [UnityTest]
        public IEnumerator ADraftOfSomeoneElseStartsAsStartDidAndThePickedCardAsPlayAsDid()
        {
            var emma = CastTemplates.Find("emma-brown");
            // Built in the creator from Casey's card and renamed: somebody other than the pick.
            var robin = CharacterDraft.FromAppearance(CastTemplates.Find("casey-wilson"));
            robin.Name = "Robin Vale";

            SeasonBuilder.Choice committed = null;
            var cast = Open(value => committed = value);
            yield return null;
            Click(cast, emma.Name);
            yield return null;
            cast.SetDraft(robin);
            cast.Resume();
            yield return null;
            Click(cast, FrontDoorWords.StartAs("Robin"));
            AssertSameSeason(committed, OldCommit(emma.Id, robin), "Start as Robin, over Emma's pick (the old Start)");

            committed = null;
            cast.Show(value => committed = value, () => { }, (_, __) => { });
            yield return null;
            Click(cast, emma.Name);
            yield return null;
            cast.SetDraft(robin);
            cast.Resume();
            yield return null;
            Click(cast, emma.Name);
            yield return null;
            Assert.That(cast.StartCaptionNow, Is.EqualTo(FrontDoorWords.StartAs("Emma")), "Pressing the picked card picks her back.");
            Click(cast, FrontDoorWords.StartAs("Emma"));
            AssertSameSeason(committed, OldCommit(emma.Id, null), "Start as Emma after Robin (the old Play as Emma)");
        }
    }
}
