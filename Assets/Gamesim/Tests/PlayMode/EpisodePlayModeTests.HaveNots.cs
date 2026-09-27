using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Have-Nots and the veto's prizes in the house: the briefing says what a competition decides
    /// beyond its winner, the standings mark who it decided, the cast rail badges the week's
    /// Have-Nots, and a player among them reads what it costs beside the conversations it takes.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A default-cast season that plays Have-Nots, saved and loaded, shaped by <paramref name="shape"/>.</summary>
        private IEnumerator InstallHaveNotSeason(uint seed, System.Action<EpisodeState> shape)
        {
            var state = ContentCatalog.Create(seed);
            state.competitionRulesVersion = CompetitionRules.Current;
            state.haveNotRulesStartWeek = 1;
            shape?.Invoke(state);
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
        }

        private IEnumerator OpenStation()
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
        }

        private static string HeroText(RectTransform hero) =>
            string.Join(" ", hero.GetComponentsInChildren<TMP_Text>().Select(text => text.text));

        [UnityTest]
        public IEnumerator HaveNots_TheBriefingSaysWhoTheyWillBeAndTheStandingsAndTheRailSayWhoTheyAre()
        {
            yield return InstallHaveNotSeason(31, state => state.phase = EpisodePhase.HoH);
            yield return OpenStation();
            Assert.That(HeroText(ActiveRect(EpisodeHud.BriefingHeroName)), Does.Contain("The last two out are this week's Have-Nots."),
                "A house of six names two, and the stakes say so.");
            ButtonWithCaption(EpisodeDirector.AccessibleCompetitionCaption(CompetitionRules.Current)).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.haveNots, Has.Count.EqualTo(2));

            var card = SceneComponents<CompetitionResult>().Single();
            Assert.That(card.IsPlaying, Is.True);
            var names = card.GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Name").Select(text => text.text).ToList();
            foreach (var id in after.haveNots)
            {
                string name = after.Find(id).name;
                Assert.That(names.Single(row => row.StartsWith(name)), Does.Contain(EpisodeDirector.HaveNotNote), name + "'s row says they are a Have-Not.");
            }
            Assert.That(names.Count(row => row.Contains(EpisodeDirector.HaveNotNote)), Is.EqualTo(2), "and nobody else's does.");

            // The rail badges them once the result is dismissed.
            card.Cancel();
            yield return null; yield return null;
            var rail = director.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == CastRail.RootName);
            var badges = rail.GetComponentsInChildren<TMP_Text>().Where(text => text.text == CastRail.HaveNotBadge).ToList();
            Assert.That(badges.Count, Is.EqualTo(after.haveNots.Count(id => id != after.hohId && !after.nominees.Contains(id))),
                "Each Have-Not's chip wears the badge.");
            foreach (var badge in badges)
            {
                badge.ForceMeshUpdate();
                Assert.That(badge.isTextOverflowing, Is.False, "The badge's word fits its pill.");
                // Truncation is not overflow: the wide chip's fixed 46 drew "HAVE-N" and read as fitting.
                Assert.That(badge.GetPreferredValues(badge.text).x, Is.LessThanOrEqualTo(badge.rectTransform.rect.width + .5f),
                    "The pill is as wide as its word: " + badge.rectTransform.rect.width.ToString("0") + " for \"" + badge.text + "\".");
            }
        }

        [UnityTest]
        public IEnumerator HaveNots_APlayerAmongThemReadsWhatItCostsBesideTheirConversations()
        {
            int budget = 0;
            yield return InstallHaveNotSeason(32, state =>
            {
                budget = EpisodeEngine.SocialActionBudget(state);
                state.haveNots.Add(state.playerId);
            });
            yield return OpenStation();
            var panel = ActiveRect("Episode panel");
            string text = string.Join(" ", panel.GetComponentsInChildren<TMP_Text>().Select(label => label.text));
            Assert.That(text, Does.Contain(EpisodeDirector.HaveNotLine));
            Assert.That(EpisodeEngine.SocialActionBudget(director.Snapshot), Is.EqualTo(budget - 1), "One fewer conversation this week.");
        }

        [UnityTest]
        public IEnumerator VetoPrizes_TheBriefingSaysWhatSecondAndLastTakeAndTheStandingsNameThem()
        {
            yield return InstallHaveNotSeason(33, state =>
            {
                state.phase = EpisodePhase.Veto;
                state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                state.nominees = state.Active.Where(actor => actor.id != state.hohId && !actor.isPlayer).Take(2).Select(actor => actor.id).ToList();
                state.vetoPlayers = state.Active.Select(actor => actor.id).ToList();
            });
            yield return OpenStation();
            Assert.That(HeroText(ActiveRect(EpisodeHud.BriefingHeroName)), Does.Contain("Second place wins a prize; last place takes a punishment."));
            ButtonWithCaption(EpisodeDirector.AccessibleCompetitionCaption(CompetitionRules.Current)).onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.vetoPrizes, Has.Count.EqualTo(2));
            var card = SceneComponents<CompetitionResult>().Single();
            var names = card.GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Name").Select(text => text.text).ToList();
            var prize = HaveNots.Find(after.vetoPrizes[0].prizeId);
            var punishment = HaveNots.Find(after.vetoPrizes[1].prizeId);
            Assert.That(names.Single(row => row.StartsWith(after.Find(after.vetoPrizes[0].contestantId).name)), Does.Contain("Prize: " + prize.Title));
            Assert.That(names.Single(row => row.StartsWith(after.Find(after.vetoPrizes[1].contestantId).name)), Does.Contain("Punishment: " + punishment.Title));
        }
    }
}
