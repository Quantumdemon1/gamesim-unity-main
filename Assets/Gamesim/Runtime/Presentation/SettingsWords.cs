using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>The sections of the Settings frame, in the order the list shows them (menu slice 4, the review's change 2).</summary>
    public enum SettingsSection { Saves, Sound, Display, Accessibility, Controls, Advanced }

    /// <summary>
    /// Settings and saves in words and rules (menu slice 4, the review's change 2): the game menu
    /// card the house's door opens first, the sections of the Settings frame, and the name-and-value
    /// rows each section is made of - what a row's value reads and what left, right and Enter do to it.
    ///
    /// <para>Plain C#, so the Unity-free suite reads the same words and steps the screens use. A
    /// caption is a contract: tests, screen readers and the standalone verify find a control by it.
    /// A row's caption is the setting's NAME; its value is a label of its own beside it.</para>
    /// </summary>
    public static class SettingsWords
    {
        // ------------------------------------------------------------------ the game menu card

        /// <summary>The card's title.</summary>
        public const string GameMenuTitle = "Game menu";
        /// <summary>The small label over the title, shown only while the house's clock really is held.</summary>
        public const string PausedLabel = "PAUSED";
        /// <summary>
        /// The label's size at standard text: 15 px (it was the head's 12 px eyebrow, muted, and
        /// free to shrink). It grows with the text size and never shrinks below that.
        /// </summary>
        public const int PausedLabelSize = 15;
        /// <summary>The label's font size at a text-size scale.</summary>
        public static int PausedLabelFont(float scale) => (int)Math.Round(PausedLabelSize * Math.Max(1f, scale), MidpointRounding.AwayFromZero);
        /// <summary>The label's box height for a font size: the row kit's 1.3 times, so the line is never cut off.</summary>
        public static float PausedLabelBox(int font) => (float)Math.Ceiling(font * WayOut.LabelBoxRatio);
        /// <summary>The card as a place a way back returns to: Settings opened from it go back there.</summary>
        public const string GameMenuPlace = "the game menu";
        /// <summary>The card's one main button: back to the house.</summary>
        public const string ResumeCaption = "Resume";
        /// <summary>Saves the season and stays on the card.</summary>
        public const string SaveCaption = "Save";
        /// <summary>Opens the Settings frame on its first section.</summary>
        public const string SettingsCaption = "Settings";
        /// <summary>
        /// The way to the main menu, as it was worded in Settings (menu slice 2 kept it as an onward
        /// action). It keeps the season: opening any panel writes the house to disk, and the main
        /// menu's Continue goes back to the season in memory.
        /// </summary>
        public const string MainMenuCaption = "Main menu";
        /// <summary>The muted line under the way to the main menu, saying what it keeps.</summary>
        public const string MainMenuKeepsLine = "Your season is kept. Continue on the main menu goes back to it.";

        /// <summary>The card's line once the season was saved from it: "Saved at 7:42 PM."</summary>
        public static string SavedAtLine(DateTime when) => "Saved at " + when.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture) + ".";

        /// <summary>
        /// The one clock Settings read "now" from (the game menu's saved line, the save list's "saved
        /// 2 hours ago"). A test sets it and puts it back; nothing else does.
        /// </summary>
        public static Func<DateTime> Clock = () => DateTime.Now;

        /// <summary>Now, by <see cref="Clock"/>; the real clock when a test left it null.</summary>
        public static DateTime Now => Clock != null ? Clock() : DateTime.Now;

        // ------------------------------------------------------------------ the sections

        /// <summary>Every section, in the list's order.</summary>
        public static readonly IReadOnlyList<SettingsSection> Sections = (SettingsSection[])Enum.GetValues(typeof(SettingsSection));

        /// <summary>A section's name in the list: its caption, the one tests and screen readers find it by.</summary>
        public static string Name(SettingsSection section)
        {
            switch (section)
            {
                case SettingsSection.Saves: return "Saves";
                case SettingsSection.Sound: return "Sound";
                case SettingsSection.Display: return "Display";
                case SettingsSection.Accessibility: return "Accessibility";
                case SettingsSection.Controls: return "Controls";
                default: return "Advanced";
            }
        }

        /// <summary>The section's heading over its rows, in the one section heading style (it draws in capitals).</summary>
        public static string Heading(SettingsSection section) => Name(section).ToUpperInvariant();

        /// <summary>The object name of a section's entry in the list.</summary>
        public static string TabName(SettingsSection section) => TabPrefix + Name(section);

        /// <summary>How every section entry's object name begins.</summary>
        public const string TabPrefix = "Settings section: ";

        /// <summary>The section the frame opens on: Saves, and in blocked recovery Advanced, where the recovery tools are.</summary>
        public static SettingsSection OpeningSection(bool blockedRecovery) => blockedRecovery ? SettingsSection.Advanced : SettingsSection.Saves;

        /// <summary>The section one step from <paramref name="section"/> in the list (wrapping), for a press of left or right on a section entry.</summary>
        public static SettingsSection Neighbour(SettingsSection section, int direction)
        {
            int count = Sections.Count;
            return Sections[Wrap((int)section + Math.Sign(direction), count)];
        }

        // ------------------------------------------------------------------ the rows

        /// <summary>A switch's value.</summary>
        public const string On = "On";
        /// <summary>A switch's value.</summary>
        public const string Off = "Off";

        /// <summary>What a switch reads: "On" or "Off".</summary>
        public static string SwitchValue(bool on) => on ? On : Off;

        /// <summary>What a slider reads: "70%".</summary>
        public static string Percent(int percent) => Clamp(percent) + "%";

        /// <summary>A slider's step for a press of left or right, in percent.</summary>
        public const int SliderStep = 10;

        /// <summary>A slider moved one step by left (negative) or right (positive), held to 0 to 100.</summary>
        public static int SliderStepped(int percent, int direction) => Clamp(percent + Math.Sign(direction) * SliderStep);

        /// <summary>A choice moved one step by left (negative), right or Enter (positive), wrapping round its values.</summary>
        public static int ChoiceStepped(int index, int count, int direction) => count <= 0 ? 0 : Wrap(index + Math.Sign(direction), count);

        /// <summary>
        /// What a row says to a screen reader: its name and its value together ("Music: On"), and
        /// when it cannot be used, why ("Music: On. Not while ...").
        /// </summary>
        public static string Description(string name, string value, string whyDisabled = null)
        {
            string said = string.IsNullOrEmpty(value) ? name : name + ": " + value;
            return string.IsNullOrEmpty(whyDisabled) ? said : said + ". " + whyDisabled;
        }

        // ------------------------------------------------------------------ the confirm card

        /// <summary>The object name of the pick-then-confirm card the row kit draws.</summary>
        public const string ConfirmCardName = "Settings confirm card";

        private static int Clamp(int percent) => Math.Max(0, Math.Min(100, percent));
        private static int Wrap(int index, int count) => ((index % count) + count) % count;
    }
}
