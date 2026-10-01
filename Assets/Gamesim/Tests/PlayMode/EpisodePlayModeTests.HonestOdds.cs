using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Honest odds on screen (ACTIONS-DEALS-ALLIANCES-PLAN V6 and X-a): the deal table and the plea
    /// show the player's read of a houseguest rather than the houseguest's own mind, and say so; an
    /// empty deal table past the season's ceiling says why; and a veto that saves one nominee does
    /// not take a second nominee's yes. The reader itself is pinned in KnownOddsTests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly string[] TagSeparator = { " · " };

        /// <summary>Every houseguest privately cold on the player, while the player reads every one of them warmly.</summary>
        private static void ColdBehindAWarmReading(EpisodeState state)
        {
            foreach (var npc in state.Active.Where(actor => !actor.isPlayer))
            {
                Reading(state, npc.id, state.playerId, -90);
                Reading(state, state.playerId, npc.id, 60);
            }
        }

        /// <summary>The words on a row's tag: the chip the HUD pins beside its caption.</summary>
        private static string TagWords(Button row)
        {
            var tag = row.transform.Find("Tag");
            Assert.That(tag != null, Is.True, "'" + row.name + "' carries a tag.");
            var label = tag.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label != null, Is.True, "'" + row.name + "' has words on its tag.");
            return label.text;
        }

        /// <summary>Every control on screen carrying exactly these words, in the order they are drawn.</summary>
        private List<Button> ActiveButtons(string caption) => director.GetComponentsInChildren<Button>(true)
            .Where(button => button.IsActive() && button.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == caption))
            .ToList();

        /// <summary>Opens a conversation with the first houseguest the player can reach, and says who it is with.</summary>
        private IEnumerator OpenSomebody(System.Action<string> opened)
        {
            director.ClosePanels();
            yield return null;
            foreach (var npc in SceneComponents<HouseNpc>().Where(body => body.gameObject.activeInHierarchy))
            {
                var who = director.Snapshot.Find(npc.Id);
                if (who == null || who.isPlayer || who.status != ContestantStatus.Active) continue;
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4f;
                    var candidate = npc.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.7f;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.5f, player.Agent.areaMask) || !player.Agent.Warp(hit.position)) continue;
                    player.Agent.ResetPath();
                    Physics.SyncTransforms();
                    if (!director.TryOpenNpc(npc.Id)) continue;
                    opened(npc.Id);
                    yield return null;
                    yield break;
                }
            }
            Assert.Fail("Nobody in the house could be reached for a word.");
        }

        [UnityTest]
        public IEnumerator HonestOdds_TheDealTableShowsYourReadOfThemAndSaysSo()
        {
            yield return InstallStrategySeason(44, state =>
            {
                state.phase = EpisodePhase.Social;
                ColdBehindAWarmReading(state);
                // Nothing on anybody yet: no read, no claim, no history.
                state.memories.RemoveAll(memory => memory.ownerId == state.playerId);
            });
            string id = null;
            yield return OpenSomebody(opened => id = opened);
            var state = director.Snapshot;
            var npc = state.Find(id);

            var plain = PlayerDeals.Available(state, id).Where(kind => kind != DealKind.TargetAgreement).ToList();
            Assert.That(plain, Is.Not.Empty, "There is something to put to " + npc.name + ".");
            int unlike = 0;
            foreach (string kind in plain)
            {
                string shown = TagWords(FindButton(EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant())))
                    .Split(TagSeparator, System.StringSplitOptions.None).Last();
                Assert.That(shown, Is.EqualTo(KnownOdds.Deal(state, id, kind, null).word), kind + ": the tag is the player's read of them.");
                if (shown != KnownOdds.Word(PlayerDeals.AcceptanceChance(state, id, kind, null))) unlike++;
            }
            Assert.That(unlike, Is.GreaterThan(0),
                "Behind the warm reading they are cold on the player: the roll knows it, and no tag may say it.");

            // A target agreement per person it could be about: nothing learned of where they stand
            // with any of them, so every row says the same.
            var subjects = PlayerDeals.Subjects(state, id);
            Assert.That(subjects, Is.Not.Empty);
            var against = subjects.Select(about => TagWords(FindButton(EpisodeHud.DealProposeCaption(
                    DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant() + " against " + state.Find(about).name)))
                .Split(TagSeparator, System.StringSplitOptions.None).Last()).Distinct().ToList();
            Assert.That(against, Is.EqualTo(new[] { KnownOdds.Deal(state, id, DealKind.TargetAgreement, subjects[0]).word }),
                "Lining the rows up says nothing about whom they like or who they are secretly with.");

            Assert.That(ShownText(), Does.Contain(EpisodeDirector.OddsReadLine(FinalistRead.FirstName(npc.name))),
                "The table says the odds are the player's read, not a promise.");
            Assert.That(KnownOdds.Unknowns(state, id), Is.EqualTo(KnownOdds.Many), "No read, no claim, no history.");
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.LittleToGoOnLine));
            var chip = ActiveRect(EpisodeHud.UnknownsChipName);
            Assert.That(chip != null, Is.True, "A chip says how little the read has to go on.");
            var words = chip.GetComponentInChildren<TMP_Text>(true);
            Assert.That(words.text, Is.EqualTo(EpisodeHud.UnknownsChipWords));
            words.ForceMeshUpdate();
            Assert.That(words.isTextOverflowing, Is.False, "The chip is as wide as its words.");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeDirector.DealCeilingLine), "A table with rows has no reason to give.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HonestOdds_APleaShowsYourReadOfWhoeverIsDeciding()
        {
            string hoh = null;
            yield return InstallStrategySeason(41, state =>
            {
                state.phase = EpisodePhase.Nomination;
                hoh = state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                ColdBehindAWarmReading(state);
            });
            yield return TalkTo(hoh);
            var state = director.Snapshot;
            string name = state.Find(hoh).name;
            ButtonWithCaption(EpisodeHud.LobbyAskCaption(state, name, LobbyAsk.Spare, state.playerId)).onClick.Invoke();
            yield return null;

            state = director.Snapshot;
            int unlike = 0;
            foreach (string approach in LobbyApproach.All)
            {
                string shown = TagWords(FindButton(EpisodeHud.LobbyApproachCaption(approach, true)))
                    .Split(TagSeparator, System.StringSplitOptions.None)[0];
                Assert.That(shown, Is.EqualTo(KnownOdds.Plea(state, hoh, LobbyAsk.Spare, state.playerId, approach).word),
                    approach + ": the tag is the player's read of " + name + ".");
                if (shown != KnownOdds.Word(StrategyRules.Chance(state, hoh, LobbyAsk.Spare, state.playerId, approach))) unlike++;
            }
            Assert.That(unlike, Is.GreaterThan(0), "The roll reads how they really see the player; the tags may not.");
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.OddsReadLine(FinalistRead.FirstName(name))), "and the plea says so.");
            Assert.That(director.Snapshot.lobbies, Is.Empty, "Reading the odds commits nothing.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The read's note and its chip hold at both text sizes: the note wraps, the chip is as wide
        /// as its words, and neither runs past the column.
        /// </summary>
        [UnityTest]
        public IEnumerator HonestOdds_TheReadAndItsChipFitAtBothTextSizes()
        {
            foreach (bool larger in new[] { false, true })
            {
                string hoh = null;
                yield return InstallStrategySeason(41, state =>
                {
                    state.phase = EpisodePhase.Nomination;
                    hoh = state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                });
                yield return ApplyTextSize(larger);
                yield return TalkTo(hoh);
                var state = director.Snapshot;
                ButtonWithCaption(EpisodeHud.LobbyAskCaption(state, state.Find(hoh).name, LobbyAsk.Spare, state.playerId)).onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                string at = larger ? "the larger text" : "the standard text";

                var column = ActiveRect(EpisodeHud.ConversationColumnName);
                Assert.That(column != null, Is.True, "A conversation is open at " + at + ".");
                var pieces = director.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect.gameObject.activeInHierarchy && (rect.name == EpisodeHud.OddsNoteName || rect.name == EpisodeHud.UnknownsChipName))
                    .ToList();
                Assert.That(pieces.Any(rect => rect.name == EpisodeHud.OddsNoteName), Is.True, "The note is drawn at " + at + ".");
                Assert.That(pieces.Any(rect => rect.name == EpisodeHud.UnknownsChipName), Is.True, "and, with nothing to go on, the chip.");
                foreach (var label in pieces.SelectMany(rect => rect.GetComponentsInChildren<TMP_Text>(true)))
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, "'" + label.text + "' fits its box at " + at + ".");
                }
                var inside = ScreenRect(column);
                foreach (var chip in pieces.Where(rect => rect.name == EpisodeHud.UnknownsChipName).Select(rect => (RectTransform)rect.GetChild(0)))
                    Assert.That(ScreenRect(chip).xMax, Is.LessThanOrEqualTo(inside.xMax + .5f), "The chip stays in the column at " + at + ".");
                director.ClosePanels();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HonestOdds_PastTheDealCeilingTheTableSaysWhyItIsEmpty()
        {
            yield return InstallStrategySeason(44, state =>
            {
                state.phase = EpisodePhase.Social;
                // The house's own lapsed bargains: the ceiling counts every deal the season has written.
                var npcs = state.Active.Where(actor => !actor.isPlayer).Select(actor => actor.id).ToList();
                for (int i = 0; state.deals.Count < PlayerDeals.PlayerDealCeiling; i++)
                    state.deals.Add(new DealState
                    {
                        id = "deal-npc-lapsed-" + i, type = DealKind.Partnership,
                        proposerId = npcs[i % npcs.Count], recipientId = npcs[(i + 1) % npcs.Count],
                        status = DealStatus.Expired, week = state.week, expiresWeek = state.week,
                        trustImpact = DealKind.DefaultTrust(DealKind.Partnership),
                    });
            });
            string id = null;
            yield return OpenSomebody(opened => id = opened);
            var state = director.Snapshot;
            Assert.That(EpisodeDirector.PastTheDealCeiling(state), Is.True);
            Assert.That(PlayerDeals.Available(state, id), Is.Empty, "Past the ceiling nothing can be put to anybody.");
            Assert.That(ShownText(), Does.Contain("WHAT YOU COULD PUT TO " + state.Find(id).name.ToUpperInvariant()), "The table keeps its heading,");
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.DealCeilingLine), "and says why it has no rows.");
            foreach (string kind in DealKind.All)
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant())), Is.Null, kind);
            Assert.That(ActiveRect(EpisodeHud.OddsNoteName) == null, Is.True, "With no odds on the table there is no read to explain.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator VetoOffers_OnceOneNomineeHasYourWordTheOthersYesIsGreyedAndTheirNoIsNot()
        {
            yield return InstallStrategySeason(43, state =>
            {
                AtVetoMeeting(state, true);
                for (int i = 0; i < state.nominees.Count; i++)
                    state.deals.Add(new DealState
                    {
                        id = "deal-veto-" + (i + 1), type = DealKind.VetoUse, proposerId = state.nominees[i], recipientId = state.playerId,
                        status = DealStatus.Proposed, week = state.week, expiresWeek = state.week,
                        trustImpact = DealKind.DefaultTrust(DealKind.VetoUse),
                    });
            });
            yield return OpenStation();
            yield return null;
            var state = director.Snapshot;
            var accepts = ActiveButtons(EpisodeHud.DealAcceptCaption);
            Assert.That(accepts, Has.Count.EqualTo(2), "Both nominees ask for the veto.");
            Assert.That(accepts.All(button => button.IsInteractable()), Is.True, "Until one is answered, either can be taken.");
            var first = NpcDeals.Pending(state).First(deal => deal.type == DealKind.VetoUse);
            string promised = state.Find(first.proposerId).name;
            Assert.That(ShownText(), Does.Not.Contain(EpisodeDirector.VetoSavesOneLine(promised)));

            ScrollIntoView(accepts[0]);
            AssertPressable(accepts[0], EpisodeHud.DealAcceptCaption);
            accepts[0].onClick.Invoke();
            yield return null; yield return null;

            var after = director.Snapshot;
            Assert.That(after.deals.Single(deal => deal.id == first.id).status, Is.EqualTo(DealStatus.Active), "The first yes is taken.");
            var other = after.deals.Single(deal => deal.type == DealKind.VetoUse && deal.id != first.id);
            Assert.That(other.status, Is.EqualTo(DealStatus.Proposed), "Nothing answered the other nominee for the player.");
            var greyed = FindButton(EpisodeHud.DealAcceptCaption);
            Assert.That(greyed.IsInteractable(), Is.False, "The veto saves one of them: a second yes is a word that cannot be kept.");
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.VetoSavesOneLine(promised)), "and the screen says so.");

            int revision = after.revision;
            greyed.onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "Pressed anyway, it commits nothing.");

            ButtonWithCaption(EpisodeHud.DealDeclineCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.deals.Single(deal => deal.id == other.id).status, Is.EqualTo(DealStatus.Declined),
                "Turning the other nominee down is still the player's to do.");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeDirector.VetoSavesOneLine(promised)), "Answered, the line goes with the offer.");
            Assert.That(ButtonWithCaptionOrNull("Do not use the veto"), Is.Not.Null, "The decision itself is still to make.");
        }
    }
}
