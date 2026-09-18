namespace FateWeaver.Core.Status
{
    /// <summary>A status applied to a holder (data). Count is the remaining turns (Turns) or remaining charges
    /// (UntilConsumed), 0 otherwise — 표시가 읽는 값이라 뜻을 바꾸지 않는다. Magnitude is the effect strength
    /// (e.g. block points), 0 when a status has no magnitude. 만료는 Expiry(공통 정책)와 남은 방문 수로 판단하며
    /// StatusLifetimePolicy만 방문을 센다.</summary>
    public sealed class StatusInstance
    {
        public StatusKey Key { get; }
        public StatusLifetimeKind Kind { get; private set; }
        public int Count { get; set; }
        public int Magnitude { get; set; }

        /// <summary>이 상태의 만료 정책.</summary>
        public ExpiryPolicy Expiry { get; private set; }

        /// <summary>PhaseVisits 정책에서 남은 기준 시점 방문 수. Turns는 Count와 함께 줄어든다.</summary>
        internal int VisitsLeft { get; private set; }

        public StatusInstance(StatusKey key, StatusLifetime lifetime, int magnitude = 0)
        {
            Key = key;
            Refresh(lifetime, magnitude);
        }

        /// <summary>지금 남은 수명(다른 보유자에게 옮길 때 쓴다).</summary>
        public StatusLifetime Lifetime
            => Kind == StatusLifetimeKind.PhaseVisits
                ? StatusLifetime.Of(ExpiryPolicy.PhaseVisits(Expiry.Phase, VisitsLeft))
                : StatusLifetime.Of(Kind, Count);

        /// <summary>같은 상태를 다시 부여받았다: 수명과 수치를 새 값으로 갱신한다(가방 안 위치는 그대로).</summary>
        internal void Refresh(StatusLifetime lifetime, int magnitude)
        {
            Kind = lifetime.Kind;
            Count = lifetime.Kind == StatusLifetimeKind.PhaseVisits ? 0 : lifetime.Count;
            Magnitude = magnitude;
            Expiry = lifetime.Expiry;
            VisitsLeft = Expiry.Mode == ExpiryMode.PhaseVisits ? Expiry.RemainingVisits : 0;
        }

        /// <summary>기준 시점을 한 번 방문했다. 다 쓰면 true(만료).</summary>
        internal bool SpendVisit()
        {
            VisitsLeft--;
            if (Kind == StatusLifetimeKind.Turns)
            {
                Count = VisitsLeft;
            }

            return VisitsLeft <= 0;
        }
    }
}
