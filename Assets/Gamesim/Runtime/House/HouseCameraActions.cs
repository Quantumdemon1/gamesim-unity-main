using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamesim.House
{
    /// <summary>
    /// The camera's controls as an Input Actions map (MASTER-PLAN §3.E Phase 3), so that a mouse,
    /// a keyboard and a gamepad all reach the rig through the same ten actions, and a test drives
    /// any of them by queueing device state.
    ///
    /// <para>The code here is the truth. <see cref="BuildAsset"/> is what the rig uses when nothing
    /// is assigned, and "Gamesim/U07/Export the camera actions" writes the same map to
    /// <c>Assets/Gamesim/Input/HouseCamera.inputactions</c> for anyone who would rather rebind in
    /// the editor; an asset assigned on the rig wins, and must carry a "Camera" map with these
    /// action names.</para>
    ///
    /// <para>Two units, kept apart on purpose. A mouse drag and the wheel arrive as amounts - pixels
    /// this frame, notches this frame - and a stick or trigger arrives as a rate to be multiplied by
    /// the frame's time. Folding them onto one action would make the stick's speed depend on the
    /// frame rate, so the orbit and the zoom each have an amount action and a rate action.</para>
    /// </summary>
    public sealed class HouseCameraActions : IDisposable
    {
        public const string MapName = "Camera";
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
        /// <summary>The right shoulder, or ]: follow the next houseguest (the director reads it).</summary>
        public InputAction Next { get; }
        /// <summary>The left shoulder, or [: the previous one.</summary>
        public InputAction Previous { get; }

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
        }

        public void Enable() { if (!disposed) Asset.FindActionMap(MapName).Enable(); }
        public void Disable() { if (!disposed && Asset != null) Asset.FindActionMap(MapName).Disable(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Asset == null) return;
            Asset.FindActionMap(MapName)?.Disable();
            if (ownsAsset)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Asset);
                else UnityEngine.Object.DestroyImmediate(Asset);
            }
        }

        /// <summary>The default map, built in code: one map, ten actions, two control schemes.</summary>
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
            var previous = map.AddAction(nameof(Previous), InputActionType.Button);
            previous.AddBinding("<Gamepad>/leftShoulder", groups: pad);
            previous.AddBinding("<Keyboard>/leftBracket", groups: km);

            asset.AddControlScheme(km).WithRequiredDevice<Keyboard>().WithRequiredDevice<Mouse>();
            asset.AddControlScheme(pad).WithRequiredDevice<Gamepad>();
            return asset;
        }

        /// <summary>The ten action names, in the map's order, for the export and its test.</summary>
        public static readonly string[] ActionNames =
        {
            nameof(Orbit), nameof(OrbitRate), nameof(Pan), nameof(Drag), nameof(Zoom), nameof(ZoomRate),
            nameof(Recenter), nameof(Point), nameof(Next), nameof(Previous),
        };
    }
}
