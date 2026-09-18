using System.Linq;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Combat
{
    /// <summary>전투 승패(전투 실행 계약 스펙 §2·§8). 카드 하나 또는 턴 시점 묶음이 끝났을 때만 부른다.
    /// 아군 전멸이면 적 전멸이어도 패배다(패배 우선).</summary>
    public static class CombatOutcomeEvaluator
    {
        public static Outcome Evaluate(CombatState state)
        {
            if (state.Party.All(member => !member.IsAlive)) return Outcome.Lose;
            if (state.Enemies.All(enemy => enemy.Hp <= 0)) return Outcome.Win;
            return Outcome.Ongoing;
        }
    }
}
