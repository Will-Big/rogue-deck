using System;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using TMPro;
using UnityEngine;

namespace FateWeaver.Unity
{
    public sealed class DescriptionLineView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Color _allySymbolColor;
        [SerializeField] private Color _enemySymbolColor;

        public void Bind(CardDescriptionLine line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (_text == null)
            {
                throw new InvalidOperationException(
                    "DescriptionLineView is missing its TMP text reference.");
            }

            if (!line.Target.HasValue)
            {
                _text.text = line.Text;
                return;
            }

            Color color;
            switch (line.Target.Value.Faction)
            {
                case CardTargetFaction.Ally:
                    color = _allySymbolColor;
                    break;
                case CardTargetFaction.Enemy:
                    color = _enemySymbolColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(line),
                        line.Target.Value.Faction,
                        "Undefined target faction.");
            }

            _text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(color)
                + ">◆</color> " + line.Text;
        }
    }
}
