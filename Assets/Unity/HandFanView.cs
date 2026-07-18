using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Presentation;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>The hand as a slight curved fan (spec §2): full CardViews positioned by HandFanLayout,
    /// no layout group — poses are absolute so cards can tilt. Hover and held presentation
    /// are layered on each prefab instance without changing the underlying card data.</summary>
    public sealed class HandFanView : MonoBehaviour
    {
        [SerializeField] private CardView _cardPrefab;

        private const float Spacing = 150f;
        private const float AnglePerCard = 4f;
        private const float ArcDrop = 10f;
        private static readonly Vector2 CardSize = new Vector2(170f, 238f);

        private readonly List<CardView> _views = new List<CardView>();
        private readonly List<HandCardHoverEffect> _hoverEffects = new List<HandCardHoverEffect>();
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();

        public void EditorBuild(CardView cardPrefab)
        {
            _cardPrefab = cardPrefab;
        }

        public void SetCards(
            IReadOnlyList<CardPresentation> cards,
            Action<int> onClick,
            Action<int, bool> onHover)
        {
            foreach (var view in _views)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
            _hoverEffects.Clear();
            _groups.Clear();
            var root = (RectTransform)transform;
            for (int i = 0; i < cards.Count; i++)
            {
                var view = Instantiate(_cardPrefab, root);
                var rect = (RectTransform)view.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = CardSize;
                var pose = HandFanLayout.PoseFor(i, cards.Count, Spacing, AnglePerCard, ArcDrop);
                rect.anchoredPosition = new Vector2(pose.XOffset, pose.YOffset);
                rect.localRotation = Quaternion.Euler(0f, 0f, pose.AngleDegrees);
                int captured = i;
                view.Bind(cards[i], () => onClick?.Invoke(captured));
                var hover = view.gameObject.AddComponent<HandCardHoverEffect>();
                hover.Capture();
                hover.Initialize(hovering => onHover?.Invoke(captured, hovering));
                _hoverEffects.Add(hover);
                _groups.Add(view.gameObject.AddComponent<CanvasGroup>());
                _views.Add(view);
            }
        }

        public void SetHeld(int index, bool value)
        {
            if (index >= 0 && index < _hoverEffects.Count)
            {
                _hoverEffects[index].Hold(value);
            }
        }

        public bool TryGetCardScreenPoint(int index, out Vector2 screenPoint)
        {
            if (index < 0 || index >= _views.Count)
            {
                screenPoint = Vector2.zero;
                return false;
            }

            screenPoint = RectTransformUtility.WorldToScreenPoint(
                null, _views[index].transform.position);
            return true;
        }

        public void SetTargetSelection(int selectedIndex, bool active)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _groups[i].alpha = !active || i == selectedIndex ? 1f : 0.35f;
                _views[i].SetInteractable(!active);
                _views[i].SetSelection(active && i == selectedIndex
                    ? CardView.SelectionKind.Primary
                    : CardView.SelectionKind.None);
            }
        }

        public void SetHoverSuppressed(bool value)
        {
            foreach (var hover in _hoverEffects)
            {
                hover.SetSuppressed(value);
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
            foreach (var view in _views)
            {
                view.SetInteractable(value);
            }
        }
    }
}
