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
        public static void ApplyFresh(EpisodeState fresh)
        {
            if (fresh == null) throw new ArgumentNullException(nameof(fresh));
            fresh.competitionRulesVersion = CompetitionRules.Current;
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
            // keeps its mode (the lead's decision D6), and the schema stays 28 (D2).
            fresh.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            fresh.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
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
                Field("npcSocial.rulesStartWeek", s.npcSocial?.rulesStartWeek ?? 0),
                Field("story.rulesStartWeek", s.story?.rulesStartWeek ?? 0),
                Field("story.rulesVersion", s.story?.rulesVersion ?? 0),
            };
        }

        private static KeyValuePair<string, int> Field(string name, int value) => new KeyValuePair<string, int>(name, value);
    }
}
