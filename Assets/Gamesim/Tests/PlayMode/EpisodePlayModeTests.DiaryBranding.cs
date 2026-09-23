using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The Diary Room looks like the Diary Room - in the shot the player actually sees.
    ///
    /// <para>It was a chair against a dark wall. The pack's diary art is now built on that wall,
    /// and the question worth asking is not whether the objects exist but whether they are IN the
    /// frame the confessional camera takes: a sign above the top of the shot is a sign nobody sees.
    /// So this waits for the shot to land and projects each piece through that camera.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator DiaryRoom_TheConfessionalWearsItsIdentityInTheShot()
        {
            var palette = HouseBrandingPalette.Current;
            Assert.That(palette, Is.Not.Null, "The branding palette must be built and in Resources.");

            yield return InstallDiaryFixture(
                state => state.Find(state.playerId).status == ContestantStatus.Active,
                "an active player who can sit for a confessional");
            yield return OpenDiaryFixturePanel();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline
                   && !(cameraRig.HasShot && cameraRig.HasArrived(.1f) && !cameraRig.IsTravelling))
                yield return null;
            Assert.That(cameraRig.HasShot, Is.True, "The diary is open and the camera never took its shot.");
            yield return null;

            var studio = SceneComponents<Transform>().Single(t => t.name == "Diary interview backdrop" && t.gameObject.activeInHierarchy);
            Renderer Piece(string name) => studio.GetComponentsInChildren<Renderer>(true).Single(r => r.name == name);
            var halo = Piece(DiaryStudioBranding.HaloName);
            var sign = Piece(DiaryStudioBranding.SignName);
            Assert.That(halo.sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(palette.diaryHalo), "The halo is the pack's diary halo.");
            Assert.That(sign.sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(palette.diaryNeon), "The sign is the pack's diary neon.");
            Assert.That(halo.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000), "Light added to the wall, drawn after it.");
            Assert.That(studio.GetComponentsInChildren<Renderer>(true).Count(r => r.name == DiaryStudioBranding.StripName), Is.EqualTo(2),
                "Two strips, one each side: the confessional is shot square-on.");
            Assert.That(studio.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Set dressing takes no clicks and blocks nothing.");
            var wash = studio.GetComponentsInChildren<Light>(true).SingleOrDefault(l => l.name == DiaryStudioBranding.WashName);
            Assert.That(wash, Is.Not.Null, "The violet wash that lifts the wall out of black.");

            var camera = cameraRig.ViewCamera;
            void InFrame(Renderer piece, string what)
            {
                var b = piece.bounds;
                foreach (var corner in new[] { b.min, b.max, new Vector3(b.min.x, b.max.y, b.center.z), new Vector3(b.max.x, b.min.y, b.center.z) })
                {
                    var seen = camera.WorldToViewportPoint(corner);
                    Assert.That(seen.z > 0f && seen.y > -0.02f && seen.y < 1.02f, Is.True,
                        what + " runs out of the confessional shot at " + seen + ": a sign above the frame is a sign nobody sees.");
                }
            }
            InFrame(sign, "The Diary Room sign");
            var face = camera.WorldToViewportPoint(player.GetComponent<DiarySeatPose>().FacePosition);
            var haloCentre = camera.WorldToViewportPoint(halo.bounds.center);
            Assert.That(Mathf.Abs(face.x - haloCentre.x), Is.LessThan(0.08f),
                "The halo is centred behind the speaker's head, square to the camera.");

            if (Application.isBatchMode) CaptureCamera(camera, "diary-branded");
        }

        /// <summary>Renders <paramref name="camera"/> to a 1280x720 PNG beside Assets, for review.</summary>
        private static void CaptureCamera(Camera camera, string name)
        {
            const int w = 1280, h = 720;
            var target = new RenderTexture(w, h, 24);
            var readback = new Texture2D(w, h, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                readback.Apply();
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", name + ".png"));
                System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Gamesim] " + name + " captured -> " + path);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(readback);
            }
        }
    }
}
