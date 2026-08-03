using FateWeaver.Core.Authoring.Json;
using FateWeaver.Simulation;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>내보내기의 Unity 껍데기. 파일 쓰기는 코어의 ContentExportWriter가 하고 여기서는
    /// AssetDatabase.Refresh만 한다 — 그래서 전용 워크트리에서 헤드리스로도 내보낼 수 있다
    /// (규칙 17). 계획 3d가 라이터와 이 껍데기를 함께 제거한다.</summary>
    public static class CardContentExporter
    {
        private const string OutputRoot = "Assets/StreamingAssets/Content";

        [MenuItem("Fate Weaver/Export Card Content to JSON")]
        public static void ExportAll()
        {
            var written = ContentExportWriter.WriteAll(
                OutputRoot, PartyPrototypeCharacterSpecs.Build());

            AssetDatabase.Refresh();
            Debug.Log("Exported " + written.Count + " content files to " + OutputRoot);
        }
    }
}
