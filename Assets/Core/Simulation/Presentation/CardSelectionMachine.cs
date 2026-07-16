using System;
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

    public readonly struct SelectionResult
    {
        private readonly SelectionTargetRef[] _targets;

        public bool IsComplete { get; }
        public int HandIndex { get; }
        public IReadOnlyList<SelectionTargetRef> Targets
            => _targets ?? Array.Empty<SelectionTargetRef>();

        private SelectionResult(
            bool isComplete, int handIndex, SelectionTargetRef[] targets)
        {
            IsComplete = isComplete;
            HandIndex = handIndex;
            _targets = targets;
        }

        public static SelectionResult None
            => new SelectionResult(false, -1, Array.Empty<SelectionTargetRef>());

        internal static SelectionResult Complete(
            int handIndex, IReadOnlyCollection<SelectionTargetRef> targets)
        {
            var copy = new SelectionTargetRef[targets.Count];
            int index = 0;
            foreach (var target in targets)
            {
                copy[index++] = target;
            }

            return new SelectionResult(true, handIndex, copy);
        }
    }

    /// <summary>Pure selection-flow state machine. The Unity layer supplies target references,
    /// applies completed results, and reports whether the commit succeeded.</summary>
    public sealed class CardSelectionMachine
    {
        private readonly List<SelectionTargetRef> _picked = new List<SelectionTargetRef>();
        private readonly ReadOnlyCollection<SelectionTargetRef> _pickedView;
        private SelectionTargetKind _targetKind;

        public CardSelectionMachine()
        {
            _pickedView = _picked.AsReadOnly();
        }

        public SelectionPhase Phase { get; private set; } = SelectionPhase.Idle;
        public int SelectedHandIndex { get; private set; } = -1;
        public int RequiredTargets { get; private set; }
        public IReadOnlyList<SelectionTargetRef> PickedTargets => _pickedView;

        public void SelectCard(
            int handIndex, SelectionTargetKind targetKind, int requiredTargets)
        {
            Cancel();
            SelectedHandIndex = handIndex;
            RequiredTargets = requiredTargets;
            _targetKind = targetKind;
            Phase = requiredTargets <= 0
                ? SelectionPhase.ConfirmPlacement
                : requiredTargets == 1
                    ? SelectionPhase.PickSingleTarget
                    : SelectionPhase.PickMultipleTargets;
        }

        public SelectionResult ClickApplyArea()
        {
            if (Phase != SelectionPhase.ConfirmPlacement)
            {
                return SelectionResult.None;
            }

            return SelectionResult.Complete(SelectedHandIndex, _picked);
        }

        public SelectionResult ClickTarget(SelectionTargetRef target)
        {
            if ((Phase != SelectionPhase.PickSingleTarget
                    && Phase != SelectionPhase.PickMultipleTargets)
                || target.Kind != _targetKind
                || _picked.Contains(target)
                || _picked.Count >= RequiredTargets)
            {
                return SelectionResult.None;
            }

            _picked.Add(target);
            if (Phase == SelectionPhase.PickSingleTarget)
            {
                return SelectionResult.Complete(SelectedHandIndex, _picked);
            }

            if (_picked.Count >= RequiredTargets)
            {
                Phase = SelectionPhase.ReadyToConfirm;
            }

            return SelectionResult.None;
        }

        public SelectionResult Confirm()
        {
            if (Phase != SelectionPhase.ReadyToConfirm)
            {
                return SelectionResult.None;
            }

            return SelectionResult.Complete(SelectedHandIndex, _picked);
        }

        public void CommitSucceeded()
        {
            Cancel();
        }

        public void RejectCompletion(IReadOnlyCollection<SelectionTargetRef> validTargets)
        {
            if (RequiredTargets == 1)
            {
                _picked.Clear();
            }
            else
            {
                var validTargetSet = new HashSet<SelectionTargetRef>(validTargets);
                _picked.RemoveAll(target => !validTargetSet.Contains(target));
            }

            Phase = RequiredTargets <= 0
                ? SelectionPhase.ConfirmPlacement
                : RequiredTargets == 1
                    ? SelectionPhase.PickSingleTarget
                    : SelectionPhase.PickMultipleTargets;
        }

        public void Cancel()
        {
            Phase = SelectionPhase.Idle;
            SelectedHandIndex = -1;
            RequiredTargets = 0;
            _targetKind = SelectionTargetKind.None;
            _picked.Clear();
        }
    }
}
