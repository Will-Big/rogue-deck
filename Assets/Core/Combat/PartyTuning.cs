using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Combat
{
    /// <summary>파티 규모 한계와 생존자 수별 드로우 표. 멤버 HP·생존 충전은 캐릭터 JSON이 원본이다.</summary>
    public sealed class PartyTuning
    {
        public int MinPartySize { get; init; } = 1;
        public int MaxPartySize { get; init; } = 3;
        public IReadOnlyDictionary<int, int> DrawByLivingCount { get; init; }

        public int DrawFor(int livingCount)
        {
            if (DrawByLivingCount == null
                || !DrawByLivingCount.TryGetValue(livingCount, out var drawCount)
                || drawCount <= 0)
            {
                throw new ArgumentException("Draw tuning must contain a positive value for the living party count.");
            }

            return drawCount;
        }

        public static PartyTuning Prototype => new PartyTuning
        {
            DrawByLivingCount = new Dictionary<int, int>
            {
                { 1, 3 },
                { 2, 4 },
                { 3, 5 }
            }
        };
    }
}
