using System;
using FateWeaver.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    public sealed class TargetGlyphView : MonoBehaviour
    {
        [SerializeField] private RectTransform _frontOneVisual;
        [SerializeField] private RectTransform _frontTwoVisual;
        [SerializeField] private RectTransform _backOneVisual;
        [SerializeField] private RectTransform _backTwoVisual;
        [SerializeField] private RectTransform _allVisual;
        [SerializeField] private RectTransform _selfVisual;
        [SerializeField] private RectTransform _emptyVisual;
        [SerializeField] private Color _allyColor;
        [SerializeField] private Color _enemyColor;

        public void Bind(CardTargetKey? key)
        {
            if (!key.HasValue)
            {
                ActivateOnly(_emptyVisual);
                SetMirror(_emptyVisual, false);
                return;
            }

            Validate(key.Value);
            var visual = VisualFor(key.Value.Range);
            ActivateOnly(visual);
            SetMirror(
                visual,
                key.Value.Faction == CardTargetFaction.Enemy);

            var color = key.Value.Faction == CardTargetFaction.Ally
                ? _allyColor
                : _enemyColor;
            foreach (var graphic in visual.GetComponentsInChildren<Graphic>(true))
            {
                graphic.color = color;
            }
        }

        private RectTransform VisualFor(CardTargetRange range)
        {
            switch (range)
            {
                case CardTargetRange.FrontOne:
                    return _frontOneVisual;
                case CardTargetRange.FrontTwo:
                    return _frontTwoVisual;
                case CardTargetRange.BackOne:
                    return _backOneVisual;
                case CardTargetRange.BackTwo:
                    return _backTwoVisual;
                case CardTargetRange.All:
                    return _allVisual;
                case CardTargetRange.Self:
                    return _selfVisual;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(range),
                        range,
                        "Undefined target range.");
            }
        }

        private void ActivateOnly(RectTransform activeVisual)
        {
            if (activeVisual == null)
            {
                throw new InvalidOperationException(
                    "TargetGlyphView is missing an authored visual reference.");
            }

            foreach (var visual in AllVisuals())
            {
                if (visual == null)
                {
                    throw new InvalidOperationException(
                        "TargetGlyphView is missing an authored visual reference.");
                }

                visual.gameObject.SetActive(visual == activeVisual);
            }
        }

        private RectTransform[] AllVisuals()
            => new[]
            {
                _frontOneVisual,
                _frontTwoVisual,
                _backOneVisual,
                _backTwoVisual,
                _allVisual,
                _selfVisual,
                _emptyVisual
            };

        private static void SetMirror(RectTransform visual, bool mirror)
        {
            var scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * (mirror ? -1f : 1f);
            visual.localScale = scale;
        }

        private static void Validate(CardTargetKey key)
        {
            if (key.Faction != CardTargetFaction.Ally
                && key.Faction != CardTargetFaction.Enemy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(key),
                    key,
                    "Undefined target faction.");
            }

            if (!Enum.IsDefined(typeof(CardTargetRange), key.Range))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(key),
                    key,
                    "Undefined target range.");
            }
        }
    }
}
