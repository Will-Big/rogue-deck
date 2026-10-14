using UnityEngine;
namespace FateWeaver.Unity
{
    /// <summary>Re-renders bound card text without changing card input or selection state.</summary>
    public sealed class CardLocalizationView : MonoBehaviour
    {
        [SerializeField] private CardView _view;
        [SerializeField] private CardStatusTooltipView _tooltipPrefab;
        [SerializeField] private CardStatusIconView _lockIcon;
        private CardStatusTooltipView _tooltip;
        private CardPresentation _source;
        private GameLocalization _localization;
        public void Bind(CardPresentation source)
        {
            if (_localization != null) _localization.Changed -= Refresh;
            _source = source;
            _localization = source.TextSource;
            if (_localization != null)
            {
                _localization.Changed += Refresh;
                if (source.IsLocked && _lockIcon != null && _tooltipPrefab != null)
                {
                    if (_tooltip == null)
                    {
                        var canvas = GetComponentInParent<Canvas>();
                        if (canvas == null) return;
                        _tooltip = Instantiate(_tooltipPrefab, canvas.transform);
                        _tooltip.gameObject.SetActive(false);
                    }
                    var sprite = CardStatusIconSprites.LockIconSprite();
                    _lockIcon.Bind(new CardStatusPresentation("lock", sprite,
                        _localization.Name("card_status.lock.name"), _localization.Name("card_status.lock.description")), _tooltip);
                    _lockIcon.Localize(_localization);
                }
            }
        }
        private void Refresh() { if (_view != null && _localization != null) _view.RefreshLocalizedText(_source.Relocalized()); }
        private void OnDestroy() { if (_localization != null) _localization.Changed -= Refresh; if (_tooltip != null) Destroy(_tooltip.gameObject); }
    }
}
