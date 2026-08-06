using System;
using TMPro;
using UnityEngine;

namespace FateWeaver.Unity
{
    public sealed class CardStatusTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Vector2 _screenOffset = new Vector2(12f, -12f);

        private CardStatusIconView _owner;

        public void Show(
            CardStatusIconView owner,
            string title,
            string description,
            Vector2 screenPosition)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (_titleText == null || _descriptionText == null)
            {
                throw new InvalidOperationException(
                    "CardStatusTooltipView is missing a TMP text reference.");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException(
                    "Tooltip title is required.", nameof(title));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException(
                    "Tooltip description is required.", nameof(description));
            }

            _owner = owner;
            _titleText.text = title;
            _descriptionText.text = description;
            transform.position = screenPosition + _screenOffset;
            gameObject.SetActive(true);
        }

        public void Hide(CardStatusIconView owner)
        {
            if (owner == null || owner != _owner)
            {
                return;
            }

            _owner = null;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _owner = null;
        }
    }
}
