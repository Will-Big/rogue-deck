using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation.Playback
{
    /// <summary>한 비트는 화면에서 한 순간으로 보여야 하는 이벤트 묶음이다. 비트끼리는 순차로,
    /// 비트 안은 동시에 재생한다.</summary>
    public sealed class PlaybackBeat
    {
        public PlaybackBeat(IReadOnlyList<ResolutionEvent> events)
        {
            Events = events;
        }

        public IReadOnlyList<ResolutionEvent> Events { get; }
    }

    /// <summary>타임라인을 비트로 나눈다. 코어는 "이 셋은 한 건이다"를 알려주지 않으므로 순서로
    /// 묶는다 — 개시 이벤트가 비트를 열고, 다음 개시를 만날 때까지 뒤따르는 것들이 그 비트에 속한다.
    /// TurnResolver가 CardResolved를 넣고 곧바로 그 카드가 낸 이벤트들을 이어 붙이기 때문에 이것이
    /// 성립한다. 코어가 그 순서를 바꾸면 TimelineBeatPlannerTests가 먼저 걸린다.
    ///
    /// 이 클래스가 FateWeaver.Simulation에 있는 이유는 TimelineTextFormatter와 같다 — 이벤트 목록을
    /// 표현용 구조로 바꾸는 순수 함수라 UnityEngine이 필요 없고, 헤드리스로 검증된다.</summary>
    public static class TimelineBeatPlanner
    {
        public static IReadOnlyList<PlaybackBeat> Plan(IReadOnlyList<ResolutionEvent> timeline)
        {
            var beats = new List<PlaybackBeat>();
            if (timeline == null)
            {
                return beats;
            }

            List<ResolutionEvent> current = null;
            bool inTickRun = false;

            foreach (var evt in timeline)
            {
                // 턴 종료 틱 구간: 첫 StatusTicked가 구간을 열고, 뒤이은 틱 이벤트는 보유자가 달라도
                // 같은 비트에 담는다 — 여러 유닛의 독 피해가 한 순간으로 보여야 한다.
                bool isTick = evt is StatusTicked;
                bool opensBeat = IsBeatOpener(evt) || (isTick && !inTickRun);

                if (opensBeat)
                {
                    Flush(beats, ref current);
                    current = new List<ResolutionEvent>();
                    inTickRun = isTick;
                }
                else if (current == null)
                {
                    // 열린 비트가 없는 상태의 낱개 이벤트는 그것만의 비트가 된다.
                    beats.Add(new PlaybackBeat(new[] { evt }));
                    continue;
                }

                current.Add(evt);

                if (IsSoloBeat(evt))
                {
                    Flush(beats, ref current);
                    inTickRun = false;
                }
            }

            Flush(beats, ref current);
            return beats;
        }

        /// <summary>비트를 여는 이벤트. 이것이 재생 계층이 코어에 대해 세우는 유일한 순서 가정이다.</summary>
        private static bool IsBeatOpener(ResolutionEvent evt)
            => evt is CardResolved || evt is CardCancelled || IsSoloBeat(evt);

        /// <summary>턴 경계는 앞뒤 어느 것과도 묶이지 않는다.</summary>
        private static bool IsSoloBeat(ResolutionEvent evt)
            => evt is TurnStarted || evt is TurnEnded;

        private static void Flush(List<PlaybackBeat> beats, ref List<ResolutionEvent> current)
        {
            if (current != null && current.Count > 0)
            {
                beats.Add(new PlaybackBeat(current));
            }

            current = null;
        }
    }
}
