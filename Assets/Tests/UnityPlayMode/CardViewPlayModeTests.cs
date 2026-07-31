using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityPlayMode
{
    public class CardViewPlayModeTests
    {
        [UnityTest]
        public IEnumerator Repeated_bind_replaces_generated_hierarchy_immediately()
        {
            var fixture = CreateFixture();
            try
            {
                Assert.IsTrue(Application.isPlaying);
                fixture.View.Bind(
                    Presentation(
                        new[]
                        {
                            new CardTargetKey(
                                CardTargetFaction.Ally,
                                CardTargetRange.Self),
                            new CardTargetKey(
                                CardTargetFaction.Enemy,
                                CardTargetRange.FrontOne)
                        },
                        new[]
                        {
                            new CardDescriptionLine(null, "First line."),
                            new CardDescriptionLine(null, "Second line.")
                        }),
                    null);
                var staleTargets = Children(fixture.TargetContent);
                var staleLines = Children(fixture.DescriptionContent);
                Assert.AreEqual(2, staleTargets.Length);
                Assert.AreEqual(2, staleLines.Length);

                fixture.View.Bind(
                    Presentation(
                        new[]
                        {
                            new CardTargetKey(
                                CardTargetFaction.Enemy,
                                CardTargetRange.BackOne)
                        },
                        new[] { new CardDescriptionLine(null, "Replacement.") }),
                    null);

                Assert.AreEqual(1, fixture.TargetContent.childCount);
                Assert.AreEqual(1, fixture.DescriptionContent.childCount);
                Assert.IsTrue(staleTargets.All(child => child.parent == null));
                Assert.IsTrue(staleLines.All(child => child.parent == null));
                Assert.IsTrue(staleTargets.All(child => !child.gameObject.activeSelf));
                Assert.IsTrue(staleLines.All(child => !child.gameObject.activeSelf));

                yield return null;

                Assert.IsTrue(staleTargets.All(child => child == null));
                Assert.IsTrue(staleLines.All(child => child == null));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static Fixture CreateFixture()
        {
            var root = new GameObject(
                "CardViewPlayModeFixture",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline),
                typeof(CardView));
            var view = root.GetComponent<CardView>();
            var targetContent = ChildRect(root.transform, "TargetContent");
            var descriptionContent =
                ChildRect(root.transform, "DescriptionContent");
            var targetTemplateObject = new GameObject(
                "TargetGlyphTemplate",
                typeof(RectTransform),
                typeof(TargetGlyphView));
            var targetTemplate =
                targetTemplateObject.GetComponent<TargetGlyphView>();
            var lineTemplateObject = new GameObject(
                "DescriptionLineTemplate",
                typeof(RectTransform),
                typeof(DescriptionLineView));
            var lineTemplate =
                lineTemplateObject.GetComponent<DescriptionLineView>();
            var glyphSlot = ChildRect(
                lineTemplateObject.transform,
                "GlyphSlot");
            var lineGlyphObject = new GameObject(
                "LineGlyph",
                typeof(RectTransform),
                typeof(TargetGlyphView));
            lineGlyphObject.transform.SetParent(glyphSlot, false);
            var lineText = ChildText(
                lineTemplateObject.transform,
                "LineText");
            SetField(lineTemplate, "_glyphSlot", glyphSlot);
            SetField(
                lineTemplate,
                "_glyph",
                lineGlyphObject.GetComponent<TargetGlyphView>());
            SetField(lineTemplate, "_text", lineText);

            SetField(view, "_prefabCategory", CardCategory.Execution);
            SetField(view, "_art", ChildImage(root.transform, "Art"));
            SetField(
                view,
                "_artFallback",
                ChildImage(root.transform, "ArtFallback"));
            SetField(view, "_nameText", ChildText(root.transform, "Name"));
            SetField(
                view,
                "_executionOrderText",
                ChildText(root.transform, "ExecutionOrder"));
            SetField(view, "_costText", ChildText(root.transform, "Cost"));
            SetField(view, "_descriptionContent", descriptionContent);
            SetField(view, "_targetContent", targetContent);
            SetField(view, "_targetPanel", targetContent);
            SetField(view, "_selectionOutline", root.GetComponent<Outline>());
            SetField(view, "_button", root.GetComponent<Button>());
            SetField(view, "_targetGlyphPrefab", targetTemplate);
            SetField(view, "_descriptionLinePrefab", lineTemplate);

            return new Fixture(
                view,
                targetContent,
                descriptionContent,
                targetTemplateObject,
                lineTemplateObject);
        }

        private static CardPresentation Presentation(
            CardTargetKey[] targetEntries,
            CardDescriptionLine[] lines)
            => new CardPresentation(
                "execution",
                "execution",
                3,
                1,
                Side.Player,
                new CardDescriptionLayout(targetEntries, lines, "plain"),
                null,
                false);

        private static Transform[] Children(Transform parent)
            => Enumerable.Range(0, parent.childCount)
                .Select(parent.GetChild)
                .ToArray();

        private static RectTransform ChildRect(
            Transform parent,
            string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static Image ChildImage(Transform parent, string name)
        {
            var child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Image>();
        }

        private static TMP_Text ChildText(Transform parent, string name)
        {
            var child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            return child.GetComponent<TMP_Text>();
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private sealed class Fixture
        {
            private readonly GameObject _targetTemplate;
            private readonly GameObject _lineTemplate;

            public Fixture(
                CardView view,
                RectTransform targetContent,
                RectTransform descriptionContent,
                GameObject targetTemplate,
                GameObject lineTemplate)
            {
                View = view;
                TargetContent = targetContent;
                DescriptionContent = descriptionContent;
                _targetTemplate = targetTemplate;
                _lineTemplate = lineTemplate;
            }

            public CardView View { get; }
            public RectTransform TargetContent { get; }
            public RectTransform DescriptionContent { get; }

            public void Destroy()
            {
                Object.DestroyImmediate(View.gameObject);
                Object.DestroyImmediate(_targetTemplate);
                Object.DestroyImmediate(_lineTemplate);
            }
        }
    }
}
