using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The front door the houseguests come through on the first night: a facade, a pair of doors and
    /// the light behind them, standing in the west yard for the length of the intro and struck after it.
    ///
    /// <para>It ports the reference build's <c>EntranceDoor</c> - the rattle before the doors give, the
    /// swing, the flood of light over the threshold, the sparkles falling past the frame and the eye
    /// pulsing over it - into the real house, so each reveal happens in the yard the guest is about to
    /// live in rather than in a tunnel that exists for twenty seconds. The stage decides who walks
    /// through and when; this only builds the door and moves its parts.</para>
    ///
    /// <para><b>Hinged at the jambs.</b> The reference rotated each leaf about its own centre, so a
    /// closed door stood with a 0.7 m slot down the middle and an open one spun in place. Each leaf here
    /// hangs from a pivot on its jamb, at z 12.7 and z 14.9 on the facade's front face, and the two meet
    /// at z 13.8 when closed. Opening turns the left leaf +80° and the right -80°, which swings both free
    /// edges out toward the camera (+x) and leaves 1.8 m of clear doorway between them.</para>
    ///
    /// <para><b>Nothing has a collider.</b> Every primitive loses its collider the moment it is made,
    /// before the physics scene can be queried with it. The houseguests' marks behind and in front of
    /// this door are validated with <c>HouseRoomQuery.HasCapsuleClearance</c>, the camera boom pulls in
    /// to the first thing its occlusion ray hits, and click-to-move walks only when its first hit is
    /// walkable floor - a facade with a collider would fail the marks, shove the door camera into the
    /// guest's face and put a dead strip across the yard. The house's rule for runtime props is no
    /// colliders and no <c>NavMeshObstacle</c>s, and this is a runtime prop.</para>
    ///
    /// <para><b>The far wall is a light, and it is opaque.</b> The houseguests who have not been
    /// revealed yet wait behind the facade, and the doorway is a hole in it. The vestibule closes that
    /// hole: two side flats, a ceiling, and at the back a plane of unlit white that reads as daylight
    /// beyond the door. From the door camera every sight line through the aperture ends on one of those
    /// faces, so nobody queued behind can be seen with the doors wide open. That plane is deliberately
    /// NOT one of the additive glow pieces: an additive plane adds light over whatever is behind it
    /// instead of hiding it.</para>
    ///
    /// <para>Motion is decoration here too. Under reduced motion the leaves land at once, nothing
    /// rattles, no sparkle falls and the light stands at its settled level: the door is still visibly
    /// open and lit, which is the information.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OpeningDoorSet : MonoBehaviour
    {
        /// <summary>The scene root's name, which the stage and its tests find the set by.</summary>
        public const string RootName = "Opening door set runtime";

        // The geometry the stage and its sight-line test measure against, in world metres. The set's
        // root stands at the origin, so every local position below is a world position too.

        /// <summary>The facade slab's centre plane; it is 0.2 m thick.</summary>
        public const float FacadeX = -3.3f;
        /// <summary>The facade's face toward the yard camera, where the hinges are.</summary>
        public const float FacadeFrontX = -3.2f;
        public const float FacadeMinZ = 10.15f;
        public const float FacadeMaxZ = 18.6f;
        public const float FacadeHeight = 4.6f;
        /// <summary>The doorway the leaves close: z 12.7 to 14.9, floor to 2.6 m.</summary>
        public const float ApertureMinZ = 12.7f;
        public const float ApertureMaxZ = 14.9f;
        public const float ApertureHeight = 2.6f;
        /// <summary>Where the vestibule's light plane stands, closing the aperture from behind.</summary>
        public const float VestibuleFarX = -4.4f;
        /// <summary>The underside of the vestibule's ceiling.</summary>
        public const float VestibuleCeiling = 3.0f;

        /// <summary>The middle of the doorway, on the floor, at the facade's front face.</summary>
        public static readonly Vector3 DoorCentre = new Vector3(FacadeFrontX, 0f, 13.8f);

        private const float LeafWidth = 1.1f;
        private const float LeafHeight = 2.6f;
        private const float LeafThickness = 0.08f;
        private const float SignCapHeight = 0.22f;

        private const float OpenYaw = 80f;
        private const float JitterDegrees = 1.7f;
        private const float JitterSeconds = 0.3f;
        private const float OpenRate = 3f;
        // The reference closes at 3/s too. Here the door shuts on the previous guest while the next
        // one is already walking up behind it, and at 3/s it was still a third open when they arrived.
        private const float CloseRate = 6f;
        private const float BurstRamp = 0.5f;
        private const float BurstSettled = 0.2f;
        private const float BurstDecay = 2f;
        private const float LightPeak = 5f;
        private const float LightSettled = 1f;
        private const float DarkRate = 3f;
        private const int SparkleCount = 25;
        private const float EmblemRate = 3f;

        private struct Sparkle
        {
            public Transform transform;
            public Renderer renderer;
            public float x, y, z, top, fall, phase;
        }

        private readonly List<Object> owned = new List<Object>();
        // Its own stream, seeded, so the door never draws from UnityEngine.Random - other presentation
        // code shares that - and two captures of the same moment show the same sparkles.
        private readonly System.Random noise = new System.Random(0x0D00E);

        private Transform leftHinge, rightHinge;
        private Material burstMaterial, emblemMaterial;
        private Renderer burstRenderer;
        private Light burstLight;
        private Sparkle[] sparkles = new Sparkle[0];
        private Texture2D radial;
        private Color burstTint, emblemDeep, emblemBright;

        private bool open, lit, reduced, jittering;
        private float openness, sinceOpen, clock;
        private float burstClock, burstLevel, lightLevel, rampFromLevel, rampFromLight;

        /// <summary>Whether the doors have been told to open and not since told to close.</summary>
        public bool IsOpen => open;

        /// <summary>How far the leaves have actually swung, 0 closed to 1 at ±80°, not counting the rattle.</summary>
        public float Openness => openness;

        /// <summary>
        /// Builds the whole set, closed and dark, as a root of its own in <paramref name="scene"/>.
        ///
        /// <para>A scene root rather than a child of the director or the yard floor: the stage strikes
        /// it by destroying one object, and nothing that enumerates the director's controls or the
        /// floor's furniture ever meets a door. A scene that is not loaded cannot take it, and the set
        /// then stays in the active scene, which is where a new object lands anyway.</para>
        /// </summary>
        public static OpeningDoorSet Build(Scene scene)
        {
            var root = new GameObject(RootName);
            if (scene.IsValid() && scene.isLoaded && root.scene != scene)
                SceneManager.MoveGameObjectToScene(root, scene);
            var set = root.AddComponent<OpeningDoorSet>();
            set.Assemble();
            return set;
        }

        /// <summary>
        /// Opens the doors and throws the light over the threshold.
        ///
        /// <para>From closed the leaves rattle for 0.3 s first, still shut, the way the reference plays
        /// the moment before they give; a door already swinging shut turns straight back instead. Under
        /// <paramref name="reducedMotion"/> they are simply open and the light stands at its settled
        /// level.</para>
        /// </summary>
        public void Open(bool reducedMotion)
        {
            reduced = reducedMotion;
            if (!open)
            {
                open = true;
                sinceOpen = 0f;
                jittering = !reducedMotion && openness < 0.01f;
                Burst();
            }
            if (!reducedMotion) return;
            jittering = false;
            openness = 1f;
            burstLevel = BurstSettled;
            lightLevel = LightSettled;
            ApplyPose(0f, 0f);
            ApplyLight();
            HideSparkles();
        }

        /// <summary>Closes the doors and lets the light die away; at once under <paramref name="reducedMotion"/>.</summary>
        public void Close(bool reducedMotion)
        {
            reduced = reducedMotion;
            open = false;
            lit = false;
            jittering = false;
            HideSparkles();
            if (!reducedMotion) return;
            openness = 0f;
            burstLevel = 0f;
            lightLevel = 0f;
            ApplyPose(0f, 0f);
            ApplyLight();
        }

        /// <summary>
        /// The threshold light on its own: the glow comes up over half a second, flares the point light
        /// to full, then settles to a steady spill. <see cref="Open"/> calls it; the stage can call it
        /// again for a second flourish. A burst over a light already up rises from where it is, so it
        /// never blinks to black first.
        /// </summary>
        public void Burst()
        {
            lit = true;
            burstClock = 0f;
            rampFromLevel = burstLevel;
            rampFromLight = lightLevel;
            if (!reduced) return;
            burstLevel = BurstSettled;
            lightLevel = LightSettled;
            ApplyLight();
        }

        // ---------------------------------------------------------------- the frame

        private void Update()
        {
            // Unscaled: the intro plays over a paused season as happily as over a running one.
            float dt = Time.unscaledDeltaTime;
            clock += dt;
            TickDoors(dt);
            TickBurst(dt);
            TickSparkles(dt);
            TickEmblem();
        }

        private void TickDoors(float dt)
        {
            if (open) sinceOpen += dt;
            if (jittering && (reduced || sinceOpen >= JitterSeconds)) jittering = false;
            float target = open && !jittering ? 1f : 0f;
            openness = reduced ? target : Approach(openness, target, open ? OpenRate : CloseRate, dt);
            ApplyPose(jittering ? Jitter() : 0f, jittering ? Jitter() : 0f);
        }

        private void TickBurst(float dt)
        {
            if (lit)
            {
                burstClock += dt;
                if (reduced) { burstLevel = BurstSettled; lightLevel = LightSettled; }
                else if (burstClock < BurstRamp)
                {
                    float t = burstClock / BurstRamp;
                    burstLevel = Mathf.Lerp(rampFromLevel, 1f, t);
                    lightLevel = Mathf.Lerp(rampFromLight, LightPeak, t);
                }
                else
                {
                    burstLevel = Approach(burstLevel, BurstSettled, BurstDecay, dt);
                    lightLevel = Approach(lightLevel, LightSettled, BurstDecay, dt);
                }
            }
            else if (reduced) { burstLevel = 0f; lightLevel = 0f; }
            else
            {
                burstLevel = Approach(burstLevel, 0f, DarkRate, dt);
                lightLevel = Approach(lightLevel, 0f, DarkRate, dt);
            }
            ApplyLight();
        }

        /// <summary>
        /// Gold motes falling past the doorway while it is open, each respawning at the top when it
        /// reaches the floor, their size breathing - the reference's sparkle loop, unchanged.
        /// </summary>
        private void TickSparkles(float dt)
        {
            if (!open || reduced) { HideSparkles(); return; }
            for (int i = 0; i < sparkles.Length; i++)
            {
                ref var s = ref sparkles[i];
                if (s.transform == null) continue;
                s.y -= s.fall * dt;
                if (s.y < 0f) s.y = s.top;
                float size = 0.02f + 0.06f * (0.5f + 0.5f * Mathf.Sin(clock * 4f + s.phase));
                s.transform.localPosition = new Vector3(s.x, s.y, s.z);
                s.transform.localScale = new Vector3(size, size, 1f);
                s.renderer.enabled = true;
            }
        }

        private void HideSparkles()
        {
            for (int i = 0; i < sparkles.Length; i++)
            {
                ref var s = ref sparkles[i];
                s.y = s.top;
                if (s.renderer != null) s.renderer.enabled = false;
            }
        }

        /// <summary>The eye over the frame breathes between two blues; under reduced motion it holds between them.</summary>
        private void TickEmblem()
        {
            if (emblemMaterial == null) return;
            float blend = reduced ? 0.5f : 0.5f + 0.5f * Mathf.Sin(clock * EmblemRate);
            Tint(emblemMaterial, Hdr(Color.Lerp(emblemDeep, emblemBright, blend), 2f));
        }

        private void ApplyPose(float leftJitter, float rightJitter)
        {
            float yaw = openness * OpenYaw;
            if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(0f, yaw + leftJitter, 0f);
            if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(0f, -yaw + rightJitter, 0f);
        }

        private void ApplyLight()
        {
            if (burstMaterial != null) Tint(burstMaterial, new Color(burstTint.r, burstTint.g, burstTint.b, burstLevel));
            if (burstRenderer != null) burstRenderer.enabled = burstLevel > 0.002f;
            if (burstLight == null) return;
            burstLight.intensity = lightLevel;
            burstLight.enabled = lightLevel > 0.002f;
        }

        private float Jitter() => ((float)noise.NextDouble() * 2f - 1f) * JitterDegrees;

        private float Range(float min, float max) => min + (float)noise.NextDouble() * (max - min);

        /// <summary>Frame-rate independent easing toward <paramref name="target"/> at <paramref name="rate"/> per second.</summary>
        private static float Approach(float value, float target, float rate, float dt)
        {
            float next = value + (target - value) * (1f - Mathf.Exp(-rate * dt));
            return Mathf.Abs(target - next) < 0.0001f ? target : next;
        }

        private void OnDestroy()
        {
            // The primitives' meshes are Unity's own and the pack textures belong to Resources; only
            // what this set made is destroyed here.
            foreach (var thing in owned)
            {
                if (thing == null) continue;
                if (Application.isPlaying) Destroy(thing);
                else DestroyImmediate(thing);
            }
            owned.Clear();
        }

        // ---------------------------------------------------------------- building

        private void Assemble()
        {
            var facade = Solid("Opening facade", UiTheme.Hex("0A0F1E"), 0f, 0.3f, Color.black);
            var leaf = Solid("Opening door leaf", UiTheme.Hex("0A0A14"), 0.4f, 0.4f, Color.black);
            var panel = Solid("Opening door panel", UiTheme.Hex("0D0D1A"), 0.3f, 0.3f, Color.black);
            var goldColour = UiTheme.Hex("FBBF24");
            var gold = Solid("Opening door gold", goldColour, 0.8f, 0.75f, Hdr(goldColour, 0.15f));
            var floor = Solid("Opening vestibule floor", UiTheme.Hex("0A0A14"), 0f, 0.2f, Color.black);

            BuildFacade(facade, gold);
            BuildLeaves(leaf, panel, gold);
            BuildSign();
            BuildEmblem();
            BuildVestibule(facade, floor);
            BuildBurst();
            BuildSparkles();

            ApplyPose(0f, 0f);
            ApplyLight();
            HideSparkles();
            TickEmblem();
        }

        /// <summary>
        /// The slab in three panels round the doorway, a lintel closing the doorway's top to 2.6 m, and
        /// the gold jambs and crown standing proud of it.
        /// </summary>
        private void BuildFacade(Material facade, Material gold)
        {
            var group = Group("Facade", transform);
            const float depth = 0.2f, jambLeft = 12.65f, jambRight = 14.95f, head = 2.8f;
            Box("Left panel", group, new Vector3(FacadeX, FacadeHeight / 2f, (FacadeMinZ + jambLeft) / 2f),
                new Vector3(depth, FacadeHeight, jambLeft - FacadeMinZ), facade, true);
            Box("Right panel", group, new Vector3(FacadeX, FacadeHeight / 2f, (jambRight + FacadeMaxZ) / 2f),
                new Vector3(depth, FacadeHeight, FacadeMaxZ - jambRight), facade, true);
            Box("Top panel", group, new Vector3(FacadeX, (head + FacadeHeight) / 2f, DoorCentre.z),
                new Vector3(depth, FacadeHeight - head, jambRight - jambLeft), facade, true);
            // The crown sits at 2.7-2.8 and the leaves stop at 2.6; without this the closed door had a
            // 10 cm slot over it.
            Box("Lintel", group, new Vector3(FacadeX, ApertureHeight + 0.05f, DoorCentre.z),
                new Vector3(depth, 0.1f, ApertureMaxZ - ApertureMinZ), facade, true);

            Box("Left jamb", group, new Vector3(FacadeX, head / 2f, jambLeft), new Vector3(0.24f, head, 0.1f), gold, true);
            Box("Right jamb", group, new Vector3(FacadeX, head / 2f, jambRight), new Vector3(0.24f, head, 0.1f), gold, true);
            Box("Crown", group, new Vector3(FacadeX, 2.75f, DoorCentre.z), new Vector3(0.24f, 0.1f, 2.4f), gold, true);
        }

        private void BuildLeaves(Material leaf, Material panel, Material gold)
        {
            var group = Group("Doors", transform);
            leftHinge = Leaf("Left door hinge", group, ApertureMinZ, 1f, leaf, panel, gold);
            rightHinge = Leaf("Right door hinge", group, ApertureMaxZ, -1f, leaf, panel, gold);
        }

        /// <summary>
        /// One leaf, hung from a pivot on its jamb. <paramref name="side"/> is the direction it reaches
        /// across the doorway: +1 for the left leaf, -1 for the right. Its yard face is flush with the
        /// facade's front, so a closed door reads as part of the wall.
        /// </summary>
        private Transform Leaf(string name, Transform parent, float hingeZ, float side, Material leaf, Material panel, Material gold)
        {
            var hinge = Group(name, parent);
            hinge.localPosition = new Vector3(FacadeFrontX, 0f, hingeZ);
            float across = side * LeafWidth / 2f;
            Box("Leaf", hinge, new Vector3(-LeafThickness / 2f, LeafHeight / 2f, across),
                new Vector3(LeafThickness, LeafHeight, LeafWidth), leaf, true);
            Box("Upper panel", hinge, new Vector3(0.005f, 1.875f, across), new Vector3(0.02f, 0.95f, 0.7f), panel, true);
            Box("Lower panel", hinge, new Vector3(0.005f, 0.75f, across), new Vector3(0.02f, 0.9f, 0.7f), panel, true);
            Box("Rail", hinge, new Vector3(0.012f, 1.3f, across), new Vector3(0.02f, 0.04f, 0.7f), gold, true);
            Piece("Handle", PrimitiveType.Sphere, hinge, new Vector3(0.035f, 1.05f, side * (LeafWidth - 0.15f)),
                Quaternion.identity, Vector3.one * 0.06f, gold, true);
            return hinge;
        }

        /// <summary>
        /// The wordmark over the door, in 3D text: the reference's sign was a blank white bar.
        ///
        /// <para>Turned to yaw 270 - the door camera's own yaw - because text reads from its local -z,
        /// so a sign reads left to right for a camera sharing its rotation. At yaw 90 it would face into
        /// the vestibule and read mirrored from the yard.</para>
        /// </summary>
        private void BuildSign()
        {
            var sign = new GameObject("Sign", typeof(TextMeshPro));
            sign.layer = 0;
            sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(-3.18f, 3.02f, DoorCentre.z);
            sign.transform.localRotation = FacingYard;
            sign.transform.localScale = Vector3.one;

            var label = sign.GetComponent<TextMeshPro>();
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) label.font = bold;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            // Midline centres the letters themselves, so the capitals span 2.91-3.13 m: on the crown,
            // under the eye, and no wider than the door frame. At 0.4 m the word ran 3.4 m, wider than
            // the frame, and covered the eye.
            label.alignment = TextAlignmentOptions.Midline;
            label.fontSize = FontSizeFor(label.font, SignCapHeight);
            label.characterSpacing = 8f;
            label.color = Color.white;
            label.enableVertexGradient = true;
            var top = UiTheme.Hex("6CC0FF");
            var bottom = UiTheme.Hex("3A86FF");
            label.colorGradient = new VertexGradient(top, top, bottom, bottom);
            label.rectTransform.sizeDelta = new Vector2(4f, 0.8f);
            label.text = Localisation.Text("GAMESIM");
            var renderer = sign.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// The eye over the sign: the HUD's own eye icon on a glow, or a drawn ring-iris-pupil on a
        /// clone that has not generated the icons.
        /// </summary>
        private void BuildEmblem()
        {
            emblemDeep = UiTheme.Hex("3B82F6");
            emblemBright = UiTheme.Hex("6CC0FF");
            var icon = UiTheme.Icon("eye");
            Texture texture = icon != null ? icon.texture : null;
            float aspect = 1f;
            if (texture == null) texture = Own(EyeTexture());
            else if (icon.rect.height > 0f) aspect = icon.rect.width / icon.rect.height;

            emblemMaterial = Glow("Opening eye emblem", texture, Hdr(emblemBright, 2f));
            if (emblemMaterial != null && icon != null && icon.texture != null) MapSprite(emblemMaterial, icon);
            Piece("Eye emblem", PrimitiveType.Quad, transform, new Vector3(-3.17f, 3.42f, DoorCentre.z), FacingYard,
                new Vector3(0.34f * aspect, 0.34f, 1f), emblemMaterial, false);
        }

        private void BuildVestibule(Material walls, Material floor)
        {
            var group = Group("Vestibule", transform);
            float length = FacadeX - VestibuleFarX;
            float middle = (FacadeX + VestibuleFarX) / 2f;
            Box("Left flat", group, new Vector3(middle, VestibuleCeiling / 2f, 12.65f), new Vector3(length, VestibuleCeiling, 0.1f), walls, true);
            Box("Right flat", group, new Vector3(middle, VestibuleCeiling / 2f, 14.95f), new Vector3(length, VestibuleCeiling, 0.1f), walls, true);
            Box("Ceiling", group, new Vector3(middle, VestibuleCeiling + 0.05f, DoorCentre.z), new Vector3(length, 0.1f, 2.4f), walls, true);
            Piece("Floor", PrimitiveType.Quad, group, new Vector3((VestibuleFarX + FacadeFrontX) / 2f, 0.012f, DoorCentre.z),
                Quaternion.Euler(90f, 0f, 0f), new Vector3(FacadeFrontX - VestibuleFarX, ApertureMaxZ - ApertureMinZ, 1f), floor, true);

            // Opaque, unlit and above 1, so the bloom reads it as daylight past the door. See the class
            // comment for why this one must hide what is behind it.
            var outside = Lamp("Opening outside light", Hdr(Color.white, 2f));
            Piece("Outside light", PrimitiveType.Quad, group, new Vector3(VestibuleFarX, (VestibuleCeiling + 0.05f) / 2f, DoorCentre.z),
                FacingYard, new Vector3(2.4f, VestibuleCeiling + 0.05f, 1f), outside, false);

            var amber = Glow("Opening vestibule strip", null, Hdr(UiTheme.Hex("F59E0B"), 2f));
            const float stripFrom = VestibuleFarX, stripTo = -3.4f;
            foreach (float z in new[] { ApertureMinZ + 0.02f, ApertureMaxZ - 0.02f })
                foreach (float y in new[] { 0.03f, VestibuleCeiling - 0.02f })
                    Box("Glow strip", group, new Vector3((stripFrom + stripTo) / 2f, y, z),
                        new Vector3(stripTo - stripFrom, 0.03f, 0.03f), amber, false);
        }

        /// <summary>
        /// The light over the threshold: an additive glow in the doorway, which is what the camera sees,
        /// and a point light, which is what lands on the guest walking through it.
        /// </summary>
        private void BuildBurst()
        {
            burstTint = Hdr(UiTheme.Hex("FFFBE6"), 1.5f);
            // The set's own radial glow rather than the pack's: that one is cyan and squarish, and
            // tinted warm it read as a green smear at the door's edge instead of daylight.
            burstMaterial = Glow("Opening door burst", Radial(), new Color(burstTint.r, burstTint.g, burstTint.b, 0f));
            burstRenderer = Piece("Burst", PrimitiveType.Quad, transform, new Vector3(-3.1f, 1.4f, DoorCentre.z), FacingYard,
                new Vector3(3f, 3f, 1f), burstMaterial, false).GetComponent<Renderer>();

            var lamp = new GameObject("Burst light");
            lamp.layer = 0;
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(-3.0f, 1.8f, DoorCentre.z);
            burstLight = lamp.AddComponent<Light>();
            burstLight.type = LightType.Point;
            burstLight.range = 8f;
            burstLight.color = UiTheme.Hex("FFFBE6");
            burstLight.intensity = 0f;
            burstLight.shadows = LightShadows.None;
            burstLight.enabled = false;
        }

        /// <summary>Twenty-five glow quads over the doorway, above the frame, hidden until the doors open.</summary>
        private void BuildSparkles()
        {
            var group = Group("Sparkles", transform);
            var material = Glow("Opening sparkle", Radial(), Hdr(UiTheme.Hex("FFD37A"), 2f, 0.8f));
            sparkles = new Sparkle[SparkleCount];
            for (int i = 0; i < SparkleCount; i++)
            {
                var s = new Sparkle
                {
                    x = Range(-3.0f, -1.8f),
                    z = Range(ApertureMinZ, ApertureMaxZ),
                    top = Range(3.5f, 5.0f),
                    fall = Range(0.3f, 1.0f),
                    phase = Range(0f, Mathf.PI * 2f),
                };
                s.y = s.top;
                var quad = Piece("Sparkle", PrimitiveType.Quad, group, new Vector3(s.x, s.y, s.z), FacingYard,
                    new Vector3(0.05f, 0.05f, 1f), material, false);
                s.transform = quad.transform;
                s.renderer = quad.GetComponent<Renderer>();
                s.renderer.enabled = false;
                sparkles[i] = s;
            }
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>Yaw 270: the door camera's own rotation, so a quad or a sign shows its face to the yard.</summary>
        private static Quaternion FacingYard => Quaternion.Euler(0f, 270f, 0f);

        private Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.layer = 0;
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid) =>
            Piece(name, PrimitiveType.Cube, parent, position, Quaternion.identity, size, material, solid);

        /// <summary>
        /// A primitive with its collider already gone, on the default layer, casting no shadow.
        ///
        /// <para>No shadows at all, solid pieces included: depending on where the sun stands the facade's
        /// would land on the reveal mark or across half the yard, and nothing about a door reveal needs
        /// it. Solid pieces still receive shadows, so the set sits in the yard's light.</para>
        /// </summary>
        private GameObject Piece(string name, PrimitiveType type, Transform parent, Vector3 position, Quaternion rotation,
            Vector3 size, Material material, bool solid)
        {
            var piece = GameObject.CreatePrimitive(type);
            var collider = piece.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            piece.name = name;
            piece.layer = 0;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localRotation = rotation;
            piece.transform.localScale = size;
            var renderer = piece.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Without any usable shader the primitive keeps the pipeline's default material, which
                // is plainer but still a door.
                if (material != null) renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = solid;
            }
            return piece;
        }

        // ---------------------------------------------------------------- materials and textures

        private T Own<T>(T thing) where T : Object
        {
            if (thing != null) owned.Add(thing);
            return thing;
        }

        /// <summary>A lit surface: URP Lit, or URP Unlit, or the built-in unlit colour, whichever exists.</summary>
        private Material Solid(string name, Color colour, float metallic, float smoothness, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            var material = Own(new Material(shader) { name = name });
            Tint(material, colour);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        /// <summary>An opaque unlit surface: it is its own light and it hides what stands behind it.</summary>
        private Material Lamp(string name, Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            var material = Own(new Material(shader) { name = name });
            Tint(material, colour);
            return material;
        }

        /// <summary>
        /// A glowing, additive material: the branding palette's template when it has been built - its
        /// variant is guaranteed to ship - otherwise URP Unlit set up the same way at runtime, then the
        /// built-in sprite shader, then a flat unlit colour.
        /// </summary>
        private Material Glow(string name, Texture texture, Color colour)
        {
            var palette = HouseBrandingPalette.Current;
            var material = palette != null ? palette.Glow(name, texture, colour) : null;
            if (material == null) material = AdditiveFallback(name, texture, colour);
            return Own(material);
        }

        /// <summary>The template's settings, as <c>HouseBrandingAssets.ConfigureAdditive</c> applies them.</summary>
        private static Material AdditiveFallback(string name, Texture texture, Color colour)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var material = new Material(unlit) { name = name };
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 2f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                material.SetFloat("_AlphaClip", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
                material.SetColor("_BaseColor", colour);
                return material;
            }
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            var fallback = new Material(shader) { name = name };
            Tint(fallback, colour);
            if (texture != null && fallback.HasProperty("_MainTex")) fallback.mainTexture = texture;
            return fallback;
        }

        /// <summary>Points the material's main texture at the sprite's own rectangle, in case it was ever packed.</summary>
        private static void MapSprite(Material material, Sprite sprite)
        {
            var texture = sprite.texture;
            if (texture == null || texture.width <= 0 || texture.height <= 0) return;
            var rect = new Rect(0f, 0f, texture.width, texture.height);
            // A tightly packed sprite has no rectangle to ask for; it then keeps the whole texture.
            if (!sprite.packed || sprite.packingMode == SpritePackingMode.Rectangle) rect = sprite.textureRect;
            material.mainTextureScale = new Vector2(rect.width / texture.width, rect.height / texture.height);
            material.mainTextureOffset = new Vector2(rect.x / texture.width, rect.y / texture.height);
        }

        private static void Tint(Material material, Color colour)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }

        /// <summary>
        /// <paramref name="colour"/> brightened past 1 so the bloom reads it as light. Unlike
        /// <c>Color * float</c> it leaves alpha alone: the glow blends by alpha, and an alpha of 2 would
        /// double the light a second time.
        /// </summary>
        private static Color Hdr(Color colour, float intensity, float alpha = 1f) =>
            new Color(colour.r * intensity, colour.g * intensity, colour.b * intensity, alpha);

        private static float FontSizeFor(TMP_FontAsset font, float capHeight)
        {
            // A world-space TextMeshPro draws one em at a tenth of its font size, in metres. Inter's
            // capitals stand 0.727 em tall, which is the fallback when the asset does not say.
            float capEm = 0.727f, scale = 1f;
            if (font != null)
            {
                var face = font.faceInfo;
                if (face.pointSize > 0 && face.capLine > 0f) capEm = face.capLine / face.pointSize;
                if (face.scale > 0f) scale = face.scale;
            }
            return capHeight / (capEm * scale * 0.1f);
        }

        /// <summary>A soft white dot, for the sparkles, and for the burst when the pack glow is missing.</summary>
        private Texture2D Radial()
        {
            if (radial != null) return radial;
            const int size = 64;
            float half = (size - 1) / 2f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - r);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                }
            radial = Own(Paint("Opening door radial glow", size, pixels));
            return radial;
        }

        /// <summary>
        /// A drawn eye - ring, iris, pupil - in white for the glow to tint. It fills the middle two
        /// thirds of the quad, as the HUD's eye icon does, so it clears the sign's capitals either way.
        /// </summary>
        private static Texture2D EyeTexture()
        {
            const int size = 128;
            float half = (size - 1) / 2f, feather = 1.5f / half;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Max(Band(r, 0.52f, 0.65f, feather),
                        Mathf.Max(Band(r, 0.22f, 0.40f, feather) * 0.85f, Band(r, -1f, 0.12f, feather)));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            return Paint("Opening eye emblem", size, pixels);
        }

        private static float Band(float r, float inner, float outer, float feather) =>
            Mathf.Clamp01((r - inner) / feather + 0.5f) * Mathf.Clamp01((outer - r) / feather + 0.5f);

        private static Texture2D Paint(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            // Uploaded once and released from the CPU side: nothing reads these back.
            texture.Apply(false, true);
            return texture;
        }
    }
}
