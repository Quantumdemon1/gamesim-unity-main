using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Where story state bites: the NPC Head of Household's nominations, the veto holder's save,
    /// the eviction vote and the jury.
    ///
    /// <para><b>Every term is labelled native and reproduces today's output on an empty story
    /// state.</b> With the story system off, or on with nothing written yet, every function here
    /// returns exactly the number it replaced - the parity fixtures and every recorded season stay
    /// byte-identical. Each term is gated by the milestone that introduced it.</para>
    /// </summary>
    public static class StoryConsumers
    {
        /// <summary>
        /// How much an NPC Head of Household wants to keep somebody off the block: the lowest are
        /// nominated. Today's whole sort is the relationship; the story adds the grudge (−0.3 of it),
        /// "their word" (+30 for an active safety promise or safety pact the HoH made them), a
        /// nemesis (−20), a partner (+50), and splitting a known couple (−15).
        /// </summary>
        public static double NominationPreference(EpisodeState s, string hohId, string candidateId)
        {
            double value = s.Score(hohId, candidateId);
            if (!EpisodeEngine.StoryOn(s)) return value;
            int v = s.story.rulesVersion;
            if (v >= StoryRules.Grudges)
            {
                value -= 0.3 * Grudges.Severity(s, hohId, candidateId);
                // From the strategy windows a safety agreement is weighed with the other deals
                // (StrategyRules.NominationReluctance); only a promise is the story's to count then.
                if (StrategyRules.Apply(s) ? PromisedSafety(s, hohId, candidateId) : TheirWord(s, hohId, candidateId)) value += 30;
            }
            if (v >= StoryRules.Bonds)
            {
                if (Bonds.Nemesis(s, hohId, candidateId)) value -= 20;
                if (Bonds.Partner(s, hohId, candidateId)) value += 50;
                string partner = Bonds.ShowmancePartner(s, candidateId);
                if (partner != null && partner != hohId && !Bonds.Partner(s, hohId, partner)
                    && Knowledge.Of(s, FactKinds.Couple, CoupleRef(s, candidateId, partner)) is HouseFactState couple
                    && Knowledge.Knows(couple, hohId))
                    value -= 15;
            }
            return value;
        }

        /// <summary>
        /// Whether a Head of Household gave somebody their word: an active safety promise, or an
        /// active safety pact between them. NPC HoHs honour it here rather than ignoring it until the
        /// nomination breaks it - a real behaviour change, measured by the season sweep.
        /// </summary>
        /// <summary>Whether the Head of Household has promised this houseguest safety, and the promise stands.</summary>
        public static bool PromisedSafety(EpisodeState s, string hohId, string candidateId) =>
            s.promises.Any(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.Safety && p.fromId == hohId && p.toId == candidateId);

        public static bool TheirWord(EpisodeState s, string hohId, string candidateId) =>
            s.promises.Any(p => p.status == PromiseStatus.Active && p.kind == PromiseKind.Safety && p.fromId == hohId && p.toId == candidateId)
            || s.deals.Any(d => d.status == DealStatus.Active && d.type == DealKind.SafetyAgreement
                                && ((d.proposerId == hohId && d.recipientId == candidateId) || (d.proposerId == candidateId && d.recipientId == hohId)));

        /// <summary>
        /// The veto holder's save order: their score, less 0.3 of a grudge, +30 for an active veto
        /// commitment between them, +25 for a partner. Never somebody they hold sixty against, or a
        /// nemesis. The existing rules around it stay: a locked Final Four veto saves nobody, a
        /// nominee holding it saves themselves, and a save needs more than thirty.
        /// </summary>
        public static double SavePreference(EpisodeState s, string holderId, string candidateId)
        {
            double value = s.Score(holderId, candidateId);
            if (!EpisodeEngine.StoryOn(s)) return value;
            int v = s.story.rulesVersion;
            if (v >= StoryRules.Grudges)
            {
                value -= 0.3 * Grudges.Severity(s, holderId, candidateId);
                // A veto commitment: the strategy windows weigh it themselves (StrategyRules.VetoWillingness).
                if (!StrategyRules.Apply(s) && s.deals.Any(d => d.status == DealStatus.Active && d.type == DealKind.VetoUse
                                     && ((d.proposerId == holderId && d.recipientId == candidateId) || (d.proposerId == candidateId && d.recipientId == holderId))))
                    value += 30;
            }
            if (v >= StoryRules.Bonds && Bonds.Partner(s, holderId, candidateId)) value += 25;
            return value;
        }

        /// <summary>Whether the veto holder refuses outright to save somebody: a grudge of sixty or a nemesis.</summary>
        public static bool WillNotSave(EpisodeState s, string holderId, string candidateId) =>
            EpisodeEngine.StoryAt(s, StoryRules.Grudges)
            && (Grudges.Severity(s, holderId, candidateId) >= 60 || Bonds.Nemesis(s, holderId, candidateId));

        /// <summary>The eviction vote's grudge factor: −0.25 of the voter's grudge against the nominee, down to −25.</summary>
        public static double VoteGrudge(EpisodeState s, string voterId, string nomineeId) =>
            EpisodeEngine.StoryAt(s, StoryRules.Grudges) ? -Math.Min(25, 0.25 * Grudges.Severity(s, voterId, nomineeId)) : 0;

        /// <summary>The eviction vote's bond factor: a partner or ride-or-die +25, a nemesis −30.</summary>
        public static double VoteBond(EpisodeState s, string voterId, string nomineeId)
        {
            if (!EpisodeEngine.StoryAt(s, StoryRules.Bonds)) return 0;
            if (Bonds.Partner(s, voterId, nomineeId)) return 25;
            if (Bonds.Nemesis(s, voterId, nomineeId)) return -30;
            return 0;
        }

        /// <summary>
        /// The native jury term: a bitter juror is not noise. −0.15 of a grudge of thirty or more (a
        /// nominated juror's seventy is −10.5, the size of a full final impression), +8 for a kept
        /// showmance or ride-or-die, −12 for a betrayed one, −10 for a nemesis. Clamped to −15..+12.
        /// Zero on an empty story state, so the jury parity fixtures hold; the web's four terms,
        /// <see cref="WebJuryVoting.Obligations"/> included, are untouched.
        /// </summary>
        public static double JuryStory(EpisodeState s, string jurorId, string finalistId)
        {
            if (!EpisodeEngine.StoryOn(s)) return 0;
            double value = 0;
            if (s.story.rulesVersion >= StoryRules.Grudges)
            {
                double grudge = Grudges.Severity(s, jurorId, finalistId);
                if (grudge >= 30) value -= 0.15 * grudge;
            }
            if (s.story.rulesVersion >= StoryRules.Bonds)
            {
                foreach (var kind in new[] { BondKinds.Showmance, BondKinds.RideOrDie })
                {
                    if (Bonds.Holds(s, jurorId, finalistId, kind)) value += 8;
                    if (Bonds.Betrayed(s, jurorId, finalistId, kind)) value -= 12;
                }
                if (Bonds.Nemesis(s, jurorId, finalistId)) value -= 10;
            }
            return Math.Max(-15, Math.Min(12, value));
        }

        /// <summary>The couple fact's reference: the showmance bond's id.</summary>
        public static string CoupleRef(EpisodeState s, string a, string b) =>
            s.story?.bonds.FirstOrDefault(x => x.kind == BondKinds.Showmance && BondStatus.Holds(x.status)
                                               && ((x.aId == a && x.bId == b) || (x.aId == b && x.bId == a)))?.id;
    }
}
