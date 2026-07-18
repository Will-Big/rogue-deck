using System;
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
            => Ordered(_cards).ToList();

        public int PreviewInsertionIndex(ExecutionCardInstance candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            return Ordered(_cards.Concat(new[] { candidate })).ToList().IndexOf(candidate);
        }

        private static IOrderedEnumerable<ExecutionCardInstance> Ordered(
            IEnumerable<ExecutionCardInstance> cards)
            => cards
                .OrderBy(card => card.ExecutionOrder)
                .ThenBy(card => card.Def.Side == Side.Player ? 0 : 1);
    }
}
