using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Enemies;

namespace FateWeaver.Simulation.Run
{
    /// <summary>편성 스트림을 받아 이번 전투의 적마다 (적, 정책) 쌍을 새로 만든다. 정책은 상태를
    /// 가지므로(ShuffleBagPolicy) 매 호출 새 인스턴스를 돌려줘야 한다.</summary>
    public interface IEncounterSource
    {
        EncounterSetup Pick(Random encounterRng);
    }

    /// <summary>적 하나와 그 적의 정책. 편성 전체에 정책 하나를 두면 "이 카드가 어느 적 것인가"를
    /// 말할 수단이 없어진다 — 지금 세션이 적 둘 이상에서 카드 주인을 비우는 원인이 그것이다
    /// (DeckCombatSession.BeginTurn). 세션이 쌍 목록을 받게 되는 것은 필수 후속 작업이다.</summary>
    public sealed class EncounterEnemy
    {
        public EncounterEnemy(Enemy enemy, IEnemyTurnPolicy policy)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public Enemy Enemy { get; }
        public IEnemyTurnPolicy Policy { get; }
    }

    public sealed class EncounterSetup
    {
        public EncounterSetup(IReadOnlyList<EncounterEnemy> enemies)
        {
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        }

        public IReadOnlyList<EncounterEnemy> Enemies { get; }
    }
}
