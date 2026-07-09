using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Flat, pure card data (the single source the headless sims read). Built from a CardAsset SO
    /// at edit time (code generation) and converted to a core CardDefinition by CardSpecMapper.</summary>
    public sealed class CardSpec
    {
        public string Id;
        public string Name;
        public Side Side;
        public CardType Type;
        public CardCategory Category;
        public int EnergyCost;
        public int BaseExecutionOrder;
        public EffectSpec[] Effects;
        public InterventionKind Intervention;
        public int InterventionEffectValue;
    }
}
