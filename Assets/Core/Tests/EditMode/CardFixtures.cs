using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>규칙 단위 테스트가 쓰는 합성 카드. **콘텐츠가 아니다** — 여기 있는 카드는
    /// Content/Cards/*.json에 없고, 있어서도 안 된다.
    ///
    /// 메서드 이름이 카드 정체성(`Slash`)이 아니라 **효과 모양**(`Damage`)인 것이 요점이다.
    /// 테스트가 왜 그 카드를 쓰는지가 호출부에서 보이고, 밸런스 조정이 규칙 테스트를 깨지 않는다.
    /// 실제 카드의 동작을 검증하려면 픽스처가 아니라 TestContent.Cards()를 쓴다.</summary>
    public static class CardFixtures
    {
        /// <summary>플레이어 카드의 기본 실행 순서. 적(6)보다 앞이라 "적보다 먼저 해결"이 기본이다.</summary>
        public const int DefaultExecutionOrder = 5;

        public static CardDefinition Damage(
            string id, int damage, int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(id, executionOrder, cost, new EffectData(EffectKeys.Damage, damage));

        public static CardDefinition Block(
            string id, int magnitude, int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, magnitude));

        public static CardDefinition DamageOnFirstTrigger(
            string id, int baseDamage, int whenFirst,
            int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.Conditional(
                    EffectKeys.Damage, baseDamage, new FirstToTrigger(), whenFirst));

        public static CardDefinition DamageAfterEnemyDamage(
            string id, int baseDamage, int whenAfter,
            int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.Conditional(
                    EffectKeys.Damage, baseDamage,
                    new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage), whenAfter));

        public static CardDefinition BlockBeforeEnemyDamage(
            string id, int baseMagnitude, int whenBefore,
            int executionOrder = DefaultExecutionOrder, int cost = 1)
            => Execution(
                id, executionOrder, cost,
                EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, baseMagnitude)
                    with
                    {
                        Condition = new AdjacentCardHasEffect(
                            AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage),
                        SuccessEffectValue = whenBefore
                    });

        public static CardDefinition ChangeExecutionOrder(string id, int delta, int cost = 1)
            => Intervention(
                id, cost,
                new InterventionActionData(
                    InterventionActionKeys.ChangeExecutionOrder,
                    interventionCost: cost, effectValue: delta));

        public static CardDefinition SwapExecutionOrder(string id, int cost = 1)
            => Intervention(
                id, cost,
                new InterventionActionData(
                    InterventionActionKeys.SwapExecutionOrder,
                    interventionCost: cost, effectValue: 0));

        /// <summary>적 의도 카드. 적 카드는 아직 JSON이 아니므로(별도 계획) 픽스처가 필요하다.</summary>
        public static CardDefinition EnemyAttack(string id, int executionOrder, int damage)
            => new CardDefinition(
                id, id, Side.Enemy, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) })
                { EnergyCost = 0, Category = CardCategory.Execution };

        private static CardDefinition Execution(
            string id, int executionOrder, int cost, EffectData effect)
            => new CardDefinition(id, id, Side.Player, executionOrder, new[] { effect })
                { EnergyCost = cost, Category = CardCategory.Execution };

        private static CardDefinition Intervention(
            string id, int cost, InterventionActionData action)
            => new CardDefinition(id, id, Side.Player, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = cost,
                    Category = CardCategory.Intervention,
                    InterventionAction = action
                };
    }
}
