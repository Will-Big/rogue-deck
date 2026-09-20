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
            return For(ids[encounterRng.Next(ids.Count)]);
        }

        /// <summary>편성 하나를 이름으로 만든다. 추첨을 거치지 않으므로 편성 후보가 늘어도 결과가
        /// 변하지 않는다 — 특정 편성을 전제하는 테스트가 쓴다.</summary>
        public EncounterSetup For(string battleId)
        {
            var battle = _content.Battles.Get(battleId);
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
