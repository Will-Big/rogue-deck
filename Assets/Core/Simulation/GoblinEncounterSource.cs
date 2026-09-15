using System;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Simulation
{
    /// <summary>1단계 편성 공급자: 항상 고블린 한 마리. 편성 스트림을 받지만 쓰지 않는다 — 스트림은
    /// 이름표로 파생되므로 안 써도 전투·보상 스트림 값이 변하지 않는다. 2단계에서 편성 JSON 공급자로
    /// 대체된다(전투 노드 설계 2.3).</summary>
    public sealed class GoblinEncounterSource : IEncounterSource
    {
        public EncounterSetup Pick(Random encounterRng)
            => new EncounterSetup(new[]
            {
                new EncounterEnemy(
                    new Enemy(GoblinDeck.EnemyId + "#0", GoblinDeck.EnemyId, GoblinDeck.StartingHp),
                    GoblinDeck.Policy())
            });
    }
}
