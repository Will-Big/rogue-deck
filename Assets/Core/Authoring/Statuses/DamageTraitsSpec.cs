using System;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>저작된 피해 속성(JSON "damage"). 생략한 칸은 보통 피해(false)다.</summary>
    [Serializable]
    public sealed class DamageTraitsSpec
    {
        public bool Piercing;
        public bool IgnoresMultipliers;

        public DamageTraits ToTraits() => new DamageTraits(Piercing, IgnoresMultipliers);
    }
}
