using System;
using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>Inputs a status behavior may read when a hook fires.</summary>
    public sealed class StatusContext
    {
        public StatusInstance Instance;

        /// <summary>이 전투의 상태 규칙 (배율). 취약·약화·손상처럼 배율만 필요한 훅이 쓴다.</summary>
        public StatusRuleSet Rules;

        /// <summary>이 전투의 상태 저작 콘텐츠 (수명 종류 + 세기). 둔화·가속처럼 카드가 아니라
        /// 상태 자신이 세기를 아는 훅이 쓴다.</summary>
        public Authoring.Statuses.StatusContentCatalog Content;
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

        /// <summary>이 전투의 상태 저작 콘텐츠. 독의 턴당 성장치처럼 규칙 수치를 읽는 훅이 쓴다.</summary>
        public Authoring.Statuses.StatusContentCatalog Content;
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

        /// <summary>Entity-scoped: fold into damage the holder is about to DEAL (e.g. weak).</summary>
        int ModifyOutgoingDamage(int damage, StatusContext ctx);

        /// <summary>Entity-scoped: fold into the magnitude the holder is about to GAIN from an applied
        /// status (e.g. damaged reducing block gain). The behavior decides which gained keys it affects,
        /// so no central switch grows here.</summary>
        int ModifyGainedMagnitude(StatusKey gained, int magnitude, StatusContext ctx);

        /// <summary>Card-scoped: return true to nullify/skip the card's resolution.</summary>
        bool InterceptCardResolve(StatusContext ctx);

        /// <summary>Entity-scoped: fold into the executionOrder of a card owned by the holder (e.g. slow/haste).</summary>
        int ModifyExecutionOrder(int executionOrder, StatusContext ctx);

        /// <summary>행동 턴 종료(수명 만료 전)에 보유자 단위로 발동하는 틱 (예: 독 피해+성장).</summary>
        void OnTurnEnd(StatusTickContext ctx);

        /// <summary>보유자가 사망한 직후 발동 (예: 남은 독 이전).</summary>
        void OnHolderDied(StatusDeathContext ctx);

        /// <summary>이번 턴 이 상태의 발동을 막는다. trigger_status가 즉시 발동시킨 뒤 호출하며,
        /// 어떤 마커를 쓰는지는 상태 자신만 안다 — 카드가 알 필요가 없다.</summary>
        void SuppressThisTurn(StatusBag holderBag);

        /// <summary>이 상태를 저작할 때 쓰는 스펙 타입의 빈 인스턴스. JSON 판별자가 이걸로
        /// "poison → PoisonStatusSpec"을 안다 — 스펙 **모양**은 코드가, **값**은 JSON이 갖는다.
        /// 리플렉션 대신 각 행동이 스스로 답한다 (규칙 9).</summary>
        Authoring.Statuses.StatusSpec NewSpec();
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
        public virtual int ModifyOutgoingDamage(int damage, StatusContext ctx) => damage;

        public virtual int ModifyGainedMagnitude(StatusKey gained, int magnitude, StatusContext ctx)
            => magnitude;
        public virtual bool InterceptCardResolve(StatusContext ctx) => false;
        public virtual int ModifyExecutionOrder(int executionOrder, StatusContext ctx) => executionOrder;
        public virtual void OnTurnEnd(StatusTickContext ctx) { }
        public virtual void OnHolderDied(StatusDeathContext ctx) { }
        public virtual void SuppressThisTurn(StatusBag holderBag) { }

        public virtual Authoring.Statuses.StatusSpec NewSpec()
            => new Authoring.Statuses.StatusSpec();
    }
}
