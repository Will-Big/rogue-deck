using System;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    public sealed class DescriptionLineView : MonoBehaviour
    {
        [Serializable]
        public sealed class FactionLabel
        {
            public CardTargetFaction Faction;
            public string Symbol;
            public string Label;
        }

        [Serializable]
        public sealed class RangeLabel
        {
            public CardTargetRange Range;
            public string Label;
        }

        [SerializeField] private TMP_Text _text;
        [SerializeField] private TMP_Text _headingText;
        [SerializeField] private GameObject _separator;
        [SerializeField] private LayoutElement _bodyLayout;
        [SerializeField] private Color _allySymbolColor;
        [SerializeField] private Color _enemySymbolColor;
        [SerializeField] private FactionLabel[] _factionLabels;
        [SerializeField] private RangeLabel[] _rangeLabels;

        public void Bind(CardDescriptionLine line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (_text == null || _headingText == null || _bodyLayout == null || _separator == null)
                throw new InvalidOperationException("DescriptionLineView is missing an authored reference.");
            ValidateLabels();
            _text.text = line.Text;
            _headingText.gameObject.SetActive(line.Target.HasValue);
            if (!line.Target.HasValue)
            {
                _headingText.text = string.Empty;
                return;
            }

            var key = line.Target.Value;
            var faction = Array.Find(_factionLabels, item => item.Faction == key.Faction);
            var range = Array.Find(_rangeLabels, item => item.Range == key.Range);
            if (faction == null || range == null)
                throw new ArgumentOutOfRangeException(nameof(line), key, "Undefined target key.");
            var color = key.Faction == CardTargetFaction.Ally ? _allySymbolColor : _enemySymbolColor;
            _headingText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">"
                + faction.Symbol + "</color> " + faction.Label + " " + range.Label;
        }

        public void SetLayout(bool onlyGroup, bool showSeparator)
        {
            _separator.SetActive(showSeparator);
            // TMP Left combines horizontal left with vertical middle (not baseline MidlineLeft).
            _text.alignment = onlyGroup ? TextAlignmentOptions.Left : TextAlignmentOptions.TopLeft;
        }

        public void Measure(float width)
        {
            // Let uGUI allocate preferred heights first and shrink towards measured minima.
            // TMP then fits the body in its allocated area without borrowing the heading area.
            float previousSize = _text.fontSize;
            bool previousAutoSize = _text.enableAutoSizing;
            _text.enableAutoSizing = false;
            _text.fontSize = _text.fontSizeMin;
            _bodyLayout.minHeight = _text.GetPreferredValues(_text.text, width, float.PositiveInfinity).y;
            _text.fontSize = _text.fontSizeMax;
            _bodyLayout.preferredHeight = _text.GetPreferredValues(_text.text, width, float.PositiveInfinity).y;
            _bodyLayout.flexibleHeight = 1;
            _text.fontSize = previousSize;
            _text.enableAutoSizing = previousAutoSize;
        }

        private void ValidateLabels()
        {
            if (_factionLabels == null || _factionLabels.Length != Enum.GetValues(typeof(CardTargetFaction)).Length
                || _rangeLabels == null || _rangeLabels.Length != Enum.GetValues(typeof(CardTargetRange)).Length)
                throw new InvalidOperationException("Description labels must contain each faction and range exactly once.");
            foreach (CardTargetFaction faction in Enum.GetValues(typeof(CardTargetFaction)))
            {
                if (_factionLabels == null || Array.FindAll(_factionLabels,
                    item => item != null && item.Faction == faction && !string.IsNullOrEmpty(item.Symbol)
                    && !string.IsNullOrEmpty(item.Label)).Length != 1)
                    throw new InvalidOperationException("Description faction labels must be complete and unique.");
            }
            foreach (CardTargetRange range in Enum.GetValues(typeof(CardTargetRange)))
            {
                if (_rangeLabels == null || Array.FindAll(_rangeLabels,
                    item => item != null && item.Range == range && !string.IsNullOrEmpty(item.Label)).Length != 1)
                    throw new InvalidOperationException("Description range labels must be complete and unique.");
            }
        }
    }
}
