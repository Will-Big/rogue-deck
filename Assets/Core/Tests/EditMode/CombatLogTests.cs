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
            r.Register(new PoisonBehavior());
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
        public void A_card_cancelled_mid_effects_keeps_its_dealt_damage_on_the_event()
        {
            // 효과 1(피해 5)이 마지막 적을 죽이고, 효과 2(피해)가 대상을 못 찾아 카드가 취소된다.
            // 취소는 이미 준 피해를 되돌리지 않으므로 로그에서도 사라지면 안 된다.
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 3));
            var def = new CardDefinition("double_strike", "double_strike", Side.Player, 1,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 5),
                    new EffectData(EffectKeys.Damage, 5)
                });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var cancelled = events.OfType<CardCancelled>().Single();

            Assert.IsEmpty(events.OfType<CardResolved>());
            Assert.AreEqual(5, cancelled.DamageDealt);
            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "goblin"));
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
        public void Damage_emits_hp_changed_per_target_with_before_and_after()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin_a", 10));
            state.Enemies.Add(new Enemy("goblin_b", 10));
            var def = new CardDefinition("sweep", "sweep", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 4) { TargetSelector = TargetSelector.All } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var changes = events.OfType<HpChanged>().ToArray();

            Assert.AreEqual(2, changes.Length);
            Assert.AreEqual(("goblin_a", 10, 6, HpChangeSource.CardDamage, "sweep"),
                (changes[0].HolderId, changes[0].Before, changes[0].After, changes[0].Source, changes[0].SourceId));
            Assert.AreEqual(("goblin_b", 10, 6), (changes[1].HolderId, changes[1].Before, changes[1].After));
        }

        [Test]
        public void Deaths_door_hp_change_shows_the_clamp_to_one()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(30);
            player.Hp = 4;
            player.SurviveCharges = 1;
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("jab", "jab", Side.Enemy, 1,
                new[] { new EffectData(EffectKeys.Damage, 6) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = "goblin" });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var change = events.OfType<HpChanged>().Single();

            Assert.AreEqual((CombatState.SoloPlayerId, 4, 1), (change.HolderId, change.Before, change.After));
            Assert.IsTrue(events.OfType<DeathsDoorSurvived>().Any());
        }

        [Test]
        public void Turn_end_poison_tick_emits_hp_changed_with_status_source()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 10);
            enemy.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            state.Enemies.Add(enemy);

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var change = events.OfType<HpChanged>().Single();

            Assert.AreEqual(("goblin", 10, 7, HpChangeSource.StatusTick, "poison"),
                (change.HolderId, change.Before, change.After, change.Source, change.SourceId));
        }

        [Test]
        public void A_fully_blocked_hit_leaves_no_hp_changed()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 10);
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), 10);
            state.Enemies.Add(enemy);
            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 4) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<HpChanged>());
        }

        [Test]
        public void Formatter_handles_an_empty_timeline_without_throwing()
        {
            Assert.AreEqual(
                string.Empty,
                TimelineTextFormatter.Format(
                    System.Array.Empty<ResolutionEvent>(), Korean));
        }

        [Test]
        public void Granting_next_turn_fate_emits_fate_energy_gained()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("distill", "증류", Side.Player, 5,
                new[] { new EffectData(EffectKeys.GrantNextTurnFate, 1) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new GrantNextTurnFateHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);
            var gained = events.OfType<FateEnergyGained>().Single();

            Assert.AreEqual(("distill", 1), (gained.SourceCardId, gained.Amount));
        }

        [Test]
        public void Consuming_a_status_emits_status_consumed_with_the_amount()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            var enemy = new Enemy("goblin", 20);
            enemy.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 2);
            state.Enemies.Add(enemy);
            var def = new CardDefinition("drain", "흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 0) }
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new ConsumeStatusHandler());
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior());

            var events = new TurnResolver(effects, statuses).Resolve(state, 0);
            var consumed = events.OfType<StatusConsumed>().Single();

            Assert.AreEqual(("goblin", "poison", 2), (consumed.HolderId, consumed.StatusId, consumed.Amount));
        }

        [Test]
        public void Zero_fate_gain_emits_no_state_change_event()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("empty_distill", "빈 증류", Side.Player, 5,
                new[] { new EffectData(EffectKeys.GrantNextTurnFate, 0) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new GrantNextTurnFateHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);

            Assert.AreEqual(0, state.PendingNextTurnFateEnergy);
            Assert.IsEmpty(events.OfType<FateEnergyGained>());
        }

        [Test]
        public void Consuming_a_missing_status_emits_no_state_change_event()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("empty_drain", "빈 흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 0) }
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new ConsumeStatusHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<StatusConsumed>());
        }
    }
}
