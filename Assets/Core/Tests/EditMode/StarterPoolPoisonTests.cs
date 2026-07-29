using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class StarterPoolPoisonTests
    {
        private static CombatState NewState(params Enemy[] enemies)
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            foreach (var enemy in enemies) state.Enemies.Add(enemy);
            return state;
        }

        private static ExecutionCardInstance Place(CombatState state, CardSpec spec)
        {
            var card = new ExecutionCardInstance(CardSpecMapper.ToDefinition(spec))
                { OwnerId = CombatState.SoloPlayerId };
            state.Zone.Add(card);
            return card;
        }

        private static System.Collections.Generic.List<ResolutionEvent> Resolve(CombatState state)
            => new TurnResolver(CombatRegistriesAccessor.Effects(), CombatRegistriesAccessor.Statuses())
                .Resolve(state, 0);

        [Test]
        public void Build_contains_all_22_cards_and_validates()
        {
            Assert.AreEqual(22, StarterPoolSpecs.Build().Count);
            CollectionAssert.IsEmpty(AuthoringValidator.Validate(
                StarterPoolSpecs.Build(), AuthoringContext.Default()));
        }

        [Test]
        public void Venom_thrust_deals_2_applies_poison_1_which_ticks_and_grows()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, StarterPoolSpecs.VenomThrust());

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
            Place(state, StarterPoolSpecs.VanguardSlash()); // 순서 3, 먼저
            Place(state, StarterPoolSpecs.LastDrop());      // 순서 7, 마지막 → 독 2

            Resolve(state);

            // 독 2 부여 → 턴 종료 2 피해 + 성장 → 독 3. HP: 20 - 5(선봉) - 2 = 13.
            Assert.AreEqual(13, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Spread_culture_hits_and_poisons_every_living_enemy()
        {
            var state = NewState(new Enemy("front", 20), new Enemy("back", 20));
            Place(state, StarterPoolSpecs.SpreadCulture());

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
            Place(state, StarterPoolSpecs.CondensedBurst());

            var events = Resolve(state);

            // 소비 3 → 피해 2+6=8. 남은 독 1 + 재부여 1 = 2 → 틱 2 → 독 3.
            Assert.AreEqual(8, events.OfType<CardResolved>().Single().DamageDealt);
            Assert.AreEqual(30 - 8 - 2, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Toxic_reclaim_blocks_only_after_a_real_consume()
        {
            // 독 없음: 방어 없음, 독 1만 부여됨.
            var without = NewState(new Enemy("goblin", 20));
            Place(without, StarterPoolSpecs.ToxicReclaim());
            Resolve(without);
            Assert.IsFalse(without.Party[0].Statuses.Has(StatusKeys.Block));
            Assert.AreEqual(2, without.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude); // 1 부여→틱 성장

            // 독 있음: 1 소비 후 재부여, 자신 방어 4.
            var with = NewState(new Enemy("goblin", 20));
            with.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            var card = Place(with, StarterPoolSpecs.ToxicReclaim());
            Resolve(with);
            Assert.AreEqual(1, card.ConsumedStatusAmount);
        }

        [Test]
        public void Distill_banks_fate_only_when_poison_was_consumed()
        {
            var with = NewState(new Enemy("goblin", 20));
            with.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            Place(with, StarterPoolSpecs.Distill());
            Resolve(with);
            Assert.AreEqual(1, with.PendingNextTurnFateEnergy);

            var without = NewState(new Enemy("goblin", 20));
            Place(without, StarterPoolSpecs.Distill());
            Resolve(without);
            Assert.AreEqual(0, without.PendingNextTurnFateEnergy);
        }

        [Test]
        public void Early_onset_moves_the_tick_earlier_without_adding_one()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, StarterPoolSpecs.EarlyOnset());

            var events = Resolve(state);

            Assert.AreEqual(19, state.Enemies[0].Hp);   // 즉시 발동 1회만
            Assert.AreEqual(1, events.OfType<StatusTicked>().Count());
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Stable_culture_poisons_the_back_enemy_without_growth_this_turn()
        {
            var state = NewState(new Enemy("front", 20), new Enemy("back", 20));
            Place(state, StarterPoolSpecs.StableCulture());

            Resolve(state);

            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Poison));
            Assert.AreEqual(18, state.Enemies[1].Hp);   // 독 2 피해
            Assert.AreEqual(2, state.Enemies[1].Statuses.Get(StatusKeys.Poison).Magnitude); // 성장 없음
        }

        [Test]
        public void Posthumous_spread_marks_the_target_for_on_death_transfer()
        {
            var state = NewState(new Enemy("victim", 2), new Enemy("next", 20));
            Place(state, StarterPoolSpecs.PosthumousSpread());  // 피해 1 + 독 1 + 전염
            Place(state, StarterPoolSpecs.VanguardSlash());     // 피해 5 → 처치

            var events = Resolve(state);

            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "victim"));
            var transfer = events.OfType<StatusTransferred>().Single();
            Assert.AreEqual("next", transfer.ToHolderId);
            // 이전받은 독 1이 턴 종료 발동 → next는 19, 독 2.
            Assert.AreEqual(19, state.Enemies[1].Hp);
        }
    }
}
