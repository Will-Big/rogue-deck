using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>The execution rail: a horizontally scrollable strip of RailCardViews in resolution order
    /// (spec §2 — the rail can hold many cards). Hovering a card shows the full CardView preview on the
    /// overlay layer since mini cards carry no rules text (spec §3).</summary>
    public sealed class ExecutionRailView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private CardView _previewPrefab;
        [SerializeField] private RailCardView _cardPrefab;
        [SerializeField] private RectTransform _previewLayer;
        [SerializeField] private Image _backdrop;
        [SerializeField] private Button _railClickButton;

        private static readonly Vector2 CardSize = new Vector2(96f, 132f);
        private static readonly Vector2 PreviewSize = new Vector2(200f, 280f);
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color DropHintColor = new Color(0.95f, 0.72f, 0.25f, 0.14f);

        private readonly List<RailCardView> _views = new List<RailCardView>();
        private CardView _preview;
        private Action _onRailClicked;

        private void Awake()
        {
            if (_railClickButton != null)
            {
                _railClickButton.onClick.AddListener(() => _onRailClicked?.Invoke());
            }
        }

        public void SetRailClicked(Action onRailClicked)
        {
            _onRailClicked = onRailClicked;
        }

        public void SetDropHint(bool value)
        {
            if (_backdrop != null)
            {
                _backdrop.color = value ? DropHintColor : BackdropColor;
            }
        }

        public void SetPickedTargets(IReadOnlyList<int> picked)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                bool isPicked = false;
                if (picked != null)
                {
                    for (int p = 0; p < picked.Count; p++)
                    {
                        if (picked[p] == i)
                        {
                            isPicked = true;
                            break;
                        }
                    }
                }

                _views[i].SetSelection(
                    isPicked ? CardView.SelectionKind.Secondary : CardView.SelectionKind.None);
            }
        }

        /// <summary>Editor-time construction (called by BattleSceneBuilder); the built children and
        /// references serialize into the scene.</summary>
        public void EditorBuild(CardView previewPrefab, RailCardView cardPrefab, RectTransform previewLayer)
        {
            _previewPrefab = previewPrefab;
            _cardPrefab = cardPrefab;
            _previewLayer = previewLayer;

            var rect = (RectTransform)transform;
            _scrollRect = gameObject.AddComponent<ScrollRect>();

            var viewport = BattleUiKit.Rect(rect, "Viewport");
            BattleUiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _backdrop = viewport.gameObject.AddComponent<Image>();
            _backdrop.color = BackdropColor;
            _railClickButton = viewport.gameObject.AddComponent<Button>();
            _railClickButton.targetGraphic = _backdrop;
            _railClickButton.transition = Selectable.Transition.None;

            var content = BattleUiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.viewport = viewport;
            _scrollRect.content = content;
            _scrollRect.horizontal = true;
            _scrollRect.vertical = false;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 30f;

            _content = content;
        }

        public void SetCards(IReadOnlyList<CardPresentation> cards, Action<int> onClick)
        {
            HidePreview();
            foreach (var view in _views)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                var view = Instantiate(_cardPrefab, _content);
                ((RectTransform)view.transform).sizeDelta = CardSize;
                int captured = i;
                var data = cards[i];
                view.Bind(data, () => onClick?.Invoke(captured), hovering => OnHover(view, data, hovering));
                _views.Add(view);
            }
        }

        public void SetSelection(int index, CardView.SelectionKind kind)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].SetSelection(i == index ? kind : CardView.SelectionKind.None);
            }
        }

        public void SetInputEnabled(bool value)
        {
            if (_scrollRect != null)
            {
                _scrollRect.enabled = value;
            }

            if (!value)
            {
                HidePreview();
            }

            foreach (var view in _views)
            {
                view.SetInteractable(value);
            }
        }

        private void OnHover(RailCardView source, CardPresentation data, bool hovering)
        {
            if (!hovering)
            {
                HidePreview();
                return;
            }

            if (_previewPrefab == null || _previewLayer == null)
            {
                return;
            }

            if (_preview == null)
            {
                _preview = Instantiate(_previewPrefab, _previewLayer);
                var previewRect = (RectTransform)_preview.transform;
                previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
                previewRect.sizeDelta = PreviewSize;
                foreach (var graphic in _preview.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }

            _preview.gameObject.SetActive(true);
            _preview.Bind(data, null);

            var screen = RectTransformUtility.WorldToScreenPoint(null, source.transform.position);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewLayer, screen, null, out local);
            local.y += CardSize.y * 0.5f + PreviewSize.y * 0.5f + 14f;
            float maxX = _previewLayer.rect.width * 0.5f - PreviewSize.x * 0.5f - 8f;
            local.x = Mathf.Clamp(local.x, -maxX, maxX);
            ((RectTransform)_preview.transform).anchoredPosition = local;
        }

        private void HidePreview()
        {
            if (_preview != null)
            {
                _preview.gameObject.SetActive(false);
            }
        }
    }
}
