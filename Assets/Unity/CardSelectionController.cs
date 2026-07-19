using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Coordinates targetless placement and every explicit card-target selection flow.</summary>
    public sealed class CardSelectionController : MonoBehaviour
    {
        [SerializeField] private HandFanView _hand;
        [SerializeField] private ExecutionRailView _rail;
        [SerializeField] private GameObject _dimLayer;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TargetingArrowView _arrow;

        private readonly CardSelectionMachine _machine = new CardSelectionMachine();
        private readonly HashSet<SelectionTargetRef> _validTargets =
            new HashSet<SelectionTargetRef>();
        private readonly Dictionary<SelectionTargetRef, UnitView> _unitTargets =
            new Dictionary<SelectionTargetRef, UnitView>();
        private Func<SelectionResult, bool> _tryApply;
        private Func<SelectionTargetKind, IReadOnlyList<SelectionTargetRef>> _currentTargets;
        private Action _onApplied;
        private int _visualHandIndex = -1;
        private int _hoverHandIndex = -1;
        private SelectionTargetKind _targetKind = SelectionTargetKind.None;

        public bool SelectionActive => _machine.Phase != SelectionPhase.Idle;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        public void Initialize(
            Func<SelectionResult, bool> tryApply,
            Func<SelectionTargetKind, IReadOnlyList<SelectionTargetRef>> currentTargets,
            Action onApplied)
        {
            _tryApply = tryApply;
            _currentTargets = currentTargets;
            _onApplied = onApplied;
        }

        public void RegisterUnitTarget(SelectionTargetRef target, UnitView view)
        {
            _unitTargets[target] = view;
        }

        public void ClearUnitTargets()
        {
            _unitTargets.Clear();
        }

        public void BeginPlacement(
            int handIndex, CardPresentation card, int insertionIndex)
        {
            EndSelectionVisuals();
            _machine.SelectCard(handIndex, SelectionTargetKind.None, 0);
            _visualHandIndex = handIndex;
            _hoverHandIndex = -1;
            _hand.SetHeld(handIndex, true);
            _hand.SetHoverSuppressed(true);
            _hand.SetSelection(handIndex, CardView.SelectionKind.Secondary);
            _rail.SetDropHint(true);
            _rail.ShowPlacementHover(card, insertionIndex);
            _rail.ArmPlacementPreview(OnPlacementPreviewClicked);
        }

        public void ShowPlacementHover(
            int handIndex, CardPresentation card, int insertionIndex)
        {
            if (SelectionActive || card.Category != CardCategory.Execution)
            {
                return;
            }

            _hoverHandIndex = handIndex;
            _rail.ShowPlacementHover(card, insertionIndex);
        }

        public void HidePlacementHover(int handIndex)
        {
            if (SelectionActive || _hoverHandIndex != handIndex)
            {
                return;
            }

            _hoverHandIndex = -1;
            _rail.ClearPlacementPreview();
        }

        public void BeginTargetSelection(
            int handIndex,
            SelectionTargetKind targetKind,
            int requiredTargets,
            IReadOnlyList<SelectionTargetRef> candidates)
        {
            if (targetKind == SelectionTargetKind.None)
            {
                throw new ArgumentException("Explicit target selection requires a target kind.",
                    nameof(targetKind));
            }

            if (requiredTargets < 1)
            {
                throw new ArgumentException("Explicit target selection requires at least one target.",
                    nameof(requiredTargets));
            }

            EndSelectionVisuals();
            _machine.SelectCard(handIndex, targetKind, requiredTargets);
            _targetKind = targetKind;
            _visualHandIndex = handIndex;
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    _validTargets.Add(candidates[i]);
                }
            }

            _hand.SetHeld(handIndex, true);
            _hand.SetHoverSuppressed(true);
            _dimLayer.SetActive(true);
            RefreshTargetVisuals();
            _arrow.Show(SelectedCardScreen(), MouseScreen());
        }

        public void OnTargetClicked(SelectionTargetRef target)
        {
            if (!SelectionActive
                || target.Kind != _targetKind
                || !_validTargets.Contains(target))
            {
                return;
            }

            var result = _machine.ClickTarget(target);
            RefreshTargetVisuals();
            TryDispatch(result);
        }

        public void CancelSelection()
        {
            _machine.Cancel();
            EndSelectionVisuals();
        }

        private void OnConfirmClicked()
        {
            TryDispatch(_machine.Confirm());
        }

        private void OnPlacementPreviewClicked()
        {
            TryDispatch(_machine.ClickApplyArea());
        }

        private void TryDispatch(SelectionResult result)
        {
            if (!result.IsComplete)
            {
                return;
            }

            bool applied = _tryApply != null && _tryApply(result);
            if (applied)
            {
                _machine.CommitSucceeded();
                EndSelectionVisuals();
                _onApplied?.Invoke();
                return;
            }

            if (_machine.RequiredTargets <= 0)
            {
                CancelSelection();
                return;
            }

            ReloadValidTargetsAfterRejection();
            _machine.RejectCompletion(_validTargets);
            RefreshTargetVisuals();
            if (_validTargets.Count < _machine.RequiredTargets)
            {
                CancelSelection();
            }
        }

        private void ReloadValidTargetsAfterRejection()
        {
            _validTargets.Clear();
            var targets = _currentTargets?.Invoke(_targetKind);
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                _validTargets.Add(targets[i]);
            }
        }

        private void RefreshTargetVisuals()
        {
            bool active = _machine.Phase == SelectionPhase.PickSingleTarget
                || _machine.Phase == SelectionPhase.PickMultipleTargets
                || _machine.Phase == SelectionPhase.ReadyToConfirm;
            _hand.SetTargetSelection(_visualHandIndex, active);
            _rail.SetTargetSelection(active, _validTargets, _machine.PickedTargets);
            foreach (var pair in _unitTargets)
            {
                pair.Value.SetTargetSelection(
                    active, _validTargets.Contains(pair.Key), IsPicked(pair.Key));
            }

            _confirmButton.gameObject.SetActive(
                _machine.RequiredTargets >= 2
                && _machine.Phase == SelectionPhase.ReadyToConfirm);
        }

        private bool IsPicked(SelectionTargetRef target)
        {
            for (int i = 0; i < _machine.PickedTargets.Count; i++)
            {
                if (_machine.PickedTargets[i].Equals(target))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 SelectedCardScreen()
        {
            return _hand.TryGetCardScreenPoint(_visualHandIndex, out var screenPoint)
                ? screenPoint
                : Vector2.zero;
        }

        private void Update()
        {
            if (_machine.Phase == SelectionPhase.PickSingleTarget
                || _machine.Phase == SelectionPhase.PickMultipleTargets
                || _machine.Phase == SelectionPhase.ReadyToConfirm)
            {
                _arrow.Track(SelectedCardScreen(), MouseScreen());
            }
        }

        private void EndSelectionVisuals()
        {
            _hand.SetTargetSelection(-1, false);
            _hand.SetSelection(-1, CardView.SelectionKind.None);
            _hand.SetHoverSuppressed(false);
            _rail.SetDropHint(false);
            _rail.ClearPlacementPreview();
            _rail.SetTargetSelection(false, _validTargets, _machine.PickedTargets);
            foreach (var view in _unitTargets.Values)
            {
                view.SetTargetSelection(false, false, false);
            }

            _dimLayer.SetActive(false);
            _confirmButton.gameObject.SetActive(false);
            _arrow.Hide();
            _hand.SetHeld(_visualHandIndex, false);
            _visualHandIndex = -1;
            _hoverHandIndex = -1;

            _validTargets.Clear();
            _targetKind = SelectionTargetKind.None;
        }

        private static Vector2 MouseScreen()
        {
            var mouse = Mouse.current;
            return mouse != null ? (Vector2)mouse.position.ReadValue() : Vector2.zero;
        }

    }
}
