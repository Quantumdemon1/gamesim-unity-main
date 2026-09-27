using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class CompetitionSurfacePlayModeTests
    {
        private GameObject owner;
        private CompetitionGameScreen screen;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            CleanupInput();
            if(screen!=null)Object.Destroy(screen.gameObject);
            if(owner!=null)Object.Destroy(owner);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardHasFourFixedRowsAndSpatialNavigationAtLargeText()
        {
            owner=new GameObject("Competition test");screen=CompetitionGameScreen.Attach(owner);screen.FontScale=1.2f;
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Memory,12,2);
            screen.Show(run,"Head of Household","One\nTwo\nThree",false,i=>run.Flip(i),()=>{},d=>{},()=>{},()=>{});
            yield return null;
            Canvas.ForceUpdateCanvases();
            var cards=screen.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Memory card ")).ToArray();
            Assert.That(cards.Length,Is.EqualTo(16));
            Assert.That(cards.Select(b=>((RectTransform)b.transform).anchoredPosition.x).Distinct().Count(),Is.EqualTo(4));
            Assert.That(cards.Select(b=>((RectTransform)b.transform).anchoredPosition.y).Distinct().Count(),Is.EqualTo(4));
            Assert.That(cards[5].navigation.selectOnUp,Is.SameAs(cards[1]));
            Assert.That(cards[5].navigation.selectOnRight,Is.SameAs(cards[6]));
            Assert.That(screen.GetComponentsInChildren<ScrollRect>(),Is.Empty,"A timed board cannot depend on scrolling.");
            Assert.That(run.Elapsed,Is.Zero);
            Assert.That(screen.AdvanceReady(4),Is.False,"The transition frame consumes no playing time.");
            Assert.That(screen.IsPlaying,Is.True);
            Assert.That(run.Elapsed,Is.Zero);
            var instance=cards[0].GetEntityId();run.Flip(0);screen.Refresh();
            Assert.That(cards[0].GetEntityId(),Is.EqualTo(instance),"Flipping updates content in place, preserving focus and position.");
        }

        [UnityTest]
        public IEnumerator PausingAndClosingStopInputWithoutChangingTheAttempt()
        {
            owner=new GameObject("Competition test");screen=CompetitionGameScreen.Attach(owner);
            var run=new MiniGameRun(CompetitionMiniGames.Kind.Endurance,12,2);
            screen.Show(run,"Veto","One\nTwo",true,i=>{},()=>{},d=>{},()=>run.SetHolding(!run.Holding),()=>{});
            yield return null;screen.AdvanceReady(4);
            run.SetHolding(true);screen.TogglePause();
            Assert.That(run.Holding,Is.False);Assert.That(screen.AdvanceReady(100),Is.False);
            Assert.That(run.Elapsed,Is.Zero);Assert.That(screen.IsPlaying,Is.False);
            screen.Hide();Assert.That(screen.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            Assert.That(screen.AdvanceReady(100),Is.False);Assert.That(run.Elapsed,Is.Zero);
        }
    }
}
