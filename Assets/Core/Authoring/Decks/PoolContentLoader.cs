using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Decks
{
    /// <summary>풀 로드 한 번의 결과. 실패하면 카탈로그를 내주지 않고 모든 이유를 모아 보고한다
    /// (설계 §4.5).</summary>
    public sealed class PoolContentLoadResult
    {
        private PoolContentLoadResult(PoolContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public PoolContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static PoolContentLoadResult Ok(PoolContentCatalog catalog)
            => new PoolContentLoadResult(catalog, new string[0]);

        public static PoolContentLoadResult Failed(IReadOnlyList<string> errors)
            => new PoolContentLoadResult(null, errors);
    }

    /// <summary>풀 콘텐츠 소스 목록을 파싱·검증해 카탈로그로 만든다. 덱 로더와 같은 형태이되
    /// 같은 카드 id가 두 번 오는 것을 거부한다 — 풀은 후보 집합이라 중복이 저작 실수다.</summary>
    public static class PoolContentLoader
    {
        private static readonly string[] RequiredKeys = { "id", "cards" };

        public static PoolContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            CardContentCatalog cards)
        {
            var errors = new List<string>();
            var pools = new Dictionary<string, IReadOnlyList<string>>();
            var origin = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                PoolSpec spec;
                try
                {
                    spec = ContentJson.Read<PoolSpec>(source.Json);
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
                        source.Name + ": duplicate pool id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                var cardIds = spec.Cards ?? new string[0];
                var rejected = false;
                var seen = new HashSet<string>();
                foreach (var cardId in cardIds)
                {
                    if (!cards.Cards.ContainsKey(cardId))
                    {
                        errors.Add(source.Name + ": unknown card id '" + cardId + "'.");
                        rejected = true;
                        continue;
                    }

                    if (!seen.Add(cardId))
                    {
                        errors.Add(
                            source.Name + ": duplicate card id '" + cardId + "' in pool.");
                        rejected = true;
                        continue;
                    }

                    if (!ValidateGradeAndTags(source.Name, cardId, cards, errors))
                    {
                        rejected = true;
                    }
                }

                origin.Add(spec.Id, source.Name);
                if (!rejected)
                {
                    pools.Add(spec.Id, cardIds);
                }
            }

            if (errors.Count > 0)
            {
                return PoolContentLoadResult.Failed(errors);
            }

            return PoolContentLoadResult.Ok(new PoolContentCatalog(pools));
        }

        /// <summary>풀 소속 카드에만 걸리는 규칙이다. fixture 카드처럼 풀에 들지 않는 카드는
        /// 등급·태그가 없어도 정상이므로 AuthoringValidator(전역)로 올리지 않는다.
        /// CardPoolAsset.Validate가 하던 판정을 그대로 옮겨 왔다.</summary>
        private static bool ValidateGradeAndTags(
            string sourceName,
            string cardId,
            CardContentCatalog cards,
            List<string> errors)
        {
            var spec = cards.Specs[cardId];
            var ok = true;

            if (spec.Grade == CardGrade.None)
            {
                errors.Add(sourceName + ": card '" + cardId + "' must have a grade.");
                ok = false;
            }

            var tags = spec.Tags ?? new string[0];
            if (tags.Length == 0)
            {
                errors.Add(
                    sourceName + ": card '" + cardId + "' must have at least one tag.");
                return false;
            }

            var seenTags = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < tags.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(tags[i]))
                {
                    errors.Add(
                        sourceName + ": card '" + cardId + "' has an empty tag at index "
                        + i + ".");
                    ok = false;
                    continue;
                }

                if (!seenTags.Add(tags[i]))
                {
                    errors.Add(
                        sourceName + ": card '" + cardId + "' has duplicate tag '"
                        + tags[i] + "'.");
                    ok = false;
                }
            }

            return ok;
        }
    }
}
