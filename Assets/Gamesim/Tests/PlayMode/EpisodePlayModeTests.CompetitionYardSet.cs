using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// What stands in the yard when (competition yard, slice 2). In everyday life the competition's
    /// equipment - the neon stacks and their crates, the lanes and their markings, the gates, the
    /// lettering, the podiums - is put away: not drawn, and with no collider in anyone's way. The
    /// television set's dressing stays: the backdrop with the public display on its face, the entrance,
    /// the light towers, the studio camera, the road cases. The course is deployed as a competition's
    /// set from the moment the player starts the ranked game or practice, and for a staged show (the
    /// CompetitionShow_ cases), and put away when that presentation ends: a cancel, a load, the card.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The yard's listed pieces as authored: each outermost node of a <see cref="CompetitionYardPieces"/> name standing in the yard.</summary>
        private static List<Transform> YardSetPieces(out Bounds deck)
        {
            var floor = SceneComponents<BoxCollider>().Single(collider => collider.name == "Competition yard floor");
            deck = floor.bounds;
            var yard = deck;
            var house = floor.transform.root;
            Assert.That(house.name, Is.EqualTo(CompetitionYardDressing.HouseRoot));
            return house.GetComponentsInChildren<Transform>(true)
                .Where(node => node != floor.transform && CompetitionYardPieces.Find(node.name) != null)
                .Where(node => { for (var at = node.parent; at != null; at = at.parent) if (CompetitionYardPieces.Find(at.name) != null) return false; return true; })
                .Where(node => node.position.x >= yard.min.x - 1f && node.position.x <= yard.max.x + 1f
                    && node.position.z >= yard.min.z - 1f && node.position.z <= yard.max.z + 1f)
                .ToList();
        }

        private static bool Drawn(Renderer renderer) => renderer.enabled && renderer.gameObject.activeInHierarchy;

        /// <summary>
        /// Everyday life: every piece put away is there, switched off, drawing nothing and holding no live
        /// collider; every piece that stays draws; a houseguest's capsule standing where a put-away piece
        /// stands meets nothing of it.
        /// </summary>
        private void AssertEverydayYard(string when)
        {
            Assert.That(director.CompetitionYardDeployed, Is.False, "No competition's set stands " + when + ".");
            Assert.That(director.CompetitionYardKind, Is.Null);
            Assert.That(director.CompetitionYardPutAway, Is.Empty, "Nothing is struck from a field " + when + ".");
            var pieces = YardSetPieces(out var deck);
            var stowed = new HashSet<GameObject>(director.CompetitionYardStowed);
            var template = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .Select(npc => npc.GetComponent<CapsuleCollider>()).FirstOrDefault(capsule => capsule != null && capsule.enabled);
            float radius = template != null ? template.radius : .3f, height = template != null ? template.height : 1.8f;
            foreach (var listed in CompetitionYardPieces.All)
            {
                var nodes = pieces.Where(node => CompetitionYardPieces.Stem(node.name) == listed.Name).ToList();
                Assert.That(nodes.Count, Is.GreaterThanOrEqualTo(listed.Count), "The yard holds every " + listed.Name + " " + when + ".");
                foreach (var node in nodes)
                {
                    if (listed.State == CompetitionYardPieces.Everyday.Stays)
                    {
                        Assert.That(node.gameObject.activeInHierarchy, Is.True, node.name + " stays " + when + ": " + listed.Reason);
                        continue;
                    }
                    Assert.That(node.gameObject.activeInHierarchy, Is.False, node.name + " is put away " + when + ".");
                    Assert.That(stowed.Contains(node.gameObject), Is.True, node.name + " is put away by the yard's one owner.");
                    Assert.That(node.GetComponentsInChildren<Renderer>(true).Where(Drawn), Is.Empty, node.name + " draws nothing " + when + ".");
                    Assert.That(node.GetComponentsInChildren<Collider>(true).Where(collider => collider.enabled && collider.gameObject.activeInHierarchy),
                        Is.Empty, node.name + " holds no live collider " + when + ".");
                    var feet = new Vector3(node.position.x, deck.max.y, node.position.z);
                    var hits = Physics.OverlapCapsule(feet + Vector3.up * radius, feet + Vector3.up * (height - radius), radius,
                        Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    Assert.That(hits.Where(hit => hit.transform.IsChildOf(node)), Is.Empty, "A body standing at " + node.name + " meets nothing of it " + when + ".");
                }
            }
            foreach (var name in new[] { "bb_set_comp_backdrop", HousePublicDisplays.YardName, "bb_set_entrancearch", "bb_set_lighttower", "bb_set_studiocam" })
                Assert.That(pieces.Where(node => CompetitionYardPieces.Stem(node.name) == name).SelectMany(node => node.GetComponentsInChildren<Renderer>(true)).Any(Drawn),
                    Is.True, name + " is drawn " + when + ".");
        }

        /// <summary>A competition's set: every piece put away in everyday life stands again, the lanes and gates drawn.</summary>
        private void AssertDeployedYard(string kind, string when)
        {
            Assert.That(director.CompetitionYardDeployed, Is.True, "The course is deployed " + when + ".");
            Assert.That(director.CompetitionYardKind, Is.EqualTo(kind));
            Assert.That(director.CompetitionYardStowed, Is.Empty, "Nothing stays put away " + when + ".");
            var pieces = YardSetPieces(out _);
            foreach (var node in pieces.Where(node => CompetitionYardPieces.PutAwayInEveryday(node.name)))
                Assert.That(node.gameObject.activeInHierarchy, Is.True, node.name + " stands as part of the set " + when + ".");
            foreach (var name in new[] { "bb_set_comp_lane", "bb_set_comp_gate" })
                Assert.That(pieces.Where(node => CompetitionYardPieces.Stem(node.name) == name)
                    .Count(node => node.GetComponentsInChildren<Renderer>(true).Any(Drawn)), Is.EqualTo(3), "Every " + name + " is drawn " + when + ".");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CompetitionYard_InEverydayLifeTheEquipmentIsPutAwayAndInNobodysWay()
        {
            director.ClosePanels();
            yield return Frames(3);
            AssertEverydayYard("in free time");
            // The player's own body never stands in a put-away piece either: the stacks' and podiums'
            // floor is a hole in the baked NavMesh, which the agent cannot be placed on or routed into.
            var pieces = YardSetPieces(out var deck);
            foreach (var node in pieces.Where(node => CompetitionYardPieces.PutAwayInEveryday(node.name)))
            {
                var feet = new Vector3(node.position.x, deck.max.y, node.position.z);
                if (UnityEngine.AI.NavMesh.SamplePosition(feet, out var hit, 2f, player.Agent.areaMask))
                {
                    var hits = Physics.OverlapSphere(hit.position + Vector3.up * .5f, player.Agent.radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    Assert.That(hits.Where(collider => collider.transform.IsChildOf(node)), Is.Empty,
                        "The nearest walkable floor to " + node.name + " holds nothing of it.");
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CompetitionYard_TheBriefingShowsTheEverydayYardWithItsBackdropAndDisplayInFrame()
        {
            yield return SettleCast();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(EpisodeEngine.IsCompetition(director.Snapshot.phase), Is.True);
            AssertEverydayYard("under the briefing");
            Assert.That(director.IsFramingBriefing, Is.True, "The briefing frames the yard.");
            yield return Settle(() => !cameraRig.IsTravelling && cameraRig.HasArrived(0.05f), 4f);
            // The frame the sheet leaves holds the backdrop and the house's public display on its face:
            // the yard where the competition will stand, with the week and its office holders on the wall.
            var sheet = ScreenRect(ActiveRect("Episode panel"));
            var display = SceneComponents<Renderer>().Single(renderer => renderer.name == HousePublicDisplays.YardName);
            Assert.That(Drawn(display), Is.True);
            var at = cameraRig.ViewCamera.WorldToScreenPoint(display.bounds.center);
            Assert.That(at.z, Is.GreaterThan(0f), "The display is in front of the briefing's lens.");
            Assert.That(at.x, Is.GreaterThan(sheet.xMax).And.LessThan(Screen.width), "The display stands right of the sheet, in the frame: " + at + ".");
            Assert.That(at.y, Is.GreaterThan(0f).And.LessThan(Screen.height), "and within its height: " + at + ".");
            director.ClosePanels();
            yield return null;
            AssertEverydayYard("after the briefing closes");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CompetitionYard_PracticeDeploysTheSetAndACancelPutsItAway()
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith("Skill"));
            AssertEverydayYard("at the briefing");
            var before = director.Snapshot;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            AssertDeployedYard("Skill", "from the moment practice starts");
            yield return null;
            yield return PressKey(Key.Escape);
            yield return null; yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty, "The practice is over.");
            AssertEverydayYard("after the practice is cancelled");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState), "The yard draws nothing from the season.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CompetitionYard_TheRankedGameDeploysTheSetAndACancelPutsItAway()
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith("Endurance"));
            var before = director.Snapshot;
            ButtonWithCaption(CompetitionMiniGames.EnterCaption(CompetitionMiniGames.For("Endurance"))).onClick.Invoke();
            AssertDeployedYard("Endurance", "from the moment the ranked game starts");
            Assert.That(director.CompetitionYardPutAway, Is.Not.Empty, "The set's pieces in the station field are struck, as in slice 1.");
            yield return null;
            yield return PressKey(Key.Escape);
            yield return null; yield return null;
            AssertEverydayYard("after the ranked attempt is cancelled");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A cancelled attempt commits nothing.");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CompetitionYard_ALoadDuringPracticeOpensOnTheEverydayYard()
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWith("Endurance"));
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            AssertDeployedYard("Endurance", "while practising");
            yield return null;
            director.LoadNow();
            yield return null; yield return null;
            Assert.That(SceneComponents<CompetitionApparatus>(), Is.Empty, "The load ends the practice.");
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.HoH), "The saved season came back.");
            AssertEverydayYard("after the load");
        }
    }
}
