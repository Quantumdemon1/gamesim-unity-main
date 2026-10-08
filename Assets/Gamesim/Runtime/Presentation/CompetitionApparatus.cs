using System;
using System.Collections.Generic;
using System.Globalization;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Collider-free, stage-owned instruments. They mirror the player's existing attempt;
    /// no instrument invents NPC scores, reveals hidden cards, accepts input or advances RNG.
    /// The actor's reserved floor anchor and the simulation remain authoritative.
    /// </summary>
    public sealed class CompetitionApparatus : MonoBehaviour
    {
        public enum Family { Signals, PairConsole, GripRig, DiceTray, WordConsole }
        public Family Instrument { get; private set; }
        public string DefinitionId { get; private set; }
        public string ActorId { get; private set; }
        public string ProgressText => progress != null ? progress.text : "";
        public IReadOnlyList<Renderer> OverlayRenderers => overlays;
        public int VisibleMemoryFaces { get; private set; }
        public float GripFraction { get; private set; }
        public int LitSignal { get; private set; } = -1;

        private readonly List<Renderer> overlays = new List<Renderer>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<MeshFilter> solidMeshes=new List<MeshFilter>();
        private readonly List<MeshFilter> raisedMeshes=new List<MeshFilter>();
        private readonly HashSet<MeshFilter> yawingMeshes=new HashSet<MeshFilter>();
        private readonly List<Renderer> indicators = new List<Renderer>();
        private readonly List<TMP_Text> glyphs = new List<TMP_Text>();
        private readonly Dictionary<Renderer,Renderer> repeaters = new Dictionary<Renderer,Renderer>();
        private readonly Dictionary<TMP_Text,TMP_Text> repeatWords = new Dictionary<TMP_Text,TMP_Text>();
        private MaterialPropertyBlock tint;
        private Material frame, accent, dark, paper, readoutFontMaterial;
        private Mesh box;
        private TMP_Text progress;
        private Transform progressFrontBacking,progressAudienceBacking;
        private Transform weight, gripBar, gripTrack, weightGuide;
        private readonly List<Transform> gripTicks = new List<Transform>();
        private Transform leftUpright, rightUpright, topBrace, handle, responsePad;
        private Transform trayPedestal, trayBed, trayBack, trayLeft, trayRight;
        private Renderer gripRenderer;
        private float gripHeight = 1.16f;
        private bool overlaysVisible = true;
        private float nearestRaisedZ;
        private const float Front = .56f;

        public static Family For(CompetitionDefinition definition, string category = null)
        {
            switch (CompetitionMiniGames.For(definition?.Category ?? category))
            {
                case CompetitionMiniGames.Kind.Memory: return Family.PairConsole;
                case CompetitionMiniGames.Kind.Endurance: return Family.GripRig;
                case CompetitionMiniGames.Kind.Dice: return Family.DiceTray;
                case CompetitionMiniGames.Kind.Words: return Family.WordConsole;
                default: return Family.Signals;
            }
        }

        public static CompetitionApparatus Create(Transform anchor, CompetitionDefinition definition,
            string category, string actorId, Color award)
        {
            var root = new GameObject("Competition apparatus · " + For(definition, category));
            root.transform.SetParent(anchor, false);
            var apparatus = root.AddComponent<CompetitionApparatus>();
            apparatus.Instrument = For(definition, category);
            apparatus.DefinitionId = definition?.Id;
            apparatus.ActorId = actorId;
            apparatus.Build(award);
            return apparatus;
        }

        private void Build(Color award)
        {
            // Unity native objects must be created after component construction. Build also
            // runs for an inactive reserved anchor, whose Awake has not run yet.
            tint = new MaterialPropertyBlock();
            frame = Material("Arena graphite", UiTheme.Hex("24334A"), true);
            accent = Material("Award trim", award, false);
            dark = Material("Instrument face", UiTheme.Hex("102138"), false);
            paper = Material("Indicator face", UiTheme.Paper, false);
            box = BoxMesh();
            meshes.Add(box);
            // An open-footed stance mark belongs to the instrument, rather than a generic disc.
            Part("Left stance rail", new Vector3(-.30f, .016f, .02f), new Vector3(.035f, .025f, .55f), accent, clearsActor:false);
            Part("Right stance rail", new Vector3(.30f, .016f, .02f), new Vector3(.035f, .025f, .55f), accent, clearsActor:false);
            switch (Instrument)
            {
                case Family.PairConsole: BuildPairs(); break;
                case Family.GripRig: BuildGrip(); break;
                case Family.Signals: BuildSignals(); break;
                case Family.DiceTray: BuildDice(); break;
                case Family.WordConsole: BuildWords(); break;
            }
            // The dice pedestal extends 125 mm toward the actor. Its former common
            // readout plane was 45 mm inside that support, hiding the middle letters.
            // The grip scale's ticks reach Front-.10: its text must sit ahead of
            // those real faces, as dice progress sits ahead of its pedestal.
            float progressFront = Instrument == Family.DiceTray ? Front - .145f
                : Instrument == Family.GripRig ? Front - .12f : Front - .08f;
            float progressHeight=Instrument==Family.GripRig?1.73f:.74f;
            progress = Words("Instrument progress", new Vector3(0, progressHeight, progressFront), .90f, .13f, "Ready");
            // Each opposing plane has its own dark face. Saved yard lights and pillars
            // can remain behind the words without washing out their public progress.
            progressFrontBacking=Part("Progress front backing", new Vector3(0, progressHeight, progressFront + .006f), new Vector3(.96f, .16f, .008f), dark).transform;
            progressAudienceBacking=Part("Progress audience backing", new Vector3(0, progressHeight, Front + .154f), new Vector3(.96f, .16f, .008f), dark).transform;
            // The show's wide camera sees the audience-facing readout on the back of a console.
            // Repeat only the same public information, rather than turning the actor away from it.
            foreach (TMP_Text glyph in glyphs) RepeatWord(glyph);
            RepeatWord(progress);
            if(Instrument==Family.PairConsole || Instrument==Family.Signals || Instrument==Family.WordConsole)
                foreach(Renderer indicator in indicators)
                {
                    Vector3 at=indicator.transform.localPosition;at.x=-at.x;at.z=Front+.13f;
                    repeaters[indicator]=Part(indicator.name+" audience face",at,indicator.transform.localScale,paper);
                }
            nearestRaisedZ=NearestRaisedSolid();
        }

        private void Console(float height, float width)
        {
            Part("Console left leg", new Vector3(-width * .37f, .42f, Front + .08f), new Vector3(.07f, .84f, .14f), frame);
            Part("Console right leg", new Vector3(width * .37f, .42f, Front + .08f), new Vector3(.07f, .84f, .14f), frame);
            Part("Console housing", new Vector3(0, height, Front + .04f), new Vector3(width, .80f, .14f), frame);
            Part("Recessed display", new Vector3(0, height, Front - .039f), new Vector3(width - .09f, .69f, .025f), dark);
            Part("Award header", new Vector3(0, height + .38f, Front - .04f), new Vector3(width, .045f, .03f), accent);
        }

        private void BuildPairs()
        {
            Console(1.27f, .92f);
            for (int i = 0; i < 16; i++)
            {
                Vector3 at = new Vector3((i % 4 - 1.5f) * .19f, 1.28f - (i / 4 - 1.5f) * .16f, Front - .064f);
                indicators.Add(Part("Memory tile " + i, at, new Vector3(.16f, .13f, .025f), paper));
                glyphs.Add(Words("Memory face " + i, at + Vector3.back * .018f, .14f, .11f, "?"));
            }
        }

        private void BuildSignals()
        {
            Console(1.20f, .95f);
            Vector2[] places = { new Vector2(0, .23f), new Vector2(.25f, 0), new Vector2(0, -.23f), new Vector2(-.25f, 0) };
            string[] labels = { "UP", "RIGHT", "DOWN", "LEFT" };
            for (int i = 0; i < places.Length; i++)
            {
                Vector3 at = new Vector3(places[i].x, 1.22f + places[i].y, Front - .065f);
                indicators.Add(Part("Signal target " + labels[i], at, new Vector3(.20f, .17f, .03f), paper));
                glyphs.Add(Words("Signal direction " + labels[i], at + Vector3.back * .02f, .18f, .12f, labels[i]));
            }
            responsePad=Part("Response pad", new Vector3(0, 1.16f, .39f), new Vector3(.14f, .065f, .15f), accent).transform;
        }

        private void BuildGrip()
        {
            leftUpright=Part("Rig left upright", new Vector3(-.42f, .78f, Front), new Vector3(.065f, 1.56f, .09f), frame).transform;
            rightUpright=Part("Rig right upright", new Vector3(.42f, .78f, Front), new Vector3(.065f, 1.56f, .09f), frame).transform;
            topBrace=Part("Rig top brace", new Vector3(0, 1.56f, Front), new Vector3(.91f, .07f, .10f), accent).transform;
            handle=Part("Grip handle", new Vector3(0, 1.16f, Front - .055f), new Vector3(.78f, .045f, .055f), accent).transform;
            gripTrack=Part("Grip scale track", new Vector3(.30f, 1.09f, Front - .075f), new Vector3(.055f, .69f, .04f), dark).transform;
            gripBar = Part("Grip remaining", new Vector3(.30f, 1.09f, Front - .085f), new Vector3(.035f, .69f, .025f), paper).transform;
            gripRenderer=gripBar.GetComponent<Renderer>();
            weightGuide=Part("Counterweight guide", new Vector3(0, 1.10f, Front + .15f), new Vector3(.03f, .76f, .03f), frame).transform;
            weight = Part("Pressure counterweight", new Vector3(0, .85f, Front + .15f), new Vector3(.25f, .16f, .17f), frame).transform;
            for (int i = 0; i < 5; i++)
                gripTicks.Add(Part("Grip scale tick " + i, new Vector3(.35f, .77f + i * .16f, Front - .085f), new Vector3(.06f, .012f, .03f), accent).transform);
        }

        private void BuildDice()
        {
            trayPedestal=Part("Tray pedestal", new Vector3(0, .55f, Front), new Vector3(.20f, 1.10f, .25f), frame).transform;
            trayBed=Part("Dice tray bed", new Vector3(0, 1.14f, Front), new Vector3(.91f, .08f, .42f), dark).transform;
            trayBack=Part("Tray back rail", new Vector3(0, 1.21f, Front + .20f), new Vector3(.96f, .13f, .05f), accent).transform;
            trayLeft=Part("Tray left rail", new Vector3(-.455f, 1.21f, Front), new Vector3(.05f, .13f, .44f), accent).transform;
            trayRight=Part("Tray right rail", new Vector3(.455f, 1.21f, Front), new Vector3(.05f, .13f, .44f), accent).transform;
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = new Vector3((i - 1) * .25f, 1.28f, Front - .115f);
                indicators.Add(Part("Die " + i, at, Vector3.one * .17f, paper, sweepsYaw:true));
                glyphs.Add(Words("Die face " + i, at + Vector3.back * .09f, .14f, .14f, "—"));
            }
        }

        private void BuildWords()
        {
            Console(1.22f, 1.02f);
            for (int i = 0; i < 12; i++)
            {
                Vector3 at = new Vector3((i % 6 - 2.5f) * .145f, i < 6 ? 1.30f : 1.08f, Front - .064f);
                indicators.Add(Part("Letter tile " + i, at, new Vector3(.125f, .16f, .025f), paper));
                glyphs.Add(Words("Letter " + i, at + Vector3.back * .018f, .11f, .14f, ""));
            }
            Words("Word tray label", new Vector3(0, 1.51f, Front - .07f), .85f, .12f, "LETTER TRAY");
        }

        /// <summary>Pure presentation reads. An absent attempt means another entrant's ready station.</summary>
        public void Sync(MiniGameRun run, bool started, bool preview, bool paused, bool reducedMotion)
        {
            if (run == null) { SetWord(progress,"Ready"); return; }
            SetWord(progress,paused ? "Paused" : !started && !preview ? "Ready" : Readout(run));
            switch (Instrument)
            {
                case Family.PairConsole:
                    VisibleMemoryFaces = 0;
                    for (int i = 0; i < indicators.Count; i++)
                    {
                        bool matched = run.Matched[i];
                        bool revealed = matched || preview && !paused || started && !paused && (i == run.FirstFlip || i == run.SecondFlip);
                        if (revealed) VisibleMemoryFaces++;
                        SetWord(glyphs[i],revealed ? CompetitionGameScreen.MemoryFaceName(run.Faces[i]) : "?");
                        Colorize(indicators[i], matched ? UiTheme.Positive : revealed ? UiTheme.Accent : UiTheme.Hex("31425B"));
                    }
                    break;
                case Family.Signals:
                    LitSignal = started && run.TargetLive ? (int)run.TargetDirection : -1;
                    for (int i = 0; i < indicators.Count; i++)
                        Colorize(indicators[i], i == LitSignal ? UiTheme.Positive : UiTheme.Hex("31425B"));
                    break;
                case Family.GripRig:
                    GripFraction = Mathf.Clamp01((float)(run.Meter / CompetitionMiniGames.MeterFull));
                    gripBar.localScale = new Vector3(.035f, .69f * GripFraction, .025f);
                    gripBar.localPosition = new Vector3(.30f, gripHeight-.415f + .345f * GripFraction, Front - .085f);
                    Colorize(gripRenderer, GripFraction < .25f ? UiTheme.Danger : UiTheme.Positive);
                    weight.localPosition = new Vector3(0, run.GripPressure > 1 ? gripHeight + .18f : gripHeight - .31f, Front + .15f);
                    // The guide's two positions indicate the actual pressure band, with no extra motion.
                    break;
                case Family.DiceTray:
                    for (int i = 0; i < glyphs.Count; i++)
                    {
                        int face = run.Face(i);
                        SetWord(glyphs[i],face > 0 ? face.ToString(CultureInfo.InvariantCulture) : run.Rolling ? "…" : "—");
                        indicators[i].transform.localRotation = reducedMotion || paused || run.DieLanded(i)
                            ? Quaternion.identity : Quaternion.Euler(0, (float)run.Elapsed * 200, 0);
                    }
                    break;
                case Family.WordConsole:
                    for (int i = 0; i < glyphs.Count; i++)
                    {
                        bool present = i < run.Scrambled.Length;
                        bool hidden=!started || paused;
                        SetWord(glyphs[i],present ? hidden ? "?" : run.Scrambled[i].ToString() : "");
                        Colorize(indicators[i], present && !hidden && run.TilePicked(i) ? UiTheme.Positive : UiTheme.Hex("31425B"));
                    }
                    break;
            }
        }

        public static string Readout(MiniGameRun run)
        {
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: return run.MatchedPairs + " / " + run.Pairs + " pairs";
                case CompetitionMiniGames.Kind.Reaction: return run.Hits + " / " + run.Spawned + " hits";
                case CompetitionMiniGames.Kind.Endurance:
                    return "Effort " + run.Held.ToString("0.0", CultureInfo.InvariantCulture) + "s · grip " + Math.Round(run.Meter) + "%";
                case CompetitionMiniGames.Kind.Dice: return "Roll " + run.RollsUsed + " / " + MiniGameRun.MaxRolls + " · total " + run.RollTotal;
                case CompetitionMiniGames.Kind.Words: return run.WordsSolved + " solved · " + run.WordPoints.ToString("0.#", CultureInfo.InvariantCulture) + " points";
                default: return "Ready";
            }
        }

        /// <summary>Clear the actor capsule with the nearest raised solid, including the response pad and tray.</summary>
        public void FitActorClearance(float bodyRadius,Vector3 bodyOffset=default)
        {
            if(Instrument==Family.GripRig)return; // The hand-fit owns that rig's depth.
            transform.localPosition=new Vector3(bodyOffset.x,0,bodyOffset.z+Mathf.Max(0,bodyRadius+.10f-nearestRaisedZ));
        }

        private float NearestRaisedSolid()
        {
            // Measure the authored solids once, before contact fitting. All later contact
            // adjustments change height only; the dice also turn about their local Y axis.
            // Reserve that entire sweep so progress reads cannot move a corner into the body.
            float nearest=float.PositiveInfinity;
            var inverse=transform.worldToLocalMatrix;
            foreach(var mesh in raisedMeshes)
            {
                var bounds=mesh.sharedMesh.bounds;
                var toLocal=inverse*mesh.transform.localToWorldMatrix;
                Vector3 pivot=toLocal.MultiplyPoint3x4(Vector3.zero);
                bool sweepsYaw=yawingMeshes.Contains(mesh);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    Vector3 point=toLocal.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
                    float depth=point.z;
                    if(sweepsYaw)
                    {
                        float dx=point.x-pivot.x,dz=point.z-pivot.z;
                        depth=pivot.z-Mathf.Sqrt(dx*dx+dz*dz);
                    }
                    nearest=Mathf.Min(nearest,depth);
                }
            }
            return nearest;
        }

        /// <summary>Fit only the grip contact geometry; the reserved root and navigation are untouched.</summary>
        public void FitGrip(float shoulderHeight, float shoulderForward, float reach, float bodyRadius,Vector3 bodyOffset=default)
        {
            if(Instrument!=Family.GripRig)return;
            float rise=reach*.62f;
            gripHeight=shoulderHeight+rise;
            float clearance=bodyOffset.z+bodyRadius+.11f;
            float forward=Mathf.Max(clearance,shoulderForward+Mathf.Sqrt(Mathf.Max(.01f,reach*reach-rise*rise))*.86f);
            transform.localPosition=new Vector3(bodyOffset.x,0,forward-Front+.055f);
            handle.localPosition=new Vector3(0,gripHeight,Front-.055f);
            gripTrack.localPosition=new Vector3(.30f,gripHeight-.07f,Front-.075f);
            weightGuide.localPosition=new Vector3(0,gripHeight-.06f,Front+.15f);
            for(int i=0;i<gripTicks.Count;i++)gripTicks[i].localPosition=new Vector3(.35f,gripHeight-.39f+i*.16f,Front-.085f);
            float top=gripHeight+.26f;
            FitUpright(leftUpright,top);FitUpright(rightUpright,top);
            topBrace.localPosition=new Vector3(0,top,Front);
            // A waist-high plate is behind the contestant during an actual hold. Keep the
            // existing two-sided progress display above its fitted brace, in the same station.
            float readoutHeight=top+.14f;
            FitPartHeight(progress.transform,readoutHeight);
            if(repeatWords.TryGetValue(progress,out var audience))FitPartHeight(audience.transform,readoutHeight);
            FitPartHeight(progressFrontBacking,readoutHeight);FitPartHeight(progressAudienceBacking,readoutHeight);
        }

        private static void FitUpright(Transform upright,float top)
        {
            var p=upright.localPosition;p.y=top*.5f;upright.localPosition=p;
            var scale=upright.localScale;scale.y=top;upright.localScale=scale;
        }

        private static void FitPartHeight(Transform part,float height)
        {var at=part.localPosition;at.y=height;part.localPosition=at;}

        public Vector3 HandContact(bool left)
        {
            if(handle!=null)return handle.TransformPoint(new Vector3(left?-.33f:.33f,0,-.5f));
            if(responsePad!=null)return responsePad.position+Vector3.up*.04f;
            if(Instrument==Family.DiceTray)return indicators[left?0:2].transform.TransformPoint(Vector3.back*.5f);
            return transform.TransformPoint(new Vector3(left?-.22f:.22f,.99f,Front-.12f));
        }

        /// <summary>Put the real pad/tray at the actor's hand height without moving their body.</summary>
        public void FitPress(Vector3 shoulder,float reach)
        {
            if(responsePad!=null)
            {
                Vector3 at=responsePad.localPosition;at.x=shoulder.x;at.y=shoulder.y-.04f-reach*.10f;
                responsePad.localPosition=at;
            }
            if(Instrument!=Family.DiceTray)return;
            float shift=Mathf.Clamp(shoulder.y-.06f-1.28f,-.4f,.4f);
            trayPedestal.localPosition=new Vector3(0,.55f+shift*.5f,Front);
            trayPedestal.localScale=new Vector3(.20f,1.10f+shift,.25f);
            FitPartHeight(trayBed,1.14f+shift);FitPartHeight(trayBack,1.21f+shift);
            FitPartHeight(trayLeft,1.21f+shift);FitPartHeight(trayRight,1.21f+shift);
            for(int i=0;i<indicators.Count;i++)
            {
                Vector3 at=indicators[i].transform.localPosition;at.y=1.28f+shift;indicators[i].transform.localPosition=at;
                at=glyphs[i].transform.localPosition;at.y=1.28f+shift;glyphs[i].transform.localPosition=at;
                if(repeatWords.TryGetValue(glyphs[i],out var copy)){at=copy.transform.localPosition;at.y=1.28f+shift;copy.transform.localPosition=at;}
            }
        }

        public void SetOverlaysVisible(bool visible)
        {
            overlaysVisible = visible;
            foreach (Renderer renderer in overlays) if (renderer != null) renderer.enabled = visible;
        }

        public static bool IsOverlayRenderer(Renderer renderer)
        {
            var apparatus = renderer.GetComponentInParent<CompetitionApparatus>(true);
            return apparatus != null && apparatus.overlays.Contains(renderer);
        }

        public bool Fits(CompetitionStageFootprint footprint)
        {
            foreach(var mesh in solidMeshes)if(!footprint.Contains(mesh))return false;
            return true;
        }

        private Renderer Part(string name, Vector3 position, Vector3 scale, Material material,bool clearsActor=true,bool sweepsYaw=false)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var mesh=part.GetComponent<MeshFilter>();mesh.sharedMesh = box;solidMeshes.Add(mesh);
            if(clearsActor)raisedMeshes.Add(mesh);
            if(sweepsYaw)yawingMeshes.Add(mesh);
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        private TMP_Text Words(string name, Vector3 position, float width, float height, string value)
        {
            var font = UiTheme.Font(UiTheme.Weight.Regular);
            if (font == null || font.material == null || !font.material.HasProperty("_CullMode"))
                throw new InvalidOperationException("The competition readout needs a house font with front-face culling.");
            if (readoutFontMaterial == null)
            {
                // Opposing public readouts must not show each other's mirrored back faces.
                // Own the clone: the house font is also used by unrelated HUDs and actors.
                readoutFontMaterial = new Material(font.material) { name = "Competition front-face text" };
                readoutFontMaterial.SetFloat("_CullMode", (float)CullMode.Back);
                materials.Add(readoutFontMaterial);
            }
            // Set the house font before TMP awakens. Its implicit default is an unused
            // legacy font asset whose first load otherwise migrates persistent content.
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            var label = root.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSharedMaterial = readoutFontMaterial;
            label.text = value;
            label.fontSize = 1.2f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = UiTheme.Paper;
            label.enableAutoSizing = true;
            label.fontSizeMin = .45f;
            label.fontSizeMax = 1.2f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            root.SetActive(true);
            var renderer = root.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = overlaysVisible;
            overlays.Add(renderer);
            return label;
        }

        private Material Material(string name, Color color, bool lit)
        {
            Shader shader = lit ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("No competition apparatus shader is available.");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            materials.Add(material);
            return material;
        }

        private void Colorize(Renderer renderer, Color color)
        {
            tint.Clear();
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            renderer.SetPropertyBlock(tint);
            if(repeaters.TryGetValue(renderer,out var repeater))repeater.SetPropertyBlock(tint);
        }

        private void RepeatWord(TMP_Text source)
        {
            Vector3 at=source.transform.localPosition;at.x=-at.x;at.z=Front+.16f;
            TMP_Text copy=Words(source.name+" audience readout",at,source.rectTransform.sizeDelta.x,source.rectTransform.sizeDelta.y,source.text);
            copy.transform.localRotation=Quaternion.Euler(0,180,0);
            repeatWords[source]=copy;
        }

        private void SetWord(TMP_Text label,string value)
        {
            label.text=value;
            if(repeatWords.TryGetValue(label,out var copy))copy.text=value;
        }

        private static Mesh BoxMesh()
        {
            Vector3[] corners = { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
            int[][] faces = { new[]{0,3,2,1},new[]{5,6,7,4},new[]{4,7,3,0},new[]{1,2,6,5},new[]{3,7,6,2},new[]{4,0,1,5} };
            var vertices = new Vector3[24]; var triangles = new int[36]; var uv = new Vector2[24];
            Vector2[] faceUv = { Vector2.zero,Vector2.up,Vector2.one,Vector2.right };
            for (int face = 0; face < 6; face++)
            {
                for (int i = 0; i < 4; i++) { vertices[face * 4 + i] = corners[faces[face][i]]; uv[face * 4 + i] = faceUv[i]; }
                int at = face * 4, t = face * 6;
                triangles[t] = at; triangles[t+1] = at+1; triangles[t+2] = at+2;
                triangles[t+3] = at; triangles[t+4] = at+2; triangles[t+5] = at+3;
            }
            var mesh = new Mesh { name = "Competition instrument block" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.uv = uv;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
