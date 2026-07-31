using System;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>StatusKeyRef를 {"id":"block"}이 아니라 "block"으로 쓴다. 저작자가 보는 파일에서
    /// 상태 참조가 한 겹 덜 중첩된다.</summary>
    public sealed class StatusKeyRefJsonConverter : JsonConverter<StatusKeyRef>
    {
        public override StatusKeyRef ReadJson(
            JsonReader reader, Type objectType, StatusKeyRef existingValue,
            bool hasExistingValue, JsonSerializer serializer)
            => new StatusKeyRef { Id = (string)reader.Value };

        public override void WriteJson(JsonWriter writer, StatusKeyRef value, JsonSerializer serializer)
            => writer.WriteValue(value.Id);
    }

    /// <summary>InterventionKeyRef도 같은 이유로 평범한 문자열로 쓴다.</summary>
    public sealed class InterventionKeyRefJsonConverter : JsonConverter<InterventionKeyRef>
    {
        public override InterventionKeyRef ReadJson(
            JsonReader reader, Type objectType, InterventionKeyRef existingValue,
            bool hasExistingValue, JsonSerializer serializer)
            => new InterventionKeyRef { Id = (string)reader.Value };

        public override void WriteJson(
            JsonWriter writer, InterventionKeyRef value, JsonSerializer serializer)
            => writer.WriteValue(value.Id);
    }
}
