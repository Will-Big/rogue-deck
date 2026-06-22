using System;
using FateWeaver.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>One card widget: art (or side-tinted fallback) + name/initiative + description block,
    /// a selection outline and a lock badge. Bound from a CardPresentation; clicking raises onClick.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public enum SelectionKind { None, Primary, Secondary }

        [SerializeField] private Image _art;
        [SerializeField] private Image _artFallback;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _initiativeText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Image _selectionOutline;
        [SerializeField] private GameObject _lockBadge;
        [SerializeField] private Button _button;

        private static readonly Color OutlineNone = new Color(0f, 0f, 0f, 0f);
        private static readonly Color OutlinePrimary = new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color OutlineSecondary = new Color(0.35f, 0.75f, 0.95f, 1f);
        private static readonly Color EnemyTint = new Color(0.45f, 0.18f, 0.18f, 1f);
        private static readonly Color PlayerTint = new Color(0.22f, 0.28f, 0.36f, 1f);

        public void Bind(CardPresentation data, Action onClick)
        {
            _nameText.text = data.DisplayName;
            _initiativeText.text = data.Initiative.ToString();
            _descriptionText.text = data.Description;

            if (data.Art != null)
            {
                _art.enabled = true;
                _art.sprite = data.Art;
                _artFallback.enabled = false;
            }
            else
            {
                _art.enabled = false;
                _artFallback.enabled = true;
                _artFallback.color = data.Side == Side.Enemy ? EnemyTint : PlayerTint;
            }

            if (_lockBadge != null)
            {
                _lockBadge.SetActive(data.IsLocked);
            }

            _button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                _button.onClick.AddListener(() => onClick());
            }

            SetSelection(SelectionKind.None);
        }

        public void SetSelection(SelectionKind kind)
        {
            _selectionOutline.color =
                kind == SelectionKind.Primary ? OutlinePrimary :
                kind == SelectionKind.Secondary ? OutlineSecondary :
                OutlineNone;
        }
    }
}
