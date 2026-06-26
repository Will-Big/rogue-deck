using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Supplies localized text fragments for the <see cref="DescriptionComposer"/>.
    /// Fragments carry NO trailing punctuation; the composer assembles sentences. One implementation
    /// per language owns all wording and grammar (including composite-condition joins).</summary>
    public interface IDescriptionVocabulary
    {
        /// <summary>e.g. "피해 4".</summary>
        string Damage(int amount);

        /// <summary>e.g. "방어 4", "적 둔화 3 (2턴)". <paramref name="magnitude"/> is the status strength
        /// (rides on EffectData.Amount); <paramref name="lifetime"/> drives any duration suffix.</summary>
        string Status(StatusKey key, StatusApplyTarget target, int magnitude, StatusLifetime lifetime);

        /// <summary>e.g. "다음 플레이어 조건 보상을 무효화".</summary>
        string NullifyNextReward();

        /// <summary>e.g. "다음 플레이어 공격 피해 +6".</summary>
        string GrantNextAttackBonus(int amount);

        /// <summary>A full conditional clause ending in the appropriate Korean conditional ending
        /// (e.g. "첫 발동이면", "바로 뒤가 적 공격이면"). Handles AllOf internally.</summary>
        string Condition(Condition condition);

        /// <summary>A complete fate-card sentence fragment, e.g. "한 카드의 주도력 -2",
        /// "두 카드의 주도력을 교환", "한 카드를 고정".</summary>
        string Fate(FateActionData fate);
    }
}
