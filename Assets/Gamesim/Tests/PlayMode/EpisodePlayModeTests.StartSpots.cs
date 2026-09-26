using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Where the season starts its people. Body slot 1 was authored on the competition course's
    /// centre lane, heels in the stacking prop's plinth and head in the prototype's pendant, so no
    /// angle at the introductions could frame the face. The pendant comes down when the house opens,
    /// the slot starts beside the lane instead, and an audit fails - naming the spot and the prop -
    /// the next time any start spot is spoiled by a small prop or a piece of furniture.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly Vector3 CourseAnchorForTests = new Vector3(0f, 0f, 15f);
        private static readonly Vector3 CourseAnchorMovedToForTests = new Vector3(1.1f, 0f, 14.8f);

        /// <summary>
        /// Every place the season starts somebody - each houseguest's slot and the player - is clear
        /// of the small props the introductions' framing counts and of the furniture layer: no face
        /// sample inside a prop, no prop across the body's column, no furniture collider in the capsule.
        /// </summary>
        [UnityTest]
        public IEnumerator StartSpots_NoPropOrFurnitureSpoilsWhereTheSeasonStartsAnybody()
        {
            yield return null;
            var scene = director.gameObject.scene;
            var spots = director.StartSpots;
            Assert.That(spots, Is.Not.Empty, "The season seats its cast somewhere.");
            var cast = director.Snapshot.contestants.Where(actor => !actor.isPlayer).ToList();

            float radius = 0.35f, height = 1.9f;
            foreach (var npc in SceneComponents<HouseNpc>())
            {
                var capsule = npc.GetComponent<CapsuleCollider>();
                if (capsule == null) continue;
                radius = Mathf.Max(radius, capsule.radius);
                height = Mathf.Max(height, capsule.height);
            }

            var problems = new List<string>();
            for (int i = 0; i < spots.Count; i++)
            {
                var spoilers = StartSpotAudit.Spoilers(scene, spots[i], radius, height);
                if (spoilers.Count == 0) continue;
                string who = i < cast.Count ? cast[i].name : "an unnamed body";
                problems.Add(who + "'s start spot (body slot " + i + ") at " + spots[i].ToString("F2") + ": " + string.Join("; ", spoilers));
            }

            var own = player.GetComponent<CapsuleCollider>();
            var playerSpoilers = StartSpotAudit.Spoilers(scene, director.PlayerStartSpot,
                own != null ? own.radius : 0.35f, own != null ? own.height : 1.8f);
            if (playerSpoilers.Count > 0)
                problems.Add("The player's start spot at " + director.PlayerStartSpot.ToString("F2") + ": " + string.Join("; ", playerSpoilers));

            Assert.That(problems, Is.Empty, "Spoiled start spots:\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// The prototype's pendant over the competition course is gone once the house is open: no
        /// object of its name anywhere in the scene, and nothing hanging at head height over the
        /// anchor it hung above.
        /// </summary>
        [UnityTest]
        public IEnumerator StartSpots_TheCompetitionLampIsTakenDownWhenTheHouseOpens()
        {
            yield return null;
            var lamps = SceneComponents<Transform>().Where(node => node.name == "Lamp - Competition")
                .Select(node => StartSpotAudit.PathOf(node)).ToArray();
            Assert.That(lamps, Is.Empty, "The competition pendant is taken down: " + string.Join(", ", lamps));

            var overhead = SceneComponents<Renderer>()
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy
                    && Flat(renderer.bounds.center, CourseAnchorForTests) < 0.3f
                    && renderer.bounds.min.y > 1.2f && renderer.bounds.min.y < 2.4f
                    && renderer.GetComponentInParent<HouseNpc>() == null && renderer.GetComponentInParent<HousePlayerController>() == null
                    && renderer.GetComponentInParent<Canvas>() == null)
                .Select(renderer => StartSpotAudit.PathOf(renderer.transform)).ToArray();
            Assert.That(overhead, Is.Empty, "Nothing hangs at head height over the course's centre lane: " + string.Join(", ", overhead));
        }

        /// <summary>
        /// The override moves exactly the slot the scene authored on the course - body slot 1 - to the
        /// yard spot beside the lane, on the same floor, puts its body there, and leaves every other
        /// slot on the spot the scene authored for it.
        /// </summary>
        [UnityTest]
        public IEnumerator StartSpots_TheCourseAnchorMovesSlotOneAndNobodyElse()
        {
            yield return null;
            var authored = director.AuthoredStartSpots;
            var start = director.StartSpots;
            Assert.That(start.Count, Is.EqualTo(authored.Count), "One authored anchor for every start spot.");

            var onCourse = Enumerable.Range(0, authored.Count).Where(i => Flat(authored[i], CourseAnchorForTests) <= 0.3f).ToArray();
            Assert.That(onCourse, Is.EqualTo(new[] { 1 }), "The saved scene authors body slot 1, and only slot 1, on the competition course.");

            Assert.That(Flat(start[1], CourseAnchorMovedToForTests), Is.LessThan(0.26f),
                "Slot 1 starts in the open yard beside the lane, not at " + start[1].ToString("F2") + ".");
            Assert.That(Mathf.Abs(start[1].y - authored[1].y), Is.LessThan(0.2f), "on the floor the anchor was authored on.");
            for (int i = 0; i < authored.Count; i++)
                if (i != 1) Assert.That(start[i], Is.EqualTo(authored[i]), "Body slot " + i + " keeps the spot the scene authored for it.");

            var second = director.Snapshot.contestants.Where(actor => !actor.isPlayer).ElementAt(1);
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == second.id);
            Assert.That(Flat(body.transform.position, start[1]), Is.LessThan(0.6f),
                second.name + " was put on the moved spot when the house opened, not only told about it.");
        }
    }
}
