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
            machine.SelectCard(2, 0);
            Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);

            var command = machine.ClickApplyArea();

            Assert.IsTrue(command.PlayExecution);
            Assert.AreEqual(2, command.HandIndex);
            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
        }

        [Test]
        public void Apply_area_click_does_nothing_while_picking_targets()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 1);

            var command = machine.ClickApplyArea();

            Assert.IsFalse(command.PlayExecution || command.PlayIntervention);
            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
        }

        [Test]
        public void Single_target_commits_on_target_click()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(1, 1);

            var command = machine.ClickTarget(3);

            Assert.IsTrue(command.PlayIntervention);
            Assert.AreEqual(1, command.HandIndex);
            Assert.AreEqual(3, command.TargetA);
            Assert.AreEqual(-1, command.TargetB);
            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
        }

        [Test]
        public void Target_click_in_confirm_placement_is_ignored()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 0);

            var command = machine.ClickTarget(1);

            Assert.IsFalse(command.PlayExecution || command.PlayIntervention);
            Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);
        }

        [Test]
        public void Two_target_flow_requires_distinct_picks_then_confirm()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(4, 2);
            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);

            Assert.IsFalse(machine.ClickTarget(1).PlayIntervention);
            CollectionAssert.AreEqual(new[] { 1 }, machine.PickedTargets);

            Assert.IsFalse(machine.ClickTarget(1).PlayIntervention);
            CollectionAssert.AreEqual(new[] { 1 }, machine.PickedTargets);

            Assert.IsFalse(machine.ClickTarget(3).PlayIntervention);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);

            var command = machine.Confirm();

            Assert.IsTrue(command.PlayIntervention);
            Assert.AreEqual(4, command.HandIndex);
            Assert.AreEqual(1, command.TargetA);
            Assert.AreEqual(3, command.TargetB);
            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
        }

        [Test]
        public void Confirm_before_requirement_met_does_nothing()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);

            var command = machine.Confirm();

            Assert.IsFalse(command.PlayIntervention);
            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
        }

        [Test]
        public void Cancel_clears_everything_without_command()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);

            machine.Cancel();

            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
            Assert.AreEqual(0, machine.PickedTargets.Count);
            Assert.AreEqual(-1, machine.SelectedHandIndex);
        }

        [Test]
        public void Selecting_another_card_resets_previous_picks()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);

            machine.SelectCard(3, 1);

            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
            Assert.AreEqual(0, machine.PickedTargets.Count);
            Assert.AreEqual(3, machine.SelectedHandIndex);
        }

        [Test]
        public void Extra_target_click_after_ready_is_ignored()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);
            machine.ClickTarget(2);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);

            Assert.IsFalse(machine.ClickTarget(4).PlayIntervention);
            CollectionAssert.AreEqual(new[] { 1, 2 }, machine.PickedTargets);
        }
    }
}
