using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>시드 파생식은 사실상 저장 형식이다 — 바뀌면 모든 기존 시드의 결과가 바뀐다.
    /// 골든 값은 설계 확정 시점(2026-09-15)에 같은 식을 독립 구현으로 계산해 박았다.</summary>
    public class SeedDerivationTests
    {
        [Test]
        public void NodeSeed_matches_the_golden_values()
        {
            Assert.AreEqual(459615264, SeedDerivation.NodeSeed(1, 0));
            Assert.AreEqual(479680206, SeedDerivation.NodeSeed(1, 1));
            Assert.AreEqual(-672360639, SeedDerivation.NodeSeed(-7, 3));
        }

        [Test]
        public void Stream_matches_the_golden_values()
        {
            var node = SeedDerivation.NodeSeed(1, 0);

            Assert.AreEqual(1652837576, SeedDerivation.Stream(node, SeedStream.Encounter));
            Assert.AreEqual(1998286874, SeedDerivation.Stream(node, SeedStream.Combat));
            Assert.AreEqual(-45639579, SeedDerivation.Stream(node, SeedStream.Reward));
        }

        [Test]
        public void Same_inputs_give_the_same_seed()
        {
            Assert.AreEqual(SeedDerivation.NodeSeed(42, 5), SeedDerivation.NodeSeed(42, 5));
        }

        [Test]
        public void Adjacent_nodes_and_streams_differ()
        {
            var node0 = SeedDerivation.NodeSeed(42, 0);
            var node1 = SeedDerivation.NodeSeed(42, 1);

            Assert.AreNotEqual(node0, node1);
            Assert.AreNotEqual(
                SeedDerivation.Stream(node0, SeedStream.Combat),
                SeedDerivation.Stream(node0, SeedStream.Reward));
            Assert.AreNotEqual(
                SeedDerivation.Stream(node0, SeedStream.Encounter),
                SeedDerivation.Stream(node0, SeedStream.Combat));
        }
    }
}
