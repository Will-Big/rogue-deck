using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>One card widget: art (or side-tinted fallback) + name/executionOrder + description block,
    /// a selection outline and a lock badge. Bound from a CardPresentation; clicking raises onClick.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public enum SelectionKind { None, Primary, Secondary }

        private const float BaseWidth = 200f;
        private const float BaseHeight = 280f;
        private const float BaseAspect = BaseWidth / BaseHeight;
        private const float MinCardHeight = 180f;

        [SerializeField] private Image _art;
        [SerializeField] private Image _artFallback;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _executionOrderText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Outline _selectionOutline;
        [SerializeField] private GameObject _ownerChip;
        [SerializeField] private Image _ownerChipBackground;
        [SerializeField] private TMP_Text _ownerChipText;
        // Template for card status icons. The prefab places its parent row as the root's last child so icons draw above art.
        [SerializeField] private GameObject _lockBadge;
        [SerializeField] private Button _button;
        [SerializeField] private RectTransform _backFacePrefab;

        private static readonly Color OutlinePrimary = new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color OutlineSecondary = new Color(0.35f, 0.75f, 0.95f, 1f);
        private static readonly Color EnemyTint = new Color(0.45f, 0.18f, 0.18f, 1f);
        private static readonly Color PlayerTint = new Color(0.22f, 0.28f, 0.36f, 1f);

        private RectTransform _rect;
        private LayoutElement _layoutElement;
        private HorizontalLayoutGroup _parentLayout;
        private float _lastPreferredWidth = -1f;
        private float _lastPreferredHeight = -1f;

        private void Awake()
        {
            CacheLayoutReferences();
            ApplyResponsiveLayout();
        }

        private void OnEnable()
        {
            CacheLayoutReferences();
            ApplyResponsiveLayout();
        }

        private void LateUpdate()
        {
            ApplyResponsiveLayout();
        }

        public void Bind(CardPresentation data, Action onClick)
        {
            _nameText.text = data.DisplayName;
            _executionOrderText.text = data.ExecutionOrder.ToString();
            if (_costText != null)
            {
                _costText.text = data.EnergyCost.ToString();
            }

            _descriptionText.text = data.Description;

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

            RefreshStatusIcons(data.StatusIcons);
            RefreshOwnerChip(data);

            _button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                _button.onClick.AddListener(() => onClick());
            }

            SetSelection(SelectionKind.None);
            ApplyResponsiveLayout();
        }

        public void SetInteractable(bool value)
        {
            if (_button != null)
            {
                _button.interactable = value;
            }
        }

        public void SetSelection(SelectionKind kind)
        {
            if (kind == SelectionKind.None)
            {
                _selectionOutline.enabled = false;
                return;
            }

            _selectionOutline.effectColor = kind == SelectionKind.Primary
                ? OutlinePrimary
                : OutlineSecondary;
            _selectionOutline.enabled = true;
        }

        /// <summary>Instantiates the serialized card back prefab as a hidden child scaled to
        /// cover this card. The placement flight activates it when the flip passes edge-on.</summary>
        public RectTransform CreateBackFace()
        {
            if (_backFacePrefab == null)
            {
                return null;
            }

            var rect = (RectTransform)transform;
            var back = Instantiate(_backFacePrefab, rect);
            back.anchorMin = back.anchorMax = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = Vector2.zero;
            back.localScale = new Vector3(
                rect.rect.width / back.rect.width,
                rect.rect.height / back.rect.height,
                1f);
            back.gameObject.SetActive(false);
            return back;
        }

        private void RefreshStatusIcons(IReadOnlyList<CardStatusIcon> icons)
        {
            if (_lockBadge == null)
            {
                return;
            }

            var statusRoot = _lockBadge.transform.parent as RectTransform;
            if (statusRoot == null)
            {
                return;
            }

            ClearGeneratedStatusIcons(statusRoot);
            bool hasIcons = icons != null && icons.Count > 0;
            statusRoot.gameObject.SetActive(hasIcons);
            _lockBadge.SetActive(false);
            if (!hasIcons)
            {
                return;
            }

            for (int i = 0; i < icons.Count; i++)
            {
                var iconObject = i == 0
                    ? _lockBadge
                    : Instantiate(_lockBadge, statusRoot);
                iconObject.SetActive(true);
                ConfigureStatusIcon(iconObject, icons[i]);
            }
        }

        private static void ClearGeneratedStatusIcons(RectTransform statusRoot)
        {
            for (int i = statusRoot.childCount - 1; i >= 1; i--)
            {
                var child = statusRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static void ConfigureStatusIcon(GameObject iconObject, CardStatusIcon icon)
        {
            var image = iconObject.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = PlaytestCardArt.StatusIconSprite(icon);

            if (image.sprite != null)
            {
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
        }

        private void CacheLayoutReferences()
        {
            if (_rect == null)
            {
                _rect = transform as RectTransform;
            }

            if (_layoutElement == null)
            {
                _layoutElement = GetComponent<LayoutElement>();
            }

            var parent = transform.parent;
            _parentLayout = parent != null ? parent.GetComponent<HorizontalLayoutGroup>() : null;
        }

        private void ApplyResponsiveLayout()
        {
            CacheLayoutReferences();
            UpdatePreferredCardSize();

            if (_rect == null)
            {
                return;
            }

            float width = _rect.rect.width > 0f ? _rect.rect.width : BaseWidth;
            float height = _rect.rect.height > 0f ? _rect.rect.height : BaseHeight;
            float scale = Mathf.Min(width / BaseWidth, height / BaseHeight);
            if (scale <= 0f)
            {
                return;
            }

            LayoutTopStretch(_art != null ? _art.rectTransform : null, 23f, 27f, 23f, 125f, scale);
            LayoutTopStretch(_artFallback != null ? _artFallback.rectTransform : null, 23f, 27f, 23f, 125f, scale);
            LayoutTopStretch(ParentRect(_nameText), 23f, 144.5f, 23f, 31f, scale);
            LayoutTopStretch(TextRect(_nameText), 14f, 3.5f, 14f, 24f, scale);
            LayoutTopStretch(TextRect(_descriptionText), 23f, 175.5f, 23f, 82.4f, scale);

            LayoutTopLeft(ParentRect(_costText), 29f, 29f, 48f, 48f, scale);
            LayoutTopRight(ParentRect(_executionOrderText), 25f, 27f, 40f, 40f, scale);
            LayoutTopLeft(TextRect(_costText), 23f, 22f, 34f, 32f, scale);
            LayoutTopRight(TextRect(_executionOrderText), 20f, 19f, 32f, 28f, scale);
            LayoutStatusRow(scale);
            LayoutOwnerChip(scale);
            ScaleText(scale);
        }

        private void UpdatePreferredCardSize()
        {
            if (_layoutElement == null || _rect == null)
            {
                return;
            }

            var parentRect = _rect.parent as RectTransform;
            float preferredHeight = BaseHeight;
            if (parentRect != null && parentRect.rect.width > 0f)
            {
                int siblingCount = ActiveLayoutSiblingCount(parentRect);
                float spacing = _parentLayout != null ? _parentLayout.spacing : 0f;
                int left = _parentLayout != null ? _parentLayout.padding.left : 0;
                int right = _parentLayout != null ? _parentLayout.padding.right : 0;
                float availableWidth = parentRect.rect.width - left - right - spacing * Mathf.Max(0, siblingCount - 1);
                float widthLimitedHeight = availableWidth > 0f && siblingCount > 0
                    ? (availableWidth / siblingCount) / BaseAspect
                    : BaseHeight;
                preferredHeight = Mathf.Min(BaseHeight, widthLimitedHeight);
            }

            preferredHeight = Mathf.Max(MinCardHeight, preferredHeight);
            float preferredWidth = preferredHeight * BaseAspect;
            if (Mathf.Abs(_lastPreferredWidth - preferredWidth) < 0.5f
                && Mathf.Abs(_lastPreferredHeight - preferredHeight) < 0.5f)
            {
                return;
            }

            _lastPreferredWidth = preferredWidth;
            _lastPreferredHeight = preferredHeight;
            _layoutElement.preferredWidth = preferredWidth;
            _layoutElement.preferredHeight = preferredHeight;
            _layoutElement.flexibleWidth = 0f;
            _layoutElement.flexibleHeight = 0f;

            if (parentRect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
            }
        }

        private static int ActiveLayoutSiblingCount(RectTransform parent)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.gameObject.activeInHierarchy && child.GetComponent<CardView>() != null)
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        private static RectTransform TextRect(TMP_Text text)
            => text != null ? text.rectTransform : null;

        private static RectTransform ParentRect(Component component)
            => component != null ? component.transform.parent as RectTransform : null;

        private static void LayoutTopStretch(RectTransform rect, float left, float top, float right, float height, float scale)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f * scale, -top * scale);
            rect.sizeDelta = new Vector2(-(left + right) * scale, height * scale);
        }

        private static void LayoutTopLeft(RectTransform rect, float centerX, float centerY, float width, float height, float scale)
        {
            LayoutFixed(rect, new Vector2(0f, 1f), new Vector2(centerX * scale, -centerY * scale), width, height, scale);
        }

        private static void LayoutTopRight(RectTransform rect, float centerXFromRight, float centerY, float width, float height, float scale)
        {
            LayoutFixed(rect, new Vector2(1f, 1f), new Vector2(-centerXFromRight * scale, -centerY * scale), width, height, scale);
        }

        private static void LayoutFixed(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, float width, float height, float scale)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(width * scale, height * scale);
        }

        private void LayoutStatusRow(float scale)
        {
            var statusRoot = _lockBadge != null ? _lockBadge.transform.parent as RectTransform : null;
            if (statusRoot == null)
            {
                return;
            }

            statusRoot.anchorMin = new Vector2(1f, 0f);
            statusRoot.anchorMax = new Vector2(1f, 0f);
            statusRoot.pivot = new Vector2(1f, 1f);
            statusRoot.anchoredPosition = new Vector2(-20f * scale, 119.5f * scale);
            statusRoot.sizeDelta = new Vector2(120f * scale, 42f * scale);

            var layout = statusRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 6f * scale;
            }
        }

        private void RefreshOwnerChip(CardPresentation data)
        {
            if (_ownerChip == null)
            {
                return;
            }

            bool visible = data.Side == Side.Player && !string.IsNullOrEmpty(data.OwnerDisplayName);
            _ownerChip.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (_ownerChipText != null)
            {
                _ownerChipText.text = data.OwnerDisplayName;
            }

            if (_ownerChipBackground != null)
            {
                _ownerChipBackground.color = data.OwnerColor;
            }
        }

        private void LayoutOwnerChip(float scale)
        {
            var rect = _ownerChip != null ? _ownerChip.transform as RectTransform : null;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(18f * scale, 17f * scale);
            rect.sizeDelta = new Vector2(84f * scale, 24f * scale);
        }

        private void ScaleText(float scale)
        {
            SetFontSize(_nameText, 13f, scale);
            SetFontSize(_descriptionText, 11f, scale);
            SetFontSize(_executionOrderText, 12f, scale);
            SetFontSize(_costText, 20f, scale);
        }

        private static void SetFontSize(TMP_Text text, float baseSize, float scale)
        {
            if (text != null)
            {
                text.fontSize = Mathf.Max(8f, baseSize * scale);
            }
        }
    }
}
