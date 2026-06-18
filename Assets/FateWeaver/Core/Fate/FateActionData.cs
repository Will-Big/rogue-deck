namespace FateWeaver.Core.Fate
{
    public sealed class FateActionData
    {
        public FateActionKey Key { get; }
        public int Cost { get; }
        public int Amount { get; }

        public FateActionData(FateActionKey key, int cost, int amount)
        {
            Key = key;
            Cost = cost;
            Amount = amount;
        }
    }
}
