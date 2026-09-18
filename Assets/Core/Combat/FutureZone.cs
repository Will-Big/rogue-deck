using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>한 턴의 실행선. 목록의 순서가 곧 실행 순서이며, 조회할 때 다시 정렬하지 않는다
    /// (전투 실행 계약 스펙 §6). 번호는 항상 오름차순으로 유지된다 — 새 카드는 번호 구간 안의 정해진
    /// 자리에 들어가고, 이동은 빼서 다시 넣으며, 교환은 자리와 번호를 함께 바꾼다.
    ///
    /// 번호 구간 안의 자리: 새 플레이어 카드는 그 구간의 마지막 플레이어 카드 뒤(없으면 구간 맨 앞),
    /// 새 적 카드는 구간 맨 뒤. 교환으로 적이 플레이어 앞에 온 구간도 다시 정렬하지 않는다.
    ///
    /// 카드가 실행선에 들어간 뒤 ExecutionOrder를 직접 고치면 이 불변식이 깨진다. 번호 변경은
    /// <see cref="MoveTo"/>·<see cref="SwapPositions"/>로만 한다.</summary>
    public sealed class FutureZone
    {
        private readonly List<ExecutionCardInstance> _cards = new();

        /// <summary>현재 실행 순서 그대로의 목록(살아 있는 뷰).</summary>
        public IReadOnlyList<ExecutionCardInstance> Cards => _cards;

        public void Add(ExecutionCardInstance card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (Contains(card))
            {
                throw new ArgumentException("The card is already on the execution line.", nameof(card));
            }

            _cards.Insert(InsertionIndex(card.Def.Side, card.ExecutionOrder), card);
        }

        public bool Contains(ExecutionCardInstance card) => _cards.Contains(card);

        /// <summary>Empties the zone (used when rebuilding it for a new turn).</summary>
        public void Clear() => _cards.Clear();

        /// <summary>현재 실행 순서의 사본. 호출자가 들고 있는 동안 실행선이 바뀌어도 변하지 않는다.</summary>
        public IReadOnlyList<ExecutionCardInstance> ResolutionOrder() => _cards.ToList();

        /// <summary>후보를 지금 넣으면 들어갈 자리. <see cref="Add"/>와 같은 계산을 쓴다.</summary>
        public int PreviewInsertionIndex(ExecutionCardInstance candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            return InsertionIndex(candidate.Def.Side, candidate.ExecutionOrder);
        }

        /// <summary>카드를 다른 번호로 옮긴다. 목적 번호 구간에는 새 카드와 같은 규칙으로 들어간다.
        /// 같은 번호로의 이동은 자리를 바꾸지 않는다.</summary>
        public void MoveTo(ExecutionCardInstance card, int executionOrder)
        {
            var index = IndexOrThrow(card, nameof(card));
            if (card.ExecutionOrder == executionOrder)
            {
                return;
            }

            _cards.RemoveAt(index);
            card.ExecutionOrder = executionOrder;
            _cards.Insert(InsertionIndex(card.Def.Side, executionOrder), card);
        }

        /// <summary>두 카드의 자리와 번호를 함께 바꾼다. 진영 우선 규칙을 다시 적용하지 않는다.</summary>
        public void SwapPositions(ExecutionCardInstance first, ExecutionCardInstance second)
        {
            if (ReferenceEquals(first, second))
            {
                throw new ArgumentException("A card cannot be swapped with itself.", nameof(second));
            }

            var firstIndex = IndexOrThrow(first, nameof(first));
            var secondIndex = IndexOrThrow(second, nameof(second));

            (_cards[firstIndex], _cards[secondIndex]) = (_cards[secondIndex], _cards[firstIndex]);
            (first.ExecutionOrder, second.ExecutionOrder) = (second.ExecutionOrder, first.ExecutionOrder);
        }

        /// <summary>주인이 죽었을 때 그 주인의 아직 차례가 오지 않은 카드만 실행선에서 뺀다. 실행 중이거나
        /// 이미 실행된 카드는 이번 턴에 남는다(스펙 §6). 뺀 카드를 실행 순서대로 돌려준다.</summary>
        public IReadOnlyList<ExecutionCardInstance> RemovePendingOwnedBy(string ownerId)
        {
            var removed = new List<ExecutionCardInstance>();
            if (string.IsNullOrEmpty(ownerId))
            {
                return removed;
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.OwnerId == ownerId && card.ExecutionState == CardExecutionState.Pending)
                {
                    removed.Add(card);
                }
            }

            foreach (var card in removed)
            {
                _cards.Remove(card);
                card.ExecutionState = CardExecutionState.Removed;
            }

            return removed;
        }

        /// <summary>아직 차례가 오지 않은 첫 카드. 없으면 null.</summary>
        public ExecutionCardInstance NextPending()
        {
            foreach (var card in _cards)
            {
                if (card.ExecutionState == CardExecutionState.Pending)
                {
                    return card;
                }
            }

            return null;
        }

        private int IndexOrThrow(ExecutionCardInstance card, string parameterName)
        {
            var index = card == null ? -1 : _cards.IndexOf(card);
            if (index < 0)
            {
                throw new ArgumentException("The card is not on the execution line.", parameterName);
            }

            return index;
        }

        private int InsertionIndex(Side side, int executionOrder)
        {
            var start = 0;
            while (start < _cards.Count && _cards[start].ExecutionOrder < executionOrder)
            {
                start++;
            }

            var end = start;
            var afterLastPlayer = -1;
            while (end < _cards.Count && _cards[end].ExecutionOrder == executionOrder)
            {
                if (_cards[end].Def.Side == Side.Player)
                {
                    afterLastPlayer = end + 1;
                }

                end++;
            }

            if (side == Side.Player)
            {
                return afterLastPlayer >= 0 ? afterLastPlayer : start;
            }

            return end;
        }
    }
}
