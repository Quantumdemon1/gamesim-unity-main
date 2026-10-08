#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Tests.EditMode
{
    /// <summary>A player the lab plays a season as. It sees the season only through a <see cref="PlayerView"/>.</summary>
    internal interface IBalancePolicy
    {
        string Name { get; }
        /// <summary>The player's next command, or null to let the phase take its own step (the engine tests' walker).</summary>
        EpisodeCommand Next(PlayerView view);
        /// <summary>The house said no: the screen showed why, and a player does not press the same thing again this week.</summary>
        void Refused(EpisodeCommand command, string reason);
    }

    /// <summary>
    /// An oracle: one of the harnesses' own scripted players, which read the season itself - hidden views,
    /// statistics, NPC-only pacts. Kept, and labelled, as an upper bound on what reading can do: no player
    /// under the knowledge gate can play them (BALANCE plan F2).
    /// </summary>
    internal interface IOraclePolicy
    {
        string Name { get; }
        EpisodeCommand Next(EpisodeState state, uint seed);
    }

    /// <summary>The lab's players (BALANCE plan B.4), by name, in report order.</summary>
    internal static class BalancePolicies
    {
        public const string Passive = "passive", Random = "random", Social = "social", Reader = "reader", Schemer = "schemer",
            Loyalist = "loyalist", Floater = "floater", Beast = "beast", Novice = "novice", Exploit = "exploit",
            OracleReader = "oracle-reader", OracleSkilled = "oracle-skilled";

        /// <summary>The ten knowledge-gated players.</summary>
        public static readonly string[] Gated = { Passive, Random, Social, Reader, Schemer, Loyalist, Floater, Beast, Novice, Exploit };

        /// <summary>The two oracles, which read the season itself.</summary>
        public static readonly string[] Oracles = { OracleReader, OracleSkilled };

        public static readonly string[] All = Gated.Concat(Oracles).ToArray();

        public static bool IsOracle(string name) => Array.IndexOf(Oracles, name) >= 0;

        /// <summary>A fresh player for one season: they keep their own notes from week to week, as a person would.</summary>
        public static object Create(string name)
        {
            switch (name)
            {
                case Passive: return new PassivePolicy();
                case Random: return new RandomPolicy();
                case Social: return new SocialPolicy();
                case Reader: return new ReaderPolicy();
                case Schemer: return new SchemerPolicy();
                case Loyalist: return new LoyalistPolicy();
                case Floater: return new FloaterPolicy();
                case Beast: return new BeastPolicy();
                case Novice: return new NovicePolicy();
                case Exploit: return new ExploitPolicy();
                case OracleReader: return new Oracle(OracleReader, (s, seed) => GameSenseHarnessTests.ReaderNext(s, seed));
                case OracleSkilled: return new Oracle(OracleSkilled, (s, seed) => StorySeasonTests.SkilledNext(s, (int)seed));
                default: throw new ArgumentException("No such policy: " + name, nameof(name));
            }
        }

        private sealed class Oracle : IOraclePolicy
        {
            private readonly Func<EpisodeState, uint, EpisodeCommand> next;
            public string Name { get; }
            public Oracle(string name, Func<EpisodeState, uint, EpisodeCommand> next) { Name = name; this.next = next; }
            public EpisodeCommand Next(EpisodeState state, uint seed) => next(state, seed);
        }
    }

    /// <summary>
    /// What every gated player shares: the week's mandatory decisions with a flavour of their own, answers
    /// to what the house puts to them, and a memory of what the house refused this week.
    /// </summary>
    internal abstract class GatedPolicy : IBalancePolicy
    {
        public abstract string Name { get; }
        private readonly HashSet<string> refused = new HashSet<string>(StringComparer.Ordinal);
        private int week;

        private string Key(EpisodeCommandKind kind, string target, string second) =>
            week.ToString(CultureInfo.InvariantCulture) + "|" + kind + "|" + target + "|" + second;

        public void Refused(EpisodeCommand command, string reason) => refused.Add(Key(command.kind, command.targetId, command.secondTargetId));

        protected bool WasRefused(EpisodeCommandKind kind, string target, string second = null) => refused.Contains(Key(kind, target, second));

        /// <summary>A command, unless the house refused the same one this week.</summary>
        protected EpisodeCommand Make(PlayerView v, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            WasRefused(kind, target, second) ? null : v.Command(kind, target, second, text);

        public EpisodeCommand Next(PlayerView v)
        {
            if (v.Week != week) { week = v.Week; refused.Clear(); }
            if (v.DiaryPending) return null;
            if (v.MustCompete) return Compete(v);
            if (v.MustNominate) return Nominate(v);
            if (v.MustDecideVeto) return Veto(v);
            if (v.MustVote) return Vote(v);
            if (v.MustFinalEvict) return FinalEvict(v);
            if (!v.InTheHouse) return null;
            return Answer(v) ?? Act(v);
        }

        // ---------------------------------------------------------------- the week's decisions, by default the walker's

        /// <summary>Compete at whatever the performance model gives; null takes the walker's, which competes.</summary>
        protected virtual EpisodeCommand Compete(PlayerView v) => null;
        protected virtual EpisodeCommand Nominate(PlayerView v) => null;
        protected virtual EpisodeCommand Veto(PlayerView v) => null;
        protected virtual EpisodeCommand Vote(PlayerView v) => null;
        protected virtual EpisodeCommand FinalEvict(PlayerView v) => null;
        /// <summary>What the house put to the player: beats, offers, cards, oaths. Null leaves them to lapse.</summary>
        protected virtual EpisodeCommand Answer(PlayerView v) => null;
        /// <summary>The player's own move in a window, or null when they are done with it.</summary>
        protected virtual EpisodeCommand Act(PlayerView v) => null;

        // ---------------------------------------------------------------- helpers

        /// <summary>The two candidates the score puts highest, as a nomination.</summary>
        protected EpisodeCommand NominateBy(PlayerView v, Func<string, double> worst)
        {
            var pool = v.NominationCandidates.Where(id => id != v.Me).OrderByDescending(worst).ThenBy(id => id, StringComparer.Ordinal).ToList();
            return pool.Count < 2 ? null : Make(v, EpisodeCommandKind.Nominate, pool[0], pool[1]);
        }

        /// <summary>A ballot against one nominee, never the player.</summary>
        protected EpisodeCommand VoteAgainst(PlayerView v, string target)
        {
            var choices = v.Nominees.Where(id => id != v.Me).ToList();
            if (choices.Count == 0) return null;
            if (target == null || !choices.Contains(target)) target = choices[0];
            return Make(v, EpisodeCommandKind.CastVote, target);
        }

        /// <summary>
        /// The veto meeting: the holder uses it on themselves when nominated, else on <paramref name="saveOther"/>
        /// (null keeps the block); a Head of Household names the replacement an NPC holder's save needs.
        /// </summary>
        protected EpisodeCommand VetoAs(PlayerView v, string saveOther, Func<string, double> replacementWorst)
        {
            var replacements = v.ReplacementCandidates.Where(id => id != v.Me).OrderByDescending(replacementWorst).ThenBy(id => id, StringComparer.Ordinal).ToList();
            if (v.VetoHolder != v.Me)
            {
                string saved = v.VetoSaved;
                if (saved == null || replacements.Count == 0) return null;
                var c = Make(v, EpisodeCommandKind.ResolveVeto, saved, replacements[0]);
                if (c != null) c.useVeto = true;
                return c;
            }
            string save = v.Nominees.Contains(v.Me) ? v.Me : saveOther;
            bool use = save != null && v.Nominees.Contains(save) && replacements.Count > 0 && !v.VetoLockedAtFinalFour;
            var veto = Make(v, EpisodeCommandKind.ResolveVeto, use ? save : v.Nominees.FirstOrDefault(), use ? replacements[0] : null);
            if (veto != null) veto.useVeto = use;
            return veto;
        }

        protected EpisodeCommand FinalEvictBy(PlayerView v, Func<string, double> worst)
        {
            var pick = v.Others.OrderByDescending(worst).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            return pick == null ? null : Make(v, EpisodeCommandKind.FinalEvict, pick);
        }

        /// <summary>A word with somebody in the open window, if the window has a seat and allows it.</summary>
        protected EpisodeCommand Say(PlayerView v, EpisodeCommandKind verb, string target, string about = null, string text = null)
        {
            if (target == null || !v.ActionsLeft || !v.CanConverse(target, verb)) return null;
            return Make(v, verb, target, about, text);
        }

        /// <summary>A story beat answered with the option the score rates highest among those the player can take.</summary>
        protected EpisodeCommand AnswerBeat(PlayerView v, Func<PlayerView.Choice, double> rate, Func<PlayerView.Choice, string> person)
        {
            foreach (var beat in v.Beats)
            {
                var options = beat.choices.Where(c => !c.locked && (!c.costsAction || v.ActionsLeft) && (!c.pickPerson || c.eligibleIds.Count > 0)).ToList();
                if (options.Count == 0) continue;
                var choice = options.OrderByDescending(rate).ThenBy(c => c.optionId, StringComparer.Ordinal).First();
                var command = Make(v, EpisodeCommandKind.ProgressStoryline, beat.id, choice.optionId, choice.pickPerson ? person(choice) : null);
                if (command != null) return command;
            }
            return null;
        }

        protected EpisodeCommand RespondTo(PlayerView v, PlayerView.Offer offer, bool accept) =>
            Make(v, EpisodeCommandKind.RespondToDeal, offer.id, null, accept ? EpisodeEngine.AcceptDeal : "decline");

        protected EpisodeCommand Oath(PlayerView v, string id, bool swear) =>
            v.FreeTime ? Make(v, swear ? EpisodeCommandKind.SwearLoyalty : EpisodeCommandKind.DeclineLoyalty, id) : null;

        protected EpisodeCommand Reply(PlayerView v, PlayerView.Card card, string key) => Make(v, EpisodeCommandKind.ReplyToHouseguest, card.id, null, key);

        /// <summary>The first night: each houseguest met in the approach the player prefers, then the meet-and-greet closed.</summary>
        protected EpisodeCommand MeetTheHouse(PlayerView v, string approach)
        {
            if (!v.FirstNight) return null;
            var next = v.StillToMeet.FirstOrDefault();
            if (next != null) return Make(v, EpisodeCommandKind.Introduce, next, approach);
            return Make(v, EpisodeCommandKind.MarkOpeningBeat, OpeningBeat.MeetAndGreet);
        }

        /// <summary>The houseguest a known estimate rates best for an ask, at or over a chance.</summary>
        protected static string Best(IEnumerable<string> ids, Func<string, double> score, double atLeast = double.MinValue) =>
            ids.Select(id => (id, value: score(id))).Where(x => x.value >= atLeast)
                .OrderByDescending(x => x.value).ThenBy(x => x.id, StringComparer.Ordinal).Select(x => x.id).FirstOrDefault();

        /// <summary>What the player can see of a houseguest as a competitor: their wins.</summary>
        protected static double Record(PlayerView v, string id)
        {
            var g = v.Find(id);
            return g == null ? 0 : g.hohWins * 1.2 + g.vetoWins;
        }
    }

    // -------------------------------------------------------------------- the ten

    /// <summary>Passive: does only what each phase asks, as the engine tests' walker does.</summary>
    internal sealed class PassivePolicy : GatedPolicy
    {
        public override string Name => BalancePolicies.Passive;
    }

    /// <summary>Random: a coin for every choice - who to talk to and how, what to answer, who to put up and vote out.</summary>
    internal sealed class RandomPolicy : GatedPolicy
    {
        private static readonly EpisodeCommandKind[] Verbs =
        {
            EpisodeCommandKind.Talk, EpisodeCommandKind.SmallTalk, EpisodeCommandKind.PersonalChat, EpisodeCommandKind.DiscussGame,
            EpisodeCommandKind.RelationshipBuilding, EpisodeCommandKind.ShareSecret, EpisodeCommandKind.VentAbout,
        };

        public override string Name => BalancePolicies.Random;

        /// <summary>A salt of a name's own, so the coin differs person by person and option by option.</summary>
        private static int Salt(string id, int purpose) => (int)(SeededRandom.HashSeed(id ?? "") % 100000u) * 7 + purpose;

        protected override EpisodeCommand Nominate(PlayerView v) => NominateBy(v, id => v.Coin(Salt(id, 7)));
        protected override EpisodeCommand Vote(PlayerView v) => VoteAgainst(v, v.Pick(v.Nominees.Where(id => id != v.Me).ToList(), 3));
        protected override EpisodeCommand Veto(PlayerView v) => VetoAs(v, v.Coin(4) < 0.5 ? v.Pick(v.Nominees, 5) : null, id => v.Coin(Salt(id, 6)));
        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => v.Coin(Salt(id, 8)));

        protected override EpisodeCommand Answer(PlayerView v)
        {
            if (v.Coin(10) < 0.7)
            {
                var beat = AnswerBeat(v, c => v.Coin(Salt(c.optionId, 11)), c => v.Pick(c.eligibleIds, 12));
                if (beat != null) return beat;
            }
            var offer = v.Offers.FirstOrDefault();
            if (offer != null) return RespondTo(v, offer, v.Coin(13) < 0.5);
            var oath = v.OathOffers.FirstOrDefault();
            if (oath != null && v.FreeTime) return Oath(v, oath, v.Coin(14) < 0.5);
            var card = v.Cards.FirstOrDefault();
            if (card != null && card.replies.Count > 0) return Reply(v, card, v.Pick(card.replies, 15));
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, v.Pick(new[] { WebIntroductions.Warm, WebIntroductions.Calculated, WebIntroductions.Bold }, 16));
            if (!v.ActionsLeft || v.Coin(1) >= 0.6) return null;
            var verb = Verbs[(int)(v.Coin(2) * Verbs.Length) % Verbs.Length];
            string target = v.Pick(v.Others, 17);
            string about = verb == EpisodeCommandKind.VentAbout ? v.Pick(v.Others.Where(id => id != target).ToList(), 18) : null;
            if (verb == EpisodeCommandKind.VentAbout && about == null) verb = EpisodeCommandKind.Talk;
            return Say(v, verb, target, about);
        }
    }

    /// <summary>
    /// Social: every seat spent on warm conversation - mending the coldest view it knows of half the time,
    /// warming the warmest the rest - every offer and oath taken, no pacts or deals of its own; votes with
    /// the house and nominates the two it likes least.
    /// </summary>
    internal sealed class SocialPolicy : GatedPolicy
    {
        private static readonly EpisodeCommandKind[] Warm = { EpisodeCommandKind.PersonalChat, EpisodeCommandKind.RelationshipBuilding, EpisodeCommandKind.SmallTalk };
        public override string Name => BalancePolicies.Social;

        protected override EpisodeCommand Nominate(PlayerView v) => NominateBy(v, id => -v.MyView(id));
        protected override EpisodeCommand Vote(PlayerView v) => VoteAgainst(v, v.VoteReadAvailable ? v.VoteSheet.predictedEvicteeId : null);
        protected override EpisodeCommand Veto(PlayerView v) => VetoAs(v, null, id => -v.MyView(id));
        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => -v.PresumedView(id));

        protected override EpisodeCommand Answer(PlayerView v)
        {
            var beat = AnswerBeat(v, c => (c.lapse ? -100 : 0) + (c.conduct ? -50 : 0) + c.shownChance, c => Best(c.eligibleIds, id => v.MyView(id)));
            if (beat != null) return beat;
            var offer = v.Offers.FirstOrDefault();
            if (offer != null) return RespondTo(v, offer, true);
            var oath = v.OathOffers.FirstOrDefault();
            if (oath != null && v.FreeTime) return Oath(v, oath, true);
            var card = v.Cards.FirstOrDefault();
            if (card != null) return Reply(v, card, card.replies.Contains("apologize") ? "apologize" : card.replies.Contains("promise") ? "promise"
                : card.replies.Contains("promise-safety") ? "promise-safety" : card.replies.Contains("slide") ? "slide" : card.replies.FirstOrDefault());
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Warm);
            if (!v.ActionsLeft) return null;
            string target = v.Coin(1) < 0.5 ? Best(v.Others, id => -v.PresumedView(id)) : Best(v.Others, id => v.PresumedView(id));
            return Say(v, Warm[(int)(v.Coin(2) * Warm.Length) % Warm.Length], target);
        }
    }

    /// <summary>
    /// Reader, under the knowledge gate: the GameSense reader's plan (ask every voter, read every voter, plead
    /// from the block, call the vote through a pact, deal with the torn, vote with the whip count), worked from
    /// the vote read, the shown odds and the player's own view - never how a houseguest privately sees them.
    /// </summary>
    internal sealed class ReaderPolicy : GatedPolicy
    {
        public override string Name => BalancePolicies.Reader;

        protected override EpisodeCommand Nominate(PlayerView v) =>
            NominateBy(v, id => (v.AlliedWith(id) ? -1000 : 0) + Record(v, id) * 10 - v.PresumedView(id));

        protected override EpisodeCommand Vote(PlayerView v)
        {
            string target = v.VoteReadAvailable ? v.VoteSheet.predictedEvicteeId : null;
            if (target == null || target == v.Me || v.AlliedWith(target)) target = v.Nominees.Where(id => id != v.Me).OrderBy(id => v.AlliedWith(id) ? 1 : 0).ThenBy(id => v.PresumedView(id)).FirstOrDefault();
            return VoteAgainst(v, target);
        }

        protected override EpisodeCommand Veto(PlayerView v) =>
            VetoAs(v, v.Nominees.FirstOrDefault(id => v.AlliedWith(id)), id => (v.AlliedWith(id) ? -1000 : 0) - v.PresumedView(id));

        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => Record(v, id) * 10 + v.PresumedView(id));

        protected override EpisodeCommand Answer(PlayerView v)
        {
            var beat = AnswerBeat(v, c => (c.lapse ? -100 : 0) + (c.conduct ? -50 : 0) + c.shownChance, c => Best(c.eligibleIds, id => -v.PresumedView(id)));
            if (beat != null) return beat;
            foreach (var offer in v.Offers)
                return RespondTo(v, offer, v.PresumedView(offer.fromId) >= 0 && offer.aboutId != v.Me);
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Calculated);
            if (v.Phase == EpisodePhase.Campaign && v.VoteReadAvailable)
            {
                var voters = v.Voters.Where(id => id != v.Me).ToList();
                var unasked = voters.FirstOrDefault(id => !v.AskedThisWeek(id) && !WasRefused(EpisodeCommandKind.AskVote, id));
                if (unasked != null) return Make(v, EpisodeCommandKind.AskVote, unasked);
                var unread = voters.FirstOrDefault(id => !v.ReadThisWeek(id) && !WasRefused(EpisodeCommandKind.ReadPerson, id));
                if (unread != null) return Make(v, EpisodeCommandKind.ReadPerson, unread);
                if (!v.ActionsLeft) return null;
                var sheet = v.VoteSheet;
                string wantsOut = v.Nominees.Contains(v.Me) ? v.Nominees.FirstOrDefault(id => id != v.Me) : sheet.predictedEvicteeId ?? v.Nominees.FirstOrDefault(id => id != v.Me && !v.AlliedWith(id));
                if (v.Nominees.Contains(v.Me))
                {
                    foreach (var voter in voters.OrderByDescending(id => v.PresumedView(id)))
                    {
                        var approach = LobbyApproach.All.OrderByDescending(a => v.PleaOdds(voter, LobbyAsk.Vote, v.Me, a).chance).First();
                        if (v.CanLobby(voter, LobbyAsk.Vote, v.Me, out _) && !WasRefused(EpisodeCommandKind.Lobby, voter, v.Me))
                            return Say(v, EpisodeCommandKind.Lobby, voter, v.Me, LobbyAsk.Encode(LobbyAsk.Vote, approach));
                    }
                }
                if (wantsOut != null && wantsOut != v.Me)
                {
                    foreach (var pact in v.MyPacts.Where(p => p.active && !v.CalledThisWeek(p.id)))
                    {
                        var ally = pact.members.Select(m => m.id).FirstOrDefault(id => voters.Contains(id));
                        if (ally != null && !WasRefused(EpisodeCommandKind.CallTheVote, ally, wantsOut)) return Say(v, EpisodeCommandKind.CallTheVote, ally, wantsOut, pact.id);
                    }
                    var torn = voters.FirstOrDefault(id =>
                    {
                        var read = sheet.voters.FirstOrDefault(r => r.voterId == id);
                        return read != null && read.confidence != Gamesim.Simulation.VoteRead.Firm && read.leaningId != wantsOut
                            && v.CanPropose(id, DealKind.VoteEvict, wantsOut, out _) && v.DealOdds(id, DealKind.VoteEvict, wantsOut).chance >= 45
                            && !WasRefused(EpisodeCommandKind.ProposeDeal, id, wantsOut);
                    });
                    if (torn != null) return Say(v, EpisodeCommandKind.ProposeDeal, torn, wantsOut, DealKind.VoteEvict);
                }
                return null;
            }
            if (v.Phase == EpisodePhase.Social && v.ActionsLeft)
            {
                if (!v.MyPacts.Any(p => p.active))
                {
                    string friend = Best(v.Others.Where(id => !WasRefused(EpisodeCommandKind.FormAlliance, id)), id => v.AllianceOdds(id).chance, 55);
                    if (friend != null) return Say(v, EpisodeCommandKind.FormAlliance, friend);
                }
                return Say(v, EpisodeCommandKind.PersonalChat, Best(v.Others, id => -v.PresumedView(id)));
            }
            return null;
        }
    }

    /// <summary>
    /// Schemer: deals with anyone the shown odds favour, buys a conversation a week, whispers against the
    /// biggest visible threat, and keeps no word that is in its way - it votes and nominates for itself.
    /// </summary>
    internal sealed class SchemerPolicy : GatedPolicy
    {
        public override string Name => BalancePolicies.Schemer;

        protected override EpisodeCommand Nominate(PlayerView v) => NominateBy(v, id => Record(v, id) * 10 + (v.AlliedWith(id) ? 5 : 0) - v.MyView(id) * 0.1);
        protected override EpisodeCommand Vote(PlayerView v) => VoteAgainst(v, v.Nominees.Where(id => id != v.Me).OrderByDescending(id => Record(v, id)).ThenBy(id => v.MyView(id)).FirstOrDefault());
        protected override EpisodeCommand Veto(PlayerView v) => VetoAs(v, null, id => Record(v, id) * 10 - v.MyView(id) * 0.1);
        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => Record(v, id) * 10 + v.PresumedView(id));

        protected override EpisodeCommand Answer(PlayerView v)
        {
            var beat = AnswerBeat(v, c => (c.lapse ? -100 : 0) + (c.conduct ? 20 : 0) + c.shownChance, c => Best(c.eligibleIds, id => Record(v, id)));
            if (beat != null) return beat;
            var offer = v.Offers.FirstOrDefault();
            if (offer != null) return RespondTo(v, offer, offer.aboutId != v.Me);
            var card = v.Cards.FirstOrDefault();
            if (card != null) return Reply(v, card, card.replies.Contains("promise") ? "promise" : card.replies.Contains("promise-safety") ? "promise-safety"
                : card.replies.Contains("gossip-back") ? "gossip-back" : card.replies.FirstOrDefault());
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Bold);
            if (!v.InTheHouse) return null;
            if (v.Phase == EpisodePhase.Social && v.CanBuyAction && v.BoughtThisWeek == 0 && !WasRefused(EpisodeCommandKind.BuyActionPoint, null))
                return Make(v, EpisodeCommandKind.BuyActionPoint, Best(v.Others, id => -v.MyView(id)), null, WebSocialVocabulary.BurnOne);
            if (!v.ActionsLeft) return null;
            string threat = Best(v.Others, id => Record(v, id) * 10 - v.MyView(id) * 0.1);
            if (v.Phase == EpisodePhase.Campaign && v.VoteReadAvailable)
            {
                string wantsOut = v.Nominees.Where(id => id != v.Me).OrderByDescending(id => Record(v, id)).FirstOrDefault();
                string partner = wantsOut == null ? null : Best(v.Voters.Where(id => id != wantsOut && v.CanPropose(id, DealKind.VoteEvict, wantsOut, out _)
                    && !WasRefused(EpisodeCommandKind.ProposeDeal, id, wantsOut)), id => v.DealOdds(id, DealKind.VoteEvict, wantsOut).chance, 45);
                if (partner != null) return Say(v, EpisodeCommandKind.ProposeDeal, partner, wantsOut, DealKind.VoteEvict);
            }
            if (v.Phase == EpisodePhase.Social)
            {
                string mark = Best(v.Others.Where(id => id != threat && v.CanPropose(id, DealKind.FinalTwo, null, out _) && !WasRefused(EpisodeCommandKind.ProposeDeal, id)),
                    id => v.DealOdds(id, DealKind.FinalTwo).chance, 55);
                if (mark != null) return Say(v, EpisodeCommandKind.ProposeDeal, mark, null, DealKind.FinalTwo);
                string listener = Best(v.Others.Where(id => id != threat), id => v.PresumedView(id));
                if (threat != null && listener != null && v.Coin(1) < 0.5 && !WasRefused(EpisodeCommandKind.SpreadLie, listener, threat))
                    return Say(v, EpisodeCommandKind.SpreadLie, listener, threat);
                if (threat != null && listener != null && !WasRefused(EpisodeCommandKind.VentAbout, listener, threat))
                    return Say(v, EpisodeCommandKind.VentAbout, listener, threat);
            }
            return Say(v, EpisodeCommandKind.StrategicDiscussion, Best(v.Others, id => -v.PresumedView(id)));
        }
    }

    /// <summary>
    /// Loyalist: one pact early with whoever the shown odds favour, a final two with them when the house is
    /// small, every ally's offer and oath taken, and never a decision that breaks its word (the screen's
    /// breach warning decides between candidates).
    /// </summary>
    internal sealed class LoyalistPolicy : GatedPolicy
    {
        private readonly HashSet<string> promisedFinalTwo = new HashSet<string>(StringComparer.Ordinal);
        public override string Name => BalancePolicies.Loyalist;

        private bool Breaks(PlayerView v, CommitmentsRead.Decision decision) => v.WouldBreak(decision).Count > 0;

        protected override EpisodeCommand Nominate(PlayerView v)
        {
            var pool = v.NominationCandidates.Where(id => id != v.Me).OrderByDescending(id => (v.AlliedWith(id) ? -1000 : 0) - v.MyView(id)).ThenBy(id => id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < pool.Count; i++)
                for (int j = i + 1; j < pool.Count; j++)
                    if (!Breaks(v, CommitmentsRead.Decision.Nominate(pool[i], pool[j]))) return Make(v, EpisodeCommandKind.Nominate, pool[i], pool[j]);
            return NominateBy(v, id => (v.AlliedWith(id) ? -1000 : 0) - v.MyView(id));
        }

        protected override EpisodeCommand Vote(PlayerView v)
        {
            var choices = v.Nominees.Where(id => id != v.Me).OrderBy(id => v.AlliedWith(id) ? 1 : 0).ThenBy(id => v.MyView(id)).ToList();
            var kept = choices.FirstOrDefault(id => !Breaks(v, CommitmentsRead.Decision.Vote(id)));
            return VoteAgainst(v, kept ?? choices.FirstOrDefault());
        }

        protected override EpisodeCommand Veto(PlayerView v) => VetoAs(v, v.Nominees.FirstOrDefault(id => v.AlliedWith(id)), id => (v.AlliedWith(id) ? -1000 : 0) - v.MyView(id));
        protected override EpisodeCommand FinalEvict(PlayerView v)
        {
            var kept = v.Others.Where(id => !Breaks(v, CommitmentsRead.Decision.FinalEviction(id))).OrderBy(id => v.AlliedWith(id) ? 1 : 0).ThenBy(id => v.MyView(id)).FirstOrDefault();
            return kept != null ? Make(v, EpisodeCommandKind.FinalEvict, kept) : FinalEvictBy(v, id => -v.MyView(id));
        }

        protected override EpisodeCommand Answer(PlayerView v)
        {
            var beat = AnswerBeat(v, c => (c.lapse ? -100 : 0) + (c.conduct ? -50 : 0) + c.shownChance, c => Best(c.eligibleIds, id => v.AlliedWith(id) ? 100 : v.MyView(id)));
            if (beat != null) return beat;
            var offer = v.Offers.FirstOrDefault();
            if (offer != null) return RespondTo(v, offer, v.AlliedWith(offer.fromId) && offer.aboutId != v.Me);
            var oath = v.OathOffers.FirstOrDefault();
            if (oath != null && v.FreeTime) return Oath(v, oath, v.AlliedWith(oath) || v.PresumedView(oath) >= 20);
            var card = v.Cards.FirstOrDefault();
            if (card != null) return Reply(v, card, card.replies.Contains("apologize") ? "apologize" : card.replies.Contains("noncommittal") ? "noncommittal"
                : card.replies.Contains("hear") ? "hear" : card.replies.Contains("slide") ? "slide" : card.replies.FirstOrDefault());
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Warm);
            if (!v.ActionsLeft) return null;
            var allies = v.MyPacts.Where(p => p.active).SelectMany(p => p.members).Select(m => m.id).Where(id => id != v.Me && v.Others.Contains(id)).Distinct().ToList();
            if (v.Phase == EpisodePhase.Social && !v.MyPacts.Any(p => p.active))
            {
                string friend = Best(v.Others.Where(id => !WasRefused(EpisodeCommandKind.FormAlliance, id)), id => v.AllianceOdds(id).chance, 45);
                if (friend != null) return Say(v, EpisodeCommandKind.FormAlliance, friend);
            }
            if (v.Phase == EpisodePhase.Social && allies.Count > 0 && v.HouseCount <= 6)
            {
                string partner = allies.FirstOrDefault(id => !promisedFinalTwo.Contains(id) && !WasRefused(EpisodeCommandKind.PromiseFinalTwo, id));
                var promise = partner == null ? null : Say(v, EpisodeCommandKind.PromiseFinalTwo, partner);
                if (promise != null) { promisedFinalTwo.Add(partner); return promise; }
            }
            if (v.Phase == EpisodePhase.Campaign && v.VoteReadAvailable)
            {
                string wantsOut = v.Nominees.Where(id => id != v.Me && !v.AlliedWith(id)).OrderBy(id => v.MyView(id)).FirstOrDefault();
                foreach (var pact in v.MyPacts.Where(p => p.active && !v.CalledThisWeek(p.id)))
                {
                    var ally = pact.members.Select(m => m.id).FirstOrDefault(id => v.Voters.Contains(id));
                    if (ally != null && wantsOut != null && !WasRefused(EpisodeCommandKind.CallTheVote, ally, wantsOut)) return Say(v, EpisodeCommandKind.CallTheVote, ally, wantsOut, pact.id);
                }
            }
            string talkTo = allies.Count > 0 && v.Coin(1) < 0.7 ? allies[(int)(v.Coin(2) * allies.Count) % allies.Count] : Best(v.Others, id => v.PresumedView(id));
            return Say(v, EpisodeCommandKind.RelationshipBuilding, talkTo);
        }
    }

    /// <summary>
    /// Floater: everyone in turn, nobody close - no pacts, no deals, offers and oaths declined; votes with the
    /// house's majority and nominates the strongest records.
    /// </summary>
    internal sealed class FloaterPolicy : GatedPolicy
    {
        private int turn;
        public override string Name => BalancePolicies.Floater;

        protected override EpisodeCommand Nominate(PlayerView v) => NominateBy(v, id => Record(v, id) * 10 - v.MyView(id) * 0.01);
        protected override EpisodeCommand Vote(PlayerView v) => VoteAgainst(v, v.VoteReadAvailable ? v.VoteSheet.predictedEvicteeId : null);
        protected override EpisodeCommand Veto(PlayerView v) => VetoAs(v, null, id => Record(v, id));
        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => Record(v, id) * 10 + v.PresumedView(id));

        protected override EpisodeCommand Answer(PlayerView v)
        {
            var beat = AnswerBeat(v, c => (c.lapse ? 10 : 0) + (c.conduct ? -50 : 0) + c.shownChance * 0.1, c => c.eligibleIds.FirstOrDefault());
            if (beat != null) return beat;
            var offer = v.Offers.FirstOrDefault();
            if (offer != null) return RespondTo(v, offer, false);
            var oath = v.OathOffers.FirstOrDefault();
            if (oath != null && v.FreeTime) return Oath(v, oath, false);
            var card = v.Cards.FirstOrDefault();
            if (card != null) return Reply(v, card, card.replies.Contains("noncommittal") ? "noncommittal" : card.replies.Contains("hear") ? "hear"
                : card.replies.Contains("deflect") ? "deflect" : card.replies.Contains("slide") ? "slide" : card.replies.FirstOrDefault());
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Warm);
            if (!v.ActionsLeft || v.Others.Count == 0) return null;
            var everyone = v.Others.OrderBy(id => id, StringComparer.Ordinal).ToList();
            return Say(v, EpisodeCommandKind.SmallTalk, everyone[turn++ % everyone.Count]);
        }
    }

    /// <summary>
    /// Competition beast: plays every competition hard (its performance runs high), studies the house in free
    /// time until its preparation is full, uses the veto on itself, and nominates the strongest records.
    /// </summary>
    internal sealed class BeastPolicy : GatedPolicy
    {
        public override string Name => BalancePolicies.Beast;

        protected override EpisodeCommand Nominate(PlayerView v) => NominateBy(v, id => Record(v, id) * 10 - v.MyView(id) * 0.01);
        protected override EpisodeCommand Vote(PlayerView v) => VoteAgainst(v, v.Nominees.Where(id => id != v.Me).OrderByDescending(id => Record(v, id)).FirstOrDefault());
        protected override EpisodeCommand Veto(PlayerView v) => VetoAs(v, null, id => Record(v, id));
        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => Record(v, id));

        protected override EpisodeCommand Answer(PlayerView v) =>
            AnswerBeat(v, c => (c.lapse ? -100 : 0) + (c.conduct ? -50 : 0) + c.shownChance, c => Best(c.eligibleIds, id => -Record(v, id)));

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Bold);
            if (!v.ActionsLeft) return null;
            // Study until the preparation is full (five), which it then keeps all season.
            if (v.Phase == EpisodePhase.Social && v.Preparation < 5 && !WasRefused(EpisodeCommandKind.StudyHouse, "memorize-layout"))
                return Make(v, EpisodeCommandKind.StudyHouse, "memorize-layout");
            return Say(v, EpisodeCommandKind.Talk, Best(v.Others, id => v.PresumedView(id)));
        }
    }

    /// <summary>
    /// Novice, a first-timer: spends about half its seats on plain talk with whoever it likes, answers what
    /// the house asks with the first thing offered, takes every offer, swears every oath, reads nothing and
    /// pulls no lever; votes out and nominates whoever it likes least. Its performance runs low.
    /// </summary>
    internal sealed class NovicePolicy : GatedPolicy
    {
        public override string Name => BalancePolicies.Novice;

        protected override EpisodeCommand Nominate(PlayerView v) => NominateBy(v, id => -v.MyView(id));
        protected override EpisodeCommand Vote(PlayerView v) => VoteAgainst(v, v.Nominees.Where(id => id != v.Me).OrderBy(id => v.MyView(id)).FirstOrDefault());
        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => -v.MyView(id));

        protected override EpisodeCommand Answer(PlayerView v)
        {
            var beat = AnswerBeat(v, c => c.lapse ? -1 : 0, c => c.eligibleIds.FirstOrDefault());
            if (beat != null) return beat;
            var offer = v.Offers.FirstOrDefault();
            if (offer != null) return RespondTo(v, offer, true);
            var oath = v.OathOffers.FirstOrDefault();
            if (oath != null && v.FreeTime) return Oath(v, oath, true);
            var card = v.Cards.FirstOrDefault();
            if (card != null && card.replies.Count > 0) return Reply(v, card, card.replies[0]);
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Warm);
            if (!v.ActionsLeft || v.Coin(1) >= 0.5) return null;
            return Say(v, v.Coin(2) < 0.5 ? EpisodeCommandKind.Talk : EpisodeCommandKind.SmallTalk, Best(v.Others, id => v.MyView(id)));
        }
    }

    /// <summary>
    /// Exploit hunter: greedy on everything that costs nothing and comes round again - every voter asked and
    /// read each week, every walk-in, every reply and oath, every pact renamed, time bought to the ceiling at
    /// the cheaper price - and every seat on the warmest verb. What it gets away with is the measurement.
    /// </summary>
    internal sealed class ExploitPolicy : GatedPolicy
    {
        public override string Name => BalancePolicies.Exploit;

        protected override EpisodeCommand Nominate(PlayerView v) => NominateBy(v, id => (v.AlliedWith(id) ? -1000 : 0) + Record(v, id) * 10 - v.PresumedView(id));
        protected override EpisodeCommand Vote(PlayerView v) => VoteAgainst(v, v.VoteReadAvailable ? v.VoteSheet.predictedEvicteeId : null);
        protected override EpisodeCommand Veto(PlayerView v) => VetoAs(v, v.Nominees.FirstOrDefault(id => v.AlliedWith(id)), id => -v.PresumedView(id));
        protected override EpisodeCommand FinalEvict(PlayerView v) => FinalEvictBy(v, id => Record(v, id) * 10 + v.PresumedView(id));

        protected override EpisodeCommand Answer(PlayerView v)
        {
            var beat = AnswerBeat(v, c => (c.lapse ? -100 : 0) + c.shownChance + (c.costsAction ? -30 : 0), c => Best(c.eligibleIds, id => v.MyView(id)));
            if (beat != null) return beat;
            var offer = v.Offers.FirstOrDefault();
            if (offer != null) return RespondTo(v, offer, offer.aboutId != v.Me);
            var oath = v.OathOffers.FirstOrDefault(id => !WasRefused(EpisodeCommandKind.SwearLoyalty, id));
            if (oath != null && v.FreeTime) return Oath(v, oath, true);
            var card = v.Cards.FirstOrDefault();
            if (card != null) return Reply(v, card, card.replies.Contains("apologize") ? "apologize" : card.replies.Contains("promise") ? "promise"
                : card.replies.Contains("promise-safety") ? "promise-safety" : card.replies.Contains("slide") ? "slide" : card.replies.FirstOrDefault());
            return null;
        }

        protected override EpisodeCommand Act(PlayerView v)
        {
            if (v.FirstNight) return MeetTheHouse(v, WebIntroductions.Warm);
            if (!v.InTheHouse) return null;
            if (v.Phase == EpisodePhase.Campaign && v.VoteReadAvailable)
            {
                var voters = v.Voters.Where(id => id != v.Me).ToList();
                var unasked = voters.FirstOrDefault(id => !v.AskedThisWeek(id) && !WasRefused(EpisodeCommandKind.AskVote, id));
                if (unasked != null) return Make(v, EpisodeCommandKind.AskVote, unasked);
            }
            if (v.FreeTime)
            {
                var unread = v.Others.FirstOrDefault(id => !v.ReadThisWeek(id) && !WasRefused(EpisodeCommandKind.ReadPerson, id));
                if (unread != null) return Make(v, EpisodeCommandKind.ReadPerson, unread);
            }
            if (v.Phase == EpisodePhase.Social)
            {
                var others = v.Others.OrderBy(id => id, StringComparer.Ordinal).ToList();
                foreach (var a in others)
                    foreach (var b in others.Where(x => string.CompareOrdinal(a, x) < 0))
                        if (v.ProximityOpen(a, b) && !WasRefused(EpisodeCommandKind.WitnessProximity, a, b))
                            return Make(v, EpisodeCommandKind.WitnessProximity, a, b, "the kitchen");
                foreach (var pact in v.MyPacts.Where(p => p.active))
                {
                    var member = pact.members.Select(m => m.id).FirstOrDefault(id => v.Others.Contains(id));
                    var name = v.PactNamesOnOffer(pact.id).FirstOrDefault(n => n != pact.name);
                    if (member != null && name != null && !WasRefused(EpisodeCommandKind.RenameAlliance, member, pact.id))
                        return Make(v, EpisodeCommandKind.RenameAlliance, member, pact.id, name);
                }
            }
            if (v.FreeTime && v.CanBuyAction && !WasRefused(EpisodeCommandKind.BuyActionPoint, null))
            {
                // The cheaper price: eight with one houseguest, or three with each of the others.
                string price = 3 * Math.Max(0, v.HouseCount - 1) < 8 ? WebSocialVocabulary.SpreadAll : WebSocialVocabulary.BurnOne;
                return Make(v, EpisodeCommandKind.BuyActionPoint, price == WebSocialVocabulary.BurnOne ? Best(v.Others, id => -v.MyView(id)) : null, null, price);
            }
            if (!v.ActionsLeft) return null;
            return Say(v, EpisodeCommandKind.SmallTalk, Best(v.Others, id => -v.PresumedView(id)));
        }
    }
}
#endif
