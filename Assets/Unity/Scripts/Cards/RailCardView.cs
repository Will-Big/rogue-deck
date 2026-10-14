using System;
using FateWeaver.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Compact execution-rail card: category frame + art + top-center execution-order badge.
    /// No rules text — the rail is too small for it (spec §3); hovering raises a callback so the rail
    /// shows the full CardView preview instead.</summary>
    public sealed class RailCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Image _art;
        [SerializeField] private Image _artFallback;
        [SerializeField] private TMP_Text _orderText;
        [SerializeField] private Image _selectionOutline;
        [SerializeField] private Image _executionOutline;

        [Header("아웃라인 색")]
        [Tooltip("대상 선택의 첫 대상.")]
        [SerializeField] private Color _primaryOutlineColor = new Color(0.95f, 0.72f, 0.25f, 1f);

        [Tooltip("대상 선택의 두 번째 대상.")]
        [SerializeField] private Color _secondaryOutlineColor = new Color(0.35f, 0.75f, 0.95f, 1f);

        [Tooltip("재생 중 지금 실행되고 있는 카드. 두께는 ExecutionOutline 자식의 RectTransform에서 조절한다.")]
        [SerializeField] private Color _executingOutlineColor = new Color(1f, 0.95f, 0.62f, 1f);
        [SerializeField] private Image _lockIcon;
        [SerializeField] private GameObject _ownerChip;
        [SerializeField] private Image _ownerChipBackground;
        [SerializeField] private TMP_Text _ownerChipText;
        [SerializeField] private GameObject _targetDim;
        [SerializeField] private Button _button;

        private static readonly Color ExecutionFrame = new Color(0.55f, 0.42f, 0.22f, 1f);
        private static readonly Color InterventionFrame = new Color(0.24f, 0.45f, 0.55f, 1f);
        private static readonly Color EnemyTint = new Color(0.45f, 0.18f, 0.18f, 1f);
        private static readonly Color PlayerTint = new Color(0.22f, 0.28f, 0.36f, 1f);
        /// <summary>아웃라인이 꺼진 상태. 투명은 튜닝 값이 아니라 "없음"의 표현이라 상수로 둔다.</summary>
        private static readonly Color OutlineNone = new Color(0f, 0f, 0f, 0f);

        private CardPresentation _textSource;
        private GameLocalization _localization;
        private void RefreshLanguage()
        {
            var data = _textSource.Relocalized();
            if (_ownerChipText != null && !string.IsNullOrEmpty(data.OwnerDisplayName)) _ownerChipText.text = data.OwnerDisplayName;
        }
        private void OnDestroy() { if (_localization != null) _localization.Changed -= RefreshLanguage; }
        private Action<bool> _onHover;
        private bool _inputEnabled = true;

        /// <summary>이 뷰가 그리고 있는 카드 인스턴스의 식별자. 재생 계층이 CardResolved의
        /// InstanceId로 레일의 어느 카드인지 찾는 데 쓴다.</summary>
        public int InstanceId { get; private set; } = -1;

        public void Bind(CardPresentation data, Action onClick, Action<bool> onHover)
        {
            if (_localization != null) _localization.Changed -= RefreshLanguage;
            data = data.Relocalized(); _textSource = data; _localization = data.TextSource;
            if (_localization != null) _localization.Changed += RefreshLanguage;
            _onHover = onHover;
            InstanceId = data.InstanceId;
            SetExecuting(false);
            _frame.color = data.Category == CardCategory.Intervention ? InterventionFrame : ExecutionFrame;
            _orderText.text = data.ExecutionOrder.ToString();

            if (data.Art != null)
            {
                _art.enabled = true;
                _art.sprite = data.Art;
                _art.preserveAspect = true;
                _artFallback.enabled = false;
            }
            else
            {
                _art.enabled = false;
                _artFallback.enabled = true;
                _artFallback.color = data.Side == Side.Enemy ? EnemyTint : PlayerTint;
            }

            _lockIcon.gameObject.SetActive(data.IsLocked);
            bool showOwner = data.Side == Side.Player && !string.IsNullOrEmpty(data.OwnerDisplayName);
            _ownerChip.SetActive(showOwner);
            if (showOwner)
            {
                _ownerChipBackground.color = data.OwnerColor;
                _ownerChipText.text = data.OwnerDisplayName;
            }
            _button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                _button.onClick.AddListener(() => onClick());
            }

            SetSelection(CardView.SelectionKind.None);
        }

        public void SetInteractable(bool value)
        {
            _inputEnabled = value;
            _button.interactable = value;
        }

        /// <summary>이 카드가 지금 실행 중인지. 대상 선택 아웃라인과 겹치지 않도록 별도 그래픽을
        /// 쓴다 — 선택은 플레이어의 조작, 실행은 재생의 진행이라 동시에 보일 수 있다.</summary>
        public void SetExecuting(bool executing)
        {
            if (_executionOutline != null)
            {
                _executionOutline.color = executing ? _executingOutlineColor : OutlineNone;
            }
        }

        public void SetSelection(CardView.SelectionKind kind)
        {
            _selectionOutline.color =
                kind == CardView.SelectionKind.Primary ? _primaryOutlineColor :
                kind == CardView.SelectionKind.Secondary ? _secondaryOutlineColor :
                OutlineNone;
        }

        public void SetTargetSelection(bool active, bool candidate, bool selected)
        {
            _targetDim.SetActive(active && !candidate);
            SetSelection(active && candidate
                ? selected ? CardView.SelectionKind.Secondary : CardView.SelectionKind.Primary
                : CardView.SelectionKind.None);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_inputEnabled)
            {
                _onHover?.Invoke(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_inputEnabled)
            {
                _onHover?.Invoke(false);
            }
        }
    }
}
