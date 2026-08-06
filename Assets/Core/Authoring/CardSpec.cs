using FateWeaver.Core.Cards;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>카드 종류와 무관한 공통 저작 필드. 실행·개입 각각의 고유 필드는 파생 클래스가
    /// 가지므로, 개입 카드가 실행 순서를·실행 카드가 개입 키를 드는 오저작을 타입이 막는다
    /// (ContentJson의 MissingMemberHandling.Error가 부팅에서 거부한다).
    ///
    /// 이 기반 클래스의 필드에는 모두 명시적 Order가 붙어 있다(파생 클래스의 필드는 그렇지 않다):
    /// .NET 리플렉션이 Type.GetFields를 파생 타입 선언분부터 반환하기 때문에(실측: Derived.C,
    /// Derived.D, Base.A, Base.B 순), Order 없이는 파생 클래스의 무순서 필드(BaseExecutionOrder·
    /// Effects 등)가 이 기반 필드들보다 먼저 직렬화된다. Order가 없는 필드끼리는 이 발견 순서로
    /// 동률이 갈린다 — 그래서 기반 필드 전부에 음수 Order를 줘 파생 필드(무순서, 기본값 -1)보다
    /// 앞서게 고정한다.</summary>
    public abstract class CardSpec
    {
        [JsonProperty(Order = -10)]
        public string Id;

        [JsonProperty(Order = -9)]
        public string Name;

        // Player/Execution은 각 enum의 0번째(기본) 값이라 DefaultValueHandling.Ignore가 지운다.
        // CardContentLoader가 "side"·"category" 키의 존재 자체로 무결성을 검증하므로
        // (생략 시 조용히 Player/Execution이 되는 사고 방지), 여기서는 항상 써야 한다.
        [JsonProperty(Order = -8, DefaultValueHandling = DefaultValueHandling.Include)]
        public Side Side;

        [JsonProperty(Order = -7, DefaultValueHandling = DefaultValueHandling.Include)]
        public CardCategory Category;

        [JsonProperty(Order = -6)]
        public int EnergyCost;

        /// <summary>카드 풀 후보 구성용 등급. None은 등급 개념이 없는 카드(fixture 등)의 정상
        /// 상태이므로 Side·Category와 달리 Include 처방을 쓰지 않는다 — 생략이 곧 None이라
        /// 정보 손실이 없다.
        /// Order는 키 순서를 위한 것이다: 파생 클래스의 무순서 필드(BaseExecutionOrder·Effects)
        /// 뒤로 등급·태그를 보내야 기존 카드 JSON 26장의 키 순서가 유지된다.</summary>
        [JsonProperty(Order = 100)]
        public CardGrade Grade;

        /// <summary>저작 분류 태그. 풀 소속 카드는 하나 이상 가져야 한다(PoolContentLoader).</summary>
        [JsonProperty(Order = 101)]
        public string[] Tags;
    }

    /// <summary>실행 카드의 저작 데이터. 레일에 올라 효과를 순서대로 발동한다.</summary>
    public sealed class ExecutionCardSpec : CardSpec
    {
        public int BaseExecutionOrder;
        public EffectSpec[] Effects;
    }

    /// <summary>개입 카드의 저작 데이터. 액션별 파라미터는 InterventionSpec이 소유하므로 이 클래스는
    /// 액션이 늘어도 자라지 않는다 — 계획 3.5 이전에는 lock 카드가 쓰지 않는 칸 셋을 들고 있었다.</summary>
    public sealed class InterventionCardSpec : CardSpec
    {
        public InterventionSpec Intervention;
    }

    /// <summary>개입 대상 진영 제한. Any=제한 없음, Player=재촉류, Enemy=유예류.</summary>
    public enum InterventionTargetSideRef { Any, Player, Enemy }
}
