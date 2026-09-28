using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A thread (plan 31): a season-long story the player is inside - a bond, a rivalry, the numbers.
    /// Its chapters are plays and arcs aimed at its people, how one ends picks the next, and it ends at
    /// a fixed point that reads the season.
    ///
    /// <para>A thread is a story cycle in its own lane (<see cref="StoryLanes.Thread"/>), so it adds no
    /// field to the save: its cast is who it is about, its path one step per chapter (the chapter
    /// cycle's id, and how it went), and its ending how the season came out for it. It adds no mechanics
    /// either: the bonds, grudges, promises and alliances its chapters make are what the votes and the
    /// jury already read.</para>
    /// </summary>
    public sealed class ThreadTemplate
    {
        /// <summary>One of <see cref="ThreadKinds"/>.</summary>
        public string kind;

        /// <summary>How the notebook and the log name it, with role tokens: "Your bond with {FRIEND}".</summary>
        public string label;

        /// <summary>What it is about, with role tokens: the line it begins with.</summary>
        public string premise;

        /// <summary>Every chapter it may play.</summary>
        public ThreadChapter[] chapters = Array.Empty<ThreadChapter>();

        /// <summary>
        /// The chapters to try next, best first, given how the earlier ones went. Empty when all that is
        /// left is its climax: a thread waiting for its climax never fades.
        /// </summary>
        public Func<StoryContext, StoryCycle, IEnumerable<string>> next;

        /// <summary>Whether it has reached its climax, or a stated end, at this anchor; null while it goes on.</summary>
        public Func<StoryContext, StoryCycle, string> climax;

        /// <summary>How it ends at the finale, reading the jury. Null: it faded.</summary>
        public Func<StoryContext, StoryCycle, string> finale;

        /// <summary>How it ends when the player leaves the house before it does.</summary>
        public string onPlayerExit = ThreadEndings.LeftHouse;

        /// <summary>Whether it fades after a month with nothing it could play (the numbers wait for the final four instead).</summary>
        public bool fades = true;

        /// <summary>The line each ending writes, with role tokens.</summary>
        public Dictionary<string, string> endings = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The endings that went the player's way.</summary>
        public HashSet<string> good = new HashSet<string>(StringComparer.Ordinal);

        public ThreadChapter Chapter(string arcId) => Array.Find(chapters, c => c.arcId == arcId);
    }

    /// <summary>One chapter a thread may play: an arc, aimed at the thread's people.</summary>
    public sealed class ThreadChapter
    {
        public string arcId;

        /// <summary>Who the chapter is aimed at (<see cref="StoryContext.focus"/>), or null to let the arc cast as it would.</summary>
        public Func<StoryContext, StoryCycle, string[]> focus;

        /// <summary>Whether it went the thread's way, read from the chapter's cycle once it ended. Null: a play won.</summary>
        public Func<EpisodeState, StorylineState, bool> landed;

        /// <summary>How many times the thread may try it.</summary>
        public int attempts = 2;
    }

    /// <summary>How one chapter of a thread went, as the notebook shows it.</summary>
    public static class ThreadChapterResults
    {
        public const string Open = "open", Landed = "landed", Missed = "missed", Never = "never";
    }

    public static class ThreadKinds
    {
        public const string Bond = "bond", Rivalry = "rivalry", Numbers = "numbers";
        public static readonly string[] All = { Bond, Rivalry, Numbers };
    }

    /// <summary>How a thread ends, in the words the notebook and the recap use.</summary>
    public static class ThreadEndings
    {
        public const string ToTheEnd = "to-the-end", ForYou = "for-you", AgainstYou = "against-you", Parted = "parted",
            MadePeace = "made-peace", Won = "won", Lost = "lost", Held = "held", Broken = "broken", Faded = "faded",
            LeftHouse = "left-house";
    }
}
