namespace FateWeaver.Core.Status
{
    /// <summary>A status applied to a holder (data). Lifetime kind is fixed at application time;
    /// Count is the remaining turns (Turns) or remaining charges (UntilConsumed). Magnitude is the
    /// effect strength (e.g. block points), 0 when a status has no magnitude.</summary>
    public sealed class StatusInstance
    {
        public StatusKey Key { get; }
        public StatusLifetimeKind Kind { get; }
        public int Count { get; set; }
        public int Magnitude { get; set; }

        public StatusInstance(StatusKey key, StatusLifetime lifetime, int magnitude = 0)
        {
            Key = key;
            Kind = lifetime.Kind;
            Count = lifetime.Count;
            Magnitude = magnitude;
        }
    }
}
