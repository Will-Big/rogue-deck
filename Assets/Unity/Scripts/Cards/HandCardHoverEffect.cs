using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateWeaver.Unity
{
    /// <summary>Moves a hand card between the fan pose and the reading pose its layout reserves.</summary>
    public sealed class HandCardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("A resting hand keeps this rect on screen; the card face below it may leave the hand area.")]
        [SerializeField] private RectTransform _restLine;

        private RectTransform _rect;
        private Vector2 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _activePosition;
        private Vector3 _activeScale = Vector3.one;
        private int _baseSiblingIndex;
        private bool _hovering;
        private bool _held;
        private bool _suppressed;
        private System.Action<bool> _onHover;
        private Vector3 _authoredScale = Vector3.one;
        private bool _initialized;
        private bool _smooth;
        private float _smoothSpeed;
        private Sequence _motion;
        private bool _snapNext;

        internal Vector3 AuthoredScale => _authoredScale;

        internal RectTransform RestLine => _restLine;

        internal bool IsActive => _hovering || _held;

        public void Initialize(System.Action<bool> onHover)
        {
            _onHover = onHover;
            _initialized = true;
        }

        public void Capture()
        {
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _baseRotation = _rect.localRotation;
            _baseScale = _rect.localScale;
            _authoredScale = _baseScale;
            _baseSiblingIndex = _rect.GetSiblingIndex();
            _activePosition = _basePosition;
            _activeScale = _baseScale;
        }

        public void UpdateBaseline(
            Vector2 position,
            Quaternion rotation,
            int siblingIndex, Vector3 scale, Vector2 activePosition, Vector3 activeScale,
            bool smooth, float smoothSpeed, bool immediate = false)
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            _basePosition = position;
            _baseRotation = rotation;
            _baseSiblingIndex = siblingIndex;
            _baseScale = scale;
            _activePosition = activePosition;
            _activeScale = activeScale;
            _smooth = smooth;
            _smoothSpeed = smoothSpeed;
            _snapNext = immediate;
            if (_hovering || _held)
            {
                Enlarge();
            }
            else
            {
                Restore();
            }
        }

        internal void ReapplyActiveSiblingOrder()
        {
            if (_hovering || _held)
            {
                _rect.SetAsLastSibling();
            }
        }

        public void Hold(bool value)
        {
            _held = value;
            if (value)
            {
                Enlarge();
            }
            else if (!_hovering)
            {
                Restore();
            }
        }

        public void SetSuppressed(bool value)
        {
            _suppressed = value;
            if (value && !_held)
            {
                _hovering = false;
                Restore();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_initialized || _suppressed || _held)
            {
                return;
            }

            _hovering = true;
            Enlarge();
            _onHover?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_initialized) return;
            bool wasHovering = _hovering;
            _hovering = false;
            if (!_held)
            {
                Restore();
            }

            if (wasHovering)
            {
                _onHover?.Invoke(false);
            }
        }

        private void Enlarge()
        {
            if (_rect == null)
            {
                Capture();
            }

            _rect.SetAsLastSibling();
            MoveTo(_activePosition, Quaternion.identity, _activeScale);
        }

        private void Restore()
        {
            if (_rect == null)
            {
                return;
            }

            _rect.SetSiblingIndex(_baseSiblingIndex);
            MoveTo(_basePosition, _baseRotation, _baseScale);
        }
        private void MoveTo(Vector2 position, Quaternion rotation, Vector3 scale)
        {
            _motion?.Kill();
            _motion = null;
            bool snap = _snapNext;
            _snapNext = false;
            if (snap || !_smooth || !Application.isPlaying || !isActiveAndEnabled)
            {
                _rect.anchoredPosition = position;
                _rect.localRotation = rotation;
                _rect.localScale = scale;
                return;
            }
            float duration = 1f / Mathf.Max(.01f, _smoothSpeed);
            _motion = DOTween.Sequence()
                .Join(_rect.DOAnchorPos(position, duration))
                .Join(_rect.DOLocalRotateQuaternion(rotation, duration))
                .Join(_rect.DOScale(scale, duration))
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void OnDisable()
        {
            _motion?.Kill();
            _motion = null;
            _hovering = false;
            _held = false;
            if (_initialized && _rect != null)
            {
                // Unity forbids sibling changes while a parent is activating/deactivating.
                _rect.anchoredPosition = _basePosition;
                _rect.localRotation = _baseRotation;
                _rect.localScale = _baseScale;
            }
        }

        private void OnDestroy() => _motion?.Kill();
    }
}
