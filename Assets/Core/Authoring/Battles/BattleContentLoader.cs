using System.Collections.Generic;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Authoring.Json;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Battles
{
    public sealed class BattleContentLoadResult
    {
        private BattleContentLoadResult(BattleContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public BattleContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static BattleContentLoadResult Ok(BattleContentCatalog catalog)
            => new BattleContentLoadResult(catalog, new string[0]);

        public static BattleContentLoadResult Failed(IReadOnlyList<string> errors)
            => new BattleContentLoadResult(null, errors);
    }

    /// <summary>편성 콘텐츠 소스를 파싱·검증한다. 적 카탈로그를 받으므로 적 뒤에 온다.</summary>
    public static class BattleContentLoader
    {
        private static readonly string[] RequiredKeys = { "id", "enemies" };

        public static BattleContentLoadResult Load(IEnumerable<CardContentSource> sources, EnemyContentCatalog enemies)
        {
            var errors = new List<string>();
            var battles = new Dictionary<string, BattleDefinition>();
            var origin = new Dictionary<string, string>();
            var count = 0;

            foreach (var source in sources)
            {
                count++;
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                BattleSpec spec;
                try
                {
                    spec = ContentJson.Read<BattleSpec>(source.Json);
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
                        source.Name + ": duplicate battle id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                origin.Add(spec.Id, source.Name);

                var ids = spec.Enemies ?? new string[0];
                var rejected = false;

                // 같은 적 id가 여러 번 나와도 된다 — 편성 공급자가 전투 안 id(goblin#0, goblin#1)로 가른다.
                if (ids.Length == 0)
                {
                    errors.Add(source.Name + ": at least one enemy is required.");
                    rejected = true;
                }

                foreach (var enemyId in ids)
                {
                    if (!enemies.Contains(enemyId))
                    {
                        errors.Add(source.Name + ": unknown enemy id '" + enemyId + "'.");
                        rejected = true;
                    }
                }

                if (!rejected)
                {
                    battles.Add(spec.Id, new BattleDefinition(spec.Id, ids));
                }
            }

            if (count == 0)
            {
                errors.Add("Battles: at least one battle is required.");
            }

            if (errors.Count > 0)
            {
                return BattleContentLoadResult.Failed(errors);
            }

            return BattleContentLoadResult.Ok(new BattleContentCatalog(battles));
        }
    }
}
