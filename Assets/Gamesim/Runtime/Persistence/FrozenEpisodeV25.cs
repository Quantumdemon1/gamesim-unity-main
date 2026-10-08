// UNUSED frozen public schema25 contract pinned to f4566b69afc10c1008d64e112c12ed39241caa36.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Gamesim.Persistence.Frozen25Data;

namespace Gamesim.Persistence
{
    /// <summary>
    /// Complete literal schema25 shape and its committed public Safety/storage semantics.
    /// Mode0 stays empty; public mode1 requires active C0 and knowledge, with hearing0 or hearing1.
    /// No migration dispatcher selects this contract yet. It never normalizes or installs state.
    /// All semantic types, enums, vocabularies, bounds and reachable readers are local snapshots;
    /// only BCL and Newtonsoft infrastructure are external dependencies.
    /// </summary>
    internal static class FrozenEpisodeV25
    {
        internal static void Validate(JObject payload)
        {
            try
            {
                if (payload == null) throw new InvalidDataException("Historical schema25 payload is missing.");
                FrozenV25Shape.Validate(payload);
                var state = payload.ToObject<EpisodeState>(Serializer());
                FrozenV25Validator.Validate(state);
            }
            catch (Exception error) when (error is JsonException || error is OverflowException
                || error is ArgumentException || error is InvalidOperationException || error is FormatException)
            { throw new InvalidDataException("Historical schema25 exceeds its frozen contract.", error); }
        }

        private static readonly PublicFieldsResolver Fields = new PublicFieldsResolver();
        private static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64,
            ContractResolver = Fields
        });

        // Reflection is restricted to fixed local data-only snapshot types, never live simulation DTOs.
        private sealed class PublicFieldsResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) =>
                type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
        }
    }
}
