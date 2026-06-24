using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Scripted enemy telegraph policy: a fixed, pre-defined list of enemy cards per turn.
    /// Turns past the end clamp to the last defined turn. Ideal for tutorials/set-piece fights and tests.</summary>
    public sealed class EnemyIntent : IEnemyTurnPolicy
    {
        private readonly IReadOnlyList<IReadOnlyList<CardDefinition>> _turns;

        public EnemyIntent(IReadOnlyList<IReadOnlyList<CardDefinition>> turns)
        {
            _turns = turns ?? Array.Empty<IReadOnlyList<CardDefinition>>();
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex) => ForTurn(turnIndex);

        public IReadOnlyList<CardDefinition> ForTurn(int turnIndex)
        {
            if (_turns.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            var index = turnIndex < 0 ? 0 : Math.Min(turnIndex, _turns.Count - 1);
            return _turns[index];
        }
    }
}
