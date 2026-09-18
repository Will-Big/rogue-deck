using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Status
{
    public enum StatusLifetimeKind
    {
        Permanent,
        ThisTurn,
        Turns,
        UntilConsumed,

        /// <summary>저작된 만료 정책: 기준 시점을 정해진 횟수만큼 방문하면 사라진다(예: 방어 = 다음 턴 준비).</summary>
        PhaseVisits
    }

    /// <summary>상태를 부여할 때의 수명. 어떤 수명을 쓰는지는 카드가 아니라 상태 저작 콘텐츠가 정한다
    /// (StatusContentCatalog.LifetimeFor, Content/Statuses/*.json의 lifetime 또는 expiry). 카드는 수치 하나만 준다 —
    /// Turns·UntilConsumed면 그 수치가 Count(지속·횟수)가 된다.
    /// 만료 판단은 Expiry(공통 정책)로 한다: ThisTurn = Cleanup 1회, Turns(n) = Cleanup n회, PhaseVisits = 저작된 시점·횟수.</summary>
    public readonly struct StatusLifetime
    {
        public StatusLifetimeKind Kind { get; }
        public int Count { get; }

        /// <summary>PhaseVisits의 기준 시점(그 밖의 종류에서는 뜻이 없다).</summary>
        public CombatPhase Phase { get; }

        private StatusLifetime(StatusLifetimeKind kind, int count, CombatPhase phase = CombatPhase.Cleanup)
        {
            Kind = kind;
            Count = count;
            Phase = phase;
        }

        public static readonly StatusLifetime Permanent = new StatusLifetime(StatusLifetimeKind.Permanent, 0);
        public static readonly StatusLifetime ThisTurn = new StatusLifetime(StatusLifetimeKind.ThisTurn, 0);
        public static StatusLifetime Turns(int turns) => new StatusLifetime(StatusLifetimeKind.Turns, turns);
        public static StatusLifetime UntilConsumed(int charges = 1) => new StatusLifetime(StatusLifetimeKind.UntilConsumed, charges);

        /// <summary>카탈로그가 읽은 수명 종류로 조립한다. count는 Turns·UntilConsumed에서만 의미가 있다.
        /// PhaseVisits는 시점이 필요하므로 Of(ExpiryPolicy)를 쓴다.</summary>
        public static StatusLifetime Of(StatusLifetimeKind kind, int count) => new StatusLifetime(kind, count);

        /// <summary>만료 정책으로 조립한다.</summary>
        public static StatusLifetime Of(ExpiryPolicy policy)
        {
            switch (policy.Mode)
            {
                case ExpiryMode.PhaseVisits:
                    return new StatusLifetime(StatusLifetimeKind.PhaseVisits, policy.RemainingVisits, policy.Phase);
                case ExpiryMode.UntilConsumed:
                    return UntilConsumed(policy.RemainingVisits);
                default:
                    return Permanent;
            }
        }

        /// <summary>이 수명의 공통 만료 정책.</summary>
        public ExpiryPolicy Expiry
        {
            get
            {
                switch (Kind)
                {
                    case StatusLifetimeKind.ThisTurn: return ExpiryPolicy.PhaseVisits(CombatPhase.Cleanup, 1);
                    case StatusLifetimeKind.Turns: return ExpiryPolicy.PhaseVisits(CombatPhase.Cleanup, Count);
                    case StatusLifetimeKind.UntilConsumed: return ExpiryPolicy.UntilConsumed(Count);
                    case StatusLifetimeKind.PhaseVisits: return ExpiryPolicy.PhaseVisits(Phase, Count);
                    default: return ExpiryPolicy.Permanent;
                }
            }
        }
    }
}
