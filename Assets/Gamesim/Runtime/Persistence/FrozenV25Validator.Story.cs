// Exact semantic snapshot of EpisodeStoryValidation.cs at f4566b69; SHA256 48a89f4f28e6f1930fd8cedf73bcc0ec2809d0dbce33cc8dd7b3fbbbc0307e70.
// UNUSED contract. Never route this snapshot through later live validators or rule helpers.
using System;
using Gamesim.Persistence.Frozen25Data;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Schema 16's invariants: the story bundle, story beats and story cycles.
    ///
    /// <para>Every list is bounded, for the reason every list in this file is: nothing legitimate
    /// makes thousands of anything, and an unbounded list is a save that grows until it will not
    /// load. The bounds are the ones <c>20-story-arcs-brainstorm.md</c> §2.3 named, widened only
    /// where a real season can reach them.</para>
    /// </summary>
    internal static partial class FrozenV25Validator
    {
        private static bool TryValidateStory(EpisodeState s, out string error)
        {
            error = null;
            var w = s.story;
            if (w == null) return Fail(out error, "Missing story state.");
            bool Id(string id) => id != null && s.contestants.Any(c => c.id == id);
            bool OptionalId(string id) => string.IsNullOrEmpty(id) || Id(id);
            int cast = s.contestants.Count;

            if (w.rulesStartWeek != 0 && (w.rulesStartWeek < 1 || w.rulesStartWeek > Math.Min(101, s.week + 1)))
                return Fail(out error, "A story rules boundary cannot be further off than next week.");
            if (w.rulesVersion < 0 || w.rulesVersion > StoryRules.Current || (w.rulesStartWeek != 0 && w.rulesVersion < StoryRules.Spine))
                return Fail(out error, "Unsupported story rules version.");
            if (w.productionStrictness != StoryRules.Lenient && w.productionStrictness != StoryRules.Standard)
                return Fail(out error, "Unsupported production strictness.");

            if (w.grudges == null || w.grudges.Count > 256 || w.grudges.Any(g => g == null || !Id(g.holderId) || !Id(g.targetId)
                    || g.holderId == g.targetId || !GrudgeCauses.IsKnown(g.cause) || !Finite(g.severity) || g.severity < 0 || g.severity > 100
                    || g.originWeek < 1 || g.originWeek > s.week || g.count < 1 || g.count > 100)
                || w.grudges.GroupBy(g => g.holderId + "|" + g.targetId).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid grudge data.");

            if (w.facts == null || w.facts.Count > Knowledge.Ceiling || w.facts.Any(f => f == null || !Text(f.id, 160) || !FactKinds.IsKnown(f.kind)
                    || !OptionalId(f.actorId) || !OptionalId(f.subjectId) || !ShortOrAbsent(f.refId, 160) || !FactVisibility.IsKnown(f.visibility)
                    || f.week < 1 || f.week > s.week || f.knowers == null || f.knowers.Count > cast || f.knowers.Any(id => !Id(id))
                    || f.knowers.Distinct(StringComparer.Ordinal).Count() != f.knowers.Count)
                || w.facts.GroupBy(f => f.id).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid house fact data.");

            if (w.bonds == null || w.bonds.Count > 64 || w.bonds.Any(b => b == null || !Text(b.id, 160) || !BondKinds.IsKnown(b.kind)
                    || !Id(b.aId) || !Id(b.bId) || b.aId == b.bId || !BondStatus.IsKnown(b.status) || !ShortOrAbsent(b.cycleId, 160)
                    || b.sinceWeek < 1 || b.sinceWeek > s.week || (b.endedWeek != 0 && (b.endedWeek < b.sinceWeek || b.endedWeek > s.week)))
                || w.bonds.GroupBy(b => b.id).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid bond data.");

            if (w.hooks == null || w.hooks.Count > 32 || w.hooks.Any(h => h == null || !Text(h.id, 160) || !Id(h.holderId) || !Id(h.overId)
                    || h.holderId == h.overId || !ShortOrAbsent(h.factId, 160) || h.week < 1 || h.week > s.week)
                || w.hooks.GroupBy(h => h.id).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid hook data.");

            if (w.contacts == null || w.contacts.Count > cast || w.contacts.Any(c => c == null || !Id(c.npcId) || c.rapport < 0 || c.rapport > 100
                    || c.weekCount < 0 || c.weekCount > 100 || c.lastWeek < 0 || c.lastWeek > s.week)
                || w.contacts.GroupBy(c => c.npcId).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid contact data.");

            if (w.lore == null || w.lore.Count > cast || w.lore.Any(l => l == null || !Id(l.contestantId) || !Text(l.sheetKey, 100)
                    || !LoreSources.IsKnown(l.source) || l.factIds == null || l.factIds.Count > 16 || l.factIds.Any(f => !Text(f, 100))
                    || l.factIds.Distinct(StringComparer.Ordinal).Count() != l.factIds.Count)
                || w.lore.GroupBy(l => l.contestantId).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid lore data.");

            if (w.knownFacts == null || w.knownFacts.Count > 200 || w.knownFacts.Any(f => !Text(f, 100))
                || w.knownFacts.Distinct(StringComparer.Ordinal).Count() != w.knownFacts.Count)
                return Fail(out error, "Invalid learned lore.");

            if (w.conduct == null || w.conduct.Count > cast || w.conduct.Any(c => c == null || !Id(c.contestantId) || c.strikes < 0 || c.strikes > 10
                    || c.lastStrikeWeek < 0 || c.lastStrikeWeek > s.week || c.cleanWeeks < 0 || c.cleanWeeks > 100
                    || c.sitsOutWeek < 0 || c.sitsOutWeek > s.week + 1 || c.pushedBack < 0 || c.pushedBack > 1
                    || c.reasons == null || c.reasons.Count > 10 || c.reasons.Any(r => !Text(r, 100)))
                || w.conduct.GroupBy(c => c.contestantId).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid conduct data.");

            if (w.removals == null || w.removals.Count > cast || w.removals.Any(r => r == null || !Id(r.contestantId) || !Text(r.reasonId, 100)
                    || r.week < 1 || r.week > s.week)
                || w.removals.GroupBy(r => r.contestantId).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid removal data.");
            // The status and the record are two accounts of one removal and must agree both ways.
            if (s.contestants.Any(c => c.status == ContestantStatus.Expelled && !w.removals.Any(r => r.contestantId == c.id))
                || w.removals.Any(r => s.Find(r.contestantId)?.status != ContestantStatus.Expelled))
                return Fail(out error, "A removal and the houseguest's status disagree.");

            if (!string.IsNullOrEmpty(w.pendingRemovalId) && s.Find(w.pendingRemovalId)?.status != ContestantStatus.Active)
                return Fail(out error, "A pending removal must name somebody still in the house.");

            if (w.cooldowns == null || w.cooldowns.Count > 256 || w.cooldowns.Any(c => c == null || !Text(c.key, 200)
                    || c.untilWeek < 1 || c.untilWeek > s.week + 100)
                || w.cooldowns.GroupBy(c => c.key).Any(x => x.Count() > 1))
                return Fail(out error, "Invalid story cooldowns.");

            if (w.reckonings == null || w.reckonings.Count > 64 || w.reckonings.Any(r => r == null || !Id(r.npcId) || !Text(r.cause, 60)
                    || r.week < 1 || r.week > s.week))
                return Fail(out error, "Invalid reckonings.");

            foreach (var modifier in s.activeModifiers)
                if (!string.IsNullOrEmpty(modifier.ownerId) && !Id(modifier.ownerId))
                    return Fail(out error, "A modifier names somebody who is not in the season.");

            foreach (var story in s.storylines)
            {
                if (story.cast == null || story.path == null || story.vars == null)
                    return Fail(out error, "Invalid story cycle.");
                if (story.beatId == null)
                {
                    // A legacy one-chapter storyline: nothing of the cycle may be set.
                    if (story.cast.Count > 0 || story.path.Count > 0 || story.vars.Count > 0 || story.lane != null
                        || story.nextAnchor != null || story.endingId != null || story.nextWeek != 0 || story.variant != 0)
                        return Fail(out error, "A legacy storyline carries story-cycle state.");
                    continue;
                }
                if (!Text(story.beatId, 120) || !StoryLanes.IsKnown(story.lane) || story.variant < 0 || story.variant > 10
                    || story.cast.Count > 8 || story.cast.Any(r => r == null || !Text(r.role, 40) || !Id(r.contestantId))
                    || story.cast.GroupBy(r => r.role).Any(x => x.Count() > 1)
                    || story.path.Count > 24 || story.path.Any(p => p == null || !Text(p.beatId, 120) || !ShortOrAbsent(p.optionId, 60)
                        || !StoryResults.IsKnown(p.result) || p.week < 1 || p.week > s.week || p.logSequence < 0 || p.logSequence >= s.nextSequence)
                    || story.vars.Count > 16 || story.vars.Any(v => v == null || !Text(v.key, 40) || v.value < -1000 || v.value > 1000)
                    || story.vars.GroupBy(v => v.key).Any(x => x.Count() > 1)
                    || story.nextWeek < 0 || story.nextWeek > s.week + 20
                    || (story.nextAnchor != null && !StoryAnchors.IsKnown(story.nextAnchor))
                    || !ShortOrAbsent(story.endingId, 80))
                    return Fail(out error, "Invalid story cycle.");
            }

            foreach (var item in s.houseEvents)
            {
                if (item.cast == null) return Fail(out error, "Invalid story beat.");
                foreach (var choice in item.choices)
                    if (choice.eligibleIds == null || choice.bonusTraits == null || choice.against == null
                        || choice.effects == null || choice.backfire == null)
                        return Fail(out error, "Invalid story option.");
                if (!item.IsStory)
                {
                    // A legacy event carries none of a beat's state.
                    if (item.contentId != null || item.cycleId != null || item.closesAnchor != null || item.surface != null
                        || item.venue != null || item.lapseOptionId != null || item.cast.Count > 0
                        || item.choices.Any(c => c.optionId != null || c.effects.Count > 0 || c.backfire.Count > 0
                                                 || c.eligibleIds.Count > 0 || c.next != null || c.nextOnBackfire != null
                                                 || c.checkBase != -1 || c.lapse || c.conduct || c.pickPerson || c.locked
                                                 || c.costsAction || c.subjectId != null || c.glyph != null || c.lockReason != null
                                                 || c.approach != null
                                                 || c.bonusTraits.Count > 0 || c.against.Count > 0))
                        return Fail(out error, "A legacy house event carries story-beat state.");
                    continue;
                }
                if (!Text(item.contentId, 120) || !ShortOrAbsent(item.cycleId, 160)
                    || (item.closesAnchor != null && !StoryAnchors.IsKnown(item.closesAnchor))
                    || !StorySurfaces.IsKnown(item.surface) || !ShortOrAbsent(item.venue, 60) || !ShortOrAbsent(item.lapseOptionId, 60)
                    || item.cast.Count > 8 || item.cast.Any(r => r == null || !Text(r.role, 40) || !Id(r.contestantId)))
                    return Fail(out error, "Invalid story beat.");
                if (item.choices.Any(c => c.optionId == null) || item.choices.GroupBy(c => c.optionId).Any(x => x.Count() > 1))
                    return Fail(out error, "Every story option needs its own id.");
                foreach (var choice in item.choices)
                {
                    if (!Text(choice.optionId, 60) || !ShortOrAbsent(choice.glyph, 40) || !ShortOrAbsent(choice.lockReason, 120)
                        || (choice.approach != null && !Personality.Approach.IsKnown(choice.approach))
                        || !(choice.checkBase == -1 || (Finite(choice.checkBase) && choice.checkBase >= 0 && choice.checkBase <= 100))
                        || !OptionalId(choice.subjectId)
                        || choice.eligibleIds.Count > 32 || choice.eligibleIds.Any(id => !Id(id))
                        || choice.bonusTraits.Count > 17 || choice.bonusTraits.Any(t => !Text(t, 40))
                        || choice.against.Count > 17 || choice.against.Any(t => !Text(t, 40))
                        || choice.effects.Count > 24 || choice.backfire.Count > 24
                        || choice.effects.Concat(choice.backfire).Any(e => BadEffect(e, OptionalId))
                        || !ShortOrAbsent(choice.next, 120) || !ShortOrAbsent(choice.nextOnBackfire, 120))
                        return Fail(out error, "Invalid story option.");
                }
            }
            return true;
        }

        private static bool BadEffect(StoryEffectState e, Func<string, bool> optionalId) =>
            e == null || !StoryEffects.IsKnown(e.kind)
            || !(optionalId(e.fromId) || e.fromId == StoryEffects.Picked)
            || !(optionalId(e.toId) || e.toId == StoryEffects.Picked)
            || !(optionalId(e.thirdId) || e.thirdId == StoryEffects.Picked)
            || !ShortOrAbsent(e.type, 60) || !ShortOrAbsent(e.text, 300)
            || !Finite(e.amount) || Math.Abs(e.amount) > 200 || e.weeks < 0 || e.weeks > 20;
    }
}
