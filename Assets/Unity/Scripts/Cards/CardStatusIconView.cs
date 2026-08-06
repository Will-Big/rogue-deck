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
                _data.Title,
                _data.Description,
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
