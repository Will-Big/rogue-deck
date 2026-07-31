using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Per-effect inputs/outputs. Handler mutates State and writes its outcome here.</summary>
    public sealed class EffectContext
    {
        public ExecutionCardInstance Card;
        public CombatState State;
        public ResolutionContext ResolutionContext;
        public StatusRegistry StatusRegistry;

        /// <summary>이 카드를 쓰는 쪽의 상태 (약화처럼 주는 피해를 접는 훅이 읽는다).
        /// 소유자를 확정할 수 없으면 null이며, 그 경우 행위자 상태는 적용되지 않는다.</summary>
        public StatusBag ActorStatuses;
        public EffectData Effect;
        public int EffectValue;

        // outputs (read by TurnResolver)
        public int DamageDealt;
        public string TargetId;

        /// <summary>이 효과가 만든 부가 타임라인 이벤트 (예: 즉시 상태 발동의 StatusTicked).
        /// TurnResolver가 CardResolved/CardCancelled 뒤에 발생 순서대로 붙인다.</summary>
        public List<ResolutionEvent> ExtraEvents = new List<ResolutionEvent>();

        /// <summary>Records why this card's effects could not resolve. Only the first reason is kept;
        /// handlers must not mutate state or HP after cancelling (see ExecutionCardInstance.CancellationReason).</summary>
        public void Cancel(Combat.CardCancellationReason reason)
        {
            if (Card != null && Card.CancellationReason == null)
            {
                Card.CancellationReason = reason;
            }
        }
    }

    public interface IEffectHandler
    {
        EffectKey Key { get; }
        void Apply(EffectContext ctx);
    }
}
