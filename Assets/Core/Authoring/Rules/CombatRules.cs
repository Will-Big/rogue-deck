using System;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Authoring.Rules
{
    /// <summary>검증된 전투 규칙 묶음. 세션은 Party만, 노드는 운명력·보상 장수까지 쓴다(설계 결정 15).</summary>
    public sealed class CombatRules
    {
        public CombatRules(PartyTuning party, int fateEnergyPerTurn, int rewardChoices)
        {
            Party = party ?? throw new ArgumentNullException(nameof(party));
            FateEnergyPerTurn = fateEnergyPerTurn;
            RewardChoices = rewardChoices;
        }

        public PartyTuning Party { get; }
        public int FateEnergyPerTurn { get; }
        public int RewardChoices { get; }
    }
}
