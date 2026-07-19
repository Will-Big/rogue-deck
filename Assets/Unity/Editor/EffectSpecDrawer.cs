using System.Linq;
using FateWeaver.Simulation.Authoring;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>Dropdown-driven [SerializeReference] picker for EffectSpec. Candidates come from the
    /// explicit EffectSpecCatalog (no reflection scan — AGENTS.md rule 14 준수).</summary>
    [CustomPropertyDrawer(typeof(EffectSpec), useForChildren: true)]
    public sealed class EffectSpecDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var infos = EffectSpecCatalog.All();
            var current = property.managedReferenceValue as EffectSpec;
            var currentIndex = current == null
                ? -1
                : infos.ToList().FindIndex(i => i.SpecType == current.GetType());

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var names = new[] { "(효과 선택)" }.Concat(infos.Select(i => i.DisplayName)).ToArray();
            var picked = EditorGUI.Popup(line, label.text, currentIndex + 1, names) - 1;
            if (picked != currentIndex && picked >= 0)
            {
                property.managedReferenceValue = infos[picked].Create();
            }

            if (property.managedReferenceValue != null)
            {
                var body = new Rect(
                    position.x,
                    position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    position.height - EditorGUIUtility.singleLineHeight);
                EditorGUI.indentLevel++;
                foreach (var child in ChildProperties(property))
                {
                    var h = EditorGUI.GetPropertyHeight(child, includeChildren: true);
                    EditorGUI.PropertyField(new Rect(body.x, body.y, body.width, h), child, includeChildren: true);
                    body.y += h + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.indentLevel--;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue != null)
            {
                foreach (var child in ChildProperties(property))
                {
                    height += EditorGUI.GetPropertyHeight(child, includeChildren: true)
                        + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }

        private static System.Collections.Generic.IEnumerable<SerializedProperty> ChildProperties(
            SerializedProperty property)
        {
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            if (!iterator.NextVisible(enterChildren: true)) yield break;
            while (!SerializedProperty.EqualContents(iterator, end))
            {
                yield return iterator.Copy();
                if (!iterator.NextVisible(enterChildren: false)) yield break;
            }
        }
    }
}
