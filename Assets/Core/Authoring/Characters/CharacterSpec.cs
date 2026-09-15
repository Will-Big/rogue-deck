namespace FateWeaver.Core.Authoring.Characters
{
    /// <summary>저작된 캐릭터 하나. 시작 덱을 id로 가리킨다. 색 틴트는 이 게임의 아트이므로
    /// 표현 데이터이고 Unity의 CharacterAsset에 남는다(설계 §4.5: Unity는 표현만 담당).</summary>
    public sealed class CharacterSpec
    {
        public string Id;
        public string DisplayName;
        public string Deck;

        /// <summary>이 캐릭터가 소유한 카드풀 id. 전투 보상 후보가 여기서 나온다(전투 노드 설계 결정 2·3).</summary>
        public string Pool;
    }
}
