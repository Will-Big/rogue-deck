using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>Unity EditMode 테스트용 합성 카드. 코어의 CardFixtures와 같은 역할이지만,
    /// FateWeaver.Tests.UnityEditMode가 FateWeaver.Tests.EditMode를 참조하지 않아 따로 둔다
    /// (asmdef 경계). 필요한 모양만 담는다 — 전부 옮기지 않는다.</summary>
    public static class UnityCardFixtures
    {
        public static CardDefinition ChangeExecutionOrder(string id, int delta, int cost = 1)
            => new CardDefinition(id, id, Side.Player, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = cost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        InterventionActionKeys.ChangeExecutionOrder,
                        interventionCost: cost,
                        new ChangeExecutionOrderPayload(Delta: delta, TargetSide: null))
                };
    }
}
