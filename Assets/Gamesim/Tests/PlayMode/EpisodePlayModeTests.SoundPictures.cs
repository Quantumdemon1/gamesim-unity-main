using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Every sound has a picture (PLAN A, A11): every cue that carries news (<see cref="SoundPictures"/>)
    /// comes with a change of the text on screen in the frame it sounds - so a player who cannot hear
    /// it, or who muted it, is told the same thing. A week of the season played muted by the keyboard
    /// walk's own presses; each kind of competition board played through its hits and its misses; and
    /// the finale's verdict. And the three volumes beside the master each reach their own sounds and
    /// nothing else.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Runs at the head of every frame, before the game's own updates.</summary>
        [DefaultExecutionOrder(-30000)]
        private sealed class FrameHeadProbe : MonoBehaviour
        {
            public Action Head;
            private void Update() => Head?.Invoke();
        }

        /// <summary>Runs at the foot of every frame, after the game's own late updates.</summary>
        [DefaultExecutionOrder(30000)]
        private sealed class FrameFootProbe : MonoBehaviour
        {
            public Action Foot;
            private void LateUpdate() => Foot?.Invoke();
        }

        /// <summary>
        /// Listens to the house's cues and reads the words on screen at the head and the foot of every
        /// frame. An information cue asked for between the two is pictured when a label's words changed
        /// in that frame - a label that was not already changing the frame before (a clock), or one
        /// whose words changed in more than their numbers (a countdown that becomes "WAVE").
        ///
        /// <para>A label is known by its place in its canvas - the names down to it, with the words of
        /// every label at that place together - so a HUD that rebuilds itself with the same words has
        /// changed nothing, and a card fading in counts from the frame its words are set.</para>
        /// </summary>
        private sealed class SoundPictureWatch : IDisposable
        {
            private readonly HouseAudio audio;
            private readonly Func<string> where;
            private readonly GameObject probes;
            private Dictionary<string, string> atHead, lastFoot;
            private HashSet<string> lastChanged = new HashSet<string>(StringComparer.Ordinal);
            private HashSet<string> moving = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<HouseAudio.Cue> cues = new List<HouseAudio.Cue>();

            /// <summary>Every information cue asked for, and how often.</summary>
            public readonly Dictionary<HouseAudio.Cue, int> Heard = new Dictionary<HouseAudio.Cue, int>();
            /// <summary>The information cues asked for between a head and a foot: the ones the watch judged.</summary>
            public readonly Dictionary<HouseAudio.Cue, int> Checked = new Dictionary<HouseAudio.Cue, int>();
            /// <summary>The cues that sounded in a frame whose words did not change.</summary>
            public readonly List<string> Silent = new List<string>();
            /// <summary>Each judged cue with the change that pictured it, for the log.</summary>
            public readonly List<string> Pictures = new List<string>();

            public SoundPictureWatch(HouseAudio audio, Func<string> where)
            {
                this.audio = audio;
                this.where = where;
                probes = new GameObject("Sound picture probes");
                probes.AddComponent<FrameHeadProbe>().Head = Head;
                probes.AddComponent<FrameFootProbe>().Foot = Foot;
                audio.CueAsked += Listen;
            }

            public void Dispose()
            {
                if (audio != null) audio.CueAsked -= Listen;
                if (probes != null) Object.Destroy(probes);
            }

            public static string Count(Dictionary<HouseAudio.Cue, int> cues) =>
                string.Join(", ", cues.Select(pair => pair.Key + " x" + pair.Value));

            private void Listen(HouseAudio.Cue cue)
            {
                if (!SoundPictures.IsInformation(cue.ToString())) return;
                cues.Add(cue);
                Heard[cue] = Heard.TryGetValue(cue, out int count) ? count + 1 : 1;
            }

            private void Head()
            {
                atHead = LabelsOnScreen();
                // What changed the frame before, and between that frame's foot and this head: a label
                // still changing now was changing anyway.
                moving = new HashSet<string>(lastChanged, StringComparer.Ordinal);
                if (lastFoot != null) moving.UnionWith(Changed(lastFoot, atHead));
                cues.Clear();
            }

            private void Foot()
            {
                if (atHead == null) return;
                var now = LabelsOnScreen();
                var changed = Changed(atHead, now);
                if (cues.Count > 0)
                {
                    foreach (var cue in cues) Checked[cue] = Checked.TryGetValue(cue, out int count) ? count + 1 : 1;
                    string said = string.Join("+", cues);
                    string picture = changed
                        .Where(path => !moving.Contains(path) || Shape(Read(atHead, path)) != Shape(Read(now, path)))
                        .Select(path => path + ": '" + Read(atHead, path) + "' -> '" + Read(now, path) + "'")
                        .FirstOrDefault();
                    if (picture != null) Pictures.Add(said + ": " + picture);
                    else Silent.Add(said + " in " + where() + " (frame " + Time.frameCount
                        + (changed.Count > 0 ? "; only labels already changing moved: " + string.Join(", ", changed.Take(3)) : "") + ")");
                }
                cues.Clear();
                lastChanged = changed;
                lastFoot = now;
            }

            private static string Read(Dictionary<string, string> labels, string path) =>
                labels.TryGetValue(path, out var words) ? words : null;

            /// <summary>The words with their numbers taken out: a clock's every tick has the same shape.</summary>
            private static string Shape(string words) => words == null ? "\u0000" : Regex.Replace(words, "[0-9]+", "#");

            private static HashSet<string> Changed(Dictionary<string, string> before, Dictionary<string, string> after)
            {
                var changed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var pair in after)
                    if (!before.TryGetValue(pair.Key, out var words) || words != pair.Value) changed.Add(pair.Key);
                foreach (var pair in before)
                    if (!after.ContainsKey(pair.Key)) changed.Add(pair.Key);
                return changed;
            }

            /// <summary>
            /// Every label up on a screen canvas with words in it, by its place: what the screen says this
            /// moment, a card fading in included (its words are set the frame it is played).
            /// </summary>
            private static Dictionary<string, string> LabelsOnScreen()
            {
                var byPlace = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text)) continue;
                    var canvas = label.canvas;
                    if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) continue;
                    string place = Place(label.transform, canvas.rootCanvas.transform);
                    if (!byPlace.TryGetValue(place, out var words)) byPlace[place] = words = new List<string>();
                    words.Add(label.text);
                }
                var labels = new Dictionary<string, string>(byPlace.Count, StringComparer.Ordinal);
                foreach (var pair in byPlace)
                {
                    pair.Value.Sort(StringComparer.Ordinal);
                    labels[pair.Key] = string.Join("\u0001", pair.Value);
                }
                return labels;
            }

            private static string Place(Transform label, Transform root)
            {
                var names = new List<string>();
                for (var at = label; at != null && at != root; at = at.parent) names.Add(at.name);
                names.Add(root.name);
                names.Reverse();
                return string.Join("/", names);
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Audio_EveryInformationCueComesWithTextOnScreenInTheSameFrame()
        {
            var audio = director.GetComponent<HouseAudio>();
            // Muted, as a player who cannot hear it plays: every cue is still asked for, and none sounds.
            director.OpenSettings();
            yield return null; yield return null;
            ButtonWithCaption("Mute sound").onClick.Invoke();
            yield return null;
            director.ClosePanels();
            yield return null;
            Assert.That(audio.Muted, Is.True, "The walk is played with the sound off.");

            player.Agent.speed = 25; player.Agent.acceleration = 100;
            director.SetCeremonyPace(CeremonyPace.Quick);

            using (var watch = new SoundPictureWatch(audio, () => director.Snapshot.phase + " week " + director.Snapshot.week))
            {
                var previous = director.Snapshot;
                bool recapped = false;
                for (int guard = 0; guard < 60 && !recapped; guard++)
                {
                    if (SceneComponents<CompetitionResult>().Any(result => result.IsPlaying))
                        yield return ContinueCompetitionResults(byKeyboard: true);
                    yield return AwaitRecap(previous, director.Snapshot);
                    if (director.IsWeeklyRecapOpen)
                    {
                        yield return KeyboardSubmit(WeeklyRecapScreen.ContinueCaption);
                        recapped = true;
                        break;
                    }
                    if (!director.IsPanelOpen)
                    {
                        WarpPlayer(director.StationPosition);
                        Assert.That(director.TryOpenPhasePanel(), Is.True, "The station must open in " + director.Snapshot.phase);
                        yield return null; yield return null;
                    }
                    var state = director.Snapshot;
                    foreach (var caption in KeyboardCaptions(state))
                        yield return KeyboardSubmit(caption);
                    yield return SettleReveal();
                    previous = state;
                }
                Assert.That(recapped, Is.True, "The walk reached the first week's recap.");

                string said = SoundPictureWatch.Count(watch.Heard);
                TestContext.WriteLine("Information cues in the first week, muted: " + said);
                Assert.That(watch.Checked.ContainsKey(HouseAudio.Cue.Nomination) && watch.Checked.ContainsKey(HouseAudio.Cue.Eviction), Is.True,
                    "A week's nominations and its eviction both sound: " + said);
                Assert.That(watch.Silent, Is.Empty,
                    "These cues sounded in a frame whose text did not change - news only a hearing player was given: " + string.Join(" | ", watch.Silent));
            }
        }

        /// <summary>The board each kind of competition is played on here: the kind's standard definition, and the waves for endurance's drop.</summary>
        private static CompetitionDefinition CueBoard(CompetitionMiniGames.Kind kind)
        {
            switch (kind)
            {
                case CompetitionMiniGames.Kind.Reaction: return CompetitionDefinitions.SignalSprint;
                case CompetitionMiniGames.Kind.Memory: return CompetitionDefinitions.HouseMemory;
                // The waves are endurance's only fall: Hold Your Ground has none.
                case CompetitionMiniGames.Kind.Endurance: return CompetitionDefinitions.PressureCooker;
                case CompetitionMiniGames.Kind.Words: return CompetitionDefinitions.WordScramble;
                // The dice, and the timing bar's briefing (no category deals the bar; the seam starts it).
                default: return CompetitionDefinitions.RollTheDice;
            }
        }

        /// <summary>The first seed whose season opens with this board at its first Head of Household.</summary>
        private static uint SeedOpeningWithBoard(CompetitionDefinition board)
        {
            for (uint seed = 1; seed < 4000; seed++)
                if (CompetitionDefinitions.Version4(seed, 1, EpisodePhase.HoH) == board) return seed;
            throw new InvalidOperationException("No season opens with " + board.Id);
        }

        /// <summary>
        /// The competition's own news has pictures too (A11): each kind of board practised from the
        /// briefing of a season's first Head of Household - GO is its start, a hit, a pair, a kept roll
        /// or a solved word rises, a wrong way, a let-go target, a wrong pair, a pressure wave or a
        /// wrong word falls - and each of those sounds in a frame whose words changed with it. The
        /// timing bar, which no category deals any more, is started through its seam: its start is the
        /// panel coming up.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Audio_EveryCompetitionCueComesWithTextOnScreenInTheSameFrame(
            [Values(CompetitionMiniGames.Kind.Precision, CompetitionMiniGames.Kind.Reaction, CompetitionMiniGames.Kind.Memory,
                CompetitionMiniGames.Kind.Endurance, CompetitionMiniGames.Kind.Dice, CompetitionMiniGames.Kind.Words)] CompetitionMiniGames.Kind kind)
        {
            var board = CueBoard(kind);
            yield return InstallRules4AtFirstHoH(SeedOpeningWithBoard(board));
            var audio = director.GetComponent<HouseAudio>();
            var expected = new List<HouseAudio.Cue> { HouseAudio.Cue.CompetitionStart };
            bool rises = kind != CompetitionMiniGames.Kind.Precision && kind != CompetitionMiniGames.Kind.Endurance;
            bool falls = kind != CompetitionMiniGames.Kind.Precision && kind != CompetitionMiniGames.Kind.Dice;
            if (rises) expected.Add(HouseAudio.Cue.SocialUp);
            if (falls) expected.Add(HouseAudio.Cue.SocialDown);

            MiniGameRun run = null;
            using (var watch = new SoundPictureWatch(audio, () => kind + (run != null ? " at " + run.Elapsed.ToString("0.00") + " s" : " at the briefing")))
            {
                if (kind == CompetitionMiniGames.Kind.Precision)
                {
                    Assert.That(director.StartTimingBarForDiagnostics(practice: true), Is.True, "The timing bar starts from the briefing.");
                    yield return Frames(2);
                }
                else
                {
                    ButtonWithCaption("Practice this competition").onClick.Invoke();
                    yield return null;
                    var screen = SceneComponents<CompetitionGameScreen>().Single();
                    if (screen.IsAssembling)
                        screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
                    for (int frame = 0; frame < 120 && !screen.IsPlaying; frame++) { screen.AdvanceReady(.2f); yield return null; }
                    Assert.That(screen.IsPlaying, Is.True, "The practice is under way.");
                    run = ChallengeRun();
                    Assert.That(run.Kind, Is.EqualTo(kind));
                    Assert.That(run.Definition, Is.SameAs(board));
                    // GO's frame is the start's; the game is played from the next.
                    yield return null;
                    switch (kind)
                    {
                        case CompetitionMiniGames.Kind.Reaction: yield return PlayReactionForCues(run); break;
                        case CompetitionMiniGames.Kind.Memory: yield return PlayMemoryForCues(run); break;
                        case CompetitionMiniGames.Kind.Endurance:
                            yield return WaitFor(() => watch.Heard.ContainsKey(HouseAudio.Cue.SocialDown), 12f, "The first pressure wave comes at 4.5 s.");
                            yield return null;
                            break;
                        case CompetitionMiniGames.Kind.Dice: yield return PlayDiceForCues(screen, run); break;
                        case CompetitionMiniGames.Kind.Words: yield return PlayWordsForCues(run); break;
                    }
                }

                string heard = SoundPictureWatch.Count(watch.Heard);
                TestContext.WriteLine(kind + ": " + heard + "\n" + string.Join("\n", watch.Pictures));
                foreach (var cue in expected)
                    Assert.That(watch.Checked.ContainsKey(cue), Is.True, kind + " sounds " + cue + " in a frame the watch judged: " + heard);
                Assert.That(watch.Silent, Is.Empty,
                    "These cues sounded in a frame whose text did not change - news only a hearing player was given: " + string.Join(" | ", watch.Silent));
            }
        }

        /// <summary>A hit on the target's own way, a wrong way on the next, and one let go.</summary>
        private IEnumerator PlayReactionForCues(MiniGameRun run)
        {
            yield return WaitFor(() => run.TargetLive, 10f, "A target comes up.");
            int hits = run.Hits;
            director.TapTarget();
            yield return null;
            Assert.That(run.Hits, Is.EqualTo(hits + 1), "The target's own way is a hit.");

            yield return WaitFor(() => !run.TargetLive, 5f, "The hit target goes.");
            yield return WaitFor(() => run.TargetLive, 10f, "The next target comes up.");
            int wrong = run.WrongDirections;
            var other = (MiniGameRun.Direction)(((int)run.TargetDirection + 1) % 4);
            // The board's arrows reach the director's own TapDirection; called as they call it.
            typeof(EpisodeDirector).GetMethod("TapDirection", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { other });
            yield return null;
            Assert.That(run.WrongDirections, Is.EqualTo(wrong + 1), "Another way is a wrong way.");

            int expired = run.ExpiredTargets;
            yield return WaitFor(() => run.ExpiredTargets > expired, 10f, "A target left alone expires.");
            yield return null;
        }

        /// <summary>A pair, then two cards that are not one.</summary>
        private IEnumerator PlayMemoryForCues(MiniGameRun run)
        {
            int cards = run.Faces.Count;
            int pair = Enumerable.Range(1, cards - 1).First(i => run.Faces[i] == run.Faces[0]);
            director.FlipCard(0);
            yield return null;
            director.FlipCard(pair);
            yield return null;
            Assert.That(run.MatchedPairs, Is.EqualTo(1), "The two cards of a face are a pair.");
            yield return null;
            int other = Enumerable.Range(1, cards - 1).First(i => i != pair && run.Faces[i] != run.Faces[0]);
            int wrong = Enumerable.Range(1, cards - 1).First(i => i != pair && i != other && run.Faces[i] != run.Faces[other] && run.Faces[i] != run.Faces[0]);
            director.FlipCard(other);
            yield return null;
            director.FlipCard(wrong);
            yield return null;
            Assert.That(run.WrongFlips, Is.EqualTo(1), "Two cards of different faces are a wrong pair.");
            yield return null;
        }

        /// <summary>A roll, and the roll kept.</summary>
        private IEnumerator PlayDiceForCues(CompetitionGameScreen screen, MiniGameRun run)
        {
            screen.GetComponentsInChildren<Button>().Single(button => button.name == "Roll dice").onClick.Invoke();
            yield return WaitFor(() => run.HasRoll && screen.GetComponentsInChildren<Button>().Any(button => button.name == "Keep roll"), 5f,
                "The dice land, and the roll can be kept.");
            screen.GetComponentsInChildren<Button>().Single(button => button.name == "Keep roll").onClick.Invoke();
            yield return null;
            Assert.That(run.Finished, Is.True, "Keeping the roll ends the attempt.");
            yield return null;
        }

        /// <summary>A word typed right, then the next typed in the wrong order.</summary>
        private IEnumerator PlayWordsForCues(MiniGameRun run)
        {
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            run.ClearTiles();
            string word = run.Word;
            foreach (char letter in word) yield return PressKey((Key)((int)Key.A + (letter - 'A')));
            Assert.That(run.WordsSolved, Is.EqualTo(1), word + ", typed in order, is solved.");
            yield return WaitFor(() => !run.ShowingVerdict, 5f, "The verdict on " + word + " clears.");
            string next = run.Word;
            string jumbled = next.Substring(1) + next[0];
            if (jumbled == next) jumbled = new string(next.Reverse().ToArray());
            Assert.That(jumbled, Is.Not.EqualTo(next), "A wrong order of " + next + " to type.");
            run.ClearTiles();
            foreach (char letter in jumbled) yield return PressKey((Key)((int)Key.A + (letter - 'A')));
            Assert.That(run.WordsSolved, Is.EqualTo(1), jumbled + " is not " + next + ".");
            yield return null;
        }

        /// <summary>
        /// The finale's verdict has its picture (A11): the jury's ballots read one at a time and the
        /// winner named, each ballot and the winner's sound in a frame whose words say it.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Audio_TheFinaleSoundsWithTextOnScreenInTheSameFrame()
        {
            HoldTheHouseForTheFixture();
            yield return InstallFinaleFixture(true);
            director.SetCeremonyPace(CeremonyPace.Quick);
            yield return OpenFinalePanel();
            ButtonWithCaption(EpisodeHud.JurySkipCaption).onClick.Invoke();
            yield return Frames(2);
            ButtonWithCaption(EpisodeHud.SpeechSkipCaption).onClick.Invoke();
            yield return Frames(2);
            ButtonWithCaption(EpisodeHud.SpeechContinueCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Jury));

            var audio = director.GetComponent<HouseAudio>();
            using (var watch = new SoundPictureWatch(audio, () => director.Snapshot.phase + (SceneComponents<JuryReveal>().Any(reveal => reveal.IsPlaying) ? " under the jury's reveal" : "")))
            {
                ButtonWithCaption("Continue episode").onClick.Invoke();
                yield return Frames(2);
                Assert.That(director.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
                var reveal = SceneComponents<JuryReveal>().Single();
                Assert.That(reveal.IsPlaying, Is.True, "The winner is read out.");
                // Every ballot read on the clock, as a player watching sees it.
                yield return WaitFor(() => reveal.ShowingResult, 90f, "The jury's reveal names the winner.");
                yield return Frames(2);

                string heard = SoundPictureWatch.Count(watch.Heard);
                TestContext.WriteLine("The finale: " + heard + "\n" + string.Join("\n", watch.Pictures));
                Assert.That(watch.Checked.ContainsKey(HouseAudio.Cue.Finale), Is.True, "The winner's sound is judged: " + heard);
                Assert.That(watch.Silent, Is.Empty,
                    "These cues sounded in a frame whose text did not change - news only a hearing player was given: " + string.Join(" | ", watch.Silent));
            }
            yield return SkipReveals();
        }

        /// <summary>
        /// The music's, the effects' and the interface's volumes (A11), each a share of the master
        /// beside it: each control moves its own share and only its own, the cues and the beds follow
        /// their channel, and the master stays where it was.
        /// </summary>
        [UnityTest]
        public IEnumerator Audio_TheThreeVolumesStandBesideTheMaster()
        {
            var audio = director.GetComponent<HouseAudio>();
            director.OpenSettings();
            yield return null; yield return null;
            float master = audio.Volume;
            float nomination = audio.VolumeOf(HouseAudio.Cue.Nomination), button = audio.VolumeOf(HouseAudio.Cue.Button);
            Assert.That(nomination, Is.EqualTo(audio.CueVolume).Within(1e-5f), "At the whole of the master, a cue plays at the master, as it always did.");

            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Effects)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Effects), Is.EqualTo(90));
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Nomination), Is.EqualTo(nomination * .9f).Within(1e-5f), "The effects' control lowers the game's news,");
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Button), Is.EqualTo(button).Within(1e-5f), "and not the interface's answers.");

            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Interface)).onClick.Invoke();
            yield return null;
            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Interface)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Interface), Is.EqualTo(80));
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Button), Is.EqualTo(button * .8f).Within(1e-5f), "The interface's control lowers the interface,");
            Assert.That(audio.VolumeOf(HouseAudio.Cue.Nomination), Is.EqualTo(nomination * .9f).Within(1e-5f), "and nothing else.");

            ButtonWithCaption(SoundPictures.DownCaption(SoundChannel.Music)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Music), Is.EqualTo(90));
            Assert.That(audio.ChannelVolume(SoundChannel.Music), Is.EqualTo(.9f).Within(1e-5f), "The music's control lowers the music's share.");
            Assert.That(audio.Volume, Is.EqualTo(master).Within(1e-5f), "The master stays where it was through all three.");

            ButtonWithCaption(SoundPictures.UpCaption(SoundChannel.Effects)).onClick.Invoke();
            yield return null;
            ButtonWithCaption(SoundPictures.UpCaption(SoundChannel.Effects)).onClick.Invoke();
            yield return null;
            Assert.That(director.ChannelVolumePercent(SoundChannel.Effects), Is.EqualTo(100), "A share never passes the whole of the master.");
            director.SetChannelVolume(SoundChannel.Music, 100);
            director.SetChannelVolume(SoundChannel.Interface, 100);
            director.ClosePanels();
            yield return null;
        }
    }
}
