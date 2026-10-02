#if !UNITY_5_3_OR_NEWER
// The R0/C0 recorded-season evidence, reproducible on demand: Tools/SimulationTests only. Unity's
// batch runner executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The commitment rules' recorded-season evidence (ACTIONS-DEALS-ALLIANCES-PLAN R0, C0): 54
    /// seeded seasons - three rule sets a recorded season can have (the director's, the legacy one and
    /// the strategy harness's), houses of 6, 8 and 12, six seeds each - played to their ends by a busy
    /// scripted player who studies, whispers, calls people out, promises, proposes and answers deals,
    /// asks and reads. Each season is digested at every phase change: the state without schema 22's
    /// additions, and every reader the commitment rules touch - warmth, threat, trust, the jury's
    /// obligations and score, the deal odds and their words, the story's odds, the houseguest notes,
    /// Game Sense's notes and the jury read.
    ///
    /// <para><see cref="Recorded"/> holds the digests the build before the rules played (7991687), made
    /// by this file compiled against that build's simulation; nothing here names an API it lacked, and
    /// the rules are switched on by reflection for the same reason. Game Sense's notes are digested
    /// without their weekly-recap flag (<see cref="GameSense.Note.known"/>): a note's visibility in the
    /// recap is not an outcome, and the review of R0/C0 changed it for a vote deal's ending everywhere.</para>
    ///
    /// <para>Explicit: run with <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~CommitmentRulesSeasonDigests"</c>.
    /// A copy change in one of the digested readers moves every digest without moving a season; the
    /// evidence is for a change to the rules, and a reader's words are re-recorded with it. Each
    /// later slice behind the same boundary keeps the first half green and shows, in the second, that
    /// the same seasons reach what it changes (C1: partnerships judged, safety pacts kept, information
    /// readings, offers accepted and broken).</para>
    /// </summary>
    public sealed class CommitmentRulesSeasonDigests
    {
        /// <summary>Each season as the build before the commitment rules played it: rule set, house size, seed, digest.</summary>
        private static readonly string[] Recorded =
        {
            "director n6 s1 2950b099ef761fe5",
            "director n6 s2 781bd23f491d8010",
            "director n6 s3 2cf36e5440d0e6fb",
            "director n6 s4 b7c6c731d6c1ab3e",
            "director n6 s5 bf8bb1d539c58d55",
            "director n6 s6 d0d783631e93063e",
            "director n8 s1 2191ab4748c8f05e",
            "director n8 s2 b270059c5dc184a4",
            "director n8 s3 c46a6c5e0033c0cf",
            "director n8 s4 12976af8aff175ee",
            "director n8 s5 b9382cb0f81c6c17",
            "director n8 s6 d018369410bc7a6f",
            "director n12 s1 e2672e61e5cf7857",
            "director n12 s2 a80b9b83f025db66",
            "director n12 s3 a447fea88e6ade7b",
            "director n12 s4 c29b0937c9953793",
            "director n12 s5 a94df0639460d63d",
            "director n12 s6 a92547e10e4b2e42",
            "legacy n6 s1 683f96e1eeb30a03",
            "legacy n6 s2 4abb22de7ab5178a",
            "legacy n6 s3 008cfc27d07de527",
            "legacy n6 s4 c5d34025eea1b129",
            "legacy n6 s5 39a578fdb1cf1843",
            "legacy n6 s6 4d1e9c1be173db90",
            "legacy n8 s1 2572edb5e3c9be4d",
            "legacy n8 s2 6ea4ecfa7ced2f2c",
            "legacy n8 s3 07f022081b142207",
            "legacy n8 s4 0b03d812284d895b",
            "legacy n8 s5 1a55664a7e5fee2f",
            "legacy n8 s6 04da9768b03d1110",
            "legacy n12 s1 0447e3c4092eab16",
            "legacy n12 s2 c416515ebca49d94",
            "legacy n12 s3 30b8c0a8e9632620",
            "legacy n12 s4 4ddf922569bd8e92",
            "legacy n12 s5 f4c650d48ee87f55",
            "legacy n12 s6 4cddd88ec863db7b",
            "harness n6 s1 2332c05bde1a95c1",
            "harness n6 s2 1dbf29ea7e422d71",
            "harness n6 s3 2f0f144c0dc6c7de",
            "harness n6 s4 5d44d1c9706d564e",
            "harness n6 s5 16ca0086b29a256e",
            "harness n6 s6 88221bb528cf3c13",
            "harness n8 s1 01b090cd5e7dce06",
            "harness n8 s2 f4650a9a2792e3c8",
            "harness n8 s3 7ffd4f5a2d9edd16",
            "harness n8 s4 6b20e69aead7f50e",
            "harness n8 s5 d7413d8e33ff9191",
            "harness n8 s6 0a5e11e0c21342f0",
            "harness n12 s1 a1f738f12f382504",
            "harness n12 s2 9f09f3519e41801f",
            "harness n12 s3 3a8662eac519cd17",
            "harness n12 s4 80aec8f21bdfa8c1",
            "harness n12 s5 89320b91ed25e73d",
            "harness n12 s6 3e8e6fc0b71a84bb",
        };

        [Test, Explicit("Plays 54 seasons to their ends: the commitment rules' recorded-season evidence, run on demand.")]
        public void WithoutTheRulesEverySeasonPlaysAsItDidBeforeThem()
        {
            var played = Run(false, out int errors, out _);
            Assert.That(errors, Is.Zero, string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            Assert.That(Recorded, Has.Length.EqualTo(played.Count), "Every season has its recorded digest.");
            var moved = played.Select(Key).Where(key => !Recorded.Contains(key)).ToList();
            Assert.That(moved, Is.Empty, "A season without the commitment rules moved:\n" + string.Join("\n", moved));
        }

        [Test, Explicit("Plays 54 seasons to their ends under the commitment rules: the evidence that the harness reaches what they change.")]
        public void UnderTheRulesTheSameSeasonsMoveAndStayLegal()
        {
            var played = Run(true, out int errors, out var reached);
            Assert.That(errors, Is.Zero, "Every command a season under the rules was given is legal under them:\n"
                + string.Join("\n", played.Where(line => line.Contains(" ERROR "))));
            int moved = played.Select(Key).Count(key => !Recorded.Contains(key));
            TestContext.Out.WriteLine(moved + " of " + played.Count + " seasons move under the rules; reached: "
                + string.Join(", ", reached.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value)));
            Assert.That(moved * 2, Is.GreaterThan(played.Count), "Most seasons reach something the rules change.");
            Assert.That(reached.TryGetValue("study", out int studies) && studies > 0, Is.True, "The player studied.");
            Assert.That(reached.TryGetValue("whisper", out int whispers) && whispers > 0, Is.True, "The player whispered.");
            Assert.That(reached.TryGetValue("deal-broken", out int broken) && broken > 0, Is.True, "Deals broke.");
            // C1 (every deal does something): the same seasons reach each of its rules.
            Assert.That(reached.TryGetValue("partnership-judged", out int partnerships) && partnerships > 0, Is.True, "Partnerships were judged at the vote.");
            Assert.That(reached.TryGetValue("safety-kept", out int spared) && spared > 0, Is.True, "Safety pacts were kept.");
            Assert.That(reached.TryGetValue("information-reading", out int readings) && readings > 0, Is.True, "Information deals passed their readings.");
            Assert.That(reached.TryGetValue("accepted-offer-broken", out int accepted) && accepted > 0, Is.True, "Offers the player accepted broke.");
        }

        /// <summary>A played line's key: its rule set, size, seed and digest, without the run's counts.</summary>
        private static string Key(string line) => string.Join(" ", line.Split(' ').Take(4));

        /// <summary>
        /// Plays the 54 seasons, without the commitment rules or with them from week one, and returns a
        /// line for each: rule set, size, seed, digest and the season's counts. Public, so a scratch
        /// program compiled against another build can print the same lines.
        /// </summary>
        public static List<string> Run(bool rulesOn, out int errors, out Dictionary<string, int> reached)
        {
            var lines = new List<string>();
            errors = 0;
            reached = new Dictionary<string, int>();
            foreach (var config in new[] { "director", "legacy", "harness" })
            foreach (int size in new[] { 6, 8, 12 })
            for (uint seed = 1; seed <= 6; seed++)
            {
                Play(config, size, seed, rulesOn, out string digest, out string stats, out string error, reached);
                if (error != null) errors++;
                lines.Add(config + " n" + size + " s" + seed + " " + digest + " " + stats + (error != null ? " ERROR " + error : ""));
            }
            return lines;
        }

        private static EpisodeState Season(string config, int size, uint seed, bool rulesOn)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, seed);
            if (config == "director")
            {
                s.competitionRulesVersion = CompetitionRules.Current;
                s.haveNotRulesStartWeek = 1; s.strategyRulesStartWeek = 1;
                EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s);
                EpisodeEngine.EnableWeek(s); EpisodeEngine.EnableAgency(s); EpisodeEngine.EnableFinale(s);
            }
            else if (config == "harness")
            {
                s.strategyRulesStartWeek = 1; s.blocRulesStartWeek = 1;
                EpisodeEngine.EnableStory(s); EpisodeEngine.EnableRead(s); EpisodeEngine.EnableLevers(s); EpisodeEngine.EnableAgency(s);
            }
            if (rulesOn)
            {
                // By reflection, so the file compiles against the build before the rules too.
                var field = typeof(EpisodeState).GetField("commitmentRulesStartWeek");
                if (field == null) throw new InvalidOperationException("The rules need the schema 22 field.");
                field.SetValue(s, 1);
            }
            return s;
        }

        private static void Play(string config, int size, uint seed, bool rulesOn, out string digest, out string stats, out string error, Dictionary<string, int> counts)
        {
            error = null;
            var engine = new EpisodeEngine(Season(config, size, seed, rulesOn));
            var trace = new StringBuilder();
            var lastPhase = (EpisodePhase)(-1);
            int i = 0;
            for (; i < 3000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                if (s.phase != lastPhase) { Checkpoint(s, trace); lastPhase = s.phase; }
                bool done = false;
                for (int attempt = 0; attempt < 6 && !done; attempt++)
                {
                    var own = Busy(s, seed, attempt);
                    if (own == null) break;
                    var result = engine.Apply(own);
                    if (result.accepted) { done = true; Count(counts, own.kind.ToString()); }
                }
                if (done) continue;
                var next = NextCommand(s);
                var applied = engine.Apply(next);
                if (!applied.accepted) { error = "week " + s.week + " " + s.phase + "/" + s.evictionStage + " " + next.kind + ": " + applied.reason; break; }
            }
            var final = engine.Snapshot;
            Checkpoint(final, trace);
            if (error == null && final.phase != EpisodePhase.Finished) error = "unfinished after " + i;
            int broken = final.deals.Count(d => d.status == DealStatus.Broken), kept = final.deals.Count(d => d.status == DealStatus.Fulfilled);
            int brokenPromises = final.promises.Count(p => p.status == PromiseStatus.Broken), keptPromises = final.promises.Count(p => p.status == PromiseStatus.Fulfilled);
            Count(counts, "deal-broken", broken); Count(counts, "deal-kept", kept);
            Count(counts, "promise-broken", brokenPromises); Count(counts, "promise-kept", keptPromises);
            Count(counts, "player-deal-broken", final.deals.Count(d => d.status == DealStatus.Broken && (d.proposerId == final.playerId || d.recipientId == final.playerId)));
            Count(counts, "rumour-to-player", final.events.Count(e => e.kind == "information" && e.text.Contains(" told you ") && !e.text.EndsWith(" has to go.", StringComparison.Ordinal)));
            Count(counts, "hunt-to-player", final.events.Count(e => e.kind == "information" && e.text.EndsWith(" has to go.", StringComparison.Ordinal)));
            Count(counts, "whisper", final.events.Count(e => e.kind == "rumour" && e.text.StartsWith("You whispered", StringComparison.Ordinal)));
            Count(counts, "study", final.events.Count(e => e.kind == "study-house"));
            // C1's rules: a partnership judged at the vote, a safety pact kept, an information deal's
            // reading (the last 256 lines only), and an offer the player accepted that then broke.
            Count(counts, "partnership-judged", final.deals.Count(d => d.type == DealKind.Partnership && (d.status == DealStatus.Fulfilled || d.status == DealStatus.Broken)));
            Count(counts, "safety-kept", final.deals.Count(d => d.type == DealKind.SafetyAgreement && d.status == DealStatus.Fulfilled));
            Count(counts, "information-reading", final.events.Count(e => e.kind == "vote-read" && e.text.Contains(" kept you in the loop")));
            Count(counts, "accepted-offer-broken", final.deals.Count(d => d.status == DealStatus.Broken && d.recipientId == final.playerId
                && (d.id.StartsWith("deal-ask-", StringComparison.Ordinal) || d.id.StartsWith("deal-veto-", StringComparison.Ordinal))));
            stats = "cmds=" + i + " week=" + final.week + " deals=" + final.deals.Count + " broken=" + broken + " promisesBroken=" + brokenPromises
                + " winner=" + final.winnerId;
            digest = Hash(trace.ToString());
        }

        private static void Count(Dictionary<string, int> counts, string key, int by = 1)
        {
            counts.TryGetValue(key, out int n); counts[key] = n + by;
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        /// <summary>The state without schema 22's additions, and every reader the commitment rules touch.</summary>
        private static void Checkpoint(EpisodeState s, StringBuilder trace)
        {
            var o = JObject.FromObject(s);
            o.Remove("schemaVersion"); o.Remove("commitmentRulesStartWeek");
            foreach (var row in ((JArray)o["deals"]).OfType<JObject>()) { row.Remove("brokenById"); row.Remove("settledWeek"); }
            foreach (var row in ((JArray)o["promises"]).OfType<JObject>()) { row.Remove("brokenById"); row.Remove("settledWeek"); }
            trace.Append(o.ToString(Formatting.None)).Append('\n');
            var people = s.contestants.ToList();
            foreach (var a in people)
                foreach (var b in people)
                {
                    if (a.id == b.id) continue;
                    trace.Append(F(NpcDeals.Adjusted(s, a.id, b.id))).Append(',')
                        .Append(F(ThreatAssessment.Total(s, a.id, b.id))).Append(',')
                        .Append(F(ThreatAssessment.TrustScore(s, a.id, b.id))).Append(',')
                        .Append(F(WebJuryVoting.Obligations(s, a.id, b.id))).Append(',')
                        .Append(F(WebJuryVoting.Score(s, a.id, b.id))).Append(';');
                }
            trace.Append('\n');
            foreach (var npc in s.Active.Where(c => !c.isPlayer))
            {
                string about = s.Active.Where(c => !c.isPlayer && c.id != npc.id).Select(c => c.id).FirstOrDefault();
                foreach (var kind in DealKind.All)
                {
                    string aboutId = kind == DealKind.VoteSave || kind == DealKind.VoteEvict ? s.nominees.FirstOrDefault(id => id != npc.id) ?? about : about;
                    var known = KnownOdds.Deal(s, npc.id, kind, aboutId);
                    trace.Append(F(PlayerDeals.AcceptanceChance(s, npc.id, kind, aboutId))).Append(',')
                        .Append(PlayerDeals.Reasoning(s, npc.id, kind, false)).Append(',')
                        .Append(F(known.chance)).Append(known.word).Append(';');
                }
                trace.Append(StoryOdds.PlayerBrokeTheirWord(s, npc.id)).Append('|');
                foreach (var note in HouseguestNotes.For(s, npc.id)) trace.Append(note.ToString()).Append('|');
            }
            trace.Append('\n');
            var sense = GameSense.Evaluate(s);
            trace.Append(sense.score).Append('/').Append(sense.competitions).Append('/').Append(sense.strategy).Append('/').Append(sense.social);
            foreach (var note in sense.notes) trace.Append('|').Append(note.text).Append(F(note.points));
            trace.Append('\n');
            var house = JuryHouseRead.Read(s);
            foreach (var juror in house.jurors) trace.Append(juror.id).Append(':').Append(juror.band).Append(':').Append(juror.reason).Append('|');
            trace.Append('\n');
        }

        private static string F(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static EpisodeCommand Cmd(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "digest-" + s.revision + "-" + kind + "-" + target + "-" + second, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        /// <summary>A coin that has nothing to do with the engine's stream.</summary>
        private static int Pick(EpisodeState s, uint seed, int salt, int n) =>
            n <= 0 ? 0 : (int)((((uint)s.revision * 2654435761u) ^ (seed * 40503u) ^ ((uint)salt * 2246822519u) ^ ((uint)s.week * 97u)) % 100003u) % n;

        private static EpisodeCommand AnswerBeat(EpisodeState s)
        {
            var item = HouseEvents.Pending(s);
            if (item == null) return null;
            bool actionsLeft = EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);
            if (!item.IsStory)
                return item.choices.Count == 0 ? null : Cmd(s, EpisodeCommandKind.ResolveHouseEvent, item.id, null, item.choices[0].label);
            var choice = item.choices.FirstOrDefault(c => !c.lapse && !c.locked && !c.pickPerson && !c.conduct && (!c.costsAction || actionsLeft))
                ?? item.choices.FirstOrDefault(c => !c.locked && !c.pickPerson && !c.conduct && (!c.costsAction || actionsLeft))
                ?? item.choices.FirstOrDefault(c => c.lapse);
            return choice == null ? null : Cmd(s, EpisodeCommandKind.ProgressStoryline, item.id, choice.optionId);
        }

        /// <summary>A busy player: every action the commitment rules touch, by <see cref="Pick"/>.</summary>
        private static EpisodeCommand Busy(EpisodeState s, uint seed, int attempt)
        {
            if (s.pendingDiary != null) return null;
            var me = s.Find(s.playerId);
            if (me == null || me.status != ContestantStatus.Active) return null;
            if (attempt == 0)
            {
                var beat = AnswerBeat(s);
                if (beat != null) return beat;
                var offer = NpcDeals.Pending(s).FirstOrDefault();
                if (offer != null) return Cmd(s, EpisodeCommandKind.RespondToDeal, offer.id, null, Pick(s, seed, 9, 3) == 0 ? "decline" : EpisodeEngine.AcceptDeal);
            }
            bool social = s.phase == EpisodePhase.Social, campaign = s.phase == EpisodePhase.Campaign;
            if (!social && !campaign) return null;
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            if (npcs.Count == 0) return null;
            bool left = EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s);
            if (campaign && attempt <= 1 && VoteRead.Available(s))
            {
                var voters = EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToList();
                var unasked = voters.FirstOrDefault(v => !EpisodeEngine.AskedThisWeek(s, v.id));
                if (unasked != null && attempt == 0) return Cmd(s, EpisodeCommandKind.AskVote, unasked.id);
                var unread = voters.FirstOrDefault(v => !EpisodeEngine.ReadThisWeek(s, v.id));
                if (unread != null) return Cmd(s, EpisodeCommandKind.ReadPerson, unread.id);
            }
            if (!left)
                return s.boughtActionPoints < 2 && Pick(s, seed, 11 + attempt, 3) == 0
                    ? Cmd(s, EpisodeCommandKind.BuyActionPoint, null, null, WebSocialVocabulary.SpreadAll) : null;
            var a = npcs[Pick(s, seed, 1 + attempt * 7, npcs.Count)];
            var others = npcs.Where(x => x.id != a.id).ToList();
            var b = others.Count > 0 ? others[Pick(s, seed, 2 + attempt * 7, others.Count)] : null;
            switch (Pick(s, seed, 3 + attempt * 7, 17))
            {
                case 0: return social ? Cmd(s, EpisodeCommandKind.StudyHouse, "memorize-layout") : null;
                case 1: return social && b != null ? Cmd(s, EpisodeCommandKind.SpreadRumor, b.id, a.id, EpisodeEngine.WhisperCampaign) : null;
                case 2: return social && b != null ? Cmd(s, EpisodeCommandKind.SpreadRumor, b.id, null, EpisodeEngine.PublicCallout) : null;
                case 3: return Cmd(s, EpisodeCommandKind.PromiseSafety, a.id);
                case 4: return Cmd(s, EpisodeCommandKind.PromiseFinalTwo, a.id);
                case 5:
                    if (!campaign || s.nominees.Count == 0) return null;
                    // Including a promise to evict the one it is made to, which a season without the rules takes.
                    return Cmd(s, EpisodeCommandKind.PromiseVote, a.id, s.nominees[Pick(s, seed, 5, s.nominees.Count)]);
                case 6:
                case 7:
                {
                    var kinds = PlayerDeals.Available(s, a.id);
                    if (kinds.Count == 0) return null;
                    string kind = kinds[Pick(s, seed, 6 + attempt, kinds.Count)];
                    string about = kind == DealKind.TargetAgreement ? PlayerDeals.Subjects(s, a.id).FirstOrDefault()
                        : kind == DealKind.VoteSave || kind == DealKind.VoteEvict ? s.nominees.FirstOrDefault(id => id != a.id && id != s.playerId) : null;
                    return Cmd(s, EpisodeCommandKind.ProposeDeal, a.id, about, kind);
                }
                case 8: return Cmd(s, EpisodeCommandKind.FormAlliance, a.id);
                case 9: return Cmd(s, EpisodeCommandKind.SmallTalk, a.id);
                case 10: return Cmd(s, EpisodeCommandKind.AskForIntel, a.id);
                case 11: return social ? Cmd(s, EpisodeCommandKind.Eavesdrop) : null;
                case 12: return b != null ? Cmd(s, EpisodeCommandKind.SpreadLie, a.id, b.id) : null;
                case 13: return b != null ? Cmd(s, EpisodeCommandKind.VentAbout, a.id, b.id) : null;
                case 14: return Cmd(s, EpisodeCommandKind.DiscussGame, a.id);
                case 15: return Cmd(s, EpisodeCommandKind.HouseMeeting, null, null, EpisodeEngine.RallyTroops);
                default: return social ? Cmd(s, EpisodeCommandKind.StudyHouse, "sneak-peek") : null;
            }
        }

        /// <summary>Whatever the phase asks of the player, the plainest legal answer.</summary>
        private static EpisodeCommand NextCommand(EpisodeState s)
        {
            var c = Cmd(s, EpisodeCommandKind.Advance);
            c.id = "next-" + s.revision;
            if (s.pendingDiary != null) { c.kind = EpisodeCommandKind.SkipDiary; c.targetId = s.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer))
            { c.kind = EpisodeCommandKind.Compete; c.performance = 0.5; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                c.kind = EpisodeCommandKind.Nominate; var pool = EpisodeEngine.NominationCandidates(s).ToArray(); c.targetId = pool[0].id; c.secondTargetId = pool[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && (s.vetoHolderId == s.playerId || (s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null)))
            {
                c.kind = EpisodeCommandKind.ResolveVeto;
                c.useVeto = EpisodeEngine.ReplacementCandidates(s).Any() && !EpisodeEngine.VetoIsLockedAtFinalFour(s);
                c.targetId = s.vetoHolderId == s.playerId ? s.nominees[0] : EpisodeEngine.NpcVetoSave(s);
                c.secondTargetId = EpisodeEngine.ReplacementCandidates(s).FirstOrDefault()?.id;
            }
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
                && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(x => x.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitEvictionSpeech; c.text = "I'd like to stay."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId) &&
                (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.nominees[(s.revision + s.week) % 2]; }
            else if (s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId)
            { c.kind = EpisodeCommandKind.FinalEvict; c.targetId = s.Active.First(p => !p.isPlayer).id; }
            else if (s.phase == EpisodePhase.JuryQuestioning && !s.juryExchanges[s.juryQuestionIndex].completed)
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex]; c.kind = EpisodeCommandKind.AnswerJury;
                c.targetId = exchange.finalistId == s.playerId ? exchange.questionerId : exchange.finalistId;
                c.secondTargetId = exchange.finalistId != s.playerId ? "neutral"
                    : EpisodeEngine.FinaleOn(s) ? FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0] : "A";
            }
            else if (s.phase == EpisodePhase.FinalSpeeches && s.Active.Any(p => p.isPlayer) && !s.finalSpeeches.Any(p => p.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitSpeech; c.text = "Thank you."; }
            else if (s.phase == EpisodePhase.Jury && !s.Active.Any(p => p.isPlayer) && !s.votes.Any(v => v.voterId == s.playerId))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.Active.First().id; }
            return c;
        }
    }
}
#endif
