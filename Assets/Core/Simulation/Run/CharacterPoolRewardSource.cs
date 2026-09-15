using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Simulation.Run
{
    /// <summary>캐릭터 JSON의 pool이 가리키는 풀에서 보상 후보를 만든다. 임시인 것은 코드가 아니라
    /// 데이터다 — 캐릭터를 설계하면 pool 값만 바뀐다(전투 노드 설계 결정 3a).</summary>
    public sealed class CharacterPoolRewardSource : IRewardCandidateSource
    {
        private readonly GameContent _content;

        public CharacterPoolRewardSource(GameContent content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds)
        {
            var pairs = new List<RewardCandidate>();
            foreach (var characterId in livingCharacterIds)
            {
                var cardIds = new List<string>(
                    _content.Pools.Get(_content.Characters.Get(characterId).Pool));
                cardIds.Sort(StringComparer.Ordinal);
                foreach (var cardId in cardIds)
                {
                    pairs.Add(new RewardCandidate(_content.Cards.Get(cardId), characterId));
                }
            }

            return pairs;
        }
    }
}
