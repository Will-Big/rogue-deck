using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 적의 상태를 최대치까지 소비한다 (소비형 독 카드).</summary>
    [Serializable]
    public sealed class ConsumeStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public int MaxAmount;
        public int DamageBonusPerConsumed;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.ConsumeStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, 0)
            {
                Payload = new ConsumeStatusPayload(Status.ToKey(), MaxAmount, DamageBonusPerConsumed)
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty)
            {
                yield return "consume_status spec requires a status key.";
            }
            else if (!context.HasStatus(Status.ToKey()))
            {
                yield return "Unknown status key '" + Status.Id + "'.";
            }

            if (MaxAmount < 1)
            {
                yield return "consume_status MaxAmount must be at least 1.";
            }
        }

        public override string ToLiteral()
            => "new ConsumeStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", MaxAmount = " + MaxAmount
                + ", DamageBonusPerConsumed = " + DamageBonusPerConsumed
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";
    }
}
