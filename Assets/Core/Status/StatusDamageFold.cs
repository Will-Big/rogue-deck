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

        /// <summary>행위자의 엔티티 스코프 상태를 접어 주는 피해를 계산한다. 흡수는 받는 쪽
        /// 개념이므로 여기서는 배율 층만 접는다.</summary>
        public static int Outgoing(StatusBag bag, StatusRegistry registry, StatusRuleSet rules, int damage)
        {
            if (registry == null || bag == null)
            {
                return damage;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (!registry.TryResolve(status.Key, out var behavior)
                    || behavior.DamageLayer != StatusDamageLayer.Multiplier)
                {
                    continue;
                }

                var after = behavior.ModifyOutgoingDamage(
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

        /// <summary>보유자가 얻으려는 상태 수치를 그 보유자의 상태로 접는다 (예: 손상이 방어도
        /// 획득을 깎는다). 어느 키에 걸릴지는 각 행동이 판단한다.</summary>
        public static int GainedMagnitude(
            StatusKey gained,
            StatusBag bag,
            StatusRegistry registry,
            StatusRuleSet rules,
            int magnitude)
        {
            if (registry == null || bag == null)
            {
                return magnitude;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (!registry.TryResolve(status.Key, out var behavior))
                {
                    continue;
                }

                var after = behavior.ModifyGainedMagnitude(
                    gained,
                    magnitude,
                    new StatusContext { Instance = status, Rules = rules });
                if (after != magnitude)
                {
                    bag.Consume(status);
                }

                magnitude = after;
            }

            return magnitude;
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
