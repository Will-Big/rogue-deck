using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>
    /// Prefab-authored full card frame. Bind changes content and state only; all
    /// coordinates, badge overflow, and category-specific regions live in prefabs.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public enum SelectionKind
        {
            None,
            Primary,
            Secondary
        }

        [SerializeField] private CardCategory _prefabCategory;
        [SerializeField] private Image _art;
        [SerializeField] private Image _artFallback;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _executionOrderText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private RectTransform _descriptionContent;
        [SerializeField] private RectTransform _targetContent;
        [SerializeField] private RectTransform _targetPanel;
        [SerializeField] private RectTransform _executionOrderBadge;
        [SerializeField] private Outline _selectionOutline;
        [SerializeField] private GameObject _ownerChip;
        [SerializeField] private Image _ownerChipBackground;
        [SerializeField] private TMP_Text _ownerChipText;
        [SerializeField] private GameObject _lockBadge;
        [SerializeField] private Button _button;
        [SerializeField] private CardBackView _backFace;
        [SerializeField] private TargetGlyphView _targetGlyphPrefab;
        [SerializeField] private DescriptionLineView _descriptionLinePrefab;

        private static readonly Color OutlinePrimary =
            new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color OutlineSecondary =
            new Color(0.35f, 0.75f, 0.95f, 1f);
        private static readonly Color EnemyTint =
            new Color(0.45f, 0.18f, 0.18f, 1f);
        private static readonly Color PlayerTint =
            new Color(0.22f, 0.28f, 0.36f, 1f);

        public CardCategory PrefabCategory => _prefabCategory;

        public void Configure(CardPrefabCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            _targetGlyphPrefab = catalog.TargetGlyphPrefab;
            _descriptionLinePrefab = catalog.DescriptionLinePrefab;
        }

        public void Bind(CardPresentation data, Action onClick)
        {
            if (data.Category != _prefabCategory)
            {
                throw new InvalidOperationException(
                    $"Cannot bind {data.Category} card '{data.Id}' "
                    + $"to {_prefabCategory} prefab '{name}'.");
            }

            _nameText.text = data.DisplayName;
            if (_executionOrderText != null)
            {
                _executionOrderText.text = data.ExecutionOrder.ToString();
            }

            if (_costText != null)
            {
                _costText.text = data.EnergyCost.ToString();
            }

            BindTargetEntries(data);
            BindDescriptionLines(data);
            BindArt(data);
            RefreshStatusIcons(data.StatusIcons);
            RefreshOwnerChip(data);
            BindBackFace(data);
            BindButton(onClick);
            SetSelection(SelectionKind.None);
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
            if (_selectionOutline == null)
            {
                return;
            }

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

        public void ShowBackFace(bool value)
        {
            if (_backFace != null)
            {
                _backFace.gameObject.SetActive(value);
            }
        }

        private void BindTargetEntries(CardPresentation data)
        {
            if (_prefabCategory == CardCategory.Intervention)
            {
                return;
            }

            if (_targetPanel == null || _targetContent == null)
            {
                throw new InvalidOperationException(
                    "Execution card prefab is missing its target panel.");
            }

            if (_targetGlyphPrefab == null)
            {
                throw new InvalidOperationException(
                    "CardView is not configured with a target glyph prefab.");
            }

            ClearGeneratedChildren(_targetContent);
            var entries = data.DescriptionLayout.TargetEntries;
            if (entries.Count == 0)
            {
                CreateTargetGlyph(null);
                return;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                CreateTargetGlyph(entries[index]);
            }
        }

        private void CreateTargetGlyph(CardTargetKey? key)
        {
            var glyph = Instantiate(_targetGlyphPrefab, _targetContent);
            glyph.Bind(key);
        }

        private void BindDescriptionLines(CardPresentation data)
        {
            if (_descriptionContent == null)
            {
                throw new InvalidOperationException(
                    "Card prefab is missing its description content.");
            }

            if (_descriptionLinePrefab == null)
            {
                throw new InvalidOperationException(
                    "CardView is not configured with a description line prefab.");
            }

            ClearGeneratedChildren(_descriptionContent);
            var lines = data.DescriptionLayout.Lines;
            for (int index = 0; index < lines.Count; index++)
            {
                var line = Instantiate(_descriptionLinePrefab, _descriptionContent);
                line.Bind(lines[index]);
            }
        }

        private void BindArt(CardPresentation data)
        {
            if (data.Art != null)
            {
                _art.enabled = true;
                _art.sprite = data.Art;
                _art.preserveAspect = true;
                _artFallback.enabled = false;
                return;
            }

            _art.enabled = false;
            _artFallback.enabled = true;
            _artFallback.color =
                data.Side == Side.Enemy ? EnemyTint : PlayerTint;
        }

        private void BindBackFace(CardPresentation data)
        {
            if (_backFace == null)
            {
                return;
            }

            _backFace.Bind(
                data.Art,
                data.Side == Side.Enemy ? EnemyTint : PlayerTint);
            _backFace.gameObject.SetActive(false);
        }

        private void BindButton(Action onClick)
        {
            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                _button.onClick.AddListener(() => onClick());
            }
        }

        private void RefreshOwnerChip(CardPresentation data)
        {
            if (_ownerChip == null)
            {
                return;
            }

            bool visible =
                data.Side == Side.Player
                && !string.IsNullOrEmpty(data.OwnerDisplayName);
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

            for (int index = 0; index < icons.Count; index++)
            {
                var iconObject = index == 0
                    ? _lockBadge
                    : Instantiate(_lockBadge, statusRoot);
                iconObject.SetActive(true);
                ConfigureStatusIcon(iconObject, icons[index]);
            }
        }

        private static void ClearGeneratedChildren(RectTransform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                {
                    child.transform.SetParent(null, false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static void ClearGeneratedStatusIcons(RectTransform statusRoot)
        {
            for (int index = statusRoot.childCount - 1; index >= 1; index--)
            {
                var child = statusRoot.GetChild(index).gameObject;
                child.SetActive(false);
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

        private static void ConfigureStatusIcon(
            GameObject iconObject,
            CardStatusIcon icon)
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
    }
}
