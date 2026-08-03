namespace FateWeaver.Core.Authoring.Decks
{
    /// <summary>저작된 덱 하나. 카드 규칙은 담지 않고 Content/Cards의 id를 가리키기만 한다
    /// (설계 §4.5: 카드 규칙의 유일한 원본은 카드 JSON이다). 같은 id가 여러 번 올 수 있다 —
    /// 덱은 장수를 갖는다.</summary>
    public sealed class DeckSpec
    {
        public string Id;
        public string[] Cards;
    }
}
