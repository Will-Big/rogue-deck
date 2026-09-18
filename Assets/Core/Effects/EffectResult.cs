using System;
using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Effects
{
    /// <summary>효과 하나를 수행한 결과. 뒤 효과의 결과 참조(requires·scaleBy)가 효과 ID로 읽는다
    /// (전투 실행 계약 스펙 §5). 수행하지 않은 효과(생략·요건 미달·대상 없음)는 Applied=false다.</summary>
    public sealed record EffectResult(bool Applied, int ConsumedAmount, int DamageDealt)
    {
        public static readonly EffectResult Skipped = new EffectResult(false, 0, 0);

        /// <summary>이 효과가 대상으로 고른 개체의 id, 고른 순서대로. 대상을 고르지 않는 효과는 빈 목록이다.</summary>
        public IReadOnlyList<string> TargetIds { get; init; } = Array.Empty<string>();

        /// <summary>이 효과가 만든 표시 이벤트, 발생 순서대로: 효과 자체의 변화 → 사망·카드 제거 → 직접 반응.</summary>
        public IReadOnlyList<ResolutionEvent> Events { get; init; } = Array.Empty<ResolutionEvent>();

        /// <summary>이 효과의 피해가 상태로 바뀐 단계들.</summary>
        public IReadOnlyList<DamageStep> DamageSteps { get; init; } = Array.Empty<DamageStep>();

        /// <summary>이 효과(와 그 사망 정리)가 낸 규칙 사건, 반응 처리 순서대로 순번이 붙어 있다.</summary>
        public IReadOnlyList<CombatSignal> Signals { get; init; } = Array.Empty<CombatSignal>();
    }
}
