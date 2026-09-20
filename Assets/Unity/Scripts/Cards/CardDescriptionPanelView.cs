using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;

namespace FateWeaver.Unity
{
    public sealed class CardDescriptionPanelView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private DescriptionLineView _linePrefab;

        public void Configure(DescriptionLineView linePrefab)
        {
            _linePrefab = linePrefab != null ? linePrefab : throw new ArgumentNullException(nameof(linePrefab));
        }

        public void Bind(IReadOnlyList<CardDescriptionLine> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (_content == null || _linePrefab == null)
                throw new InvalidOperationException("Description panel is missing its content or line prefab.");
            GeneratedCardChildren.Clear(_content);
            for (int index = 0; index < lines.Count; index++)
            {
                var line = Instantiate(_linePrefab, _content);
                line.Bind(lines[index]);
                line.SetLayout(lines.Count == 1, index > 0);
                line.Measure(_content.rect.width);
            }
        }
    }
}
