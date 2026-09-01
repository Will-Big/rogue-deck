using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>적의 선택 단위. 한 턴에 함께 미래 존에 전개될 카드들을 담는다. 카드 한 장짜리도
    /// 묶음으로 만든다 — 선택 단위가 하나뿐이라야 정책이 낱장과 묶음을 갈라 다루지 않는다.
    ///
    /// 묶음은 "함께 등장한다"는 뜻이지 "붙어서 실행된다"는 뜻이 아니다. 존에서의 순서는 각 카드의
    /// BaseExecutionOrder가 정하며 플레이어의 개입 카드가 그 사이를 가를 수 있다. 다만 존의 정렬이
    /// 안정적이라(FutureZone.Ordered) 실행 순서가 같은 카드끼리는 여기 적은 순서를 유지한다.</summary>
    public sealed class EnemyCardBundle
    {
        public IReadOnlyList<CardDefinition> Cards { get; }

        public EnemyCardBundle(IReadOnlyList<CardDefinition> cards)
        {
            Cards = cards ?? Array.Empty<CardDefinition>();
        }

        public EnemyCardBundle(params CardDefinition[] cards)
            : this((IReadOnlyList<CardDefinition>)(cards ?? Array.Empty<CardDefinition>()))
        {
        }

        public int Count => Cards.Count;
    }
}
