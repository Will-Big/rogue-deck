using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Status;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class StarterDeckSpecEquivalenceTests
    {
        private static readonly string[] SelectedIds =
        {
            "probing_strike", "delayed_strike", "quick_cover", "early_guard",
            "breather", "hasten", "toxic_reclaim", "early_onset", "spore_veil",
            "last_drop"
        };

        private static CardDefinition Def(CardSpec spec) =>
            CardSpecMapper.ToDefinition(spec);

        private static EnemyIntent Goblin(int executionOrder, int damage) => new EnemyIntent(
            new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", executionOrder, damage) }
            });

        private static int HandIndex(DeckCombatSession s, string id)
        {
            for (int i = 0; i < s.Hand.Count; i++) if (s.Hand[i].Def.Id == id) return i;
            return -1;
        }

        private static int ZoneIndex(DeckCombatSession s, string id)
        {
            var order = s.CurrentOrder;
            for (int i = 0; i < order.Count; i++) if (order[i].Def.Id == id) return i;
            return -1;
        }

        private static int DamageOf(IReadOnlyList<ResolutionEvent> t, string id)
            => t.OfType<CardResolved>().First(e => e.CardId == id).DamageDealt;

        // 아래 픽스처는 폐기된 StarterDeckSpecs 팩터리(QuickCut/Counter/Cover/PullForward)의
        // 값을 그대로 옮긴 것이다. 이 세 테스트는 출시 카드가 아니라 규칙(개입 재정렬,
        // PrevExecutedIsEnemyDamageCard 조건, 블록 흡수)을 검사하므로 어떤 카드가
        // 실제로 출시되는지에 의존하지 않도록 인라인 픽스처를 쓴다.
        private static CardSpec Fixture(string id, int order, params EffectSpec[] effects)
            => new CardSpec
            {
                Id = id,
                Name = id,
                Side = Side.Player,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = order,
                Effects = effects
            };

        private static CardSpec QuickCutFixture() => Fixture("quick_cut", 5,
            new DamageSpec
            {
                Value = 2,
                Condition = new ConditionSpec { Kind = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 }
            });

        private static CardSpec CounterFixture() => Fixture("counter_stance", 7,
            new DamageSpec
            {
                Value = 4,
                Condition = new ConditionSpec { Kind = ConditionKind.PrevExecutedIsEnemyDamageCard, SuccessEffectValue = 9 }
            });

        private static CardSpec CoverFixture() => Fixture("cover", 5,
            new ApplyStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Block),
                Value = 2,
                Lifetime = StatusLifetimeKind.ThisTurn,
                Target = StatusApplyTarget.Self,
                Condition = new ConditionSpec { Kind = ConditionKind.NextIsEnemyDamageCard, SuccessEffectValue = 7 }
            });

        private static CardSpec PullForwardFixture() => new CardSpec
        {
            Id = "pull_forward",
            Name = "pull_forward",
            Side = Side.Player,
            Category = CardCategory.Intervention,
            EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = -1
        };

        [Test]
        public void Spec_deck_has_same_composition()
        {
            var specs = StarterDeckSpecs.Build();
            Assert.AreEqual(10, specs.Count);
            CollectionAssert.AreEqual(SelectedIds, specs.Select(spec => spec.Id).ToArray());
            Assert.AreEqual(8, specs.Count(s => s.Category == CardCategory.Execution));
            Assert.AreEqual(2, specs.Count(s => s.Category == CardCategory.Intervention));
        }

        [Test]
        public void Spec_quick_cut_pulled_first_deals_eight()
        {
            var session = new DeckCombatSession(
                new[] { Def(QuickCutFixture()), Def(PullForwardFixture()) }, 30,
                new[] { new Enemy("goblin", 100) }, Goblin(5, 3), 3, 5, 1);
            session.PlayExecutionCard(HandIndex(session, "quick_cut"));
            session.PlayInterventionCard(HandIndex(session, "pull_forward"), ZoneIndex(session, "quick_cut"));
            Assert.AreEqual(8, DamageOf(session.ResolveTurn(), "quick_cut"));
        }

        [Test]
        public void Spec_counter_immediately_after_enemy_attack_deals_nine()
        {
            var session = new DeckCombatSession(
                new[] { Def(CounterFixture()) }, 30,
                new[] { new Enemy("goblin", 100) },
                Goblin(6, 4), 3, 5, 1);
            session.PlayExecutionCard(HandIndex(session, "counter_stance"));
            Assert.AreEqual(9, DamageOf(session.ResolveTurn(), "counter_stance"));
        }

        [Test]
        public void Spec_cover_before_enemy_attack_absorbs()
        {
            var session = new DeckCombatSession(
                new[] { Def(CoverFixture()) }, 30,
                new[] { new Enemy("goblin", 100) }, Goblin(6, 3), 3, 5, 1);
            session.PlayExecutionCard(HandIndex(session, "cover"));
            int hp = session.State.Party[0].Hp;
            session.ResolveTurn();
            Assert.AreEqual(hp, session.State.Party[0].Hp);
        }
    }
}
