using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// The small signs of the house's things being used: bubbles always rising in the hot tub,
    /// steam off the hob while the player cooks, and splashes round the player while they swim.
    ///
    /// <para>The web game's effects, carried over (<c>EnvironmentalParticles.tsx</c>'s steam and
    /// splash, <c>BackyardArea.tsx</c>'s hot-tub bubbles): the same counts, and the colours, sizes
    /// and opacities but for the splash's, with the motion put in this house's metres and seconds.
    /// Its fireflies wait for a time of day the house does not have.</para>
    ///
    /// <para>Motion is the decoration: under reduced motion none of it plays. Presentation only - the
    /// particles draw their own randomness, never the season's.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseActivityEffects : MonoBehaviour
    {
        public const string RootName = "House activity effects";
        public const string BubblesName = "Hot tub bubbles", SteamName = "Hob steam", SplashName = "Swimmer splash";

        /// <summary>The effects, when the house has the piece each belongs to; null otherwise.</summary>
        public ParticleSystem Bubbles { get; private set; }
        public ParticleSystem Steam { get; private set; }
        public ParticleSystem Splash { get; private set; }

        /// <summary>The pool's water line, where a swimmer's splashes start.</summary>
        public float PoolWaterLine { get; private set; }

        private static Material material;

        /// <summary>Builds the effects for the set pieces the scene holds, once, under the host.</summary>
        public static HouseActivityEffects Attach(Scene scene, Transform host)
        {
            var existing = host.GetComponentInChildren<HouseActivityEffects>(true);
            if (existing != null) return existing;
            var root = new GameObject(RootName);
            root.transform.SetParent(host, false);
            var effects = root.AddComponent<HouseActivityEffects>();
            var pieces = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true))
                .Where(t => t.gameObject.activeInHierarchy).ToArray();
            var tub = pieces.FirstOrDefault(t => t.name == HouseActivityAnchors.TubPiece);
            var pool = pieces.FirstOrDefault(t => t.name == HouseActivityAnchors.PoolPiece);
            var run = pieces.FirstOrDefault(t => t.name == HouseActivityAnchors.KitchenRunPiece);

            // BackyardArea.tsx: fifty white points, 0.05 across at 0.6 opacity, rising 0.3 through
            // the water and starting again. Here they rise off the surface, inside the well.
            if (tub != null)
            {
                var bubbles = Make(BubblesName, root.transform, tub.position + Vector3.up * HouseActivityAnchors.TubWater,
                    new Color(1f, 1f, 1f, .6f), .05f, 50, .5f, .3f, .6f, 0f);
                Upward(bubbles, HouseActivityAnchors.TubWaterRadius * .85f, 0f);
                bubbles.GetComponent<ParticleSystemRenderer>().sortingOrder = 1;
                effects.Bubbles = bubbles;
            }

            // EnvironmentalParticles.tsx: twelve pale motes, 0.08 across at 0.35 opacity, climbing
            // 1.5 m off a 0.3 m patch over the cooking and drifting as they go.
            if (run != null)
            {
                var steam = Make(SteamName, root.transform, HouseActivityAnchors.Hob(run) + Vector3.up * .05f,
                    new Color(.91f, .91f, .91f, .35f), .08f, 12, 1.25f, .6f, 1.2f, 0f);
                // A flat box turned to face up: it emits along its own forward, now straight up.
                var shape = steam.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                shape.scale = new Vector3(.3f, .3f, .02f);
                var drift = steam.velocityOverLifetime;
                drift.enabled = true;
                drift.space = ParticleSystemSimulationSpace.World;
                drift.x = new ParticleSystem.MinMaxCurve(-.15f, .15f);
                drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                drift.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
                effects.Steam = steam;
            }

            // EnvironmentalParticles.tsx: eight drops, 0.06 across at 0.5 opacity, thrown up from
            // half a metre round the swimmer and falling back. Paler and a little larger than the
            // web's #60a5fa, which vanishes against this pool's lit blue water.
            if (pool != null)
            {
                effects.PoolWaterLine = pool.position.y + HouseActivityAnchors.PoolWater;
                var splash = Make(SplashName, root.transform, pool.position + Vector3.up * HouseActivityAnchors.PoolWater,
                    new Color(.78f, .9f, 1f, .7f), .08f, 8, .4f, 1f, 2f, 1f);
                // Drawn after the pool's translucent water, which shares its queue and would
                // otherwise sort over the drops whenever the swimmer is past the middle.
                splash.GetComponent<ParticleSystemRenderer>().sortingOrder = 1;
                Upward(splash, .25f, 20f);
                effects.Splash = splash;
            }
            return effects;
        }

        /// <summary>
        /// What plays this frame: the bubbles unless motion is reduced, the steam while the player
        /// cooks, and the splashes at <paramref name="swimmer"/> while there is one.
        /// </summary>
        public void Tick(bool reducedMotion, bool cooking, Vector3? swimmer)
        {
            Set(Bubbles, !reducedMotion, reducedMotion);
            Set(Steam, cooking && !reducedMotion, reducedMotion);
            if (Splash != null && swimmer.HasValue)
                Splash.transform.position = new Vector3(swimmer.Value.x, PoolWaterLine, swimmer.Value.z);
            Set(Splash, swimmer.HasValue && !reducedMotion, reducedMotion);
        }

        private static void Set(ParticleSystem system, bool on, bool clear)
        {
            if (system == null) return;
            if (on) { if (!system.isEmitting) system.Play(true); return; }
            // Put down, the last of it finishes; reduced motion takes it away at once.
            if (clear) { if (system.particleCount > 0 || system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            else if (system.isEmitting) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>
        /// Out of a flat disc and up: a cone laid on its back, spreading by <paramref name="spread"/>
        /// degrees. A circle shape would throw them outward across the water instead.
        /// </summary>
        private static void Upward(ParticleSystem system, float radius, float spread)
        {
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = radius;
            shape.angle = spread;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        private static ParticleSystem Make(string name, Transform parent, Vector3 at, Color colour, float size, int count,
            float lifetime, float slowest, float fastest, float gravity)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = at;
            var system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(slowest, fastest);
            main.startSize = size;
            main.startColor = colour;
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.rateOverTime = count / lifetime;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = Material();
            return system;
        }

        /// <summary>
        /// One soft round dot for all of them. Sprites/Default is always in a build, reads the
        /// particle's colour, and tests depth like any world object - the UI shader would take its
        /// depth test from whatever the last canvas left behind.
        /// </summary>
        private static Material Material()
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            const int side = 32;
            var dot = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "House activity dot", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dx = (x + .5f) / side * 2f - 1f, dy = (y + .5f) / side * 2f - 1f;
                    float edge = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * side + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.SmoothStep(0f, 1f, edge)));
                }
            dot.SetPixels32(pixels);
            dot.Apply(false, true);
            material = new Material(shader) { name = "House activity particles", mainTexture = dot };
            return material;
        }
    }
}
