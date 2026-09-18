using System;
using System.Collections.Generic;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Combat
{
    /// <summary>카드 한 장을 실행하는 동안의 값: 시작 때 한 번 고정한 조건 결과와 효과 ID별 결과표
    /// (전투 실행 계약 스펙 §2·§5). 카드마다 새로 만들며 카드가 끝나면 버린다.</summary>
    public sealed class CardExecutionContext
    {
        private readonly Dictionary<string, EffectResult> _results = new(StringComparer.Ordinal);

        public CardExecutionContext(ExecutionCardInstance card, ConditionTier startTier)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            StartTier = startTier;
        }

        public ExecutionCardInstance Card { get; }

        /// <summary>카드 시작 조건의 결과. 카드 도중 상태가 바뀌어도 다시 평가하지 않는다.</summary>
        public ConditionTier StartTier { get; }

        /// <summary>효과 결과를 기록한다. ID 없는 효과는 참조될 수 없으므로 기록하지 않는다.
        /// 같은 ID를 두 번 기록하면 계약 위반이다(로딩 검증이 중복 ID를 먼저 거부한다).</summary>
        public void Record(string effectId, EffectResult result)
        {
            if (string.IsNullOrEmpty(effectId))
            {
                return;
            }

            if (_results.ContainsKey(effectId))
            {
                throw new InvalidOperationException("Effect result '" + effectId + "' was already recorded.");
            }

            _results.Add(effectId, result ?? throw new ArgumentNullException(nameof(result)));
        }

        /// <summary>앞 효과의 결과. 아직 기록되지 않은 ID는 계약 위반이다 — 로딩 검증이 앞 효과만
        /// 참조하도록 막으므로 여기 오면 구현 오류다.</summary>
        public EffectResult Get(string effectId)
        {
            if (effectId == null || !_results.TryGetValue(effectId, out var result))
            {
                throw new InvalidOperationException("No effect result recorded for '" + effectId + "'.");
            }

            return result;
        }
    }
}
