using System;
using DG.Tweening;
using FateWeaver.Core.Events;

namespace FateWeaver.Unity.Playback
{
    /// <summary>지금 실행 중인 카드에 아웃라인을 켠다. 카드가 해결되든 취소되든 "이 카드 차례"라는
    /// 사실은 같으므로 두 이벤트에 각각 등록해 쓴다 — EventType을 생성자로 받는 이유다.
    ///
    /// 지속 시간을 갖지 않는다. 켜 두면 다음 카드의 큐가 옮겨 가고, 재생이 끝나면 RefreshAll이
    /// 레일을 다시 만들면서 사라진다. 그래서 "언제 끄는가"를 아무도 관리하지 않아도 된다.</summary>
    public sealed class RailCardHighlightPresenter : IResolutionEventPresenter
    {
        private readonly ExecutionRailView _rail;

        public RailCardHighlightPresenter(ExecutionRailView rail, Type eventType)
        {
            _rail = rail != null ? rail : throw new ArgumentNullException(nameof(rail));
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        }

        public Type EventType { get; }

        public PlaybackCue Build(ResolutionEvent evt)
        {
            if (!TryInstanceId(evt, out var instanceId))
            {
                return PlaybackCue.None;
            }

            var rail = _rail;
            return PlaybackCue.Lead(
                DOTween.Sequence().AppendCallback(() => rail.SetExecutingCard(instanceId)));
        }

        private static bool TryInstanceId(ResolutionEvent evt, out int instanceId)
        {
            switch (evt)
            {
                case CardResolved resolved:
                    instanceId = resolved.InstanceId;
                    return true;
                case CardCancelled cancelled:
                    instanceId = cancelled.InstanceId;
                    return true;
                default:
                    instanceId = -1;
                    return false;
            }
        }
    }
}
