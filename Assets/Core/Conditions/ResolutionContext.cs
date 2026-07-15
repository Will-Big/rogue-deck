using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Conditions
{
    /// <summary>Frozen, ordered view of the cards resolving this turn (ascending executionOrder).
    /// Conditions and effect handlers query position/adjacency against this snapshot.
    /// Also tracks which cards actually finished resolution (CardResolved, not cancelled) as the
    /// turn progresses, so "previous executed card" conditions can skip cancelled cards. Cards are
    /// only ever appended, in resolution order, via <see cref="MarkExecuted"/>.</summary>
    public sealed class ResolutionContext
    {
        private readonly IReadOnlyList<ExecutionCardInstance> _order;
        private readonly List<ExecutionCardInstance> _executedCards = new();

        private ResolutionContext(IReadOnlyList<ExecutionCardInstance> order)
        {
            _order = order;
        }

        public IReadOnlyList<ExecutionCardInstance> Order => _order;

        /// <summary>Cards that emitted CardResolved so far this turn, in resolution order. Excludes
        /// cancelled cards (OwnerDied / NoValidTarget / StatusIntercepted).</summary>
        public IReadOnlyList<ExecutionCardInstance> ExecutedCards => _executedCards;

        /// <summary>The most recently resolved card of either side, or null before any card has
        /// resolved this turn.</summary>
        public ExecutionCardInstance LastExecutedCard
            => _executedCards.Count > 0 ? _executedCards[^1] : null;

        /// <summary>The most recently resolved player-side card, or null if none has resolved yet.</summary>
        public ExecutionCardInstance LastExecutedPlayerCard
        {
            get
            {
                for (int i = _executedCards.Count - 1; i >= 0; i--)
                {
                    if (_executedCards[i].Def.Side == Side.Player)
                    {
                        return _executedCards[i];
                    }
                }

                return null;
            }
        }

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

        /// <summary>Records that a card finished resolution (emitted CardResolved). Called by
        /// TurnResolver only for non-cancelled cards, in resolution order.</summary>
        public void MarkExecuted(ExecutionCardInstance card) => _executedCards.Add(card);
    }
}
