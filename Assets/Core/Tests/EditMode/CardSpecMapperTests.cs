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
    /// <summary>저작 스펙 → CardDefinition 변환(전투 실행 계약 스펙 §4). 위치는 카드의 진영 축에서, 조건은
    /// 카드 단위로, 성공 수치·생략·결과 참조는 효과 단위로 옮겨진다.</summary>
    public class CardSpecMapperTests
    {
        private static ExecutionCardSpec Execution(
            EffectSpec effect, CardTargetsSpec targets = null, StartConditionSpec condition = null, Side side = Side.Player)
            => new ExecutionCardSpec
            {
                Id = "card", Name = "카드", Side = side,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Targets = targets, StartCondition = condition,
                Effects = new[] { effect }
            };

        private static EffectData Single(ExecutionCardSpec spec) => CardSpecMapper.ToDefinition(spec).Effects[0];

        /// <summary>첫 효과가 실행에서 고를 위치 키(효과 진영 + 카드의 그 진영 축).</summary>
        private static CardTargetKey? TargetOfSingle(ExecutionCardSpec spec)
        {
            var def = CardSpecMapper.ToDefinition(spec);
            return def.TargetOf(def.Effects[0]);
        }

        private static CardTargetKey Key(CardTargetFaction faction, CardTargetRange range) => new CardTargetKey(faction, range);

        private static CardTargetsSpec Enemy(CardTargetRange range) => new CardTargetsSpec { Enemy = range };

        private static CardTargetsSpec Ally(CardTargetRange range) => new CardTargetsSpec { Ally = range };

        [Test]
        public void Target_range_schema_contains_only_approved_ranges()
        {
            CollectionAssert.AreEqual(
                new[] { "Self", "FrontOne", "FrontTwo", "BackOne", "BackTwo", "All" },
                Enum.GetNames(typeof(CardTargetRange)));
        }

        [Test]
        public void Maps_flat_damage_action_from_the_enemy_axis()
        {
            var def = CardSpecMapper.ToDefinition(Execution(
                new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, Value = 3 },
                Enemy(CardTargetRange.FrontOne)));

            Assert.AreEqual(CardCategory.Execution, def.Category);
            Assert.AreEqual(1, def.EnergyCost);
            Assert.IsNull(def.StartCondition);
            var effect = def.Effects[0];
            Assert.AreEqual(EffectKeys.Damage, effect.Key);
            Assert.AreEqual("hit", effect.Id);
            Assert.AreEqual(3, effect.EffectValue);
            Assert.AreEqual(CardTargetFaction.Enemy, effect.TargetFaction);
            Assert.AreEqual(CardTargetRange.FrontOne, def.EnemyTarget);
            Assert.IsNull(def.AllyTarget);
        }

        [Test]
        public void Start_condition_moves_to_the_card_and_success_value_stays_on_the_effect()
        {
            var def = CardSpecMapper.ToDefinition(Execution(
                new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, Value = 2, SuccessEffectValue = 8 },
                Enemy(CardTargetRange.FrontOne),
                new StartConditionSpec { Kind = ConditionKind.FirstToTrigger }));

            Assert.IsInstanceOf<FirstToTrigger>(def.StartCondition);
            Assert.AreEqual(2, def.Effects[0].EffectValue);
            Assert.AreEqual(8, def.Effects[0].SuccessEffectValue);
        }

        [Test]
        public void Maps_conditional_apply_status_on_self()
        {
            var def = CardSpecMapper.ToDefinition(Execution(
                new ApplyStatusSpec
                {
                    Id = "cover", TargetFaction = CardTargetFaction.Ally, Count = 2, SuccessEffectValue = 7,
                    Status = StatusKeyRef.Of(StatusKeys.Block)
                },
                Ally(CardTargetRange.Self),
                new StartConditionSpec { Kind = ConditionKind.NextIsEnemyDamageCard }));

            var effect = def.Effects[0];
            Assert.AreEqual(2, effect.EffectValue);
            Assert.AreEqual(7, effect.SuccessEffectValue);
            Assert.AreEqual(StatusKeys.Block, ((ApplyStatusPayload)effect.Payload).Key);
            Assert.AreEqual(Key(CardTargetFaction.Ally, CardTargetRange.Self), def.TargetOf(effect));
            var adjacent = (AdjacentCardHasEffect)def.StartCondition;
            Assert.AreEqual(AdjacentDirection.Next, adjacent.Direction);
            Assert.AreEqual(Side.Enemy, adjacent.Side);
        }

        [Test]
        public void Maps_fate_card()
        {
            var def = CardSpecMapper.ToDefinition(new InterventionCardSpec
            {
                Id = "pull_forward", Name = "앞당김", Side = Side.Player,
                Category = CardCategory.Intervention, EnergyCost = 1,
                Intervention = new ChangeExecutionOrderSpec { Delta = -2 }
            });

            Assert.AreEqual(CardCategory.Intervention, def.Category);
            Assert.AreEqual(0, def.Effects.Count);
            Assert.AreEqual(InterventionActionKeys.ChangeExecutionOrder, def.InterventionAction.Key);
            Assert.AreEqual(1, def.InterventionAction.InterventionCost);
            Assert.AreEqual(-2, ((ChangeExecutionOrderPayload)def.InterventionAction.Payload).Delta);
        }

        [TestCase(CardTargetRange.FrontOne)]
        [TestCase(CardTargetRange.FrontTwo)]
        [TestCase(CardTargetRange.BackOne)]
        [TestCase(CardTargetRange.BackTwo)]
        [TestCase(CardTargetRange.All)]
        public void Enemy_axis_range_is_where_damage_picks_its_targets(CardTargetRange range)
        {
            Assert.AreEqual(
                Key(CardTargetFaction.Enemy, range),
                TargetOfSingle(Execution(
                    new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, Value = 4 }, Enemy(range))));
        }

        [Test]
        public void Enemy_axis_status_targets_the_enemy_at_that_range()
        {
            var spec = Execution(
                new ApplyStatusSpec
                {
                    Id = "slow", TargetFaction = CardTargetFaction.Enemy, Count = 2, Status = StatusKeyRef.Of(StatusKeys.Slow)
                },
                Enemy(CardTargetRange.BackOne));

            Assert.AreEqual(StatusKeys.Slow, ((ApplyStatusPayload)Single(spec).Payload).Key);
            Assert.AreEqual(Key(CardTargetFaction.Enemy, CardTargetRange.BackOne), TargetOfSingle(spec));
        }

        [Test]
        public void Ally_all_status_targets_every_party_member()
        {
            var spec = Execution(
                new ApplyStatusSpec
                {
                    Id = "wall", TargetFaction = CardTargetFaction.Ally, Count = 4, Status = StatusKeyRef.Of(StatusKeys.Block)
                },
                Ally(CardTargetRange.All));

            Assert.AreEqual(Key(CardTargetFaction.Ally, CardTargetRange.All), TargetOfSingle(spec));
        }

        [Test]
        public void Ally_position_status_targets_the_party_at_that_range()
        {
            var spec = Execution(
                new ApplyStatusSpec
                {
                    Id = "cover", TargetFaction = CardTargetFaction.Ally, Count = 4, Status = StatusKeyRef.Of(StatusKeys.Block)
                },
                Ally(CardTargetRange.FrontOne));

            Assert.AreEqual(Key(CardTargetFaction.Ally, CardTargetRange.FrontOne), TargetOfSingle(spec));
        }

        [Test]
        public void Enemy_card_self_status_stays_on_the_enemy_itself()
        {
            var spec = Execution(
                new ApplyStatusSpec
                {
                    Id = "guard", TargetFaction = CardTargetFaction.Enemy, Count = 3, Status = StatusKeyRef.Of(StatusKeys.Block)
                },
                Enemy(CardTargetRange.Self),
                side: Side.Enemy);

            Assert.AreEqual(Key(CardTargetFaction.Enemy, CardTargetRange.Self), TargetOfSingle(spec));
        }

        [Test]
        public void Enemy_card_damage_uses_the_ally_axis()
        {
            var spec = Execution(
                new DamageSpec { Id = "jab", TargetFaction = CardTargetFaction.Ally, Value = 4 },
                Ally(CardTargetRange.FrontOne),
                side: Side.Enemy);

            Assert.AreEqual(Key(CardTargetFaction.Ally, CardTargetRange.FrontOne), TargetOfSingle(spec));
        }

        [Test]
        public void Maps_move_formation_effect_key()
        {
            var effect = Single(Execution(
                new MoveFormationSpec { Id = "step", TargetFaction = CardTargetFaction.Ally, Value = -1 },
                Ally(CardTargetRange.Self)));

            Assert.AreEqual(EffectKeys.MoveFormation, effect.Key);
            Assert.AreEqual(-1, effect.EffectValue);
        }

        [Test]
        public void Maps_consumption_mode_and_result_references()
        {
            var def = CardSpecMapper.ToDefinition(new ExecutionCardSpec
            {
                Id = "burst", Name = "파열", Side = Side.Player, Category = CardCategory.Execution,
                BaseExecutionOrder = 5,
                Targets = Enemy(CardTargetRange.FrontOne),
                Effects = new EffectSpec[]
                {
                    new ConsumeStatusSpec
                    {
                        Id = "pay", TargetFaction = CardTargetFaction.Enemy,
                        Status = StatusKeyRef.Of(StatusKeys.Poison), Amount = 3, Mode = ConsumptionMode.Exact
                    },
                    new DamageSpec
                    {
                        Id = "hit", TargetFaction = CardTargetFaction.Enemy, Value = 2,
                        ScaleBy = new EffectScalingSpec { SourceEffectId = "pay", PerConsumed = 2 }
                    },
                    new GrantNextTurnFateSpec
                    {
                        Id = "reward", Value = 1,
                        Requires = new EffectRequirementSpec { SourceEffectId = "pay", MinimumConsumed = 1 }
                    }
                }
            });

            var consume = (ConsumeStatusPayload)def.Effects[0].Payload;
            Assert.AreEqual(3, consume.Amount);
            Assert.AreEqual(ConsumptionMode.Exact, consume.Mode);
            Assert.AreEqual(new EffectResultScaling("pay", 2), def.Effects[1].Scaling);
            Assert.AreEqual(new EffectResultRequirement("pay", 1), def.Effects[2].Requirement);
            Assert.IsNull(def.Effects[2].TargetFaction, "대상을 고르지 않는 효과는 진영이 없다");
        }

        [Test]
        public void Maps_previous_executed_player_attack_condition()
        {
            var def = CardSpecMapper.ToDefinition(Execution(
                new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, SuccessEffectValue = 4 },
                Enemy(CardTargetRange.FrontOne),
                new StartConditionSpec { Kind = ConditionKind.PrevExecutedIsPlayerDamageCard }));

            Assert.AreEqual(new PreviousExecutedCardHasEffect(Side.Player, EffectKeys.Damage), def.StartCondition);
        }

        [Test]
        public void Maps_no_following_enemy_card_condition()
        {
            var def = CardSpecMapper.ToDefinition(Execution(
                new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, Value = 2, SuccessEffectValue = 7 },
                Enemy(CardTargetRange.FrontOne),
                new StartConditionSpec { Kind = ConditionKind.NoFollowingEnemyCard }));

            Assert.AreEqual(Side.Enemy, ((NoFollowingCardOfSide)def.StartCondition).Side);
            Assert.AreEqual(7, def.Effects[0].SuccessEffectValue);
        }
    }
}
