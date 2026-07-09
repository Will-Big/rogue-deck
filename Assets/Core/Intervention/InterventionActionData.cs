namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionActionData
    {
        public InterventionActionKey Key { get; }
        public int InterventionCost { get; }
        public int EffectValue { get; }

        public InterventionActionData(InterventionActionKey key, int interventionCost, int effectValue)
        {
            Key = key;
            InterventionCost = interventionCost;
            EffectValue = effectValue;
        }
    }
}
