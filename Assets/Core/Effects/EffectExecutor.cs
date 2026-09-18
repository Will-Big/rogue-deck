using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>효과 하나의 적용 경계(전투 실행 계약 스펙 §3): 수행 여부와 수치를 정하고, 지금의 위치로
    /// 대상을 고른 뒤 처리기에 넘겨 그 결과를 EffectResult로 돌려준다. 결과를 결과표에 기록하는 일과
    /// 다음 효과로 넘어가는 일은 호출자(카드 실행)가 한다.</summary>
    public sealed class EffectExecutor
    {
        private readonly EffectRegistry _effects;
        private readonly StatusRegistry _statuses;
        private readonly EffectTargetResolver _targets = new EffectTargetResolver();

        public EffectExecutor(EffectRegistry effects, StatusRegistry statuses = null)
        {
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _statuses = statuses;
        }

        public EffectResult Apply(CardExecutionContext context, EffectData effect)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            if (!ShouldApply(effect, context))
            {
                return EffectResult.Skipped;
            }

            var card = context.Card;
            var handler = _effects.Resolve(effect.Key);
            var key = handler.TargetFor(card.Def, effect);
            EffectTargetSnapshot targets = null;
            if (key.HasValue)
            {
                targets = _targets.Resolve(context.State, card, key.Value);
                if (targets.IsEmpty)
                {
                    return EffectResult.Skipped;
                }
            }

            var ctx = new EffectContext
            {
                Card = card,
                State = context.State,
                ResolutionContext = context.Resolution,
                StatusRegistry = _statuses,
                ActorStatuses = CardActor.StatusesFor(context.State, card),
                Effect = effect,
                EffectValue = ResolveEffectValue(effect, context),
                Targets = targets
            };
            handler.Apply(ctx);

            return new EffectResult(true, ctx.ConsumedAmount, ctx.DamageDealt)
            {
                TargetIds = targets?.Ids ?? Array.Empty<string>(),
                Events = ctx.ExtraEvents,
                DamageSteps = ctx.DamageSteps
            };
        }

        /// <summary>이 효과를 수행하는가: 카드 조건이 Basic이면 SkipOnBasic 효과를 건너뛰고, 앞 효과의
        /// 실제 소비량이 요건에 못 미치면 건너뛴다.</summary>
        private static bool ShouldApply(EffectData effect, CardExecutionContext context)
        {
            if (effect.SkipOnBasic
                && context.Card.Def.StartCondition != null
                && context.StartTier == ConditionTier.Basic)
            {
                return false;
            }

            var requirement = effect.Requirement;
            return requirement == null
                || context.Get(requirement.SourceEffectId).ConsumedAmount >= requirement.MinimumConsumed;
        }

        /// <summary>카드 조건 결과로 기본/성공 수치를 고르고, 소비량 비례 가산을 더한다.</summary>
        private static int ResolveEffectValue(EffectData effect, CardExecutionContext context)
        {
            var value = context.StartTier == ConditionTier.Success && effect.SuccessEffectValue.HasValue
                ? effect.SuccessEffectValue.Value
                : effect.EffectValue;
            var scaling = effect.Scaling;
            return scaling == null
                ? value
                : value + context.Get(scaling.SourceEffectId).ConsumedAmount * scaling.PerConsumed;
        }
    }
}
