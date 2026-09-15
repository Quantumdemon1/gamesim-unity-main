using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>Locally synthesized ambience and restrained cues adapted from web useGameSFX.ts.</summary>
    [DisallowMultipleComponent]
    public sealed class HouseAudio : MonoBehaviour
    {
        public enum Cue
        {
            Button, Save, SocialUp, SocialDown, CompetitionStart, CompetitionWin,
            Nomination, Veto, Vote, Eviction, Finale
        }

        private const int SampleRate = 22050;
        private readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        private AudioSource ambienceSource, cueSource;
        private AudioClip ambience;
        private bool initialized, ambienceEnabled = true;
        private float volume = 0.35f;
        public bool Muted { get; private set; }
        public float Volume => volume;

        private void Awake()
        {
            // The scene's audio enables before the director's delayed Start. Honor the saved
            // mute choice before the first source can play; the director may override it later.
            Muted = PlayerPrefs.GetInt("Gamesim.Muted", 0) == 1;
        }

        public static HouseAudio Attach(GameObject root)
        {
            if (root == null) return null;
            var component = root.GetComponent<HouseAudio>();
            if (component == null) component = root.AddComponent<HouseAudio>();
            component.enabled = true;
            component.Initialize();
            return component;
        }

        public void SetMuted(bool value)
        {
            Muted = value;
            ApplySettings();
        }

        public void SetVolume(float value)
        {
            volume = float.IsNaN(value) ? 0.35f : Mathf.Clamp01(value);
            ApplySettings();
        }

        public void SetAmbienceEnabled(bool value)
        {
            ambienceEnabled = value;
            ApplySettings();
        }

        public void PlayCue(Cue cue)
        {
            if (!isActiveAndEnabled || Muted || volume <= 0f) return;
            Initialize();
            if (clips.TryGetValue(cue, out var clip)) cueSource.PlayOneShot(clip);
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Initialize();
            ApplySettings();
        }

        private void Initialize()
        {
            if (initialized || !Application.isPlaying) return;
            initialized = true;
            // Own two new sources; do not edit another component's audio or global listener volume.
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.priority = 180;
            cueSource = gameObject.AddComponent<AudioSource>();
            cueSource.playOnAwake = false;
            cueSource.spatialBlend = 0f;
            cueSource.priority = 80;
            ambience = BuildAmbience();
            ambienceSource.clip = ambience;
            clips[Cue.Button] = Compose("Button", 0.07f, new Tone(600, 0.06f, 0, 0.11f));
            clips[Cue.Save] = Compose("Save", 0.24f, new Tone(1000, 0.08f, 0, 0.12f), new Tone(1200, 0.10f, 0.10f, 0.12f));
            clips[Cue.SocialUp] = Compose("Connection", 0.33f, new Tone(330, 0.18f, 0, 0.13f), new Tone(523.25f, 0.22f, 0.10f, 0.13f));
            clips[Cue.SocialDown] = Compose("Tension", 0.33f, new Tone(523.25f, 0.18f, 0, 0.12f), new Tone(330, 0.22f, 0.10f, 0.12f));
            clips[Cue.CompetitionStart] = Compose("Competition begins", 0.65f, new Tone(220, 0.6f, 0, 0.15f), new Tone(330, 0.55f, 0.05f, 0.10f));
            clips[Cue.CompetitionWin] = Compose("Competition result", 1.1f,
                new Tone(523.25f, 0.3f, 0, 0.14f), new Tone(659.25f, 0.3f, 0.23f, 0.14f),
                new Tone(783.99f, 0.45f, 0.46f, 0.14f), new Tone(1046.5f, 0.35f, 0.72f, 0.10f));
            clips[Cue.Nomination] = Compose("Nomination", 0.85f, new Tone(110, 0.75f, 0, 0.16f), new Tone(220, 0.5f, 0.2f, 0.07f));
            clips[Cue.Veto] = Compose("Veto result", 0.75f,
                new Tone(587.33f, 0.28f, 0, 0.13f), new Tone(739.99f, 0.3f, 0.18f, 0.13f), new Tone(880, 0.36f, 0.37f, 0.12f));
            clips[Cue.Vote] = Compose("Vote recorded", 0.20f, new Tone(880, 0.18f, 0, 0.11f));
            clips[Cue.Eviction] = Compose("Eviction", 0.95f, new Tone(82.41f, 0.85f, 0, 0.15f), new Tone(164.81f, 0.6f, 0.12f, 0.07f));
            clips[Cue.Finale] = Compose("Final result", 1.5f,
                new Tone(523.25f, 1.35f, 0, 0.10f), new Tone(659.25f, 1.25f, 0.10f, 0.09f),
                new Tone(783.99f, 1.15f, 0.20f, 0.09f), new Tone(1046.5f, 0.75f, 0.55f, 0.065f));
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (!initialized) return;
            if (ambienceSource != null)
            {
                ambienceSource.mute = Muted;
                ambienceSource.volume = volume;
                if (isActiveAndEnabled && ambienceEnabled)
                {
                    if (!ambienceSource.isPlaying) ambienceSource.Play();
                }
                else ambienceSource.Stop();
            }
            if (cueSource != null)
            {
                cueSource.mute = Muted;
                cueSource.volume = volume;
            }
        }

        private readonly struct Tone
        {
            public readonly float frequency, duration, start, gain;
            public Tone(float frequency, float duration, float start, float gain)
            {
                this.frequency = frequency; this.duration = duration; this.start = start; this.gain = gain;
            }
        }

        private static AudioClip Compose(string name, float duration, params Tone[] tones)
        {
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            foreach (var tone in tones)
            {
                int start = Mathf.RoundToInt(tone.start * SampleRate);
                int count = Mathf.Min(Mathf.CeilToInt(tone.duration * SampleRate), samples.Length - start);
                for (int index = 0; index < count; index++)
                {
                    float time = index / (float)SampleRate;
                    float attack = Mathf.Clamp01(time / 0.015f);
                    float release = Mathf.Clamp01((tone.duration - time) / 0.035f);
                    float envelope = attack * release * Mathf.Exp(-3.5f * time / tone.duration);
                    samples[start + index] += Mathf.Sin(time * tone.frequency * Mathf.PI * 2f) * tone.gain * envelope;
                }
            }
            for (int index = 0; index < samples.Length; index++) samples[index] = Mathf.Clamp(samples[index], -0.65f, 0.65f);
            var clip = AudioClip.Create("Gamesim " + name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildAmbience()
        {
            const int duration = 8;
            var samples = new float[duration * SampleRate];
            uint noise = 0x47534D31u;
            float filtered = 0f;
            for (int index = 0; index < samples.Length; index++)
            {
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                float random = noise / (float)uint.MaxValue * 2f - 1f;
                filtered += (random - filtered) * 0.012f;
                float time = index / (float)SampleRate;
                float edge = Mathf.Min(Mathf.Clamp01(time / 0.35f), Mathf.Clamp01((duration - time) / 0.35f));
                // Quiet interior air and an unobtrusive low room tone, with a click-free loop seam.
                samples[index] = (filtered * 0.045f + Mathf.Sin(time * 120f * Mathf.PI * 2f) * 0.003f) * edge;
            }
            var clip = AudioClip.Create("Gamesim house room tone", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDisable()
        {
            if (ambienceSource != null) ambienceSource.Stop();
            if (cueSource != null) cueSource.Stop();
        }

        private void OnDestroy()
        {
            if (ambienceSource != null) { ambienceSource.Stop(); Release(ambienceSource); }
            if (cueSource != null) { cueSource.Stop(); Release(cueSource); }
            if (ambience != null) Release(ambience);
            foreach (var clip in clips.Values) if (clip != null) Release(clip);
            clips.Clear();
        }

        private static void Release(Object owned)
        {
            if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
        }
    }
}
