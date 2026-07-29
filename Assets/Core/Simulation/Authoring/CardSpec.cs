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
        public CardCategory Category;
        public int EnergyCost;
        public int BaseExecutionOrder;
        public EffectSpec[] Effects;
        public InterventionKeyRef Intervention;
        public int InterventionEffectValue;
        public InterventionTargetSideRef InterventionTargetSide;
        public bool InterventionRequireAdjacent;
    }

    /// <summary>개입 대상 진영 제한. Any=제한 없음, Player=재촉류, Enemy=유예류.</summary>
    public enum InterventionTargetSideRef { Any, Player, Enemy }
}
