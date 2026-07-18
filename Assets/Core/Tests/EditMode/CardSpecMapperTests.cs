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
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new[] { new EffectSpec { Kind = EffectKind.Damage, EffectValue = 3 } }
            });

            Assert.AreEqual(CardCategory.Execution, def.Category);
            Assert.AreEqual(1, def.EnergyCost);
            Assert.AreEqual(1, def.Effects.Count);
            Assert.AreEqual(EffectKeys.Damage, def.Effects[0].Key);
            Assert.AreEqual(3, def.Effects[0].EffectValue);
            Assert.IsNull(def.Effects[0].Condition);
        }

        [Test]
        public void Maps_conditional_damage()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new[] { new EffectSpec {
                    Kind = EffectKind.Damage, EffectValue = 2,
                    Condition = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 } }
            });

            var e = def.Effects[0];
            Assert.AreEqual(2, e.EffectValue);
            Assert.AreEqual(8, e.SuccessEffectValue);
            Assert.IsInstanceOf<FirstToTrigger>(e.Condition);
        }

        [Test]
        public void Maps_conditional_apply_status()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "cover", Name = "엄호", Side = Side.Player, Type = CardType.Defense,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new[] { new EffectSpec {
                    Kind = EffectKind.ApplyStatus, EffectValue = 2, Status = StatusKindRef.Block,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                    Condition = ConditionKind.NextIsEnemyAttack, SuccessEffectValue = 7 } }
            });

            var e = def.Effects[0];
            Assert.AreEqual(EffectKeys.ApplyStatus, e.Key);
            Assert.AreEqual(2, e.EffectValue);
            Assert.AreEqual(7, e.SuccessEffectValue);
            Assert.IsInstanceOf<ApplyStatusPayload>(e.Payload);
            Assert.AreEqual(StatusKeys.Block, ((ApplyStatusPayload)e.Payload).Key);
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
                Category = CardCategory.Intervention, EnergyCost = 1, Intervention = InterventionKind.ChangeExecutionOrder, InterventionEffectValue = -2
            });

            Assert.AreEqual(CardCategory.Intervention, def.Category);
            Assert.AreEqual(0, def.Effects.Count);
            Assert.AreEqual(InterventionActionKeys.ChangeExecutionOrder, def.InterventionAction.Key);
            Assert.AreEqual(1, def.InterventionAction.InterventionCost);
            Assert.AreEqual(-2, def.InterventionAction.EffectValue);
        }

        [Test]
        public void Maps_slow_and_haste_apply_status()
        {
            var slow = CardSpecMapper.ToEffectData(new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 3, Status = StatusKindRef.Slow,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.TargetEnemy });
            Assert.AreEqual(StatusKeys.Slow, ((ApplyStatusPayload)slow.Payload).Key);

            var haste = CardSpecMapper.ToEffectData(new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 3, Status = StatusKindRef.Haste,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.Self });
            Assert.AreEqual(StatusKeys.Haste, ((ApplyStatusPayload)haste.Payload).Key);
        }

        [Test]
        public void Maps_no_following_enemy_card_condition()
        {
            var effect = CardSpecMapper.ToEffectData(new EffectSpec
            {
                Kind = EffectKind.Damage,
                EffectValue = 2,
                Condition = ConditionKind.NoFollowingEnemyCard,
                SuccessEffectValue = 7
            });

            Assert.AreEqual(2, effect.EffectValue);
            Assert.AreEqual(7, effect.SuccessEffectValue);
            var condition = (NoFollowingCardOfSide)effect.Condition;
            Assert.AreEqual(Side.Enemy, condition.Side);
        }

        [Test]
        public void Maps_second_from_front_selector()
        {
            var effect = CardSpecMapper.ToEffectData(new EffectSpec
            {
                Kind = EffectKind.Damage,
                EffectValue = 4,
                Selector = TargetSelectorRef.SecondFromFront
            });

            Assert.AreEqual(TargetSelector.SecondFromFront, effect.TargetSelector);
        }

        [Test]
        public void Maps_all_party_members_status_target()
        {
            var effect = CardSpecMapper.ToEffectData(new EffectSpec
            {
                Kind = EffectKind.ApplyStatus,
                EffectValue = 4,
                Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn,
                Target = StatusApplyTarget.AllPartyMembers
            });

            Assert.AreEqual(
                StatusApplyTarget.AllPartyMembers,
                ((ApplyStatusPayload)effect.Payload).Target);
        }

        [Test]
        public void Maps_move_formation_effect_key()
        {
            var effect = CardSpecMapper.ToEffectData(new EffectSpec
            {
                Kind = EffectKind.MoveFormation,
                EffectValue = -1
            });

            Assert.AreEqual(EffectKeys.MoveFormation, effect.Key);
        }

        [Test]
        public void Maps_previous_executed_player_attack_condition()
        {
            var effect = CardSpecMapper.ToEffectData(new EffectSpec
            {
                Kind = EffectKind.Damage,
                Condition = ConditionKind.PrevExecutedIsPlayerAttack,
                SuccessEffectValue = 4
            });

            Assert.AreEqual(
                new PreviousExecutedCardIs(Side.Player, CardType.Attack),
                effect.Condition);
        }
    }
}
