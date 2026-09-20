using System;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Enemies;

namespace FateWeaver.Simulation
{
    /// <summary>적 하나와 그 적의 정책. 편성 전체에 정책 하나를 두면 "이 카드가 어느 적 것인가"를
    /// 말할 수단이 없어지므로 세션은 이 쌍의 목록을 받는다. 정책이 없는 적(null)은 카드를 내지 않는
    /// 대상 전용 적이다 — 플레이어 HP 기반 단일 행위자 경로가 쓴다.</summary>
    public sealed class EncounterEnemy
    {
        public EncounterEnemy(Enemy enemy, IEnemyTurnPolicy policy)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        private EncounterEnemy(Enemy enemy)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        }

        public Enemy Enemy { get; }
        public IEnemyTurnPolicy Policy { get; }

        /// <summary>카드를 내지 않는 적. 단일 행위자 경로에서 두 번째 이후의 적이 여기 해당한다.</summary>
        public static EncounterEnemy Passive(Enemy enemy) => new EncounterEnemy(enemy);
    }
}
