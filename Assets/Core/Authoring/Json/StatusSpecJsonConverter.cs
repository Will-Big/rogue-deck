using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Status;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>StatusSpec의 다형 (역)직렬화. 판별자는 상태 키 자체다 — EffectSpec이 EffectKey.Id를
    /// 쓰는 것과 같은 형태이며, 여러 상태가 같은 스펙 타입을 공유하므로 타입이 아니라 키로 가른다.</summary>
    public sealed class StatusSpecJsonConverter : JsonConverter<StatusSpec>
    {
        public const string KeyProperty = "key";

        private static readonly Dictionary<string, Func<StatusSpec>> FactoryByKey =
            BuildFactories(CombatRegistries.Statuses());

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

        /// <summary>판별자의 원본은 행동 레지스트리다. 등록된 상태만 저작될 수 있고, 스펙 타입은
        /// 행동이 답한다 — 코드에 값 목록을 두지 않고도 다형 역직렬화가 성립한다.</summary>
        internal static Dictionary<string, Func<StatusSpec>> BuildFactories(StatusRegistry behaviors)
        {
            var table = new Dictionary<string, Func<StatusSpec>>();
            foreach (var key in behaviors.RegisteredKeys)
            {
                var behavior = behaviors.Resolve(key);
                var keyRef = StatusKeyRef.Of(key);
                table.Add(key.Id, () =>
                {
                    var created = behavior.NewSpec();
                    created.Key = keyRef;
                    return created;
                });
            }

            return table;
        }
    }
}
