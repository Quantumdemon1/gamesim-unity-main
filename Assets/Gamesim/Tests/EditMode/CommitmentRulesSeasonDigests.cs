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
    /// readings, offers accepted and broken; C5: somebody asked into a pact, a member's no, a pact
    /// renamed; C7: counters taken, the veto bought, prices voided, promises called in, fences mended -
    /// their busy moves only ever made under the rules, so the first half never sees them; C8: deals the
    /// player broke in front of the house heard of, every hearing named to them in a line; C9: final
    /// three deals struck and kept, and a houseguest's final choice). C9's final three deal is a kind
    /// the recorded build never had, so its odds stay out of the digest.</para>
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
            reached.TryGetValue("partnership-broken", out int partnershipsBroken);
            reached.TryGetValue("partnership-kept", out int partnershipsKept);
            Assert.That(partnershipsBroken + partnershipsKept, Is.GreaterThan(0), "Partnerships were judged at the vote.");
            Assert.That(reached.TryGetValue("safety-kept", out int spared) && spared > 0, Is.True, "Safety pacts were kept.");
            Assert.That(reached.TryGetValue("information-reading", out int readings) && readings > 0, Is.True, "Information deals passed their readings.");
            Assert.That(reached.TryGetValue("accepted-offer-broken", out int accepted) && accepted > 0, Is.True, "Offers the player accepted broke.");
            // C6 (allies share intel): allies were asked and answered straight, pacts met, and meetings in a
            // vote week told a vote the reveal judged.
            Assert.That(reached.TryGetValue("ally-asked", out int allyAsked) && allyAsked > 0, Is.True, "The player asked somebody who answers as an ally.");
            reached.TryGetValue("ally-asked-straight", out int straight);
            Assert.That(straight, Is.EqualTo(allyAsked), "Every ally asked answered with their ballot as it stood.");
            Assert.That(reached.TryGetValue("AllianceMeet", out int meetings) && meetings > 0, Is.True, "Pacts met.");
            Assert.That(reached.TryGetValue("ally-claim", out int allyClaims) && allyClaims > 0, Is.True, "A meeting told the player a vote.");
            Assert.That(reached.TryGetValue("ally-claim-kept", out int allyKept) && allyKept > 0, Is.True, "The reveal judged it.");
            // C5 (grow and manage alliances): the same seasons ask somebody into a pact, bring somebody in -
            // the members asked only on the one asked's yes, by the web's bar for a pact of more than two -
            // and rename one. A member's no after that yes is rare under that bar, where a member must be
            // hostile to the newcomer (the count says how rare); GrowAndManageAlliancesTests holds it.
            Assert.That(reached.TryGetValue("BringIntoAlliance", out int askings) && askings > 0, Is.True, "The player asked somebody into a pact.");
            Assert.That(reached.TryGetValue("pact-joined", out int joins) && joins > 0, Is.True, "Somebody was brought into a pact.");
            Assert.That(reached.TryGetValue("RenameAlliance", out int renames) && renames > 0, Is.True, "A pact was renamed.");
            // C7 (negotiation): the same seasons reach each of its rules.
            Assert.That(reached.TryGetValue("counter-taken", out int countered) && countered > 0, Is.True, "Counters were taken, and paid for.");
            Assert.That(reached.TryGetValue("veto-price", out int vetoPrices) && vetoPrices > 0, Is.True, "The veto was bought with a price.");
            Assert.That(reached.TryGetValue("price-void", out int voided) && voided > 0, Is.True, "Prices were voided.");
            Assert.That(vetoPrices + countered, Is.GreaterThan(voided), "and some stood.");
            Assert.That(reached.TryGetValue("called-in", out int calledIn) && calledIn > 0, Is.True, "Promises were called in.");
            Assert.That(reached.TryGetValue("amends", out int amends) && amends > 0, Is.True, "Fences were mended.");
            // C8 (your word in the house): deals the player broke in front of the house became facts the two
            // of them knew, the house's gossip carried them, and every houseguest it reached was named to the
            // player in a line as it did - never a hearing without one.
            Assert.That(reached.TryGetValue("broken-word", out int brokenWords) && brokenWords > 0, Is.True, "The player broke deals in front of the house.");
            Assert.That(reached.TryGetValue("word-heard", out int hearings) && hearings > 0, Is.True, "The gossip carried them.");
            reached.TryGetValue("word-heard-line", out int hearingLines);
            Assert.That(hearingLines, Is.EqualTo(hearings), "Every houseguest the gossip reached was named to the player.");
            // C9 (the endgame): final three deals were struck and kept, and a houseguest made the final choice with
            // the player among the two they chose between. FinalChoiceSeasonHarness measures that choice over 1,500 seasons.
            Assert.That(reached.TryGetValue("final-three", out int finalThrees) && finalThrees > 0, Is.True, "Final three deals were struck.");
            Assert.That(reached.TryGetValue("final-three-kept", out int finalThreesKept) && finalThreesKept > 0, Is.True, "A final three deal was kept.");
            Assert.That(reached.TryGetValue("final-choice", out int finalChoices) && finalChoices > 0, Is.True, "A houseguest's final choice weighed the player.");
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
            int i = 0, heard = 0, voided = 0, wordSeen = 0, wordLines = 0;
            for (; i < 3000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                var s = engine.Snapshot;
                voided += VoidLines(s, ref heard);
                wordLines += WordLines(s, ref wordSeen);
                if (s.phase != lastPhase) { Checkpoint(s, trace); lastPhase = s.phase; }
                bool done = false;
                for (int attempt = 0; attempt < 6 && !done; attempt++)
                {
                    var own = Busy(s, seed, attempt, rulesOn);
                    if (own == null) break;
                    // C6: whether the one asked answers as an ally, read before the question is put.
                    bool ally = own.kind == EpisodeCommandKind.AskVote && AnswersAsAnAlly(s, own.targetId);
                    var result = engine.Apply(own);
                    if (result.accepted)
                    {
                        done = true;
                        Count(counts, own.kind.ToString());
                        if (ally)
                        {
                            // An ally's answer is their ballot as it stood when asked, and never a deflection.
                            Count(counts, "ally-asked");
                            var told = result.state.ledger.claims.LastOrDefault(k => k.week == s.week && k.voterId == own.targetId);
                            if (told != null && told.source == ClaimSource.Told && told.targetId == EpisodeEngine.ProjectBallot(s, own.targetId).selectedNomineeId)
                                Count(counts, "ally-asked-straight");
                        }
                        CountGrowth(counts, s, result.state);
                    }
                }
                if (done) continue;
                var next = NextCommand(s);
                var applied = engine.Apply(next);
                if (!applied.accepted) { error = "week " + s.week + " " + s.phase + "/" + s.evictionStage + " " + next.kind + ": " + applied.reason; break; }
                // C9: a houseguest's final choice with the player among the two they chose between, and whom it took.
                if (s.phase == EpisodePhase.FinalEviction && s.hohId != s.playerId && s.Find(s.playerId)?.status == ContestantStatus.Active)
                {
                    Count(counts, "final-choice");
                    if (applied.state.Find(applied.state.playerId)?.status == ContestantStatus.Active) Count(counts, "final-choice-took-player");
                }
            }
            var final = engine.Snapshot;
            voided += VoidLines(final, ref heard);
            wordLines += WordLines(final, ref wordSeen);
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
            // C1's rules: a partnership judged at the vote - broken, or kept and standing, which leaves a
            // record both ways - a safety pact kept, an information deal's reading (the last 256 lines
            // only) and its breach by a lie, and an offer the player accepted that then broke.
            Count(counts, "partnership-broken", final.deals.Count(d => d.type == DealKind.Partnership && d.status == DealStatus.Broken));
            Count(counts, "partnership-kept", final.relationships.Sum(r => r.events.Count(e => e.type == "deal_fulfilled"
                && e.description != null && e.description.Contains(" honoured a partnership with "))) / 2);
            Count(counts, "safety-kept", final.deals.Count(d => d.type == DealKind.SafetyAgreement && d.status == DealStatus.Fulfilled));
            Count(counts, "information-reading", final.events.Count(e => e.kind == "vote-read" && e.text.Contains(" kept you in the loop")));
            Count(counts, "information-lie", final.deals.Count(d => d.type == DealKind.InformationSharing && d.status == DealStatus.Broken));
            Count(counts, "accepted-offer-broken", final.deals.Count(d => d.status == DealStatus.Broken && d.recipientId == final.playerId
                && (d.id.StartsWith("deal-ask-", StringComparison.Ordinal) || d.id.StartsWith("deal-veto-", StringComparison.Ordinal))));
            // C2 and C3, by the words they write, so the file still compiles against the build before them.
            Count(counts, "betrayal", final.relationships.Where(r => r.fromId == final.playerId).SelectMany(r => r.events).Count(e => e.type == "alliance-betrayed"));
            Count(counts, "pact-ended-betrayed", final.ledger.alliances.Count(r => r.why != null && r.why.EndsWith("/betrayed", StringComparison.Ordinal)));
            Count(counts, "pact-ended-turned", final.ledger.alliances.Count(r => r.why != null && r.why.StartsWith("player", StringComparison.Ordinal)
                && r.why.EndsWith("/turned", StringComparison.Ordinal)));
            // C6, by the record's own words: an ally's account of their vote at a meeting, and how the reveal judged it.
            Count(counts, "ally-claim", final.ledger.claims.Count(k => k.source == ClaimSource.Ally));
            Count(counts, "ally-claim-kept", final.ledger.claims.Count(k => k.source == ClaimSource.Ally && k.status == ClaimStatus.Kept));
            // C5's groups ruling: a member who turned on a pact of the player's of three or more is taken out
            // of it, in a story defector's words (which a story's own defector writes too); the last 256 lines only.
            Count(counts, "pact-member-out", final.events.Count(e => e.kind == "alliance" && e.text != null && e.text.Contains(" is out of ")));
            // C7, by the words and ids it writes: prices struck - the player's, a counter's, and a
            // houseguest's, a veto's - prices voided (by the line that says so, not every price that
            // lapsed), promises called in and fences mended.
            var prices = final.deals.Where(d => d.id.StartsWith("deal-price-", StringComparison.Ordinal)).ToList();
            Count(counts, "counter-taken", prices.Count(d => d.proposerId == final.playerId));
            Count(counts, "veto-price", prices.Count(d => d.recipientId == final.playerId));
            Count(counts, "price-void", voided);
            var record = final.relationships.Where(r => r.toId == final.playerId).SelectMany(r => r.events).ToList();
            Count(counts, "called-in", record.Count(e => e.type != null && (e.type.StartsWith("promise-held:", StringComparison.Ordinal)
                || e.type.StartsWith("promise-pressed:", StringComparison.Ordinal))));
            Count(counts, "amends", record.Count(e => e.type == "amends-made" || e.type == "amends-refused"));
            // C8, by the fact kind and the line's words, so the file still compiles against the build before
            // it: the player's broken word as house knowledge, each houseguest past the two it was struck
            // between who heard of it, and the lines that named them to the player, counted as they were said.
            var brokenWord = final.story?.facts?.Where(f => f.kind == "broken-word" && f.actorId == final.playerId).ToList() ?? new List<HouseFactState>();
            Count(counts, "broken-word", brokenWord.Count);
            Count(counts, "word-heard", brokenWord.Sum(f => Math.Max(0, f.knowers.Count - 2)));
            Count(counts, "word-heard-line", wordLines);
            // C9, by the kind's spelling, so the file still compiles against the build before it: final three
            // deals struck - the player's, an offer the player took, and between houseguests - and those kept.
            Count(counts, "final-three", final.deals.Count(d => d.type == "final_three"));
            Count(counts, "final-three-player", final.deals.Count(d => d.type == "final_three" && (d.proposerId == final.playerId || d.recipientId == final.playerId)));
            Count(counts, "final-three-kept", final.deals.Count(d => d.type == "final_three" && d.status == DealStatus.Fulfilled));
            Count(counts, "final-three-broken", final.deals.Count(d => d.type == "final_three" && d.status == DealStatus.Broken));
            stats = "cmds=" + i + " week=" + final.week + " deals=" + final.deals.Count + " broken=" + broken + " promisesBroken=" + brokenPromises
                + " winner=" + final.winnerId;
            digest = Hash(trace.ToString());
        }

        /// <summary>C6's reader of who answers the player as an ally (EpisodeEngine.SharesIntel), by reflection so the file still compiles against the build before it; nobody there.</summary>
        private static readonly System.Reflection.MethodInfo SharesIntel =
            typeof(EpisodeEngine).GetMethod("SharesIntel", new[] { typeof(EpisodeState), typeof(string) });

        private static bool AnswersAsAnAlly(EpisodeState s, string npcId) =>
            SharesIntel != null && !string.IsNullOrEmpty(npcId) && (bool)SharesIntel.Invoke(null, new object[] { s, npcId });

        private static void Count(Dictionary<string, int> counts, string key, int by = 1)
        {
            counts.TryGetValue(key, out int n); counts[key] = n + by;
        }

        /// <summary>
        /// C5's outcomes, by the words a command wrote, so the file still compiles against the build
        /// before them: somebody brought into a pact, a member's no after the one asked said yes (the
        /// only time the members are asked), the one asked's own no, a pact renamed, and a pact of three
        /// or more the player walked out of going on without them.
        /// </summary>
        private static void CountGrowth(Dictionary<string, int> counts, EpisodeState before, EpisodeState after)
        {
            foreach (var e in after.events.Where(e => e.sequence >= before.nextSequence && e.text != null))
            {
                if (e.kind == "alliance" && e.text.Contains(" joined ")) Count(counts, "pact-joined");
                else if (e.kind == "alliance-refused" && e.text.Contains(" won't have "))
                    Count(counts, "pact-join-refused-by-a-member");
                else if (e.kind == "alliance-refused" && (e.text.Contains(" turned down The ")
                    || PactNamesOnOffer.Any(name => e.text.Contains(" turned down " + name + "."))))
                    Count(counts, "pact-join-refused-by-the-one-asked");
                else if (e.kind == "alliance-renamed") Count(counts, "pact-renamed");
                else if (e.kind == "alliance" && e.text.EndsWith(" goes on without you.", StringComparison.Ordinal)) Count(counts, "pact-left-goes-on");
            }
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
            // Preserve compatibility with recorded older assemblies: only exact27 requires
            // the new test observer. It validates a separate field view; returned trace26
            // retains the original computed getters and feeds the unchanged legacy suffix.
            bool chronology = o["unifiedCommitments"] is JArray canonical && canonical.OfType<JObject>()
                .Any(row => row.Property("voteBindingWeek") != null || row.Property("voteFirstRevealWeek") != null);
            Assert.That(o["schemaVersion"]?.Type, Is.EqualTo(JTokenType.Integer));
            if ((long)o["schemaVersion"] >= 27 || chronology)
            {
                Assert.That((long)o["schemaVersion"], Is.EqualTo(27), "Do not project future or wrongly grouped chronology.");
                var observer = typeof(CommitmentRulesSeasonDigests).Assembly
                    .GetType("Gamesim.Tests.EditMode.LegacyDigestSchema27Observer", true);
                var method = observer.GetMethod("ProjectTrace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(EpisodeState), typeof(JObject) }, null);
                Assert.That(method, Is.Not.Null, "Exact27 requires its reviewed observer; it never skips an absent dependency.");
                Assert.That(method.ReturnType, Is.EqualTo(typeof(JObject)));
                o = (JObject)method.Invoke(null, new object[] { s, o });
            }
            // Only the literal inert26 archive is removable. This file must also compile
            // against old recorded assemblies, so inspect JSON rather than growing DTO fields.
            bool hasVoteArchive = o.TryGetValue("unifiedVoteReveals", out var voteArchive);
            if (o["schemaVersion"].Value<int>() >= 26 || hasVoteArchive)
            {
                Assert.That(o["schemaVersion"].Value<int>(), Is.EqualTo(26), "Do not project an unknown future schema.");
                Assert.That(hasVoteArchive, Is.True);
                Assert.That(voteArchive, Is.InstanceOf<JArray>());
                Assert.That(((JArray)voteArchive).Count, Is.Zero, "Never erase actual ballots in legacy parity.");
                o.Remove("unifiedVoteReveals");
            }
            // Keep this harness compilable against the recorded pre-schema-23 assembly too.
            foreach (var field in new[] { "economyRulesVersion", "moveInExtrasSpent" })
            {
                if (o.TryGetValue(field, out var value))
                    Assert.That(value.Value<int>(), Is.Zero, "Legacy goldens must not run the new economy: " + field);
                o.Remove(field);
            }
            // Schema 25's hearing fields are removable only as a complete disabled/empty group.
            // This guards the source projection rather than concealing newly active consequences.
            bool hasHearingRules = o.TryGetValue("unifiedHearingRulesVersion", out var hearingRules);
            bool hasHearingEvidence = o.TryGetValue("unifiedHearingEvidence", out var hearingEvidence);
            bool hasHearingReceipts = o.TryGetValue("unifiedHearingReceipts", out var hearingReceipts);
            Assert.That(hasHearingEvidence, Is.EqualTo(hasHearingRules));
            Assert.That(hasHearingReceipts, Is.EqualTo(hasHearingRules));
            if (o["schemaVersion"].Value<int>() >= 25)
                Assert.That(hasHearingRules, Is.True, "A current season must contain all hearing fields.");
            if (hasHearingRules)
            {
                Assert.That(hearingRules.Type, Is.EqualTo(JTokenType.Integer));
                Assert.That(hearingRules.Value<long>(), Is.Zero, "Legacy goldens must not activate hearing coordination.");
                Assert.That(hearingEvidence, Is.InstanceOf<JArray>());
                Assert.That(hearingReceipts, Is.InstanceOf<JArray>());
                Assert.That(((JArray)hearingEvidence).Count, Is.Zero, "Never hide audible evidence in legacy parity.");
                Assert.That(((JArray)hearingReceipts).Count, Is.Zero, "Never hide a hearing effect in legacy parity.");
                o.Remove("unifiedHearingRulesVersion"); o.Remove("unifiedHearingEvidence"); o.Remove("unifiedHearingReceipts");
            }
            // Schema 24 reserves two INACTIVE fields only. Keep compiling against old recorded
            // assemblies, but never erase an active/nonempty authority to manufacture old parity.
            bool hasUnifiedRules = o.TryGetValue("unifiedCommitmentRulesVersion", out var unifiedRules);
            bool hasUnifiedRows = o.TryGetValue("unifiedCommitments", out var unifiedRows);
            Assert.That(hasUnifiedRows, Is.EqualTo(hasUnifiedRules), "The schema-24 fields occur together.");
            if (o["schemaVersion"].Value<int>() >= 24)
                Assert.That(hasUnifiedRules, Is.True, "A current season must contain both reserved fields.");
            if (hasUnifiedRules)
            {
                Assert.That(unifiedRules.Type, Is.EqualTo(JTokenType.Integer));
                Assert.That(unifiedRules.Value<long>(), Is.Zero, "Legacy goldens must not activate unified commitments.");
                Assert.That(unifiedRows, Is.InstanceOf<JArray>());
                Assert.That(((JArray)unifiedRows).Count, Is.Zero, "Never hide a canonical commitment in legacy parity.");
                o.Remove("unifiedCommitmentRulesVersion"); o.Remove("unifiedCommitments");
            }
            o.Remove("schemaVersion"); o.Remove("commitmentRulesStartWeek");
            // Schema 22's deal fields - C7's link with C0's record - which a season without the rules leaves null and 0.
            foreach (var row in ((JArray)o["deals"]).OfType<JObject>()) { row.Remove("brokenById"); row.Remove("settledWeek"); row.Remove("linkedDealId"); }
            foreach (var row in ((JArray)o["promises"]).OfType<JObject>()) { row.Remove("brokenById"); row.Remove("settledWeek"); }
            // And its alliance mark - C5's invitation into somebody else's pact - which a season without the rules leaves false.
            foreach (var row in ((JArray)o["alliances"]).OfType<JObject>()) row.Remove("playerJoined");
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
                // The ten kinds the recorded build knew, in its order: C9's final three deal, the commitment
                // rules' own kind (by its spelling, so the file still compiles against that build), is no
                // reader a season without the rules has, and its odds are not digested.
                foreach (var kind in DealKind.All.Where(k => k != "final_three"))
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

        /// <summary>
        /// C7's moves, under the rules only - by their words, and the command kind by name, so the file
        /// still compiles against the build before the rules and a season without them is given exactly
        /// what it always was. A counter that stands is answered, yes or no; holding the veto before the
        /// meeting, a price is named to a nominee; and now and then a promise owed is called in, or fences
        /// are mended with somebody (which the engine refuses where the player broke nothing).
        /// </summary>
        private static EpisodeCommand Negotiating(EpisodeState s, uint seed, int attempt)
        {
            if (attempt != 0 || !RulesOn(s)) return null;
            string countering = StandingCounter(s);
            if (countering != null)
                return Cmd(s, EpisodeCommandKind.RespondToDeal, countering, null, Pick(s, seed, 31, 2) == 0 ? "decline" : EpisodeEngine.AcceptDeal);
            var negotiate = (EpisodeCommandKind)Enum.Parse(typeof(EpisodeCommandKind), "Negotiate");
            if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.vetoHolderId == s.playerId && s.nominees.Count == 2 && Pick(s, seed, 32, 2) == 0)
                return Cmd(s, negotiate, s.nominees[Pick(s, seed, 33, 2)], null,
                    "veto-price:" + (s.Active.Count() <= NpcDeals.EndgameSize ? DealKind.FinalTwo : DealKind.VoteSave));
            if ((s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign) && Pick(s, seed, 34, 4) == 0)
            {
                var owed = s.promises.FirstOrDefault(p => p.toId == s.playerId && p.status == PromiseStatus.Active
                    && (p.kind == PromiseKind.Safety || p.kind == PromiseKind.FinalTwo));
                if (owed != null) return Cmd(s, negotiate, owed.fromId, owed.id, "call-in:" + new[] { "remind", "demand", "threaten" }[Pick(s, seed, 35, 3)]);
                var npcs = s.Active.Where(c => !c.isPlayer).ToList();
                if (npcs.Count > 0) return Cmd(s, negotiate, npcs[Pick(s, seed, 36, npcs.Count)].id, null, "mend-fences");
            }
            return null;
        }

        /// <summary>
        /// Who has a counter standing, by the engine's rule (Negotiation.OpenCounter) read from the words:
        /// the latest line the player heard is a counter, said this week and in this phase. Lines between
        /// houseguests only are passed over, as the player heard none of them.
        /// </summary>
        private static string StandingCounter(EpisodeState s)
        {
            for (int i = s.events.Count - 1; i >= 0; i--)
            {
                var said = s.events[i];
                if (said == null || (said.audienceIds != null && said.audienceIds.Count > 0 && !said.audienceIds.Contains(s.playerId))) continue;
                if (said.kind != "deal-counter" || said.week != s.week || said.phase != s.phase || said.audienceIds == null) return null;
                return said.audienceIds.FirstOrDefault(id => id != s.playerId);
            }
            return null;
        }

        /// <summary>
        /// The prices voided since the last look (C7), by their line - "... no longer owe(s) ...", only ever
        /// said of a price void - counted as they are said, since the season keeps only its last 256 lines.
        /// </summary>
        private static int VoidLines(EpisodeState s, ref int seen)
        {
            int found = 0, newest = seen;
            for (int i = s.events.Count - 1; i >= 0 && s.events[i].sequence > seen; i--)
            {
                var said = s.events[i];
                newest = Math.Max(newest, said.sequence);
                if (said.kind == "deal-outcome" && said.text != null && said.text.Contains(" no longer owe")) found++;
            }
            seen = newest;
            return found;
        }

        /// <summary>
        /// The lines naming somebody the gossip told of the player's broken word since the last look (C8),
        /// by their words - "Word in the house: ... heard you went back on your ..." - counted as they are
        /// said, since the season keeps only its last 256 lines.
        /// </summary>
        private static int WordLines(EpisodeState s, ref int seen)
        {
            int found = 0, newest = seen;
            for (int i = s.events.Count - 1; i >= 0 && s.events[i].sequence > seen; i--)
            {
                var said = s.events[i];
                newest = Math.Max(newest, said.sequence);
                if (said.text != null && said.text.StartsWith("Word in the house: ", StringComparison.Ordinal)
                    && said.text.Contains(" heard you went back on your ")) found++;
            }
            seen = newest;
            return found;
        }

        /// <summary>
        /// A busy player: every action the commitment rules touch, by <see cref="Pick"/>. Under the rules
        /// only (<paramref name="rulesOn"/>), so the half without them plays the recorded seasons' own
        /// commands, a pact of the player's meets now and then (C6).
        /// </summary>
        private static EpisodeCommand Busy(EpisodeState s, uint seed, int attempt, bool rulesOn)
        {
            if (s.pendingDiary != null) return null;
            var me = s.Find(s.playerId);
            if (me == null || me.status != ContestantStatus.Active) return null;
            var negotiating = Negotiating(s, seed, attempt);
            if (negotiating != null) return negotiating;
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
            // C6, the rules' half only: a pact of the player's meets through one of its members, on a salt of
            // its own (clear of C5's 21-24 and C7's 31-36). A pact that has met this week refuses, and the
            // attempt goes on to something else.
            if (rulesOn && Pick(s, seed, 41 + attempt, 3) == 0)
            {
                var pact = s.alliances.FirstOrDefault(p => p.active && p.members.Contains(s.playerId)
                    && p.members.Any(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active));
                var mate = pact?.members.Where(id => id != s.playerId).Select(s.Find).FirstOrDefault(m => m != null && m.status == ContestantStatus.Active);
                if (mate != null) return Cmd(s, EpisodeCommandKind.AllianceMeet, mate.id, null, pact.id);
            }
            var grow = attempt == 0 && RulesOn(s) ? GrowOrManage(s, seed, npcs) : null;
            if (grow != null) return grow;
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

        /// <summary>
        /// Whether the season plays the commitment rules this week, read by reflection so the file
        /// compiles against the build before them, where it never does.
        /// </summary>
        private static bool RulesOn(EpisodeState s)
        {
            var field = typeof(EpisodeState).GetField("commitmentRulesStartWeek");
            if (field == null) return false;
            int start = (int)field.GetValue(s);
            return start >= 1 && s.week >= start;
        }

        /// <summary>The web's names for a pact, which C5's rename offers (PactNames.Web), as words.</summary>
        private static readonly string[] PactNamesOnOffer = { "The Outsiders", "Dream Team", "Power Players", "The Silent Circle", "The Hidden Council", "The Golden Crew" };

        /// <summary>
        /// C5 (grow and manage alliances), under the rules only, now and then: bring somebody into one of
        /// the player's pacts, rename one the player founded, or leave one of three or more. The two new
        /// kinds by number (BringIntoAlliance 59, RenameAlliance 60), and the members' say by reflection
        /// (NpcAlliances.WouldWelcome), so the file still compiles against the build before them; a season
        /// without the rules never comes here. The busy player asks only somebody it can see may be asked -
        /// in no pact with the player, and not somebody it has soured on - and somebody every member would
        /// welcome where there is anybody (a harness may peek, to reach the join), anybody otherwise.
        /// </summary>
        private static EpisodeCommand GrowOrManage(EpisodeState s, uint seed, List<ContestantState> npcs)
        {
            var mine = s.alliances.Where(p => p.active && p.members.Contains(s.playerId)).ToList();
            if (mine.Count == 0) return null;
            var pact = mine[Pick(s, seed, 21, mine.Count)];
            var here = pact.members.Where(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active).ToList();
            switch (Pick(s, seed, 22, 8))
            {
                case 0:
                case 1:
                    var outside = npcs.Where(n => !pact.members.Contains(n.id) && !s.Allied(s.playerId, n.id)
                        && s.Score(s.playerId, n.id) >= NpcAlliances.SourLine).ToList();
                    var welcome = typeof(NpcAlliances).GetMethod("WouldWelcome");
                    var welcomed = welcome == null ? new List<ContestantState>()
                        : outside.Where(n => here.All(m => (bool)welcome.Invoke(null, new object[] { s, m, n.id }))).ToList();
                    var asked = welcomed.Count > 0 ? welcomed : outside;
                    return asked.Count > 0 ? Cmd(s, (EpisodeCommandKind)59, asked[Pick(s, seed, 23, asked.Count)].id, pact.id) : null;
                case 2:
                    return here.Count > 0 && pact.members[0] == s.playerId
                        ? Cmd(s, (EpisodeCommandKind)60, here[0], pact.id, PactNamesOnOffer[Pick(s, seed, 24, PactNamesOnOffer.Length)]) : null;
                case 3:
                    return here.Count >= 2 ? Cmd(s, EpisodeCommandKind.LeaveAlliance, here[0], pact.id) : null;
                default:
                    return null;
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
