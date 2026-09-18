using System.Collections.Generic;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>카드 한 장의 실행 진행(전투 실행 계약 스펙 §2·§3): 가로채기 → 시작 조건 고정 → 실행 사실 기록 →
    /// 효과를 저작 순서대로(각 효과의 사망 정리·직접 반응은 EffectExecutor가 끝낸다) → CardResolved/CardCancelled.
    /// 피해 계산·구체 상태 이름·승패 판정은 모른다.</summary>
    public sealed class CardExecutor
    {
        private readonly EffectExecutor _executor;
        private readonly StatusRegistry _statuses;

        public CardExecutor(EffectExecutor executor, StatusRegistry statuses = null)
        {
            _executor = executor ?? throw new System.ArgumentNullException(nameof(executor));
            _statuses = statuses;
        }

        /// <summary>카드 한 장을 끝까지 수행하고 그 이벤트를 붙인다. 승패는 판정하지 않는다 — 호출자가 카드가
        /// 끝난 뒤 판정한다(스펙 §2). 차례가 온 카드는 효과가 없거나 취소되거나 수행자가 죽어도 실행 사실이 남는다.</summary>
        public void Execute(
            CombatState state,
            ResolutionContext resolutionContext,
            ExecutionCardInstance card,
            List<ResolutionEvent> events)
        {
            if (card.ExecutionState == CardExecutionState.Removed)
            {
                return;
            }

            card.ExecutionState = CardExecutionState.Executing;
            // 차례 전에 기록된 취소 사유(가로채기 등)가 있으면 효과를 하나도 수행하지 않는다.
            if (card.CancellationReason == null && IsInterceptedByStatus(state, card, events))
            {
                card.CancellationReason = CardCancellationReason.StatusIntercepted;
            }

            if (card.CancellationReason != null)
            {
                events.Add(new CardCancelled(card.InstanceId, card.Def.Id, card.OwnerId, card.CancellationReason.Value));
                Finish(resolutionContext, card);
                return;
            }

            int totalDamage = 0;
            string targetId = null;
            var pendingDeathEvents = new List<ResolutionEvent>();
            var damageSteps = new List<DamageStep>();

            // 카드 시작 조건은 차례를 맞은 지금 한 번 평가하고 카드가 끝날 때까지 고정한다(스펙 §2).
            var execution = new CardExecutionContext(
                card, ResolveStartTier(card, resolutionContext, pendingDeathEvents), state, resolutionContext);

            // 조건을 읽은 뒤 실행 사실을 기록한다 — 자기 자신을 직전 실행 카드로 보지 않는다(스펙 §2·§6).
            resolutionContext.MarkExecuted(card);

            // 효과마다 그 시작 시점의 위치로 대상을 고른다. 대상이 없는 효과는 미적용으로 기록되고
            // 카드는 다음 효과로 계속한다(스펙 §2).
            // 효과마다 사망 정리와 직접 반응까지 효과 실행기가 끝낸다(스펙 §7).
            foreach (var effect in card.Def.Effects)
            {
                var result = _executor.Apply(execution, effect);
                execution.Record(effect.Id, result);
                totalDamage += result.DamageDealt;
                damageSteps.AddRange(result.DamageSteps);
                if (targetId == null && result.TargetIds.Count > 0)
                {
                    // CardResolved.TargetId = 처음 적용된 효과의 첫 대상.
                    targetId = result.TargetIds[0];
                }
                pendingDeathEvents.AddRange(result.Events);
            }

            // CardResolved 뒤에 이 카드의 효과가 만든 변화·사망·카드 제거·직접 반응이 발생 순서대로 이어진다.
            events.Add(new CardResolved(
                card.InstanceId, card.OwnerId, card.Def.Id, card.Def.Side, totalDamage, targetId, execution.StartTier)
            {
                DamageSteps = damageSteps
            });
            events.AddRange(pendingDeathEvents);
            card.ExecutionState = CardExecutionState.Executed;
        }

        /// <summary>효과 없이 끝난 카드(가로채기 등)도 실행 사실을 남긴다.</summary>
        private static void Finish(ResolutionContext resolutionContext, ExecutionCardInstance card)
        {
            resolutionContext.MarkExecuted(card);
            card.ExecutionState = CardExecutionState.Executed;
        }

        private bool IsInterceptedByStatus(
            CombatState state, ExecutionCardInstance card, List<ResolutionEvent> events)
        {
            if (_statuses == null)
            {
                return false;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(card.Statuses.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.CardInstance
                    && behavior.InterceptCardResolve(
                        new StatusContext { Instance = status, Rules = state.StatusRules }))
                {
                    var countBefore = status.Count;
                    card.Statuses.Consume(status);
                    var remaining = card.Statuses.Get(status.Key);
                    var consumed = countBefore - (remaining?.Count ?? 0);
                    if (consumed > 0)
                    {
                        events.Add(new CardBuffConsumed(
                            card.InstanceId, card.Def.Id, status.Key.Id, consumed));
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>카드 시작 조건의 결과. 조건이 없으면 Basic이다.</summary>
        private static ConditionTier ResolveStartTier(
            ExecutionCardInstance card,
            ResolutionContext resolutionContext,
            List<ResolutionEvent> pending)
        {
            if (card.Def.StartCondition == null)
            {
                return ConditionTier.Basic;
            }

            var tier = ConditionEvaluator.Evaluate(card.Def.StartCondition, card, resolutionContext);
            if (tier == ConditionTier.Success)
            {
                // reward-nullified disruption forces a success down to basic, spending its charge.
                var nullified = card.Statuses.Get(StatusKeys.RewardNullified);
                if (nullified != null)
                {
                    var countBefore = nullified.Count;
                    card.Statuses.Consume(nullified);
                    var remaining = card.Statuses.Get(StatusKeys.RewardNullified);
                    var consumed = countBefore - (remaining?.Count ?? 0);
                    if (consumed > 0)
                    {
                        pending.Add(new CardBuffConsumed(
                            card.InstanceId, card.Def.Id,
                            StatusKeys.RewardNullified.Id, consumed));
                    }

                    return ConditionTier.Basic;
                }
            }

            return tier;
        }
    }
}
