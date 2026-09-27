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

        // ---------------------------------------------------------------- schema 14: story beats
        //
        // A story beat is a house event of kind "story". Its words are not stored: the title and
        // narrative above hold a name-free fallback sentence (validation requires both, and an
        // older reader shows something sensible), and every surface renders the real text from
        // contentId and cast through StoryText. Null and empty on every legacy event.

        /// <summary>The beat's catalogue key, "arc:beat".</summary>
        public string contentId;

        /// <summary>The story cycle (storyline record) this beat belongs to.</summary>
        public string cycleId;

        /// <summary>The anchor whose Advance lapses this beat if it is still open.</summary>
        public string closesAnchor;

        /// <summary>How the beat reaches the player (<see cref="StorySurfaces"/>).</summary>
        public string surface;

        /// <summary>The room it happens in, a staging hint.</summary>
        public string venue;

        /// <summary>The option taken when the beat lapses.</summary>
        public string lapseOptionId;

        /// <summary>Who plays each part in this beat.</summary>
        public List<StoryRoleState> cast = new List<StoryRoleState>();

        public HouseEventState Clone()
        {
            var copy = (HouseEventState)MemberwiseClone();
            copy.involvedIds = new List<string>(involvedIds);
            copy.choices = choices.Select(x => x.Clone()).ToList();
            copy.cast = cast.Select(x => x.Clone()).ToList();
            return copy;
        }

        /// <summary>Whether this is a story beat rather than a legacy house event.</summary>
        public bool IsStory => kind == HouseEventKind.Story;
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

        // ---------------------------------------------------------------- schema 14: story options
        //
        // Empty on every legacy choice. A story option is answered by its optionId, never by its
        // label: the label is a fixed caption constant and doubles as the control's accessible
        // name, which is exactly why it must never be what the engine keys on.

        /// <summary>The option's catalogue id within its beat.</summary>
        public string optionId;

        /// <summary>The glyph drawn beside it, chosen by the catalogue rather than by matching words.</summary>
        public string glyph;

        /// <summary>
        /// How it approaches the person it is aimed at (<see cref="Personality.Approach"/>), or null.
        /// Decides whether a gain resonates or grates with them when it lands.
        /// </summary>
        public string approach;

        /// <summary>
        /// The base chance of the check, 0-100, or −1 when the option is certain. The chance shown
        /// and used is <see cref="StoryOdds.Chance"/> of this plus the visible terms.
        /// </summary>
        public double checkBase = -1;

        /// <summary>Who the check is against: the houseguest whose view of the player decides it.</summary>
        public string subjectId;

        /// <summary>The option the beat takes when it lapses.</summary>
        public bool lapse;

        /// <summary>Whether taking it spends a social action.</summary>
        public bool costsAction;

        /// <summary>A rule break: always a production strike, labelled before it is chosen.</summary>
        public bool conduct;

        /// <summary>The player chooses somebody; the command carries the id and it must be in <see cref="eligibleIds"/>.</summary>
        public bool pickPerson;
        public List<string> eligibleIds = new List<string>();

        /// <summary>Shown but not available: drawn as one muted line naming what it needs.</summary>
        public bool locked;
        public string lockReason;

        /// <summary>Player traits that make it +15 (the web's trait bonus).</summary>
        public List<string> bonusTraits = new List<string>();

        /// <summary>Player traits against which choosing it costs a stress step.</summary>
        public List<string> against = new List<string>();

        /// <summary>What it does on success (or always, when certain), and on a backfire.</summary>
        public List<StoryEffectState> effects = new List<StoryEffectState>();
        public List<StoryEffectState> backfire = new List<StoryEffectState>();

        /// <summary>The beat that follows, on success and on a backfire. Null ends the thread.</summary>
        public string next, nextOnBackfire;

        public HouseEventChoice Clone()
        {
            var copy = (HouseEventChoice)MemberwiseClone();
            copy.impacts = impacts.Select(x => x.Clone()).ToList();
            copy.eligibleIds = new List<string>(eligibleIds);
            copy.bonusTraits = new List<string>(bonusTraits);
            copy.against = new List<string>(against);
            copy.effects = effects.Select(x => x.Clone()).ToList();
            copy.backfire = backfire.Select(x => x.Clone()).ToList();
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

        /// <summary>
        /// A beat of a story cycle (schema 14). Answered by <c>ProgressStoryline</c> with an option
        /// id, never by <c>ResolveHouseEvent</c>'s label match.
        /// </summary>
        public const string Story = "story";

        /// <summary>
        /// Something the player did in the house: HOUSE-LIFE-PLAN's queued kind, appended in the same
        /// schema-14 bundle so the format needs one migration rather than two.
        /// </summary>
        public const string Activity = "activity";

        // Append only: an older build rejects a save holding a kind it does not know, which is the
        // honest answer; reordering would make it reject kinds it did know.
        public static readonly string[] All = { Phase, House, Proximity, Ambient, Emergent, Crisis, Story, Activity };
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
