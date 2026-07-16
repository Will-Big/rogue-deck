using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class KoreanDescriptionGrammar : IDescriptionGrammar
    {
        public string Target(TargetSelector selector)
        {
            switch (selector)
            {
                case TargetSelector.FrontMost: return "가장 앞의 대상에게";
                case TargetSelector.SecondFromFront: return "전열에서 두 번째 대상에게";
                case TargetSelector.BackMost: return "가장 뒤의 대상에게";
                default: return "무작위 대상에게";
            }
        }

        public string Condition(Condition condition)
        {
            switch (condition)
            {
                case NoPrecedingCardOfSide n:
                    return "이전에 실행한 " + SideName(n.Side) + " 카드가 없으면";
                case NoFollowingCardOfSide n:
                    return "뒤에 배치된 " + SideName(n.Side) + " 카드가 없으면";
                case AllOf all:
                    return JoinAll(all.Conditions) + "이면";
                default:
                    return ConditionStem(condition) + "이면";
            }
        }

        public string StatusTargetPrefix(StatusApplyTarget target)
        {
            switch (target)
            {
                case StatusApplyTarget.TargetEnemy: return "적 ";
                case StatusApplyTarget.PartyMember: return "선택한 아군에게 ";
                case StatusApplyTarget.AllPartyMembers: return "모든 아군에게 ";
                default: return string.Empty;
            }
        }

        public string LifetimeSuffix(StatusLifetime lifetime)
        {
            switch (lifetime.Kind)
            {
                case StatusLifetimeKind.Turns:
                    return "(" + lifetime.Count + "턴)";
                case StatusLifetimeKind.UntilConsumed:
                    return "(" + lifetime.Count + "회)";
                default:
                    return string.Empty;
            }
        }

        private static string ConditionStem(Condition condition)
        {
            switch (condition)
            {
                case FirstToTrigger _:
                    return "첫 발동";
                case WithinNth w:
                    return w.N + "번째 안";
                case BeforeNextEnemyAttack _:
                    return "다음 적 공격 전";
                case SameTarget _:
                    return "같은 대상";
                case AdjacentCardIs a:
                    return AdjacentStem(a);
                case PreviousExecutedCardIs p:
                    return PreviousExecutedStem(p);
                case AllOf all:
                    return JoinAll(all.Conditions);
                default:
                    return string.Empty;
            }
        }

        private static string AdjacentStem(AdjacentCardIs adjacent)
        {
            var subject = adjacent.Type.HasValue
                ? SideName(adjacent.Side) + " " + CardTypeName(adjacent.Type.Value)
                : SideName(adjacent.Side) + " 카드";
            return adjacent.Direction == AdjacentDirection.Previous
                ? "앞에 배치된 카드가 " + subject
                : "바로 뒤가 " + subject;
        }

        private static string PreviousExecutedStem(PreviousExecutedCardIs previous)
        {
            var subject = previous.Type.HasValue
                ? SideName(previous.Side) + " " + CardTypeName(previous.Type.Value)
                : SideName(previous.Side) + " 카드";
            return "직전에 실행한 카드가 " + subject;
        }

        private static string JoinAll(IReadOnlyList<Condition> children)
        {
            var stems = new string[children.Count];
            for (var i = 0; i < children.Count; i++)
                stems[i] = ConditionStem(children[i]);
            return string.Join("이고 ", stems);
        }

        private static string SideName(Side side) => side == Side.Player ? "플레이어" : "적";

        private static string CardTypeName(CardType type)
        {
            switch (type)
            {
                case CardType.Attack: return "공격";
                case CardType.Defense: return "방어";
                default: return "스킬";
            }
        }
    }
}
