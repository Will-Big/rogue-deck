using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Status
{
    /// <summary>공통 턴 시점의 상태 만료(전투 실행 계약 스펙 §8). 상태 이름을 보지 않고 만료 정책만 평가한다.
    /// 방어 전용 단계나 상태 키 분기를 두지 않는다.</summary>
    public static class StatusLifetimePolicy
    {
        /// <summary>시점 하나를 이 상태가 방문한다. 이번 방문으로 만료됐으면 true. 가방에서 빼는 일은 호출자가 한다.</summary>
        public static bool Visit(StatusInstance instance, CombatPhase phase)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return instance.Expiry.Mode == ExpiryMode.PhaseVisits
                && instance.Expiry.Phase == phase
                && instance.SpendVisit();
        }

        /// <summary>가방의 상태들이 시점을 방문한다. **시점에 들어설 때 있던 상태만** 방문한다 — 도중에 새로 얻은
        /// 상태는 이번 방문에서 만료되지 않는다. 만료된 키를 부여 순서대로 돌려준다. onExpired는 상태를 뺀 직후 불린다.</summary>
        public static IReadOnlyList<StatusKey> VisitAll(StatusBag bag, CombatPhase phase, Action<StatusKey> onExpired)
        {
            var expired = new List<StatusKey>();
            var atEntry = new List<StatusInstance>(bag.All);
            foreach (var instance in atEntry)
            {
                if (!bag.Contains(instance) || !Visit(instance, phase))
                {
                    continue;
                }

                bag.Remove(instance.Key);
                expired.Add(instance.Key);
                onExpired?.Invoke(instance.Key);
            }

            return expired;
        }
    }
}
