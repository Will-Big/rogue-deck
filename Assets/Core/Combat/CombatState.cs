using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Combat
{
    /// <summary>가변 전투 상태. FateEnergyPerTurn은 변수다(3 고정이 아니다).
    /// Party와 Enemies는 서로 독립된 두 진영의 대형이며, 각 진영의 인덱스 0이 맨 앞이다.</summary>
    public sealed class CombatState
    {
        /// <summary>솔로(비파티) 전투가 쓰는 유일한 파티원의 id. 솔로 덱 카드에 찍히는 OwnerId이기도
        /// 해서, 덱 소유와 파티 구성원이 서로 맞는다.</summary>
        public const string SoloPlayerId = "player";
        private const string SoloPlayerName = "Player";

        private Random _rng;

        /// <summary>독립된 파티 대형. 인덱스 0이 파티의 맨 앞이다.</summary>
        public List<PartyMember> Party { get; } = new();

        /// <summary>독립된 적 대형. 인덱스 0이 적 진영의 맨 앞이다.</summary>
        public List<Enemy> Enemies { get; } = new();

        public FutureZone Zone { get; } = new();
        public int FateEnergy { get; set; }
        public int FateEnergyPerTurn { get; set; }
        /// <summary>다음 플레이어 사용 턴의 운명력 리필에 더해지는 1회성 적립분 (grant_next_turn_fate).
        /// 리필 시점에 합산 후 0으로 소거된다.</summary>
        public int PendingNextTurnFateEnergy { get; set; }
        public int RngSeed { get; set; }

        /// <summary>상태 콘텐츠. 수치와 수명 종류의 단일 출처이며 원본은 Content/Statuses/*.json이다.
        ///
        /// <para><b>전투 단위가 아니다 (2026-08-28 정정, 2026-09-15 배선 갱신).</b> 이것은 콘텐츠
        /// JSON을 읽어 만든 런타임 객체이고, <c>CombatNodeFlow.Start()</c>에서 한 번 로드되어 그
        /// 흐름 객체의 <c>_content</c> 필드에 머문다(<c>static</c>이 아니므로 흐름 객체가 죽으면
        /// 함께 사라진다). 런을 재시작하거나(HUD `초기화`) 다음 노드로 넘어갈 때도 <c>_content</c>는
        /// 재사용되므로, <b>전투를 다시 시작해도 같은 인스턴스</b>다. 이전 주석은 "전투 단위라 변경이
        /// 런으로 새지 않는다"고 적혀 있었으나 사실이 아니었다.
        ///
        /// 그래서 <c>StatusContent.Rules</c>에 쓴 값은(<c>StatusRuleSet.Set</c>) 전투가 끝나도
        /// 사라지지 않는다. <b>가변인 것 자체는 의도된 설계다</b> — 유물 같은 효과가 수치를 바꾸게
        /// 하려는 것이다. 문제는 쓰는 것이 아니라 <b>지워질 시점이 없다</b>는 것이다: 변경은 전투
        /// 하나 또는 런 하나만큼만 살아야 하는데, 담기는 객체는 전투 화면 세션만큼 산다.
        ///
        /// 아직 터지지 않은 이유는 프로덕션에서 <c>Rules.Set</c>을 부르는 코드가 하나도 없기
        /// 때문이다. 테스트는 이미 이 누수를 밟아 <c>TestContent.Statuses()</c>가 호출마다 카탈로그를
        /// 새로 만드는 것으로 우회한다.
        ///
        /// <b>여기에 전투용 사본을 뜨는 방식은 채택하지 않는다</b> — 카드 변형 설계 §4.3이 같은
        /// 이유로 기각했다(전투 중 발생한 런 지속 변경이 사본과 함께 사라진다). 수명별 층을 두고
        /// 유효값을 합성하는 쪽이며, 그 층이 아직 없다는 것이 진짜 결함이다. README 후속 작업
        /// 대기열의 "런 층·전투 층" 항목이 다룬다.</para></summary>
        public Authoring.Statuses.StatusContentCatalog StatusContent { get; }

        /// <summary>상태 콘텐츠 없이는 전투가 성립하지 않는다 — 규칙 수치가 전부 거기 있다.
        /// 기본값을 두면 코드가 JSON과 같은 값을 두 벌 갖게 되므로 생성자에서 요구한다.</summary>
        public CombatState(Authoring.Statuses.StatusContentCatalog statusContent)
        {
            StatusContent = statusContent
                ?? throw new ArgumentNullException(nameof(statusContent));
        }

        public Status.StatusRuleSet StatusRules => StatusContent.Rules;

        /// <summary>모든 전투 규칙 로직이 함께 쓰는 시드 RNG(AGENTS.md 규칙 7: 다른 곳에서 즉석
        /// `new Random()` 금지). RngSeed를 객체 이니셜라이저로 대입할 수 있도록, 처음 읽을 때
        /// RngSeed로부터 지연 생성한다.</summary>
        public Random Rng
        {
            get
            {
                if (_rng == null)
                {
                    _rng = new Random(RngSeed);
                }

                return _rng;
            }
        }

        /// <summary>솔로 모드의 파티원을 추가한다. 파티 모드는 대신 자기 구성원을 직접 추가한다.</summary>
        public PartyMember AddSoloPlayer(int hp)
        {
            var member = new PartyMember(SoloPlayerId, SoloPlayerName, hp);
            Party.Add(member);
            return member;
        }
    }
}
