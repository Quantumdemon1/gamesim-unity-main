using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The veto meeting's screens (PACK8-PASS-PLAN B3, the owner's mockups 81 and 82). Screenshots
    /// 74 and 75 were a paragraph of who holds what, a title, the holder twice when they were on
    /// the block, and "Emma Brown saves Emma Brown" after it; the holder's own decision had no title
    /// at all, and a player who was Head of Household and holder read every replacement twice.
    ///
    /// <para>Each screen fits the strategy stage without a scroll in the default house: who holds
    /// what in a strip across the header; the title; the holder and the block as one row of cards,
    /// the holder once, with both pills; and under them whatever the player has to do. The captions
    /// are the ones the walks press - "Save {name} (HoH chooses replacement)", "Do not use the
    /// veto", a candidate's name, "Continue episode" - and the mockup's words are headlines and
    /// lines beside them.</para>
    ///
    /// <para>The row of cards gives way first: it takes only the height the rest of the step leaves
    /// it, and none when there is none. What can still run past the stage is the list of who can go
    /// up in a large house - thirteen names at sixteen, a row of faces and readings for every three
    /// - and a nominee's offer still waiting at the larger text. So that list is always the last
    /// thing on the screen, under the decision and the rule it follows from, and when the step is
    /// taller than the stage it is the end of the list that scrolls, never a way to decide.</para>
    ///
    /// <para>What the player sees is what the house knows: nobody is told what a houseguest holding
    /// the veto will do before they do it, unless the player is the Head of Household who has to
    /// name the replacement, which is how the meeting has always worked. Nothing here is saved; the
    /// Head of Household and holder's choice of whom to save is the screen's until a name is
    /// pressed.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The meeting's words, beside the captions they sit on (PACK8-PASS-PLAN decision 4).</summary>
        public const string UseTheVetoEyebrow = "USE THE VETO", KeepNominationsLine = "Keep nominations the same",
            VetoInfoLine = "Using the veto removes one nominee and forces the HoH to name a replacement.",
            HoldTheMeetingHeadline = "HOLD THE MEETING", ToTheCampaignHeadline = "CONTINUE TO EVICTION CAMPAIGN",
            NameTheReplacementEyebrow = "NAME THE REPLACEMENT NOMINEE", ReplacementMarkWords = "REPLACEMENT NOMINEE",
            OnTheBlockPill = "ON THE BLOCK", SavedPill = "SAVED", VetoUsedPill = "VETO USED", VetoNotUsedPill = "VETO NOT USED";

        /// <summary>Why the holder has only the one way, as the decision has always said it.</summary>
        public const string VetoLockedLine = "At the final four a veto holder who is not on the block cannot use the veto. Nominations stand.",
            VetoNoReplacementLine = "No legal replacement exists at the final four, so the veto cannot be used.";

        /// <summary>The caption of a choice of whom to save, when the player is Head of Household and holder: a pick, not a commit.</summary>
        public static string VetoSavePickCaption(string name) => "Save " + name;

        /// <summary>Whom the player, Head of Household and holder at once, has picked to save this week: the screen's, never saved.</summary>
        private string vetoSavePick;
        private int vetoSavePickWeek;

        /// <summary>The meeting's row of cards, waiting for the rest of the step so it can take the height left.</summary>
        private sealed class PendingMeetingRow
        {
            public int At, Marker;
            public List<EpisodeHud.MeetingCard> Cards;
        }
        private PendingMeetingRow meetingRow;

        /// <summary>
        /// Which way the week's veto went, read from committed state: this week's power row in the
        /// ledger, and without one - a season saved before the ledger kept it - from who was named
        /// this week and is no longer on the block, with the replacement where the engine puts it,
        /// at the block's end.
        /// </summary>
        public struct VetoOutcome
        {
            public bool Used;
            public string HolderId, SavedId, ReplacementId;
        }

        public static VetoOutcome VetoMeetingOutcome(EpisodeState s)
        {
            var outcome = new VetoOutcome { HolderId = s?.vetoHolderId };
            if (s == null || !s.vetoResolved) return outcome;
            var row = s.ledger?.power?.LastOrDefault(p => p.week == s.week);
            if (row != null && !string.IsNullOrEmpty(row.vetoHolderId))
            {
                outcome.Used = row.vetoUsed;
                outcome.SavedId = row.vetoUsed ? row.savedId : null;
                outcome.ReplacementId = row.vetoUsed ? row.replacementId : null;
                return outcome;
            }
            var saved = s.contestants.FirstOrDefault(c => c.status == ContestantStatus.Active && c.nominationWeeks != null
                && c.nominationWeeks.Contains(s.week) && !s.nominees.Contains(c.id));
            if (saved != null)
            {
                outcome.Used = true;
                outcome.SavedId = saved.id;
                outcome.ReplacementId = s.nominees.LastOrDefault();
            }
            return outcome;
        }

        /// <summary>
        /// "Emma Brown is using the veto on herself.": the holder's decision as the Head of
        /// Household who must name the replacement hears it, with the holder's own reflexive when
        /// they are saving themselves. Null without a holder or a saved nominee.
        /// </summary>
        public static string VetoUsingLine(EpisodeState s, string savedId)
        {
            var holder = s?.Find(s.vetoHolderId);
            var saved = s?.Find(savedId);
            if (holder == null || saved == null) return null;
            string whom = saved.id == holder.id ? (holder.isPlayer ? "yourself" : StoryPeople.Pronouns(holder).themselves)
                : saved.isPlayer ? "you" : saved.name;
            return (holder.isPlayer ? "You are" : holder.name + " is") + " using the veto on " + whom + ".";
        }

        /// <summary>What the Head of Household reads over the replacement's candidates after a houseguest's save.</summary>
        public static string VetoReplacementLine(EpisodeState s, string savedId)
        {
            string line = VetoUsingLine(s, savedId);
            return line == null ? null : line + " Name the replacement nominee.";
        }

        /// <summary>
        /// The house's status line on the veto meeting, as the strip across the stage's header;
        /// false anywhere else, and the caller writes the paragraph it always wrote. Nothing over a
        /// preparation view, where the paragraph says nothing either.
        /// </summary>
        private bool VetoMeetingStatus(EpisodeState s) =>
            s != null && s.phase == EpisodePhase.VetoMeeting && !ViewOverPreparation(s) && hud.HouseStatusStrip(HouseStatus(s));

        /// <summary>
        /// The ceremony's screen for the meeting when the player has nothing to decide: before it,
        /// the holder and the block, and the way on wears "HOLD THE MEETING" - the houseguest
        /// decides at that press, and nothing here says what they will do; after it, one row that
        /// says who went up in whose place and whether the veto was used, and the way on wears
        /// "CONTINUE TO EVICTION CAMPAIGN". The row is built last (<see cref="VetoMeetingWayOn"/>),
        /// in the height the rest of the step leaves it.
        /// </summary>
        private void VetoMeetingScreen(EpisodeState s)
        {
            // Never a row a render before this one left: its place in the column was that render's.
            meetingRow = null;
            var holder = s.Find(s.vetoHolderId);
            if (holder == null) return;
            if (!s.vetoResolved)
            {
                hud.MeetingTitle("Power of Veto Meeting", holder.name + " holds the Golden Power of Veto and must decide whether to use it.");
                meetingRow = new PendingMeetingRow { At = hud.MeetingColumnMark(), Marker = -1, Cards = MeetingCardsBefore(s, null) };
                return;
            }
            var outcome = VetoMeetingOutcome(s);
            // The line the meeting logged, which says who saved whom in the house's words.
            var decision = s.events.LastOrDefault(e => e.kind == "veto" && e.week == s.week);
            hud.MeetingTitle("The Veto Meeting Is Over", decision?.text);
            var cards = MeetingCardsAfter(s, outcome, out int marker);
            meetingRow = new PendingMeetingRow { At = hud.MeetingColumnMark(), Marker = marker, Cards = cards };
            hud.MeetingInfoStrip(outcome.Used ? VetoUsedLine : VetoNotUsedLine, PackArt.Pack8VetoOutcome, PackArt.Pack8IconVeto, "veto-token", UiTheme.Gold);
        }

        /// <summary>
        /// The end of the meeting's step, after the way on is pinned: the row of cards the screen
        /// left for last, in the height the column has left, and the mockup's words on the way on.
        /// Anywhere but the meeting it clears what it was left and does nothing.
        /// </summary>
        private void VetoMeetingWayOn(EpisodeState s)
        {
            var pending = meetingRow;
            meetingRow = null;
            if (s == null || s.phase != EpisodePhase.VetoMeeting) return;
            if (pending != null) hud.MeetingCards(pending.Cards, pending.Marker, ReplacementMarkWords, hud.MeetingRoomLeft(), pending.At);
            if (s.vetoResolved) hud.WayOnHeadline(ToTheCampaignHeadline, UiTheme.Accent, PackArt.Pack8ContinueButton);
            else hud.WayOnHeadline(HoldTheMeetingHeadline, UiTheme.Gold, PackArt.Pack8ButtonGold);
        }

        /// <summary>
        /// The meeting's decision on the episode screen, when it is the player's (PACK8-PASS-PLAN
        /// B3): the holder uses the veto on a nominee or keeps the block; the Head of Household
        /// names the replacement after a houseguest's save; or the player is both and does both.
        /// False when the player is the Head of Household and the houseguest holding the veto will
        /// not use it: then there is nothing to decide, and the ceremony's screen is drawn. The
        /// diary keeps its own column (<see cref="RenderPlayerDecision"/>).
        /// </summary>
        private bool VetoMeetingDecision(EpisodeState state)
        {
            bool holds = state.vetoHolderId == state.playerId;
            if (!holds)
            {
                string savedByNpc = EpisodeEngine.NpcVetoSave(state);
                if (savedByNpc == null) return false;
                hud.MeetingTitle("Power of Veto Meeting", VetoReplacementLine(state, savedByNpc));
                int mark = hud.MeetingColumnMark();
                var named = MeetingCardsBefore(state, savedByNpc);
                hud.MeetingEyebrow(NameTheReplacementEyebrow, UiTheme.Danger, PackArt.Pack8IconTarget, "target");
                // Which names would break the player's word, over the names (EpisodeDirector.YourWord).
                hud.BreachStrip(VetoWarning(state, null));
                VetoReplacementGrid(state, savedByNpc);
                hud.MeetingCards(named, -1, null, hud.MeetingRoomLeft(), mark);
                return true;
            }

            bool alsoHoh = state.hohId == state.playerId;
            hud.MeetingTitle("Power of Veto Meeting", alsoHoh
                ? "You hold the Golden Power of Veto, and as Head of Household you name the replacement too."
                : "You hold the Golden Power of Veto and must decide whether to use it.");
            int at = hud.MeetingColumnMark();
            var cards = MeetingCardsBefore(state, null);
            // Nominees who asked for it, answered where the decision is made.
            VetoOffers(state);
            bool locked = EpisodeEngine.VetoIsLockedAtFinalFour(state);
            if (locked || !EpisodeEngine.ReplacementCandidates(state).Any())
            {
                KeepTheBlock(state);
                hud.Paragraph(locked ? VetoLockedLine : VetoNoReplacementLine);
                hud.BreachStrip(VetoWarning(state, null));
            }
            else
            {
                hud.MeetingEyebrow(UseTheVetoEyebrow, UiTheme.Gold, PackArt.Pack8IconVeto, "veto-token");
                // Whom the Head of Household and holder is saving: null for a holder who is not.
                string saving = null;
                if (alsoHoh)
                {
                    // Whom to save, then one list of who goes up in their place: every candidate's
                    // name once. The pick is the screen's; the first nominee until another is pressed.
                    saving = vetoSavePickWeek == state.week && state.nominees.Contains(vetoSavePick) ? vetoSavePick : state.nominees.FirstOrDefault();
                    var pick = hud.MeetingGrid(EpisodeHud.MeetingChoiceWidth, 2);
                    foreach (var nominee in state.nominees)
                    {
                        string id = nominee;
                        var choice = hud.MeetingGridActionFor(pick, id, VetoSavePickCaption(state.Find(id).name), () =>
                        {
                            vetoSavePick = id; vetoSavePickWeek = state.week;
                            Render();
                        });
                        hud.MeetingPicked(choice, id == saving);
                    }
                }
                else
                {
                    // The nominees are peers, side by side under the eyebrow, as they always were.
                    var saves = hud.Pairs();
                    foreach (var nominee in state.nominees)
                    {
                        string saved = nominee;
                        hud.MeetingGoldFrame(hud.PairedActionFor(saves, saved, "Save " + state.Find(saved).name + " (HoH chooses replacement)", () =>
                            OfferPlayerDecision(state, false, EpisodeCommandKind.ResolveVeto,
                                "Use the veto to save " + state.Find(saved).name + ". The HoH chooses the replacement.", saved, useVeto: true)));
                    }
                }
                KeepTheBlock(state);
                hud.MeetingInfoStrip(VetoInfoLine, PackArt.Pack8InfoStrip, PackArt.Pack8IconInfo, "bulb", UiTheme.Accent);
                // What each way would break of the player's word, under the rule and over who goes up
                // (EpisodeDirector.YourWord): a label, never a control, measured into the step.
                hud.BreachStrip(VetoWarning(state, saving));
                // Who goes up comes last, under the way to keep the block and the rule that says a
                // replacement follows: in a large house the list is taller than the stage has left,
                // and it is the end of the list that scrolls then, never the way to decline.
                if (state.Find(saving) != null)
                {
                    hud.MeetingEyebrow(NameTheReplacementEyebrow, UiTheme.Danger, PackArt.Pack8IconTarget, "target");
                    VetoReplacementGrid(state, saving);
                }
            }
            hud.MeetingCards(cards, -1, null, hud.MeetingRoomLeft(), at);
            return true;
        }

        /// <summary>The holder's way to keep the block: its own row, "Do not use the veto", under the mockup's words for it.</summary>
        private void KeepTheBlock(EpisodeState state) =>
            hud.MeetingKeepRow("Do not use the veto", KeepNominationsLine, () => OfferPlayerDecision(state, false,
                EpisodeCommandKind.ResolveVeto, "Decline to use the veto. Both current nominees remain nominated."));

        /// <summary>Who can go up in the saved nominee's place, each once, as a grid of peers: pressing a name uses the veto and names them.</summary>
        private void VetoReplacementGrid(EpisodeState state, string saved)
        {
            if (state.Find(saved) == null) return;
            var grid = hud.MeetingGrid(EpisodeHud.MeetingChoiceWidth, 4);
            foreach (var candidate in EpisodeEngine.ReplacementCandidates(state))
            {
                string id = candidate.id;
                hud.MeetingGridActionFor(grid, id, candidate.name, () => OfferPlayerDecision(state, false, EpisodeCommandKind.ResolveVeto,
                    "Use the veto to save " + state.Find(saved).name + " and nominate " + state.Find(id).name + " as the replacement.", saved, id, true));
            }
        }

        /// <summary>
        /// The row before the decision: the holder first, on the veto's gold, with ON THE BLOCK as
        /// well when they are on it - one card, not two (screenshot 74) - then the rest of the
        /// block. With a houseguest's save known to the Head of Household, the saved one carries
        /// SAVED instead.
        /// </summary>
        private static List<EpisodeHud.MeetingCard> MeetingCardsBefore(EpisodeState s, string saved)
        {
            var cards = new List<EpisodeHud.MeetingCard>();
            string holder = s.vetoHolderId;
            if (s.Find(holder) != null)
            {
                bool savesThemselves = saved != null && saved == holder;
                string second = savesThemselves ? SavedPill : s.nominees.Contains(holder) ? OnTheBlockPill : null;
                cards.Add(new EpisodeHud.MeetingCard(holder, "VETO", UiTheme.Gold, PackArt.Pack8VetoAutoHoh, true,
                    second, savesThemselves ? UiTheme.Positive : UiTheme.Danger));
            }
            foreach (var id in s.nominees.Where(id => id != holder && s.Find(id) != null))
                cards.Add(id == saved
                    ? new EpisodeHud.MeetingCard(id, SavedPill, UiTheme.Positive, PackArt.Pack8HouseguestNeutral)
                    : new EpisodeHud.MeetingCard(id, "NOM", UiTheme.Danger, PackArt.Pack8HouseguestNominee));
            return cards;
        }

        /// <summary>
        /// The row after the meeting (mockup 82): the final block, the replacement last among them
        /// and beside the holder, the replacement's marker between, then the holder with VETO USED
        /// or VETO NOT USED - and a nominee the holder saved who was not the holder, as SAVED.
        /// <paramref name="marker"/> is the holder's place in the row, or -1 with no replacement.
        /// </summary>
        private static List<EpisodeHud.MeetingCard> MeetingCardsAfter(EpisodeState s, VetoOutcome outcome, out int marker)
        {
            var cards = new List<EpisodeHud.MeetingCard>();
            string holder = s.vetoHolderId;
            var block = s.nominees.Where(id => id != holder && s.Find(id) != null).ToList();
            bool replaced = outcome.Used && outcome.ReplacementId != null && block.Remove(outcome.ReplacementId);
            if (replaced) block.Add(outcome.ReplacementId);
            foreach (var id in block) cards.Add(new EpisodeHud.MeetingCard(id, "NOM", UiTheme.Danger, PackArt.Pack8HouseguestNominee));
            marker = -1;
            if (s.Find(holder) == null) return cards;
            if (replaced) marker = cards.Count;
            cards.Add(new EpisodeHud.MeetingCard(holder, outcome.Used ? VetoUsedPill : VetoNotUsedPill, outcome.Used ? UiTheme.Gold : UiTheme.Muted,
                PackArt.Pack8VetoAutoHoh, true, s.nominees.Contains(holder) ? "NOM" : null, UiTheme.Danger));
            if (outcome.Used && outcome.SavedId != null && outcome.SavedId != holder && s.Find(outcome.SavedId) != null)
                cards.Add(new EpisodeHud.MeetingCard(outcome.SavedId, SavedPill, UiTheme.Positive, PackArt.Pack8HouseguestNeutral));
            return cards;
        }
    }
}
