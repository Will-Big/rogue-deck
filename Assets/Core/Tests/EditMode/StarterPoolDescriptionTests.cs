using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests
{
    /// <summary>Description smoke test for the starter pool (Fix 2): every card's auto-generated
    /// description must render something, All-target cards must not fall back to "무작위" wording, and
    /// SkipOnBasic conditional effects (독성 환원/증류) must render the consumed-condition stem exactly
    /// once with no bare "이면".</summary>
    public class StarterPoolDescriptionTests
    {
        private static readonly KoreanDescriptionCatalog Korean = KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses());
        private static readonly CardContentCatalog Pool = TestContent.Cards();

        private static string Describe(CardDefinition definition)
            => DescriptionComposer.Describe(definition, Korean);

        private static IReadOnlyList<CardDefinition> StarterPoolCards()
        {
            var content = TestContent.Content();
            return content.Pools.Get("starter").Select(id => content.Cards.Get(id)).ToList();
        }

        [Test]
        public void Every_pool_card_composes_a_non_empty_description()
        {
            foreach (var card in StarterPoolCards())
            {
                var text = Describe(card);
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), card.Id + " composed an empty description.");
            }
        }

        [Test]
        public void Spread_culture_never_falls_back_to_random_wording()
        {
            StringAssert.DoesNotContain("무작위", Describe(Pool.Get("spread_culture")));
        }

        [Test]
        public void Toxic_reclaim_and_distill_render_the_consumed_condition_stem_without_a_bare_condition()
        {
            var toxic = Describe(Pool.Get("toxic_reclaim"));
            var distill = Describe(Pool.Get("distill"));

            StringAssert.Contains("소비했다면", toxic);
            StringAssert.Contains("소비했다면", distill);
            StringAssert.DoesNotContain("이면", toxic);
            StringAssert.DoesNotContain("이면", distill);
        }

        [Test]
        public void Skip_on_basic_effects_do_not_duplicate_the_basic_clause()
        {
            // Before the DescriptionComposer fix, a SkipOnBasic effect rendered its basic fragment
            // AND the success fragment with identical wording ("방어 4. 소비했다면 방어 4.").
            var toxic = Describe(Pool.Get("toxic_reclaim"));
            Assert.AreEqual(1, CountOccurrences(toxic, "방어 4"));

            var distill = Describe(Pool.Get("distill"));
            Assert.AreEqual(1, CountOccurrences(distill, "운명력 1 획득"));
        }

        [Test]
        public void Korean_spread_culture() =>
            Assert.AreEqual(
                "[◆] 피해 2. 독 1.",
                Describe(Pool.Get("spread_culture")));

        [Test]
        public void Korean_toxic_reclaim() =>
            Assert.AreEqual(
                "[◆] 독 최대 1 소비. 독 1.\n[◆] 소비했다면 방어 4.",
                Describe(Pool.Get("toxic_reclaim")));

        [Test]
        public void Korean_distill() =>
            Assert.AreEqual(
                "[◆] 독 최대 1 소비. 독 1.\n소비했다면 다음 사용 턴에 운명력 1 획득.",
                Describe(Pool.Get("distill")));

        [Test]
        public void Korean_quick_cover() =>
            Assert.AreEqual(
                "[◆] 방어 4.",
                Describe(Pool.Get("quick_cover")));

        private static int CountOccurrences(string haystack, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }
    }
}
