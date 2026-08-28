using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>타임라인이 전투 상호작용을 빠짐없이 담는지. 규칙 11에 따라 로그의 원천은
    /// 타임라인 하나뿐이다.</summary>
    public class CombatLogTests
    {
        private static EffectRegistry Effects()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            r.Register(new ApplyStatusHandler());
            return r;
        }

        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new VulnerableBehavior());
            r.Register(new BlockBehavior());
            r.Register(new WeakBehavior());
            r.Register(new DamagedBehavior());
            return r;
        }

        [Test]
        public void Damage_steps_record_weak_then_vulnerable_then_block()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), 5);
            state.Enemies.Add(enemy);

            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 10) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var resolved = events.OfType<CardResolved>().Single();

            // 10 -> 약화 7 -> 취약 10 -> 방어 5 흡수 -> 5
            Assert.AreEqual(
                new[] { "weak", "vulnerable", "block" },
                resolved.DamageSteps.Select(s => s.StatusId).ToArray());
            Assert.AreEqual((10, 7), (resolved.DamageSteps[0].Before, resolved.DamageSteps[0].After));
            Assert.AreEqual((7, 10), (resolved.DamageSteps[1].Before, resolved.DamageSteps[1].After));
            Assert.AreEqual((10, 5), (resolved.DamageSteps[2].Before, resolved.DamageSteps[2].After));
            Assert.AreEqual(5, resolved.DamageDealt);
        }

        [Test]
        public void Damage_steps_name_the_holder_of_each_status()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);

            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 10) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var steps = events.OfType<CardResolved>().Single().DamageSteps;

            Assert.AreEqual(CombatState.SoloPlayerId, steps[0].HolderId); // 약화는 공격자에게
            Assert.AreEqual("goblin", steps[1].HolderId);                 // 취약은 대상에게
        }

        [Test]
        public void A_card_with_no_status_involved_records_no_steps()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 30));
            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 4) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<CardResolved>().Single().DamageSteps);
        }
    }
}
