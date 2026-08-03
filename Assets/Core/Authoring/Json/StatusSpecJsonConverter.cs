using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Statuses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>StatusSpec의 다형 (역)직렬화. 판별자는 상태 키 자체다 — EffectSpec이 EffectKey.Id를
    /// 쓰는 것과 같은 형태이며, 여러 상태가 같은 스펙 타입을 공유하므로 타입이 아니라 키로 가른다.</summary>
    public sealed class StatusSpecJsonConverter : JsonConverter<StatusSpec>
    {
        public const string KeyProperty = "key";

        private static readonly Dictionary<string, Func<StatusSpec>> FactoryByKey = BuildFactories();

        public override StatusSpec ReadJson(
            JsonReader reader, Type objectType, StatusSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var key = (string)entry[KeyProperty];
            if (string.IsNullOrEmpty(key))
            {
                throw new JsonSerializationException(
                    "Status entry requires a '" + KeyProperty + "' property.");
            }

            if (!FactoryByKey.TryGetValue(key, out var create))
            {
                throw new JsonSerializationException("Unknown status key '" + key + "'.");
            }

            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Plain.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, StatusSpec value, JsonSerializer serializer)
            => JObject.FromObject(value, ContentJson.Plain).WriteTo(writer);

        private static Dictionary<string, Func<StatusSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<StatusSpec>>();
            foreach (var spec in StatusContentDefaults.Specs())
            {
                var key = spec.Key.Id;
                if (table.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "Duplicate status key '" + key + "' in StatusContentDefaults.");
                }

                var prototype = spec;
                table.Add(key, () =>
                {
                    var created = prototype.NewInstance();
                    created.Key = prototype.Key;
                    return created;
                });
            }

            return table;
        }
    }
}
