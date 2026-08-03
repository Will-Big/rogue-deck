namespace FateWeaver.Core.Authoring.Characters
{
    /// <summary>저작된 캐릭터 하나. 시작 덱을 id로 가리킨다. 색 틴트는 이 게임의 아트이므로
    /// 표현 데이터이고 Unity의 CharacterAsset에 남는다(설계 §4.5: Unity는 표현만 담당).</summary>
    public sealed class CharacterSpec
    {
        public string Id;
        public string DisplayName;
        public string Deck;
    }
}
