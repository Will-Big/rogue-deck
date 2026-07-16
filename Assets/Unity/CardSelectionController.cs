using System;
using System.Collections;
using FateWeaver.Simulation.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Owns the visual flow for execution placement and execution-rail intervention targets.
    /// Party-member targeting remains in BattleScreenController.</summary>
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
        private Action<SelectionCommand> _onCommand;
        private CardView _floatingCard;
        private CardView _emphasisCard;
        private Coroutine _emphasis;
        private int _visualHandIndex = -1;

        public bool SelectionActive => _machine.Phase != SelectionPhase.Idle;

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        public void Initialize(Action<SelectionCommand> onCommand)
        {
            _onCommand = onCommand;
        }

        public void BeginSelection(int handIndex, int requiredTargets, CardPresentation card)
        {
            EndSelectionVisuals();
            _machine.SelectCard(handIndex, requiredTargets);
            _visualHandIndex = handIndex;
            _hand.SetHoverSuppressed(true);

            if (_machine.Phase == SelectionPhase.ConfirmPlacement)
            {
                _rail.SetDropHint(true);
                _hand.SetGhost(handIndex, true);
                SpawnFloatingCard(card);
            }
            else if (_machine.Phase == SelectionPhase.PickSingleTarget)
            {
                _hand.SetHeld(handIndex, true);
                _arrow.Show(MouseScreen());
            }
            else
            {
                _dimLayer.SetActive(true);
            }
        }

        public bool OnZoneClicked(int zoneIndex, CardPresentation zoneCard)
        {
            if (!SelectionActive)
            {
                return false;
            }

            if (_machine.Phase == SelectionPhase.ConfirmPlacement)
            {
                Dispatch(_machine.ClickApplyArea());
                return true;
            }

            int previousCount = _machine.PickedTargets.Count;
            var command = _machine.ClickTarget(zoneIndex);
            if (_machine.Phase == SelectionPhase.PickMultipleTargets
                || _machine.Phase == SelectionPhase.ReadyToConfirm)
            {
                _rail.SetPickedTargets(_machine.PickedTargets);
                if (_machine.PickedTargets.Count > previousCount)
                {
                    PlayCenterEmphasis(zoneCard);
                }

                _confirmButton.gameObject.SetActive(_machine.Phase == SelectionPhase.ReadyToConfirm);
            }

            Dispatch(command);
            return true;
        }

        public void OnRailAreaClicked()
        {
            if (SelectionActive)
            {
                Dispatch(_machine.ClickApplyArea());
            }
        }

        public void CancelSelection()
        {
            _machine.Cancel();
            EndSelectionVisuals();
        }

        private void OnConfirmClicked()
        {
            Dispatch(_machine.Confirm());
        }

        private void Dispatch(SelectionCommand command)
        {
            if (!command.PlayExecution && !command.PlayIntervention)
            {
                return;
            }

            EndSelectionVisuals();
            _onCommand?.Invoke(command);
        }

        private void Update()
        {
            if (_machine.Phase == SelectionPhase.ConfirmPlacement && _floatingCard != null)
            {
                MoveToScreen((RectTransform)_floatingCard.transform, MouseScreen());
            }
            else if (_machine.Phase == SelectionPhase.PickSingleTarget)
            {
                _arrow.Track(MouseScreen());
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
            _hand.SetHoverSuppressed(false);
            _rail.SetDropHint(false);
            _rail.SetPickedTargets(null);
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
