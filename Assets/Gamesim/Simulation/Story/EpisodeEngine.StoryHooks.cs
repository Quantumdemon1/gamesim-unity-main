using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Where the story system meets the rest of the engine: the grudge writers at nominations,
    /// vetoes, broken words and votes; the conversation hook; the week turn; the social close and
    /// production's removal; and the systems that run at particular anchors.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        // ---------------------------------------------------------------- grudge writers (M2)
        //
        // All behind the Grudges rules version, none of them rolls, and only NPCs hold grudges -
        // the player's own feelings are the player's to act on.

        /// <summary>
        /// A nomination: each initial nominee resents the Head of Household. Seventy, or 84 when an
        /// active alliance joins them. The web's table (<c>grudge-system.ts</c>) - its live
        /// nomination path records no event for its weekly detector to find, so this completes the
        /// web's intent rather than copying its behaviour (21 §7, D-A).
        /// </summary>
        private static void StoryNominated(EpisodeState s, IEnumerable<string> nominees)
        {
            if (!StoryAt(s, StoryRules.Grudges) || string.IsNullOrEmpty(s.hohId)) return;
            foreach (var id in nominees)
                if (id != s.playerId && id != s.hohId)
                    Grudges.Add(s, id, s.hohId, 70, GrudgeCauses.Nominated, alliedMultiplier: true);
        }

        /// <summary>
        /// A veto replacement resents the Head of Household who named them (70, ×1.2 allied) and the
        /// veto holder who forced it (40, native). The direction is the intuitive one, not the web
        /// fold's argument-order inversion (21 §7, D-B).
        /// </summary>
        private static void StoryReplacement(EpisodeState s, string replacement)
        {
            if (!StoryAt(s, StoryRules.Grudges) || replacement == null || replacement == s.playerId) return;
            if (s.hohId != null && s.hohId != replacement) Grudges.Add(s, replacement, s.hohId, 70, GrudgeCauses.Replacement, alliedMultiplier: true);
            if (s.vetoHolderId != null && s.vetoHolderId != s.hohId && s.vetoHolderId != replacement)
                Grudges.Add(s, replacement, s.vetoHolderId, 40, GrudgeCauses.ReplacementVeto);
        }

        /// <summary>
        /// Somebody walked out of an alliance: whoever they left behind resents them at the web's
        /// eighty. The weekly settle's quiet endings write nothing (SESSION-HANDOFF:913 stands for
        /// those); only a named betrayer does.
        /// </summary>
        private static void StoryAllianceLeft(EpisodeState s, string leftBehindId, string betrayerId)
        {
            if (!StoryAt(s, StoryRules.Grudges) || leftBehindId == s.playerId) return;
            Grudges.Add(s, leftBehindId, betrayerId, AllianceLeftGrudge, GrudgeCauses.AllianceBetrayed);
        }

        /// <summary>
        /// A broken word: the wronged party resents the breaker, scaled by how dangerous the breaker
        /// looks (the web fold's ×(1 + threat/200)). When the player is one of the two, it also
        /// becomes a reckoning: the next conversation between them is about what happened.
        /// </summary>
        private static void StoryWordBroken(EpisodeState s, string wrongedId, string breakerId, string cause, double severity)
            => StoryWordBrokenBeforeSafetyEffects(s, wrongedId, breakerId, cause, severity, null);

        private static void StoryWordBrokenBeforeSafetyEffects(EpisodeState s, string wrongedId, string breakerId,
            string cause, double severity, IReadOnlyList<string> excludedSafetyEffects)
            => StoryWordBrokenBeforeCommitmentEffects(s, wrongedId, breakerId, cause, severity, excludedSafetyEffects, null);

        /// <summary>The same, a reveal's own Vote effects excluded from the breaker's reputation too (vote family V5c).</summary>
        private static void StoryWordBrokenBeforeCommitmentEffects(EpisodeState s, string wrongedId, string breakerId,
            string cause, double severity, IReadOnlyList<string> excludedSafetyEffects, IReadOnlyCollection<string> excludedVoteEffects)
        {
            if (!StoryOn(s) || wrongedId == null || breakerId == null || wrongedId == breakerId) return;
            if (StoryAt(s, StoryRules.Grudges) && wrongedId != s.playerId)
                Grudges.Add(s, wrongedId, breakerId, Grudges.ThreatScaledBeforeCommitmentEffects(s, severity, wrongedId, breakerId,
                    excludedSafetyEffects, excludedVoteEffects), cause);
            if (breakerId == s.playerId) AddReckoning(s, wrongedId, cause, true);
            else if (wrongedId == s.playerId) AddReckoning(s, breakerId, cause, false);
        }

        /// <summary>A lie came back: the person it was about resents the liar at fifty.</summary>
        private static void StoryLieDiscovered(EpisodeState s, string aboutId, string liarId)
        {
            if (!StoryAt(s, StoryRules.Grudges) || aboutId == s.playerId) return;
            Grudges.Add(s, aboutId, liarId, 50, GrudgeCauses.LieDiscovered);
        }

        /// <summary>
        /// The votes are read: somebody voted out of their own alliance holds it against the ally who
        /// voted against them - forty, ×1.2 while the alliance stands. The person voted against holds
        /// it, which is the web's rule.
        /// </summary>
        private static void StoryVotesRevealed(EpisodeState s, string evicted)
        {
            if (!StoryAt(s, StoryRules.Grudges)) return;
            foreach (var vote in s.votes)
                if (vote.targetId != s.playerId && vote.voterId != vote.targetId && s.Allied(vote.voterId, vote.targetId))
                    Grudges.Add(s, vote.targetId, vote.voterId, 40, GrudgeCauses.VotedAgainst, alliedMultiplier: true);

            // The flip, seen from the chair (A6, native): an NPC Head of Household whose real target
            // survived resents the people who were meant to be with them - allies and anyone with a
            // vote deal - and voted the other way. Everybody else voting their own mind is the game.
            var hoh = StoryPeople.NpcHoh(s);
            if (hoh == null || evicted == null || s.nominees.Count != 2) return;
            string target = s.nominees.OrderBy(id => s.Score(hoh.id, id)).ThenBy(id => id, StringComparer.Ordinal).First();
            if (target == evicted) return;
            // Mode 2 (vote family V4): the vote deals are canonical rows; read them with the raw ones, as one list.
            var deals = UnifiedVoteStore.On(s) ? UnifiedVoteReferences.DealsUnchecked(s) : (IReadOnlyList<DealState>)s.deals;
            foreach (var vote in s.votes.Where(v => v.voterId != hoh.id && v.targetId != target))
            {
                bool meantToBeWithThem = s.Allied(hoh.id, vote.voterId) || deals.Any(d =>
                    (d.type == DealKind.VoteTogether || d.type == DealKind.VoteEvict || d.type == DealKind.TargetAgreement)
                    && (d.status == DealStatus.Active || d.status == DealStatus.Broken || d.status == DealStatus.Fulfilled) && d.week == s.week
                    && ((d.proposerId == hoh.id && d.recipientId == vote.voterId) || (d.proposerId == vote.voterId && d.recipientId == hoh.id)));
                if (meantToBeWithThem) Grudges.Add(s, hoh.id, vote.voterId, 40, GrudgeCauses.VotedAgainst);
            }
        }

        /// <summary>A broken word between the player and a houseguest, raised the next time they talk.</summary>
        private static void AddReckoning(EpisodeState s, string npcId, string cause, bool betrayerIsPlayer)
        {
            if (s?.story == null || npcId == null || npcId == s.playerId) return;
            s.story.reckonings.RemoveAll(r => r.npcId == npcId);
            if (s.story.reckonings.Count >= 64) s.story.reckonings.RemoveAt(0);
            s.story.reckonings.Add(new ReckoningState
            {
                npcId = npcId, cause = Trim(string.IsNullOrEmpty(cause) ? "story" : cause, 60),
                betrayerIsPlayer = betrayerIsPlayer, week = s.week,
            });
        }

        // ---------------------------------------------------------------- conversations

        /// <summary>
        /// After one of the player's conversations: contact and rapport, a lore reveal, a waiting
        /// reckoning, a beat waiting on this conversation, and the web's conversation trigger.
        /// </summary>
        internal static void StoryConversation(EpisodeState s, string npcId, EpisodeCommandKind kind, string aboutId = null)
        {
            if (!StoryOn(s)) return;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer) return;
            // Only a houseguest still in the house can be what a conversation was about.
            if (aboutId != null && (aboutId == npcId || s.Find(aboutId)?.status != ContestantStatus.Active)) aboutId = null;

            var contact = s.story.contacts.FirstOrDefault(c => c.npcId == npcId);
            if (contact == null && s.story.contacts.Count < s.contestants.Count)
            {
                contact = new ContactState { npcId = npcId };
                s.story.contacts.Add(contact);
            }
            if (contact != null)
            {
                if (contact.lastWeek != s.week) contact.weekCount = 0;
                contact.weekCount = Math.Min(100, contact.weekCount + 1);
                contact.lastWeek = s.week;
                if (StoryAt(s, StoryRules.Lore)) contact.rapport = Math.Min(100, contact.rapport + Lore.RapportFor(kind, npc));
            }

            if (StoryAt(s, StoryRules.Lore))
            {
                // A fresh-season personal chat learns up to two distinct, currently reachable
                // facts. Re-read after each learn, preserving depth/secret/catalogue/cap rules.
                // This is still one conversation and one story trigger, not two social actions.
                int reveals = ConversationIntentRules.RevealLimit(s, kind);
                for (int i = 0; i < reveals; i++)
                {
                    var fact = Lore.NextReveal(s, npcId, kind, FinishedPersonalBeat(s, npcId));
                    if (fact == null || !Lore.Learn(s, fact.id)) break;
                    Log(s, "story-lore", "You learned something about " + npc.name + ": " + fact.text, s.playerId, npcId);
                }
            }

            // Only one thing opens per conversation, in this order: the reckoning, a waiting beat, a new thread.
            var reckoning = s.story.reckonings.FirstOrDefault(r => r.npcId == npcId);
            if (reckoning != null && StoryAt(s, StoryRules.Grudges))
            {
                var arc = StoryCatalog.Find(reckoning.betrayerIsPlayer ? "the-reckoning" : "the-reckoning-yours");
                var ctx = new StoryContext(s, StoryAnchors.Conversation, npcId, kind, aboutId);
                var binding = Castable(s, ctx, arc);
                if (binding != null)
                {
                    s.story.reckonings.Remove(reckoning);
                    StartCycle(s, arc, binding, StoryAnchors.Conversation);
                    return;
                }
            }

            foreach (var cycle in RunningCycles(s).Where(x => x.nextAnchor == StoryAnchors.Conversation).ToList())
            {
                var template = StoryCatalog.Find(cycle.templateId);
                var beat = template?.Beat(cycle.beatId);
                if (beat == null) continue;
                var view = new StoryCycle(cycle, template);
                if (beat.talkRole != null && view.Role(beat.talkRole) != npcId) continue;
                if (beat.ready != null && !beat.ready(new StoryContext(s, StoryAnchors.Conversation, npcId, kind, aboutId), view)) continue;
                // The web always plays a storyline's next chapter when you talk to somebody in it,
                // so a waiting conversation beat is not held to the weekly budget - only to one card at a time.
                if (!beat.NpcHeld && !AirtimeFor(s, beat.surface, mustFire: true)) return;
                Fire(s, view, beat, StoryAnchors.Conversation, true, chained: true);
                return;
            }

            TryStartFromConversation(s, npcId, kind, aboutId);
        }

        /// <summary>
        /// The web's conversation trigger (<c>storyline-trigger-utils.ts</c>): a chance by topic -
        /// small talk 10%, a personal chat 20%, game talk 30%, venting 35%, a secret 40% - scaled by
        /// how strong the relationship is either way, plus a broken deal or a new alliance with them
        /// this week, capped at 65% (<see cref="ConversationStoryChance"/>). Keyed, never the season's stream.
        /// </summary>
        private static void TryStartFromConversation(EpisodeState s, string npcId, EpisodeCommandKind kind, string aboutId)
        {
            double chance = ConversationStoryChance(s, npcId, kind);
            if (chance <= 0) return;
            if (!StoryRandom.Chance(s, "w" + s.week + ":talk:" + npcId + ":" + s.nextSequence, chance)) return;

            var ctx = new StoryContext(s, StoryAnchors.Conversation, npcId, kind, aboutId);
            var candidates = new List<(ArcTemplate template, ArcBinding binding, double weight)>();
            foreach (var template in StoryCatalog.All)
            {
                if (template.conversationTopics == null || Array.IndexOf(template.conversationTopics, kind) < 0) continue;
                var binding = Castable(s, ctx, template);
                if (binding == null) continue;
                double weight = template.weight == null ? 10 : template.weight(ctx, binding);
                if (weight > 0) candidates.Add((template, binding, weight));
            }
            if (candidates.Count == 0) return;
            double roll = StoryRandom.Unit(s, "w" + s.week + ":talk-pick:" + npcId + ":" + s.nextSequence) * candidates.Sum(c => c.weight);
            foreach (var candidate in candidates.OrderBy(c => c.template.id, StringComparer.Ordinal))
            {
                if (roll < candidate.weight) { StartCycle(s, candidate.template, candidate.binding, StoryAnchors.Conversation); return; }
                roll -= candidate.weight;
            }
        }

        /// <summary>
        /// The chance a conversation with <paramref name="npcId"/> on <paramref name="kind"/> starts a storyline, as
        /// <see cref="TryStartFromConversation"/> draws on it with the state as it stands when the conversation has been had:
        /// zero for a topic that starts none. Pure: it reads the state and draws nothing.
        /// </summary>
        public static double ConversationStoryChance(EpisodeState s, string npcId, EpisodeCommandKind kind)
        {
            double chance = TopicChance(kind);
            if (chance <= 0) return 0;
            double strength = Math.Abs(s.Score(s.playerId, npcId));
            chance *= strength >= 60 ? 1.8 : strength >= 40 ? 1.4 : strength >= 20 ? 1.1 : 0.6;
            // Mode 2 (vote family V5c) as mode 1: the deal view, a canonical Safety deal dated by its settlement and every
            // other deal - a canonical vote deal among them - by the week it was struck, as mode 1's raw rows are.
            bool unifiedSafety = UnifiedCommitments.SafetyAuthorityOn(s);
            var deals = unifiedSafety ? CommitmentReferences.Deals(s) : s.deals;
            // A broken canonical deal needs an actual decision receipt, not just a status label.
            // Validate its history without turning unilateral promises into this source's deal trigger.
            if (unifiedSafety && s.unifiedCommitments.Any(row => row.sourcePolicy == UnifiedCommitments.DealPolicy
                && row.status == DealStatus.Broken)) UnifiedCommitmentHistory.Breaches(s);
            if (deals.Any(d => d.status == DealStatus.Broken
                && (unifiedSafety && d.type == DealKind.SafetyAgreement ? CommitmentReferences.ReceiptWeek(s, d.id, d.week) : d.week) == s.week
                && ((d.proposerId == npcId && d.recipientId == s.playerId) || (d.proposerId == s.playerId && d.recipientId == npcId))))
                chance += 0.25;
            if (s.alliances.Any(a => a.active && a.members.Contains(npcId) && a.members.Contains(s.playerId)
                                     && s.events.Any(e => e.kind == "alliance" && e.week == s.week && e.audienceIds.Contains(npcId))))
                chance += 0.20;
            return Math.Min(0.65, chance);
        }

        /// <summary>The web's base chance for a conversation to start a storyline, by topic.</summary>
        public static double TopicChance(EpisodeCommandKind kind)
        {
            switch (kind)
            {
                case EpisodeCommandKind.SmallTalk:
                case EpisodeCommandKind.Talk: return 0.10;
                case EpisodeCommandKind.PersonalChat:
                case EpisodeCommandKind.RelationshipBuilding: return 0.20;
                case EpisodeCommandKind.DiscussGame:
                case EpisodeCommandKind.StrategicDiscussion: return 0.30;
                case EpisodeCommandKind.VentAbout: return 0.35;
                case EpisodeCommandKind.ShareSecret: return 0.40;
                // The room acts (D-E) are conversations too, practice excepted: nobody talks during a drill.
                case EpisodeCommandKind.PillowTalk:
                case EpisodeCommandKind.InviteUp: return 0.20;
                case EpisodeCommandKind.AllianceMeet: return 0.30;
                case EpisodeCommandKind.PlayAGame:
                case EpisodeCommandKind.PublicDefense:
                case EpisodeCommandKind.Cook: return 0.10;
                default: return 0;
            }
        }

        /// <summary>Whether the player has finished a personal story with somebody: the key to depth three.</summary>
        private static bool FinishedPersonalBeat(EpisodeState s, string npcId) =>
            s.storylines.Any(x => x.lane == StoryLanes.Personal && x.status == StorylineStatus.Completed
                                  && x.cast.Any(r => r.contestantId == npcId) && x.path.Any(p => p.result != StoryResults.Lapsed));

        // ---------------------------------------------------------------- the week

        /// <summary>After the week turns: grudges fade, stress eases, clean weeks count, the Have-Not week ends.</summary>
        private static void StoryWeekTurn(EpisodeState s)
        {
            if (s?.story == null || s.story.rulesStartWeek < 1) return;
            if (s.story.rulesVersion >= StoryRules.Grudges) Grudges.Age(s);
            if (s.story.rulesVersion >= StoryRules.Bonds)
            {
                // Stress with a reader needs a relief valve, or one nomination penalises a whole
                // season: a step off for anyone not nominated last week, and another with a partner
                // still in the house.
                foreach (var who in s.Active.ToList())
                {
                    if (!who.nominationWeeks.Contains(s.week - 1)) Personality.AdjustStress(who, -1);
                    if (s.story.bonds.Any(b => (b.aId == who.id || b.bId == who.id) && BondStatus.Holds(b.status)
                                               && (b.kind == BondKinds.RideOrDie || b.kind == BondKinds.Showmance)))
                        Personality.AdjustStress(who, -1);
                }
            }
            if (s.story.rulesVersion >= StoryRules.Production) Production.Age(s);
            s.story.cooldowns.RemoveAll(c => c.untilWeek <= s.week);
            foreach (var contact in s.story.contacts) contact.weekCount = 0;
            s.story.reckonings.RemoveAll(r => r.week < s.week - 3);
        }

        /// <summary>
        /// The social window closes: every beat still open lapses, production carries out a removal
        /// it has decided on, and at the final three every story ends.
        /// </summary>
        private static void StorySocialClose(EpisodeState s)
        {
            if (s?.story == null) return;
            StoryLapse(s, StoryAnchors.SocialClose);
            if (StoryAt(s, StoryRules.Production) && !string.IsNullOrEmpty(s.story.pendingRemovalId))
                Expel(s, s.story.pendingRemovalId);
            // A thread (plan 31) is heading for the jury: it ends at the finale, not here.
            if (s.Active.Count() <= 3)
                foreach (var cycle in RunningCycles(s).Where(x => x.lane != StoryLanes.Thread).ToList()) EndCycle(s, cycle, "final-three");
        }

        /// <summary>
        /// Production removes somebody. Carried out as the social window closes, after the diary
        /// check, so no eviction can intervene and no juror row ever needs dropping. Everything that
        /// named them is cleaned up in the same step: alliances, oaths, promises, deals, bonds,
        /// hooks, grudges and stories. They take no jury seat.
        /// </summary>
        private static void Expel(EpisodeState s, string id)
        {
            var who = s.Find(id);
            s.story.pendingRemovalId = null;
            if (who == null || who.status != ContestantStatus.Active || s.Active.Count() < 4) return;
            who.status = ContestantStatus.Expelled;
            if (s.story.removals.Count < s.contestants.Count)
                s.story.removals.Add(new RemovalState { contestantId = id, week = s.week, reasonId = "conduct" });
            NpcAlliances.EndBroken(s);
            s.oathOpportunities.Remove(id);
            s.loyaltyOaths.RemoveAll(o => o.playerId == id || o.targetId == id);
            ResolveUnifiedVoteExpiry(s, UnifiedCommitmentExpiry.Expulsion, id);
            if (UnifiedCommitments.SafetyAuthorityOn(s)) ResolveUnifiedSafetyExpiry(s, UnifiedCommitmentExpiry.Expulsion, id);
            foreach (var promise in s.promises.Where(p => p.status == PromiseStatus.Active && (p.fromId == id || p.toId == id)))
                promise.status = PromiseStatus.Expired;
            foreach (var deal in s.deals.Where(d => DealStatus.Binds(d.status) && (d.proposerId == id || d.recipientId == id || d.targetId == id)))
                deal.status = DealStatus.Expired;
            Bonds.EndAll(s, id);
            Hooks.Void(s, id);
            Grudges.Forget(s, id);
            s.story.reckonings.RemoveAll(r => r.npcId == id);
            foreach (var cycle in RunningCycles(s).Where(x => x.cast.Any(r => r.contestantId == id)).ToList())
                EndCycle(s, cycle, "left-house");
            Log(s, StoryLog.Expulsion, who.isPlayer
                ? "Production removed you from the house. You will follow the rest of the season from outside it."
                : who.name + " has been removed from the house by production.");
        }

        /// <summary>
        /// Whether conduct options are open in this beat: never to a player on an alumni card, never
        /// against an alumnus, and never past a removal already pending.
        /// </summary>
        private static bool ConductOpen(EpisodeState s, StoryCycle cycle)
        {
            if (!StoryAt(s, StoryRules.Production)) return false;
            if (StoryPeople.IsRealPerson(s, s.playerId)) return false;
            if (cycle.record.cast.Any(r => StoryPeople.IsRealPerson(s, r.contestantId))) return false;
            if (!string.IsNullOrEmpty(s.story.pendingRemovalId)) return false;
            return true;
        }

        // ---------------------------------------------------------------- anchor systems

        /// <summary>The systems that run at particular anchors, before any cycle pulses.</summary>
        private static void StorySystemsAt(EpisodeState s, string anchor)
        {
            if (UnifiedCommitmentHearings.WritesOn(s)) UnifiedCommitmentHearings.RequireValid(s);
            if (anchor == StoryAnchors.EvictionNight && StoryAt(s, StoryRules.Bonds)) NpcShowmancePass(s);
            // The weekly leak (WAVE-D-NPC-PACTS-PLAN §2.3): after the showmances, before the gossip, so the
            // same anchor's gossip can carry a pact that has just got out.
            if (anchor == StoryAnchors.EvictionNight && AllianceLeaks.On(s)) LeakPass(s);
            if (StoryAt(s, StoryRules.Bonds) && anchor != StoryAnchors.Conversation)
                foreach (var (fact, listener) in Knowledge.Spread(s, anchor))
                {
                    if (listener == s.playerId) Log(s, StoryLog.Whisper, Whisper(s, fact), s.playerId);
                    // Your word in the house (ACTIONS-DEALS-ALLIANCES-PLAN C8): a houseguest the gossip
                    // tells of the player's broken word thinks less of them, and the player hears who.
                    else if (YourWord.On(s) && YourWord.IsYours(s, fact)) HeardOfYourWord(s, fact, listener);
                    // Double-dealing (WAVE-D-NPC-PACTS-PLAN §2.3): an ally of the player's the gossip tells
                    // of the player's other pact holds it against them, and the player hears who.
                    else if (AllianceLeaks.On(s)) CaughtDoubleDealing(s, fact, listener);
                }
        }

        /// <summary>What the player hears when a fact reaches them through the house.</summary>
        private static string Whisper(EpisodeState s, HouseFactState fact)
        {
            string actor = s.Find(fact.actorId)?.name ?? "somebody", subject = s.Find(fact.subjectId)?.name ?? "somebody";
            switch (fact.kind)
            {
                case FactKinds.Alliance:
                {
                    // Under the leak rules the whisper names everyone in it (WAVE-D-NPC-PACTS-PLAN §2.3), so it
                    // can only be about this pact; for a pair it is the two names it always said.
                    var pact = AllianceLeaks.On(s) ? s.alliances.FirstOrDefault(a => a?.id == fact.refId) : null;
                    if (pact != null) return AllianceLeaks.WhisperLine(s, pact);
                    return "Word in the house: " + actor + " and " + subject + " are working together.";
                }
                case FactKinds.Couple: return "Word in the house: " + actor + " and " + subject + " are more than friends.";
                case FactKinds.BrokenWord: return "Word in the house: " + actor + " went back on their word to " + subject + ".";
                case FactKinds.Strike: return "Word in the house: " + actor + " was called to the Diary Room and came back quiet.";
                default: return "Something is going round the house about " + actor + ".";
            }
        }

        /// <summary>
        /// NPC showmances form on their own: at most one a week, at most two at once, the warmest
        /// open pair of fictional houseguests. Through the ledger only - no roll, no arc - and the
        /// house does not know yet; the couple is a private fact until somebody notices.
        /// </summary>
        /// <summary>
        /// How warm a pair of houseguests has to be, both ways, before they pair off. Measured, not
        /// ported (plan §2): houseguests warm to each other slowly here - the warmest pair of a
        /// season rarely passes thirty - so on the pacing sweep this floor gives 0.64 showmances a
        /// season in the default eight and 0.99 in a house of twelve (target 0.5-1.5), and none in
        /// All-Stars, where nobody is romance-open.
        /// </summary>
        public const double NpcShowmanceFloor = 5;

        private static void NpcShowmancePass(EpisodeState s)
        {
            if (!s.story.romanceStorylines) return;
            if (s.story.bonds.Count(b => b.kind == BondKinds.Showmance && BondStatus.Holds(b.status)) >= 2) return;
            if (s.story.bonds.Any(b => b.kind == BondKinds.Showmance && b.sinceWeek == s.week)) return;
            var open = s.Active.Where(c => !c.isPlayer && Lore.RomanceOpen(s, c.id) && Bonds.ShowmancePartner(s, c.id) == null).ToList();
            (ContestantState a, ContestantState b, double warmth) best = (null, null, 0);
            for (int i = 0; i < open.Count; i++)
                for (int j = i + 1; j < open.Count; j++)
                {
                    double warmth = StoryPeople.Mutual(s, open[i].id, open[j].id);
                    if (warmth >= NpcShowmanceFloor && (best.a == null || warmth > best.warmth)) best = (open[i], open[j], warmth);
                }
            if (best.a == null) return;
            var bond = Bonds.Form(s, best.a.id, best.b.id, BondKinds.Showmance, BondStatus.Private, null);
            if (bond == null) return;
            RelationshipLedger.Move(s, best.a.id, best.b.id, 4);
            RelationshipLedger.Record(s, best.a.id, best.b.id, "showmance", 4, "Late nights together.");
            var fact = new HouseFactState
            {
                id = "fact-" + s.nextSequence++, kind = FactKinds.Couple, actorId = best.a.id, subjectId = best.b.id,
                refId = bond.id, visibility = FactVisibility.Whispered, week = s.week,
            };
            fact.knowers.Add(best.a.id);
            fact.knowers.Add(best.b.id);
            // A full list makes room as every writer's does: under the commitment rules never by dropping
            // an alliance's fact or the player's broken word (X14).
            Knowledge.MakeRoom(s);
            s.story.facts.Add(fact);
        }

        // ---------------------------------------------------------------- the backdoor (M3)

        /// <summary>The backdoor arc. Its running cycle is the plan: who the week is really aimed at.</summary>
        internal const string BackdoorArc = "the-backdoor";

        /// <summary>
        /// The NPC Head of Household's backdoor: when their lowest-ranked candidate has won
        /// something, or leads the candidates on competition, and six or more are in the house, they
        /// nominate the next two instead and plan to name the real target as the replacement if the
        /// veto is used. From v5 a couple the Head of Household knows about is the favourite target:
        /// the partner with more wins. Deterministic, no roll. Returns the target, or null.
        /// </summary>
        private static string NpcBackdoorTarget(EpisodeState s, List<ContestantState> ranked)
        {
            if (!StoryAt(s, StoryRules.Staging) || ranked.Count < 3 || s.Active.Count() < 6) return null;
            // A backdoor is a schemer's move: a Head of Household who plans (Steady) or does not
            // mind lying to two pawns (Honest below zero). Everybody else nominates who they mean.
            var planner = Personality.Of(s.Find(s.hohId));
            if (planner.Steady < 2 && planner.Honest > -1) return null;
            var target = ranked[0];
            if (StoryAt(s, StoryRules.Bonds))
                foreach (var candidate in ranked.Take(3))
                {
                    string partner = Bonds.ShowmancePartner(s, candidate.id);
                    var other = partner == null || partner == s.hohId ? null : ranked.FirstOrDefault(c => c.id == partner);
                    var couple = other == null ? null : Knowledge.Of(s, FactKinds.Couple, StoryConsumers.CoupleRef(s, candidate.id, partner));
                    if (couple == null || !Knowledge.Knows(couple, s.hohId)) continue;
                    target = Wins(other) > Wins(candidate) ? other : candidate;
                    break;
                }
            bool winner = Wins(target) > 0 || ranked.All(c => c.stats.competition <= target.stats.competition);
            return winner ? target.id : null;
        }

        private static int Wins(ContestantState who) => who.hohWins + who.vetoWins;

        /// <summary>
        /// Starts this week's backdoor once the nominations are made: the plan, its target, its two
        /// pawns, and whichever seat the player holds (the Head of Household, a pawn, the target or
        /// none). Rule-driven, so it is neither drawn from the pool nor held behind the Game lane,
        /// and nobody's seat is left out: the player's is simply the role nobody plays.
        /// </summary>
        private static void StartBackdoor(EpisodeState s, string targetId, List<string> pawns)
        {
            if (!StoryAt(s, StoryRules.Staging) || targetId == null || pawns == null || pawns.Count != 2) return;
            var arc = StoryCatalog.Find(BackdoorArc);
            if (arc == null || s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            foreach (var stale in RunningCycles(s).Where(x => x.templateId == BackdoorArc).ToList()) EndCycle(s, stale, "stale");
            string NpcOnly(string id) => id == s.playerId ? null : id;
            var binding = StoryCatalog.Bind()
                .With("HOH", NpcOnly(s.hohId)).With("TARGET", NpcOnly(targetId))
                .With("PAWN1", NpcOnly(pawns[0])).With("PAWN2", NpcOnly(pawns[1]));
            var cycle = StartCycle(s, arc, binding, StoryAnchors.NomsSet);
            if (cycle != null && StorylineStatus.Running(cycle.status)) new StoryCycle(cycle, arc).SetVar("planned", 1);
        }

        /// <summary>
        /// The target of this week's backdoor plan, or null: the player when the plan's target part
        /// is nobody's but an NPC holds the Head of Household.
        /// </summary>
        public static string BackdoorPlanned(EpisodeState s)
        {
            if (!StoryAt(s, StoryRules.Staging)) return null;
            var record = RunningCycles(s).FirstOrDefault(x => x.templateId == BackdoorArc && x.week == s.week);
            var arc = StoryCatalog.Find(BackdoorArc);
            if (record == null || arc == null) return null;
            var cycle = new StoryCycle(record, arc);
            if (cycle.Var("planned") != 1) return null;
            return cycle.Role("TARGET") ?? (cycle.Role("HOH") != null ? s.playerId : null);
        }

        // ---------------------------------------------------------------- production's ladder (M6)

        /// <summary>
        /// A strike from production and everything that follows it: the ladder's rung, then the
        /// summons, the penalty's announcement or the notice. The one way a strike lands - a story's
        /// strike effect comes through here - and public for the verification runs and the tests.
        /// Returns the rung it landed on, or 0 when nothing landed.
        /// </summary>
        public static int ProductionStrike(EpisodeState s, string id, string reason, StorylineState incident = null)
        {
            if (!StoryAt(s, StoryRules.Production)) return 0;
            int rung = Production.Strike(s, id, reason ?? "conduct");
            if (rung > 0) StoryStruck(s, id, rung, incident, reason);
            return rung;
        }

        /// <summary>
        /// A strike landed. For the player, production's ladder opens at the rung it landed on - a
        /// warning, a penalty, or the removal - as a Diary Room summons that is never held back for
        /// airtime. For a houseguest the first strike starts <c>on-notice</c>: somebody one outburst
        /// from the door, and the house watching.
        /// </summary>
        private static void StoryStruck(EpisodeState s, string id, int rung, StorylineState incident, string reason)
        {
            var who = s.Find(id);
            if (who == null) return;
            // The other party to the incident, if the story that caused it had one.
            string other = incident?.cast.Select(r => r.contestantId)
                .FirstOrDefault(x => x != id && s.Find(x)?.status == ContestantStatus.Active && !s.Find(x).isPlayer);
            if (rung == 2)
                Log(s, StoryLog.Penalty, who.isPlayer
                    ? "Production has penalised you: a Have-Not week, and you sit out the next Head of Household competition."
                    : who.name + " has been penalised by production: a Have-Not week, and no Head of Household competition next week.");
            if (who.isPlayer)
            {
                var ladder = StoryCatalog.Find("diary-room-calls");
                if (ladder == null) return;
                foreach (var old in RunningCycles(s).Where(x => x.templateId == ladder.id).ToList()) EndCycle(s, old, "superseded");
                StartCycle(s, ladder, StoryCatalog.Bind().With("OTHER", other), CurrentAnchor(s) ?? StoryAnchors.EvictionNight,
                    rung >= 3 ? "removal" : rung == 2 ? "penalty" : "warning");
                return;
            }
            if (rung != 1) return;
            var notice = StoryCatalog.Find("on-notice");
            var ctx = new StoryContext(s, CurrentAnchor(s) ?? StoryAnchors.EvictionNight, id, null, other);
            var binding = notice == null ? null : Castable(s, ctx, notice);
            if (binding != null) StartCycle(s, notice, binding, ctx.anchor);
        }

        // ---------------------------------------------------------------- NPC-initiated moments (G5)

        // Houseguests act on the player all week; not every act becomes a card. Each of these is a
        // keyed chance, so the house's own turns fill the week's gaps rather than its every slot.

        /// <summary>A houseguest confronted the player: half the time, the web's confront menu as a beat to answer.</summary>
        internal static void StoryConfronted(EpisodeState s, string npcId)
        {
            // The strategy windows' reply cards own the house's approaches wherever they play.
            if (StrategyRules.Apply(s)) return;
            if (StoryOn(s) && StoryRandom.Chance(s, "w" + s.week + ":confronted:" + npcId, 0.5))
                StartAimed(s, "confronted", npcId, StoryAnchors.EvictionNight);
        }

        /// <summary>A nominee campaigned to the player: half the time, the web's campaign menu as a beat to answer.</summary>
        internal static void StoryCampaignedTo(EpisodeState s, string npcId)
        {
            if (StrategyRules.Apply(s)) return;
            if (StoryOn(s) && StoryRandom.Chance(s, "w" + s.week + ":campaign:" + npcId, 0.5))
                StartAimed(s, "campaign-pitch", npcId, StoryAnchors.BlockSet);
        }

        /// <summary>
        /// A houseguest spread something about the player to somebody else, and some of the time it
        /// gets back to the player - through the listener when they are close enough to pass it on.
        /// </summary>
        internal static void StoryGossipedAbout(EpisodeState s, string gossipId, string listenerId)
        {
            if (!StoryAt(s, StoryRules.Grudges) || StrategyRules.Apply(s)) return;
            string teller = listenerId != null && s.Score(listenerId, s.playerId) >= 20 ? listenerId : null;
            double chance = teller != null ? 0.5 : 0.25;
            if (StoryRandom.Chance(s, "w" + s.week + ":gossip:" + gossipId + ":" + listenerId, chance))
                StartAimed(s, "caught-talking", gossipId, StoryAnchors.EvictionNight, teller);
        }

        /// <summary>
        /// Starts an arc at somebody the engine already chose - the houseguest who confronted you,
        /// the nominee who asked for your vote - when it may start now. It casts from
        /// <see cref="StoryContext.talkingTo"/> (and <see cref="StoryContext.about"/>, when given).
        /// </summary>
        private static void StartAimed(EpisodeState s, string arcId, string npcId, string anchor, string aboutId = null)
        {
            if (!StoryOn(s)) return;
            var arc = StoryCatalog.Find(arcId);
            var binding = arc == null ? null : Castable(s, new StoryContext(s, anchor, npcId, null, aboutId), arc);
            if (binding != null) StartCycle(s, arc, binding, anchor);
        }

        // ---------------------------------------------------------------- walking in on two houseguests

        /// <summary>
        /// Whether the house may put a walk-in in front of the player now: the same airtime every
        /// story beat shares, not the legacy one-situation-a-week slot.
        /// </summary>
        public static bool ProximityOpen(EpisodeState s) =>
            StoryOn(s) ? s.Active.Count(c => !c.isPlayer) >= 2 && AirtimeFor(s, StorySurfaces.Scene)
                       : HouseEvents.Ready(s) && HouseEvents.Pending(s) == null;

        /// <summary>
        /// Whether walking in on these two would come to anything now: the house has room for it,
        /// and some walk-in arc will take this pair this week. The director asks before it offers.
        /// </summary>
        public static bool ProximityOpen(EpisodeState s, string firstId, string secondId, string room = null)
        {
            if (!ProximityOpen(s)) return false;
            if (!StoryOn(s)) return true;
            var first = s.Find(firstId);
            var second = s.Find(secondId);
            if (first == null || second == null || first.id == second.id || first.isPlayer || second.isPlayer
                || first.status != ContestantStatus.Active || second.status != ContestantStatus.Active) return false;
            var ctx = new StoryContext(s, CurrentAnchor(s) ?? StoryAnchors.EvictionNight, first.id);
            string venue = StoryVenues.ForRoom(room);
            return StoryCatalog.ProximityArcs.Any(id => OpensIn(StoryCatalog.Find(id), venue)
                && ProximityBinding(s, ctx, StoryCatalog.Find(id), first.id, second.id) != null);
        }

        /// <summary>
        /// Whether a walk-in arc can open in the room the player walked into: an arc whose first scene
        /// is set somewhere opens only there. A bedroom walk-in on a feuding pair became "Words in the
        /// Kitchen" (playtest, 2026-09-27) - the room came with the command and nothing read it. A
        /// room nobody can place (<paramref name="venue"/> null) keeps the old reading, anywhere.
        /// </summary>
        private static bool OpensIn(ArcTemplate arc, string venue)
        {
            if (arc == null || venue == null || arc.beats == null || arc.beats.Length == 0) return true;
            string set = arc.beats[0].venue;
            return string.IsNullOrEmpty(set) || set == venue;
        }

        /// <summary>
        /// Past the story boundary a walk-in is a story beat: an argument between two houseguests
        /// who have reason for one is <c>kitchen-blowup</c> with them in it, anything else the
        /// one-beat <c>walked-in</c>. The director still only offers who is standing where; the
        /// engine decides what it is.
        /// </summary>
        private static void StoryProximity(EpisodeState s, EpisodeCommand c)
        {
            var first = s.Find(c.targetId);
            var second = s.Find(c.secondTargetId);
            Require(first != null && second != null && first.id != second.id && !first.isPlayer && !second.isPlayer
                    && first.status == ContestantStatus.Active && second.status == ContestantStatus.Active,
                "There is nobody there to walk in on.");
            Require(ProximityOpen(s), "Deal with what is already in front of you first.");
            var ctx = new StoryContext(s, CurrentAnchor(s) ?? StoryAnchors.EvictionNight, first.id);
            string venue = StoryVenues.ForRoom(c.text);
            foreach (var id in StoryCatalog.ProximityArcs)
            {
                var arc = StoryCatalog.Find(id);
                if (!OpensIn(arc, venue)) continue;
                var binding = ProximityBinding(s, ctx, arc, first.id, second.id);
                if (binding == null) continue;
                StartCycle(s, arc, binding, ctx.anchor);
                return;
            }
            throw new RuleException("Nothing comes of it.");
        }

        /// <summary>A proximity arc cast with the two people the player walked in on, if it will take them.</summary>
        private static ArcBinding ProximityBinding(EpisodeState s, StoryContext ctx, ArcTemplate arc, string a, string b)
        {
            if (arc == null) return null;
            var cast = StoryCatalog.ProximityCast(s, arc.id, a, b);
            return cast == null ? null : Castable(s, ctx, arc.Recast(_ => cast));
        }
    }
}
