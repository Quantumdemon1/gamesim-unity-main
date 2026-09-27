using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

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
        /// <summary>The story ceremony a commit wrote, if the player may see it; played after the show's own.</summary>
        private void PlayFallout(EpisodeState state, EpisodeEvent entry, HashSet<string> wasActive)
        {
            if (state == null || entry == null || !StoryFallout.IsFallout(entry.kind)) return;
            var who = FalloutPeople(state, entry, wasActive);
            if (takeover != null)
            {
                EndCeremonyCards();
                string badge = StoryFallout.BadgeFor(entry.kind);
                var subjects = who.Select(state.Find).Where(actor => actor != null)
                    .Select(actor => new CeremonyTakeover.Subject(actor.name, badge, CharacterPortraits.Get(actor), actor)).ToList();
                takeover.Play(entry.kind, state.week, subjects, reducedMotion);
            }
            ReactToFallout(state, entry.kind, who);
            FrameCeremony(entry.kind);
            lastCeremonyKind = entry.kind;
            // Production stepping in is what the feeds cut away from.
            if (entry.kind == StoryLog.Penalty || entry.kind == StoryLog.Expulsion) HoldFeeds();
        }

        /// <summary>
        /// Who a story ceremony is about. A removal: whoever production took out of the house in this
        /// commit. A penalty: whoever production has just penalised. A blow-up or a meeting: the
        /// people in the beat that wrote it, the player's own face left to the player.
        /// </summary>
        private static List<string> FalloutPeople(EpisodeState state, EpisodeEvent entry, HashSet<string> wasActive)
        {
            switch (entry.kind)
            {
                case StoryLog.Expulsion:
                    return state.contestants.Where(c => c.status == ContestantStatus.Expelled && wasActive != null && wasActive.Contains(c.id))
                        .Select(c => c.id).ToList();
                case StoryLog.Penalty:
                    return state.story.conduct.Where(c => c.strikes == 2 && c.lastStrikeWeek == state.week)
                        .Select(c => c.contestantId).ToList();
                default:
                    var beat = state.houseEvents.LastOrDefault(e => e.IsStory && e.resolved && StoryText.BeatOf(e)?.fallout == entry.kind);
                    return beat == null ? new List<string>()
                        : beat.involvedIds.Concat(beat.cast.Select(role => role.contestantId))
                            .Where(id => !string.IsNullOrEmpty(id) && id != state.playerId && state.Find(id)?.status == ContestantStatus.Active)
                            .Distinct().Take(4).ToList();
            }
        }

        /// <summary>
        /// The bodies act it out, with the stand-ins the plan names until the new clips exist, and
        /// the rest of the house turns to look at the first of them.
        /// </summary>
        private void ReactToFallout(EpisodeState state, string kind, List<string> who)
        {
            var reaction = kind == StoryLog.Expulsion ? CharacterPresentation.Reaction.Tearful
                : kind == StoryLog.Blowup ? CharacterPresentation.Reaction.Furious
                : CharacterPresentation.Reaction.Shocked;
            string first = who.FirstOrDefault();
            foreach (var id in who) ReactStory(id, reaction, id == first ? who.Skip(1).FirstOrDefault() : first);
            TurnHeads(state, first, who);
        }

        /// <summary>
        /// A story reaction (plan §5.1): the clip it asks for where the body has one, and otherwise
        /// the ceremony stand-in - Shocked plays as Nominated, Tearful as Evicted, Embrace as Saved;
        /// Furious and StormOff are a look only, at <paramref name="towardId"/>, until their clips
        /// and the comfort room's walk exist.
        /// </summary>
        private void ReactStory(string id, CharacterPresentation.Reaction wanted, string towardId)
        {
            var body = BodyFor(id);
            var visual = body != null ? body.GetComponent<CharacterPresentation>() : null;
            if (visual == null) return;
            if (visual.Supports(wanted)) { visual.React(wanted); return; }
            switch (wanted)
            {
                case CharacterPresentation.Reaction.Shocked: visual.React(CharacterPresentation.Reaction.Nominated); break;
                case CharacterPresentation.Reaction.Tearful: visual.React(CharacterPresentation.Reaction.Evicted); break;
                case CharacterPresentation.Reaction.Embrace: visual.React(CharacterPresentation.Reaction.Saved); break;
                default:
                    var target = string.IsNullOrEmpty(towardId) ? null : BodyFor(towardId);
                    if (target != null) visual.LookAt(target, HeadTurnSeconds);
                    break;
            }
        }
    }
}
