using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamesim.House
{
    /// <summary>
    /// The game's controls as one Input Actions asset (MASTER-PLAN §3.E Phase 3; PLAN A, A1), so
    /// that a mouse, a keyboard and a gamepad all reach the house, the ceremony cards, the
    /// competitions and the season report through the same actions, and a test drives any of them
    /// by queueing device state. Six maps: the camera, the house's shortcuts, the ceremony cards,
    /// the competitions, the season report, and the prototype house's dialogue.
    ///
    /// <para>The code here is the truth. <see cref="BuildAsset"/> is what the rig uses when nothing
    /// is assigned, and "Gamesim/U07/Export the camera actions" writes the same asset to
    /// <c>Assets/Gamesim/Input/HouseCamera.inputactions</c> for anyone who would rather rebind in
    /// the editor; an asset assigned on the rig wins, and must carry every map with these action
    /// names. Everything other than the rig and the director reads the asset through
    /// <see cref="HouseInput"/>, which hands out the rig's when one is up.</para>
    ///
    /// <para>Two units, kept apart on purpose. A mouse drag and the wheel arrive as amounts - pixels
    /// this frame, notches this frame - and a stick or trigger arrives as a rate to be multiplied by
    /// the frame's time. Folding them onto one action would make the stick's speed depend on the
    /// frame rate, so the orbit and the zoom each have an amount action and a rate action.</para>
    /// </summary>
    public sealed class HouseCameraActions : IDisposable
    {
        public const string MapName = "Camera";
        /// <summary>The house's shortcuts, a second map in the same asset: what the director reads.</summary>
        public const string ShortcutsMapName = "Shortcuts";
        /// <summary>The ceremony cards: skip, speed up, and the opening's Space.</summary>
        public const string CeremonyMapName = "Ceremony";
        /// <summary>A competition's board and its results card.</summary>
        public const string CompetitionMapName = "Competition";
        /// <summary>The season report's scrolling.</summary>
        public const string ReportMapName = "Report";
        /// <summary>The prototype house's conversation (HousePrototype only; the episode has its own panels).</summary>
        public const string DialogueMapName = "Dialogue";
        public const string KeyboardMouseScheme = "Keyboard&Mouse";
        public const string GamepadScheme = "Gamepad";

        /// <summary>Right-drag: pixels this frame.</summary>
        public InputAction Orbit { get; }
        /// <summary>Right stick: a rate, in [-1, 1] per axis.</summary>
        public InputAction OrbitRate { get; }
        /// <summary>WASD, the arrows or the left stick: a direction, at most unit length.</summary>
        public InputAction Pan { get; }
        /// <summary>Middle-drag: pixels this frame; the ground under the cursor stays under it.</summary>
        public InputAction Drag { get; }
        /// <summary>The wheel: raw scroll this frame (120 a notch on Windows, about 1 elsewhere).</summary>
        public InputAction Zoom { get; }
        /// <summary>Triggers, or - and =: a rate in [-1, 1], positive in.</summary>
        public InputAction ZoomRate { get; }
        /// <summary>F, or the right stick pressed: back to the player.</summary>
        public InputAction Recenter { get; }
        /// <summary>The pointer's position, for zooming toward it and for the screen's edges.</summary>
        public InputAction Point { get; }
        /// <summary>
        /// The right shoulder, ], or Tab: follow the next houseguest (the director reads it). Tab
        /// only with no HUD control focused - with one focused, Tab walks the HUD's ring
        /// (<see cref="HouseInput.TabStep"/>).
        /// </summary>
        public InputAction Next { get; }
        /// <summary>The left shoulder, [, or Shift+Tab: the previous one.</summary>
        public InputAction Previous { get; }

        // The Shortcuts map (§3.D controller navigation): each of the director's keys with a
        // gamepad button beside it, so a controller reaches every panel the keyboard does.
        /// <summary>Escape, or Start: close the top panel, or open the settings when none is open.</summary>
        public InputAction Menu { get; }
        /// <summary>J, or Select: the notebook.</summary>
        public InputAction Notebook { get; }
        /// <summary>F5, or the left stick pressed: save now.</summary>
        public InputAction Save { get; }
        /// <summary>R, or North: walk to the diary room.</summary>
        public InputAction Diary { get; }
        /// <summary>E, or West: talk, enter the diary, or open the episode screen.</summary>
        public InputAction Interact { get; }
        /// <summary>Space, or South: a competition's tap or hold.</summary>
        public InputAction Hit { get; }
        /// <summary>M, or the d-pad up: the overview of the whole house, and back.</summary>
        public InputAction Overview { get; }
        /// <summary>G, or the d-pad right: your moves, the card over your own chip.</summary>
        public InputAction Emote { get; }
        /// <summary>
        /// The pad's B: back out of the top panel, by the same chain as <see cref="Menu"/>, and
        /// nothing at all with nothing open - where Start is the pause menu (PLAN A, A2). The
        /// keyboard's back is Escape, which is <see cref="Menu"/>.
        /// </summary>
        public InputAction Back { get; }

        // The Ceremony map: what every ceremony card reads, never through the event system, so a
        // card cannot take a click or a Submit meant for the house (CeremonyTakeover's notes).
        /// <summary>Enter, Num Enter, Escape or a left click; the pad's A or B: skip a reveal, or close the card.</summary>
        public InputAction Skip { get; }
        /// <summary>Space, or the pad's X: speed a reveal up, or back to its own pace.</summary>
        public InputAction Speed { get; }
        /// <summary>Space: move the opening on (a pad presses its focused Continue).</summary>
        public InputAction Advance { get; }

        // The Competition map: a board's own keys, and its results card's.
        /// <summary>Up or W, the d-pad or the left stick up: a direction game's fresh press.</summary>
        public InputAction CompetitionUp { get; }
        public InputAction CompetitionRight { get; }
        public InputAction CompetitionDown { get; }
        public InputAction CompetitionLeft { get; }
        /// <summary>Space: tap the classic reaction game's target.</summary>
        public InputAction CompetitionHit { get; }
        /// <summary>Space or the right trigger, read as a level: hold on in the endurance game.</summary>
        public InputAction CompetitionHold { get; }
        /// <summary>P, or Start: pause and resume (the word board spells P while it is played).</summary>
        public InputAction CompetitionPause { get; }
        /// <summary>Escape, or B: leave the board, or step back from the results' details.</summary>
        public InputAction CompetitionBack { get; }
        /// <summary>Backspace, or X: take the word board's last letter back.</summary>
        public InputAction CompetitionUndo { get; }
        /// <summary>Tab, or the right shoulder: the board's next control.</summary>
        public InputAction CompetitionFocusNext { get; }
        /// <summary>Shift+Tab, or the left shoulder: the board's previous control.</summary>
        public InputAction CompetitionFocusPrevious { get; }
        /// <summary>Enter, or A: continue from the results card.</summary>
        public InputAction CompetitionConfirm { get; }

        // The Report map: the season report's reading keys.
        /// <summary>Page Up, or the left shoulder.</summary>
        public InputAction ReportPageUp { get; }
        /// <summary>Page Down, or the right shoulder.</summary>
        public InputAction ReportPageDown { get; }
        /// <summary>Home, or the left trigger: the top of the season.</summary>
        public InputAction ReportHome { get; }
        /// <summary>End, or the right trigger: its foot.</summary>
        public InputAction ReportEnd { get; }
        /// <summary>The right stick: a lean that scrolls.</summary>
        public InputAction ReportScroll { get; }
        /// <summary>The d-pad or the left stick: walking the report's ring, which stops the stick scrolling.</summary>
        public InputAction ReportWalk { get; }

        // The Dialogue map: the prototype house's numbered replies.
        public InputAction Reply1 { get; }
        public InputAction Reply2 { get; }
        public InputAction Reply3 { get; }

        public InputActionAsset Asset { get; }
        private readonly bool ownsAsset;
        private bool disposed;

        /// <summary>
        /// Wraps the "Camera" map of <paramref name="asset"/>, or builds the default asset when it
        /// is null. A map that lacks one of the actions throws, so a hand-edited asset fails at the
        /// rig's first frame rather than leaving a control silently dead.
        /// </summary>
        public HouseCameraActions(InputActionAsset asset = null)
        {
            ownsAsset = asset == null;
            Asset = asset != null ? asset : BuildAsset();
            var map = Asset.FindActionMap(MapName, throwIfNotFound: true);
            Orbit = map.FindAction(nameof(Orbit), throwIfNotFound: true);
            OrbitRate = map.FindAction(nameof(OrbitRate), throwIfNotFound: true);
            Pan = map.FindAction(nameof(Pan), throwIfNotFound: true);
            Drag = map.FindAction(nameof(Drag), throwIfNotFound: true);
            Zoom = map.FindAction(nameof(Zoom), throwIfNotFound: true);
            ZoomRate = map.FindAction(nameof(ZoomRate), throwIfNotFound: true);
            Recenter = map.FindAction(nameof(Recenter), throwIfNotFound: true);
            Point = map.FindAction(nameof(Point), throwIfNotFound: true);
            Next = map.FindAction(nameof(Next), throwIfNotFound: true);
            Previous = map.FindAction(nameof(Previous), throwIfNotFound: true);
            var shortcuts = Asset.FindActionMap(ShortcutsMapName, throwIfNotFound: true);
            Menu = shortcuts.FindAction(nameof(Menu), throwIfNotFound: true);
            Notebook = shortcuts.FindAction(nameof(Notebook), throwIfNotFound: true);
            Save = shortcuts.FindAction(nameof(Save), throwIfNotFound: true);
            Diary = shortcuts.FindAction(nameof(Diary), throwIfNotFound: true);
            Interact = shortcuts.FindAction(nameof(Interact), throwIfNotFound: true);
            Hit = shortcuts.FindAction(nameof(Hit), throwIfNotFound: true);
            Overview = shortcuts.FindAction(nameof(Overview), throwIfNotFound: true);
            Emote = shortcuts.FindAction(nameof(Emote), throwIfNotFound: true);
            Back = shortcuts.FindAction(nameof(Back), throwIfNotFound: true);
            var ceremony = Asset.FindActionMap(CeremonyMapName, throwIfNotFound: true);
            Skip = ceremony.FindAction(nameof(Skip), throwIfNotFound: true);
            Speed = ceremony.FindAction(nameof(Speed), throwIfNotFound: true);
            Advance = ceremony.FindAction(nameof(Advance), throwIfNotFound: true);
            var competition = Asset.FindActionMap(CompetitionMapName, throwIfNotFound: true);
            CompetitionUp = competition.FindAction("Up", throwIfNotFound: true);
            CompetitionRight = competition.FindAction("Right", throwIfNotFound: true);
            CompetitionDown = competition.FindAction("Down", throwIfNotFound: true);
            CompetitionLeft = competition.FindAction("Left", throwIfNotFound: true);
            CompetitionHit = competition.FindAction("Hit", throwIfNotFound: true);
            CompetitionHold = competition.FindAction("Hold", throwIfNotFound: true);
            CompetitionPause = competition.FindAction("Pause", throwIfNotFound: true);
            CompetitionBack = competition.FindAction("Back", throwIfNotFound: true);
            CompetitionUndo = competition.FindAction("Undo", throwIfNotFound: true);
            CompetitionFocusNext = competition.FindAction("FocusNext", throwIfNotFound: true);
            CompetitionFocusPrevious = competition.FindAction("FocusPrevious", throwIfNotFound: true);
            CompetitionConfirm = competition.FindAction("Confirm", throwIfNotFound: true);
            var report = Asset.FindActionMap(ReportMapName, throwIfNotFound: true);
            ReportPageUp = report.FindAction("PageUp", throwIfNotFound: true);
            ReportPageDown = report.FindAction("PageDown", throwIfNotFound: true);
            ReportHome = report.FindAction("Home", throwIfNotFound: true);
            ReportEnd = report.FindAction("End", throwIfNotFound: true);
            ReportScroll = report.FindAction("Scroll", throwIfNotFound: true);
            ReportWalk = report.FindAction("Walk", throwIfNotFound: true);
            var dialogue = Asset.FindActionMap(DialogueMapName, throwIfNotFound: true);
            Reply1 = dialogue.FindAction(nameof(Reply1), throwIfNotFound: true);
            Reply2 = dialogue.FindAction(nameof(Reply2), throwIfNotFound: true);
            Reply3 = dialogue.FindAction(nameof(Reply3), throwIfNotFound: true);
        }

        public void Enable() { if (!disposed) Asset.Enable(); }
        public void Disable() { if (!disposed && Asset != null) Asset.Disable(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Asset == null) return;
            Asset.Disable();
            if (ownsAsset)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Asset);
                else UnityEngine.Object.DestroyImmediate(Asset);
            }
        }

        /// <summary>The default asset, built in code: the six maps of <see cref="Maps"/>, two control schemes.</summary>
        public static InputActionAsset BuildAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "HouseCamera";
            var map = asset.AddActionMap(MapName);
            const string km = KeyboardMouseScheme;
            const string pad = GamepadScheme;

            var orbit = map.AddAction(nameof(Orbit), InputActionType.Value, expectedControlLayout: "Vector2");
            orbit.AddCompositeBinding("OneModifier")
                .With("Modifier", "<Mouse>/rightButton", km)
                .With("Binding", "<Mouse>/delta", km);

            var orbitRate = map.AddAction(nameof(OrbitRate), InputActionType.Value, expectedControlLayout: "Vector2");
            orbitRate.AddBinding("<Gamepad>/rightStick", groups: pad);
            // The keyboard's turn (PLAN A, A7; the lead's decision 6): Q left and C right, a rate like
            // the stick, so a player without a mouse's right button can turn the camera. E is Interact.
            orbitRate.AddCompositeBinding("2DVector")
                .With("Left", "<Keyboard>/q", km)
                .With("Right", "<Keyboard>/c", km);

            var pan = map.AddAction(nameof(Pan), InputActionType.Value, expectedControlLayout: "Vector2");
            pan.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", km).With("Down", "<Keyboard>/s", km)
                .With("Left", "<Keyboard>/a", km).With("Right", "<Keyboard>/d", km);
            pan.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", km).With("Down", "<Keyboard>/downArrow", km)
                .With("Left", "<Keyboard>/leftArrow", km).With("Right", "<Keyboard>/rightArrow", km);
            pan.AddBinding("<Gamepad>/leftStick", groups: pad);

            var drag = map.AddAction(nameof(Drag), InputActionType.Value, expectedControlLayout: "Vector2");
            drag.AddCompositeBinding("OneModifier")
                .With("Modifier", "<Mouse>/middleButton", km)
                .With("Binding", "<Mouse>/delta", km);

            var zoom = map.AddAction(nameof(Zoom), InputActionType.Value, expectedControlLayout: "Axis");
            zoom.AddBinding("<Mouse>/scroll/y", groups: km);

            var zoomRate = map.AddAction(nameof(ZoomRate), InputActionType.Value, expectedControlLayout: "Axis");
            zoomRate.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftTrigger", pad)
                .With("Positive", "<Gamepad>/rightTrigger", pad);
            zoomRate.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/minus", km)
                .With("Positive", "<Keyboard>/equals", km);

            var recenter = map.AddAction(nameof(Recenter), InputActionType.Button);
            recenter.AddBinding("<Keyboard>/f", groups: km);
            recenter.AddBinding("<Gamepad>/rightStickPress", groups: pad);

            var point = map.AddAction(nameof(Point), InputActionType.Value, expectedControlLayout: "Vector2");
            point.AddBinding("<Pointer>/position", groups: km);

            var next = map.AddAction(nameof(Next), InputActionType.Button);
            next.AddBinding("<Gamepad>/rightShoulder", groups: pad);
            next.AddBinding("<Keyboard>/rightBracket", groups: km);
            // Tab, folded in from the director's own read (PLAN A, A1): the director follows on it
            // only with nothing focused, and the HUD's ring steps on it otherwise.
            next.AddBinding("<Keyboard>/tab", groups: km);
            var previous = map.AddAction(nameof(Previous), InputActionType.Button);
            previous.AddBinding("<Gamepad>/leftShoulder", groups: pad);
            previous.AddBinding("<Keyboard>/leftBracket", groups: km);
            previous.AddCompositeBinding("OneModifier")
                .With("Modifier", "<Keyboard>/shift", km)
                .With("Binding", "<Keyboard>/tab", km);

            var shortcuts = asset.AddActionMap(ShortcutsMapName);
            var menu = shortcuts.AddAction(nameof(Menu), InputActionType.Button);
            menu.AddBinding("<Keyboard>/escape", groups: km);
            menu.AddBinding("<Gamepad>/start", groups: pad);
            var notebook = shortcuts.AddAction(nameof(Notebook), InputActionType.Button);
            notebook.AddBinding("<Keyboard>/j", groups: km);
            notebook.AddBinding("<Gamepad>/select", groups: pad);
            var save = shortcuts.AddAction(nameof(Save), InputActionType.Button);
            save.AddBinding("<Keyboard>/f5", groups: km);
            save.AddBinding("<Gamepad>/leftStickPress", groups: pad);
            var diary = shortcuts.AddAction(nameof(Diary), InputActionType.Button);
            diary.AddBinding("<Keyboard>/r", groups: km);
            diary.AddBinding("<Gamepad>/buttonNorth", groups: pad);
            var interact = shortcuts.AddAction(nameof(Interact), InputActionType.Button);
            interact.AddBinding("<Keyboard>/e", groups: km);
            interact.AddBinding("<Gamepad>/buttonWest", groups: pad);
            var hit = shortcuts.AddAction(nameof(Hit), InputActionType.Button);
            hit.AddBinding("<Keyboard>/space", groups: km);
            hit.AddBinding("<Gamepad>/buttonSouth", groups: pad);
            var overview = shortcuts.AddAction(nameof(Overview), InputActionType.Button);
            overview.AddBinding("<Keyboard>/m", groups: km);
            overview.AddBinding("<Gamepad>/dpad/up", groups: pad);
            // The d-pad's right, which the HUD's rings never use: they clear Left and Right on
            // every control, so the press cannot also walk a panel underneath the card.
            var emote = shortcuts.AddAction(nameof(Emote), InputActionType.Button);
            emote.AddBinding("<Keyboard>/g", groups: km);
            emote.AddBinding("<Gamepad>/dpad/right", groups: pad);
            // B, apart from Menu: the pad's way back out of a panel. No key - Escape is Menu, and a
            // second action on it would be a second answer to one press.
            var shortcutBack = shortcuts.AddAction(nameof(Back), InputActionType.Button);
            shortcutBack.AddBinding("<Gamepad>/buttonEast", groups: pad);

            var ceremony = asset.AddActionMap(CeremonyMapName);
            var skip = ceremony.AddAction(nameof(Skip), InputActionType.Button);
            skip.AddBinding("<Keyboard>/enter", groups: km);
            skip.AddBinding("<Keyboard>/numpadEnter", groups: km);
            skip.AddBinding("<Keyboard>/escape", groups: km);
            skip.AddBinding("<Mouse>/leftButton", groups: km);
            skip.AddBinding("<Gamepad>/buttonSouth", groups: pad);
            skip.AddBinding("<Gamepad>/buttonEast", groups: pad);
            var speed = ceremony.AddAction(nameof(Speed), InputActionType.Button);
            speed.AddBinding("<Keyboard>/space", groups: km);
            speed.AddBinding("<Gamepad>/buttonWest", groups: pad);
            var advance = ceremony.AddAction(nameof(Advance), InputActionType.Button);
            advance.AddBinding("<Keyboard>/space", groups: km);

            var competition = asset.AddActionMap(CompetitionMapName);
            AddDirection(competition, "Up", "upArrow", "w", "up");
            AddDirection(competition, "Right", "rightArrow", "d", "right");
            AddDirection(competition, "Down", "downArrow", "s", "down");
            AddDirection(competition, "Left", "leftArrow", "a", "left");
            var competitionHit = competition.AddAction("Hit", InputActionType.Button);
            competitionHit.AddBinding("<Keyboard>/space", groups: km);
            var hold = competition.AddAction("Hold", InputActionType.Button);
            hold.AddBinding("<Keyboard>/space", groups: km);
            hold.AddBinding("<Gamepad>/rightTrigger", groups: pad);
            var pause = competition.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/p", groups: km);
            pause.AddBinding("<Gamepad>/start", groups: pad);
            var back = competition.AddAction("Back", InputActionType.Button);
            back.AddBinding("<Keyboard>/escape", groups: km);
            back.AddBinding("<Gamepad>/buttonEast", groups: pad);
            var undo = competition.AddAction("Undo", InputActionType.Button);
            undo.AddBinding("<Keyboard>/backspace", groups: km);
            undo.AddBinding("<Gamepad>/buttonWest", groups: pad);
            var focusNext = competition.AddAction("FocusNext", InputActionType.Button);
            focusNext.AddBinding("<Keyboard>/tab", groups: km);
            focusNext.AddBinding("<Gamepad>/rightShoulder", groups: pad);
            var focusPrevious = competition.AddAction("FocusPrevious", InputActionType.Button);
            focusPrevious.AddCompositeBinding("OneModifier")
                .With("Modifier", "<Keyboard>/shift", km)
                .With("Binding", "<Keyboard>/tab", km);
            focusPrevious.AddBinding("<Gamepad>/leftShoulder", groups: pad);
            var confirm = competition.AddAction("Confirm", InputActionType.Button);
            confirm.AddBinding("<Keyboard>/enter", groups: km);
            confirm.AddBinding("<Gamepad>/buttonSouth", groups: pad);

            var report = asset.AddActionMap(ReportMapName);
            var pageUp = report.AddAction("PageUp", InputActionType.Button);
            pageUp.AddBinding("<Keyboard>/pageUp", groups: km);
            pageUp.AddBinding("<Gamepad>/leftShoulder", groups: pad);
            var pageDown = report.AddAction("PageDown", InputActionType.Button);
            pageDown.AddBinding("<Keyboard>/pageDown", groups: km);
            pageDown.AddBinding("<Gamepad>/rightShoulder", groups: pad);
            var home = report.AddAction("Home", InputActionType.Button);
            home.AddBinding("<Keyboard>/home", groups: km);
            home.AddBinding("<Gamepad>/leftTrigger", groups: pad);
            var end = report.AddAction("End", InputActionType.Button);
            end.AddBinding("<Keyboard>/end", groups: km);
            end.AddBinding("<Gamepad>/rightTrigger", groups: pad);
            var scroll = report.AddAction("Scroll", InputActionType.Value, expectedControlLayout: "Vector2");
            scroll.AddBinding("<Gamepad>/rightStick", groups: pad);
            var walk = report.AddAction("Walk", InputActionType.Value, expectedControlLayout: "Vector2");
            walk.AddBinding("<Gamepad>/dpad", groups: pad);
            walk.AddBinding("<Gamepad>/leftStick", groups: pad);

            var dialogue = asset.AddActionMap(DialogueMapName);
            for (var reply = 1; reply <= 3; reply++)
            {
                var action = dialogue.AddAction("Reply" + reply, InputActionType.Button);
                action.AddBinding("<Keyboard>/" + reply, groups: km);
                action.AddBinding("<Keyboard>/numpad" + reply, groups: km);
            }

            asset.AddControlScheme(km).WithRequiredDevice<Keyboard>().WithRequiredDevice<Mouse>();
            asset.AddControlScheme(pad).WithRequiredDevice<Gamepad>();
            return asset;
        }

        /// <summary>One of a direction game's four: an arrow and a letter, the d-pad and the left stick, each a fresh press.</summary>
        private static void AddDirection(InputActionMap map, string name, string arrow, string letter, string pad)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding("<Keyboard>/" + arrow, groups: KeyboardMouseScheme);
            action.AddBinding("<Keyboard>/" + letter, groups: KeyboardMouseScheme);
            action.AddBinding("<Gamepad>/dpad/" + pad, groups: GamepadScheme);
            action.AddBinding("<Gamepad>/leftStick/" + pad, groups: GamepadScheme);
        }

        /// <summary>
        /// What an action is bound to in one control scheme, in the glossary's words: each binding
        /// and each composite once, joined ("WASD / Arrows", "LT / RT"), empty when the scheme has
        /// none. Read off the bindings as they stand, so the controls page says what the map does.
        /// </summary>
        public static string BindingWords(InputAction action, string scheme)
        {
            if (action == null) return "";
            var words = new System.Collections.Generic.List<string>();
            var bindings = action.bindings;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding.isComposite)
                {
                    var parts = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
                    bool inScheme = false;
                    int part = index + 1;
                    for (; part < bindings.Count && bindings[part].isPartOfComposite; part++)
                    {
                        parts.Add(new System.Collections.Generic.KeyValuePair<string, string>(bindings[part].name, bindings[part].effectivePath));
                        if (InScheme(bindings[part], scheme)) inScheme = true;
                    }
                    string composite = binding.path ?? "";
                    int parameters = composite.IndexOf('(');
                    if (parameters >= 0) composite = composite.Substring(0, parameters);
                    if (inScheme) words.Add(Gamesim.Presentation.InputGlossary.CompositeLabel(composite, parts));
                    index = part - 1;
                }
                else if (!binding.isPartOfComposite && InScheme(binding, scheme))
                    words.Add(Gamesim.Presentation.InputGlossary.BindingLabel(binding.effectivePath));
            }
            return Gamesim.Presentation.InputGlossary.Join(words);
        }

        private static bool InScheme(InputBinding binding, string scheme)
        {
            foreach (var group in (binding.groups ?? "").Split(';'))
                if (group == scheme) return true;
            return false;
        }

        /// <summary>The shortcut names, in the map's order.</summary>
        public static readonly string[] ShortcutNames =
        {
            nameof(Menu), nameof(Notebook), nameof(Save), nameof(Diary), nameof(Interact), nameof(Hit), nameof(Overview),
            nameof(Emote), nameof(Back),
        };

        /// <summary>The ten action names, in the map's order, for the export and its test.</summary>
        public static readonly string[] ActionNames =
        {
            nameof(Orbit), nameof(OrbitRate), nameof(Pan), nameof(Drag), nameof(Zoom), nameof(ZoomRate),
            nameof(Recenter), nameof(Point), nameof(Next), nameof(Previous),
        };

        /// <summary>The ceremony cards' actions, in the map's order.</summary>
        public static readonly string[] CeremonyNames = { nameof(Skip), nameof(Speed), nameof(Advance) };

        /// <summary>A competition's actions, in the map's order.</summary>
        public static readonly string[] CompetitionNames =
        {
            "Up", "Right", "Down", "Left", "Hit", "Hold", "Pause", "Back", "Undo", "FocusNext", "FocusPrevious", "Confirm",
        };

        /// <summary>The season report's actions, in the map's order.</summary>
        public static readonly string[] ReportNames = { "PageUp", "PageDown", "Home", "End", "Scroll", "Walk" };

        /// <summary>The prototype house's replies, in the map's order.</summary>
        public static readonly string[] DialogueNames = { nameof(Reply1), nameof(Reply2), nameof(Reply3) };

        /// <summary>Every map in the asset, in its order, with its actions' names.</summary>
        public static System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string[]>> Maps { get; } = new[]
        {
            new System.Collections.Generic.KeyValuePair<string, string[]>(MapName, ActionNames),
            new System.Collections.Generic.KeyValuePair<string, string[]>(ShortcutsMapName, ShortcutNames),
            new System.Collections.Generic.KeyValuePair<string, string[]>(CeremonyMapName, CeremonyNames),
            new System.Collections.Generic.KeyValuePair<string, string[]>(CompetitionMapName, CompetitionNames),
            new System.Collections.Generic.KeyValuePair<string, string[]>(ReportMapName, ReportNames),
            new System.Collections.Generic.KeyValuePair<string, string[]>(DialogueMapName, DialogueNames),
        };
    }
}
