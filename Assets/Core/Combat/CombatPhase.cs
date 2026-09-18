namespace FateWeaver.Core.Combat
{
    /// <summary>한 턴의 고정 실행 단계(전투 실행 계약 스펙 §8): 턴 준비(공통 만료·비용 초기화) → 턴 시작 상태 →
    /// 계획(적 배치·드로우·플레이어 행동) → 실행선 처리 → 턴 종료 상태 → 턴 정리. 개별 능력의 발동 사건은
    /// CombatSignalKey로 확장하고 여기에 능력 이름을 더하지 않는다.</summary>
    public enum CombatPhase
    {
        Prepare,
        TurnStart,
        Planning,
        Resolve,
        TurnEnd,
        Cleanup
    }
}
