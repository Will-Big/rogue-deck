using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using UnityEngine;

namespace FateWeaver.Unity
{
    [CreateAssetMenu(
        menuName = "Fate Weaver/Card Prefab Catalog",
        fileName = "CardPrefabCatalog")]
    public sealed class CardPrefabCatalog : ScriptableObject
    {
        [SerializeField] private CardView _executionCard;
        [SerializeField] private CardView _interventionCard;
        [SerializeField] private TargetGlyphView _targetGlyph;
        [SerializeField] private DescriptionLineView _descriptionLine;

        internal TargetGlyphView TargetGlyphPrefab => _targetGlyph;
        internal DescriptionLineView DescriptionLinePrefab => _descriptionLine;

        public CardView Resolve(CardCategory category)
        {
            switch (category)
            {
                case CardCategory.Execution:
                    return _executionCard;
                case CardCategory.Intervention:
                    return _interventionCard;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(category),
                        category,
                        "Undefined card category.");
            }
        }

        public CardView Create(CardPresentation presentation, RectTransform parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var prefab = Resolve(presentation.Category);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"No card prefab is assigned for category '{presentation.Category}'.");
            }

            var view = Instantiate(prefab, parent);
            view.Configure(this);
            return view;
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            ValidateFullCard(
                _executionCard,
                CardCategory.Execution,
                "execution card",
                errors);
            ValidateFullCard(
                _interventionCard,
                CardCategory.Intervention,
                "intervention card",
                errors);
            if (_targetGlyph == null)
            {
                errors.Add("Card prefab catalog target glyph reference is missing.");
            }

            if (_descriptionLine == null)
            {
                errors.Add("Card prefab catalog description line reference is missing.");
            }

            return errors;
        }

        public void ValidateOrThrow()
        {
            var errors = Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Card prefab catalog validation failed:\n"
                    + string.Join("\n", errors));
            }
        }

        private static void ValidateFullCard(
            CardView prefab,
            CardCategory expectedCategory,
            string label,
            ICollection<string> errors)
        {
            if (prefab == null)
            {
                errors.Add($"Card prefab catalog {label} reference is missing.");
                return;
            }

            if (prefab.PrefabCategory != expectedCategory)
            {
                errors.Add(
                    $"Card prefab catalog {label} category mismatch: "
                    + $"expected {expectedCategory}, got {prefab.PrefabCategory}.");
            }
        }
    }
}
