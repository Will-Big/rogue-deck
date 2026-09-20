using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Enemies;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>짝 전투는 약체 둘이다(전투 템포 설계 변경 4). 총 HP는 단독 고블린과 같고,
    /// 두 개체는 같은 덱의 사본을 각자 섞어 쓴다.</summary>
    public class GoblinRuntContentTests
    {
        private static ContentEncounterSource Source()
            => new ContentEncounterSource(TestContent.Content(), CombatRegistries.EnemyPolicies());

        [Test]
        public void The_pair_is_two_runts_with_the_same_total_hp_as_one_goblin()
        {
            var setup = Source().For("goblin_pair");

            CollectionAssert.AreEqual(
                new[] { "goblin_runt#0", "goblin_runt#1" },
                setup.Enemies.Select(e => e.Enemy.Id).ToArray());
            Assert.AreEqual(28, setup.Enemies.Sum(e => e.Enemy.Hp));
        }

        [Test]
        public void Each_runt_gets_its_own_shuffle_bag_over_one_shared_deck()
        {
            var definition = TestContent.Content().Enemies.Get("goblin_runt");
            var setup = Source().For("goblin_pair");

            Assert.AreEqual(EnemyPolicyKeys.ShuffleBag, definition.Policy);
            Assert.AreNotSame(
                setup.Enemies[0].Policy, setup.Enemies[1].Policy,
                "개체마다 정책 인스턴스가 따로여야 가방이 갈린다.");
            Assert.AreEqual(4, definition.Bundles.Count);
        }

        [Test]
        public void Runt_jab_hits_the_front_party_member_for_three()
        {
            var card = TestContent.Content().Cards.Get("runt_jab");

            Assert.AreEqual(Side.Enemy, card.Side);
            Assert.AreEqual(6, card.BaseExecutionOrder);
            Assert.AreEqual(CardTargetRange.FrontOne, card.AllyTarget);
            Assert.AreEqual(
                3, card.Effects.Where(e => e.Key == EffectKeys.Damage).Sum(e => e.EffectValue));
        }
    }
}
