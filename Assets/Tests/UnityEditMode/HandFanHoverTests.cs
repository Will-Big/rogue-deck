using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class HandFanHoverTests
    {
        [Test]
        public void Hand_card_reports_its_index_on_hover_enter_and_exit()
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<CardView>(
                    "Assets/Unity/Prefabs/CardView.prefab");
                Assert.IsNotNull(prefab);
                var hand = root.AddComponent<HandFanView>();
                hand.EditorBuild(prefab);
                var calls = new List<(int Index, bool Hovering)>();
                var cards = new[]
                {
                    new CardPresentation(
                        "execution", "execution", 3, 1, Side.Player,
                        string.Empty, null, false)
                };
                hand.SetCards(cards, _ => { },
                    (index, hovering) => calls.Add((index, hovering)));
                var hover = root.GetComponentInChildren<HandCardHoverEffect>();

                hover.OnPointerEnter(null);
                hover.OnPointerExit(null);

                CollectionAssert.AreEqual(
                    new[] { (0, true), (0, false) }, calls.ToArray());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
