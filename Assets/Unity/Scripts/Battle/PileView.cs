using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>A deck-pile button (draw / discard / full deck — spec §2) that opens a scrollable
    /// popup listing the pile's cards as full CardViews. Contents come from a provider delegate so
    /// the popup always reflects the current session state.</summary>
    public sealed class PileView : MonoBehaviour
    {
        [SerializeField] private string _title;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private GameObject _popup;
        [SerializeField] private RectTransform _popupContent;
        [SerializeField] private CardPrefabCatalog _cardPrefabs;
        [SerializeField] private Button _button;
        [SerializeField] private Button _closeButton;

        [SerializeField] private GameLocalization _localization;
        [SerializeField] private string _titleKey;
        private int _count;
        private void OnEnable() { if (_localization != null) _localization.Changed += RefreshLanguage; }
        private void OnDisable() { if (_localization != null) _localization.Changed -= RefreshLanguage; }
        private void RefreshLanguage() => SetCount(_count);
        private Func<IReadOnlyList<CardPresentation>> _cards;
        private readonly List<CardView> _spawned = new List<CardView>();

        private void Awake()
        {
            _button.onClick.AddListener(Open);
            _closeButton.onClick.AddListener(Close);
        }

        public void Bind(Func<IReadOnlyList<CardPresentation>> cards)
        {
            _cards = cards;
        }

        public void SetCount(int count)
        {
            _count = count;
            _labelText.text = _localization != null ? _localization.UI("ui.pile.count", ("title", GameLocalization.Request(_titleKey)), ("amount", count)) : _title + "\n" + count;
        }

        public void SetInputEnabled(bool value)
        {
            _button.interactable = value;
            if (!value && _popup.activeSelf)
            {
                Close();
            }
        }

        private void Open()
        {
            if (_cards == null)
            {
                return;
            }

            Clear();
            foreach (var data in _cards())
            {
                var view = _cardPrefabs.Create(data, _popupContent);
                view.Bind(data, null);
                _spawned.Add(view);
            }

            _popup.SetActive(true);
        }

        public void Close()
        {
            Clear();
            _popup.SetActive(false);
        }

        private void Clear()
        {
            foreach (var view in _spawned)
            {
                Destroy(view.gameObject);
            }

            _spawned.Clear();
        }
    }
}
