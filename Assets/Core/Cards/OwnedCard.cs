namespace FateWeaver.Core.Cards
{
    /// <summary>A card definition paired with its owning party member for one combat deck.
    /// A null owner means that the party owns the card as a whole.</summary>
    public sealed class OwnedCard
    {
        public CardDefinition Def { get; }
        public string OwnerId { get; }
        public bool IsPartyOwned => OwnerId == null;

        public OwnedCard(CardDefinition def, string ownerId)
        {
            Def = def;
            OwnerId = ownerId;
        }
    }
}
