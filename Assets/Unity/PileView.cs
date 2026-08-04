using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>A deck-pile button (draw / discard / full deck — spec §2) that opens a scrollable
    /// popup listing the pile's cards as full CardViews. Contents come from a provider delegate so
    /// the popup always reflects the current session state.</summary>
    public sealed class PileView : MonoBehaviour
    {
        [SerializeField] private string _title;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private GameObject _popup;
        [SerializeField] private RectTransform _popupContent;
        [SerializeField] private CardPrefabCatalog _cardPrefabs;
        [SerializeField] private Button _button;
        [SerializeField] private Button _closeButton;

        private Func<IReadOnlyList<CardPresentation>> _cards;
        private readonly List<CardView> _spawned = new List<CardView>();

        private void Awake()
        {
            _button.onClick.AddListener(Open);
            _closeButton.onClick.AddListener(Close);
        }

        public void Bind(Func<IReadOnlyList<CardPresentation>> cards)
        {
            _cards = cards;
        }

        public void SetCount(int count)
        {
            _labelText.text = _title + "\n" + count;
        }

        public void SetInputEnabled(bool value)
        {
            _button.interactable = value;
            if (!value && _popup.activeSelf)
            {
                Close();
            }
        }

        private void Open()
        {
            if (_cards == null)
            {
                return;
            }

            Clear();
            foreach (var data in _cards())
            {
                var view = _cardPrefabs.Create(data, _popupContent);
                view.Bind(data, null);
                _spawned.Add(view);
            }

            _popup.SetActive(true);
        }

        public void Close()
        {
            Clear();
            _popup.SetActive(false);
        }

        private void Clear()
        {
            foreach (var view in _spawned)
            {
                Destroy(view.gameObject);
            }

            _spawned.Clear();
        }

        /// <summary>Editor-time construction: the pile button under <paramref name="parent"/> and its
        /// popup under <paramref name="popupLayer"/> (a full-screen overlay above everything).</summary>
        public static PileView Create(
            RectTransform parent,
            RectTransform popupLayer,
            string title,
            CardPrefabCatalog catalog,
            Vector2 buttonSize)
        {
            var root = BattleUiKit.Rect(parent, "Pile_" + title);
            root.sizeDelta = buttonSize;

            var view = root.gameObject.AddComponent<PileView>();

            var background = BattleUiKit.Image(root, "Background", new Color(0.16f, 0.2f, 0.3f, 0.92f));
            BattleUiKit.Stretch(background.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            var label = BattleUiKit.Text(root, "Label", 16f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(label.rectTransform);
            label.text = title;

            var popup = BattleUiKit.Rect(popupLayer, "Popup_" + title);
            BattleUiKit.Stretch(popup);

            var dim = BattleUiKit.Image(popup, "Dim", new Color(0f, 0f, 0f, 0.75f));
            BattleUiKit.Stretch(dim.rectTransform);
            var closeButton = dim.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = dim;

            var titleText = BattleUiKit.Text(popup, "Title", 28f, TextAlignmentOptions.Center);
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(400f, 40f);
            titleText.text = title;

            var scrollArea = BattleUiKit.Rect(popup, "Scroll");
            BattleUiKit.Anchor(scrollArea, 0.08f, 0.08f, 0.92f, 0.88f);
            var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();

            var viewport = BattleUiKit.Rect(scrollArea, "Viewport");
            BattleUiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

            var content = BattleUiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170f, 238f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            popup.gameObject.SetActive(false);

            view._title = title;
            view._labelText = label;
            view._popup = popup.gameObject;
            view._popupContent = content;
            view._cardPrefabs = catalog;
            view._button = button;
            view._closeButton = closeButton;
            return view;
        }
    }
}
