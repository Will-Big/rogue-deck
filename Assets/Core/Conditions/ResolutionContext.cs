using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Conditions
{
    /// <summary>한 턴 동안 조건과 효과가 묻는 두 가지 질의(전투 실행 계약 스펙 §6).
    /// <list type="bullet">
    /// <item>실행선 — 앞/뒤/인접/몇 번째 같은 배치 질의. 고정 사본이 아니라 현재 실행선을 본다. 주인이 죽어
    /// 빠진 카드는 여기에 없고, 차례가 왔지만 효과가 없었던 카드는 남아 있다.</item>
    /// <item>실행 이력 — "직전에 실행된 카드" 질의. 차례가 온 카드를 실행 순서대로 담는다. 효과가 없거나
    /// 취소된 카드도 포함한다.</item>
    /// </list></summary>
    public sealed class ResolutionContext
    {
        private readonly FutureZone _zone;
        private readonly List<ExecutionCardInstance> _executedCards = new();

        private ResolutionContext(FutureZone zone)
        {
            _zone = zone;
        }

        /// <summary>현재 실행선(실행 순서 그대로, 살아 있는 뷰).</summary>
        public IReadOnlyList<ExecutionCardInstance> Order => _zone.Cards;

        /// <summary>이번 턴에 차례가 온 카드들, 실행 순서대로.</summary>
        public IReadOnlyList<ExecutionCardInstance> ExecutedCards => _executedCards;

        /// <summary>The most recently executed card of either side, or null before any card has
        /// executed this turn.</summary>
        public ExecutionCardInstance LastExecutedCard
            => _executedCards.Count > 0 ? _executedCards[^1] : null;

        /// <summary>The most recently executed player-side card, or null if none has executed yet.</summary>
        public ExecutionCardInstance LastExecutedPlayerCard
        {
            get
            {
                for (int i = _executedCards.Count - 1; i >= 0; i--)
                {
                    if (_executedCards[i].Def.Side == Side.Player)
                    {
                        return _executedCards[i];
                    }
                }

                return null;
            }
        }

        public static ResolutionContext From(CombatState state)
            => new ResolutionContext(state.Zone);

        public int IndexOf(ExecutionCardInstance card)
        {
            var order = Order;
            for (int i = 0; i < order.Count; i++)
            {
                if (ReferenceEquals(order[i], card))
                {
                    return i;
                }
            }

            return -1;
        }

        public ExecutionCardInstance CardAt(int index)
        {
            var order = Order;
            return index >= 0 && index < order.Count ? order[index] : null;
        }

        /// <summary>카드의 차례가 끝났음을 이력에 남긴다. TurnResolver가 그 카드의 조건을 모두 읽은 뒤,
        /// 효과·취소 여부와 무관하게 실행 순서대로 부른다.</summary>
        public void MarkExecuted(ExecutionCardInstance card) => _executedCards.Add(card);
    }
}
