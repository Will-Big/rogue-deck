using System;
using FateWeaver.Simulation.Descriptions;
using TMPro;
using UnityEngine;

namespace FateWeaver.Unity
{
    public sealed class DescriptionLineView : MonoBehaviour
    {
        [SerializeField] private RectTransform _glyphSlot;
        [SerializeField] private TargetGlyphView _glyph;
        [SerializeField] private TMP_Text _text;

        public void Bind(CardDescriptionLine line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            bool hasTarget = line.Target.HasValue;
            if (_glyphSlot != null)
            {
                _glyphSlot.gameObject.SetActive(hasTarget);
            }

            if (_glyph != null)
            {
                _glyph.gameObject.SetActive(hasTarget);
                if (hasTarget)
                {
                    _glyph.Bind(line.Target.Value);
                }
            }

            if (_text != null)
            {
                _text.text = line.Text;
            }
        }
    }
}
