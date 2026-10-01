using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
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
    /// Offers announce themselves (ACTIONS-DEALS-ALLIANCES-PLAN V2). A weekly offer wrote no log line
    /// and waited inside one houseguest's conversation until the house's next round wrote it off.
    /// These hold the three places it is said now - a badge on its sender's chip, a line in the
    /// objective, and what moving on costs in the strategy screens' footer - each read from what
    /// is pending and gone with it, and never for anything between two houseguests.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// An offer put to the player wears a badge on its sender's chip and nobody else's; the badge
        /// is decoration, not a control; and answered, it is gone. In a batch run the frame with the
        /// badge up is captured as 'offer-badges'.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBadges_AnOfferWearsABadgeOnItsSendersChipUntilItIsAnswered()
        {
            HoldTheHouseForTheFixture();
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == ContentCatalog.MayaId);
            PutBeforeThePlayer(live => live.deals.Add(WaitingOffer(live, maya.Id, DealKind.SafetyAgreement, "deal-ask-badge")));
            director.ClosePanels();
            yield return null;

            var state = director.Snapshot;
            Assert.That(WaitingOnYou.Read(state).items.Select(item => item.id), Is.EqualTo(new[] { "deal-ask-badge" }),
                "The fixture has the one offer waiting.");
            var rail = LiveRail();
            foreach (var actor in state.contestants)
                Assert.That(WaitingBadges(rail, actor), Is.EqualTo(actor.id == maya.Id ? new[] { CastRail.OfferWaitingName } : new string[0]),
                    actor.name + "'s chip says what of theirs waits on the player, and nothing else.");
            var badge = RailEntry(rail, state.Find(maya.Id)).GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == CastRail.OfferWaitingName);
            AssertDecoration(badge, "Maya's badge");
            Assert.That(RailEntry(rail, state.Find(maya.Id)).GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1),
                "The chip stays the one control it was.");

            if (Application.isBatchMode)
            {
                yield return WaitForRailFaces("offer-badges");
                yield return CaptureFraming("offer-badges");
            }

            yield return OpenNearbyNpc(maya);
            ButtonWithCaption(EpisodeHud.DealDeclineCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.Snapshot.deals.Single(deal => deal.id == "deal-ask-badge").status, Is.EqualTo(DealStatus.Declined));
            director.ClosePanels();
            yield return null;
            Assert.That(WaitingBadges(LiveRail(), director.Snapshot.Find(maya.Id)), Is.Empty, "Answered, the badge is gone.");
        }

        /// <summary>
        /// Last week's offer at the veto meeting after the decision: its sender's chip wears the
        /// badge, the footer says the offer expires when the campaign opens, and continuing writes it
        /// off - and the badge goes with it.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBadges_LastWeeksOfferExpiresAsTheCampaignOpensAndItsBadgeGoes()
        {
            HoldTheHouseForTheFixture();
            string from = null;
            yield return InstallStrategySeason(47, state =>
            {
                AtVetoMeeting(state, false);
                state.vetoResolved = true;
                state.week = 2;
                // Nothing else lapses on the advance, so the strip is the offer's; and a cold house
                // puts nothing new to the player as the campaign opens.
                state.houseEvents.RemoveAll(item => item.IsStory && !item.resolved);
                foreach (var npc in state.Active.Where(actor => !actor.isPlayer)) Reading(state, npc.id, state.playerId, 0);
                from = state.Active.First(actor => !actor.isPlayer && actor.id != state.hohId && actor.id != state.vetoHolderId
                    && !state.nominees.Contains(actor.id)).id;
                var offer = WaitingOffer(state, from, DealKind.Partnership, "deal-ask-old");
                offer.week = offer.expiresWeek = 1;
                state.deals.Add(offer);
            });
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return null;
            var before = director.Snapshot;
            Assert.That(WaitingBadges(LiveRail(), before.Find(from)), Is.EqualTo(new[] { CastRail.OfferWaitingName }));

            yield return OpenStation();
            yield return null;
            var words = FooterWords();
            Assert.That(words.name, Is.EqualTo(EpisodeDirector.MovingOnCostsName), "What continuing costs holds the strip.");
            Assert.That(words.text, Is.EqualTo(First(before, from) + "'s offer expires when the campaign opens."));
            Assert.That(words.color, Is.EqualTo(UiTheme.Warning), "in the warning colour.");

            ButtonWithCaption("Continue episode").onClick.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(after.deals.Single(deal => deal.id == "deal-ask-old").status, Is.EqualTo(DealStatus.Expired),
                "The engine wrote it off as the footer said it would.");
            yield return PutAwayTheCards();
            director.ClosePanels();
            yield return null;
            Assert.That(WaitingBadges(LiveRail(), director.Snapshot.Find(from)), Is.Empty, "Lapsed, the badge is gone.");
        }

        /// <summary>
        /// The objective says who has an offer for the player over the next stop it always had, and
        /// how many once there are several; at both text sizes it fits its one line.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBadges_TheObjectiveSaysWhoHasAnOfferAndHowMany()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return null;
            var quiet = director.Snapshot;
            Assert.That(ObjectiveFirstLine().text, Is.EqualTo(EpisodeHud.WaitingLine(quiet) ?? EpisodeHud.ObjectiveTitle(quiet)),
                "Before anything is put to the player, the objective says what it always said.");

            var maya = quiet.Find(ContentCatalog.MayaId);
            var jamie = quiet.contestants.First(actor => actor.name != null && actor.name.StartsWith("Jamie"));
            PutBeforeThePlayer(live => live.deals.Add(WaitingOffer(live, maya.id, DealKind.Partnership, "deal-ask-one")));
            director.ClosePanels();
            yield return null;
            var line = ObjectiveFirstLine();
            Assert.That(line.name, Is.EqualTo(EpisodeHud.ObjectiveWaitingName));
            Assert.That(line.text, Is.EqualTo(First(quiet, maya.id) + " has an offer for you"));
            Assert.That(ObjectiveWords(), Does.Contain(director.GetComponentInChildren<EpisodeHud>().NextStop(director.Snapshot)),
                "The next stop is under it, as it always was.");
            AssertFitsItsLine(line, "The objective's line");

            PutBeforeThePlayer(live => live.deals.Add(WaitingOffer(live, jamie.id, DealKind.SafetyAgreement, "deal-ask-two")));
            director.ClosePanels();
            yield return null;
            Assert.That(ObjectiveFirstLine().text, Is.EqualTo("2 offers waiting"));

            yield return ApplyTextSize(true);
            director.ClosePanels();
            yield return null;
            AssertFitsItsLine(ObjectiveFirstLine(), "The objective's line at the larger text");
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// The campaign's footer says what closing it costs - the plea that goes unanswered and the
        /// window's seats - in the warning colour beside the way on, outranking what comes next, at
        /// both text sizes. Then the strip's rank, on the open stage in one frame: the storylines'
        /// warning still takes the strip, and an Up next line never does.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBadges_TheCampaignsFooterSaysWhatClosingItCosts()
        {
            HoldTheHouseForTheFixture();
            string pleading = null;
            yield return InstallStrategySeason(46, state =>
            {
                AtVetoMeeting(state, false);
                state.phase = EpisodePhase.Campaign;
                state.vetoResolved = true;
                EpisodeEngine.EnableWeek(state, state.week);
                state.houseEvents.RemoveAll(item => item.IsStory && !item.resolved);
                pleading = state.nominees[0];
                state.replyCards.Add(new ReplyCardState { id = "reply-8", week = state.week, kind = ReplyCards.Plea, fromId = pleading, aboutId = state.nominees[1] });
            });
            HoldTheHouseForTheFixture();
            yield return AtBothTextSizes(larger =>
            {
                var state = director.Snapshot;
                string where = "The campaign" + (larger ? " at the larger text" : "");
                string costs = WaitingOnYou.AdvanceNote(state);
                Assert.That(costs, Is.EqualTo(First(state, pleading) + "'s plea goes unanswered when campaigning closes. "
                    + EpisodeEngine.AfterVetoSeats + " unused actions will be lost."), where + ": the plea and the window's seats go with it.");
                Assert.That(EpisodeEngine.LapsingOnAdvance(state), Is.Empty, "The fixture lets no storyline pass.");
                var words = FooterWords();
                Assert.That(words.name, Is.EqualTo(EpisodeDirector.MovingOnCostsName), where + ": what closing costs outranks what comes next.");
                Assert.That(words.text, Is.EqualTo(costs), where);
                Assert.That(words.color, Is.EqualTo(UiTheme.Warning), where + ": in the warning colour.");
                var strip = ActiveRect(EpisodeHud.StrategyStripName);
                Assert.That(strip.parent, Is.SameAs(ActiveRect("Episode panel")), where + ": beside the way on, outside the scroll.");
                Assert.That(ScreenRect(strip).Overlaps(ScreenRect((RectTransform)FindButton(CloseCampaigning).transform)), Is.False,
                    where + ": the strip and the way on share the row without meeting.");
                AssertEveryLabelDraws(strip, where + "'s strip");
            });

            yield return OpenStation();
            yield return null;
            var hud = director.GetComponentInChildren<EpisodeHud>();
            var line = ActiveRect(EpisodeHud.StrategyStripName).GetComponentsInChildren<TMP_Text>().Single();
            Assert.That(line.name, Is.EqualTo(EpisodeDirector.MovingOnCostsName));
            hud.PinnedNote("Up next: the probe's next step.", null, false);
            Assert.That(line.name, Is.EqualTo(EpisodeDirector.MovingOnCostsName), "An Up next line never covers what moving on costs,");
            hud.PinnedNote("Moving on lets the probe pass.", EpisodeDirector.AdvanceWarningName, true);
            Assert.That(line.name, Is.EqualTo(EpisodeDirector.AdvanceWarningName), "the storylines' warning takes the strip,");
            hud.PinnedNote("A probe's cost.", EpisodeDirector.MovingOnCostsName, EpisodeHud.FooterRank.Notice, true);
            Assert.That(line.text, Is.EqualTo("Moving on lets the probe pass."), "and what moving on costs never covers it.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// An offer one houseguest put to another, and a deal two of them struck, are theirs: no chip
        /// wears a badge for either, nothing is waiting on the player, and the objective names no
        /// offer.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBadges_NoBadgeForAnOfferBetweenTwoHouseguests()
        {
            HoldTheHouseForTheFixture();
            var state = director.Snapshot;
            var npcs = state.Active.Where(actor => !actor.isPlayer).ToList();
            Assert.That(npcs.Count, Is.GreaterThanOrEqualTo(4), "The scenario's house is the player and five others.");
            PutBeforeThePlayer(live =>
            {
                var asked = WaitingOffer(live, npcs[0].id, DealKind.Partnership, "deal-npc-asked");
                asked.recipientId = npcs[1].id;
                live.deals.Add(asked);
                var struck = WaitingOffer(live, npcs[2].id, DealKind.SafetyAgreement, "deal-npc-struck");
                struck.recipientId = npcs[3].id;
                struck.status = DealStatus.Active;
                live.deals.Add(struck);
            });
            director.ClosePanels();
            yield return null;

            var after = director.Snapshot;
            Assert.That(after.deals.Count(deal => deal.id.StartsWith("deal-npc-")), Is.EqualTo(2), "The fixture holds both.");
            Assert.That(WaitingOnYou.Read(after).items, Is.Empty, "Neither is the player's to answer.");
            var rail = LiveRail();
            foreach (var actor in after.contestants)
                Assert.That(WaitingBadges(rail, actor), Is.Empty, actor.name + "'s chip says nothing of a deal between houseguests.");
            Assert.That(ObjectiveFirstLine().text, Does.Not.Contain("offer"), "The objective names no offer.");
        }

        /// <summary>
        /// The badge sits on the face, inside the chip's border, clear of every part a test pins the
        /// chip by - the role pill and the standing tag at the ring's foot, the name, the mood and
        /// its face and target, the role's mark, the standing bar, the traits - and moves none of
        /// them: every part is where a chip with nothing waiting puts it. Swept across the narrow and
        /// the landscape chip at both text sizes, from the smallest scale the strip draws at; each
        /// rail is built and read in one frame, so no render can replace it under the sweep.
        /// </summary>
        [UnityTest]
        public IEnumerator OfferBadges_TheBadgeSitsOnTheFaceClearOfEveryPartAChipIsPinnedBy()
        {
            director.ClosePanels();
            yield return null;
            var probe = new GameObject("Waiting probe", typeof(RectTransform)).GetComponent<RectTransform>();
            probe.SetParent(HudCanvas().transform, false);
            probe.anchorMin = Vector2.zero; probe.anchorMax = Vector2.zero; probe.pivot = Vector2.zero;
            int narrow = 0, wide = 0;
            bool sawFloor = false;
            try
            {
                foreach (int house in new[] { 8, 12 })
                {
                    var waiting = BadgedHouse(house, true);
                    var quiet = BadgedHouse(house, false);
                    foreach (float text in new[] { 1f, 1.2f })
                    for (float width = 760f; width <= 1700f; width += 31f)
                    {
                        probe.sizeDelta = new Vector2(width, 200f);
                        var plain = CastRail.Build(probe, quiet, text, TMP_Settings.defaultFontAsset, _ => null);
                        var rail = CastRail.Build(probe, waiting, text, TMP_Settings.defaultFontAsset, _ => null);
                        try
                        {
                            Canvas.ForceUpdateCanvases();
                            float scale = rail.sizeDelta.y / CastRail.Height;
                            if (Mathf.Abs(scale - .72f) < .001f) sawFloor = true;
                            string at = "A house of " + house + " at " + text + " text, " + width + " wide (scale " + scale.ToString("F3") + ")";
                            Assert.That(RailLayout(rail), Is.EqualTo(RailLayout(plain)), at + ": a badge moves none of the chip's parts.");
                            foreach (RectTransform entry in rail)
                            {
                                var actor = waiting.contestants.Single(candidate => candidate.name == entry.name);
                                var badges = entry.GetComponentsInChildren<RectTransform>(true)
                                    .Where(rect => rect.name == CastRail.OfferWaitingName || rect.name == CastRail.AnswerWaitingName).ToArray();
                                Assert.That(badges.Length, Is.EqualTo(actor.isPlayer ? 0 : 1), at + ": " + entry.name + " wears one badge, or the player none.");
                                if (badges.Length == 0) continue;
                                var badge = badges[0];
                                Assert.That(badge.parent, Is.SameAs(entry), at + ": the badge is the chip's own.");
                                AssertDecoration(badge, at + ": " + entry.name + "'s badge");
                                var box = LocalBounds(entry, badge);
                                var glass = LocalBounds(entry, (RectTransform)Child(entry, CastRail.ChipName));
                                float clearance = UiTheme.BorderThickness + 1f;
                                Assert.That(box.xMin >= glass.xMin + clearance - .01f && box.xMax <= glass.xMax - clearance + .01f
                                    && box.yMin >= glass.yMin + clearance - .01f && box.yMax <= glass.yMax - clearance + .01f, Is.True,
                                    at + ": " + entry.name + "'s badge " + box + " stands inside the chip's border " + glass + ".");
                                foreach (var part in PinnedParts(entry))
                                    Assert.That(Inset(box).Overlaps(LocalBounds(entry, part)), Is.False,
                                        at + ": " + entry.name + "'s badge " + box + " lies on its '" + part.name + "' " + LocalBounds(entry, part) + ".");
                                if (entry.rect.width > 120f) wide++; else narrow++;
                            }
                        }
                        finally
                        {
                            Object.DestroyImmediate(plain.gameObject);
                            Object.DestroyImmediate(rail.gameObject);
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(probe.gameObject);
            }
            Assert.That(narrow, Is.GreaterThan(0), "The sweep never drew a narrow chip with a badge.");
            Assert.That(wide, Is.GreaterThan(0), "The sweep never drew a landscape chip with a badge.");
            Assert.That(sawFloor, Is.True, "The sweep never reached the strip's smallest scale.");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Puts something before the player the way the house would - an offer, a card - straight
        /// into the live season, and refreshes the projection the HUD draws from. Nothing commits:
        /// the house made this move, and there is no command for it (the same seam as the deal
        /// panel's own test).
        /// </summary>
        private void PutBeforeThePlayer(System.Action<EpisodeState> put)
        {
            var engine = (EpisodeEngine)typeof(EpisodeDirector)
                .GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
            var live = (EpisodeState)typeof(EpisodeEngine)
                .GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(engine);
            put(live);
            typeof(EpisodeDirector).GetMethod("Project", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
        }

        /// <summary>An offer to the player in the shape <see cref="NpcDeals.Propose"/> files one: proposed, lapsing with its week.</summary>
        private static DealState WaitingOffer(EpisodeState state, string fromId, string type, string id) => new DealState
        {
            id = id, type = type, proposerId = fromId, recipientId = state.playerId, status = DealStatus.Proposed,
            week = state.week, expiresWeek = state.week, trustImpact = DealKind.DefaultTrust(type),
        };

        /// <summary>The names of the waiting badges on one houseguest's chip.</summary>
        private static string[] WaitingBadges(RectTransform rail, ContestantState actor)
        {
            if (rail == null || actor == null) return new string[0];
            return RailEntry(rail, actor).GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name == CastRail.OfferWaitingName || rect.name == CastRail.AnswerWaitingName)
                .Select(rect => rect.name).ToArray();
        }

        /// <summary>Decoration only: nothing under it takes a click, nothing is a control, and it carries no copy a caption lookup could find.</summary>
        private static void AssertDecoration(RectTransform badge, string what)
        {
            Assert.That(badge.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.raycastTarget).Select(graphic => graphic.name),
                Is.Empty, what + " takes no click.");
            Assert.That(badge.GetComponentsInChildren<Selectable>(true), Is.Empty, what + " is no control.");
            Assert.That(badge.GetComponentsInChildren<TMP_Text>(true), Is.Empty, what + " carries no copy; its name and the objective say it.");
        }

        /// <summary>The objective chip's first line - its title, or the waiting line in its place - in the frame's live chrome.</summary>
        private TMP_Text ObjectiveFirstLine()
        {
            var objective = LastActive("Objective");
            Assert.That(objective, Is.Not.Null, "The objective is up.");
            return objective.GetComponentsInChildren<TMP_Text>().First(label => !label.text.StartsWith("Next stop:") && label.text != "!");
        }

        private string ObjectiveWords() => string.Join("\n", LastActive("Objective").GetComponentsInChildren<TMP_Text>().Select(label => label.text));

        /// <summary>One line, every word drawn, nothing cut, and a box Inter draws in at the size the fit chose.</summary>
        private static void AssertFitsItsLine(TMP_Text line, string what)
        {
            Canvas.ForceUpdateCanvases();
            line.ForceMeshUpdate(true);
            Assert.That(line.isTextOverflowing, Is.False, what + " '" + line.text + "' is cut off at " + line.fontSize.ToString("0.#") + ".");
            Assert.That(line.textInfo.lineCount, Is.EqualTo(1), what + " stays on one line.");
            Assert.That(line.textInfo.characterInfo.Take(line.textInfo.characterCount).Count(glyph => glyph.isVisible),
                Is.EqualTo(line.text.Count(ch => !char.IsWhiteSpace(ch))), what + " draws every character.");
        }

        /// <summary>
        /// A house with something put to the player by everyone in it - offers, and a nominee's plea -
        /// and a chip of every kind: roles, standings both ways, an ally, and a nominee angry at the
        /// Head of Household, whose face rides on the ring opposite the badge. With
        /// <paramref name="waiting"/> false, the same house with nothing waiting.
        /// </summary>
        private static EpisodeState BadgedHouse(int house, bool waiting)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = house }, 2042);
            string you = state.playerId;
            var others = state.contestants.Where(actor => actor.id != you).ToArray();
            state.hohId = others[0].id;
            state.vetoHolderId = others[1].id;
            state.nominees = new List<string> { others[2].id, others[3].id };
            others[2].mood = "Angry";
            others[2].nominationWeeks.Add(state.week);
            Reading(state, you, others[0].id, 20);
            Reading(state, you, others[1].id, -50);
            Reading(state, you, others[2].id, 20);
            Reading(state, you, others[3].id, -20);
            state.alliances.Add(new AllianceState { id = "probe", name = "Probe", members = new List<string> { you, others[4].id }, active = true });
            if (!waiting) return state;
            for (int i = 0; i < others.Length; i++)
            {
                if (i == 3)
                    state.replyCards.Add(new ReplyCardState { id = "reply-" + i, week = state.week, kind = ReplyCards.Plea, fromId = others[i].id, aboutId = others[2].id });
                else state.deals.Add(WaitingOffer(state, others[i].id, DealKind.Partnership, "deal-ask-" + i));
            }
            return state;
        }

        /// <summary>
        /// The parts of a chip the strip's tests pin it by, wherever they hang from it: the role pill
        /// and the standing tag, the name (the chip's first direct label), the mood word, its face and
        /// its target, the role's mark on the ring, the landscape chip's standing bar, and the traits.
        /// </summary>
        private static IEnumerable<RectTransform> PinnedParts(RectTransform entry)
        {
            string[] names = { CastRail.BadgeName, CastRail.StandingTagName, CastRail.MoodWordName, CastRail.MoodGlyphName,
                CastRail.MoodTargetName, "Role mark", "Standing track", "Standing bar", CastRail.TraitName, CastRail.TraitDotName };
            foreach (var part in entry.GetComponentsInChildren<RectTransform>(true).Where(rect => names.Contains(rect.name)))
                yield return part;
            var name = entry.Cast<Transform>().Select(child => child.GetComponent<TMP_Text>()).FirstOrDefault(label => label != null && label.name == "Text");
            if (name != null) yield return name.rectTransform;
        }

        /// <summary>Every part of a rail but the waiting badges, by its place in the tree, with its bounds in the rail's units.</summary>
        private static Dictionary<string, Rect> RailLayout(RectTransform rail)
        {
            var layout = new Dictionary<string, Rect>();
            void Walk(Transform node, string path)
            {
                var seen = new Dictionary<string, int>();
                foreach (Transform child in node)
                {
                    if (child.name == CastRail.OfferWaitingName || child.name == CastRail.AnswerWaitingName) continue;
                    seen.TryGetValue(child.name, out int count);
                    seen[child.name] = count + 1;
                    string key = path + "/" + child.name + "#" + count;
                    var bounds = LocalBounds(rail, (RectTransform)child);
                    layout[key] = new Rect(Mathf.Round(bounds.x * 100f) / 100f, Mathf.Round(bounds.y * 100f) / 100f,
                        Mathf.Round(bounds.width * 100f) / 100f, Mathf.Round(bounds.height * 100f) / 100f);
                    Walk(child, key);
                }
            }
            Walk(rail, "");
            return layout;
        }

        /// <summary>A rect a hundredth of a unit smaller all round, so two parts that only touch do not count as one lying on the other.</summary>
        private static Rect Inset(Rect rect) => Rect.MinMaxRect(rect.xMin + .01f, rect.yMin + .01f, rect.xMax - .01f, rect.yMax - .01f);
    }
}
