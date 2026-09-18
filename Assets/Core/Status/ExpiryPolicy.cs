using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Status
{
    /// <summary>상태가 언제 사라지는가의 방식.</summary>
    public enum ExpiryMode
    {
        /// <summary>시점 방문으로 사라지지 않는다.</summary>
        Permanent,

        /// <summary>기준 시점(Phase)을 RemainingVisits번 방문하면 사라진다.</summary>
        PhaseVisits,

        /// <summary>능력이 발동해 횟수를 다 쓰면 사라진다(StatusBag.Consume).</summary>
        UntilConsumed
    }

    /// <summary>상태의 만료 정책(전투 실행 계약 스펙 §8, 계획 T6). 상태 이름이 아니라 이 정책만으로 만료를 판단한다.
    /// 구형 수명은 이렇게 옮겨진다: ThisTurn = Cleanup 1회, Turns(n) = Cleanup n회, UntilConsumed(n) = 소모 n회.
    /// 방어는 상태 JSON이 Prepare 1회(다음 턴 준비에 만료)를 적는다. Phase·RemainingVisits는 PhaseVisits에서만 뜻이 있다.</summary>
    public sealed record ExpiryPolicy(ExpiryMode Mode, CombatPhase Phase, int RemainingVisits)
    {
        public static readonly ExpiryPolicy Permanent = new ExpiryPolicy(ExpiryMode.Permanent, CombatPhase.Cleanup, 0);

        public static ExpiryPolicy PhaseVisits(CombatPhase phase, int visits)
            => new ExpiryPolicy(ExpiryMode.PhaseVisits, phase, visits);

        public static ExpiryPolicy UntilConsumed(int charges)
            => new ExpiryPolicy(ExpiryMode.UntilConsumed, CombatPhase.Cleanup, charges);
    }
}
