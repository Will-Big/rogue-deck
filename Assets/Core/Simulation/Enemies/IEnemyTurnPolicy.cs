using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Decides which enemy cards land on the future zone each turn — the seam that lets an enemy be
    /// scripted, a random moveset, a shuffle bag, or (someday) deck-backed, without touching the combat loop.
    ///
    /// Contract: the combat loop calls <see cref="CardsForTurn"/> exactly once per turn, in increasing turn
    /// order, passing the combat's single seeded RNG (CombatState.Rng — AGENTS.md rule 7). Implementations
    /// must draw all randomness from that RNG (never their own) so the whole run replays from one seed;
    /// scripted policies may ignore it.</summary>
    public interface IEnemyTurnPolicy
    {
        IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng);
    }
}
