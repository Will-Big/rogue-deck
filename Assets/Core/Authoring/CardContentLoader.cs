using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>로드 한 번의 결과. 실패하면 카탈로그를 내주지 않고 모든 이유를 모아 보고한다
    /// (설계 §4.5: 실패한 모드 콘텐츠는 로드를 거부하며 이유를 보고한다).</summary>
    public sealed class CardContentLoadResult
    {
        private CardContentLoadResult(CardContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public CardContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static CardContentLoadResult Ok(CardContentCatalog catalog)
            => new CardContentLoadResult(catalog, new string[0]);

        public static CardContentLoadResult Failed(IReadOnlyList<string> errors)
            => new CardContentLoadResult(null, errors);
    }

    /// <summary>콘텐츠 소스 목록을 파싱·검증해 카탈로그로 만든다. 파일 I/O는 CardContentFiles가
    /// 맡으므로 이 클래스는 순수하고 헤드리스 테스트가 그대로 돌린다.</summary>
    public static class CardContentLoader
    {
        /// <summary>생략되면 조용히 기본값이 들어가서는 안 되는 키. side가 빠진 카드가 말없이
        /// 플레이어 카드가 되는 사고를 막는다.</summary>
        private static readonly string[] RequiredKeys = { "id", "name", "side", "category" };

        public static CardContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            AuthoringContext context)
        {
            var errors = new List<string>();
            var specs = new List<CardSpec>();
            var origin = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                CardSpec spec;
                try
                {
                    spec = ContentJson.Read<CardSpec>(source.Json);
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
                        source.Name + ": duplicate card id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                origin.Add(spec.Id, source.Name);
                specs.Add(spec);
            }

            foreach (var error in AuthoringValidator.Validate(specs, context))
            {
                errors.Add(error);
            }

            if (errors.Count > 0)
            {
                return CardContentLoadResult.Failed(errors);
            }

            var cards = new Dictionary<string, CardDefinition>();
            foreach (var spec in specs)
            {
                cards.Add(spec.Id, CardSpecMapper.ToDefinition(spec));
            }

            return CardContentLoadResult.Ok(new CardContentCatalog(cards));
        }
    }
}
