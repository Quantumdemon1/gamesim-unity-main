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
            TutorialStep,
            // The front door (AU-06): its leaves giving after the rattle, and landing shut. The
            // director plays them as the door set says its leaves moved (OpeningDoorSet). Appended.
            DoorOpen, DoorShut,
            // The chime that calls the house to a staged ceremony (AU-07), with the summons line on
            // the strip. Appended.
            Summons,
            // A competition's count (AU-08): each step of the 3-2-1 before GO and each of the attempt's
            // last five seconds, and an early press on the reaction board. The game's news on the
            // effects' volume, where the pointer's tick used to stand in for them. Appended.
            CountTick, FalseStart,
            // A staged competition watched in the yard (AU-13): the start horn (and time called), a
            // console locking an answer in (and a finish), and a podium reveal's right and wrong.
            // The director plays them from the show's beats (CompetitionShowSounds). Appended.
            ShowHorn, ShowLockIn, ShowRight, ShowWrong
        }

        /// <summary>The last cue asked for, muted or not - what a test listens to.</summary>
        public Cue? LastCue { get; private set; }

        /// <summary>How many cues have been asked for, muted or not: what a test counts a double click by.</summary>
        public int CuesAsked { get; private set; }

        // Ballots can catch up in one UI frame. Every reveal still reaches the caption and board.
        private int lastVoteVoiceFrame = -1;
        public int CueVoicesStarted { get; private set; }
        // When each spaced cue last sounded, on the real clock (SoundPictures.Row.Spacing).
        private readonly Dictionary<Cue, float> lastSounded = new Dictionary<Cue, float>();

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
        ///
        /// <para>One departure from it: <see cref="Competition"/>, a competition's own driving loop,
        /// from the attempt opening or the show starting to the result card. The season's bed steps
        /// aside for it and carries on from where it paused once the card comes.</para>
        ///
        /// <para>And a second: <see cref="Title"/>, a quiet loop made from the theme, on the main
        /// menu (and the Settings it opens) only. Cast select and the creator stay silent.</para>
        /// </summary>
        public enum Music { Silent, Theme, Season, Competition, Title }

        // Two music voices, so the theme can be let go while the season's bed comes up: each has
        // its own source, and a level from 0 to 1 that moves toward its target at the reference
        // build's constant rates (MusicFade). The level is what the voice is doing now; the target is
        // what the requested state wants from it.
        private AudioSource cueSource, themeSource, seasonSource;
        private float themeLevel, themeTarget, seasonLevel, seasonTarget;
        private bool themeRunning;
        private SeasonVoice seasonVoice;

        // A third, for a competition's loop: it starts from the top each time, and the season's bed
        // pauses under it rather than stopping, so the bed carries on where it was after the card.
        private AudioSource competitionSource;
        private float competitionLevel, competitionTarget;
        private bool competitionRunning;
        private AudioClip competitionBed;

        // A fourth, for the main menu's title loop: it starts from the top each time the menu asks
        // for it, and whatever else was playing falls away under it as it does under silence.
        private AudioSource titleSource;
        private float titleLevel, titleTarget;
        private bool titleRunning;
        private AudioClip titleBed;

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

        // The ducks (MusicDuck): the music and the rooms' beds held down while the game menu card is
        // open, and the music under the big moments. A share beside each voice's own fade, on the
        // music's clock; the cues have none.
        private readonly MusicDuck musicDuck = new MusicDuck();
        private readonly MusicDuck bedDuck = new MusicDuck();

        /// <summary>The music's duck now: the share of its level both music voices play at, 1 when nothing holds it down.</summary>
        public float MusicDuckLevel => musicDuck.Level;

        /// <summary>The rooms' beds' duck now, 1 when nothing holds them down.</summary>
        public float BedDuckLevel => bedDuck.Level;

        /// <summary>
        /// The levels the music and the rooms' beds should be ducked to, each 0 to 1 (1 is not
        /// ducked): the director asks every frame, from what is on screen. Each duck eases there on
        /// the music's clock (<see cref="MusicFade.Duck"/>), so asking again changes nothing, and the
        /// music's state (<see cref="SetMusic"/>) is not touched.
        /// </summary>
        public void SetDuck(float music, float beds)
        {
            musicDuck.Ask(music);
            bedDuck.Ask(beds);
        }

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

        /// <summary>How loud a competition's loop is now, 0 to 1 of its full level, as its fade has it.</summary>
        public float CompetitionLevel => competitionLevel;

        /// <summary>The competition voice's volume as it is being played: the fade's level at the loop's peak, under the preferences and the duck.</summary>
        public float CompetitionVolume => competitionSource != null ? competitionSource.volume : 0f;

        /// <summary>Whether a competition's loop is sounding as the engine has it: its source's own isPlaying.</summary>
        public bool CompetitionPlaying => competitionSource != null && competitionSource.isPlaying;

        /// <summary>Whether the competitions' loop is the project's recording (<c>Resources/Audio/Competition</c>) rather than the synthesized stand-in.</summary>
        public bool HasRecordedCompetition { get; private set; }

        /// <summary>How loud the main menu's title loop is now, 0 to 1 of its full level, as its fade has it.</summary>
        public float TitleLevel => titleLevel;

        /// <summary>The title voice's volume as it is being played: the fade's level at the loop's peak, under the preferences and the duck.</summary>
        public float TitleVolume => titleSource != null ? titleSource.volume : 0f;

        /// <summary>Whether the title loop is sounding as the engine has it: its source's own isPlaying.</summary>
        public bool TitlePlaying => titleSource != null && titleSource.isPlaying;

        /// <summary>Whether the title loop is the project's recording (<c>Resources/Audio/Title</c>) rather than the synthesized stand-in.</summary>
        public bool HasRecordedTitle { get; private set; }

        /// <summary>Whether the beds - the house's air and the rooms' - are switched on: off behind the front door's screens (<see cref="SetAmbienceEnabled"/>).</summary>
        public bool AmbienceEnabled => ambienceEnabled;

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
        /// <summary>
        /// Whether a room's own bed is playing right now: the bed asked for, from the moment its
        /// room is entered and while it fades in. The bed of a room just left, fading out under it,
        /// does not count.
        /// </summary>
        public bool RoomTonePlaying => CurrentBedPlaying(room: true);

        // The beds (§3.C room tone per room): two voices, so a room crossed - the generic air
        // included - is a crossfade rather than a cut (RoomToneBlend). Each bed keeps its place in
        // its loop when its voice lets it go, and comes back from there.
        private readonly RoomToneBlend beds = new RoomToneBlend();
        private readonly AudioSource[] bedVoices = new AudioSource[RoomToneBlend.Voices];
        private readonly Dictionary<AudioClip, int> bedPlaces = new Dictionary<AudioClip, int>();

        /// <summary>How many bed voices are sounding: two while a crossfade runs, one otherwise, none when the beds are off.</summary>
        public int BedVoicesSounding
        {
            get
            {
                int sounding = 0;
                foreach (var voice in bedVoices) if (voice != null && voice.isPlaying) sounding++;
                return sounding;
            }
        }

        /// <summary>How loud a room's bed is now, 0 to 1 of its full level, as its fade has it; null is the generic air.</summary>
        public float BedLevel(string room) => beds.LevelOf(room ?? RoomToneBlend.Air);

        /// <summary>Whether the preferences let the beds be heard: switched on, not reduced or muted, and some volume.</summary>
        private bool HearBeds => isActiveAndEnabled && ambienceEnabled && !ReducedAudio && !Muted && volume > 0f && effectsShare > 0f;

        private bool CurrentBedPlaying(bool room)
        {
            int voice = beds.Current;
            if (voice < 0 || bedVoices[voice] == null || !bedVoices[voice].isPlaying) return false;
            return room == (beds.KeyOf(voice) != RoomToneBlend.Air);
        }

        // The player's activity (ActivitySounds): one voice for the loop of what they are doing - the
        // sizzle at the hob, the strokes in the pool, the dance's beat - under the effects volume and
        // the beds' duck, fading in as the activity gets under way and out as they get up (ActivityLoop).
        private readonly ActivityLoop activity = new ActivityLoop();
        private AudioSource activityVoice;
        private string activityVoiceKey;
        private readonly Dictionary<string, AudioClip> activityClips = new Dictionary<string, AudioClip>();

        /// <summary>The loop the activity voice is sounding, by its activity's name, or null while it is silent.</summary>
        public string ActivitySounding => ActivityPlaying ? activityVoiceKey : null;

        /// <summary>Whether the activity voice is sounding as the engine has it: its source's own isPlaying.</summary>
        public bool ActivityPlaying => activityVoice != null && activityVoice.isPlaying;

        /// <summary>How loud the activity's loop is now, 0 to 1 of its full level, as its fade has it.</summary>
        public float ActivityLevel => activity.Level;

        /// <summary>The activity voice's volume as it is being played.</summary>
        public float ActivityVolume => activityVoice != null ? activityVoice.volume : 0f;

        /// <summary>
        /// Whether the season's music steps back for the player's activity: a loop that is music (the
        /// dance's beat) asked for, with its recording, and the preferences letting it be heard. The
        /// director reads it every frame, with the game's other reasons to duck (ApplyMusic).
        /// </summary>
        public bool ActivityDucksMusic => HearActivity && ActivitySounds.DucksMusic(activity.Asked) && ActivityClip(activity.Asked) != null;

        /// <summary>Whether the preferences let the activity's loop be heard: not reduced or muted, and some volume. Reduced motion is not asked: a sound is not motion.</summary>
        private bool HearActivity => isActiveAndEnabled && !ReducedAudio && !Muted && volume > 0f && effectsShare > 0f;

        /// <summary>
        /// What the player is doing, by the activity's name (<c>HouseFurnitureActivity</c>), or null for
        /// nothing: the director asks every frame while the activity is under way, and null from the
        /// moment the player starts to get up. An activity with a loop (<see cref="ActivitySounds"/>)
        /// fades it in over <see cref="ActivitySounds.FadeIn"/>; one without, or none, lets the loop
        /// sounding fade out over <see cref="ActivitySounds.FadeOut"/>. Asking again changes nothing.
        /// </summary>
        public void SetActivity(string activityName)
        {
            string loop = ActivitySounds.LoopOf(activityName);
            if (loop == activity.Asked) return;
            activity.Ask(loop);
            Initialize();
            if (!initialized) return;
            LoadActivityVoice();
            WriteActivityVolume();
        }

        /// <summary>An activity's loop, or null for none and for one the project ships no recording of.</summary>
        private AudioClip ActivityClip(string loop)
        {
            if (string.IsNullOrEmpty(loop)) return null;
            if (!activityClips.TryGetValue(loop, out var clip))
            {
                clip = Resources.Load<AudioClip>(ActivitySounds.ResourceFolder + loop);
                activityClips[loop] = clip;
            }
            return clip;
        }

        /// <summary>The activity voice given the loop its fade carries, and started or stopped as the fade and the preferences want.</summary>
        private void LoadActivityVoice()
        {
            if (activityVoice == null) return;
            if (activity.Key != activityVoiceKey)
            {
                // One loop at a time: the voice changes loops only once the old one has faded out.
                activityVoice.Stop();
                activityVoiceKey = activity.Key;
                activityVoice.clip = ActivityClip(activityVoiceKey);
                activityVoice.volume = 0f;
            }
            bool wanted = HearActivity && activityVoice.clip != null && activity.Sounding;
            if (wanted && !activityVoice.isPlaying) activityVoice.Play();
            else if (!wanted && activityVoice.isPlaying) activityVoice.Stop();
        }

        /// <summary>
        /// The activity voice moved on by <paramref name="seconds"/> of its fade, once a frame on the
        /// real clock as the beds are. Held while the preferences take it away, so the loop asked for
        /// comes back in from silence.
        /// </summary>
        private void TickActivity(float seconds)
        {
            if (!initialized || !HearActivity) return;
            activity.Tick(seconds);
            LoadActivityVoice();
            WriteActivityVolume();
        }

        /// <summary>The activity voice's volume: its fade's level and its loop's gain, a share of the effects volume, under the beds' duck.</summary>
        private void WriteActivityVolume()
        {
            if (activityVoice == null) return;
            activityVoice.mute = Muted;
            activityVoice.volume = volume * effectsShare * ActivitySounds.GainOf(activityVoiceKey) * activity.Level * bedDuck.Level;
        }

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
        /// <summary>Whether the room tone is playing right now: the generic air, as the bed asked for (see <see cref="RoomTonePlaying"/>).</summary>
        public bool AmbiencePlaying => CurrentBedPlaying(room: false);

        // The three volumes beside the master (PLAN A, A11), each a share of it: the music, the
        // effects - the game's news and the house's air - and the interface's answers to a hand.
        private float musicShare = 1f, effectsShare = 1f, interfaceShare = 1f;

        /// <summary>A channel's share of the master, 0 to 1.</summary>
        public float ChannelVolume(SoundChannel channel) =>
            channel == SoundChannel.Music ? musicShare : channel == SoundChannel.Interface ? interfaceShare : effectsShare;

        /// <summary>Sets the three shares of the master: the music's, the effects' and the interface's, each 0 to 1.</summary>
        public void SetChannelVolumes(float music, float effects, float interfaceVolume)
        {
            musicShare = Share(music); effectsShare = Share(effects); interfaceShare = Share(interfaceVolume);
            ApplySettings();
        }

        private static float Share(float value) => float.IsNaN(value) ? 1f : Mathf.Clamp01(value);

        /// <summary>The volume a cue plays at, after the master, reduced audio, its channel's share and its place in the mix (<see cref="SoundPictures.Row.GainDb"/>).</summary>
        public float VolumeOf(Cue cue)
        {
            string name = cue.ToString();
            return CueVolume * ChannelVolume(SoundPictures.ChannelOf(name)) * SoundPictures.GainOf(name);
        }

        /// <summary>
        /// Every cue asked for, muted or not, as it is asked: what a test listens to for the picture
        /// each sound carries (A11). Nothing in the game subscribes.
        /// </summary>
        public event System.Action<Cue> CueAsked;

        public void SetReducedAudio(bool value)
        {
            ReducedAudio = value;
            ApplySettings();
        }

        /// <summary>
        /// The room the camera looks into (§3.C room tone per room). A room with a bed in
        /// Resources plays it in place of the generic air; a room without one, or none, plays the
        /// air. The bed that was playing fades out as the new one fades in, over
        /// <see cref="RoomToneBlend.CrossfadeSeconds"/>, and a bed heard before carries on from where
        /// it left off. Asked every time the focus crosses a threshold; the same room again costs nothing.
        /// </summary>
        public void SetRoom(string room)
        {
            if (room == CurrentRoom) return;
            CurrentRoom = room;
            Initialize();
            if (!initialized) return;
            EnterBed(room);
            ApplySettings();
        }

        /// <summary>The room's bed asked of the voices: loaded into one when neither carries it.</summary>
        private void EnterBed(string room)
        {
            string key = RoomClip(room) != null ? room : RoomToneBlend.Air;
            int voice = beds.Enter(key, out bool given);
            var source = bedVoices[voice];
            if (!given || source == null) return;
            // The voice's old bed is let go, its place kept for when it comes back.
            KeepBedPlace(source);
            source.Stop();
            source.clip = key == RoomToneBlend.Air ? ambience : RoomClip(room);
            source.volume = 0f;
        }

        /// <summary>A room's bed, or null for a room without one and for none.</summary>
        private AudioClip RoomClip(string room)
        {
            if (string.IsNullOrEmpty(room)) return null;
            if (!roomTones.TryGetValue(room, out var clip))
            {
                clip = Resources.Load<AudioClip>(RoomResourceFolder + room);
                roomTones[room] = clip;
            }
            return clip;
        }

        /// <summary>Where a sounding bed is in its loop, kept for its return.</summary>
        private void KeepBedPlace(AudioSource source)
        {
            if (source != null && source.clip != null && source.isPlaying) bedPlaces[source.clip] = source.timeSamples;
        }

        /// <summary>A bed voice started, from the place its bed was let go at when it has one.</summary>
        private void ResumeBed(AudioSource source)
        {
            source.Play();
            if (bedPlaces.TryGetValue(source.clip, out int place) && place > 0 && place < source.clip.samples)
                source.timeSamples = place;
        }

        /// <summary>
        /// Both bed voices moved on by <paramref name="seconds"/> of their crossfade, and a voice
        /// that has faded all the way out let go, its place kept. Called once a frame on the real
        /// clock, the music's clamp on a long frame included. Held while the beds are off, so the
        /// bed asked for comes back in from silence rather than at whatever level the clock reached.
        /// </summary>
        private void TickBeds(float seconds)
        {
            if (!initialized || !HearBeds) return;
            beds.Tick(seconds);
            for (int i = 0; i < bedVoices.Length; i++)
            {
                var source = bedVoices[i];
                if (source != null && source.isPlaying && !beds.Wanted(i))
                {
                    KeepBedPlace(source);
                    source.Stop();
                }
            }
            WriteBedVolumes();
        }

        /// <summary>Each bed voice's volume: its fade's level at its bed's peak, a share of the effects volume, under the beds' duck.</summary>
        private void WriteBedVolumes()
        {
            for (int i = 0; i < bedVoices.Length; i++)
            {
                var source = bedVoices[i];
                if (source == null) continue;
                source.volume = volume * effectsShare * RoomToneBlend.PeakOf(beds.KeyOf(i)) * beds.LevelOf(i) * bedDuck.Level;
            }
        }

        private void Awake()
        {
            // The scene's audio enables before the director's delayed Start. Honor the saved
            // mute choice before the first source can play; the director may override it later.
            // The session's store, decided as the director will decide it (A13): an isolated root
            // reads its own file or the defaults, never the machine's PlayerPrefs.
            var saved = Gamesim.Episode.EpisodeDirector.LaunchPreferences();
            Muted = saved.GetInt("Gamesim.Muted", 0) == 1;
            ReducedAudio = saved.GetInt("Gamesim.ReducedAudio", 0) == 1;
            volume = Mathf.Clamp(saved.GetInt("Gamesim.Volume", 35), 0, 100) / 100f;
            musicShare = SoundPictures.Clamp(saved.GetInt(SoundPictures.MusicVolumeKey, SoundPictures.DefaultVolume)) / 100f;
            effectsShare = SoundPictures.Clamp(saved.GetInt(SoundPictures.EffectsVolumeKey, SoundPictures.DefaultVolume)) / 100f;
            interfaceShare = SoundPictures.Clamp(saved.GetInt(SoundPictures.InterfaceVolumeKey, SoundPictures.DefaultVolume)) / 100f;
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

        /// <summary>
        /// Switches the beds - the house's air and the rooms' - on or off, beside the preferences:
        /// the director turns them off behind the front door's screens, where the house is not the
        /// game, and on again in the house, where the bed asked for comes back in from silence and
        /// from the place it was let go at. Asked every frame; the same answer again costs nothing.
        /// </summary>
        public void SetAmbienceEnabled(bool value)
        {
            if (ambienceEnabled == value) return;
            ambienceEnabled = value;
            ApplySettings();
        }

        /// <summary>
        /// Puts the music bed into a state, idempotently.
        ///
        /// <para>Callers say what is happening rather than what to play — the opening sequence asks
        /// for <see cref="Music.Theme"/>, the house asks for <see cref="Music.Season"/>, the main menu
        /// for <see cref="Music.Title"/>, and every other screen that is not the game asks for
        /// <see cref="Music.Silent"/>. Repeating the state a
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
            // The beds' crossfade is not the music's, and ManualMusicClock does not hold it.
            TickBeds(MusicFade.FrameStep(Time.unscaledDeltaTime));
            // Nor the activity's loop, which fades on the same real clock as the beds.
            TickActivity(MusicFade.FrameStep(Time.unscaledDeltaTime));
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
        /// <see cref="ManualMusicClock"/>, with exactly the time it says and no clamp. The ducks
        /// (<see cref="SetDuck"/>) move on the same clock.
        /// </summary>
        public void TickMusic(float seconds)
        {
            if (!initialized) return;
            themeLevel = MusicFade.Theme(themeLevel, themeTarget, seconds);
            seasonLevel = MusicFade.Season(seasonLevel, seasonTarget, seconds);
            competitionLevel = MusicFade.Competition(competitionLevel, competitionTarget, seconds);
            titleLevel = MusicFade.Title(titleLevel, titleTarget, seconds);
            musicDuck.Tick(seconds);
            bedDuck.Tick(seconds);
            ReleaseSilentVoices();
            WriteMusicVolumes();
            WriteBedVolumes();
            WriteActivityVolume();
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
        /// <para><b>A competition's loop</b> cuts whatever is left of the theme and rises from silence,
        /// from the top, over 1 s, while the season's bed falls over its 1.5 s and pauses where it is.
        /// Asked for again while it is still falling - the attempt opened again as its card went - it
        /// turns round where it is. When the bed is asked for, the loop falls over 1.5 s and stops.</para>
        ///
        /// <para><b>The title loop</b> - the main menu's - rises from silence, from the top, over 2 s,
        /// while whatever else is sounding falls away as it does under silence: the bed pauses where
        /// it is, to carry on from there in the house. Asked for again while still falling, it turns
        /// round where it is. Leaving the menu lets it fall over 1.5 s and stop, except into the
        /// intro: the theme cuts it, and so starts from silence as it always has.</para>
        ///
        /// <para><b>Silence</b> fades out whatever is sounding over 1.5 s.</para>
        /// </summary>
        private void EnterMusic()
        {
            if (!initialized || !isActiveAndEnabled || themeSource == null || seasonSource == null || competitionSource == null || titleSource == null) return;
            switch (music)
            {
                case Music.Theme:
                    CutSeason();
                    CutCompetition();
                    CutTitle();
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
                    // A competition's loop goes over its own fall while the bed comes back up.
                    competitionTarget = 0f;
                    // And the title's, when the menu hands the house back.
                    titleTarget = 0f;
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
                case Music.Competition:
                    CutTheme();
                    // The bed falls and pauses where it is (ReleaseSilentVoices), to carry on from there after the card.
                    seasonTarget = 0f;
                    titleTarget = 0f;
                    competitionTarget = 1f;
                    // Marked running but not playing: a reset stopped it behind the bookkeeping.
                    if (competitionRunning && !competitionSource.isPlaying) competitionRunning = false;
                    if (!competitionRunning)
                    {
                        competitionSource.Stop();
                        competitionSource.clip = competitionBed;
                        competitionLevel = 0f;
                        competitionSource.volume = 0f;
                        competitionSource.Play();
                        competitionRunning = true;
                    }
                    break;
                case Music.Title:
                    // Everything else falls away as it does under silence; the bed pauses where it is.
                    themeTarget = 0f;
                    seasonTarget = 0f;
                    competitionTarget = 0f;
                    titleTarget = 1f;
                    // Marked running but not playing: a reset stopped it behind the bookkeeping.
                    if (titleRunning && !titleSource.isPlaying) titleRunning = false;
                    if (!titleRunning)
                    {
                        titleSource.Stop();
                        titleSource.clip = titleBed;
                        titleLevel = 0f;
                        titleSource.volume = 0f;
                        titleSource.Play();
                        titleRunning = true;
                    }
                    break;
                default:
                    themeTarget = 0f;
                    seasonTarget = 0f;
                    competitionTarget = 0f;
                    titleTarget = 0f;
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
            if (competitionRunning && competitionTarget <= 0f && competitionLevel <= 0f) CutCompetition();
            if (titleRunning && titleTarget <= 0f && titleLevel <= 0f) CutTitle();
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
            // A competition's loop from the top too: the reset lost its place.
            if (competitionRunning && competitionSource != null) competitionSource.Play();
            // And the title's.
            if (titleRunning && titleSource != null) titleSource.Play();
            if (seasonSource != null)
            {
                if (seasonVoice == SeasonVoice.Playing) seasonSource.Play();
                else if (seasonVoice == SeasonVoice.Paused)
                {
                    seasonSource.Stop();
                    seasonVoice = SeasonVoice.Stopped;
                }
            }
            foreach (var voice in bedVoices) if (voice != null) voice.Stop();
            // The activity's loop too: ApplySettings starts it again if it is still wanted.
            if (activityVoice != null) activityVoice.Stop();
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

        /// <summary>A competition's loop off at once and rewound: the next competition starts it from the top.</summary>
        private void CutCompetition()
        {
            competitionLevel = competitionTarget = 0f;
            competitionRunning = false;
            if (competitionSource == null) return;
            competitionSource.Stop();
            competitionSource.volume = 0f;
        }

        /// <summary>The title loop off at once and rewound: the menu starts it from the top next time.</summary>
        private void CutTitle()
        {
            titleLevel = titleTarget = 0f;
            titleRunning = false;
            if (titleSource == null) return;
            titleSource.Stop();
            titleSource.volume = 0f;
        }

        /// <summary>
        /// Each voice's volume: its fade's level at its peak, under the preferences and the music's duck.
        ///
        /// <para>Written every frame and on every change of preference, so the master volume and
        /// reduced audio reach a fade in progress at once - the reference build reads its master
        /// volume only when a fade begins - and "Mute sound" silences the music with everything else,
        /// where the reference's sound switch leaves its music playing.</para>
        /// </summary>
        private void WriteMusicVolumes()
        {
            float preferences = volume * (ReducedAudio ? 0.5f : 1f) * musicShare * musicDuck.Level;
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
            if (competitionSource != null)
            {
                competitionSource.mute = Muted;
                competitionSource.volume = preferences * MusicFade.CompetitionPeak * competitionLevel;
            }
            if (titleSource != null)
            {
                titleSource.mute = Muted;
                titleSource.volume = preferences * MusicFade.TitlePeak * titleLevel;
            }
        }

        public void PlayCue(Cue cue)
        {
            LastCue = cue;
            CuesAsked++;
            CueAsked?.Invoke(cue);
            if (!isActiveAndEnabled || Muted || volume <= 0f) return;
            Initialize();
            // At its channel's share of the master (A11), and at its place in the mix: the source carries the master.
            string name = cue.ToString();
            float gain = ChannelVolume(SoundPictures.ChannelOf(name)) * SoundPictures.GainOf(name);
            if (gain <= 0f || !clips.TryGetValue(cue, out var clip)) return;
            if (cue == Cue.Vote)
            {
                if (lastVoteVoiceFrame == Time.frameCount) return;
                lastVoteVoiceFrame = Time.frameCount;
            }
            // A cue that keeps a spacing (the hover's tick) is heard at most once inside it, on the
            // real clock: a sweep down a list asks once a row, and every ask is still counted above.
            float spacing = SoundPictures.SpacingOf(name);
            if (spacing > 0f)
            {
                float now = Time.unscaledTime;
                if (!SoundPictures.Sounds(now, lastSounded.TryGetValue(cue, out float last) ? last : float.NaN, spacing)) return;
                lastSounded[cue] = now;
            }
            cueSource.PlayOneShot(clip, gain);
            CueVoicesStarted++;
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
            // Own new sources; do not edit another component's audio or global listener volume.
            for (int i = 0; i < bedVoices.Length; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = true;
                voice.spatialBlend = 0f;
                voice.priority = 180;
                voice.volume = 0f;
                bedVoices[i] = voice;
            }
            cueSource = gameObject.AddComponent<AudioSource>();
            cueSource.playOnAwake = false;
            cueSource.spatialBlend = 0f;
            cueSource.priority = 80;
            themeSource = MusicVoice();
            seasonSource = MusicVoice();
            // The activity's voice last, so every source before it keeps its place on the object.
            // Above the beds in priority: it is the sound of what the player is doing.
            activityVoice = gameObject.AddComponent<AudioSource>();
            activityVoice.playOnAwake = false;
            activityVoice.loop = true;
            activityVoice.spatialBlend = 0f;
            activityVoice.priority = 170;
            activityVoice.volume = 0f;
            // The competitions' music voice after it, last, for the same reason.
            competitionSource = MusicVoice();
            // And the main menu's title voice after that, last of all.
            titleSource = MusicVoice();

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
            // The competitions' loop the same way: its first Play() is the frame an attempt opens.
            var recordedCompetition = Resources.Load<AudioClip>("Audio/Competition");
            competitionBed = recordedCompetition != null ? recordedCompetition : Own(BuildBed("Competition bed", CompetitionChords, 0.9375f));
            HasRecordedCompetition = recordedCompetition != null;
            Preload(competitionBed);
            // The title loop too: its first Play() is the launch's first frame at the front door.
            var recordedTitle = Resources.Load<AudioClip>("Audio/Title");
            titleBed = recordedTitle != null ? recordedTitle : Own(BuildBed("Title bed", ThemeChords, 2.25f));
            HasRecordedTitle = recordedTitle != null;
            Preload(titleBed);

            ambience = Own(BuildAmbience());
            // The room beds decoded now too, for the same reason: their metas do not preload, and
            // the first visit to each room decoded twelve seconds of its bed on the main thread.
            foreach (var bed in Resources.LoadAll<AudioClip>(RoomResourceFolder.TrimEnd('/')))
            {
                if (bed == null) continue;
                roomTones[bed.name] = bed;
                Preload(bed);
            }
            // The activities' loops as well: a sizzle left for the first cook decoded on the main
            // thread in the very frame the cooking got under way.
            foreach (var loop in Resources.LoadAll<AudioClip>(ActivitySounds.ResourceFolder.TrimEnd('/')))
            {
                if (loop == null) continue;
                activityClips[loop.name] = loop;
                Preload(loop);
            }
            EnterBed(CurrentRoom);
            foreach (var composition in Compositions)
            {
                var clip = Recorded(composition.cue) ?? Own(Compose(composition.name, composition.duration, composition.tones));
                // Decoded here too: the cues' metas do not preload either, so a cue's first sounding
                // decoded its recording on the main thread in the very frame it was asked for.
                Preload(clip);
                clips[composition.cue] = clip;
            }
            ApplySettings();
        }

        /// <summary>
        /// Every cue the house plays, one row a cue, with the composition it falls back to when the
        /// project ships no recording of it (<see cref="CueResourceFolder"/>). A slice that adds a cue
        /// adds its row here, its row in <see cref="SoundPictures"/> and its recording.
        /// </summary>
        private static readonly Composition[] Compositions =
        {
            new Composition(Cue.Button, "Button", 0.07f, new Tone(600, 0.06f, 0, 0.11f)),
            new Composition(Cue.Save, "Save", 0.24f, new Tone(1000, 0.08f, 0, 0.12f), new Tone(1200, 0.10f, 0.10f, 0.12f)),
            new Composition(Cue.SocialUp, "Connection", 0.33f, new Tone(330, 0.18f, 0, 0.13f), new Tone(523.25f, 0.22f, 0.10f, 0.13f)),
            new Composition(Cue.SocialDown, "Tension", 0.33f, new Tone(523.25f, 0.18f, 0, 0.12f), new Tone(330, 0.22f, 0.10f, 0.12f)),
            new Composition(Cue.CompetitionStart, "Competition begins", 0.65f, new Tone(220, 0.6f, 0, 0.15f), new Tone(330, 0.55f, 0.05f, 0.10f)),
            new Composition(Cue.CompetitionWin, "Competition result", 1.1f,
                new Tone(523.25f, 0.3f, 0, 0.14f), new Tone(659.25f, 0.3f, 0.23f, 0.14f),
                new Tone(783.99f, 0.45f, 0.46f, 0.14f), new Tone(1046.5f, 0.35f, 0.72f, 0.10f)),
            new Composition(Cue.Nomination, "Nomination", 0.85f, new Tone(110, 0.75f, 0, 0.16f), new Tone(220, 0.5f, 0.2f, 0.07f)),
            new Composition(Cue.Veto, "Veto result", 0.75f,
                new Tone(587.33f, 0.28f, 0, 0.13f), new Tone(739.99f, 0.3f, 0.18f, 0.13f), new Tone(880, 0.36f, 0.37f, 0.12f)),
            new Composition(Cue.Vote, "Vote recorded", 0.20f, new Tone(880, 0.18f, 0, 0.11f)),
            new Composition(Cue.Eviction, "Eviction", 0.95f, new Tone(82.41f, 0.85f, 0, 0.15f), new Tone(164.81f, 0.6f, 0.12f, 0.07f)),
            new Composition(Cue.Finale, "Final result", 1.5f,
                new Tone(523.25f, 1.35f, 0, 0.10f), new Tone(659.25f, 1.25f, 0.10f, 0.09f),
                new Tone(783.99f, 1.15f, 0.20f, 0.09f), new Tone(1046.5f, 0.75f, 0.55f, 0.065f)),
            new Composition(Cue.PanelOpen, "Panel opens", 0.28f, new Tone(520, 0.12f, 0, 0.08f), new Tone(780, 0.14f, 0.1f, 0.08f)),
            new Composition(Cue.PanelClose, "Panel closes", 0.24f, new Tone(760, 0.1f, 0, 0.08f), new Tone(460, 0.12f, 0.09f, 0.08f)),
            new Composition(Cue.Hover, "Hover", 0.09f, new Tone(1800, 0.07f, 0, 0.05f)),
            new Composition(Cue.TutorialStep, "Tutorial step", 0.1f, new Tone(500, 0.1f, 0, 0.15f, 700)),
            new Composition(Cue.DoorOpen, "Door opens", 0.6f, new Tone(160, 0.12f, 0, 0.14f, 90), new Tone(220, 0.45f, 0.1f, 0.05f, 300)),
            new Composition(Cue.DoorShut, "Door shuts", 0.55f, new Tone(120, 0.4f, 0, 0.16f, 45), new Tone(1800, 0.05f, 0.03f, 0.04f)),
            new Composition(Cue.Summons, "Summons", 1.2f,
                new Tone(392f, 0.6f, 0, 0.12f), new Tone(659.25f, 0.6f, 0.24f, 0.12f), new Tone(523.25f, 0.7f, 0.48f, 0.12f)),
            // The reference build's compProgress: a 1000 Hz sine for 50 ms (useGameSFX.ts).
            new Composition(Cue.CountTick, "Count tick", 0.1f, new Tone(1000, 0.05f, 0, 0.15f)),
            new Composition(Cue.FalseStart, "False start", 0.24f, new Tone(196, 0.1f, 0, 0.13f), new Tone(196, 0.12f, 0.12f, 0.13f, 175)),
            new Composition(Cue.ShowHorn, "Show horn", 0.7f, new Tone(233.08f, 0.65f, 0, 0.12f), new Tone(349.23f, 0.65f, 0, 0.1f)),
            new Composition(Cue.ShowLockIn, "Show lock in", 0.1f, new Tone(1480, 0.06f, 0, 0.1f)),
            new Composition(Cue.ShowRight, "Show right", 0.5f, new Tone(880, 0.3f, 0, 0.12f), new Tone(1174.66f, 0.38f, 0.11f, 0.12f)),
            new Composition(Cue.ShowWrong, "Show wrong", 0.45f, new Tone(118, 0.45f, 0, 0.14f), new Tone(123, 0.45f, 0, 0.12f)),
        };

        /// <summary>How many of the cues' clips are loaded, or loading, rather than waiting for their first sounding: every cue once the house has started.</summary>
        public int CuesPreloaded
        {
            get
            {
                int loaded = 0;
                foreach (var clip in clips.Values)
                    if (clip != null && (clip.loadState == AudioDataLoadState.Loaded || clip.loadState == AudioDataLoadState.Loading)) loaded++;
                return loaded;
            }
        }

        private void ApplySettings()
        {
            if (!initialized) return;
            bool hearAmbience = HearBeds;
            // The beds - the house's air and the rooms' - are effects (A11). Stopped in this frame
            // when the preferences take them away; the bed asked for comes back in from silence.
            if (!hearAmbience) beds.Silence();
            for (int i = 0; i < bedVoices.Length; i++)
            {
                var source = bedVoices[i];
                if (source == null) continue;
                source.mute = Muted;
                bool wanted = hearAmbience && source.clip != null && beds.Wanted(i);
                if (wanted && !source.isPlaying) ResumeBed(source);
                else if (!wanted && source.isPlaying)
                {
                    KeepBedPlace(source);
                    source.Stop();
                }
            }
            WriteBedVolumes();
            // The player's activity, an effect too: stopped in this frame when the preferences take
            // it away, and its loop comes back in from silence when they give it back.
            if (!HearActivity) activity.Silence();
            LoadActivityVoice();
            WriteActivityVolume();
            if (cueSource != null)
            {
                cueSource.mute = Muted;
                cueSource.volume = CueVolume;
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

        /// <summary>The competitions' bed: E minor, quicker, and it turns on B to start again.</summary>
        private static readonly float[][] CompetitionChords =
        {
            new[] { 164.81f, 196.00f, 246.94f },   // Em
            new[] { 130.81f, 164.81f, 196.00f },   // C
            new[] { 146.83f, 185.00f, 220.00f },   // D
            new[] { 123.47f, 155.56f, 185.00f },   // B
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

        /// <summary>A cue and the composition it falls back to: its clip's name, its length in seconds and its voices.</summary>
        private readonly struct Composition
        {
            public readonly Cue cue;
            public readonly string name;
            public readonly float duration;
            public readonly Tone[] tones;
            public Composition(Cue cue, string name, float duration, params Tone[] tones)
            {
                this.cue = cue; this.name = name; this.duration = duration; this.tones = tones;
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
            foreach (var voice in bedVoices)
            {
                KeepBedPlace(voice);
                if (voice != null) voice.Stop();
            }
            beds.Silence();
            if (activityVoice != null) activityVoice.Stop();
            activity.Silence();
            if (cueSource != null) cueSource.Stop();
            // The music too. A disabled component ticks no fades, and the bed used to play on under
            // one with nothing left to fade it or stop it. The theme goes; the bed keeps its place -
            // when it has one - and OnEnable brings back whatever is asked for then.
            CutTheme();
            // A competition's loop goes as the theme does: it starts from the top when it is asked for again.
            CutCompetition();
            // And the title's.
            CutTitle();
            if (seasonVoice == SeasonVoice.Playing) PutSeasonAway();
            seasonLevel = seasonTarget = 0f;
            WriteMusicVolumes();
        }

        private void OnDestroy()
        {
            foreach (var voice in bedVoices) if (voice != null) { voice.Stop(); Release(voice); }
            if (cueSource != null) { cueSource.Stop(); Release(cueSource); }
            if (themeSource != null) { themeSource.Stop(); Release(themeSource); }
            if (seasonSource != null) { seasonSource.Stop(); Release(seasonSource); }
            if (competitionSource != null) { competitionSource.Stop(); Release(competitionSource); }
            if (titleSource != null) { titleSource.Stop(); Release(titleSource); }
            if (activityVoice != null) { activityVoice.Stop(); Release(activityVoice); }
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
