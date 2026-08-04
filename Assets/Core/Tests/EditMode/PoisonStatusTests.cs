using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class PoisonStatusTests
    {
        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior());
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            return statuses;
        }

        private static CombatState OneEnemy(int hp = 20)
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", hp));
            return state;
        }

        [Test]
        public void Poison_ticks_at_turn_end_dealing_magnitude_then_growing_by_one()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);

            var events = new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(17, state.Enemies[0].Hp);   // 피해 3
            Assert.AreEqual(4, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude); // 그 후 +1
            var tick = events.OfType<StatusTicked>().Single();
            Assert.AreEqual(3, tick.Damage);
            Assert.AreEqual(4, tick.Magnitude);
        }

        [Test]
        public void Dormant_marker_skips_this_turns_tick_entirely()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            state.Enemies[0].Statuses.Add(StatusKeys.PoisonDormant, StatusLifetime.ThisTurn);

            var events = new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
            // 마커는 이번 턴로 소멸 — 다음 턴에는 정상 발동한다.
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.PoisonDormant));
        }

        [Test]
        public void Stasis_marker_deals_damage_but_suppresses_growth()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 2);
            state.Enemies[0].Statuses.Add(StatusKeys.PoisonStasis, StatusLifetime.ThisTurn);

            new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(18, state.Enemies[0].Hp);
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Zero_magnitude_poison_does_not_tick()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 0);

            var events = new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
        }

        [Test]
        public void Default_registries_resolve_poison_and_markers()
        {
            var context = FateWeaver.Core.Authoring.AuthoringContext.Default();
            Assert.IsTrue(context.HasStatus(StatusKeys.Poison));
            Assert.IsTrue(context.HasStatus(StatusKeys.PoisonDormant));
            Assert.IsTrue(context.HasStatus(StatusKeys.PoisonStasis));
        }
    }
}
