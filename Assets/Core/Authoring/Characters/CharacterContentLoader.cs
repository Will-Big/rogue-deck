using System.Collections.Generic;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Json;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Characters
{
    /// <summary>캐릭터 로드 한 번의 결과. 실패하면 카탈로그를 내주지 않고 모든 이유를 모아
    /// 보고한다(설계 §4.5).</summary>
    public sealed class CharacterContentLoadResult
    {
        private CharacterContentLoadResult(
            CharacterContentCatalog catalog,
            IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public CharacterContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static CharacterContentLoadResult Ok(CharacterContentCatalog catalog)
            => new CharacterContentLoadResult(catalog, new string[0]);

        public static CharacterContentLoadResult Failed(IReadOnlyList<string> errors)
            => new CharacterContentLoadResult(null, errors);
    }

    /// <summary>캐릭터 콘텐츠 소스 목록을 파싱·검증해 카탈로그로 만든다. 덱 카탈로그를 인자로
    /// 받으므로 부팅 순서의 마지막이다 — 카드 → 덱·풀 → 캐릭터.</summary>
    public static class CharacterContentLoader
    {
        private static readonly string[] RequiredKeys = { "id", "displayName", "deck" };

        public static CharacterContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            DeckContentCatalog decks)
        {
            var errors = new List<string>();
            var characters = new Dictionary<string, CharacterContent>();
            var origin = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                CharacterSpec spec;
                try
                {
                    spec = ContentJson.Read<CharacterSpec>(source.Json);
                }
                catch (JsonException ex)
                {
                    errors.Add(source.Name + ": " + ContentJsonError.Describe(ex));
                    continue;
                }

                if (string.IsNullOrEmpty(spec.Id))
                {
                    errors.Add(source.Name + ": required key 'id' must be a non-empty string.");
                    continue;
                }

                if (origin.TryGetValue(spec.Id, out var first))
                {
                    errors.Add(
                        source.Name + ": duplicate character id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                origin.Add(spec.Id, source.Name);

                var rejected = false;
                if (string.IsNullOrEmpty(spec.DisplayName))
                {
                    errors.Add(source.Name + ": requires a displayName.");
                    rejected = true;
                }

                if (!decks.Contains(spec.Deck))
                {
                    errors.Add(source.Name + ": unknown deck id '" + spec.Deck + "'.");
                    rejected = true;
                }

                if (!rejected)
                {
                    characters.Add(
                        spec.Id, new CharacterContent(spec.Id, spec.DisplayName, spec.Deck));
                }
            }

            if (errors.Count > 0)
            {
                return CharacterContentLoadResult.Failed(errors);
            }

            return CharacterContentLoadResult.Ok(new CharacterContentCatalog(characters));
        }
    }
}
