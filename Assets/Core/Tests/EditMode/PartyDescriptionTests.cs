using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests
{
    public class PartyDescriptionTests
    {
        private static readonly DescriptionRequestCatalog Catalog =
            DescriptionRequestCatalog.CreateDefault(TestContent.Statuses());

        private static CardDefinition Execution(params EffectData[] effects) =>
            new CardDefinition("party_test", "파티 테스트", Side.Player, 0, effects)
            {
                EnemyTarget = CardTargetRange.FrontOne,
                Category = CardCategory.Execution
            };

        private static EffectData Hit(int value)
            => new EffectData(EffectKeys.Damage, value) { TargetFaction = CardTargetFaction.Enemy };

        private static LocalizedTextRequest Body(LocalizedTextRequest sentence)
            => (LocalizedTextRequest)sentence.Arguments["body"];

        private static LocalizedTextRequest Arg(LocalizedTextRequest request, string name)
            => (LocalizedTextRequest)request.Arguments[name];

        /// <summary>피해 1(성공 시 2) 카드에 조건을 걸고, 기본 문장 뒤에 오는 조건부 문장의 조건 요청을 꺼낸다.</summary>
        private static LocalizedTextRequest ConditionOf(Condition condition)
        {
            var card = Execution(Hit(1) with { SuccessEffectValue = 2 }) with { StartCondition = condition };
            var line = DescriptionComposer.Compose(card, Catalog).Lines.Single();

            Assert.AreEqual(2, line.Sentences.Count);
            Assert.AreEqual("sentence", line.Sentences[0].Key);
            Assert.AreEqual(1, Body(line.Sentences[0]).Arguments["amount"]);
            Assert.AreEqual("sentence.conditional", line.Sentences[1].Key);
            Assert.AreEqual(2, Body(line.Sentences[1]).Arguments["amount"]);
            return Arg(line.Sentences[1], "condition");
        }

        [TestCase(CardTargetRange.FrontOne)]
        [TestCase(CardTargetRange.FrontTwo)]
        [TestCase(CardTargetRange.BackOne)]
        [TestCase(CardTargetRange.BackTwo)]
        public void Position_range_becomes_the_line_target(CardTargetRange range)
        {
            var card = Execution(Hit(4)) with { EnemyTarget = range };

            var doc = DescriptionComposer.Compose(card, Catalog);

            var expected = new CardTargetKey(CardTargetFaction.Enemy, range);
            Assert.AreEqual(expected, doc.Lines.Single().Target);
            CollectionAssert.AreEqual(new[] { expected }, doc.TargetEntries);
            Assert.AreEqual("effect.damage", Body(doc.Lines[0].Sentences.Single()).Key);
            Assert.AreEqual(4, Body(doc.Lines[0].Sentences[0]).Arguments["amount"]);
        }

        [Test]
        public void All_party_status_targets_all_allies()
        {
            var card = Execution(EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 4))
                with { AllyTarget = CardTargetRange.All };

            var doc = DescriptionComposer.Compose(card, Catalog);

            Assert.AreEqual(new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.All), doc.Lines.Single().Target);
            var body = Body(doc.Lines[0].Sentences.Single());
            Assert.AreEqual("effect.apply_status", body.Key);
            Assert.AreEqual(4, body.Arguments["amount"]);
            Assert.AreEqual("status.block.name", Arg(body, "status").Key);
        }

        [Test]
        public void First_to_trigger_condition()
            => Assert.AreEqual("condition.first", ConditionOf(new FirstToTrigger()).Key);

        [Test]
        public void Within_nth_condition_carries_n()
        {
            var condition = ConditionOf(new WithinNth(3));
            Assert.AreEqual("condition.within", condition.Key);
            Assert.AreEqual(3, condition.Arguments["n"]);
        }

        [Test]
        public void Before_next_enemy_damage_condition()
            => Assert.AreEqual("condition.before_enemy_damage", ConditionOf(new BeforeNextEnemyDamageCard()).Key);

        [Test]
        public void Previous_executed_condition_names_execution_history()
        {
            var condition = ConditionOf(new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage));

            Assert.AreEqual("condition.previous", condition.Key);
            Assert.AreEqual("side.enemy", Arg(condition, "side").Key);
            Assert.AreEqual("kind.damage_card", Arg(condition, "kind").Key);
        }

        [Test]
        public void Previous_executed_card_condition_names_a_plain_card()
        {
            var condition = ConditionOf(new PreviousExecutedCardIs(Side.Player));

            Assert.AreEqual("condition.previous", condition.Key);
            Assert.AreEqual("side.player", Arg(condition, "side").Key);
            Assert.AreEqual("kind.card", Arg(condition, "kind").Key);
        }

        [Test]
        public void Non_damage_effect_condition_names_the_effect()
        {
            var condition = ConditionOf(new PreviousExecutedCardHasEffect(Side.Player, EffectKeys.ApplyStatus));

            var kind = Arg(condition, "kind");
            Assert.AreEqual("kind.effect_card", kind.Key);
            var effect = Arg(kind, "effect");
            Assert.AreEqual(TranslationTable.Content, effect.Table);
            Assert.AreEqual("effect." + EffectKeys.ApplyStatus.Id + ".name", effect.Key);
        }

        [Test]
        public void Previous_adjacent_condition_names_frozen_placement_order()
        {
            var condition = ConditionOf(new AdjacentCardHasEffect(AdjacentDirection.Previous, Side.Player, EffectKeys.Damage));

            Assert.AreEqual("condition.adjacent.previous", condition.Key);
            Assert.AreEqual("side.player", Arg(condition, "side").Key);
            Assert.AreEqual("kind.damage_card", Arg(condition, "kind").Key);
        }

        [Test]
        public void Next_adjacent_card_condition_names_a_plain_card()
        {
            var condition = ConditionOf(new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy));

            Assert.AreEqual("condition.adjacent.next", condition.Key);
            Assert.AreEqual("side.enemy", Arg(condition, "side").Key);
            Assert.AreEqual("kind.card", Arg(condition, "kind").Key);
        }

        [Test]
        public void No_preceding_condition_names_placement_on_the_line()
        {
            var condition = ConditionOf(new NoPrecedingCardOfSide(Side.Player));

            Assert.AreEqual("condition.no_preceding", condition.Key);
            Assert.AreEqual("side.player", Arg(condition, "side").Key);
        }

        [Test]
        public void No_following_condition_names_frozen_placement_order()
        {
            var condition = ConditionOf(new NoFollowingCardOfSide(Side.Enemy));

            Assert.AreEqual("condition.no_following", condition.Key);
            Assert.AreEqual("side.enemy", Arg(condition, "side").Key);
        }

        [Test]
        public void All_of_condition_chains_its_children_in_order()
        {
            var condition = ConditionOf(new AllOf(new Condition[]
            {
                new PreviousExecutedCardIs(Side.Player),
                new WithinNth(3),
                new FirstToTrigger()
            }));

            // ((previous ∧ within) ∧ first) — 왼쪽으로 접는다.
            Assert.AreEqual("condition.all", condition.Key);
            Assert.AreEqual("condition.first", Arg(condition, "next").Key);
            var inner = Arg(condition, "first");
            Assert.AreEqual("condition.all", inner.Key);
            Assert.AreEqual("condition.previous", Arg(inner, "first").Key);
            Assert.AreEqual(3, Arg(inner, "next").Arguments["n"]);
        }

        [Test]
        public void Empty_all_of_condition_fails_fast()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ConditionDescriptionRequests.Create(new AllOf(new Condition[0])));
        }
    }
}
