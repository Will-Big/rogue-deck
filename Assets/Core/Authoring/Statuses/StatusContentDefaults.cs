using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>저작된 상태의 기본값. 이 게임의 상태 규칙이 실제로 사는 곳이며, 내보내기와 헤드리스
    /// 폴백이 **둘 다 여기서 읽는다** — 값이 두 곳에 있으면 어긋날 수 있기 때문이다.
    /// 후속 계획에서 JSON이 진실의 원천이 되면 이 클래스는 제거된다.</summary>
    public static class StatusContentDefaults
    {
        public static IReadOnlyList<StatusSpec> Specs() => new[]
        {
            Simple(StatusKeys.Block, StatusLifetimeKind.ThisTurn),
            Simple(StatusKeys.Contagion, StatusLifetimeKind.Turns),
            Simple(StatusKeys.PoisonDormant, StatusLifetimeKind.ThisTurn),
            Simple(StatusKeys.PoisonStasis, StatusLifetimeKind.ThisTurn),
            Simple(StatusKeys.RewardNullified, StatusLifetimeKind.UntilConsumed),
            new PoisonStatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Poison),
                Lifetime = StatusLifetimeKind.Permanent,
                GrowthPerTurn = 1
            },
            // 취약 150 = 받는 피해 +50%, 약화 75 = 주는 피해 -25%, 손상 75 = 방어 획득 -25%.
            Multiplier(StatusKeys.Vulnerable, 150),
            Multiplier(StatusKeys.Weak, 75),
            Multiplier(StatusKeys.Damaged, 75),
            Order(StatusKeys.Slow, 2),
            Order(StatusKeys.Haste, -2)
        };

        /// <summary>파일 없이 도는 헤드리스 테스트와 하니스가 쓰는 카탈로그.
        /// 내보낸 JSON과 같은 Specs()에서 만들어지므로 둘이 어긋날 수 없다.</summary>
        public static StatusContentCatalog Catalog()
        {
            var specs = new Dictionary<StatusKey, StatusSpec>();
            foreach (var spec in Specs())
            {
                specs.Add(spec.Key.ToKey(), spec);
            }

            return new StatusContentCatalog(specs);
        }

        /// <summary>true면 이 키는 콘텐츠(수명·세기)를 저작할 수 있다 — apply_status처럼 핸들러가
        /// StatusContentCatalog를 읽는 효과가 이걸로 저작 시점에 걸러야, 등록만 되고 콘텐츠가 없는
        /// 상태를 카드가 가리켜 해결 시점에 KeyNotFoundException으로 죽는 일이 없다.</summary>
        public static bool HasContent(StatusKey key)
        {
            foreach (var spec in Specs())
            {
                if (spec.Key.ToKey() == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static StatusSpec Simple(StatusKey key, StatusLifetimeKind lifetime)
            => new StatusSpec { Key = StatusKeyRef.Of(key), Lifetime = lifetime };

        private static StatusSpec Multiplier(StatusKey key, int percent)
            => new MultiplierStatusSpec
            {
                Key = StatusKeyRef.Of(key),
                Lifetime = StatusLifetimeKind.Turns,
                MultiplierPercent = percent
            };

        private static StatusSpec Order(StatusKey key, int delta)
            => new ExecutionOrderStatusSpec
            {
                Key = StatusKeyRef.Of(key),
                Lifetime = StatusLifetimeKind.Turns,
                ExecutionOrderDelta = delta
            };
    }
}
