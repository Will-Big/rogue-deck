using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>InterventionSpec의 다형 (역)직렬화. 판별자는 스펙이 이미 갖고 있는
    /// InterventionActionKey.Id이고 타입 표는 InterventionSpecCatalog에서 만든다 — 리플렉션 스캔
    /// 없음(AGENTS.md 규칙 9). EffectSpecJsonConverter와 같은 형태다.</summary>
    public sealed class InterventionSpecJsonConverter : JsonConverter<InterventionSpec>
    {
        public const string KindProperty = "kind";

        private static readonly Dictionary<string, Func<InterventionSpec>> FactoryByKind = BuildFactories();
        private static readonly Dictionary<Type, string> KindByType = BuildKinds();

        public override InterventionSpec ReadJson(
            JsonReader reader, Type objectType, InterventionSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var kind = (string)entry[KindProperty];
            if (string.IsNullOrEmpty(kind))
            {
                throw new JsonSerializationException(
                    "Intervention entry requires a '" + KindProperty + "' property.");
            }

            if (!FactoryByKind.TryGetValue(kind, out var create))
            {
                throw new JsonSerializationException("Unknown intervention kind '" + kind + "'.");
            }

            entry.Remove(KindProperty);
            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Plain.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, InterventionSpec value, JsonSerializer serializer)
        {
            if (!KindByType.TryGetValue(value.GetType(), out var kind))
            {
                throw new JsonSerializationException(
                    "Intervention spec type '" + value.GetType().Name
                    + "' is not registered in InterventionSpecCatalog.");
            }

            var entry = JObject.FromObject(value, ContentJson.Plain);
            entry.AddFirst(new JProperty(KindProperty, kind));
            entry.WriteTo(writer);
        }

        private static Dictionary<string, Func<InterventionSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<InterventionSpec>>();
            foreach (var info in InterventionSpecCatalog.All())
            {
                var kind = info.Create().Key.Id;
                if (table.ContainsKey(kind))
                {
                    throw new InvalidOperationException(
                        "Duplicate intervention spec kind '" + kind + "' in InterventionSpecCatalog.");
                }

                table.Add(kind, info.Create);
            }

            return table;
        }

        private static Dictionary<Type, string> BuildKinds()
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
