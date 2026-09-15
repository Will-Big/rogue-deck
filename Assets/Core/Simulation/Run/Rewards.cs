using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>보상 후보 하나. 소유자는 카드가 속한 풀의 주인이며, 뽑은 뒤에 붙이는 값이 아니다
    /// (전투 노드 설계 결정 3).</summary>
    public sealed class RewardCandidate
    {
        public RewardCandidate(CardDefinition card, string ownerId)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
        }

        public CardDefinition Card { get; }
        public string OwnerId { get; }
    }

    public sealed class RewardOffer
    {
        public RewardOffer(IReadOnlyList<RewardCandidate> candidates)
        {
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        }

        public IReadOnlyList<RewardCandidate> Candidates { get; }
    }

    /// <summary>살아남은 캐릭터 각자의 풀에서 뽑을 수 있는 (카드, 소유자) 쌍 전체를 돌려준다.
    /// 순서: 인자로 받은 캐릭터 순서 → 풀 안에서는 카드 id 서수 정렬. 이 순서는 보상 추첨의 입력이라
    /// 사실상 저장 형식처럼 굳는다. 한 카드가 여러 풀에 있으면 쌍이 여러 개 나온다(임시 데이터 기간).</summary>
    public interface IRewardCandidateSource
    {
        IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds);
    }
}
