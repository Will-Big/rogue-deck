using System;
using System.Collections.Generic;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>저작된 피해 속성 목록의 공통 검증(카드 피해 효과·상태가 함께 쓴다): 정의된 값만, 중복 없이.</summary>
    public static class DamageTraitRules
    {
        public static IEnumerable<string> Validate(DamageTrait[] traits, string field)
        {
            if (traits == null)
            {
                yield break;
            }

            var seen = new HashSet<DamageTrait>();
            foreach (var trait in traits)
            {
                if (!Enum.IsDefined(typeof(DamageTrait), trait))
                {
                    yield return field + " has an unknown damage trait '" + trait + "'.";
                }
                else if (!seen.Add(trait))
                {
                    yield return field + " lists '" + trait + "' twice.";
                }
            }
        }
    }
}
