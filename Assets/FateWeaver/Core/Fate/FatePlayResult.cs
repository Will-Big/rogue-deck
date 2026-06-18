namespace FateWeaver.Core.Fate
{
    public sealed class FatePlayResult
    {
        public int AppliedCount { get; }
        public int RejectedIndex { get; }
        public int FateEnergySpent { get; }

        public FatePlayResult(int appliedCount, int rejectedIndex, int fateEnergySpent)
        {
            AppliedCount = appliedCount;
            RejectedIndex = rejectedIndex;
            FateEnergySpent = fateEnergySpent;
        }
    }
}
