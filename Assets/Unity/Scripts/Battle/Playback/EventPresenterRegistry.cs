using System;
using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Unity.Playback
{
    /// <summary>이벤트 타입에서 연출자들을 찾는다. 새 연출을 더할 때 중앙 switch를 키우지 않기 위한
    /// 지점이다(규칙 9).
    ///
    /// **한 이벤트에 연출자가 여럿 붙을 수 있다.** 사건 하나가 화면에서 여러 곳에 나타나기 때문이다 —
    /// 카드가 해결되면 시전자가 움직이고(유닛) 그 카드에 아웃라인이 켜진다(레일). 둘을 한 연출자에
    /// 몰면 그 클래스의 책임을 '그리고' 없이 못 쓴다(규칙 30). 등록 순서가 곧 큐가 만들어지는
    /// 순서다.
    ///
    /// 미등록 이벤트는 실패가 아니다 — 연출을 붙이지 않기로 한 이벤트가 대부분이다. 등록을 빠뜨린
    /// 것과 의도적 미등록을 가르는 일은 RegisteredEventTypes를 단언하는 테스트가 맡는다.</summary>
    public sealed class EventPresenterRegistry
    {
        private static readonly IResolutionEventPresenter[] None =
            Array.Empty<IResolutionEventPresenter>();

        private readonly Dictionary<Type, List<IResolutionEventPresenter>> _byEventType =
            new Dictionary<Type, List<IResolutionEventPresenter>>();

        public IReadOnlyCollection<Type> RegisteredEventTypes => _byEventType.Keys;

        public void Register(IResolutionEventPresenter presenter)
        {
            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            var eventType = presenter.EventType;
            if (eventType == null)
            {
                throw new ArgumentException(
                    "연출자가 맡을 이벤트 타입을 밝히지 않았다.", nameof(presenter));
            }

            if (!typeof(ResolutionEvent).IsAssignableFrom(eventType))
            {
                throw new ArgumentException(
                    "ResolutionEvent 파생 타입만 등록할 수 있다: " + eventType.Name,
                    nameof(presenter));
            }

            if (!_byEventType.TryGetValue(eventType, out var presenters))
            {
                presenters = new List<IResolutionEventPresenter>();
                _byEventType.Add(eventType, presenters);
            }

            presenters.Add(presenter);
        }

        /// <summary>등록 순서대로 돌려준다. 없으면 빈 목록이며 예외가 아니다.</summary>
        public IReadOnlyList<IResolutionEventPresenter> PresentersFor(ResolutionEvent evt)
            => evt != null && _byEventType.TryGetValue(evt.GetType(), out var presenters)
                ? presenters
                : None;
    }
}
