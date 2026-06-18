namespace FateWeaver.Core.Status
{
    /// <summary>A status applied to a holder (data). Stacks; duration/turn-tick deferred.</summary>
    public sealed class StatusInstance
    {
        public StatusKey Key { get; }
        public int Stacks { get; set; }

        public StatusInstance(StatusKey key, int stacks = 1)
        {
            Key = key;
            Stacks = stacks;
        }
    }
}
