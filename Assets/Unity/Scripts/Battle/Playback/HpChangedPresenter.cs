using System;
using DG.Tweening;
using FateWeaver.Core.Events;

namespace FateWeaver.Unity.Playback
{
    /// <summary>HP가 바뀌면 맞은 쪽이 흔들리고, 색이 번쩍이고, 숫자가 떠오르고, 막대가 흐른다.
    /// 네 가지가 한 시퀀스로 동시에 간다.
    ///
    /// 후속 큐다 — 같은 비트의 다른 피격들과 나란히 흘러야 광역 공격이 한 번의 타격으로 보인다.
    /// 막대의 시작값을 이벤트의 Before로 강제하지 않고 뷰의 현재 표시값에서 잇는 이유는, 한 턴에
    /// 같은 유닛이 여러 번 맞을 때 앞 트윈이 끝난 값에서 이어져야 하기 때문이다.</summary>
    public sealed class HpChangedPresenter : IResolutionEventPresenter
    {
        private readonly BattleStage _stage;

        public HpChangedPresenter(BattleStage stage)
        {
            _stage = stage != null ? stage : throw new ArgumentNullException(nameof(stage));
        }

        public Type EventType => typeof(HpChanged);

        public PlaybackCue Build(ResolutionEvent evt)
        {
            if (!(evt is HpChanged changed))
            {
                return PlaybackCue.None;
            }

            var unit = _stage.UnitOf(changed.HolderId);
            if (unit == null)
            {
                return PlaybackCue.None;
            }

            var sequence = DOTween.Sequence();
            var delta = changed.After - changed.Before;

            if (delta < 0)
            {
                var motion = _stage.MotionOf(changed.HolderId);
                if (motion != null)
                {
                    Join(sequence, motion.HitShake());
                    Join(sequence, motion.Flash());
                }
            }

            Join(sequence, unit.TweenHpTo(changed.After));

            if (delta != 0)
            {
                var number = _stage.SpawnNumber(_stage.AnchorOf(changed.HolderId));
                if (number != null)
                {
                    Join(sequence, number.Play(delta));
                }
            }

            return PlaybackCue.Follow(sequence);
        }

        private static void Join(Sequence sequence, Tween tween)
        {
            if (tween != null)
            {
                sequence.Join(tween);
            }
        }
    }
}
