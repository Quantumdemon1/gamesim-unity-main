using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The rules a season the game starts plays under, in one place (BALANCE plan B0).
    ///
    /// <para>Every season the director starts goes through <see cref="ApplyFresh"/>, and so does every
    /// harness that means to measure the game as it ships: before this the director's setup was written
    /// inline in its StartSeason and each harness kept its own hand copy, which drifted (a default arm with
    /// no economy, finale or commitments measured a game nobody plays).</para>
    ///
    /// <para>Fresh seasons only. Load, migration, recovery and the importer never call it and never infer
    /// any of it: a season keeps the rules it was played under. A rule a later wave adds to the shipped
    /// game is added here, and only here; the director's StartSeason sets no rule field of its own
    /// (StressHouseTests holds it to that by reading its source).</para>
    ///
    /// <para>Pure: it reads and writes the state it is given and nothing else. The order is the
    /// director's own and matters - agency's first impressions are seeded on a season that has not begun,
    /// and the economy is selected only on an unplayed season with window rules.</para>
    /// </summary>
    public static class ShippedRules
    {
        /// <summary>Switches every shipped rule on for a fresh, unplayed season, from week one.</summary>
        public static void ApplyFresh(EpisodeState fresh) => ApplyFresh(fresh, null, alumniHistories: true);

        /// <summary>
        /// The same, with what the player chose on the cast screen (DIALOGUE-PLAN D1). Null, or a choice with
        /// nothing opted in, gives exactly <see cref="ApplyFresh(EpisodeState)"/>. The one option today is the
        /// preview chip, <see cref="SeasonBuilder.Choice.CampaignConversations"/>: it starts the season on the
        /// pinned <see cref="DialogueRules.Offered"/>, never the bound <see cref="DialogueRules.Version"/>, and is
        /// written after every rule the pilot's prerequisites read (DialogueRules.Prerequisites); only the alumni
        /// histories follow it. The chip draws nothing, logs nothing and mints no id; in a season that seats an
        /// alumnus the histories after it mint their ids, as they do through <see cref="ApplyFresh(EpisodeState)"/>.
        /// </summary>
        public static void ApplyFresh(EpisodeState fresh, SeasonBuilder.Choice options) => ApplyFresh(fresh, options, alumniHistories: true);

        /// <summary>
        /// The same, with the alumni histories' last step left out when <paramref name="alumniHistories"/> is false:
        /// for the balance lab's paired control only (ALL-STAR-HISTORIES-PLAN §9: a harness may hold a fresh season at
        /// an earlier alumni rules value for its control; shipping never does). Internal, so the director and every
        /// other assembly reach only the shipped form.
        /// </summary>
        internal static void ApplyFresh(EpisodeState fresh, bool alumniHistories) => ApplyFresh(fresh, null, alumniHistories);

        /// <summary>
        /// The one body every form above runs (ALL-STAR-HISTORIES-PLAN 8.5, the merge of the histories with dialogue D1):
        /// the shipped rules, then the cast screen's preview chip, then the alumni histories as the very last step. Two
        /// bodies would let the options form, which the director's StartSeason calls, start a season without its histories
        /// while every test of the one-argument form stayed green. Only the balance lab's control passes false.
        /// </summary>
        internal static void ApplyFresh(EpisodeState fresh, SeasonBuilder.Choice options, bool alumniHistories)
        {
            if (fresh == null) throw new ArgumentNullException(nameof(fresh));
            fresh.competitionRulesVersion = CompetitionRules.Current;
            // B9's balance bands (B9-BALANCE-PLAN §4.3): a plain int, drawing nothing; every save, migration, import and
            // test-built season stays at 0 and plays as it always did.
            fresh.tuningRulesVersion = TuningRules.Shipped;
            fresh.haveNotRulesStartWeek = 1;
            fresh.strategyRulesStartWeek = 1;
            // Every season the director starts plays under the story system from week one:
            // arcs, grudges, lore, bonds and production. Seasons built directly by tests and
            // the default scene engine stay off unless they switch it on themselves.
            EpisodeEngine.EnableStory(fresh);
            EpisodeEngine.EnableRead(fresh);
            EpisodeEngine.EnableLevers(fresh);
            EpisodeEngine.EnableWeek(fresh);
            EpisodeEngine.EnableEconomy(fresh);
            // NPC agency from week one, and with it the house's first impressions of each other
            // and of the player's persona (NPC-AGENCY-PLAN.md §2).
            EpisodeEngine.EnableAgency(fresh);
            // The finale rules (ENDGAME-PLAN §3): history questions, the five responses, the argument.
            EpisodeEngine.EnableFinale(fresh);
            // The commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0, C0): study costs the window's
            // action, a whisper reaches who it is told to, a breach counts against whoever broke it.
            EpisodeEngine.EnableCommitments(fresh);
            // Leaks and double-dealing (WAVE-D-NPC-PACTS-PLAN D4): secret pacts get out on a keyed coin,
            // one pair is one pact, and an ally who finds out about another pact holds it against you.
            EpisodeEngine.EnableAllianceLeaks(fresh);
            // The war rooms (WAVE-D-NPC-PACTS-PLAN D3): a pact of three or more meets once the block is
            // set, its members say who they want out, and the player goes with it, counters once or lies low.
            EpisodeEngine.EnablePactPlans(fresh);
            // The house's turns all week (WAVE-D-NPC-PACTS-PLAN D2): spread over the four windows, fired on the
            // season's own steps, each act in a room the player can walk in on. After the week rules, which it plays in.
            EpisodeEngine.EnableAllWeek(fresh);
            // The unified vote rules (vote family V6, mode 2): canonical Safety and Vote authority and
            // durable hearings from the start, with C0, story knowledge and the house's deal pass
            // already active - every vote promise and deal one canonical row, settled at the reveal
            // under Rule2 and archived with its frame. Do not infer this opt-in while loading,
            // recovering, migrating or importing an existing season: a season recorded in mode 0 or 1
            // keeps its mode (the lead's decision D6).
            fresh.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            fresh.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
            // Dialogue stays off in a shipped season (OWNER-DECISIONS-2026-10-22 §5) unless the player opts this one
            // in on the cast screen. Like every rule here it is never inferred while loading, recovering,
            // migrating or importing: a season keeps the dialogue rules it began on.
            if (options != null && options.CampaignConversations) fresh.dialogueRulesVersion = DialogueRules.Offered;
            // The All-Star histories (ALL-STAR-HISTORIES-PLAN H1b), last: after the first impressions, so a pair's
            // standing is its first impression plus its past game, and after the commitment rules and the unified
            // mode, so the Hitmen's final two is written under the season's own deal rules. After the preview chip
            // too, so the cast screen's every choice starts the same histories (ALL-STAR-HISTORIES-PLAN 8.5). The alumni
            // rules new seasons start from (AlumniRules.Latest: 5, the growing pacts, §14.A, §14.L) only in a season
            // that seats an identity-intact alumnus; every other season stays at 0, as it was.
            if (alumniHistories) EpisodeEngine.EnableAlumniHistories(fresh);
        }

        /// <summary>
        /// Every rule boundary a season carries, by its field's name, in the order the state declares
        /// them: the season's own start weeks and versions, then the NPC world's and the story's. What a
        /// season played under, for a report or a comparison; it reads and changes nothing else.
        /// A test holds this list to every such field the state has, so a rule field added later is
        /// listed here with it.
        /// </summary>
        public static List<KeyValuePair<string, int>> Fields(EpisodeState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return new List<KeyValuePair<string, int>>
            {
                Field("competitionRulesVersion", s.competitionRulesVersion),
                Field("readRulesStartWeek", s.readRulesStartWeek),
                Field("leverRulesStartWeek", s.leverRulesStartWeek),
                Field("weekRulesStartWeek", s.weekRulesStartWeek),
                Field("economyRulesVersion", s.economyRulesVersion),
                Field("agencyRulesStartWeek", s.agencyRulesStartWeek),
                Field("finaleRulesStartWeek", s.finaleRulesStartWeek),
                Field("commitmentRulesStartWeek", s.commitmentRulesStartWeek),
                Field("unifiedCommitmentRulesVersion", s.unifiedCommitmentRulesVersion),
                Field("unifiedHearingRulesVersion", s.unifiedHearingRulesVersion),
                Field("blocRulesStartWeek", s.blocRulesStartWeek),
                Field("socialBudgetRulesStartWeek", s.socialBudgetRulesStartWeek),
                Field("dealRulesStartWeek", s.dealRulesStartWeek),
                Field("eventRulesStartWeek", s.eventRulesStartWeek),
                Field("storyRulesStartWeek", s.storyRulesStartWeek),
                Field("haveNotRulesStartWeek", s.haveNotRulesStartWeek),
                Field("strategyRulesStartWeek", s.strategyRulesStartWeek),
                Field("allianceLeakRulesStartWeek", s.allianceLeakRulesStartWeek),
                Field("pactPlanRulesStartWeek", s.pactPlanRulesStartWeek),
                Field("allWeekRulesStartWeek", s.allWeekRulesStartWeek),
                Field("dialogueRulesVersion", s.dialogueRulesVersion),
                Field("alumniRulesVersion", s.alumniRulesVersion),
                Field("tuningRulesVersion", s.tuningRulesVersion),
                Field("npcSocial.rulesStartWeek", s.npcSocial?.rulesStartWeek ?? 0),
                Field("story.rulesStartWeek", s.story?.rulesStartWeek ?? 0),
                Field("story.rulesVersion", s.story?.rulesVersion ?? 0),
            };
        }

        private static KeyValuePair<string, int> Field(string name, int value) => new KeyValuePair<string, int>(name, value);
    }
}
