namespace FateWeaver.Core.Status
{
    public enum StatusLifetimeKind
    {
        Permanent,
        ThisTurn,
        Turns,
        UntilConsumed
    }

    /// <summary>How long an applied status lasts. Chosen PER APPLICATION (not per status type),
    /// so the same status can be applied as e.g. UntilConsumed(3) by one card and Turns(1) by another.
    /// Count = number of turns (Turns) or number of charges (UntilConsumed); unused otherwise.</summary>
    public readonly struct StatusLifetime
    {
        public StatusLifetimeKind Kind { get; }
        public int Count { get; }

        private StatusLifetime(StatusLifetimeKind kind, int count)
        {
            Kind = kind;
            Count = count;
        }

        public static readonly StatusLifetime Permanent = new StatusLifetime(StatusLifetimeKind.Permanent, 0);
        public static readonly StatusLifetime ThisTurn = new StatusLifetime(StatusLifetimeKind.ThisTurn, 0);
        public static StatusLifetime Turns(int turns) => new StatusLifetime(StatusLifetimeKind.Turns, turns);
        public static StatusLifetime UntilConsumed(int charges = 1) => new StatusLifetime(StatusLifetimeKind.UntilConsumed, charges);
    }
}
