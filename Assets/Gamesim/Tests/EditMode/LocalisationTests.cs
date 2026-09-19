using System.Collections.Generic;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The localisation key layer (MASTER-PLAN §3.D): an English caption is its own key, a table
    /// changes the words and nothing else, and a caption without an entry is shown as it is.
    /// </summary>
    public sealed class LocalisationTests
    {
        [TearDown]
        public void Restore() => Localisation.Use(null, null);

        [Test]
        public void WithoutATableEveryCaptionIsItself()
        {
            Localisation.Use(null, null);
            Assert.That(Localisation.HasTable, Is.False);
            Assert.That(Localisation.Language, Is.EqualTo(Localisation.DefaultLanguage));
            Assert.That(Localisation.Text("Deliver your speech"), Is.EqualTo("Deliver your speech"));
            Assert.That(Localisation.Text(null), Is.Null);
            Assert.That(Localisation.Text(""), Is.EqualTo(""));
        }

        [Test]
        public void ATableTranslatesWhatItHoldsAndLeavesTheRest()
        {
            Localisation.Use("de", new Dictionary<string, string> { { "Notebook [J]", "Notizbuch [J]" }, { "Empty", "" } });
            Assert.That(Localisation.Language, Is.EqualTo("de"));
            Assert.That(Localisation.Text("Notebook [J]"), Is.EqualTo("Notizbuch [J]"));
            Assert.That(Localisation.Text("Settings"), Is.EqualTo("Settings"), "A caption the table lacks is shown as it is.");
            Assert.That(Localisation.Text("Empty"), Is.EqualTo("Empty"), "An empty entry is no entry.");
        }

        [Test]
        public void ALanguageWithNoTableFallsBackToTheDefault()
        {
            Assert.That(Localisation.Load("xx"), Is.False);
            Assert.That(Localisation.Language, Is.EqualTo(Localisation.DefaultLanguage));
            Assert.That(Localisation.HasTable, Is.False);
            Assert.That(Localisation.Available(), Does.Contain(Localisation.DefaultLanguage));
        }

        [Test]
        public void TheTableFormatIsAFlatJsonObject()
        {
            var into = new Dictionary<string, string>();
            Localisation.Parse("{\n  \"Save now  [F5]\": \"Jetzt speichern  [F5]\",\n  \"Line\\nbreak\": \"Zeilen\\numbruch\", \"Quote \\\"x\\\"\": \"Zitat \\\"x\\\"\" }", into);
            Assert.That(into["Save now  [F5]"], Is.EqualTo("Jetzt speichern  [F5]"));
            Assert.That(into["Line\nbreak"], Is.EqualTo("Zeilen\numbruch"));
            Assert.That(into["Quote \"x\""], Is.EqualTo("Zitat \"x\""));
            Assert.That(into.Count, Is.EqualTo(3));
        }
    }
}
