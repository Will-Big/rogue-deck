using System;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Json
{
    public sealed class EffectSpecJsonConverter : JsonConverter<EffectSpec>
    {
        public override EffectSpec ReadJson(
            JsonReader reader, Type objectType, EffectSpec existingValue,
            bool hasExistingValue, JsonSerializer serializer)
            => throw new NotImplementedException();

        public override void WriteJson(JsonWriter writer, EffectSpec value, JsonSerializer serializer)
            => throw new NotImplementedException();
    }
}
