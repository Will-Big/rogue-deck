using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Core.Combat
{
    /// <summary>Ordered set of action cards for one turn.</summary>
    public sealed class FutureZone
    {
        private readonly List<ActionCardInstance> _cards = new();

        public IReadOnlyList<ActionCardInstance> Cards => _cards;

        public void Add(ActionCardInstance card) => _cards.Add(card);

        /// <summary>Ascending initiative, stable on ties (LINQ OrderBy is a stable sort).</summary>
        public IReadOnlyList<ActionCardInstance> ResolutionOrder()
            => _cards.OrderBy(c => c.Initiative).ToList();
    }
}
