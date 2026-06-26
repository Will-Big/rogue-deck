using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Ordered set of action cards for one turn.</summary>
    public sealed class FutureZone
    {
        private readonly List<ActionCardInstance> _cards = new();

        public IReadOnlyList<ActionCardInstance> Cards => _cards;

        public void Add(ActionCardInstance card) => _cards.Add(card);

        /// <summary>Empties the zone (used when rebuilding it for a new turn).</summary>
        public void Clear() => _cards.Clear();

        /// <summary>Ascending initiative, with player cards before enemy cards on ties.</summary>
        public IReadOnlyList<ActionCardInstance> ResolutionOrder()
            => _cards
                .OrderBy(c => c.Initiative)
                .ThenBy(c => c.Def.Side == Side.Player ? 0 : 1)
                .ToList();
    }
}
