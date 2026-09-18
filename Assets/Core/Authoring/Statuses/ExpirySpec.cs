using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>저작된 만료 정책(JSON "expiry"). 지금은 시점 방문(PhaseVisits)만 저작한다 — 영구·소모는 lifetime으로 적는다.
    /// 세 칸을 항상 쓴다: 기본값(Prepare 등)이 생략되면 뜻이 흐려진다.</summary>
    [Serializable]
    public sealed class ExpirySpec
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public ExpiryMode Mode;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public CombatPhase Phase;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int RemainingVisits;

        public ExpiryPolicy ToPolicy() => ExpiryPolicy.PhaseVisits(Phase, RemainingVisits);

        public IEnumerable<string> Validate()
        {
            if (Mode != ExpiryMode.PhaseVisits)
            {
                yield return "expiry mode must be PhaseVisits (use lifetime for Permanent·UntilConsumed).";
            }

            if (!Enum.IsDefined(typeof(CombatPhase), Phase))
            {
                yield return "expiry phase is not a combat phase.";
            }

            if (RemainingVisits < 1)
            {
                yield return "expiry remainingVisits must be at least 1.";
            }
        }
    }
}
