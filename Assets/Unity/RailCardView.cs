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
        [SerializeField] private Image _lockIcon;
        [SerializeField] private GameObject _ownerChip;
        [SerializeField] private Image _ownerChipBackground;
        [SerializeField] private TMP_Text _ownerChipText;
        [SerializeField] private GameObject _targetDim;
        [SerializeField] private GameObject _targetOrderBadge;
        [SerializeField] private TMP_Text _targetOrderText;
        [SerializeField] private Button _button;

        private static readonly Color ExecutionFrame = new Color(0.55f, 0.42f, 0.22f, 1f);
        private static readonly Color InterventionFrame = new Color(0.24f, 0.45f, 0.55f, 1f);
        private static readonly Color EnemyTint = new Color(0.45f, 0.18f, 0.18f, 1f);
        private static readonly Color PlayerTint = new Color(0.22f, 0.28f, 0.36f, 1f);
        private static readonly Color OutlineNone = new Color(0f, 0f, 0f, 0f);
        private static readonly Color OutlinePrimary = new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color OutlineSecondary = new Color(0.35f, 0.75f, 0.95f, 1f);

        private Action<bool> _onHover;
        private bool _inputEnabled = true;

        public void Bind(CardPresentation data, Action onClick, Action<bool> onHover)
        {
            _onHover = onHover;
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

        public void SetSelection(CardView.SelectionKind kind)
        {
            _selectionOutline.color =
                kind == CardView.SelectionKind.Primary ? OutlinePrimary :
                kind == CardView.SelectionKind.Secondary ? OutlineSecondary :
                OutlineNone;
        }

        public void SetTargetSelection(bool active, bool candidate, int selectionOrder)
        {
            _targetDim.SetActive(active && !candidate);
            SetSelection(active && candidate
                ? CardView.SelectionKind.Primary
                : CardView.SelectionKind.None);
            _targetOrderBadge.SetActive(selectionOrder > 0);
            _targetOrderText.text = selectionOrder > 0
                ? selectionOrder.ToString()
                : string.Empty;
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

        /// <summary>Editor-only prefab authoring hook used by BattleSceneBuilder.</summary>
        public static RailCardView EditorCreate(RectTransform parent, Vector2 size)
        {
            var root = BattleUiKit.Rect(parent, "RailCard");
            root.sizeDelta = size;

            var view = root.gameObject.AddComponent<RailCardView>();

            var selection = BattleUiKit.Image(root, "Selection", OutlineNone);
            var selectionRect = selection.rectTransform;
            BattleUiKit.Stretch(selectionRect);
            selectionRect.offsetMin = new Vector2(-4f, -4f);
            selectionRect.offsetMax = new Vector2(4f, 4f);
            selection.raycastTarget = false;

            var frame = BattleUiKit.Image(root, "Frame", ExecutionFrame);
            BattleUiKit.Stretch(frame.rectTransform);

            var artFallback = BattleUiKit.Image(root, "ArtFallback", PlayerTint);
            BattleUiKit.Stretch(artFallback.rectTransform);
            artFallback.rectTransform.offsetMin = new Vector2(5f, 5f);
            artFallback.rectTransform.offsetMax = new Vector2(-5f, -5f);
            artFallback.raycastTarget = false;

            var art = BattleUiKit.Image(root, "Art", Color.white);
            BattleUiKit.Stretch(art.rectTransform);
            art.rectTransform.offsetMin = new Vector2(5f, 5f);
            art.rectTransform.offsetMax = new Vector2(-5f, -5f);
            art.raycastTarget = false;

            var badge = BattleUiKit.Image(root, "OrderBadge", new Color(0.12f, 0.12f, 0.16f, 0.92f));
            var badgeRect = badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, 2f);
            badgeRect.sizeDelta = new Vector2(32f, 24f);
            badge.raycastTarget = false;

            var orderText = BattleUiKit.Text(badgeRect, "Order", 16f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(orderText.rectTransform);

            var lockIcon = BattleUiKit.Image(root, "LockIcon", Color.white);
            var lockRect = lockIcon.rectTransform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0f, 1f);
            lockRect.anchoredPosition = new Vector2(14f, -14f);
            lockRect.sizeDelta = new Vector2(20f, 20f);
            lockIcon.sprite = PlaytestCardArt.StatusIconSprite(CardStatusIcon.Lock);
            lockIcon.preserveAspect = true;
            lockIcon.raycastTarget = false;
            lockIcon.gameObject.SetActive(false);

            var ownerChip = BattleUiKit.Rect(root, "OwnerChip");
            ownerChip.anchorMin = ownerChip.anchorMax = new Vector2(0f, 0f);
            ownerChip.pivot = new Vector2(0f, 0f);
            ownerChip.anchoredPosition = new Vector2(6f, 6f);
            ownerChip.sizeDelta = new Vector2(70f, 18f);
            var ownerBackground = BattleUiKit.Image(ownerChip, "Background", PlayerTint);
            BattleUiKit.Stretch(ownerBackground.rectTransform);
            ownerBackground.raycastTarget = false;
            var ownerText = BattleUiKit.Text(ownerChip, "Label", 10f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(ownerText.rectTransform);
            ownerText.raycastTarget = false;
            ownerChip.gameObject.SetActive(false);

            var targetDim = BattleUiKit.Image(root, "TargetDim", new Color(0f, 0f, 0f, 0.55f));
            BattleUiKit.Stretch(targetDim.rectTransform);
            targetDim.raycastTarget = false;

            var targetOrderBadge = BattleUiKit.Image(
                root, "TargetOrderBadge", new Color(0.95f, 0.72f, 0.25f, 1f));
            var targetOrderBadgeRect = targetOrderBadge.rectTransform;
            targetOrderBadgeRect.anchorMin = targetOrderBadgeRect.anchorMax = new Vector2(1f, 1f);
            targetOrderBadgeRect.anchoredPosition = new Vector2(-16f, -14f);
            targetOrderBadgeRect.sizeDelta = new Vector2(28f, 22f);
            targetOrderBadge.raycastTarget = false;

            var targetOrderText = BattleUiKit.Text(
                targetOrderBadgeRect, "Order", 14f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(targetOrderText.rectTransform);
            targetOrderText.color = new Color(0.12f, 0.12f, 0.16f, 1f);

            // Click/hover land on the frame graphic; the handlers live on this root (uGUI bubbles up).
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;

            view._frame = frame;
            view._art = art;
            view._artFallback = artFallback;
            view._orderText = orderText;
            view._selectionOutline = selection;
            view._lockIcon = lockIcon;
            view._ownerChip = ownerChip.gameObject;
            view._ownerChipBackground = ownerBackground;
            view._ownerChipText = ownerText;
            view._targetDim = targetDim.gameObject;
            view._targetOrderBadge = targetOrderBadge.gameObject;
            view._targetOrderText = targetOrderText;
            view._button = button;
            targetDim.gameObject.SetActive(false);
            targetOrderBadge.gameObject.SetActive(false);
            return view;
        }
    }
}
