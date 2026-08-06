using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>CardSpec의 다형 (역)직렬화. 판별자는 카드 분류이며 스펙의 실제 필드이기도 하므로,
    /// EffectSpecJsonConverter와 달리 읽기 전에 떼어내거나 쓰기 후에 되붙일 필요가 없다.
    /// CardContentLoader의 RequiredKeys가 "category"를 필수로 강제하므로 판별자는 항상 존재한다.</summary>
    public sealed class CardSpecJsonConverter : JsonConverter<CardSpec>
    {
        public const string CategoryProperty = "category";

        private static readonly Dictionary<string, Func<CardSpec>> FactoryByCategory =
            new Dictionary<string, Func<CardSpec>>(StringComparer.Ordinal)
            {
                { CardCategory.Execution.ToString(), () => new ExecutionCardSpec() },
                { CardCategory.Intervention.ToString(), () => new InterventionCardSpec() }
            };

        public override CardSpec ReadJson(
            JsonReader reader, Type objectType, CardSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            var entry = JObject.Load(reader);
            var category = (string)entry[CategoryProperty];
            if (string.IsNullOrEmpty(category))
            {
                throw new JsonSerializationException(
                    "Card entry requires a '" + CategoryProperty + "' property.");
            }

            if (!FactoryByCategory.TryGetValue(category, out var create))
            {
                throw new JsonSerializationException("Unknown card category '" + category + "'.");
            }

            var spec = create();
            using (var subReader = entry.CreateReader())
            {
                ContentJson.Nested.Populate(subReader, spec);
            }

            return spec;
        }

        public override void WriteJson(JsonWriter writer, CardSpec value, JsonSerializer serializer)
            => JObject.FromObject(value, ContentJson.Nested).WriteTo(writer);
    }
}
