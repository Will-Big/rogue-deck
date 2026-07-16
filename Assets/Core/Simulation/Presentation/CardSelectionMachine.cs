using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FateWeaver.Simulation.Presentation
{
    public enum SelectionPhase
    {
        Idle,
        ConfirmPlacement,
        PickSingleTarget,
        PickMultipleTargets,
        ReadyToConfirm
    }

    /// <summary>Command emitted when the UI has collected enough explicit input to call the session.</summary>
    public readonly struct SelectionCommand
    {
        public bool PlayExecution { get; }
        public bool PlayIntervention { get; }
        public int HandIndex { get; }
        public int TargetA { get; }
        public int TargetB { get; }

        private SelectionCommand(bool playExecution, bool playIntervention, int handIndex, int targetA, int targetB)
        {
            PlayExecution = playExecution;
            PlayIntervention = playIntervention;
            HandIndex = handIndex;
            TargetA = targetA;
            TargetB = targetB;
        }

        public static SelectionCommand None => new SelectionCommand(false, false, -1, -1, -1);

        public static SelectionCommand Execution(int handIndex)
            => new SelectionCommand(true, false, handIndex, -1, -1);

        public static SelectionCommand Intervention(int handIndex, int targetA, int targetB = -1)
            => new SelectionCommand(false, true, handIndex, targetA, targetB);
    }

    /// <summary>Pure selection-flow state machine. The Unity layer supplies clicks and applies emitted
    /// commands; party-member targeting is intentionally handled by the party battle controller.</summary>
    public sealed class CardSelectionMachine
    {
        private readonly List<int> _picked = new List<int>();
        private readonly ReadOnlyCollection<int> _pickedView;

        public CardSelectionMachine()
        {
            _pickedView = _picked.AsReadOnly();
        }

        public SelectionPhase Phase { get; private set; } = SelectionPhase.Idle;
        public int SelectedHandIndex { get; private set; } = -1;
        public int RequiredTargets { get; private set; }
        public IReadOnlyList<int> PickedTargets => _pickedView;

        public void SelectCard(int handIndex, int requiredTargets)
        {
            Cancel();
            SelectedHandIndex = handIndex;
            RequiredTargets = requiredTargets;
            Phase = requiredTargets <= 0
                ? SelectionPhase.ConfirmPlacement
                : requiredTargets == 1
                    ? SelectionPhase.PickSingleTarget
                    : SelectionPhase.PickMultipleTargets;
        }

        public SelectionCommand ClickApplyArea()
        {
            if (Phase != SelectionPhase.ConfirmPlacement)
            {
                return SelectionCommand.None;
            }

            var command = SelectionCommand.Execution(SelectedHandIndex);
            Cancel();
            return command;
        }

        public SelectionCommand ClickTarget(int zoneIndex)
        {
            if (Phase == SelectionPhase.PickSingleTarget)
            {
                var command = SelectionCommand.Intervention(SelectedHandIndex, zoneIndex);
                Cancel();
                return command;
            }

            if (Phase == SelectionPhase.PickMultipleTargets && !_picked.Contains(zoneIndex))
            {
                _picked.Add(zoneIndex);
                if (_picked.Count >= RequiredTargets)
                {
                    Phase = SelectionPhase.ReadyToConfirm;
                }
            }

            return SelectionCommand.None;
        }

        public SelectionCommand Confirm()
        {
            if (Phase != SelectionPhase.ReadyToConfirm)
            {
                return SelectionCommand.None;
            }

            var command = SelectionCommand.Intervention(SelectedHandIndex, _picked[0], _picked[1]);
            Cancel();
            return command;
        }

        public void Cancel()
        {
            Phase = SelectionPhase.Idle;
            SelectedHandIndex = -1;
            RequiredTargets = 0;
            _picked.Clear();
        }
    }
}
