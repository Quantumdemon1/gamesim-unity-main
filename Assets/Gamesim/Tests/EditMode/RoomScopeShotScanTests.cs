using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gamesim.Episode;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The room scope's callers (ROOM-SCOPE-PLAN RS6b, R5, §3.2 a): every call under <c>Runtime/</c> that moves
    /// the main camera - <c>MoveTo</c>, <c>CutTo</c>, <c>FocusSubject</c> - is found by a scan of the scripts,
    /// not a hand-written table, and each one names its rooms in the call or is declared below: on the
    /// whole-house list (the overview, the opening and the walk-out, D1 and D6, where the shot it takes is
    /// one the scan cannot read in the call) or on the list of callers still on the default, whose view is
    /// free play's or following's and so the whole house until RS5 makes those two live and empties it.
    ///
    /// <para>What names its rooms: a timed move given its rooms (the fourth argument); a shot whose
    /// initializer sets <c>Rooms</c> or <c>RoomsFromFocus</c>, in the call, where the method builds or sets
    /// the shot it passes, or in the definition of the member it passes from the same file (a property, a
    /// field, a method such as the rig's <c>TwoShot</c>); and a cut or a follow, which carries its subject's
    /// room by the rig's rule. A shot that names nothing (the whole house by default) and a free move (the
    /// floor under its point) name no rooms. The rig's own overloads, which forward to each other, are not
    /// callers.</para>
    ///
    /// <para>Every declared entry claims its exact count of calls, so a call that goes or comes back
    /// changes the list; each whole-house entry's source has no switch (it is the whole house by decision),
    /// and each default entry's source is not live, so the list fails the slice that turns free play or
    /// following on until it is emptied. In the whole-house files every other move names
    /// <c>RoomSet.WholeHouse</c> itself.</para>
    ///
    /// <para>Unity-free and type-initializer-free (only the scope's constants and enums are read), so it runs
    /// under Unity's Mono harness as well as in the editor. The scripts are found under the folder
    /// <c>GAMESIM_SOURCE_ROOT</c> names, else above the working directory (the editor's is the project) or
    /// the test's own binary.</para>
    /// </summary>
    public sealed class RoomScopeShotScanTests
    {
        public enum Why { WholeHouse, OnTheDefault }

        private sealed class Declared
        {
            public readonly string File, Member, Code, Reason;
            public readonly int Count;
            public readonly Why Why;
            public readonly EpisodeDirector.RoomScopeSource Source;
            public Declared(string file, string member, string code, int count, Why why, EpisodeDirector.RoomScopeSource source, string reason)
            { File = file; Member = member; Code = code; Count = count; Why = why; Source = source; Reason = reason; }
            public bool Claims(Site site) => site.File == File && site.Member == Member && site.Code.Contains(Code);
            public override string ToString() => File + " " + Member + " '" + Code + "'";
        }

        /// <summary>One call that moves the main camera, as the scan read it.</summary>
        public sealed class Site
        {
            public string File, Member, Kind, Code, Evidence;
            public int Line;
            /// <summary>Whether the call names its rooms; <see cref="Evidence"/> says where.</summary>
            public bool Names;
            public override string ToString() => File + ":" + Line + " (" + Member + ") " + Code;
        }

        private const string Ep = "Runtime/Episode/", Rig = "Runtime/House/", Pres = "Runtime/Presentation/";
        private const EpisodeDirector.RoomScopeSource FreePlay = EpisodeDirector.RoomScopeSource.FreePlay;
        private const EpisodeDirector.RoomScopeSource Following = EpisodeDirector.RoomScopeSource.Following;

        /// <summary>The overview, the opening and the walk-out (D1, D6): the files whose moves are the whole house by decision.</summary>
        private static readonly (string File, EpisodeDirector.RoomScopeSource Source)[] WholeHouseFiles =
        {
            (Ep + "EpisodeDirector.Overview.cs", EpisodeDirector.RoomScopeSource.Overview),
            (Ep + "EpisodeDirector.Opening.cs", EpisodeDirector.RoomScopeSource.Opening),
            (Pres + "OpeningSequence.Cards.cs", EpisodeDirector.RoomScopeSource.Opening),
            (Ep + "EpisodeDirector.WalkOut.cs", EpisodeDirector.RoomScopeSource.WalkOut),
        };

        /// <summary>The declared callers: the whole-house list, and the callers still on the default (RS5 empties that one).</summary>
        private static readonly Declared[] Lists =
        {
            // ---- the whole house by decision (D1, D6): the call's shot is not readable in the call
            new Declared(Ep + "EpisodeDirector.Opening.cs", "HoldInFrame", "cameraRig.MoveTo(shot)", 1, Why.WholeHouse, EpisodeDirector.RoomScopeSource.Opening,
                "the introduction's shot taken again as it follows the body: a copy of the introduction's, which names the whole house"),
            new Declared(Ep + "EpisodeDirector.WalkOut.cs", "TickWalkOut", "FocusSubject(body, false)", 1, Why.WholeHouse, EpisodeDirector.RoomScopeSource.WalkOut,
                "the walk-out follows the one leaving: the whole house whatever the rig follows over it"),
            new Declared(Ep + "EpisodeDirector.WalkOut.cs", "SkipWalkOut", "MoveTo(ExitDoorShot)", 1, Why.WholeHouse, EpisodeDirector.RoomScopeSource.WalkOut,
                "the exit's door shot on a press: the living room's or the yard's door shot, each naming the whole house"),

            // ---- still on the default: free play's and following's views, the whole house until RS5
            new Declared(Ep + "EpisodeDirector.Camera.cs", "FollowHouseguest", "FocusSubject(body)", 1, Why.OnTheDefault, Following,
                "a follow from the cast rail: their room, tracked, once following is live (RS5)"),
            new Declared(Ep + "EpisodeDirector.Camera.cs", "FollowNext", "FocusSubject(ring[next])", 1, Why.OnTheDefault, Following,
                "Tab and the brackets: the next houseguest's room (RS5)"),
            new Declared(Ep + "EpisodeDirector.CeremonyFraming.cs", "FrameCeremonyRoutine", "MoveTo(focus, distance,", 1, Why.OnTheDefault, FreePlay,
                "a framing's move back: the framing is over as it starts, so free play's view; it names no rooms (the floor under its point) for RS5 to decide"),
            new Declared(Ep + "EpisodeDirector.Conversation.cs", "SelectHouseguest", "FocusSubject(player.transform, false)", 1, Why.OnTheDefault, FreePlay,
                "the walk over to talk to someone: the player's room (RS5)"),
            new Declared(Ep + "EpisodeDirector.Opening.cs", "OpeningFinished", "FocusSubject(player.transform, reframe: false)", 1, Why.OnTheDefault, FreePlay,
                "the opening's hand-over (D6): the player's room from the call, free play's view; RS5 makes it the cut onto the player's room's framing"),
            new Declared(Ep + "EpisodeDirector.TalkSpots.cs", "TryWalkToTalkSpot", "FocusSubject(player.transform, false)", 1, Why.OnTheDefault, FreePlay,
                "the walk to a talk spot: the player's room (RS5)"),
            new Declared(Ep + "EpisodeDirector.Travel.cs", "TryTravel", "CutTo(player.transform)", 1, Why.OnTheDefault, FreePlay,
                "travel's warp behind the blink: the player's room at once (RS5)"),
            new Declared(Ep + "EpisodeDirector.Travel.cs", "TryTravel", "FocusSubject(player.transform, false)", 1, Why.OnTheDefault, FreePlay,
                "travel on foot: the player's room (RS5)"),
            new Declared(Ep + "EpisodeDirector.WalkOut.cs", "FinishWalkOut", "FocusSubject(player.transform, false)", 1, Why.OnTheDefault, FreePlay,
                "an unstaged walk-out hands the camera back to the player (RS5)"),
            new Declared(Rig + "HousePlayerController.cs", "DispatchClick", "FocusSubject(seatedGuest.transform)", 1, Why.OnTheDefault, Following,
                "a click on a seated houseguest follows them (RS5)"),
            new Declared(Rig + "HousePlayerController.cs", "DispatchClick", "FocusSubject(houseguest.transform)", 1, Why.OnTheDefault, Following,
                "a click on a houseguest follows them (RS5)"),
            new Declared(Rig + "HousePlayerController.cs", "DispatchClick", "FocusSubject(transform)", 1, Why.OnTheDefault, FreePlay,
                "a click on the player: free play (RS5)"),
            new Declared(Rig + "HousePlayerController.cs", "DispatchClick", "FocusSubject(transform, false)", 1, Why.OnTheDefault, FreePlay,
                "a floor click: free play, the player's room (RS5)"),
        };

        [Test]
        public void RoomScope_EveryShotNamesItsRooms()
        {
            var sites = Scan();
            Assert.That(sites.Count, Is.GreaterThanOrEqualTo(30), "The scan finds the house's camera moves: " + sites.Count + ".");
            foreach (var kind in new[] { "MoveTo", "CutTo", "FocusSubject" })
                Assert.That(sites.Any(site => site.Kind == kind), Is.True, "The scan finds " + kind + "'s callers.");
            var failures = new List<string>();

            // The lists stay exact: each entry claims its count of calls, its source is the one it says.
            var switches = Enum.GetNames(typeof(EpisodeDirector.RoomScopeSources));
            foreach (var entry in Lists)
            {
                int claimed = sites.Count(entry.Claims);
                if (claimed != entry.Count) failures.Add(entry + " claims " + claimed + " calls, not " + entry.Count);
                bool hasSwitch = switches.Contains(entry.Source.ToString());
                if (entry.Why == Why.WholeHouse && hasSwitch)
                    failures.Add(entry + ": " + entry.Source + " has a switch, so it is not the whole house by decision");
                if (entry.Why == Why.OnTheDefault)
                {
                    if (entry.Source != FreePlay && entry.Source != Following)
                        failures.Add(entry + ": only free play and following are on the default (RS5), not " + entry.Source);
                    if (Live(entry.Source))
                        failures.Add(entry + ": " + entry.Source + " is live, so this caller is no longer on the default: name its rooms and take it off the list");
                }
            }

            // Every call names its rooms or is declared.
            foreach (var site in sites)
            {
                var claims = Lists.Where(entry => entry.Claims(site)).ToList();
                if (claims.Count > 1) failures.Add(site + " is claimed twice");
                if (!site.Names && claims.Count == 0) failures.Add(site + " names no rooms (" + site.Evidence + ") and is on neither list");
            }

            // In the whole-house files every move names the whole house, unless it is declared.
            foreach (var (file, source) in WholeHouseFiles)
            {
                Assert.That(switches.Contains(source.ToString()), Is.False, source + " is the whole house by decision: it has no switch.");
                var inFile = sites.Where(site => site.File == file).ToList();
                if (inFile.Count == 0) failures.Add(file + " moves the camera nowhere: the whole-house list names a file that has gone");
                foreach (var site in inFile.Where(site => !Lists.Any(entry => entry.Claims(site))))
                    if (site.Kind != "MoveTo" || site.Evidence == null || !site.Evidence.Contains("RoomSet.WholeHouse"))
                        failures.Add(site + " is in a whole-house file (" + source + ") and does not name RoomSet.WholeHouse: " + site.Evidence);
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            int onTheDefault = Lists.Where(entry => entry.Why == Why.OnTheDefault).Sum(entry => entry.Count);
            Console.WriteLine("[Gamesim] RS6b: " + sites.Count + " camera moves under Runtime/, " + sites.Count(site => site.Names) + " naming their rooms, "
                + onTheDefault + " still on the default until RS5.");
        }

        [Test]
        public void RoomScope_TheShotScanReadsCodeAndTheRoomsItNames()
        {
            const string source = @"
namespace Gamesim.Sample
{
    public sealed partial class Sample
    {
        private static HouseCameraRig.Shot Door => new HouseCameraRig.Shot { Focus = f, Rooms = RoomSet.WholeHouse };
        private static HouseCameraRig.Shot Bare => new HouseCameraRig.Shot { Focus = f };
        private HouseCameraRig.Shot Built(int x) { return new HouseCameraRig.Shot { Focus = f, RoomsFromFocus = true }; }
        [SomeAttribute(1)]
        private void Inline() { rig.MoveTo(new HouseCameraRig.Shot { Focus = f, Rooms = RoomSet.Of(""Yard"") }); }
        private void Nothing() { rig.MoveTo(new HouseCameraRig.Shot { Focus = f, Seconds = 1f }); }
        private void Free() { rig.MoveTo(point, 12f); rig.MoveTo(point, 12f, 1.2f); }
        private void FreeNamed() { rig.MoveTo(point, 12f, 0f, RoomSet.Of(room)); }
        private void SetBefore() { var shot = Bare; shot.Seconds = 1f; shot.Rooms = RoomSet.WholeHouse; rig?.MoveTo(shot); }
        private void Copied() { var shot = held; shot.Seconds = 1f; rig.MoveTo(shot); }
        private void FromMember() { if (rig != null) rig.MoveTo(Door); director.cameraRig.MoveTo(world.Door); }
        private void FromBare() { rig.MoveTo(Bare); }
        private void LocalFromMember() { var door = Door; door.Seconds = .01f; rig.MoveTo(door); }
        private void FromMethod() { rig.MoveTo(Built(2)); }
        private void Follow() { rig.FocusSubject(body, false); rig?.CutTo(body); }
        public float Property { get { rig.MoveTo(Bare); return 1f; } }
        private void Words()
        {
            // rig.MoveTo(new HouseCameraRig.Shot { Rooms = RoomSet.WholeHouse }); in a comment
            /* rig.CutTo(body); */
            Log(""rig.FocusSubject(body) in a string"");
            seasonPlayer.TryMoveTo(point);
        }
        public void MoveTo(Vector3 focus, float distance) { MoveTo(focus, distance, 0f); }
    }
}";
            var sites = ScanSource("Runtime/Sample.cs", source);
            string Read(Site site) => site.Member + ":" + site.Kind + ":" + (site.Names ? "names" : "none");
            Assert.That(sites.Select(Read).ToArray(), Is.EqualTo(new[]
            {
                "Inline:MoveTo:names", "Nothing:MoveTo:none", "Free:MoveTo:none", "Free:MoveTo:none", "FreeNamed:MoveTo:names",
                "SetBefore:MoveTo:names", "Copied:MoveTo:none", "FromMember:MoveTo:names", "FromMember:MoveTo:names", "FromBare:MoveTo:none",
                "LocalFromMember:MoveTo:names", "FromMethod:MoveTo:names", "Follow:FocusSubject:names", "Follow:CutTo:names",
                "Property:MoveTo:none", "MoveTo:MoveTo:none",
            }), "Calls in code, in order, by their member: comments, strings, TryMoveTo and declarations are not calls; " + string.Join(", ", sites.Select(Read)));
            Assert.That(sites.Single(site => site.Member == "Inline").Evidence, Does.Contain("Rooms = RoomSet.Of"), "The inline shot's rooms are its evidence.");
            Assert.That(sites.First(site => site.Member == "FromMember").Evidence, Does.Contain("RoomSet.WholeHouse"), "and a member's are its definition's.");
            Assert.That(sites.Single(site => site.Member == "SetBefore").Evidence, Does.Contain("shot.Rooms = RoomSet.WholeHouse"), "and a shot set before the call, its setting.");
            Assert.That(sites.Single(site => site.Member == "Nothing").Line, Is.EqualTo(11), "Lines are the source's own, comments and strings kept as blank space.");
            // The rig's own overloads forward to each other: in its own file they are not callers.
            Assert.That(ScanSource(Rig + "HouseCameraRig.cs", source).Any(site => site.Member == "MoveTo"), Is.False,
                "In the rig's own file, a call inside its MoveTo is the rig's, not a caller.");
        }

        // ------------------------------------------------------------ the scan

        private static bool Live(EpisodeDirector.RoomScopeSource source)
            => ((int)EpisodeDirector.LiveRoomScopeSources & (1 << (int)source)) != 0;

        private static List<Site> Scan()
        {
            var sites = new List<Site>();
            string root = GamesimRoot();
            foreach (var path in Directory.GetFiles(Path.Combine(root, "Runtime"), "*.cs", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                string file = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                sites.AddRange(ScanSource(file, File.ReadAllText(path)));
            }
            return sites;
        }

        private static readonly Regex Call = new Regex(@"(?<![\w])(?<kind>MoveTo|CutTo|FocusSubject)\s*\(", RegexOptions.Compiled);
        private static readonly Regex Declaration = new Regex(
            @"^\s*((public|private|protected|internal|static|override|virtual|sealed|async|unsafe|new|extern)\s+)+[\w\.<>\[\], ]+\s+$", RegexOptions.Compiled);
        private static readonly Regex NamesRooms = new Regex(@"\bRooms(FromFocus)?\s*=(?![=>])\s*(?<value>[^,;}\r\n]*)", RegexOptions.Compiled);
        private static readonly Regex TypeHeader = new Regex(@"^\s*(\w+\s+)*(class|struct|interface|record|enum|namespace)\s+[\w\.]", RegexOptions.Compiled);
        private static readonly Regex Chain = new Regex(@"^[A-Za-z_]\w*(\s*\??\.\s*[A-Za-z_]\w*)*", RegexOptions.Compiled);

        /// <summary>The calls that move the camera in one script, read from its code with comments and literals blanked out.</summary>
        public static List<Site> ScanSource(string file, string source)
        {
            string code = Strip(source);
            var sites = new List<Site>();
            bool rigFile = Path.GetFileName(file) == "HouseCameraRig.cs";
            foreach (Match match in Call.Matches(code))
            {
                int at = match.Index;
                int lineStart = code.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
                if (Declaration.IsMatch(code.Substring(lineStart, at - lineStart))) continue;
                int open = match.Index + match.Length - 1;
                int close = Matching(code, open);
                if (close < 0) continue;
                string kind = match.Groups["kind"].Value;
                Enclosing(code, at, out string member, out int bodyStart);
                if (rigFile && (member == "MoveTo" || member == "CutTo" || member == "FocusSubject")) continue;
                int line = 1;
                for (int i = 0; i < at; i++) if (code[i] == '\n') line++;
                int lineEnd = code.IndexOf('\n', at);
                var site = new Site
                {
                    File = file, Member = member, Kind = kind, Line = line,
                    Code = code.Substring(lineStart, (lineEnd < 0 ? code.Length : lineEnd) - lineStart).Trim(),
                };
                site.Names = NamesItsRooms(code, kind, code.Substring(open + 1, close - open - 1), bodyStart, at, out site.Evidence);
                sites.Add(site);
            }
            return sites;
        }

        /// <summary>Whether a call names its rooms, and where: the call, the method's shot, or a member's definition.</summary>
        private static bool NamesItsRooms(string code, string kind, string arguments, int bodyStart, int at, out string evidence)
        {
            if (kind != "MoveTo") { evidence = "the subject's room: " + arguments.Trim(); return true; }
            var parts = SplitTop(arguments);
            if (parts.Count >= 4) { evidence = "named in the call: " + parts[3].Trim(); return true; }
            var inline = NamesRooms.Match(arguments);
            if (inline.Success) { evidence = inline.Value.Trim(); return true; }
            if (parts.Count != 1) { evidence = "a free move: the floor under its point"; return false; }
            string expression = parts[0].Trim();
            if (expression.StartsWith("new ", StringComparison.Ordinal) || expression.StartsWith("new\n", StringComparison.Ordinal))
            { evidence = "a shot that names no rooms"; return false; }
            var chain = Chain.Match(expression);
            if (!chain.Success) { evidence = "a shot the scan cannot read: " + expression; return false; }
            var names = Regex.Split(chain.Value, @"\s*\??\.\s*");
            string local = names[0], last = names[names.Length - 1];
            if (names.Length == 1)
            {
                string before = code.Substring(bodyStart, at - bodyStart);
                var set = Regex.Matches(before, @"\b" + Regex.Escape(local) + @"\s*\.\s*Rooms(FromFocus)?\s*=(?![=>])\s*[^,;}\r\n]*");
                if (set.Count > 0) { evidence = set[set.Count - 1].Value.Trim(); return true; }
                var assigned = Regex.Matches(before, @"\b" + Regex.Escape(local) + @"\s*=(?![=>])");
                if (assigned.Count > 0)
                {
                    int from = assigned[assigned.Count - 1].Index + assigned[assigned.Count - 1].Length;
                    int end = StatementEnd(before, from);
                    string value = before.Substring(from, end - from);
                    var named = NamesRooms.Match(value);
                    if (named.Success) { evidence = named.Value.Trim(); return true; }
                    var rhs = Chain.Match(value.Trim());
                    if (rhs.Success && !value.Trim().StartsWith("new", StringComparison.Ordinal))
                    {
                        var rhsNames = Regex.Split(rhs.Value, @"\s*\??\.\s*");
                        if (MemberNames(code, rhsNames[rhsNames.Length - 1], out evidence)) return true;
                    }
                    evidence = local + " is a shot that names no rooms: " + value.Trim();
                    return false;
                }
            }
            if (MemberNames(code, last, out evidence)) return true;
            evidence = evidence ?? ("a shot that names no rooms: " + expression);
            return false;
        }

        /// <summary>Whether the file defines a shot member of that name - a property, a field or a method - whose definition names rooms.</summary>
        private static bool MemberNames(string code, string name, out string evidence)
        {
            evidence = null;
            foreach (Match match in Regex.Matches(code, @"\bShot\s*(\[\])?\s+" + Regex.Escape(name) + @"\b\s*(?=[\(\{=])"))
            {
                int p = match.Index + match.Length;
                if (code[p] == '(') { int shut = Matching(code, p); if (shut < 0) continue; p = shut + 1; }
                while (p < code.Length && char.IsWhiteSpace(code[p])) p++;
                int end;
                if (p < code.Length && code[p] == '{') { end = Matching(code, p); if (end < 0) continue; end++; }
                else end = StatementEnd(code, p);
                var named = NamesRooms.Match(code.Substring(match.Index, end - match.Index));
                if (named.Success) { evidence = name + "'s definition: " + named.Value.Trim(); return true; }
                evidence = name + "'s definition names no rooms";
            }
            return false;
        }

        /// <summary>
        /// The member a position stands in: the outermost block that is not a type's or a namespace's, named by
        /// its header - a method, a property, an accessor's property - and where its body starts. At a type's
        /// own level (a field's initializer, an expression-bodied member), the statement's.
        /// </summary>
        private static void Enclosing(string code, int at, out string member, out int bodyStart)
        {
            var open = new Stack<int>();
            for (int i = 0; i < at; i++)
            {
                if (code[i] == '{') open.Push(i);
                else if (code[i] == '}' && open.Count > 0) open.Pop();
            }
            foreach (int brace in open.Reverse())
            {
                string header = Header(code, brace);
                if (TypeHeader.IsMatch(header)) continue;
                member = NameOf(header);
                bodyStart = brace;
                return;
            }
            int boundary = Math.Max(code.LastIndexOf(';', Math.Max(0, at - 1)), Math.Max(code.LastIndexOf('{', Math.Max(0, at - 1)), code.LastIndexOf('}', Math.Max(0, at - 1))));
            member = NameOf(code.Substring(boundary + 1, at - boundary - 1));
            bodyStart = boundary + 1;
        }

        /// <summary>A block's header: the code between the last statement boundary and its brace, attributes dropped.</summary>
        private static string Header(string code, int brace)
        {
            int boundary = Math.Max(code.LastIndexOf(';', Math.Max(0, brace - 1)), Math.Max(code.LastIndexOf('{', Math.Max(0, brace - 1)), code.LastIndexOf('}', Math.Max(0, brace - 1))));
            string header = code.Substring(boundary + 1, brace - boundary - 1);
            return Regex.Replace(header, @"^\s*(\[[^\]]*\]\s*)+", " ");
        }

        /// <summary>A member's name from its header: the last name before its parameters, its arrow or its initializer.</summary>
        private static string NameOf(string header)
        {
            int cut = header.Length;
            foreach (var mark in new[] { "(", "=>", "=" })
            {
                int index = header.IndexOf(mark, StringComparison.Ordinal);
                if (index >= 0 && index < cut) cut = index;
            }
            string head = Regex.Replace(header.Substring(0, cut), @"<[^<>]*>", "");
            var words = Regex.Matches(head, @"[A-Za-z_]\w*");
            return words.Count > 0 ? words[words.Count - 1].Value : "";
        }

        /// <summary>The index of the bracket that closes the one at <paramref name="open"/>, or -1.</summary>
        private static int Matching(string code, int open)
        {
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        /// <summary>Where the statement from <paramref name="from"/> ends: its semicolon at its own depth, or the text's end.</summary>
        private static int StatementEnd(string code, int from)
        {
            int depth = 0;
            for (int i = from; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') { if (depth == 0) return i; depth--; }
                else if (c == ';' && depth == 0) return i;
            }
            return code.Length;
        }

        /// <summary>The arguments, split at their own commas.</summary>
        private static List<string> SplitTop(string arguments)
        {
            var parts = new List<string>();
            if (arguments.Trim().Length == 0) return parts;
            int depth = 0, start = 0;
            for (int i = 0; i < arguments.Length; i++)
            {
                char c = arguments[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0) { parts.Add(arguments.Substring(start, i - start)); start = i + 1; }
            }
            parts.Add(arguments.Substring(start));
            return parts;
        }

        /// <summary>The source with comments and the insides of string and character literals blanked: same length, same lines.</summary>
        private static string Strip(string source)
        {
            var code = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < source.Length && source[i] != '\n') { code.Append(' '); i++; }
                    if (i < source.Length) code.Append('\n');
                    continue;
                }
                if (c == '/' && next == '*')
                {
                    code.Append("  "); i += 2;
                    while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/')) { code.Append(source[i] == '\n' ? '\n' : ' '); i++; }
                    if (i < source.Length) { code.Append("  "); i++; }
                    continue;
                }
                if (c == '"' || (c == '@' && next == '"') || (c == '$' && next == '"') || (c == '$' && next == '@') || (c == '@' && next == '$'))
                {
                    bool verbatim = false;
                    while (source[i] != '"') { verbatim |= source[i] == '@'; code.Append(source[i]); i++; }
                    code.Append('"'); i++;
                    while (i < source.Length)
                    {
                        if (!verbatim && source[i] == '\\' && i + 1 < source.Length) { code.Append("  "); i += 2; continue; }
                        if (source[i] == '"')
                        {
                            if (verbatim && i + 1 < source.Length && source[i + 1] == '"') { code.Append("  "); i += 2; continue; }
                            break;
                        }
                        code.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < source.Length) code.Append('"');
                    continue;
                }
                if (c == '\'')
                {
                    code.Append('\''); i++;
                    while (i < source.Length && source[i] != '\'') { if (source[i] == '\\') { code.Append(' '); i++; } code.Append(' '); i++; }
                    if (i < source.Length) code.Append('\'');
                    continue;
                }
                code.Append(c);
            }
            return code.ToString();
        }

        private static string GamesimRoot()
        {
            var starts = new[] { Environment.GetEnvironmentVariable("GAMESIM_SOURCE_ROOT"), Environment.CurrentDirectory, AppContext.BaseDirectory };
            foreach (var start in starts.Where(start => !string.IsNullOrEmpty(start)))
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string candidate = Path.Combine(directory.FullName, "Assets", "Gamesim");
                    if (Directory.Exists(Path.Combine(candidate, "Runtime"))) return Path.GetFullPath(candidate);
                }
            Assert.Fail("Assets/Gamesim was not found above " + string.Join(" or ", starts.Where(start => !string.IsNullOrEmpty(start))));
            return null;
        }
    }
}
