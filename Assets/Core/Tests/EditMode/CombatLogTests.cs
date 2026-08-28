using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests
{
    /// <summary>타임라인이 전투 상호작용을 빠짐없이 담는지. 규칙 11에 따라 로그의 원천은
    /// 타임라인 하나뿐이다.</summary>
    public class CombatLogTests
    {
        private static readonly KoreanDescriptionCatalog Korean =
            KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses());

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

        [Test]
        public void Applying_a_status_emits_status_applied_with_the_folded_magnitude()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(new Enemy("goblin", 30));
            var def = new CardDefinition("guard", "guard", Side.Player, 1,
                new[]
                {
                    EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 5)
                });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var applied = events.OfType<StatusApplied>().Single(e => e.StatusId == "block");

            Assert.AreEqual(CombatState.SoloPlayerId, applied.HolderId);
            Assert.AreEqual(3, applied.Magnitude); // 손상으로 floor(5 x 0.75)
        }

        [Test]
        public void A_status_that_runs_out_of_turns_emits_status_expired()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(1)); // 이번 턴 끝에 만료
            state.Enemies.Add(new Enemy("goblin", 30));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsTrue(events.OfType<StatusExpired>()
                .Any(e => e.HolderId == CombatState.SoloPlayerId && e.StatusId == "weak"));
        }

        [Test]
        public void Formatter_spells_out_each_damage_step_and_the_deaths_door_save()
        {
            var timeline = new ResolutionEvent[]
            {
                new TurnStarted(0),
                new CardResolved(1, "goblin", "jab", Side.Enemy, 4, "member_a")
                {
                    DamageSteps = new[]
                    {
                        new DamageStep("member_a", "vulnerable", 4, 6),
                        new DamageStep("member_a", "block", 6, 4)
                    }
                },
                new DeathsDoorSurvived("member_a"),
                new TurnEnded(0, Outcome.Ongoing)
            };

            var text = TimelineTextFormatter.Format(timeline, Korean);

            StringAssert.Contains("취약", text);
            StringAssert.Contains("4", text);
            StringAssert.Contains("6", text);
            StringAssert.Contains("방어", text);
            StringAssert.Contains("치명", text);   // 왜 살아남았는지가 반드시 보여야 한다
            StringAssert.Contains("member_a", text);
        }

        [Test]
        public void Formatter_handles_an_empty_timeline_without_throwing()
        {
            Assert.AreEqual(
                string.Empty,
                TimelineTextFormatter.Format(
                    System.Array.Empty<ResolutionEvent>(), Korean));
        }
    }
}
