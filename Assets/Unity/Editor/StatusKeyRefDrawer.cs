using System.Linq;
using FateWeaver.Simulation.Authoring;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    [CustomPropertyDrawer(typeof(StatusKeyRef))]
    public sealed class StatusKeyRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                var id = property.FindPropertyRelative(nameof(StatusKeyRef.Id));
                if (id == null || id.propertyType != SerializedPropertyType.String)
                {
                    EditorGUI.PropertyField(position, property, label, includeChildren: true);
                    return;
                }

                var model = StatusKeyDropdownOptions.CreateDefault(id.stringValue);
                var labels = model.Options.Select(option => option.Label).ToArray();
                var wasShowingMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = id.hasMultipleDifferentValues;
                int selectedIndex;
                try
                {
                    selectedIndex = EditorGUI.Popup(
                        position,
                        label.text,
                        model.SelectedIndex,
                        labels);
                }
                finally
                {
                    EditorGUI.showMixedValue = wasShowingMixedValue;
                }

                if (!id.hasMultipleDifferentValues && selectedIndex != model.SelectedIndex)
                    id.stringValue = model.Options[selectedIndex].Id;
            }
            finally
            {
                EditorGUI.EndProperty();
            }
        }
    }
}
