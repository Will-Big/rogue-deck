namespace FateWeaver.Core.Status
{
    /// <summary>Folds a holder's entity-scoped statuses into the executionOrder of a card it owns.
    /// Mirrors DamageHandler.FoldIncoming, but duration-based (no charge consume).</summary>
    public static class StatusExecutionOrder
    {
        public static int ExecutionOrderFor(
            int baseExecutionOrder,
            StatusBag bag,
            StatusRegistry registry,
            StatusRuleSet rules,
            Authoring.Statuses.StatusContentCatalog content)
        {
            if (registry == null || bag == null)
            {
                return baseExecutionOrder;
            }

            var result = baseExecutionOrder;
            foreach (var status in bag.All)
            {
                if (registry.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.Entity)
                {
                    result = behavior.ModifyExecutionOrder(
                        result,
                        new StatusContext { Instance = status, Rules = rules, Content = content });
                }
            }

            return result;
        }
    }
}
