namespace FateWeaver.Core.Events
{
    /// <summary>규칙 수행용 전투 사실(전투 실행 계약 스펙 §7). 표시용 ResolutionEvent와 다르다 — 반응 능력이
    /// 이것을 받는다. 값은 사건 시점에 고정된다.
    /// SourceId는 원인 개체(공격한 쪽 등, 없으면 null), TargetId는 사건 당사자(반응 후보를 찾는 보유자)다.
    /// TargetOrdinal·Sequence는 효과 실행기가 붙인다: 효과 시작 대상 목록에서의 순번(목록 밖이면 그 뒤)과
    /// 경계 안 발생 순서. 반응은 이 두 값의 순서로 처리한다.</summary>
    public sealed record CombatSignal(CombatSignalKey Key, string SourceId, string TargetId, int Amount)
    {
        /// <summary>이 사건을 낸 효과의 기원. 반응 능력이 발동 조건으로 읽는다 — 예: 공격에 대한 반응은 반응 공격이
        /// 낸 공격(Origin = Reaction)에는 발동하지 않는다. 기원으로 사건 전체를 막는 공통 규칙은 없다(계획 D11).</summary>
        public Effects.EffectOrigin Origin { get; init; }

        public int TargetOrdinal { get; init; }
        public int Sequence { get; init; }

        /// <summary>사건 종류별 보조 값(StatusGained의 상태 id 등). 없으면 null.</summary>
        public string Detail { get; init; }
    }
}
