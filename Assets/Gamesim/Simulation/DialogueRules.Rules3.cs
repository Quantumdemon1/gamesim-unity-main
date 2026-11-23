using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 3 (DIALOGUE-PLAN §2, slice D3): the campaign conversation with its four trees
    /// (<see cref="DialogueCampaignTrees"/>: the vote word, the memory, the pressure and the neutral tree) and the appeal to
    /// the veto holder (<see cref="DialogueHolderAppeal"/>), each a closed content pair, and the exact validator of a
    /// rules-3 conversation.
    ///
    /// <para>Rules 2 keeps its own code (DialogueRules.Rules2.cs), keyed to <c>== 2</c> where it differs; rules 1 keeps
    /// its own, keyed to <c>== 1</c>. Rules 3 has rules 2's bounds (five steps, 32 conversations, three evidence rows, one
    /// statement), needs schema 30 like every version from 2, adds no saved field, and funds every conversation with the
    /// window's action, as rules 2 does. What it adds is new content, one new occasion kind (<c>holder</c>, §5.1) and the
    /// windows and phases that kind lives in (§4.1). Nothing here reads <see cref="Version"/> for behaviour.</para>
    /// </summary>
    public static partial class DialogueRules
    {
        /// <summary>The version that introduced the four campaign trees and the holder appeal.</summary>
        public const int Rules3 = 3;

        /// <summary>The words a holder appeal is refused in when the veto meeting has nothing left to ask.</summary>
        public const string HolderRefusal = "The veto holder's decision is no longer open.";

        /// <summary>The occasion of a holder appeal (§5.1): one a week for the holder, as a campaign occasion is one a week for a voter.</summary>
        public static string HolderOccasion(int week, string npcId) =>
            "week:" + week.ToString(CultureInfo.InvariantCulture) + ":" + DialogueHolderAppeal.Kind + ":" + npcId;

        /// <summary>Whether a season's dialogue rules hold the holder appeal: rules 3 and later.</summary>
        public static bool HolderAppealOn(EpisodeState s) => On(s) && s.dialogueRulesVersion >= Rules3;

        /// <summary>
        /// Whether the plea a nominee makes for themselves to this decider is a conversation's entry: the Emotional plea for
        /// a voter's vote in the Campaign in every dialogue season, and from rules 3 the Emotional plea to the veto holder
        /// to use it on the player at the veto meeting. The plea panel's one question; reads the state only.
        /// </summary>
        public static bool IsConversationPlea(EpisodeState s, string deciderId, string ask, string subjectId)
        {
            if (!On(s) || s.playerId == null || subjectId != s.playerId) return false;
            if (s.phase == EpisodePhase.Campaign) return ask == LobbyAsk.Vote;
            return HolderAppealOn(s) && s.phase == EpisodePhase.VetoMeeting && ask == LobbyAsk.Save
                && deciderId != null && deciderId == s.vetoHolderId;
        }

        /// <summary>The occasion kind a conversation started now would have: a holder appeal at the veto meeting from rules 3, otherwise the campaign.</summary>
        internal static string KindNow(EpisodeState s) =>
            s != null && s.dialogueRulesVersion >= Rules4 && s.phase == EpisodePhase.Veto ? DialogueVetoQuestion.Kind
            : s != null && s.dialogueRulesVersion >= Rules3 && s.phase == EpisodePhase.VetoMeeting ? DialogueHolderAppeal.Kind : DialogueCampaignV1.Kind;

        /// <summary>The occasion a conversation with this houseguest started now would have.</summary>
        internal static string OccasionNow(EpisodeState s, string npcId) =>
            KindNow(s) == DialogueVetoQuestion.Kind ? VetoQuestionOccasion(s.week, npcId)
            : KindNow(s) == DialogueHolderAppeal.Kind ? HolderOccasion(s.week, npcId) : Occasion(s.week, npcId);

        /// <summary>Whether the phase is one a conversation can be entered or fall back from: the Campaign, and from rules 3 the veto meeting.</summary>
        internal static bool ConversationPhase(EpisodeState s) => s != null
            && (s.phase == EpisodePhase.Campaign || s.dialogueRulesVersion >= Rules3 && s.phase == EpisodePhase.VetoMeeting
                || s.dialogueRulesVersion >= Rules4 && s.phase == EpisodePhase.Veto);

        /// <summary>The phase an open conversation of this kind belongs to, and in which it lapses.</summary>
        internal static EpisodePhase PhaseOf(DialogueEpisodeState e) =>
            e?.occasionKind == DialogueVetoQuestion.Kind ? EpisodePhase.Veto
            : e?.occasionKind == DialogueHolderAppeal.Kind ? EpisodePhase.VetoMeeting : EpisodePhase.Campaign;

        /// <summary>
        /// Whether a plea to an open conversation's houseguest closes that conversation unmade: a plea for the vote to a
        /// voter in a campaign conversation, and any plea at all to the holder in a holder appeal (to save somebody, to keep
        /// the nominations, or, when the holder is also the Head of Household, about a replacement): a houseguest hears one
        /// plea a phase (StrategyRules.AlreadyAsked), so any other plea to them is the one the appeal would have ended in.
        /// The plea is the ordinary one-shot plea, at its own cost; the conversation lapses with it.
        /// </summary>
        internal static bool ClosesOnPlea(DialogueEpisodeState e, string ask) =>
            e?.occasionKind == DialogueHolderAppeal.Kind ? LobbyAsk.IsKnown(ask)
            : e?.occasionKind != DialogueVetoQuestion.Kind && ask == LobbyAsk.Vote;

        /// <summary>The ask a plea command carries, or null.</summary>
        internal static string AskOf(string text) => LobbyAsk.TryDecode(text, out string ask, out _) ? ask : null;

        private static int WindowOf(string kind) =>
            kind == DialogueHolderAppeal.Kind || kind == DialogueVetoQuestion.Kind ? Windows.AfterNominations : Windows.AfterVeto;

        internal static ArcTemplate Rules3Template(string id, int version) =>
            id == DialogueCampaignTrees.TemplateId && version == DialogueCampaignTrees.ContentVersion ? DialogueCampaignTrees.CreateTemplate()
            : id == DialogueHolderAppeal.TemplateId && version == DialogueHolderAppeal.ContentVersion ? DialogueHolderAppeal.CreateTemplate() : null;

        internal static bool IsRules3Template(string id) => id == DialogueCampaignTrees.TemplateId || id == DialogueHolderAppeal.TemplateId;

        private static bool IsRules3Content(string id) => id != null
            && (id.StartsWith(DialogueCampaignTrees.TemplateId + ":", StringComparison.Ordinal)
                || id.StartsWith(DialogueHolderAppeal.TemplateId + ":", StringComparison.Ordinal));

        /// <summary>The templates a season on this version may hold: rules 2's one tree, or from rules 3 the trees and the holder appeal.</summary>
        private static bool IsTemplateFor(int version, string id) =>
            version >= Rules4 && IsRules4Template(id) || (version >= Rules3 ? IsRules3Template(id) : IsRules2Template(id));

        private static bool IsContentFor(int version, string id) =>
            version >= Rules4 && IsRules4Content(id) || (version >= Rules3 ? IsRules3Content(id) : IsRules2Content(id));

        /// <summary>The template a rules-3 season opens now: the holder appeal at the veto meeting, the trees otherwise.</summary>
        internal static string Rules3EntryTemplate(EpisodeState s) =>
            KindNow(s) == DialogueVetoQuestion.Kind ? DialogueVetoQuestion.TemplateId
            : KindNow(s) == DialogueHolderAppeal.Kind ? DialogueHolderAppeal.TemplateId : DialogueCampaignTrees.TemplateId;

        // ---------------------------------------------------------------- starting a rules-3 conversation

        private static bool CanStartRules3(EpisodeState s, string npcId, out string error)
        {
            if (!TryValidate(s, out error)) return false;
            if (!On(s)) return Fail(out error, "Dialogue rules are not active.");
            string kind = KindNow(s);
            if (!EligibleFor(s, kind, npcId, out error)) return false;
            if (Active(s) != null) return Fail(out error, "Finish the active dialogue first.");
            if (s.dialogue.episodes.Any(e => e.occasionId == OccasionNow(s, npcId)))
                return Fail(out error, kind != DialogueCampaignV1.Kind ? "This veto occasion has already been used." : "This campaign occasion has already been used.");
            if (s.dialogue.episodes.Count >= ConversationBudget(s.dialogueRulesVersion)) return Fail(out error, BudgetRefusal);
            if (s.storylines.Count >= Rules2StorylineCeiling || s.houseEvents.Count > MaximumEvents - StepsFor(s.dialogueRulesVersion))
                return Fail(out error, RoomRefusal);
            if (s.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)))
                return Fail(out error, "Answer the existing house moment first.");
            // From rules 4 the nominee's grant pays in place of the action (DialogueRules.Grants.cs); never for the veto question.
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s) && GrantFor(s, kind) == null)
                return Fail(out error, "You have no conversations left this week.");
            return true;
        }

        /// <summary>
        /// Whether a conversation of this kind with this houseguest is still playable: a campaign as rules 1 and 2 have it,
        /// and a holder appeal while the player is nominated, the veto meeting's decision is open, this houseguest holds the
        /// veto, and the plea to them is still the player's to make.
        /// </summary>
        private static bool EligibleFor(EpisodeState s, string kind, string npcId, out string error)
        {
            if (kind == DialogueVetoQuestion.Kind) return VetoQuestionEligible(s, npcId, out error);
            if (kind != DialogueHolderAppeal.Kind) return Eligible(s, npcId, out error);
            if (s.phase != EpisodePhase.VetoMeeting || s.vetoResolved || s.Find(s.playerId)?.status != ContestantStatus.Active
                || s.nominees == null || !s.nominees.Contains(s.playerId) || string.IsNullOrEmpty(npcId) || npcId == s.playerId
                || npcId != s.vetoHolderId || s.Find(npcId)?.status != ContestantStatus.Active)
                return Fail(out error, HolderRefusal);
            return StrategyRules.CanLobby(s, npcId, LobbyAsk.Save, s.playerId, out error);
        }

        // ---------------------------------------------------------------- the facts and the answers (engine-side, keyed)

        /// <summary>
        /// The history the memory tree stands on (§5.1's evidence: a fact the player holds, never hearsay): the most recent
        /// earlier-week vote this voter told the player about that the reveal judged, kept or caught as a lie. A kept one
        /// counts only when it named somebody other than the player, who is still in the season; a lie only while the
        /// player still remembers it (<see cref="CaughtLie"/>). Null when there is none.
        /// </summary>
        public static ClaimRow HistoryFact(EpisodeState s, string npcId, int beforeWeek)
        {
            var kept = s.ledger?.claims?.Where(k => k != null && k.voterId == npcId && k.source == ClaimSource.Told
                    && k.status == ClaimStatus.Kept && k.week >= 1 && k.week < beforeWeek
                    && k.targetId != s.playerId && k.targetId != npcId && s.Find(k.targetId) != null)
                .OrderByDescending(k => k.week).FirstOrDefault();
            var lie = CaughtLie(s, npcId, beforeWeek);
            if (lie == null) return kept;
            var lied = s.ledger.claims.Where(k => k != null && k.voterId == npcId && k.source == ClaimSource.Told
                    && k.status == ClaimStatus.Lied && k.week == lie.week).LastOrDefault();
            return kept != null && (lied == null || kept.week >= lied.week) ? kept : lied;
        }

        private static double Candour(EpisodeState s, string npcId) => Math.Max(0.2, Math.Min(0.9, 0.5 + s.Score(npcId, s.playerId) / 100));

        /// <summary>
        /// The voter's answer to the approach the player chose (§5.1, step 2), at path index 1. Asking where they stand
        /// answers with their word if they told the player this week, otherwise a concern or a deflection on a keyed coin;
        /// the history answers with the fact itself and draws nothing; keeping things easy and leaning on them each draw a
        /// keyed coin weighed by how the voter sees the player. Nothing is drawn from the season's stream.
        /// </summary>
        internal static string TreesAnswer(EpisodeState s, string npcId, string occasionId, string approach)
        {
            switch (approach)
            {
                case DialogueCampaignTrees.AskStand:
                    var claim = ToldClaim(s, npcId, s.week);
                    if (claim != null) return claim.targetId == s.playerId ? DialogueCampaignTrees.Against : DialogueCampaignTrees.Word;
                    return StoryRandom.Unit(s, Key(occasionId, 1)) < Candour(s, npcId) ? DialogueCampaignTrees.Concern : DialogueCampaignTrees.Guarded;
                case DialogueCampaignTrees.BringUp:
                    var fact = HistoryFact(s, npcId, s.week);
                    return fact == null ? null : fact.status == ClaimStatus.Kept ? DialogueCampaignTrees.Kept : DialogueCampaignTrees.Lied;
                case DialogueCampaignTrees.KeepEasy:
                    return StoryRandom.Unit(s, Key(occasionId, 1)) < Candour(s, npcId) ? DialogueCampaignTrees.Easy : DialogueCampaignTrees.Distracted;
                case DialogueCampaignTrees.Lean:
                    return StoryRandom.Unit(s, Key(occasionId, 1)) < Candour(s, npcId) ? DialogueCampaignTrees.Wavers : DialogueCampaignTrees.Bristles;
                default: return null;
            }
        }

        /// <summary>The holder's answer to the open, on its own keyed coin at path index 0, weighed by how they see the player.</summary>
        internal static string HolderAnswer(EpisodeState s, string npcId, string occasionId) =>
            StoryRandom.Unit(s, Key(occasionId, 0)) < Candour(s, npcId) ? DialogueHolderAppeal.Listening : DialogueHolderAppeal.Closed;

        /// <summary>A reaction to a reply, on the reply's own keyed coin; null for an option that draws nothing.</summary>
        internal static string Rules3Reaction(EpisodeState s, string kind, string occasionId, int pathIndex, string optionId)
        {
            double warm, sour;
            bool known = kind == DialogueHolderAppeal.Kind
                ? DialogueHolderAppeal.TryOdds(optionId, out warm, out sour)
                : DialogueCampaignTrees.TryOdds(optionId, out warm, out sour);
            if (!known) return null;
            double draw = StoryRandom.Unit(s, Key(occasionId, pathIndex));
            return draw < warm ? DialogueCampaignTrees.Warmed : draw >= 1 - sour ? DialogueCampaignTrees.Backfired : DialogueCampaignTrees.Unmoved;
        }

        /// <summary>The word a rules-3 conversation holds while its current node is the reply it was given in.</summary>
        private static bool TryReadStatementRules3(EpisodeState s, string eventId, out StatementView view)
        {
            view = null;
            if (!On(s) || !EpisodeValidation.TryValidate(s, out _)) return false;
            var episode = Active(s);
            var cycle = episode == null ? null : Cycle(s, episode);
            if (episode?.statement == null || cycle == null || cycle.eventId != eventId
                || cycle.templateId != DialogueCampaignTrees.TemplateId || DialogueCampaignTrees.AnswerOf(cycle.beatId) == null) return false;
            view = new StatementView(episode.statement);
            return true;
        }

        // ---------------------------------------------------------------- the validator

        private static bool EpisodeRules3(EpisodeState s, DialogueEpisodeState e, EpisodeEngine.PendingDialogueCommand pending,
            HashSet<string> ticks, HashSet<int> logs, HashSet<string> ids, out string error)
        {
            error = null;
            int version = s.dialogueRulesVersion, steps = StepsFor(version);
            var cycle = Cycle(s, e);
            var template = Rules3Template(cycle?.templateId, e.contentVersion);
            bool holder = template?.id == DialogueHolderAppeal.TemplateId;
            string kind = holder ? DialogueHolderAppeal.Kind : DialogueCampaignV1.Kind;
            var phase = holder ? EpisodePhase.VetoMeeting : EpisodePhase.Campaign;
            string occasion = holder ? HolderOccasion(e.week, e.npcId) : Occasion(e.week, e.npcId);
            if (template == null || e.occasionKind != kind
                || e.week < 1 || e.week > s.week || e.window != WindowOf(kind) || e.startedPhase != phase
                || e.startedSequence < 1 || e.startedSequence >= s.nextSequence
                || e.id != "story-" + e.startedSequence.ToString(CultureInfo.InvariantCulture)
                || string.IsNullOrEmpty(e.npcId) || e.npcId.Length > 160 || e.npcId == s.playerId
                || s.Find(e.npcId) == null || e.occasionId != occasion
                || !Revision(s, e.startedRevision, e, DialogueRevisionField.Started, pending))
                return Fail(out error, "Invalid conversation identity or start.");
            if (cycle.variant != 0 || cycle.week != e.week || cycle.title != template.title || cycle.category != template.eyebrow
                || cycle.lane != StoryLanes.Game || !Cast(cycle.cast, e.npcId) || cycle.vars == null || cycle.vars.Count != 0
                || cycle.nextWeek != 0 || cycle.nextAnchor != null || cycle.path == null || cycle.path.Count < 1 || cycle.path.Count > steps)
                return Fail(out error, "The dialogue must own its exact conversation cycle.");
            if (e.entry == null || !AcceptsFunding(version, e.entry.kind))
                return Fail(out error, "This version funds a conversation with the window's action only.");
            string tick = EntryTick(e.entry, e.week, e.window);
            // From rules 4 a nominee's grant may pay instead of the action (DialogueRules.Grants.cs); AcceptsFunding refuses it below.
            if (e.entry.kind == GrantEntry) { if (!GrantEntryRules4(s, e, pending, ticks, out error)) return false; }
            else if (tick == null || e.entry.grantId != null
                || e.entry.revision != e.startedRevision || !Revision(s, e.entry.revision, e.entry, DialogueRevisionField.Entry, pending)
                || e.entry.windowActionBefore < 0 || e.entry.windowActionAfter > MaximumAction
                || (long)e.entry.windowActionAfter != (long)e.entry.windowActionBefore + 1
                || e.week == s.week && e.entry.windowActionAfter > s.windowActions[e.window]
                || !ticks.Add(tick))
                return Fail(out error, "Invalid ordinary dialogue entry receipt.");
            var earlier = s.dialogue.episodes.TakeWhile(row => !ReferenceEquals(row, e))
                .LastOrDefault(row => row.week == e.week && row.window == e.window && row.entry?.kind == ActionEntry);
            if (earlier != null && e.entry.windowActionBefore < earlier.entry.windowActionAfter)
                return Fail(out error, "Dialogue entry ticks must retain their spending order.");
            if (e.responses == null || e.responses.Count > steps || e.responses.Any(r => r == null)
                || e.evidence == null || e.evidence.Count > EvidenceRows(version) || e.evidence.Any(row => row == null))
                return Fail(out error, "Unsupported conversation response or evidence storage.");
            var items = s.houseEvents.Where(item => item.cycleId == e.id).ToArray();
            if (items.Length < 2 || items.Length > steps || items[0].id != e.entryEventId)
                return Fail(out error, "The conversation's event shelf does not match its entry.");
            string beatId = DialogueCampaignTrees.Open, ending = null, answer = null, reaction = null, cited = null;
            int citedAt = -1, answeredAt = -1;
            int previousSequence = e.startedSequence, previousRevision = e.startedRevision - 1, responseIndex = 0;
            for (int index = 0; index < items.Length; index++)
            {
                var item = items[index]; var beat = template.Beat(beatId);
                if (beat == null || !EventRules2(item, beat, e, template.id, out int eventSequence) || eventSequence <= previousSequence || eventSequence >= s.nextSequence)
                    return Fail(out error, "A retained conversation event differs from its canonical definition.");
                previousSequence = eventSequence;
                if (index >= cycle.path.Count)
                {
                    if (index != items.Length - 1 || item.resolved || item.chosenIndex != -1 || item.outcome != null)
                        return Fail(out error, "Only the current conversation node may remain unanswered.");
                    break;
                }
                var step = cycle.path[index];
                if (step == null || step.beatId != beatId || step.week != e.week || !item.resolved
                    || item.chosenIndex < 0 || item.chosenIndex >= item.choices.Count
                    || step.optionId != item.choices[item.chosenIndex].optionId
                    || step.logSequence <= eventSequence || step.logSequence >= s.nextSequence || !logs.Add(step.logSequence)
                    || s.storylines.Any(other => other.id != e.id && other.path != null && other.path.Any(p => p != null && p.logSequence == step.logSequence)))
                    return Fail(out error, "A conversation step does not own its resolved event and outcome.");
                var option = beat.Option(step.optionId);
                bool lapsed = step.result == StoryResults.Lapsed;
                if (index == 0 && (step.optionId != DialogueCampaignTrees.OpenCase || step.result != StoryResults.Plain)
                    || step.result != StoryResults.Plain && !lapsed
                    || lapsed && (index != cycle.path.Count - 1 || step.optionId != DialogueCampaignTrees.Leave)
                    || item.outcome != (lapsed ? DialogueCampaignV1.LapsedOutcome : option.outcome))
                    return Fail(out error, "The conversation path must follow its exact authored edges.");
                previousSequence = step.logSequence;
                string actual = Suffix(e, responseIndex, template.id);
                string suffix;
                if (lapsed) suffix = "occasion-closed";
                else if (option.next == DialogueCampaignTrees.ByFacts) suffix = DialogueCampaignTrees.Listening;
                else if (option.next == DialogueCampaignTrees.ByAnswer)
                {
                    string[] allowed = holder ? DialogueHolderAppeal.Answers : DialogueCampaignTrees.AnswersOf(step.optionId);
                    if (allowed == null || Array.IndexOf(allowed, actual) < 0)
                        return Fail(out error, "The answer is not one this conversation can give.");
                    answer = suffix = actual; answeredAt = index;
                }
                else if (option.next == DialogueCampaignTrees.ByReaction)
                {
                    if (Array.IndexOf(DialogueCampaignTrees.Reactions, actual) < 0)
                        return Fail(out error, "The reaction is not one this conversation can give.");
                    reaction = suffix = actual;
                    cited = holder ? null : DialogueCampaignTrees.EvidenceOf(step.optionId);
                    if (cited != null) citedAt = index;
                }
                else if (!holder && step.optionId == DialogueCampaignTrees.Plea)
                {
                    if (!VoteResponseRules2(s, e, actual, cited != null))
                        return Fail(out error, "An answered campaign must retain its actual vote response.");
                    suffix = actual;
                }
                else if (holder && step.optionId == DialogueHolderAppeal.AskToUse)
                {
                    if (!SaveResponse(s, e, actual)) return Fail(out error, "An answered appeal must retain its actual veto plea response.");
                    suffix = actual;
                }
                else suffix = step.optionId == DialogueCampaignTrees.KeepOpen ? "no-promise-made"
                    : step.optionId == DialogueHolderAppeal.LeaveWithThem ? DialogueHolderAppeal.NoAsk : "respectful-exit";
                bool omitted = lapsed && responseIndex == e.responses.Count;
                if (!omitted)
                {
                    if (e.responses.Count <= responseIndex) return Fail(out error, "A resolved conversation step is missing its response.");
                    var response = e.responses[responseIndex++];
                    if (response.eventId != item.id || response.pathIndex != index || response.speakerId != e.npcId
                        || response.responseKey != template.id + "/" + suffix
                        || !Revision(s, response.revision, response, DialogueRevisionField.Response, pending)
                        || response.revision <= previousRevision || index == 0 && response.revision != e.entry.revision)
                        return Fail(out error, "A conversation response does not match its accepted step.");
                    previousRevision = response.revision;
                }
                var retainedLog = s.events.FirstOrDefault(row => row.sequence == step.logSequence);
                if (retainedLog != null && (retainedLog.kind != StoryLog.Outcome || retainedLog.week != e.week
                    || retainedLog.phase != phase || retainedLog.audienceIds == null
                    || !retainedLog.audienceIds.SequenceEqual(new[] { s.playerId, e.npcId })))
                    return Fail(out error, "A retained conversation outcome has the wrong owner or audience.");
                string next = lapsed ? "end:lapsed" : option.next;
                if (next.StartsWith("end:", StringComparison.Ordinal))
                {
                    ending = next.Substring(4);
                    if (index != cycle.path.Count - 1 || index != items.Length - 1)
                        return Fail(out error, "A terminal conversation cannot contain another node.");
                    break;
                }
                if (next == DialogueCampaignTrees.ByFacts)
                {
                    // The approach node is fixed by the history the player held when they opened. It is not re-derived
                    // here: a remembered lie can leave the thirty memories a houseguest keeps, and a save must still load.
                    // Choosing the history checks the fact as it stands, and its copy is checked on its own terms below.
                    string drawn = index + 1 < items.Length ? BeatOf(items[index + 1], template.id) : null;
                    if (holder || !DialogueCampaignTrees.IsApproach(drawn))
                        return Fail(out error, "The approach node must follow the open.");
                    beatId = drawn;
                }
                else if (next == DialogueCampaignTrees.ByAnswer)
                    beatId = holder ? DialogueHolderAppeal.ReplyBeat(answer) : DialogueCampaignTrees.ReplyBeat(answer);
                else if (next == DialogueCampaignTrees.ByReaction) beatId = DialogueCampaignTrees.FinishBeat(reaction);
                else beatId = next;
            }
            if (responseIndex != e.responses.Count || cycle.eventId != items[items.Length - 1].id || cycle.beatId != beatId)
                return Fail(out error, "The conversation path, current node and responses must agree.");
            if (!StatementRules3(s, e, cycle, items, answer, answeredAt, pending, ids, out error)) return false;
            if (!EvidenceRules3(s, e, cycle, items, cited, citedAt, pending, ids, out error)) return false;
            if (ending == null)
            {
                if (cycle.status != StorylineStatus.Active || cycle.endedWeek != 0 || cycle.endingId != null
                    || e.time != null || items.Length != cycle.path.Count + 1)
                    return Fail(out error, "An active conversation must retain its funded unanswered node.");
                if (e.week != s.week) return Fail(out error, "An active conversation cannot survive its week.");
                if (!EligibleFor(s, kind, e.npcId, out error)) return false;
            }
            else
            {
                if (cycle.status != StorylineStatus.Completed || cycle.endedWeek != e.week || cycle.endingId != ending
                    || e.time == null || e.time.reason != (ending == "lapsed" ? "occasion-closed" : ending)
                    || e.time.window != e.window || e.time.proposedDelta != 0 || e.time.appliedDelta != 0
                    || e.time.spentRevision != 0 || e.time.spentSequence != 0 || e.time.spentWindowAction != 0
                    || !Revision(s, e.time.revision, e.time, DialogueRevisionField.Time, pending)
                    || e.time.sequence != cycle.path[cycle.path.Count - 1].logSequence)
                    return Fail(out error, "A closed conversation requires its exact zero-time receipt.");
                bool omitted = responseIndex != cycle.path.Count;
                bool currentClosure = e.time.revision == s.revision || e.time.revision > s.revision;
                bool present = s.Find(e.npcId).status == ContestantStatus.Active;
                if (omitted && (ending != "lapsed" || e.time.revision <= previousRevision || present)
                    || !omitted && (e.time.revision != previousRevision || ending == "lapsed" && currentClosure && !present))
                    return Fail(out error, "The conversation's closure and its actual speaker response disagree.");
                if (ending == "lapsed" && currentClosure && e.week == s.week && s.phase == phase
                    && EligibleFor(s, kind, e.npcId, out _) && pending?.OwnsCampaignClosure(s, e) != true)
                    return Fail(out error, "The current conversation occasion has not closed.");
            }
            return true;
        }

        // The plea at the end of a holder appeal is the ordinary Emotional save plea's: one row, as that plea writes it.
        private static bool SaveResponse(EpisodeState s, DialogueEpisodeState e, string suffix)
        {
            string response = suffix == "save-" + LobbyResponse.Receptive ? LobbyResponse.Receptive
                : suffix == "save-" + LobbyResponse.Open ? LobbyResponse.Open
                : suffix == "save-" + LobbyResponse.Skeptical ? LobbyResponse.Skeptical
                : suffix == "save-" + LobbyResponse.Hostile ? LobbyResponse.Hostile : null;
            if (response == null) return false;
            var rows = s.lobbies.Where(l => l.week == e.week && l.phase == EpisodePhase.VetoMeeting && l.deciderId == e.npcId).ToArray();
            return (e.week < s.week && rows.Length == 0) || rows.Length == 1 && rows[0].ask == LobbyAsk.Save
                && rows[0].subjectId == s.playerId && rows[0].approach == LobbyApproach.Emotional && rows[0].response == response;
        }

        // One statement a conversation (§6): the vote claim the voter's answer stands on, copied the moment they gave it -
        // this week's word, or, in the memory tree, an earlier week's word the reveal showed they kept.
        private static bool StatementRules3(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, HouseEventState[] items,
            string answer, int at, EpisodeEngine.PendingDialogueCommand pending, HashSet<string> ids, out string error)
        {
            error = null;
            bool holder = cycle.templateId == DialogueHolderAppeal.TemplateId;
            if (holder || !DialogueCampaignTrees.WritesStatement(answer))
                return e.statement == null || Fail(out error, "Only a voter's word holds a statement.");
            var row = e.statement;
            var response = e.responses.FirstOrDefault(r => r.pathIndex == at);
            int sequence = cycle.path[at].logSequence - 1;
            bool kept = answer == DialogueCampaignTrees.Kept;
            if (row == null || response == null || !Sequence(items[at], out int asked) || sequence <= asked || !FreeSequence(s, sequence)
                || row.id != "dialogue-statement-" + sequence.ToString(CultureInfo.InvariantCulture) || !ids.Add(row.id) || row.sequence != sequence
                || row.eventId != items[at].id || row.pathIndex != at || row.speakerId != e.npcId
                || row.conditionKind != VoteClaim || row.answer != VoteOut
                || row.revision != response.revision || !Revision(s, row.revision, row, DialogueRevisionField.Statement, pending)
                || row.audienceIds == null || !row.audienceIds.SequenceEqual(new[] { s.playerId })
                || string.IsNullOrEmpty(row.targetId) || s.Find(row.targetId) == null || row.targetId == e.npcId
                || (kept ? row.week < 1 || row.week >= e.week || row.targetId == s.playerId
                    : row.week != e.week || (answer == DialogueCampaignTrees.Against) != (row.targetId == s.playerId)
                      // While the week is on, the claim it copies is still on the ledger, word for word.
                      || e.week == s.week && s.ledger?.claims?.Any(k => k != null && k.week == e.week && k.voterId == e.npcId
                          && k.source == ClaimSource.Told && k.targetId == row.targetId) != true))
                return Fail(out error, "A voter's word must be its exact copied vote claim.");
            return true;
        }

        private static bool EvidenceRules3(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, HouseEventState[] items, string cited,
            int at, EpisodeEngine.PendingDialogueCommand pending, HashSet<string> ids, out string error)
        {
            error = null;
            if (cited == null) return e.evidence.Count == 0 || Fail(out error, "Only a citing reply may retain evidence.");
            if (e.evidence.Count != 1) return Fail(out error, "A citation requires exactly one source-owned evidence row.");
            var row = e.evidence[0];
            var response = e.responses.FirstOrDefault(r => r.pathIndex == at);
            int sequence = cycle.path[at].logSequence - 1;
            bool owned = row.kind == cited && row.pathIndex == at && row.eventId == items[at].id && response != null
                && row.capturedRevision == response.revision
                && Revision(s, row.capturedRevision, row, DialogueRevisionField.EvidenceCaptured, pending)
                && Sequence(items[at], out int replied) && sequence > replied && FreeSequence(s, sequence)
                && row.id == "dialogue-evidence-" + sequence.ToString(CultureInfo.InvariantCulture) && ids.Add(row.id);
            bool source = cited == DialogueCampaignTrees.StatementEvidence
                    ? row.statementId != null && row.statementId == e.statement?.id && row.power == null && row.memory == null
                : cited == DialogueCampaignTrees.MemoryEvidence && row.statementId == null && row.power == null && CaughtLieCopy(s, e, row.memory);
            if (!owned || !source)
                return Fail(out error, "The citation must retain its exact owned revision, event, identity and source.");
            return true;
        }
    }
}
