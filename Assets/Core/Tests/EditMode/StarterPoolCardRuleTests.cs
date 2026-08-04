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
    public class StarterPoolCardRuleTests
    {
        private static readonly CardContentCatalog Pool = TestContent.Cards();

        [Test]
        public void Riposte_after_enemy_damage_card_deals_boosted_damage()
        {
            var state = NewState();
            var enemyJab = CardFixtures.EnemyAttack("goblin_jab", 4, 1);
            state.Zone.Add(new ExecutionCardInstance(enemyJab) { OwnerId = "goblin" });
            state.Zone.Add(new ExecutionCardInstance(Pool.Get("riposte"))
                { OwnerId = CombatState.SoloPlayerId });

            var events = Resolve(state);

            Assert.AreEqual(7, events.OfType<CardResolved>()
                .Single(e => e.CardId == "riposte").DamageDealt);
        }

        [Test]
        public void Quick_cover_blocks_the_front_ally_not_the_owner()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Add(new PartyMember("front", "F", 20));
            state.Party.Add(new PartyMember("back", "B", 20));
            state.Enemies.Add(new Enemy("goblin", 30));
            var enemyJab = CardFixtures.EnemyAttack("goblin_jab", 9, 4);
            state.Zone.Add(new ExecutionCardInstance(enemyJab) { OwnerId = "goblin" });
            state.Zone.Add(new ExecutionCardInstance(Pool.Get("quick_cover"))
                { OwnerId = "back" });

            Resolve(state);

            Assert.AreEqual(20, state.Party[0].Hp);   // 전열이 방어 4로 흡수
        }

        [Test]
        public void Crossover_swaps_only_adjacent_unlocked_cards()
        {
            var def = Pool.Get("crossover");
            Assert.AreEqual(CardCategory.Intervention, def.Category);
            Assert.IsTrue(def.InterventionAction.RequireAdjacentTargets);
        }

        [Test]
        public void Hasten_targets_player_cards_and_delay_targets_enemy_cards()
        {
            Assert.AreEqual(Side.Player,
                Pool.Get("hasten").InterventionAction.TargetSide);
            Assert.AreEqual(-1,
                Pool.Get("hasten").InterventionAction.EffectValue);
            Assert.AreEqual(Side.Enemy,
                Pool.Get("delay").InterventionAction.TargetSide);
            Assert.AreEqual(Side.Player,
                Pool.Get("breather").InterventionAction.TargetSide);
            Assert.AreEqual(1,
                Pool.Get("breather").InterventionAction.EffectValue);
        }

        private static CombatState NewState()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 30));
            return state;
        }

        private static System.Collections.Generic.List<ResolutionEvent> Resolve(CombatState state)
            => new TurnResolver(CombatRegistriesAccessor.Effects(), CombatRegistriesAccessor.Statuses())
                .Resolve(state, 0);
    }

    /// <summary>CombatRegistries는 internal — 테스트가 쓰는 기본 레지스트리 접근자.
    /// (AuthoringContext.Default()와 같은 구성을 노출하는 헬퍼가 이미 있으면 그걸 사용한다.)</summary>
    internal static class CombatRegistriesAccessor
    {
        public static FateWeaver.Core.Effects.EffectRegistry Effects()
        {
            var effects = new FateWeaver.Core.Effects.EffectRegistry();
            effects.Register(new FateWeaver.Core.Effects.DamageHandler());
            effects.Register(new FateWeaver.Core.Effects.ApplyStatusHandler());
            effects.Register(new FateWeaver.Core.Effects.ConsumeStatusHandler());
            effects.Register(new FateWeaver.Core.Effects.TriggerStatusHandler());
            effects.Register(new FateWeaver.Core.Effects.GrantNextTurnFateHandler());
            effects.Register(new FateWeaver.Core.Effects.MoveFormationHandler());
            effects.Register(new FateWeaver.Core.Effects.NullifyNextPlayerConditionRewardHandler());
            effects.Register(new FateWeaver.Core.Effects.GrantNextPlayerDamageCardBonusHandler());
            return effects;
        }

        public static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new VulnerableBehavior());
            statuses.Register(new RewardSuppressionBehavior());
            statuses.Register(new BlockBehavior());
            statuses.Register(new SlowBehavior());
            statuses.Register(new HasteBehavior());
            statuses.Register(new PoisonBehavior());
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new ContagionBehavior());
            return statuses;
        }
    }
}
