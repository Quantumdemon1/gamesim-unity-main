using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Allies share intel (ACTIONS-DEALS-ALLIANCES-PLAN C6), under the commitment rules
    /// (<see cref="CommitmentRulesOn"/>): a season without them asks, reads and meets exactly as it
    /// did, roll for roll and line for line.
    ///
    /// <para><b>Who is an ally.</b> A houseguest in a pact with the player that holds from their own
    /// side as far as the player can know (<see cref="SharesIntel"/>): both still in the house, no
    /// betrayal of theirs the player can know standing against it (<see cref="Allegiance.KnownBetrayed"/>),
    /// their own view of the player not under <see cref="Allegiance.QuietLine"/>. A betrayal told only
    /// by a ballot the player cannot place changes nothing here - the betrayer goes on answering as an
    /// ally, their true ballot still - or a question would tell the player of a ballot they were never
    /// told. A member gone cold, or whose betrayal the player knows of, answers as anybody does, and a
    /// member who will not say where their vote is has told the player where they stand
    /// (<see cref="Allegiance.CommitmentKnown"/>): the notes then say which, "gone quiet" for a member
    /// gone cold and the betrayal for one who turned. C3's refused call reads the true state as it
    /// did: following is a roll (<see cref="WebVotingBlocs.Complies"/>), so a refusal tells nothing
    /// for certain.</para>
    ///
    /// <para><b>"Where's your head at?"</b> An ally answers straight (<see cref="SharesIntel"/>): never a
    /// deflection, their ballot as it stands (<see cref="ProjectBallot"/>), told to the player's face
    /// (<see cref="ClaimSource.Told"/>) and the week's ask, as every answer is. Nothing is left to
    /// chance, so nothing is drawn: under the rules asking an ally takes no draw from the season's
    /// stream, as a deflection never did. With an information deal (C1) the two compose by C1's own
    /// rule - the partner's word is as good as it is to anybody who asks - so an ally's reading is the
    /// truth too (<see cref="AnswerHonesty"/>); the deal's keyed coin is still the one drawn, and a
    /// partner who is no ally keeps C1's odds.</para>
    ///
    /// <para><b>The meeting.</b> "Go over the plan in private" (<see cref="EpisodeCommandKind.AllianceMeet"/>)
    /// was a word with one ally, offered in the backyard. Under the rules it is a meeting of a pact the
    /// player is in, held through whichever member the player is talking to, in any of the house's
    /// private rooms (<see cref="PrivateRooms"/>: the house offers it, as every room act is offered
    /// only where it can be done). Holding it calls the pact together, as the house's own meetings do
    /// (<see cref="NpcSocialActions"/>' alliance upkeep, the web's holdAllianceMeeting): every member
    /// still in the house is at it. Every pair of them, the player's included, is worth
    /// <see cref="AllianceMeetingWarmth"/>: the player's own pairs through the engine's path, a roll
    /// each, as any conversation of theirs; the houseguests' pairs as the house moves them, symmetric
    /// and without a roll or an arc, on the record under a type of the meeting's own
    /// (<see cref="PactMeetingType"/>). In a vote week,
    /// while two nominees stand (<see cref="VoteRead.Available"/>), one member says where their vote is
    /// going: one <see cref="ClaimSource.Ally"/> claim, their ballot as it stands after the meeting,
    /// no roll, judged at the reveal like every other claim (<see cref="MeetingSpeaker"/> says who).
    /// Outside a vote week the meeting is the warmth alone. A pact meets once a week
    /// (<see cref="MetThisWeek"/>), and a meeting is a social action like any room act.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// What a meeting is worth between every pair at it: the house's own alliance meeting's
        /// (<see cref="NpcSocialActions.AllianceMeetingImpact"/>), the web's holdAllianceMeeting.
        /// </summary>
        public const double AllianceMeetingWarmth = NpcSocialActions.AllianceMeetingImpact;

        /// <summary>
        /// The record a meeting of the player's pact leaves between two of its houseguests. It fades as
        /// the house's own <c>alliance-meeting</c> record does (<see cref="RelationshipLedger.Decays"/>),
        /// under a type of its own: what reads the house's private meetings - the Spy Screen casts from
        /// them - must not show the player two of their own pact-mates meeting behind their back.
        /// </summary>
        public const string PactMeetingType = "pact-meeting";

        /// <summary>
        /// The rooms where nobody listens, by the house's room ids (<see cref="RoomWords.Rooms"/>), read
        /// from the room acts: the bedroom, the backyard - the yard, where the plan was always gone over
        /// "where nobody listens" - the Head of Household's suite and the game room, each a room whose
        /// act is between two people and has no audience. Not the living room or the kitchen, the
        /// house's open rooms, whose acts are watched by everybody in either
        /// (<see cref="EpisodeCommandKind.PublicDefense"/>'s witnesses, <see cref="EpisodeCommandKind.Cook"/>'s
        /// table); nor the nomination room, where the house gathers for its ceremonies; nor the private
        /// room, which is the diary room's, a confessional for one.
        /// </summary>
        public static readonly string[] PrivateRooms = { "Bedroom", "Yard", "HoH", "Games" };

        /// <summary>Whether a room of the house is one where nobody listens (<see cref="PrivateRooms"/>).</summary>
        public static bool IsPrivateRoom(string roomId) => roomId != null && Array.IndexOf(PrivateRooms, roomId) >= 0;

        /// <summary>
        /// Whether this houseguest answers the player as an ally (C6): under the commitment rules, in a
        /// pact with the player that holds from their own side as far as the player can know -
        /// <see cref="Allegiance.Holds"/> on the season as the player knows it
        /// (<see cref="Allegiance.AsThePlayerKnows"/>), without the copy: both in the house, no betrayal
        /// of theirs the player can know standing against it, their own view of the player not under
        /// <see cref="Allegiance.QuietLine"/>. A betrayal told only by a ballot the player cannot place
        /// does not count here, or who answers straight would tell it; what they answer is still their
        /// true ballot. Never without the rules, never the player.
        /// </summary>
        public static bool SharesIntel(EpisodeState s, string npcId)
        {
            if (!CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId) || npcId == s.playerId) return false;
            var npc = s.Find(npcId);
            return npc != null && !npc.isPlayer && s.Allied(npcId, s.playerId)
                   && !Allegiance.KnownBetrayed(s, npcId) && s.Score(npcId, s.playerId) >= Allegiance.QuietLine;
        }

        /// <summary>
        /// How likely a voter is to tell the player the truth about their vote: an ally always
        /// (<see cref="SharesIntel"/>, C6), anybody else by <see cref="VoteHonesty"/>. What an
        /// information partner's reading is drawn on (<see cref="PassTheReadings"/>, C1).
        /// </summary>
        public static double AnswerHonesty(EpisodeState s, ContestantState voter) =>
            SharesIntel(s, voter.id) ? 1 : VoteHonesty(voter, s.Score(voter.id, s.playerId));

        // ---------------------------------------------------------------- the meeting

        /// <summary>The story cooldown that says a pact has met this week: the week and the pact.</summary>
        public static string MeetingKey(EpisodeState s, string pactId) => "meeting:" + s.week + ":" + pactId;

        /// <summary>Whether this pact has met this week (once a week, C6).</summary>
        public static bool MetThisWeek(EpisodeState s, AllianceState pact) =>
            s?.story != null && pact != null && Cooling(s, MeetingKey(s, pact.id));

        /// <summary>
        /// The pact a meeting held through this houseguest is for, as the house offers it: under the
        /// commitment rules, the first standing pact of the player's they are in (the oldest, in the
        /// season's own order) that has not met this week. Null when there is none, or without the
        /// rules, where the meeting is the old word with one ally. Under the war rooms
        /// (<see cref="PactPlanRulesOn"/>) a pact of three or more is offered only once the block is set,
        /// and not again the week it has a plan (WAVE-D-NPC-PACTS-PLAN D3-M1).
        /// </summary>
        public static AllianceState MeetingPact(EpisodeState s, string npcId)
        {
            if (!CommitmentRulesOn(s) || s.story == null || string.IsNullOrEmpty(npcId) || !s.Allied(s.playerId, npcId)) return null;
            bool warRooms = PactPlanRulesOn(s);
            return s.alliances.FirstOrDefault(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(npcId) && !MetThisWeek(s, a)
                && !(warRooms && PactPlans.IsWarRoomPact(s, a) && (!PactPlans.BlockSet(s) || PactPlans.ThisWeek(s, a.id) != null)));
        }

        /// <summary>Who is at a meeting of this pact besides the player: every member still in the house, in the pact's own order.</summary>
        public static List<string> AtTheMeeting(EpisodeState s, AllianceState pact) =>
            pact?.members == null ? new List<string>()
                : pact.members.Where(id => id != s.playerId && Allegiance.InHouse(s, id)).Distinct().ToList();

        /// <summary>
        /// Whether a meeting of this pact could tell the player a vote, by what the player can see: a
        /// vote week, and somebody at it who casts a ballot. Whether they will say is theirs: only an
        /// ally does (<see cref="MeetingSpeaker"/>). For the row's pill; it reads nothing hidden.
        /// </summary>
        public static bool MeetingCouldTellAVote(EpisodeState s, AllianceState pact)
        {
            if (!VoteRead.Available(s) || pact == null) return false;
            var at = AtTheMeeting(s, pact);
            return Voters(s).Any(v => !v.isPlayer && at.Contains(v.id));
        }

        /// <summary>
        /// Whose vote a meeting tells, with no roll: in a vote week, of the members at it who cast a
        /// ballot and answer as allies (<see cref="SharesIntel"/>), the first whose vote the player has
        /// not heard this week (no claim from them yet, however heard) - the one the meeting was held
        /// through first, then the pact's own order. Where the player has heard every one of them, the
        /// first again. Null outside a vote week, or with no ally at it who votes: a member gone cold, or
        /// whose betrayal the player knows of, says nothing, as they would say nothing straight to anybody.
        /// </summary>
        public static string MeetingSpeaker(EpisodeState s, AllianceState pact, string throughId)
        {
            if (!VoteRead.Available(s) || pact == null) return null;
            var at = AtTheMeeting(s, pact);
            var voters = new HashSet<string>(Voters(s).Where(v => !v.isPlayer).Select(v => v.id));
            var order = new List<string>();
            if (throughId != null && at.Contains(throughId)) order.Add(throughId);
            order.AddRange(at.Where(id => id != throughId));
            var speakers = order.Where(id => voters.Contains(id) && SharesIntel(s, id)).ToList();
            if (speakers.Count == 0) return null;
            return speakers.FirstOrDefault(id => !s.ledger.claims.Any(k => k != null && k.week == s.week && k.voterId == id)) ?? speakers[0];
        }

        /// <summary>
        /// The meeting's line: "The Riley Pact met where nobody listens: you, Riley Chen and Sam Ortiz
        /// went over the plan." and, in a vote week, " Riley Chen told the pact they're voting to evict
        /// Maya Hassan." What was said, and never a number.
        /// </summary>
        public static string MeetingLine(EpisodeState s, AllianceState pact, IEnumerable<string> at, string speakerId, string statedId)
        {
            string line = pact.name + " met where nobody listens: " + Allegiance.Join(new[] { "you" }.Concat(at.Select(id => Name(s, id))))
                + " went over the plan.";
            if (speakerId != null && statedId != null)
                line += " " + Name(s, speakerId) + " told the pact they're voting to evict " + Target(s, statedId, speakerId) + ".";
            return line;
        }

        /// <summary>
        /// What each member remembers of a meeting. Nameless, as a nomination's memory is: the eviction
        /// vote's memory term matches a memory to a nominee by name, and a meeting is no word about one.
        /// </summary>
        public static string MeetingMemory(int week) => "We went over the plan in private in week " + week + ".";

        /// <summary>
        /// A meeting of a pact (C6), after <see cref="Social"/> has checked the phase, the person and the
        /// budget, and the room act that the person is in a pact with the player. The pact is the one the
        /// command names by its id (<c>text</c>, as a call names its pact), or the first the player shares
        /// with them that has not met this week (<see cref="MeetingPact"/>).
        /// </summary>
        private static void HoldAllianceMeeting(EpisodeState s, ContestantState through, EpisodeCommand c)
        {
            string named = (c.text ?? string.Empty).Trim();
            var pact = named.Length > 0
                ? s.alliances.FirstOrDefault(a => a.id == named)
                : MeetingPact(s, through.id)
                  ?? s.alliances.FirstOrDefault(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(through.id));
            Require(pact != null && pact.active && pact.members.Contains(s.playerId) && pact.members.Contains(through.id),
                "Hold the meeting through somebody in that alliance.");
            // Under the war rooms a pact of three or more meets once the block is set, once a week (D3-M1).
            RequireWarRoomTiming(s, pact);
            Require(!MetThisWeek(s, pact), pact.name + " has already met this week.");
            // Whether this is a war room, by what the player can see before it starts; whether anybody at it
            // has a say is read after it, from the state the meeting leaves.
            bool warRoom = PactPlans.CouldConvene(s, pact);
            var at = AtTheMeeting(s, pact);

            // Every pair at it: the player's own through the engine's path, a roll each, as any of their
            // conversations; the houseguests' as the house moves its own (Arcs belong to the player).
            foreach (var id in at) Change(s, s.playerId, id, AllianceMeetingWarmth);
            string record = pact.name + " met in week " + s.week;
            for (int i = 0; i < at.Count; i++)
                for (int j = i + 1; j < at.Count; j++)
                {
                    RelationshipLedger.Move(s, at[i], at[j], AllianceMeetingWarmth);
                    RelationshipLedger.Record(s, at[i], at[j], PactMeetingType, AllianceMeetingWarmth, record);
                }
            foreach (var id in at) Remember(s, id, s.playerId, MeetingMemory(s.week), true);

            // The war room (WAVE-D-NPC-PACTS-PLAN §3): the says and an open plan in place of one ally's claim.
            if (warRoom && HoldWarRoom(s, pact, through, at)) return;

            // In a vote week one member says where their vote is going: their ballot as it now stands,
            // decided, so nothing is drawn. Judged at the reveal like every claim (SettleVoteRead).
            string speaker = MeetingSpeaker(s, pact, through.id);
            string stated = speaker == null ? null : ProjectBallot(s, speaker).selectedNomineeId;
            if (string.IsNullOrEmpty(stated)) speaker = stated = null;
            else SeasonLedger.Append(s.ledger, s.ledger.claims, new ClaimRow { week = s.week, voterId = speaker, targetId = stated, source = ClaimSource.Ally });

            Cool(s, MeetingKey(s, pact.id), s.week + 1);
            Log(s, "conversation", MeetingLine(s, pact, at, speaker, stated), new[] { s.playerId }.Concat(at).ToArray());
        }
    }
}
