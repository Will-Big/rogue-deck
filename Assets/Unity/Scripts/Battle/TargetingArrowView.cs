using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Serialized overlay arrow whose head follows the pointer during single-target selection.</summary>
    public sealed class TargetingArrowView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _shaft;
        [SerializeField] private RectTransform _head;

        public void Show(Vector2 startScreen, Vector2 currentScreen)
        {
            gameObject.SetActive(true);
            Track(startScreen, currentScreen);
        }

        public void Track(Vector2 startScreen, Vector2 currentScreen)
        {
            var start = ToLocal(startScreen);
            var current = ToLocal(currentScreen);
            var delta = current - start;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _shaft.anchoredPosition = start;
            _shaft.sizeDelta = new Vector2(delta.magnitude, 6f);
            _shaft.localRotation = Quaternion.Euler(0f, 0f, angle);
            _head.anchoredPosition = current;
            _head.localRotation = Quaternion.Euler(0f, 0f, angle + 45f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            return local;
        }
    }
}
