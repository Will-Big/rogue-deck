using System;
using FateWeaver.Core.Events;

namespace FateWeaver.Unity.Playback
{
    /// <summary>이벤트 하나를 연출 하나로 바꾼다. 재생 순서도, 다른 이벤트도, 전체 타임라인도 모른다
    /// (규칙 9: 새 연출 = 구현 클래스 1개 + 등록 한 줄).
    ///
    /// 뷰를 찾는 BattleStage는 매개변수가 아니라 구현체의 생성자로 받는다. 스테이지는 이벤트마다
    /// 달라지는 값이 아니라 고정 협력자이고, 그래야 이 계약이 스테이지 타입을 몰라도 된다.
    ///
    /// 구현체는 이벤트 페이로드만 읽고 CombatState를 뒤지지 않는다(규칙 11). 한국어 문자열을 갖지
    /// 않는다(규칙 10). 연출 시간·강도는 여기가 아니라 뷰 컴포넌트의 [SerializeField]에 있다(규칙 8).</summary>
    public interface IResolutionEventPresenter
    {
        /// <summary>이 연출자가 맡는 이벤트 타입. 레지스트리의 키다.</summary>
        Type EventType { get; }

        /// <summary>연출을 만든다. 대상 뷰를 찾지 못하는 등 그릴 것이 없으면 PlaybackCue.None을
        /// 돌려준다 — 예외를 던지지 않는다. 재생 도중 한 이벤트 때문에 턴 전체가 멈추면 안 된다.</summary>
        PlaybackCue Build(ResolutionEvent evt);
    }
}
