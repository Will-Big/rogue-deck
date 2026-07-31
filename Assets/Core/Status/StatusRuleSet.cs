using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>상태별 규칙 보관소. 전투 단위로 CombatState가 보유하므로 전투 중 변경이 시드·
    /// 스냅샷 경계를 넘지 않는다 (AGENTS.md 규칙 7). 런 지속 변경(유물 등)은 전투 시작 시
    /// 이 값을 시딩해 반영한다. 등록되지 않은 키는 중립 배율을 돌려준다.</summary>
    public sealed class StatusRuleSet
    {
        private static readonly StatusRule Neutral = new StatusRule();

        private readonly Dictionary<StatusKey, StatusRule> _rules = new();

        public void Set(StatusKey key, StatusRule rule) => _rules[key] = rule;

        public StatusRule For(StatusKey key)
            => _rules.TryGetValue(key, out var rule) ? rule : Neutral;
    }
}
