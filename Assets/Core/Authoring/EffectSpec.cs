using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    public enum ConditionKind { None, FirstToTrigger, WithinNth, BeforeNextEnemyDamageCard, PrevExecutedIsPlayerDamageCard, NextIsEnemyDamageCard, PrevExecutedIsEnemyDamageCard, NoPrecedingPlayerCard, NoFollowingEnemyCard, NoFollowingPlayerCard }

    /// <summary>카드 시작 조건(전투 실행 계약 스펙 §2·§4). 카드가 차례를 맞을 때 한 번 평가한다.
    /// Closed condition combinator (백로그 §10): the kind enum + central switch stay by design.
    /// 앞 효과의 결과를 읽는 요건은 여기가 아니라 효과의 requires다.</summary>
    public sealed class StartConditionSpec
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public ConditionKind Kind;

        public int N;

        public Condition ToCondition()
        {
            switch (Kind)
            {
                case ConditionKind.FirstToTrigger: return new FirstToTrigger();
                case ConditionKind.WithinNth: return new WithinNth(N);
                case ConditionKind.BeforeNextEnemyDamageCard: return new BeforeNextEnemyDamageCard();
                case ConditionKind.PrevExecutedIsPlayerDamageCard:
                    return new PreviousExecutedCardHasEffect(Side.Player, EffectKeys.Damage);
                case ConditionKind.PrevExecutedIsEnemyDamageCard:
                    return new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage);
                case ConditionKind.NextIsEnemyDamageCard:
                    return new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage);
                case ConditionKind.NoPrecedingPlayerCard:
                    return new NoPrecedingCardOfSide(Side.Player);
                case ConditionKind.NoFollowingEnemyCard:
                    return new NoFollowingCardOfSide(Side.Enemy);
                case ConditionKind.NoFollowingPlayerCard:
                    return new NoFollowingCardOfSide(Side.Player);
                default: return null;
            }
        }
    }

    /// <summary>카드 한 장의 위치 규칙. 진영마다 하나의 범위만 가진다 — 같은 진영에 두 범위를 쓰는
    /// 카드는 로딩에서 거부된다(스펙 §4). 진영은 절대 진영이다(Ally=파티, Enemy=적).</summary>
    public sealed class CardTargetsSpec
    {
        public CardTargetRange? Ally;
        public CardTargetRange? Enemy;

        public CardTargetRange? RangeOf(CardTargetFaction faction)
            => faction == CardTargetFaction.Ally ? Ally : Enemy;
    }

    /// <summary>앞 효과의 실제 소비량이 MinimumConsumed 이상일 때만 수행한다 — "소비했다면".</summary>
    public sealed class EffectRequirementSpec
    {
        public string SourceEffectId;
        public int MinimumConsumed;
    }

    /// <summary>앞 효과의 실제 소비량 × PerConsumed를 이 효과의 수치에 더한다 — "소비 1당 +k".</summary>
    public sealed class EffectScalingSpec
    {
        public string SourceEffectId;
        public int PerConsumed;
    }

    /// <summary>One authored effect. Each concrete spec owns its parameters (real types), its mapping
    /// to core EffectData and its validation — adding a new effect touches no central enum/switch
    /// (AGENTS.md rule 9). Registered explicitly in EffectSpecCatalog.
    ///
    /// 공통 필드(전투 실행 계약 스펙 §4): 카드 안에서 유일한 id, 대상 효과의 진영(카드의 위치 규칙 중 어느
    /// 축을 쓰는가), 카드 시작 조건에 따른 성공 수치·생략, 앞 효과 결과 참조(requires·scaleBy).
    /// 기반 필드의 Order는 CardSpec과 같은 이유로 명시한다 — 파생 필드(무순서)보다 id·진영이 앞에,
    /// 조건·참조가 뒤에 오게 고정한다.</summary>
    public abstract class EffectSpec
    {
        [JsonProperty(Order = -20)]
        public string Id;

        [JsonProperty(Order = -19)]
        public CardTargetFaction? TargetFaction;

        [JsonProperty(Order = 50)]
        public int? SuccessEffectValue;

        [JsonProperty(Order = 51)]
        public bool SkipOnBasic;

        [JsonProperty(Order = 52)]
        public EffectRequirementSpec Requires;

        [JsonProperty(Order = 53)]
        public EffectScalingSpec ScaleBy;

        [JsonIgnore]
        public abstract EffectKey Key { get; }

        /// <summary>이 효과가 카드의 위치 규칙에서 대상을 고르는가. 고르는 효과는 TargetFaction이 필수다.</summary>
        [JsonIgnore]
        public virtual bool IsTargeted => false;

        /// <summary>이 효과가 실제 소비량을 결과로 내는가. requires·scaleBy는 이런 효과만 가리킬 수 있다.</summary>
        [JsonIgnore]
        public virtual bool ProducesConsumption => false;

        /// <summary>코어 EffectData로 바꾼다. 공통 필드(id·진영·성공 수치·생략·결과 참조)는 여기서 붙인다.
        /// 위치는 효과가 아니라 카드의 축이 갖는다(CardSpecMapper가 CardDefinition에 옮긴다).</summary>
        public EffectData ToEffectData()
            => Build() with
            {
                Id = Id,
                TargetFaction = IsTargeted ? TargetFaction : null,
                SuccessEffectValue = SuccessEffectValue,
                SkipOnBasic = SkipOnBasic,
                Requirement = Requires == null
                    ? null
                    : new EffectResultRequirement(Requires.SourceEffectId, Requires.MinimumConsumed),
                Scaling = ScaleBy == null
                    ? null
                    : new EffectResultScaling(ScaleBy.SourceEffectId, ScaleBy.PerConsumed)
            };

        protected abstract EffectData Build();

        public virtual IEnumerable<string> Validate(AuthoringContext context)
        {
            yield break;
        }

        /// <summary>카드 진영과 이 효과가 고른 위치의 조합이 이 효과에 뜻이 있는지. 대상 효과만 호출된다.</summary>
        public virtual IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
        {
            yield break;
        }

        /// <summary>카드 쪽 진영(플레이어 카드면 Ally, 적 카드면 Enemy).</summary>
        protected static CardTargetFaction OwnFaction(Side cardSide)
            => cardSide == Side.Player ? CardTargetFaction.Ally : CardTargetFaction.Enemy;

        /// <summary>상대 진영.</summary>
        protected static CardTargetFaction OpposingFaction(Side cardSide)
            => cardSide == Side.Player ? CardTargetFaction.Enemy : CardTargetFaction.Ally;

        /// <summary>진영이 정해진 위치 효과의 공통 검증: 그 진영이어야 하고 Self가 아니어야 한다.</summary>
        protected IEnumerable<string> RequirePositional(CardTargetKey target, CardTargetFaction faction)
        {
            if (target.Faction != faction)
            {
                yield return Key.Id + " targets only the " + faction + " side, not " + target.Faction + ".";
            }

            if (target.Range == CardTargetRange.Self)
            {
                yield return Key.Id + " needs a position range, not Self.";
            }
        }
    }
}
