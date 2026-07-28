using System;
using NUnit.Framework;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class TargetingRequirementTests
    {
        [Test]
        public void Change_and_lock_declare_one_rail_target()
        {
            Assert.AreEqual(TargetKind.RailCard, new ChangeExecutionOrderHandler().Targeting.Kind);
            Assert.AreEqual(1, new ChangeExecutionOrderHandler().Targeting.Count);
            Assert.AreEqual(TargetKind.RailCard, new LockHandler().Targeting.Kind);
            Assert.AreEqual(1, new LockHandler().Targeting.Count);
        }

        [Test]
        public void Swap_declares_two_distinct_rail_targets()
        {
            var targeting = new SwapExecutionOrderHandler().Targeting;
            Assert.AreEqual(TargetKind.RailCard, targeting.Kind);
            Assert.AreEqual(2, targeting.Count);
            Assert.IsFalse(targeting.AllowDuplicates);
        }

        [Test]
        public void None_requirement_is_the_default()
        {
            Assert.AreEqual(TargetKind.None, TargetingRequirement.None.Kind);
            Assert.AreEqual(0, TargetingRequirement.None.Count);
        }

        [Test]
        public void Rail_requirement_rejects_nonpositive_count()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TargetingRequirement.RailCards(0));
        }

        // 확장 증명: 새 2대상 핸들러 = 클래스 1개 + 키 등록. 선언 누락은 컴파일 에러.
        [Test]
        public void A_new_handler_exposes_its_requirement_through_the_registry()
        {
            var registry = new InterventionActionRegistry();
            registry.Register(new FakeDoubleLockHandler());

            var resolved = registry.Resolve(FakeDoubleLockHandler.FakeKey).Targeting;

            Assert.AreEqual(TargetKind.RailCard, resolved.Kind);
            Assert.AreEqual(2, resolved.Count);
        }

        private sealed class FakeDoubleLockHandler : IInterventionActionHandler
        {
            public static readonly InterventionActionKey FakeKey =
                new InterventionActionKey("test_double_lock");

            public InterventionActionKey Key => FakeKey;
            public TargetingRequirement Targeting => TargetingRequirement.RailCards(2);
            public bool CanApply(InterventionPlayContext ctx) => false;
            public void Apply(InterventionPlayContext ctx) { }
        }
    }
}
