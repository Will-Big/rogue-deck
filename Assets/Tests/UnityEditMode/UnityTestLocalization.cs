using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>Edit Mode에서는 GameLocalization.Awake가 돌지 않아 표를 비동기로 불러오지 못한다.
    /// 프로덕션과 같은 표를 에디터에서 동기로 읽어 렌더러를 꽂아 준다.</summary>
    public static class UnityTestLocalization
    {
        public static GameLocalization AddTo(GameObject host, string localeCode = "ko")
        {
            var localization = host.GetComponent<GameLocalization>() ?? host.AddComponent<GameLocalization>();
            Load(localization, localeCode);
            return localization;
        }

        public static void Load(GameLocalization localization, string localeCode = "ko")
        {
            var tables = new Dictionary<TranslationTable, StringTable>();
            foreach (TranslationTable role in Enum.GetValues(typeof(TranslationTable)))
            {
                var table = LocalizationEditorSettings.GetStringTableCollection("FW_" + role).GetTable(localeCode) as StringTable;
                Assert.IsNotNull(table, "Missing " + localeCode + " table for " + role);
                tables[role] = table;
            }
            typeof(GameLocalization).GetProperty(nameof(GameLocalization.Renderer))
                .SetValue(localization, new UnityLocalizedTextRenderer(tables));
        }
    }
}
