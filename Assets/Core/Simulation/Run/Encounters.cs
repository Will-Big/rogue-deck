using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Enemies;

namespace FateWeaver.Simulation.Run
{
    /// <summary>편성 스트림을 받아 이번 전투의 적마다 (적, 정책) 쌍을 새로 만든다. 정책은 상태를
    /// 가지므로(ShuffleBagPolicy) 매 호출 새 인스턴스를 돌려줘야 한다.</summary>
    public interface IEncounterSource
    {
        EncounterSetup Pick(Random encounterRng);
    }

    public sealed class EncounterSetup
    {
        public EncounterSetup(IReadOnlyList<EncounterEnemy> enemies)
        {
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        }

        public IReadOnlyList<EncounterEnemy> Enemies { get; }
    }
}
