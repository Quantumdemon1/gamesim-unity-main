using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly string[] VolumeSliderCaptions =
        {
            "Master volume", SoundPictures.MeterCaption(SoundChannel.Music),
            SoundPictures.MeterCaption(SoundChannel.Effects), SoundPictures.MeterCaption(SoundChannel.Interface),
        };

        private Slider VolumeControl(string caption) => director.GetComponentsInChildren<Slider>()
            .Single(slider => slider.gameObject.activeInHierarchy && slider.name == caption + " slider");

        private IEnumerator OpenIsolatedVolumeSettings()
        {
            EpisodeDirector.PreferencesBesideSaves = true;
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.RootFile));
            HoldTheHouseForTheFixture();
            director.OpenSettings();
            yield return Frames(2);
        }

        private IEnumerator ReachVolumeControl(string caption, bool pad)
        {
            for (int step = 0; step < 180 && SelectedName() != caption + " slider"; step++) yield return StepDown(pad);
            Assert.That(SelectedName(), Is.EqualTo(caption + " slider"), "Actual navigation must reach the literal volume slider.");
        }

        private void AssertVolumeCaptionsKept()
        {
            // Presence/identity only: ButtonWithCaption also scrolls, which would move a captured slider out of view.
            var buttons = director.GetComponentsInChildren<Button>(true).Where(button => button.IsActive() && button.IsInteractable()).ToArray();
            void Kept(string caption) => Assert.That(buttons.Any(button => button.name == caption
                && button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text == caption)), Is.True, caption + " retains its control name and words.");
            foreach (string caption in new[] { "Volume down", "Volume up" }) Kept(caption);
            Kept(director.GetComponent<HouseAudio>().Muted ? "Turn sound on" : "Mute sound");
            foreach (var channel in new[] { SoundChannel.Music, SoundChannel.Effects, SoundChannel.Interface })
            {
                Kept(SoundPictures.DownCaption(channel));
                Kept(SoundPictures.UpCaption(channel));
            }
        }

        private void AssertVolumeReadback(string caption, int percent)
        {
            var audio = director.GetComponent<HouseAudio>();
            var slider = VolumeControl(caption);
            Assert.That(slider.value, Is.EqualTo(percent));
            Assert.That(slider.GetComponentsInChildren<TMP_Text>().Any(label => label.text == percent + "%"), Is.True, caption + ": live numeric readback.");
            if (caption == "Master volume") Assert.That(audio.Volume, Is.EqualTo(percent / 100f).Within(1e-5f));
            else
            {
                var channel = new[] { SoundChannel.Music, SoundChannel.Effects, SoundChannel.Interface }
                    .Single(each => SoundPictures.MeterCaption(each) == caption);
                Assert.That(director.ChannelVolumePercent(channel), Is.EqualTo(percent));
                Assert.That(audio.ChannelVolume(channel), Is.EqualTo(percent / 100f).Within(1e-5f));
            }
        }

        [UnityTest]
        public IEnumerator Accessibility_VolumeSlidersAnswerKeyboardAndPadWithoutLosingFocus([Values(false, true)] bool pad)
        {
            yield return OpenIsolatedVolumeSettings();
            var gamepad = pad ? TestGamepad() : null;
            string seasonBefore = JsonUtility.ToJson(director.Snapshot);
            foreach (string caption in VolumeSliderCaptions)
            {
                yield return ReachVolumeControl(caption, pad);
                var slider = VolumeControl(caption);
                Assert.That((slider.minValue, slider.maxValue, slider.wholeNumbers), Is.EqualTo((0f, 100f, true)));
                int before = Mathf.RoundToInt(slider.value);
                var shares = VolumeSliderCaptions.Select(name => Mathf.RoundToInt(VolumeControl(name).value)).ToArray();
                int delta = before == 0 ? 1 : -1;
                if (pad) yield return PressPad(gamepad, delta > 0 ? GamepadButton.DpadRight : GamepadButton.DpadLeft);
                else yield return PressKey(delta > 0 ? Key.RightArrow : Key.LeftArrow);
                Assert.That(VolumeControl(caption), Is.SameAs(slider), "Changing the share must not redraw/destroy an active slider.");
                Assert.That(Selected(), Is.SameAs(slider.gameObject), "Horizontal adjustment stays on the selected slider.");
                AssertVolumeReadback(caption, before + delta);
                for (int index = 0; index < shares.Length; index++)
                    if (VolumeSliderCaptions[index] != caption) AssertVolumeReadback(VolumeSliderCaptions[index], shares[index]);
                if (pad) yield return PressPad(gamepad, delta > 0 ? GamepadButton.DpadLeft : GamepadButton.DpadRight);
                else yield return PressKey(delta > 0 ? Key.LeftArrow : Key.RightArrow);
                AssertVolumeReadback(caption, before);
                if (pad) yield return PressPad(gamepad, GamepadButton.DpadDown);
                else yield return PressKey(Key.Tab);
                Assert.That(Selected(), Is.Not.SameAs(slider.gameObject), "The control can be left without a mouse.");
                Assert.That(Selected().transform.IsChildOf(ActiveRect("Episode panel")), Is.True, "Navigation stays inside settings.");
            }
            AssertVolumeCaptionsKept();
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(seasonBefore), "Preferences are not season commands and consume no season RNG.");
            if (!pad) yield return AssertKeyboardRing("Settings with four literal volume sliders", "Episode panel");
        }

        [UnityTest]
        public IEnumerator Audio_VolumeSliderPointerDragStaysLiveAndAllFourValuesSurviveReload()
        {
            yield return OpenIsolatedVolumeSettings();
            var before = JsonUtility.ToJson(director.Snapshot);
            var expected = new int[VolumeSliderCaptions.Length];
            for (int index = 0; index < VolumeSliderCaptions.Length; index++)
            {
                string caption = VolumeSliderCaptions[index];
                yield return ReachVolumeControl(caption, false);
                var slider = VolumeControl(caption);
                Canvas.ForceUpdateCanvases();
                var area = (RectTransform)slider.handleRect.parent;
                var canvas = slider.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 Point(float fraction) => RectTransformUtility.WorldToScreenPoint(camera,
                    area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, fraction), area.rect.center.y)));
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left, position = Point(.2f),
                    pointerPressRaycast = new RaycastResult { module = slider.GetComponentInParent<GraphicRaycaster>(), gameObject = slider.gameObject },
                };
                ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return null;
                int desired = 40 + index * 17;
                pointer.position = Point(desired / 100f);
                ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
                yield return null;
                ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                Assert.That(VolumeControl(caption), Is.SameAs(slider), "The same control survives a multi-frame pointer drag.");
                expected[index] = Mathf.RoundToInt(slider.value);
                Assert.That(expected[index], Is.EqualTo(desired), "The actual slider's pointer geometry selects the requested share.");
                AssertVolumeReadback(caption, expected[index]);
                slider.value = -10;
                AssertVolumeReadback(caption, 0);
                slider.value = 110;
                AssertVolumeReadback(caption, 100);
                slider.value = expected[index];
            }
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before));
            var stored = PreferenceFile.Read(Path.Combine(temporaryDirectory, PreferenceFile.FileName), out var reason);
            Assert.That(reason, Is.Null.Or.Empty);
            Assert.That(stored.GetInt("Gamesim.Volume", -1), Is.EqualTo(expected[0]));
            Assert.That(stored.GetInt(SoundPictures.MusicVolumeKey, -1), Is.EqualTo(expected[1]));
            Assert.That(stored.GetInt(SoundPictures.EffectsVolumeKey, -1), Is.EqualTo(expected[2]));
            Assert.That(stored.GetInt(SoundPictures.InterfaceVolumeKey, -1), Is.EqualTo(expected[3]));
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            director.OpenSettings();
            yield return Frames(2);
            for (int index = 0; index < expected.Length; index++) AssertVolumeReadback(VolumeSliderCaptions[index], expected[index]);
            AssertVolumeCaptionsKept();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Accessibility_VolumeSlidersHoldAtAllThreeTextSizesAndFourReviewFrames()
        {
            yield return OpenIsolatedVolumeSettings();
            foreach (var textSize in TextSizes.All)
            {
                yield return ApplyTextSize(textSize);
                director.OpenSettings();
                yield return Frames(2);
                for (int index = 0; index < VolumeSliderCaptions.Length; index++)
                {
                    string caption = VolumeSliderCaptions[index];
                    yield return ReachVolumeControl(caption, false);
                    yield return OnFrames("volume-slider-" + index + SizeSuffix(textSize), caption + " at " + TextSizes.Name(textSize) + " text", at =>
                    {
                        AssertVolumeCaptionsKept();
                        var slider = VolumeControl(caption);
                        Assert.That(slider.IsActive() && slider.IsInteractable(), Is.True, at + ": " + caption);
                        var scroll = slider.GetComponentInParent<ScrollRect>();
                        Assert.That(scroll, Is.Not.Null, at + ": settings own the scrollable control.");
                        Assert.That(Encloses(ScreenRect(scroll.viewport), ScreenRect((RectTransform)slider.transform)), Is.True,
                            at + ": the selected whole row is inside the actual viewport.");
                        AssertEveryLabelDraws((RectTransform)slider.transform, at);
                        var labels = slider.GetComponentsInChildren<TMP_Text>();
                        Assert.That(labels, Has.Length.EqualTo(2));
                        Assert.That(ScreenRect(labels[0].rectTransform).Overlaps(ScreenRect(labels[1].rectTransform)), Is.False, at + ": caption and percentage do not overlap.");
                        foreach (var label in labels)
                        {
                            label.ForceMeshUpdate();
                            Assert.That(label.isTextOverflowing || label.isTextTruncated, Is.False, at + ": " + label.text);
                            Assert.That(label.fontSize, Is.EqualTo(Mathf.Round(16 * TextSizes.Scale(textSize))).Within(.01f), at + ": the requested text size is not shrunk.");
                        }
                    }, arrange: () => EventSystem.current.SetSelectedGameObject(VolumeControl(caption).gameObject));
                }
            }
            yield return ApplyTextSize(TextSize.Standard);
        }
    }
}
