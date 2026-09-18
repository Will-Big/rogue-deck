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

        /// <summary>이 효과가 시작할 때 고른 대상(진영이 없는 효과는 null). 비어 있지 않다 —
        /// 대상이 없으면 EffectExecutor가 처리기를 부르지 않는다.</summary>
        public EffectTargetSnapshot Targets;

        /// <summary>대상을 고르는 효과의 대상. 진영 없이 저작된 효과면 데이터 오류로 예외를 던진다 — 저작 콘텐츠는
        /// 로딩 검증(EffectSpec.IsTargeted)이 진영을 요구하므로 C# 정의 오류다.</summary>
        public EffectTargetSnapshot RequireTargets()
            => Targets ?? throw new System.InvalidOperationException(
                "Effect '" + Effect?.Key.Id + "' on card '" + Card?.Def.Id + "' needs a TargetFaction.");

        // outputs (read by EffectExecutor)
        public int DamageDealt;

        /// <summary>이 효과가 실제로 소비한 양. 효과 결과(EffectResult.ConsumedAmount)가 된다.</summary>
        public int ConsumedAmount;

        /// <summary>이 효과가 만든 부가 타임라인 이벤트 (예: 즉시 상태 발동의 StatusTicked).
        /// 발생 순서대로 EffectResult.Events가 된다.</summary>
        public List<ResolutionEvent> ExtraEvents = new List<ResolutionEvent>();

        /// <summary>이 효과의 피해가 상태로 바뀐 단계들. 카드 단위로 모여 CardResolved에 실린다.</summary>
        public List<Events.DamageStep> DamageSteps = new List<Events.DamageStep>();
    }

    /// <summary>효과 하나를 적용한다. 처리기는 받은 대상에만 적용하며 카드 전체를 취소하거나 다음 효과를
    /// 부르지 않는다(전투 실행 계약 스펙 §3). 누구를 고를지는 처리기가 아니라 데이터(효과 진영 + 카드 축)가
    /// 정한다.</summary>
    public interface IEffectHandler
    {
        EffectKey Key { get; }

        void Apply(EffectContext ctx);
    }
}
