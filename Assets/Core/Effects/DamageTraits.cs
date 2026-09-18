using System;
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Core.Effects
{
    /// <summary>피해 속성 하나. 새 속성은 여기에 값을 더하고 공통 피해 경로(DamageService)가 그 뜻을 적용한다.</summary>
    public enum DamageTrait
    {
        /// <summary>관통: 흡수 층(방어)을 건너뛴다.</summary>
        Piercing,

        /// <summary>배율 무시: 받는 쪽 배율 층(취약 등)을 건너뛴다.</summary>
        IgnoresMultipliers
    }

    /// <summary>피해 한 번이 가진 속성들(전투 실행 계약 스펙 §7, 계획 D10). 여러 개를 함께 가질 수 있다 — 독은
    /// 관통이면서 배율 무시다. 어느 원인(카드·상태·반응)의 피해든 가질 수 있고, 값은 데이터가 정한다
    /// (카드 피해 효과의 traits, 상태의 damageTraits). 비어 있으면 보통 피해다.</summary>
    public sealed class DamageTraits : IEquatable<DamageTraits>
    {
        private readonly DamageTrait[] _traits;

        private DamageTraits(DamageTrait[] traits)
        {
            _traits = traits;
        }

        /// <summary>속성 없는 보통 피해: 배율과 흡수를 모두 거친다.</summary>
        public static readonly DamageTraits Normal = new DamageTraits(Array.Empty<DamageTrait>());

        public static DamageTraits Of(IEnumerable<DamageTrait> traits)
        {
            var distinct = (traits ?? Array.Empty<DamageTrait>()).Distinct().OrderBy(t => (int)t).ToArray();
            return distinct.Length == 0 ? Normal : new DamageTraits(distinct);
        }

        public static DamageTraits Of(params DamageTrait[] traits) => Of((IEnumerable<DamageTrait>)traits);

        public IReadOnlyList<DamageTrait> All => _traits;

        public bool Has(DamageTrait trait) => Array.IndexOf(_traits, trait) >= 0;

        public bool Equals(DamageTraits other) => other != null && _traits.SequenceEqual(other._traits);
        public override bool Equals(object obj) => Equals(obj as DamageTraits);
        public override int GetHashCode() => _traits.Aggregate(17, (hash, t) => hash * 31 + (int)t);
        public override string ToString() => _traits.Length == 0 ? "Normal" : string.Join("+", _traits);
    }

    /// <summary>damage 효과의 입력: 피해 속성. 속성이 없는 피해는 페이로드를 두지 않는다.</summary>
    public sealed record DamagePayload(DamageTraits Traits) : IEffectPayload;
}
