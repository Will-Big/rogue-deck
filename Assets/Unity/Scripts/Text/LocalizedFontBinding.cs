using TMPro;
using UnityEngine;
using UnityEngine.Localization;
namespace FateWeaver.Unity
{
    /// <summary>Font selection is driven by Unity's localized Asset Table reference.</summary>
    public sealed class LocalizedFontBinding : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private LocalizedTmpFont _font = new LocalizedTmpFont();
        public void Configure(TMP_Text text, UnityEngine.Localization.Tables.TableReference table)
        { _text = text; _font.TableReference = table; _font.TableEntryReference = "font.main"; }
        private void OnEnable() => _font.AssetChanged += Apply;
        private void OnDisable() => _font.AssetChanged -= Apply;
        private void Apply(TMP_FontAsset font) { if (_text != null && font != null) _text.font = font; }
    }
}
