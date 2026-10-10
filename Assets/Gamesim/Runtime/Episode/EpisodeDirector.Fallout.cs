using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Fallout (plan §5.1): what the house makes of a story moment. A blow-up, production's
    /// penalty, a removal and a house meeting each get the ceremony treatment - the title card, the
    /// room framed, the bodies acting it out and the room turning to look - when the show's own
    /// ceremonies do not already have the screen.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>One person in a story ceremony and the part the beat's cast gives them, if any.</summary>
        private readonly struct FalloutPart
        {
            public readonly string id, role;
            public FalloutPart(string id, string role) { this.id = id; this.role = role; }
        }

        /// <summary>The story ceremony a commit wrote, if the player may see it; played after the show's own.</summary>
        private void PlayFallout(EpisodeState state, EpisodeEvent entry, HashSet<string> wasActive)
        {
            if (state == null || entry == null || !StoryFallout.IsFallout(entry.kind)) return;
            var parts = FalloutPeople(state, entry, wasActive, out string contentId);
            var who = parts.Select(part => part.id).ToList();
            if (takeover != null)
            {
                EndCeremonyCards();
                string badge = StoryFallout.BadgeFor(entry.kind);
                var subjects = who.Select(state.Find).Where(actor => actor != null)
                    .Select(actor => new CeremonyTakeover.Subject(actor.name, badge, CharacterPortraits.Get(actor), actor)).ToList();
                takeover.Play(entry.kind, state.week, subjects, reducedMotion);
            }
            ReactToFallout(state, entry.kind, contentId, parts);
            FrameCeremony(entry.kind);
            lastCeremonyKind = entry.kind;
            // Production stepping in is what the feeds cut away from.
            if (entry.kind == StoryLog.Penalty || entry.kind == StoryLog.Expulsion) HoldFeeds();
        }

        /// <summary>
        /// Who a story ceremony is about, and the part each plays. A removal: whoever production
        /// took out of the house in this commit. A penalty: whoever production has just penalised.
        /// A blow-up or a meeting: the people in the beat that wrote it, with the parts its cast
        /// gives them, and the beat's content id; the player's own face is left to the player, and
        /// at most four are named.
        /// </summary>
        private static List<FalloutPart> FalloutPeople(EpisodeState state, EpisodeEvent entry, HashSet<string> wasActive, out string contentId)
        {
            contentId = null;
            switch (entry.kind)
            {
                case StoryLog.Expulsion:
                    return state.contestants.Where(c => c.status == ContestantStatus.Expelled && wasActive != null && wasActive.Contains(c.id))
                        .Select(c => new FalloutPart(c.id, StoryReactions.Expelled)).ToList();
                case StoryLog.Penalty:
                    return state.story.conduct.Where(c => c.strikes == 2 && c.lastStrikeWeek == state.week)
                        .Select(c => new FalloutPart(c.contestantId, null)).ToList();
                default:
                    var beat = state.houseEvents.LastOrDefault(e => e.IsStory && e.resolved && StoryText.BeatOf(e)?.fallout == entry.kind);
                    if (beat == null) return new List<FalloutPart>();
                    contentId = beat.contentId;
                    var roles = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var part in beat.cast)
                        if (!string.IsNullOrEmpty(part.contestantId) && !roles.ContainsKey(part.contestantId)) roles[part.contestantId] = part.role;
                    return beat.involvedIds.Concat(beat.cast.Select(role => role.contestantId))
                        .Where(id => !string.IsNullOrEmpty(id) && id != state.playerId && state.Find(id)?.status == ContestantStatus.Active)
                        .Distinct().Take(4)
                        .Select(id => new FalloutPart(id, roles.TryGetValue(id, out var role) ? role : null)).ToList();
            }
        }

        /// <summary>
        /// The bodies act it out by the story-reaction table (<see cref="StoryReactions"/>), each
        /// turned to the other person it is about, and the rest of the house turns to look at the
        /// first of them. A removal also has the houseguests standing nearest the removed one -
        /// distance in the room, nothing else - act their shock.
        /// </summary>
        private void ReactToFallout(EpisodeState state, string kind, string contentId, List<FalloutPart> parts)
        {
            var ids = parts.Select(part => part.id).ToList();
            string first = ids.FirstOrDefault();
            foreach (var part in parts)
            {
                var wanted = StoryReactions.For(StoryReactions.Moment.OnFallout, contentId, part.role, kind) ?? DefaultFallout(kind);
                string toward = part.id == first ? ids.Skip(1).FirstOrDefault() : first;
                ReactStory(part.id, wanted, toward);
            }
            if (kind == StoryLog.Expulsion && !string.IsNullOrEmpty(first))
            {
                var onlooking = StoryReactions.For(StoryReactions.Moment.OnFallout, contentId, StoryReactions.Onlooker, kind)
                    ?? CharacterPresentation.Reaction.Shocked;
                foreach (var id in Onlookers(state, first, ids)) ReactStory(id, onlooking, first);
            }
            TurnHeads(state, first, ids);
        }

        private static CharacterPresentation.Reaction DefaultFallout(string kind) =>
            kind == StoryLog.Expulsion ? CharacterPresentation.Reaction.Tearful
            : kind == StoryLog.Blowup ? CharacterPresentation.Reaction.Furious
            : CharacterPresentation.Reaction.Shocked;

        /// <summary>
        /// The houseguests near enough to see a removal: in the house, not the player, not the one
        /// removed, within <see cref="StoryReactions.OnlookerRadius"/> of that body - nearest first,
        /// ties by id, at most <see cref="StoryReactions.MaxOnlookers"/>. Public for the tests.
        /// </summary>
        public List<string> Onlookers(EpisodeState state, string aboutId, ICollection<string> except)
        {
            var result = new List<string>();
            var about = BodyFor(aboutId);
            if (state == null || about == null) return result;
            var near = new List<(float distance, string id)>();
            foreach (var c in state.contestants)
            {
                if (c.status != ContestantStatus.Active || c.id == state.playerId || c.id == aboutId || (except != null && except.Contains(c.id))) continue;
                var body = BodyFor(c.id);
                if (body == null) continue;
                var offset = body.transform.position - about.transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance <= StoryReactions.OnlookerRadius) near.Add((distance, c.id));
            }
            foreach (var (_, id) in near.OrderBy(n => n.distance).ThenBy(n => n.id, StringComparer.Ordinal).Take(StoryReactions.MaxOnlookers))
                result.Add(id);
            return result;
        }

        /// <summary>
        /// A story reaction (plan §5.1): the clip it asks for where the body has one, and on a body
        /// whose controller lacks it the ceremony stand-in - Shocked plays as Nominated, Tearful as
        /// Evicted, Embrace as Saved; Furious and StormOff are a look only, at
        /// <paramref name="towardId"/>. The primitive rig records the beat itself. A furious body is
        /// turned to the one it is furious with first, and given its own heading back after
        /// <see cref="FuriousFacingSeconds"/> unless something else has turned it since.
        /// </summary>
        private void ReactStory(string id, CharacterPresentation.Reaction wanted, string towardId)
        {
            var body = BodyFor(id);
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual == null) return;
            var target = string.IsNullOrEmpty(towardId) ? null : BodyFor(towardId);
            // The primitive rig plays nothing, so it is only told the beat it would have played.
            if (visual.Supports(wanted) || visual.IsPrimitiveBody)
            {
                if (wanted == CharacterPresentation.Reaction.Furious && target != null) FaceForFury(visual, target.transform);
                visual.React(wanted);
                return;
            }
            switch (wanted)
            {
                case CharacterPresentation.Reaction.Shocked: visual.React(CharacterPresentation.Reaction.Nominated); break;
                case CharacterPresentation.Reaction.Tearful: visual.React(CharacterPresentation.Reaction.Evicted); break;
                case CharacterPresentation.Reaction.Embrace: visual.React(CharacterPresentation.Reaction.Saved); break;
                default:
                    if (target != null) visual.LookAt(target, HeadTurnSeconds);
                    break;
            }
        }

        /// <summary>How long a furious body keeps the heading it was turned to.</summary>
        public const float FuriousFacingSeconds = 3.5f;

        private void FaceForFury(CharacterPresentation visual, Transform toward)
        {
            var offset = toward.position - visual.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f) return;
            float previous = visual.FacingYaw;
            float turned = Quaternion.LookRotation(offset).eulerAngles.y;
            visual.SetFacing(turned);
            AfterStoryDelay(FuriousFacingSeconds, () =>
            {
                // Only the heading this set: a scene or a meeting that turned the body since keeps it.
                if (visual != null && Mathf.Approximately(visual.FacingYaw, turned)) visual.SetFacing(previous);
            });
        }
    }
}
