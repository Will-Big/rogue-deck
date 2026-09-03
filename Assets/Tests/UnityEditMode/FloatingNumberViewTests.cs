using DG.Tweening;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>부동 숫자의 표기 규칙 하나를 잠근다 — 피해든 회복이든 숫자는 언제나 양수이고,
    /// 어느 쪽인지는 색이 말한다. 빼기표를 붙이면 색과 같은 말을 두 번 한다.</summary>
    public class FloatingNumberViewTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("FloatingNumberTestRoot", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void Damage_renders_without_a_minus_sign()
        {
            Assert.AreEqual("7", TextAfterPlaying(-7));
        }

        [Test]
        public void Heal_renders_without_a_plus_sign()
        {
            Assert.AreEqual("5", TextAfterPlaying(5));
        }

        private string TextAfterPlaying(int delta)
        {
            var view = FloatingNumberView.EditorCreate(
                (RectTransform)_root.transform, new Vector2(120f, 48f));
            var label = view.GetComponentInChildren<TMP_Text>();

            var tween = view.Play(delta);
            var text = label.text;
            tween?.Kill();
            return text;
        }
    }
}
