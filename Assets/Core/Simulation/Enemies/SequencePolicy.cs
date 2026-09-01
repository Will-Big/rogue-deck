using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>정해진 순서대로 묶음을 하나씩 전개한다. 턴 번호로 색인하며, 목록 끝을 넘어선 턴은
    /// 마지막 묶음에 고정된다. 대본이 있는 전투(보스·튜토리얼)와 테스트가 쓴다.
    ///
    /// RNG를 소비하지 않는다 — 같은 턴 번호면 항상 같은 묶음이다.</summary>
    public sealed class SequencePolicy : IEnemyTurnPolicy
    {
        private readonly IReadOnlyList<EnemyCardBundle> _bundles;

        public SequencePolicy(IReadOnlyList<EnemyCardBundle> bundles)
        {
            _bundles = bundles ?? Array.Empty<EnemyCardBundle>();
        }

        /// <summary>턴별 카드 목록으로 바로 짓는 편의 생성자 — 안쪽 목록 하나가 묶음 하나다.
        /// "이 턴에 함께 깔릴 카드들"이 곧 묶음의 정의이므로 변환이 아니라 같은 것의 다른 표기다.</summary>
        public SequencePolicy(IReadOnlyList<IReadOnlyList<CardDefinition>> turns)
            : this(ToBundles(turns))
        {
        }

        private static IReadOnlyList<EnemyCardBundle> ToBundles(
            IReadOnlyList<IReadOnlyList<CardDefinition>> turns)
        {
            if (turns == null)
            {
                return Array.Empty<EnemyCardBundle>();
            }

            var bundles = new EnemyCardBundle[turns.Count];
            for (int i = 0; i < turns.Count; i++)
            {
                bundles[i] = new EnemyCardBundle(turns[i]);
            }

            return bundles;
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng)
            => ForTurn(turnIndex);

        public IReadOnlyList<CardDefinition> ForTurn(int turnIndex)
        {
            if (_bundles.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            var index = turnIndex < 0 ? 0 : Math.Min(turnIndex, _bundles.Count - 1);
            return _bundles[index].Cards;
        }
    }
}
