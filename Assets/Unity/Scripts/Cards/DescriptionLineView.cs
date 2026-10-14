using System;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    public sealed class DescriptionLineView : MonoBehaviour
    {
        [Serializable]
        public sealed class FactionSymbol
        {
            public CardTargetFaction Faction;
            public string Symbol;
        }

        // One entry per faction and range pair because word order differs between languages.
        [Serializable]
        public sealed class TargetLabel
        {
            public CardTargetFaction Faction;
            public CardTargetRange Range;
            public LocalizedString Label;
        }

        [SerializeField] private TMP_Text _text;
        [SerializeField] private TMP_Text _headingText;
        [SerializeField] private GameObject _separator;
        [SerializeField] private LayoutElement _bodyLayout;
        [SerializeField] private Color _allySymbolColor;
        [SerializeField] private Color _enemySymbolColor;
        [SerializeField] private FactionSymbol[] _factionSymbols;
        [SerializeField] private TargetLabel[] _targetLabels;

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
            var faction = Array.Find(_factionSymbols, item => item.Faction == key.Faction);
            var target = Array.Find(_targetLabels, item => item.Faction == key.Faction && item.Range == key.Range);
            if (faction == null || target == null)
                throw new ArgumentOutOfRangeException(nameof(line), key, "Undefined target key.");
            var color = key.Faction == CardTargetFaction.Ally ? _allySymbolColor : _enemySymbolColor;
            _headingText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">"
                + faction.Symbol + "</color> " + target.Label.GetLocalizedString();
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
            foreach (CardTargetFaction faction in Enum.GetValues(typeof(CardTargetFaction)))
            {
                if (_factionSymbols == null || Array.FindAll(_factionSymbols,
                    item => item != null && item.Faction == faction && !string.IsNullOrEmpty(item.Symbol)).Length != 1)
                    throw new InvalidOperationException("Description faction symbols must be complete and unique.");
                foreach (CardTargetRange range in Enum.GetValues(typeof(CardTargetRange)))
                {
                    if (_targetLabels == null || Array.FindAll(_targetLabels,
                        item => item != null && item.Faction == faction && item.Range == range
                            && item.Label != null && !item.Label.IsEmpty).Length != 1)
                        throw new InvalidOperationException("Description target labels must be complete and unique.");
                }
            }
        }
    }
}
