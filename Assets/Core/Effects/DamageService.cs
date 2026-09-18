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

    /// <summary>피해 한 번의 입력. Piercing은 흡수 층(방어)을 건너뛰고, ApplyMultipliers가 false면 배율 층
    /// (취약 등)을 건너뛴다. 독은 원인=상태, Piercing, 배율 미적용이다(계획 D1).
    /// HpSource·HpSourceId는 표시용 HpChanged의 원인 표기다.</summary>
    public readonly struct DamageRequest
    {
        public DamageRequest(
            int amount, DamageCause cause, bool piercing, bool applyMultipliers,
            string sourceId, HpChangeSource hpSource, string hpSourceId)
        {
            Amount = amount;
            Cause = cause;
            Piercing = piercing;
            ApplyMultipliers = applyMultipliers;
            SourceId = sourceId;
            HpSource = hpSource;
            HpSourceId = hpSourceId;
        }

        public int Amount { get; }
        public DamageCause Cause { get; }
        public bool Piercing { get; }
        public bool ApplyMultipliers { get; }

        /// <summary>피해를 준 개체 id(사건의 SourceId). 상태 피해면 null.</summary>
        public string SourceId { get; }
        public HpChangeSource HpSource { get; }
        public string HpSourceId { get; }

        /// <summary>공격 피해: 배율과 흡수를 모두 거친다.</summary>
        public static DamageRequest Attack(int amount, string sourceId, HpChangeSource hpSource, string hpSourceId)
            => new DamageRequest(amount, DamageCause.Attack, false, true, sourceId, hpSource, hpSourceId);

        /// <summary>상태 피해(독 틱): 관통, 배율 미적용.</summary>
        public static DamageRequest StatusTick(int amount, string statusId)
            => new DamageRequest(amount, DamageCause.Status, true, false, null, HpChangeSource.StatusTick, statusId);
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
    /// 공격받음·체력 피해 사건. 원인·관통·배율 여부는 호출자가 요청으로 밝힌다 — 독만의 우회 경로를 두지 않는다.</summary>
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
            if (request.ApplyMultipliers)
            {
                amount = StatusDamageFold.Incoming(
                    bag, _statuses, state.StatusRules, amount, StatusDamageLayer.Multiplier, holderId, sink.Steps);
            }

            if (!request.Piercing)
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
