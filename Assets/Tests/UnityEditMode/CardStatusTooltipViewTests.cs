using System;
using System.Collections.Generic;
using System.Reflection;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public sealed class CardStatusTooltipViewTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private Texture2D _texture;
        private Sprite _sprite;

        [TearDown]
        public void TearDown()
        {
            for (var index = _objects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
            if (_sprite != null)
            {
                Object.DestroyImmediate(_sprite);
            }

            if (_texture != null)
            {
                Object.DestroyImmediate(_texture);
            }
        }

        [Test]
        public void Status_display_content_rejects_missing_json_projection_fields()
        {
            Assert.Throws<ArgumentException>(() => new CardStatusDisplayContent(
                "", "잠금", "설명", "lock"));
            Assert.Throws<ArgumentException>(() => new CardStatusDisplayContent(
                "lock", "", "설명", "lock"));
            Assert.Throws<ArgumentException>(() => new CardStatusDisplayContent(
                "lock", "잠금", "", "lock"));
            Assert.Throws<ArgumentException>(() => new CardStatusDisplayContent(
                "lock", "잠금", "설명", ""));
        }

        [Test]
        public void Hover_shows_bound_title_and_description_without_field_labels()
        {
            var tooltip = BuildTooltip(out var title, out var description);
            var icon = BuildIcon();
            var presentation = new CardStatusPresentation(
                "lock",
                StatusSprite(),
                "잠금",
                "이 카드는 실행 순서를 변경할 수 없습니다.");
            icon.Bind(presentation, tooltip);

            icon.OnPointerEnter(PointerAt(new Vector2(120f, 80f)));

            Assert.IsTrue(tooltip.gameObject.activeSelf);
            Assert.AreEqual("잠금", title.text);
            Assert.AreEqual(
                "이 카드는 실행 순서를 변경할 수 없습니다.",
                description.text);
            Assert.AreSame(
                presentation.Icon,
                icon.GetComponent<Image>().sprite);
        }

        [Test]
        public void Older_icon_exit_cannot_hide_newer_icon_tooltip()
        {
            var tooltip = BuildTooltip(out var title, out var description);
            var first = BuildIcon("First");
            var second = BuildIcon("Second");
            first.Bind(new CardStatusPresentation(
                "lock", StatusSprite(), "잠금", "첫 설명"), tooltip);
            second.Bind(new CardStatusPresentation(
                "poison", StatusSprite(), "독", "둘째 설명"), tooltip);

            first.OnPointerEnter(PointerAt(Vector2.zero));
            second.OnPointerEnter(PointerAt(Vector2.one));
            first.OnPointerExit(PointerAt(Vector2.zero));

            Assert.IsTrue(tooltip.gameObject.activeSelf);
            Assert.AreEqual("독", title.text);
            Assert.AreEqual("둘째 설명", description.text);

            second.OnPointerExit(PointerAt(Vector2.one));
            Assert.IsFalse(tooltip.gameObject.activeSelf);
        }

        [Test]
        public void Disabling_bound_icon_hides_its_tooltip()
        {
            var tooltip = BuildTooltip(out _, out _);
            var icon = BuildIcon();
            icon.Bind(new CardStatusPresentation(
                "lock", StatusSprite(), "잠금", "설명"), tooltip);
            icon.OnPointerEnter(PointerAt(Vector2.zero));

            var onDisable = typeof(CardStatusIconView).GetMethod(
                "OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable);
            onDisable.Invoke(icon, null);

            Assert.IsFalse(tooltip.gameObject.activeSelf);
        }

        private CardStatusTooltipView BuildTooltip(
            out TMP_Text title,
            out TMP_Text description)
        {
            var root = Track(new GameObject(
                "CardStatusTooltip",
                typeof(RectTransform),
                typeof(CardStatusTooltipView)));
            title = BuildText(root.transform, "Title");
            description = BuildText(root.transform, "Description");
            var tooltip = root.GetComponent<CardStatusTooltipView>();
            SetField(tooltip, "_titleText", title);
            SetField(tooltip, "_descriptionText", description);
            SetField(tooltip, "_screenOffset", new Vector2(12f, -12f));
            root.SetActive(false);
            return tooltip;
        }

        private CardStatusIconView BuildIcon(string name = "StatusIcon")
        {
            var root = Track(new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(CardStatusIconView)));
            var view = root.GetComponent<CardStatusIconView>();
            SetField(view, "_icon", root.GetComponent<Image>());
            return view;
        }

        private TMP_Text BuildText(Transform parent, string name)
        {
            var textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<TMP_Text>();
        }

        private Sprite StatusSprite()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            _texture = new Texture2D(2, 2);
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            return _sprite;
        }

        private static PointerEventData PointerAt(Vector2 position)
            => new PointerEventData(null) { position = position };

        private GameObject Track(GameObject value)
        {
            _objects.Add(value);
            return value;
        }

        private static void SetField<T>(object target, string name, T value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }
    }
}
