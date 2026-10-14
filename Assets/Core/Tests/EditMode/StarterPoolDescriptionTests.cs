using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests
{
    /// <summary>Description smoke test for the starter pool (Fix 2): every card's auto-generated
    /// description must yield request lines, All-target cards must keep the All target (no "random"
    /// fallback), and SkipOnBasic/requirement effects (독성 환원/증류) must carry the consumed-condition
    /// request exactly once with no bare start-condition sentence. 문장 자체는 번역 템플릿 몫이므로
    /// 요청 키·인자·구조만 단언한다.</summary>
    public class StarterPoolDescriptionTests
    {
        private static readonly DescriptionRequestCatalog Catalog = DescriptionRequestCatalog.CreateDefault(TestContent.Statuses());
        private static readonly CardContentCatalog Pool = TestContent.Cards();

        private static LocalizedCardDescription Compose(CardDefinition definition)
            => DescriptionComposer.Compose(definition, Catalog);

        private static IReadOnlyList<CardDefinition> StarterPoolCards()
        {
            var content = TestContent.Content();
            return content.Pools.Get("starter").Select(id => content.Cards.Get(id)).ToList();
        }

        private static LocalizedTextRequest Body(LocalizedTextRequest sentence)
            => (LocalizedTextRequest)sentence.Arguments["body"];

        private static LocalizedTextRequest Arg(LocalizedTextRequest request, string name)
            => (LocalizedTextRequest)request.Arguments[name];

        /// <summary>요청 트리의 모든 노드를 깊이 우선으로 편다.</summary>
        private static IEnumerable<LocalizedTextRequest> Walk(LocalizedTextRequest request)
        {
            yield return request;
            foreach (var nested in request.Arguments.Values.OfType<LocalizedTextRequest>())
                foreach (var child in Walk(nested))
                    yield return child;
        }

        private static IEnumerable<LocalizedTextRequest> AllRequests(LocalizedCardDescription doc)
            => doc.Lines.SelectMany(line => line.Sentences).SelectMany(Walk);

        private static readonly CardTargetKey EnemyFrontOne = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne);

        [Test]
        public void Every_pool_card_composes_a_non_empty_description()
        {
            foreach (var card in StarterPoolCards())
            {
                var doc = Compose(card);
                Assert.IsNotEmpty(doc.Lines, card.Id + " composed an empty description.");
                Assert.IsTrue(
                    AllRequests(doc).All(request => !string.IsNullOrWhiteSpace(request.Key)),
                    card.Id + " has a blank request key.");
            }
        }

        [Test]
        public void Spread_culture_keeps_the_all_enemies_target()
        {
            var doc = Compose(Pool.Get("spread_culture"));

            var target = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.All);
            CollectionAssert.AreEqual(new[] { target }, doc.TargetEntries);
            Assert.AreEqual(target, doc.Lines.Single().Target);
        }

        [Test]
        public void Toxic_reclaim_and_distill_carry_the_consumed_condition_without_a_bare_condition()
        {
            foreach (var id in new[] { "toxic_reclaim", "distill" })
            {
                var all = AllRequests(Compose(Pool.Get(id))).ToList();

                Assert.AreEqual(1, all.Count(r => r.Key == "effect.required"), id);
                Assert.AreEqual(1, all.Count(r => r.Key == "condition.consumed"), id);
                Assert.AreEqual(0, all.Count(r => r.Key == "sentence.conditional"), id);
            }
        }

        [Test]
        public void Skip_on_basic_effects_do_not_duplicate_the_basic_clause()
        {
            // Before the DescriptionComposer fix, a SkipOnBasic effect rendered its basic fragment
            // AND the success fragment with identical wording ("방어 3. 소비했다면 방어 3.").
            var toxic = AllRequests(Compose(Pool.Get("toxic_reclaim"))).ToList();
            Assert.AreEqual(1, toxic.Count(r => r.Key == "effect.apply_status"
                && Arg(r, "status").Key == "status.block.name"));

            var distill = AllRequests(Compose(Pool.Get("distill"))).ToList();
            Assert.AreEqual(1, distill.Count(r => r.Key == "effect.next_turn_fate"));
        }

        [Test]
        public void Spread_culture_requests()
        {
            var line = Compose(Pool.Get("spread_culture")).Lines.Single();

            CollectionAssert.AreEqual(
                new[] { "effect.damage", "effect.apply_status" },
                line.Sentences.Select(s => Body(s).Key).ToArray());
            Assert.AreEqual(2, Body(line.Sentences[0]).Arguments["amount"]);
            Assert.AreEqual("status.poison.name", Arg(Body(line.Sentences[1]), "status").Key);
            Assert.AreEqual(1, Body(line.Sentences[1]).Arguments["amount"]);
        }

        [Test]
        public void Toxic_reclaim_requests()
        {
            var doc = Compose(Pool.Get("toxic_reclaim"));

            Assert.AreEqual(2, doc.Lines.Count);
            Assert.AreEqual(EnemyFrontOne, doc.Lines[0].Target);
            var consume = Body(doc.Lines[0].Sentences[0]);
            Assert.AreEqual("effect.consume_status.up_to", consume.Key);
            Assert.AreEqual("status.poison.name", Arg(consume, "status").Key);
            Assert.AreEqual(1, consume.Arguments["amount"]);
            Assert.AreEqual("effect.apply_status", Body(doc.Lines[0].Sentences[1]).Key);

            Assert.AreEqual(new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self), doc.Lines[1].Target);
            var required = Body(doc.Lines[1].Sentences.Single());
            Assert.AreEqual("effect.required", required.Key);
            Assert.AreEqual("condition.consumed", Arg(required, "condition").Key);
            var block = Arg(required, "body");
            Assert.AreEqual("effect.apply_status", block.Key);
            Assert.AreEqual("status.block.name", Arg(block, "status").Key);
            Assert.AreEqual(3, block.Arguments["amount"]);
        }

        [Test]
        public void Distill_requests()
        {
            var doc = Compose(Pool.Get("distill"));

            Assert.AreEqual(2, doc.Lines.Count);
            Assert.AreEqual(EnemyFrontOne, doc.Lines[0].Target);
            CollectionAssert.AreEqual(
                new[] { "effect.consume_status.up_to", "effect.apply_status" },
                doc.Lines[0].Sentences.Select(s => Body(s).Key).ToArray());

            // 운명력 획득은 대상이 없어 대상 없는 줄에 따로 선다.
            Assert.IsNull(doc.Lines[1].Target);
            var required = Body(doc.Lines[1].Sentences.Single());
            Assert.AreEqual("effect.required", required.Key);
            Assert.AreEqual("condition.consumed", Arg(required, "condition").Key);
            Assert.AreEqual("effect.next_turn_fate", Arg(required, "body").Key);
            Assert.AreEqual(1, Arg(required, "body").Arguments["amount"]);
        }

        /// <summary>소비량 비례 가산은 피해 효과 문장에 붙는다(scaleBy). 구형은 소비 문장에 "(소비 1당 피해 +2)"를
        /// 달았다 — 가산을 받는 쪽이 피해 효과임을 문장 위치로 드러낸다(전투 실행 계약 T2a).</summary>
        [Test]
        public void Condensed_burst_requests()
        {
            var line = Compose(Pool.Get("condensed_burst")).Lines.Single();

            CollectionAssert.AreEqual(
                new[] { "effect.consume_status.up_to", "effect.scaled", "effect.apply_status" },
                line.Sentences.Select(s => Body(s).Key).ToArray());
            Assert.AreEqual(3, Body(line.Sentences[0]).Arguments["amount"]);
            var scaled = Body(line.Sentences[1]);
            Assert.AreEqual(2, scaled.Arguments["amount"]);
            Assert.AreEqual("effect.damage", Arg(scaled, "body").Key);
            Assert.AreEqual(2, Arg(scaled, "body").Arguments["amount"]);
        }

        [Test]
        public void Quick_cover_requests()
        {
            var line = Compose(Pool.Get("quick_cover")).Lines.Single();

            Assert.AreEqual(new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.FrontOne), line.Target);
            var block = Body(line.Sentences.Single());
            Assert.AreEqual("effect.apply_status", block.Key);
            Assert.AreEqual("status.block.name", Arg(block, "status").Key);
            Assert.AreEqual(3, block.Arguments["amount"]);
        }
    }
}
