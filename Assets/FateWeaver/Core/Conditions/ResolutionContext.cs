using System.Collections.Generic;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Conditions
{
    /// <summary>Frozen, ordered view of the cards resolving this turn (ascending initiative).
    /// Conditions and effect handlers query position/adjacency against this snapshot.</summary>
    public sealed class ResolutionContext
    {
        private readonly IReadOnlyList<ExecutionCardInstance> _order;

        private ResolutionContext(IReadOnlyList<ExecutionCardInstance> order)
        {
            _order = order;
        }

        public IReadOnlyList<ExecutionCardInstance> Order => _order;

        public static ResolutionContext From(CombatState state)
            => new ResolutionContext(state.Zone.ResolutionOrder());

        public int IndexOf(ExecutionCardInstance card)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                if (ReferenceEquals(_order[i], card))
                {
                    return i;
                }
            }

            return -1;
        }

        public ExecutionCardInstance CardAt(int index)
            => index >= 0 && index < _order.Count ? _order[index] : null;
    }
}
