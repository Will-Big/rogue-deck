using FateWeaver.Core.Cards;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Flat, pure card data (the single source the headless sims read). Built from a CardAsset SO
    /// at edit time (code generation) and converted to a core CardDefinition by CardSpecMapper.</summary>
    public sealed class CardSpec
    {
        public string Id;
        public string Name;

        // Player/Execution은 각 enum의 0번째(기본) 값이라 DefaultValueHandling.Ignore가 지운다.
        // CardContentLoader가 "side"·"category" 키의 존재 자체로 무결성을 검증하므로
        // (생략 시 조용히 Player/Execution이 되는 사고 방지), 여기서는 항상 써야 한다.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public Side Side;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public CardCategory Category;

        public int EnergyCost;
        public int BaseExecutionOrder;
        public EffectSpec[] Effects;
        public InterventionKeyRef Intervention;
        public int InterventionEffectValue;
        public InterventionTargetSideRef InterventionTargetSide;
        public bool InterventionRequireAdjacent;

        /// <summary>카드 풀 후보 구성용 등급. None은 등급 개념이 없는 카드(fixture 등)의 정상
        /// 상태이므로 Side·Category와 달리 Include 처방을 쓰지 않는다 — 생략이 곧 None이라
        /// 정보 손실이 없다.</summary>
        public CardGrade Grade;

        /// <summary>저작 분류 태그. 풀 소속 카드는 하나 이상 가져야 한다(PoolContentLoader).</summary>
        public string[] Tags;
    }

    /// <summary>개입 대상 진영 제한. Any=제한 없음, Player=재촉류, Enemy=유예류.</summary>
    public enum InterventionTargetSideRef { Any, Player, Enemy }
}
