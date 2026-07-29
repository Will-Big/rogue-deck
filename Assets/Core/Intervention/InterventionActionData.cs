using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionActionData
    {
        public InterventionActionKey Key { get; }
        public int InterventionCost { get; }
        public int EffectValue { get; }

        /// <summary>대상 레일 카드가 속해야 하는 진영 (null = 제한 없음). 재촉=Player, 유예=Enemy.</summary>
        public Side? TargetSide { get; }

        /// <summary>true면 두 대상이 실행 순서상 서로 인접해야 한다 (엇갈림).</summary>
        public bool RequireAdjacentTargets { get; }

        public InterventionActionData(InterventionActionKey key, int interventionCost, int effectValue)
            : this(key, interventionCost, effectValue, null, false)
        {
        }

        public InterventionActionData(
            InterventionActionKey key,
            int interventionCost,
            int effectValue,
            Side? targetSide,
            bool requireAdjacentTargets)
        {
            Key = key;
            InterventionCost = interventionCost;
            EffectValue = effectValue;
            TargetSide = targetSide;
            RequireAdjacentTargets = requireAdjacentTargets;
        }
    }
}
