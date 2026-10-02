using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// A conversation's rows under the dial, in four groups by what they are for
    /// (ACTIONS-DEALS-ALLIANCES-PLAN V5): BOND, the time spent together; LEARN, the questions and
    /// what you share; SCHEME, the moves against somebody; BARGAIN, the deals, promises, pacts and
    /// calls.
    ///
    /// <para>Every verb aimed at a third houseguest is one row now, and pressing it opens its people
    /// beneath it (EpisodeHud.ConversationGroups.cs). A conversation drew four rows for each of them -
    /// vent, lie, whisper, call out - and a target agreement besides: fifty controls in a house of
    /// eight, ninety in a house of sixteen. Grouped it is twenty-five in free time and thirty-two in
    /// the campaign, whatever the size of the house. Every caption keeps its words; the people's
    /// buttons carry the full sentence, so "Vent about Jo" is still the control called that.</para>
    ///
    /// <para>What has to be answered stays above the dial, where it always was: what the player came
    /// for, a loyalty declaration on offer, and the plea to whoever is deciding.</para>
    ///
    /// <para>Presentation only. No command, roll, cost or saved field changes here; the rows commit
    /// exactly what they committed before.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The words on the pill beside a conversation's verbs (see <see cref="Category"/>).</summary>
        public const string WarmthTag = "warmth", LearnTag = "learn", RiskTag = "risk", BindsYouTag = "binds you", FreeTag = "free";

        /// <summary>
        /// The pill on "Leave our alliance" the week its houseguest turned on the pact (C2): free, and
        /// nobody holds it against you. In a pact of three or more it cuts the betrayer out and the
        /// rest of you keep the pact, and the pill says that instead.
        /// </summary>
        public const string FreeExitTag = FreeTag + " · no grudge", FreeExitCutOutTag = FreeTag + " · cuts them out";

        /// <summary>The four groups, as their heads read.</summary>
        public const string BondGroupTitle = "BOND", LearnGroupTitle = "LEARN", SchemeGroupTitle = "SCHEME", BargainGroupTitle = "BARGAIN";

        /// <summary>
        /// The verb rows that open a picker of people. Each is new, and the ellipsis is what keeps it
        /// unique: a person's caption has a space and a name where it has the ellipsis, so no
        /// houseguest's name - not even one the player typed, "someone" included - can make a
        /// person's button carry its verb row's caption.
        /// </summary>
        public const string VentPickerCaption = "Vent about…",
            LiePickerCaption = "Tell them something untrue about…",
            WhisperPickerCaption = "Whisper about…",
            CalloutPickerCaption = "Call out publicly…",
            TargetDealPickerCaption = "Propose a target agreement against…",
            PromiseToEvictPickerCaption = "Promise to evict…";

        /// <summary>The words on one person's button, exactly as their row always read.</summary>
        public static string VentCaption(string about) => "Vent about " + about;
        public static string LieCaption(string about) => "Tell them something untrue about " + about;
        public static string PromiseToEvictCaption(string about) => "Promise to evict " + about;

        /// <summary>The pill beside a command's control in a conversation (<see cref="Category"/>). A read for tests.</summary>
        public static string VerbTag(EpisodeCommandKind kind) => Category(kind);

        /// <summary>
        /// What a loyalty declaration on offer says it is (X12). The engine checks every nomination and
        /// every revealed vote between the two of them, in either direction, and a breach by either is
        /// logged to the whole house, which turns on the breaker (WebLoyaltyOaths.Resolve). Nothing
        /// makes the houseguest weigh it when they decide, so it is no promise from them either.
        /// </summary>
        public static string OathOfferLine(string name) =>
            "This is your commitment, not " + name + "'s consent or promise. It holds the two of you all the same: "
            + "if either of you nominates or votes to evict the other, the whole house hears the oath was broken, "
            + "and most of them think less of whoever broke it.";

        /// <summary>What a declaration the player has made says, in its houseguest's conversation (X12).</summary>
        public static string OathRecordedLine(string name) =>
            "Your loyalty declaration is recorded. It is not a promise from " + name + ", but it holds both ways: "
            + "if either of you nominates or votes to evict the other, the whole house hears the oath was broken, "
            + "and most of them think less of whoever broke it.";

        /// <summary>
        /// What the notebook says under each declaration (X12), in the same terms: no promise from
        /// them, and broken in front of the house by a nomination or an evicting vote either way.
        /// </summary>
        public const string OathNotebookNote =
            "It is not a promise from them, but it holds both ways: a nomination or a vote to evict between you, by either of you, breaks it in front of the house.";

        /// <summary>How every target agreement's caption begins: the deal table's own words, which the picker folds.</summary>
        private static readonly string TargetDealStem =
            EpisodeHud.DealProposeCaption(DealKind.Title(DealKind.TargetAgreement).ToLowerInvariant() + " against ");

        /// <summary>The line under each group's head: what the group is for and what it costs.</summary>
        public const string BondLine = "Time with them: the topics on the dial, and these. Each costs an action.",
            LearnLine = "What they know and what they think. Reading them, and asking about the vote in the campaign, are free once a week.",
            // In a decision window the look and the vote question are not on offer (AskRows): the
            // read waits for free time and the campaign, and the vote question for the campaign.
            LearnWindowLine = "What they know and what they think. Reading them waits for free time and the campaign.",
            SchemeLine = "Turn the house against someone. Each costs an action, and most can backfire on you.",
            BargainLine = "Deals, promises and alliances. Proposing one costs an action; answering an offer is free.";

        /// <summary>Whose conversation the open picker belongs to, and which verb it is, by its row's caption. View state.</summary>
        private string conversationPick, conversationPickFor;

        /// <summary>The verb whose people the open conversation is showing, by its row's caption; null when every picker is shut. A read for tests.</summary>
        public string ConversationPicker => focusedNpc != null && conversationPickFor == focusedNpc.Id ? conversationPick : null;

        /// <summary>
        /// Whom the player may promise this houseguest to vote out: the nominees, during the campaign,
        /// to a player who has a vote - never the houseguest's own eviction (X2).
        ///
        /// <para>The engine refuses a promise from a nominee, from the Head of Household and outside
        /// the campaign, so those rows could only ever be refused. It accepts a promise to evict the
        /// person it is made to, and keeping it then earns their thanks on their way to the jury;
        /// the row is simply not offered.</para>
        /// </summary>
        public static List<string> PromiseToEvictTargets(EpisodeState state, string npcId)
        {
            if (state == null || state.phase != EpisodePhase.Campaign || state.nominees == null) return new List<string>();
            if (!EpisodeEngine.Voters(state).Any(voter => voter.id == state.playerId)) return new List<string>();
            return state.nominees.Where(id => id != npcId && id != state.playerId && state.Find(id) != null).ToList();
        }

        /// <summary>
        /// The four groups, under the dial. <paramref name="cameToAsk"/> and <paramref name="cameToDeal"/>
        /// leave out what was already drawn above it.
        /// </summary>
        private void ConversationGroups(EpisodeState state, ContestantState npc, bool window, bool allied, bool cameToAsk, bool cameToDeal)
        {
            var others = state.Active.Where(c => !c.isPlayer && c.id != npc.id).ToList();

            // BOND: the dial above, and the time spent together below it. "Talk game openly" stays
            // the first row after the dial, which is where the dial's More petal sends the keyboard.
            hud.ConversationGroup(BondGroupTitle, "heart", BondLine);
            hud.Tag(hud.Action(EpisodeHud.DiscussGameCaption, () => Commit(state, EpisodeCommandKind.DiscussGame, npc.id)),
                Category(EpisodeCommandKind.DiscussGame));
            // What this room offers that no other does (decision D-E): pillow talk in a bedroom,
            // an invitation in the suite, cooking in the kitchen.
            RoomActs(state, npc);

            // LEARN: the questions, unless they were asked first, and what you share.
            hud.ConversationGroup(LearnGroupTitle, "eye", window ? LearnWindowLine : LearnLine);
            if (!cameToAsk) AskRows(state, npc, window);
            hud.Tag(hud.Action("Share something I know", () => Commit(state, EpisodeCommandKind.ShareInformation, npc.id)),
                Category(EpisodeCommandKind.ShareInformation));

            // SCHEME: each verb about a third houseguest is one row that opens its people. Venting and
            // lying are said to the person in front of you; a rumour is told to the house, so it waits
            // for free time, as scheming does.
            if (others.Count > 0 || !window)
            {
                hud.ConversationGroup(SchemeGroupTitle, "target", SchemeLine);
                PersonPicker(npc, VentPickerCaption, Category(EpisodeCommandKind.VentAbout), others, about => VentCaption(about.name),
                    about => Commit(state, EpisodeCommandKind.VentAbout, npc.id, about));
                PersonPicker(npc, LiePickerCaption, Category(EpisodeCommandKind.SpreadLie), others, about => LieCaption(about.name),
                    about => Commit(state, EpisodeCommandKind.SpreadLie, npc.id, about));
                if (!window)
                {
                    // Told to the person in front of you (R0, X7): the engine reaches them under the
                    // commitment rules, and before those rules it drew a listener as it always did.
                    PersonPicker(npc, WhisperPickerCaption, Category(EpisodeCommandKind.SpreadRumor), others, about => EpisodeHud.WhisperCaption(about.name),
                        about => Commit(state, EpisodeCommandKind.SpreadRumor, about, npc.id, text: EpisodeEngine.WhisperCampaign));
                    PersonPicker(npc, CalloutPickerCaption, Category(EpisodeCommandKind.SpreadRumor), others, about => EpisodeHud.CalloutCaption(about.name),
                        about => Commit(state, EpisodeCommandKind.SpreadRumor, about, text: EpisodeEngine.PublicCallout));
                    hud.Tag(hud.Action("Work against them quietly", () => Commit(state, EpisodeCommandKind.SchemeAgainst, npc.id)),
                        Category(EpisodeCommandKind.SchemeAgainst));
                }
            }

            // BARGAIN: what carries no chance first - the promises and the pact, a promise about the
            // vote, and calling it through an ally - then the deal table, whose heading and note on
            // the odds speak only for the rows under them: an offer waiting, then what could be put.
            // The came-to-deal path draws the promises before the table for the same reason. Under
            // the commitment rules the pact carries the invitation's chance (C4), and the note on the
            // odds comes above it instead, once (ProposeAllianceRow).
            var promised = PromiseToEvictTargets(state, npc.id);
            var calls = Calls(state, npc);
            if (!cameToDeal || promised.Count > 0 || calls.Count > 0)
            {
                hud.ConversationGroup(BargainGroupTitle, "handshake", BargainLine);
                if (!cameToDeal) DealRows(state, npc, allied);
                PersonPicker(npc, PromiseToEvictPickerCaption, Category(EpisodeCommandKind.PromiseVote),
                    promised.Select(state.Find).ToList(), nominee => PromiseToEvictCaption(nominee.name),
                    nominee => Commit(state, EpisodeCommandKind.PromiseVote, npc.id, nominee));
                // Calling the vote (STRATEGY-LOOP-PLAN.md section 3): through an ally, once per
                // alliance a week, naming who the bloc evicts. At most a row per nominee, so these
                // stay rows.
                foreach (var call in calls)
                {
                    string about = call.nomineeId, allianceId = call.pact.id;
                    hud.Tag(hud.ActionFor(about, EpisodeHud.CallTheVoteCaption(call.pact.name, state.Find(about).name),
                            () => Commit(state, EpisodeCommandKind.CallTheVote, npc.id, about, text: allianceId)),
                        Category(EpisodeCommandKind.CallTheVote), EpisodeHud.TagSeat.PastReading);
                }
                if (!cameToDeal) FoldedDealPanel(state, npc);
            }

            // An open picker keeps its row at the top of the column, with as many of its people under
            // it as the column holds: every render starts the column at its top - the one that opens
            // the picker, and any the house orders while it is open - and the keyboard staying on the
            // row would otherwise leave the people below the fold. The HUD does it once the rows have
            // their final heights, not now. Anything the player says shuts the picker (Submit), so
            // the render that answers starts at the head of the conversation, as it always did.
            if (PickerOpen(npc.id, conversationPick)) hud.RevealAtTop(conversationPick);
        }

        /// <summary>Every alliance the player shares with this houseguest that has not called this week's vote, and each nominee it could name.</summary>
        private static List<(AllianceState pact, string nomineeId)> Calls(EpisodeState state, ContestantState npc)
        {
            var calls = new List<(AllianceState, string)>();
            if (!EpisodeEngine.LeverRulesOn(state) || state.phase != EpisodePhase.Campaign || !VoteRead.Available(state)) return calls;
            foreach (var pact in state.alliances.Where(a => a.active && a.members.Contains(state.playerId) && a.members.Contains(npc.id)
                         && !state.ledger.calls.Any(k => k.week == state.week && k.allianceId == a.id)))
                foreach (string nomineeId in state.nominees.Where(id => id != state.playerId))
                    calls.Add((pact, nomineeId));
            return calls;
        }

        /// <summary>
        /// The deal table, with its target agreements folded under one row. The table itself is
        /// untouched: the picker takes the rows whose captions begin as a target agreement's does,
        /// and everything else the table draws - its offers, its other proposals, its odds - is drawn
        /// as it always was.
        /// </summary>
        private void FoldedDealPanel(EpisodeState state, ContestantState npc)
        {
            string id = npc.id;
            hud.BeginPersonPicker(TargetDealPickerCaption, BindsYouTag, PickerOpen(id, TargetDealPickerCaption),
                () => TogglePicker(id, TargetDealPickerCaption), ClosePicker,
                caption => caption != null && caption.StartsWith(TargetDealStem, StringComparison.Ordinal));
            try { DealPanel(state, npc); }
            finally { hud.EndPersonPicker(); }
        }

        /// <summary>
        /// One verb's row and, while it is open, its people: a button for each, captioned
        /// <paramref name="caption"/>, committing <paramref name="choose"/>. Nobody to aim it at, no row.
        /// </summary>
        private void PersonPicker(ContestantState npc, string verb, string tag, IList<ContestantState> people,
            Func<ContestantState, string> caption, Action<string> choose)
        {
            string id = npc.id;
            hud.BeginPersonPicker(verb, tag, PickerOpen(id, verb), () => TogglePicker(id, verb), ClosePicker);
            try
            {
                foreach (var person in people)
                {
                    if (person == null) continue;
                    string about = person.id;
                    hud.ActionFor(about, caption(person), () => choose(about));
                }
            }
            finally { hud.EndPersonPicker(); }
        }

        private bool PickerOpen(string npcId, string verb) => conversationPickFor == npcId && conversationPick == verb;

        /// <summary>Opens one verb's people and shuts any other; pressed again, shuts it. A view change: nothing is committed.</summary>
        private void TogglePicker(string npcId, string verb)
        {
            if (focusedNpc == null || focusedNpc.Id != npcId) return;
            conversationPick = PickerOpen(npcId, verb) ? null : verb;
            conversationPickFor = npcId;
            Render();
        }

        /// <summary>A person was chosen: the picker shuts, and the conversation answers at its head.</summary>
        private void ClosePicker() => conversationPick = null;
    }
}
