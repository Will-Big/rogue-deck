using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>부팅 시 한 번 만들어지는 상태 규칙 모음. 전투당 하나이며 캐릭터별 규칙은 없다.</summary>
    public sealed class StatusContentCatalog
    {
        private readonly Dictionary<StatusKey, StatusSpec> _specs;
        private readonly List<string> _keys;

        public StatusContentCatalog(Dictionary<StatusKey, StatusSpec> specs)
        {
            _specs = specs;
            Rules = new StatusRuleSet();
            _keys = new List<string>();
            foreach (var pair in specs)
            {
                Rules.Set(pair.Key, pair.Value.ToRule());
                _keys.Add(pair.Key.Id);
            }

            _keys.Sort(StringComparer.Ordinal);
        }

        public StatusRuleSet Rules { get; }

        /// <summary>정렬된 키 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Keys => _keys;

        public StatusLifetimeKind LifetimeOf(StatusKey key) => Spec(key).Lifetime;

        public bool CountIsDuration(StatusKey key) => Spec(key).CountIsDuration;

        public int ExecutionOrderDeltaOf(StatusKey key)
            => Spec(key) is ExecutionOrderStatusSpec spec ? spec.ExecutionOrderDelta : 0;

        public int GrowthPerTurnOf(StatusKey key)
            => Spec(key) is PoisonStatusSpec spec ? spec.GrowthPerTurn : 0;

        private StatusSpec Spec(StatusKey key)
        {
            if (!_specs.TryGetValue(key, out var spec))
            {
                throw new KeyNotFoundException("No authored status content for '" + key.Id + "'.");
            }

            return spec;
        }
    }
}
