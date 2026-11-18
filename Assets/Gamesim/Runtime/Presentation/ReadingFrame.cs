using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>The two kinds of panel the season's HUD docks (menu slice 3, the review's change 5).</summary>
    public enum PanelKind
    {
        /// <summary>A page to read or browse, with nothing on it to answer now: it opens tall, over a veil.</summary>
        Reading,
        /// <summary>A question or a short choice answered while looking at the house: it keeps its own shape.</summary>
        Quick,
    }

    /// <summary>
    /// The tall reading frame, in kinds and measures (menu slice 3, the review's change 5). Every
    /// layout the season's HUD can dock a panel in has exactly one kind here, by the layout's name
    /// (<c>EpisodeHud.ActivityLayout</c>); a layout added without one is reported by the HUD as it
    /// opens and fails the kinds test. A READING panel opens in one frame about seven tenths of the
    /// canvas tall, beside the left rail, over a veil that dims the house and takes the pointer; a
    /// QUICK panel keeps the shape its layout gives it.
    ///
    /// <para>Plain C#, so the Unity-free suite checks the same arithmetic the HUD lays the frame out
    /// with. The HUD's half is <c>EpisodeHud.ReadingFrame.cs</c>.</para>
    /// </summary>
    public static class ReadingFrame
    {
        /// <summary>The share of the canvas's height the frame takes where the room allows it.</summary>
        public const float HeightShare = .70f;
        /// <summary>The widest the frame runs: the notebook's width before the frame, a comfortable line for its pages and room for the web and the map.</summary>
        public const float MaxWidth = 1180f;
        /// <summary>How dark the veil over the house is (the theme's background colour at this opacity).</summary>
        public const float VeilOpacity = .62f;

        /// <summary>The veil's object name, so a test can find it the way it finds a named panel.</summary>
        public const string VeilName = "Reading veil";

        /// <summary>
        /// Every docked layout's kind, by the layout's name. The notebook's layout (every page of
        /// the notebook: notes, Deals &amp; promises, alliances, relationships, houseguests and a
        /// houseguest's profile, who is where, the vote, the season) is the one READING layout. The
        /// others are a conversation, a prompt, a card, a decision, a ballot, a competition, the
        /// diary, a house event, the episode screen's stage, the strategy stage's boards and the
        /// settings: QUICK.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, PanelKind> Kinds = new Dictionary<string, PanelKind>
        {
            // The fitted card: a houseguest's notice, a scene or ask card, a quiet beat of the week.
            { "Standard", PanelKind.Quick },
            // The notebook, every page of it.
            { "Reading", PanelKind.Reading },
            { "Conversation", PanelKind.Quick },
            { "ConversationNotice", PanelKind.Quick },
            { "Competition", PanelKind.Quick },
            // Laid out by the general branch; nothing opens it today.
            { "Creation", PanelKind.Quick },
            { "Diary", PanelKind.Quick },
            { "Nominations", PanelKind.Quick },
            { "HouseEvent", PanelKind.Quick },
            { "Ballot", PanelKind.Quick },
            // The other slice's (Settings and saves): it keeps its own layout.
            { "Settings", PanelKind.Quick },
            // The episode screen and its stage pages: a decision, a purchase, a share, a story step.
            { "Stage", PanelKind.Quick },
            // The strategy stage: free time's board, a houseguest's screen from it, the nominations,
            // the veto, the final choice, the finale page, the jury house.
            { "Strategy", PanelKind.Quick },
        };

        /// <summary>The layout's kind, or false for a layout the table does not list.</summary>
        public static bool TryKind(string layout, out PanelKind kind)
        {
            kind = PanelKind.Quick;
            return layout != null && Kinds.TryGetValue(layout, out kind);
        }

        /// <summary>The layout's kind; a layout the table does not list is an error, never a guess.</summary>
        public static PanelKind KindOf(string layout)
        {
            if (TryKind(layout, out var kind)) return kind;
            throw new ArgumentException("The docked layout '" + layout + "' has no kind: list it in ReadingFrame.Kinds as Reading or Quick.", nameof(layout));
        }

        /// <summary>Where a frame stands, in canvas units from the canvas's bottom-left corner.</summary>
        public struct Box
        {
            public float X, Y, Width, Height;
            public float Right => X + Width;
            public float Top => Y + Height;
            public override string ToString() => "(x " + X + ", y " + Y + ", " + Width + " x " + Height + ")";
        }

        /// <summary>
        /// The frame on a canvas of the given size: from <paramref name="floor"/> (the top of the
        /// status line's band, which stays up under the frame) upward, about seven tenths of the
        /// canvas tall but never into the top bar's <paramref name="headroom"/>; as wide as a
        /// comfortable line, centred in the room right of the rail (<paramref name="left"/>), with
        /// <paramref name="edge"/> kept clear at the right. The body scrolls inside it, so a larger
        /// text size makes the page longer, never the frame taller than its room.
        /// </summary>
        public static Box Place(float canvasWidth, float canvasHeight, float left, float floor, float headroom, float edge)
        {
            float room = Math.Max(0f, canvasWidth - left - edge);
            float width = Math.Min(MaxWidth, room);
            float ceiling = Math.Max(floor, canvasHeight - headroom);
            float height = Math.Min(canvasHeight * HeightShare, ceiling - floor);
            return new Box { X = left + (room - width) * .5f, Y = floor, Width = width, Height = height };
        }
    }
}
