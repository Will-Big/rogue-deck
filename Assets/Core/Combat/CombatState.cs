using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Combat
{
    /// <summary>Mutable combat state. FateEnergyPerTurn is a variable (NOT fixed 3).
    /// Party and Enemies are two independent side formations; index 0 is each side's front.</summary>
    public sealed class CombatState
    {
        /// <summary>Id of the single party member that solo (non-party) combats use. Also the OwnerId
        /// stamped on solo deck cards, so deck ownership and party membership agree.</summary>
        public const string SoloPlayerId = "player";
        private const string SoloPlayerName = "Player";

        private Random _rng;

        /// <summary>Independent party formation; index 0 is the party's front.</summary>
        public List<PartyMember> Party { get; } = new();

        /// <summary>Independent enemy formation; index 0 is the enemy side's front.</summary>
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
        /// <para><b>전투 단위가 아니다 (2026-08-28 정정).</b> 이것은 콘텐츠 JSON을 읽어 만든 런타임
        /// 객체이고, 전투 화면에 진입할 때 <c>BattleScreenController.Start()</c>의 첫
        /// <c>StartSession()</c>에서 만들어져 그 컨트롤러의 <c>_content</c> 필드에 머문다
        /// (<c>static</c>이 아니므로 컨트롤러가 죽으면 함께 사라진다). <c>StartSession</c>은 HUD의
        /// 재시작 버튼에도 배선되어 있는데 그때 <c>_content</c>는 재사용되므로, <b>전투를 다시
        /// 시작해도 같은 인스턴스</b>다. 이전 주석은 "전투 단위라 변경이 런으로 새지 않는다"고
        /// 적혀 있었으나 사실이 아니었다.
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

        /// <summary>Seeded RNG shared by all combat rule logic (AGENTS.md rule 7: no ad-hoc `new Random()`
        /// elsewhere). Lazily created from RngSeed so RngSeed can still be assigned via object initializer.</summary>
        public Random Rng => _rng ??= new Random(RngSeed);

        /// <summary>Adds the solo-mode party member. Party mode adds its own members instead.</summary>
        public PartyMember AddSoloPlayer(int hp)
        {
            var member = new PartyMember(SoloPlayerId, SoloPlayerName, hp);
            Party.Add(member);
            return member;
        }
    }
}
