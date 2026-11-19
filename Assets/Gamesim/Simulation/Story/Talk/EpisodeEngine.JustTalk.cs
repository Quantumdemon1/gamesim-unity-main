using System;
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
            contact.agreement = Math.Max(-TalkRules.MostAgreement, Math.Min(TalkRules.MostAgreement, contact.agreement + result.Landing));
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
        }
    }
}
