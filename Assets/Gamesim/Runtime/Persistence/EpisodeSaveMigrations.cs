using System;
using System.Collections.Generic;
using System.IO;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Call only AFTER verifying the envelope
    /// checksum against its original payload, then validate the returned current
    /// DTO shape and simulation invariants before installing any state.
    /// </summary>
    public static class EpisodeSaveMigrations
    {
        /// <summary>Call after original-envelope checksum verification; every historical step remains detached.</summary>
        public static JObject PrepareCurrentPayload(JObject original, out bool migrated)
        {
            migrated = false;
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("Simulation schema version must be an integer.");
            long version = (long)original["schemaVersion"];
            if (version == 6) return (JObject)original.DeepClone();
            if (version < 1 || version > 5) throw new InvalidDataException("Unsupported simulation schema version.");
            var v5 = version == 5 ? original : PrepareV5Payload(original, out _);
            var result = UpgradeV5ToV6(v5);
            migrated = true;
            return result;
        }

        /// <summary>Frozen v1-v4-to-v5 dispatch; do not retarget its historical defaults.</summary>
        public static JObject PrepareV5Payload(JObject original, out bool migrated)
        {
            migrated = false;
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("Simulation schema version must be an integer.");
            long version = (long)original["schemaVersion"];
            if (version == 5) return (JObject)original.DeepClone();
            if (version < 1 || version > 4) throw new InvalidDataException("Unsupported simulation schema version.");
            var v4 = version == 4 ? original : PrepareV4Payload(original, out _);
            var current = UpgradeV4ToV5(v4);
            migrated = true;
            return current;
        }

        /// <summary>Validate all original v5 data before adding an empty, future-activated subsystem.</summary>
        public static JObject UpgradeV5ToV6(JObject original)
        {
            FrozenEpisodeV5.Validate(original);
            int week = (int)original["week"];
            uint seed = (uint)original["seed"];
            var result = (JObject)original.DeepClone();
            // Explicit defaults, not a serialization of the evolving runtime DTO.
            result.Add("npcSocial", new JObject
            {
                ["rulesStartWeek"] = checked(week + 1),
                ["clockTick"] = 0L, ["nextScanTick"] = 3L, ["nextConversationSequence"] = 1L,
                ["randomState"] = NpcSocialState.InitialRandomState(seed),
                ["pending"] = new JArray(), ["cooldowns"] = new JArray(), ["pairMemory"] = new JArray()
            });
            result["schemaVersion"] = 6;
            return result;
        }

        /// <summary>Preserve the entire captured old week; never infer activation from ballots or phase.</summary>
        public static JObject UpgradeV4ToV5(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer
                || (long)original["schemaVersion"] != 4)
                throw new InvalidDataException("Expected simulation schema version 4.");
            SaveJson.CheckDtoShape(original, typeof(FrozenEpisodeV4.State), "state(v4)");
            RequireOldEnum(original["phase"], 15, "state(v4).phase");
            CheckEnumRows(original["contestants"], "status", 4, "state(v4).contestants");
            CheckEnumRows(original["promises"], "kind", 4, "state(v4).promises");
            CheckEnumRows(original["promises"], "status", 3, "state(v4).promises");
            CheckEnumRows(original["events"], "phase", 15, "state(v4).events");
            var historical = original.ToObject<FrozenEpisodeV4.State>(SaveJson.Serializer());
            FrozenEpisodeV4.ValidateScalarRanges(historical);
            int startWeek = checked(historical.week + 1);
            // Full unchanged semantic/storage guards run on a detached validation view only.
            // No Store/migration recursion, engine command, RNG sample, or source mutation occurs.
            var validation = original.ToObject<EpisodeState>(SaveJson.Serializer());
            validation.schemaVersion = 5;
            validation.blocRulesStartWeek = startWeek;
            FrozenV5Validator.Validate(validation);
            var result = (JObject)original.DeepClone();
            result.Add("blocRulesStartWeek", startWeek);
            result["schemaVersion"] = 5;
            return result;
        }

        /// <summary>Frozen v1–v3-to-v4 dispatch. Never retarget historical transformations.</summary>
        public static JObject PrepareV4Payload(JObject original, out bool migrated)
        {
            migrated = false;
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("Simulation schema version must be an integer.");
            long version = (long)original["schemaVersion"];
            if (version == 4) return (JObject)original.DeepClone();
            if (version < 1 || version > 3) throw new InvalidDataException("Unsupported simulation schema version.");
            var v3 = version == 3 ? original : PrepareV3Payload(original, out _);
            var current = UpgradeV3ToV4(v3);
            migrated = true;
            return current;
        }

        /// <summary>Validate the complete frozen v3 shape before adding the one reviewed v4 default.</summary>
        public static JObject UpgradeV3ToV4(JObject original)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer
                || (long)original["schemaVersion"] != 3)
                throw new InvalidDataException("Expected simulation schema version 3.");
            SaveJson.CheckDtoShape(original, typeof(FrozenEpisodeV3.State), "state(v3)");
            RequireOldEnum(original["phase"], 15, "state(v3).phase");
            CheckEnumRows(original["contestants"], "status", 4, "state(v3).contestants");
            CheckEnumRows(original["promises"], "kind", 4, "state(v3).promises");
            CheckEnumRows(original["promises"], "status", 3, "state(v3).promises");
            CheckEnumRows(original["events"], "phase", 15, "state(v3).events");
            // V4 permits a higher simulated score, but must not retroactively accept a
            // checksum-valid v1/v2/v3 save that violated its original scalar contract.
            if (original["competitionScores"] is not JArray oldScores)
                throw new InvalidDataException("state(v3).competitionScores must be an array.");
            foreach (var value in oldScores)
            {
                if (value is not JObject row)
                    throw new InvalidDataException("state(v3).competitionScores contains a null record.");
                double score = (double)row["score"];
                if (double.IsNaN(score) || double.IsInfinity(score) || score < 0 || score > 1000)
                    throw new InvalidDataException("state(v3).competitionScores must preserve the historical 0..1000 score bound.");
            }
            var result = (JObject)original.DeepClone();
            result.Add("playerStudyBonus", 0);
            result["schemaVersion"] = 4;
            return result;
        }

        /// <summary>Frozen v1/v2-to-v3 dispatch, retained independently of the current schema.</summary>
        public static JObject PrepareV3Payload(JObject original, out bool migrated)
        {
            migrated = false;
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("Simulation schema version must be an integer.");
            var version = (long)original["schemaVersion"];
            if (version == 3) return (JObject)original.DeepClone();
            if (version != 1 && version != 2) throw new InvalidDataException("Unsupported simulation schema version.");
            // Keep the pinned v1 -> v2 transformation unchanged; both paths then
            // pass the same recursive frozen v2 contract before adding v3 data.
            var v2 = version == 1 ? PrepareV2Payload(original, out _) : original;
            var result = UpgradeV2ToV3(v2, new JObject
            {
                ["playerPersona"] = new JObject
                {
                    ["current"] = "Neutral",
                    ["scores"] = new JArray(
                        PersonaDefault("Neutral"), PersonaDefault("Remorseful"), PersonaDefault("Ruthless"),
                        PersonaDefault("Calculated"), PersonaDefault("Social Butterfly")),
                    ["history"] = new JArray()
                },
                ["jurySentiment"] = new JObject { ["jurors"] = new JArray(), ["overallSentiment"] = 0 },
                ["pendingDiary"] = null,
                ["lastDiaryRoomWeek"] = 0,
                ["phaseEventSocialBonus"] = 0,
                ["phaseEventCompBonus"] = 0,
                ["resolvedDiaryIds"] = new JArray(),
                ["loyaltyOaths"] = new JArray(),
                ["oathOpportunities"] = new JArray(),
                ["shownOathMilestones"] = new JArray()
            });
            migrated = true;
            return result;
        }

        private static JObject PersonaDefault(string name) => new JObject { ["persona"] = name, ["score"] = 0 };

        /// <summary>Detached v2 step; historical nested shapes never follow the evolving runtime DTO.</summary>
        public static JObject UpgradeV2ToV3(JObject original, JObject newRootFields)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer
                || (long)original["schemaVersion"] != 2)
                throw new InvalidDataException("Expected simulation schema version 2.");
            if (newRootFields == null) throw new ArgumentNullException(nameof(newRootFields));
            SaveJson.CheckDtoShape(original, typeof(FrozenEpisodeV2.State), "state(v2)");
            RequireOldEnum(original["phase"], 15, "state(v2).phase");
            CheckEnumRows(original["contestants"], "status", 4, "state(v2).contestants");
            CheckEnumRows(original["promises"], "kind", 4, "state(v2).promises");
            CheckEnumRows(original["promises"], "status", 3, "state(v2).promises");
            CheckEnumRows(original["events"], "phase", 15, "state(v2).events");
            foreach (var property in newRootFields.Properties())
                if (original.Property(property.Name) != null)
                    throw new InvalidDataException("Migration defaults may not overwrite existing field: " + property.Name);
            var result = (JObject)original.DeepClone();
            foreach (var property in newRootFields.Properties()) result.Add(property.Name, property.Value.DeepClone());
            result["schemaVersion"] = 3;
            return result;
        }

        /// <summary>Version dispatch for the currently agreed v2 additions.</summary>
        public static JObject PrepareV2Payload(JObject original, out bool migrated)
        {
            migrated = false;
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("Simulation schema version must be an integer.");
            var version = (long)original["schemaVersion"];
            if (version == 2) return (JObject)original.DeepClone();
            if (version != 1) throw new InvalidDataException("Unsupported simulation schema version.");
            var result = UpgradeV1ToV2(original, new JObject
            {
                ["juryExchanges"] = new JArray(),
                ["juryQuestionIndex"] = 0,
                ["finalSpeeches"] = new JArray(),
                ["relationshipArcs"] = new JArray()
            });
            foreach (JObject actor in (JArray)result["contestants"])
            {
                // V1 has no emotional fields. Neutral/Normal are explicit unknown
                // history defaults, not a replay of earlier nomination effects.
                actor.Add("mood", "Neutral");
                actor.Add("stressLevel", "Normal");
            }
            if (result["relationships"] is not JArray relationships)
                throw new InvalidDataException("relationships must be an array.");
            foreach (var value in relationships)
            {
                if (value is not JObject edge) throw new InvalidDataException("relationships contains a null record.");
                edge.Add("notes", new JArray());
                edge.Add("events", new JArray());
                edge.Add("lastInteractionWeek", 0);
            }
            migrated = true;
            return result;
        }

        /// <summary>
        /// Performs a detached, data-only v1-to-v2 step. The caller supplies the
        /// explicitly reviewed defaults for NEW root fields, not a new DTO whose
        /// constructor may silently change future migration behavior. This step
        /// preserves old phase values, including Jury=12 and Finished=13.
        /// </summary>
        public static JObject UpgradeV1ToV2(JObject original, JObject newRootFields)
        {
            if (original == null || original["schemaVersion"]?.Type != JTokenType.Integer
                || (long)original["schemaVersion"] != 1)
                throw new InvalidDataException("Expected simulation schema version 1.");
            if (newRootFields == null) throw new ArgumentNullException(nameof(newRootFields));

            // All nested shapes are frozen. Do not substitute typeof(EpisodeState):
            // adding a new field there must not retroactively redefine a v1 save.
            SaveJson.CheckDtoShape(original, typeof(StateV1), "state(v1)");
            RequireOldEnum(original["phase"], 13, "state.phase");
            CheckEnumRows(original["contestants"], "status", 4, "contestants");
            CheckEnumRows(original["promises"], "kind", 4, "promises");
            CheckEnumRows(original["promises"], "status", 3, "promises");
            CheckEnumRows(original["events"], "phase", 13, "events");
            foreach (var property in newRootFields.Properties())
            {
                if (original.Property(property.Name) != null)
                    throw new InvalidDataException("Migration defaults may not overwrite existing field: " + property.Name);
            }

            var result = (JObject)original.DeepClone();
            foreach (var property in newRootFields.Properties())
                result.Add(property.Name, property.Value.DeepClone());
            result["schemaVersion"] = 2;
            return result;
        }

        private static void CheckEnumRows(JToken rows, string field, int max, string path)
        {
            // A null collection is a recognized DTO shape, but never valid state.
            if (rows is not JArray array) throw new InvalidDataException(path + " must be an array.");
            for (var index = 0; index < array.Count; index++)
            {
                if (array[index] is not JObject row) throw new InvalidDataException(path + " contains a null record.");
                RequireOldEnum(row[field], max, path + "[" + index + "]." + field);
            }
        }

        private static void RequireOldEnum(JToken token, int max, string path)
        {
            if (token?.Type != JTokenType.Integer || (long)token < 0 || (long)token > max)
                throw new InvalidDataException(path + " is not a supported v1 enum value.");
        }

        // Persistence field contracts, frozen from the pinned native schema 1.
        // No Unity/runtime DTO references: future nested additions stay separate.
#pragma warning disable 0649
        private sealed class StatsV1
        {
            public double physical, mental, endurance, social, luck, competition, strategic, loyalty;
        }

        private sealed class ContestantV1
        {
            public string id, name, pronouns, motive, homeRoom;
            public bool isPlayer;
            public int status;
            public StatsV1 stats;
            public List<string> traits;
            public int hohWins, vetoWins, timesNominated;
            public List<int> nominationWeeks;
        }

        private sealed class RelationshipV1 { public string fromId, toId; public double score; }
        private sealed class PromiseV1
        {
            public string id, fromId, toId, targetId;
            public int kind, status, week, expiresWeek;
            public string impact;
        }
        private sealed class AllianceV1 { public string id, name; public List<string> members; public bool active; }
        private sealed class MemoryV1 { public string ownerId, subjectId, text; public int week; public bool isPrivate; }
        private sealed class VoteV1 { public string voterId, targetId, reason; }
        private sealed class CompetitionScoreV1 { public string contestantId; public double score; }
        private sealed class EventV1
        {
            public int sequence, week, phase;
            public string kind, text;
            public List<string> audienceIds;
        }

        private sealed class StateV1
        {
            public int schemaVersion;
            public string sessionId;
            public uint seed, randomState;
            public int revision, week, nextSequence, socialActions, phase;
            public string playerId, hohId, previousHohId, vetoHolderId, winnerId, runnerUpId;
            public string finalPart1WinnerId, finalPart2WinnerId;
            public bool competitionResolved, vetoResolved, evictionResolved;
            public List<ContestantV1> contestants;
            public List<RelationshipV1> relationships;
            public List<PromiseV1> promises;
            public List<AllianceV1> alliances;
            public List<MemoryV1> memories;
            public List<string> nominees, vetoPlayers;
            public List<VoteV1> votes;
            public List<CompetitionScoreV1> competitionScores;
            public List<EventV1> events;
            public List<string> acceptedCommandIds;
        }
#pragma warning restore 0649
    }
}
