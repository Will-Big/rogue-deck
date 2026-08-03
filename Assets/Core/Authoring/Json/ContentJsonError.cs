using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>Newtonsoft의 예외에서 줄·열을 꺼내 저작자가 고칠 수 있는 문장으로 만든다.
    /// 콘텐츠 로더가 넷(카드·덱·풀·캐릭터)이라 문장 형태를 한곳에 둔다.</summary>
    public static class ContentJsonError
    {
        public static string Describe(JsonException exception)
        {
            if (exception is JsonReaderException reader)
            {
                return exception.Message + " (line " + reader.LineNumber
                    + ", position " + reader.LinePosition + ")";
            }

            if (exception is JsonSerializationException serialization
                && serialization.LineNumber > 0)
            {
                return exception.Message + " (line " + serialization.LineNumber
                    + ", position " + serialization.LinePosition + ")";
            }

            return exception.Message;
        }
    }
}
