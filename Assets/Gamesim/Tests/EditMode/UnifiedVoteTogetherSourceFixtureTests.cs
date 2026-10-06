using System;
using System.Collections.Generic;
using System.IO;
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
    /// Portable SOURCE04 recorded original-web system boundary, not a Unity season or applied-effect oracle.
    /// Only the detached VoteTogether verdict is compared with native code. Recorded requests, nested bus
    /// delivery, mutable history snapshots and private ballots are source observations, not knowledge grants.
    /// External absolute paths in the binding are provenance strings only: this consumer never opens them.
    /// </summary>
    public sealed class UnifiedVoteTogetherSourceFixtureTests
    {
        const string RelativeRoot = "Assets/Gamesim/Tests/EditMode/Fixtures/VoteTogetherSource56";
        const string IndexHash = "d44133c3d86d920e122a14a12d7e20b01a9cbd069de6a540b188e46ff7cdd662";
        const string ClosureHash = "9887247435621defc04309be74ebd7916f7607c3e2db068f3e9ef20b696ddfdb";
        const string Attributes = "# Preserve byte-exact original source JSON evidence.\n*.json -text -eol\n.gitattributes -text -eol\n";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly string[] Names = { "binding.json", "inputs-after.json", "inputs-before.json", "source-manifest.json", "vote-settlement-fixtures.json" };
        static readonly long[] Lengths = { 12213, 9566, 9566, 6459, 824319 };
        static readonly string[] Hashes = {
            "38603c886f23d046316cf141c8145fca4f01ce38017f736701c30fb5816655ed",
            "b417352cd3fdbd00df8ea04a3a2ae063301bc5e620d1b4841239641a2070d256",
            "b417352cd3fdbd00df8ea04a3a2ae063301bc5e620d1b4841239641a2070d256",
            "5d061fbc5863a078551d8e9e90e1a84d5f71e298e0ae8d584956acf0bec028c0",
            "9795ad5d415fdcf503a490087faf7e1a63abc53c2cf88dc21f5e7d648d9e901f" };
        static readonly string[] SourcePaths = {
            "src/config.ts", "src/contexts/reducers/reducers/eviction-reducer.ts", "src/models/deal.ts", "src/models/game-state.ts",
            "src/systems/admin-config.ts", "src/systems/deal-action-rules.ts", "src/systems/deal-system.ts", "src/systems/game-event-bus.ts",
            "src/systems/relationship-arc-tracker.ts", "src/utils/random.ts", "src/utils/relationship-snapshot.ts" };
        static readonly string[] CaseIds = {
            "settle-AB-proposer-first-same-low", "settle-AB-proposer-first-same-medium", "settle-AB-proposer-first-same-high", "settle-AB-proposer-first-same-critical",
            "settle-AB-proposer-first-different-low", "settle-AB-proposer-first-different-medium", "settle-AB-proposer-first-different-high", "settle-AB-proposer-first-different-critical",
            "settle-AB-recipient-first-same-low", "settle-AB-recipient-first-same-medium", "settle-AB-recipient-first-same-high", "settle-AB-recipient-first-same-critical",
            "settle-AB-recipient-first-different-low", "settle-AB-recipient-first-different-medium", "settle-AB-recipient-first-different-high", "settle-AB-recipient-first-different-critical",
            "settle-BA-proposer-first-same-low", "settle-BA-proposer-first-same-medium", "settle-BA-proposer-first-same-high", "settle-BA-proposer-first-same-critical",
            "settle-BA-proposer-first-different-low", "settle-BA-proposer-first-different-medium", "settle-BA-proposer-first-different-high", "settle-BA-proposer-first-different-critical",
            "settle-BA-recipient-first-same-low", "settle-BA-recipient-first-same-medium", "settle-BA-recipient-first-same-high", "settle-BA-recipient-first-same-critical",
            "settle-BA-recipient-first-different-low", "settle-BA-recipient-first-different-medium", "settle-BA-recipient-first-different-high", "settle-BA-recipient-first-different-critical",
            "pending-AB-no-votes", "pending-AB-proposer-only", "pending-AB-recipient-only", "pending-AB-outsider-only", "pending-AB-outsider-before-party", "pending-AB-repeated-party",
            "pending-BA-no-votes", "pending-BA-proposer-only", "pending-BA-recipient-only", "pending-BA-outsider-only", "pending-BA-outsider-before-party", "pending-BA-repeated-party",
            "terminal-fulfilled", "terminal-broken", "terminal-expired", "terminal-declined",
            "unsupported-source-settlement-vote-save", "unsupported-source-settlement-vote-evict",
            "boundary-learning-exact-point-four", "boundary-learning-below-point-four", "boundary-alternate-cast-order", "boundary-only-parties-active",
            "boundary-first-ballot-overdue", "boundary-partner-overdue-after-first" };
        static readonly object Gate = new object();
        static Package cached;

        [TestCase("settle-AB-proposer-first-same-low")]
        [TestCase("settle-AB-proposer-first-same-medium")]
        [TestCase("settle-AB-proposer-first-same-high")]
        [TestCase("settle-AB-proposer-first-same-critical")]
        [TestCase("settle-AB-proposer-first-different-low")]
        [TestCase("settle-AB-proposer-first-different-medium")]
        [TestCase("settle-AB-proposer-first-different-high")]
        [TestCase("settle-AB-proposer-first-different-critical")]
        [TestCase("settle-AB-recipient-first-same-low")]
        [TestCase("settle-AB-recipient-first-same-medium")]
        [TestCase("settle-AB-recipient-first-same-high")]
        [TestCase("settle-AB-recipient-first-same-critical")]
        [TestCase("settle-AB-recipient-first-different-low")]
        [TestCase("settle-AB-recipient-first-different-medium")]
        [TestCase("settle-AB-recipient-first-different-high")]
        [TestCase("settle-AB-recipient-first-different-critical")]
        [TestCase("settle-BA-proposer-first-same-low")]
        [TestCase("settle-BA-proposer-first-same-medium")]
        [TestCase("settle-BA-proposer-first-same-high")]
        [TestCase("settle-BA-proposer-first-same-critical")]
        [TestCase("settle-BA-proposer-first-different-low")]
        [TestCase("settle-BA-proposer-first-different-medium")]
        [TestCase("settle-BA-proposer-first-different-high")]
        [TestCase("settle-BA-proposer-first-different-critical")]
        [TestCase("settle-BA-recipient-first-same-low")]
        [TestCase("settle-BA-recipient-first-same-medium")]
        [TestCase("settle-BA-recipient-first-same-high")]
        [TestCase("settle-BA-recipient-first-same-critical")]
        [TestCase("settle-BA-recipient-first-different-low")]
        [TestCase("settle-BA-recipient-first-different-medium")]
        [TestCase("settle-BA-recipient-first-different-high")]
        [TestCase("settle-BA-recipient-first-different-critical")]
        [TestCase("pending-AB-no-votes")]
        [TestCase("pending-AB-proposer-only")]
        [TestCase("pending-AB-recipient-only")]
        [TestCase("pending-AB-outsider-only")]
        [TestCase("pending-AB-outsider-before-party")]
        [TestCase("pending-AB-repeated-party")]
        [TestCase("pending-BA-no-votes")]
        [TestCase("pending-BA-proposer-only")]
        [TestCase("pending-BA-recipient-only")]
        [TestCase("pending-BA-outsider-only")]
        [TestCase("pending-BA-outsider-before-party")]
        [TestCase("pending-BA-repeated-party")]
        [TestCase("terminal-fulfilled")]
        [TestCase("terminal-broken")]
        [TestCase("terminal-expired")]
        [TestCase("terminal-declined")]
        [TestCase("unsupported-source-settlement-vote-save")]
        [TestCase("unsupported-source-settlement-vote-evict")]
        [TestCase("boundary-learning-exact-point-four")]
        [TestCase("boundary-learning-below-point-four")]
        [TestCase("boundary-alternate-cast-order")]
        [TestCase("boundary-only-parties-active")]
        [TestCase("boundary-first-ballot-overdue")]
        [TestCase("boundary-partner-overdue-after-first")]
        public void ActualLatestBallotVerdictAndSeparateSourceOwnerControls(string id)
        {
            var package = Load(); var scenario = Case(package, id); var before = scenario.DeepClone();
            var input = (JObject)scenario["input"]; var steps = (JArray)scenario["steps"];
            var prior = (JObject)scenario["initial"]["deal"];
            int comparisons = 0;
            if (S(prior["type"]) == DealKind.VoteTogether)
            {
                Compare(prior, (JObject)scenario["initial"]["actualPrivateBallots"], null, S(prior["status"]) == DealStatus.Active); comparisons++;
            }
            foreach (JObject step in steps)
            {
                var ballots = (JObject)step["actualPrivateBallots"]; var row = (JObject)step["deal"];
                var action = step["action"]["payload"];
                Assert.That(S(ballots[S(action["voterId"])]), Is.EqualTo(S(action["nomineeId"])));
                Assert.That(S(step["action"]["type"]), Is.EqualTo("SET_EVICTION_VOTE"));
                string type = S(prior["type"]), status = S(prior["status"]), result = S(row["status"]);
                bool partyAction = S(action["voterId"]) == S(prior["proposerId"]) || S(action["voterId"]) == S(prior["recipientId"]);
                if (type == DealKind.VoteTogether && status == DealStatus.Active && partyAction)
                {
                    string expected = result == DealStatus.Fulfilled || result == DealStatus.Broken ? result : null;
                    Compare(prior, ballots, expected, true); comparisons++;
                    // Missing-partner expiry happens AFTER the source predicate. It is not leaf date logic.
                    if (result == DealStatus.Expired) Assert.That(expected, Is.Null);
                }
                else
                {
                    Assert.That(result, Is.EqualTo(status));
                    Assert.That(JToken.DeepEquals(row["context"], prior["context"]), Is.True);
                    Assert.That((int)step["relationshipRequestCount"], Is.EqualTo(0));
                    Assert.That((int)step["interactionRequestCount"], Is.EqualTo(0));
                    Assert.That((int)step["randomDrawCount"], Is.EqualTo(0));
                    if (type == DealKind.VoteTogether && status != DealStatus.Active) { Compare(prior, ballots, null, false); comparisons++; }
                    // vote_save/vote_evict are ORIGINAL-source unsupported settlement controls, not native positives.
                }
                prior = row;
            }
            Assert.That(comparisons + (S(input["type"]) == DealKind.VoteTogether ? 0 : steps.Count), Is.GreaterThan(0));
            Assert.That(JToken.DeepEquals(prior, scenario["final"]["deal"]), Is.True);
            Assert.That(JToken.DeepEquals(before, scenario), Is.True, "readers must not modify captured source evidence");
            Unchanged(package);
        }

        [Test]
        public void CompleteNamedInventoryBoundsAndNonemptyActualSourceDelivery()
        {
            var p = Load(); var doc = ParseObject(p.Raw[Names[4]]);
            Assert.That((int)doc["schema"], Is.EqualTo(1));
            Assert.That(S(doc["kind"]), Is.EqualTo("original-web-vote-together-source-system-boundary-oracle"));
            Assert.That((int)doc["actualCases"], Is.EqualTo(56)); Assert.That((int)doc["expectedCases"], Is.EqualTo(56));
            var cases = (JArray)doc["scenarios"];
            Assert.That(cases.Select(c => S(c["id"])).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(CaseIds.OrderBy(x => x, StringComparer.Ordinal).ToArray()));
            var groups = new Dictionary<string, int> { { "complete-settlement", 32 }, { "ordered-pending", 12 }, { "terminal-guard", 4 }, { "targeted-kind-negative", 2 }, { "source-boundary", 6 } };
            foreach (var group in groups) { Assert.That((int)doc["groups"][group.Key], Is.EqualTo(group.Value)); Assert.That(cases.Count(c => S(c["group"]) == group.Key), Is.EqualTo(group.Value)); }
            Assert.That(((JObject)doc["groups"]).Count, Is.EqualTo(5));
            int actions = 0, requests = 0, interactions = 0, draws = 0, clocks = 0, events = 0;
            foreach (JObject c in cases)
            {
                var input = c["input"]; var steps = (JArray)c["steps"];
                Assert.That(((JArray)input["castOrder"]).Count, Is.EqualTo(6)); Assert.That(steps.Count, Is.LessThanOrEqualTo(8));
                Assert.That((bool)input["boundaryDealNotCreatedViaCreateDeal"], Is.True);
                Assert.That(S(input["optionalPromiseSystem"]), Is.EqualTo("not-attached")); Assert.That(((JArray)input["loyaltyOaths"]).Count, Is.EqualTo(0));
                Assert.That(((JArray)c["sourceConsoleErrors"]).Count, Is.EqualTo(0));
                Assert.That(((JArray)c["orderedObservations"]).Any(o => S(o["kind"]) == "unexpected-legacy-fallback"), Is.False);
                int n = 0; foreach (var step in steps) Assert.That((int)step["index"], Is.EqualTo(n++));
                n = 0; foreach (var observation in (JArray)c["orderedObservations"]) Assert.That((int)observation["order"], Is.EqualTo(n++));
                Assert.That(n, Is.LessThanOrEqualTo(512));
                foreach (string field in new[] { "randomDraws", "clockCalls" })
                { n = 0; foreach (var tick in (JArray)c[field]) Assert.That((int)tick["index"], Is.EqualTo(n++)); Assert.That(n, Is.LessThanOrEqualTo(64)); }
                actions += steps.Count; requests += ((JArray)c["relationshipRequests"]).Count; interactions += ((JArray)c["interactionRequests"]).Count;
                draws += ((JArray)c["randomDraws"]).Count; clocks += ((JArray)c["clockCalls"]).Count; events += ((JArray)c["globalListenerDeliveryOrder"]).Count;
                Assert.That(((JArray)c["orderedObservations"]).Count(o => S(o["kind"]) == "public-reducer-action"), Is.EqualTo(steps.Count));
                Assert.That(((JArray)c["globalListenerDeliveryOrder"]).Count(o => S(o["type"]) == "vote_cast"), Is.EqualTo(steps.Count));
            }
            Assert.That(actions, Is.EqualTo(104)); Assert.That(requests, Is.EqualTo(42)); Assert.That(interactions, Is.EqualTo(39));
            Assert.That(draws, Is.EqualTo(87)); Assert.That(clocks, Is.EqualTo(183)); Assert.That(events, Is.EqualTo(143));
            Unchanged(p);
        }

        [Test]
        public void RecordedAcceptedTwoRunBindingAndOriginalClosureAreProvenanceNotExternalIO()
        {
            var p = Load(); var b = ParseObject(p.Raw[Names[0]]); var m = ParseObject(p.Raw[Names[3]]);
            Assert.That((bool)b["accepted"], Is.True); Assert.That((bool)b["twiceByteEqual"], Is.True); Assert.That((bool)b["sourceInputsUnchanged"], Is.True);
            Assert.That((int)b["requiredIndependentRuns"], Is.EqualTo(2)); Assert.That((int)b["actualCasesPerRun"], Is.EqualTo(56));
            Assert.That((int)b["deadlineSecondsPerNodeProcess"], Is.EqualTo(180));
            var runs = (JArray)b["runs"]; Assert.That(runs.Count, Is.EqualTo(2));
            Assert.That(runs.Select(r => S(r["slot"])).ToArray(), Is.EqualTo(new[] { "oracle-a", "oracle-b" }));
            foreach (var run in runs)
            {
                Assert.That((bool)run["completed"], Is.True); Assert.That((bool)run["timedOut"], Is.False); Assert.That((int)run["exitCode"], Is.EqualTo(0));
                Assert.That((bool)run["nativeHandleCaptured"], Is.True);
                Assert.That(S(run["fixture"]["sha256"]), Is.EqualTo(Hashes[4])); Assert.That((long)run["fixture"]["bytes"], Is.EqualTo(Lengths[4]));
                Assert.That(S(run["manifest"]["sha256"]), Is.EqualTo(Hashes[3])); Assert.That(S(run["sourceClosureSha256"]), Is.EqualTo(ClosureHash));
                Assert.That((long)run["stderr"]["bytes"], Is.EqualTo(0));
            }
            Assert.That(p.Raw[Names[1]].SequenceEqual(p.Raw[Names[2]]), Is.True);
            var inputs = ParseArray(p.Raw[Names[2]]); Assert.That(inputs.Count, Is.EqualTo(28));
            Assert.That(inputs.Select(i => S(i["label"])).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(28));
            var otherLabels = new[] { "node", "esbuild-executable", "esbuild-main", "esbuild-package", "original-tsconfig", "original-package",
                "retained-golden-fixtures.json", "retained-collect-fixtures.ts", "retained-generate-fixtures.mjs", "retained-source-manifest.json",
                "procedure-collect-vote-settlement-fixtures.ts", "procedure-generate-vote-settlement-fixtures.mjs", "procedure-run-vote-settlement-fixtures.ps1",
                "retained-v2-launcher", "console-host-tool", "retained-v3-launcher", "actual-v4-launcher" };
            Assert.That(inputs.Select(i => S(i["label"])).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(SourcePaths.Concat(otherLabels).OrderBy(x => x, StringComparer.Ordinal).ToArray()));
            foreach (var entry in inputs) { Assert.That((long)entry["bytes"], Is.GreaterThan(0)); Assert.That(IsHash(S(entry["sha256"])), Is.True); }
            Assert.That(S(b["before"]["sha256"]), Is.EqualTo(Hashes[2])); Assert.That(S(b["after"]["sha256"]), Is.EqualTo(Hashes[1]));
            Assert.That((bool)m["accepted"], Is.True); Assert.That((bool)m["sourceInputsUnchanged"], Is.True);
            Assert.That((int)m["actualBundledOriginalModules"], Is.EqualTo(11)); Assert.That((int)m["collectorModules"], Is.EqualTo(1));
            Assert.That(S(m["sourceClosureSha256"]), Is.EqualTo(ClosureHash));
            var closure = (JArray)m["sourceClosure"]; Assert.That(closure.Select(c => S(c["path"])).ToArray(), Is.EqualTo(SourcePaths));
            foreach (var leaf in closure)
            {
                var actual = inputs.Single(i => S(i["label"]) == S(leaf["path"]));
                Assert.That(S(actual["sha256"]), Is.EqualTo(S(leaf["sha256"]))); Assert.That((long)actual["bytes"], Is.EqualTo((long)leaf["bytes"]));
            }
            var closureText = string.Join("\n", closure.Select(c => S(c["path"]) + " " + (long)c["bytes"] + " " + S(c["sha256"])));
            Assert.That(Sha(Utf8.GetBytes(closureText)), Is.EqualTo(ClosureHash));
            Assert.That(((JArray)m["procedures"]).Count, Is.EqualTo(3));
            Assert.That(S(m["procedures"][2]["sha256"]), Is.EqualTo("8ebb0c688b0be907c9f47e119436d915bee9ac53de539a759f2382b86b9e631d"));
            Assert.That(S(b["actualLauncher"]["sha256"]), Is.EqualTo("dc5a4bf54cc93d8d803d130c6ef7c4af54a757d918f731ca2b307d7b5a33a272"));
            Assert.That(S(b["actualLauncher"]["sha256"]), Is.Not.EqualTo(S(m["procedures"][2]["sha256"])));
            foreach (var procedure in (JArray)m["procedures"])
            {
                var recorded = inputs.Single(i => S(i["label"]) == "procedure-" + S(procedure["path"]));
                Assert.That(S(recorded["sha256"]), Is.EqualTo(S(procedure["sha256"])));
                Assert.That((long)recorded["bytes"], Is.EqualTo((long)procedure["bytes"]));
            }
            foreach (string key in new[] { "actualLauncher", "retainedV2Launcher", "retainedV3Launcher", "consoleHost" })
            {
                var recorded = inputs.Single(i => S(i["label"]) == S(b[key]["label"]));
                Assert.That(S(recorded["sha256"]), Is.EqualTo(S(b[key]["sha256"]))); Assert.That((long)recorded["bytes"], Is.EqualTo((long)b[key]["bytes"]));
            }
            var observers = (JArray)b["consoleObservers"]; Assert.That(observers.Count, Is.EqualTo(2));
            var owners = (JArray)b["ownedProcesses"]; Assert.That(owners.Count, Is.EqualTo(2));
            foreach (var observer in observers)
            {
                Assert.That((bool)observer["parentClosed"], Is.True); Assert.That((bool)observer["closed"], Is.True);
                Assert.That(observer["closureError"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(S(observer["role"]), Is.EqualTo("console-host-observer"));
                Assert.That((int)observer["closureWaitBoundMilliseconds"], Is.EqualTo(1000));
                Assert.That(owners.Any(o => (int)o["pid"] == (int)observer["pid"]), Is.False);
                Assert.That(runs.Any(r => (int)r["pid"] == (int)observer["parentPid"] && (long)r["startTicks"] == (long)observer["parentStartTicks"]), Is.True);
            }
            foreach (string flag in new[] { "nativeUnityAcceptance", "ordinarySaveAcceptance", "targetedNativeExtensionAcceptance" }) Assert.That((bool)m[flag], Is.False);
            Assert.That((bool)m["reusedGoldens"]["regenerated"], Is.False);
            Assert.That((int)m["reusedGoldens"]["groups"][0]["rows"], Is.EqualTo(14)); Assert.That((int)m["reusedGoldens"]["groups"][1]["rows"], Is.EqualTo(63));
            Unchanged(p);
        }

        [TestCase("low", false, 8, 12)]
        [TestCase("medium", false, 12, 18)]
        [TestCase("high", false, 16, 24)]
        [TestCase("critical", false, 24, 36)]
        [TestCase("low", true, -15, -30)]
        [TestCase("medium", true, -22, -44)]
        [TestCase("high", true, -30, -60)]
        [TestCase("critical", true, -45, -90)]
        public void OriginalRoundedEffectRequestsAreDirectionalSourceOutputsOnly(string weight, bool broken, int change, int interaction)
        {
            var p = Load(); string outcome = broken ? "different" : "same";
            foreach (string direction in new[] { "AB", "BA" }) foreach (string order in new[] { "proposer-first", "recipient-first" })
            {
                var c = Case(p, "settle-" + direction + "-" + order + "-" + outcome + "-" + weight);
                var input = c["input"]; var requests = (JArray)c["relationshipRequests"]; var interactions = (JArray)c["interactionRequests"];
                Assert.That(requests.Count, Is.EqualTo(1)); Assert.That(interactions.Count, Is.EqualTo(1));
                Assert.That(S(requests[0]["type"]), Is.EqualTo("UPDATE_RELATIONSHIPS"));
                Assert.That(S(requests[0]["payload"]["guestId1"]), Is.EqualTo(S(input["proposerId"])));
                Assert.That(S(requests[0]["payload"]["guestId2"]), Is.EqualTo(S(input["recipientId"])));
                Assert.That((int)requests[0]["payload"]["change"], Is.EqualTo(change)); Assert.That((int)interactions[0][4], Is.EqualTo(interaction));
                Assert.That((int)c["final"]["alliances"][0]["stability"], Is.EqualTo(broken ? 65 : 78));
                var delivery = (JArray)c["globalListenerDeliveryOrder"]; var history = (JArray)c["eventHistoryInsertionOrderFinal"];
                string kind = broken ? "deal_broken" : "deal_fulfilled";
                Assert.That(delivery.Select(e => S(e["type"])).ToArray(), Is.EqualTo(new[] { "vote_cast", kind, "vote_cast" }));
                Assert.That(history.Select(e => S(e["type"])).ToArray(), Is.EqualTo(new[] { "vote_cast", "vote_cast", kind }));
                Assert.That(JToken.DeepEquals(delivery[1]["data"]["deal"], c["final"]["deal"]), Is.True);
                Assert.That((int)((JArray)c["steps"])[0]["relationshipRequestCount"], Is.EqualTo(0));
            }
            // Medium original Math.round(-22.5) is -22. Native legacy raw-double effects remain -22.5;
            // this fixture deliberately does NOT execute a relationship reducer or alter native arithmetic.
            Unchanged(p);
        }

        [TestCase("boundary-learning-exact-point-four", 4, 1, "broken")]
        [TestCase("boundary-learning-below-point-four", 5, 2, "broken")]
        [TestCase("boundary-alternate-cast-order", 6, 3, "broken")]
        [TestCase("boundary-only-parties-active", 0, 1, "broken")]
        [TestCase("boundary-first-ballot-overdue", 0, 0, "expired")]
        [TestCase("boundary-partner-overdue-after-first", 0, 1, "fulfilled")]
        public void RecordedSourceGossipAndLazyExpiryDoNotInventNativePolicy(string id, int draws, int requests, string status)
        {
            var p = Load(); var c = Case(p, id); var random = (JArray)c["randomDraws"]; var relation = (JArray)c["relationshipRequests"];
            Assert.That(random.Count, Is.EqualTo(draws)); Assert.That(relation.Count, Is.EqualTo(requests)); Assert.That(S(c["final"]["deal"]["status"]), Is.EqualTo(status));
            if (id == "boundary-learning-exact-point-four") Assert.That(random.All(r => (double)r["value"] == 0.4), Is.True);
            if (id == "boundary-learning-below-point-four")
            { Assert.That((double)random[0]["value"], Is.LessThan(0.4)); Assert.That(S(relation[1]["payload"]["guestId1"]), Is.EqualTo("C")); Assert.That(S(relation[1]["payload"]["guestId2"]), Is.EqualTo("A")); Assert.That((int)relation[1]["payload"]["change"], Is.EqualTo(-10)); }
            if (id == "boundary-alternate-cast-order")
            {
                Assert.That(((JArray)c["input"]["castOrder"]).Select(S).ToArray(), Is.EqualTo(new[] { "F", "D", "A", "E", "B", "C" }));
                Assert.That(relation.Skip(1).Select(r => S(r["payload"]["guestId1"])).ToArray(), Is.EqualTo(new[] { "F", "E" }));
                Assert.That(relation.Skip(1).Select(r => (int)r["payload"]["change"]).ToArray(), Is.EqualTo(new[] { -5, -15 }));
            }
            if (id == "boundary-first-ballot-overdue")
            {
                Assert.That(S(c["steps"][0]["deal"]["status"]), Is.EqualTo("expired"));
                Assert.That(((JObject)c["final"]["deal"]["context"]["_voteTracking"]).Count, Is.EqualTo(1));
                Assert.That(((JObject)c["final"]["actualPrivateBallots"]).Count, Is.EqualTo(2));
            }
            if (id == "boundary-partner-overdue-after-first")
            { Assert.That((int)c["input"]["actions"][1]["week"], Is.EqualTo(5)); Assert.That((int)c["final"]["deal"]["expiresWeek"], Is.EqualTo(4)); }
            Unchanged(p);
        }

        [TestCase("missing")]
        [TestCase("extra")]
        [TestCase("raw-byte")]
        [TestCase("index-byte")]
        [TestCase("metadata-byte")]
        [TestCase("missing-root-meta")]
        [TestCase("extra-attributes-meta")]
        [TestCase("attribute-byte")]
        public void DetachedPackageTamperingIsRefusedWithoutWritingAssets(string defect)
        {
            var p = Load(); var image = p.Image.ToDictionary(k => k.Key, v => (byte[])v.Value.Clone(), StringComparer.Ordinal);
            if (defect == "missing") image.Remove(Names[0]);
            if (defect == "extra") image.Add("unexpected.json", new byte[] { 123, 125 });
            if (defect == "raw-byte") image[Names[4]][0] ^= 1;
            if (defect == "index-byte") image["portable-index.json"][0] ^= 1;
            if (defect == "metadata-byte") image[Names[0] + ".meta"][0] ^= 1;
            if (defect == "missing-root-meta") image.Remove("@root.meta");
            if (defect == "extra-attributes-meta") image.Add(".gitattributes.meta", image[Names[0] + ".meta"]);
            if (defect == "attribute-byte") image[".gitattributes"][0] ^= 1;
            Assert.Throws<InvalidDataException>(() => ValidateImage(image)); Unchanged(p);
        }

        [TestCase("../binding.json")]
        [TestCase("sub/binding.json")]
        [TestCase("sub\\binding.json")]
        [TestCase("C:\\binding.json")]
        [TestCase("/binding.json")]
        [TestCase("binding.json:stream")]
        public void PortablePathGrammarRefusesTraversalAbsoluteAndAlternateStreams(string path)
        { Assert.Throws<InvalidDataException>(() => Relative(path)); Unchanged(Load()); }

        static void Compare(JObject source, JObject map, string expected, bool legacy)
        {
            var sourceBefore = source.DeepClone(); var mapBefore = map.DeepClone();
            var row = new UnifiedCommitmentState { id = S(source["id"]), kind = "vote", sourcePolicy = UnifiedCommitments.DealPolicy,
                subtype = DealKind.VoteTogether, reciprocal = true, targetId = null,
                makerId = S(source["proposerId"]), beneficiaryId = S(source["recipientId"]), status = S(source["status"]) };
            var ballots = map.Properties().Select(v => new UnifiedVoteBallotState { voterId = v.Name, targetId = S(v.Value) }).ToList();
            var rowBefore = row.Clone(); var ballotBefore = ballots.Select(b => b.Clone()).ToList();
            Assert.That(UnifiedVoteTogether.TryVerdict(row, ballots, out string verdict, out string error), Is.True, error);
            Assert.That(error, Is.Null); Assert.That(verdict, Is.EqualTo(expected));
            if (legacy)
            {
                var deal = new DealState { id = row.id, type = DealKind.VoteTogether, proposerId = row.makerId, recipientId = row.beneficiaryId, status = row.status };
                var votes = ballots.Select(b => new VoteState { voterId = b.voterId, targetId = b.targetId }).ToList();
                Assert.That(DealResolution.VoteTogether(deal, votes), Is.EqualTo(expected));
            }
            Assert.That(JsonConvert.SerializeObject(row), Is.EqualTo(JsonConvert.SerializeObject(rowBefore)));
            Assert.That(JsonConvert.SerializeObject(ballots), Is.EqualTo(JsonConvert.SerializeObject(ballotBefore)));
            Assert.That(JToken.DeepEquals(source, sourceBefore), Is.True); Assert.That(JToken.DeepEquals(map, mapBefore), Is.True);
        }

        sealed class Package
        {
            public string Root;
            public Dictionary<string, byte[]> Image, Raw;
        }
        static Package Load()
        {
            lock (Gate)
            {
                if (cached != null) return cached;
                string root = Discover(); NoReparse(root); var image = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var expected = ExpectedImage();
                var files = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToArray();
                Require(Directory.GetDirectories(root).Length == 0 && files.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.Keys.Where(k => k != "@root.meta").OrderBy(x => x, StringComparer.Ordinal)), "exact inner inventory required");
                // Every allocation has an exact pre-read size bound. Five captured files total < 1 MiB.
                long total = 0;
                foreach (var entry in expected)
                {
                    string path = entry.Key == "@root.meta" ? root + ".meta" : Path.Combine(root, Relative(entry.Key));
                    long length = entry.Value; total = checked(total + length); Require(total <= 1024 * 1024, "package allocation bound");
                    image.Add(entry.Key, ReadExact(path, length));
                }
                ValidateImage(image);
                cached = new Package { Root = root, Image = image, Raw = Names.ToDictionary(n => n, n => image[n], StringComparer.Ordinal) };
                return cached;
            }
        }
        static Dictionary<string, long> ExpectedImage()
        {
            var expected = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < Names.Length; i++) expected.Add(Names[i], Lengths[i]);
            // This literal index was authored separately and pinned before tests were compiled.
            expected.Add("portable-index.json", 777); expected.Add(".gitattributes", Utf8.GetByteCount(Attributes));
            foreach (string name in Names.Concat(new[] { "portable-index.json" })) expected.Add(name + ".meta", Meta(name, false).Length);
            expected.Add("@root.meta", Meta("", true).Length); return expected;
        }
        static void ValidateImage(Dictionary<string, byte[]> image)
        {
            var expected = ExpectedImage(); Require(image != null && image.Count == expected.Count && expected.Keys.All(image.ContainsKey), "exact package image required");
            foreach (var entry in expected) Require(image[entry.Key] != null && image[entry.Key].LongLength == entry.Value, "exact bounded bytes required");
            Require(Sha(image["portable-index.json"]) == IndexHash, "index pin");
            Require(image[".gitattributes"].SequenceEqual(Utf8.GetBytes(Attributes)), "byte-preserving attributes");
            var index = ParseObject(image["portable-index.json"]); Require(index.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "files", "kind", "schema", "version" }), "index exact fields");
            Require((int)index["schema"] == 1 && (int)index["version"] == 1 && S(index["kind"]) == "gamesim-original-vote-together-source56-portable", "index version");
            var entries = (JArray)index["files"]; Require(entries.Count == 5, "five originals only");
            for (int i = 0; i < Names.Length; i++)
            {
                var e = (JObject)entries[i]; Require(e.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "bytes", "path", "sha256" }), "index entry shape");
                Require(Relative(S(e["path"])) == Names[i] && (long)e["bytes"] == Lengths[i] && S(e["sha256"]) == Hashes[i] && Sha(image[Names[i]]) == Hashes[i], "raw evidence pin");
            }
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in Names.Concat(new[] { "portable-index.json" }))
            { Require(image[name + ".meta"].SequenceEqual(Meta(name, false)), "canonical file metadata"); Require(guids.Add(Guid(name)), "unique metadata"); }
            Require(image["@root.meta"].SequenceEqual(Meta("", true)) && guids.Add(Guid("")) && guids.Count == 7, "canonical root metadata");
        }
        static string Discover()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory, Path.GetDirectoryName(typeof(UnifiedVoteTogetherSourceFixtureTests).Assembly.Location) })
            {
                if (string.IsNullOrEmpty(start)) continue; var dir = new DirectoryInfo(Path.GetFullPath(start));
                for (int depth = 0; dir != null && depth < 12; depth++, dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, RelativeRoot.Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(candidate)) { NoReparse(dir.FullName); NoReparse(candidate); found.Add(Path.GetFullPath(candidate)); }
                }
            }
            Require(found.Count == 1, "exactly one portable source package required; no absolute provenance fallback"); return found.Single();
        }
        static void NoReparse(string path)
        {
            for (var dir = new DirectoryInfo(Path.GetFullPath(path)); dir != null; dir = dir.Parent)
                Require((dir.Attributes & FileAttributes.ReparsePoint) == 0, "reparse ancestor refused");
        }
        static byte[] ReadExact(string path, long length)
        {
            var info = new FileInfo(path); Require(info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0 && info.Length == length && length >= 0 && length <= 1024 * 1024, "pre-read exact bounded file required");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Require(stream.Length == length, "file changed before bounded read");
                var bytes = new byte[(int)length]; int offset = 0;
                while (offset < bytes.Length)
                { int read = stream.Read(bytes, offset, bytes.Length - offset); Require(read > 0, "truncated bounded read"); offset += read; }
                Require(stream.ReadByte() == -1 && stream.Length == length, "file grew during bounded read"); return bytes;
            }
        }
        static void Unchanged(Package p)
        {
            var expected = ExpectedImage(); NoReparse(p.Root);
            Require(Directory.GetDirectories(p.Root).Length == 0, "no new package directories");
            Require(Directory.GetFiles(p.Root).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.Keys.Where(k => k != "@root.meta").OrderBy(x => x, StringComparer.Ordinal)), "physical inventory unchanged");
            foreach (var entry in p.Image)
            { string path = entry.Key == "@root.meta" ? p.Root + ".meta" : Path.Combine(p.Root, Relative(entry.Key)); Assert.That(ReadExact(path, expected[entry.Key]).SequenceEqual(entry.Value), Is.True, entry.Key + " byte immutability"); }
        }
        static byte[] Meta(string name, bool folder) => Utf8.GetBytes("fileFormatVersion: 2\nguid: " + Guid(name) + "\n" +
            (folder ? "folderAsset: yes\nDefaultImporter:\n" : "TextScriptImporter:\n") + "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        static string Guid(string name) => Sha(Utf8.GetBytes("gamesim-vote-together-source56-fixture-v1|" + name)).Substring(0, 32);
        static string Relative(string path)
        { Require(!string.IsNullOrEmpty(path) && path.Length <= 96 && path != "." && path != ".." && path.IndexOfAny(new[] { '/', '\\', ':', '\0' }) < 0 && !Path.IsPathRooted(path), "single relative filename required"); return path; }
        static JObject Case(Package p, string id) => (JObject)((JArray)ParseObject(p.Raw[Names[4]])["scenarios"]).Single(c => S(c["id"]) == id).DeepClone();
        static JObject ParseObject(byte[] bytes) => (JObject)Parse(bytes);
        static JArray ParseArray(byte[] bytes) => (JArray)Parse(bytes);
        static JToken Parse(byte[] bytes)
        {
            using (var reader = new JsonTextReader(new StringReader(Utf8.GetString(bytes))) { DateParseHandling = DateParseHandling.None })
            { var result = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); Require(!reader.Read(), "trailing JSON refused"); return result; }
        }
        static string S(JToken token) { Require(token != null && token.Type == JTokenType.String, "string required"); return (string)token; }
        static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        static string Sha(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
    }
}
