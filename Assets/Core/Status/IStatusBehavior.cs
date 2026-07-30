using System;
using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>Inputs a status behavior may read when a hook fires.</summary>
    public sealed class StatusContext
    {
        public StatusInstance Instance;
    }

    /// <summary>턴 종료 틱 훅 입력. DealDamage는 보유자에게 직접 피해를 주는 배선(파티원은
    /// TakeDamage, 적은 Hp 차감)이며 ModifyIncomingDamage를 경유하지 않는다. Events에 추가한
    /// 이벤트는 타임라인의 현재 위치에 이어 붙는다.</summary>
    public sealed class StatusTickContext
    {
        public StatusInstance Instance;
        public StatusBag HolderBag;
        public string HolderId;
        public Action<int> DealDamage;
        public List<ResolutionEvent> Events;
    }

    /// <summary>보유자 사망 훅 입력. State는 이전 대상 탐색 등 규칙 판단에 쓴다.</summary>
    public sealed class StatusDeathContext
    {
        public StatusInstance Instance;
        public StatusBag HolderBag;
        public string HolderId;
        public Combat.CombatState State;
        public List<ResolutionEvent> Events;
    }

    /// <summary>Behavior for a status key. Implement only the relevant hooks (defaults are no-ops).
    /// Behavior lives here (code, registered); the StatusInstance on a holder is just data.</summary>
    public interface IStatusBehavior
    {
        StatusKey Key { get; }
        StatusScope Scope { get; }

        /// <summary>재부여 시 수치를 교체하지 않고 합산할지 (방어·독 = true; §3.1/§3.2).</summary>
        bool StacksMagnitude { get; }

        /// <summary>피해 계산에서 이 상태가 접히는 단계 (방어만 흡수 층).</summary>
        StatusDamageLayer DamageLayer { get; }

        /// <summary>Entity-scoped: fold into damage the holder is about to RECEIVE.</summary>
        int ModifyIncomingDamage(int damage, StatusContext ctx);

        /// <summary>Card-scoped: return true to nullify/skip the card's resolution (e.g. stun).</summary>
        bool InterceptCardResolve(StatusContext ctx);

        /// <summary>Entity-scoped: fold into the executionOrder of a card owned by the holder (e.g. slow/haste).</summary>
        int ModifyExecutionOrder(int executionOrder, StatusContext ctx);

        /// <summary>행동 턴 종료(수명 만료 전)에 보유자 단위로 발동하는 틱 (예: 독 피해+성장).</summary>
        void OnTurnEnd(StatusTickContext ctx);

        /// <summary>보유자가 사망한 직후 발동 (예: 남은 독 이전).</summary>
        void OnHolderDied(StatusDeathContext ctx);
    }

    /// <summary>Base class with no-op hook defaults. Concrete statuses override what they use.
    /// (Abstract base instead of default-interface-methods, which Unity 6 / netstandard2.1 lacks.)</summary>
    public abstract class StatusBehavior : IStatusBehavior
    {
        public abstract StatusKey Key { get; }
        public abstract StatusScope Scope { get; }

        public virtual bool StacksMagnitude => false;
        public virtual StatusDamageLayer DamageLayer => StatusDamageLayer.Multiplier;
        public virtual int ModifyIncomingDamage(int damage, StatusContext ctx) => damage;
        public virtual bool InterceptCardResolve(StatusContext ctx) => false;
        public virtual int ModifyExecutionOrder(int executionOrder, StatusContext ctx) => executionOrder;
        public virtual void OnTurnEnd(StatusTickContext ctx) { }
        public virtual void OnHolderDied(StatusDeathContext ctx) { }
    }
}
