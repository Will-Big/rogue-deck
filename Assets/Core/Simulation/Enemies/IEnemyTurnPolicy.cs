using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Decides which enemy cards land on the future zone each turn — the seam that lets an enemy
    /// pick its bundle at random, without replacement, or from a fixed script, without touching the combat
    /// loop. 선택 단위는 낱장이 아니라 EnemyCardBundle이며, 묶음 자체는 이 경계를 넘지 않는다 —
    /// 돌려주는 것은 고른 묶음의 카드 목록이다.
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
