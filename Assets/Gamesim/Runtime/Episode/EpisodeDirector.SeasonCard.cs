using System;
using System.IO;
using Gamesim.Persistence;
using Gamesim.Presentation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The main menu's season card: which season "Continue your season" opens.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// The season card's line, read from the top of this slot's save (its backup when the save
        /// itself is not there, as Continue is offered for either), or null when there is no card
        /// or its facts could not be read. A look at the file's header only (<see cref="SaveHeader"/>):
        /// it builds no season, takes nothing from the save store and writes nothing.
        /// </summary>
        private string ContinueCardFacts(bool canContinue)
        {
            if (!canContinue || saves == null) return null;
            string path = File.Exists(saves.SavePath) ? saves.SavePath : saves.BackupPath;
            return SeasonCardWords.Facts(SaveHeader.Peek(path), DateTime.UtcNow);
        }
    }
}
