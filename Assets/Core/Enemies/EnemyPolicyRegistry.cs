using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Enemies
{
    /// <summary>정책 키 → 정책 생성 함수. 정책은 상태를 가지므로(ShuffleBagPolicy) 인스턴스가 아니라
    /// 생성 함수를 등록하고, Create가 매번 새로 만든다. 새 정책 = 클래스 1개 + 키 등록(규칙 9).</summary>
    public sealed class EnemyPolicyRegistry
    {
        private readonly Dictionary<EnemyPolicyKey, Func<IReadOnlyList<EnemyCardBundle>, IEnemyTurnPolicy>> _factories = new();

        public void Register(EnemyPolicyKey key, Func<IReadOnlyList<EnemyCardBundle>, IEnemyTurnPolicy> create)
            => _factories[key] = create ?? throw new ArgumentNullException(nameof(create));

        public bool Contains(EnemyPolicyKey key) => _factories.ContainsKey(key);

        public IEnemyTurnPolicy Create(EnemyPolicyKey key, IReadOnlyList<EnemyCardBundle> bundles)
            => _factories.TryGetValue(key, out var create)
                ? create(bundles)
                : throw new KeyNotFoundException($"No enemy policy registered for '{key}'");
    }
}
