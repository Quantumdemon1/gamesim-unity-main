using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// What a houseguest's profile gathers from the places the game already shows it (menu slice 3,
    /// the lead's ruling N6 i and iv). A person's details were split over the notes, the vote, the
    /// alliances and the house's map; the profile now holds them, by calling the readers and the
    /// words those pages use, so it can never say what they would not. The player's own "You" card
    /// is gathered the same way, from what the HUD and the notebook already tell the player about
    /// themselves. Nothing here computes anything new about anyone.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The profile's block headings, each its block's card name after <see cref="EpisodeHud.ProfileBlockPrefix"/>.</summary>
        public const string ProfileNotesHeading = "From your notes", ProfileVotesHeading = "Their ballots you know",
            ProfileHeardAlliancesHeading = "Alliances you have heard of", YourWeekHeading = "This week",
            YourAlliancesBlockHeading = "Your alliances";

        /// <summary>The "You" card's title in the notebook's head.</summary>
        public const string YourCardTitle = "Your card";

        /// <summary>How many lines of a source a profile block shows before it counts the rest.</summary>
        public const int ProfileLinesShown = 6;

        /// <summary>
        /// What the notes page has on them (<see cref="HouseguestNotes.For"/>), each line as the notes card
        /// prints it, newest first - less the promises between you and your memories of them, which the
        /// profile's "Other records" and "What you remember" already say. The alliance-call notes stay,
        /// each once (<see cref="ProfileNotes"/>).
        /// </summary>
        public static List<string> ProfileNotesLines(EpisodeState state, string id, out int more)
        {
            var notes = ProfileNotes.For(state, id).Select(n => n.ToString()).ToList();
            more = System.Math.Max(0, notes.Count - ProfileLinesShown);
            return notes.Take(ProfileLinesShown).ToList();
        }

        /// <summary>
        /// Their ballots the vote page shows you (<see cref="VoteRecords.Read"/>): only those you know, each
        /// in the page's words with how you know it. A ballot you have not learned is not here, as it is
        /// not there - not even as a count.
        /// </summary>
        public static List<string> ProfileVoteLines(EpisodeState state, string id)
        {
            var lines = new List<string>();
            if (state == null || string.IsNullOrEmpty(id)) return lines;
            foreach (var record in VoteRecords.Read(state).Records)
                foreach (var ballot in record.Ballots.Where(b => b.Known && b.VoterId == id))
                {
                    string tag = BallotTag(ballot);
                    lines.Add("Week " + record.Week + " · " + BallotWords(ballot) + (string.IsNullOrEmpty(tag) ? "" : " · " + tag));
                }
            return lines;
        }

        /// <summary>
        /// The pacts between others that the alliances page shows you and that they are in
        /// (<see cref="AllianceRead.Read"/>): known or suspected, and with whom - only on word that reached you.
        /// </summary>
        public static List<string> ProfileHeardAllianceLines(EpisodeState state, string id)
        {
            var lines = new List<string>();
            if (state == null || string.IsNullOrEmpty(id)) return lines;
            foreach (var pact in AllianceRead.Read(state).suspected.Where(p => p.memberIds.Contains(id)))
            {
                var members = pact.memberIds.Select(m => state.Find(m)).Where(a => a != null).Select(a => a.name).ToList();
                lines.Add((pact.certainty == FinalistRead.Confirmed ? "Known alliance" : "Suspected alliance")
                    + " · " + AllianceRead.Join(members));
            }
            return lines;
        }

        /// <summary>
        /// Where they are standing now, as the room directory reads it: "Now in the kitchen". Null for
        /// someone out of the house or whose body the house cannot place, as the directory leaves them.
        /// </summary>
        private string ProfileWhere(EpisodeState state, ContestantState actor)
        {
            if (state == null || actor == null) return null;
            foreach (var room in HouseOccupancy(state))
                if (room.Occupants != null && room.Occupants.Any(o => o.Id == actor.id))
                    return "Now in the " + RoomLabels.InSentence(room.Name);
            return null;
        }

        /// <summary>
        /// The "You" card's week: who holds what (the episode screen's line), the week and its phase (the
        /// overview's), and in free time the actions you have left (the conversation's words).
        /// </summary>
        public static List<string> YourWeekLines(EpisodeState state)
        {
            var lines = new List<string>();
            if (state == null) return lines;
            lines.Add("Week " + state.week + "  ·  " + EpisodeHud.PhaseShort(state.phase));
            string holds = HouseStatus(state);
            if (holds != null) lines.Add(holds);
            if (state.phase == EpisodePhase.Social && state.Find(state.playerId)?.status == ContestantStatus.Active)
                lines.Add(ActionsLeft(state) + " left");
            if (YourWord.On(state))
            {
                lines.Add(YourWord.Title(state));
                string summary = YourWord.Summary(state);
                if (!string.IsNullOrEmpty(summary)) lines.Add(summary);
            }
            return lines;
        }

        /// <summary>
        /// The "You" card's deals and promises: the Deals &amp; promises page's lines
        /// (<see cref="CommitmentsRead.Line"/>), those still open first, and how many more the page holds.
        /// </summary>
        public static List<string> YourDealLines(EpisodeState state, out int more)
        {
            var all = CommitmentsRead.Of(state);
            var ordered = all.Where(c => c.IsOpen).Concat(all.Where(c => !c.IsOpen)).Select(CommitmentsRead.Line).ToList();
            more = System.Math.Max(0, ordered.Count - ProfileLinesShown);
            return ordered.Take(ProfileLinesShown).ToList();
        }

        /// <summary>The "You" card's alliances: each of your pacts as the alliances page heads it, by name, with since when or that it ended.</summary>
        public static List<string> YourAllianceLines(EpisodeState state)
        {
            var lines = new List<string>();
            if (state == null) return lines;
            foreach (var pact in AllianceRead.Read(state).yours)
                lines.Add(pact.name + (pact.active
                    ? (pact.formedWeek > 0 ? " · since week " + pact.formedWeek : " · standing")
                    : " · ended" + (pact.endedWeek > 0 ? " in week " + pact.endedWeek : "")));
            return lines;
        }

        /// <summary>The blocks a houseguest's profile gathers from the other pages, each only where it has something.</summary>
        private void ProfileSourceBlocks(EpisodeState state, ContestantState actor, EpisodeHud.ProfileView view)
        {
            var notes = ProfileNotesLines(state, actor.id, out int more);
            if (more > 0) notes.Add("and " + more + (more == 1 ? " more line" : " more lines") + " in the season's record");
            AddBlock(view, ProfileNotesHeading, notes);
            AddBlock(view, ProfileVotesHeading, ProfileVoteLines(state, actor.id));
            AddBlock(view, ProfileHeardAlliancesHeading, ProfileHeardAllianceLines(state, actor.id));
        }

        private static void AddBlock(EpisodeHud.ProfileView view, string heading, List<string> lines)
        {
            if (lines != null && lines.Count > 0) view.Blocks.Add((heading, (IList<string>)lines));
        }

        /// <summary>
        /// The "You" card (ruling N6 iv): the profile's frame for the player's own houseguest - name,
        /// archetype and traits, the card they came in with, the week, Deals &amp; promises, alliances and
        /// actions left - every line one another screen already shows the player, by the same words.
        /// No trust card: trust is what you hold in someone else.
        /// </summary>
        private void RenderYourCard(EpisodeState state, ContestantState you)
        {
            var view = ProfileIdentity(you);
            view.Status = "Status: " + StatusWord(you.status);
            view.Records = new List<string> { CeremonyRecord(you) };
            view.Back = BackToHouseguests;
            view.BackCaption = ProfileBackCaptionFor(profileFrom);
            // Free talk at talk rules 2: how the house reads the player (EpisodeDirector.JustTalk.cs); nothing below that level.
            YourTalkReadBlocks(state, view);
            AddBlock(view, YourWeekHeading, YourWeekLines(state));
            var deals = YourDealLines(state, out int more);
            if (more > 0) deals.Add("and " + more + " more in " + DealsAndPromisesName);
            AddBlock(view, DealsAndPromisesName, deals);
            AddBlock(view, YourAlliancesBlockHeading, YourAllianceLines(state));
            hud.HouseguestProfile(view);
            hud.Mark(NotebookSection.People);
        }

        /// <summary>Who someone is, as their card says it: the identity half every profile shares.</summary>
        private static EpisodeHud.ProfileView ProfileIdentity(ContestantState actor)
        {
            var facts = new List<string>();
            if (actor.age > 0) facts.Add(actor.age.ToString());
            if (!string.IsNullOrEmpty(actor.occupation)) facts.Add(actor.occupation);
            // Their pronouns, as the conversation's notice gives them.
            if (!string.IsNullOrEmpty(actor.pronouns)) facts.Add(actor.pronouns);
            return new EpisodeHud.ProfileView
            {
                Name = actor.name,
                Archetype = actor.archetype,
                Facts = facts.Count > 0 ? string.Join(" · ", facts) : null,
                Hometown = string.IsNullOrEmpty(actor.hometown) ? null : "From " + actor.hometown,
                About = string.IsNullOrEmpty(actor.bio) ? null : actor.bio,
                Character = actor,
                Traits = actor.traits != null ? actor.traits.Where(t => !string.IsNullOrEmpty(t)).ToList() : new List<string>(),
            };
        }

        /// <summary>The season's ceremony counts, which the whole house watched: the first line of a profile's records.</summary>
        private static string CeremonyRecord(ContestantState actor) =>
            "Head of Household " + Times(actor.hohWins) + " · Veto " + Times(actor.vetoWins)
            + " · Nominated " + Times(actor.timesNominated);
    }
}
