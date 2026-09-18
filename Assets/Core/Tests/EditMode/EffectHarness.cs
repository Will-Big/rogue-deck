using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>처리기 하나를 실제 효과 적용 경계(EffectExecutor)로 돌린다. 대상 선택·미적용 판정이 실행 경로와
    /// 같으므로, 처리기를 직접 부르던 테스트가 결과(적용 여부·대상 id)를 EffectResult로 단언한다.</summary>
    internal static class EffectHarness
    {
        public static EffectResult Apply(
            IEffectHandler handler,
            CombatState state,
            ExecutionCardInstance card,
            EffectData effect = null,
            StatusRegistry statuses = null)
        {
            var effects = new EffectRegistry();
            effects.Register(handler);
            var context = new CardExecutionContext(card, ConditionTier.Basic, state, ResolutionContext.From(state));
            return new EffectExecutor(effects, statuses).Apply(context, effect ?? card.Def.Effects[0]);
        }
    }
}
