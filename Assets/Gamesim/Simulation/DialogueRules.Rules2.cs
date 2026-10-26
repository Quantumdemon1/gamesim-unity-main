using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Dialogue rules 2 (DIALOGUE-PLAN §2, slice D2): the version ladder's bounds, the closed content registry,
    /// entry funding, the voter's keyed answers and the exact validator of a rules-2 conversation.
    ///
    /// <para>Rules 1 keeps its own code in DialogueRules.cs, keyed to <c>== 1</c>, untouched. Rules 2 is defined only
    /// from schema 30 (<see cref="Rules2Schema"/>, decision 7): at the live schema 29 every rules-2 season is refused,
    /// so the frozen schema-29 reader holds dialogue rules 0 and 1 only. Nothing here reads <see cref="Version"/> for
    /// behaviour.</para>
    /// </summary>
    public static partial class DialogueRules
    {
        /// <summary>The versions that introduced behaviour. A later version keeps what an earlier one added unless it says otherwise.</summary>
        public const int Rules1 = 1, Rules2 = 2;

        /// <summary>The schema that introduced the dialogue store (29), and the one that introduced rules 2 (30, DIALOGUE-PLAN §2).</summary>
        public const int StoreSchema = 29, Rules2Schema = 30;

        /// <summary>The first version a role grant may fund an entry in (the ladder's rules 4, D5a). No defined version reaches it yet.</summary>
        public const int GrantFundingFrom = 4;

        /// <summary>How a conversation's entry is paid: the window's action, or (from <see cref="GrantFundingFrom"/>) a role grant.</summary>
        public const string ActionEntry = DialogueCampaignV1.EntryKind, GrantEntry = "grant";

        /// <summary>
        /// Rules 2's bounds (DIALOGUE-PLAN §2's table): five steps a conversation, five events reserved while it is open,
        /// 32 conversations a season counted on every started one, never past the 92 storylines the story pool stops at,
        /// three evidence rows and one statement a conversation.
        /// </summary>
        public const int Rules2Steps = 5, Rules2Conversations = 32, Rules2StorylineCeiling = 92, Rules2EvidenceRows = 3;

        /// <summary>The closed vocabulary of a copied vote claim (DIALOGUE-PLAN §6's fit check).</summary>
        public const string VoteClaim = "vote-claim", VoteOut = "vote-out";

        /// <summary>The keyed-draw prefix of every dialogue coin (§5.2). No story key used it before rules 2.</summary>
        public const string KeyPrefix = "dialogue:";

        public const string BudgetRefusal = "This season has had all its conversations.";
        public const string RoomRefusal = "The house has no room for another conversation.";

        /// <summary>The closed set of dialogue rules this build defines: 0 to <see cref="Version"/>.</summary>
        public static bool IsDefined(int version) => version >= 0 && version <= Version;

        /// <summary>The schema that introduced a version: 29 for rules 0 and 1, 30 for rules 2 and later.</summary>
        public static int SchemaFor(int version) => version >= Rules2 ? Rules2Schema : StoreSchema;

        /// <summary>Whether a season at <paramref name="schema"/> may hold dialogue rules <paramref name="version"/>.</summary>
        public static bool Admits(int version, int schema) => IsDefined(version) && schema >= SchemaFor(version);

        /// <summary>Steps (path entries, house events and responses) in one conversation: 3 on rules 1, 5 from rules 2.</summary>
        public static int StepsFor(int version) => !IsDefined(version) || version < Rules1 ? 0 : version == Rules1 ? MaximumSteps : Rules2Steps;

        /// <summary>Conversations a season: rules 1's 100 (<see cref="MaximumEpisodes"/>), 32 from rules 2 (§5.5).</summary>
        public static int ConversationBudget(int version) =>
            !IsDefined(version) || version < Rules1 ? 0 : version == Rules1 ? MaximumEpisodes : Rules2Conversations;

        /// <summary>Evidence rows in one conversation: one on rules 1 (V2's citation), three from rules 2.</summary>
        public static int EvidenceRows(int version) => !IsDefined(version) || version < Rules1 ? 0 : version == Rules1 ? 1 : Rules2EvidenceRows;

        /// <summary>Whether a version accepts an entry paid this way: the action from rules 1, a grant only from <see cref="GrantFundingFrom"/>.</summary>
        public static bool AcceptsFunding(int version, string kind) => IsDefined(version) && version >= Rules1
            && (kind == ActionEntry || kind == GrantEntry && version >= GrantFundingFrom);

        /// <summary>
        /// The window-action tick an entry spends, keyed by how it was paid (the fix ROLE-MOMENTS-PLAN §6.1 asks of
        /// DialogueRules.cs:182): an action entry owns "week:window:after"; a grant entry spends no tick and owns none,
        /// so it can never collide with an action entry's tick in the same window. Rules 1 keeps its own line.
        /// </summary>
        public static string EntryTick(DialogueEntryReceiptState entry, int week, int window) => entry?.kind == ActionEntry
            ? week.ToString(CultureInfo.InvariantCulture) + ":" + window.ToString(CultureInfo.InvariantCulture) + ":"
              + entry.windowActionAfter.ToString(CultureInfo.InvariantCulture)
            : null;

        /// <summary>The grant a grant entry spends, as the identity no second entry may spend again; null for any other entry.</summary>
        public static string EntryGrant(DialogueEntryReceiptState entry) =>
            entry?.kind == GrantEntry && !string.IsNullOrEmpty(entry.grantId) ? "grant:" + entry.grantId : null;

        /// <summary>The key of the coin drawn at one step of one occasion (§5.2): keyed on the occasion, never on sequence numbers.</summary>
        public static string Key(string occasionId, int pathIndex) => KeyPrefix + occasionId + ":" + pathIndex.ToString(CultureInfo.InvariantCulture);

        /// <summary>The template a season on <paramref name="rulesVersion"/> opens a campaign conversation with.</summary>
        public static string EntryTemplateFor(int rulesVersion) =>
            rulesVersion >= Rules2 && IsDefined(rulesVersion) ? DialogueCampaignWord.TemplateId : rulesVersion == Rules1 ? DialogueCampaignV1.TemplateId : null;

        /// <summary>
        /// The closed content registry, keyed on template and content version: rules 1's two pairs, and from rules 2
        /// the vote-word tree. A fresh definition on every read; an unknown pair, or a pair of another version's, is null.
        /// </summary>
        public static ArcTemplate ContentFor(int rulesVersion, string templateId, int contentVersion) =>
            rulesVersion == Rules1 ? CampaignTemplate(templateId, contentVersion)
            : rulesVersion >= Rules2 && IsDefined(rulesVersion) ? Rules2Template(templateId, contentVersion) : null;

        internal static ArcTemplate Rules2Template(string id, int version) =>
            id == DialogueCampaignWord.TemplateId && version == DialogueCampaignWord.ContentVersion ? DialogueCampaignWord.CreateTemplate() : null;

        /// <summary>Any dialogue content pair the engine can play: the season's version decides which ones it may start.</summary>
        internal static ArcTemplate AnyTemplate(string id, int version) => CampaignTemplate(id, version) ?? Rules2Template(id, version);

        /// <summary>Every dialogue template, of any version: never an ambient-pool arc, never answered by ProgressStoryline.</summary>
        public static bool IsDialogueTemplate(string id) => IsCampaignTemplate(id) || IsRules2Template(id);

        internal static bool IsRules2Template(string id) => id == DialogueCampaignWord.TemplateId;

        private static bool IsRules2Content(string id) => id != null && id.StartsWith(DialogueCampaignWord.TemplateId + ":", StringComparison.Ordinal);

        // ---------------------------------------------------------------- starting a rules-2 conversation

        private static bool CanStartRules2(EpisodeState s, string npcId, out string error)
        {
            if (!TryValidate(s, out error)) return false;
            if (!On(s)) return Fail(out error, "Dialogue rules are not active.");
            if (!Eligible(s, npcId, out error)) return false;
            if (Active(s) != null) return Fail(out error, "Finish the active dialogue first.");
            if (s.dialogue.episodes.Any(e => e.occasionId == Occasion(s.week, npcId)))
                return Fail(out error, "This campaign occasion has already been used.");
            // Every started conversation counts, lapsed and abandoned ones too (§5.5): the attempt is the row.
            if (s.dialogue.episodes.Count >= ConversationBudget(s.dialogueRulesVersion)) return Fail(out error, BudgetRefusal);
            if (s.storylines.Count >= Rules2StorylineCeiling || s.houseEvents.Count > MaximumEvents - StepsFor(s.dialogueRulesVersion))
                return Fail(out error, RoomRefusal);
            if (s.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)))
                return Fail(out error, "Answer the existing house moment first.");
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s))
                return Fail(out error, "You have no conversations left this week.");
            return true;
        }

        /// <summary>
        /// Whether a refused conversation leaves its plea to be made the ordinary way (DIALOGUE-PLAN §5.5): from rules 2
        /// a refusal never takes a plea away. Rules 1 locks the Emotional plea instead, as merged.
        /// </summary>
        public static bool KeepsThePleaWhenRefused(EpisodeState s) => On(s) && s.dialogueRulesVersion >= Rules2;

        // ---------------------------------------------------------------- the voter's answers (engine-side, keyed)

        /// <summary>The vote claim the voter told the player this week, the only claim a conversation copies (§5.1).</summary>
        internal static ClaimRow ToldClaim(EpisodeState s, string npcId, int week) => s.ledger?.claims?
            .LastOrDefault(k => k != null && k.week == week && k.voterId == npcId && k.source == ClaimSource.Told);

        /// <summary>
        /// The voter's answer to the open (DIALOGUE-PLAN §5.1, step 2). A voter who told the player where their vote
        /// is this week stands by it: <see cref="DialogueCampaignWord.Word"/>, or <see cref="DialogueCampaignWord.Against"/>
        /// when it named the player. Otherwise a keyed coin, weighed by how they see the player, decides between a
        /// concern and a deflection. Nothing is drawn from the season's stream.
        /// </summary>
        internal static string VoterAnswer(EpisodeState s, string npcId, string occasionId)
        {
            var claim = ToldClaim(s, npcId, s.week);
            if (claim != null) return claim.targetId == s.playerId ? DialogueCampaignWord.Against : DialogueCampaignWord.Word;
            double candour = Math.Max(0.2, Math.Min(0.9, 0.5 + s.Score(npcId, s.playerId) / 100));
            return StoryRandom.Unit(s, Key(occasionId, 0)) < candour ? DialogueCampaignWord.Concern : DialogueCampaignWord.Guarded;
        }

        /// <summary>The voter's one reaction to a reply, on its own keyed coin; null for the exit, which draws nothing.</summary>
        internal static string VoterReaction(EpisodeState s, string occasionId, string optionId)
        {
            if (!DialogueCampaignWord.TryOdds(optionId, out double warm, out double sour)) return null;
            double draw = StoryRandom.Unit(s, Key(occasionId, 1));
            return draw < warm ? DialogueCampaignWord.Warmed : draw >= 1 - sour ? DialogueCampaignWord.Backfired : DialogueCampaignWord.Unmoved;
        }

        /// <summary>
        /// The lie the reveal caught this voter telling the player in an earlier week, as the player remembers it: the
        /// memory the reveal wrote beside the claim it judged (EpisodeEngine.VoteRead's SettleVoteRead). Only a lie told to
        /// the player's face qualifies, so hearsay is never cited. Null when either has gone.
        /// </summary>
        internal static MemoryState CaughtLie(EpisodeState s, string npcId, int beforeWeek)
        {
            var claim = s.ledger?.claims?.Where(k => k != null && k.voterId == npcId && k.source == ClaimSource.Told
                    && k.status == ClaimStatus.Lied && k.week >= 1 && k.week < beforeWeek)
                .OrderByDescending(k => k.week).FirstOrDefault();
            string target = claim == null ? null : s.Find(claim.targetId)?.name;
            if (target == null) return null;
            string prefix = LiePrefix(s, npcId, claim.week) + target + ", and voted out ";
            return s.memories?.LastOrDefault(m => m != null && m.ownerId == s.playerId && m.subjectId == npcId && m.week == claim.week
                && m.isPrivate && m.text != null && m.text.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static string LiePrefix(EpisodeState s, string npcId, int week) =>
            (s.Find(npcId)?.name ?? "\u0000") + " told me in week " + week.ToString(CultureInfo.InvariantCulture) + " they would vote out ";

        /// <summary>A detached public view of the voter's word on the reply node: who they said goes. Never the ballot.</summary>
        public sealed class StatementView
        {
            public string SpeakerId { get; }
            public string TargetId { get; }
            public int Week { get; }
            internal StatementView(DialogueStatementState row) { SpeakerId = row.speakerId; TargetId = row.targetId; Week = row.week; }
        }

        /// <summary>The word the voter gave the player, while the conversation's current node is the reply it was given in.</summary>
        public static bool TryReadStatement(EpisodeState s, string eventId, out StatementView view)
        {
            view = null;
            if (!On(s) || s.dialogueRulesVersion < Rules2 || !EpisodeValidation.TryValidate(s, out _)) return false;
            var episode = Active(s);
            var cycle = episode == null ? null : Cycle(s, episode);
            if (episode?.statement == null || cycle == null || cycle.eventId != eventId
                || DialogueCampaignWord.AnswerOf(cycle.beatId) == null) return false;
            view = new StatementView(episode.statement);
            return true;
        }

        // ---------------------------------------------------------------- the validator

        private static bool TryValidateRules2(EpisodeState s, EpisodeEngine.PendingDialogueCommand pending, out string error)
        {
            error = null;
            int version = s.dialogueRulesVersion;
            if (s.schemaVersion < SchemaFor(version))
                return Fail(out error, "Dialogue rules version " + version.ToString(CultureInfo.InvariantCulture) + " needs schema "
                    + SchemaFor(version).ToString(CultureInfo.InvariantCulture) + " or later.");
            if (!Prerequisites(s)) return Fail(out error, "Dialogue requires the complete active-from-week-one rules.");
            if (s.unifiedCommitments == null || s.unifiedCommitments.Any(r => r == null || r.conditionalVeto != null))
                return Fail(out error, "Conditional dialogue obligations are not supported by this version.");
            if (s.week < 1 || s.week > 100 || s.revision < 0 || s.nextSequence < 1 || s.contestants == null
                || s.contestants.Any(c => c == null) || s.Find(s.playerId) == null
                || s.storylines == null || s.storylines.Count > MaximumEpisodes || s.storylines.Any(c => c == null)
                || s.houseEvents == null || s.houseEvents.Count > MaximumEvents || s.houseEvents.Any(e => e == null)
                || s.windowActions == null || s.windowActions.Count != Windows.Count || s.windowActions.Any(n => n < 0)
                || s.lobbies == null || s.lobbies.Any(l => l == null) || s.events == null || s.events.Any(e => e == null)
                || s.memories == null || s.memories.Any(m => m == null))
                return Fail(out error, "Dialogue requires complete bounded episode owners.");
            if (s.dialogue.episodes.Count > ConversationBudget(version) || s.dialogue.episodes.Any(e => e == null)
                || s.dialogue.grants.Count != 0 || s.dialogue.vetoOccasions.Count != 0)
                return Fail(out error, "Only action-funded campaign conversations are supported by this version.");
            if (s.storylines.Any(c => c.templateId?.StartsWith("dialogue-", StringComparison.Ordinal) == true && !IsRules2Template(c.templateId))
                || s.houseEvents.Any(e => e.contentId?.StartsWith("dialogue-", StringComparison.Ordinal) == true && !IsRules2Content(e.contentId)))
                return Fail(out error, "Unknown dialogue content is not supported by this version.");
            if (s.dialogue.episodes.GroupBy(e => e.id).Any(g => g.Count() != 1)
                || s.dialogue.episodes.GroupBy(e => e.occasionId).Any(g => g.Count() != 1)
                || s.dialogue.episodes.GroupBy(e => e.entryEventId).Any(g => g.Count() != 1)
                || s.storylines.GroupBy(c => c.id).Any(g => g.Count() != 1)
                || s.houseEvents.GroupBy(e => e.id).Any(g => g.Count() != 1))
                return Fail(out error, "Dialogue identities must be unique.");
            var episodeIds = new HashSet<string>(s.dialogue.episodes.Select(e => e.id), StringComparer.Ordinal);
            if (s.storylines.Any(c => IsRules2Template(c.templateId) && !episodeIds.Contains(c.id))
                || s.houseEvents.Any(e => IsRules2Content(e.contentId) && !episodeIds.Contains(e.cycleId)))
                return Fail(out error, "A campaign cycle or event has no dialogue owner.");
            var ticks = new HashSet<string>(StringComparer.Ordinal);
            var logs = new HashSet<int>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            DialogueEpisodeState previous = null;
            int activeCount = 0;
            foreach (var episode in s.dialogue.episodes)
            {
                if (previous != null && (previous.time == null || episode.startedRevision <= previous.time.revision
                    || episode.startedSequence <= previous.time.sequence))
                    return Fail(out error, "Dialogue episodes must retain their nonoverlapping chronology.");
                if (!EpisodeRules2(s, episode, pending, ticks, logs, ids, out error)) return false;
                if (Cycle(s, episode).status == StorylineStatus.Active) activeCount++;
                previous = episode;
            }
            if (activeCount > 1) return Fail(out error, "Only one dialogue may be active.");
            if (s.houseEvents.Count + ReservedEvents(s) > MaximumEvents)
                return Fail(out error, "The active dialogue's remaining event capacity is reserved.");
            return true;
        }

        private static bool EpisodeRules2(EpisodeState s, DialogueEpisodeState e, EpisodeEngine.PendingDialogueCommand pending,
            HashSet<string> ticks, HashSet<int> logs, HashSet<string> ids, out string error)
        {
            error = null;
            int version = s.dialogueRulesVersion, steps = StepsFor(version);
            var cycle = Cycle(s, e);
            var template = Rules2Template(cycle?.templateId, e.contentVersion);
            if (template == null || e.occasionKind != DialogueCampaignV1.Kind
                || e.week < 1 || e.week > s.week || e.window != Windows.AfterVeto || e.startedPhase != EpisodePhase.Campaign
                || e.startedSequence < 1 || e.startedSequence >= s.nextSequence
                || e.id != "story-" + e.startedSequence.ToString(CultureInfo.InvariantCulture)
                || string.IsNullOrEmpty(e.npcId) || e.npcId.Length > 160 || e.npcId == s.playerId
                || s.Find(e.npcId) == null || e.occasionId != Occasion(e.week, e.npcId)
                || !Revision(s, e.startedRevision, e, DialogueRevisionField.Started, pending))
                return Fail(out error, "Invalid campaign episode identity or start.");
            if (cycle.variant != 0 || cycle.week != e.week || cycle.title != template.title || cycle.category != template.eyebrow
                || cycle.lane != StoryLanes.Game || !Cast(cycle.cast, e.npcId) || cycle.vars == null || cycle.vars.Count != 0
                || cycle.nextWeek != 0 || cycle.nextAnchor != null || cycle.path == null || cycle.path.Count < 1 || cycle.path.Count > steps)
                return Fail(out error, "The dialogue must own its exact campaign cycle.");
            if (e.entry == null || !AcceptsFunding(version, e.entry.kind))
                return Fail(out error, "This version funds a conversation with the window's action only.");
            string tick = EntryTick(e.entry, e.week, e.window);
            if (tick == null || e.entry.grantId != null
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
                return Fail(out error, "Unsupported campaign response or evidence storage.");
            var items = s.houseEvents.Where(item => item.cycleId == e.id).ToArray();
            if (items.Length < 2 || items.Length > steps || items[0].id != e.entryEventId)
                return Fail(out error, "The campaign event shelf does not match its entry.");
            var rescue = PublicRescueSource(s, e.npcId, e.week);
            string beatId = DialogueCampaignWord.Open, ending = null, answer = null, reaction = null, cited = null;
            int previousSequence = e.startedSequence, previousRevision = e.startedRevision - 1, responseIndex = 0;
            for (int index = 0; index < items.Length; index++)
            {
                var item = items[index]; var beat = template.Beat(beatId);
                if (beat == null || !EventRules2(item, beat, e, template.id, out int eventSequence) || eventSequence <= previousSequence || eventSequence >= s.nextSequence)
                    return Fail(out error, "A retained campaign event differs from its canonical definition.");
                previousSequence = eventSequence;
                if (index >= cycle.path.Count)
                {
                    if (index != items.Length - 1 || item.resolved || item.chosenIndex != -1 || item.outcome != null)
                        return Fail(out error, "Only the current campaign node may remain unanswered.");
                    break;
                }
                var step = cycle.path[index];
                if (step == null || step.beatId != beatId || step.week != e.week || !item.resolved
                    || item.chosenIndex < 0 || item.chosenIndex >= item.choices.Count
                    || step.optionId != item.choices[item.chosenIndex].optionId
                    || step.logSequence <= eventSequence || step.logSequence >= s.nextSequence || !logs.Add(step.logSequence)
                    || s.storylines.Any(other => other.id != e.id && other.path != null && other.path.Any(p => p != null && p.logSequence == step.logSequence)))
                    return Fail(out error, "A campaign step does not own its resolved event and outcome.");
                var option = beat.Option(step.optionId);
                bool lapsed = step.result == StoryResults.Lapsed;
                if (index == 0 && (step.optionId != DialogueCampaignWord.OpenCase || step.result != StoryResults.Plain)
                    || step.result != StoryResults.Plain && !lapsed
                    || lapsed && (index != cycle.path.Count - 1 || step.optionId != DialogueCampaignWord.Leave)
                    || item.outcome != (lapsed ? DialogueCampaignV1.LapsedOutcome : option.outcome))
                    return Fail(out error, "The campaign path must follow its exact authored edges.");
                previousSequence = step.logSequence;
                string suffix = lapsed ? "occasion-closed"
                    : index == 0 || option.next == DialogueCampaignWord.ByReaction || step.optionId == DialogueCampaignWord.Plea ? null
                    : step.optionId == DialogueCampaignWord.KeepOpen ? "no-promise-made" : "respectful-exit";
                if (suffix == null)
                {
                    string actual = Suffix(e, responseIndex, template.id);
                    if (index == 0)
                    {
                        if (Array.IndexOf(DialogueCampaignWord.Answers, actual) < 0)
                            return Fail(out error, "The voter's answer is not one this conversation can give.");
                        answer = actual;
                    }
                    else if (option.next == DialogueCampaignWord.ByReaction)
                    {
                        if (Array.IndexOf(DialogueCampaignWord.Reactions, actual) < 0)
                            return Fail(out error, "The voter's reaction is not one this conversation can give.");
                        reaction = actual; cited = DialogueCampaignWord.EvidenceOf(step.optionId);
                    }
                    else if (!VoteResponseRules2(s, e, actual, cited != null))
                        return Fail(out error, "An answered campaign must retain its actual vote response.");
                    suffix = actual;
                }
                bool omitted = lapsed && responseIndex == e.responses.Count;
                if (!omitted)
                {
                    if (e.responses.Count <= responseIndex) return Fail(out error, "A resolved campaign step is missing its response.");
                    var response = e.responses[responseIndex++];
                    if (response.eventId != item.id || response.pathIndex != index || response.speakerId != e.npcId
                        || response.responseKey != template.id + "/" + suffix
                        || !Revision(s, response.revision, response, DialogueRevisionField.Response, pending)
                        || response.revision <= previousRevision || index == 0 && response.revision != e.entry.revision)
                        return Fail(out error, "A campaign response does not match its accepted step.");
                    previousRevision = response.revision;
                }
                var retainedLog = s.events.FirstOrDefault(row => row.sequence == step.logSequence);
                if (retainedLog != null && (retainedLog.kind != StoryLog.Outcome || retainedLog.week != e.week
                    || retainedLog.phase != EpisodePhase.Campaign || retainedLog.audienceIds == null
                    || !retainedLog.audienceIds.SequenceEqual(new[] { s.playerId, e.npcId })))
                    return Fail(out error, "A retained campaign outcome has the wrong owner or audience.");
                string next = lapsed ? "end:lapsed" : option.next;
                if (next.StartsWith("end:", StringComparison.Ordinal))
                {
                    ending = next.Substring(4);
                    if (index != cycle.path.Count - 1 || index != items.Length - 1)
                        return Fail(out error, "A terminal campaign cannot contain another node.");
                    break;
                }
                if (next == DialogueCampaignWord.ByAnswer)
                {
                    string drawn = index + 1 < items.Length ? BeatOf(items[index + 1], template.id) : null;
                    if (drawn == null || DialogueCampaignWord.AnswerOf(drawn) != answer
                        || DialogueCampaignWord.OffersRescue(drawn) != (answer != DialogueCampaignWord.Word && rescue != null))
                        return Fail(out error, "The reply node must follow the voter's answer and the facts the player holds.");
                    beatId = drawn;
                }
                else if (next == DialogueCampaignWord.ByReaction) beatId = DialogueCampaignWord.FinishBeat(reaction);
                else beatId = next;
            }
            if (responseIndex != e.responses.Count || cycle.eventId != items[items.Length - 1].id || cycle.beatId != beatId)
                return Fail(out error, "The campaign path, current node and responses must agree.");
            if (!StatementRules2(s, e, cycle, items, answer, pending, ids, out error)) return false;
            if (!EvidenceRules2(s, e, cycle, items, cited, rescue, pending, ids, out error)) return false;
            if (ending == null)
            {
                if (cycle.status != StorylineStatus.Active || cycle.endedWeek != 0 || cycle.endingId != null
                    || e.time != null || items.Length != cycle.path.Count + 1)
                    return Fail(out error, "An active campaign must retain its funded unanswered node.");
                if (e.week != s.week) return Fail(out error, "An active campaign cannot survive its week.");
                if (!Eligible(s, e.npcId, out error)) return false;
            }
            else
            {
                if (cycle.status != StorylineStatus.Completed || cycle.endedWeek != e.week || cycle.endingId != ending
                    || e.time == null || e.time.reason != (ending == "lapsed" ? "occasion-closed" : ending)
                    || e.time.window != Windows.AfterVeto || e.time.proposedDelta != 0 || e.time.appliedDelta != 0
                    || e.time.spentRevision != 0 || e.time.spentSequence != 0 || e.time.spentWindowAction != 0
                    || !Revision(s, e.time.revision, e.time, DialogueRevisionField.Time, pending)
                    || e.time.sequence != cycle.path[cycle.path.Count - 1].logSequence)
                    return Fail(out error, "A closed campaign requires its exact zero-time receipt.");
                bool omitted = responseIndex != cycle.path.Count;
                bool currentClosure = e.time.revision == s.revision || e.time.revision > s.revision;
                bool present = s.Find(e.npcId).status == ContestantStatus.Active;
                if (omitted && (ending != "lapsed" || e.time.revision <= previousRevision || present)
                    || !omitted && (e.time.revision != previousRevision || ending == "lapsed" && currentClosure && !present))
                    return Fail(out error, "The campaign closure and its actual speaker response disagree.");
                if (ending == "lapsed" && currentClosure && e.week == s.week && s.phase == EpisodePhase.Campaign
                    && Eligible(s, e.npcId, out _) && pending?.OwnsCampaignClosure(s, e) != true)
                    return Fail(out error, "The current campaign occasion has not closed.");
            }
            return true;
        }

        private static string Suffix(DialogueEpisodeState e, int index, string templateId)
        {
            string key = e.responses.Count > index ? e.responses[index].responseKey : null;
            return key != null && key.StartsWith(templateId + "/", StringComparison.Ordinal) ? key.Substring(templateId.Length + 1) : null;
        }

        private static string BeatOf(HouseEventState item, string templateId) =>
            item?.contentId != null && item.contentId.StartsWith(templateId + ":", StringComparison.Ordinal)
                ? item.contentId.Substring(templateId.Length + 1) : null;

        private static bool Sequence(HouseEventState item, out int sequence)
        {
            sequence = 0;
            return item.id != null && item.id.StartsWith("house-event-", StringComparison.Ordinal)
                && int.TryParse(item.id.Substring(12), NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
        }

        // Rules 1's event check, with each option's own risk: a reply that can backfire says so.
        private static bool EventRules2(HouseEventState item, BeatTemplate beat, DialogueEpisodeState episode, string templateId, out int sequence)
        {
            sequence = 0;
            if (!Sequence(item, out sequence) || sequence < 1 || item.id != "house-event-" + sequence.ToString(CultureInfo.InvariantCulture)
                || item.kind != HouseEventKind.Story || item.week != episode.week || item.cycleId != episode.id
                || item.contentId != templateId + ":" + beat.id || item.title != beat.title || item.narrative != beat.summary
                || item.closesAnchor != beat.closes || item.surface != beat.surface || item.venue != beat.venue || item.lapseOptionId != beat.lapse
                || !Cast(item.cast, episode.npcId) || item.involvedIds == null || !item.involvedIds.SequenceEqual(new[] { episode.npcId })
                || item.choices == null || item.choices.Count != beat.options.Length) return false;
            for (int index = 0; index < beat.options.Length; index++)
            {
                var c = item.choices[index]; var o = beat.options[index];
                if (c == null || c.optionId != o.id || c.label != o.label || c.description != o.description || c.risk != o.risk
                    || c.glyph != null || c.approach != null || c.checkBase != -1 || c.subjectId != null || c.lapse != (o.id == beat.lapse)
                    || c.costsAction || c.conduct || c.pickPerson || c.locked || c.lockReason != null || c.trustChange != 0
                    || c.next != o.next || c.nextOnBackfire != null || c.impacts == null || c.impacts.Count != 0
                    || c.eligibleIds == null || c.eligibleIds.Count != 0 || c.bonusTraits == null || c.bonusTraits.Count != 0
                    || c.against == null || c.against.Count != 0 || c.effects == null || c.effects.Count != 0 || c.backfire == null || c.backfire.Count != 0) return false;
            }
            return true;
        }

        // The plea at the end is the ordinary plea's: Strategic after a citation, Emotional otherwise (§5.1).
        private static bool VoteResponseRules2(EpisodeState s, DialogueEpisodeState e, string suffix, bool cited)
        {
            string response = suffix == "vote-" + LobbyResponse.Receptive ? LobbyResponse.Receptive
                : suffix == "vote-" + LobbyResponse.Open ? LobbyResponse.Open
                : suffix == "vote-" + LobbyResponse.Skeptical ? LobbyResponse.Skeptical
                : suffix == "vote-" + LobbyResponse.Hostile ? LobbyResponse.Hostile : null;
            if (response == null) return false;
            var rows = s.lobbies.Where(l => l.week == e.week && l.phase == EpisodePhase.Campaign && l.deciderId == e.npcId).ToArray();
            string approach = cited ? LobbyApproach.Strategic : LobbyApproach.Emotional;
            return (e.week < s.week && rows.Length == 0) || rows.Length == 1 && rows[0].ask == LobbyAsk.Vote
                && rows[0].subjectId == s.playerId && rows[0].approach == approach && rows[0].response == response;
        }

        private static bool FreeSequence(EpisodeState s, int sequence) => sequence > 0 && !s.events.Any(x => x.sequence == sequence)
            && !s.houseEvents.Any(x => x.id == "house-event-" + sequence.ToString(CultureInfo.InvariantCulture))
            && !s.storylines.Any(c => c.id == "story-" + sequence.ToString(CultureInfo.InvariantCulture));

        // One statement a conversation (§6): the voter's word, copied from the claim they told the player, the moment they gave it.
        private static bool StatementRules2(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, HouseEventState[] items, string answer,
            EpisodeEngine.PendingDialogueCommand pending, HashSet<string> ids, out string error)
        {
            error = null;
            bool told = answer == DialogueCampaignWord.Word || answer == DialogueCampaignWord.Against;
            if (!told) return e.statement == null || Fail(out error, "Only a voter who gave their word holds a statement.");
            var row = e.statement;
            var response = e.responses.FirstOrDefault(r => r.pathIndex == 0);
            int sequence = cycle.path[0].logSequence - 1;
            if (row == null || response == null || !Sequence(items[0], out int opened) || sequence <= opened || !FreeSequence(s, sequence)
                || row.id != "dialogue-statement-" + sequence.ToString(CultureInfo.InvariantCulture) || !ids.Add(row.id) || row.sequence != sequence
                || row.eventId != items[0].id || row.pathIndex != 0 || row.speakerId != e.npcId
                || row.conditionKind != VoteClaim || row.answer != VoteOut || row.week != e.week
                || row.revision != response.revision || !Revision(s, row.revision, row, DialogueRevisionField.Statement, pending)
                || row.audienceIds == null || !row.audienceIds.SequenceEqual(new[] { s.playerId })
                || string.IsNullOrEmpty(row.targetId) || s.Find(row.targetId) == null || row.targetId == e.npcId
                || (answer == DialogueCampaignWord.Against) != (row.targetId == s.playerId)
                // While the week is on, the claim it copies is still on the ledger, word for word.
                || e.week == s.week && s.ledger?.claims?.Any(k => k != null && k.week == e.week && k.voterId == e.npcId
                    && k.source == ClaimSource.Told && k.targetId == row.targetId) != true)
                return Fail(out error, "A voter's word must be its exact copied vote claim.");
            return true;
        }

        private static bool EvidenceRules2(EpisodeState s, DialogueEpisodeState e, StorylineState cycle, HouseEventState[] items, string cited,
            PowerRow rescue, EpisodeEngine.PendingDialogueCommand pending, HashSet<string> ids, out string error)
        {
            error = null;
            if (cited == null) return e.evidence.Count == 0 || Fail(out error, "Only a citing reply may retain evidence.");
            if (e.evidence.Count != 1) return Fail(out error, "A citation requires exactly one source-owned evidence row.");
            var row = e.evidence[0];
            var response = e.responses.FirstOrDefault(r => r.pathIndex == 1);
            int sequence = cycle.path[1].logSequence - 1;
            bool owned = row.kind == cited && row.pathIndex == 1 && row.eventId == items[1].id && response != null
                && row.capturedRevision == response.revision
                && Revision(s, row.capturedRevision, row, DialogueRevisionField.EvidenceCaptured, pending)
                && Sequence(items[1], out int replied) && sequence > replied && FreeSequence(s, sequence)
                && row.id == "dialogue-evidence-" + sequence.ToString(CultureInfo.InvariantCulture) && ids.Add(row.id);
            bool source = cited == DialogueCampaignWord.StatementEvidence
                    ? row.statementId != null && row.statementId == e.statement?.id && row.power == null && row.memory == null
                : cited == DialogueCampaignWord.PowerEvidence
                    ? row.statementId == null && row.memory == null && SamePower(row.power, rescue)
                : cited == DialogueCampaignWord.MemoryEvidence && row.statementId == null && row.power == null && CaughtLieCopy(s, e, row.memory);
            if (!owned || !source)
                return Fail(out error, "The citation must retain its exact owned revision, event, identity and source.");
            return true;
        }

        // The copy outlives the memory list's thirty rows, so it is checked on its own terms: the player's, about this
        // voter, from an earlier week, in the reveal's own words.
        private static bool CaughtLieCopy(EpisodeState s, DialogueEpisodeState e, MemoryState m) => m != null
            && m.ownerId == s.playerId && m.subjectId == e.npcId && m.isPrivate && m.week >= 1 && m.week < e.week
            && m.text != null && m.text.Length <= 2000 && m.text.StartsWith(LiePrefix(s, e.npcId, m.week), StringComparison.Ordinal);
    }
}
