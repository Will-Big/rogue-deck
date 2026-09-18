using System;

namespace FateWeaver.Core.Effects
{
    /// <summary>소비 효과가 요구량을 치르는 방식(전투 실행 계약 스펙 §5).</summary>
    public enum ConsumptionMode
    {
        /// <summary>보유량과 요구량 중 작은 만큼 차감한다.</summary>
        UpTo,

        /// <summary>요구량 전부가 있어야 차감한다. 모자라면 차감하지 않는다.</summary>
        Exact
    }

    public static class ConsumptionRule
    {
        /// <summary>실제로 차감할 양. 보유량이 음수이거나 요구량이 1 미만이면 계약 위반이다 —
        /// 로딩 검증이 저작 단계에서 먼저 거부한다.</summary>
        public static int Take(int available, int requested, ConsumptionMode mode)
        {
            if (available < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(available), available, "Available amount must not be negative.");
            }

            if (requested <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requested), requested, "Requested amount must be positive.");
            }

            switch (mode)
            {
                case ConsumptionMode.Exact:
                    return available >= requested ? requested : 0;
                case ConsumptionMode.UpTo:
                    return Math.Min(available, requested);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported consumption mode.");
            }
        }
    }
}
