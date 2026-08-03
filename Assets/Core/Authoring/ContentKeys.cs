using System;

namespace FateWeaver.Core.Authoring
{
    /// <summary>생략되면 조용히 기본값이 들어가서는 안 되는 키를 파싱 전에 확인한다. 콘텐츠
    /// 로더가 넷(카드·덱·풀·캐릭터)이라 확인 방식을 한곳에 둔다.</summary>
    public static class ContentKeys
    {
        /// <summary>required 중 JSON 본문에 없는 첫 키. 모두 있으면 null.</summary>
        public static string FirstMissing(string json, string[] required)
        {
            foreach (var key in required)
            {
                if (json.IndexOf("\"" + key + "\"", StringComparison.Ordinal) < 0)
                {
                    return key;
                }
            }

            return null;
        }
    }
}
