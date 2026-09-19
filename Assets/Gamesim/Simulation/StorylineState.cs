using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A storyline the player is in the middle of.
    ///
    /// <para>Ported from the reference's <c>storyline-fallbacks.ts</c>. A storyline is a house event
    /// that remembers: it has a category, it will not repeat for some weeks after it ends, and what
    /// the player chose leaves a <see cref="StoryModifierState"/> behind that outlives the choice.
    /// </para>
    ///
    /// <para>The chapter itself is offered as a <see cref="HouseEventState"/> rather than being
    /// modelled twice — a chapter is a situation with choices, which is exactly what that already
    /// is. This record is what a situation cannot carry: which template it came from, so the
    /// cooldown means something, and whether it finished.</para>
    /// </summary>
    [Serializable]
    public sealed class StorylineState
    {
        public string id;
        public string templateId;
        public string category;
        public string title;

        /// <summary>The house event carrying the chapter the player is being asked about.</summary>
        public string eventId;

        public string status = StorylineStatus.Active;
        public int week;

        /// <summary>The week it ended, so the cooldown can be measured from it. Zero while running.</summary>
        public int endedWeek;

        public StorylineState Clone() => (StorylineState)MemberwiseClone();
    }

    /// <summary>Where a storyline has got to.</summary>
    public static class StorylineStatus
    {
        public const string Active = "active";
        public const string Completed = "completed";

        /// <summary>
        /// Left behind rather than finished.
        ///
        /// <para>The reference drops a storyline the player has ignored for three weeks. A thread
        /// nobody picked up is not a thread that failed, and recording it as abandoned rather than
        /// completed keeps the cooldown honest about what actually happened.</para>
        /// </summary>
        public const string Abandoned = "abandoned";

        public static readonly string[] All = { Active, Completed, Abandoned };
        public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
        public static bool Running(string status) => status == Active;
    }

    /// <summary>
    /// Something a storyline choice left behind.
    ///
    /// <para>The reference's modifiers: a name, a few weeks, and a bonus to competitions or to the
    /// social week. They are the reason a storyline is worth finishing rather than a paragraph with
    /// buttons — a choice that only moved a relationship would be an ordinary house event.</para>
    ///
    /// <para><b>Both effects feed paths that already exist and are already read.</b> A bonus nothing
    /// consumes is the shape this port keeps finding and fixing, and adding a third one would have
    /// been a poor joke.</para>
    /// </summary>
    [Serializable]
    public sealed class StoryModifierState
    {
        public string id;
        public string name;
        public string description;

        /// <summary>Weeks left, counted down at the top of each week. Zero means it is over.</summary>
        public int weeksLeft;

        /// <summary>Added to the player's competition score while it lasts.</summary>
        public double competitionBonus;

        /// <summary>
        /// Added to the player's social allowance while it lasts, and it can be negative.
        ///
        /// <para>The reference's numbers here are in the tens because its social bonus is a
        /// percentage applied to relationship gains. This port's allowance is a handful of actions,
        /// so <see cref="StoryModifiers.ActionsFrom"/> converts rather than using the figure raw —
        /// five points would otherwise be five extra conversations, which is a whole week's worth.
        /// </para>
        /// </summary>
        public double socialBonus;

        public StoryModifierState Clone() => (StoryModifierState)MemberwiseClone();
    }

    /// <summary>How a modifier's social figure becomes something this port can spend.</summary>
    public static class StoryModifiers
    {
        /// <summary>
        /// How many of the reference's social points are worth one interaction here.
        ///
        /// <para>Ten, so its largest modifier is worth one extra conversation and its smallest is
        /// worth none. The alternative — spending its numbers directly — would hand a player five
        /// extra actions for one storyline choice, against an ordinary week's allowance of three or
        /// four.</para>
        /// </summary>
        public const double PointsPerAction = 10;

        /// <summary>The interactions a social figure is worth, rounded toward zero.</summary>
        public static int ActionsFrom(double socialBonus) => (int)(socialBonus / PointsPerAction);
    }
}
