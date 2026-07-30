using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>약화·취약·손상. count는 남은 턴이고 배율은 StatusRules에서 온다. 배율은 단계마다
    /// 정수로 버리며 피해 최소 1을 보장하지 않는다.</summary>
    public class DebuffStatusTests
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

        // 방어를 Turns(2)로 거는 것은 저작 관례가 아니라 테스트 편의다. ThisTurn으로 걸면
        // 턴 종료 정리가 인스턴스를 지워 Magnitude를 조회할 수 없다.
        private static ExecutionCardInstance PlayerGuard(string id, int block)
        {
            var def = new CardDefinition(id, id, Side.Player, 1,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Turns(2), StatusApplyTarget.Self, block)
                });
            return new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId };
        }

        private static ExecutionCardInstance PlayerStrike(string id, int damage)
        {
            var def = new CardDefinition(id, id, Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId };
        }

        [Test]
        public void Weak_reduces_outgoing_damage_by_the_rule_multiplier()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 8));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt); // floor(8 x 0.75) = 6
            Assert.AreEqual(24, enemy.Hp);
        }

        [Test]
        public void Weak_floors_and_does_not_guarantee_minimum_damage()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 1));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(0, ((CardResolved)events[1]).DamageDealt); // floor(1 x 0.75) = 0
            Assert.AreEqual(30, enemy.Hp);
        }

        [Test]
        public void Weak_stacking_extends_duration_not_intensity()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(4)); // 재부여 = 수명 갱신
            var enemy = new Enemy("goblin", 30);
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 8));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt); // 여전히 x0.75
            Assert.AreEqual(3, player.Statuses.Get(StatusKeys.Weak).Count); // 4 -> 턴 끝에 3
        }

        [Test]
        public void Weak_then_vulnerable_floors_at_each_stage()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 10));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // floor(10 x 0.75) = 7, floor(7 x 1.5) = 10
            Assert.AreEqual(10, ((CardResolved)events[1]).DamageDealt);
        }

        [Test]
        public void Weak_on_the_target_does_not_reduce_damage_it_receives()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2)); // 대상 쪽 약화는 무관
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 8));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(8, ((CardResolved)events[1]).DamageDealt);
        }

        [Test]
        public void Damaged_reduces_block_gained_by_the_rule_multiplier()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(new Enemy("goblin", 30));
            state.Zone.Add(PlayerGuard("guard", 5));

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // floor(5 x 0.75) = 3
            Assert.AreEqual(3, player.Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Damaged_does_not_reduce_other_gained_statuses()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(new Enemy("goblin", 30));
            var def = new CardDefinition("hex", "hex", Side.Player, 1,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Vulnerable, StatusLifetime.Turns(4), StatusApplyTarget.Self)
                });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(3, player.Statuses.Get(StatusKeys.Vulnerable).Count); // 4 -> 턴 끝에 3
        }

        [Test]
        public void Damaged_on_someone_else_does_not_reduce_the_block_this_holder_gains()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerGuard("guard", 5));

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(5, player.Statuses.Get(StatusKeys.Block).Magnitude); // 감소 없음
        }
    }
}
