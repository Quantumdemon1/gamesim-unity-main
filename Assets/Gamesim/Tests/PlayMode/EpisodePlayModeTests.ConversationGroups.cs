using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The conversation grouped by intent (ACTIONS-DEALS-ALLIANCES-PLAN V5, with X-a's promise rows
    /// and X12's oath): four groups under the dial - bond, learn, scheme, bargain - each verb aimed at
    /// a third houseguest one row that opens its people beneath it, and every caption the
    /// conversation had still exactly one control, in free time and in the campaign, in a house of
    /// eight and of sixteen.
    ///
    /// <para>Measured before the change, from the director's own calls on a new season's state: a
    /// conversation with a voter held 50 controls in free time and 58 in the campaign in a house of
    /// eight, and 90 and 98 in a house of sixteen - four rows for every other houseguest, and a target
    /// agreement for each of them besides. Grouped it holds 25 and 32, at either size.</para>
    ///
    /// <para>The same shape holds where the conversation starts somewhere else: a deal the player came
    /// to pitch opens on the folded deal table, and a strategy window's decider hears only what the
    /// window allows.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The most live controls a grouped conversation holds with every picker shut, the room's own acts aside.</summary>
        private const int FreeTimeConversationCeiling = 27, CampaignConversationCeiling = 34;

        /// <summary>The pact the fit tests give the player, so a person's foot has an ALLY to show.</summary>
        private const string FitAllianceId = "alliance-conversation-fit";

        /// <summary>The room's own acts: they come and go with where the player stands, so the counts leave them out.</summary>
        private static readonly string[] ConversationRoomActs =
        {
            EpisodeDirector.PillowTalkCaption, EpisodeDirector.CookCaption, EpisodeDirector.InviteUpCaption,
            EpisodeDirector.PublicDefenseCaption, EpisodeDirector.AllianceMeetCaption, EpisodeDirector.CompPracticeCaption,
            EpisodeDirector.PlayAGameCaption,
        };

        /// <summary>
        /// A house of <paramref name="houseguests"/> in free time, or at its campaign with the first
        /// houseguest at the head of it, the next two on the block, the fourth holding the veto and the
        /// levers on; nothing on the deal table; shaped by <paramref name="shape"/>. The house is held,
        /// so nothing commits under the test.
        /// </summary>
        private IEnumerator InstallTalkingHouse(int houseguests, bool campaign, System.Action<EpisodeState> shape = null)
        {
            HoldTheHouseForTheFixture();
            var state = FullHouse(4401, houseguests);
            state.strategyRulesStartWeek = 1;
            if (campaign)
            {
                AtVetoMeeting(state, false);
                state.phase = EpisodePhase.Campaign;
                state.vetoResolved = true;
                EpisodeEngine.EnableLevers(state);
            }
            state.deals.Clear();
            shape?.Invoke(state);
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(state);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return null;
        }

        /// <summary>Who the player talks to: a houseguest with a body who votes this week and holds nothing - not the head of the house, not on the block, not the veto.</summary>
        private ContestantState Listener(EpisodeState state)
        {
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy).Select(npc => npc.Id).ToList();
            var listener = state.Active.FirstOrDefault(c => !c.isPlayer && c.id != state.hohId && !state.nominees.Contains(c.id)
                && c.id != state.vetoHolderId && bodies.Contains(c.id));
            Assert.That(listener, Is.Not.Null, "The house has somebody with a body to talk to.");
            return listener;
        }

        /// <summary>Every picker a conversation outside the decision windows should offer, by its verb row, with the captions of exactly the people it should hold.</summary>
        private static List<(string verb, string[] people)> ExpectedPickers(EpisodeState state, string listenerId)
        {
            var others = state.Active.Where(c => !c.isPlayer && c.id != listenerId).ToList();
            var pickers = new List<(string verb, string[] people)>
            {
                (EpisodeDirector.VentPickerCaption, others.Select(c => EpisodeDirector.VentCaption(c.name)).ToArray()),
                (EpisodeDirector.LiePickerCaption, others.Select(c => EpisodeDirector.LieCaption(c.name)).ToArray()),
                (EpisodeDirector.WhisperPickerCaption, others.Select(c => EpisodeHud.WhisperCaption(c.name)).ToArray()),
                (EpisodeDirector.CalloutPickerCaption, others.Select(c => EpisodeHud.CalloutCaption(c.name)).ToArray()),
            };
            // The deal table's target agreements, one for each houseguest it could be about.
            var targets = PlayerDeals.Available(state, listenerId).Contains(DealKind.TargetAgreement)
                ? PlayerDeals.Subjects(state, listenerId) : new List<string>();
            if (targets.Count > 0)
                pickers.Add((EpisodeDirector.TargetDealPickerCaption,
                    targets.Select(id => EpisodeHud.DealProposeCaption("target agreement against " + state.Find(id).name)).ToArray()));
            var promised = EpisodeDirector.PromiseToEvictTargets(state, listenerId);
            if (promised.Count > 0)
                pickers.Add((EpisodeDirector.PromiseToEvictPickerCaption,
                    promised.Select(id => EpisodeDirector.PromiseToEvictCaption(state.Find(id).name)).ToArray()));
            return pickers;
        }

        /// <summary>Every live control on the open panel: its close, the dial's petals and the rows. A closing panel's ghost has none.</summary>
        private List<Button> PanelControls() => director.GetComponentsInChildren<Button>()
            .Where(button => button.IsActive() && button.IsInteractable() && UnderPanel(button.transform)).ToList();

        private static bool UnderPanel(Transform node)
        {
            for (; node != null; node = node.parent)
                if (node.name == "Episode panel") return true;
            return false;
        }

        /// <summary>The words on a control's pill - a row's chip, a petal's badge, a person's foot line - or null for none.</summary>
        private static string TagOn(Button button)
        {
            var label = button.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "Tag" || (text.transform.parent != null && text.transform.parent.name == "Tag"));
            return label == null ? null : label.text;
        }

        /// <summary>
        /// The player in a pact with whoever <paramref name="who"/> names, the two reading each other
        /// differently - the player warmly, they coldly - so a person's foot has an ALLY to show and a
        /// reading that can only be the player's own.
        /// </summary>
        private static System.Action<EpisodeState> AlliedWith(System.Func<EpisodeState, string> who) => state =>
        {
            string ally = who(state);
            state.alliances.Add(new AllianceState
            {
                id = FitAllianceId, name = "The Fit", active = true, members = new List<string> { state.playerId, ally },
            });
            Reading(state, state.playerId, ally, 42);
            Reading(state, ally, state.playerId, -42);
        };

        /// <summary>The houseguest the fit tests' pact is with.</summary>
        private static string AllyIn(EpisodeState state) =>
            state.alliances.Single(pact => pact.id == FitAllianceId).members.Single(id => id != state.playerId);

        /// <summary>Presses a row the way a pointer does: brought into view, taken by the selection, then clicked.</summary>
        private IEnumerator PressRow(string caption)
        {
            var row = ButtonWithCaption(caption);
            EventSystem.current.SetSelectedGameObject(row.gameObject);
            row.onClick.Invoke();
            yield return null;
        }

        /// <summary>The people an open picker holds, failing when it is not open or holds nobody.</summary>
        private List<RectTransform> PickerCells(string verb, string where)
        {
            var grid = ActiveRect(EpisodeHud.PersonPickerPrefix + verb);
            Assert.That(grid != null, Is.True, where + ": '" + verb + "' is open.");
            var cells = grid.GetComponentsInChildren<Button>().Select(button => (RectTransform)button.transform).ToList();
            Assert.That(cells.Count, Is.GreaterThan(0), where + ": '" + verb + "' holds its people.");
            return cells;
        }

        /// <summary>Whether any of an open picker's people carries the line named <paramref name="line"/> on its foot.</summary>
        private bool PickerShows(string verb, string line) => PickerCells(verb, "'" + verb + "'").Any(cell => cell.Find(line) != null);

        [UnityTest]
        public IEnumerator ConversationGroups_FourGroupsUnderTheDialAndEachVerbIsOneRow()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            var state = director.Snapshot;
            var content = ButtonWithCaption(EpisodeHud.DiscussGameCaption).transform.parent;
            int Index(string name)
            {
                var child = content.Find(name);
                Assert.That(child, Is.Not.Null, "The conversation draws '" + name + "'.");
                return child.GetSiblingIndex();
            }
            int bond = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.BondGroupTitle);
            int learn = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.LearnGroupTitle);
            int scheme = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.SchemeGroupTitle);
            int bargain = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.BargainGroupTitle);
            Assert.That(Index(EpisodeHud.DialSeatName), Is.LessThan(bond), "The groups stand under the dial.");
            Assert.That(new[] { bond, learn, scheme, bargain }, Is.Ordered.Ascending, "Bond, learn, scheme, bargain, in that order.");
            void Within(string caption, int from, int to)
            {
                int at = Index(caption);
                Assert.That(at > from && at < to, Is.True, "'" + caption + "' belongs to the group it is for.");
            }
            Within(EpisodeHud.DiscussGameCaption, bond, learn);
            Within("Ask what they have heard", learn, scheme);
            Within(EpisodeHud.ReadPersonCaption, learn, scheme);
            Within("Share something I know", learn, scheme);
            foreach (var verb in new[] { EpisodeDirector.VentPickerCaption, EpisodeDirector.LiePickerCaption,
                EpisodeDirector.WhisperPickerCaption, EpisodeDirector.CalloutPickerCaption, "Work against them quietly" })
                Within(verb, scheme, bargain);
            var pickers = ExpectedPickers(state, maya.Id);
            var bargains = new List<string> { "Promise safety", "Propose a final-two promise", "Propose an alliance" };
            if (pickers.Any(picker => picker.verb == EpisodeDirector.TargetDealPickerCaption)) bargains.Add(EpisodeDirector.TargetDealPickerCaption);
            foreach (var caption in bargains)
                Assert.That(Index(caption), Is.GreaterThan(bargain), "'" + caption + "' is a bargain.");
            // What carries no chance comes first - the promises and the pact - then the deal table,
            // whose note on the odds speaks only for the rows under it.
            var promises = new[] { "Promise safety", "Propose a final-two promise", "Propose an alliance" };
            if (pickers.Any(picker => picker.verb == EpisodeDirector.TargetDealPickerCaption))
                foreach (var caption in promises)
                    Assert.That(Index(caption), Is.LessThan(Index(EpisodeDirector.TargetDealPickerCaption)),
                        "'" + caption + "' comes before the deal table's target agreements.");
            foreach (var kind in PlayerDeals.Available(state, maya.Id)
                         .Where(kind => kind != DealKind.TargetAgreement && kind != DealKind.VoteSave && kind != DealKind.VoteEvict))
                Assert.That(Index(EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant())), Is.GreaterThan(Index(promises.Last())),
                    "The deal table's '" + DealKind.Title(kind) + "' comes after the promises and the pact.");

            // Shut, nobody's row is on the screen: each verb is one row.
            foreach (var picker in pickers)
                foreach (var caption in picker.people)
                    Assert.That(ButtonWithCaptionOrNull(caption), Is.Null, "'" + caption + "' waits behind '" + picker.verb + "'.");

            // Each verb's pill says what it is for, and the free ones say so.
            Assert.That(TagOn(ButtonWithCaption(EpisodeDirector.VentPickerCaption)), Is.EqualTo(EpisodeDirector.RiskTag),
                "Venting can cost ten with the listener; it was tagged social.");
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.DiscussGameCaption)), Is.EqualTo(EpisodeDirector.RiskTag));
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.SmallTalkCaption)), Is.EqualTo(EpisodeDirector.WarmthTag));
            Assert.That(TagOn(ButtonWithCaption("Ask what they have heard")), Is.EqualTo(EpisodeDirector.LearnTag));
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.ReadPersonCaption)),
                Is.EqualTo(EpisodeDirector.LearnTag + " · " + EpisodeDirector.RiskTag + " · " + EpisodeDirector.FreeTag),
                "A read is free, and one they notice costs three with them.");
            Assert.That(TagOn(ButtonWithCaption("Promise safety")), Is.EqualTo(EpisodeDirector.BindsYouTag));
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationGroups_EveryCaptionIsStillOneControlInFreeTimeAndTheCampaign()
        {
            foreach (int houseguests in new[] { 8, 16 })
            foreach (bool campaign in new[] { false, true })
            {
                yield return InstallTalkingHouse(houseguests, campaign);
                var listener = Listener(director.Snapshot);
                yield return TalkTo(listener.id);
                Assert.That(director.IsConversationOpen, Is.True, "The conversation opens.");
                var state = director.Snapshot;
                string where = (campaign ? "The campaign" : "Free time") + " in a house of " + houseguests;

                // What the walks and the other tests press directly is still a row of its own, once.
                var direct = PetalCaptions.Concat(RowCaptions)
                    .Concat(new[] { EpisodeHud.ReadPersonCaption, "Propose an alliance", "Work against them quietly" }).ToList();
                if (campaign) direct.Add(EpisodeHud.AskVoteCaption);
                foreach (var kind in PlayerDeals.Available(state, listener.id)
                             .Where(kind => kind != DealKind.TargetAgreement && kind != DealKind.VoteSave && kind != DealKind.VoteEvict))
                    direct.Add(EpisodeHud.DealProposeCaption(DealKind.Title(kind).ToLowerInvariant()));
                if (campaign)
                    foreach (var nominee in state.nominees)
                        direct.Add(EpisodeHud.VoteDealCaption(DealKind.VoteEvict, state.Find(nominee).name));
                foreach (var caption in direct)
                    Assert.That(ButtonWithCaption(caption), Is.Not.Null, where + ": '" + caption + "'.");

                var pickers = ExpectedPickers(state, listener.id);
                Assert.That(pickers.Any(p => p.verb == EpisodeDirector.PromiseToEvictPickerCaption), Is.EqualTo(campaign),
                    where + ": a promise about the vote is offered in the campaign only.");
                foreach (var (verb, people) in pickers)
                {
                    foreach (var caption in people)
                        Assert.That(ButtonWithCaptionOrNull(caption), Is.Null, where + ": '" + caption + "' waits until its verb is opened.");
                    ButtonWithCaption(verb).onClick.Invoke();
                    yield return null;
                    Assert.That(director.ConversationPicker, Is.EqualTo(verb), where + ": '" + verb + "' opens its people.");
                    var grid = ActiveRect(EpisodeHud.PersonPickerPrefix + verb);
                    Assert.That(grid, Is.Not.Null, where + ": '" + verb + "' draws its people under it.");
                    Assert.That(grid.GetComponentsInChildren<Button>().Select(button => button.name), Is.EquivalentTo(people),
                        where + ": '" + verb + "' holds exactly its people, each under the caption its row had.");
                    // ButtonWithCaption finds a control by Single(): one control for each caption, and a player can press it.
                    foreach (var caption in people)
                        Assert.That(ButtonWithCaption(caption).transform.IsChildOf(grid), Is.True, where + ": '" + caption + "' is the picker's.");
                    foreach (var other in pickers.Where(p => p.verb != verb))
                        Assert.That(ActiveRect(EpisodeHud.PersonPickerPrefix + other.verb), Is.Null, where + ": one verb's people at a time.");
                }
                ButtonWithCaption(pickers.Last().verb).onClick.Invoke();
                yield return null;
                Assert.That(director.ConversationPicker, Is.Null, where + ": pressed again, a verb row shuts its people.");

                if (houseguests == 8 && !campaign)
                {
                    // A person's button commits exactly what its row did, and the picker shuts behind it.
                    var about = state.Active.First(c => !c.isPlayer && c.id != listener.id);
                    ButtonWithCaption(EpisodeDirector.VentPickerCaption).onClick.Invoke();
                    yield return null;
                    int revision = director.Snapshot.revision;
                    ButtonWithCaption(EpisodeDirector.VentCaption(about.name)).onClick.Invoke();
                    yield return null;
                    var after = director.Snapshot;
                    Assert.That(after.revision, Is.EqualTo(revision + 1), "One press, one command.");
                    Assert.That(after.events.Last(entry => entry.kind == "vent").text, Does.Contain(about.name).And.Contain(listener.name),
                        "Vented to the houseguest in front of you, about the one the button names.");
                    Assert.That(director.ConversationPicker, Is.Null, "Chosen, the picker shuts and the answer is at the conversation's head.");

                    // Anything else said with a picker open shuts it too: the render that answers is not
                    // held at a picker's row.
                    yield return PressRow(EpisodeDirector.LiePickerCaption);
                    Assert.That(director.ConversationPicker, Is.EqualTo(EpisodeDirector.LiePickerCaption));
                    revision = director.Snapshot.revision;
                    yield return PressRow(EpisodeHud.ReadPersonCaption);
                    Assert.That(director.Snapshot.revision, Is.EqualTo(revision + 1), "The read is taken,");
                    Assert.That(director.ConversationPicker, Is.Null, "and saying something else shuts the open picker.");
                }
                director.ClosePanels();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ConversationGroups_AConversationNoLongerGrowsWithTheHouse()
        {
            var held = new Dictionary<string, int>();
            foreach (bool campaign in new[] { false, true })
            foreach (int houseguests in new[] { 8, 16 })
            {
                yield return InstallTalkingHouse(houseguests, campaign);
                var listener = Listener(director.Snapshot);
                yield return TalkTo(listener.id);
                var state = director.Snapshot;
                string where = (campaign ? "The campaign" : "Free time") + " in a house of " + houseguests;
                var controls = PanelControls().Select(button => button.name).Where(name => !ConversationRoomActs.Contains(name)).ToList();
                int ceiling = campaign ? CampaignConversationCeiling : FreeTimeConversationCeiling;
                Assert.That(controls.Count, Is.LessThanOrEqualTo(ceiling),
                    where + " holds " + controls.Count + " controls: " + string.Join(" | ", controls));
                // What the panel drew before: these rows, less each verb's one row, plus a row for every
                // one of its people - the part that grew with the house.
                var pickers = ExpectedPickers(state, listener.id);
                int people = pickers.Sum(picker => picker.people.Length);
                Assert.That(people, Is.GreaterThanOrEqualTo(4 * (houseguests - 2)), where + ": four verbs fold every other houseguest away.");
                int before = controls.Count - pickers.Count + people;
                Assert.That(controls.Count * 3, Is.LessThanOrEqualTo(before * 2),
                    where + " holds " + controls.Count + " controls where it drew " + before + ": a third or more of them are gone.");
                held[(campaign ? "campaign" : "free time") + " " + houseguests] = controls.Count;
                director.ClosePanels();
                yield return null;
            }
            Assert.That(held["free time 16"], Is.EqualTo(held["free time 8"]), "Free time holds as many controls in a house of sixteen as in a house of eight.");
            Assert.That(held["campaign 16"], Is.EqualTo(held["campaign 8"]), "So does the campaign.");
        }

        [UnityTest]
        public IEnumerator ConversationGroups_APromiseToEvictIsForAVoterAndNeverAboutTheOneTheyTalkTo()
        {
            yield return InstallTalkingHouse(8, true);
            var state = director.Snapshot;
            var first = state.Find(state.nominees[0]);
            var second = state.Find(state.nominees[1]);

            yield return TalkTo(first.id);
            ButtonWithCaption(EpisodeDirector.PromiseToEvictPickerCaption).onClick.Invoke();
            yield return null;
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.PromiseToEvictCaption(first.name)), Is.Null,
                "Nobody is offered a promise to vote themselves out: kept, it bought their thanks on the way to the jury.");
            Assert.That(ButtonWithCaption(EpisodeDirector.PromiseToEvictCaption(second.name)), Is.Not.Null, "The other nominee is on offer.");

            yield return TalkTo(Listener(state).id);
            ButtonWithCaption(EpisodeDirector.PromiseToEvictPickerCaption).onClick.Invoke();
            yield return null;
            foreach (var nominee in new[] { first, second })
                Assert.That(ButtonWithCaption(EpisodeDirector.PromiseToEvictCaption(nominee.name)), Is.Not.Null, "A voter hears either promise.");
            director.ClosePanels();
            yield return null;

            // A player with no vote, and anybody outside the campaign, is offered none: the engine refuses every one.
            foreach (var (shape, campaign, who) in new (System.Action<EpisodeState>, bool, string)[]
            {
                (s => s.nominees[1] = s.playerId, true, "from the block"),
                (s => s.hohId = s.playerId, true, "at the head of the house"),
                (null, false, "in free time"),
            })
            {
                yield return InstallTalkingHouse(8, campaign, shape);
                yield return TalkTo(Listener(director.Snapshot).id);
                Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.PromiseToEvictPickerCaption), Is.Null, "No promise to evict " + who + ".");
                Assert.That(PanelControls().Any(button => button.name.StartsWith("Promise to evict")), Is.False, "No promise about the vote of any kind " + who + ".");
                director.ClosePanels();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ConversationGroups_AnOpenPickerIsOnTheKeyboardRingAndEnterCommitsAPerson()
        {
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            yield return OpenNearbyNpc(maya);
            yield return AssertKeyboardRing("the conversation, every picker shut", ModalRoot);

            yield return KeyboardSubmit(EpisodeDirector.VentPickerCaption);
            Assert.That(director.ConversationPicker, Is.EqualTo(EpisodeDirector.VentPickerCaption), "Enter on a verb row opens its people.");
            yield return AssertKeyboardRing("the conversation with venting open", ModalRoot);

            var row = ControlCarrying(EpisodeDirector.VentPickerCaption);
            EventSystem.current.SetSelectedGameObject(row.gameObject);
            yield return null;
            yield return PressKey(Key.DownArrow);
            // By name: a render the house orders keeps the keyboard on the control of that name, not on the object.
            string first = ActiveRect(EpisodeHud.PersonPickerPrefix + EpisodeDirector.VentPickerCaption).GetComponentsInChildren<Button>()[0].name;
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(first), "Down from the verb row is its first person.");
            yield return PressKey(Key.UpArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(EpisodeDirector.VentPickerCaption), "and Up goes back to it.");

            // The person's button carries their row's words, so what it is about is the name they end with.
            string subject = first.Substring(EpisodeDirector.VentCaption("").Length);
            var before = director.Snapshot;
            int revision = before.revision, vents = before.events.Count(entry => entry.kind == "vent");
            string listener = before.Find(ContentCatalog.MayaId).name;
            yield return KeyboardSubmit(first);
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), "Enter on a person commits one command:");
            Assert.That(after.events.Count(entry => entry.kind == "vent"), Is.EqualTo(vents + 1), "a vent, which is what their row did,");
            Assert.That(after.events.Last(entry => entry.kind == "vent").text, Does.StartWith("You vented about " + subject + " to " + listener),
                "about the person the button named, to the houseguest in front of the player.");
            Assert.That(director.ConversationPicker, Is.Null, "and the picker shuts.");
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(EpisodeDirector.VentPickerCaption),
                "The keyboard stays on the verb it came from, not back at the head of the dial.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationGroups_TheLoyaltyDeclarationSaysItHoldsBothWays()
        {
            yield return EarnVisibleOathOpportunity();
            string name = director.Snapshot.Find(ContentCatalog.MayaId).name;
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.OathOfferLine(name)),
                "The offer says what the engine does: a breach by either of them is the whole house's news.");
            Assert.That(ActiveDiaryText(), Does.Not.Contain("does not bind"));
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.OathDeclareCaption)), Is.EqualTo(EpisodeDirector.BindsYouTag + " · " + EpisodeDirector.FreeTag));
            Assert.That(TagOn(ButtonWithCaption(EpisodeHud.OathDeclineCaption)), Is.EqualTo(EpisodeDirector.FreeTag));
            ButtonWithCaption(EpisodeHud.OathDeclareCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.loyaltyOaths.Any(oath => oath.targetId == ContentCatalog.MayaId), Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.OathRecordedLine(name)), "Once made, it says it holds both ways.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The groups and two of their pickers - venting, and the deal table's target agreements,
        /// which carry the table's odds on their feet - in the smallest house and the largest, at both
        /// text sizes. Every row keeps room for its words, nothing is clipped, every label draws and
        /// every pill stands in a box at least 1.3 times its words. A verb opened stands at the top of
        /// the column with its people under it, though rows above it wrap. Every person stands inside
        /// the column, apart from the next, face, words and foot each in their own place, the foot
        /// saying the player's own reading and ALLY where there is a pact. Photographed in a batch run,
        /// at 16:9 and on a 4:3 frame, as 'conversation-grouped', and with each picker open as
        /// 'conversation-grouped-picker' and 'conversation-grouped-deals'.
        /// </summary>
        // Four pickers laid out and photographed at two sizes on two frames in two houses: over two
        // minutes on its own, and past the runner's three-minute default in a full suite.
        [UnityTest, Timeout(600000)]
        public IEnumerator ConversationGroups_TheGroupsAndAPickerFitAtBothTextSizes()
        {
            foreach (int houseguests in new[] { 3, 16 })
            {
                yield return InstallTalkingHouse(houseguests, false, AlliedWith(season => season.Active.Last(c => !c.isPlayer).id));
                // A body that finishes assembling renders the HUD again; measure in a house that has.
                yield return SettleCast();
                foreach (bool larger in new[] { false, true })
                {
                    yield return ApplyTextSize(larger);
                    var listener = Listener(director.Snapshot);
                    yield return TalkTo(listener.id);
                    var state = director.Snapshot;
                    string where = "Free time in a house of " + houseguests + (larger ? " at the larger text" : "");
                    AssertConversationFits(larger, where);
                    AssertTagsHaveRoom(where);
                    if (houseguests == 16)
                        yield return CaptureConversation(larger ? "conversation-grouped-large" : "conversation-grouped", where, null);
                    Assert.That(PlayerDeals.Subjects(state, listener.id), Is.Not.Empty, where + ": the deal table has target agreements to fold.");
                    foreach (var (verb, frame) in new[]
                    {
                        (EpisodeDirector.VentPickerCaption, "conversation-grouped-picker"),
                        (EpisodeDirector.TargetDealPickerCaption, "conversation-grouped-deals"),
                    })
                    {
                        yield return PressRow(verb);
                        string open = where + " with '" + verb + "' open";
                        // Before anything else scrolls the column.
                        AssertVerbAtTop(verb, open);
                        AssertConversationFits(larger, open);
                        AssertTagsHaveRoom(open);
                        AssertPickerFits(verb, open);
                        if (AllyIn(state) != listener.id)
                            Assert.That(PickerShows(verb, EpisodeHud.PickerAllyName), Is.True, open + ": the pact shows on its person's foot.");
                        if (verb == EpisodeDirector.TargetDealPickerCaption)
                            Assert.That(PickerCells(verb, open).All(cell => cell.Find(EpisodeHud.PickerTagName) != null && ChanceOn(cell.GetComponent<Button>()) != null), Is.True,
                                open + ": every target agreement carries the table's stakes on a chip and the player's read of the odds beside it.");
                        if (houseguests == 16)
                            yield return CaptureConversation(frame + (larger ? "-large" : ""), open, verb);
                    }
                    director.ClosePanels();
                    yield return null;
                }
                yield return ApplyTextSize(false);
            }
        }

        /// <summary>
        /// The campaign's pickers in the largest house, at both text sizes, measured as free time's
        /// are: the promise about the vote, whose people are the nominees - one of them in a pact with
        /// the player, so a foot says ALLY - and the deal table's target agreements. Photographed in a
        /// batch run, at 16:9 and on a 4:3 frame, as 'conversation-grouped-promise'.
        /// </summary>
        [UnityTest]
        public IEnumerator ConversationGroups_TheCampaignsPickersFitAtBothTextSizes()
        {
            yield return InstallTalkingHouse(16, true, AlliedWith(season => season.nominees[1]));
            yield return SettleCast();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                var listener = Listener(director.Snapshot);
                yield return TalkTo(listener.id);
                string where = "The campaign in a house of 16" + (larger ? " at the larger text" : "");
                AssertConversationFits(larger, where);
                AssertTagsHaveRoom(where);
                // The vote deals' rows ("Propose a vote to keep ...") carry the player's own reading,
                // said so, clear of the chevron (UI-UX-PASS-PLAN T0).
                AssertReadingsClearOfChevrons(where, true);
                foreach (string verb in new[] { EpisodeDirector.PromiseToEvictPickerCaption, EpisodeDirector.TargetDealPickerCaption })
                {
                    yield return PressRow(verb);
                    string open = where + " with '" + verb + "' open";
                    AssertVerbAtTop(verb, open);
                    AssertConversationFits(larger, open);
                    AssertTagsHaveRoom(open);
                    AssertPickerFits(verb, open);
                    AssertReadingsClearOfChevrons(open, true);
                    Assert.That(PickerShows(verb, EpisodeHud.PickerAllyName), Is.True, open + ": the nominee in a pact with the player shows it on their foot.");
                    if (verb == EpisodeDirector.PromiseToEvictPickerCaption)
                        yield return CaptureConversation(larger ? "conversation-grouped-promise-large" : "conversation-grouped-promise", open, verb);
                }
                director.ClosePanels();
                yield return null;
            }
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// A conversation the player came into to pitch a deal (the free-time screen's "Pitch a
        /// deal") opens on it: the promises and the pact, then the deal table with its target
        /// agreements folded under their verb row like any other picker - nobody's row until it is
        /// pressed, then exactly the people the table offers, each with the table's stakes on a chip
        /// and the player's read of the odds beside it - and a press commits that agreement. None of it
        /// is drawn again under the dial, where free time has nothing left to bargain with.
        /// </summary>
        [UnityTest]
        public IEnumerator ConversationGroups_ADealYouCameToPitchOpensOnItsFoldedTable()
        {
            yield return InstallTalkingHouse(8, false);
            yield return SettleCast();
            var listener = Listener(director.Snapshot);
            // From somewhere the conversation opens at once, then in again with what the player came for.
            yield return TalkTo(listener.id);
            director.ClosePanels();
            yield return null;
            director.TalkWithIntent(listener.id, EpisodeDirector.IntentDeal);
            yield return null; yield return null;
            const string where = "A deal the player came to pitch";
            Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens");
            Assert.That(director.ConversationIntent, Is.EqualTo(EpisodeDirector.IntentDeal), "on what the player came for.");
            var state = director.Snapshot;

            var content = FindButton(EpisodeHud.DiscussGameCaption).transform.parent;
            int Row(string caption) => FindButton(caption).transform.GetSiblingIndex();
            int Says(string words)
            {
                for (int i = 0; i < content.childCount; i++)
                {
                    var child = content.GetChild(i);
                    if (child.gameObject.activeInHierarchy && child.GetComponentsInChildren<TMP_Text>().Any(text => text.text == words)) return i;
                }
                Assert.Fail(where + ": the conversation says '" + words + "'.");
                return -1;
            }
            var dial = content.Find(EpisodeHud.DialSeatName);
            Assert.That(dial != null, Is.True, where + ": the dial has its seat.");
            // FindButton is Single(): each of these is drawn once, above the dial and not again below it.
            Assert.That(new[]
                {
                    Says("WHAT YOU CAME TO PUT TO THEM"), Row("Promise safety"), Row("Propose a final-two promise"), Row("Propose an alliance"),
                    Row(EpisodeDirector.TargetDealPickerCaption), dial.GetSiblingIndex(),
                },
                Is.Ordered.Ascending, where + ": what the player came for comes first - the promises and the pact, then the deal "
                    + "table with its target agreements folded - and the dial after it.");
            Assert.That(ActiveRect(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.BargainGroupTitle) == null, Is.True,
                where + ": free time has nothing left to bargain with under the dial.");

            var subjects = PlayerDeals.Subjects(state, listener.id);
            Assert.That(subjects, Is.Not.Empty, where + ": there is somebody to name in a target agreement.");
            string Against(string id) =>
                EpisodeHud.DealProposeCaption(DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant() + " against " + state.Find(id).name);
            foreach (string id in subjects)
                Assert.That(ButtonWithCaptionOrNull(Against(id)) == null, Is.True, where + ": '" + Against(id) + "' waits until its verb row is pressed.");

            yield return PressRow(EpisodeDirector.TargetDealPickerCaption);
            AssertVerbAtTop(EpisodeDirector.TargetDealPickerCaption, where, wrappedAbove: false);
            Assert.That(PickerCells(EpisodeDirector.TargetDealPickerCaption, where).Select(cell => cell.name), Is.EquivalentTo(subjects.Select(Against)),
                where + ": the picker holds exactly the table's target agreements, each under the caption its row had.");
            foreach (string id in subjects)
            {
                var card = FindButton(Against(id));
                Assert.That(TagOn(card), Is.EqualTo(EpisodeDirector.Stakes(DealKind.TargetAgreement)),
                    where + ": '" + Against(id) + "' carries the table's stakes on its chip,");
                Assert.That(ChanceOn(card), Is.EqualTo(KnownOdds.CardWord(KnownOdds.Deal(state, listener.id, DealKind.TargetAgreement, id))),
                    "and the player's read of the odds beside it, 'no read' where it has nothing behind it.");
            }
            AssertConversationFits(false, where + " with its target agreements open");
            AssertTagsHaveRoom(where);
            AssertPickerFits(EpisodeDirector.TargetDealPickerCaption, where);

            // A press commits that agreement, as its row did.
            string about = subjects[0];
            int revision = director.Snapshot.revision, deals = director.Snapshot.deals.Count;
            ButtonWithCaption(Against(about)).onClick.Invoke();
            yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(revision + 1), where + ": one press, one command.");
            Assert.That(after.events.Last(entry => entry.kind == "deal").text,
                Does.StartWith(state.Find(listener.id).name).And.Contain(DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant()),
                where + ": a target agreement, put to the houseguest in front of the player.");
            if (after.deals.Count > deals)
                Assert.That(after.deals.Last().targetId, Is.EqualTo(about), where + ": agreed, it is about the houseguest the button named.");
            Assert.That(director.ConversationPicker, Is.Null, where + ": chosen, the picker shuts.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A word with whoever is deciding, in a strategy window: the plea stays above the dial, where
        /// it always was, and the groups below it hold only what the window allows. The asking row
        /// without the read, which waits for free time and the campaign, and the group's line says
        /// so; venting and a lie, told to the one person in front of the player, without the rumours
        /// and the scheming that wait for free time; the promises, the pact and the deal table; and no
        /// promise about a vote that nobody is casting yet.
        /// </summary>
        [UnityTest]
        public IEnumerator ConversationGroups_InAStrategyWindowTheGroupsHoldWhatTheWindowAllows()
        {
            string hoh = null;
            yield return InstallStrategySeason(41, season =>
            {
                season.phase = EpisodePhase.Nomination;
                hoh = season.hohId = season.Active.First(actor => !actor.isPlayer).id;
            });
            yield return TalkTo(hoh);
            const string where = "A word with the Head of Household before nominations";
            Assert.That(director.IsConversationOpen, Is.True, where + ": the conversation opens.");
            var state = director.Snapshot;
            var content = FindButton(EpisodeHud.DiscussGameCaption).transform.parent;
            int Index(string name)
            {
                var child = content.Find(name);
                Assert.That(child != null, Is.True, where + ": the conversation draws '" + name + "'.");
                return child.GetSiblingIndex();
            }
            int dial = Index(EpisodeHud.DialSeatName);
            Assert.That(FindButton(EpisodeHud.LobbyAskCaption(state, state.Find(hoh).name, LobbyAsk.Spare, state.playerId)).transform.GetSiblingIndex(),
                Is.LessThan(dial), where + ": the plea stands above the dial, as it always did.");
            int bond = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.BondGroupTitle);
            int learn = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.LearnGroupTitle);
            int scheme = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.SchemeGroupTitle);
            int bargain = Index(EpisodeHud.ConversationGroupPrefix + EpisodeDirector.BargainGroupTitle);
            Assert.That(new[] { dial, bond, learn, scheme, bargain }, Is.Ordered.Ascending, where + ": the four groups under the dial, in order.");

            // LEARN: the question, without the read, and a line that says why.
            Assert.That(Index("Ask what they have heard"), Is.GreaterThan(learn).And.LessThan(scheme), where + ": what they have heard can be asked.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ReadPersonCaption) == null, Is.True, where + ": the read waits for free time and the campaign,");
            Assert.That(ShownText(), Does.Contain(EpisodeDirector.LearnWindowLine), "and the group's line says so,");
            Assert.That(ShownText(), Does.Not.Contain(EpisodeDirector.LearnLine), "rather than offering a read the window does not.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.AskVoteCaption) == null, Is.True, where + ": there is no vote yet to ask about.");

            // SCHEME: what is said to the person in front of the player, and nothing told to the house.
            foreach (string verb in new[] { EpisodeDirector.VentPickerCaption, EpisodeDirector.LiePickerCaption })
                Assert.That(Index(verb), Is.GreaterThan(scheme).And.LessThan(bargain), where + ": '" + verb + "' is said to them alone.");
            foreach (string later in new[] { EpisodeDirector.WhisperPickerCaption, EpisodeDirector.CalloutPickerCaption, "Work against them quietly" })
                Assert.That(ButtonWithCaptionOrNull(later) == null, Is.True, where + ": '" + later + "' waits for free time.");

            // BARGAIN: the promises, the pact and the table, and no promise about the vote.
            foreach (string caption in new[] { "Promise safety", "Propose a final-two promise", "Propose an alliance" })
                Assert.That(Index(caption), Is.GreaterThan(bargain), where + ": '" + caption + "' is a bargain.");
            Assert.That(ButtonWithCaptionOrNull(EpisodeDirector.PromiseToEvictPickerCaption) == null, Is.True, where + ": nobody is casting a vote yet.");

            // Venting opens the house, all but the one being talked to.
            yield return PressRow(EpisodeDirector.VentPickerCaption);
            Assert.That(PickerCells(EpisodeDirector.VentPickerCaption, where).Select(cell => cell.name),
                Is.EquivalentTo(state.Active.Where(c => !c.isPlayer && c.id != hoh).Select(c => EpisodeDirector.VentCaption(c.name))),
                where + ": venting opens everybody else, each under the caption their row had.");
            AssertConversationFits(false, where + " with venting open");
            AssertPickerFits(EpisodeDirector.VentPickerCaption, where);
            director.ClosePanels();
            yield return null;
        }

        private void AssertConversationFits(bool larger, string where)
        {
            AssertRowsHaveRoom(larger, where);
            AssertCopyHolds(where);
        }

        /// <summary>Nothing on the screen is cut off, and every label on the panel draws its words.</summary>
        private void AssertCopyHolds(string where)
        {
            AssertNothingInThePanelIsClipped(where);
            AssertEveryLabelDraws(ActiveRect("Episode panel"), where);
        }

        /// <summary>
        /// In a batch run, photographs the conversation as '<paramref name="name"/>' on a 16:9 frame
        /// and as '<paramref name="name"/>-4x3' on a 4:3 one, and checks it on each while it is laid
        /// out there: the copy, the pills and, with <paramref name="verb"/> open, where its row stands
        /// and how its people fit. The editor's game view is whatever shape it happens to be.
        /// </summary>
        private IEnumerator CaptureConversation(string name, string where, string verb)
        {
            if (!Application.isBatchMode) yield break;
            yield return CaptureFraming(name, inspect: _ => AssertOnFrame(where + " on the 16:9 frame", verb));
            yield return CaptureFraming(name + "-4x3", inspect: _ => AssertOnFrame(where + " on the 4:3 frame", verb), width: 1200, height: 900);
        }

        private void AssertOnFrame(string where, string verb)
        {
            AssertCopyHolds(where);
            AssertTagsHaveRoom(where);
            if (verb == null) return;
            AssertVerbAtTop(verb, where);
            AssertPickerFits(verb, where);
        }

        /// <summary>Every pill in the column - a row's chip, a person's tag, reading and ALLY - stands in a box at least 1.3 times its words, and is drawn whole.</summary>
        private void AssertTagsHaveRoom(string where)
        {
            Canvas.ForceUpdateCanvases();
            var column = ActiveRect(EpisodeHud.ConversationColumnName);
            Assert.That(column != null, Is.True, where + ": a conversation is open.");
            var pills = column.GetComponentsInChildren<TMP_Text>()
                .Where(text => !string.IsNullOrWhiteSpace(text.text) && (text.name == EpisodeHud.PickerTagName
                    || (text.transform.parent != null && text.transform.parent.name == "Tag")
                    || text.name == EpisodeHud.PickerReadingName || text.name == EpisodeHud.PickerAllyName))
                .ToList();
            Assert.That(pills.Count, Is.GreaterThan(0), where + ": the column's rows carry their pills.");
            foreach (var pill in pills) AssertLineHasRoom(pill, where);
        }

        /// <summary>A line of words in a box at least 1.3 times their size - Inter draws nothing in one under 1.21 of it - and drawn whole.</summary>
        private static void AssertLineHasRoom(TMP_Text line, string where)
        {
            float size = line.enableAutoSizing ? line.fontSizeMax : line.fontSize;
            Assert.That(line.rectTransform.rect.height, Is.GreaterThanOrEqualTo(size * 1.3f - .5f),
                where + ": '" + line.text + "' stands in a box " + line.rectTransform.rect.height.ToString("0.#") + " tall for words of " + size.ToString("0.#") + ".");
            line.ForceMeshUpdate();
            Assert.That(line.isTextOverflowing, Is.False, where + ": '" + line.text + "' is cut off.");
            Assert.That(line.textInfo.characterInfo.Take(line.textInfo.characterCount).Any(glyph => glyph.isVisible), Is.True,
                where + ": '" + line.text + "' draws nothing.");
        }

        /// <summary>
        /// The verb row a press just opened stands at the top of the column with its first person
        /// under it - or the column is scrolled as far as it goes, or has nowhere to scroll, and the
        /// row is in view. Measured before anything else scrolls the column, and with
        /// <paramref name="wrappedAbove"/> only where lines above the row wrap: a reveal worked out
        /// before the rows took their heights put the row wherever the rows above it grew it to.
        /// </summary>
        private void AssertVerbAtTop(string verb, string where, bool wrappedAbove = true)
        {
            Canvas.ForceUpdateCanvases();
            var row = (RectTransform)FindButton(verb).transform;
            var scroll = row.GetComponentInParent<ScrollRect>();
            Assert.That(scroll != null, Is.True, where + ": '" + verb + "' is in the column's scrolling list.");
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            if (wrappedAbove)
            {
                int wrapped = 0;
                for (int i = 0; i < row.GetSiblingIndex(); i++)
                {
                    var above = scroll.content.GetChild(i);
                    if (!above.gameObject.activeInHierarchy) continue;
                    foreach (var label in above.GetComponentsInChildren<TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        if (label.textInfo.lineCount > 1) wrapped++;
                    }
                }
                Assert.That(wrapped, Is.GreaterThan(0), where + ": lines above '" + verb + "' wrap, so where it lands depends on the heights they took.");
            }
            var view = viewport.rect;
            var at = LocalRect(viewport, row);
            float travel = scroll.content.rect.height - view.height;
            bool inView = at.yMax <= view.yMax + 1f && at.yMin >= view.yMin - 1f;
            string measured = "its top " + (view.yMax - at.yMax).ToString("0.#") + " under the column's, " + travel.ToString("0")
                + " to scroll, scrolled to " + scroll.verticalNormalizedPosition.ToString("0.###");
            if (travel <= 1f)
                Assert.That(inView, Is.True, where + ": the column has nowhere to scroll, and '" + verb + "' is in it: " + measured + ".");
            else if (scroll.verticalNormalizedPosition <= .001f)
                Assert.That(inView, Is.True, where + ": the column is scrolled as far as it goes, and '" + verb + "' is in view: " + measured + ".");
            else
                Assert.That(Mathf.Abs(view.yMax - at.yMax), Is.LessThanOrEqualTo(2f), where + ": opened, '" + verb + "' stands at the top of the column: " + measured + ".");
            var first = LocalRect(viewport, PickerCells(verb, where)[0]);
            Assert.That(first.yMax <= at.yMin + .5f && first.yMax <= view.yMax + 1f && first.yMax > view.yMin, Is.True,
                where + ": the first of its people shows under it: " + first + " under " + at + " in " + view + ".");
        }

        /// <summary>
        /// An open picker's people stand inside the conversation's column, apart from one another. On
        /// each, the face, the name, the words, the deal's chip and chance, the trust bar and the foot's
        /// lines - the player's reading and ALLY - keep to their own places inside it, every line in a
        /// box at least 1.3 times its words and drawn whole. The card is drawn around its caption
        /// (UI-UX-PASS-PLAN P1): the caption is the button's first label, on screen in the muted type,
        /// smaller than the person's name over it. The foot says what the row said at its end: the
        /// player's own trust of the person, never theirs of the player, in the rows' words and as a bar
        /// filled to it, and ALLY exactly when the two share a pact. A target agreement's card carries
        /// the table's stakes on its chip and the player's read of the chance beside it - "no read"
        /// exactly where the table's unknowns chip stands and the card's person is one the player knows
        /// nothing of. Measured in the column's own units, so it holds on a captured frame as well as on
        /// the screen.
        /// </summary>
        private void AssertPickerFits(string verb, string where)
        {
            Canvas.ForceUpdateCanvases();
            var column = ActiveRect(EpisodeHud.ConversationColumnName);
            Assert.That(column != null, Is.True, where + ": a conversation is open.");
            var bounds = LocalRect(column, column);
            var cells = PickerCells(verb, where);
            var state = director.Snapshot;
            bool unknowns = ActiveRect(EpisodeHud.UnknownsChipName) != null;
            for (int a = 0; a < cells.Count; a++)
            {
                var cell = cells[a];
                string about = where + ": '" + cell.name + "'";
                var box = LocalRect(column, cell);
                Assert.That(box.xMin >= bounds.xMin - .5f && box.xMax <= bounds.xMax + .5f, Is.True,
                    about + " runs out of the column: " + box + " in " + bounds + ".");
                for (int b = a + 1; b < cells.Count; b++)
                    Assert.That(Inset(box).Overlaps(Inset(LocalRect(column, cells[b]))), Is.False,
                        about + " and '" + cells[b].name + "' are drawn over each other.");

                var words = cell.GetComponentsInChildren<TMP_Text>(true).Single(text => text.transform.parent == cell && text.text == cell.name);
                var lines = new List<TMP_Text> { words };
                foreach (string name in new[] { EpisodeHud.PickerNameName, EpisodeHud.PickerTagName, EpisodeHud.PickerChanceName,
                             EpisodeHud.PickerReadingName, EpisodeHud.PickerAllyName })
                {
                    var line = cell.Find(name);
                    if (line != null && line.gameObject.activeInHierarchy) lines.Add(line.GetComponent<TMP_Text>());
                }
                // The tag's words stand on their chip, so the chip takes their place among the pieces.
                var pieces = lines.Where(line => line.name != EpisodeHud.PickerTagName).Select(line => line.rectTransform).ToList();
                if (cell.Find("Portrait") is RectTransform face) pieces.Add(face);
                var track = cell.Find(EpisodeHud.PickerTrackName) as RectTransform;
                if (track != null) pieces.Add(track);
                var chip = cell.Find(EpisodeHud.PickerChipName) as RectTransform;
                if (chip != null) pieces.Add(chip);
                foreach (var piece in pieces)
                {
                    var at = LocalRect(column, piece);
                    Assert.That(at.xMin >= box.xMin - .5f && at.xMax <= box.xMax + .5f && at.yMin >= box.yMin - .5f && at.yMax <= box.yMax + .5f, Is.True,
                        about + ": " + Piece(piece) + " stands outside it: " + at + " in " + box + ".");
                }
                for (int p = 0; p < pieces.Count; p++)
                for (int q = p + 1; q < pieces.Count; q++)
                    Assert.That(Inset(LocalRect(column, pieces[p])).Overlaps(Inset(LocalRect(column, pieces[q]))), Is.False,
                        about + ": " + Piece(pieces[p]) + " and " + Piece(pieces[q]) + " are drawn over each other.");
                foreach (var line in lines) AssertLineHasRoom(line, about);

                // The caption: the button's first label, the words it is known by, on screen in the muted type.
                Assert.That(cell.GetComponentInChildren<TMP_Text>(true), Is.SameAs(words), about + ": the caption is the button's first label, as a row's is.");
                Assert.That(words.gameObject.activeInHierarchy && words.enabled && words.color.a > .99f, Is.True, about + ": the caption is on screen.");
                Assert.That(words.color, Is.EqualTo(UiTheme.Muted), about + ": the caption is drawn in the muted type of the verb the picker repeats.");

                var person = PersonOnCell(state, cell.name);
                Assert.That(person != null, Is.True, about + " names somebody in the house.");
                var nameLabel = cell.Find(EpisodeHud.PickerNameName);
                Assert.That(nameLabel != null, Is.True, about + " carries the name of who it is about.");
                var nameText = nameLabel.GetComponent<TMP_Text>();
                Assert.That(nameText.text, Is.EqualTo(person.name), about + ": the name is theirs.");
                Assert.That(nameText.fontSize, Is.GreaterThan(words.fontSize),
                    about + ": the name stands large (" + nameText.fontSize.ToString("0.#") + ") over the caption (" + words.fontSize.ToString("0.#") + ").");
                Assert.That(LocalRect(column, nameText.rectTransform).yMin, Is.GreaterThanOrEqualTo(LocalRect(column, words.rectTransform).yMax - .5f),
                    about + ": the name stands over the caption.");

                double score = state.Score(state.playerId, person.id);
                var reading = cell.Find(EpisodeHud.PickerReadingName);
                Assert.That(reading != null, Is.True, about + " says the player's reading of them, as its row did.");
                var readingText = reading.GetComponent<TMP_Text>();
                Assert.That(readingText.text, Is.EqualTo(EpisodeHud.TrustReading(score)),
                    about + ": the reading is the player's own, said so, never theirs of the player.");
                Assert.That(track != null, Is.True, about + " carries the player's trust of them as a bar.");
                var fill = track.Find(EpisodeHud.PickerFillName) as RectTransform;
                Assert.That(fill != null, Is.True, about + ": the bar is filled to something.");
                Assert.That(fill.rect.width / track.rect.width, Is.EqualTo(Mathf.Clamp01((float)(score + 100.0) / 200f)).Within(.01f),
                    about + ": the bar is filled to the player's own trust of them, " + score.ToString("0") + ", on the scale from -100 to 100.");
                Assert.That(fill.GetComponent<Image>().color, Is.EqualTo(readingText.color), about + ": the bar and the reading say one standing in one colour.");
                Assert.That(cell.Find(EpisodeHud.PickerAllyName) != null, Is.EqualTo(state.Allied(state.playerId, person.id)),
                    about + ": ALLY exactly when the two share a pact.");

                // A target agreement's card: the table's stakes on the chip, the player's read beside it.
                if (verb != EpisodeDirector.TargetDealPickerCaption) continue;
                Assert.That(chip != null, Is.True, about + " stands its stakes on a chip.");
                var stakes = cell.Find(EpisodeHud.PickerTagName);
                Assert.That(stakes.GetComponent<TMP_Text>().text, Is.EqualTo(EpisodeDirector.Stakes(DealKind.TargetAgreement)), about + ": the chip says the stakes.");
                var stakesBox = LocalRect(column, (RectTransform)stakes);
                var chipBox = LocalRect(column, chip);
                Assert.That(stakesBox.xMin >= chipBox.xMin - .5f && stakesBox.xMax <= chipBox.xMax + .5f && stakesBox.yMin >= chipBox.yMin - .5f
                    && stakesBox.yMax <= chipBox.yMax + .5f, Is.True, about + ": the stakes stand on their chip: " + stakesBox + " on " + chipBox + ".");
                var estimate = KnownOdds.Deal(state, director.TalkingToId, DealKind.TargetAgreement, person.id);
                string chance = ChanceOn(cell.GetComponent<Button>());
                Assert.That(chance, Is.EqualTo(KnownOdds.CardWord(estimate)), about + ": the chance beside the chip is the player's read.");
                Assert.That(chance == KnownOdds.NoRead, Is.EqualTo(unknowns && !estimate.aboutKnown),
                    about + ": 'no read' exactly where the table says it has little to go on (" + (unknowns ? "it does" : "it does not")
                    + ") and the player knows nothing of where they stand with " + person.name + ".");
            }
        }

        /// <summary>
        /// Who a person's button is about: the houseguest whose name its caption ends with - or
        /// carries between words, as "Call X out publicly" does - the longest such name.
        /// </summary>
        private static ContestantState PersonOnCell(EpisodeState state, string caption) => state.contestants
            .Where(c => !c.isPlayer && (caption.EndsWith(" " + c.name, System.StringComparison.Ordinal) || caption.Contains(" " + c.name + " ")))
            .OrderByDescending(c => c.name.Length).FirstOrDefault();

        /// <summary>The player's read of the chance beside a deal's chip on a person's card, or null where the card has none.</summary>
        private static string ChanceOn(Button button)
        {
            var chance = button.transform.Find(EpisodeHud.PickerChanceName);
            return chance == null ? null : chance.GetComponent<TMP_Text>().text;
        }

        /// <summary>A rectangle's corners in <paramref name="space"/>'s own units, whatever camera the canvas is drawn through.</summary>
        private static Rect LocalRect(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 low = space.InverseTransformPoint(corners[0]), high = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
        }

        /// <summary>A rectangle a quarter of a unit smaller all round: two boxes that only share an edge do not overlap.</summary>
        private static Rect Inset(Rect rect) => Rect.MinMaxRect(rect.xMin + .25f, rect.yMin + .25f, rect.xMax - .25f, rect.yMax - .25f);
    }
}
