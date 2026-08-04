using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class FormationTargetingIntegrationTests
    {
        private const int MemberHp = 10;
        private const int AttackDamage = 3;
        private const int BeyondFormation = 99;

        private static ExecutionCardInstance Card(
            string id,
            string name,
            Side side,
            int executionOrder,
            EffectData effect,
            string ownerId)
            => new ExecutionCardInstance(new CardDefinition(
                id,
                name,
                side,
                executionOrder,
                new[] { effect }))
            {
                OwnerId = ownerId
            };

        private static void ApplyMove(CombatState state, Side side, string ownerId, int distance)
        {
            var effect = new EffectData(EffectKeys.MoveFormation, distance);
            var card = Card(
                "validation_move",
                "[검증] 대형 이동",
                side,
                executionOrder: 1,
                effect,
                ownerId);
            var context = new EffectContext
            {
                Card = card,
                State = state,
                Effect = effect,
                EffectValue = distance
            };

            new MoveFormationHandler().Apply(context);
        }

        [Test]
        public void Later_effect_does_not_promote_a_new_enemy_after_captured_front_two_die()
        {
            var state = new CombatState();
            state.AddSoloPlayer(MemberHp);
            state.Enemies.Add(new Enemy("a", 2));
            state.Enemies.Add(new Enemy("b", 2));
            state.Enemies.Add(new Enemy("c", 2));
            var damage = new EffectData(EffectKeys.Damage, 2)
            {
                TargetSelector = TargetSelector.FrontTwo
            };
            var poison = EffectData.ApplyStatus(
                StatusKeys.Poison,
                StatusApplyTarget.TargetEnemy,
                count: 1) with
            {
                TargetSelector = TargetSelector.FrontTwo
            };
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition(
                "snapshot_kill", "Snapshot Kill", Side.Player, 1, new[] { damage, poison }))
            {
                OwnerId = CombatState.SoloPlayerId
            });
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new ApplyStatusHandler());

            new TurnResolver(effects).Resolve(state, 0);

            Assert.AreEqual(2, state.Enemies[2].Hp);
            Assert.IsFalse(state.Enemies[2].Statuses.Has(StatusKeys.Poison));
        }

        [Test]
        public void Player_move_changes_only_party_order()
        {
            var state = new CombatState();
            state.Party.Clear();
            state.Party.Add(new PartyMember("validation_party_a", "[검증] A", MemberHp));
            state.Party.Add(new PartyMember("validation_party_b", "[검증] B", MemberHp));
            state.Party.Add(new PartyMember("validation_party_c", "[검증] C", MemberHp));
            state.Enemies.Add(new Enemy("validation_enemy_a", MemberHp));
            state.Enemies.Add(new Enemy("validation_enemy_b", MemberHp));

            ApplyMove(state, Side.Player, "validation_party_b", distance: -1);

            CollectionAssert.AreEqual(
                new[] { "validation_party_b", "validation_party_a", "validation_party_c" },
                state.Party.Select(member => member.Id).ToArray());
            CollectionAssert.AreEqual(
                new[] { "validation_enemy_a", "validation_enemy_b" },
                state.Enemies.Select(enemy => enemy.Id).ToArray());
        }

        [Test]
        public void Enemy_move_changes_only_enemy_order()
        {
            var state = new CombatState();
            state.Party.Clear();
            state.Party.Add(new PartyMember("validation_party_a", "[검증] A", MemberHp));
            state.Party.Add(new PartyMember("validation_party_b", "[검증] B", MemberHp));
            state.Enemies.Add(new Enemy("validation_enemy_a", MemberHp));
            state.Enemies.Add(new Enemy("validation_enemy_b", MemberHp));
            state.Enemies.Add(new Enemy("validation_enemy_c", MemberHp));

            ApplyMove(state, Side.Enemy, "validation_enemy_a", distance: 2);

            CollectionAssert.AreEqual(
                new[] { "validation_enemy_b", "validation_enemy_c", "validation_enemy_a" },
                state.Enemies.Select(enemy => enemy.Id).ToArray());
            CollectionAssert.AreEqual(
                new[] { "validation_party_a", "validation_party_b" },
                state.Party.Select(member => member.Id).ToArray());
        }

        [Test]
        public void Movement_clamps_to_own_formation_bounds()
        {
            var state = new CombatState();
            state.Party.Clear();
            state.Party.Add(new PartyMember("validation_party_a", "[검증] A", MemberHp));
            state.Party.Add(new PartyMember("validation_party_b", "[검증] B", MemberHp));
            state.Party.Add(new PartyMember("validation_party_c", "[검증] C", MemberHp));

            ApplyMove(state, Side.Player, "validation_party_b", distance: -BeyondFormation);
            Assert.AreEqual("validation_party_b", state.Party[0].Id);

            ApplyMove(state, Side.Player, "validation_party_b", distance: BeyondFormation);
            Assert.AreEqual("validation_party_b", state.Party[^1].Id);
        }

        [Test]
        public void Dead_or_missing_owner_cancels_instead_of_moving_front_member()
        {
            AssertInvalidPlayerOwnerDoesNotMove("validation_party_dead", deadOwner: true);
            AssertInvalidPlayerOwnerDoesNotMove("validation_party_missing", deadOwner: false);
            AssertInvalidEnemyOwnerDoesNotMove("validation_enemy_dead", deadOwner: true);
            AssertInvalidEnemyOwnerDoesNotMove(ownerId: null, deadOwner: false);
        }

        [Test]
        public void Later_frontmost_attack_uses_formation_after_earlier_move()
        {
            var state = new CombatState();
            state.Party.Clear();
            var memberA = new PartyMember("validation_party_a", "[검증] A", MemberHp);
            var memberB = new PartyMember("validation_party_b", "[검증] B", MemberHp);
            state.Party.Add(memberA);
            state.Party.Add(memberB);
            state.Enemies.Add(new Enemy("validation_enemy", MemberHp));

            var move = Card(
                "validation_move_formation",
                "[검증] 대형 이동",
                Side.Player,
                executionOrder: 2,
                new EffectData(EffectKeys.MoveFormation, -1),
                ownerId: memberB.Id);
            var attack = Card(
                "validation_frontmost_attack",
                "[검증] 전열 공격",
                Side.Enemy,
                executionOrder: 5,
                new EffectData(EffectKeys.Damage, AttackDamage)
                {
                    TargetSelector = TargetSelector.FrontOne
                },
                ownerId: "validation_enemy");
            state.Zone.Add(move);
            state.Zone.Add(attack);

            var registry = new EffectRegistry();
            registry.Register(new MoveFormationHandler());
            registry.Register(new DamageHandler());
            var events = new TurnResolver(registry).Resolve(state, turnIndex: 0);

            Assert.AreEqual(MemberHp, memberA.Hp);
            Assert.AreEqual(MemberHp - AttackDamage, memberB.Hp);
            var resolved = events.OfType<CardResolved>().ToArray();
            CollectionAssert.AreEqual(
                new[] { "validation_move_formation", "validation_frontmost_attack" },
                resolved.Select(card => card.CardId).ToArray());
            Assert.AreEqual(memberB.Id, resolved[1].TargetId);
        }

        private static void AssertInvalidPlayerOwnerDoesNotMove(string ownerId, bool deadOwner)
        {
            var state = new CombatState();
            state.Party.Clear();
            var front = new PartyMember("validation_party_front", "[검증] 전열", MemberHp);
            state.Party.Add(front);
            if (deadOwner)
            {
                var dead = new PartyMember(ownerId, "[검증] 전투 불능", MemberHp) { Hp = 0 };
                state.Party.Add(dead);
            }

            var effect = new EffectData(EffectKeys.MoveFormation, 1);
            var card = Card(
                "validation_invalid_player_move",
                "[검증] 무효 플레이어 이동",
                Side.Player,
                executionOrder: 1,
                effect,
                ownerId);
            var context = new EffectContext
            {
                Card = card,
                State = state,
                Effect = effect,
                EffectValue = effect.EffectValue
            };

            new MoveFormationHandler().Apply(context);

            Assert.AreEqual(CardCancellationReason.NoValidTarget, card.CancellationReason);
            Assert.AreSame(front, state.Party[0]);
        }

        private static void AssertInvalidEnemyOwnerDoesNotMove(string ownerId, bool deadOwner)
        {
            var state = new CombatState();
            var front = new Enemy("validation_enemy_front", MemberHp);
            state.Enemies.Add(front);
            if (deadOwner)
            {
                state.Enemies.Add(new Enemy(ownerId, hp: 0));
            }
            else
            {
                state.Enemies.Add(new Enemy(id: null, hp: MemberHp));
            }

            var effect = new EffectData(EffectKeys.MoveFormation, 1);
            var card = Card(
                "validation_invalid_enemy_move",
                "[검증] 무효 적 이동",
                Side.Enemy,
                executionOrder: 1,
                effect,
                ownerId);
            var context = new EffectContext
            {
                Card = card,
                State = state,
                Effect = effect,
                EffectValue = effect.EffectValue
            };

            new MoveFormationHandler().Apply(context);

            Assert.AreEqual(CardCancellationReason.NoValidTarget, card.CancellationReason);
            Assert.AreSame(front, state.Enemies[0]);
        }
    }
}
