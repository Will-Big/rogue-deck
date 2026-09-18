using System;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public interface IEffectDescriptionHandler
    {
        EffectKey Key { get; }
        EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context);
    }

    public interface IInterventionDescriptionHandler
    {
        InterventionActionKey Key { get; }
        string DisplayName { get; }
        string Describe(InterventionActionData action, DescriptionContext context);
    }

    public interface IDescriptionGrammar
    {
        string Symbol(CardTargetKey target);
        string Condition(Condition condition);

        /// <summary>앞 효과의 결과를 요구하는 문장의 머리("소비했다면").</summary>
        string Requirement(EffectResultRequirement requirement);

        /// <summary>앞 효과의 소비량에 비례한 가산을 효과 문장 뒤에 붙이는 꼬리(" (소비 1당 +2)").</summary>
        string ScalingSuffix(EffectResultScaling scaling);
        /// <summary>수명 종류(count가 지속일 때만 의미가 있다)와 그 count로 "(N턴)"/"(N회)" 접미사를
        /// 만든다. 카드는 더 이상 StatusLifetime을 갖지 않으므로 종류와 개수를 따로 받는다.</summary>
        string LifetimeSuffix(StatusLifetimeKind kind, int count);
    }

    public sealed class DescriptionContext
    {
        private readonly IDescriptionGrammar _grammar;

        public DescriptionContext(
            IDescriptionGrammar grammar,
            StatusDescriptionRegistry statuses,
            StatusContentCatalog statusContent,
            CardDefinition card)
        {
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            StatusContent = statusContent ?? throw new ArgumentNullException(nameof(statusContent));
            Card = card ?? throw new ArgumentNullException(nameof(card));
        }

        public StatusDescriptionRegistry Statuses { get; }

        /// <summary>설명하는 카드. 효과의 대상 위치는 효과가 아니라 카드의 축이 갖는다.</summary>
        public CardDefinition Card { get; }
        public string CardId => Card.Id;
        public Side CardSide => Card.Side;

        /// <summary>이 전투의 상태 저작 콘텐츠. 카드 텍스트에서 숫자가 세기인지 지속인지는 카드가
        /// 아니라 이 카탈로그만 안다(규칙 10) — 설명 컴포저가 이걸 못 보면 규칙 10을 지킬 수 없다.</summary>
        public StatusContentCatalog StatusContent { get; }
        /// <summary>효과가 대상을 고르는 위치(효과 진영 + 카드의 그 진영 축). 실행과 같은 계산이다
        /// (CardDefinition.TargetOf). 대상을 고르지 않는 효과는 null.</summary>
        public CardTargetKey? TargetOf(EffectData effect) => Card.TargetOf(effect);

        public CardTargetKey SelfTarget()
            => new CardTargetKey(
                CardSide == Side.Player ? CardTargetFaction.Ally : CardTargetFaction.Enemy,
                CardTargetRange.Self);

        public string Condition(Condition condition) => _grammar.Condition(condition);

        public string Requirement(EffectResultRequirement requirement) => _grammar.Requirement(requirement);

        public string ScalingSuffix(EffectResultScaling scaling) => _grammar.ScalingSuffix(scaling);

        public string LifetimeSuffix(StatusLifetimeKind kind, int count)
            => _grammar.LifetimeSuffix(kind, count);

        public string Symbol(CardTargetKey target) => _grammar.Symbol(target);
    }
}
