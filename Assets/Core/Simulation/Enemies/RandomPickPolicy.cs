using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>매 턴 묶음 목록에서 하나를 무작위로 골라 통째로 전개한다. 복원 추출이라 같은 묶음이
    /// 연달아 나올 수 있다 — 턴 간 기억이 필요하면 ShuffleBagPolicy를 쓴다.
    ///
    /// 무작위는 CardsForTurn에 넘어온 전투 RNG에서만 나온다(AGENTS.md 규칙 7). 소비는 턴당 정확히
    /// 한 번이다.</summary>
    public sealed class RandomPickPolicy : IEnemyTurnPolicy
    {
        private readonly IReadOnlyList<EnemyCardBundle> _bundles;

        public RandomPickPolicy(IReadOnlyList<EnemyCardBundle> bundles)
        {
            _bundles = bundles ?? Array.Empty<EnemyCardBundle>();
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng)
        {
            if (_bundles.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            return _bundles[rng.Next(_bundles.Count)].Cards;
        }
    }
}
