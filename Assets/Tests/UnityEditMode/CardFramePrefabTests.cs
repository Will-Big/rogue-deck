using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardFramePrefabTests
    {
        private const string StatusTooltipPath =
            "Assets/Unity/Prefabs/CardStatusTooltipView.prefab";

        private static readonly string[] GlyphVisualNames =
        {
            "FrontOne",
            "FrontTwo",
            "BackOne",
            "BackTwo",
            "All",
            "Self",
            "Empty"
        };

        [TestCase(CardTargetRange.FrontOne, "FrontOne")]
        [TestCase(CardTargetRange.FrontTwo, "FrontTwo")]
        [TestCase(CardTargetRange.BackOne, "BackOne")]
        [TestCase(CardTargetRange.BackTwo, "BackTwo")]
        [TestCase(CardTargetRange.All, "All")]
        [TestCase(CardTargetRange.Self, "Self")]
        public void Target_glyph_activates_exactly_one_authored_range_visual(
            CardTargetRange range,
            string expectedName)
        {
            var glyph = InstantiateGlyph();
            try
            {
                glyph.Bind(new CardTargetKey(CardTargetFaction.Ally, range));

                AssertActiveVisual(glyph, expectedName);
            }
            finally
            {
                Object.DestroyImmediate(glyph.gameObject);
            }
        }

        [TestCase(CardTargetRange.FrontOne, "FrontOne")]
        [TestCase(CardTargetRange.FrontTwo, "FrontTwo")]
        [TestCase(CardTargetRange.BackOne, "BackOne")]
        [TestCase(CardTargetRange.BackTwo, "BackTwo")]
        [TestCase(CardTargetRange.All, "All")]
        [TestCase(CardTargetRange.Self, "Self")]
        public void Enemy_target_mirrors_the_same_range_visual(
            CardTargetRange range,
            string expectedName)
        {
            var glyph = InstantiateGlyph();
            try
            {
                glyph.Bind(new CardTargetKey(CardTargetFaction.Enemy, range));

                AssertActiveVisual(glyph, expectedName);
                Assert.Less(
                    Child(glyph.transform, expectedName).localScale.x,
                    0f);

                glyph.Bind(new CardTargetKey(CardTargetFaction.Ally, range));
                AssertActiveVisual(glyph, expectedName);
                Assert.Greater(
                    Child(glyph.transform, expectedName).localScale.x,
                    0f);
            }
            finally
            {
                Object.DestroyImmediate(glyph.gameObject);
            }
        }

        [TestCase(CardTargetFaction.Ally, "#5DADE2", 1f)]
        [TestCase(CardTargetFaction.Enemy, "#E85D5D", -1f)]
        public void Target_glyph_uses_color_only_and_points_front_toward_center(
            CardTargetFaction faction,
            string expectedHex,
            float expectedScaleSign)
        {
            var glyph = InstantiateGlyph();
            try
            {
                glyph.Bind(new CardTargetKey(faction, CardTargetRange.FrontOne));
                var visual = Child(glyph.transform, "FrontOne");

                Assert.AreEqual(
                    expectedScaleSign,
                    Mathf.Sign(visual.localScale.x));
                Assert.IsTrue(
                    visual.GetComponentsInChildren<Graphic>(true)
                        .All(graphic =>
                            "#" + ColorUtility.ToHtmlStringRGB(graphic.color)
                            == expectedHex));
                Assert.IsEmpty(glyph.GetComponentsInChildren<Outline>(true));
            }
            finally
            {
                Object.DestroyImmediate(glyph.gameObject);
            }
        }

        [Test]
        public void Positional_target_visuals_have_equal_authored_widths()
        {
            var prefab = Load<TargetGlyphView>(CardPrefabCatalogTests.TargetGlyphPath);
            var widths = new[] { "FrontOne", "FrontTwo", "BackOne", "BackTwo", "All" }
                .Select(name => ActiveGraphicBoundsWidth(Child(prefab.transform, name)))
                .ToArray();

            Assert.That(widths.Max() - widths.Min(), Is.LessThanOrEqualTo(0.5f));
            Assert.That(widths, Has.All.EqualTo(31.2f).Within(0.5f));
        }

        [Test]
        public void Self_and_empty_use_single_neutral_grammars()
        {
            var glyph = InstantiateGlyph();
            try
            {
                glyph.Bind(new CardTargetKey(
                    CardTargetFaction.Ally,
                    CardTargetRange.Self));
                AssertActiveVisual(glyph, "Self");
                var allyStructure = DirectChildNames(Child(glyph.transform, "Self"));
                Assert.IsTrue(Child(glyph.transform, "Self")
                    .GetComponentsInChildren<Graphic>(true)
                    .All(graphic =>
                        ColorUtility.ToHtmlStringRGB(graphic.color) == "5DADE2"));

                glyph.Bind(new CardTargetKey(
                    CardTargetFaction.Enemy,
                    CardTargetRange.Self));
                CollectionAssert.AreEqual(
                    allyStructure,
                    DirectChildNames(Child(glyph.transform, "Self")));
                Assert.IsTrue(Child(glyph.transform, "Self")
                    .GetComponentsInChildren<Graphic>(true)
                    .All(graphic =>
                        ColorUtility.ToHtmlStringRGB(graphic.color) == "E85D5D"));

                glyph.Bind(null);
                AssertActiveVisual(glyph, "Empty");
                Assert.IsTrue(Child(glyph.transform, "Empty")
                    .GetComponentsInChildren<Graphic>(true)
                    .All(graphic =>
                    {
                        var hex = ColorUtility.ToHtmlStringRGB(graphic.color);
                        return hex != "5DADE2" && hex != "E85D5D";
                    }));
            }
            finally
            {
                Object.DestroyImmediate(glyph.gameObject);
            }
        }

        [Test]
        public void Target_glyph_has_separate_faction_markers_and_bracketed_all()
        {
            var prefab = Load<TargetGlyphView>(CardPrefabCatalogTests.TargetGlyphPath);
            Assert.IsNotNull(CardPrefabCatalogTests.Field<Image>(prefab, "_allyMarker"));
            Assert.IsNotNull(CardPrefabCatalogTests.Field<Image>(prefab, "_enemyMarker"));
            var all = Child(prefab.transform, "All");
            CollectionAssert.IsSubsetOf(new[] { "Unit0", "Unit1", "Unit2", "LeftBracket", "RightBracket" }, DirectChildNames(all));
            Assert.IsEmpty(prefab.GetComponentsInChildren<TMP_Text>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Outline>(true));
        }

        [Test]
        public void Target_glyph_declares_only_authored_visual_and_palette_fields()
        {
            var serializedFields = typeof(TargetGlyphView)
                .GetFields(
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly)
                .Where(field => field.GetCustomAttribute<SerializeField>() != null)
                .Select(field => (field.Name, field.FieldType))
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    ("_frontOneVisual", typeof(RectTransform)),
                    ("_frontTwoVisual", typeof(RectTransform)),
                    ("_backOneVisual", typeof(RectTransform)),
                    ("_backTwoVisual", typeof(RectTransform)),
                    ("_allVisual", typeof(RectTransform)),
                    ("_selfVisual", typeof(RectTransform)),
                    ("_emptyVisual", typeof(RectTransform)),
                    ("_allyMarker", typeof(Image)),
                    ("_enemyMarker", typeof(Image)),
                    ("_allyColor", typeof(Color)),
                    ("_enemyColor", typeof(Color))
                },
                serializedFields);
        }

        [Test]
        public void Target_and_description_prefabs_share_the_same_faction_palette()
        {
            var glyph = Load<TargetGlyphView>(CardPrefabCatalogTests.TargetGlyphPath);
            var line = Load<DescriptionLineView>(CardPrefabCatalogTests.DescriptionLinePath);

            Assert.AreEqual(
                CardPrefabCatalogTests.Field<Color>(glyph, "_allyColor"),
                CardPrefabCatalogTests.Field<Color>(line, "_allySymbolColor"));
            Assert.AreEqual(
                CardPrefabCatalogTests.Field<Color>(glyph, "_enemyColor"),
                CardPrefabCatalogTests.Field<Color>(line, "_enemySymbolColor"));
        }

        [TestCase(CardTargetFaction.Ally, "#5DADE2")]
        [TestCase(CardTargetFaction.Enemy, "#E85D5D")]
        public void Description_line_colors_heading_symbol_and_preserves_body(CardTargetFaction faction, string expectedHex)
        {
            var line = InstantiateDescriptionLine();
            try
            {
                line.Bind(new CardDescriptionLine(new CardTargetKey(faction, CardTargetRange.Self), "방어 2."));
                Assert.AreEqual("방어 2.", CardPrefabCatalogTests.Field<TMP_Text>(line, "_text").text);
                string label = faction == CardTargetFaction.Ally ? "●</color> 아군" : "◆</color> 적군";
                Assert.AreEqual("<color=" + expectedHex + ">" + label + " 자신",
                    CardPrefabCatalogTests.Field<TMP_Text>(line, "_headingText").text);
            }
            finally { Object.DestroyImmediate(line.gameObject); }
        }

        [Test]
        public void Description_line_uses_separate_heading_and_full_width_body()
        {
            var prefab = Load<DescriptionLineView>(CardPrefabCatalogTests.DescriptionLinePath);
            var text = CardPrefabCatalogTests.Field<TMP_Text>(prefab, "_text");
            Assert.IsEmpty(prefab.GetComponentsInChildren<TargetGlyphView>(true));
            Assert.AreEqual(2, prefab.GetComponentsInChildren<TMP_Text>(true).Length);
            Assert.IsNotNull(prefab.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(TextWrappingModes.Normal, text.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Overflow, text.overflowMode);
            Assert.IsTrue(text.richText);
            Assert.IsTrue(text.enableAutoSizing);
        }

        [Test]
        public void Description_line_rejects_missing_or_duplicate_range_labels()
        {
            var line = InstantiateDescriptionLine();
            try
            {
                var field = typeof(DescriptionLineView).GetField("_rangeLabels", BindingFlags.NonPublic | BindingFlags.Instance);
                var labels = CardPrefabCatalogTests.Field<DescriptionLineView.RangeLabel[]>(line, "_rangeLabels");
                field.SetValue(line, labels.Take(1).ToArray());
                Assert.Throws<InvalidOperationException>(() => line.Bind(new CardDescriptionLine(null, "피해 8.")));
                field.SetValue(line, labels.Concat(new[] { labels[0] }).ToArray());
                Assert.Throws<InvalidOperationException>(() => line.Bind(new CardDescriptionLine(null, "피해 8.")));
            }
            finally { Object.DestroyImmediate(line.gameObject); }
        }

        [Test]
        public void Description_line_requires_its_text_reference()
        {
            var root = new GameObject("UnconfiguredDescriptionLine");
            try
            {
                var line = root.AddComponent<DescriptionLineView>();

                Assert.Throws<InvalidOperationException>(
                    () => line.Bind(
                        new CardDescriptionLine(null, "카드 1장 뽑기.")));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Description_line_rejects_an_undefined_target_faction()
        {
            var line = InstantiateDescriptionLine();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => line.Bind(new CardDescriptionLine(
                        new CardTargetKey(
                            (CardTargetFaction)999,
                            CardTargetRange.FrontOne),
                        "피해 3.")));
            }
            finally
            {
                Object.DestroyImmediate(line.gameObject);
            }
        }

        [TestCase(CardTargetRange.FrontOne)]
        [TestCase(CardTargetRange.All)]
        [TestCase(CardTargetRange.Self)]
        public void Description_line_prefix_does_not_encode_range(
            CardTargetRange range)
        {
            var line = InstantiateDescriptionLine();
            try
            {
                line.Bind(new CardDescriptionLine(
                    new CardTargetKey(CardTargetFaction.Enemy, range),
                    "피해 3."));
                Assert.AreEqual(
                    "피해 3.",
                    CardPrefabCatalogTests.Field<TMP_Text>(line, "_text").text);

                line.Bind(new CardDescriptionLine(null, "카드 1장 뽑기."));
                Assert.AreEqual(
                    "카드 1장 뽑기.",
                    CardPrefabCatalogTests.Field<TMP_Text>(line, "_text").text);
            }
            finally
            {
                Object.DestroyImmediate(line.gameObject);
            }
        }

        [Test]
        public void Description_line_wraps_to_remaining_width_and_grows_in_a_constrained_parent()
        {
            var parentObject = new GameObject(
                "ConstrainedDescription",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            DescriptionLineView line = null;
            try
            {
                var parent = (RectTransform)parentObject.transform;
                parent.sizeDelta = new Vector2(158f, 200f);
                var parentLayout = parentObject.GetComponent<VerticalLayoutGroup>();
                parentLayout.childControlWidth = true;
                parentLayout.childControlHeight = true;
                parentLayout.childForceExpandWidth = true;
                parentLayout.childForceExpandHeight = false;

                line = Object.Instantiate(
                    Load<DescriptionLineView>(
                        CardPrefabCatalogTests.DescriptionLinePath),
                    parent);
                line.Bind(new CardDescriptionLine(
                    new CardTargetKey(
                        CardTargetFaction.Enemy,
                        CardTargetRange.FrontOne),
                    "A sufficiently long card effect description should wrap "
                    + "across several lines inside the remaining width."));

                line.SetLayout(true, false);
                line.Measure(158f);
                var lineRect = (RectTransform)line.transform;
                var text = CardPrefabCatalogTests.Field<TMP_Text>(line, "_text");
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(lineRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                LayoutRebuilder.ForceRebuildLayoutImmediate(lineRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
                Canvas.ForceUpdateCanvases();

                Assert.That(lineRect.rect.width, Is.EqualTo(158f).Within(0.1f));
                Assert.That(text.rectTransform.rect.width, Is.EqualTo(158f).Within(0.5f));
                Assert.Greater(lineRect.rect.height, 28f);
                Assert.That(
                    lineRect.rect.height,
                    Is.GreaterThanOrEqualTo(text.preferredHeight - 0.5f));
            }
            finally
            {
                if (line != null)
                {
                    Object.DestroyImmediate(line.gameObject);
                }

                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void Empty_glyph_and_cost_badges_share_a_minimal_circle_sprite()
        {
            var glyph = Load<TargetGlyphView>(
                CardPrefabCatalogTests.TargetGlyphPath);
            var circle = Child(Child(glyph.transform, "Empty"), "Circle")
                .GetComponent<Image>().sprite;
            var innerCircle = Child(Child(glyph.transform, "Self"), "Center")
                .GetComponent<Image>().sprite;
            var executionCost = Child(
                LoadExecution().transform,
                "CostBadge").GetComponent<Image>().sprite;
            var interventionCost = Child(
                LoadIntervention().transform,
                "CostBadge").GetComponent<Image>().sprite;

            Assert.IsNotNull(circle);
            Assert.AreSame(circle, innerCircle);
            Assert.AreSame(circle, executionCost);
            Assert.AreSame(circle, interventionCost);
            Assert.That(
                AssetDatabase.GetAssetPath(circle),
                Does.Not.Contain("poster"));

            var texture = circle.texture;
            Assert.AreEqual(texture.width, texture.height);
            var pixels = texture.GetPixels32();
            Assert.AreEqual(0, pixels[0].a);
            Assert.AreEqual(
                255,
                pixels[(texture.height / 2) * texture.width + texture.width / 2].a);
            Assert.IsTrue(
                pixels.Where(pixel => pixel.a > 0)
                    .All(pixel => pixel.r == 255
                                  && pixel.g == 255
                                  && pixel.b == 255),
                "The reusable circle must contain no poster color or ornament.");
        }

        [Test]
        public void Execution_frame_reads_order_tab_cost_then_left_aligned_name()
        {
            var view = LoadExecution();
            var cost = Child(view.transform, "CostBadge");
            var order = Child(view.transform, "ExecutionOrderBadge");
            var nameText = CardPrefabCatalogTests.Field<TMP_Text>(view, "_nameText");
            var name = nameText.rectTransform;
            Assert.IsEmpty(Child(view.transform, "SymbolOnlyTargetPanel").GetComponentsInChildren<TMP_Text>(true));
            AssertBadgeOutsideFrame(view, order);
            var frame = (RectTransform)view.transform;
            var corners = new Vector3[4]; cost.GetWorldCorners(corners);
            foreach (var corner in corners) Assert.IsTrue(frame.rect.Contains(frame.InverseTransformPoint(corner)));
            AssertHeaderReadsLeftToRight(frame, order, cost, name);
            Assert.AreEqual(HorizontalAlignmentOptions.Left, nameText.horizontalAlignment);
            Assert.AreEqual(cost.TransformPoint(cost.rect.center).y, name.TransformPoint(name.rect.center).y, .01f);
            Assert.AreEqual(0, Mathf.DeltaAngle(order.localEulerAngles.z, 0), .01f);
            AssertNoMaskAncestor(order, view.transform);
        }

        [Test]
        public void Execution_target_panel_is_one_centered_horizontal_row()
        {
            var view = LoadExecution();
            var strip = CardPrefabCatalogTests.Field<CardTargetStripView>(view, "_targetStrip");
            var layout = strip.GetComponent<HorizontalLayoutGroup>();
            Assert.IsNotNull(layout);
            Assert.AreEqual(TextAnchor.MiddleCenter, layout.childAlignment);
            Assert.IsFalse(layout.reverseArrangement);
            Assert.IsNotNull(CardPrefabCatalogTests.Field<RectTransform>(strip, "_firstSlot"));
            Assert.IsNotNull(CardPrefabCatalogTests.Field<RectTransform>(strip, "_secondSlot"));
            Assert.IsNotNull(CardPrefabCatalogTests.Field<GameObject>(strip, "_separator"));
        }

        [Test]
        public void Two_factions_bind_enemy_left_ally_right_without_changing_input_order()
        {
            var view = InstantiateConfigured(LoadExecution());
            try
            {
                var input = new[] { new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
                    new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne) };
                view.Bind(CardPrefabCatalogTests.Presentation(CardCategory.Execution, input, Array.Empty<CardDescriptionLine>()), null);
                var strip = CardPrefabCatalogTests.Field<CardTargetStripView>(view, "_targetStrip");
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)strip.transform);
                var enemy = CardPrefabCatalogTests.Field<RectTransform>(strip, "_firstSlot");
                var ally = CardPrefabCatalogTests.Field<RectTransform>(strip, "_secondSlot");
                Assert.Less(enemy.position.x, ally.position.x);
                Assert.AreEqual(enemy.position.y, ally.position.y, .01f);
                Assert.IsTrue(CardPrefabCatalogTests.Field<Image>(enemy.GetComponentInChildren<TargetGlyphView>(), "_enemyMarker").gameObject.activeSelf);
                Assert.IsTrue(CardPrefabCatalogTests.Field<Image>(ally.GetComponentInChildren<TargetGlyphView>(), "_allyMarker").gameObject.activeSelf);
                Assert.AreEqual(CardTargetFaction.Ally, input[0].Faction);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [Test]
        public void One_target_centers_and_no_target_is_execution_only()
        {
            var execution = InstantiateConfigured(LoadExecution());
            var intervention = InstantiateConfigured(LoadIntervention());
            try
            {
                execution.Bind(
                    CardPrefabCatalogTests.Presentation(
                        CardCategory.Execution,
                        new[]
                        {
                            new CardTargetKey(
                                CardTargetFaction.Ally,
                                CardTargetRange.Self)
                        },
                        Array.Empty<CardDescriptionLine>()),
                    null);
                var content = TargetSlot(execution);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                var target = (RectTransform)content.GetChild(0);
                var targetCenter = content.InverseTransformPoint(
                    target.TransformPoint(target.rect.center));
                Assert.AreEqual(
                    content.rect.center.x,
                    targetCenter.x,
                    0.5f);

                intervention.Bind(
                    CardPrefabCatalogTests.Presentation(
                        CardCategory.Intervention,
                        Array.Empty<CardTargetKey>(),
                        new[]
                        {
                            new CardDescriptionLine(null, "순서를 바꾼다.")
                        }),
                    null);
                Assert.IsEmpty(
                    intervention.GetComponentsInChildren<TargetGlyphView>(true));
            }
            finally
            {
                Object.DestroyImmediate(execution.gameObject);
                Object.DestroyImmediate(intervention.gameObject);
            }
        }

        [Test]
        public void Intervention_description_reclaims_the_target_region_and_gap()
        {
            var execution = LoadExecution();
            var intervention = LoadIntervention();
            var target = Child(execution.transform, "SymbolOnlyTargetPanel");
            var description = Child(execution.transform, "DescriptionPanel");
            var expanded = Child(
                intervention.transform,
                "ExpandedDescriptionPanel");
            var targetToDescriptionGap =
                target.anchoredPosition.y
                - target.rect.height
                - description.anchoredPosition.y;
            var expectedHeight =
                target.rect.height
                + targetToDescriptionGap
                + description.rect.height;

            Assert.AreEqual(
                target.anchoredPosition.y,
                expanded.anchoredPosition.y,
                0.5f);
            Assert.AreEqual(
                expectedHeight,
                expanded.rect.height,
                0.5f);
            Assert.AreEqual(
                description.anchoredPosition.y - description.rect.height,
                expanded.anchoredPosition.y - expanded.rect.height,
                0.5f);
        }

        [Test]
        public void Intervention_frame_omits_target_and_order_and_expands_description()
        {
            var intervention = LoadIntervention();
            var execution = LoadExecution();
            var overlay = Child(intervention.transform, "OverlayLayer");
            var cost = Child(intervention.transform, "CostBadge");

            Assert.IsNull(ChildOrNull(
                intervention.transform,
                "SymbolOnlyTargetPanel"));
            Assert.IsNull(ChildOrNull(
                intervention.transform,
                "ExecutionOrderBadge"));
            Assert.IsNull(
                CardPrefabCatalogTests.Field<CardTargetStripView>(intervention, "_targetStrip"));
            Assert.IsNull(
                (RectTransform)CardPrefabCatalogTests.Field<CardTargetStripView>(intervention, "_targetStrip")?.transform);
            Assert.IsNull(
                CardPrefabCatalogTests.Field<RectTransform>(
                    intervention,
                    "_executionOrderBadge"));
            Assert.Greater(
                Child(intervention.transform, "ExpandedDescriptionPanel").rect.height,
                Child(execution.transform, "DescriptionPanel").rect.height);
            Assert.AreSame(overlay, cost.parent);
            Assert.IsNotNull(Child(intervention.transform, "CategoryTab"));
            AssertNoMaskAncestor(cost, intervention.transform);
            Assert.That(cost.rect.size, Is.EqualTo(new Vector2(28f, 28f)));
        }

        [Test]
        public void Intervention_frame_reads_category_tab_cost_then_left_aligned_name()
        {
            var view = LoadIntervention();
            var nameText = CardPrefabCatalogTests.Field<TMP_Text>(view, "_nameText");
            var cost = Child(view.transform, "CostBadge");
            AssertHeaderReadsLeftToRight((RectTransform)view.transform,
                Child(view.transform, "CategoryTab"), cost, nameText.rectTransform);
            Assert.AreEqual(HorizontalAlignmentOptions.Left, nameText.horizontalAlignment);
            Assert.AreEqual(cost.TransformPoint(cost.rect.center).y,
                nameText.rectTransform.TransformPoint(nameText.rectTransform.rect.center).y, .01f);
        }

        // Overlapping hand cards show only their left strip, so the tab, the cost and the start of
        // the name must read left to right without overlapping.
        private static void AssertHeaderReadsLeftToRight(
            RectTransform frame, RectTransform tab, RectTransform cost, RectTransform name)
        {
            float Left(RectTransform rect) => RectTransformUtility.CalculateRelativeRectTransformBounds(frame, rect).min.x;
            float Right(RectTransform rect) => RectTransformUtility.CalculateRelativeRectTransformBounds(frame, rect).max.x;
            Assert.LessOrEqual(Right(tab), Left(cost) + .01f, "cost must follow the tab");
            Assert.LessOrEqual(Right(cost), Left(name) + .01f, "name must follow the cost");
        }

        [Test]
        public void Full_card_prefabs_are_independent_regular_assets_with_category_markers()
        {
            var execution = LoadExecution();
            var intervention = LoadIntervention();

            Assert.AreEqual(CardCategory.Execution, execution.PrefabCategory);
            Assert.AreEqual(CardCategory.Intervention, intervention.PrefabCategory);
            Assert.AreEqual(
                PrefabAssetType.Regular,
                PrefabUtility.GetPrefabAssetType(execution.gameObject));
            Assert.AreEqual(
                PrefabAssetType.Regular,
                PrefabUtility.GetPrefabAssetType(intervention.gameObject));
        }

        [Test]
        public void Full_card_prefabs_retain_shared_art_owner_status_selection_and_back_face()
        {
            foreach (var view in new[] { LoadExecution(), LoadIntervention() })
            {
                Assert.IsNotNull(CardPrefabCatalogTests.Field<Image>(view, "_art"));
                Assert.IsNotNull(CardPrefabCatalogTests.Field<Image>(view, "_artFallback"));
                Assert.IsNotNull(CardPrefabCatalogTests.Field<GameObject>(view, "_ownerChip"));
                Assert.IsNotNull(CardPrefabCatalogTests.Field<GameObject>(view, "_lockBadge"));
                Assert.IsNotNull(CardPrefabCatalogTests.Field<Outline>(view, "_selectionOutline"));
                Assert.IsNotNull(CardPrefabCatalogTests.Field<CardBackView>(view, "_backFace"));
                Assert.IsNotNull(CardPrefabCatalogTests.Field<Button>(view, "_button"));
            }
        }

        [TestCase(CardCategory.Execution)]
        [TestCase(CardCategory.Intervention)]
        public void Bound_full_card_uses_only_root_and_status_hover_raycasts(
            CardCategory category)
        {
            var source = category == CardCategory.Execution
                ? LoadExecution()
                : LoadIntervention();
            var view = InstantiateConfigured(source);
            try
            {
                var button = CardPrefabCatalogTests.Field<Button>(view, "_button");
                var rootGraphic = view.GetComponent<Image>();
                Assert.AreSame(rootGraphic, button.targetGraphic);
                Assert.IsTrue(rootGraphic.raycastTarget);
                var raycastGraphics = view.GetComponentsInChildren<Graphic>(true)
                    .Where(graphic => graphic.raycastTarget)
                    .ToArray();
                Assert.AreEqual(2, raycastGraphics.Length);
                CollectionAssert.Contains(raycastGraphics, rootGraphic);
                Assert.AreEqual(
                    1,
                    raycastGraphics.Count(graphic =>
                        graphic.GetComponent<CardStatusIconView>() != null));

                int firstCalls = 0;
                int secondCalls = 0;
                var presentation = CardPrefabCatalogTests.Presentation(
                    category,
                    Array.Empty<CardTargetKey>(),
                    Array.Empty<CardDescriptionLine>());
                view.Bind(presentation, () => firstCalls++);
                button.onClick.Invoke();
                view.Bind(presentation, () => secondCalls++);
                button.onClick.Invoke();

                Assert.AreEqual(1, firstCalls);
                Assert.AreEqual(1, secondCalls);
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [TestCase(CardCategory.Execution, CardCategory.Intervention)]
        [TestCase(CardCategory.Intervention, CardCategory.Execution)]
        public void Card_view_rejects_bound_category_mismatch(
            CardCategory prefabCategory,
            CardCategory boundCategory)
        {
            var source = prefabCategory == CardCategory.Execution
                ? LoadExecution()
                : LoadIntervention();
            var view = Object.Instantiate(source);
            try
            {
                view.Configure(CardPrefabCatalogTests.LoadCatalog());
                var presentation = CardPrefabCatalogTests.Presentation(
                    boundCategory,
                    Array.Empty<CardTargetKey>(),
                    Array.Empty<CardDescriptionLine>());

                Assert.Throws<InvalidOperationException>(
                    () => view.Bind(presentation, null));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Execution_with_no_unit_targets_shows_one_empty_target_glyph()
        {
            var view = InstantiateConfigured(LoadExecution());
            try
            {
                view.Bind(
                    CardPrefabCatalogTests.Presentation(
                        CardCategory.Execution,
                        Array.Empty<CardTargetKey>(),
                        Array.Empty<CardDescriptionLine>()),
                    null);

                var targetContent =
                    TargetSlot(view);
                Assert.AreEqual(1, targetContent.childCount);
                var glyph = targetContent.GetChild(0).GetComponent<TargetGlyphView>();
                Assert.IsNotNull(glyph);
                AssertActiveVisual(glyph, "Empty");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Bind_creates_each_target_entry_and_preserves_each_description_line()
        {
            var view = InstantiateConfigured(LoadExecution());
            try
            {
                var lines = new[]
                {
                    new CardDescriptionLine(
                        new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne),
                        "피해 3."),
                    new CardDescriptionLine(
                        new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
                        "방어 2."),
                    new CardDescriptionLine(null, "카드 1장 뽑기.")
                };
                view.Bind(
                    CardPrefabCatalogTests.Presentation(
                        CardCategory.Execution,
                        new[]
                        {
                            new CardTargetKey(
                                CardTargetFaction.Ally,
                                CardTargetRange.Self),
                            new CardTargetKey(
                                CardTargetFaction.Enemy,
                                CardTargetRange.FrontOne)
                        },
                        lines),
                    null);

                var targetContent =
                    TargetSlot(view);
                var descriptionContent =
                    DescriptionContent(view);
                Assert.AreEqual(2, view.GetComponentsInChildren<TargetGlyphView>().Length);
                Assert.AreEqual(3, descriptionContent.childCount);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "피해 3.",
                        "방어 2.",
                        "카드 1장 뽑기."
                    },
                    descriptionContent
                        .GetComponentsInChildren<DescriptionLineView>(true)
                        .Select(line => CardPrefabCatalogTests.Field<TMP_Text>(line, "_text").text)
                        .ToArray());
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Intervention_bind_never_creates_a_target_panel_glyph()
        {
            var view = InstantiateConfigured(LoadIntervention());
            try
            {
                view.Bind(
                    CardPrefabCatalogTests.Presentation(
                        CardCategory.Intervention,
                        Array.Empty<CardTargetKey>(),
                        new[] { new CardDescriptionLine(null, "순서를 바꾼다.") }),
                    null);

                Assert.IsNull(CardPrefabCatalogTests.Field<CardTargetStripView>(view, "_targetStrip"));
                Assert.IsEmpty(view.GetComponentsInChildren<TargetGlyphView>(true)
                    .Where(glyph => glyph.GetComponentInParent<DescriptionLineView>() == null));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Bind_does_not_recalculate_authored_card_coordinates()
        {
            var view = InstantiateConfigured(LoadExecution());
            try
            {
                var authored = new[]
                {
                    Child(view.transform, "ArtPanel"),
                    Child(view.transform, "SymbolOnlyTargetPanel"),
                    Child(view.transform, "DescriptionPanel"),
                    Child(view.transform, "CostBadge"),
                    Child(view.transform, "ExecutionOrderBadge")
                }.ToDictionary(rect => rect.name, Snapshot);

                view.Bind(
                    CardPrefabCatalogTests.Presentation(
                        CardCategory.Execution,
                        Array.Empty<CardTargetKey>(),
                        Array.Empty<CardDescriptionLine>()),
                    null);

                foreach (var pair in authored)
                {
                    Assert.AreEqual(
                        pair.Value,
                        Snapshot(Child(view.transform, pair.Key)),
                        pair.Key + " coordinates changed during Bind.");
                }

                Assert.IsNull(
                    typeof(CardView).GetMethod(
                        "LateUpdate",
                        BindingFlags.Instance | BindingFlags.NonPublic));
                Assert.IsNull(
                    typeof(CardView).GetMethod(
                        "ApplyResponsiveLayout",
                        BindingFlags.Instance | BindingFlags.NonPublic));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [TestCase(CardPrefabCatalogTests.ExecutionPath)]
        [TestCase(CardPrefabCatalogTests.InterventionPath)]
        public void Status_grid_uses_four_columns_and_grows_from_its_top_edge(
            string path)
        {
            var card = Load<CardView>(path);
            var grid = Child(card.transform, "CardStatusGrid");
            var layout = grid.GetComponent<GridLayoutGroup>();
            var fitter = grid.GetComponent<ContentSizeFitter>();
            var template = Child(grid, "StatusIconTemplate");

            Assert.IsNotNull(layout);
            Assert.AreEqual(new Vector2(26f, 26f), layout.cellSize);
            Assert.AreEqual(new Vector2(4f, 4f), layout.spacing);
            Assert.AreEqual(
                GridLayoutGroup.Constraint.FixedColumnCount,
                layout.constraint);
            Assert.AreEqual(4, layout.constraintCount);
            Assert.AreEqual(GridLayoutGroup.Corner.UpperLeft, layout.startCorner);
            Assert.AreEqual(GridLayoutGroup.Axis.Horizontal, layout.startAxis);
            Assert.AreEqual(TextAnchor.UpperLeft, layout.childAlignment);
            Assert.AreEqual(1f, grid.pivot.y);
            Assert.IsNotNull(fitter);
            Assert.AreEqual(
                ContentSizeFitter.FitMode.Unconstrained,
                fitter.horizontalFit);
            Assert.AreEqual(
                ContentSizeFitter.FitMode.PreferredSize,
                fitter.verticalFit);
            Assert.IsFalse(template.gameObject.activeSelf);
            Assert.IsNotNull(template.GetComponent<CardStatusIconView>());
            Assert.IsTrue(template.GetComponent<Image>().raycastTarget);
        }

        [Test]
        public void Status_tooltip_prefab_has_unlabeled_colored_title_and_body()
        {
            var tooltip = Load<CardStatusTooltipView>(StatusTooltipPath);
            var title = CardPrefabCatalogTests.Field<TMP_Text>(tooltip, "_titleText");
            var description = CardPrefabCatalogTests.Field<TMP_Text>(
                tooltip,
                "_descriptionText");

            Assert.IsFalse(tooltip.gameObject.activeSelf);
            Assert.AreEqual("", title.text);
            Assert.AreEqual("", description.text);
            Assert.AreEqual("F2C14E", ColorUtility.ToHtmlStringRGB(title.color));
            Assert.AreEqual(
                "E8EDF2",
                ColorUtility.ToHtmlStringRGB(description.color));
            Assert.AreEqual(2, tooltip.GetComponentsInChildren<TMP_Text>(true).Length);
        }

        [Test]
        public void Description_body_preserves_compact_text_without_faction_prefix()
        {
            var view = InstantiateDescriptionLine();
            try
            {
                view.Bind(new CardDescriptionLine(
                    new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.All),
                    "피해 8. 약화 1."));
                Assert.AreEqual("피해 8. 약화 1.",
                    CardPrefabCatalogTests.Field<TMP_Text>(view, "_text").text);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [Test]
        public void Description_rebind_keeps_headings_above_body_and_clears_old_groups()
        {
            var view = InstantiateConfigured(LoadExecution());
            try
            {
                var enemy = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.All);
                var ally = new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self);
                var pairs = new[] { new CardDescriptionLine(enemy, "피해 8. 약화 1."), new CardDescriptionLine(ally, "방어 3.") };
                var panel = CardPrefabCatalogTests.Field<CardDescriptionPanelView>(view, "_descriptionPanel");
                var content = DescriptionContent(view);
                foreach (var lines in new[] { pairs, new[] { pairs[0] }, Array.Empty<CardDescriptionLine>(), pairs,
                    new[] { pairs[0], new CardDescriptionLine(null, "카드 1장 뽑기.") } })
                {
                    panel.Bind(lines);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                    Assert.AreEqual(lines.Length, content.childCount);
                    var groups = content.GetComponentsInChildren<DescriptionLineView>();
                    Assert.AreEqual(Mathf.Max(0, lines.Length - 1), groups.Count(group =>
                        CardPrefabCatalogTests.Field<GameObject>(group, "_separator").activeSelf));
                    for (int index = 0; index < groups.Length; index++)
                    {
                        var body = CardPrefabCatalogTests.Field<TMP_Text>(groups[index], "_text");
                        var heading = CardPrefabCatalogTests.Field<TMP_Text>(groups[index], "_headingText");
                        Assert.AreEqual(lines[index].Text, body.text);
                        Assert.AreEqual(lines[index].Target.HasValue, heading.gameObject.activeSelf);
                        Assert.AreEqual(lines.Length == 1 ? TextAlignmentOptions.Left : TextAlignmentOptions.TopLeft, body.alignment);
                        var bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, body.transform);
                        Assert.GreaterOrEqual(bodyBounds.min.y, content.rect.yMin - .1f);
                        Assert.LessOrEqual(bodyBounds.max.y, content.rect.yMax + .1f);
                        if (heading.gameObject.activeSelf)
                        {
                            var headingBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, heading.transform);
                            Assert.GreaterOrEqual(headingBounds.min.y, bodyBounds.max.y);
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [TestCase(CardTargetFaction.Enemy, CardTargetRange.FrontOne)]
        [TestCase(CardTargetFaction.Ally, CardTargetRange.FrontTwo)]
        [TestCase(CardTargetFaction.Ally, CardTargetRange.All)]
        [TestCase(CardTargetFaction.Enemy, CardTargetRange.Self)]
        public void Target_strip_rebind_centers_single_and_empty_and_restores_divider(CardTargetFaction faction, CardTargetRange range)
        {
            var view = InstantiateConfigured(LoadExecution());
            try
            {
                var strip = CardPrefabCatalogTests.Field<CardTargetStripView>(view, "_targetStrip");
                var rect = (RectTransform)strip.transform;
                var divider = CardPrefabCatalogTests.Field<GameObject>(strip, "_separator");
                var both = new[] { new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self), new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.All) };
                foreach (var entries in new[] { both, new[] { new CardTargetKey(faction, range) }, Array.Empty<CardTargetKey>(), both })
                {
                    strip.Bind(entries);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    var glyphs = strip.GetComponentsInChildren<TargetGlyphView>();
                    Assert.AreEqual(Mathf.Max(1, entries.Length), glyphs.Length);
                    Assert.AreEqual(entries.Length == 2, divider.activeSelf);
                    if (entries.Length < 2)
                    {
                        var glyph = (RectTransform)glyphs[0].transform;
                        Assert.AreEqual(rect.rect.center.x, rect.InverseTransformPoint(glyph.TransformPoint(glyph.rect.center)).x, .1f);
                    }
                }
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [Test]
        public void Every_content_card_keeps_body_inside_its_authored_area()
        {
            var root = new GameObject("ContentCardCanvas", typeof(RectTransform), typeof(Canvas));
            using (CardFrameRenderCapture.CloneCatalogForCapture(CardPrefabCatalogTests.LoadCatalog(), out var catalog))
            {
                try
                {
                    var korean = KoreanDescriptionCatalog.CreateDefault(UnityTestContent.Statuses());
                    foreach (var pair in UnityTestContent.Cards().Cards)
                    {
                        var presentation = CardPresentation.FromDefinition(pair.Value, korean);
                        var view = catalog.Create(presentation, (RectTransform)root.transform);
                        try
                        {
                            view.Bind(presentation, null);
                            LayoutRebuilder.ForceRebuildLayoutImmediate(DescriptionContent(view));
                            foreach (var group in view.GetComponentsInChildren<DescriptionLineView>())
                            {
                                var body = CardPrefabCatalogTests.Field<TMP_Text>(group, "_text");
                                body.ForceMeshUpdate(true, true);
                                Assert.LessOrEqual(body.textBounds.size.y, body.rectTransform.rect.height + 1f, pair.Key + ": " + body.text);
                                Assert.IsFalse(body.isTextOverflowing, pair.Key + ": " + body.text);
                            }
                            var title = CardPrefabCatalogTests.Field<TMP_Text>(view, "_nameText");
                            title.ForceMeshUpdate(true, true);
                            Assert.LessOrEqual(title.textInfo.lineCount, 2, pair.Key + " title");
                        }
                        finally { Object.DestroyImmediate(view.gameObject); }
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        private static RectTransform TargetSlot(CardView view)
            => CardPrefabCatalogTests.Field<RectTransform>(CardPrefabCatalogTests.Field<CardTargetStripView>(view, "_targetStrip"), "_firstSlot");
        private static RectTransform DescriptionContent(CardView view)
            => CardPrefabCatalogTests.Field<RectTransform>(CardPrefabCatalogTests.Field<CardDescriptionPanelView>(view, "_descriptionPanel"), "_content");

        private static TargetGlyphView InstantiateGlyph()
            => Object.Instantiate(
                Load<TargetGlyphView>(CardPrefabCatalogTests.TargetGlyphPath));

        private static DescriptionLineView InstantiateDescriptionLine()
            => Object.Instantiate(
                Load<DescriptionLineView>(CardPrefabCatalogTests.DescriptionLinePath));

        private static CardView LoadExecution()
            => Load<CardView>(CardPrefabCatalogTests.ExecutionPath);

        private static CardView LoadIntervention()
            => Load<CardView>(CardPrefabCatalogTests.InterventionPath);

        private static CardView InstantiateConfigured(CardView prefab)
        {
            var view = Object.Instantiate(prefab);
            view.Configure(CardPrefabCatalogTests.LoadCatalog());
            return view;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, path + " must exist.");
            return asset;
        }

        private static string[] DirectChildNames(Transform parent)
            => Enumerable.Range(0, parent.childCount)
                .Select(index => parent.GetChild(index).name)
                .ToArray();

        private static RectTransform Child(Transform parent, string name)
        {
            var child = ChildOrNull(parent, name);
            Assert.IsNotNull(child, name + " is missing from " + parent.name + ".");
            return child;
        }

        private static RectTransform ChildOrNull(Transform parent, string name)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name)
                {
                    return (RectTransform)child;
                }

                var descendant = ChildOrNull(child, name);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static void AssertActiveVisual(
            TargetGlyphView glyph,
            string expectedName)
        {
            foreach (var name in GlyphVisualNames)
            {
                Assert.AreEqual(
                    name == expectedName,
                    Child(glyph.transform, name).gameObject.activeSelf,
                    name);
            }
        }

        private static float ActiveGraphicBoundsWidth(Transform root)
        {
            var corners = new Vector3[4];
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    min = Mathf.Min(min, corner.x);
                    max = Mathf.Max(max, corner.x);
                }
            }

            return max - min;
        }

        private static void AssertBadgeOutsideFrame(
            CardView view,
            RectTransform badge)
        {
            var frame = (RectTransform)view.transform;
            var corners = new Vector3[4];
            badge.GetWorldCorners(corners);
            var localCorners = corners
                .Select(frame.InverseTransformPoint)
                .ToArray();
            Assert.IsTrue(
                localCorners.Any(corner => !frame.rect.Contains(corner)),
                badge.name + " must protrude outside the frame.");
        }

        private static void AssertNoMaskAncestor(
            Transform badge,
            Transform frame)
        {
            for (var current = badge; current != null; current = current.parent)
            {
                Assert.IsNull(current.GetComponent<Mask>());
                Assert.IsNull(current.GetComponent<RectMask2D>());
                if (current == frame)
                {
                    return;
                }
            }

            Assert.Fail(badge.name + " is not under the card frame.");
        }

        private static RectSnapshot Snapshot(RectTransform rect)
            => new RectSnapshot(
                rect.anchorMin,
                rect.anchorMax,
                rect.pivot,
                rect.anchoredPosition,
                rect.sizeDelta,
                rect.localRotation);

        private readonly struct RectSnapshot : IEquatable<RectSnapshot>
        {
            private readonly Vector2 _anchorMin;
            private readonly Vector2 _anchorMax;
            private readonly Vector2 _pivot;
            private readonly Vector2 _anchoredPosition;
            private readonly Vector2 _sizeDelta;
            private readonly Quaternion _rotation;

            public RectSnapshot(
                Vector2 anchorMin,
                Vector2 anchorMax,
                Vector2 pivot,
                Vector2 anchoredPosition,
                Vector2 sizeDelta,
                Quaternion rotation)
            {
                _anchorMin = anchorMin;
                _anchorMax = anchorMax;
                _pivot = pivot;
                _anchoredPosition = anchoredPosition;
                _sizeDelta = sizeDelta;
                _rotation = rotation;
            }

            public bool Equals(RectSnapshot other)
                => _anchorMin == other._anchorMin
                   && _anchorMax == other._anchorMax
                   && _pivot == other._pivot
                   && _anchoredPosition == other._anchoredPosition
                   && _sizeDelta == other._sizeDelta
                   && _rotation == other._rotation;

            public override bool Equals(object obj)
                => obj is RectSnapshot other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _anchorMin.GetHashCode();
                    hash = (hash * 397) ^ _anchorMax.GetHashCode();
                    hash = (hash * 397) ^ _pivot.GetHashCode();
                    hash = (hash * 397) ^ _anchoredPosition.GetHashCode();
                    hash = (hash * 397) ^ _sizeDelta.GetHashCode();
                    hash = (hash * 397) ^ _rotation.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
