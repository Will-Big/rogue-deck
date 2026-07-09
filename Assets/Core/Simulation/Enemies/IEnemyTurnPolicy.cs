using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Decides which enemy cards land on the future zone each turn — the seam that lets an enemy be
    /// scripted, a random moveset, a shuffle bag, or (someday) deck-backed, without touching the combat loop.
    ///
    /// Contract: the combat loop calls <see cref="CardsForTurn"/> exactly once per turn, in increasing turn
    /// order. Implementations must be deterministic given their seed; they may be pure functions of the turn
    /// index (idempotent) or stateful across turns (advancing per call) as long as that order is respected.</summary>
    public interface IEnemyTurnPolicy
    {
        IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex);
    }
}
