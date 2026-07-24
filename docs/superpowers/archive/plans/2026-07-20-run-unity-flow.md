# 런 Unity 흐름·콘텐츠·통합(Run Unity Flow) Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 앞선 4개 계획의 산출물을 하나의 플레이 가능한 런으로 통합한다 — 핸들러 부팅 등록 + 원 사이클 헤드리스 통합 테스트, SO 저작 파이프라인(RunDefinition·인카운터·보상 풀), 맵/전투/고용·회복/결과 화면 흐름, 씬·콘텐츠 생성.

**Architecture:** 스펙 [2026-07-20-run-cycle-skeleton-design.md](../specs/2026-07-20-run-cycle-skeleton-design.md) §3.6, §4, §5. **단일 씬 + 패널 전환** 방식 — 씬 간 상태 전달 문제를 피하고, 기존 전투 씬(패널 구성)의 관례를 따른다. `RunController`가 `RunState`를 소유하고 노드 키로 핸들러를 resolve해 패널을 전환한다. 씬·SO 콘텐츠는 기존 `BattleSceneBuilder` 패턴대로 에디터 스크립트로 생성한다(런타임 문자열 탐색 금지 준수).

**Tech Stack:** C# 9, NUnit 헤드리스, Unity UGUI + ScriptableObject, 에디터 씬 빌더.

## Global Constraints

- **선행 조건: `feat/run-core`·`feat/run-combat-node`·`feat/run-recruit-heal-node`·`feat/run-reward` 4개가 모두 master에 머지된 뒤 시작한다.**
- 헤드리스 테스트 실행: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
- LangVersion 9. `Assets/Core/**` UnityEngine 금지. Unity 레이어: 프리팹 재사용(규칙 1), 참조는 인스펙터 할당(규칙 2), 문자열 탐색 금지(규칙 3), `[SerializeField] private`(규칙 4), 콘텐츠는 SO(규칙 5).
- 작업은 전용 워크트리에서: `git worktree add ../rogue-deck-run-unity-flow -b feat/run-unity-flow`
- 워크트리에서 Unity GUI 저작 금지 (규칙 17) — 씬·SO 에셋은 **에디터 배치 스크립트**로 생성하고, Play 검증은 머지 전 사용자가 명시 요청 시(또는 머지 후 메인 체크아웃에서) 수행한다. 배치 로그는 `/private/tmp`에.
- `Assets/` 아래 새 파일 `.meta`는 Unity `-batchmode` 실행으로 생성해 함께 커밋 (규칙 16).

---

### Task 1: RunRegistries 부팅 등록 + 원 사이클 헤드리스 통합 테스트

**Files:**
- Create: `Assets/Core/Simulation/Run/RunRegistries.cs`
- Test: `Assets/Core/Tests/EditMode/RunCycleIntegrationTests.cs`

**Interfaces:**
- Consumes: 4개 선행 계획의 모든 공개 타입 (`RunState`, `RunNodeRegistry`, `CombatNodeHandler`, `RecruitHealNodeHandler`, `RewardRoller`, `EnemyKinds`, `RunDefinitionValidator` 등)
- Produces: `RunRegistries.NodeHandlers(EnemyKindRegistry enemyKinds, int fateEnergyPerTurn) → RunNodeRegistry` — 4개 노드 키 전부 등록(보스는 `isBossNode: true`). `CombatRegistries` 패턴을 따른 부팅 조립 지점.

- [ ] **Step 1: 실패하는 테스트 작성**

스펙 §3.6의 두 테스트: (1) 결정론 — 같은 정의+시드 = 같은 진행 궤적, (2) 원 사이클 완주 — 시작→종착(승리 또는 패배).

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RunCycleIntegrationTests
    {
        private const int MaxTurnsPerCombat = 60;
        private const int FateEnergyPerTurn = 3;

        private static RunDefinition SkeletonMap() => new RunDefinition(new[]
        {
            new RunNodeData(RunNodeKeys.NormalCombat, new CombatNodePayload(
                new EncounterDefinition("n1", GoblinDeck.EnemyId, GoblinDeck.StartingHp))),
            new RunNodeData(RunNodeKeys.RecruitHeal, new RecruitHealPayload(
                new RecruitCandidate("member_b", "파티원 B", PartyPrototypeDeck.Build()), healPercent: 100)),
            new RunNodeData(RunNodeKeys.EliteCombat, new CombatNodePayload(
                new EncounterDefinition("e1", WardenDeck.EnemyId, WardenDeck.StartingHp))),
            new RunNodeData(RunNodeKeys.BossCombat, new CombatNodePayload(
                new EncounterDefinition("b1", WardenDeck.EnemyId, WardenDeck.StartingHp)))
        });

        private static List<string> PlayRun(int seed, out RunState run)
        {
            var trace = new List<string>();
            run = new RunState(
                SkeletonMap(),
                new[]
                {
                    new RunMember(
                        "member_a", "파티원 A",
                        PartyTuning.Prototype.DefaultMemberMaxHp, StarterDeck.Build())
                },
                PartyTuning.Prototype,
                seed);
            var registry = RunRegistries.NodeHandlers(EnemyKinds.Default(), FateEnergyPerTurn);
            var pools = new RewardPools(
                new Dictionary<string, IReadOnlyList<CardDefinition>>
                {
                    { "member_b", PartyPrototypeDeck.Build() }
                },
                StarterDeck.Build());

            Assert.That(
                RunDefinitionValidator.Validate(new RunDefinition(run.Nodes), registry), Is.Empty);

            while (run.Outcome == RunOutcome.InProgress)
            {
                var node = run.CurrentNode;
                var handler = registry.Resolve(node.Key);
                if (handler is CombatNodeHandler combat)
                {
                    var session = combat.CreateSession(run, node);
                    AutoPlay(session);
                    combat.ApplyResult(run, session);
                    if (run.Outcome == RunOutcome.InProgress && session.Outcome == Outcome.Win)
                    {
                        var roll = RewardRoller.Roll(run, pools, RewardTuning.Prototype);
                        if (roll.Count > 0)
                        {
                            RewardRoller.Apply(run, roll[0]); // 자동 플레이는 항상 첫 후보 선택
                        }

                        trace.Add("reward:" + string.Join(
                            ",", roll.Select(c => c.Card.Id + "@" + c.OwnerId)));
                    }

                    trace.Add($"combat:{node.Key}:{session.Outcome}"
                        + $":hp={string.Join("/", run.Party.Select(m => m.Hp))}");
                }
                else if (handler is RecruitHealNodeHandler recruitHeal)
                {
                    var result = recruitHeal.Resolve(run, node);
                    trace.Add($"camp:recruited={result.RecruitedMemberId ?? "none"}"
                        + $":hp={string.Join("/", run.Party.Select(m => m.Hp))}");
                }

                if (run.Outcome != RunOutcome.InProgress || !run.AdvanceToNextNode())
                {
                    break;
                }
            }

            trace.Add("outcome:" + run.Outcome);
            return trace;
        }

        /// <summary>무조작에 가까운 자동 플레이: 낼 수 있는 실행 카드를 앞에서부터 낸다.
        /// 개입 카드·수동 타겟팅은 쓰지 않는다 — 사이클 배선 검증이 목적이다.</summary>
        private static void AutoPlay(DeckCombatSession session)
        {
            for (int turn = 0; turn < MaxTurnsPerCombat && !session.IsComplete; turn++)
            {
                bool played = true;
                while (played && !session.CurrentTurnResolved)
                {
                    played = false;
                    for (int i = 0; i < session.Hand.Count; i++)
                    {
                        if (session.PlayExecutionCard(i))
                        {
                            played = true;
                            break;
                        }
                    }
                }

                session.ResolveTurn();
                if (!session.IsComplete)
                {
                    session.BeginNextTurn();
                }
            }
        }

        [Test]
        public void FullCycle_SameSeed_SameTrace()
        {
            var traceA = PlayRun(2026, out _);
            var traceB = PlayRun(2026, out _);
            Assert.That(traceA, Is.EqualTo(traceB));
        }

        [Test]
        public void FullCycle_ReachesTerminalOutcome()
        {
            PlayRun(2026, out var run);
            Assert.That(run.Outcome, Is.Not.EqualTo(RunOutcome.InProgress));
        }
    }
}
```

주의: `ReachesTerminalOutcome`이 자동 플레이 한계로 실패하면(전투가 `MaxTurnsPerCombat` 안에 안 끝남) 테스트 시나리오의 인카운터 HP를 낮춰 조정한다 — 게임 규칙이 아니라 테스트 데이터의 문제다. `PlayExecutionCard(int handIndex)`가 기본 타겟으로 배치되는지는 [DeckCombatSessionTests.cs](../../Assets/Core/Tests/EditMode/DeckCombatSessionTests.cs)의 기존 용례를 따른다.

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RunCycleIntegrationTests`
Expected: 컴파일 에러 (`RunRegistries` 미정의)

- [ ] **Step 3: 구현**

`RunRegistries.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    /// <summary>Boot-time node handler assembly, mirroring CombatRegistries. New node types:
    /// add one handler + one Register line here (AGENTS.md rule 9).</summary>
    public static class RunRegistries
    {
        public static RunNodeRegistry NodeHandlers(EnemyKindRegistry enemyKinds, int fateEnergyPerTurn)
        {
            var registry = new RunNodeRegistry();
            registry.Register(new CombatNodeHandler(
                RunNodeKeys.NormalCombat, isBossNode: false, enemyKinds, fateEnergyPerTurn));
            registry.Register(new CombatNodeHandler(
                RunNodeKeys.EliteCombat, isBossNode: false, enemyKinds, fateEnergyPerTurn));
            registry.Register(new CombatNodeHandler(
                RunNodeKeys.BossCombat, isBossNode: true, enemyKinds, fateEnergyPerTurn));
            registry.Register(new RecruitHealNodeHandler());
            return registry;
        }
    }
}
```

- [ ] **Step 4: 통과 확인 (전체)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 PASS

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/RunRegistries.cs Assets/Core/Tests/EditMode/RunCycleIntegrationTests.cs
git commit -m "feat(run): boot registry assembly and full-cycle determinism test"
```

---

### Task 2: SO 저작 파이프라인 — EncounterAsset·RunDefinitionAsset·RewardPoolAsset

**Files:**
- Create: `Assets/Unity/EncounterAsset.cs`
- Create: `Assets/Unity/RunDefinitionAsset.cs`
- Create: `Assets/Unity/RewardPoolAsset.cs`
- 참고(수정 없음): [Assets/Unity/CharacterAsset.cs](../../Assets/Unity/CharacterAsset.cs), [Assets/Unity/DeckAsset.cs](../../Assets/Unity/DeckAsset.cs) — `ToSpecs()` + `CardSpecMapper.ToDefinition` 변환 관례

**Interfaces:**
- Consumes: 코어 런 타입 전부, `CardSpecMapper.ToDefinition`, `CharacterAsset`(Id·DisplayName·Deck), `CardAsset.ToSpec()`
- Produces:
  - `EncounterAsset : ScriptableObject` — `ToDefinition() → EncounterDefinition`
  - `RunDefinitionAsset : ScriptableObject` — 노드 배열(종류 enum + 페이로드 참조) → `ToDefinition() → RunDefinition`
  - `RewardPoolAsset : ScriptableObject` — `ToPools() → RewardPools`

- [ ] **Step 1: 구현** (Unity SO — 컴파일 검증은 Step 2에서)

`EncounterAsset.cs`:

```csharp
using FateWeaver.Simulation.Run;
using UnityEngine;

namespace FateWeaver.Unity
{
    [CreateAssetMenu(menuName = "Fate Weaver/Encounter")]
    public sealed class EncounterAsset : ScriptableObject
    {
        [SerializeField] private string _id;
        [Tooltip("EnemyKinds.Default()에 등록된 키: goblin, warden")]
        [SerializeField] private string _enemyKindId;
        [SerializeField] private int _enemyMaxHp;

        public EncounterDefinition ToDefinition() => new EncounterDefinition(_id, _enemyKindId, _enemyMaxHp);
    }
}
```

`RunDefinitionAsset.cs` — 저작용 enum과 키 매핑은 SO→코어 변환 한 곳에만 존재한다:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Run;
using UnityEngine;

namespace FateWeaver.Unity
{
    [CreateAssetMenu(menuName = "Fate Weaver/Run Definition")]
    public sealed class RunDefinitionAsset : ScriptableObject
    {
        public enum NodeKind { NormalCombat, EliteCombat, BossCombat, RecruitHeal }

        [Serializable]
        public struct NodeEntry
        {
            public NodeKind Kind;
            [Tooltip("전투 노드 전용")] public EncounterAsset Encounter;
            [Tooltip("고용·회복 노드 전용 — 비우면 순수 회복")] public CharacterAsset RecruitCandidate;
            [Tooltip("고용·회복 노드 전용 — MaxHp 대비 %")] public int HealPercent;
        }

        private static readonly Dictionary<NodeKind, RunNodeKey> KeyByKind = new()
        {
            { NodeKind.NormalCombat, RunNodeKeys.NormalCombat },
            { NodeKind.EliteCombat, RunNodeKeys.EliteCombat },
            { NodeKind.BossCombat, RunNodeKeys.BossCombat },
            { NodeKind.RecruitHeal, RunNodeKeys.RecruitHeal }
        };

        [SerializeField] private NodeEntry[] _nodes = Array.Empty<NodeEntry>();

        public RunDefinition ToDefinition()
        {
            var nodes = new List<RunNodeData>();
            foreach (var entry in _nodes)
            {
                nodes.Add(new RunNodeData(KeyByKind[entry.Kind], PayloadFor(entry)));
            }

            return new RunDefinition(nodes);
        }

        private static IRunNodePayload PayloadFor(NodeEntry entry)
        {
            if (entry.Kind == NodeKind.RecruitHeal)
            {
                var candidate = entry.RecruitCandidate == null
                    ? null
                    : new RecruitCandidate(
                        entry.RecruitCandidate.Id,
                        entry.RecruitCandidate.DisplayName,
                        entry.RecruitCandidate.Deck.ToSpecs()
                            .Select(CardSpecMapper.ToDefinition).ToList());
                return new RecruitHealPayload(candidate, entry.HealPercent);
            }

            return new CombatNodePayload(entry.Encounter.ToDefinition());
        }
    }
}
```

`RewardPoolAsset.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Run;
using UnityEngine;

namespace FateWeaver.Unity
{
    [CreateAssetMenu(menuName = "Fate Weaver/Reward Pools")]
    public sealed class RewardPoolAsset : ScriptableObject
    {
        [Serializable]
        public struct UniquePool
        {
            public CharacterAsset Character;
            public CardAsset[] Cards;
        }

        [SerializeField] private UniquePool[] _uniquePools = Array.Empty<UniquePool>();
        [SerializeField] private CardAsset[] _genericCards = Array.Empty<CardAsset>();

        public RewardPools ToPools()
        {
            var unique = new Dictionary<string, IReadOnlyList<CardDefinition>>();
            foreach (var pool in _uniquePools)
            {
                unique[pool.Character.Id] = pool.Cards
                    .Select(card => CardSpecMapper.ToDefinition(card.ToSpec())).ToList();
            }

            IReadOnlyList<CardDefinition> generic = _genericCards
                .Select(card => CardSpecMapper.ToDefinition(card.ToSpec())).ToList();
            return new RewardPools(unique, generic);
        }
    }
}
```

주의: `CardAsset.ToSpec()`·`CardSpecMapper.ToDefinition` 시그니처는 [BattleScreenController.cs:77](../../Assets/Unity/BattleScreenController.cs)의 기존 용례와 동일하게 맞춘다.

- [ ] **Step 2: 컴파일 검증**

Unity `-batchmode` EditMode 테스트 1회 실행(규칙 17 허용) — 컴파일 확인 + `.meta` 생성. 헤드리스도 재실행: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0` → 전부 PASS.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/EncounterAsset.cs* Assets/Unity/RunDefinitionAsset.cs* Assets/Unity/RewardPoolAsset.cs*
git commit -m "feat(unity): SO authoring pipeline for run definition, encounters, reward pools"
```

---

### Task 3: BattleScreenController 외부 세션 주입 심(seam)

**Files:**
- Modify: `Assets/Unity/BattleScreenController.cs`

**Interfaces:**
- Consumes: 기존 private `StartSession`(63행)·`SpawnUnits`·`BindPiles`·`RefreshAll`·`PresentationFor`, `OnTurnButton`(304행)의 `IsComplete` 처리
- Produces:
  - `public void LaunchExternal(DeckCombatSession session, Action onCombatEnded)` — 외부(RunController)가 만든 세션으로 전투 UI를 구동. 내부적으로 기존 `StartSession`의 세션 생성부만 건너뛰고 `BuildArtLookup → SpawnUnits → BindPiles → RefreshAll` 공통 초기화를 재사용한다.
  - `public CardPresentation BuildPresentation(OwnedCard card)` — private `PresentationFor` 공개 래퍼 (보상 패널 표현 생성용).
  - `[SerializeField] private bool _standaloneMode = true` — 기존 씬(FateWeaverBattle)은 true 유지(기존 동작 불변), 런 씬은 false로 저작. `Start()`의 자동 `StartSession()` 호출을 `_standaloneMode`일 때만 하도록 감싼다. `_resetButton`도 standalone일 때만 동작(외부 모드에서는 버튼 비활성).

**구현 방법:** `StartSession()`을 둘로 쪼갠다 — 세션 생성부(기존 66~87행)와 공통 초기화부(88행 이후 `BuildArtLookup(); SpawnUnits(); BindPiles(); SetMessage(...); RefreshAll();`). 공통부를 `private void InitializeViews()`로 추출하고:

```csharp
public void LaunchExternal(DeckCombatSession session, Action onCombatEnded)
{
    _selection.CancelSelection();
    _session = session;
    _onCombatEnded = onCombatEnded;
    InitializeViews();
}

public CardPresentation BuildPresentation(OwnedCard card) => PresentationFor(card);

private Action _onCombatEnded;
```

`OnTurnButton()`의 `IsComplete` 분기(314행 근처)에서 결과 메시지 출력 후:

```csharp
if (_session.IsComplete && _onCombatEnded != null)
{
    var callback = _onCombatEnded;
    _onCombatEnded = null;
    callback();
}
```

콜백은 해석 완료 메시지를 사용자가 볼 수 있게 **턴 버튼으로 완료를 확인한 시점**(기존 `IsComplete` 분기)에서만 호출한다. 정확한 삽입 위치는 구현 시 해당 메서드를 읽고 결정하되, 기존 standalone 동작(리셋 버튼·자동 시작)이 회귀하지 않아야 한다.

- [ ] **Step 1: 구현** (위 명세대로 — 기존 씬 동작 불변이 최우선)

- [ ] **Step 2: 컴파일 + 기존 테스트 검증**

Unity `-batchmode` EditMode 테스트 실행 → 컴파일·기존 테스트 통과 확인. 헤드리스 전체도 PASS 확인.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/BattleScreenController.cs
git commit -m "feat(unity): battle screen accepts externally created run sessions"
```

---

### Task 4: MapStripView·RunController — 화면 흐름

**Files:**
- Create: `Assets/Unity/MapStripView.cs`
- Create: `Assets/Unity/MapNodeIconView.cs`
- Create: `Assets/Unity/RunController.cs`

**Interfaces:**
- Consumes: Task 1~3 산출물 + 4개 선행 계획의 코어 타입, `RewardPanelController`(run-reward 계획)
- Produces:
  - `MapNodeIconView : MonoBehaviour` — `Bind(string label, bool isCurrent, bool isCleared)`; `[SerializeField]` TMP_Text 라벨 + 하이라이트 Image
  - `MapStripView : MonoBehaviour` — `Bind(IReadOnlyList<RunNodeData> nodes, int currentIndex)`; 노드마다 `MapNodeIconView` 프리팹 인스턴스화(라벨: 일반 "전투"/엘리트 "정예"/보스 "보스"/고용·회복 "야영" — 키→라벨 매핑은 인스펙터 저작 또는 뷰 내 사전)
  - `RunController : MonoBehaviour` — 씬의 런 오케스트레이터:
    - 직렬화 필드: `RunDefinitionAsset _runDefinition`, `CharacterAsset _startingCharacter`, `RewardPoolAsset _rewardPools`, `int _runSeed`, `int _fateEnergyPerTurn`, `BattleScreenController _battle`, `RewardPanelController _rewardPanel`, `MapStripView _mapStrip`, 패널 `GameObject` 4개(`_mapPanel`/`_battlePanel`/`_campPanel`/`_resultPanel`), `Button _enterNodeButton`/`_campContinueButton`/`_newRunButton`, `TMP_Text _campText`/`_resultText`
    - `Start()` → `StartNewRun()`: SO→코어 변환, 시작 캐릭터 1인으로 `RunState` 생성, `RunRegistries.NodeHandlers(EnemyKinds.Default(), _fateEnergyPerTurn)` 조립, `RunDefinitionValidator` 통과 확인(에러면 메시지 표시 후 중단), 맵 패널 표시
    - 노드 진입 버튼 → `registry.Resolve(node.Key)`:
      - `CombatNodeHandler` → `CreateSession` → `_battle.LaunchExternal(session, () => OnCombatEnded(handler, session))` → 전투 패널
      - `RecruitHealNodeHandler` → `Resolve` → 캠프 패널에 결과 텍스트(회복량·합류자) → 계속 버튼 → `AdvanceOrFinish()`
    - `OnCombatEnded`: `handler.ApplyResult(run, session)`; 승리·런 계속이면 `RewardRoller.Roll` → `_rewardPanel.Show(candidates, presentations, onPicked: i => { RewardRoller.Apply(run, candidates[i]); AdvanceOrFinish(); }, onSkipped: AdvanceOrFinish)` — presentation은 `_battle.BuildPresentation(new OwnedCard(candidate.Card, candidate.OwnerId))`로 생성; 그 외는 곧장 `AdvanceOrFinish()`
    - `AdvanceOrFinish()`: `run.Outcome != InProgress` → 결과 패널("승리!"/"패배…" + 새 런 버튼 → `StartNewRun()`); 아니면 `AdvanceToNextNode()` 후 맵 패널 갱신
    - 패널 전환은 `GameObject.SetActive`만 사용. 모든 참조는 인스펙터 할당(규칙 2·3).

- [ ] **Step 1: MapNodeIconView·MapStripView 구현**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    public sealed class MapNodeIconView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _highlight;
        [SerializeField] private Color _currentColor;
        [SerializeField] private Color _clearedColor;
        [SerializeField] private Color _pendingColor;

        public void Bind(string label, bool isCurrent, bool isCleared)
        {
            _label.text = label;
            _highlight.color = isCurrent ? _currentColor : isCleared ? _clearedColor : _pendingColor;
        }
    }
}
```

```csharp
using System.Collections.Generic;
using FateWeaver.Simulation.Run;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Linear run map strip: one icon per node, current position highlighted.</summary>
    public sealed class MapStripView : MonoBehaviour
    {
        [SerializeField] private MapNodeIconView _iconPrefab;
        [SerializeField] private RectTransform _row;

        private static readonly Dictionary<RunNodeKey, string> LabelByKey = new()
        {
            { RunNodeKeys.NormalCombat, "전투" },
            { RunNodeKeys.EliteCombat, "정예" },
            { RunNodeKeys.BossCombat, "보스" },
            { RunNodeKeys.RecruitHeal, "야영" }
        };

        public void Bind(IReadOnlyList<RunNodeData> nodes, int currentIndex)
        {
            foreach (Transform child in _row)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var icon = Instantiate(_iconPrefab, _row);
                LabelByKey.TryGetValue(nodes[i].Key, out var label);
                icon.Bind(label ?? nodes[i].Key.Id, i == currentIndex, i < currentIndex);
            }
        }
    }
}
```

- [ ] **Step 2: RunController 구현** (위 Produces 명세대로. 메서드 구조: `StartNewRun` / `ShowMap` / `OnEnterNode` / `OnCombatEnded` / `ShowRewards` / `AdvanceOrFinish` / `ShowResult` + 패널 전환 헬퍼 `SwitchPanel(GameObject active)`. 모든 흐름 결정은 코어 결과 값으로만 — 코어 내부 상태를 새로 뒤지지 않는다, 규칙 11.)

- [ ] **Step 3: 컴파일 검증** — Unity `-batchmode` EditMode 테스트 + 헤드리스 전체 PASS 확인.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Unity/MapStripView.cs* Assets/Unity/MapNodeIconView.cs* Assets/Unity/RunController.cs*
git commit -m "feat(unity): run controller with map strip and panel flow"
```

---

### Task 5: 씬·콘텐츠 생성 (에디터 빌더) + 사용자 검증 인계

**Files:**
- Create: `Assets/Unity/Editor/RunSceneBuilder.cs`
- Create(에디터 스크립트가 생성): `Assets/Scenes/FateWeaverRun.unity`, `Assets/Unity/RunSO/` 아래 콘텐츠 에셋들

**Interfaces:**
- Consumes: [Assets/Unity/Editor/BattleSceneBuilder.cs](../../Assets/Unity/Editor/BattleSceneBuilder.cs)의 씬 조립 패턴(메뉴 아이템 → 프로그램적으로 캔버스·패널·프리팹 배선 → 씬 저장), `AssetDatabase.CreateAsset`
- Produces: 메뉴 `Fate Weaver/Build Run Scene` — 실행 시:
  1. **콘텐츠 SO 생성** (`Assets/Unity/RunSO/`):
     - `encounter_normal.asset` — goblin, HP `GoblinDeck.StartingHp`
     - `encounter_elite.asset` — warden, HP `WardenDeck.StartingHp`
     - `encounter_boss.asset` — warden, HP는 엘리트의 2배(초안 수치 — 인스펙터에서 조정 가능한 데이터)
     - `run_skeleton.asset` (RunDefinitionAsset) — 스펙 §5 예시 배열: 일반 → 일반 → 고용·회복(member_b 합류, 회복 100%) → 일반 → 엘리트 → 고용·회복(후보 없음, 회복 100%) → 보스
     - `reward_pools.asset` — 고유 풀: member_a=StarterDeck 카드들, member_b=PartyPrototypeDeck 카드들; 범용 풀: guard·quick_cut 등 기존 CardAsset 일부
  2. **씬 조립** — 기존 FateWeaverBattle 씬을 복제/재구성해 전투 패널로 삼고, 맵/캠프/결과 패널과 `RunController`·`RewardPanelController`·`MapStripView`(+ MapNodeIconView 프리팹, CardView 프리팹 재사용)를 추가, 모든 `[SerializeField]` 참조를 스크립트에서 배선(`_standaloneMode = false` 포함), `FateWeaverRun.unity`로 저장.
- 세부 조립 코드는 `BattleSceneBuilder`의 형태를 그대로 따른다 (규칙 13 — 기존 패턴 검색·준수).

- [ ] **Step 1: RunSceneBuilder 작성** (위 명세, BattleSceneBuilder 패턴)
- [ ] **Step 2: 배치 실행으로 씬·에셋 생성** — Unity `-batchmode -executeMethod`로 빌더 실행(로그 `/private/tmp`), `git status`로 생성물·`.meta` 확인 후 의도한 파일만 스테이징
- [ ] **Step 3: 헤드리스 전체 테스트 최종 확인** — `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0` 전부 PASS
- [ ] **Step 4: 커밋**

```bash
git add Assets/Unity/Editor/RunSceneBuilder.cs* Assets/Scenes/FateWeaverRun.unity* Assets/Unity/RunSO/
git commit -m "feat(unity): run scene builder with skeleton map content"
```

- [ ] **Step 5: 사용자 검증 인계** — 워크트리에서는 Play 검증을 하지 않는다(규칙 17). 사용자에게 다음 체크리스트로 메인 체크아웃(머지 후) 또는 명시 요청 시 워크트리 Play 확인을 요청한다:
  1. FateWeaverRun 씬 Play → 맵에 노드 7개, 첫 노드 하이라이트
  2. 일반 전투 승리 → 보상 3장 표시, 선택/스킵 모두 동작
  3. 야영 노드 → 회복 + member_b 합류(2인 전투 확인)
  4. 피해 이월 확인(전투 종료 HP가 다음 전투 시작 HP)
  5. 보스 승리 → 승리 화면 → 새 런; 전멸 → 패배 화면 → 새 런
  6. 기존 FateWeaverBattle 씬이 여전히 단독 동작(회귀 없음)

---

## 완료 기준

- 전체 헤드리스 테스트 + Unity batchmode EditMode 테스트 통과.
- 사용자 Play 검증 체크리스트 통과 후, 사용자 승인을 받아 master 머지 (규칙 19). 머지 후 워크트리·브랜치 정리.
