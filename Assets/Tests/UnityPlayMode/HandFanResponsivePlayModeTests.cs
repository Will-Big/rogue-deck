using System;
using System.Collections;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityPlayMode
{
    public class HandFanResponsivePlayModeTests
    {
        [UnityTest]
        public IEnumerator Empty_hand_survives_resize_without_transform_drift()
        {
            var root = new GameObject("HandFan", typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(420f, 190f);
                var contentObject = new GameObject("Content", typeof(RectTransform));
                var content = (RectTransform)contentObject.transform;
                content.SetParent(rootRect, false);
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
                var hand = root.AddComponent<HandFanView>();
                hand.EditorBuild(null, content);
                hand.SetCards(Array.Empty<CardPresentation>(), null, null);
                yield return null;

                rootRect.sizeDelta = new Vector2(900f, 260f);
                yield return null;
                Vector3 scaleAfterResize = content.localScale;
                Vector2 positionAfterResize = content.anchoredPosition;
                Assert.AreEqual(Vector3.one, scaleAfterResize);
                yield return null;
                Assert.AreEqual(scaleAfterResize, content.localScale);
                Assert.AreEqual(positionAfterResize, content.anchoredPosition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator Hover_animates_in_unscaled_time_then_restores_latest_layout()
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            float previousTimeScale = Time.timeScale;
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(900f, 300f);
                var content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
                content.SetParent(root.transform, false);
                content.sizeDelta = Vector2.zero;
                var card = (RectTransform)new GameObject("Card", typeof(RectTransform)).transform;
                card.SetParent(content, false);
                card.sizeDelta = new Vector2(200f, 336f);
                var hover = card.gameObject.AddComponent<HandCardHoverEffect>();
                hover.Capture();
                hover.Initialize(null);
                var layout = root.AddComponent<HandFanLayoutView>();
                layout.EditorBuild(content);
                layout.Bind(new[] { hover });
                yield return null;

                Time.timeScale = 0f;
                var baseline = card.anchoredPosition;
                hover.OnPointerEnter(null);
                Assert.That(card.anchoredPosition.y, Is.EqualTo(baseline.y).Within(.001f));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(card.anchoredPosition.y, Is.EqualTo(baseline.y + 46f).Within(.01f));
                Assert.That(card.localScale.x, Is.EqualTo(.64f * 1.35f).Within(.001f));

                rootRect.sizeDelta = new Vector2(400f, 190f);
                hover.OnPointerExit(null);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(card.anchoredPosition, Is.EqualTo(baseline));
                Assert.That(card.localScale.x, Is.EqualTo(.64f).Within(.001f));
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root.transform, card);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-95.001f));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(95.001f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator Resize_recomputes_a_populated_hand_without_a_manual_refresh()
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(420f, 190f);
                var content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
                content.SetParent(root.transform, false);
                content.sizeDelta = Vector2.zero;
                var cards = new HandCardHoverEffect[5];
                for (int i = 0; i < cards.Length; i++)
                {
                    var card = (RectTransform)new GameObject("Card", typeof(RectTransform)).transform;
                    card.SetParent(content, false);
                    card.sizeDelta = new Vector2(200f, 336f);
                    cards[i] = card.gameObject.AddComponent<HandCardHoverEffect>();
                    cards[i].Capture();
                    cards[i].Initialize(null);
                }
                var layout = root.AddComponent<HandFanLayoutView>();
                layout.EditorBuild(content);
                layout.Bind(cards);
                yield return null;
                float narrowScale = content.localScale.x;
                rootRect.sizeDelta = new Vector2(900f, 300f);
                yield return null;
                Assert.Greater(content.localScale.x, narrowScale);
                var after = content.localScale;
                yield return null;
                Assert.AreEqual(after, content.localScale);
                cards[0].OnPointerEnter(null);
                Assert.AreEqual(4, cards[0].transform.GetSiblingIndex());
                root.SetActive(false);
                root.SetActive(true);
                cards[0].OnPointerEnter(null);
                yield return null;
                Assert.AreEqual(4, cards[0].transform.GetSiblingIndex());
                cards[0].OnPointerExit(null);
                Assert.AreEqual(0, cards[0].transform.GetSiblingIndex());
            }
            finally { Object.DestroyImmediate(root); }
        }

    }
}
