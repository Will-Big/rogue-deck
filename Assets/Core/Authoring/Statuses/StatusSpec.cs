using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>저작된 상태 하나. 파라미터가 없는 상태(방어·전염·독 잠복·독 안정·보상 무효)는 이
    /// 클래스를 그대로 쓴다 — 쓰이지 않는 칸을 만들지 않기 위해 파라미터가 있는 상태만 서브클래스를
    /// 갖는다. behavior 클래스는 코드에 남고 키로 등록된다(규칙 9).</summary>
    [Serializable]
    public class StatusSpec
    {
        public StatusKeyRef Key;

        /// <summary>카드 본문과 UI가 이 상태를 부르는 이름. 상태에 관한 저작 데이터이므로 설명
        /// 카탈로그가 아니라 상태가 소유한다 — 이름 변경이 재컴파일 없이 끝난다.</summary>
        public string DisplayName;

        /// <summary>이 상태의 수명 종류. 카드가 적는 count의 뜻을 여기서 정한다 —
        /// Permanent·ThisTurn이면 세기, Turns·UntilConsumed면 지속. Expiry와 둘 중 하나만 쓴다.
        /// Permanent은 0번째(기본) 값이라 Ignore면 지워지므로 Include로 쓰고, 없을 때(null)만 생략한다.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include, NullValueHandling = NullValueHandling.Ignore)]
        public StatusLifetimeKind? Lifetime;

        /// <summary>시점 방문으로 만료되는 정책(예: 방어 = 다음 턴 준비 1회, 스펙 §8). Lifetime 대신 쓴다. 방문 횟수를
        /// 데이터가 정하므로 카드가 적는 count는 세기다.</summary>
        public ExpirySpec Expiry;

        /// <summary>이 상태가 주는 피해(독 틱 등)의 속성들. 피해를 주지 않는 상태는 생략한다 — 생략하면 보통
        /// 피해(방어·배율 적용)다. 관통·배율 무시는 상태가 아니라 이 데이터가 정한다(계획 D10).</summary>
        public Effects.DamageTrait[] DamageTraits;

        /// <summary>빈 목록도 생략한다 — 편집 도구와 같은 규칙.</summary>
        public bool ShouldSerializeDamageTraits() => DamageTraits != null && DamageTraits.Length > 0;

        [JsonIgnore]
        public bool CountIsDuration
            => Expiry == null
                && (Lifetime == StatusLifetimeKind.Turns || Lifetime == StatusLifetimeKind.UntilConsumed);

        /// <summary>카드가 count를 주었을 때 이 상태가 받는 수명.</summary>
        public StatusLifetime LifetimeFor(int count)
        {
            if (Expiry != null)
            {
                return StatusLifetime.Of(Expiry.ToPolicy());
            }

            var kind = Lifetime ?? StatusLifetimeKind.Permanent;
            return StatusLifetime.Of(kind, CountIsDuration ? count : 0);
        }

        /// <summary>자기 타입의 빈 인스턴스. JSON 컨버터가 Populate 대상으로 쓴다.
        /// 리플렉션 대신 각 타입이 스스로 답한다 (규칙 9).</summary>
        public virtual StatusSpec NewInstance() => new StatusSpec();

        public virtual StatusRule ToRule() => new StatusRule();

        public virtual IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Key.IsEmpty)
            {
                yield return "status spec requires a key.";
            }
            else if (!context.HasStatus(Key.ToKey()))
            {
                yield return "no runtime behavior for status key '" + Key.Id + "'.";
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                yield return "status spec requires a displayName.";
            }

            if (Lifetime.HasValue == (Expiry != null))
            {
                yield return "status spec needs exactly one of lifetime or expiry.";
            }

            if (Expiry != null)
            {
                foreach (var error in Expiry.Validate()) yield return error;
            }

            foreach (var error in DamageTraitRules.Validate(DamageTraits, "damageTraits"))
            {
                yield return error;
            }
        }
    }
}
