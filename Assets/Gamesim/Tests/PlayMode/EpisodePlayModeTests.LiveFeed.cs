using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The live feed (VISUAL-TARGET.md, phase V5): a second camera on the house in a card, aimed
    /// at the action, captioned with the room and the count - never with the talk, unless the
    /// player is there to hear it.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator LiveFeed_PublishesWordsOnlyWithTheirRenderedFrameAndFreezesBoth()
        {
            var host = new GameObject("Atomic feed test");
            var feed = LiveFeed.Attach(host);
            try
            {
                feed.Paused = true;
                feed.Aim(new Vector3(1,0,1), 0, "FIRST ROOM");
                for (int frame = 0; frame < LiveFeed.EveryNthFrame + 1; frame++) yield return null;
                Assert.That(feed.CaptureRevision, Is.Zero);
                Assert.That(feed.CapturedCaption, Is.Empty, "An aim request is not a rendered image.");

                feed.Paused = false;
                float until = Time.realtimeSinceStartup + 3f;
                while (feed.CaptureRevision == 0 && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(feed.CaptureRevision, Is.GreaterThan(0), "This visual test requires a rendering device.");
                Assert.That(feed.CapturedCaption, Is.EqualTo("FIRST ROOM"));
                Assert.That(feed.CapturedSubject, Is.EqualTo(new Vector3(1,0,1)));

                feed.Paused = true;
                int revision = feed.CaptureRevision;
                var capturedEye = feed.Camera.transform.position;
                feed.Aim(new Vector3(6,0,6), 180, "SECOND ROOM");
                for (int frame = 0; frame < LiveFeed.EveryNthFrame + 1; frame++) yield return null;
                Assert.That(feed.CaptureRevision, Is.EqualTo(revision));
                Assert.That(feed.Camera.transform.position, Is.EqualTo(capturedEye));
                Assert.That(feed.CapturedCaption, Is.EqualTo("FIRST ROOM"));
                Assert.That(feed.CapturedSubject, Is.EqualTo(new Vector3(1,0,1)));

                string published = null;
                feed.FrameCaptured += caption => published = caption;
                feed.Paused = false;
                until = Time.realtimeSinceStartup + 3f;
                while (feed.CaptureRevision == revision && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(published, Is.EqualTo("SECOND ROOM"));
                Assert.That(feed.CapturedSubject, Is.EqualTo(new Vector3(6,0,6)));
                Assert.That(feed.Camera.transform.position, Is.Not.EqualTo(capturedEye));
            }
            finally { Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator LiveFeed_WatchesTheHouseFromASecondCameraAndNamesTheRoom()
        {
            Assert.That(director.LiveFeedTexture, Is.Not.Null, "The feed renders into a texture the card can show.");
            var feedCamera = director.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.name == LiveFeed.CameraName);
            Assert.That(feedCamera, Is.Not.Null);
            Assert.That(feedCamera.CompareTag("MainCamera"), Is.False, "Never the main camera: the tags and the ring read Camera.main.");
            Assert.That(feedCamera.enabled, Is.False, "Rendered by hand, every third frame.");
            Assert.That(Camera.main, Is.SameAs(cameraRig.ViewCamera), "The viewer's camera is still the main one.");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (string.IsNullOrEmpty(director.LiveFeedCaption) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.LiveFeedCaption, Is.Not.Empty, "The feed found something to look at.");
            var feed = director.GetComponent<LiveFeed>();
            Assert.That(feed.HasSubject, Is.True);
            var markers = SceneComponents<HouseRoomMarker>();
            Assert.That(markers.Any(m => Vector3.Distance(m.transform.position, feed.Subject) < 12f), Is.True, "The subject is in the house.");
            if (string.IsNullOrEmpty(director.ObservedNpcConversation))
                Assert.That(director.LiveFeedCaption, Does.Match(@"^[A-Z ]+ · \d+ HOUSEGUESTS?$"),
                    "Unwitnessed, the caption names the room and the count, never the talk: " + director.LiveFeedCaption);

            director.ClosePanels();
            yield return null;
            var card = director.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == EpisodeDirector.LiveFeedCardName);
            Assert.That(card, Is.Not.Null, "The card is fixed chrome.");
            var picture = card.GetComponentInChildren<RawImage>(true);
            Assert.That(picture, Is.Not.Null);
            Assert.That(picture.texture, Is.SameAs(director.LiveFeedTexture));
            Assert.That(card.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text), Does.Contain(director.LiveFeedCaption));

            Assert.That(director.ShowOverview(), Is.True);
            yield return null;
            Assert.That(director.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == EpisodeDirector.LiveFeedCardName), Is.False,
                "The overview is the live house; the card steps aside for its column.");
            director.EndOverview();
            yield return null;
            Assert.That(director.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == EpisodeDirector.LiveFeedCardName), Is.True);
        }
    }
}
