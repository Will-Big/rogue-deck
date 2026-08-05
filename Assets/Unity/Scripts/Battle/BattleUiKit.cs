using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Factory for the battle screen's code-built uGUI nodes (no prefab authoring).
    /// Public so the editor-assembly scene builder can reuse it. The Korean TMP font is the same
    /// Resources asset PLAYTEST.md pins; a missing font falls back to the TMP default.</summary>
    public static class BattleUiKit
    {
        private static TMP_FontAsset _koreanFont;
        private static bool _fontLookedUp;

        public static TMP_FontAsset KoreanFont()
        {
            if (!_fontLookedUp)
            {
                _koreanFont = Resources.Load<TMP_FontAsset>("Fonts/KoreanTMP");
                _fontLookedUp = true;
            }

            return _koreanFont;
        }

        public static RectTransform Rect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image Image(RectTransform parent, string name, Color color)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TMP_Text Text(RectTransform parent, string name, float fontSize, TextAlignmentOptions align)
        {
            var rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = KoreanFont();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = fontSize;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
