using System;
using System.Collections.Generic;

namespace FateWeaver.Unity
{
    /// <summary>시각 테스트의 카드 목록과 위치 기반 선택만 관리한다. 전투 덱이 아니다.</summary>
    public sealed class HandFanSandboxState
    {
        private readonly CardPresentation[] _samples;
        private readonly List<CardPresentation> _cards = new List<CardPresentation>();
        private int _cursor;

        public HandFanSandboxState(IReadOnlyList<CardPresentation> samples)
        {
            if (samples == null || samples.Count == 0)
                throw new ArgumentException("At least one sample card is required.", nameof(samples));
            _samples = new CardPresentation[samples.Count];
            for (int i = 0; i < samples.Count; i++) _samples[i] = samples[i];
            Cards = _cards.AsReadOnly();
        }

        public IReadOnlyList<CardPresentation> Cards { get; }
        public int SelectedIndex { get; private set; } = -1;
        public bool CanRemove => SelectedIndex >= 0 && SelectedIndex < _cards.Count;
        public bool CanClear => _cards.Count > 0;

        public void Add()
        {
            _cards.Add(_samples[_cursor]);
            _cursor = (_cursor + 1) % _samples.Length;
        }

        public void Select(int index)
            => SelectedIndex = index >= 0 && index < _cards.Count ? index : -1;

        public void RemoveSelected()
        {
            if (!CanRemove) return;
            _cards.RemoveAt(SelectedIndex);
            SelectedIndex = -1;
        }

        public void Clear()
        {
            _cards.Clear();
            SelectedIndex = -1;
            _cursor = 0;
        }
    }
}
