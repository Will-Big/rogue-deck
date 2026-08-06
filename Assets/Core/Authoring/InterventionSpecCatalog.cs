using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring
{
    /// <summary>저작 가능한 개입 스펙 하나의 메타데이터: 표시 이름, 구체 타입, 무인자 팩토리.</summary>
    public sealed class InterventionSpecInfo
    {
        public InterventionSpecInfo(string displayName, Type specType, Func<InterventionSpec> create)
        {
            DisplayName = displayName;
            SpecType = specType;
            Create = create;
        }

        public string DisplayName { get; }
        public Type SpecType { get; }
        public Func<InterventionSpec> Create { get; }
    }

    /// <summary>저작 가능한 개입 스펙의 명시적 목록. JSON 컨버터가 판별자 표를 만들 때와 노트북
    /// 저작 스키마 생성기가 폼을 만들 때 읽는다 — 스펙/핸들러 클래스를 쓰는 것 외에 필요한 유일한
    /// 등록 절차다(AGENTS.md 규칙 9). EffectSpecCatalog와 같은 형태다.
    /// 런타임에서 "실행 가능한 것"을 답하는 InterventionActionRegistry와 짝이며, 둘이 어긋나면
    /// AuthoringValidator가 부팅에서 잡는다.</summary>
    public static class InterventionSpecCatalog
    {
        public static IReadOnlyList<InterventionSpecInfo> All() => new[]
        {
            new InterventionSpecInfo("실행 순서 변경", typeof(ChangeExecutionOrderSpec), () => new ChangeExecutionOrderSpec()),
            new InterventionSpecInfo("실행 순서 교환", typeof(SwapExecutionOrderSpec), () => new SwapExecutionOrderSpec()),
            new InterventionSpecInfo("고정", typeof(LockSpec), () => new LockSpec())
        };
    }
}
