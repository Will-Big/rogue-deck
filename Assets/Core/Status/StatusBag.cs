using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>Holds the statuses attached to one holder (entity or card instance). One instance per key, in
    /// application order — 반응·턴 시점 처리가 이 순서로 방문한다. 재부여는 같은 자리의 인스턴스를 갱신하고
    /// 새 부여만 끝에 붙는다(스펙 §7).</summary>
    public sealed class StatusBag
    {
        private readonly List<StatusInstance> _statuses = new();

        public IReadOnlyList<StatusInstance> All => _statuses;

        /// <summary>Applies a status with the given lifetime. 이미 있으면 그 인스턴스의 수명·수치를 새 값으로 바꾸고
        /// 자리는 유지한다.</summary>
        public void Add(StatusKey key, StatusLifetime lifetime, int magnitude = 0)
        {
            var existing = Get(key);
            if (existing != null)
            {
                existing.Refresh(lifetime, magnitude);
                return;
            }

            _statuses.Add(new StatusInstance(key, lifetime, magnitude));
        }

        /// <summary>이 인스턴스가 아직 가방에 있는가(도중에 빠졌는지 확인).</summary>
        public bool Contains(StatusInstance instance) => _statuses.Contains(instance);

        public StatusInstance Get(StatusKey key)
        {
            foreach (var status in _statuses)
            {
                if (status.Key == key)
                {
                    return status;
                }
            }

            return null;
        }

        public bool Has(StatusKey key) => Get(key) != null;

        public bool Remove(StatusKey key)
        {
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i].Key == key)
                {
                    _statuses.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Spends one charge of an UntilConsumed status (call after its hook actually fired);
        /// removes it at zero. No-op for other lifetime kinds.</summary>
        public void Consume(StatusInstance status)
        {
            if (status == null || status.Kind != StatusLifetimeKind.UntilConsumed)
            {
                return;
            }

            status.Count--;
            if (status.Count <= 0)
            {
                _statuses.Remove(status);
            }
        }

        /// <summary>수치 합산 적용: 같은 키가 있으면 Magnitude만 더하고(최초 적용의 수명 유지),
        /// 없으면 새로 추가한다. StacksMagnitude를 선언한 상태(방어·독)에 사용한다.</summary>
        public StatusInstance Stack(StatusKey key, StatusLifetime lifetime, int magnitude)
        {
            var existing = Get(key);
            if (existing != null)
            {
                existing.Magnitude += magnitude;
                return existing;
            }

            var created = new StatusInstance(key, lifetime, magnitude);
            _statuses.Add(created);
            return created;
        }
    }

    /// <summary>Anything that can carry statuses: an entity (player/enemy) or a card instance.</summary>
    public interface IStatusHolder
    {
        StatusBag Statuses { get; }
    }
}
