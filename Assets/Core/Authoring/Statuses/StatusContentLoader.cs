using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Status;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Statuses
{
    public sealed class StatusContentLoadResult
    {
        private StatusContentLoadResult(StatusContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public StatusContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static StatusContentLoadResult Ok(StatusContentCatalog catalog)
            => new StatusContentLoadResult(catalog, new string[0]);

        public static StatusContentLoadResult Failed(IReadOnlyList<string> errors)
            => new StatusContentLoadResult(null, errors);
    }

    /// <summary>상태 소스를 파싱·검증해 카탈로그로 만든다. 카드 로더와 같은 형태이며 파일을 직접
    /// 읽지 않는다. 등록된 상태가 하나라도 저작되지 않으면 거부한다 — 빠진 상태를 거는 카드가
    /// 조용히 잘못 동작하는 것을 막는다.</summary>
    public static class StatusContentLoader
    {
        public static StatusContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            AuthoringContext context)
        {
            var errors = new List<string>();
            var specs = new Dictionary<StatusKey, StatusSpec>();
            var origin = new Dictionary<StatusKey, string>();

            foreach (var source in sources)
            {
                StatusSpec spec;
                try
                {
                    spec = ContentJson.Read<StatusSpec>(source.Json);
                }
                catch (JsonException ex)
                {
                    errors.Add(source.Name + ": " + ex.Message);
                    continue;
                }

                var key = spec.Key.ToKey();
                if (origin.TryGetValue(key, out var first))
                {
                    errors.Add(
                        source.Name + ": duplicate status '" + key.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                foreach (var error in spec.Validate(context))
                {
                    errors.Add(source.Name + ": " + error);
                }

                origin.Add(key, source.Name);
                specs.Add(key, spec);
            }

            foreach (var key in context.RegisteredStatusKeys)
            {
                if (!specs.ContainsKey(key))
                {
                    errors.Add("Status '" + key.Id + "' is registered but has no authored content.");
                }
            }

            return errors.Count > 0
                ? StatusContentLoadResult.Failed(errors)
                : StatusContentLoadResult.Ok(new StatusContentCatalog(specs));
        }
    }
}
