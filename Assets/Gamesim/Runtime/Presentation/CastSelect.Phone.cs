using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    public sealed partial class CastSelect
    {
        private enum PhoneView { Cast, Person, Options, Filters, Library, Slots }
        private PhoneView phoneView;
        private PhoneMenuPage phone;
        private string inspecting, focusOnReturn, profileQuery, editingProfileQuery;
        private int profilePage;
        private readonly Dictionary<PhoneView, float> phoneOffsets = new Dictionary<PhoneView, float>();

        internal string CreatorReturnPlace => phoneView == PhoneView.Options ? "season setup"
            : phoneView == PhoneView.Library ? "My Houseguests" : WayOut.Cast;

        private void ResetPhoneSetup()
        {
            phoneOffsets.Clear();
            phoneView = PhoneView.Cast;
            inspecting = focusOnReturn = null;
            profileQuery = editingProfileQuery = string.Empty;
            profilePage = 0;
            phone = null;
        }

        private void OpenPhoneView(PhoneView next, string focus = null)
        {
            if (phone != null) phoneOffsets[phoneView] = phone.ScrollPosition;
            phoneView = next;
            phone = null;
            focusOnReturn = focus;
            Rebuild();
        }

        private bool BackWithinPhoneSetup()
        {
            if (phoneView == PhoneView.Cast) return false;
            bool fromProfiles = phoneView == PhoneView.Slots || phoneView == PhoneView.Library;
            var person = phoneView == PhoneView.Person ? CastTemplates.Find(inspecting) : null;
            OpenPhoneView(fromProfiles ? PhoneView.Options : PhoneView.Cast, person?.Name);
            return true;
        }

        private void RebuildPhoneSetup()
        {
            if (phone != null) phoneOffsets[phoneView] = phone.ScrollPosition;
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            portraits.Clear();
            startButton = conversationChip = null;
            builtFor = ((RectTransform)transform).rect.size;
            var person = string.IsNullOrEmpty(inspecting) ? null : CastTemplates.Find(inspecting);
            string title = "Choose your houseguest";
            switch (phoneView)
            {
                case PhoneView.Person: title = person.Name; break;
                case PhoneView.Options: title = "Season setup"; break;
                case PhoneView.Filters: title = "Roster and filters"; break;
                case PhoneView.Library: title = "My Houseguests"; break;
                case PhoneView.Slots: title = SeasonHouseguestsCaption; break;
            }
            phone = PhoneMenuPage.Create(transform, title, textScale);
            string back = phoneView == PhoneView.Cast ? BackCaption
                : WayOut.BackTo(phoneView == PhoneView.Slots || phoneView == PhoneView.Library ? "season setup" : WayOut.Cast);
            phone.AddBack(back, Dismiss);
            phone.Copy(resumeError);
            switch (phoneView)
            {
                case PhoneView.Cast:
                    phone.Copy(CastTemplates.RosterName(roster) + " · " + houseSize + " houseguests");
                    if (category != CastTemplates.AllCategories) phone.Copy(category);
                    startButton = phone.ActionRow(StartCaptionNow, StartSeason, primary: true);
                    phone.ActionRow("Season setup", () => OpenPhoneView(PhoneView.Options));
                    phone.ActionRow("Roster and filters", () => OpenPhoneView(PhoneView.Filters));
                    foreach (var guest in CastTemplates.Filter(roster, category))
                    {
                        bool picked = selectedId == guest.Id && !DraftOfSomeoneElse;
                        phone.ActionRow(guest.Name, () => { inspecting = guest.Id; OpenPhoneView(PhoneView.Person); }, selected: picked);
                    }
                    break;
                case PhoneView.Person:
                    PhonePerson(person);
                    break;
                case PhoneView.Options:
                    PhoneOptions();
                    break;
                case PhoneView.Filters:
                    foreach (CastTemplates.Roster item in Enum.GetValues(typeof(CastTemplates.Roster)))
                        phone.ActionRow(CastTemplates.RosterName(item), () =>
                        {
                            if (roster != item) { roster = item; selectedId = null; houseSize = SeasonBuilder.ClampHouseSize(item, houseSize); }
                            phoneOffsets.Remove(PhoneView.Cast);
                            OpenPhoneView(PhoneView.Cast, "Roster and filters");
                        }, selected: roster == item);
                    foreach (string item in new[] { CastTemplates.AllCategories }.Concat(CastTemplates.Categories))
                        phone.ActionRow(item, () =>
                        {
                            category = item;
                            phoneOffsets.Remove(PhoneView.Cast);
                            OpenPhoneView(PhoneView.Cast, "Roster and filters");
                        }, selected: category == item);
                    break;
                default:
                    PhoneProfiles();
                    break;
            }
            phone.Restore(phoneOffsets.TryGetValue(phoneView, out float position) ? position : 1f, focusOnReturn);
            focusOnReturn = null;
        }

        private void PhonePerson(CastTemplates.Template guest)
        {
            var picture = phone.Portrait();
            picture.texture = CastSelectArt.Glamour(guest.Id);
            if (picture.texture == null)
            {
                picture.color = Color.clear;
                portraits.Add(new KeyValuePair<RawImage, ContestantState>(picture, CastTemplates.ToContestant(guest, false)));
            }
            phone.Copy(guest.CardLine);
            phone.Copy(guest.Hometown);
            if (guest.Traits != null) phone.Copy(string.Join(" · ", guest.Traits));
            phone.Copy(guest.Bio);
            bool picked = selectedId == guest.Id && !DraftOfSomeoneElse;
            phone.ActionRow(picked ? "Clear selection" : "Use this houseguest", () =>
            {
                retainedDraft = null;
                selectedId = picked ? null : guest.Id;
                OpenPhoneView(PhoneView.Cast, picked ? guest.Name : StartCaptionNow);
            }, primary: true);
        }

        private void PhoneOptions()
        {
            phone.Copy(houseSize + " houseguests, including you.");
            phone.Copy(campaignConversations ? ConversationPreviewOnLine : ConversationPreviewOffLine);
            int fewer = SeasonBuilder.ClampHouseSize(roster, Math.Max(customHouseguests.Count + 1, houseSize - 1));
            int more = SeasonBuilder.ClampHouseSize(roster, houseSize + 1);
            phone.ActionRow("Fewer houseguests", () => { houseSize = fewer; Rebuild(); }, enabled: fewer < houseSize);
            phone.ActionRow("More houseguests", () => { houseSize = more; Rebuild(); }, enabled: more > houseSize);
            conversationChip = phone.ActionRow(campaignConversations ? ConversationPreviewOffCaption : ConversationPreviewOnCaption,
                ToggleConversationPreview, selected: campaignConversations);
            if (onCustomise != null)
            {
                phone.ActionRow(CharacterCreator.CreateCaption, () => OpenCreator(CharacterDraft.Blank(), CharacterCreator.EntryMode.Quick));
                phone.ActionRow(retainedDraft == null ? CharacterCreator.CustomiseCaption : "Resume setup", () =>
                {
                    if (retainedDraft != null) { OpenCreator(retainedDraft.Copy(), retainedCreatorMode); return; }
                    var chosen = CastTemplates.Find(selectedId);
                    OpenCreator(chosen == null ? CharacterDraft.Blank() : CharacterDraft.FromAppearance(chosen));
                });
                phone.ActionRow("My Houseguests", () => OpenPhoneView(PhoneView.Library));
                phone.ActionRow(SeasonHouseguestsCaption, () => OpenPhoneView(PhoneView.Slots));
            }
        }

        private void PhoneProfiles()
        {
            bool slots = phoneView == PhoneView.Slots;
            if (slots)
            {
                phone.Copy(customHouseguests.Count + " of " + (houseSize - 1) + " places filled.");
                for (int i = 0; i < customHouseguests.Count; i++)
                {
                    int slot = i;
                    phone.Copy((i + 1) + ". " + customHouseguests[i].name);
                    if (creator != null) phone.ActionRow(EditSeasonHouseguestCaption(i + 1), () => EditCastSlot(slot));
                    phone.ActionRow(RemoveSeasonHouseguestCaption(i + 1), () => { customHouseguests.RemoveAt(slot); Rebuild(); });
                }
            }
            var store = ProfileStore;
            var all = store.List();
            foreach (var error in store.ReadErrors) phone.Copy(error);
            void ShowResults(int newPage, string query)
            {
                profilePage = newPage;
                profileQuery = editingProfileQuery = query.Trim();
                phoneOffsets.Remove(phoneView);
                phone = null;
                var first = all.Where(p => (p.name ?? "").IndexOf(profileQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Skip(newPage * CharacterProfileBrowser.PageSize).FirstOrDefault();
                focusOnReturn = first == null || slots && customHouseguests.Count >= houseSize - 1
                    ? "Search houseguests" : slots ? "Add " + first.name + " to the cast" : "Choose " + first.name;
                Rebuild();
            }
            phone.Search("Profile name search", editingProfileQuery, value => editingProfileQuery = value,
                value => ShowResults(0, value));
            phone.ActionRow("Search houseguests", () => ShowResults(0, editingProfileQuery));
            if (profileQuery.Length > 0) phone.ActionRow("Clear search", () => ShowResults(0, ""));
            var matches = all.Where(p => (p.name ?? "").IndexOf(profileQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (matches.Count == 0) phone.Copy(all.Count == 0 ? "No saved houseguests yet." : "No saved houseguests match this name.");
            int last = Math.Max(0, (matches.Count - 1) / CharacterProfileBrowser.PageSize);
            profilePage = Math.Min(profilePage, last);
            foreach (var entry in matches.Skip(profilePage * CharacterProfileBrowser.PageSize).Take(CharacterProfileBrowser.PageSize))
            {
                if (slots)
                    phone.ActionRow("Add " + entry.name + " to the cast", () =>
                    {
                        if (customHouseguests.Count >= houseSize - 1) return;
                        customHouseguests.Add(entry.Clone());
                        Rebuild();
                    }, enabled: customHouseguests.Count < houseSize - 1, profile: entry);
                else
                {
                    phone.ActionRow("Choose " + entry.name, () => OpenCreator(entry.ToDraft()), profile: entry);
                    phone.ActionRow("Remix " + entry.name, () =>
                    {
                        var draft = entry.ToDraft(); draft.Name += " (copy)"; OpenCreator(draft);
                    });
                }
            }
            if (last > 0)
            {
                phone.Copy("Page " + (profilePage + 1) + " of " + (last + 1));
                phone.ActionRow("Previous houseguests", () => ShowResults(profilePage - 1, profileQuery), enabled: profilePage > 0);
                phone.ActionRow("Next houseguests", () => ShowResults(profilePage + 1, profileQuery), enabled: profilePage < last);
            }
        }
    }
}
