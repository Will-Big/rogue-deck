namespace FateWeaver.Core.Status
{
    /// <summary>상태 배율의 기본값. PartyTuning.Prototype과 같은 역할이며, 튜닝 수치가 계산식이
    /// 아니라 명명된 한 곳에 모이게 한다 (AGENTS.md 규칙 8). 저작 데이터에서 값을 주입하게 되면
    /// 이 카탈로그가 그 기본값 출처가 된다.</summary>
    public static class StatusRuleCatalog
    {
        public const int VulnerableIncomingPercent = 150;
        public const int WeakOutgoingPercent = 75;

        public static StatusRuleSet Default()
        {
            var rules = new StatusRuleSet();
            rules.Set(StatusKeys.Vulnerable, new StatusRule { MultiplierPercent = VulnerableIncomingPercent });
            rules.Set(StatusKeys.Weak, new StatusRule { MultiplierPercent = WeakOutgoingPercent });
            return rules;
        }
    }
}
