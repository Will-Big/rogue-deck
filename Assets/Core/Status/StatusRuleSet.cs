using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>상태별 배율 보관소. 백분율 배율을 쓰는 상태만 여기 실린다 — 2026-08-28 기준
    /// 취약 150·약화 75·손상 75 셋이고, 독의 성장량이나 가속·감속의 순서 증감은 여기가 아니라
    /// 카탈로그의 상태별 접근자에서 나온다. 등록되지 않은 키는 중립 배율을 돌려준다.
    ///
    /// <para><b>CombatState가 보유하지 않는다 (2026-08-28 정정).</b> 실제 소유자는
    /// <c>StatusContentCatalog</c>이고 <c>CombatState.StatusRules</c>는 그것을 그대로 통과시키는
    /// 위임일 뿐이다. 카탈로그는 전투 화면 세션만큼 살고 전투 재시작에도 재사용되므로, <c>Set</c>으로
    /// 쓴 값은 전투 경계를 넘는다. 이전 주석은 "전투 단위라 시드·스냅샷 경계를 넘지 않는다"고
    /// 적혀 있었으나 사실이 아니었다.
    ///
    /// 가변인 것은 의도된 설계다(유물 등이 수치를 바꾼다). 다만 지금은 <b>그 변경이 지워질 시점이
    /// 없다</b> — 전투 하나 또는 런 하나만큼 사는 그릇이 아직 없다. 프로덕션에서 <c>Set</c>을
    /// 부르는 코드는 아직 없고, 테스트만 쓰며 그쪽은 호출마다 카탈로그를 새로 만들어 격리한다.
    /// README 후속 작업 대기열의 "런 층·전투 층" 항목 참고.</para></summary>
    public sealed class StatusRuleSet
    {
        private static readonly StatusRule Neutral = new StatusRule();

        private readonly Dictionary<StatusKey, StatusRule> _rules = new();

        public void Set(StatusKey key, StatusRule rule) => _rules[key] = rule;

        public StatusRule For(StatusKey key)
            => _rules.TryGetValue(key, out var rule) ? rule : Neutral;
    }
}
