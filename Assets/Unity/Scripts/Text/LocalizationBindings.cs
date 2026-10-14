using System;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;
using UnityEngine.Localization.Tables;
namespace FateWeaver.Unity
{
    [CreateAssetMenu(menuName = "Fate Weaver/Localization Bindings")]
    public sealed class LocalizationBindings : ScriptableObject
    {
        [SerializeField] private TableReference _content;
        [SerializeField] private TableReference _descriptions;
        [SerializeField] private TableReference _ui;
        public void Configure(TableReference content, TableReference descriptions, TableReference ui)
        { _content = content; _descriptions = descriptions; _ui = ui; }
        public TableReference TableFor(TranslationTable role)
        {
            switch (role)
            {
                case TranslationTable.Content: return _content;
                case TranslationTable.Descriptions: return _descriptions;
                case TranslationTable.UI: return _ui;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }
    }
}
