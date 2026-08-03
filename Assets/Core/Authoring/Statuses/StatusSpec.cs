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
        /// Permanent·ThisTurn이면 세기, Turns·UntilConsumed면 지속.
        /// Permanent은 StatusLifetimeKind의 0번째(기본) 값이라 DefaultValueHandling.Ignore가
        /// 지운다. ApplyStatusSpec.Lifetime과 같은 이유로, 생략된 lifetime이 조용히
        /// "영원히 지속"으로 복원되는 사고를 막기 위해 항상 써야 한다.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public StatusLifetimeKind Lifetime;

        [JsonIgnore]
        public bool CountIsDuration
            => Lifetime == StatusLifetimeKind.Turns
                || Lifetime == StatusLifetimeKind.UntilConsumed;

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
        }
    }
}
