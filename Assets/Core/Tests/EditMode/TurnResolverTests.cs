using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class TurnResolverTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            r.Register(new ApplyStatusHandler());
            return r;
        }

        private static ExecutionCardInstance Card(string id, Side side, int executionOrder, int damage)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void Resolves_in_executionOrder_order_and_emits_timeline()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            // player card has higher executionOrder (2) than enemy card (1) => enemy resolves first
            state.Zone.Add(Card("strike", Side.Player, 2, 5));
            state.Zone.Add(Card("jab", Side.Enemy, 1, 3));

            var events = new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            Assert.AreEqual(27, state.Party[0].Hp);  // took 3
            Assert.AreEqual(7, state.Enemies[0].Hp); // took 5

            Assert.IsInstanceOf<TurnStarted>(events[0]);
            var first = (CardResolved)events[1];
            var second = (CardResolved)events[2];
            Assert.AreEqual("jab", first.CardId);    // enemy first (lower executionOrder)
            Assert.AreEqual("strike", second.CardId);
            Assert.AreEqual(5, second.DamageDealt);
            Assert.IsInstanceOf<TurnEnded>(events[^1]);
            Assert.AreEqual(Outcome.Ongoing, ((TurnEnded)events[^1]).Outcome);
        }

        [Test]
        public void Reports_win_when_all_enemies_dead()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 4));
            state.Zone.Add(Card("strike", Side.Player, 1, 5));

            var events = new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            Assert.AreEqual(Outcome.Win, ((TurnEnded)events[^1]).Outcome);
        }

        [Test]
        public void Reports_lose_when_player_dead()
        {
            var state = new CombatState();
            state.AddSoloPlayer(3);
            state.Enemies.Add(new Enemy("goblin", 12));
            state.Zone.Add(Card("jab", Side.Enemy, 1, 5)); // 5 >= 3 player HP

            var events = new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            Assert.LessOrEqual(state.Party[0].Hp, 0);
            Assert.AreEqual(Outcome.Lose, ((TurnEnded)events[^1]).Outcome);
        }

        [Test]
        public void Empty_zone_still_brackets_turn()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));

            var events = new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            Assert.AreEqual(2, events.Count); // only TurnStarted + TurnEnded
            Assert.IsInstanceOf<TurnStarted>(events[0]);
            Assert.IsInstanceOf<TurnEnded>(events[1]);
            Assert.AreEqual(Outcome.Ongoing, ((TurnEnded)events[1]).Outcome);
        }

        [Test]
        public void Resolution_is_deterministic()
        {
            CombatState Build()
            {
                var s = new CombatState();
                s.AddSoloPlayer(30);
                s.Enemies.Add(new Enemy("goblin", 12));
                s.Zone.Add(Card("strike", Side.Player, 2, 5));
                s.Zone.Add(Card("jab", Side.Enemy, 1, 3));
                return s;
            }

            var a = new TurnResolver(Registry()).Resolve(Build(), 0);
            var b = new TurnResolver(Registry()).Resolve(Build(), 0);

            CollectionAssert.AreEqual(
                a.Select(e => e.ToString()).ToArray(),
                b.Select(e => e.ToString()).ToArray());
        }

        [Test]
        public void Multi_target_effect_clears_a_previous_single_target_from_card_resolved()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("front", 12));
            state.Enemies.Add(new Enemy("back", 12));
            var self = EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusLifetime.Permanent,
                StatusApplyTarget.Self,
                magnitude: 1);
            var all = new EffectData(EffectKeys.Damage, 2)
            {
                TargetSelector = TargetSelector.All
            };
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition(
                "self_then_all", "Self Then All", Side.Player, 1, new[] { self, all }))
            {
                OwnerId = CombatState.SoloPlayerId
            });

            var resolved = new TurnResolver(Registry()).Resolve(state, 0)
                .OfType<CardResolved>()
                .Single();

            Assert.IsNull(resolved.TargetId);
            Assert.AreEqual(10, state.Enemies[0].Hp);
            Assert.AreEqual(10, state.Enemies[1].Hp);
        }
    }
}
