using System.Linq;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>고블린의 묶음 목록이 "덱"으로 동작하는지 잠근다(전투 템포 설계 변경 3).
    /// 공격 없는 묶음이 있으면 적이 쉬는 턴이 생기고, 복원 추출 정책이면 순서 개념이 없어진다.</summary>
    public class GoblinBundleContentTests
    {
        [Test]
        public void Every_bundle_has_at_least_one_attack()
        {
            var goblin = TestContent.Content().Enemies.Get("goblin");

            Assert.IsTrue(
                goblin.Bundles.All(bundle => bundle.Cards.Any(card => card.Id != "crude_guard")),
                "방어만 있는 묶음이 있으면 적이 아무것도 하지 않는 턴이 생긴다.");
        }

        [Test]
        public void Goblin_draws_its_bundles_without_replacement()
        {
            Assert.AreEqual(
                EnemyPolicyKeys.ShuffleBag, TestContent.Content().Enemies.Get("goblin").Policy);
        }
    }
}
