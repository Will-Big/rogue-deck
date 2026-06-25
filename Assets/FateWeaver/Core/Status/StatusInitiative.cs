namespace FateWeaver.Core.Status
{
    /// <summary>Folds a holder's entity-scoped statuses into the initiative of a card it owns.
    /// Mirrors DamageHandler.FoldIncoming, but duration-based (no charge consume).</summary>
    public static class StatusInitiative
    {
        public static int InitiativeFor(int baseInitiative, StatusBag bag, StatusRegistry registry)
        {
            if (registry == null || bag == null)
            {
                return baseInitiative;
            }

            var result = baseInitiative;
            foreach (var status in bag.All)
            {
                if (registry.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.Entity)
                {
                    result = behavior.ModifyInitiative(result, new StatusContext { Instance = status });
                }
            }

            return result;
        }
    }
}
