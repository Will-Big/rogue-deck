using NUnit.Framework;
using FateWeaver.Simulation.Presentation;

namespace FateWeaver.Tests
{
    public class CardSelectionMachineTests
    {
        [Test]
        public void Starts_idle()
        {
            Assert.AreEqual(SelectionPhase.Idle, new CardSelectionMachine().Phase);
        }

        [Test]
        public void Zero_target_card_waits_for_apply_area_click()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(2, SelectionTargetKind.None, 0);
            Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);

            var result = machine.ClickApplyArea();

            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(2, result.HandIndex);
            CollectionAssert.IsEmpty(result.Targets);
            Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);
        }

        [Test]
        public void Apply_area_click_does_nothing_while_picking_targets()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 1);

            var result = machine.ClickApplyArea();

            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
        }

        [Test]
        public void Single_target_completes_without_confirmation()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(1, SelectionTargetKind.ExecutionCard, 1);
            var target = SelectionTargetRef.ExecutionCard(1);

            var result = machine.ClickTarget(target);

            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(1, result.HandIndex);
            CollectionAssert.AreEqual(new[] { target }, result.Targets);
            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
        }

        [Test]
        public void Pending_single_target_completion_ignores_another_target()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(0);
            var second = SelectionTargetRef.ExecutionCard(1);
            machine.SelectCard(1, SelectionTargetKind.ExecutionCard, 1);

            Assert.IsTrue(machine.ClickTarget(first).IsComplete);

            var result = machine.ClickTarget(second);

            Assert.IsFalse(result.IsComplete);
            CollectionAssert.AreEqual(new[] { first }, machine.PickedTargets);
        }

        [Test]
        public void Target_click_in_confirm_placement_is_ignored()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, SelectionTargetKind.None, 0);

            var result = machine.ClickTarget(SelectionTargetRef.ExecutionCard(1));

            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);
        }

        [Test]
        public void Multiple_targets_require_explicit_confirmation()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(1);
            var second = SelectionTargetRef.ExecutionCard(3);
            machine.SelectCard(4, SelectionTargetKind.ExecutionCard, 2);

            Assert.IsFalse(machine.ClickTarget(first).IsComplete);
            Assert.IsFalse(machine.ClickTarget(second).IsComplete);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);
            CollectionAssert.AreEqual(new[] { first, second }, machine.PickedTargets);

            var result = machine.Confirm();
            Assert.IsTrue(result.IsComplete);
            CollectionAssert.AreEqual(new[] { first, second }, result.Targets);
        }

        [Test]
        public void Confirm_before_requirement_met_does_nothing()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 2);
            machine.ClickTarget(SelectionTargetRef.ExecutionCard(1));

            var result = machine.Confirm();

            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
        }

        [Test]
        public void Rejected_completion_removes_invalid_picks_and_resumes_selection()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(1);
            var second = SelectionTargetRef.ExecutionCard(3);
            machine.SelectCard(4, SelectionTargetKind.ExecutionCard, 2);
            machine.ClickTarget(first);
            machine.ClickTarget(second);
            machine.Confirm();

            machine.RejectCompletion(new[] { second, SelectionTargetRef.ExecutionCard(5) });

            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
            CollectionAssert.AreEqual(new[] { second }, machine.PickedTargets);
        }

        [Test]
        public void Rejected_completion_with_all_required_targets_still_valid_stays_ready_to_confirm()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(1);
            var second = SelectionTargetRef.ExecutionCard(3);
            machine.SelectCard(4, SelectionTargetKind.ExecutionCard, 2);
            machine.ClickTarget(first);
            machine.ClickTarget(second);
            machine.Confirm();

            machine.RejectCompletion(new[] { first, second });

            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);
            var result = machine.Confirm();
            Assert.IsTrue(result.IsComplete);
            CollectionAssert.AreEqual(new[] { first, second }, result.Targets);
        }

        [Test]
        public void Rejected_single_target_completion_clears_pick_and_allows_another_target()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(0);
            var second = SelectionTargetRef.ExecutionCard(1);
            machine.SelectCard(1, SelectionTargetKind.ExecutionCard, 1);
            machine.ClickTarget(first);

            machine.RejectCompletion(new[] { first, second });

            CollectionAssert.IsEmpty(machine.PickedTargets);
            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
            Assert.IsTrue(machine.ClickTarget(second).IsComplete);
            CollectionAssert.AreEqual(new[] { second }, machine.PickedTargets);
        }

        [Test]
        public void Successful_completion_is_the_only_operation_that_returns_to_idle()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(2, SelectionTargetKind.ExecutionCard, 1);
            machine.ClickTarget(SelectionTargetRef.ExecutionCard(0));

            machine.CommitSucceeded();

            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
            Assert.AreEqual(0, machine.PickedTargets.Count);
        }

        [Test]
        public void Cancel_clears_everything_without_result()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 2);
            machine.ClickTarget(SelectionTargetRef.ExecutionCard(1));

            machine.Cancel();

            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
            Assert.AreEqual(0, machine.PickedTargets.Count);
            Assert.AreEqual(-1, machine.SelectedHandIndex);
        }

        [Test]
        public void Selecting_another_card_resets_previous_picks()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 2);
            machine.ClickTarget(SelectionTargetRef.ExecutionCard(1));

            machine.SelectCard(3, SelectionTargetKind.ExecutionCard, 1);

            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
            Assert.AreEqual(0, machine.PickedTargets.Count);
            Assert.AreEqual(3, machine.SelectedHandIndex);
        }

        [Test]
        public void Selected_multiple_target_click_removes_pick_before_requirement_is_met()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(1);
            var second = SelectionTargetRef.ExecutionCard(2);
            machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 3);
            machine.ClickTarget(first);
            machine.ClickTarget(second);

            var result = machine.ClickTarget(first);

            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
            CollectionAssert.AreEqual(new[] { second }, machine.PickedTargets);
        }

        [Test]
        public void Ready_target_click_removes_pick_and_reselection_restores_ready()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(1);
            var second = SelectionTargetRef.ExecutionCard(2);
            machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 2);
            machine.ClickTarget(first);
            machine.ClickTarget(second);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);

            Assert.IsFalse(machine.ClickTarget(first).IsComplete);
            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
            CollectionAssert.AreEqual(new[] { second }, machine.PickedTargets);

            Assert.IsFalse(machine.ClickTarget(first).IsComplete);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);
            CollectionAssert.AreEqual(new[] { second, first }, machine.PickedTargets);
        }

        [Test]
        public void Extra_target_click_after_ready_is_ignored()
        {
            var machine = new CardSelectionMachine();
            var first = SelectionTargetRef.ExecutionCard(1);
            var second = SelectionTargetRef.ExecutionCard(2);
            machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 2);
            machine.ClickTarget(first);
            machine.ClickTarget(second);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);

            Assert.IsFalse(machine.ClickTarget(SelectionTargetRef.ExecutionCard(4)).IsComplete);
            CollectionAssert.AreEqual(new[] { first, second }, machine.PickedTargets);
        }
    }
}
