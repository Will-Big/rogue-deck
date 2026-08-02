using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Applies a status effect to the selected target(s).</summary>
    [Serializable]
    public sealed class ApplyStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public int Value;

        // Permanent은 StatusLifetimeKind의 0번째(기본) 값이라 DefaultValueHandling.Ignore가 지운다.
        // 생략된 lifetime이 조용히 "영원히 지속"으로 복원되는 사고를 막기 위해 항상 써야 한다.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public StatusLifetimeKind Lifetime;
        public int LifetimeCount;
        public StatusApplyTarget Target;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.ApplyStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, ResolvedCount())
            {
                Payload = new ApplyStatusPayload(Status.ToKey(), Target)
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty)
            {
                yield return "apply_status spec requires a status key.";
            }
            else if (!context.HasStatus(Status.ToKey()))
            {
                yield return "Unknown status key '" + Status.Id + "'.";
            }
        }

        public override string ToLiteral()
            => "new ApplyStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", Value = " + Value
                + ", Lifetime = StatusLifetimeKind." + Lifetime
                + ", LifetimeCount = " + LifetimeCount
                + ", Target = StatusApplyTarget." + Target
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";

        /// <summary>카드가 apply_status에 주는 count 하나로 접는다. Turns·UntilConsumed는 지속(카드가
        /// 적은 LifetimeCount), 그 외(Permanent·ThisTurn)는 세기(Value)다 — ApplyStatusHandler가
        /// StatusContentCatalog에서 같은 판단을 다시 하므로, 여기 Lifetime은 그 판단과 일치해야 한다
        /// (카드 저작 시점의 Lifetime·LifetimeCount·Value 3필드는 후속 작업에서 정리 대상이다).</summary>
        private int ResolvedCount()
            => Lifetime == StatusLifetimeKind.Turns || Lifetime == StatusLifetimeKind.UntilConsumed
                ? LifetimeCount
                : Value;
    }
}
