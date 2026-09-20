using System;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    /// <summary>편성을 이름으로 고정하는 테스트용 공급자. 추첨을 거치지 않으므로 편성 후보가 늘어도
    /// 결과가 변하지 않는다 — 특정 편성을 전제하는 테스트가 쓴다.</summary>
    public sealed class FixedBattleEncounter : IEncounterSource
    {
        private readonly ContentEncounterSource _source;
        private readonly string _battleId;

        public FixedBattleEncounter(ContentEncounterSource source, string battleId)
        {
            _source = source;
            _battleId = battleId;
        }

        public EncounterSetup Pick(Random encounterRng) => _source.For(_battleId);
    }
}
