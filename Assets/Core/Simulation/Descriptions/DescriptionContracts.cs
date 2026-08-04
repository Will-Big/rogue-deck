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
            string cardId,
            Side cardSide)
        {
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            StatusContent = statusContent ?? throw new ArgumentNullException(nameof(statusContent));
            CardId = cardId ?? throw new ArgumentNullException(nameof(cardId));
            CardSide = cardSide;
        }

        public StatusDescriptionRegistry Statuses { get; }
        public string CardId { get; }
        public Side CardSide { get; }

        /// <summary>이 전투의 상태 저작 콘텐츠. 카드 텍스트에서 숫자가 세기인지 지속인지는 카드가
        /// 아니라 이 카탈로그만 안다(규칙 10) — 설명 컴포저가 이걸 못 보면 규칙 10을 지킬 수 없다.</summary>
        public StatusContentCatalog StatusContent { get; }
        public CardTargetRange Range(TargetSelector? selector)
        {
            switch (selector ?? TargetSelector.FrontOne)
            {
                case TargetSelector.FrontOne: return CardTargetRange.FrontOne;
                case TargetSelector.FrontTwo: return CardTargetRange.FrontTwo;
                case TargetSelector.BackOne: return CardTargetRange.BackOne;
                case TargetSelector.BackTwo: return CardTargetRange.BackTwo;
                case TargetSelector.All: return CardTargetRange.All;
                default: throw new ArgumentOutOfRangeException(nameof(selector));
            }
        }

        public CardTargetKey EnemyRange(TargetSelector? selector)
            => new CardTargetKey(CardTargetFaction.Enemy, Range(selector));

        public CardTargetKey AllyRange(TargetSelector? selector)
            => new CardTargetKey(CardTargetFaction.Ally, Range(selector));

        public CardTargetKey OpposingRange(TargetSelector? selector)
            => new CardTargetKey(
                CardSide == Side.Player ? CardTargetFaction.Enemy : CardTargetFaction.Ally,
                Range(selector));

        public CardTargetKey SelfTarget()
            => new CardTargetKey(
                CardSide == Side.Player ? CardTargetFaction.Ally : CardTargetFaction.Enemy,
                CardTargetRange.Self);

        public string Condition(Condition condition) => _grammar.Condition(condition);

        public string LifetimeSuffix(StatusLifetimeKind kind, int count)
            => _grammar.LifetimeSuffix(kind, count);

        public string Symbol(CardTargetKey target) => _grammar.Symbol(target);
    }
}
