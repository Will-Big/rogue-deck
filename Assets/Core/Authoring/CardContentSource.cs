namespace FateWeaver.Core.Authoring
{
    /// <summary>로더의 입력 한 단위. Name은 오류 메시지에만 쓰이며 보통 파일 이름이다.
    /// 로더가 파일을 직접 읽지 않으므로 헤드리스 테스트가 임시 파일 없이 검증할 수 있다.</summary>
    public sealed class CardContentSource
    {
        public CardContentSource(string name, string json)
        {
            Name = name;
            Json = json;
        }

        public string Name { get; }
        public string Json { get; }
    }
}
