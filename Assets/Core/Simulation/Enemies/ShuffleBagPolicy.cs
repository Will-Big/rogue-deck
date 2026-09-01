using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>묶음을 비복원으로 하나씩 낸다. 가방이 비면 전체 목록을 다시 섞어 새 가방을 만든다.
    /// 복원 추출인 RandomPickPolicy와 달리 한 바퀴 안에서는 같은 묶음이 두 번 나오지 않는다.
    ///
    /// 셔플은 CardsForTurn에 넘어온 전투 RNG에서만 나온다(AGENTS.md 규칙 7).</summary>
    public sealed class ShuffleBagPolicy : IEnemyTurnPolicy
    {
        private readonly IReadOnlyList<EnemyCardBundle> _bundles;
        private List<EnemyCardBundle> _bag = new List<EnemyCardBundle>();

        public ShuffleBagPolicy(IReadOnlyList<EnemyCardBundle> bundles)
        {
            _bundles = bundles ?? Array.Empty<EnemyCardBundle>();
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng)
        {
            if (_bundles.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            if (_bag.Count == 0)
            {
                _bag = Shuffled(rng);
            }

            var drawn = _bag[0];
            _bag.RemoveAt(0);
            return drawn.Cards;
        }

        private List<EnemyCardBundle> Shuffled(Random rng)
        {
            var bundles = new List<EnemyCardBundle>(_bundles);
            for (int i = bundles.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = bundles[i];
                bundles[i] = bundles[j];
                bundles[j] = tmp;
            }

            return bundles;
        }
    }
}
