using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>보유자의 상태를 층 순서대로 접어 받는 피해를 계산한다. 배율 층을 모두 접은 뒤
    /// (각 단계에서 정수로 버림) 흡수 층을 적용한다. 값을 실제로 바꾼 UntilConsumed 상태는 그
    /// 자리에서 수명을 1 소비한다.</summary>
    public static class StatusDamageFold
    {
        public static int Incoming(StatusBag bag, StatusRegistry registry, StatusRuleSet rules, int damage)
        {
            if (registry == null || bag == null)
            {
                return damage;
            }

            damage = FoldLayer(bag, registry, rules, damage, StatusDamageLayer.Multiplier);
            damage = FoldLayer(bag, registry, rules, damage, StatusDamageLayer.Absorb);
            return damage;
        }

        private static int FoldLayer(
            StatusBag bag,
            StatusRegistry registry,
            StatusRuleSet rules,
            int damage,
            StatusDamageLayer layer)
        {
            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (!registry.TryResolve(status.Key, out var behavior)
                    || behavior.DamageLayer != layer)
                {
                    continue;
                }

                var after = behavior.ModifyIncomingDamage(
                    damage,
                    new StatusContext { Instance = status, Rules = rules });
                if (after != damage)
                {
                    bag.Consume(status);
                }

                damage = after;
            }

            return damage;
        }
    }
}
