using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    public class StarterPoolPoisonTests
    {
        private static readonly CardContentCatalog Pool = TestContent.Cards();

        private static CombatState NewState(params Enemy[] enemies)
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            foreach (var enemy in enemies) state.Enemies.Add(enemy);
            return state;
        }

        private static ExecutionCardInstance Place(CombatState state, CardDefinition definition)
        {
            var card = new ExecutionCardInstance(definition)
                { OwnerId = CombatState.SoloPlayerId };
            state.Zone.Add(card);
            return card;
        }

        private static System.Collections.Generic.List<ResolutionEvent> Resolve(CombatState state)
            => new TurnResolver(CombatRegistriesAccessor.Effects(), CombatRegistriesAccessor.Statuses())
                .Resolve(state, 0);

        [Test]
        public void Venom_thrust_deals_2_applies_poison_1_which_ticks_and_grows()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, Pool.Get("venom_thrust"));

            var events = Resolve(state);

            // 피해 2 + 턴 종료 독 1 = 17. 독은 2로 성장.
            Assert.AreEqual(17, state.Enemies[0].Hp);
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
            Assert.AreEqual(1, events.OfType<StatusTicked>().Single().Damage);
        }

        [Test]
        public void Last_drop_applies_2_when_no_player_card_follows()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, Pool.Get("vanguard_slash")); // 순서 3, 먼저
            Place(state, Pool.Get("last_drop"));      // 순서 7, 마지막 → 독 2

            Resolve(state);

            // 독 2 부여 → 턴 종료 2 피해 + 성장 → 독 3. HP: 20 - 5(선봉) - 2 = 13.
            Assert.AreEqual(13, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Spread_culture_hits_and_poisons_every_living_enemy()
        {
            var state = NewState(new Enemy("front", 20), new Enemy("back", 20));
            Place(state, Pool.Get("spread_culture"));

            Resolve(state);

            // 각각 피해 2 + 독 1 틱 = 17, 독 2로 성장.
            foreach (var enemy in state.Enemies)
            {
                Assert.AreEqual(17, enemy.Hp);
                Assert.AreEqual(2, enemy.Statuses.Get(StatusKeys.Poison).Magnitude);
            }
        }

        [Test]
        public void Condensed_burst_consumes_up_to_3_for_scaled_damage_then_reapplies_1()
        {
            var state = NewState(new Enemy("goblin", 30));
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 4);
            Place(state, Pool.Get("condensed_burst"));

            var events = Resolve(state);

            // 소비 3 → 피해 2+6=8. 남은 독 1 + 재부여 1 = 2 → 틱 2 → 독 3.
            Assert.AreEqual(8, events.OfType<CardResolved>().Single().DamageDealt);
            Assert.AreEqual(30 - 8 - 2, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Toxic_reclaim_blocks_only_after_a_real_consume()
        {
            // Block is ThisTurn — it expires in EndOfTurnMaintenance before any post-Resolve assert
            // could observe it directly, so prove the block existed behaviorally: place an enemy
            // attack (order 7) after toxic_reclaim (order 5) and check whether it lands.

            // 독 없음: 방어 없음 → 뒤이은 공격 4가 그대로 적중.
            var without = NewState(new Enemy("goblin", 20));
            Place(without, Pool.Get("toxic_reclaim"));
            without.Zone.Add(new ExecutionCardInstance(
                CardFixtures.EnemyAttack("goblin_jab", 7, 4))
                { OwnerId = "goblin" });
            Resolve(without);
            Assert.AreEqual(2, without.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude); // 1 부여→틱 성장
            Assert.AreEqual(30 - 4, without.Party[0].Hp); // 방어 없음 → 4 그대로 적중

            // 독 있음: 1 소비 후 재부여, 자신 방어 4 → 뒤이은 공격 4를 흡수.
            var with = NewState(new Enemy("goblin", 20));
            with.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            var card = Place(with, Pool.Get("toxic_reclaim"));
            with.Zone.Add(new ExecutionCardInstance(
                CardFixtures.EnemyAttack("goblin_jab", 7, 4))
                { OwnerId = "goblin" });
            Resolve(with);
            Assert.AreEqual(1, card.ConsumedStatusAmount);
            Assert.AreEqual(30, with.Party[0].Hp); // 방어 4가 공격 4를 흡수 → 무피해
        }

        [Test]
        public void Distill_banks_fate_only_when_poison_was_consumed()
        {
            var with = NewState(new Enemy("goblin", 20));
            with.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            Place(with, Pool.Get("distill"));
            Resolve(with);
            Assert.AreEqual(1, with.PendingNextTurnFateEnergy);

            var without = NewState(new Enemy("goblin", 20));
            Place(without, Pool.Get("distill"));
            Resolve(without);
            Assert.AreEqual(0, without.PendingNextTurnFateEnergy);
        }

        [Test]
        public void Early_onset_moves_the_tick_earlier_without_adding_one()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, Pool.Get("early_onset"));

            var events = Resolve(state);

            Assert.AreEqual(19, state.Enemies[0].Hp);   // 즉시 발동 1회만
            Assert.AreEqual(1, events.OfType<StatusTicked>().Count());
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Stable_culture_poisons_the_back_enemy_without_growth_this_turn()
        {
            var state = NewState(new Enemy("front", 20), new Enemy("back", 20));
            Place(state, Pool.Get("stable_culture"));

            Resolve(state);

            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Poison));
            Assert.AreEqual(18, state.Enemies[1].Hp);   // 독 2 피해
            Assert.AreEqual(2, state.Enemies[1].Statuses.Get(StatusKeys.Poison).Magnitude); // 성장 없음
        }

        [Test]
        public void Venom_thrust_hits_the_new_living_front_after_the_original_front_dies_mid_turn()
        {
            // Position spec §3: "앞 하나" is re-evaluated against the living formation — a card that
            // kills the front enemy must not leave a later card's FrontOne selector locked onto the
            // now-dead corpse (the legacy ByIdOrFront fallback would return raw Enemies[0] regardless
            // of HP).
            var state = NewState(new Enemy("front", 3), new Enemy("back", 20));
            Place(state, Pool.Get("vanguard_slash")); // 순서 3, 피해 5 → front(3) 처치
            Place(state, Pool.Get("venom_thrust"));   // 순서 4, 새 전열(back)을 타격해야 함

            var events = Resolve(state);

            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "front"));
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Poison)); // 시체(front)는 그대로
            // back: 피해 2 + 턴 종료 독 1 = 17, 독은 2로 성장 (단일 적 시나리오와 동일 수치).
            Assert.AreEqual(17, state.Enemies[1].Hp);
            Assert.AreEqual(2, state.Enemies[1].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Poisoned_enemy_that_dies_to_its_own_tick_transfers_contagion_without_double_ticking_recipient()
        {
            // Distinguishes the tick-death path (EndOfTurnMaintenance) from the mid-turn card-kill path
            // already covered by ContagionStatusTests: here RunTurnEndTicks ticks every living enemy
            // BEFORE the post-tick death sweep dispatches OnHolderDied/StatusTransferred, so the
            // recipient's newly-received poison must NOT tick again in the same EndOfTurnMaintenance.
            var state = NewState(new Enemy("victim", 1), new Enemy("next", 20));
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            state.Enemies[0].Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));

            var events = Resolve(state);

            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "victim"));
            var transfer = events.OfType<StatusTransferred>().Single();
            Assert.AreEqual("victim", transfer.FromHolderId);
            Assert.AreEqual("next", transfer.ToHolderId);
            // victim의 독은 사망을 부른 그 틱에서 이미 1 → 2로 성장한 뒤 전이된다 (성장은 사망 여부와
            // 무관하게 OnTurnEnd 안에서 무조건 일어남 — PoisonBehavior 기존 동작).
            Assert.AreEqual(2, transfer.Magnitude);
            Assert.AreEqual(1, events.OfType<StatusTicked>().Count()); // victim만 틱, next는 이번 턴 틱 없음
            Assert.AreEqual(20, state.Enemies[1].Hp); // next는 이번 턴 독 피해를 받지 않음
            Assert.AreEqual(2, state.Enemies[1].Statuses.Get(StatusKeys.Poison).Magnitude); // 이전만, 이번 턴 추가 성장 없음
        }

        [Test]
        public void Posthumous_spread_marks_the_target_for_on_death_transfer()
        {
            var state = NewState(new Enemy("victim", 2), new Enemy("next", 20));
            Place(state, Pool.Get("posthumous_spread"));  // 순서 4, 먼저 → 피해 1 + 독 1 + 전염
            Place(state, Pool.Get("delayed_strike"));     // 순서 5, 나중 → 피해 5 → 처치

            var events = Resolve(state);

            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "victim"));
            var transfer = events.OfType<StatusTransferred>().Single();
            Assert.AreEqual("next", transfer.ToHolderId);
            Assert.AreEqual(1, transfer.Magnitude);
            // 이전받은 독 1이 턴 종료 발동 → next는 19, 독 2.
            Assert.AreEqual(19, state.Enemies[1].Hp);
        }
    }
}
