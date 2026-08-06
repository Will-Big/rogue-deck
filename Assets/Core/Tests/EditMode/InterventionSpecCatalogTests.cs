using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    /// <summary>저작 명부와 런타임 레지스트리가 같은 키 집합을 덮는지, 각 스펙이 자기 페이로드를
    /// 만드는지 잠근다. 둘이 어긋나면 저작은 되는데 실행 핸들러가 없는 카드가 생긴다.</summary>
    public class InterventionSpecCatalogTests
    {
        [Test]
        public void Every_authored_spec_has_a_registered_runtime_handler()
        {
            var context = AuthoringContext.Default();

            foreach (var info in InterventionSpecCatalog.All())
            {
                Assert.IsTrue(
                    context.HasIntervention(info.Create().Key),
                    "저작 명부의 '" + info.DisplayName + "'에 런타임 핸들러가 없다.");
            }
        }

        [Test]
        public void Catalog_has_no_duplicate_keys()
        {
            var ids = InterventionSpecCatalog.All().Select(i => i.Create().Key.Id).ToList();

            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void Change_execution_order_spec_builds_its_payload()
        {
            var spec = new ChangeExecutionOrderSpec
            {
                Delta = -2,
                TargetSide = InterventionTargetSideRef.Player
            };

            var payload = (ChangeExecutionOrderPayload)spec.ToPayload();

            Assert.AreEqual(-2, payload.Delta);
            Assert.AreEqual(Side.Player, payload.TargetSide);
        }

        [Test]
        public void Swap_execution_order_spec_builds_its_payload()
        {
            var spec = new SwapExecutionOrderSpec { RequireAdjacent = true };

            var payload = (SwapExecutionOrderPayload)spec.ToPayload();

            Assert.IsTrue(payload.RequireAdjacent);
            Assert.IsNull(payload.TargetSide);
        }

        [Test]
        public void Lock_spec_has_no_payload()
        {
            Assert.IsNull(new LockSpec().ToPayload());
        }
    }
}
