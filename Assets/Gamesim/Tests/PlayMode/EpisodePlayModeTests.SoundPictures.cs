using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        /// Read-only diagnostics: each requested information event keeps its frame, source-derived
        /// expected words and its own label paths. Unrelated clocks or panel text cannot satisfy it.
        /// Active screen-canvas text is structured evidence, not rendered-pixel/legibility proof.
        /// </summary>
        private sealed class SoundPictureWatch : IDisposable
        {
            internal sealed class Need
            {
                public Component Owner;
                public TMP_Text Target;
                public string Name, Words;
                public bool Prefix;
            }

            internal sealed class Expected
            {
                public string Source;
                public readonly List<Need> Needs = new List<Need>();
                public Expected(string source) { Source = source; }
                public Expected At(Component owner, string name, string words, bool prefix = false)
                {
                    Needs.Add(new Need { Owner = owner, Name = name, Words = words, Prefix = prefix });
                    return this;
                }
                public Expected Label(TMP_Text label, string words)
                {
                    Needs.Add(new Need { Target = label, Words = words });
                    return this;
                }
            }

            private sealed class Request
            {
                public HouseAudio.Cue Cue;
                public int Frame, Ordinal;
                public Expected Expected;
                public string Error;
                public Dictionary<string, string> Before;
            }

            private readonly HouseAudio audio;
            private readonly Func<string> where;
            private readonly Func<HouseAudio.Cue, Expected> expected;
            private readonly GameObject probes;
            private Dictionary<string, string> atHead, lastFoot;
            private int headFrame = -1, nextOrdinal;
            private readonly List<Request> pending = new List<Request>();
            public readonly Dictionary<HouseAudio.Cue, int> Heard = new Dictionary<HouseAudio.Cue, int>();
            public readonly Dictionary<HouseAudio.Cue, int> Checked = new Dictionary<HouseAudio.Cue, int>();
            public readonly List<string> Silent = new List<string>();
            public readonly List<string> Pictures = new List<string>();

            public SoundPictureWatch(HouseAudio audio, Func<string> where, Func<HouseAudio.Cue, Expected> expected)
            {
                this.audio = audio;
                this.where = where;
                this.expected = expected;
                lastFoot = LabelsOnScreen();
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

            public void AssertAccounted()
            {
                Assert.That(pending, Is.Empty, "No requested event may be dropped or carried into a later frame.");
                Assert.That(Heard.Keys.OrderBy(cue => cue), Is.EqualTo(Checked.Keys.OrderBy(cue => cue)));
                foreach (var cue in Heard)
                    Assert.That(Checked[cue.Key], Is.EqualTo(cue.Value), "Every request is judged exactly once: " + cue.Key);
                Assert.That(Silent, Is.Empty, string.Join(" | ", Silent));
                Assert.That(Pictures.Count, Is.EqualTo(Heard.Values.Sum()), "One evidence row per requested information event.");
            }

            private void Listen(HouseAudio.Cue cue)
            {
                if (!SoundPictures.IsInformation(cue.ToString())) return;
                var request = new Request { Cue = cue, Frame = Time.frameCount, Ordinal = ++nextOrdinal,
                    Before = headFrame == Time.frameCount ? atHead : lastFoot };
                // Never throw through the product's sound event, mutate it, or suppress its outcome.
                try { request.Expected = expected(cue); }
                catch (Exception error) { request.Error = error.GetType().Name + ": " + error.Message; }
                pending.Add(request);
                Heard[cue] = Heard.TryGetValue(cue, out int count) ? count + 1 : 1;
            }

            private void Head()
            {
                atHead = LabelsOnScreen();
                headFrame = Time.frameCount;
                // Crucially no pending.Clear(): requests between the prior foot and this head are
                // still accounted, against the prior foot, in their actual request frame.
            }

            private void Foot()
            {
                var now = LabelsOnScreen();
                foreach (var request in pending.ToArray())
                {
                    if (request.Frame > Time.frameCount) continue;
                    Checked[request.Cue] = Checked.TryGetValue(request.Cue, out int count) ? count + 1 : 1;
                    var paths = new HashSet<string>(StringComparer.Ordinal);
                    var missing = new List<string>();
                    var wanted = request.Expected;
                    if (request.Frame != Time.frameCount) missing.Add("no foot observation in the request frame");
                    if (request.Error != null) missing.Add(request.Error);
                    if (wanted == null || wanted.Needs.Count == 0) missing.Add("no cue-specific source expectation");
                    else foreach (var need in wanted.Needs)
                    {
                        var labels = need.Target != null ? new[] { need.Target }
                            : need.Owner != null ? need.Owner.GetComponentsInChildren<TMP_Text>(true) : Array.Empty<TMP_Text>();
                        var matches = labels.Where(label => ScreenLabel(label)
                            && (need.Name == null || label.name == need.Name)
                            && (need.Prefix ? label.text.StartsWith(need.Words, StringComparison.Ordinal) : label.text == need.Words)).ToArray();
                        if (matches.Length == 0) missing.Add((need.Name ?? need.Target?.name ?? "label") + " must say '" + need.Words + "'");
                        foreach (var label in matches) paths.Add(Place(label.transform, label.canvas.rootCanvas.transform));
                    }
                    bool changed = request.Before != null && paths.Any(path => Read(request.Before, path) != Read(now, path));
                    if (!changed) missing.Add("none of this cue's own labels changed in its request frame");
                    string identity = "#" + request.Ordinal + " " + request.Cue + " frame " + request.Frame
                        + " (" + where() + "; " + wanted?.Source + ")";
                    if (missing.Count == 0)
                        Pictures.Add(identity + ": " + string.Join(" | ", paths.Select(path => path + "='" + Read(now, path) + "'")));
                    else Silent.Add(identity + ": " + string.Join("; ", missing));
                    pending.Remove(request);
                }
                lastFoot = now;
            }

            private static string Read(Dictionary<string, string> labels, string path) =>
                labels.TryGetValue(path, out var words) ? words : null;

            private static bool ScreenLabel(TMP_Text label) =>
                label != null && label.isActiveAndEnabled && !string.IsNullOrWhiteSpace(label.text)
                && label.canvas != null && label.canvas.rootCanvas.renderMode != RenderMode.WorldSpace;

            private static Dictionary<string, string> LabelsOnScreen()
            {
                var byPlace = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!ScreenLabel(label)) continue;
                    string place = Place(label.transform, label.canvas.rootCanvas.transform);
                    if (!byPlace.TryGetValue(place, out var words)) byPlace[place] = words = new List<string>();
                    words.Add(label.text);
                }
                return byPlace.ToDictionary(pair => pair.Key,
                    pair => string.Join("\u0001", pair.Value.OrderBy(words => words, StringComparer.Ordinal)), StringComparer.Ordinal);
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

        // Diagnostic field reads only: no private method, callback, setter, RNG or state writer is
        // invoked. Expectations come from the actual presentation's payload, not from the text
        // being checked; anonymous house ballots retain no voter identity.
        private static T SoundSourceField<T>(object source, string name)
        {
            var field = source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Missing sound source field: " + name);
            return (T)field.GetValue(source);
        }

        // The private roster entry exposes these actual label/source fields publicly. Reading
        // them does not invoke private methods or setters, nor accept an unrelated name/SAFE pair.
        private static T SoundRosterField<T>(object source, string name)
        {
            var field = source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException("Missing sound roster field: " + name);
            return (T)field.GetValue(source);
        }

        private static string MemoryPicturePair(CompetitionGameScreen screen, MiniGameRun run)
        {
            var previouslyShown = SoundSourceField<bool[]>(screen, "pairShown");
            int face = Enumerable.Range(0, 8).Reverse().First(value => !previouslyShown[value]
                && Enumerable.Range(0, run.Faces.Count).Count(index => run.Matched[index] && run.Faces[index] == value) == 2);
            return "Pair found: " + CompetitionGameScreen.MemoryFaceName(face);
        }

        private SoundPictureWatch.Expected InformationPictureFor(HouseAudio.Cue cue)
        {
            var key = SceneComponents<KeyCeremony>().SingleOrDefault(item => item.IsPlaying);
            if ((cue == HouseAudio.Cue.Save || cue == HouseAudio.Cue.Nomination) && key != null)
            {
                var safe = SoundSourceField<List<KeyCeremony.Person>>(key, "safe");
                var nominated = SoundSourceField<List<KeyCeremony.Person>>(key, "nominated");
                if (cue == HouseAudio.Cue.Save)
                {
                    int shown = key.KeysShown;
                    if (shown <= 0 || shown > safe.Count) throw new InvalidOperationException("Save without an actual revealed key.");
                    var picture = new SoundPictureWatch.Expected("key " + shown + "/" + safe.Count + " for " + safe[shown - 1].Id)
                        .At(key, "Progress", SoundSourceField<bool>(key, "lastKeyPending") ? "One key left" : "Revealing key " + shown + " of " + safe.Count);
                    // Pending hides only the last, unrevealed stage recipient. A coalesced Save
                    // can still name the final already-revealed safe person on their actual roster
                    // row. This is that source-keyed row, never a generic name/SAFE text pairing;
                    // each request is retained, without claiming unseen intermediate subframes.
                    if (!SoundSourceField<bool>(key, "lastKeyPending"))
                        picture.At(key, "Name", safe[shown - 1].Name).At(key, "Badge text", "SAFE");
                    else
                    {
                        var person = safe[shown - 1];
                        if (string.IsNullOrEmpty(person.Id)) throw new InvalidOperationException("Revealed safe roster identity is missing.");
                        var entries = SoundSourceField<IEnumerable>(key, "roster").Cast<object>()
                            .Where(entry => string.Equals(SoundRosterField<KeyCeremony.Person>(entry, "Person").Id,
                                person.Id, StringComparison.Ordinal)).ToArray();
                        if (entries.Length != 1) throw new InvalidOperationException("Revealed safe identity must have exactly one actual roster row.");
                        var row = entries[0];
                        if (SoundRosterField<KeyCeremony.Person>(row, "Person").Name != person.Name
                            || SoundRosterField<bool>(row, "HeadOfHousehold"))
                            throw new InvalidOperationException("Revealed safe roster row has a different source name or role.");
                        var name = SoundRosterField<TMP_Text>(row, "Name");
                        var badge = SoundRosterField<TMP_Text>(row, "ChipText");
                        if (name == null || badge == null) throw new InvalidOperationException("Revealed safe roster labels are missing.");
                        picture.Label(name, person.Name).Label(badge, "SAFE");
                    }
                    return picture;
                }
                var block = new SoundPictureWatch.Expected("actual nominated IDs " + string.Join(",", nominated.Select(person => person.Id)))
                    .At(key, "Progress", nominated.Count == 2 ? "Nominated for eviction" : "On the block");
                foreach (var person in nominated) block.At(key, "Nominee", person.Name);
                return block;
            }

            var jury = SceneComponents<JuryReveal>().SingleOrDefault(item => item.IsPlaying);
            if ((cue == HouseAudio.Cue.Vote || cue == HouseAudio.Cue.Finale) && jury != null)
            {
                var finalists = SoundSourceField<List<JuryReveal.Finalist>>(jury, "finalists");
                var jurors = SoundSourceField<List<JuryReveal.Juror>>(jury, "jurors");
                var counts = SoundSourceField<List<TMP_Text>>(jury, "counts");
                int shown = jury.VotesShown;
                var picture = new SoundPictureWatch.Expected("actual jury prefix " + shown + "/" + jurors.Count);
                for (int i = 0; i < finalists.Count; i++)
                {
                    picture.At(jury, "Finalist", HudPrimitives.WithYou(finalists[i].Name, finalists[i].IsPlayer));
                    picture.Label(counts[i], jurors.Take(shown).Count(vote => vote.TargetId == finalists[i].Id).ToString());
                }
                if (cue == HouseAudio.Cue.Finale)
                {
                    string winnerId = SoundSourceField<string>(jury, "winnerId");
                    var winner = finalists.Single(person => person.Id == winnerId);
                    int won = jurors.Count(vote => vote.TargetId == winnerId), other = jurors.Count - won;
                    picture.At(jury, "Result", (winner.IsPlayer ? "YOU" : winner.Name.ToUpperInvariant()) + "  ·  WINNER")
                        .At(jury, "Count", won + " TO " + other);
                }
                return picture;
            }

            var eviction = SceneComponents<VoteReveal>().SingleOrDefault(item => item.IsPlaying);
            if ((cue == HouseAudio.Cue.Vote || cue == HouseAudio.Cue.Eviction) && eviction != null)
            {
                var nominees = SoundSourceField<List<VoteReveal.Nominee>>(eviction, "nominees");
                var ballots = SoundSourceField<List<VoteReveal.Ballot>>(eviction, "house");
                var counts = SoundSourceField<List<TMP_Text>>(eviction, "counts");
                int shown = Math.Max(0, SoundSourceField<int>(eviction, "shown"));
                var picture = new SoundPictureWatch.Expected("anonymous house prefix " + shown + "/" + ballots.Count
                    + "; deciding=" + SoundSourceField<bool>(eviction, "tieBroken"));
                for (int i = 0; i < nominees.Count; i++)
                {
                    picture.At(eviction, "Nominee", nominees[i].Name);
                    picture.Label(counts[i], ballots.Take(shown).Count(vote => vote.TargetId == nominees[i].Id).ToString());
                }
                if (cue == HouseAudio.Cue.Eviction)
                    return picture.At(eviction, "Result", nominees.Single(person => person.Id == eviction.EvictedId).Name.ToUpperInvariant() + "  ·  EVICTED")
                        .At(eviction, "Progress", "All votes are in");
                bool tied = SoundSourceField<bool>(eviction, "tieBroken");
                string progress = tied ? (SoundSourceField<bool>(eviction, "hohIsPlayer")
                    ? "You have cast the deciding vote." : "The Head of Household has cast the deciding vote.")
                    : SoundSourceField<bool>(eviction, "lastVotePending") ? "One vote left" : "Revealing vote " + shown + " of " + ballots.Count;
                return picture.At(eviction, "Progress", progress);
            }

            var screen = SceneComponents<CompetitionGameScreen>().SingleOrDefault(item => item.IsShowing);
            var run = screen != null ? SoundSourceField<MiniGameRun>(screen, "run") : null;
            if (cue == HouseAudio.Cue.CompetitionStart)
            {
                if (screen != null && run != null)
                    return new SoundPictureWatch.Expected("actual " + run.Kind + " board GO")
                        .At(screen, "Competition title", run.Definition?.Title ?? CompetitionMiniGames.DisplayName(run.Kind))
                        .At(screen, "Countdown", "GO");
                // The existing timing-bar diagnostic has no MiniGameRun and no season category.
                return new SoundPictureWatch.Expected("explicit timing-bar diagnostic panel, not a naturally dealt competition")
                    .At(director, null, InputGlossary.ChallengeLine(false)).At(director, null, "STOP marker  [Space]");
            }
            if ((cue == HouseAudio.Cue.SocialUp || cue == HouseAudio.Cue.SocialDown) && run != null)
            {
                var picture = new SoundPictureWatch.Expected("actual " + run.Kind + " outcome; hits=" + run.Hits
                    + ", wrong=" + run.WrongDirections + ", expired=" + run.ExpiredTargets + ", pairs=" + run.MatchedPairs
                    + ", mistakes=" + run.WrongFlips + ", solved=" + run.WordsSolved + ", wrongWords=" + run.WrongSpellings);
                switch (run.Kind)
                {
                    case CompetitionMiniGames.Kind.Reaction:
                        return picture.At(screen, "Attempt feedback", run.Feedback)
                            .At(screen, "Progress", run.Hits + " / " + run.Spawned + " hits  ·  " + run.FalseStarts + " early", prefix: true);
                    case CompetitionMiniGames.Kind.Memory:
                        return picture.At(screen, "Progress", run.MatchedPairs + " / " + run.Pairs + " pairs  ·  "
                            + run.WrongFlips + (run.WrongFlips == 1 ? " mistake" : " mistakes"), prefix: true)
                            .At(screen, "Attempt feedback", cue == HouseAudio.Cue.SocialUp ? MemoryPicturePair(screen, run) : "No match. The cards turn back.");
                    case CompetitionMiniGames.Kind.Endurance:
                        return picture.At(screen, "Pressure label", "WAVE x" + run.GripPressure.ToString("0.0"));
                    case CompetitionMiniGames.Kind.Words:
                        return picture.At(screen, "Progress", run.WordsSolved + (run.WordsSolved == 1 ? " word" : " words")
                            + " spelled  ·  " + run.WordPoints.ToString("0.#") + " points")
                            .At(screen, "Attempt feedback", cue == HouseAudio.Cue.SocialUp ? "Solved: " + run.Feedback : "Not quite: the letters clear");
                    case CompetitionMiniGames.Kind.Dice:
                        return picture.At(screen, "Finish headline", "KEPT " + run.KeptTotal)
                            .At(screen, "Finish measure", "Total " + run.KeptTotal + " of 18  ·  roll " + run.RollsUsed + " of " + MiniGameRun.MaxRolls);
                }
            }
            var state = director.Snapshot;
            if (cue == HouseAudio.Cue.CompetitionWin)
            {
                var winner = state.Find(state.competitionScores.OrderByDescending(row => row.score).First().contestantId);
                var result = SceneComponents<CompetitionResult>().Single(item => item.IsPlaying);
                return new SoundPictureWatch.Expected("actual committed competition winner " + winner.id)
                    .At(result, "Winner", winner.isPlayer ? "You win!" : winner.name + " wins!");
            }
            if (cue == HouseAudio.Cue.Veto)
            {
                var actual = state.events.Last(entry => entry.kind == "veto");
                var sting = SceneComponents<CeremonySting>().Single(item => item.IsPlaying && item.PlayingKind == CeremonySting.VetoKind);
                return new SoundPictureWatch.Expected("actual committed veto event " + actual.sequence)
                    .At(sting, "Sting headline", CeremonySting.HeadlineFor(CeremonySting.VetoKind))
                    .At(sting, "Sting detail", actual.text);
            }
            throw new InvalidOperationException("No mapped actual source for information cue " + cue);
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

            using (var watch = new SoundPictureWatch(audio, () => director.Snapshot.phase + " week " + director.Snapshot.week, InformationPictureFor))
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
                watch.AssertAccounted();
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
            audio.SetMuted(true);
            Assert.That(audio.Muted, Is.True, "The board outcome evidence is collected while muted.");
            var expected = new List<HouseAudio.Cue> { HouseAudio.Cue.CompetitionStart };
            bool rises = kind != CompetitionMiniGames.Kind.Precision && kind != CompetitionMiniGames.Kind.Endurance;
            bool falls = kind != CompetitionMiniGames.Kind.Precision && kind != CompetitionMiniGames.Kind.Dice;
            if (rises) expected.Add(HouseAudio.Cue.SocialUp);
            if (falls) expected.Add(HouseAudio.Cue.SocialDown);

            MiniGameRun run = null;
            using (var watch = new SoundPictureWatch(audio, () => kind + (run != null ? " at " + run.Elapsed.ToString("0.00") + " s" : " at the briefing"), InformationPictureFor))
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
                watch.AssertAccounted();
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
            audio.SetMuted(true);
            Assert.That(audio.Muted, Is.True, "The installed finale fixture is observed while muted, not claimed as a played season.");
            using (var watch = new SoundPictureWatch(audio, () => director.Snapshot.phase + (SceneComponents<JuryReveal>().Any(reveal => reveal.IsPlaying) ? " under the jury's reveal" : ""), InformationPictureFor))
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
                watch.AssertAccounted();
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
