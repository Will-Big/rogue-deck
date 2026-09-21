using System.Collections.Generic;
using System.Linq;
using FateWeaver.Simulation.Presentation;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Authors and applies hand geometry independently of card gameplay indices.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HandFanLayoutView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;

        [Header("Fan Layout")]
        [SerializeField, Min(0f)] private float _radius = 800f;
        [SerializeField, Range(0f, 180f)] private float _totalAngle = 32f;
        [SerializeField] private bool _rotateWithArc = true;
        [SerializeField] private bool _invertRotation;

        [Header("Ordering / Spacing")]
        [Tooltip("Capture sibling order when cards are bound. Hover drawing order never changes slots.")]
        [SerializeField] private bool _useSiblingOrder = true;
        [SerializeField, Range(-30f, 30f)] private float _perItemExtraAngle;

        [Header("Adaptive Fan")]
        [SerializeField] private bool _adaptiveSpread = true;
        [SerializeField, Min(2)] private int _cardsForFullSpread = 5;
        [SerializeField, Range(0f, 180f)] private float _minAngle;

        [Header("Positioning")]
        [SerializeField] private bool _useBottomBaseline = true;
        [Tooltip("Distance from the hand area's bottom edge to the lowest card, in canvas units.")]
        [SerializeField, Min(0f)] private float _baselinePadding = 16f;
        [Tooltip("Total horizontal and vertical safety margins used for fitting the hand.")]
        [SerializeField] private Vector2 _safeMargins = new Vector2(32f, 16f);

        [Header("Card Scale")]
        [SerializeField] private bool _controlCardScale = true;
        [SerializeField, Min(.01f)] private float _cardScale = .64f;

        [Header("Animation")]
        [SerializeField] private bool _smooth = true;
        [Tooltip("Larger values settle faster. Transition duration is 1 / speed seconds.")]
        [SerializeField, Min(.01f)] private float _smoothSpeed = 12f;

        private readonly List<HandCardHoverEffect> _cards = new List<HandCardHoverEffect>();
        private readonly List<HandCardHoverEffect> _siblingOrder = new List<HandCardHoverEffect>();
        private bool _refreshPending;
        private bool _applying;

        public void EditorBuild(RectTransform content) => _content = content;

        public void Bind(IReadOnlyList<HandCardHoverEffect> cards)
        {
            _cards.Clear();
            _cards.AddRange(cards);
            _siblingOrder.Clear();
            _siblingOrder.AddRange(cards.OrderBy(card => card.transform.GetSiblingIndex()));
            Refresh(false);
        }

        [ContextMenu("Refresh Hand Layout")]
        public void Refresh() => Refresh(true);

        private void Refresh(bool animate)
        {
            if (_applying || _content == null) return;
            _refreshPending = false;
            _applying = true;
            try
            {
                var order = _useSiblingOrder ? _siblingOrder : _cards;
                var activeOrder = order.Where(card => card != null && card.IsActive)
                    .OrderBy(card => card.transform.GetSiblingIndex()).ToArray();
                var poses = new FanPose[order.Count];
                var scales = new Vector3[order.Count];
                var bounds = new Bounds();
                bool hasBounds = false;
                for (int i = 0; i < order.Count; i++)
                {
                    var card = order[i];
                    if (card == null) continue;
                    poses[i] = ArcHandLayout.PoseFor(i, order.Count, _radius, _totalAngle,
                        _rotateWithArc, _invertRotation, _adaptiveSpread, _cardsForFullSpread,
                        _minAngle, _perItemExtraAngle);
                    scales[i] = _controlCardScale
                        ? Vector3.one * Mathf.Max(.01f, _cardScale)
                        : card.AuthoredScale;
                    EncapsulateCard(ref bounds, ref hasBounds, card, poses[i], scales[i]);
                }

                FitContent(bounds, hasBounds);
                for (int i = 0; i < order.Count; i++)
                {
                    if (order[i] == null) continue;
                    var pose = poses[i];
                    order[i].UpdateBaseline(new Vector2(pose.XOffset, pose.YOffset),
                        Quaternion.Euler(0f, 0f, pose.AngleDegrees), i, scales[i],
                        _smooth, _smoothSpeed, !animate);
                }
                NormalizeSiblingOrder(activeOrder);
            }
            finally { _applying = false; }
        }

        private void FitContent(Bounds bounds, bool hasBounds)
        {
            var root = (RectTransform)transform;
            float width = Mathf.Max(0f, root.rect.width - Mathf.Max(0f, _safeMargins.x));
            float marginY = Mathf.Max(0f, _safeMargins.y) * .5f;
            float padding = Mathf.Clamp(_baselinePadding, marginY,
                Mathf.Max(marginY, root.rect.height - marginY));
            float height = Mathf.Max(0f, root.rect.height
                - (_useBottomBaseline ? padding + marginY : marginY * 2f));
            float requiredHeight = _useBottomBaseline ? bounds.size.y
                : 2f * Mathf.Max(Mathf.Abs(bounds.min.y), Mathf.Abs(bounds.max.y));
            float scale = !hasBounds ? 1f : Mathf.Min(1f,
                width / Mathf.Max(.001f, bounds.size.x),
                height / Mathf.Max(.001f, requiredHeight));
            // A small positive scale keeps pointer coordinate conversion well-defined in collapsed UI.
            scale = Mathf.Max(.0001f, scale);
            _content.localScale = Vector3.one * scale;
            _content.anchoredPosition = new Vector2(
                hasBounds ? -bounds.center.x * scale : 0f,
                _useBottomBaseline && hasBounds
                    ? -root.rect.height * .5f + padding - bounds.min.y * scale : 0f);
        }

        private static void EncapsulateCard(ref Bounds bounds, ref bool hasBounds,
            HandCardHoverEffect card, FanPose pose, Vector3 scale)
        {
            var rect = (RectTransform)card.transform;
            // Include badge overflow, not just the frame. Relative-to-self removes hover transforms.
            var local = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, rect);
            var rotation = Quaternion.Euler(0f, 0f, pose.AngleDegrees);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            {
                var corner = new Vector3(x == 0 ? local.min.x : local.max.x,
                    y == 0 ? local.min.y : local.max.y, 0f);
                var point = rotation * Vector3.Scale(corner, scale)
                    + new Vector3(pose.XOffset, pose.YOffset, 0f);
                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(point);
            }
        }

        public void NormalizeSiblingOrder()
        {
            var activeOrder = _cards.Where(card => card != null && card.IsActive)
                .OrderBy(card => card.transform.GetSiblingIndex()).ToArray();
            NormalizeSiblingOrder(activeOrder);
        }

        private void NormalizeSiblingOrder(IReadOnlyList<HandCardHoverEffect> activeOrder)
        {
            var order = _useSiblingOrder ? _siblingOrder : _cards;
            for (int i = 0; i < order.Count; i++)
                if (order[i] != null) order[i].transform.SetSiblingIndex(i);
            foreach (var card in activeOrder) card.ReapplyActiveSiblingOrder();
        }

        private void OnRectTransformDimensionsChange() => Refresh(false);
        private void OnValidate() => _refreshPending = true;
        private void OnEnable() => _refreshPending = true;
        private void LateUpdate()
        {
            if (_refreshPending) Refresh();
        }
    }
}
