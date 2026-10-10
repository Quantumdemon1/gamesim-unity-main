using System;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// How a story moment is acted out (plan §5.1; the art brief's R1 table): which beat a body
    /// plays, and when, for each part in a story beat. Presentation only - nothing here is saved
    /// and nothing the engine decides reads it.
    ///
    /// <para>The knowledge gate: a row names only the public parts a beat's own text names, a
    /// fallout's kind, and an ending the player saw - never a relationship, a grudge or a feeling.
    /// Who plays a part is the beat's cast; who stands near enough to be an onlooker is distance in
    /// the room. <c>StoryReactionsTests</c> holds every content id and role to the catalogue, so a
    /// renamed beat or part fails there and not on air.</para>
    ///
    /// <para>The first row that matches wins, so the specific rows come before the general ones.</para>
    /// </summary>
    public static class StoryReactions
    {
        /// <summary>When a row's beat is played.</summary>
        public enum Moment
        {
            /// <summary>As the fallout's ceremony card plays.</summary>
            OnFallout,
            /// <summary>As the part reaches its place in the scene's staging.</summary>
            OnSceneArrival,
            /// <summary>As the scene's staging ends.</summary>
            OnSceneEnd,
            /// <summary>As an arc ends with the row's ending, once its card closes.</summary>
            OnEnding,
        }

        /// <summary>Matches any content id or any part.</summary>
        public const string Any = "*";
        /// <summary>The ending two people make up at (<c>end:peace</c>, stored as "peace").</summary>
        public const string Peace = "peace";
        /// <summary>The part production removed from the house, in a removal's fallout.</summary>
        public const string Expelled = "EXPELLED";
        /// <summary>The part a bystander plays at a removal: near enough to see it happen.</summary>
        public const string Onlooker = "ONLOOKER";

        /// <summary>How many onlookers react to a removal, and how near they must stand, in metres.</summary>
        public const int MaxOnlookers = 3;
        public const float OnlookerRadius = 4f;

        public readonly struct Row
        {
            public readonly string contentId, role, fallout, ending;
            public readonly CharacterPresentation.Reaction reaction;
            public readonly Moment moment;

            public Row(string contentId, string role, string fallout, string ending, CharacterPresentation.Reaction reaction, Moment moment)
            {
                this.contentId = contentId; this.role = role; this.fallout = fallout; this.ending = ending;
                this.reaction = reaction; this.moment = moment;
            }

            public bool Matches(Moment when, string content, string part, string kind, string end) =>
                moment == when
                && (contentId == Any || string.Equals(contentId, content, StringComparison.Ordinal))
                && (role == Any || string.Equals(role, part, StringComparison.Ordinal))
                && (fallout == null || string.Equals(fallout, kind, StringComparison.Ordinal))
                && (ending == null || string.Equals(ending, end, StringComparison.Ordinal));
        }

        private const CharacterPresentation.Reaction Shocked = CharacterPresentation.Reaction.Shocked,
            Tearful = CharacterPresentation.Reaction.Tearful, Embrace = CharacterPresentation.Reaction.Embrace,
            Furious = CharacterPresentation.Reaction.Furious, StormOff = CharacterPresentation.Reaction.StormOff;

        public static readonly Row[] Rows =
        {
            // The kitchen blow-up boils over in the living room: the two of them furious, the
            // witnesses the beat names shocked.
            new Row("kitchen-blowup:blow-up", "HOTHEAD", StoryLog.Blowup, null, Furious, Moment.OnFallout),
            new Row("kitchen-blowup:blow-up", "TARGET", StoryLog.Blowup, null, Furious, Moment.OnFallout),
            new Row("kitchen-blowup:blow-up", "WITNESS1", StoryLog.Blowup, null, Shocked, Moment.OnFallout),
            new Row("kitchen-blowup:blow-up", "WITNESS2", StoryLog.Blowup, null, Shocked, Moment.OnFallout),
            // The staged feud: the showrunner sells it.
            new Row("staged-feud:the-fight", "SHOWRUNNER", StoryLog.Blowup, null, Furious, Moment.OnFallout),
            // The house turns on one of its own: the three furious, the one they turned on leaves.
            new Row("the-house-turns:turns", "AGGRESSOR1", StoryLog.Blowup, null, Furious, Moment.OnFallout),
            new Row("the-house-turns:turns", "AGGRESSOR2", StoryLog.Blowup, null, Furious, Moment.OnFallout),
            new Row("the-house-turns:turns", "AGGRESSOR3", StoryLog.Blowup, null, Furious, Moment.OnFallout),
            new Row("the-house-turns:turns", "TARGET", StoryLog.Blowup, null, StormOff, Moment.OnFallout),
            // The veto meeting's fallout: the nominee storms through the living room - the beat as
            // they reach the scene, the walk out of it when the scene ends, so Step in still finds
            // them there (decision D5).
            new Row("the-fallout:fallout", "NOMINEE", null, null, StormOff, Moment.OnSceneArrival),
            new Row("the-fallout:fallout", "NOMINEE", null, null, StormOff, Moment.OnSceneEnd),
            // Made up: the two who fought embrace, when the player saw them make peace.
            new Row("kitchen-blowup:words", "HOTHEAD", null, Peace, Embrace, Moment.OnEnding),
            new Row("kitchen-blowup:words", "TARGET", null, Peace, Embrace, Moment.OnEnding),
            new Row("cold-shoulder:silent", "HOTHEAD", null, Peace, Embrace, Moment.OnEnding),
            new Row("cold-shoulder:silent", "TARGET", null, Peace, Embrace, Moment.OnEnding),
            // Any removal: the one removed in tears, the nearest onlookers shocked.
            new Row(Any, Expelled, StoryLog.Expulsion, null, Tearful, Moment.OnFallout),
            new Row(Any, Onlooker, StoryLog.Expulsion, null, Shocked, Moment.OnFallout),
            new Row(Any, Any, StoryLog.Expulsion, null, Shocked, Moment.OnFallout),
            // A penalty or a house meeting: shock.
            new Row(Any, Any, StoryLog.Penalty, null, Shocked, Moment.OnFallout),
            new Row(Any, Any, StoryLog.HouseMeeting, null, Shocked, Moment.OnFallout),
            // Any other part in a blow-up: furious.
            new Row(Any, Any, StoryLog.Blowup, null, Furious, Moment.OnFallout),
        };

        /// <summary>The beat for a part at a moment, or null when no row asks for one.</summary>
        public static CharacterPresentation.Reaction? For(Moment moment, string contentId, string role, string fallout = null, string ending = null)
        {
            foreach (var row in Rows)
                if (row.Matches(moment, contentId, role, fallout, ending)) return row.reaction;
            return null;
        }

        /// <summary>Whether any row acts out an arc's ending: the arcs whose "peace" the director stages.</summary>
        public static bool ActsEnding(string contentId, string ending)
        {
            foreach (var row in Rows)
                if (row.moment == Moment.OnEnding && row.contentId != Any
                    && string.Equals(row.contentId, contentId, StringComparison.Ordinal)
                    && string.Equals(row.ending, ending, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
