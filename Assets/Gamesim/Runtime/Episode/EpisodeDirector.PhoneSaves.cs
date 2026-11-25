using System;
using System.Collections.Generic;
using Gamesim.Persistence;
using Gamesim.Presentation;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeDirector
    {
        private enum PhoneSavesView { Shelf, Sections, Import }
        private PhoneSavesView phoneSavesView;
        private string phoneSavePath, phoneSavesFocus, phoneImportPath = "";
        private int phoneSavesPage;
        private bool phoneSavesRetryFocus;
        private const int SeasonsPerPhonePage = 8;
        private const string BackToSaves = WayOut.BackPrefix + "saved seasons";
        private const string MoreSettings = "Other settings";

        private void ReturnToPhoneShelf(string focus)
        {
            phoneSavePath = savesLoadPick = savesDeletePick = null;
            phoneSavesView = PhoneSavesView.Shelf;
            RenderAndFocus(focus);
        }

        private void PhoneSaves()
        {
            string requested = phoneSavesFocus;
            phoneSavesFocus = null;
            if (phoneSavesView != PhoneSavesView.Shelf)
            {
                PhoneSavesTools(requested);
                return;
            }

            bool running = SeasonIsRunning;
            string playing = saves?.SavePath;
            var others = new List<SavedSeason>();
            drawnSeasons.Clear();
            SavedSeason selected = null;
            foreach (var season in shelf.Read(saveRoot))
            {
                if (running && SamePath(season.Path, playing)) continue;
                others.Add(season);
                drawnSeasons.Add((season.Path, SavesWords.CardName(season.Key)));
                if (SamePath(season.Path, phoneSavePath)) selected = season;
            }
            if (selected != null)
            {
                PhoneSeasonDetails(selected, SamePath(selected.Path, playing), requested);
                return;
            }
            // A file can disappear between opening its details and the next redraw.
            phoneSavePath = savesLoadPick = savesDeletePick = null;
            phoneSavesRetryFocus = false;
            var page = hud.OpenPhonePanel("Saved seasons");
            page.Copy(message);
            Button exit;
            if (settingsFromMainMenu || blockedRecovery)
                exit = page.AddBack(WayOut.BackTo(WayOut.MainMenu), OpenMainMenu);
            else if (settingsFromFrontDoor)
                exit = page.AddClose(ClosePanels);
            else
            {
                exit = page.AddBack(SettingsWayBackCaption, BackToGameMenu);
                page.AddClose(ClosePanels);
                page.ActionRow(SettingsWords.ResumeCaption, ClosePanels);
            }

            string first = exit.name;
            if (running)
            {
                var current = SavedSeason.Of(projected ?? engine.Snapshot, playing, WrittenAt(playing));
                page.Copy(SavesWords.PlayingNow, true);
                page.Copy(Title(current));
                page.Copy(SavesWords.Details(current.Week, current.Left) + " · " + SavedLine(current.SavedUtc));
                page.ActionRow("Save now", SaveNow, primary: true).name = SavesWords.SaveNowCaption;
                first = SavesWords.SaveNowCaption;
            }
            else if (others.Count == 0) page.Copy(SavesWords.NoSaves);
            int pages = Math.Max(1, (others.Count + SeasonsPerPhonePage - 1) / SeasonsPerPhonePage);
            int requestedIndex = drawnSeasons.FindIndex(card => card.control == requested);
            if (requestedIndex >= 0) phoneSavesPage = requestedIndex / SeasonsPerPhonePage;
            phoneSavesPage = Math.Min(phoneSavesPage, pages - 1);
            if (pages > 1) page.Copy("Saved seasons · " + (phoneSavesPage + 1) + " of " + pages);
            int start = phoneSavesPage * SeasonsPerPhonePage;
            for (int i = start; i < Math.Min(start + SeasonsPerPhonePage, others.Count); i++)
            {
                var season = others[i];
                string slot = SavesWords.CardName(season.Key);
                string line = Title(season) + (season.Readable ? " · " + SavesWords.Week(season.Week) : "");
                page.ActionRow((i + 1) + ". " + line + " · " + SavedLine(season.SavedUtc), () =>
                {
                    phoneSavePath = season.Path;
                    Render();
                }).name = slot;
                if (i == start && (!running || phoneSavesPage > 0)) first = slot;
            }
            if (phoneSavesPage > 0) page.ActionRow("Previous saved seasons", () => { phoneSavesPage--; Render(); });
            if (phoneSavesPage + 1 < pages) page.ActionRow("More saved seasons", () => { phoneSavesPage++; Render(); });
            page.ActionRow(NewSeasonInNewSlotCaption, NewSeason);
            page.ActionRow(MoreSettings, () => { phoneSavesView = PhoneSavesView.Sections; Render(); });
            page.ActionRow(ImportHeading, () => { phoneSavesView = PhoneSavesView.Import; Render(); });
            hud.FinishPhoneSettingsPage("shelf-" + phoneSavesPage, first, exit.onClick.Invoke, requested);
        }

        private void PhoneSeasonDetails(SavedSeason season, bool own, string requested)
        {
            var page = hud.OpenPhonePanel(Title(season));
            string slot = SavesWords.CardName(season.Key);
            Action back = () => ReturnToPhoneShelf(slot);
            if (season.Readable) page.Copy(SavesWords.Details(season.Week, season.Left));
            page.Copy(SavedLine(season.SavedUtc));
            if (SamePath(savesFailurePath, season.Path)) page.Copy(savesFailure);
            if (season.InUse)
            {
                page.Copy(SavesWords.InUseLine);
                savesDeletePick = savesLoadPick = null;
            }
            if (!season.Readable) savesLoadPick = null;
            if (own && !season.Readable) page.Copy(SavesWords.RecoveryLine);

            if (savesLoadPick != null || savesDeletePick != null)
            {
                DrawSeasonConfirmation(season, (id, question, safeCaption, safe, resultCaption, result) =>
                {
                    page.Copy(question, true);
                    var cancel = page.AddClose(safe);
                    page.ActionRow(safeCaption, safe, primary: true);
                    page.ActionRow(resultCaption, result);
                    hud.FinishPhoneSettingsPage(id, safeCaption, cancel.onClick.Invoke, requested);
                });
                return;
            }
            var exit = page.AddBack(BackToSaves, back);
            AddPhoneSavesClose(page);
            var controls = new List<EpisodeHud.SeasonControl>();
            AddSeasonControls(season, own, controls);
            if (phoneSavesRetryFocus && controls.Count > 0) requested = controls[0].Name;
            phoneSavesRetryFocus = false;
            foreach (var control in controls)
                page.ActionRow(control.Caption, control.Press).name = control.Name;
            hud.FinishPhoneSettingsPage("details-" + season.Key, exit.name, exit.onClick.Invoke, requested);
        }

        private void PhoneSavesTools(string requested)
        {
            bool settings = phoneSavesView == PhoneSavesView.Sections;
            string title = settings ? MoreSettings : ImportHeading;
            Action back = () => ReturnToPhoneShelf(title);
            var page = hud.OpenPhonePanel(title);
            var exit = page.AddBack(BackToSaves, back);
            AddPhoneSavesClose(page);
            if (settings)
            {
                foreach (var section in SettingsWords.Sections)
                    if (section != SettingsSection.Saves) page.ActionRow(SettingsWords.Name(section), () => ShowSettingsSection(section));
            }
            else
            {
                page.Copy(ImportLine);
                page.Copy(message);
                var input = page.Search("Full path to exported JSON", phoneImportPath, value => phoneImportPath = value, _ => {});
                input.characterLimit = 1024;
                page.ActionRow("Archive and import this file", () => ImportFile(input.text));
            }
            hud.FinishPhoneSettingsPage(title, exit.name, exit.onClick.Invoke, requested);
        }

        private void AddPhoneSavesClose(PhoneMenuPage page)
        {
            if (settingsFromMainMenu || blockedRecovery) page.AddClose(OpenMainMenu);
            else page.AddClose(ClosePanels);
        }
    }
}
