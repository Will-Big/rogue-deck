using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    public sealed class StatusSpecInfo
    {
        public StatusSpecInfo(StatusKey key, Type specType, Func<StatusSpec> create)
        {
            Key = key;
            SpecType = specType;
            Create = create;
        }

        public StatusKey Key { get; }
        public Type SpecType { get; }
        public Func<StatusSpec> Create { get; }
    }

    /// <summary>저작 가능한 상태의 명시적 목록. 어느 상태가 어떤 스펙 모양을 갖는지의 단일 출처이며
    /// JSON 판별자 표도 여기서 만든다 — 리플렉션 스캔 없음(규칙 9). 모드는 여기 등록된 상태의
    /// 파라미터만 조정할 수 있고 새 키는 추가할 수 없다.</summary>
    public static class StatusSpecCatalog
    {
        public static IReadOnlyList<StatusSpecInfo> All() => new[]
        {
            Simple(StatusKeys.Block),
            Simple(StatusKeys.Contagion),
            Simple(StatusKeys.PoisonDormant),
            Simple(StatusKeys.PoisonStasis),
            Simple(StatusKeys.RewardNullified),
            new StatusSpecInfo(StatusKeys.Poison, typeof(PoisonStatusSpec),
                () => new PoisonStatusSpec { Key = StatusKeyRef.Of(StatusKeys.Poison) }),
            new StatusSpecInfo(StatusKeys.Vulnerable, typeof(MultiplierStatusSpec),
                () => new MultiplierStatusSpec { Key = StatusKeyRef.Of(StatusKeys.Vulnerable) }),
            new StatusSpecInfo(StatusKeys.Weak, typeof(MultiplierStatusSpec),
                () => new MultiplierStatusSpec { Key = StatusKeyRef.Of(StatusKeys.Weak) }),
            new StatusSpecInfo(StatusKeys.Damaged, typeof(MultiplierStatusSpec),
                () => new MultiplierStatusSpec { Key = StatusKeyRef.Of(StatusKeys.Damaged) }),
            new StatusSpecInfo(StatusKeys.Slow, typeof(ExecutionOrderStatusSpec),
                () => new ExecutionOrderStatusSpec { Key = StatusKeyRef.Of(StatusKeys.Slow) }),
            new StatusSpecInfo(StatusKeys.Haste, typeof(ExecutionOrderStatusSpec),
                () => new ExecutionOrderStatusSpec { Key = StatusKeyRef.Of(StatusKeys.Haste) })
        };

        private static StatusSpecInfo Simple(StatusKey key)
            => new StatusSpecInfo(key, typeof(StatusSpec), () => new StatusSpec { Key = StatusKeyRef.Of(key) });
    }
}
