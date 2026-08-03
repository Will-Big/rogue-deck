namespace FateWeaver.Core.Authoring.Decks
{
    /// <summary>저작된 카드 풀 하나. 덱과 같은 모양이지만 후보 집합이라 같은 id가 두 번 오는 것은
    /// 저작 실수다 — 그 판정은 PoolContentLoader가 한다. DeckSpec과 합치지 않는 이유가 그것이다.
    /// 합치면 "중복 허용" 플래그라는 쓰이지 않는 칸이 생긴다.</summary>
    public sealed class PoolSpec
    {
        public string Id;
        public string[] Cards;
    }
}
