using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The house's small signs of use, after the web game: bubbles in the hot tub, steam off the
    /// hob while the player cooks, splashes round the player while they swim - and none of it
    /// under reduced motion.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static ParticleSystem.Particle[] Live(ParticleSystem system)
        {
            var particles = new ParticleSystem.Particle[system.main.maxParticles];
            int count = system.GetParticles(particles);
            return particles.Take(count).ToArray();
        }

        private static IEnumerator ForSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static float Across(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [UnityTest]
        public IEnumerator Effects_TheHotTubBubblesUnlessMotionIsReduced()
        {
            director.ClosePanels();
            cameraRig.SetReducedMotion(false);
            yield return ForSeconds(1f);
            var effects = director.ActivityEffects;
            Assert.That(effects, Is.Not.Null, "The house's effects were built.");
            Assert.That(effects.Bubbles, Is.Not.Null, "The house has a hot tub, so it bubbles.");
            var tub = PlacesFor(HouseFurnitureActivity.Soak)[0].transform.parent;
            float water = tub.position.y + HouseActivityAnchors.TubWater;
            var bubbles = Live(effects.Bubbles);
            Assert.That(bubbles.Length, Is.GreaterThan(5), "Bubbles rise, whoever is in the tub.");
            foreach (var bubble in bubbles)
            {
                Assert.That(Across(bubble.position, tub.position), Is.LessThan(HouseActivityAnchors.TubWaterRadius), "Inside the well.");
                Assert.That(bubble.position.y, Is.InRange(water - .01f, water + .5f), "Rising off the water, not sinking or flying.");
                Assert.That(bubble.velocity.y, Is.GreaterThan(0f), "Up.");
            }
            cameraRig.SetReducedMotion(true);
            yield return null;
            yield return null;
            Assert.That(effects.Bubbles.particleCount, Is.Zero, "Under reduced motion, none.");
            Assert.That(effects.Bubbles.isEmitting, Is.False);
        }

        [UnityTest]
        public IEnumerator Effects_SteamRisesOffTheHobWhileThePlayerCooks()
        {
            var stove = PlacesFor(HouseFurnitureActivity.Cook).Single();
            cameraRig.SetReducedMotion(false);
            var effects = director.ActivityEffects;
            Assert.That(effects != null && effects.Steam != null, Is.True, "The kitchen run has a hob to steam.");
            Assert.That(effects.Steam.isEmitting, Is.False, "Nothing on the hob, no steam.");

            yield return BeginInHouse(stove, HouseFurnitureActivity.Cook);
            yield return ForSeconds(.8f);
            Assert.That(effects.Steam.isEmitting, Is.True, "Cooking, the hob steams.");
            var hob = HouseActivityAnchors.Hob(stove.transform.parent);
            // The hob is the one the cook stands at: the counter's depth in front of them, not the
            // far end of the run.
            Assert.That(Across(hob, stove.Position), Is.InRange(1f, 1.35f), "The hob is in front of the cook.");
            var motes = Live(effects.Steam);
            Assert.That(motes.Length, Is.GreaterThan(0));
            foreach (var mote in motes)
            {
                Assert.That(Across(mote.position, hob), Is.LessThan(.6f), "Over the hob.");
                Assert.That(mote.position.y, Is.InRange(hob.y, hob.y + 2f), "Above it.");
                Assert.That(mote.velocity.y, Is.GreaterThan(0f), "Rising.");
            }
            if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-steam"); }

            director.FinishPlayerHouseActivity();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && director.IsPlayerHouseActivityActive) yield return null;
            yield return null;
            Assert.That(effects.Steam.isEmitting, Is.False, "Off the hob, the steam stops.");
        }

        [UnityTest]
        public IEnumerator Effects_ASwimmerSplashes()
        {
            var pool = PlacesFor(HouseFurnitureActivity.Swim).Single();
            cameraRig.SetReducedMotion(false);
            var effects = director.ActivityEffects;
            Assert.That(effects != null && effects.Splash != null, Is.True, "The house has a pool.");
            Assert.That(effects.Splash.isEmitting, Is.False, "An empty pool is still.");

            yield return BeginInHouse(pool, HouseFurnitureActivity.Swim);
            var seat = player.GetComponent<HouseSeatPresentation>();
            // Out along a length, well away from the middle, so splashes left at the middle of the
            // pool read as the mistake they would be.
            float away = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < away && Across(seat.VisualFeet, pool.Position) < 1.6f) yield return null;
            Assert.That(Across(seat.VisualFeet, pool.Position), Is.GreaterThan(1.6f), "The swimmer does a length.");
            yield return ForSeconds(.6f);
            Assert.That(effects.Splash.isEmitting, Is.True, "Swimming, the water splashes.");
            var drops = Live(effects.Splash);
            Assert.That(drops.Length, Is.GreaterThan(0));
            foreach (var drop in drops)
            {
                // Where the swimmer is now, give or take the stroke they have made since.
                Assert.That(Across(drop.position, seat.VisualFeet), Is.LessThan(1.2f), "Round the swimmer.");
                Assert.That(drop.position.y, Is.InRange(effects.PoolWaterLine - .6f, effects.PoolWaterLine + .6f), "At the water line.");
            }
            if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-splash"); }

            director.FinishPlayerHouseActivity();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && director.IsPlayerHouseActivityActive) yield return null;
            yield return null;
            Assert.That(effects.Splash.isEmitting, Is.False, "Out of the water, no more splashes.");
        }
    }
}
