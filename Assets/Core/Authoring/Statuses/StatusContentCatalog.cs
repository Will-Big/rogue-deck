using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>콘텐츠 JSON(Content/Statuses/*.json)을 읽어 만든 런타임 객체. 캐릭터별 규칙은 없다.
    ///
    /// <para><b>"전투당 하나"가 아니다 (2026-08-28 정정).</b> 만들어지는 곳은
    /// <c>StatusContentLoader</c> 하나뿐이고, 전투 화면 진입 시 <c>BattleScreenController</c>가
    /// <c>_content</c>에 담아 그 컨트롤러가 사는 동안 재사용한다. 전투를 다시 시작해도 같은
    /// 인스턴스이므로 <c>Rules</c>에 쓴 값은 전투 경계를 넘는다.
    ///
    /// <b>저작값을 읽는 통로가 여기 쌓인다.</b> 상태의 런타임 행동(<c>StatusBehavior</c> 11개)과
    /// 저작 스펙(<c>StatusSpec</c> 서브클래스)은 규칙 9대로 갈려 있어 상태를 추가해도 클래스 하나와
    /// 등록 한 줄이면 된다. 그런데 그 저작값을 <b>읽는</b> 쪽은 여기에 상태별 메서드로 쌓인다 —
    /// <c>GrowthPerTurnOf</c>는 독만, <c>ExecutionOrderDeltaOf</c>는 가속·감속만 안다. <c>is X ? … : 0</c>
    /// 는 중앙 switch를 손으로 편 것이며, 파라미터를 가진 상태를 추가할 때마다 메서드가 하나 는다.
    ///
    /// 원인은 방향이다 — 행동은 <c>NewSpec()</c>으로 자기 스펙 타입을 아는데, 읽을 때 카탈로그를
    /// 거치느라 그 지식이 버려지고 카탈로그가 대신 알게 됐다. 카탈로그가 <c>StatusKey → StatusSpec</c>
    /// 만 돌려주고 해석은 각 행동이 하면 여기는 자라지 않는다. README 후속 작업 대기열의
    /// "런 층·전투 층" 항목이 이 정리까지 함께 다룬다.</para></summary>
    public sealed class StatusContentCatalog
    {
        private readonly Dictionary<StatusKey, StatusSpec> _specs;
        private readonly List<string> _keys;

        public StatusContentCatalog(Dictionary<StatusKey, StatusSpec> specs)
        {
            _specs = specs;
            Rules = new StatusRuleSet();
            _keys = new List<string>();
            foreach (var pair in specs)
            {
                Rules.Set(pair.Key, pair.Value.ToRule());
                _keys.Add(pair.Key.Id);
            }

            _keys.Sort(StringComparer.Ordinal);
        }

        public StatusRuleSet Rules { get; }

        /// <summary>정렬된 키 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Keys => _keys;

        public StatusLifetimeKind LifetimeOf(StatusKey key) => Spec(key).Lifetime;

        public string DisplayNameOf(StatusKey key) => Spec(key).DisplayName;

        public bool CountIsDuration(StatusKey key) => Spec(key).CountIsDuration;

        public int ExecutionOrderDeltaOf(StatusKey key)
            => Spec(key) is ExecutionOrderStatusSpec spec ? spec.ExecutionOrderDelta : 0;

        public int GrowthPerTurnOf(StatusKey key)
            => Spec(key) is PoisonStatusSpec spec ? spec.GrowthPerTurn : 0;

        private StatusSpec Spec(StatusKey key)
        {
            if (!_specs.TryGetValue(key, out var spec))
            {
                throw new KeyNotFoundException("No authored status content for '" + key.Id + "'.");
            }

            return spec;
        }
    }
}
