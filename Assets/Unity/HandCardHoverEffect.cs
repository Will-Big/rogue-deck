using UnityEngine;
using UnityEngine.EventSystems;

namespace FateWeaver.Unity
{
    /// <summary>Enlarges a hand card for reading while preserving its authored fan pose.</summary>
    public sealed class HandCardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HoverScale = 1.35f;
        private const float HoverLift = 46f;

        private RectTransform _rect;
        private Vector2 _basePosition;
        private Quaternion _baseRotation;
        private int _baseSiblingIndex;
        private bool _hovering;
        private bool _held;
        private bool _suppressed;

        public void Capture()
        {
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _baseRotation = _rect.localRotation;
            _baseSiblingIndex = _rect.GetSiblingIndex();
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
            if (_suppressed || _held)
            {
                return;
            }

            _hovering = true;
            Enlarge();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            if (!_held)
            {
                Restore();
            }
        }

        private void Enlarge()
        {
            if (_rect == null)
            {
                Capture();
            }

            _rect.SetAsLastSibling();
            _rect.localRotation = Quaternion.identity;
            _rect.anchoredPosition = _basePosition + new Vector2(0f, HoverLift);
            _rect.localScale = Vector3.one * HoverScale;
        }

        private void Restore()
        {
            if (_rect == null)
            {
                return;
            }

            _rect.SetSiblingIndex(_baseSiblingIndex);
            _rect.localRotation = _baseRotation;
            _rect.anchoredPosition = _basePosition;
            _rect.localScale = Vector3.one;
        }
    }
}
