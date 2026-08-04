using System;
using System.Collections;
using System.Reflection;
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
        public IEnumerator Rect_dimension_change_automatically_recomputes_layout_exactly_once()
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

                int revisionBeforeResize = LayoutRevision(hand);
                float scaleBeforeResize = content.localScale.x;

                rootRect.sizeDelta = new Vector2(900f, 260f);
                yield return null;

                Assert.AreEqual(revisionBeforeResize + 1, LayoutRevision(hand));
                Assert.That(content.localScale.x, Is.GreaterThan(scaleBeforeResize));
                Assert.AreEqual(1f, content.localScale.x, 0.0001f);
                int revisionAfterResize = LayoutRevision(hand);

                yield return null;

                Assert.AreEqual(revisionAfterResize, LayoutRevision(hand));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static int LayoutRevision(HandFanView hand)
        {
            var field = typeof(HandFanView).GetField(
                "_layoutRevision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(
                field,
                "HandFanView must expose a private observable layout revision.");
            return (int)field.GetValue(hand);
        }
    }
}
