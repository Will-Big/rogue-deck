using System;

namespace FateWeaver.Core.Events
{
    /// <summary>전투 사건 종류(전투 실행 계약 스펙 §7). 문자열 키라서 새 사건 종류는 중앙 열거형을 바꾸지
    /// 않고 키를 만들어 내보내고 반응 처리기가 그 키를 구독하면 된다.</summary>
    public readonly struct CombatSignalKey : IEquatable<CombatSignalKey>
    {
        public string Id { get; }

        public CombatSignalKey(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Signal key must not be empty.", nameof(id));
            Id = id;
        }

        public bool Equals(CombatSignalKey other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CombatSignalKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
        public override string ToString() => Id;

        public static bool operator ==(CombatSignalKey a, CombatSignalKey b) => a.Equals(b);
        public static bool operator !=(CombatSignalKey a, CombatSignalKey b) => !a.Equals(b);
    }

    /// <summary>코어가 내는 기본 사건 키. 목록이 아니라 이름 모음이다 — 여기 없는 키도 쓸 수 있다.</summary>
    public static class CombatSignalKeys
    {
        /// <summary>공격 피해를 받았다. 방어로 전부 막혀도 발생한다. 공격에 대한 반응은 반응 공격이 낸 공격
        /// (Origin = Reaction)에는 발동하지 않는다 — 반응 공격이 반응 공격을 부르지 않는다(계획 D11). 각 능력의
        /// CanReact가 이 조건을 확인한다.</summary>
        public static readonly CombatSignalKey Attacked = new CombatSignalKey("attacked");

        /// <summary>HP가 실제로 줄었다. 원인이 공격이든 상태든 발생한다.</summary>
        public static readonly CombatSignalKey HpDamaged = new CombatSignalKey("hp_damaged");

        /// <summary>상태를 얻었다. Detail은 상태 키 id다(방어 획득 = Detail "block").</summary>
        public static readonly CombatSignalKey StatusGained = new CombatSignalKey("status_gained");

        /// <summary>대형 안 위치가 바뀌었다.</summary>
        public static readonly CombatSignalKey FormationMoved = new CombatSignalKey("formation_moved");

        /// <summary>보유자가 죽었다. 사망 시 반응(전염)이 이것에 반응한다 — 사망 원인(반응 공격 포함)과 무관하다.</summary>
        public static readonly CombatSignalKey HolderDied = new CombatSignalKey("holder_died");
    }
}
