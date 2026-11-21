using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The free "Just talk" command (talk rules 1, slice S1): the checks, the cap, the gains, the reveal and the spend, exactly as
    /// <see cref="FreeTalk.Result"/> computes them. Free: no action is spent and nothing is drawn from the season's stream (the
    /// warmth's reciprocal draws come off a keyed story stream). Writes only slice 1's saved fields, inside their bounds.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Free talk's rules 2 (ruling T7): each Friend or closer the player has just put on the block thinks the further
        /// <see cref="TalkFriendship.BetrayalCost"/> less of them, on top of the nomination's own costs. The player's own act, through
        /// the player's relationship path on a keyed story stream: nothing is drawn from the season's stream. The list is
        /// <see cref="TalkFriendship.FriendsNamed"/>'s, read before the nomination's effects; empty below talk rules 2.
        /// </summary>
        private static void FriendsBetrayed(EpisodeState s, List<string> friendIds)
        {
            if (friendIds == null) return;
            foreach (var id in friendIds)
                ChangeOnStream(s, id, s.playerId, TalkFriendship.BetrayalCost, StoryRandom.Stream(s, "talk:v1:betray:" + id + ":" + s.week));
        }

        private static void ResolveJustTalk(EpisodeState s, EpisodeCommand c)
        {
            // Talk rules 0: refused before anything else is read, written or logged.
            Require(FreeTalk.On(s), FreeTalk.OffReason);
            var offer = FreeTalk.Offer(s, c.targetId);
            Require(offer.Available, offer.Reason);
            Require(FreeTalk.TryParse(c.text, out string talkId, out string tone)
                && string.Equals(talkId, offer.Talk.Id, StringComparison.Ordinal), FreeTalk.NotOnOfferReason);
            var result = FreeTalk.Result(s, c.targetId, talkId, tone);
            Require(result != null, FreeTalk.ToneReason);

            var npc = s.Find(c.targetId);
            var contact = FreeTalk.Contact(s, npc.id);
            if (contact == null)
            {
                Require(s.story.contacts.Count < s.contestants.Count, FreeTalk.NotOnOfferReason);
                contact = new ContactState { npcId = npc.id };
                s.story.contacts.Add(contact);
            }

            // The cap, by attempt, before any gain (R5): one counted talk per housemate per window.
            int key = FreeTalk.WindowKey(s);
            contact.talkWindow = key;
            contact.talks = Math.Min(TalkRules.MostCount, contact.talks + 1);

            // Warmth through the keyed story stream, arcless: no arc step, no history row, no oath (R5).
            if (result.Warmth != 0)
                ChangeOnStream(s, s.playerId, npc.id, result.Warmth, StoryRandom.Stream(s, "talk:v1:recip:" + npc.id + ":" + key),
                    arcless: true);

            contact.rapport = result.RapportAfter;
            contact.talkTenths = result.TenthsAfter;
            contact.agreement = result.AgreementAfter;
            contact.priorTone = contact.lastTone;
            contact.lastTone = tone;
            // Spent once, with the tone it was answered in; a repeat of "the usual" adds nothing (R9).
            if (!result.Repeat && contact.spentTalks.Count < TalkRules.MostSpentTalks
                && !contact.spentTalks.Any(e => TalkRules.SpentId(e) == talkId))
                contact.spentTalks.Add(TalkRules.Spent(talkId, tone));

            // A fact learned through the lore's own path, with its log line, as a paid conversation's is.
            if (result.FactId != null && Lore.Learn(s, result.FactId))
                Log(s, "story-lore", "You learned something about " + npc.name + ": " + result.Fact, s.playerId, npc.id);

            // Where they stand: one dated snapshot a week.
            if (!string.IsNullOrEmpty(result.StandingLine))
            {
                contact.standingWeek = s.week;
                contact.standing = result.StandingLine;
            }

            // Talk rules 2 (slice S2): the lean moves by the tone (T4), the one field slice 2 writes; a stage talk writes its one
            // event row (T5; it is spent above, so it is never offered again). The favour's warmth is result.Warmth, applied above as
            // one arcless act on the story stream (T6).
            if (TalkChemistry.On(s))
            {
                s.story.talkLean = result.LeanAfter;
                if (!string.IsNullOrEmpty(result.StageEvent))
                    Log(s, TalkFriendship.StageEventKind, result.StageEvent, s.playerId, npc.id);
            }
        }
    }
}
