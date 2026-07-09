using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Ordered set of execution cards for one turn.</summary>
    public sealed class FutureZone
    {
        private readonly List<ExecutionCardInstance> _cards = new();

        public IReadOnlyList<ExecutionCardInstance> Cards => _cards;

        public void Add(ExecutionCardInstance card) => _cards.Add(card);

        /// <summary>Empties the zone (used when rebuilding it for a new turn).</summary>
        public void Clear() => _cards.Clear();

        /// <summary>Ascending executionOrder, with player cards before enemy cards on ties.</summary>
        public IReadOnlyList<ExecutionCardInstance> ResolutionOrder()
            => _cards
                .OrderBy(c => c.ExecutionOrder)
                .ThenBy(c => c.Def.Side == Side.Player ? 0 : 1)
                .ToList();
    }
}
