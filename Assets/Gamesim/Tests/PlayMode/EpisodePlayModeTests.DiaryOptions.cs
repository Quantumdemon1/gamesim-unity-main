using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The diary's own page (mockup-11): a column down the right-hand side of the frame, with
        /// the room's name and "Confessional Options" in its head rather than the week's phase
        /// band, the things to do here as cards - a glyph, the caption, what it means - and the
        /// player in the chair left in view between the column and the rail, over the strip.
        /// </summary>
        [UnityTest]
        public IEnumerator Diary_TheRoomIsAColumnOfOptionCardsBesideTheChair()
        {
            yield return OpenDiaryFixturePanel();
            Canvas.ForceUpdateCanvases();
            var hud = director.GetComponentInChildren<EpisodeHud>();
            Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Diary));

            var header = ActiveRect(EpisodeHud.DiaryHeaderName);
            Assert.That(header, Is.Not.Null, "The diary has a head of its own.");
            Assert.That(header.GetComponentsInChildren<TMPro.TMP_Text>().Select(text => text.text),
                Does.Contain(EpisodeHud.DiaryOptionsTitle).And.Contain(EpisodeHud.DiaryEyebrowCopy));
            Assert.That(ActiveRect("Phase band"), Is.Null, "The diary is a room, not a beat of the week.");

            // The study approaches are option cards: the glyph, and what the choice means in a
            // line of its own beside the caption the control is known by.
            var memorize = ButtonWithCaption(EpisodeHud.StudyMemorizeCaption);
            Assert.That(memorize.transform.Find("Option mark"), Is.Not.Null, "An option card carries its glyph.");
            Assert.That(memorize.GetComponentsInChildren<TMPro.TMP_Text>().Select(text => text.text),
                Does.Contain("Guaranteed +1 preparation, up to the limit of 5."), "An option card says what it means.");

            // A column at the right-hand side, leaving the chair in view, with the strip and the
            // rail still up around it.
            var column = ScreenRect(ActiveRect("Episode panel"));
            Assert.That(column.width, Is.LessThan(Screen.width * .32f), "The diary is a column, not a panel over the room.");
            Assert.That(Screen.width - column.xMax, Is.LessThan(Screen.width * .03f), "The column stands at the right-hand side.");
            foreach (var name in new[] { CastRail.RootName, IconRail.RootName })
            {
                var chrome = ActiveRect(name);
                Assert.That(chrome, Is.Not.Null, name + " stays up in the diary.");
                Assert.That(column.Overlaps(ScreenRect(chrome)), Is.False, "The column covers '" + name + "'.");
            }
            if (Application.isBatchMode) yield return CaptureFraming("diary-options");
        }
    }
}
