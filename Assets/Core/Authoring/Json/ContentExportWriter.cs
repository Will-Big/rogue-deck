using System.Collections.Generic;
using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;

namespace FateWeaver.Core.Authoring.Json
{
    /// <summary>손으로 쓴 C# 콘텐츠를 StreamingAssets의 JSON으로 1회 변환한다. 원본 목록이 전부
    /// 순수 C#이므로 Unity 없이 돈다 — Unity 쪽 껍데기는 AssetDatabase.Refresh만 한다. 계획 3d가
    /// C# 스펙과 함께 이 라이터를 지운다.
    ///
    /// 캐릭터만 인자로 받는다. 원본인 PartyPrototypeRoster가 FateWeaver.Simulation에 있어
    /// UnityEngine을 참조하지 않는 이 어셈블리에서 닿지 않기 때문이다(규칙 6의 어셈블리 경계).</summary>
    public static class ContentExportWriter
    {
        public const string StarterDeckId = "starter";
        public const string PartyPrototypeDeckId = "party_prototype";
        public const string StarterPoolId = "starter";

        /// <summary>rootDirectory 아래에 Decks·Pools·Characters를 채운다. 개별 파일 경로를
        /// 하드코딩하지 않고 폴더 이름 상수만 쓴다(규칙 2·3).
        ///
        /// **카드도 상태도 더 이상 쓰지 않는다.** 계획 3b가 카드의, 계획 3c가 상태의 원본을 JSON으로
        /// 확정했고 C# 스펙에는 그 값들이 없다 — 여기서 다시 쓰면 저작이 지워지므로 경로 자체를
        /// 없앴다. 남은 셋은 id 목록뿐이라 C# 스펙이 여전히 온전한 원본이다(3d가 정리한다).</summary>
        /// <returns>쓴 파일의 전체 경로. 저작 순서 그대로다.</returns>
        public static IReadOnlyList<string> WriteAll(
            string rootDirectory,
            IReadOnlyList<CharacterSpec> characters)
        {
            var written = new List<string>();

            written.Add(WriteDeck(rootDirectory, StarterDeckId, StarterDeckSpecs.Build()));
            written.Add(WriteDeck(
                rootDirectory, PartyPrototypeDeckId, PartyPrototypeDeckSpecs.Build()));

            written.Add(Write(
                rootDirectory,
                CardContentFiles.PoolsFolderName,
                StarterPoolId,
                new PoolSpec { Id = StarterPoolId, Cards = CardIds(StarterPoolSpecs.Build()) }));

            foreach (var character in characters)
            {
                written.Add(Write(
                    rootDirectory,
                    CardContentFiles.CharactersFolderName,
                    character.Id,
                    character));
            }

            return written;
        }

        private static IEnumerable<CardSpec> AuthoredCardSpecs()
            => StarterPoolSpecs.Build()
                .Concat(StarterDeckSpecs.Build())
                .Concat(PartyPrototypeDeckSpecs.Build());

        private static IEnumerable<CardSpec> DistinctById(IEnumerable<CardSpec> specs)
            => specs.GroupBy(spec => spec.Id).Select(group => group.First());

        private static string WriteDeck(
            string rootDirectory,
            string deckId,
            IReadOnlyList<CardSpec> cards)
            => Write(
                rootDirectory,
                CardContentFiles.DecksFolderName,
                deckId,
                new DeckSpec { Id = deckId, Cards = CardIds(cards) });

        /// <summary>덱·풀은 카드 규칙을 담지 않고 id만 가리킨다(설계 §4.5). 덱은 같은 id가 여러 번
        /// 올 수 있으므로 여기서 중복을 지우지 않는다.</summary>
        private static string[] CardIds(IReadOnlyList<CardSpec> cards)
            => cards.Select(spec => spec.Id).ToArray();

        private static string Write(
            string rootDirectory,
            string folderName,
            string fileStem,
            object spec)
        {
            var directory = Path.Combine(rootDirectory, folderName);
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, fileStem + ".json");
            File.WriteAllText(path, ContentJson.Write(spec) + "\n");
            return path;
        }
    }
}
