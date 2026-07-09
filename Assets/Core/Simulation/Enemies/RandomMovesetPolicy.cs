using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Each turn telegraphs a variable number of distinct cards (a random count in [minCards, maxCards],
    /// capped by the catalog size) drawn from a fixed catalog. Deterministic per (seed, turnIndex) — the turn's
    /// picks are a pure function of the turn, so re-asking for the same turn yields the same cards.</summary>
    public sealed class RandomMovesetPolicy : IEnemyTurnPolicy
    {
        private readonly IReadOnlyList<CardDefinition> _catalog;
        private readonly int _minCards;
        private readonly int _maxCards;
        private readonly int _seed;

        public RandomMovesetPolicy(IReadOnlyList<CardDefinition> catalog, int minCards, int maxCards, int seed)
        {
            _catalog = catalog ?? Array.Empty<CardDefinition>();
            _minCards = Math.Max(0, minCards);
            _maxCards = Math.Max(_minCards, maxCards);
            _seed = seed;
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex)
        {
            if (_catalog.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            // Derive a per-turn RNG so the result depends only on (seed, turnIndex), never on call order.
            var rng = new Random(unchecked(_seed * 1000003 + turnIndex));
            int max = Math.Min(_maxCards, _catalog.Count);
            int min = Math.Min(_minCards, max);
            int count = rng.Next(min, max + 1);

            var pool = Enumerable.Range(0, _catalog.Count).ToList();
            var picks = new List<CardDefinition>(count);
            for (int k = 0; k < count; k++)
            {
                int idx = rng.Next(pool.Count);
                picks.Add(_catalog[pool[idx]]);
                pool.RemoveAt(idx);
            }

            return picks;
        }
    }
}
