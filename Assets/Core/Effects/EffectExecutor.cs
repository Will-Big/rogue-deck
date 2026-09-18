using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>효과 하나의 적용 경계(전투 실행 계약 스펙 §3·§7). 수행 여부와 수치를 정하고 지금의 위치로 대상을
    /// 고른 뒤, 처리기 적용 → 사망 정리(DeathProcessor) → 직접 반응(ReactionDispatcher)을 한 번에 수행하고
    /// EffectResult를 돌려준다. 반응 효과도 같은 경계를 지나므로 그 사건에 다시 반응이 붙을 수 있다 — 어떤 사건에
    /// 발동할지는 각 반응 능력의 조건이 정한다(계획 D11). 결과를 결과표에 기록하는 일과 다음 효과로 넘어가는 일은 호출자(카드 실행)가 한다.</summary>
    public sealed class EffectExecutor
    {
        private readonly EffectRegistry _effects;
        private readonly StatusRegistry _statuses;
        private readonly EffectTargetResolver _targets = new EffectTargetResolver();

        public EffectExecutor(
            EffectRegistry effects, StatusRegistry statuses = null, ReactionRegistry reactions = null)
        {
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _statuses = statuses;
            Damage = new DamageService(statuses);
            Deaths = new DeathProcessor();
            Reactions = new ReactionDispatcher(reactions, this);
        }

        /// <summary>모든 피해가 지나는 공통 경로. 턴 시점 상태 처리도 같은 경로를 쓴다.</summary>
        public DamageService Damage { get; }

        public DeathProcessor Deaths { get; }

        public ReactionDispatcher Reactions { get; }

        public EffectResult Apply(
            CardExecutionContext context, EffectData effect, EffectOrigin origin = EffectOrigin.Primary)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            if (!ShouldApply(effect, context))
            {
                return EffectResult.Skipped;
            }

            var card = context.Card;
            var key = card.Def.TargetOf(effect);
            EffectTargetSnapshot targets = null;
            if (key.HasValue)
            {
                targets = _targets.Resolve(context.State, card, key.Value);
                if (targets.IsEmpty)
                {
                    return EffectResult.Skipped;
                }
            }

            return Run(new EffectContext
            {
                Card = card,
                State = context.State,
                ResolutionContext = context.Resolution,
                StatusRegistry = _statuses,
                Damage = Damage,
                Origin = origin,
                ActorId = CardActor.IdFor(context.State, card),
                ActorStatuses = CardActor.StatusesFor(context.State, card),
                SourceId = card.Def.Id,
                Effect = effect,
                EffectValue = ResolveEffectValue(effect, context),
                Targets = targets
            });
        }

        /// <summary>반응 능력이 낸 효과를 Reaction 기원으로 적용한다. 대상은 반응 대상 규칙으로 이미 골랐다.
        /// 행위자는 반응 보유자(사망했을 수 있다)이고, 카드 버프·결과표를 쓰지 않는다.</summary>
        public EffectResult ApplyReaction(
            CombatState state,
            ResolutionContext resolution,
            string actorId,
            StatusBag actorStatuses,
            string sourceId,
            EffectData effect,
            EffectTargetSnapshot targets)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            return Run(new EffectContext
            {
                Card = null,
                State = state,
                ResolutionContext = resolution,
                StatusRegistry = _statuses,
                Damage = Damage,
                Origin = EffectOrigin.Reaction,
                ActorId = actorId,
                ActorStatuses = actorStatuses,
                SourceId = sourceId,
                Effect = effect,
                EffectValue = effect.EffectValue,
                Targets = targets
            });
        }

        private EffectResult Run(EffectContext ctx)
        {
            var before = DeathProcessor.Capture(ctx.State);
            _effects.Resolve(ctx.Effect.Key).Apply(ctx);

            var events = ctx.ExtraEvents;
            var targetIds = ctx.Targets?.Ids ?? Array.Empty<string>();
            var signals = new List<CombatSignal>();
            Number(ctx.Signals, targetIds, ctx.Origin, signals);
            Number(Deaths.Process(ctx.State, before, events), targetIds, ctx.Origin, signals);

            Reactions.Dispatch(ctx.State, ctx.ResolutionContext, signals, events);

            return new EffectResult(true, ctx.ConsumedAmount, ctx.DamageDealt)
            {
                TargetIds = targetIds,
                Events = events,
                DamageSteps = ctx.DamageSteps,
                Signals = signals
            };
        }

        /// <summary>사건에 이 효과의 기원, 효과 시작 대상 목록의 순번(목록 밖이면 그 뒤), 경계 안 발생 순서를 붙인다.</summary>
        private static void Number(
            IReadOnlyList<CombatSignal> raw, IReadOnlyList<string> targetIds, EffectOrigin origin,
            List<CombatSignal> into)
        {
            foreach (var signal in raw)
            {
                var ordinal = IndexOf(targetIds, signal.TargetId);
                into.Add(signal with
                {
                    Origin = origin,
                    TargetOrdinal = ordinal < 0 ? targetIds.Count : ordinal,
                    Sequence = into.Count
                });
            }
        }

        private static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id) return i;
            }

            return -1;
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
