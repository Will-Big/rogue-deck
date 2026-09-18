using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>실행선의 카드를 차례대로 실행하고 이벤트 타임라인을 낸다. 실행선은 고정 사본이 아니다 —
    /// 매번 아직 차례가 오지 않은 다음 카드를 묻는다(전투 실행 계약 스펙 §6).
    /// Per card: intercept/pre-cancellation check (CardCancelled), else every effect in authored order
    /// through EffectExecutor — which also runs that effect's death cleanup (deaths, CardRemoved) and direct
    /// reactions — then CardResolved followed by those events in occurrence order. 턴 끝에는 상태 틱 → 사망 정리 →
    /// 직접 반응 → 수명 만료를 한 묶음으로 처리한다.</summary>
    public sealed class TurnResolver
    {
        private readonly StatusRegistry _statuses;
        private readonly EffectExecutor _executor;

        public TurnResolver(
            EffectRegistry effects, StatusRegistry statuses = null, ReactionRegistry reactions = null)
        {
            _statuses = statuses;
            _executor = new EffectExecutor(effects, statuses, reactions);
        }

        public List<ResolutionEvent> Resolve(CombatState state, int turnIndex)
        {
            var events = new List<ResolutionEvent> { new TurnStarted(turnIndex) };
            var resolutionContext = ResolutionContext.From(state);

            for (var card = state.Zone.NextPending(); card != null; card = state.Zone.NextPending())
            {
                card.ExecutionState = CardExecutionState.Executing;
                ResolveCard(state, resolutionContext, card, events);

                // 차례가 온 카드는 효과가 없거나 취소돼도 실행 사실이 남는다(스펙 §2·§6). 조건은
                // ResolveCard 안에서 이미 읽었으므로 여기서 기록해도 자기 자신을 직전 카드로 보지 않는다.
                resolutionContext.MarkExecuted(card);
                card.ExecutionState = CardExecutionState.Executed;
            }

            EndOfTurnMaintenance(state, resolutionContext, events);
            events.Add(new TurnEnded(turnIndex, ComputeOutcome(state)));
            return events;
        }

        private void ResolveCard(
            CombatState state,
            ResolutionContext resolutionContext,
            ExecutionCardInstance card,
            List<ResolutionEvent> events)
        {
            // 차례 전에 기록된 취소 사유(가로채기 등)가 있으면 효과를 하나도 수행하지 않는다.
            if (card.CancellationReason == null && IsInterceptedByStatus(state, card, events))
            {
                card.CancellationReason = CardCancellationReason.StatusIntercepted;
            }

            if (card.CancellationReason != null)
            {
                events.Add(new CardCancelled(card.InstanceId, card.Def.Id, card.OwnerId, card.CancellationReason.Value));
                return;
            }

            int totalDamage = 0;
            string targetId = null;
            var pendingDeathEvents = new List<ResolutionEvent>();
            var damageSteps = new List<DamageStep>();

            // 카드 시작 조건은 차례를 맞은 지금 한 번 평가하고 카드가 끝날 때까지 고정한다(스펙 §2).
            var execution = new CardExecutionContext(
                card, ResolveStartTier(card, resolutionContext, pendingDeathEvents), state, resolutionContext);

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

        /// <summary>턴 종료 시점: 상태 틱 → 사망 정리 → 직접 반응(턴 시점 처리는 Primary 기원, 계획 D7) → 수명 만료.</summary>
        private void EndOfTurnMaintenance(
            CombatState state, ResolutionContext resolutionContext, List<ResolutionEvent> events)
        {
            var before = DeathProcessor.Capture(state);
            var signals = new List<CombatSignal>();

            RunTurnEndTicks(state, events, signals);

            foreach (var died in _executor.Deaths.Process(state, before, events))
            {
                signals.Add(died);
            }

            _executor.Reactions.Dispatch(state, resolutionContext, Numbered(signals), events);

            foreach (var member in state.Party)
            {
                foreach (var key in member.Statuses.EndOfTurn())
                {
                    events.Add(new StatusExpired(member.Id, key.Id));
                }
            }

            foreach (var enemy in state.Enemies)
            {
                foreach (var key in enemy.Statuses.EndOfTurn())
                {
                    events.Add(new StatusExpired(enemy.Id, key.Id));
                }
            }
        }

        /// <summary>턴 시점 사건에는 효과 대상 목록이 없으므로 발생 순서만 붙인다(틱은 대형 순으로 일어난다).</summary>
        private static List<CombatSignal> Numbered(List<CombatSignal> signals)
        {
            var numbered = new List<CombatSignal>(signals.Count);
            for (var i = 0; i < signals.Count; i++)
            {
                numbered.Add(signals[i] with { Origin = EffectOrigin.Primary, TargetOrdinal = 0, Sequence = i });
            }

            return numbered;
        }

        /// <summary>행동 턴 종료 틱: 파티 대형 순 → 적 대형 순. 보유자별로 발동 직전에 생존을 확인하므로
        /// 앞선 틱으로 이미 사망한 대상은 제외된다(카드풀 스펙 §3.2). 틱 피해는 공통 피해 경로를 지난다.</summary>
        private void RunTurnEndTicks(CombatState state, List<ResolutionEvent> events, List<CombatSignal> signals)
        {
            if (_statuses == null)
            {
                return;
            }

            var sink = new DamageSink(events, signals);
            foreach (var member in state.Party)
            {
                if (!member.IsAlive) continue;
                var target = member;
                TickHolder(target.Statuses, target.Id, events, state.StatusContent,
                    key => damage => _executor.Damage.Deal(
                        state, target, StatusDamage(state, key, damage), sink));
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy.Hp <= 0) continue;
                var target = enemy;
                TickHolder(target.Statuses, target.Id, events, state.StatusContent,
                    key => damage => _executor.Damage.Deal(
                        state, target, StatusDamage(state, key, damage), sink));
            }
        }

        /// <summary>상태 피해 요청. 관통·배율 미적용은 그 상태의 저작 데이터가 정한다.</summary>
        private static DamageRequest StatusDamage(CombatState state, StatusKey key, int damage)
            => DamageRequest.StatusTick(damage, key.Id, state.StatusContent.DamageTraitsOf(key));

        private void TickHolder(
            StatusBag bag, string holderId, List<ResolutionEvent> events,
            Authoring.Statuses.StatusContentCatalog content, Func<StatusKey, Action<int>> dealDamageFor)
        {
            // Snapshot: a hook may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior))
                {
                    behavior.OnTurnEnd(new StatusTickContext
                    {
                        Instance = status,
                        HolderBag = bag,
                        HolderId = holderId,
                        DealDamage = dealDamageFor(status.Key),
                        Events = events,
                        Content = content
                    });
                }
            }
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

        private static Outcome ComputeOutcome(CombatState state)
        {
            if (state.Party.All(m => !m.IsAlive)) return Outcome.Lose;
            if (state.Enemies.All(e => e.Hp <= 0)) return Outcome.Win;
            return Outcome.Ongoing;
        }
    }
}
