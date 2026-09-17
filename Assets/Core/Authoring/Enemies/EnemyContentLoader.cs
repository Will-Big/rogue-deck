using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Enemies;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Enemies
{
    /// <summary>적 로드 한 번의 결과. 실패하면 카탈로그를 내주지 않고 모든 이유를 모아 보고한다.</summary>
    public sealed class EnemyContentLoadResult
    {
        private EnemyContentLoadResult(EnemyContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public EnemyContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static EnemyContentLoadResult Ok(EnemyContentCatalog catalog)
            => new EnemyContentLoadResult(catalog, new string[0]);

        public static EnemyContentLoadResult Failed(IReadOnlyList<string> errors)
            => new EnemyContentLoadResult(null, errors);
    }

    /// <summary>적 콘텐츠 소스를 파싱·검증해 카탈로그로 만든다. 카드 카탈로그와 정책 레지스트리
    /// (AuthoringContext)를 받으므로 카드 뒤에 온다. 정책 키 오타가 카드 효과 키 오타와 같은 부팅
    /// 오류 목록에 잡힌다(전투 노드 설계 결정 14).</summary>
    public static class EnemyContentLoader
    {
        private static readonly string[] RequiredKeys = { "id", "displayName", "maxHp", "policy", "bundles" };

        public static EnemyContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            CardContentCatalog cards,
            AuthoringContext context)
        {
            var errors = new List<string>();
            var enemies = new Dictionary<string, EnemyDefinition>();
            var origin = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                EnemySpec spec;
                try
                {
                    spec = ContentJson.Read<EnemySpec>(source.Json);
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
                        source.Name + ": duplicate enemy id '" + spec.Id
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

                if (spec.MaxHp <= 0)
                {
                    errors.Add(source.Name + ": maxHp must be positive.");
                    rejected = true;
                }

                var policy = new EnemyPolicyKey(spec.Policy);
                if (!context.HasEnemyPolicy(policy))
                {
                    errors.Add(source.Name + ": unknown enemy policy '" + spec.Policy + "'.");
                    rejected = true;
                }

                var bundles = new List<EnemyCardBundle>();
                if (spec.Bundles == null || spec.Bundles.Length == 0)
                {
                    errors.Add(source.Name + ": requires at least one bundle.");
                    rejected = true;
                }
                else
                {
                    for (int i = 0; i < spec.Bundles.Length; i++)
                    {
                        var ids = spec.Bundles[i];
                        if (ids == null || ids.Length == 0)
                        {
                            errors.Add(source.Name + ": bundle " + i + " is empty.");
                            rejected = true;
                            continue;
                        }

                        var bundleCards = new List<CardDefinition>(ids.Length);
                        foreach (var cardId in ids)
                        {
                            if (cardId == null || !cards.Cards.TryGetValue(cardId, out var card))
                            {
                                errors.Add(source.Name + ": unknown card id '" + cardId + "' in bundle " + i + ".");
                                rejected = true;
                                continue;
                            }

                            if (card.Side != Side.Enemy)
                            {
                                errors.Add(source.Name + ": card '" + cardId + "' in bundle " + i + " is not an enemy card.");
                                rejected = true;
                                continue;
                            }

                            bundleCards.Add(card);
                        }

                        bundles.Add(new EnemyCardBundle(bundleCards));
                    }
                }

                if (!rejected)
                {
                    enemies.Add(spec.Id, new EnemyDefinition(spec.Id, spec.DisplayName, spec.MaxHp, policy, bundles));
                }
            }

            if (errors.Count > 0)
            {
                return EnemyContentLoadResult.Failed(errors);
            }

            return EnemyContentLoadResult.Ok(new EnemyContentCatalog(enemies));
        }
    }
}
