using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>카드 콘텐츠와 세이브가 공유하는 단 하나의 직렬화 설정. 열거형은 이름으로, 키는
    /// camelCase로, 기본값인 멤버는 생략한다(생략된 값은 읽을 때 기본값으로 복원되므로 왕복이
    /// 안전하고, 파일이 사람 눈에 읽힌다 — 설계 §4.5의 diff 목표).</summary>
    public static class ContentJson
    {
        public static JsonSerializerSettings Settings => Build(includePolymorphic: true);

        /// <summary>다형 컨버터가 빠진 설정. EffectSpecJsonConverter가 자기 자신을 재귀 호출하지
        /// 않고 대상 객체의 평범한 필드만 쓰기 위해 쓴다. 외부에서 직접 쓰지 않는다.</summary>
        internal static JsonSerializer Plain { get; } =
            JsonSerializer.Create(Build(includePolymorphic: false));

        /// <summary>카드 컨버터만 뺀 설정. CardSpecJsonConverter가 중첩된 EffectSpec을 다형으로
        /// 다루면서도 자기 자신을 재귀 호출하지 않기 위해 쓴다. 외부에서 직접 쓰지 않는다.</summary>
        internal static JsonSerializer Nested { get; } =
            JsonSerializer.Create(Build(includePolymorphic: true, includeCardSpec: false));

        public static string Write(object value)
            => JsonConvert.SerializeObject(value, Settings);

        public static T Read<T>(string json)
            => JsonConvert.DeserializeObject<T>(json, Settings);

        private static JsonSerializerSettings Build(
            bool includePolymorphic, bool includeCardSpec = true)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DefaultValueHandling = DefaultValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error
            };
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new StatusKeyRefJsonConverter());
            if (includePolymorphic)
            {
                if (includeCardSpec)
                {
                    settings.Converters.Add(new CardSpecJsonConverter());
                }

                settings.Converters.Add(new EffectSpecJsonConverter());
                settings.Converters.Add(new StatusSpecJsonConverter());
                settings.Converters.Add(new InterventionSpecJsonConverter());
            }

            return settings;
        }
    }
}
