using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
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
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The most live controls a grouped conversation holds with every picker shut, the room's own acts aside.</summary>
        private const int FreeTimeConversationCeiling = 27, CampaignConversationCeiling = 34;

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

            int revision = director.Snapshot.revision;
            yield return KeyboardSubmit(first);
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision + 1), "Enter on a person commits what their row did.");
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
        /// The groups and an open picker in the smallest house and the largest, at both text sizes:
        /// every row keeps room for its words, nothing is clipped, every label draws, and every person
        /// stands inside the column without touching the next. Photographed in a batch run as
        /// 'conversation-grouped', and with venting open as 'conversation-grouped-picker'.
        /// </summary>
        [UnityTest]
        public IEnumerator ConversationGroups_TheGroupsAndAPickerFitAtBothTextSizes()
        {
            foreach (int houseguests in new[] { 3, 16 })
            {
                yield return InstallTalkingHouse(houseguests, false);
                foreach (bool larger in new[] { false, true })
                {
                    yield return ApplyTextSize(larger);
                    yield return TalkTo(Listener(director.Snapshot).id);
                    string where = "The grouped conversation in a house of " + houseguests + (larger ? " at the larger text" : "");
                    AssertConversationFits(larger, where);
                    // The batch canvas is 4:3; the capture lays the HUD out again on the 16:9 frame, so
                    // the copy is checked there too while it is.
                    if (houseguests == 16 && Application.isBatchMode)
                        yield return CaptureFraming(larger ? "conversation-grouped-large" : "conversation-grouped",
                            inspect: _ => AssertCopyHolds(where + " on the 16:9 frame"));
                    ButtonWithCaption(EpisodeDirector.VentPickerCaption).onClick.Invoke();
                    yield return null;
                    AssertConversationFits(larger, where + " with venting open");
                    AssertPickerFits(EpisodeDirector.VentPickerCaption, where);
                    if (houseguests == 16 && Application.isBatchMode)
                        yield return CaptureFraming(larger ? "conversation-grouped-picker-large" : "conversation-grouped-picker",
                            inspect: _ => AssertCopyHolds(where + " with venting open on the 16:9 frame"));
                    director.ClosePanels();
                    yield return null;
                }
                yield return ApplyTextSize(false);
            }
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

        /// <summary>An open picker's people stand inside the conversation's column, apart from one another, each face clear of its words.</summary>
        private void AssertPickerFits(string verb, string where)
        {
            Canvas.ForceUpdateCanvases();
            var grid = ActiveRect(EpisodeHud.PersonPickerPrefix + verb);
            Assert.That(grid, Is.Not.Null, where + ": the picker is open.");
            var column = ScreenRect(ActiveRect(EpisodeHud.ConversationColumnName));
            var people = grid.GetComponentsInChildren<Button>().Select(button => (RectTransform)button.transform).ToArray();
            Assert.That(people, Is.Not.Empty, where + ": the picker holds its people.");
            for (int a = 0; a < people.Length; a++)
            {
                var cell = ScreenRect(people[a]);
                Assert.That(cell.xMin >= column.xMin - .5f && cell.xMax <= column.xMax + .5f, Is.True,
                    where + ": '" + people[a].name + "' runs out of the column: " + cell + " in " + column + ".");
                var words = people[a].GetComponentsInChildren<TMP_Text>().First(text => text.text == people[a].name);
                var face = people[a].Find("Portrait") as RectTransform;
                if (face != null)
                    Assert.That(ScreenRect(face).xMax, Is.LessThanOrEqualTo(ScreenRect(words.rectTransform).xMin + .5f),
                        where + ": '" + people[a].name + "' has its face on its words.");
                for (int b = a + 1; b < people.Length; b++)
                    Assert.That(cell.Overlaps(ScreenRect(people[b])), Is.False,
                        where + ": '" + people[a].name + "' and '" + people[b].name + "' are drawn over each other.");
            }
        }
    }
}
