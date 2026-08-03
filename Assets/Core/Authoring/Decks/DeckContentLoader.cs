using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Decks
{
    /// <summary>덱 로드 한 번의 결과. 실패하면 카탈로그를 내주지 않고 모든 이유를 모아 보고한다
    /// (설계 §4.5).</summary>
    public sealed class DeckContentLoadResult
    {
        private DeckContentLoadResult(DeckContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public DeckContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static DeckContentLoadResult Ok(DeckContentCatalog catalog)
            => new DeckContentLoadResult(catalog, new string[0]);

        public static DeckContentLoadResult Failed(IReadOnlyList<string> errors)
            => new DeckContentLoadResult(null, errors);
    }

    /// <summary>덱 콘텐츠 소스 목록을 파싱·검증해 카탈로그로 만든다. CardContentLoader와 같은
    /// 형태이되 카드 카탈로그를 인자로 받는다 — 없는 카드 id를 가리키는 덱을 거부하려면 카드가
    /// 먼저 로드되어 있어야 하고, 그래서 부팅 순서가 카드 → 덱·풀 → 캐릭터로 정해진다.</summary>
    public static class DeckContentLoader
    {
        /// <summary>생략되면 조용히 기본값이 들어가서는 안 되는 키. cards가 빠진 덱이 말없이 빈
        /// 덱이 되는 사고를 막는다.</summary>
        private static readonly string[] RequiredKeys = { "id", "cards" };

        public static DeckContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            CardContentCatalog cards)
        {
            var errors = new List<string>();
            var decks = new Dictionary<string, IReadOnlyList<string>>();
            var origin = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                DeckSpec spec;
                try
                {
                    spec = ContentJson.Read<DeckSpec>(source.Json);
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
                        source.Name + ": duplicate deck id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                var cardIds = spec.Cards ?? new string[0];
                var unknown = false;
                foreach (var cardId in cardIds)
                {
                    if (!cards.Cards.ContainsKey(cardId))
                    {
                        errors.Add(source.Name + ": unknown card id '" + cardId + "'.");
                        unknown = true;
                    }
                }

                origin.Add(spec.Id, source.Name);
                if (!unknown)
                {
                    decks.Add(spec.Id, cardIds);
                }
            }

            if (errors.Count > 0)
            {
                return DeckContentLoadResult.Failed(errors);
            }

            return DeckContentLoadResult.Ok(new DeckContentCatalog(decks));
        }
    }
}
