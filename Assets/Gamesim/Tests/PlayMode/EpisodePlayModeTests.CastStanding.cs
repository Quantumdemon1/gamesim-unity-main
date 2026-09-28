using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
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
    /// The cast strip says where the player stands with each houseguest.
    ///
    /// <para>A five-week ally and a stranger were the same grey chip. The standing was already
    /// computed - the relationship web and the conversation header both show it - and the one
    /// piece of chrome that is on screen in every frame never said it. These hold that it says it
    /// in words, from the player's own record only, beside the role rather than instead of it, and
    /// inside the chip's border at every scale the strip can be drawn at.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator CastRail_ChipsSayWhereThePlayerStandsAndOnlyWhereThereIsAStanding()
        {
            director.ClosePanels();
            yield return null;
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 2042);
            string you = state.playerId;
            var others = state.contestants.Where(actor => actor.id != you).ToArray();
            Assert.That(others.Length, Is.EqualTo(7));
            var allied = others[0]; var friendly = others[1]; var wary = others[2]; var hostile = others[3];
            var untouched = others[4]; var evicted = others[5]; var admirer = others[6];

            state.hohId = you;
            Reading(state, you, allied.id, 0);   // Allied by the alliance, not by the score.
            state.alliances.Add(new AllianceState { id = "probe", name = "Probe", members = new List<string> { you, allied.id }, active = true });
            state.nominees = new List<string> { allied.id };
            Reading(state, you, friendly.id, 20);
            Reading(state, you, wary.id, -20);
            Reading(state, you, hostile.id, -50);
            state.vetoHolderId = hostile.id;
            Reading(state, you, untouched.id, 0);
            evicted.status = ContestantStatus.Evicted;
            Reading(state, you, evicted.id, 80);
            // The admirer thinks the world of the player. The player does not know that.
            Reading(state, admirer.id, you, 80);
            Reading(state, you, admirer.id, 0);

            // Built and read in one frame: a HUD render destroys everything under this canvas.
            var rail = CastRail.Build(HudCanvas().transform, state, 1f, TMP_Settings.defaultFontAsset, _ => null, _ => { });
            try
            {
                Canvas.ForceUpdateCanvases();

                var player = state.Find(you);
                Assert.That(Roles(rail, player), Is.EqualTo(new[] { "HOH" }));
                Assert.That(Tags(rail, player), Is.Empty, "The player has no standing with themself.");

                Assert.That(Roles(rail, allied), Is.EqualTo(new[] { "NOM" }));
                Assert.That(Tags(rail, allied), Is.EqualTo(new[] { "Allied" }),
                    "An ally on the block is exactly when the player most needs to see the word. A role "
                    + "and a standing are two facts; the role must not hide the reading.");
                Assert.That(Tags(rail, friendly), Is.EqualTo(new[] { "Friendly" }));
                Assert.That(Tags(rail, wary), Is.EqualTo(new[] { "Wary" }));
                Assert.That(Roles(rail, hostile), Is.EqualTo(new[] { "VETO" }));
                Assert.That(Tags(rail, hostile), Is.EqualTo(new[] { "Hostile" }));
                Assert.That(Tags(rail, untouched), Is.Empty, "Neutral says nothing.");
                Assert.That(Tags(rail, evicted), Is.Empty, "Out of the house, so no tag - even on +80.");
                Assert.That(Tags(rail, admirer), Is.Empty,
                    "The admirer's +80 toward the player is theirs. The strip reads the player's own record.");

                var grounds = new Dictionary<string, RelationshipWeb.Kind>
                {
                    { "Allied", RelationshipWeb.Kind.Alliance }, { "Friendly", RelationshipWeb.Kind.Friendship },
                    { "Wary", RelationshipWeb.Kind.Distrust }, { "Hostile", RelationshipWeb.Kind.Rivalry },
                };
                foreach (RectTransform entry in rail)
                {
                    var tags = entry.Cast<Transform>().Where(child => child.name == CastRail.StandingTagName).ToArray();
                    Assert.That(tags.Length, Is.LessThanOrEqualTo(1), entry.name + " carries more than one standing.");
                    var mood = entry.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == CastRail.MoodWordName);
                    if (tags.Length == 0) continue;

                    var word = tags[0].GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == CastRail.StandingWordName);
                    Assert.That(word.gameObject.activeInHierarchy, Is.True);
                    Assert.That(SameColour(word.color, UiTheme.Paper), Is.True,
                        "A light word on a dark pill: the inverse of a role, so the two cannot be confused.");
                    word.ForceMeshUpdate();
                    Assert.That(word.isTextOverflowing, Is.False, entry.name + "'s '" + word.text + "' is clipped.");
                    Assert.That(SameColour(tags[0].GetComponent<Image>().color, CastRail.StandingGround(grounds[word.text])), Is.True,
                        entry.name + "'s tag is not on its standing's ground.");
                    Assert.That(mood.text, Does.Not.Contain(word.text),
                        "The mood word is the houseguest's mood. The player's standing is not folded into it.");
                }
            }
            finally
            {
                Object.DestroyImmediate(rail.gameObject);
            }
        }

        /// <summary>
        /// The overlap test that isTextOverflowing cannot be: a role pill and a standing tag share
        /// the ring's foot at every scale the strip draws at, apart from each other, clear of the
        /// name below, and inside the chip's BORDER - which is drawn on the glass's own rect, so
        /// "inside the glass" would pass a pill lying across the cyan that marks the followed chip.
        /// </summary>
        [UnityTest]
        public IEnumerator CastRail_ARoleAndAStandingShareTheRingFootInsideTheBorderAtEveryScale()
        {
            director.ClosePanels();
            yield return null;
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 2042);
            string you = state.playerId;
            var others = state.contestants.Where(actor => actor.id != you).ToArray();
            Assert.That(others.Length, Is.EqualTo(11));
            // Every pair the strip can draw, with the widest - VETO beside Friendly - among them.
            state.vetoHolderId = others[0].id; Reading(state, you, others[0].id, 20);
            state.nominees = new List<string> { others[1].id, others[2].id, others[3].id };
            Reading(state, you, others[1].id, 20);
            Reading(state, you, others[2].id, -50);
            state.hohId = others[4].id; Reading(state, you, others[4].id, -20);
            state.alliances.Add(new AllianceState { id = "probe", name = "Probe", members = new List<string> { you, others[3].id, others[5].id }, active = true });
            Reading(state, you, others[6].id, 20);
            Reading(state, you, others[7].id, -20);
            Reading(state, you, others[8].id, -50);
            Reading(state, you, others[9].id, 0);
            Reading(state, you, others[10].id, 0);

            var probe = new GameObject("Standing probe", typeof(RectTransform)).GetComponent<RectTransform>();
            probe.SetParent(HudCanvas().transform, false);
            probe.anchorMin = Vector2.zero; probe.anchorMax = Vector2.zero; probe.pivot = Vector2.zero;
            bool sawFloor = false, sawStep = false, sawFull = false;
            int pairs = 0;
            try
            {
                // No yield anywhere in the sweep: a HUD render between widths destroys the probe.
                for (float width = 820f; width <= 1350f; width += 11f)
                {
                    probe.sizeDelta = new Vector2(width, 200f);
                    var rail = CastRail.Build(probe, state, 1.2f, TMP_Settings.defaultFontAsset, _ => null);
                    try
                    {
                        Canvas.ForceUpdateCanvases();
                        float scale = rail.sizeDelta.y / CastRail.Height;
                        if (Mathf.Abs(scale - .72f) < .001f) sawFloor = true;
                        if (scale >= .75f && scale <= .77f) sawStep = true;
                        if (Mathf.Abs(scale - 1.2f) < .001f) sawFull = true;

                        foreach (RectTransform entry in rail)
                        {
                            var tag = Child(entry, CastRail.StandingTagName) as RectTransform;
                            if (tag == null) continue;
                            var role = Child(entry, CastRail.BadgeName) as RectTransform;
                            // Clear of the border by a unit, not merely off it: a pill that
                            // touches the edge reads as part of it, and the edge is the cyan that
                            // says the camera is on this chip. The first version of this test only
                            // checked the border itself, and the one branch that keeps the widest
                            // pair a unit clear of it survived being deleted.
                            var glass = LocalBounds(entry, (RectTransform)Child(entry, CastRail.ChipName));
                            float clearance = UiTheme.BorderThickness + 1f;
                            float left = glass.xMin + clearance - .01f;
                            float right = glass.xMax - clearance + .01f;
                            string at = entry.name + " at scale " + scale.ToString("F3");

                            var tagBox = LocalBounds(entry, tag);
                            Assert.That(tagBox.xMin, Is.GreaterThanOrEqualTo(left), at + ": the standing tag touches the chip's left border.");
                            Assert.That(tagBox.xMax, Is.LessThanOrEqualTo(right), at + ": the standing tag touches the chip's right border.");

                            var name = entry.Cast<Transform>().Select(child => child.GetComponent<TMP_Text>())
                                .First(label => label != null && label.name == "Text");
                            Assert.That(tagBox.yMin, Is.GreaterThanOrEqualTo(LocalBounds(entry, name.rectTransform).yMax - .01f),
                                at + ": the standing tag sits on the name.");
                            AssertFits(tag, at);

                            if (role == null) continue;
                            pairs++;
                            var roleBox = LocalBounds(entry, role);
                            Assert.That(roleBox.xMin, Is.GreaterThanOrEqualTo(left), at + ": the role pill touches the chip's left border.");
                            Assert.That(roleBox.xMax, Is.LessThanOrEqualTo(tagBox.xMin + .01f), at + ": the role pill and the standing tag overlap.");
                            AssertFits(role, at);
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(rail.gameObject);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(probe.gameObject);
            }

            Assert.That(pairs, Is.GreaterThan(0), "The sweep never drew a role and a standing together.");
            Assert.That(sawFloor, Is.True, "The sweep never reached the strip's smallest scale.");
            Assert.That(sawStep, Is.True,
                "The sweep never visited the scales around three quarters, where the font rounds up and "
                + "the widest pair only fits one point smaller. A sweep that misses them proves nothing there.");
            Assert.That(sawFull, Is.True, "The sweep never reached the larger text size.");
        }

        /// <summary>
        /// The live path: work on a houseguest through the conversation controls, and their chip
        /// says so. And before any of that, the strip says nothing the player does not know - the
        /// scenario seeds a friendship BETWEEN two houseguests, and neither chip may show it.
        /// </summary>
        [UnityTest]
        public IEnumerator CastRail_AHouseguestYouHaveWorkedOnSaysSoOnTheStrip()
        {
            director.ClosePanels();
            yield return null;
            var loaded = director.Snapshot;
            foreach (var actor in loaded.contestants)
                Assert.That(Tags(LiveRail(), actor).Length, Is.EqualTo(CastRail.StandingOf(loaded, actor) == RelationshipWeb.Kind.Neutral ? 0 : 1),
                    actor.name + "'s chip does not match the player's own reading of them.");

            string mayaId = ContentCatalog.MayaId;
            var jamie = loaded.contestants.FirstOrDefault(actor => actor.name != null && actor.name.StartsWith("Jamie"));
            Assert.That(jamie, Is.Not.Null, "The scenario should hold Jamie.");
            Assert.That(System.Math.Max(loaded.Score(mayaId, jamie.id), loaded.Score(jamie.id, mayaId)),
                Is.GreaterThanOrEqualTo(RelationshipWeb.FriendThreshold),
                "The boundary check below needs the scenario's friendship between two houseguests.");
            Assert.That(RelationshipWeb.KindOf(loaded, mayaId), Is.EqualTo(RelationshipWeb.Kind.Neutral));
            Assert.That(RelationshipWeb.KindOf(loaded, jamie.id), Is.EqualTo(RelationshipWeb.Kind.Neutral));
            Assert.That(Tags(LiveRail(), loaded.Find(mayaId)), Is.Empty,
                "Maya and Jamie are friends with EACH OTHER. That is not the player's reading of either.");
            Assert.That(Tags(LiveRail(), jamie), Is.Empty);

            // The recorded walk to an alliance with Maya: three conversations and a proposal, every
            // one pressed through the panel and checked to have committed.
            var initial = ContentCatalog.Create(4);
            initial.socialBudgetRulesStartWeek = 2;
            Assert.That(EpisodeValidation.TryValidate(initial, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(initial);
            yield return ReloadEpisode();
            var maya = SceneComponents<HouseNpc>().Single(npc => npc.Id == mayaId);
            yield return OpenNearbyNpc(maya);
            for (int talk = 0; talk < 3; talk++)
                yield return ClickBlocCommand("Spend time together");
            yield return ClickBlocCommand("Propose an alliance");
            Assert.That(director.Snapshot.Allied(initial.playerId, mayaId), Is.True);

            director.ClosePanels();
            yield return WaitForBodies();
            Canvas.ForceUpdateCanvases();
            var mayaState = director.Snapshot.Find(mayaId);
            Assert.That(Tags(LiveRail(), mayaState), Is.EqualTo(new[] { "Allied" }),
                "Maya is the player's ally now, and her chip on the strip has to say so.");

            if (!Application.isBatchMode) yield break;
            yield return WaitForRailFaces("cast-strip-standing");
            yield return CaptureFraming("cast-strip-standing");
            yield return ApplyTextSize(true);
            yield return WaitForRailFaces("cast-strip-standing-large");
            yield return CaptureFraming("cast-strip-standing-large");
            yield return ApplyTextSize(false);
            yield return CaptureStandingCombos();
        }

        /// <summary>
        /// Every pair on real faces, on a canvas of its own so a HUD render cannot take it away
        /// mid-capture. Review evidence, not proof; the sweep above is the proof.
        /// </summary>
        private IEnumerator CaptureStandingCombos()
        {
            var combos = director.Snapshot.Clone();
            string you = combos.playerId;
            var others = combos.contestants.Where(actor => actor.id != you && actor.status == ContestantStatus.Active).ToArray();
            Assert.That(others.Length, Is.GreaterThanOrEqualTo(5), "The scenario's house is the player and five others.");
            combos.alliances.Clear();
            combos.vetoHolderId = others[0].id; Reading(combos, you, others[0].id, 20);
            combos.nominees = new List<string> { others[1].id, others[2].id };
            combos.alliances.Add(new AllianceState { id = "combos", name = "Combos", members = new List<string> { you, others[1].id }, active = true });
            Reading(combos, you, others[2].id, -50);
            combos.hohId = others[3].id; Reading(combos, you, others[3].id, -20);
            Reading(combos, you, others[4].id, 20);
            for (int index = 5; index < others.Length; index++) Reading(combos, you, others[index].id, 0);

            var holder = new GameObject("Standing combos", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var canvas = holder.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 900;
                var hudScaler = HudCanvas().GetComponent<CanvasScaler>();
                var scaler = holder.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = hudScaler.uiScaleMode;
                scaler.referenceResolution = hudScaler.referenceResolution;
                scaler.screenMatchMode = hudScaler.screenMatchMode;
                scaler.matchWidthOrHeight = hudScaler.matchWidthOrHeight;
                var backdrop = HudPrimitives.Fill("Backdrop", holder.transform, UiTheme.Background, 1);
                backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = new Vector2(1f, 0f);
                backdrop.pivot = new Vector2(.5f, 0f);
                backdrop.sizeDelta = new Vector2(0f, CastRail.Bottom + CastRail.Height + 20f);
                yield return null;
                Canvas.ForceUpdateCanvases();
                CastRail.Build(holder.transform, combos, 1f, TMP_Settings.defaultFontAsset,
                    id => CharacterPortraits.Get(combos.Find(id)), followedId: others[1].id);
                yield return CaptureFraming("cast-strip-standing-combos");
            }
            finally
            {
                Object.Destroy(holder);
            }
            yield return null;
        }

        private Canvas HudCanvas() => director.GetComponentsInChildren<Canvas>().Single(item => item.name == "Gamesim Episode HUD");

        private RectTransform LiveRail() => director.GetComponentsInChildren<RectTransform>(true)
            .Single(rect => rect.name == CastRail.RootName && rect.gameObject.activeInHierarchy);

        private static RectTransform RailEntry(RectTransform rail, ContestantState actor) =>
            rail.Cast<Transform>().Select(child => (RectTransform)child).Single(entry => entry.name == actor.name);

        private static string[] Roles(RectTransform rail, ContestantState actor) =>
            RailEntry(rail, actor).Cast<Transform>().Where(child => child.name == CastRail.BadgeName)
                .Select(badge => badge.GetComponentInChildren<TMP_Text>(true).text).ToArray();

        private static string[] Tags(RectTransform rail, ContestantState actor) =>
            RailEntry(rail, actor).Cast<Transform>().Where(child => child.name == CastRail.StandingTagName)
                .Select(tag => tag.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == CastRail.StandingWordName).text)
                .ToArray();

        private static void Reading(EpisodeState state, string from, string to, double score)
        {
            var record = state.relationships.FirstOrDefault(item => item.fromId == from && item.toId == to);
            if (record == null) state.relationships.Add(new RelationshipState { fromId = from, toId = to, score = score });
            else record.score = score;
        }

        /// <summary>A rect's bounds in <paramref name="space"/>'s local units, whatever the canvas scale.</summary>
        private static Rect LocalBounds(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = space.InverseTransformPoint(corners[0]), max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertFits(RectTransform pill, string at)
        {
            var label = pill.GetComponentInChildren<TMP_Text>(true);
            label.ForceMeshUpdate();
            Assert.That(label.isTextOverflowing, Is.False, at + ": '" + label.text + "' is clipped in its pill.");
        }

        private IEnumerator WaitForBodies()
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            // Only bodies in the house: one on an inactive actor never finishes.
            while (SceneComponents<CharacterPresentation>().Any(body => body.gameObject.activeInHierarchy && body.IsBodyAssembling)
                   && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }

        /// <summary>
        /// The rail asks for each face once, when it is built, so a portrait that lands later only
        /// reaches a chip on the next HUD render. Render until every active chip has its face.
        /// </summary>
        private IEnumerator WaitForRailFaces(string frame)
        {
            int Faces() => LiveRail().Cast<Transform>()
                .Count(entry => entry.GetComponentsInChildren<RawImage>(true).Any(image => image.name == "Face" && image.texture != null));
            int wanted = director.Snapshot.contestants.Count;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Faces() < wanted && Time.realtimeSinceStartup < deadline)
            {
                director.ClosePanels();
                for (int wait = 0; wait < 10; wait++) yield return null;
            }
            Debug.Log("[Gamesim] Cast strip (" + frame + ") - " + Faces() + " of " + wanted + " chips show a face before capture.");
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
    }
}
