using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    public sealed class CardStatusIconView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Image _icon;

        private CardStatusPresentation _data;
        private CardStatusTooltipView _tooltip;
        private bool _isBound;
        private GameLocalization _localization;
        private string Title => _localization != null ? _localization.Name("card_status." + _data.Key + ".name") : _data.Title;
        private string Description => _localization != null ? _localization.Name("card_status." + _data.Key + ".description") : _data.Description;
        public void Localize(GameLocalization localization)
        {
            if (_localization != null) _localization.Changed -= RefreshLanguage;
            _localization = localization;
            if (_localization != null) _localization.Changed += RefreshLanguage;
        }
        private void RefreshLanguage() { if (_isBound) _tooltip.RefreshText(this, Title, Description); }
        private void OnDestroy() { if (_localization != null) _localization.Changed -= RefreshLanguage; }

        public void Bind(
            CardStatusPresentation data,
            CardStatusTooltipView tooltip)
        {
            if (_icon == null)
            {
                throw new InvalidOperationException(
                    "CardStatusIconView is missing its icon Image reference.");
            }

            if (tooltip == null)
            {
                throw new ArgumentNullException(nameof(tooltip));
            }

            if (data.Icon == null
                || string.IsNullOrWhiteSpace(data.Key)
                || string.IsNullOrWhiteSpace(data.Title)
                || string.IsNullOrWhiteSpace(data.Description))
            {
                throw new ArgumentException(
                    "Card status presentation is incomplete.",
                    nameof(data));
            }

            _tooltip?.Hide(this);
            _data = data;
            _tooltip = tooltip;
            _icon.sprite = data.Icon;
            _icon.preserveAspect = true;
            _icon.raycastTarget = true;
            _icon.raycastTarget = true;
            _isBound = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isBound)
            {
                return;
            }

            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            _tooltip.Show(
                this,
                Title,
                Description,
                eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isBound)
            {
                _tooltip.Hide(this);
            }
        }

        private void OnDisable()
        {
            if (_isBound)
            {
                _tooltip.Hide(this);
            }
        }
    }
}
