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
        {
            if (reader.TokenType != JsonToken.String)
            {
                throw new JsonSerializationException(
                    "Status key must be a string, got " + reader.TokenType + ".");
            }

            return new StatusKeyRef { Id = (string)reader.Value };
        }

        public override void WriteJson(JsonWriter writer, StatusKeyRef value, JsonSerializer serializer)
            => writer.WriteValue(value.Id);
    }
}
