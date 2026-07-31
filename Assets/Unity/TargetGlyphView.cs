using System;
using FateWeaver.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    public sealed class TargetGlyphView : MonoBehaviour
    {
        [SerializeField] private Image _allyDirection;
        [SerializeField] private Image _rail;
        [SerializeField] private Image[] _railSegments = Array.Empty<Image>();
        [SerializeField] private Image[] _diamonds = Array.Empty<Image>();
        [SerializeField] private Image _selfOuter;
        [SerializeField] private Image _selfInner;
        [SerializeField] private Image _enemyDirection;
        [SerializeField] private Image _emptySlash;
        [SerializeField] private Color _allyFill =
            new Color(0.9f, 0.94f, 1f, 0.2f);
        [SerializeField] private Color _enemyFill =
            new Color(0.92f, 0.35f, 0.3f, 1f);

        private Vector3 _authoredScale;
        private bool _scaleCached;

        private void Awake()
        {
            CacheAuthoredScale();
        }

        public void Bind(CardTargetKey? key)
        {
            CacheAuthoredScale();
            if (!key.HasValue)
            {
                ApplyMirror(false);
                SetDirections(false, false);
                SetRail(0);
                SetDiamonds(0, CardTargetFaction.Ally);
                SetActive(_selfOuter, true);
                SetActive(_selfInner, false);
                SetActive(_emptySlash, true);
                return;
            }

            Validate(key.Value);
            var faction = key.Value.Faction;
            var range = key.Value.Range;
            bool isSelf = range == CardTargetRange.Self;
            ApplyMirror(ShouldMirror(faction, range));
            SetDirections(
                faction == CardTargetFaction.Ally,
                faction == CardTargetFaction.Enemy);
            SetActive(_selfOuter, isSelf);
            SetActive(_selfInner, isSelf);
            SetActive(_emptySlash, false);
            SetRail(isSelf ? 0 : RailSegmentCount(range));
            SetDiamonds(isSelf ? 0 : DiamondCount(range), faction);
        }

        private void CacheAuthoredScale()
        {
            if (_scaleCached)
            {
                return;
            }

            _authoredScale = transform.localScale;
            _authoredScale.x = Mathf.Abs(_authoredScale.x);
            _scaleCached = true;
        }

        private void ApplyMirror(bool mirror)
        {
            transform.localScale = new Vector3(
                mirror ? -_authoredScale.x : _authoredScale.x,
                _authoredScale.y,
                _authoredScale.z);
        }

        private void SetDirections(bool ally, bool enemy)
        {
            SetActive(_allyDirection, ally);
            SetActive(_enemyDirection, enemy);
        }

        private void SetRail(int activeSegments)
        {
            SetActive(_rail, activeSegments > 0);
            for (int index = 0; index < _railSegments.Length; index++)
            {
                SetActive(_railSegments[index], index < activeSegments);
            }
        }

        private void SetDiamonds(int activeDiamonds, CardTargetFaction faction)
        {
            bool ally = faction == CardTargetFaction.Ally;
            for (int index = 0; index < _diamonds.Length; index++)
            {
                var diamond = _diamonds[index];
                SetActive(diamond, index < activeDiamonds);
                if (diamond == null)
                {
                    continue;
                }

                diamond.color = ally ? _allyFill : _enemyFill;
                var outline = diamond.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = ally;
                }
            }
        }

        private static bool ShouldMirror(
            CardTargetFaction faction,
            CardTargetRange range)
        {
            if (range == CardTargetRange.Self)
            {
                return faction == CardTargetFaction.Enemy;
            }

            bool canonicalDirectionReversed =
                range == CardTargetRange.BackOne
                || range == CardTargetRange.BackTwo
                || range == CardTargetRange.All;
            return (faction == CardTargetFaction.Enemy)
                   != canonicalDirectionReversed;
        }

        private static int RailSegmentCount(CardTargetRange range)
        {
            switch (range)
            {
                case CardTargetRange.FrontOne:
                case CardTargetRange.BackOne:
                    return 4;
                case CardTargetRange.FrontTwo:
                case CardTargetRange.BackTwo:
                    return 3;
                case CardTargetRange.All:
                    return 5;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(range),
                        range,
                        "Range does not use a rail.");
            }
        }

        private static int DiamondCount(CardTargetRange range)
        {
            switch (range)
            {
                case CardTargetRange.FrontOne:
                case CardTargetRange.BackOne:
                case CardTargetRange.All:
                    return 1;
                case CardTargetRange.FrontTwo:
                case CardTargetRange.BackTwo:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(range),
                        range,
                        "Range does not use unit diamonds.");
            }
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

        private static void SetActive(Component component, bool value)
        {
            if (component != null)
            {
                component.gameObject.SetActive(value);
            }
        }
    }
}
