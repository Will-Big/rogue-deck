namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionActionData
    {
        public InterventionActionKey Key { get; }
        public int Cost { get; }
        public int Amount { get; }

        public InterventionActionData(InterventionActionKey key, int cost, int amount)
        {
            Key = key;
            Cost = cost;
            Amount = amount;
        }
    }
}
