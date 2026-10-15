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
            Nomination, Veto, Vote, Eviction, Finale,
            // UI foley (§3.C): a panel opening, a panel closing, a hover over a control.
            PanelOpen, PanelClose, Hover,
            // The tour moving on a step: the reference build's tutorialStep, a glide from 500 to
            // 700 Hz over a tenth of a second (useGameSFX.ts). Appended, so no cue's value moves.
            TutorialStep
        }

        /// <summary>The last cue asked for, muted or not - what a test listens to.</summary>
        public Cue? LastCue { get; private set; }

        /// <summary>How many cues have been asked for, muted or not: what a test counts a double click by.</summary>
        public int CuesAsked { get; private set; }

        private const int SampleRate = 22050;
        private readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        // The clips this component synthesised and therefore owns. A clip loaded from Resources is
        // an asset: it is shared, and destroying it is refused by the engine.
        private readonly HashSet<AudioClip> owned = new HashSet<AudioClip>();
        /// <summary>Where a recorded cue lives: <c>Resources/Audio/Cues/&lt;Cue&gt;</c>.</summary>
        public const string CueResourceFolder = "Audio/Cues/";
        /// <summary>How many of the cues came from a recording rather than the composition.</summary>
        public int RecordedCues { get; private set; }
        /// <summary>
        /// What the music bed is doing.
        ///
        /// <para>The reference's shape, from <c>GameSim-Game-Flow_v2.md</c>: a theme over the opening,
        /// then a season bed that plays until the game is over, and silence on the screens that are
        /// not the game — setup, the opening itself, and the final stats.</para>
        /// </summary>
        public enum Music { Silent, Theme, Season }

        // Two music voices, so the theme can be let go while the season's bed comes up: each has
        // its own source, and a level from 0 to 1 that moves toward its target at the reference
        // build's constant rates (MusicFade). The level is what the voice is doing now; the target is
        // what the requested state wants from it.
        private AudioSource ambienceSource, cueSource, themeSource, seasonSource, roomSource;
        private float themeLevel, themeTarget, seasonLevel, seasonTarget;
        private bool themeRunning;
        private SeasonVoice seasonVoice;

        /// <summary>
        /// The bed's source, in the three states it can be in. Its own bookkeeping rather than
        /// AudioSource.isPlaying, which is false for a paused source and a stopped one alike - and
        /// telling those two apart is the difference between the bed resuming and starting over.
        ///
        /// <para>Bookkeeping can be wrong, though, because the engine can stop a source without
        /// asking: a reset of the audio system stops every source there is. So the bookkeeping is
        /// checked against isPlaying at the three places where believing it would lose the bed -
        /// the reset's own notification (<see cref="RecoverFromAudioReset"/>), a bed asked for
        /// while it is marked playing (<see cref="EnterMusic"/>), and a bed put away
        /// (<see cref="PutSeasonAway"/>) - and a source that is not playing is stopped there, not
        /// paused or playing, whatever the enum said.</para>
        /// </summary>
        private enum SeasonVoice { Stopped, Playing, Paused }

        /// <summary>How loud the theme is now, 0 to 1 of its full level, as its fade has it.</summary>
        public float ThemeLevel => themeLevel;

        /// <summary>How loud the season's bed is now, 0 to 1 of its full level, as its fade has it.</summary>
        public float SeasonLevel => seasonLevel;

        /// <summary>Whether the season's bed is paused where it was, to carry on from there when it comes back.</summary>
        public bool SeasonPaused => seasonVoice == SeasonVoice.Paused;

        /// <summary>Where the season's bed is in its track, in samples: a read, for the tests that check it resumes rather than restarting.</summary>
        public int SeasonSamples => seasonSource != null ? seasonSource.timeSamples : 0;

        /// <summary>The season voice's volume as it is being played: the fade's level at the bed's peak, under the preferences.</summary>
        public float SeasonVolume => seasonSource != null ? seasonSource.volume : 0f;

        /// <summary>Whether the season's bed is sounding as the engine has it - the source's own isPlaying, not the bookkeeping - for the tests that stop it behind the bookkeeping's back.</summary>
        public bool SeasonPlaying => seasonSource != null && seasonSource.isPlaying;

        /// <summary>Whether the theme is sounding as the engine has it: its source's own isPlaying.</summary>
        public bool ThemePlaying => themeSource != null && themeSource.isPlaying;

        /// <summary>
        /// Whether the fades wait for <see cref="TickMusic"/> instead of following the real clock.
        ///
        /// <para>A test's seam. The fades run on unscaled time, so that a paused game still fades
        /// its music, and Time.captureDeltaTime pins only scaled time - there is no other way to
        /// hold a fade still between two assertions and move it by an exact amount.</para>
        /// </summary>
        public bool ManualMusicClock { get; set; }

        private readonly Dictionary<string, AudioClip> roomTones = new Dictionary<string, AudioClip>();
        /// <summary>Where a room's bed lives: <c>Resources/Audio/Rooms/&lt;RoomName&gt;</c>.</summary>
        public const string RoomResourceFolder = "Audio/Rooms/";
        /// <summary>The room whose bed is playing, or null when the generic air is.</summary>
        public string CurrentRoom { get; private set; }
        /// <summary>Whether a room's own bed is playing right now.</summary>
        public bool RoomTonePlaying => roomSource != null && roomSource.isPlaying;
        private AudioClip ambience, themeBed, seasonBed;
        private Music music = Music.Silent;
        private bool initialized, ambienceEnabled = true;
        private float volume = 0.35f;
        public bool Muted { get; private set; }
        public float Volume => volume;
        /// <summary>
        /// Reduced audio (§3.C), the sound's counterpart to reduced motion: the room tone stops, and
        /// the cues and the music play at half the volume. Nothing that carries information goes -
        /// every cue still sounds - it is the bed under it that is taken away.
        /// </summary>
        public bool ReducedAudio { get; private set; }
        /// <summary>The volume the cues are played at, after the preferences.</summary>
        public float CueVolume => volume * (ReducedAudio ? 0.5f : 1f);
        /// <summary>Whether the room tone is playing right now.</summary>
        public bool AmbiencePlaying => ambienceSource != null && ambienceSource.isPlaying;

        public void SetReducedAudio(bool value)
        {
            ReducedAudio = value;
            ApplySettings();
        }

        /// <summary>
        /// The room the camera looks into (§3.C room tone per room). A room with a bed in
        /// Resources plays it and the generic air stops; a room without one, or none, keeps the
        /// air. Asked every time the focus crosses a threshold; the same room again costs nothing.
        /// </summary>
        public void SetRoom(string room)
        {
            if (room == CurrentRoom) return;
            CurrentRoom = room;
            Initialize();
            if (roomSource == null) return;
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(room) && !roomTones.TryGetValue(room, out clip))
            {
                clip = Resources.Load<AudioClip>(RoomResourceFolder + room);
                roomTones[room] = clip;
            }
            if (clip == null) { roomSource.Stop(); roomSource.clip = null; }
            else if (roomSource.clip != clip) { roomSource.clip = clip; roomSource.Play(); }
            ApplySettings();
        }

        private void Awake()
        {
            // The scene's audio enables before the director's delayed Start. Honor the saved
            // mute choice before the first source can play; the director may override it later.
            // The session's store, decided as the director will decide it (A13): an isolated root
            // reads its own file or the defaults, never the machine's PlayerPrefs.
            Muted = Gamesim.Episode.EpisodeDirector.LaunchPreferences().GetInt("Gamesim.Muted", 0) == 1;
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

        /// <summary>
        /// Puts the music bed into a state, idempotently.
        ///
        /// <para>Callers say what is happening rather than what to play — the opening sequence asks
        /// for <see cref="Music.Theme"/>, the house asks for <see cref="Music.Season"/>, and every
        /// screen that is not the game asks for <see cref="Music.Silent"/>. Repeating the state a
        /// caller is already in does nothing, so a screen can assert its own music every time it
        /// renders without restarting the track under the player.</para>
        ///
        /// <para>A change fades rather than cuts, as the reference build's audio hooks do: see
        /// <see cref="EnterMusic"/> for each handover and <see cref="MusicFade"/> for the rates.</para>
        /// </summary>
        public void SetMusic(Music value)
        {
            if (music == value) return;
            music = value;
            EnterMusic();
        }

        /// <summary>The state last asked for - what a caller wants, not what can be heard while a fade is still getting there.</summary>
        public Music CurrentMusic => music;

        /// <summary>
        /// Whether the theme is a recording rather than the synthesised stand-in.
        ///
        /// <para>True in every build today. <c>Resources/Audio/Theme.wav</c> and
        /// <c>Resources/Audio/Season.wav</c> are the project's own renders, made by
        /// <c>ArtSource/audio/bb_music.py</c>, and the chord beds built below are only the fallback for
        /// a build without them. The reference's own <c>bbtheme.mp3</c> and
        /// <c>background_music.mp3</c> are deliberately not used: a file called <c>bbtheme</c> is very
        /// likely somebody else's music, and that is a licensing decision rather than a porting one.
        /// Other recordings supplied under those two names need nothing changed here.</para>
        /// </summary>
        public bool HasRecordedMusic { get; private set; }

        private void Update()
        {
            if (!ManualMusicClock) TickMusicFrame(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// One frame of the real clock, as the fades are credited it: all of an ordinary frame, and
        /// no more than <see cref="MusicFade.LongestStep"/> of a long one.
        ///
        /// <para>Unscaled time is not capped the way scaled time is by maximumDeltaTime, and the
        /// frames either side of a voice starting are the long ones: the loading gate lets the theme
        /// go on the frame the last body is built and the house is placed, and the frame after pays
        /// for whatever the first Play() had to do. Credited in full, those frames would bring the
        /// theme in partway up its 1.5 s rise - a fifth of the way, after a 300 ms frame - where the
        /// reference build takes its first step from the moment its fade begins
        /// (useIntroAudio.ts's lastTime, set inside fade()). A twentieth of a second is a step no
        /// ear notices, and a fade stalled by a hitch loses the difference rather than jumping it.
        /// A test steps exact amounts with <see cref="TickMusic"/>; this is the seam for the clamp.</para>
        /// </summary>
        public void TickMusicFrame(float frameSeconds) => TickMusic(MusicFade.FrameStep(frameSeconds));

        /// <summary>
        /// Moves both fades on by <paramref name="seconds"/>, and lets a voice that has faded all the
        /// way out go: the theme stops and rewinds, as the reference build's does once its fade-out
        /// ends (useIntroAudio.ts), and the season's bed pauses where it is, to carry on from there
        /// when it comes back (useBackgroundMusic.ts). Called once a frame, through
        /// <see cref="TickMusicFrame"/>, on unscaled time; or by a test under
        /// <see cref="ManualMusicClock"/>, with exactly the time it says and no clamp.
        /// </summary>
        public void TickMusic(float seconds)
        {
            if (!initialized) return;
            themeLevel = MusicFade.Theme(themeLevel, themeTarget, seconds);
            seasonLevel = MusicFade.Season(seasonLevel, seasonTarget, seconds);
            ReleaseSilentVoices();
            WriteMusicVolumes();
        }

        /// <summary>
        /// Starts the fades the requested state needs - the reference build's handover in each case.
        ///
        /// <para><b>The theme</b> always starts over, silent and from the top, and rises over 1.5 s:
        /// the reference's lifecycle zeroes its volume and rewinds it before every fade in. Whatever
        /// the season's bed was doing is cut and rewound with it, because a theme means an opening,
        /// and an opening means a season whose bed has not begun.</para>
        ///
        /// <para><b>The season's bed</b> cuts whatever is left of the theme - the reference build's
        /// theme goes with the intro that plays it, at the house entry or on a skip - and rises to
        /// full over 2 s from wherever it is: from silence when it is fresh, from the level a
        /// fade out had reached when it is called back mid-fade, and from the place it paused when it
        /// was put away, which it resumes rather than restarting.</para>
        ///
        /// <para><b>Silence</b> fades out whatever is sounding over 1.5 s.</para>
        /// </summary>
        private void EnterMusic()
        {
            if (!initialized || !isActiveAndEnabled || themeSource == null || seasonSource == null) return;
            switch (music)
            {
                case Music.Theme:
                    CutSeason();
                    themeSource.Stop();
                    themeSource.clip = themeBed;
                    themeLevel = 0f;
                    themeTarget = 1f;
                    themeSource.volume = 0f;
                    themeSource.Play();
                    themeRunning = true;
                    break;
                case Music.Season:
                    CutTheme();
                    seasonTarget = 1f;
                    // Marked playing but not playing: a reset stopped the source under the
                    // bookkeeping. It is a stopped bed, and starts again as one, or asking for the
                    // bed would leave it silent because the enum said it was already there.
                    if (seasonVoice == SeasonVoice.Playing && !seasonSource.isPlaying) seasonVoice = SeasonVoice.Stopped;
                    if (seasonVoice == SeasonVoice.Paused) seasonSource.UnPause();
                    else if (seasonVoice == SeasonVoice.Stopped)
                    {
                        seasonSource.clip = seasonBed;
                        seasonSource.volume = 0f;
                        seasonSource.Play();
                    }
                    seasonVoice = SeasonVoice.Playing;
                    break;
                default:
                    themeTarget = 0f;
                    seasonTarget = 0f;
                    break;
            }
            // A voice asked for silence while already silent lets go now, not a frame later: the
            // start-up flips the state twice in one frame, and nothing it started should linger.
            ReleaseSilentVoices();
            WriteMusicVolumes();
        }

        private void ReleaseSilentVoices()
        {
            if (themeRunning && themeTarget <= 0f && themeLevel <= 0f) CutTheme();
            if (seasonVoice == SeasonVoice.Playing && seasonTarget <= 0f && seasonLevel <= 0f) PutSeasonAway();
        }

        /// <summary>
        /// The bed put away where it is, to carry on from there when it comes back - if it is
        /// playing. A source the engine has stopped behind the bookkeeping (an audio reset) has no
        /// place to keep, and UnPause on a stopped source plays nothing, so marking it paused would
        /// keep "Turn music on" silent for the rest of the season. It is marked stopped instead, and
        /// comes back from the top. Asked before the Pause(), which makes isPlaying false either way.
        /// </summary>
        private void PutSeasonAway()
        {
            if (seasonSource == null) return;
            if (seasonSource.isPlaying)
            {
                seasonSource.Pause();
                seasonVoice = SeasonVoice.Paused;
            }
            else
            {
                seasonSource.Stop();
                seasonVoice = SeasonVoice.Stopped;
            }
        }

        /// <summary>
        /// Puts back what an audio reset took away. Unity resets its audio system when
        /// AudioSettings.Reset is called and, on its own, when the output device changes - headphones
        /// unplugged, a Bluetooth headset reconnecting - and a reset stops the sources that were
        /// playing; AudioSettings.OnAudioConfigurationChanged says so afterwards, and its documented
        /// answer is to Play() again. Subscribed while the component is enabled.
        ///
        /// <para>The music's bookkeeping would otherwise go on saying the voices play: nothing asks
        /// for a state that has not changed, so a stopped bed stayed silent until the next season's
        /// theme. Here the theme starts again from the top - a theme always starts from the top -
        /// and a playing bed starts again (a reset has already lost its place). A paused bed is
        /// marked stopped, because the reset lost its place too, and UnPause on a stopped source
        /// plays nothing: "Turn music on" brings it back from the top. Each keeps the level its fade
        /// had reached, and the fades carry on. The ambience and a room's bed are stopped and handed
        /// to <see cref="ApplySettings"/>, which starts whichever the preferences want.</para>
        ///
        /// <para>Public so a test can deliver the notification without resetting the audio of every
        /// test after it.</para>
        /// </summary>
        public void RecoverFromAudioReset()
        {
            if (!initialized || !isActiveAndEnabled) return;
            if (themeRunning && themeSource != null) themeSource.Play();
            if (seasonSource != null)
            {
                if (seasonVoice == SeasonVoice.Playing) seasonSource.Play();
                else if (seasonVoice == SeasonVoice.Paused)
                {
                    seasonSource.Stop();
                    seasonVoice = SeasonVoice.Stopped;
                }
            }
            if (ambienceSource != null) ambienceSource.Stop();
            if (roomSource != null) roomSource.Stop();
            ApplySettings();
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged) => RecoverFromAudioReset();

        /// <summary>The theme off at once and rewound.</summary>
        private void CutTheme()
        {
            themeLevel = themeTarget = 0f;
            themeRunning = false;
            if (themeSource == null) return;
            themeSource.Stop();
            themeSource.volume = 0f;
        }

        /// <summary>The season's bed off at once and rewound, for a season that has not begun.</summary>
        private void CutSeason()
        {
            seasonLevel = seasonTarget = 0f;
            seasonVoice = SeasonVoice.Stopped;
            if (seasonSource == null) return;
            seasonSource.Stop();
            seasonSource.volume = 0f;
        }

        /// <summary>
        /// Each voice's volume: its fade's level at its peak, under the preferences.
        ///
        /// <para>Written every frame and on every change of preference, so the master volume and
        /// reduced audio reach a fade in progress at once - the reference build reads its master
        /// volume only when a fade begins - and "Mute sound" silences the music with everything else,
        /// where the reference's sound switch leaves its music playing.</para>
        /// </summary>
        private void WriteMusicVolumes()
        {
            float preferences = volume * (ReducedAudio ? 0.5f : 1f);
            if (themeSource != null)
            {
                themeSource.mute = Muted;
                themeSource.volume = preferences * MusicFade.ThemePeak * themeLevel;
            }
            if (seasonSource != null)
            {
                seasonSource.mute = Muted;
                seasonSource.volume = preferences * MusicFade.SeasonPeak * seasonLevel;
            }
        }

        public void PlayCue(Cue cue)
        {
            LastCue = cue;
            CuesAsked++;
            if (!isActiveAndEnabled || Muted || volume <= 0f) return;
            Initialize();
            if (clips.TryGetValue(cue, out var clip)) cueSource.PlayOneShot(clip);
        }

        private void OnEnable()
        {
            if (Application.isPlaying) Initialize();
            // Told when the engine resets its audio, which stops the sources behind this
            // component's bookkeeping; OnDisable unsubscribes.
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            ApplySettings();
            // Enabled again, the music picks the requested state back up: OnDisable let it go.
            EnterMusic();
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
            themeSource = MusicVoice();
            seasonSource = MusicVoice();

            // A supplied recording wins over the synthesised bed, and neither is required.
            var recordedTheme = Resources.Load<AudioClip>("Audio/Theme");
            var recordedSeason = Resources.Load<AudioClip>("Audio/Season");
            themeBed = recordedTheme != null ? recordedTheme : Own(BuildBed("Theme bed", ThemeChords, 1.5f));
            seasonBed = recordedSeason != null ? recordedSeason : Own(BuildBed("Season bed", SeasonChords, 2.4f));
            HasRecordedMusic = recordedTheme != null;
            // Decoded here, while the house is still being put together, rather than by the first
            // Play(). Neither recording preloads (their metas turn preloadAudioData off) or loads in
            // the background, so the first Play() decompressed the whole track on the main thread -
            // 16 s of theme, 45 s of bed - and stalled the very frame its fade began on.
            Preload(themeBed);
            Preload(seasonBed);

            ambience = Own(BuildAmbience());
            ambienceSource.clip = ambience;
            roomSource = gameObject.AddComponent<AudioSource>();
            roomSource.playOnAwake = false;
            roomSource.loop = true;
            roomSource.spatialBlend = 0f;
            roomSource.priority = 190;
            clips[Cue.Button] = Recorded(Cue.Button) ?? Own(Compose("Button", 0.07f, new Tone(600, 0.06f, 0, 0.11f)));
            clips[Cue.Save] = Recorded(Cue.Save) ?? Own(Compose("Save", 0.24f, new Tone(1000, 0.08f, 0, 0.12f), new Tone(1200, 0.10f, 0.10f, 0.12f)));
            clips[Cue.SocialUp] = Recorded(Cue.SocialUp) ?? Own(Compose("Connection", 0.33f, new Tone(330, 0.18f, 0, 0.13f), new Tone(523.25f, 0.22f, 0.10f, 0.13f)));
            clips[Cue.SocialDown] = Recorded(Cue.SocialDown) ?? Own(Compose("Tension", 0.33f, new Tone(523.25f, 0.18f, 0, 0.12f), new Tone(330, 0.22f, 0.10f, 0.12f)));
            clips[Cue.CompetitionStart] = Recorded(Cue.CompetitionStart) ?? Own(Compose("Competition begins", 0.65f, new Tone(220, 0.6f, 0, 0.15f), new Tone(330, 0.55f, 0.05f, 0.10f)));
            clips[Cue.CompetitionWin] = Recorded(Cue.CompetitionWin) ?? Own(Compose("Competition result", 1.1f,
                new Tone(523.25f, 0.3f, 0, 0.14f), new Tone(659.25f, 0.3f, 0.23f, 0.14f),
                new Tone(783.99f, 0.45f, 0.46f, 0.14f), new Tone(1046.5f, 0.35f, 0.72f, 0.10f)));
            clips[Cue.Nomination] = Recorded(Cue.Nomination) ?? Own(Compose("Nomination", 0.85f, new Tone(110, 0.75f, 0, 0.16f), new Tone(220, 0.5f, 0.2f, 0.07f)));
            clips[Cue.Veto] = Recorded(Cue.Veto) ?? Own(Compose("Veto result", 0.75f,
                new Tone(587.33f, 0.28f, 0, 0.13f), new Tone(739.99f, 0.3f, 0.18f, 0.13f), new Tone(880, 0.36f, 0.37f, 0.12f)));
            clips[Cue.Vote] = Recorded(Cue.Vote) ?? Own(Compose("Vote recorded", 0.20f, new Tone(880, 0.18f, 0, 0.11f)));
            clips[Cue.Eviction] = Recorded(Cue.Eviction) ?? Own(Compose("Eviction", 0.95f, new Tone(82.41f, 0.85f, 0, 0.15f), new Tone(164.81f, 0.6f, 0.12f, 0.07f)));
            clips[Cue.PanelOpen] = Recorded(Cue.PanelOpen) ?? Own(Compose("Panel opens", 0.28f, new Tone(520, 0.12f, 0, 0.08f), new Tone(780, 0.14f, 0.1f, 0.08f)));
            clips[Cue.PanelClose] = Recorded(Cue.PanelClose) ?? Own(Compose("Panel closes", 0.24f, new Tone(760, 0.1f, 0, 0.08f), new Tone(460, 0.12f, 0.09f, 0.08f)));
            clips[Cue.Hover] = Recorded(Cue.Hover) ?? Own(Compose("Hover", 0.09f, new Tone(1800, 0.07f, 0, 0.05f)));
            clips[Cue.TutorialStep] = Recorded(Cue.TutorialStep) ?? Own(Compose("Tutorial step", 0.1f, new Tone(500, 0.1f, 0, 0.15f, 700)));
            clips[Cue.Finale] = Recorded(Cue.Finale) ?? Own(Compose("Final result", 1.5f,
                new Tone(523.25f, 1.35f, 0, 0.10f), new Tone(659.25f, 1.25f, 0.10f, 0.09f),
                new Tone(783.99f, 1.15f, 0.20f, 0.09f), new Tone(1046.5f, 0.75f, 0.55f, 0.065f)));
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (!initialized) return;
            if (ambienceSource != null)
            {
                ambienceSource.mute = Muted;
                ambienceSource.volume = volume;
                // A room's own bed replaces the generic air while it plays.
                bool roomBed = roomSource != null && roomSource.clip != null;
                if (isActiveAndEnabled && ambienceEnabled && !ReducedAudio && !roomBed)
                {
                    if (!ambienceSource.isPlaying) ambienceSource.Play();
                }
                else ambienceSource.Stop();
            }
            if (cueSource != null)
            {
                cueSource.mute = Muted;
                cueSource.volume = CueVolume;
            }
            if (roomSource != null)
            {
                roomSource.mute = Muted;
                roomSource.volume = volume * 0.7f;
                bool wanted = roomSource.clip != null && isActiveAndEnabled && ambienceEnabled && !ReducedAudio;
                if (wanted && !roomSource.isPlaying) roomSource.Play();
                else if (!wanted && roomSource.isPlaying) roomSource.Stop();
            }
            // The music's volumes, and only its volumes. Starting, stopping and fading the voices
            // belongs to EnterMusic and TickMusic, so a preference changed - or a room crossed -
            // in the middle of a fade cannot jump it, restart it or cut it.
            WriteMusicVolumes();
        }

        /// <summary>
        /// A looping music voice. Under the cues and the ambience on purpose, in priority and in
        /// gain: music is the thing that gets out of the way when the house has something to say,
        /// and a bed that competes with the eviction sting is not a bed.
        /// </summary>
        private AudioSource MusicVoice()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 200;
            source.volume = 0f;
            return source;
        }

        /// <summary>The theme: brighter, shorter, and it opens on the tonic so it reads as a fanfare.</summary>
        private static readonly float[][] ThemeChords =
        {
            new[] { 261.63f, 329.63f, 392.00f },   // C
            new[] { 349.23f, 440.00f, 523.25f },   // F
            new[] { 392.00f, 493.88f, 587.33f },   // G
            new[] { 261.63f, 329.63f, 392.00f },   // C
        };

        /// <summary>The season bed: minor, slower, and it never resolves. It plays for hours.</summary>
        private static readonly float[][] SeasonChords =
        {
            new[] { 220.00f, 261.63f, 329.63f },   // Am
            new[] { 174.61f, 220.00f, 261.63f },   // F
            new[] { 196.00f, 246.94f, 293.66f },   // G
            new[] { 164.81f, 196.00f, 246.94f },   // Em
        };

        /// <summary>
        /// A looping chord bed, synthesised.
        ///
        /// <para>A stand-in, and audibly one — but a stand-in that holds the shape the reference
        /// describes, so the opening has something to follow and the season has something under it.
        /// Each chord fades in and out of the next so the loop point is not a click.</para>
        /// </summary>
        private static AudioClip BuildBed(string name, float[][] chords, float secondsPerChord)
        {
            int perChord = Mathf.RoundToInt(SampleRate * secondsPerChord);
            var data = new float[perChord * chords.Length];
            for (int c = 0; c < chords.Length; c++)
                for (int i = 0; i < perChord; i++)
                {
                    float t = i / (float)SampleRate;
                    float phase = i / (float)perChord;
                    // Raised sine over the whole chord: silent at both ends, so chords cross-fade
                    // into each other and the wrap from last to first is seamless.
                    float envelope = 0.5f * (1f - Mathf.Cos(phase * 2f * Mathf.PI));
                    float sample = 0f;
                    foreach (var frequency in chords[c])
                        sample += Mathf.Sin(2f * Mathf.PI * frequency * t);
                    data[c * perChord + i] = sample / chords[c].Length * envelope * 0.25f;
                }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>The recorded cue for <paramref name="cue"/> when the project ships one, or null.</summary>
        private AudioClip Recorded(Cue cue)
        {
            var clip = Resources.Load<AudioClip>(CueResourceFolder + cue);
            if (clip != null) RecordedCues++;
            return clip;
        }

        private AudioClip Own(AudioClip clip)
        {
            if (clip != null) owned.Add(clip);
            return clip;
        }

        /// <summary>A clip's audio data loaded now, if it is not yet; a synthesised clip always is.</summary>
        private static void Preload(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        /// <summary>
        /// One voice of a composed cue: a sine at <c>frequency</c>, or a glide from it to
        /// <c>endFrequency</c> over its duration when one is given - the reference build's sweeps.
        /// </summary>
        private readonly struct Tone
        {
            public readonly float frequency, duration, start, gain, endFrequency;
            public Tone(float frequency, float duration, float start, float gain, float endFrequency = 0f)
            {
                this.frequency = frequency; this.duration = duration; this.start = start; this.gain = gain;
                this.endFrequency = endFrequency > 0f ? endFrequency : frequency;
            }
        }

        private static AudioClip Compose(string name, float duration, params Tone[] tones)
        {
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            foreach (var tone in tones)
            {
                int start = Mathf.RoundToInt(tone.start * SampleRate);
                int count = Mathf.Min(Mathf.CeilToInt(tone.duration * SampleRate), samples.Length - start);
                // A glide moves in a straight line in frequency, as the reference's
                // linearRampToValueAtTime does. The phase is that line's integral, so the pitch slides
                // without a click; a fixed tone's glide is zero and its phase is what it always was.
                float glide = (tone.endFrequency - tone.frequency) / tone.duration;
                for (int index = 0; index < count; index++)
                {
                    float time = index / (float)SampleRate;
                    float attack = Mathf.Clamp01(time / 0.015f);
                    float release = Mathf.Clamp01((tone.duration - time) / 0.035f);
                    float envelope = attack * release * Mathf.Exp(-3.5f * time / tone.duration);
                    float cycles = time * tone.frequency + 0.5f * glide * time * time;
                    samples[start + index] += Mathf.Sin(cycles * Mathf.PI * 2f) * tone.gain * envelope;
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
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            if (ambienceSource != null) ambienceSource.Stop();
            if (cueSource != null) cueSource.Stop();
            // The music too. A disabled component ticks no fades, and the bed used to play on under
            // one with nothing left to fade it or stop it. The theme goes; the bed keeps its place -
            // when it has one - and OnEnable brings back whatever is asked for then.
            CutTheme();
            if (seasonVoice == SeasonVoice.Playing) PutSeasonAway();
            seasonLevel = seasonTarget = 0f;
            WriteMusicVolumes();
        }

        private void OnDestroy()
        {
            if (ambienceSource != null) { ambienceSource.Stop(); Release(ambienceSource); }
            if (cueSource != null) { cueSource.Stop(); Release(cueSource); }
            if (roomSource != null) { roomSource.Stop(); Release(roomSource); }
            if (themeSource != null) { themeSource.Stop(); Release(themeSource); }
            if (seasonSource != null) { seasonSource.Stop(); Release(seasonSource); }
            foreach (var clip in owned) if (clip != null) Release(clip);
            owned.Clear();
            clips.Clear();
        }

        private static void Release(Object owned)
        {
            if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
        }
    }
}
