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
    /// 번호 구간 안은 플레이어 자리들 뒤에 적 자리들이 온다. 새 카드는 자기 진영 자리들의 끝에 들어간다.
    /// 카드가 서 있는 자리의 진영은 카드의 진영과 다를 수 있다 — 교환된 두 카드는 서로의 자리(번호·위치·
    /// 자리 진영)를 그대로 물려받고, 뒤에 오는 카드는 교환을 모르는 것처럼 자리를 찾는다(2026-09-18 사용자 결정).
    /// 예: 5번 [P, E]를 교환하면 [E(플레이어 자리), P(적 자리)], 여기에 5번 P2가 오면 [E, P2, P].
    ///
    /// 카드가 실행선에 들어간 뒤 ExecutionOrder를 직접 고치면 이 불변식이 깨진다. 번호 변경은
    /// <see cref="MoveTo"/>·<see cref="SwapPositions"/>로만 한다.</summary>
    public sealed class FutureZone
    {
        private readonly List<ExecutionCardInstance> _cards = new();

        /// <summary>카드가 서 있는 자리의 진영. 넣거나 옮길 때는 카드의 진영이고, 교환할 때만 맞바뀐다.</summary>
        private readonly Dictionary<ExecutionCardInstance, Side> _slotSides = new();

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
            _slotSides[card] = card.Def.Side;
        }

        public bool Contains(ExecutionCardInstance card) => _cards.Contains(card);

        /// <summary>Empties the zone (used when rebuilding it for a new turn).</summary>
        public void Clear()
        {
            _cards.Clear();
            _slotSides.Clear();
        }

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

        /// <summary>카드를 다른 번호로 옮긴다. 목적 번호 구간에는 새 카드와 같은 규칙으로, 카드 자신의 진영
        /// 자리에 들어간다. 같은 번호로의 이동은 자리를 바꾸지 않는다.</summary>
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
            _slotSides[card] = card.Def.Side;
        }

        /// <summary>두 카드의 자리·번호·자리 진영을 함께 바꾼다. 진영 우선 규칙을 다시 적용하지 않는다.</summary>
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
            (_slotSides[first], _slotSides[second]) = (_slotSides[second], _slotSides[first]);
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
                _slotSides.Remove(card);
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

            // 구간 안은 플레이어 자리들 뒤에 적 자리들이다. 새 플레이어는 플레이어 자리들의 끝에 선다.
            var afterPlayerSlots = start;
            while (afterPlayerSlots < _cards.Count
                && _cards[afterPlayerSlots].ExecutionOrder == executionOrder
                && _slotSides[_cards[afterPlayerSlots]] == Side.Player)
            {
                afterPlayerSlots++;
            }

            if (side == Side.Player)
            {
                return afterPlayerSlots;
            }

            var end = afterPlayerSlots;
            while (end < _cards.Count && _cards[end].ExecutionOrder == executionOrder)
            {
                end++;
            }

            return end;
        }
    }
}
