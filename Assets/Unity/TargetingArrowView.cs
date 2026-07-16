using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Serialized overlay arrow whose head follows the pointer during single-target selection.</summary>
    public sealed class TargetingArrowView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _shaft;
        [SerializeField] private RectTransform _head;

        private static readonly Color ArrowColor = new Color(0.95f, 0.72f, 0.25f, 0.9f);
        private Vector2 _startLocal;

        /// <summary>Editor-scene construction hook used only by BattleSceneBuilder.</summary>
        public static TargetingArrowView EditorCreate(RectTransform overlay)
        {
            var root = BattleUiKit.Rect(overlay, "TargetingArrow");
            BattleUiKit.Stretch(root);
            var view = root.gameObject.AddComponent<TargetingArrowView>();
            view._root = root;

            var shaft = BattleUiKit.Image(root, "Shaft", ArrowColor);
            var shaftRect = shaft.rectTransform;
            shaftRect.anchorMin = shaftRect.anchorMax = new Vector2(0.5f, 0.5f);
            shaftRect.pivot = new Vector2(0f, 0.5f);
            shaftRect.sizeDelta = new Vector2(0f, 6f);
            shaft.raycastTarget = false;
            view._shaft = shaftRect;

            var head = BattleUiKit.Image(root, "Head", ArrowColor);
            var headRect = head.rectTransform;
            headRect.anchorMin = headRect.anchorMax = new Vector2(0.5f, 0.5f);
            headRect.sizeDelta = new Vector2(18f, 18f);
            head.raycastTarget = false;
            view._head = headRect;

            root.gameObject.SetActive(false);
            return view;
        }

        public void Show(Vector2 startScreen)
        {
            _startLocal = ToLocal(startScreen);
            gameObject.SetActive(true);
            Track(startScreen);
        }

        public void Track(Vector2 currentScreen)
        {
            var current = ToLocal(currentScreen);
            var delta = current - _startLocal;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _shaft.anchoredPosition = _startLocal;
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
