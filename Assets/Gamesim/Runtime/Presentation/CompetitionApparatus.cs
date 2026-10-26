using System;
using System.Collections.Generic;
using System.Globalization;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using PartKind = Gamesim.Presentation.CompetitionApparatusMeshKit.PartKind;

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
        private CompetitionApparatusMeshKit meshKit;
        private TMP_Text progress;
        private Transform progressFrontBacking,progressAudienceBacking;
        private Transform weight, gripBar, gripTrack, weightGuide;
        private readonly List<Transform> gripTicks = new List<Transform>();
        private Transform leftUpright, rightUpright, topBrace, handle, responsePad;
        private Transform trayPedestal, trayBed, trayBack, trayLeft, trayRight;
        private Renderer gripRenderer;
        private float gripHeight = 1.16f;
        private float padLowest, padHighest;
        private bool overlaysVisible = true;
        private float nearestRaisedZ;
        /// <summary>
        /// Labels that read as one set and so share one font size (a Signals console's UP, RIGHT,
        /// DOWN and LEFT, both sides): the largest that fits every one of their boxes. TMP sizes each
        /// label alone, which drew the four at four sizes; <see cref="ShareLabelSize"/> sets them once.
        /// </summary>
        private readonly List<TMP_Text> sharedSizeLabels = new List<TMP_Text>();
        private bool labelSizeShared;
        private const float Front = .56f;
        /// <summary>
        /// The audience's copy of a die's value stands on its die's rear face, this far above the
        /// die's centre and this tall: the upper part of the face, from the back rail's top (5 mm
        /// under the die's centre, so it hides the face's lower half from the audience) to the die's
        /// top. It moves with the die at every pedestal fit.
        /// </summary>
        private const float DieRearRise = .04f, DieRearHeight = .09f;
        /// <summary>
        /// How far a die's two values stand off its centre, 5 mm off the faces of the .17 m die. They
        /// are placed in the die's own turn (<see cref="TurnLabelsWithDie"/>): a tumbling die's corners
        /// sweep .12 m round its centre, so a value left square on this plane was inside its own die
        /// for most of every tumble.
        /// </summary>
        private const float DieFaceOffset = .09f;
        /// <summary>
        /// The Signals response pad stands beside the direction cross, this far past RIGHT's keycap,
        /// so from the contestant's eye it covers no target and no label at any fitted height.
        /// </summary>
        private const float PadGap = .01f;
        /// <summary>
        /// How far in front of a die's centre the press aims. A rolling die turns about its centre,
        /// its corners sweeping .12 m round it (half a .17 m cube's diagonal); the pressing finger's
        /// tip reaches ~.02 m past the distal bone the pose puts on the contact; and a centimetre more.
        /// </summary>
        private const float DiePress = .15f;
        /// <summary>
        /// How far a key's letter stands off its keycap's face. The contestant's letters stand 5.5 mm
        /// off a memory or letter cap (5 mm off a signal cap); the audience's copies stand 5.5 mm off theirs.
        /// </summary>
        private const float CapGlyphGap = .0055f;

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
            => Create(anchor, definition, category, actorId, award,
                Resources.Load<CompetitionApparatusMeshKit>(CompetitionApparatusMeshKit.ResourceName));

        /// <summary>An explicit kit, including null for primitive-only previews and isolated tests.</summary>
        public static CompetitionApparatus Create(Transform anchor, CompetitionDefinition definition,
            string category, string actorId, Color award, CompetitionApparatusMeshKit kit)
        {
            var root = new GameObject("Competition apparatus · " + For(definition, category));
            root.transform.SetParent(anchor, false);
            var apparatus = root.AddComponent<CompetitionApparatus>();
            apparatus.Instrument = For(definition, category);
            apparatus.DefinitionId = definition?.Id;
            apparatus.ActorId = actorId;
            apparatus.meshKit = kit;
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
            Part(PartKind.StanceRail, "Left stance rail", new Vector3(-.30f, .016f, .02f), new Vector3(.035f, .025f, .55f), accent, clearsActor:false);
            Part(PartKind.StanceRail, "Right stance rail", new Vector3(.30f, .016f, .02f), new Vector3(.035f, .025f, .55f), accent, clearsActor:false);
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
            progressFrontBacking=Part(PartKind.ProgressBacking, "Progress front backing", new Vector3(0, progressHeight, progressFront + .006f), new Vector3(.96f, .16f, .008f), dark).transform;
            progressAudienceBacking=Part(PartKind.ProgressBacking, "Progress audience backing", new Vector3(0, progressHeight, Front + .154f), new Vector3(.96f, .16f, .008f), dark).transform;
            // The show's wide camera sees the audience-facing readout on the back of a console.
            // Repeat only the same public information, rather than turning the actor away from it.
            // A die's value is repeated on the die itself, not on a shared plane behind the tray.
            for (int i = 0; i < glyphs.Count; i++)
            {
                if (Instrument == Family.DiceTray) RepeatDieFace(i);
                else RepeatWord(glyphs[i]);
            }
            RepeatWord(progress);
            if(Instrument==Family.PairConsole || Instrument==Family.Signals || Instrument==Family.WordConsole)
                foreach(Renderer indicator in indicators)
                {
                    // Flush on the housing's rear face (Front+.04, .14 deep), as the front tiles sit on the display.
                    Vector3 at=indicator.transform.localPosition;at.x=-at.x;at.z=Front+.11f+indicator.transform.localScale.z*.5f;
                    PartKind faceKind = Instrument == Family.PairConsole ? PartKind.MemoryTile
                        : Instrument == Family.Signals ? PartKind.SignalTarget : PartKind.LetterTile;
                    var repeater=Part(faceKind,indicator.name+" audience face",at,indicator.transform.localScale,paper);
                    // An authored keycap's cap faces its part's -z, the contestant. The audience face turns
                    // like the repeated words, so its cap faces the audience; the centred box is unchanged.
                    repeater.transform.localRotation=Quaternion.Euler(0,180,0);
                    repeaters[indicator]=repeater;
                }
            if(Instrument==Family.Signals)
                foreach(TMP_Text direction in glyphs){sharedSizeLabels.Add(direction);sharedSizeLabels.Add(repeatWords[direction]);}
            ShareLabelSize();
            nearestRaisedZ=NearestRaisedSolid();
        }

        private void Console(float height, float width)
        {
            Part(PartKind.ConsoleLeg, "Console left leg", new Vector3(-width * .37f, .42f, Front + .08f), new Vector3(.07f, .84f, .14f), frame);
            Part(PartKind.ConsoleLeg, "Console right leg", new Vector3(width * .37f, .42f, Front + .08f), new Vector3(.07f, .84f, .14f), frame);
            Part(PartKind.ConsoleHousing, "Console housing", new Vector3(0, height, Front + .04f), new Vector3(width, .80f, .14f), frame);
            Part(PartKind.RecessedDisplay, "Recessed display", new Vector3(0, height, Front - .039f), new Vector3(width - .09f, .69f, .025f), dark);
            Part(PartKind.AwardHeader, "Award header", new Vector3(0, height + .38f, Front - .04f), new Vector3(width, .045f, .03f), accent);
        }

        private void BuildPairs()
        {
            Console(1.27f, .92f);
            for (int i = 0; i < 16; i++)
            {
                Vector3 at = new Vector3((i % 4 - 1.5f) * .19f, 1.28f - (i / 4 - 1.5f) * .16f, Front - .064f);
                indicators.Add(Part(PartKind.MemoryTile, "Memory tile " + i, at, new Vector3(.16f, .13f, .025f), paper));
                glyphs.Add(Words("Memory face " + i, at + Vector3.back * .018f, .14f, .11f, "?"));
            }
        }

        private void BuildSignals()
        {
            const float height = 1.20f, width = .95f;
            Console(height, width);
            Vector2[] places = { new Vector2(0, .23f), new Vector2(.25f, 0), new Vector2(0, -.23f), new Vector2(-.25f, 0) };
            string[] labels = { "UP", "RIGHT", "DOWN", "LEFT" };
            Vector3 target = new Vector3(.20f, .17f, .03f);
            for (int i = 0; i < places.Length; i++)
            {
                Vector3 at = new Vector3(places[i].x, 1.22f + places[i].y, Front - .065f);
                indicators.Add(Part(PartKind.SignalTarget, "Signal target " + labels[i], at, target, paper));
                glyphs.Add(Words("Signal direction " + labels[i], at + Vector3.back * .02f, .18f, .12f, labels[i]));
            }
            // The pad stands out of the cross, beside RIGHT (PadGap past its keycap), with its back on
            // the housing's front face, so it is fixed to the console rather than hanging in front of
            // RIGHT. FitPress keeps it at the hand's height between DOWN's top and the award header.
            Vector3 pad = new Vector3(.14f, .065f, .15f);
            float right = places[1].x + target.x * .5f;
            float housingFront = Front + .04f - .14f * .5f;
            padLowest = 1.22f + places[2].y + target.y * .5f + pad.y * .5f;
            padHighest = height + .38f - .045f * .5f - pad.y * .5f;
            responsePad=Part(PartKind.ResponsePad, "Response pad", new Vector3(right + PadGap + pad.x * .5f, 1.22f, housingFront - pad.z * .5f), pad, accent).transform;
        }

        private void BuildGrip()
        {
            leftUpright=Part(PartKind.RigUpright, "Rig left upright", new Vector3(-.42f, .78f, Front), new Vector3(.065f, 1.56f, .09f), frame).transform;
            rightUpright=Part(PartKind.RigUpright, "Rig right upright", new Vector3(.42f, .78f, Front), new Vector3(.065f, 1.56f, .09f), frame).transform;
            topBrace=Part(PartKind.RigBrace, "Rig top brace", new Vector3(0, 1.56f, Front), new Vector3(.91f, .07f, .10f), accent).transform;
            handle=Part(PartKind.GripHandle, "Grip handle", new Vector3(0, 1.16f, Front - .055f), new Vector3(.78f, .045f, .055f), accent).transform;
            gripTrack=Part(PartKind.GripTrack, "Grip scale track", new Vector3(.30f, 1.09f, Front - .075f), new Vector3(.055f, .69f, .04f), dark).transform;
            gripBar = Part(PartKind.GripRemaining, "Grip remaining", new Vector3(.30f, 1.09f, Front - .085f), new Vector3(.035f, .69f, .025f), paper).transform;
            gripRenderer=gripBar.GetComponent<Renderer>();
            weightGuide=Part(PartKind.CounterweightGuide, "Counterweight guide", new Vector3(0, 1.10f, Front + .15f), new Vector3(.03f, .76f, .03f), frame).transform;
            weight = Part(PartKind.Counterweight, "Pressure counterweight", new Vector3(0, .85f, Front + .15f), new Vector3(.25f, .16f, .17f), frame).transform;
            for (int i = 0; i < 5; i++)
                gripTicks.Add(Part(PartKind.GripTick, "Grip scale tick " + i, new Vector3(.35f, .77f + i * .16f, Front - .085f), new Vector3(.06f, .012f, .03f), accent).transform);
        }

        private void BuildDice()
        {
            trayPedestal=Part(PartKind.TrayPedestal, "Tray pedestal", new Vector3(0, .55f, Front), new Vector3(.20f, 1.10f, .25f), frame).transform;
            trayBed=Part(PartKind.TrayBed, "Dice tray bed", new Vector3(0, 1.14f, Front), new Vector3(.91f, .08f, .42f), dark).transform;
            trayBack=Part(PartKind.TrayBackRail, "Tray back rail", new Vector3(0, 1.21f, Front + .20f), new Vector3(.96f, .13f, .05f), accent).transform;
            trayLeft=Part(PartKind.TraySideRail, "Tray left rail", new Vector3(-.455f, 1.21f, Front), new Vector3(.05f, .13f, .44f), accent).transform;
            trayRight=Part(PartKind.TraySideRail, "Tray right rail", new Vector3(.455f, 1.21f, Front), new Vector3(.05f, .13f, .44f), accent).transform;
            // The dice are the paper's colour, so their values are in the instrument face's dark ink:
            // paper text on a paper die read as a blank die.
            Color ink = UiTheme.Hex("102138");
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = new Vector3((i - 1) * .25f, 1.28f, Front - .115f);
                indicators.Add(Part(PartKind.Die, "Die " + i, at, Vector3.one * .17f, paper, sweepsYaw:true));
                var face = Words("Die face " + i, at + Vector3.back * DieFaceOffset, .14f, .14f, "—");
                face.color = ink;
                glyphs.Add(face);
            }
        }

        private void BuildWords()
        {
            Console(1.22f, 1.02f);
            for (int i = 0; i < 12; i++)
            {
                Vector3 at = new Vector3((i % 6 - 2.5f) * .145f, i < 6 ? 1.30f : 1.08f, Front - .064f);
                indicators.Add(Part(PartKind.LetterTile, "Letter tile " + i, at, new Vector3(.125f, .16f, .025f), paper));
                glyphs.Add(Words("Letter " + i, at + Vector3.back * .018f, .11f, .14f, ""));
            }
            Words("Word tray label", new Vector3(0, 1.51f, Front - .07f), .85f, .12f, "LETTER TRAY");
        }

        /// <summary>Pure presentation reads. An absent attempt means another entrant's ready station.</summary>
        public void Sync(MiniGameRun run, bool started, bool preview, bool paused, bool reducedMotion)
        {
            ShareLabelSize();
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
                        TurnLabelsWithDie(i);
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
            // The press lands on the pad's top, on the quarter nearest the contestant and the cross: the
            // pad stands beside RIGHT now, and this keeps the reach what it was when it stood in front.
            if(responsePad!=null)return responsePad.TransformPoint(new Vector3(-.25f,0,-.25f))+Vector3.up*.04f;
            if(Instrument==Family.DiceTray)
            {
                // In front of the die, in the tray's own space: a point on the die's face turned with a
                // rolling die and carried the fingers into it. This one is clear of its whole sweep.
                Vector3 die=indicators[left?0:2].transform.localPosition;
                return transform.TransformPoint(new Vector3(die.x,die.y,die.z-DiePress));
            }
            return transform.TransformPoint(new Vector3(left?-.22f:.22f,.99f,Front-.12f));
        }

        /// <summary>
        /// Put the real pad/tray at the actor's hand height without moving their body. The pad keeps
        /// its place beside the cross and on the housing; only its height follows the hand, between
        /// DOWN's top and the award header.
        /// </summary>
        public void FitPress(Vector3 shoulder,float reach)
        {
            if(responsePad!=null)
            {
                Vector3 at=responsePad.localPosition;at.y=Mathf.Clamp(shoulder.y-.04f-reach*.10f,padLowest,padHighest);
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
                TurnLabelsWithDie(i);
            }
            // The roll readout stands under the tray. At the pedestal's lowest fits the tray comes
            // down past it, so the readout, its copy and both plates come down with the tray's
            // underside (1.10+shift), the plates' tops (.08 over the words' middle) a centimetre under it.
            float readout=Mathf.Min(.74f,1.10f+shift-.09f);
            FitPartHeight(progress.transform,readout);
            if(repeatWords.TryGetValue(progress,out var audience))FitPartHeight(audience.transform,readout);
            FitPartHeight(progressFrontBacking,readout);FitPartHeight(progressAudienceBacking,readout);
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

        private Renderer Part(PartKind kind, string name, Vector3 position, Vector3 scale, Material material,bool clearsActor=true,bool sweepsYaw=false)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var mesh=part.GetComponent<MeshFilter>();
            // Only geometry is borrowed. The original named transform, live material and all
            // clearance registries remain the same; shared assets never enter owned disposal.
            mesh.sharedMesh = meshKit != null && meshKit.TryGetMesh(Instrument, kind, scale, out var authored) ? authored : box;
            solidMeshes.Add(mesh);
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

        /// <summary>
        /// The audience's copy of <paramref name="source"/>, mirrored in x and turned to the audience.
        /// A console's key letter stands on its audience keycap's face as the contestant's stands on
        /// theirs (<see cref="CapGlyphGap"/> off it): the cap sits flush on the housing's rear face
        /// (Front+.11) and is as deep as its contestant twin. Anything else - the progress readout -
        /// stays on the shared plane in front of its backing (Front+.154).
        /// </summary>
        private void RepeatWord(TMP_Text source)
        {
            Vector3 at=source.transform.localPosition;at.x=-at.x;at.z=Front+.16f;
            int key=glyphs.IndexOf(source);
            if((Instrument==Family.PairConsole || Instrument==Family.Signals || Instrument==Family.WordConsole) && key>=0 && key<indicators.Count)
                at.z=Front+.11f+indicators[key].transform.localScale.z+CapGlyphGap;
            TMP_Text copy=Words(source.name+" audience readout",at,source.rectTransform.sizeDelta.x,source.rectTransform.sizeDelta.y,source.text);
            copy.transform.localRotation=Quaternion.Euler(0,180,0);
            repeatWords[source]=copy;
        }

        /// <summary>
        /// The audience's copy of die <paramref name="i"/>'s value, in its ink, on that die's own rear
        /// face and turned to the audience. It stays on its die - the audience sees the dice in the
        /// other order, and each value on the die it belongs to - in the face's part above the back
        /// rail (<see cref="DieRearRise"/>).
        /// </summary>
        private void RepeatDieFace(int i)
        {
            Vector3 die=indicators[i].transform.localPosition;
            TMP_Text source=glyphs[i];
            TMP_Text copy=Words(source.name+" audience readout",new Vector3(die.x,die.y+DieRearRise,die.z+DieFaceOffset),
                source.rectTransform.sizeDelta.x,DieRearHeight,source.text);
            copy.color=source.color;
            copy.transform.localRotation=Quaternion.Euler(0,180,0);
            repeatWords[source]=copy;
        }

        /// <summary>
        /// Stands die <paramref name="i"/>'s two values on their faces as the die is turned now: the
        /// contestant's <see cref="DieFaceOffset"/> in front of its centre, the audience's behind it and
        /// <see cref="DieRearRise"/> up, each turned with the die. A tumbling die carries both round with
        /// it rather than sweeping its corners through them; a square die puts them back on the planes
        /// they always stood on. The labels stay children of the instrument, under their own names.
        /// </summary>
        private void TurnLabelsWithDie(int i)
        {
            Transform die=indicators[i].transform;
            Quaternion turn=die.localRotation;Vector3 centre=die.localPosition;
            glyphs[i].transform.SetLocalPositionAndRotation(centre+turn*(Vector3.back*DieFaceOffset),turn);
            if(repeatWords.TryGetValue(glyphs[i],out var copy))
                copy.transform.SetLocalPositionAndRotation(centre+turn*new Vector3(0,DieRearRise,DieFaceOffset),turn*Quaternion.Euler(0,180,0));
        }

        /// <summary>
        /// Gives every label in <see cref="sharedSizeLabels"/> one font size, the smallest of the sizes
        /// TMP's own auto-size gives each in its box - the largest size that fits them all - and turns
        /// their auto-size off. It needs TMP to have woken the labels and built their text: a console
        /// built under an inactive anchor shares on its first read once they have (Build, then Sync).
        /// </summary>
        private void ShareLabelSize()
        {
            if(labelSizeShared || sharedSizeLabels.Count==0)return;
            float size=float.PositiveInfinity;
            foreach(TMP_Text label in sharedSizeLabels)
            {
                label.ForceMeshUpdate(true);
                if(label.textInfo==null || label.textInfo.characterCount==0)return;
                size=Mathf.Min(size,label.fontSize);
            }
            foreach(TMP_Text label in sharedSizeLabels){label.enableAutoSizing=false;label.fontSize=size;}
            labelSizeShared=true;
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
