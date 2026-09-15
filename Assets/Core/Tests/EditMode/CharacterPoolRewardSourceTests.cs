using System;
using System.Linq;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CharacterPoolRewardSourceTests
    {
        [Test]
        public void Eligible_lists_the_living_characters_pool_owned_by_that_character()
        {
            var content = TestContent.Content();
            var source = new CharacterPoolRewardSource(content);

            var pairs = source.Eligible(new[] { "member_a" });

            var poolIds = content.Pools.Get(content.Characters.Get("member_a").Pool)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(poolIds, pairs.Select(p => p.Card.Id).ToArray());
            Assert.IsTrue(pairs.All(p => p.OwnerId == "member_a"));
        }

        [Test]
        public void Eligible_follows_the_given_character_order()
        {
            var content = TestContent.Content();
            var source = new CharacterPoolRewardSource(content);
            var poolSize = content.Pools.Get("starter").Count;

            var pairs = source.Eligible(new[] { "member_b", "member_a" });

            Assert.AreEqual(poolSize * 2, pairs.Count, "임시 데이터: 두 캐릭터가 같은 starter 풀이다.");
            Assert.IsTrue(pairs.Take(poolSize).All(p => p.OwnerId == "member_b"));
            Assert.IsTrue(pairs.Skip(poolSize).All(p => p.OwnerId == "member_a"));
        }

        [Test]
        public void Eligible_is_empty_without_living_characters()
        {
            var source = new CharacterPoolRewardSource(TestContent.Content());

            Assert.AreEqual(0, source.Eligible(Array.Empty<string>()).Count);
        }
    }
}
