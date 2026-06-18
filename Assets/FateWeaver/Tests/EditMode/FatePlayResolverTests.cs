using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;

namespace FateWeaver.Tests
{
    public class FatePlayResolverTests
    {
        private static FateActionRegistry Registry()
        {
            var r = new FateActionRegistry();
            r.Register(new ChangeInitiativeHandler());
            r.Register(new LockHandler());
            return r;
        }

        private static ActionCardInstance Card(string id, int initiative)
        {
            var def = new CardDefinition(id, id, Side.Player, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, 1) });
            return new ActionCardInstance(def);
        }

        [Test]
        public void Applies_multiple_fate_plays_in_order()
        {
            var state = new CombatState { FateEnergy = 3 };
            var card = Card("quick_cut", 5);
            var plays = new[]
            {
                new FatePlay(new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2), card),
                new FatePlay(new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: 1), card)
            };

            var result = new FatePlayResolver(Registry()).Resolve(state, plays);

            Assert.AreEqual(4, card.Initiative);
            Assert.AreEqual(1, state.FateEnergy);
            Assert.AreEqual(2, result.AppliedCount);
            Assert.AreEqual(-1, result.RejectedIndex);
            Assert.AreEqual(2, result.FateEnergySpent);
        }

        [Test]
        public void Stops_on_insufficient_fate_energy_and_keeps_previous_changes()
        {
            var state = new CombatState { FateEnergy = 1 };
            var card = Card("quick_cut", 5);
            var plays = new[]
            {
                new FatePlay(new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2), card),
                new FatePlay(new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2), card)
            };

            var result = new FatePlayResolver(Registry()).Resolve(state, plays);

            Assert.AreEqual(3, card.Initiative);
            Assert.AreEqual(0, state.FateEnergy);
            Assert.AreEqual(1, result.AppliedCount);
            Assert.AreEqual(1, result.RejectedIndex);
            Assert.AreEqual(1, result.FateEnergySpent);
        }

        [Test]
        public void Can_apply_swap_initiative_play_through_resolver()
        {
            var state = new CombatState { FateEnergy = 2 };
            var first = Card("first", 1);
            var second = Card("second", 5);
            var registry = Registry();
            registry.Register(new SwapInitiativeHandler());
            var plays = new[]
            {
                new FatePlay(new FateActionData(FateActionKeys.SwapInitiative, cost: 1, amount: 0), first, second)
            };

            var result = new FatePlayResolver(registry).Resolve(state, plays);

            Assert.AreEqual(5, first.Initiative);
            Assert.AreEqual(1, second.Initiative);
            Assert.AreEqual(1, state.FateEnergy);
            Assert.AreEqual(1, result.AppliedCount);
            Assert.AreEqual(-1, result.RejectedIndex);
        }

        [Test]
        public void Stops_when_a_play_targets_a_locked_card_and_keeps_previous_changes()
        {
            var state = new CombatState { FateEnergy = 3 };
            var first = Card("first", 5);
            var second = Card("second", 3);
            var plays = new[]
            {
                new FatePlay(new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2), first),
                new FatePlay(new FateActionData(FateActionKeys.Lock, cost: 1, amount: 0), first),
                new FatePlay(new FateActionData(FateActionKeys.SwapInitiative, cost: 1, amount: 0), first, second)
            };
            var registry = Registry();
            registry.Register(new SwapInitiativeHandler());

            var result = new FatePlayResolver(registry).Resolve(state, plays);

            Assert.AreEqual(3, first.Initiative);
            Assert.AreEqual(3, second.Initiative);
            Assert.IsTrue(first.IsLocked);
            Assert.AreEqual(1, state.FateEnergy);
            Assert.AreEqual(2, result.AppliedCount);
            Assert.AreEqual(2, result.RejectedIndex);
            Assert.AreEqual(2, result.FateEnergySpent);
        }
    }
}
