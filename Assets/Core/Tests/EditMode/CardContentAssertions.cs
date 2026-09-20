using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>저작된 카드에서 수치를 꺼내는 공용 헬퍼. 카드 수치를 단언하는 테스트가 둘 이상이라
    /// 한곳에 둔다(2026-09-20 사용자 결정).</summary>
    public static class CardContentAssertions
    {
        /// <summary>이 카드가 주는 직접 피해의 합.</summary>
        public static int DamageOf(CardDefinition card)
            => card.Effects.Where(e => e.Key == EffectKeys.Damage).Sum(e => e.EffectValue);

        /// <summary>이 카드가 거는 방어의 합. 카드가 상태에 주는 것은 count 하나이고 그것이
        /// EffectValue에 실린다(EffectData 주석). 어떤 상태인지는 Payload가 든다.</summary>
        public static int BlockOf(CardDefinition card)
            => card.Effects
                .Where(e => e.Key == EffectKeys.ApplyStatus
                    && e.Payload is ApplyStatusPayload payload && payload.Key == StatusKeys.Block)
                .Sum(e => e.EffectValue);
    }
}
