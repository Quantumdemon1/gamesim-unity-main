using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Explicit conversion of supported all-active social saves, at any house size this format accepts.
    /// Unsupported gameplay is rejected;
    /// the entire supplied source is archived before any candidate can be returned.
    /// </summary>
    public static class WebSaveImporter
    {
        private static readonly string[] StatNames =
            { "physical", "mental", "endurance", "social", "luck", "competition", "strategic", "loyalty" };
        private static readonly string[] EmptyStateSlots =
        {
            "hohWinner", "povWinner", "winner", "runnerUp", "nominees", "povPlayers", "juryMembers", "finalTwo",
            "alliances", "allianceData", "gameLog", "promises", "deals", "pendingNPCProposals", "houseEvents",
            "relationshipArcs", "loyaltyOaths", "activeStorylines", "activeModifiers", "npcMemories",
            "pendingAllianceInvitations", "shownMilestones", "completedStorylineIds", "evictionVotes",
            "evictionVoteDecisions", "isFinalStage", "isSpectatorMode", "evictionCompletedThisWeek",
            "outOfPhaseSocialActionsUsed", "playerStudyBonus", "phaseEventSocialBonus", "phaseEventCompBonus",
            "playerPerceptions", "threatMatrix", "playerPersona", "jurySentiment", "weekFocusModifier",
            "backdoorTarget", "pendingStorylineEvent", "grudgeLedger", "playerLastSpeech", "lastEavesdropIntel"
        };
        private static readonly string[] GuestMetadata =
            { "age", "occupation", "hometown", "bio", "imageUrl", "avatarUrl", "avatarConfig", "personalityProfile" };

        public static bool TryImport(string json, out EpisodeState state, out string message)
            => TryImport(json, Path.Combine(Application.persistentDataPath, "Gamesim", "Imports"), out state, out message);

        public static bool TryImport(string json, string archiveDirectory, out EpisodeState state, out string message)
        {
            state = null;
            string archived = null;
            try
            {
                if (json == null || Encoding.UTF8.GetByteCount(json) > SaveJson.MaximumBytes)
                    throw new InvalidDataException("Import is missing or exceeds the eight MiB size limit.");
                var digest = SaveJson.Hash(json);
                archived = Path.Combine(Path.GetFullPath(archiveDirectory), "web-original-" + digest + ".json");
                var rawBytes = Encoding.UTF8.GetBytes(json);
                if (!File.Exists(archived)) SaveJson.WriteNewDurable(archived, rawBytes);
                else if (!File.ReadAllBytes(archived).SequenceEqual(rawBytes))
                    throw new InvalidDataException("The source archive could not be verified.");

                var root = SaveJson.ParseObject(json);
                var source = Unwrap(root);
                var report = new HashSet<string>(StringComparer.Ordinal);
                var candidate = Convert(source, digest, report);
                EpisodeSaveValidation.Validate(candidate);
                state = candidate;
                message = "Imported a supported social scenario with " + candidate.contestants.Count + " contestants. Actor IDs, stats, directed scores and competition history were preserved. "
                    + "Unity room homes, motives, camera, and deterministic random state use new scenario defaults. "
                    + "Voting-bloc rules begin in week " + candidate.blocRulesStartWeek + "; the current week is unchanged. "
                    + "NPC conversations begin in week " + candidate.npcSocial.rulesStartWeek + "; the current week is unchanged. "
                    + (report.Count == 0 ? "" : "Metadata retained in the original archive: " + string.Join(", ", report.OrderBy(value => value)) + ". ")
                    + "Original preserved in your saves folder.";
                return true;
            }
            catch (Exception error) when (SaveJson.IsExpected(error))
            {
                // The exception type rather than its text: an IO failure's message embeds the
                // full path of whatever it could not read.
                message = "Web save was not installed: " + SaveJson.Explain(error)
                    + (archived != null && File.Exists(archived)
                        ? " Original preserved in your saves folder." : " No game state was changed.");
                return false;
            }
        }

        private static JObject Unwrap(JObject input)
        {
            if ((string)input["format"] == "gamesim-local-recovery-export")
                throw new InvalidDataException("This is a recovery archive. Select and export one supported copies[].raw game payload first; copies are never chosen automatically.");
            if ((string)input["rawEncoding"] == "gamesim-structured-json-v1")
                throw new InvalidDataException("Tagged structured recovery copies require a compatible decoder and are not supported by this importer.");
            if ((string)input["format"] == "gamesim-local-fallback")
            {
                CheckKeys(input, new[] { "format", "version", "writtenAt", "value" }, "fallback envelope");
                Require(Integer(input["version"], "fallback version") == 1 && input["value"] is JObject,
                    "Unsupported fallback envelope.");
                Number(input["writtenAt"], "fallback writtenAt");
                input = (JObject)input["value"];
            }

            if (input["format"] != null || input["version"] != null || input["state"] != null)
            {
                CheckKeys(input, new[] { "format", "version", "savedAt", "state" }, "save envelope");
                var version = Integer(input["version"], "save version");
                Require((string)input["format"] == "gamesim-save" && (version == 1 || version == 2),
                    "Unsupported web save format or version; supported envelope versions are 1 and 2.");
                Require(input["savedAt"]?.Type == JTokenType.String
                    && DateTimeOffset.TryParse((string)input["savedAt"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
                    "The save timestamp is invalid.");
                Require(input["state"] is JObject, "The saved state must be an object.");
                return (JObject)input["state"];
            }

            return input;
        }

        private static EpisodeState Convert(JObject input, string digest, HashSet<string> report)
        {
            foreach (var receipt in new[] { "nominationResolution", "vetoResolution" })
                Require(input.Property(receipt) == null, "Live or historical " + receipt + " is not supported; receipt history cannot be discarded.");
            var supported = new HashSet<string>(EmptyStateSlots.Concat(new[]
            {
                "phase", "week", "houseguests", "relationships", "previousHoHId", "finalHoHWinners", "playerPersonaLabel",
                "lastDiaryRoomWeek", "lastCrisisWeek", "lastWarRoomWeek", "lastBranchingStoryWeek", "lastStoryBeatWeek"
            }), StringComparer.Ordinal);
            CheckKeys(input, supported, "game state");
            Require(Text(input["phase"], "phase").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "") == "socialinteraction",
                "Only SocialInteraction boundary saves are supported. In-progress ceremonies and finales require additional migration.");
            foreach (var slot in EmptyStateSlots) RequireDefaultStateSlot(input[slot], slot);
            if (input["finalHoHWinners"] != null && input["finalHoHWinners"].Type != JTokenType.Null)
            {
                Require(input["finalHoHWinners"] is JObject, "Final HoH history is invalid.");
                var finals = (JObject)input["finalHoHWinners"];
                CheckKeys(finals, new[] { "part1", "part2", "part3" }, "final HoH history");
                Require(finals.Properties().All(property => property.Value.Type == JTokenType.Null), "Final HoH outcomes cannot be discarded.");
            }
            Require(input["playerPersonaLabel"] == null || (string)input["playerPersonaLabel"] == "Neutral",
                "Non-neutral player persona is not represented by this importer.");
            foreach (var historical in new[] { "lastDiaryRoomWeek", "lastCrisisWeek", "lastWarRoomWeek", "lastBranchingStoryWeek", "lastStoryBeatWeek" })
                if (input[historical] != null && input[historical].Type != JTokenType.Null)
                {
                    Require(Integer(input[historical], historical) >= 0, "Historical week is invalid.");
                    report.Add(historical);
                }

            var week = Integer(input["week"], "week");
            Require(week >= 1 && week <= 100, "A playable social save must be within the supported week range 1–100.");
            // The cast is taken at whatever size the web save carries, within the range this format
            // validates. It used to demand exactly six, which rejected every ordinary web save —
            // that game defaults to eight. The guarantee that mattered is unchanged: the whole cast
            // is imported or the import fails, and actors are never silently discarded.
            Require(input["houseguests"] is JArray guests
                    && guests.Count >= EpisodeValidation.MinimumCast
                    && guests.Count <= EpisodeValidation.MaximumCast,
                "Import requires between " + EpisodeValidation.MinimumCast + " and "
                    + EpisodeValidation.MaximumCast + " contestants; actors cannot be silently discarded.");
            var seed = uint.Parse(digest.Substring(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var result = new EpisodeState
            {
                sessionId = "web-import-" + digest.Substring(0, 24), seed = seed, randomState = seed,
                week = week, phase = EpisodePhase.Social, blocRulesStartWeek = checked(week + 1),
                npcSocial = NpcSocialState.Create(seed, checked(week + 1)),
                previousHohId = input["previousHoHId"] == null || input["previousHoHId"].Type == JTokenType.Null
                    ? null : Text(input["previousHoHId"], "previousHoHId")
            };
            var homes = new[] { "Living", "Private", "Yard", "Kitchen", "Living", "Bedroom" };
            var index = 0;
            foreach (var value in (JArray)input["houseguests"])
            {
                Require(value is JObject, "Each contestant must be an object.");
                result.contestants.Add(ConvertGuest((JObject)value, homes[index++ % homes.Length], report));
            }
            Require(result.contestants.Select(actor => actor.id).Distinct(StringComparer.Ordinal).Count() == result.contestants.Count,
                "Contestant IDs must be unique.");
            Require(result.contestants.Count(actor => actor.isPlayer) == 1, "Import requires exactly one controlled player.");
            result.playerId = result.contestants.Single(actor => actor.isPlayer).id;
            Require(input["relationships"] is JObject, "Relationships must be a directed nested object.");
            var ids = new HashSet<string>(result.contestants.Select(actor => actor.id), StringComparer.Ordinal);
            foreach (var from in ((JObject)input["relationships"]).Properties())
            {
                Require(ids.Contains(from.Name) && from.Value is JObject, "Relationship source is invalid.");
                foreach (var to in ((JObject)from.Value).Properties())
                {
                    Require(ids.Contains(to.Name) && to.Name != from.Name && to.Value is JObject, "Relationship target is invalid.");
                    var relationship = (JObject)to.Value;
                    CheckKeys(relationship, new[] { "score", "alliance", "notes", "events", "lastInteractionWeek", "weekStartScore" }, "relationship");
                    Require((relationship["alliance"] == null || relationship["alliance"].Type == JTokenType.Null)
                        && (relationship["events"] == null || relationship["events"] is JArray eventArray && eventArray.Count == 0),
                        "Relationship alliance or event history is not supported.");
                    result.relationships.Add(new RelationshipState { fromId = from.Name, toId = to.Name, score = Number(relationship["score"], "relationship score") });
                    if (relationship["notes"] != null)
                    {
                        Require(relationship["notes"] is JArray, "Relationship notes must be an array.");
                        foreach (var note in (JArray)relationship["notes"])
                            result.memories.Add(new MemoryState
                            {
                                ownerId = from.Name, subjectId = to.Name, text = Text(note, "relationship note"), week = week, isPrivate = true
                            });
                        if (((JArray)relationship["notes"]).Count > 0) report.Add("relationship notes converted to private memories with import week");
                    }
                    foreach (var metadata in new[] { "lastInteractionWeek", "weekStartScore" })
                        if (relationship[metadata] != null)
                        {
                            Number(relationship[metadata], "relationship " + metadata);
                            report.Add("relationship." + metadata);
                        }
                }
            }
            if (result.relationships.Count < 30) report.Add("absent directed relationships use the Unity neutral-score default");
            return result;
        }

        private static ContestantState ConvertGuest(JObject input, string homeRoom, HashSet<string> report)
        {
            CheckKeys(input, GuestMetadata.Concat(new[]
            {
                "id", "name", "pronouns", "isPlayer", "status", "stats", "traits", "isHoH", "isPovHolder", "isNominated",
                "competitionsWon", "nominations", "timesVetoed", "mentalState", "mood", "stressLevel", "currentGoals",
                "internalThoughts", "lastReflectionWeek", "memoryStream", "currentSummary", "cognitiveGoal", "longTermGoal",
                "activeLies", "publicStatements"
            }), "contestant");
            Require(Text(input["status"], "contestant status") == "Active", "Only an all-active social boundary can be imported.");
            Require(input["isPlayer"]?.Type == JTokenType.Boolean, "Every contestant must have an explicit isPlayer flag.");
            foreach (var slot in new[] { "isHoH", "isPovHolder", "isNominated" })
                Require(input[slot] == null || input[slot].Type == JTokenType.Boolean && !(bool)input[slot],
                    "Contestant " + slot + " must be false at the supported social boundary.");
            Require(input["timesVetoed"] == null || Integer(input["timesVetoed"], "timesVetoed") == 0,
                "Nonzero timesVetoed history cannot be discarded.");
            foreach (var slot in new[] { "currentGoals", "internalThoughts",
                "memoryStream", "currentSummary", "cognitiveGoal", "longTermGoal", "activeLies", "publicStatements" })
                Require(Empty(input[slot]), "Contestant " + slot + " state is not supported.");
            Require(input["mood"] == null || (string)input["mood"] == "Neutral", "Non-neutral contestant mood is not represented.");
            Require(input["stressLevel"] == null || (string)input["stressLevel"] == "Normal", "Contestant stress state is not represented.");
            Require(DeepEmpty(input["mentalState"]), "Nonempty contestant mental state is not supported.");
            if (input["lastReflectionWeek"] != null)
            {
                Require(Integer(input["lastReflectionWeek"], "lastReflectionWeek") >= 0, "Reflection week is invalid.");
                report.Add("contestant.lastReflectionWeek");
            }
            foreach (var metadata in GuestMetadata)
                if (input[metadata] != null && !Empty(input[metadata])) report.Add("contestant." + metadata);
            Require(input["stats"] is JObject, "Complete contestant stats are required.");
            var stats = (JObject)input["stats"];
            CheckKeys(stats, StatNames, "contestant stats");
            foreach (var stat in StatNames) Number(stats[stat], "stat " + stat);
            Require(input["traits"] is JArray, "Contestant traits must be an array.");
            var result = new ContestantState
            {
                id = Text(input["id"], "contestant ID"), name = Text(input["name"], "contestant name"),
                pronouns = input["pronouns"] == null ? "they/them" : Text(input["pronouns"], "pronouns"),
                isPlayer = (bool)input["isPlayer"], status = ContestantStatus.Active, homeRoom = homeRoom,
                motive = "Explore the house and decide who to trust.",
                traits = ((JArray)input["traits"]).Select(trait => Text(trait, "trait")).ToList(),
                stats = new ContestantStats
                {
                    physical = (double)stats["physical"], mental = (double)stats["mental"], endurance = (double)stats["endurance"],
                    social = (double)stats["social"], luck = (double)stats["luck"], competition = (double)stats["competition"],
                    strategic = (double)stats["strategic"], loyalty = (double)stats["loyalty"]
                }
            };
            if (input["competitionsWon"] != null)
            {
                Require(input["competitionsWon"] is JObject, "Competition history is invalid.");
                var history = (JObject)input["competitionsWon"];
                CheckKeys(history, new[] { "hoh", "pov", "other" }, "competition history");
                Require(history["other"] == null || Integer(history["other"], "other competition wins") == 0,
                    "Other-competition wins are not represented and cannot be discarded.");
                result.hohWins = Integer(history["hoh"], "HoH wins");
                result.vetoWins = Integer(history["pov"], "veto wins");
            }
            if (input["nominations"] != null)
            {
                Require(input["nominations"] is JObject, "Nomination history is invalid.");
                var history = (JObject)input["nominations"];
                CheckKeys(history, new[] { "times", "receivedOn" }, "nomination history");
                result.timesNominated = Integer(history["times"], "nomination count");
                Require(history["receivedOn"] is JArray, "Nomination weeks must be an array.");
                result.nominationWeeks = ((JArray)history["receivedOn"]).Select(value => Integer(value, "nomination week")).ToList();
            }
            return result;
        }

        private static void CheckKeys(JObject value, IEnumerable<string> allowed, string field)
        {
            var keys = new HashSet<string>(allowed, StringComparer.Ordinal);
            var unsupported = value.Properties().FirstOrDefault(property => !keys.Contains(property.Name));
            Require(unsupported == null, "Unsupported " + field + " field: " + unsupported?.Name + ". It cannot be silently discarded.");
        }

        private static bool Empty(JToken value) => value == null || value.Type == JTokenType.Null
            || value is JArray array && array.Count == 0 || value is JObject obj && !obj.HasValues
            || value.Type == JTokenType.Boolean && !(bool)value
            || value.Type == JTokenType.Integer && (long)value == 0
            || value.Type == JTokenType.Float && (double)value == 0
            || value.Type == JTokenType.String && (string)value == "";
        private static void RequireDefaultStateSlot(JToken value, string slot)
        {
            if (value == null) return;
            bool supported;
            if (new[] { "hohWinner", "povWinner", "winner", "runnerUp" }.Contains(slot))
                supported = value.Type == JTokenType.Null;
            else if (new[] { "isFinalStage", "isSpectatorMode", "evictionCompletedThisWeek" }.Contains(slot))
                supported = value.Type == JTokenType.Boolean && !(bool)value;
            else if (new[] { "outOfPhaseSocialActionsUsed", "playerStudyBonus", "phaseEventSocialBonus", "phaseEventCompBonus" }.Contains(slot))
                supported = (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) && Number(value, slot) == 0;
            else if (new[] { "nominees", "povPlayers", "juryMembers", "finalTwo", "alliances", "allianceData", "gameLog", "promises", "deals",
                "pendingNPCProposals", "houseEvents", "relationshipArcs", "loyaltyOaths", "activeStorylines", "activeModifiers", "npcMemories",
                "pendingAllianceInvitations", "shownMilestones", "completedStorylineIds" }.Contains(slot))
                supported = value is JArray array && array.Count == 0;
            else if (new[] { "evictionVotes", "evictionVoteDecisions" }.Contains(slot))
                supported = value is JObject obj && !obj.HasValues;
            else supported = Empty(value);
            Require(supported, "Unsupported or malformed " + slot + " gameplay cannot be discarded.");
        }
        private static bool DeepEmpty(JToken value) => Empty(value) || value is JObject obj && obj.Properties().All(property => DeepEmpty(property.Value));
        private static string Text(JToken value, string field)
        {
            Require(value?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value), field + " must be a nonempty string.");
            return (string)value;
        }
        private static double Number(JToken value, string field)
        {
            Require(value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float), field + " must be numeric.");
            var number = (double)value;
            Require(!double.IsNaN(number) && !double.IsInfinity(number), field + " must be finite.");
            return number;
        }
        private static int Integer(JToken value, string field)
        {
            Require(value?.Type == JTokenType.Integer, field + " must be an integer.");
            return checked((int)value);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
