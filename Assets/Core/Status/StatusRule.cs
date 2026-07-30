namespace FateWeaver.Core.Status
{
    /// <summary>상태 하나의 배율 파라미터. count(지속·수치)와 독립이며, 런 중 유물 같은 효과로
    /// 바뀔 수 있으므로 계산식에 박지 않고 여기에 둔다. 정수 퍼센트로 표현해 결정론과 버림을
    /// 함께 지킨다.</summary>
    public sealed class StatusRule
    {
        public const int NeutralPercent = 100;

        /// <summary>100 = 변화 없음. 취약 150(받는 피해 +50%), 약화·손상 75(-25%).</summary>
        public int MultiplierPercent { get; set; } = NeutralPercent;

        /// <summary>배율을 적용하고 버린다.</summary>
        public int Apply(int value) => (value * MultiplierPercent) / NeutralPercent;
    }
}
