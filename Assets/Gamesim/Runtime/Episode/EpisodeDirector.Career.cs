using System;
using System.IO;
using Gamesim.Persistence;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The career: what the director adds to the ledger, and what it reads back for the menu,
    /// the settings panel and the season report.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>Captions. Tests and screen readers identify these controls by their words.</summary>
        public const string ResetCareerCaption = "Reset the career record";
        public const string ConfirmResetCareerCaption = "Yes, set the record aside and start fresh";
        public const string KeepCareerCaption = "Keep the career record";

        private CareerLedger career;
        private bool careerResetArmed;

        /// <summary>Where this save root keeps its career file.</summary>
        public string CareerPath => career?.FilePath;

        /// <summary>
        /// Adds a finished season to the ledger. Safe to call on every commit and every load:
        /// anything short of a finale is ignored and a season already recorded is not recorded
        /// twice. A file problem is reported in the status line and never fails the commit that
        /// finished the season — the save is the record of the season; this is the record of the
        /// player.
        /// </summary>
        private void RecordCareer(EpisodeState state)
        {
            if (career == null || state == null || state.phase != EpisodePhase.Finished) return;
            try
            {
                if (career.Record(state)) message += "  ·  Added to your career record.";
                else if (career.Notice != null) message += "  ·  " + career.Notice;
            }
            catch (Exception error) when (SaveJson.IsExpected(error))
            {
                message += "  ·  The career record could not be updated: " + error.Message;
            }
        }

        private CareerSummary CareerNow() => career == null ? null : CareerSummary.Of(career.Load());

        /// <summary>The one line the main menu carries under its buttons, or null before any season has finished.</summary>
        private string CareerLine()
        {
            var summary = CareerNow();
            return summary == null || summary.Seasons == 0 ? null : "Your career: " + summary.Line();
        }

        /// <summary>The settings panel's career block, with a reset that takes two clicks.</summary>
        private void CareerSettings()
        {
            if (career == null) return;
            hud.Heading("YOUR CAREER");
            var summary = CareerNow();
            hud.Paragraph(summary.Seasons == 0
                ? "No finished seasons yet. A season joins the record when its jury has voted."
                : summary.Line() + ".");
            if (career.Notice != null) hud.Paragraph(career.Notice);
            if (summary.Seasons == 0) { careerResetArmed = false; return; }
            if (!careerResetArmed)
            {
                hud.Action(ResetCareerCaption, () => { careerResetArmed = true; Render(); });
                return;
            }
            hud.Paragraph("This sets the record aside as a dated file beside the saves and starts a fresh one. Nothing is deleted.");
            hud.Action(ConfirmResetCareerCaption, ResetCareer);
            hud.Action(KeepCareerCaption, () => { careerResetArmed = false; Render(); });
        }

        /// <summary>The second click. Moves the file aside rather than deleting it.</summary>
        public void ResetCareer()
        {
            careerResetArmed = false;
            if (career == null) return;
            var archived = career.Reset();
            message = archived == null
                ? "There was no career record to reset."
                : "Career record set aside as " + Path.GetFileName(archived) + ". A fresh record starts with your next finished season.";
            Render();
        }
    }
}
