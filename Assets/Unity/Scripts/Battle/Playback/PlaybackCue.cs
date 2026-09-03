using DG.Tweening;

namespace FateWeaver.Unity.Playback
{
    /// <summary>비트 안에서의 선후. 개시가 먼저 흐르고, 후속들은 그 뒤에 서로 겹쳐 흐른다.
    /// 광역 공격이 "시전 한 번 → 여러 명이 동시에 피격"으로 보이는 것이 이 둘의 조합이다.</summary>
    public enum CueRole
    {
        Lead,
        Follow
    }

    /// <summary>이벤트 하나가 만든 연출. Tween이 null이면 "연출 없음"이며 디렉터가 건너뛴다 —
    /// 17종 이벤트 전부에 연출을 다는 것이 목표가 아니므로 정상 동작이다.</summary>
    public readonly struct PlaybackCue
    {
        public static readonly PlaybackCue None = default;

        public PlaybackCue(Tween tween, CueRole role)
        {
            Tween = tween;
            Role = role;
        }

        public Tween Tween { get; }
        public CueRole Role { get; }

        public bool HasTween => Tween != null;

        public static PlaybackCue Lead(Tween tween) => new PlaybackCue(tween, CueRole.Lead);

        public static PlaybackCue Follow(Tween tween) => new PlaybackCue(tween, CueRole.Follow);
    }
}
