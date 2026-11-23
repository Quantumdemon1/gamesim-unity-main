using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What the screen and the notebook print of a romance (slice S3, talk rules 3), pure and testable in the pattern of
    /// <see cref="TalkNotes"/>: the housemate page's romance line, the profile note "Took the no well", and the third beat's
    /// after-lines. Null or empty below talk rules 3. American spelling; tame; nothing about anyone's life outside the house.
    /// </summary>
    public static class TalkRomanceNotes
    {
        /// <summary>The page line's label: "Romance: ...".</summary>
        public const string RomanceLabel = "Romance";

        /// <summary>The profile note a housemate's page shows when the player took their turn-aside well (took-the-no-well).</summary>
        public const string TookTheNoWell = "Took the no well";

        /// <summary>The line the third beat adds when the couple's fact went public with this answer.</summary>
        public const string HouseKnows = "The house knows about you two now.";

        /// <summary>The housemate page's romance line ("Romance: A spark between you."), or null when there is nothing to show.</summary>
        public static string PageLine(EpisodeState s, string npcId)
        {
            string words = TalkRomance.Words(s, npcId);
            return words == null ? null : RomanceLabel + ": " + words;
        }

        /// <summary>The profile notes the romance adds to a housemate's page (<see cref="TookTheNoWell"/>); empty when none.</summary>
        public static List<string> ProfileNotes(EpisodeState s, string npcId)
        {
            var notes = new List<string>();
            if (!TalkRomance.LevelOn(s)) return notes;
            var contact = FreeTalk.Contact(s, npcId);
            if (contact != null && contact.talkFlags.Contains(TalkRules.TookTheNoWell)) notes.Add(TookTheNoWell);
            return notes;
        }

        /// <summary>The third beat's romance lines after an answer, in order (what changed, for a housemate by first name); empty when nothing romantic changed.</summary>
        public static List<string> AfterLines(TalkResult result, string firstName)
        {
            var lines = new List<string>();
            if (result == null) return lines;
            string who = string.IsNullOrEmpty(firstName) ? "They" : firstName;
            string them = string.IsNullOrEmpty(firstName) ? "them" : firstName;
            switch (result.Change)
            {
                case RomanceChange.Spark:
                    if (result.SparkAfter > result.SparkBefore) lines.Add("Something is building with " + them + ".");
                    break;
                case RomanceChange.FriendsLifted:
                    lines.Add("You flirted with " + them + " again. It's back on the table.");
                    break;
                case RomanceChange.SoftNo: lines.Add(who + " said no, kindly. Give it time."); break;
                case RomanceChange.HoldBack: lines.Add(who + " is holding back for now."); break;
                case RomanceChange.LineDrawn: lines.Add(who + " drew a line. Friends only."); break;
                case RomanceChange.TurnAside:
                    lines.Add(result.Attempt >= 3 ? who + " won't hear any more of it." : who + " turned it aside.");
                    break;
                case RomanceChange.TurnAsideAnswered:
                    if (result.Choice == TalkChoice.LaughItOff) lines.Add("You laughed it off together.");
                    else if (result.Choice == TalkChoice.PushAgain) lines.Add("You pushed. " + who + " cooled on you.");
                    else if (result.Choice == TalkChoice.Apologize) lines.Add("You took the no well.");
                    break;
                case RomanceChange.Couple: lines.Add("You and " + them + " are a couple now."); break;
                case RomanceChange.NotYet: lines.Add("Not yet. " + who + " can wait."); break;
                case RomanceChange.NotLikeThat: lines.Add("Romance with " + them + " is off for a while."); break;
                case RomanceChange.JustFriends: lines.Add("Just friends with " + them + ". No hard feelings."); break;
                case RomanceChange.Ended: lines.Add("You and " + them + " ended it kindly."); break;
                case RomanceChange.SecondDeclaration: lines.Add("Two declarations at once. Both of them walked away."); break;
            }
            if (result.WentPublic) lines.Add(HouseKnows);
            return lines;
        }
    }
}
