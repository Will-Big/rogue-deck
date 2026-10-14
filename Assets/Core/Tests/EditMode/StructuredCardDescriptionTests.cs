using System;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests.EditMode
{
    public class StructuredCardDescriptionTests
    {
        private static readonly DescriptionRequestCatalog Catalog =
            DescriptionRequestCatalog.CreateDefault(TestContent.Statuses());

        [Test]
        public void Toxic_reclaim_separates_enemy_and_ally_self_lines()
        {
            var definition = TestContent.Cards().Get("toxic_reclaim");

            var layout = DescriptionComposer.Compose(definition, Catalog);

            CollectionAssert.AreEqual(
                new[]
                {
                    new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
                    new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne)
                },
                layout.TargetEntries);
            Assert.AreEqual(2, layout.Lines.Count);
            Assert.AreEqual(
                new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne),
                layout.Lines[0].Target);
            CollectionAssert.AreEqual(
                new[] { "effect.consume_status.up_to", "effect.apply_status" },
                BodyKeys(layout.Lines[0]));
            Assert.AreEqual(
                new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
                layout.Lines[1].Target);
            CollectionAssert.AreEqual(new[] { "effect.required" }, BodyKeys(layout.Lines[1]));
        }

        [Test]
        public void Repeated_nonconsecutive_target_joins_the_first_matching_line()
        {
            var layout = DescriptionComposer.Compose(
                Execution("repeat", DamageEnemy(3), BlockSelf(2), DamageEnemy(3)), Catalog);

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.AreEqual(2, layout.TargetEntries.Count);
            Assert.AreEqual(
                new CardTargetKey(
                    CardTargetFaction.Enemy,
                    CardTargetRange.FrontOne),
                layout.Lines[0].Target.Value);
            CollectionAssert.AreEqual(new[] { "effect.damage", "effect.damage" }, BodyKeys(layout.Lines[0]));
            CollectionAssert.AreEqual(new object[] { 3, 3 }, Amounts(layout.Lines[0]));
            Assert.AreEqual(
                new CardTargetKey(
                    CardTargetFaction.Ally,
                    CardTargetRange.Self),
                layout.Lines[1].Target.Value);
            CollectionAssert.AreEqual(new[] { "effect.apply_status" }, BodyKeys(layout.Lines[1]));
            CollectionAssert.AreEqual(new object[] { 2 }, Amounts(layout.Lines[1]));
            CollectionAssert.AreEqual(
                new[]
                {
                    new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
                    new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne)
                },
                layout.TargetEntries);
        }

        [Test]
        public void Nonconsecutive_null_targets_share_one_line_without_deduplication()
        {
            var layout = DescriptionComposer.Compose(
                Execution(
                    "repeat_null",
                    new EffectData(EffectKeys.GrantNextTurnFate, 1),
                    DamageEnemy(3),
                    new EffectData(EffectKeys.GrantNextTurnFate, 2)),
                Catalog);

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.IsNull(layout.Lines[0].Target);
            CollectionAssert.AreEqual(
                new[] { "effect.next_turn_fate", "effect.next_turn_fate" },
                BodyKeys(layout.Lines[0]));
            CollectionAssert.AreEqual(new object[] { 1, 2 }, Amounts(layout.Lines[0]));
            Assert.AreEqual(
                new CardTargetKey(
                    CardTargetFaction.Enemy,
                    CardTargetRange.FrontOne),
                layout.Lines[1].Target.Value);
            CollectionAssert.AreEqual(new[] { "effect.damage" }, BodyKeys(layout.Lines[1]));
            // null 대상은 표시할 대상 항목이 없다.
            CollectionAssert.AreEqual(
                new[] { new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne) },
                layout.TargetEntries);
        }

        [Test]
        public void Target_entries_list_allies_first_then_by_range()
        {
            var card = new CardDefinition("order", "order", Side.Player, 0, new[]
            {
                DamageEnemy(1),
                EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 1)
            })
            {
                AllyTarget = CardTargetRange.All,
                EnemyTarget = CardTargetRange.BackTwo,
                Category = CardCategory.Execution
            };

            var layout = DescriptionComposer.Compose(card, Catalog);

            // 줄은 저작 순서(적 먼저), 대상 항목은 아군 먼저다.
            Assert.AreEqual(CardTargetFaction.Enemy, layout.Lines[0].Target.Value.Faction);
            CollectionAssert.AreEqual(
                new[]
                {
                    new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.All),
                    new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.BackTwo)
                },
                layout.TargetEntries);
        }

        [Test]
        public void Every_default_and_generated_card_composes_deterministically()
        {
            // TestContent.Cards().Cards.Values는 Content/Cards/*.json 전부다 — 시작 풀,
            // fixture_* 카드, 적 카드를 모두 포함한다.
            var cards = TestContent.Cards().Cards.Values;
            foreach (var card in cards)
            {
                var first = DescriptionComposer.Compose(card, Catalog);
                var second = DescriptionComposer.Compose(card, Catalog);

                CollectionAssert.AreEqual(first.TargetEntries, second.TargetEntries, card.Id);
                Assert.AreEqual(first.Lines.Count, second.Lines.Count, card.Id);
                for (var i = 0; i < first.Lines.Count; i++)
                {
                    Assert.AreEqual(first.Lines[i].Target, second.Lines[i].Target, card.Id);
                    Assert.AreEqual(Flatten(first.Lines[i]), Flatten(second.Lines[i]), card.Id);
                }
            }
        }

        private static string[] BodyKeys(LocalizedDescriptionLine line)
            => line.Sentences.Select(sentence => Body(sentence).Key).ToArray();

        private static object[] Amounts(LocalizedDescriptionLine line)
            => line.Sentences.Select(sentence => Body(sentence).Arguments["amount"]).ToArray();

        private static LocalizedTextRequest Body(LocalizedTextRequest sentence)
            => (LocalizedTextRequest)sentence.Arguments["body"];

        /// <summary>요청 트리를 키·인자 순서가 고정된 문자열로 편다(인자는 이름순).</summary>
        private static string Flatten(LocalizedDescriptionLine line)
            => string.Join(" | ", line.Sentences.Select(Flatten));

        private static string Flatten(LocalizedTextRequest request)
            => request.Table + ":" + request.Key + "(" + string.Join(",",
                request.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
                    pair.Key + "=" + (pair.Value is LocalizedTextRequest nested ? Flatten(nested) : pair.Value)))
                + ")";

        /// <summary>플레이어 실행 카드. 아군 효과는 자신, 적 효과는 적 전열 하나를 고른다.</summary>
        private static CardDefinition Execution(string id, params EffectData[] effects)
            => new CardDefinition(id, id, Side.Player, 0, effects)
            {
                AllyTarget = CardTargetRange.Self,
                EnemyTarget = CardTargetRange.FrontOne,
                Category = CardCategory.Execution
            };

        private static EffectData DamageEnemy(int amount)
            => new EffectData(EffectKeys.Damage, amount) { TargetFaction = CardTargetFaction.Enemy };

        private static EffectData BlockSelf(int amount)
            => EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, amount);
    }
}
