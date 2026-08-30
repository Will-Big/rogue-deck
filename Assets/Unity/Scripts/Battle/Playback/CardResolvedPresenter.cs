using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using UnityEngine;

namespace FateWeaver.Unity.Playback
{
    /// <summary>카드가 해결되면 시전자가 앞으로 내지른다. 비트를 여는 개시 큐다 — 뒤따르는 피격
    /// 큐들이 이 뒤에 서로 겹쳐 흐르면서 "시전 한 번 → 여러 명이 동시에 피격"이 된다.
    ///
    /// 대상이 여럿인 광역 공격은 TargetId가 null이므로(DamageHandler가 그렇게 둔다) 특정 대상 쪽이
    /// 아니라 자기 진영의 정면으로 내지른다.</summary>
    public sealed class CardResolvedPresenter : IResolutionEventPresenter
    {
        private readonly BattleStage _stage;

        public CardResolvedPresenter(BattleStage stage)
        {
            _stage = stage != null ? stage : throw new ArgumentNullException(nameof(stage));
        }

        public Type EventType => typeof(CardResolved);

        public PlaybackCue Build(ResolutionEvent evt)
        {
            if (!(evt is CardResolved resolved))
            {
                return PlaybackCue.None;
            }

            var motion = _stage.MotionOf(resolved.OwnerId);
            if (motion == null)
            {
                return PlaybackCue.None;
            }

            var tween = motion.Lunge(DirectionFor(resolved));
            return tween != null ? PlaybackCue.Lead(tween) : PlaybackCue.None;
        }

        /// <summary>대상이 하나면 그쪽으로, 아니면 진영의 정면으로. 좌표는 BattleStage가 알고
        /// 이벤트는 id만 준다(규칙 11).</summary>
        private float DirectionFor(CardResolved resolved)
        {
            var forward = resolved.Side == Side.Player ? 1f : -1f;
            if (resolved.TargetId == null)
            {
                return forward;
            }

            var from = _stage.AnchorOf(resolved.OwnerId);
            var to = _stage.AnchorOf(resolved.TargetId);
            if (from == null || to == null)
            {
                return forward;
            }

            var delta = to.position.x - from.position.x;
            return Mathf.Approximately(delta, 0f) ? forward : Mathf.Sign(delta);
        }
    }
}
