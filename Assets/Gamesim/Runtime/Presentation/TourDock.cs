using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Where the tour's card stands, and how it is laid out inside: the arithmetic, with no engine,
    /// so the Unity-free suite checks every step at every text size on both screen shapes.
    ///
    /// <para>The card docks BESIDE the thing its step names, which glows, and never over it. It
    /// tries the reference build's side first (left of what stands against the right edge, right
    /// of what stands against the left, under the top bar, over the floor), then the other sides,
    /// largest room first. Beside a target the card may narrow, down to where its title still sits
    /// on one line and its buttons still fit; over or under one it may widen, so its words take
    /// fewer lines. Only when no side has the room does it fall back to the reference's corner,
    /// clamped into the frame, and say so (<see cref="Placement.Docked"/> false).</para>
    ///
    /// <para>Units are the tour canvas's own (1600x900 at 16:9), y up, a box by its lower-left
    /// corner.</para>
    /// </summary>
    public static class TourDock
    {
        /// <summary>A rectangle by its lower-left corner and its size, y up.</summary>
        public readonly struct Box
        {
            public readonly float X, Y, W, H;
            public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
            public float XMax => X + W;
            public float YMax => Y + H;
            public float CentreX => X + W * .5f;
            public float CentreY => Y + H * .5f;

            public static Box MinMax(float xMin, float yMin, float xMax, float yMax) => new Box(xMin, yMin, xMax - xMin, yMax - yMin);

            /// <summary>Whether the two share any area (touching edges do not).</summary>
            public bool Overlaps(Box other) =>
                Math.Min(XMax, other.XMax) - Math.Max(X, other.X) > .01f && Math.Min(YMax, other.YMax) - Math.Max(Y, other.Y) > .01f;

            /// <summary>Whether this box lies within <paramref name="outer"/>, to a hundredth of a unit.</summary>
            public bool Within(Box outer) =>
                X >= outer.X - .01f && Y >= outer.Y - .01f && XMax <= outer.XMax + .01f && YMax <= outer.YMax + .01f;

            public override string ToString() => "(" + X.ToString("0.#") + ", " + Y.ToString("0.#") + ", " + W.ToString("0.#") + " x " + H.ToString("0.#") + ")";
        }

        public enum Side { Centre, LeftOf, RightOf, Below, Above }

        /// <summary>Where the card goes: its box, the side of its target it stands on, and whether it is clear of the target.</summary>
        public readonly struct Placement
        {
            public readonly Box Card;
            public readonly Side Side;
            public readonly bool Docked;
            public Placement(Box card, Side side, bool docked) { Card = card; Side = side; Docked = docked; }
        }

        /// <summary>How far the card keeps from the frame's edge.</summary>
        public const float Margin = 12f;
        /// <summary>The reference build's gaps: twenty left of a right-hand rail, fourteen right of a left-hand one, ten under the top bar or over the floor.</summary>
        public const float GapLeftOf = 20f, GapRightOf = 14f, GapVertical = 10f;
        /// <summary>The reference card's width: its max-w-sm, at the standard text size.</summary>
        public const float CardWidth = 384f;
        /// <summary>The widest a card grows over or under a target, against its standard width: wider lines are hard to read.</summary>
        public const float MaxWiden = 2.4f;

        // ---------------------------------------------------------------- the card's inside

        /// <summary>The card's padding at a text scale.</summary>
        public static float Pad(float scale) => 20f * Scale(scale);

        /// <summary>The player's text scale, never under a half.</summary>
        public static float Scale(float scale) => Math.Max(.5f, scale);

        /// <summary>Button widths and the button row's height at a text scale.</summary>
        public static float SkipWidth(float scale) => 116f * Scale(scale);
        public static float BackWidth(float scale) => 96f * Scale(scale);
        public static float NextWidth(float scale) => 124f * Scale(scale);
        public static float TakeWidth(float scale) => 150f * Scale(scale);
        public static float ButtonHeight(float scale) => 36f * Scale(scale);
        public static float CounterHeight(float scale) => 18f * Scale(scale);

        /// <summary>The width all three of a step's buttons need on one row: Skip at the left, Previous and Next at the right.</summary>
        public static float OneRowWidth(float scale)
        {
            float s = Scale(scale);
            return (Pad(s) - 10f * s) + SkipWidth(s) + 8f * s + BackWidth(s) + 8f * s + NextWidth(s) + Pad(s);
        }

        /// <summary>The narrowest card at a text scale: Previous and Next on a row, and the title on one line.</summary>
        public static float MinWidth(float scale, float titleWidth)
        {
            float s = Scale(scale);
            return Math.Max(2f * Pad(s) + BackWidth(s) + 8f * s + NextWidth(s), TitleFit(s, titleWidth));
        }

        /// <summary>The card width whose title box holds the title on one line.</summary>
        public static float TitleFit(float scale, float titleWidth) => (float)Math.Ceiling(titleWidth) + 2f * Pad(scale) + 4f;

        /// <summary>The card's standard width at a text scale, widened for a title that would wrap, and never wider than the frame allows.</summary>
        public static float BaseWidth(float scale, float titleWidth, float frameWidth)
        {
            float width = Math.Max(CardWidth * Scale(scale), TitleFit(scale, titleWidth));
            return frameWidth > 2f * Margin + 24f ? Math.Min(width, frameWidth - 2f * Margin) : width;
        }

        /// <summary>The card's rows, top down, from its top-left corner (y grows downward here, as the card is laid out).</summary>
        public readonly struct Rows
        {
            public readonly float Width, Pad, Inner, CounterY, TitleY, TitleHeight, BodyY, BodyHeight, ButtonsY, SkipY, NoteY, NoteHeight, Height;
            public readonly bool TwoRows, Offer;

            public Rows(float width, float pad, float counterY, float titleY, float titleHeight, float bodyY, float bodyHeight,
                float buttonsY, float skipY, float noteY, float noteHeight, float height, bool twoRows, bool offer)
            {
                Width = width; Pad = pad; Inner = width - 2f * pad; CounterY = counterY; TitleY = titleY; TitleHeight = titleHeight;
                BodyY = bodyY; BodyHeight = bodyHeight; ButtonsY = buttonsY; SkipY = skipY; NoteY = noteY; NoteHeight = noteHeight;
                Height = height; TwoRows = twoRows; Offer = offer;
            }
        }

        /// <summary>
        /// The card's rows at <paramref name="width"/>: the counter, the title, the words, the buttons
        /// and the note on how to take the tour again. Each text box is given the height its text
        /// asks for at the inner width, and at least 1.3 times its type (Inter draws nothing in a
        /// box under about 1.21). Narrower than <see cref="OneRowWidth"/>, "Skip the tour" takes a row
        /// of its own under Previous and Next. The offer has no counter and no note, and its two
        /// answers always share a row.
        /// </summary>
        public static Rows Measure(float width, float scale, Func<float, float> titleHeight, Func<float, float> bodyHeight,
            Func<float, float> noteHeight, bool offer = false)
        {
            float s = Scale(scale), pad = Pad(s), inner = width - 2f * pad;
            float y = pad, counterY = y;
            if (!offer) y += CounterHeight(s) + 6f * s;

            float titleY = y, title = Box13(titleHeight, inner, 21f * s, 2f);
            y += title + 8f * s;
            float bodyY = y, body = Box13(bodyHeight, inner, 15f * s, 4f);
            y += body + 18f * s;

            bool twoRows = !offer && width < OneRowWidth(s) - .01f;
            float buttonsY = y, skipY = y;
            y += ButtonHeight(s);
            if (twoRows) { skipY = y + 6f * s; y = skipY + ButtonHeight(s); }

            float noteY = y, note = 0f;
            if (!offer)
            {
                y += 8f * s;
                noteY = y;
                note = Box13(noteHeight, inner, 14f * s, 2f);
                y += note;
            }
            y += pad;
            return new Rows(width, pad, counterY, titleY, title, bodyY, body, buttonsY, skipY, noteY, note, y, twoRows, offer);
        }

        private static float Box13(Func<float, float> measure, float inner, float fontSize, float slack) =>
            (float)Math.Ceiling(Math.Max(measure != null ? measure(inner) : 0f, fontSize * 1.3f)) + slack;

        // ---------------------------------------------------------------- where it stands

        /// <summary>
        /// The card's place on the frame for a step lighting <paramref name="hole"/> (null for a
        /// centred step). <paramref name="heightFor"/> gives the card's height at a width.
        /// </summary>
        public static Placement Place(Box frame, Box? hole, float baseWidth, float minWidth, Func<float, float> heightFor)
        {
            float maxInside = Math.Max(1f, frame.W - 2f * Margin);
            if (!hole.HasValue)
            {
                float w = Math.Min(baseWidth, maxInside), h = heightFor(w);
                var centred = Clamp(frame, new Box(frame.CentreX - w * .5f, frame.CentreY - h * .5f, w, h));
                return new Placement(centred, Side.Centre, true);
            }

            var lit = Clip(hole.Value, frame);
            var preferred = Preferred(frame, lit);
            foreach (var side in Order(frame, lit, preferred))
            {
                var tried = Try(frame, lit, side, baseWidth, minWidth, heightFor);
                if (tried.HasValue) return new Placement(tried.Value, side, true);
            }

            // No side has the room: the reference build's corner, clamped into the frame.
            float width = Math.Min(baseWidth, maxInside), height = heightFor(width);
            Box corner;
            switch (preferred)
            {
                case Side.LeftOf: corner = new Box(lit.X - GapLeftOf - width, lit.YMax - height, width, height); break;
                case Side.RightOf: corner = new Box(lit.XMax + GapRightOf, lit.YMax - height, width, height); break;
                case Side.Below: corner = new Box(lit.X + 16f, lit.Y - GapVertical - height, width, height); break;
                default: corner = new Box(lit.CentreX - width * .5f, lit.YMax + GapVertical, width, height); break;
            }
            var clamped = Clamp(frame, corner);
            return new Placement(clamped, preferred, !clamped.Overlaps(lit));
        }

        /// <summary>The reference build's side for a target: by the edge it stands against, else by the half of the frame it is in.</summary>
        public static Side Preferred(Box frame, Box lit)
        {
            float edge = frame.W * .2f;
            if (frame.XMax - lit.XMax < edge && lit.X > frame.CentreX) return Side.LeftOf;
            if (lit.X - frame.X < edge && lit.XMax < frame.CentreX) return Side.RightOf;
            return lit.CentreY >= frame.CentreY ? Side.Below : Side.Above;
        }

        private static Side[] Order(Box frame, Box lit, Side preferred)
        {
            float leftRoom = lit.X - frame.X, rightRoom = frame.XMax - lit.XMax;
            float underRoom = lit.Y - frame.Y, overRoom = frame.YMax - lit.YMax;
            var vertical = underRoom >= overRoom ? new[] { Side.Below, Side.Above } : new[] { Side.Above, Side.Below };
            var horizontal = rightRoom >= leftRoom ? new[] { Side.RightOf, Side.LeftOf } : new[] { Side.LeftOf, Side.RightOf };
            var order = new Side[5];
            int n = 0;
            order[n++] = preferred;
            foreach (var side in preferred == Side.Below || preferred == Side.Above ? Concat(vertical, horizontal) : Concat(horizontal, vertical))
                if (Array.IndexOf(order, side, 0, n) < 0) order[n++] = side;
            Array.Resize(ref order, n);
            return order;
        }

        private static Side[] Concat(Side[] a, Side[] b)
        {
            var all = new Side[a.Length + b.Length];
            a.CopyTo(all, 0); b.CopyTo(all, a.Length);
            return all;
        }

        /// <summary>The card on one side of the target, or null when that side has no room for it.</summary>
        private static Box? Try(Box frame, Box lit, Side side, float baseWidth, float minWidth, Func<float, float> heightFor)
        {
            float left = frame.X + Margin, right = frame.XMax - Margin, bottom = frame.Y + Margin, top = frame.YMax - Margin;
            switch (side)
            {
                case Side.LeftOf:
                case Side.RightOf:
                {
                    float room = side == Side.LeftOf ? lit.X - GapLeftOf - left : right - (lit.XMax + GapRightOf);
                    float w = Math.Min(baseWidth, room);
                    if (w < minWidth - .01f || w <= 0f) return null;
                    float h = heightFor(w);
                    if (h > top - bottom + .01f) return null;
                    float x = side == Side.LeftOf ? lit.X - GapLeftOf - w : lit.XMax + GapRightOf;
                    // Tops level with the target's, as the reference stands it, moved only to stay in the frame.
                    float y = Math.Max(bottom, Math.Min(lit.YMax, top) - h);
                    return new Box(x, y, w, h);
                }
                default:
                {
                    bool below = side == Side.Below;
                    float room = below ? lit.Y - GapVertical - bottom : top - (lit.YMax + GapVertical);
                    if (room <= 0f) return null;
                    float widest = Math.Min(right - left, baseWidth * MaxWiden);
                    float w = Math.Min(baseWidth, right - left);
                    float h = heightFor(w);
                    while (h > room + .01f && w < widest - .01f)
                    {
                        w = Math.Min(widest, w * 1.15f);
                        h = heightFor(w);
                    }
                    if (h > room + .01f) return null;
                    float x = below ? lit.X + 16f : lit.CentreX - w * .5f;
                    x = Math.Max(left, Math.Min(x, right - w));
                    float y = below ? lit.Y - GapVertical - h : lit.YMax + GapVertical;
                    return new Box(x, y, w, h);
                }
            }
        }

        private static Box Clip(Box box, Box frame) =>
            Box.MinMax(Math.Max(box.X, frame.X), Math.Max(box.Y, frame.Y), Math.Min(box.XMax, frame.XMax), Math.Min(box.YMax, frame.YMax));

        /// <summary>A box moved, not resized, to stand inside the frame's margin (as far as it fits).</summary>
        private static Box Clamp(Box frame, Box box)
        {
            float left = frame.X + Margin, right = Math.Max(left, frame.XMax - Margin - box.W);
            float bottom = frame.Y + Margin, top = Math.Max(bottom, frame.YMax - Margin - box.H);
            return new Box(Math.Max(left, Math.Min(box.X, right)), Math.Max(bottom, Math.Min(box.Y, top)), box.W, box.H);
        }
    }
}
