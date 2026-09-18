using System.Collections.Generic;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Cards
{
    /// <summary>One effect entry on a card: which handler + its scalar effect value (M1).</summary>
    public sealed record EffectData(EffectKey Key, int EffectValue)
    {
        /// <summary>카드 안에서 유일한 효과 ID. 뒤 효과의 결과 참조가 이것으로 가리킨다. 저작 콘텐츠는
        /// 항상 갖고, 참조가 없는 C# 픽스처는 비워 둘 수 있다.</summary>
        public string Id { get; init; }

        /// <summary>카드 시작 조건이 Success일 때 쓰는 수치. 조건은 카드 단위다(CardDefinition.StartCondition).</summary>
        public int? SuccessEffectValue { get; init; }

        /// <summary>카드 시작 조건이 Basic이면 이 효과를 통째로 건너뛴다 — '~이면 X' 문법
        /// (기본 발동 없음, 성공 시에만 발동). 카드에 시작 조건이 없으면 무의미.</summary>
        public bool SkipOnBasic { get; init; }

        /// <summary>앞 효과의 실제 소비량이 모자라면 이 효과를 수행하지 않는다(null이면 요건 없음).</summary>
        public EffectResultRequirement Requirement { get; init; }

        /// <summary>앞 효과의 실제 소비량에 비례해 이 효과의 수치를 더한다(null이면 가산 없음).</summary>
        public EffectResultScaling Scaling { get; init; }

        /// <summary>Effect-kind-specific parameters (null when the scalar is enough).</summary>
        public IEffectPayload Payload { get; init; }

        // Position selector for an effect's target(s): drives enemy attacks against the player party
        // formation, player cards' positional (or All) targeting of the living enemy formation, and
        // PartyBySelector's positional ally targeting. Null means the handler's legacy default
        // (FrontOne for enemy attacks; explicit-id-else-first-enemy for pre-selector player content) —
        // this keeps old single-target content compatible without an authored selector.
        public TargetSelector? TargetSelector { get; init; }

        /// <summary>카드가 apply_status에 주는 것은 count 하나뿐이다. 그 뜻(세기 또는 지속)과
        /// 결과 수명의 종류는 상태 자신의 StatusContentCatalog 항목이 정한다 — 카드는 고르지 않는다.
        /// count는 EffectValue에 실려 조건부 SuccessEffectValue를 그대로 통과한다.</summary>
        public static EffectData ApplyStatus(
            StatusKey statusKey,
            StatusApplyTarget target,
            int count = 0)
            => new EffectData(EffectKeys.ApplyStatus, count)
            {
                Payload = new ApplyStatusPayload(statusKey, target)
            };
    }

    /// <summary>Immutable card template.</summary>
    public sealed record CardDefinition(
        string Id,
        string Name,
        Side Side,
        int BaseExecutionOrder,
        IReadOnlyList<EffectData> Effects)
    {
        /// <summary>카드가 차례를 맞을 때 한 번 평가하는 조건(null이면 조건 없음). 결과는 카드가 끝날 때까지
        /// 고정되며, 각 효과의 SuccessEffectValue·SkipOnBasic이 그 결과를 읽는다(전투 실행 계약 스펙 §2).</summary>
        public Condition StartCondition { get; init; }

        public bool HasEffect(EffectKey key)
        {
            if (string.IsNullOrEmpty(key.Id))
                throw new System.ArgumentException("Effect key must not be empty.", nameof(key));

            foreach (var effect in Effects)
            {
                if (effect.Key == key)
                    return true;
            }

            return false;
        }

        /// <summary>Energy cost to play this card.</summary>
        public int EnergyCost { get; init; }

        /// <summary>Execution (effects on the zone) or intervention (zone control).</summary>
        public CardCategory Category { get; init; }

        /// <summary>For intervention cards: the action resolved when played (null for execution cards).</summary>
        public InterventionActionData InterventionAction { get; init; }

        /// <summary>When true, the card enters the future zone locked (intervention reordering rejected).</summary>
        public bool StartsLocked { get; init; }
    }
}
