using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Card-level assembly only. Effect mapping lives on each EffectSpec subclass
    /// (no central effect switch — AGENTS.md rule 9).</summary>
    public static class CardSpecMapper
    {
        public static CardDefinition ToDefinition(CardSpec spec)
        {
            if (spec is InterventionCardSpec intervention)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = spec.EnergyCost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        intervention.Intervention.ToKey(), spec.EnergyCost, ToPayload(intervention))
                };
            }

            var execution = (ExecutionCardSpec)spec;
            var effects = (execution.Effects ?? Array.Empty<EffectSpec>())
                .Select(e => e.ToEffectData())
                .ToArray();
            return new CardDefinition(
                spec.Id, spec.Name, spec.Side, execution.BaseExecutionOrder, effects)
            {
                EnergyCost = spec.EnergyCost,
                Category = CardCategory.Execution
            };
        }

        private static Side? ToTargetSide(InterventionTargetSideRef side)
        {
            switch (side)
            {
                case InterventionTargetSideRef.Player: return Side.Player;
                case InterventionTargetSideRef.Enemy: return Side.Enemy;
                default: return null;
            }
        }

        /// <summary>계획 3.5 Task 1의 임시 다리. 저작이 아직 평평해서 키를 보고 페이로드를 만든다.
        /// Task 4가 InterventionSpec.ToPayload()로 옮기며 이 메서드를 제거한다 — 그때까지만 존재하는
        /// 규칙 9 예외다.</summary>
        private static IInterventionPayload ToPayload(InterventionCardSpec spec)
        {
            var key = spec.Intervention.ToKey();
            if (key == InterventionActionKeys.ChangeExecutionOrder)
            {
                return new ChangeExecutionOrderPayload(
                    spec.InterventionEffectValue, ToTargetSide(spec.InterventionTargetSide));
            }

            if (key == InterventionActionKeys.SwapExecutionOrder)
            {
                return new SwapExecutionOrderPayload(
                    ToTargetSide(spec.InterventionTargetSide), spec.InterventionRequireAdjacent);
            }

            return null;
        }
    }
}
