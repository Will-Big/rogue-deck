using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Metadata for one authorable effect spec type: its display name, concrete type, and a
    /// no-arg factory.</summary>
    public sealed class EffectSpecInfo
    {
        public EffectSpecInfo(string displayName, Type specType, Func<EffectSpec> create)
        {
            DisplayName = displayName;
            SpecType = specType;
            Create = create;
        }

        public string DisplayName { get; }
        public Type SpecType { get; }
        public Func<EffectSpec> Create { get; }
    }

    /// <summary>Explicit list of authorable effect specs. The Unity drawer's dropdown and the boot
    /// validation cross-check both read this — registering here is the one step besides writing the
    /// spec/handler classes (AGENTS.md rule 9: 핸들러 1개 + 키 등록).</summary>
    public static class EffectSpecCatalog
    {
        public static IReadOnlyList<EffectSpecInfo> All() => new[]
        {
            new EffectSpecInfo("피해", typeof(DamageSpec), () => new DamageSpec()),
            new EffectSpecInfo("상태 부여", typeof(ApplyStatusSpec), () => new ApplyStatusSpec()),
            new EffectSpecInfo("다음 피해 카드 강화", typeof(GrantNextDamageCardBonusSpec), () => new GrantNextDamageCardBonusSpec()),
            new EffectSpecInfo("다음 보상 무효화", typeof(NullifyNextRewardSpec), () => new NullifyNextRewardSpec()),
            new EffectSpecInfo("대형 이동", typeof(MoveFormationSpec), () => new MoveFormationSpec()),
            new EffectSpecInfo("상태 소비", typeof(ConsumeStatusSpec), () => new ConsumeStatusSpec()),
            new EffectSpecInfo("상태 즉시 발동", typeof(TriggerStatusSpec), () => new TriggerStatusSpec()),
            new EffectSpecInfo("다음 턴 운명력", typeof(GrantNextTurnFateSpec), () => new GrantNextTurnFateSpec())
        };
    }
}
