using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The localisation key layer on the HUD (MASTER-PLAN §3.D): with a table loaded a control
    /// keeps its English caption as its name and key while the words on it change, and the prompt
    /// line changes with it; without one, everything is as it was.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Localisation_ATableChangesTheWordsAndNotTheControl()
        {
            director.ClosePanels();
            yield return null;
            var notebook = ButtonWithCaption("Notebook [J]");
            Assert.That(notebook.name, Is.EqualTo("Notebook [J]"), "A control is named for its English caption.");
            try
            {
                Localisation.Use("de", new Dictionary<string, string>
                {
                    { "Notebook [J]", "Notizbuch [J]" },
                    { "SETTINGS & SAVES", "EINSTELLUNGEN & SPEICHERSTÄNDE" },
                });
                director.OpenSettings();
                yield return null;
                var settingsTitle = director.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.text == "EINSTELLUNGEN & SPEICHERSTÄNDE");
                Assert.That(settingsTitle, Is.Not.Null, "A heading the table holds is drawn in the table's words.");
                director.ClosePanels();
                yield return null;
                var translated = director.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Notebook [J]" && b.IsActive());
                Assert.That(translated, Is.Not.Null, "The control keeps its English name: the key the tests and the keyboard ring use.");
                Assert.That(translated.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Notizbuch [J]"), "and shows the table's words.");
                Assert.That(ButtonWithCaptionOrNull("Notebook [J]"), Is.Null, "The English words are not on screen.");
            }
            finally
            {
                Localisation.Use(null, null);
            }
            director.OpenSettings(); director.ClosePanels();
            yield return null;
            Assert.That(ButtonWithCaption("Notebook [J]"), Is.Not.Null, "Without a table the English caption is the words again.");
        }

        private Button ButtonWithCaptionOrNull(string caption) =>
            director.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.IsActive()
                && b.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == caption));
    }
}
