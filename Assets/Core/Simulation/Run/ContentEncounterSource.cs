using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Enemies;

namespace FateWeaver.Simulation.Run
{
    /// <summary>편성 스트림으로 저작된 편성 후보 하나를 골라, 적마다 (적, 새 정책) 쌍을 만든다.
    /// 편성이 하나뿐이어도 스트림을 한 번 쓴다 — 스트림은 이름표로 파생되므로 전투·보상 값은 변하지
    /// 않는다(전투 노드 설계 1.2·2.5).</summary>
    public sealed class ContentEncounterSource : IEncounterSource
    {
        private readonly GameContent _content;
        private readonly EnemyPolicyRegistry _policies;

        public ContentEncounterSource(GameContent content, EnemyPolicyRegistry policies)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        }

        public EncounterSetup Pick(Random encounterRng)
        {
            var ids = _content.Battles.Ids;
            var battle = _content.Battles.Get(ids[encounterRng.Next(ids.Count)]);
            var enemies = new List<EncounterEnemy>(battle.Enemies.Count);
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                var definition = _content.Enemies.Get(battle.Enemies[i]);
                enemies.Add(new EncounterEnemy(
                    new Enemy(definition.Id + "#" + i, definition.Id, definition.MaxHp),
                    _policies.Create(definition.Policy, definition.Bundles)));
            }

            return new EncounterSetup(enemies);
        }
    }
}
