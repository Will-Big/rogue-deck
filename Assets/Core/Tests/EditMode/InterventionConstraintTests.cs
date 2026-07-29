using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class InterventionConstraintTests
    {
        private static CombatState StateWithZone(params ExecutionCardInstance[] cards)
        {
            var state = new CombatState { FateEnergy = 10 };
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            foreach (var card in cards)
            {
                state.Zone.Add(card);
            }
            return state;
        }

        private static ExecutionCardInstance Card(string id, Side side, int order)
            => new ExecutionCardInstance(new CardDefinition(
                    id, id, side, order, new[] { new EffectData(EffectKeys.Damage, 1) }))
                { OwnerId = side == Side.Player ? CombatState.SoloPlayerId : "goblin" };

        [Test]
        public void Side_filtered_change_rejects_wrong_side_and_keeps_energy()
        {
            var playerCard = Card("mine", Side.Player, 4);
            var enemyCard = Card("theirs", Side.Enemy, 5);
            var state = StateWithZone(playerCard, enemyCard);
            var action = new InterventionActionData(
                InterventionActionKeys.ChangeExecutionOrder, 1, -1,
                targetSide: Side.Player, requireAdjacentTargets: false);
            var resolver = new InterventionPlayResolver(NewActions());

            var rejected = resolver.Resolve(state, new[] { new InterventionPlay(action, enemyCard) });
            Assert.AreEqual(0, rejected.AppliedCount);
            Assert.AreEqual(10, state.FateEnergy);
            Assert.AreEqual(5, enemyCard.ExecutionOrder);

            var applied = resolver.Resolve(state, new[] { new InterventionPlay(action, playerCard) });
            Assert.AreEqual(1, applied.AppliedCount);
            Assert.AreEqual(3, playerCard.ExecutionOrder);
        }

        [Test]
        public void Adjacent_swap_rejects_non_adjacent_targets()
        {
            var a = Card("a", Side.Player, 3);
            var b = Card("b", Side.Enemy, 5);
            var c = Card("c", Side.Player, 7);
            var state = StateWithZone(a, b, c);
            var action = new InterventionActionData(
                InterventionActionKeys.SwapExecutionOrder, 1, 0,
                targetSide: null, requireAdjacentTargets: true);
            var resolver = new InterventionPlayResolver(NewActions());

            // a(0)와 c(2)는 비인접 → 거부.
            var rejected = resolver.Resolve(state, new[] { new InterventionPlay(action, a, c) });
            Assert.AreEqual(0, rejected.AppliedCount);
            Assert.AreEqual(3, a.ExecutionOrder);
            Assert.AreEqual(10, state.FateEnergy);

            // a(0)와 b(1)는 인접 → 교환.
            var applied = resolver.Resolve(state, new[] { new InterventionPlay(action, a, b) });
            Assert.AreEqual(1, applied.AppliedCount);
            Assert.AreEqual(5, a.ExecutionOrder);
            Assert.AreEqual(3, b.ExecutionOrder);
        }

        [Test]
        public void Unconstrained_actions_keep_existing_behavior()
        {
            var a = Card("a", Side.Player, 3);
            var c = Card("c", Side.Player, 7);
            var state = StateWithZone(a, c);
            var action = new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, 1, 0);

            var applied = new InterventionPlayResolver(NewActions())
                .Resolve(state, new[] { new InterventionPlay(action, a, c) });

            Assert.AreEqual(1, applied.AppliedCount); // 기존 자리 교환은 인접 불요
        }

        private static InterventionActionRegistry NewActions()
        {
            var actions = new InterventionActionRegistry();
            actions.Register(new ChangeExecutionOrderHandler());
            actions.Register(new SwapExecutionOrderHandler());
            actions.Register(new LockHandler());
            return actions;
        }
    }
}
