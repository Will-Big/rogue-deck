using System;
using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Unity.Playback
{
    /// <summary>이벤트 타입에서 연출자를 찾는다. 새 연출을 더할 때 중앙 switch를 키우지 않기 위한
    /// 지점이다(규칙 9).
    ///
    /// 미등록 이벤트는 실패가 아니다 — 연출을 붙이지 않기로 한 이벤트가 대부분이다. 등록을 빠뜨린
    /// 것과 의도적 미등록을 가르는 일은 RegisteredEventTypes를 단언하는 테스트가 맡는다.</summary>
    public sealed class EventPresenterRegistry
    {
        private readonly Dictionary<Type, IResolutionEventPresenter> _byEventType =
            new Dictionary<Type, IResolutionEventPresenter>();

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

            if (_byEventType.ContainsKey(eventType))
            {
                throw new ArgumentException(
                    "이미 등록된 이벤트 타입이다: " + eventType.Name, nameof(presenter));
            }

            _byEventType.Add(eventType, presenter);
        }

        public bool TryResolve(ResolutionEvent evt, out IResolutionEventPresenter presenter)
        {
            if (evt == null)
            {
                presenter = null;
                return false;
            }

            return _byEventType.TryGetValue(evt.GetType(), out presenter);
        }
    }
}
