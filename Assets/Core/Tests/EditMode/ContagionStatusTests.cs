using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class ContagionStatusTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior());
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new ContagionBehavior());
            return statuses;
        }

        [Test]
        public void Killing_a_contagious_poisoned_enemy_transfers_poison_to_front_living_enemy()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("victim", 2));
            state.Enemies.Add(new Enemy("next", 10));
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 4);
            state.Enemies[0].Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));

            var def = new CardDefinition("finisher", "마무리", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            var transfer = events.OfType<StatusTransferred>().Single();
            Assert.AreEqual("victim", transfer.FromHolderId);
            Assert.AreEqual("next", transfer.ToHolderId);
            Assert.AreEqual(4, transfer.Magnitude);
            // 이전받은 독은 이번 턴 종료에 발동한다 (§3.2: 행동 중 부여된 독도 발동).
            Assert.AreEqual(4, events.OfType<StatusTicked>().Single(t => t.HolderId == "next").Damage);
        }

        [Test]
        public void Contagion_without_poison_does_nothing()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("victim", 2));
            state.Enemies.Add(new Enemy("next", 10));
            state.Enemies[0].Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));

            var def = new CardDefinition("finisher", "마무리", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<StatusTransferred>().ToList());
            Assert.IsFalse(state.Enemies[1].Statuses.Has(StatusKeys.Poison));
        }

        [Test]
        public void No_living_recipient_means_no_transfer()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("victim", 2));   // 유일한 적
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 4);
            state.Enemies[0].Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));

            var def = new CardDefinition("finisher", "마무리", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<StatusTransferred>().ToList());
        }
    }
}
