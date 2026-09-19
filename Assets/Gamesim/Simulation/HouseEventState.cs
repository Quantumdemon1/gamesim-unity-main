using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Something that happened to the house, and what the player did about it.
    ///
    /// <para>Ported from the reference's <c>src/models/house-event.ts</c>. Every week in this port
    /// so far happens <i>because</i> the player pressed something; the event layer is what makes one
    /// week feel unlike the last, by putting a situation in front of them that they did not create.
    /// </para>
    ///
    /// <para><b>The choices are stored, not regenerated.</b> An event is drawn with its options, and
    /// re-deriving them on load would let a reload change the offer — which is the one thing a save
    /// must never do. It is the same reason a diary prompt is stored rather than re-rolled.</para>
    /// </summary>
    [Serializable]
    public sealed class HouseEventState
    {
        public string id;
        public string kind;
        public string title;
        public string narrative;

        /// <summary>Who it is about. The player may or may not be among them.</summary>
        public List<string> involvedIds = new List<string>();

        public List<HouseEventChoice> choices = new List<HouseEventChoice>();
        public int week;
        public bool resolved;

        /// <summary>Which option was taken, or −1 while it is still open.</summary>
        public int chosenIndex = -1;

        /// <summary>What happened as a result, written when it resolves.</summary>
        public string outcome;

        public HouseEventState Clone()
        {
            var copy = (HouseEventState)MemberwiseClone();
            copy.involvedIds = new List<string>(involvedIds);
            copy.choices = choices.Select(x => x.Clone()).ToList();
            return copy;
        }
    }

    /// <summary>One way of answering a house event.</summary>
    [Serializable]
    public sealed class HouseEventChoice
    {
        public string label;
        public string description;

        /// <summary>How risky this is, which the screen shows before the player commits to it.</summary>
        public string risk = HouseEventRisk.Low;

        /// <summary>What it moves, and for whom.</summary>
        public List<HouseEventImpact> impacts = new List<HouseEventImpact>();

        /// <summary>What taking it does to the player's standing generally.</summary>
        public double trustChange;

        public HouseEventChoice Clone()
        {
            var copy = (HouseEventChoice)MemberwiseClone();
            copy.impacts = impacts.Select(x => x.Clone()).ToList();
            return copy;
        }
    }

    /// <summary>One houseguest's share of a choice's consequences.</summary>
    [Serializable]
    public sealed class HouseEventImpact
    {
        public string targetId;
        public double amount;
        public HouseEventImpact Clone() => (HouseEventImpact)MemberwiseClone();
    }

    /// <summary>
    /// How much a choice can rebound, spelled as the reference spells it.
    ///
    /// <para>Shown on the control before it is pressed. The reference draws it as a coloured dot;
    /// this port says it in words as well, because a warning carried only by colour is a warning
    /// some players never receive.</para>
    /// </summary>
    public static class HouseEventRisk
    {
        public const string Low = "low", Medium = "medium", High = "high";
        public static readonly string[] All = { Low, Medium, High };
        public static bool IsKnown(string risk) => risk != null && Array.IndexOf(All, risk) >= 0;
    }

    /// <summary>
    /// The six kinds of event the reference's layer produces, kept as strings for the same reason
    /// deals are: a save written by a later build that adds a seventh should carry it through rather
    /// than be rejected for it.
    /// </summary>
    public static class HouseEventKind
    {
        /// <summary>Something the phase itself throws up — the reference's <c>phase-event-system</c>.</summary>
        public const string Phase = "phase";

        /// <summary>A situation in the house at large — <c>house-event-system</c>.</summary>
        public const string House = "house";

        /// <summary>Two houseguests in the same room — <c>proximity-event-system</c>.</summary>
        public const string Proximity = "proximity";

        /// <summary>Background colour with no decision attached — <c>ambient-event-system</c>.</summary>
        public const string Ambient = "ambient";

        /// <summary>Something the season's own state made inevitable — <c>emergent-event-system</c>.</summary>
        public const string Emergent = "emergent";

        /// <summary>The week going wrong — <c>mid-week-crisis-system</c>.</summary>
        public const string Crisis = "crisis";

        public static readonly string[] All = { Phase, House, Proximity, Ambient, Emergent, Crisis };
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;

        /// <summary>
        /// Whether this kind asks the player anything.
        ///
        /// <para>An ambient event is told, not answered. Giving it choices would make the house feel
        /// like a questionnaire, which is the failure mode the reference avoids by having a kind
        /// that only ever narrates.</para>
        /// </summary>
        public static bool Asks(string kind) => kind != Ambient;
    }
}
