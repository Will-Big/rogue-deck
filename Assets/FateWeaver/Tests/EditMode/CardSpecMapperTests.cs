using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class CardSpecMapperTests
    {
        [Test]
        public void Maps_flat_damage_action()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "slash", Name = "베기", Side = Side.Player, Type = CardType.Attack,
                Category = CardCategory.Execution, Cost = 1, BaseInitiative = 5,
                Effects = new[] { new EffectSpec { Kind = EffectKind.Damage, Amount = 3 } }
            });

            Assert.AreEqual(CardCategory.Execution, def.Category);
            Assert.AreEqual(1, def.Cost);
            Assert.AreEqual(1, def.Effects.Count);
            Assert.AreEqual(EffectKeys.Damage, def.Effects[0].Key);
            Assert.AreEqual(3, def.Effects[0].Amount);
            Assert.IsNull(def.Effects[0].Condition);
        }

        [Test]
        public void Maps_conditional_damage()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
                Category = CardCategory.Execution, Cost = 1, BaseInitiative = 5,
                Effects = new[] { new EffectSpec {
                    Kind = EffectKind.Damage, Amount = 2,
                    Condition = ConditionKind.FirstToTrigger, SuccessAmount = 8 } }
            });

            var e = def.Effects[0];
            Assert.AreEqual(2, e.Amount);
            Assert.AreEqual(8, e.SuccessAmount);
            Assert.IsInstanceOf<FirstToTrigger>(e.Condition);
        }

        [Test]
        public void Maps_conditional_apply_status()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "cover", Name = "엄호", Side = Side.Player, Type = CardType.Defense,
                Category = CardCategory.Execution, Cost = 1, BaseInitiative = 5,
                Effects = new[] { new EffectSpec {
                    Kind = EffectKind.ApplyStatus, Amount = 2, Status = StatusKindRef.Block,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                    Condition = ConditionKind.NextIsEnemyAttack, SuccessAmount = 7 } }
            });

            var e = def.Effects[0];
            Assert.AreEqual(EffectKeys.ApplyStatus, e.Key);
            Assert.AreEqual(2, e.Amount);
            Assert.AreEqual(7, e.SuccessAmount);
            Assert.IsTrue(e.StatusKey.HasValue);
            Assert.AreEqual(StatusKeys.Block, e.StatusKey.Value);
            var adjacent = (AdjacentCardIs)e.Condition;
            Assert.AreEqual(AdjacentDirection.Next, adjacent.Direction);
            Assert.AreEqual(Side.Enemy, adjacent.Side);
        }

        [Test]
        public void Maps_fate_card()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "pull_forward", Name = "앞당김", Side = Side.Player, Type = CardType.Skill,
                Category = CardCategory.Intervention, Cost = 1, Intervention = InterventionKind.ChangeInitiative, InterventionAmount = -2
            });

            Assert.AreEqual(CardCategory.Intervention, def.Category);
            Assert.AreEqual(0, def.Effects.Count);
            Assert.AreEqual(InterventionActionKeys.ChangeInitiative, def.InterventionAction.Key);
            Assert.AreEqual(1, def.InterventionAction.Cost);
            Assert.AreEqual(-2, def.InterventionAction.Amount);
        }

        [Test]
        public void Maps_slow_and_haste_apply_status()
        {
            var slow = CardSpecMapper.ToEffectData(new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 3, Status = StatusKindRef.Slow,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.TargetEnemy });
            Assert.AreEqual(StatusKeys.Slow, slow.StatusKey.Value);

            var haste = CardSpecMapper.ToEffectData(new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 3, Status = StatusKindRef.Haste,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.Self });
            Assert.AreEqual(StatusKeys.Haste, haste.StatusKey.Value);
        }

        [Test]
        public void Maps_no_following_enemy_card_condition()
        {
            var effect = CardSpecMapper.ToEffectData(new EffectSpec
            {
                Kind = EffectKind.Damage,
                Amount = 2,
                Condition = ConditionKind.NoFollowingEnemyCard,
                SuccessAmount = 7
            });

            Assert.AreEqual(2, effect.Amount);
            Assert.AreEqual(7, effect.SuccessAmount);
            var condition = (NoFollowingCardOfSide)effect.Condition;
            Assert.AreEqual(Side.Enemy, condition.Side);
        }
    }
}
