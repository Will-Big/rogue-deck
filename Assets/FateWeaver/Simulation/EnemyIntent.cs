using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Deterministic enemy telegraph: the enemy action cards placed on the future zone each turn.
    /// Turns past the end clamp to the last defined turn. (Real enemy AI is a later phase.)</summary>
    public sealed class EnemyIntent
    {
        private readonly IReadOnlyList<IReadOnlyList<CardDefinition>> _turns;

        public EnemyIntent(IReadOnlyList<IReadOnlyList<CardDefinition>> turns)
        {
            _turns = turns ?? Array.Empty<IReadOnlyList<CardDefinition>>();
        }

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
