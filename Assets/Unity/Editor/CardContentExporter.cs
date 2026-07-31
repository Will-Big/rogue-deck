using System.Collections.Generic;
using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>손으로 쓴 C# 카드 스펙을 StreamingAssets의 JSON으로 1회 변환한다. 변환이 끝나고
    /// 계획 2가 소비자를 JSON으로 옮기면 이 익스포터와 C# 스펙은 함께 제거된다.</summary>
    public static class CardContentExporter
    {
        private const string OutputDirectory = "Assets/StreamingAssets/Content/Cards";

        [MenuItem("Fate Weaver/Export Card Content to JSON")]
        public static void ExportAll()
        {
            Directory.CreateDirectory(OutputDirectory);

            var written = 0;
            foreach (var spec in DistinctById(AuthoredSpecs()))
            {
                var path = Path.Combine(OutputDirectory, spec.Id + ".json");
                File.WriteAllText(path, ContentJson.Write(spec) + "\n");
                written++;
            }

            AssetDatabase.Refresh();
            Debug.Log("Exported " + written + " cards to " + OutputDirectory);
        }

        private static IEnumerable<CardSpec> AuthoredSpecs()
            => StarterPoolSpecs.Build()
                .Concat(StarterDeckSpecs.AllAuthored())
                .Concat(PartyPrototypeDeckSpecs.Build());

        private static IEnumerable<CardSpec> DistinctById(IEnumerable<CardSpec> specs)
            => specs.GroupBy(spec => spec.Id).Select(group => group.First());
    }
}
