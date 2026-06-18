namespace FateWeaver.Core.Status
{
    /// <summary>A status applied to a holder (data). Lifetime kind is fixed at application time;
    /// Count is the remaining turns (Turns) or remaining charges (UntilConsumed).</summary>
    public sealed class StatusInstance
    {
        public StatusKey Key { get; }
        public StatusLifetimeKind Kind { get; }
        public int Count { get; set; }

        public StatusInstance(StatusKey key, StatusLifetime lifetime)
        {
            Key = key;
            Kind = lifetime.Kind;
            Count = lifetime.Count;
        }
    }
}
