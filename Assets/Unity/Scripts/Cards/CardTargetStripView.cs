using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using UnityEngine;

namespace FateWeaver.Unity
{
    public sealed class CardTargetStripView : MonoBehaviour
    {
        [SerializeField] private RectTransform _firstSlot;
        [SerializeField] private RectTransform _secondSlot;
        [SerializeField] private GameObject _separator;
        [SerializeField] private TargetGlyphView _glyphPrefab;

        public void Configure(TargetGlyphView glyphPrefab)
        {
            _glyphPrefab = glyphPrefab != null ? glyphPrefab : throw new ArgumentNullException(nameof(glyphPrefab));
        }

        public void Bind(IReadOnlyList<CardTargetKey> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (_firstSlot == null || _secondSlot == null || _separator == null || _glyphPrefab == null)
                throw new InvalidOperationException("Target strip is missing an authored reference.");
            GeneratedCardChildren.Clear(_firstSlot);
            GeneratedCardChildren.Clear(_secondSlot);
            bool bothFactions = false;
            for (int i = 1; i < entries.Count; i++)
                bothFactions |= entries[i].Faction != entries[0].Faction;
            _secondSlot.gameObject.SetActive(bothFactions);
            _separator.SetActive(bothFactions);
            if (entries.Count == 0)
            {
                Instantiate(_glyphPrefab, _firstSlot).Bind(null);
                return;
            }
            // Group enemy first without mutating core order; several ranges of one faction
            // share a slot, so no target is silently discarded when content grows.
            foreach (var entry in entries)
            {
                var slot = bothFactions && entry.Faction == CardTargetFaction.Ally ? _secondSlot : _firstSlot;
                Instantiate(_glyphPrefab, slot).Bind(entry);
            }
        }
    }
}
