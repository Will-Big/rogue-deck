using System;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class KoreanDescriptionCatalog
    {
        public static readonly KoreanDescriptionCatalog Default = CreateDefault();

        public KoreanDescriptionCatalog(
            EffectDescriptionRegistry effects,
            InterventionDescriptionRegistry interventions,
            StatusDescriptionRegistry statuses,
            IDescriptionGrammar grammar,
            StatusContentCatalog statusContent)
        {
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            Interventions = interventions
                ?? throw new ArgumentNullException(nameof(interventions));
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            Grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            StatusContent = statusContent ?? throw new ArgumentNullException(nameof(statusContent));
        }

        public EffectDescriptionRegistry Effects { get; }
        public InterventionDescriptionRegistry Interventions { get; }
        public StatusDescriptionRegistry Statuses { get; }
        public IDescriptionGrammar Grammar { get; }
        public StatusContentCatalog StatusContent { get; }

        public DescriptionContext ContextFor(CardDefinition card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            return new DescriptionContext(
                Grammar, Statuses, StatusContent, card.Id, card.Side);
        }

        /// <summary>코드 기본값 카탈로그를 쓰는 편의 오버로드.</summary>
        public static KoreanDescriptionCatalog CreateDefault()
            => CreateDefault(StatusContentDefaults.Catalog());

        /// <summary>상태 이름을 넘겨받은 콘텐츠에서 읽는다. 로더가 파일에서 만든 카탈로그를 넘기면
        /// 카드 텍스트와 전투 규칙이 같은 상태 콘텐츠를 보게 된다 — 인자 없는 오버로드만 있으면
        /// 규칙은 파일을, 텍스트는 코드 기본값을 보고 갈린다.</summary>
        public static KoreanDescriptionCatalog CreateDefault(StatusContentCatalog statusContent)
        {
            if (statusContent == null) throw new ArgumentNullException(nameof(statusContent));

            var statuses = new StatusDescriptionRegistry();
            foreach (var id in statusContent.Keys)
            {
                var key = new StatusKey(id);
                statuses.Register(key, statusContent.DisplayNameOf(key));
            }

            var effects = new EffectDescriptionRegistry();
            effects.Register(new DamageDescriptionHandler());
            effects.Register(new ApplyStatusDescriptionHandler());
            effects.Register(new NullifyNextPlayerConditionRewardDescriptionHandler());
            effects.Register(new GrantNextPlayerDamageCardBonusDescriptionHandler());
            effects.Register(new MoveFormationDescriptionHandler());
            effects.Register(new ConsumeStatusDescriptionHandler());
            effects.Register(new TriggerStatusDescriptionHandler());
            effects.Register(new GrantNextTurnFateDescriptionHandler());

            var interventions = new InterventionDescriptionRegistry();
            interventions.Register(new ChangeExecutionOrderDescriptionHandler());
            interventions.Register(new SwapExecutionOrderDescriptionHandler());
            interventions.Register(new LockDescriptionHandler());

            return new KoreanDescriptionCatalog(
                effects,
                interventions,
                statuses,
                new KoreanDescriptionGrammar(),
                statusContent);
        }
    }
}
