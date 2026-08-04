using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class KoreanDescriptionGrammar : IDescriptionGrammar
    {
        public string Symbol(CardTargetKey target)
            => "◆";

        public string Condition(Condition condition)
        {
            switch (condition)
            {
                case NoPrecedingCardOfSide n:
                    return "이전에 실행한 " + SideName(n.Side) + " 카드가 없으면";
                case NoFollowingCardOfSide n:
                    return "뒤에 배치된 " + SideName(n.Side) + " 카드가 없으면";
                case ConsumedStatusAtLeast _:
                    return "소비했다면";
                case AllOf all:
                    return JoinAll(all.Conditions) + "이면";
                default:
                    return ConditionStem(condition) + "이면";
            }
        }

        public string LifetimeSuffix(StatusLifetimeKind kind, int count)
        {
            switch (kind)
            {
                case StatusLifetimeKind.Turns:
                    return "(" + count + "턴)";
                case StatusLifetimeKind.UntilConsumed:
                    return "(" + count + "회)";
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
                case BeforeNextEnemyDamageCard _:
                    return "다음 적 피해 카드 전";
                case SameTarget _:
                    return "같은 대상";
                case AdjacentCardIs a:
                    return AdjacentStem(a);
                case AdjacentCardHasEffect a:
                    return AdjacentEffectStem(a);
                case PreviousExecutedCardIs p:
                    return PreviousExecutedStem(p);
                case PreviousExecutedCardHasEffect p:
                    return PreviousExecutedEffectStem(p);
                case AllOf all:
                    return JoinAll(all.Conditions);
                default:
                    return string.Empty;
            }
        }

        private static string AdjacentStem(AdjacentCardIs adjacent)
        {
            var subject = SideName(adjacent.Side) + " 카드";
            return adjacent.Direction == AdjacentDirection.Previous
                ? "앞에 배치된 카드가 " + subject
                : "바로 뒤가 " + subject;
        }

        private static string AdjacentEffectStem(AdjacentCardHasEffect adjacent)
        {
            var subject = SideName(adjacent.Side) + " " + EffectCardName(adjacent.EffectKey);
            return adjacent.Direction == AdjacentDirection.Previous
                ? "앞에 배치된 카드가 " + subject
                : "바로 뒤가 " + subject;
        }

        private static string PreviousExecutedStem(PreviousExecutedCardIs previous)
        {
            var subject = SideName(previous.Side) + " 카드";
            return "직전에 실행한 카드가 " + subject;
        }

        private static string PreviousExecutedEffectStem(PreviousExecutedCardHasEffect previous)
            => "직전에 실행한 카드가 " + SideName(previous.Side) + " "
                + EffectCardName(previous.EffectKey);

        private static string JoinAll(IReadOnlyList<Condition> children)
        {
            var stems = new string[children.Count];
            for (var i = 0; i < children.Count; i++)
                stems[i] = ConditionStem(children[i]);
            return string.Join("이고 ", stems);
        }

        private static string SideName(Side side) => side == Side.Player ? "플레이어" : "적";

        private static string EffectCardName(EffectKey key)
            => key == EffectKeys.Damage ? "피해 카드" : key + " 효과 카드";
    }
}
