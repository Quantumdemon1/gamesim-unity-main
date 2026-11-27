using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The player's footsteps as they walk the house (<see cref="HouseSteps"/>): a foot lands every
    /// stride the body's speed implies, on the ground of the room under it, louder the nearer the
    /// camera looks. The director tells it each frame where the body is and whether it is on its
    /// feet; it is silent while the player sits, lies, swims or soaks.
    ///
    /// <para>It lives beside <see cref="HouseAudio"/> and follows its settings: the master volume,
    /// the Effects volume, and the game menu's duck on the house's own sounds, as the rooms' beds do;
    /// Mute sound and Reduce sound take the steps away. Reduced motion does not: a sound is not
    /// motion. Two voices take the steps in turn, a foot each, so a step's tail is never cut by the
    /// next or retuned by its pitch.</para>
    ///
    /// <para>Presentation only: nothing here reads or writes the season, and nothing draws from its
    /// random stream.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseFootsteps : MonoBehaviour
    {
        private HouseAudio sound;
        private readonly AudioSource[] voices = new AudioSource[2];
        private int nextVoice;
        private readonly StepClock clock = new StepClock();
        private readonly StepVariations variations = new StepVariations();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly int[] landed = new int[4];
        private Vector3 lastFeet;
        private bool hasFeet;
        private int lastFrame = -2;
        private bool initialized;

        /// <summary>Every foot that has landed, heard or not: what a test counts a walk's steps by.</summary>
        public int StepsTaken { get; private set; }

        /// <summary>The steps played: landed with the settings letting them be heard, near enough to the camera, and a recording to play.</summary>
        public int StepsHeard { get; private set; }

        /// <summary>The steps that have landed on <paramref name="surface"/>.</summary>
        public int StepsOn(StepSurface surface) => landed[(int)surface];

        /// <summary>The take the last step played, by its file's name (<c>Tile_3</c>); null before any.</summary>
        public string LastClip { get; private set; }

        /// <summary>The volume the last step played at, 0 when it was not heard.</summary>
        public float LastVolume { get; private set; }

        /// <summary>The pitch the last step played at.</summary>
        public float LastPitch { get; private set; } = 1f;

        /// <summary>The steps beside the house's audio, made once and kept.</summary>
        public static HouseFootsteps Attach(HouseAudio sound)
        {
            if (sound == null) return null;
            // Explicitly: a missing component is Unity's fake null in the editor, which ?? keeps.
            var steps = sound.GetComponent<HouseFootsteps>();
            if (steps == null) steps = sound.gameObject.AddComponent<HouseFootsteps>();
            steps.sound = sound;
            steps.Initialize();
            return steps;
        }

        /// <summary>Whether the settings let a step be heard: sound on and not reduced, and some master and Effects volume.</summary>
        private bool Hear => sound != null && sound.isActiveAndEnabled && isActiveAndEnabled && !sound.Muted && !sound.ReducedAudio
                             && sound.Volume > 0f && sound.ChannelVolume(SoundChannel.Effects) > 0f;

        private void Initialize()
        {
            if (initialized || !Application.isPlaying) return;
            initialized = true;
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.spatialBlend = 0f;
                // Under the cues and the activity's loop, over the beds and the music.
                voice.priority = 175;
                voice.volume = 0f;
                voices[i] = voice;
            }
            // Decoded now, while the house is put together: their metas do not preload, and a first
            // step would otherwise decode its recording on the main thread in the frame it landed.
            foreach (var clip in Resources.LoadAll<AudioClip>(HouseSteps.ResourceFolder.TrimEnd('/')))
            {
                if (clip == null) continue;
                clips[clip.name] = clip;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }

        /// <summary>
        /// The frame's travel of <paramref name="body"/>, asked once a frame by the director: true
        /// when a foot lands this frame, and the director says where with <see cref="Land"/>. Not
        /// <paramref name="afoot"/> - seated, lying, in the water, behind a setup screen, or no body
        /// at all - the walk stops and nothing lands. A frame the director skipped starts afresh
        /// rather than counting the ground covered since as one frame's walk.
        /// </summary>
        public bool Walk(Transform body, bool running, bool afoot)
        {
            int frame = Time.frameCount;
            if (frame == lastFrame) return false;
            bool skipped = frame - lastFrame != 1;
            lastFrame = frame;
            if (!Hear) Hush();
            if (body == null || !afoot)
            {
                clock.Halt();
                hasFeet = false;
                return false;
            }
            var feet = body.position;
            if (!hasFeet || skipped)
            {
                lastFeet = feet;
                hasFeet = true;
                clock.Halt();
                return false;
            }
            var travel = feet - lastFeet;
            travel.y = 0f;
            lastFeet = feet;
            if (!clock.Advance(travel.magnitude, Time.deltaTime, running)) return false;
            StepsTaken++;
            return true;
        }

        /// <summary>
        /// A foot landing on <paramref name="surface"/>, <paramref name="metresFromFocus"/> from where
        /// the camera looks: the next take of that ground, at its own pitch, at the steps' share of
        /// the Effects volume and the house's duck, fainter with distance. Counted whether or not it
        /// is heard, and the takes and pitches move on either way.
        /// </summary>
        public void Land(StepSurface surface, float metresFromFocus)
        {
            landed[(int)surface]++;
            string name = HouseSteps.ClipName(surface, variations.Take());
            float pitch = variations.Pitch();
            LastClip = name;
            LastPitch = pitch;
            LastVolume = 0f;
            if (!Hear) return;
            float volume = sound.Volume * sound.ChannelVolume(SoundChannel.Effects) * HouseSteps.Gain
                           * HouseSteps.GainAt(metresFromFocus) * sound.BedDuckLevel;
            if (!(volume > 0f)) return;
            Initialize();
            if (!clips.TryGetValue(name, out var clip) || clip == null) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            if (voice == null) return;
            voice.Stop();
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.mute = false;
            voice.Play();
            LastVolume = volume;
            StepsHeard++;
        }

        /// <summary>Any step still sounding stopped at once: the settings have taken the steps away.</summary>
        private void Hush()
        {
            foreach (var voice in voices)
                if (voice != null && voice.isPlaying) voice.Stop();
        }

        private void OnDisable()
        {
            Hush();
            clock.Halt();
            hasFeet = false;
        }

        private void OnDestroy()
        {
            foreach (var voice in voices)
            {
                if (voice == null) continue;
                voice.Stop();
                if (Application.isPlaying) Destroy(voice); else DestroyImmediate(voice);
            }
        }
    }
}
