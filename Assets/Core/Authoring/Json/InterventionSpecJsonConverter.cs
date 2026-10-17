using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>InterventionSpec의 다형 (역)직렬화. 판별자는 스펙이 이미 갖고 있는
    /// InterventionActionKey.Id이고 타입 표는 InterventionSpecCatalog에서 만든다 — 리플렉션 스캔
    /// 없음(AGENTS.md 규칙 9). EffectSpecJsonConverter와 같은 형태다. JSON 이름은 key다(효과 JSON 설계 P2).</summary>
    public sealed class InterventionSpecJsonConverter : JsonConverter<InterventionSpec>
    {
        public const string KeyProperty = "key";

        private static readonly Dictionary<string, Func<InterventionSpec>> FactoryByKey = BuildFactories();
        private static readonly Dictionary<Type, string> KeyByType = BuildKeys();

        public override InterventionSpec ReadJson(
            JsonReader reader, Type objectType, InterventionSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var key = (string)entry[KeyProperty];
            if (string.IsNullOrEmpty(key))
            {
                throw new JsonSerializationException(
                    "Intervention entry requires a '" + KeyProperty + "' property.");
            }

            if (!FactoryByKey.TryGetValue(key, out var create))
            {
                throw new JsonSerializationException("Unknown intervention key '" + key + "'.");
            }

            entry.Remove(KeyProperty);
            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Plain.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, InterventionSpec value, JsonSerializer serializer)
        {
            if (!KeyByType.TryGetValue(value.GetType(), out var key))
            {
                throw new JsonSerializationException(
                    "Intervention spec type '" + value.GetType().Name
                    + "' is not registered in InterventionSpecCatalog.");
            }

            var entry = JObject.FromObject(value, ContentJson.Plain);
            entry.AddFirst(new JProperty(KeyProperty, key));
            entry.WriteTo(writer);
        }

        private static Dictionary<string, Func<InterventionSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<InterventionSpec>>();
            foreach (var info in InterventionSpecCatalog.All())
            {
                var key = info.Create().Key.Id;
                if (table.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "Duplicate intervention spec key '" + key + "' in InterventionSpecCatalog.");
                }

                table.Add(key, info.Create);
            }

            return table;
        }

        private static Dictionary<Type, string> BuildKeys()
        {
            var table = new Dictionary<Type, string>();
            foreach (var info in InterventionSpecCatalog.All())
            {
                table[info.SpecType] = info.Create().Key.Id;
            }

            return table;
        }
    }
}
