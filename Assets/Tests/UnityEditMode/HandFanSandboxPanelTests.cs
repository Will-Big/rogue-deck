using System.Reflection;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class HandFanSandboxPanelTests
    {
        private GameObject _root;
        private HandFanSandboxPanel _panel;
        private Button _add, _remove, _clear;
        private TMP_Text _count, _error;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PanelTest", typeof(RectTransform));
            _root.SetActive(false);
            _panel = _root.AddComponent<HandFanSandboxPanel>();
            _add = Child<Button>(); _remove = Child<Button>(); _clear = Child<Button>();
            _count = Child<TextMeshProUGUI>(); _error = Child<TextMeshProUGUI>();
            var so = new SerializedObject(_panel);
            so.FindProperty("_addButton").objectReferenceValue = _add;
            so.FindProperty("_removeButton").objectReferenceValue = _remove;
            so.FindProperty("_clearButton").objectReferenceValue = _clear;
            so.FindProperty("_countText").objectReferenceValue = _count;
            so.FindProperty("_errorText").objectReferenceValue = _error;
            so.ApplyModifiedPropertiesWithoutUndo();
            _root.SetActive(true);
            Invoke("OnEnable");
        }
        private T Child<T>() where T : Component
        {
            var child = new GameObject(typeof(T).Name, typeof(RectTransform));
            child.transform.SetParent(_root.transform, false);
            return child.AddComponent<T>();
        }
        private void Invoke(string name) => typeof(HandFanSandboxPanel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_panel, null);
        [TearDown] public void TearDown() { Invoke("OnDisable"); Object.DestroyImmediate(_root); }

        [Test]
        public void State_controls_count_and_unavailable_actions_and_error_disables_all()
        {
            _panel.ShowState(0, false, false);
            Assert.That(_count.text, Does.Contain("0"));
            Assert.That(_add.interactable, Is.True);
            Assert.That(_remove.interactable, Is.False);
            Assert.That(_clear.interactable, Is.False);
            _panel.ShowState(3, true, true);
            Assert.That(_count.text, Does.Contain("3"));
            Assert.That(_remove.interactable && _clear.interactable, Is.True);
            _panel.ShowError("누락된 카드");
            Assert.That(_error.text, Does.Contain("누락된 카드"));
            Assert.That(_add.interactable || _remove.interactable || _clear.interactable, Is.False);
        }
        [Test]
        public void Repeated_enable_disable_does_not_duplicate_or_leave_input_listeners()
        {
            int adds = 0, removes = 0, clears = 0;
            _panel.AddRequested += () => adds++;
            _panel.RemoveRequested += () => removes++;
            _panel.ClearRequested += () => clears++;
            _panel.ShowState(3, true, true);
            Invoke("OnDisable");
            _add.onClick.Invoke();
            Assert.That(adds, Is.Zero);
            Invoke("OnEnable"); Invoke("OnEnable");
            _add.onClick.Invoke(); _remove.onClick.Invoke(); _clear.onClick.Invoke();
            Assert.That(new[] { adds, removes, clears }, Is.EqualTo(new[] { 1, 1, 1 }));
        }
    }
}
