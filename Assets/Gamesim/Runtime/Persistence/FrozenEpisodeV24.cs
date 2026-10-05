using System.Globalization;
using System.IO;
using Gamesim.Simulation;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// The actual schema-24 contract: schema 23, including economy, pitch and speech vocabulary,
    /// plus a disabled, empty unified-commitments authority. Future hearing fields are NOT accepted.
    /// The validation-only projections are private; no projection can become an installed save.
    /// Exact older shapes remain owned by their frozen chain. Whole unchanged semantics/storage
    /// are checked by the copied former-24 validators, not the growing current validator or DTO
    /// shape. Validation does not run an engine, draw RNG, write IDs or repair history.
    /// </summary>
    internal static class FrozenEpisodeV24
    {
        internal static void Validate(JObject original)
        {
            if (original == null || !Integer(original["schemaVersion"], 24))
                throw new InvalidDataException("Expected simulation schema version 24.");
            if (!Integer(original["unifiedCommitmentRulesVersion"], 0)
                || !(original["unifiedCommitments"] is JArray rows) || rows.Count != 0)
                throw new InvalidDataException("Historical schema 24 requires disabled, empty unified commitments.");

            var previous = (JObject)original.DeepClone();
            previous.Remove("unifiedCommitmentRulesVersion");
            previous.Remove("unifiedCommitments");
            previous["schemaVersion"] = 23;
            FrozenEpisodeV23.Validate(previous);

            // The complete frozen ancestry already rejected future/missing fields. EpisodeState
            // is a detached carrier only: no current shape, schema header or validation is used.
            FrozenV24Validator.Validate(original.ToObject<EpisodeState>(SaveJson.Serializer()));
        }

        private static bool Integer(JToken token, int expected) => token?.Type == JTokenType.Integer
            && long.TryParse(token.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
            && value == expected;
    }
}
