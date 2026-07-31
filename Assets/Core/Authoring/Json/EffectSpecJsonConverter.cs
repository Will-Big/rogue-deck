using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>EffectSpec의 다형 (역)직렬화. 판별자는 스펙이 이미 갖고 있는 EffectKey.Id이고
    /// 타입 표는 EffectSpecCatalog에서 만든다 — 리플렉션 스캔 없음(AGENTS.md 규칙 9).</summary>
    public sealed class EffectSpecJsonConverter : JsonConverter<EffectSpec>
    {
        public const string KindProperty = "kind";

        private static readonly Dictionary<string, Func<EffectSpec>> FactoryByKind = BuildFactories();
        private static readonly Dictionary<Type, string> KindByType = BuildKinds();

        public override EffectSpec ReadJson(
            JsonReader reader, Type objectType, EffectSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var kind = (string)entry[KindProperty];
            if (string.IsNullOrEmpty(kind))
            {
                throw new JsonSerializationException(
                    "Effect entry requires a '" + KindProperty + "' property.");
            }

            if (!FactoryByKind.TryGetValue(kind, out var create))
            {
                throw new JsonSerializationException("Unknown effect kind '" + kind + "'.");
            }

            entry.Remove(KindProperty);
            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Plain.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, EffectSpec value, JsonSerializer serializer)
        {
            if (!KindByType.TryGetValue(value.GetType(), out var kind))
            {
                throw new JsonSerializationException(
                    "Effect spec type '" + value.GetType().Name
                    + "' is not registered in EffectSpecCatalog.");
            }

            var entry = JObject.FromObject(value, ContentJson.Plain);
            entry.AddFirst(new JProperty(KindProperty, kind));
            entry.WriteTo(writer);
        }

        private static Dictionary<string, Func<EffectSpec>> BuildFactories()
        {
            var table = new Dictionary<string, Func<EffectSpec>>();
            foreach (var info in EffectSpecCatalog.All())
            {
                var kind = info.Create().Key.Id;
                if (table.ContainsKey(kind))
                {
                    throw new InvalidOperationException(
                        "Duplicate effect spec kind '" + kind + "' in EffectSpecCatalog.");
                }

                table.Add(kind, info.Create);
            }

            return table;
        }

        private static Dictionary<Type, string> BuildKinds()
        {
            var table = new Dictionary<Type, string>();
            foreach (var info in EffectSpecCatalog.All())
            {
                table.Add(info.SpecType, info.Create().Key.Id);
            }

            return table;
        }
    }
}
