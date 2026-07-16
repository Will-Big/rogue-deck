using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private RectTransform _overlay;
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private TargetingArrowView _arrow;

        private const float FloatingScale = 1.25f;
        private const float FloatingLift = 30f;
        private const float EmphasisHoldSeconds = 0.55f;
        private const float EmphasisGrowSeconds = 0.12f;

        private readonly CardSelectionMachine _machine = new CardSelectionMachine();
        private readonly HashSet<SelectionTargetRef> _validTargets =
            new HashSet<SelectionTargetRef>();
        private readonly Dictionary<SelectionTargetRef, UnitView> _unitTargets =
            new Dictionary<SelectionTargetRef, UnitView>();
        private Func<SelectionResult, bool> _tryApply;
        private Func<SelectionTargetKind, IReadOnlyList<SelectionTargetRef>> _currentTargets;
        private Action _onApplied;
        private CardView _floatingCard;
        private CardView _emphasisCard;
        private Coroutine _emphasis;
        private int _visualHandIndex = -1;
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

        public void BeginPlacement(int handIndex, CardPresentation card)
        {
            EndSelectionVisuals();
            _machine.SelectCard(handIndex, SelectionTargetKind.None, 0);
            _visualHandIndex = handIndex;
            _hand.SetHoverSuppressed(true);
            _rail.SetDropHint(true);
            _hand.SetGhost(handIndex, true);
            SpawnFloatingCard(card);
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

            _hand.SetHoverSuppressed(true);
            _hand.SetHeld(handIndex, true);
            _dimLayer.SetActive(true);
            RefreshTargetVisuals();
            _arrow.Show(SelectedCardScreen(), MouseScreen());
        }

        public void OnTargetClicked(SelectionTargetRef target, CardPresentation? card)
        {
            if (!SelectionActive
                || target.Kind != _targetKind
                || !_validTargets.Contains(target))
            {
                return;
            }

            int previousCount = _machine.PickedTargets.Count;
            var result = _machine.ClickTarget(target);
            RefreshTargetVisuals();
            if (_machine.PickedTargets.Count > previousCount && card.HasValue)
            {
                PlayCenterEmphasis(card.Value);
            }

            TryDispatch(result);
        }

        public void OnRailAreaClicked()
        {
            if (_machine.Phase == SelectionPhase.ConfirmPlacement)
            {
                TryDispatch(_machine.ClickApplyArea());
            }
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
            if (_machine.Phase == SelectionPhase.ConfirmPlacement && _floatingCard != null)
            {
                MoveToScreen((RectTransform)_floatingCard.transform, MouseScreen());
            }
            else if (_machine.Phase == SelectionPhase.PickSingleTarget
                || _machine.Phase == SelectionPhase.PickMultipleTargets
                || _machine.Phase == SelectionPhase.ReadyToConfirm)
            {
                _arrow.Track(SelectedCardScreen(), MouseScreen());
            }
        }

        private void SpawnFloatingCard(CardPresentation card)
        {
            if (_floatingCard == null)
            {
                _floatingCard = Instantiate(_cardPrefab, _overlay);
                var rect = (RectTransform)_floatingCard.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(170f, 238f);
                rect.localScale = Vector3.one * FloatingScale;
                rect.localRotation = Quaternion.identity;
                DisableRaycasts(_floatingCard);
            }

            _floatingCard.gameObject.SetActive(true);
            _floatingCard.Bind(card, null);
            MoveToScreen((RectTransform)_floatingCard.transform, MouseScreen());
        }

        private void PlayCenterEmphasis(CardPresentation card)
        {
            if (_emphasis != null)
            {
                StopCoroutine(_emphasis);
            }

            _emphasis = StartCoroutine(CenterEmphasis(card));
        }

        private IEnumerator CenterEmphasis(CardPresentation card)
        {
            if (_emphasisCard == null)
            {
                _emphasisCard = Instantiate(_cardPrefab, _overlay);
                var rect = (RectTransform)_emphasisCard.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(200f, 280f);
                DisableRaycasts(_emphasisCard);
            }

            _emphasisCard.gameObject.SetActive(true);
            _emphasisCard.Bind(card, null);
            var rectTransform = (RectTransform)_emphasisCard.transform;
            float elapsed = 0f;
            while (elapsed < EmphasisGrowSeconds)
            {
                elapsed += Time.deltaTime;
                rectTransform.localScale = Vector3.one
                    * Mathf.Lerp(0.6f, 1f, elapsed / EmphasisGrowSeconds);
                yield return null;
            }

            rectTransform.localScale = Vector3.one;
            yield return new WaitForSeconds(EmphasisHoldSeconds);
            _emphasisCard.gameObject.SetActive(false);
            _emphasis = null;
        }

        private void EndSelectionVisuals()
        {
            _hand.SetTargetSelection(-1, false);
            _hand.SetHoverSuppressed(false);
            _rail.SetDropHint(false);
            _rail.SetTargetSelection(false, _validTargets, _machine.PickedTargets);
            foreach (var view in _unitTargets.Values)
            {
                view.SetTargetSelection(false, false, false);
            }

            _dimLayer.SetActive(false);
            _confirmButton.gameObject.SetActive(false);
            _arrow.Hide();
            _hand.SetGhost(_visualHandIndex, false);
            _hand.SetHeld(_visualHandIndex, false);
            _visualHandIndex = -1;

            if (_floatingCard != null)
            {
                _floatingCard.gameObject.SetActive(false);
            }

            if (_emphasis != null)
            {
                StopCoroutine(_emphasis);
                _emphasis = null;
            }

            if (_emphasisCard != null)
            {
                _emphasisCard.gameObject.SetActive(false);
            }

            _validTargets.Clear();
            _targetKind = SelectionTargetKind.None;
        }

        private static void DisableRaycasts(CardView card)
        {
            foreach (var graphic in card.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static Vector2 MouseScreen()
        {
            var mouse = Mouse.current;
            return mouse != null ? (Vector2)mouse.position.ReadValue() : Vector2.zero;
        }

        private void MoveToScreen(RectTransform rect, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, null, out var local);
            rect.anchoredPosition = local + new Vector2(0f, FloatingLift);
        }
    }
}
