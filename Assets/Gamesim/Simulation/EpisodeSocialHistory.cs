using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        public static WebDiaryEvent CurrentDiary(EpisodeState s)
        {
            if (s.pendingDiary == null) return null;
            var prompt = s.pendingDiary;
            var result = WebDiaryRoom.GenerateEvent(prompt.trigger, prompt.week, s.Find(prompt.evictedId)?.name, false, prompt.isNominee);
            // Source post-eviction choices assert unverified trust/responsibility. The native
            // display keeps their identities/personas/effects but uses evidence-neutral prose.
            foreach (var choice in result.choices)
            {
                if (choice.id == "remorseful") choice.text = "I feel sad that someone had to leave. I want to remember the person, not just the vote.";
                if (choice.id == "ruthless") choice.text = "I am ready to own my decisions and keep playing hard.";
                if (choice.id == "calculated") choice.text = "I need to think carefully about my next move and how much of my plan to reveal.";
                choice.description = "Shapes your diary persona. Recorded impressions do not guarantee jury votes.";
            }
            return result;
        }

        private static void PreparePostEvictionDiary(EpisodeState s, string evictedId)
        {
            if (s.Find(s.playerId).status != ContestantStatus.Active || !WebDiaryRoom.ShouldTrigger("post_eviction", s.week,
                s.lastDiaryRoomWeek == 0 ? (int?)null : s.lastDiaryRoomWeek, () => Roll(s))) return;
            Require(s.pendingDiary == null, "Resolve the earlier diary reflection first.");
            s.pendingDiary = new DiaryPromptState { id = "diary-post_eviction-" + s.week, trigger = "post_eviction", week = s.week, evictedId = evictedId };
            Log(s, "diary-invitation", "A private post-eviction reflection is ready in the Diary Room. You may also skip it.", s.playerId);
        }

        private static void CheckDiaryCommand(EpisodeState s, EpisodeCommand c)
        {
            Require(s.pendingDiary != null && c.targetId == s.pendingDiary.id && !s.resolvedDiaryIds.Contains(c.targetId), "This diary reflection is no longer pending.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active && s.pendingDiary.week == s.week &&
                (s.phase == EpisodePhase.Social || (s.phase == EpisodePhase.Eviction && s.evictionResolved)), "Reflect during the current post-eviction free-time window.");
        }

        private static void ReflectDiary(EpisodeState s, EpisodeCommand c)
        {
            CheckDiaryCommand(s, c);
            var diary = CurrentDiary(s);
            Require(diary.choices.Any(x => x.id == c.secondTargetId), "Choose one of this diary's recorded options.");
            var plan = WebDiaryRoom.PlanChoice(s.playerPersona, diary, c.secondTargetId, s.week, s.phase.ToString());
            s.playerPersona = plan.persona;
            // These source counters are stored cumulatively. Normal native competitions/social
            // interactions do not invent a use/reset for bonuses the normal web route does not use.
            s.phaseEventSocialBonus = checked(s.phaseEventSocialBonus + plan.socialBonusDelta.GetValueOrDefault());
            s.phaseEventCompBonus = checked(s.phaseEventCompBonus + plan.competitionBonusDelta.GetValueOrDefault());
            if (plan.juryDelta.HasValue)
                s.jurySentiment = WebJurySentiment.ShiftAllJurorSentiment(s.jurySentiment, plan.juryDelta.Value, plan.juryReason, s.week);
            Log(s, "diary-room", plan.logDescription, s.playerId);
            ResolveDiary(s, c, true);
        }

        private static void ResolveDiary(EpisodeState s, EpisodeCommand c, bool reflected)
        {
            CheckDiaryCommand(s, c);
            s.lastDiaryRoomWeek = s.pendingDiary.week;
            s.resolvedDiaryIds.Add(s.pendingDiary.id); s.pendingDiary = null;
            if (!reflected) Log(s, "diary-skip", "You skipped this private reflection. No persona or impression change was applied.", s.playerId);
        }

        private static void ResolveOathOpportunity(EpisodeState s, EpisodeCommand c, bool swear)
        {
            Require(s.Find(s.playerId).status == ContestantStatus.Active && (s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign), "Loyalty declarations belong to active-player social time.");
            Require(s.oathOpportunities.Contains(c.targetId) && s.Find(c.targetId)?.status == ContestantStatus.Active, "No unclaimed loyalty opportunity exists with that housemate.");
            s.oathOpportunities.Remove(c.targetId);
            if (!swear) { Log(s, "loyalty-declined", "You chose not to make a loyalty declaration to " + Name(s, c.targetId) + ".", s.playerId); return; }
            Require(!s.loyaltyOaths.Any(o => (o.playerId == s.playerId && o.targetId == c.targetId) || (o.targetId == s.playerId && o.playerId == c.targetId)), "A loyalty oath already exists for this pair.");
            // Source milestone: a PLAYER declaration, not an NPC promise/acceptance roll.
            s.loyaltyOaths.Add(new WebOathRecord { playerId = s.playerId, targetId = c.targetId, week = s.week, timestamp = s.nextSequence++ });
            WriteScore(s, s.playerId, c.targetId, 5); // RelationshipCore one-way update, no reciprocal draw or arc.
            var edge = s.relationships.Single(r => r.fromId == s.playerId && r.toId == c.targetId);
            edge.lastInteractionWeek = s.week; edge.notes.Add("loyalty-oath");
            if (edge.notes.Count > 20) edge.notes.RemoveRange(0, edge.notes.Count - 20);
            Log(s, "loyalty-oath", "You declared loyalty to " + Name(s, c.targetId) + ". This records your commitment, not a guaranteed promise from them.", s.playerId, c.targetId);
        }

        private static WebOathSnapshot OathSnapshot(EpisodeState s) => new WebOathSnapshot
        {
            week = s.week,
            actors = s.contestants.Select(c => new WebOathActor { id = c.id, name = c.name, isPlayer = c.isPlayer,
                status = c.status == ContestantStatus.RunnerUp ? "Runner-Up" : c.status.ToString() }).ToList(),
            oaths = s.loyaltyOaths.Select(o => new WebOathRecord { playerId = o.playerId, targetId = o.targetId, week = o.week, timestamp = o.timestamp }).ToList(),
            relationships = s.relationships.Select(r => new WebOathEdge { fromId = r.fromId, toId = r.toId, score = r.score }).ToList()
        };

        private static void ApplyOathPlan(EpisodeState s, WebOathBreakPlan plan)
        {
            if (!plan.broken) return;
            s.loyaltyOaths = plan.remainingOathIndices.Select(i => s.loyaltyOaths[i]).ToList();
            foreach (var ripple in plan.ripples)
            {
                if (ripple.createVictimEdge) WriteScore(s, ripple.fromId, ripple.victimId, 0);
                WriteScore(s, ripple.fromId, ripple.toId, ripple.delta);
                var edge = s.relationships.Single(r => r.fromId == ripple.fromId && r.toId == ripple.toId);
                edge.lastInteractionWeek = ripple.lastInteractionWeek; edge.notes.Add(ripple.note);
                if (edge.notes.Count > 256) edge.notes.RemoveAt(0);
            }
            foreach (var arc in plan.arcChanges) Arc(s, arc.npcId, arc.delta, arc.reason);
            Log(s, plan.factType, plan.logDescription);
        }
    }
}
