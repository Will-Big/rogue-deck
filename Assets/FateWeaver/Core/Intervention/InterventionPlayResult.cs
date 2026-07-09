namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionPlayResult
    {
        public int AppliedCount { get; }
        public int RejectedIndex { get; }
        public int FateEnergySpent { get; }

        public InterventionPlayResult(int appliedCount, int rejectedIndex, int fateEnergySpent)
        {
            AppliedCount = appliedCount;
            RejectedIndex = rejectedIndex;
            FateEnergySpent = fateEnergySpent;
        }
    }
}
