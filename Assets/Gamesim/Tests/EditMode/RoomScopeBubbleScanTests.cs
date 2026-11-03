using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The invitation bubble's speaker rule (ROOM-SCOPE-PLAN §3.2 e, RS4) on both of the director's visits: no line
    /// over a houseguest the room scope does not draw. The bubble is one component, shown by the invitation (asks
    /// off) and by an ask's walk-over (asks on, the editor's and the standalone player's default), so the rule is
    /// read off the scripts for each: every Show of the director's bubble under <c>Runtime/</c> is the statement
    /// after one that sets <c>SpeakerDrawn</c> from <c>IsBodyDrawn(HousemateBody(id))</c> for the very speaker it
    /// names (<c>projected.Find(id).name</c>); and the room scope's late pass and its prompt rule, which keep the
    /// bubble gated and its speaker drawn while it is up, read the speaker as <c>CurrentNpcInvitationNpcId</c> - the
    /// visit's in flight, whichever way it came - and never one path's own field. Every PlayMode fixture but the
    /// asks' own runs with the asks off, so this is what holds the ask's line to the rule.
    ///
    /// <para>Unity-free, like <see cref="RoomScopeShotScanTests"/>, whose source root and comment stripping it
    /// shares, so it runs under Unity's Mono harness as well as in the editor.</para>
    /// </summary>
    public sealed class RoomScopeBubbleScanTests
    {
        private const string ScopeFile = "Runtime/Episode/EpisodeDirector.RoomScope.cs";
        private const string Speaker = "CurrentNpcInvitationNpcId";

        /// <summary>One Show of the director's bubble, as the scan read it.</summary>
        public sealed class Site
        {
            public string File, Named, Gated;
            public int Line;
            /// <summary>Whether the statement before it sets the bubble's speaker drawn for the speaker it names.</summary>
            public bool Holds => Named != null && Gated == Named;
            public override string ToString() => File + ":" + Line + " names " + (Named ?? "nobody the scan reads")
                + ", after " + (Gated != null ? "SpeakerDrawn for " + Gated : "no SpeakerDrawn");
        }

        private static readonly Regex ShowCall = new Regex(@"(?<![\w\.])npcInvitationBubble\s*\??\.\s*Show\s*\(", RegexOptions.Compiled);
        private static readonly Regex Gate = new Regex(
            @"^\s*npcInvitationBubble\s*\.\s*SpeakerDrawn\s*=\s*IsBodyDrawn\s*\(\s*HousemateBody\s*\(\s*(?<id>[\w\.]+)\s*\)\s*\)\s*$", RegexOptions.Compiled);
        private static readonly Regex NamedArgument = new Regex(@"^\s*projected\s*\.\s*Find\s*\(\s*(?<id>[\w\.]+)\s*\)\s*\.\s*name\s*$", RegexOptions.Compiled);
        private static readonly Regex OnePathsField = new Regex(@"(?<![\w\.])(npcInvitationNow|askNow)(?!\w)", RegexOptions.Compiled);
        private static readonly Regex SpeakerDefinition = new Regex(@"(?<![\w\.])" + Speaker + @"\s*=>(?<value>[^;]*);", RegexOptions.Compiled);

        [Test]
        public void RoomScope_EveryInvitationLineWaitsForADrawnSpeaker()
        {
            string root = RoomScopeShotScanTests.GamesimRoot();
            var sites = new List<Site>();
            string scope = null, definition = null;
            foreach (var path in Directory.GetFiles(Path.Combine(root, "Runtime"), "*.cs", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                string file = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                string source = File.ReadAllText(path);
                sites.AddRange(ScanSource(file, source));
                if (file == ScopeFile) scope = source;
                var defined = SpeakerDefinition.Match(RoomScopeShotScanTests.Strip(source));
                if (defined.Success) definition = defined.Groups["value"].Value;
            }
            Assert.That(sites.Select(site => site.File).Distinct(), Is.SupersetOf(new[]
            {
                "Runtime/Episode/EpisodeDirector.NpcInvitations.cs", "Runtime/Episode/EpisodeDirector.ComesToYou.cs",
            }), "The scan finds the invitation's Show and the ask's: " + string.Join("; ", sites) + ".");
            var failures = sites.Where(site => !site.Holds)
                .Select(site => site + ": set npcInvitationBubble.SpeakerDrawn = IsBodyDrawn(HousemateBody(<its speaker>)) just before it")
                .ToList();

            // The speaker the scope reads follows the switch: the ask's asker with the asks on, the invitation's speaker off.
            Assert.That(definition, Is.Not.Null, Speaker + " is defined under Runtime/.");
            foreach (var term in new[] { "AsksOn", "askNow", "npcInvitationNow" })
                if (!Regex.IsMatch(definition, @"(?<![\w\.])" + term + @"(?!\w)"))
                    failures.Add(Speaker + " => " + definition.Trim() + " does not read " + term + ": it is not the visit's in flight on both paths");
            Assert.That(scope, Is.Not.Null, ScopeFile + " is under Runtime/.");
            failures.AddRange(ScopeProblems(scope));

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            Console.WriteLine("[Gamesim] RS4: " + sites.Count + " shows of the invitation bubble under Runtime/, each over a speaker the room scope draws.");
        }

        [Test]
        public void RoomScope_TheBubbleScanReadsTheGateAndItsSpeaker()
        {
            const string source = @"
namespace Gamesim.Sample
{
    public sealed partial class Sample
    {
        private void Gated()
        {
            // npcInvitationBubble.Show(projected.Find(nobody).name) in a comment is no call
            npcInvitationBubble.SpeakerDrawn = IsBodyDrawn(HousemateBody(ask.askerId));
            npcInvitationBubble.Show(projected.Find(ask.askerId).name, ask.line, TextScale, anchor,
                () => HearAsk(view, generation, ask), () => DeferAsk(view, generation, ask), view);
        }
        private void Ungated()
        {
            TurnAskerToPlayer(ask.askerId, true);
            npcInvitationBubble.Show(projected.Find(ask.askerId).name, ask.line, TextScale, anchor, () => { }, () => { });
        }
        private void SomebodyElse()
        {
            npcInvitationBubble.SpeakerDrawn = IsBodyDrawn(HousemateBody(invitation.NpcId));
            npcInvitationBubble.Show(projected.Find(ask.askerId).name, ask.line, TextScale, anchor, () => { }, () => { });
        }
        private void NotJustBefore()
        {
            npcInvitationBubble.SpeakerDrawn = IsBodyDrawn(HousemateBody(ask.askerId));
            Log(""npcInvitationBubble.Show(projected.Find(ask.askerId).name);"");
            npcInvitationBubble?.Show(projected.Find(ask.askerId).name, ask.line, TextScale, anchor, () => { }, () => { });
        }
        private void FirstInItsBlock()
        {
            { npcInvitationBubble.Show(projected.Find(ask.askerId).name, ask.line, TextScale, anchor, () => { }, () => { }); }
        }
        private void OtherBubble() { someBubble.Show(projected.Find(ask.askerId).name, ask.line, 1f, anchor, () => { }, () => { }); }
    }
}";
            var sites = ScanSource("Sample.cs", source);
            Assert.That(sites.Count, Is.EqualTo(5), "Five calls: not the comment, the literal or another bubble's: " + string.Join("; ", sites));
            Assert.That(sites[0].Holds, Is.True, "Gated for the speaker it names: " + sites[0]);
            Assert.That(sites[0].Line, Is.EqualTo(10), "Its line: " + sites[0]);
            Assert.That(sites[0].Named, Is.EqualTo("ask.askerId"));
            Assert.That(sites[1].Holds, Is.False, "No gate: " + sites[1]);
            Assert.That(sites[1].Gated, Is.Null);
            Assert.That(sites[2].Holds, Is.False, "Gated for somebody else: " + sites[2]);
            Assert.That(sites[2].Gated, Is.EqualTo("invitation.NpcId"));
            Assert.That(sites[3].Holds, Is.False, "The gate a statement earlier is not just before: " + sites[3]);
            Assert.That(sites[4].Holds, Is.False, "First in its block, with nothing before it: " + sites[4]);

            const string scopeReadsTheVisit = @"
        private bool NamedByPrompt(HouseNpc npc)
        {
            string speaker = CurrentNpcInvitationNpcId;
            return speaker != null && npcInvitationBubble != null && npcInvitationBubble.IsVisible && npc.Id == speaker;
        }
        private void GatePromptsOnDrawnBodies()
        {
            // npcInvitationNow, in a comment, is no read
            string speaker = CurrentNpcInvitationNpcId;
            npcInvitationBubble.SpeakerDrawn = speaker == null || IsBodyDrawn(HousemateBody(speaker));
        }";
            Assert.That(ScopeProblems(scopeReadsTheVisit), Is.Empty, "The scope reading the visit in flight holds.");
            const string scopeReadsOnePath = @"
        private bool NamedByPrompt(HouseNpc npc)
        {
            return npcInvitationNow != null && npcInvitationBubble != null && npcInvitationBubble.IsVisible && npc.Id == npcInvitationNow.NpcId;
        }
        private void GatePromptsOnDrawnBodies()
        {
            npcInvitationBubble.SpeakerDrawn = npcInvitationNow == null || IsBodyDrawn(HousemateBody(npcInvitationNow.NpcId));
        }";
            var problems = ScopeProblems(scopeReadsOnePath);
            Assert.That(problems.Count(problem => problem.Contains("reads npcInvitationNow")), Is.EqualTo(4), string.Join("\n", problems));
            Assert.That(problems.Count(problem => problem.Contains("does not read " + Speaker)), Is.EqualTo(2), string.Join("\n", problems));
            Assert.That(ScopeProblems("private void Other() { }").Count(problem => problem.Contains("is gone")), Is.EqualTo(2),
                "A scope with neither member is reported, not passed.");
        }

        /// <summary>The Shows of the director's bubble in one script, read from its code with comments and literals blanked out.</summary>
        public static List<Site> ScanSource(string file, string source)
        {
            string code = RoomScopeShotScanTests.Strip(source);
            var sites = new List<Site>();
            foreach (Match call in ShowCall.Matches(code))
            {
                int open = call.Index + call.Length - 1, close = RoomScopeShotScanTests.Matching(code, open);
                var site = new Site { File = file, Line = 1 + code.Take(call.Index).Count(c => c == '\n') };
                if (close > open)
                {
                    var arguments = RoomScopeShotScanTests.SplitTop(code.Substring(open + 1, close - open - 1));
                    var named = arguments.Count > 0 ? NamedArgument.Match(arguments[0]) : Match.Empty;
                    if (named.Success) site.Named = named.Groups["id"].Value;
                }
                // The statement just before it, in its own block: from the boundary before that statement's semicolon.
                int end = code.LastIndexOfAny(new[] { ';', '{', '}' }, Math.Max(0, call.Index - 1));
                if (end >= 0 && code[end] == ';')
                {
                    int start = end > 0 ? code.LastIndexOfAny(new[] { ';', '{', '}' }, end - 1) : -1;
                    var gate = Gate.Match(code.Substring(start + 1, end - start - 1));
                    if (gate.Success) site.Gated = gate.Groups["id"].Value;
                }
                sites.Add(site);
            }
            return sites;
        }

        /// <summary>
        /// What the room scope's script gets wrong about the bubble's speaker: a read of one path's own field, or the
        /// late pass or the prompt rule without the visit's speaker.
        /// </summary>
        public static List<string> ScopeProblems(string source)
        {
            string code = RoomScopeShotScanTests.Strip(source);
            var problems = new List<string>();
            foreach (Match read in OnePathsField.Matches(code))
                problems.Add(ScopeFile + ":" + (1 + code.Take(read.Index).Count(c => c == '\n')) + " reads " + read.Value
                    + ", one path's own field: read the visit's speaker as " + Speaker);
            foreach (var member in new[] { "NamedByPrompt", "GatePromptsOnDrawnBodies" })
            {
                var header = Regex.Match(code, @"(?<![\w\.])" + member + @"\s*\([^)]*\)\s*\{");
                if (!header.Success) { problems.Add(ScopeFile + ": " + member + " is gone, so the scan cannot read its speaker"); continue; }
                int open = header.Index + header.Length - 1, close = RoomScopeShotScanTests.Matching(code, open);
                string body = close > open ? code.Substring(open, close - open + 1) : "";
                if (!Regex.IsMatch(body, @"(?<![\w\.])" + Speaker + @"(?!\w)"))
                    problems.Add(ScopeFile + ": " + member + " does not read " + Speaker + ", the visit's speaker on both paths");
            }
            return problems;
        }
    }
}
