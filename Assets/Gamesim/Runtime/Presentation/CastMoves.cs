using System;
using System.Collections.Generic;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The moves a houseguest makes when the show asks them to be themselves: the dance they dance,
    /// whether they dance or strike a pose as they walk into the house, and which pose a body of
    /// their build strikes for a camera.
    ///
    /// <para>Presentation only. Nothing here draws on the season's generator: a houseguest's pick
    /// comes from their id and their card, so it is the same every time the show is played.</para>
    /// </summary>
    public static class CastMoves
    {
        /// <summary>
        /// The poses a man's body strikes for a camera: facing it, both feet on the floor. The foot on
        /// a ledge has no ledge on a flat floor, and the look over the shoulder looks away from the lens.
        /// </summary>
        public static readonly IReadOnlyList<CharacterPresentation.Pose> MasculineForCamera = new[]
        {
            CharacterPresentation.Pose.HandBehindHead, CharacterPresentation.Pose.AtEase,
        };

        /// <summary>The poses a woman's body strikes for a camera.</summary>
        public static readonly IReadOnlyList<CharacterPresentation.Pose> FeminineForCamera = new[]
        {
            CharacterPresentation.Pose.HandOnHip, CharacterPresentation.Pose.HandOnHipGlance, CharacterPresentation.Pose.PowerStance,
        };

        private static readonly CharacterPresentation.Pose[] AnyForCamera = new[]
        {
            CharacterPresentation.Pose.HandBehindHead, CharacterPresentation.Pose.AtEase,
            CharacterPresentation.Pose.HandOnHip, CharacterPresentation.Pose.HandOnHipGlance, CharacterPresentation.Pose.PowerStance,
        };

        /// <summary>The poses a body of this build strikes for a camera; a body that did not say gets either set.</summary>
        public static IReadOnlyList<CharacterPresentation.Pose> ForCamera(BodyFrame frame) =>
            frame == BodyFrame.Masculine ? MasculineForCamera : frame == BodyFrame.Feminine ? FeminineForCamera : AnyForCamera;

        /// <summary>
        /// A houseguest's own pose for a camera: one of their build's, the same one every time they
        /// strike it - unless it is <paramref name="avoid"/>, the pose the person before them just
        /// struck, when it is the next of their build's instead, so two people revealed one after
        /// the other never strike the same one.
        /// </summary>
        public static CharacterPresentation.Pose PoseFor(BodyFrame frame, string id,
            CharacterPresentation.Pose? avoid = null)
        {
            var set = ForCamera(frame);
            int index = (int)(Stable(id) % (uint)set.Count);
            if (avoid.HasValue && set[index] == avoid.Value && set.Count > 1) index = (index + 1) % set.Count;
            return set[index];
        }

        /// <summary>
        /// The dance a houseguest dances, by the kind of player their card says they are: the
        /// socialites samba, the wildcards do the wave with its spin and its drop, the competitors
        /// hit the hip-hop, and the planners - strategists, underdogs, anyone the cards do not place
        /// - keep to the steps everybody knows.
        /// </summary>
        public static CharacterPresentation.DanceStyle DanceFor(ContestantState who)
        {
            switch (Category(who))
            {
                case "socialite": return CharacterPresentation.DanceStyle.Samba;
                case "wildcard": return CharacterPresentation.DanceStyle.Wave;
                case "competitor": return CharacterPresentation.DanceStyle.HipHop;
                default: return CharacterPresentation.DanceStyle.House;
            }
        }

        /// <summary>
        /// Whether a houseguest dances their way onto the mark rather than striking a pose there:
        /// the socialites and the wildcards, and anyone whose card calls them a party animal.
        /// </summary>
        public static bool DancesIn(ContestantState who)
        {
            if (who == null) return false;
            string category = Category(who);
            return category == "socialite" || category == "wildcard"
                || (who.archetype ?? string.Empty).IndexOf("party", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Where a dance starts when it has a second to be seen in - a houseguest on the mark, an
        /// introduction that landed - normalized: past each take's wind-up, at a moment the dancer
        /// faces the way the body does and goes on facing it for a second. Measured on a UMA body,
        /// a quarter-second at a time. The samba swings through a full turn over its eighteen
        /// seconds and faces the front for about one of them, from 12.6 s; the hip-hop's calm last
        /// third is still all knees, from 4.0 s of 6.1; the wave dance is past its wind-up by 0.75 s
        /// and turns away at 1.75. The library's steps start where they start.
        /// </summary>
        public static float Lively(CharacterPresentation.DanceStyle style)
        {
            switch (style)
            {
                case CharacterPresentation.DanceStyle.Samba: return 12.6f / 18.2f;
                case CharacterPresentation.DanceStyle.HipHop: return 4.0f / 6.1f;
                case CharacterPresentation.DanceStyle.Wave: return .75f / 16.83f;
                default: return 0f;
            }
        }

        /// <summary>The kind of player a houseguest's template says they are, lower case; empty for a houseguest with none.</summary>
        private static string Category(ContestantState who)
        {
            if (who == null) return string.Empty;
            var template = CastTemplates.Find(who.sourceTemplateId ?? who.id);
            return (template?.Category ?? string.Empty).Trim().ToLowerInvariant();
        }

        /// <summary>FNV-1a over the id: the same number on every machine and every run, unlike string.GetHashCode.</summary>
        private static uint Stable(string id)
        {
            uint hash = 2166136261;
            foreach (char c in id ?? string.Empty) { hash ^= c; hash *= 16777619; }
            return hash;
        }
    }
}
