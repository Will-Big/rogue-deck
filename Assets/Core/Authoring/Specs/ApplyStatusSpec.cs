using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring
{
    /// <summary>카드 위치 규칙이 고른 대상에게 상태를 건다. 어느 진영인지는 TargetFaction이, 어느 위치인지는
    /// 카드의 그 진영 축이 정한다(스펙 §4).
    ///
    /// 처리기(ApplyStatusHandler)는 아직 StatusApplyTarget으로 대상을 고르므로 여기서 (진영, 위치)를 그 값으로
    /// 옮긴다 — 효과 단위 대상 선택(계획 T3)이 들어오면 이 변환이 사라진다. 뜻이 없는 조합(적 카드가 파티에 거는
    /// 상태 등)은 지금 처리기가 다루지 못하므로 로딩에서 거부한다.</summary>
    [Serializable]
    public sealed class ApplyStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;

        /// <summary>이 카드가 거는 양. 뜻은 상태가 정한다 — 수명이 Permanent·ThisTurn이면 세기,
        /// Turns·UntilConsumed면 지속.</summary>
        public int Count;

        public override EffectKey Key => EffectKeys.ApplyStatus;

        public override bool IsTargeted => true;

        protected override EffectData Build(Side cardSide, CardTargetKey? target)
        {
            var key = target.Value;
            var effect = new EffectData(Key, Count);
            if (key.Range == CardTargetRange.Self)
            {
                return effect with { Payload = new ApplyStatusPayload(Status.ToKey(), StatusApplyTarget.Self) };
            }

            if (key.Faction == CardTargetFaction.Enemy)
            {
                return effect with
                {
                    Payload = new ApplyStatusPayload(Status.ToKey(), StatusApplyTarget.TargetEnemy),
                    TargetSelector = SelectorFor(key.Range)
                };
            }

            if (key.Range == CardTargetRange.All)
            {
                return effect with { Payload = new ApplyStatusPayload(Status.ToKey(), StatusApplyTarget.AllPartyMembers) };
            }

            return effect with
            {
                Payload = new ApplyStatusPayload(Status.ToKey(), StatusApplyTarget.PartyBySelector),
                TargetSelector = SelectorFor(key.Range)
            };
        }

        /// <summary>저작 콘텐츠 존재 여부는 검사하지 않는다. StatusContentLoader가 "등록된 모든
        /// 상태에 저작이 있다"를 요구하고 부팅이 상태를 카드보다 먼저 읽으므로, 여기 도달한
        /// 시점에는 HasStatus가 곧 저작 존재다 — 가드를 두면 같은 불변식을 두 곳에서 지키게 된다.</summary>
        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty)
            {
                yield return "apply_status spec requires a status key.";
            }
            else if (!context.HasStatus(Status.ToKey()))
            {
                yield return "Unknown status key '" + Status.Id + "'.";
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
        {
            if (target.Range == CardTargetRange.Self)
            {
                if (target.Faction != OwnFaction(cardSide))
                {
                    yield return "apply_status Self must be on the card's own side (" + OwnFaction(cardSide) + ").";
                }

                yield break;
            }

            if (cardSide == Side.Enemy)
            {
                yield return "apply_status on an enemy card supports only Self for now.";
            }
        }
    }
}
