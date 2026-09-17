using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Rules
{
    /// <summary>combat_rules.json의 저작 모양. 파티 전체에 걸리는 규칙만 담는다 — 멤버별 수치는 캐릭터 JSON.</summary>
    public sealed class CombatRulesSpec
    {
        public int FateEnergyPerTurn;
        public int MinPartySize;
        public int MaxPartySize;
        public Dictionary<int, int> DrawByLivingCount;
        public int RewardChoices;
    }
}
