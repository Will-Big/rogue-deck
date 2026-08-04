using System;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    public class CardSpecMapperTests
    {
        [Test]
        public void Target_selector_schema_contains_only_approved_ranges()
        {
            CollectionAssert.AreEqual(
                new[] { "FrontOne", "FrontTwo", "BackOne", "BackTwo", "All" },
                Enum.GetNames(typeof(TargetSelector)));
        }

        [Test]
        public void Removed_serialized_selector_values_are_not_reused()
        {
            Assert.IsFalse(Enum.IsDefined(typeof(TargetSelectorRef), 2));
            Assert.IsFalse(Enum.IsDefined(typeof(TargetSelectorRef), 4));
        }

        [Test]
        public void Maps_flat_damage_action()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "slash", Name = "베기", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new EffectSpec[] { new DamageSpec { Value = 3 } }
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
                Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new EffectSpec[] { new DamageSpec { Value = 2,
                    Condition = new ConditionSpec { Kind = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 } } }
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
                Id = "cover", Name = "엄호", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new EffectSpec[] { new ApplyStatusSpec { Count = 2,
                    Status = StatusKeyRef.Of(StatusKeys.Block),
                    Target = StatusApplyTarget.Self,
                    Condition = new ConditionSpec { Kind = ConditionKind.NextIsEnemyDamageCard, SuccessEffectValue = 7 } } }
            });

            var e = def.Effects[0];
            Assert.AreEqual(EffectKeys.ApplyStatus, e.Key);
            Assert.AreEqual(2, e.EffectValue);
            Assert.AreEqual(7, e.SuccessEffectValue);
            Assert.IsInstanceOf<ApplyStatusPayload>(e.Payload);
            Assert.AreEqual(StatusKeys.Block, ((ApplyStatusPayload)e.Payload).Key);
            var adjacent = (AdjacentCardHasEffect)e.Condition;
            Assert.AreEqual(AdjacentDirection.Next, adjacent.Direction);
            Assert.AreEqual(Side.Enemy, adjacent.Side);
        }

        [Test]
        public void Maps_fate_card()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "pull_forward", Name = "앞당김", Side = Side.Player,
                Category = CardCategory.Intervention, EnergyCost = 1,
                Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
                InterventionEffectValue = -2
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
            var slow = new ApplyStatusSpec {
                Count = 2, Status = StatusKeyRef.Of(StatusKeys.Slow),
                Target = StatusApplyTarget.TargetEnemy
            }.ToEffectData();
            Assert.AreEqual(StatusKeys.Slow, ((ApplyStatusPayload)slow.Payload).Key);

            var haste = new ApplyStatusSpec {
                Count = 2, Status = StatusKeyRef.Of(StatusKeys.Haste),
                Target = StatusApplyTarget.Self
            }.ToEffectData();
            Assert.AreEqual(StatusKeys.Haste, ((ApplyStatusPayload)haste.Payload).Key);
        }

        [Test]
        public void Maps_no_following_enemy_card_condition()
        {
            var effect = new DamageSpec
            {
                Value = 2,
                Condition = new ConditionSpec { Kind = ConditionKind.NoFollowingEnemyCard, SuccessEffectValue = 7 }
            }.ToEffectData();

            Assert.AreEqual(2, effect.EffectValue);
            Assert.AreEqual(7, effect.SuccessEffectValue);
            var condition = (NoFollowingCardOfSide)effect.Condition;
            Assert.AreEqual(Side.Enemy, condition.Side);
        }

        [Test]
        public void Maps_front_two_selector()
        {
            var effect = new DamageSpec
            {
                Value = 4,
                Selector = TargetSelectorRef.FrontTwo
            }.ToEffectData();

            Assert.AreEqual(TargetSelector.FrontTwo, effect.TargetSelector);
        }

        [Test]
        public void Maps_back_two_selector()
        {
            var effect = new DamageSpec
            {
                Value = 4,
                Selector = TargetSelectorRef.BackTwo
            }.ToEffectData();

            Assert.AreEqual(TargetSelector.BackTwo, effect.TargetSelector);
        }

        [Test]
        public void Maps_all_party_members_status_target()
        {
            var effect = new ApplyStatusSpec
            {
                Count = 4,
                Status = StatusKeyRef.Of(StatusKeys.Block),
                Target = StatusApplyTarget.AllPartyMembers
            }.ToEffectData();

            Assert.AreEqual(
                StatusApplyTarget.AllPartyMembers,
                ((ApplyStatusPayload)effect.Payload).Target);
        }

        [Test]
        public void Maps_move_formation_effect_key()
        {
            var effect = new MoveFormationSpec { Value = -1 }.ToEffectData();

            Assert.AreEqual(EffectKeys.MoveFormation, effect.Key);
        }

        [Test]
        public void Maps_previous_executed_player_attack_condition()
        {
            var effect = new DamageSpec
            {
                Condition = new ConditionSpec { Kind = ConditionKind.PrevExecutedIsPlayerDamageCard, SuccessEffectValue = 4 }
            }.ToEffectData();

            Assert.AreEqual(
                new PreviousExecutedCardHasEffect(Side.Player, EffectKeys.Damage),
                effect.Condition);
        }
    }
}
