using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class InterventionActionTests
    {
        private static EffectRegistry EffectRegistry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        private static ExecutionCardInstance Card(
            string id,
            Side side,
            int executionOrder,
            EffectData effect)
        {
            var def = new CardDefinition(id, id, side, executionOrder, new[] { effect });
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void ChangeExecutionOrder_reads_delta_from_payload()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var card = Card("quick_cut", Side.Player, 4, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(
                InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1,
                new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null));
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            new ChangeExecutionOrderHandler().Apply(ctx);

            Assert.AreEqual(2, card.ExecutionOrder);
            Assert.AreEqual(2, state.FateEnergy);
        }

        [Test]
        public void Lock_needs_no_payload()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var card = Card("quick_cut", Side.Player, 4, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(InterventionActionKeys.Lock, interventionCost: 1);
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            new LockHandler().Apply(ctx);

            Assert.IsTrue(card.IsLocked);
            Assert.IsNull(action.Payload);
        }

        [Test]
        public void ChangeExecutionOrder_spends_cost_and_changes_target_executionOrder()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var card = Card("quick_cut", Side.Player, 4, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null));
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            new ChangeExecutionOrderHandler().Apply(ctx);

            Assert.AreEqual(2, card.ExecutionOrder);
            Assert.AreEqual(2, state.FateEnergy);
            Assert.AreEqual(1, ctx.FateEnergySpent);
        }

        [Test]
        public void ChangeExecutionOrder_rejects_when_fate_energy_is_insufficient()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 0 };
            var card = Card("quick_cut", Side.Player, 4, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null));
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            Assert.IsFalse(new ChangeExecutionOrderHandler().CanApply(ctx));
            new ChangeExecutionOrderHandler().Apply(ctx);

            Assert.AreEqual(4, card.ExecutionOrder);
            Assert.AreEqual(0, state.FateEnergy);
            Assert.AreEqual(0, ctx.FateEnergySpent);
        }

        [Test]
        public void ChangeExecutionOrder_can_turn_basic_condition_into_success_before_resolution()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            var enemy = Card("enemy_jab", Side.Enemy, 1, new EffectData(EffectKeys.Damage, 1));
            var player = Card(
                "quick_cut",
                Side.Player,
                2,
                EffectData.Conditional(
                    EffectKeys.Damage,
                    effectValue: 2,
                    condition: new FirstToTrigger(),
                    successEffectValue: 10));
            state.Zone.Add(enemy);
            state.Zone.Add(player);

            var beforeEvents = new TurnResolver(EffectRegistry()).Resolve(CloneStateForResolution(state), 0);
            var before = (CardResolved)beforeEvents[2];
            Assert.AreEqual(ConditionTier.Basic, before.ConditionTier);
            Assert.AreEqual(2, before.DamageDealt);

            var action = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null));
            var intervention = new InterventionActionRegistry();
            intervention.Register(new ChangeExecutionOrderHandler());
            intervention.Resolve(InterventionActionKeys.ChangeExecutionOrder)
                .Apply(new InterventionPlayContext { State = state, Target = player, Intervention = action });

            var afterEvents = new TurnResolver(EffectRegistry()).Resolve(state, 0);
            var after = (CardResolved)afterEvents[1];

            Assert.AreEqual(0, player.ExecutionOrder);
            Assert.AreEqual(ConditionTier.Success, after.ConditionTier);
            Assert.AreEqual(10, after.DamageDealt);
            Assert.AreEqual(2, state.FateEnergy);
        }

        [Test]
        public void SwapExecutionOrder_spends_cost_and_swaps_two_target_executionOrders()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var first = Card("first", Side.Player, 1, new EffectData(EffectKeys.Damage, 2));
            var second = Card("second", Side.Player, 5, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false));
            var ctx = new InterventionPlayContext
            {
                State = state,
                Target = first,
                SecondaryTarget = second,
                Intervention = action
            };

            new SwapExecutionOrderHandler().Apply(ctx);

            Assert.AreEqual(5, first.ExecutionOrder);
            Assert.AreEqual(1, second.ExecutionOrder);
            Assert.AreEqual(2, state.FateEnergy);
            Assert.AreEqual(1, ctx.FateEnergySpent);
        }

        [Test]
        public void SwapExecutionOrder_can_turn_basic_condition_into_success_before_resolution()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            var enemy = Card("enemy_jab", Side.Enemy, 1, new EffectData(EffectKeys.Damage, 1));
            var player = Card(
                "quick_cut",
                Side.Player,
                2,
                EffectData.Conditional(
                    EffectKeys.Damage,
                    effectValue: 2,
                    condition: new FirstToTrigger(),
                    successEffectValue: 10));
            state.Zone.Add(enemy);
            state.Zone.Add(player);

            var action = new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false));
            new SwapExecutionOrderHandler().Apply(new InterventionPlayContext
            {
                State = state,
                Target = enemy,
                SecondaryTarget = player,
                Intervention = action
            });

            var events = new TurnResolver(EffectRegistry()).Resolve(state, 0);
            var resolved = (CardResolved)events[1];

            Assert.AreEqual(2, enemy.ExecutionOrder);
            Assert.AreEqual(1, player.ExecutionOrder);
            Assert.AreEqual(ConditionTier.Success, resolved.ConditionTier);
            Assert.AreEqual(10, resolved.DamageDealt);
            Assert.AreEqual(2, state.FateEnergy);
        }

        [Test]
        public void Lock_spends_cost_and_locks_target_card()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var card = Card("quick_cut", Side.Player, 2, new EffectData(EffectKeys.Damage, 2));
            var action = new InterventionActionData(InterventionActionKeys.Lock, interventionCost: 1);
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            new LockHandler().Apply(ctx);

            Assert.IsTrue(card.IsLocked);
            Assert.AreEqual(2, state.FateEnergy);
            Assert.AreEqual(1, ctx.FateEnergySpent);
        }

        [Test]
        public void ChangeExecutionOrder_rejects_locked_target()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var card = Card("quick_cut", Side.Player, 4, new EffectData(EffectKeys.Damage, 2));
            card.IsLocked = true;
            var action = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null));
            var ctx = new InterventionPlayContext { State = state, Target = card, Intervention = action };

            Assert.IsFalse(new ChangeExecutionOrderHandler().CanApply(ctx));
            new ChangeExecutionOrderHandler().Apply(ctx);

            Assert.AreEqual(4, card.ExecutionOrder);
            Assert.AreEqual(3, state.FateEnergy);
            Assert.AreEqual(0, ctx.FateEnergySpent);
        }

        [Test]
        public void SwapExecutionOrder_rejects_when_either_target_is_locked()
        {
            var state = new CombatState(TestContent.Statuses()) { FateEnergy = 3 };
            var first = Card("first", Side.Player, 1, new EffectData(EffectKeys.Damage, 2));
            var second = Card("second", Side.Player, 5, new EffectData(EffectKeys.Damage, 2));
            second.IsLocked = true;
            var action = new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false));
            var ctx = new InterventionPlayContext
            {
                State = state,
                Target = first,
                SecondaryTarget = second,
                Intervention = action
            };

            Assert.IsFalse(new SwapExecutionOrderHandler().CanApply(ctx));
            new SwapExecutionOrderHandler().Apply(ctx);

            Assert.AreEqual(1, first.ExecutionOrder);
            Assert.AreEqual(5, second.ExecutionOrder);
            Assert.AreEqual(3, state.FateEnergy);
            Assert.AreEqual(0, ctx.FateEnergySpent);
        }

        private static CombatState CloneStateForResolution(CombatState source)
        {
            var clone = new CombatState(source.StatusContent)
            {
                FateEnergy = source.FateEnergy,
                FateEnergyPerTurn = source.FateEnergyPerTurn,
                RngSeed = source.RngSeed
            };
            clone.AddSoloPlayer(source.Party[0].Hp);

            foreach (var enemy in source.Enemies)
            {
                clone.Enemies.Add(new Enemy(enemy.Id, enemy.Hp));
            }

            foreach (var card in source.Zone.Cards)
            {
                clone.Zone.Add(new ExecutionCardInstance(card.Def)
                {
                    ExecutionOrder = card.ExecutionOrder,
                    TargetId = card.TargetId
                });
            }

            return clone;
        }
    }
}
