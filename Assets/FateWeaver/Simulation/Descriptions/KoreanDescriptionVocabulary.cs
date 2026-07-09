using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>The Korean description vocabulary. Stateless; use <see cref="Instance"/>.
    /// Owns all Korean wording and grammar (including AllOf joins via condition stems).</summary>
    public sealed class KoreanDescriptionVocabulary : IDescriptionVocabulary
    {
        public static readonly KoreanDescriptionVocabulary Instance = new KoreanDescriptionVocabulary();

        public string Damage(int amount) => "피해 " + amount;

        public string Status(StatusKey key, StatusApplyTarget target, int magnitude, StatusLifetime lifetime)
        {
            var sb = new StringBuilder();
            if (target == StatusApplyTarget.TargetEnemy)
                sb.Append("적 ");
            sb.Append(StatusName(key)).Append(' ').Append(magnitude);

            var suffix = LifetimeSuffix(lifetime);
            if (suffix != null)
                sb.Append(' ').Append(suffix);

            return sb.ToString();
        }

        public string NullifyNextReward() => "다음 플레이어 조건 보상을 무효화";

        public string GrantNextAttackBonus(int amount) => "다음 플레이어 공격 피해 +" + amount;

        public string Condition(Condition condition)
        {
            switch (condition)
            {
                // Verb-stem predicate: takes "...없으면", not "...없으" + "이면".
                case NoPrecedingCardOfSide n:
                    return "이전 수행한 " + SideName(n.Side) + " 카드 없으면";
                case NoFollowingCardOfSide n:
                    return "이후 수행한 " + SideName(n.Side) + " 카드 없으면";
                case AllOf all:
                    return JoinAll(all.Conditions) + "이면";
                default:
                    return ConditionStem(condition) + "이면";
            }
        }

        public string Intervention(InterventionActionData intervention)
        {
            if (intervention.Key == InterventionActionKeys.ChangeInitiative)
                return "한 카드의 주도력 " + (intervention.Amount >= 0 ? "+" + intervention.Amount : intervention.Amount.ToString());
            if (intervention.Key == InterventionActionKeys.SwapInitiative)
                return "두 카드의 주도력을 교환";
            if (intervention.Key == InterventionActionKeys.Lock)
                return "한 카드를 고정";
            return string.Empty;
        }

        // --- helpers ---------------------------------------------------------

        // A condition stem WITHOUT the trailing "이면" so AllOf can join with "이고".
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
                case AllOf all:
                    return JoinAll(all.Conditions);
                default:
                    return string.Empty;
            }
        }

        // "바로 앞이 적 공격" / "바로 뒤가 적 공격" / "바로 앞이 플레이어 카드"
        private static string AdjacentStem(AdjacentCardIs a)
        {
            var position = a.Direction == AdjacentDirection.Previous ? "바로 앞이 " : "바로 뒤가 ";
            var subject = a.Type.HasValue
                ? SideName(a.Side) + " " + CardTypeName(a.Type.Value)
                : SideName(a.Side) + " 카드";
            return position + subject;
        }

        // Join child stems with "이고 ", e.g. "바로 앞이 플레이어 카드이고 3번째 안".
        private static string JoinAll(IReadOnlyList<Condition> children)
        {
            var stems = new string[children.Count];
            for (int i = 0; i < children.Count; i++)
                stems[i] = ConditionStem(children[i]);
            return string.Join("이고 ", stems);
        }

        private static string LifetimeSuffix(StatusLifetime lifetime)
        {
            switch (lifetime.Kind)
            {
                case StatusLifetimeKind.Turns:
                    return "(" + lifetime.Count + "턴)";
                case StatusLifetimeKind.UntilConsumed:
                    return "(" + lifetime.Count + "회)";
                default:
                    return null; // Permanent / ThisTurn: no suffix
            }
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

        private static string StatusName(StatusKey key)
        {
            if (key == StatusKeys.Block) return "방어";
            if (key == StatusKeys.Slow) return "둔화";
            if (key == StatusKeys.Haste) return "가속";
            if (key == StatusKeys.Stun) return "기절";
            if (key == StatusKeys.Vulnerable) return "취약";
            if (key == StatusKeys.RewardNullified) return "조건 보상 무효";
            return key.ToString();
        }
    }
}
