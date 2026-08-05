using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>The card back revealed during the placement flight flip: a gold frame border
    /// with the card's illustration (or side-tinted fallback) inside. A permanent hidden child
    /// of CardView.prefab; swap the frame visuals here when the real card-back design lands.</summary>
    public sealed class CardBackView : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Image _artFallback;

        public void Bind(Sprite art, Color fallbackTint)
        {
            if (art != null)
            {
                _art.enabled = true;
                _art.sprite = art;
                _art.preserveAspect = true;
                _artFallback.enabled = false;
                return;
            }

            _art.enabled = false;
            _artFallback.enabled = true;
            _artFallback.color = fallbackTint;
        }
    }
}
