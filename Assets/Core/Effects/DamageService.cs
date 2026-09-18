using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>피해의 원인. 공격은 공격받음 사건을 내고, 상태 피해(독 등)는 내지 않는다(스펙 §7).</summary>
    public enum DamageCause
    {
        Attack,
        Status
    }

    /// <summary>피해 한 번의 입력: 양, 원인, 피해 속성(관통·배율 미적용 — 데이터가 정한다), 원인 개체와 표시용 원인.</summary>
    public readonly struct DamageRequest
    {
        public DamageRequest(
            int amount, DamageCause cause, DamageTraits traits,
            string sourceId, HpChangeSource hpSource, string hpSourceId)
        {
            Amount = amount;
            Cause = cause;
            Traits = traits ?? DamageTraits.Normal;
            SourceId = sourceId;
            HpSource = hpSource;
            HpSourceId = hpSourceId;
        }

        public int Amount { get; }
        public DamageCause Cause { get; }
        public DamageTraits Traits { get; }

        /// <summary>피해를 준 개체 id(사건의 SourceId). 상태 피해면 null.</summary>
        public string SourceId { get; }
        public HpChangeSource HpSource { get; }
        public string HpSourceId { get; }

        /// <summary>공격 피해. 속성을 주지 않으면 보통 피해다.</summary>
        public static DamageRequest Attack(
            int amount, string sourceId, HpChangeSource hpSource, string hpSourceId, DamageTraits traits = null)
            => new DamageRequest(amount, DamageCause.Attack, traits, sourceId, hpSource, hpSourceId);

        /// <summary>상태 피해(틱·즉시 발동). 속성은 그 상태의 저작 데이터(StatusContentCatalog.DamageTraitsOf)에서 온다.</summary>
        public static DamageRequest StatusTick(int amount, string statusId, DamageTraits traits)
            => new DamageRequest(amount, DamageCause.Status, traits, null, HpChangeSource.StatusTick, statusId);
    }

    /// <summary>피해 계산이 결과를 쓰는 곳: 표시 이벤트, 규칙 사건, 피해 단계 내역.</summary>
    public sealed class DamageSink
    {
        public DamageSink(List<ResolutionEvent> events, List<CombatSignal> signals, List<DamageStep> steps = null)
        {
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Signals = signals ?? throw new ArgumentNullException(nameof(signals));
            Steps = steps;
        }

        public List<ResolutionEvent> Events { get; }
        public List<CombatSignal> Signals { get; }
        public List<DamageStep> Steps { get; }
    }

    /// <summary>모든 피해가 지나는 공통 경로(스펙 §7): 받는 쪽 상태 접기(배율 → 흡수), HP 차감, HpChanged,
    /// 공격받음·체력 피해 사건. 원인은 호출자가, 관통·배율 미적용은 피해 속성(데이터)이 정한다 — 원인이나
    /// 특정 상태에 따라 경로를 가르지 않는다.</summary>
    public sealed class DamageService
    {
        private readonly StatusRegistry _statuses;

        public DamageService(StatusRegistry statuses)
        {
            _statuses = statuses;
        }

        /// <summary>적에게 피해를 준다. 접힌 뒤의 피해량(흡수 후, HP 초과분 포함)을 돌려준다.</summary>
        public int Deal(CombatState state, Enemy target, DamageRequest request, DamageSink sink)
            => Deal(state, target.Id, target.Statuses, () => target.Hp, hp => target.Hp = hp, request, sink);

        /// <summary>파티원에게 피해를 준다.</summary>
        public int Deal(CombatState state, PartyMember target, DamageRequest request, DamageSink sink)
            => Deal(state, target.Id, target.Statuses, () => target.Hp, hp => target.Hp = hp, request, sink);

        private int Deal(
            CombatState state, string holderId, StatusBag bag, Func<int> getHp, Action<int> setHp,
            DamageRequest request, DamageSink sink)
        {
            var amount = request.Amount;
            if (!request.Traits.Has(DamageTrait.IgnoresMultipliers))
            {
                amount = StatusDamageFold.Incoming(
                    bag, _statuses, state.StatusRules, amount, StatusDamageLayer.Multiplier, holderId, sink.Steps);
            }

            if (!request.Traits.Has(DamageTrait.Piercing))
            {
                amount = StatusDamageFold.Incoming(
                    bag, _statuses, state.StatusRules, amount, StatusDamageLayer.Absorb, holderId, sink.Steps);
            }

            if (request.Cause == DamageCause.Attack)
            {
                sink.Signals.Add(new CombatSignal(CombatSignalKeys.Attacked, request.SourceId, holderId, amount));
            }

            var before = getHp();
            setHp(before - amount);
            var after = getHp();
            if (after != before)
            {
                sink.Events.Add(new HpChanged(holderId, before, after, request.HpSource, request.HpSourceId));
            }

            if (after < before)
            {
                sink.Signals.Add(new CombatSignal(CombatSignalKeys.HpDamaged, request.SourceId, holderId, before - after));
            }

            return amount;
        }
    }
}
