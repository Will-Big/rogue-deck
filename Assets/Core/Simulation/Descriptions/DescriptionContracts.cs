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
        string Describe(EffectData effect, int effectValue, DescriptionContext context);
    }

    public interface IInterventionDescriptionHandler
    {
        InterventionActionKey Key { get; }
        string DisplayName { get; }
        string Describe(InterventionActionData action, DescriptionContext context);
    }

    public interface IDescriptionGrammar
    {
        string Target(TargetSelector selector);
        string Condition(Condition condition);
        string StatusTargetPrefix(StatusApplyTarget target);

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
            StatusContentCatalog statusContent)
        {
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            StatusContent = statusContent ?? throw new ArgumentNullException(nameof(statusContent));
        }

        public StatusDescriptionRegistry Statuses { get; }

        /// <summary>이 전투의 상태 저작 콘텐츠. 카드 텍스트에서 숫자가 세기인지 지속인지는 카드가
        /// 아니라 이 카탈로그만 안다(규칙 10) — 설명 컴포저가 이걸 못 보면 규칙 10을 지킬 수 없다.</summary>
        public StatusContentCatalog StatusContent { get; }

        public string TargetPrefix(EffectData effect)
            => effect.TargetSelector.HasValue
                ? _grammar.Target(effect.TargetSelector.Value) + " "
                : string.Empty;

        public string Condition(Condition condition) => _grammar.Condition(condition);

        public string StatusTargetPrefix(StatusApplyTarget target)
            => _grammar.StatusTargetPrefix(target);

        public string LifetimeSuffix(StatusLifetimeKind kind, int count)
            => _grammar.LifetimeSuffix(kind, count);
    }
}
