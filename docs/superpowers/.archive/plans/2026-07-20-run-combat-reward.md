# 전투 보상(Combat Reward) Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 전투 승리 보상 — 런 시드로 카드 3장(수치는 데이터)을 롤해 후보를 만들고(고유 카드는 보유 캐릭터가 있을 때만·더 높은 비중, 획득 시 캐릭터 귀속), 1장 선택 또는 스킵을 `RunState`에 적용한다. 선택 UI는 전투 씬에서 전투 완료 후 뜨는 보상 패널로 처리한다.

**Architecture:** 스펙 [2026-07-20-run-cycle-skeleton-design.md](../specs/2026-07-20-run-cycle-skeleton-design.md) §3.4. 코어(`RewardRoller`)는 순수 함수 — 롤과 적용만 안다. Unity `RewardPanelController`는 미리 만들어진 `CardPresentation` 목록을 받아 표시만 한다(카드 표현 생성은 통합 계획인 run-unity-flow가 담당) — 이 경계 덕에 이 계획은 다른 병렬 계획과 파일이 겹치지 않는다.

**Tech Stack:** C# 9, NUnit, `dotnet test` 헤드리스 하니스, Unity UGUI(패널만).

## Global Constraints

- **선행 조건: `feat/run-core`(run-core-foundation 계획)가 master에 머지된 뒤 시작한다.**
- 헤드리스 테스트 실행: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
- LangVersion 9. `Assets/Core/**`는 UnityEngine 참조 금지. Unity 파일은 `Assets/Unity/`에, `[SerializeField] private` 사용 (규칙 4), 문자열 탐색 금지 (규칙 3).
- 무작위는 전부 `RunState.Rng` 경유 (규칙 7).
- 튜닝 수치 하드코딩 금지 (규칙 8) — 후보 수·가중치는 `RewardTuning` 데이터.
- 작업은 전용 워크트리에서: `git worktree add ../rogue-deck-run-reward -b feat/run-reward`
- `Assets/` 아래 새 파일의 `.meta`는 병합 전 Unity `-batchmode` 1회 실행으로 생성해 함께 커밋 (규칙 16·17).
- 전용 워크트리에서는 Unity GUI를 열지 않는다 (규칙 17) — 패널 프리팹 저작·수동 확인은 통합(run-unity-flow) 및 사용자 검증 단계에서 한다.

---

### Task 1: RewardTuning·RewardPools·RewardCandidate 데이터

**Files:**
- Create: `Assets/Core/Simulation/Run/RewardTuning.cs`
- Create: `Assets/Core/Simulation/Run/RewardPools.cs`
- Create: `Assets/Core/Simulation/Run/RewardCandidate.cs`
- Test: `Assets/Core/Tests/EditMode/RewardRollerTests.cs` (신규 — Task 2·3이 같은 파일에 테스트 추가)

**Interfaces:**
- Consumes: `CardDefinition`
- Produces:
  - `RewardTuning { int CandidateCount; int UniqueWeight; int GenericWeight; }` (init 프로퍼티) + `RewardTuning.Prototype` (후보 3, 고유 70 : 범용 30 — 초안 수치, 시뮬레이션으로 조율)
  - `RewardPools(IReadOnlyDictionary<string, IReadOnlyList<CardDefinition>> uniqueByCharacter, IReadOnlyList<CardDefinition> generic)` — `UniqueByCharacter`, `Generic`
  - `RewardCandidate(CardDefinition card, string ownerId)` — `Card`, `OwnerId`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RewardRollerTests
    {
        [Test]
        public void Tuning_Prototype_HasPositiveCandidateCountAndWeights()
        {
            var tuning = RewardTuning.Prototype;
            Assert.That(tuning.CandidateCount, Is.GreaterThan(0));
            Assert.That(tuning.UniqueWeight, Is.GreaterThan(0));
            Assert.That(tuning.GenericWeight, Is.GreaterThan(0));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RewardRollerTests`
Expected: 컴파일 에러

- [ ] **Step 3: 구현**

`RewardTuning.cs` (기존 `PartyTuning`의 init 패턴):

```csharp
namespace FateWeaver.Simulation.Run
{
    /// <summary>Reward roll tuning (AGENTS.md rule 8: data, not constants in logic).</summary>
    public sealed class RewardTuning
    {
        public int CandidateCount { get; init; }
        public int UniqueWeight { get; init; }
        public int GenericWeight { get; init; }

        /// <summary>초안 수치 — Compare 하니스/플레이테스트로 조율한다.</summary>
        public static RewardTuning Prototype => new RewardTuning
        {
            CandidateCount = 3,
            UniqueWeight = 70,
            GenericWeight = 30
        };
    }
}
```

`RewardPools.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Acquirable card pools (party-foundation §2): character-unique pools appear only
    /// while that character is alive in the party; generic cards can go to anyone.</summary>
    public sealed class RewardPools
    {
        public IReadOnlyDictionary<string, IReadOnlyList<CardDefinition>> UniqueByCharacter { get; }
        public IReadOnlyList<CardDefinition> Generic { get; }

        public RewardPools(
            IReadOnlyDictionary<string, IReadOnlyList<CardDefinition>> uniqueByCharacter,
            IReadOnlyList<CardDefinition> generic)
        {
            UniqueByCharacter = uniqueByCharacter;
            Generic = generic;
        }
    }
}
```

`RewardCandidate.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>One rolled reward option: the card plus the character it would belong to
    /// (every card is character-owned on acquisition — party-foundation §2).</summary>
    public sealed class RewardCandidate
    {
        public CardDefinition Card { get; }
        public string OwnerId { get; }

        public RewardCandidate(CardDefinition card, string ownerId)
        {
            Card = card;
            OwnerId = ownerId;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RewardRollerTests`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/ Assets/Core/Tests/EditMode/RewardRollerTests.cs
git commit -m "feat(run): add reward tuning, pools, and candidate data types"
```

---

### Task 2: RewardRoller.Roll

**Files:**
- Create: `Assets/Core/Simulation/Run/RewardRoller.cs`
- Test: `Assets/Core/Tests/EditMode/RewardRollerTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: foundation의 `RunState`(·`Rng`·`LivingMembers`), Task 1 데이터
- Produces: `RewardRoller.Roll(RunState run, RewardPools pools, RewardTuning tuning) → IReadOnlyList<RewardCandidate>`
- 규칙 (테스트가 스펙):
  1. 후보 수 = `tuning.CandidateCount` (뽑을 풀이 하나도 없으면 그만큼 줄어들 수 있음).
  2. 각 후보: 고유/범용을 가중치로 선택 → 고유면 생존 보유 캐릭터의 풀에서, 범용이면 생존 멤버 중 무작위 귀속.
  3. 고유 풀은 **생존** 멤버 것만 후보가 된다 (사망·미보유 캐릭터의 고유 카드는 등장하지 않는다).
  4. 모든 무작위는 `run.Rng` — 같은 런 시드 = 같은 후보열.
  5. 후보 간 중복 카드는 허용한다 (뼈대 단순화 — 스펙 §3.4 후보 수·가중치만 규정).

- [ ] **Step 1: 실패하는 테스트 작성 (추가)**

```csharp
private static RunState RunWith(int seed, params RunMember[] members)
{
    var definition = new RunDefinition(new[] { new RunNodeData(RunNodeKeys.NormalCombat, null) });
    return new RunState(definition, members, PartyTuning.Prototype, seed);
}

private static RewardPools PoolsFor(string uniqueOwnerId)
{
    var unique = new Dictionary<string, IReadOnlyList<CardDefinition>>
    {
        { uniqueOwnerId, PartyPrototypeDeck.Build() }
    };
    return new RewardPools(unique, StarterDeck.Build());
}

[Test]
public void Roll_ReturnsCandidateCount_AndIsDeterministicPerSeed()
{
    var tuning = RewardTuning.Prototype;
    var rollA = RewardRoller.Roll(
        RunWith(11, new RunMember("member_a", "A", 25, null)), PoolsFor("member_a"), tuning);
    var rollB = RewardRoller.Roll(
        RunWith(11, new RunMember("member_a", "A", 25, null)), PoolsFor("member_a"), tuning);

    Assert.That(rollA.Count, Is.EqualTo(tuning.CandidateCount));
    Assert.That(
        rollA.Select(c => c.Card.Id + "@" + c.OwnerId).ToList(),
        Is.EqualTo(rollB.Select(c => c.Card.Id + "@" + c.OwnerId).ToList()));
}

[Test]
public void Roll_NeverOffersUniqueCardsOfDeadOrAbsentOwners()
{
    var deadOwner = new RunMember("member_b", "B", 25, null);
    deadOwner.Hp = 0;
    var run = RunWith(3, new RunMember("member_a", "A", 25, null), deadOwner);
    var uniqueCardIds = PartyPrototypeDeck.Build().Select(c => c.Id).ToHashSet();

    // member_b(사망)의 고유 풀만 있는 상황 — 고유 후보는 나올 수 없다
    var roll = RewardRoller.Roll(run, PoolsFor("member_b"), RewardTuning.Prototype);

    Assert.That(roll.All(c => !uniqueCardIds.Contains(c.Card.Id) || c.OwnerId != "member_b"), Is.True);
    Assert.That(roll.All(c => c.OwnerId == "member_a"), Is.True); // 귀속도 생존자에게만
}

[Test]
public void Roll_AssignsEveryCandidateALivingOwner()
{
    var run = RunWith(5,
        new RunMember("member_a", "A", 25, null),
        new RunMember("member_b", "B", 25, null));

    var roll = RewardRoller.Roll(run, PoolsFor("member_a"), RewardTuning.Prototype);

    Assert.That(roll, Is.Not.Empty);
    Assert.That(roll.All(c => c.OwnerId == "member_a" || c.OwnerId == "member_b"), Is.True);
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RewardRollerTests`
Expected: 컴파일 에러 (`RewardRoller` 미정의)

- [ ] **Step 3: 구현**

`RewardRoller.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Rolls post-combat reward candidates and applies the pick. Pure functions over
    /// RunState; all randomness goes through run.Rng (AGENTS.md rule 7) so the same run seed
    /// produces the same candidate sequence (spec §3.6 determinism test).</summary>
    public static class RewardRoller
    {
        public static IReadOnlyList<RewardCandidate> Roll(
            RunState run, RewardPools pools, RewardTuning tuning)
        {
            var candidates = new List<RewardCandidate>();
            var living = run.LivingMembers;
            var uniqueOwners = living
                .Where(m => pools.UniqueByCharacter.TryGetValue(m.Id, out var pool) && pool.Count > 0)
                .ToList();

            for (int i = 0; i < tuning.CandidateCount; i++)
            {
                int uniqueWeight = uniqueOwners.Count > 0 ? tuning.UniqueWeight : 0;
                int genericWeight = pools.Generic.Count > 0 && living.Count > 0 ? tuning.GenericWeight : 0;
                int total = uniqueWeight + genericWeight;
                if (total == 0)
                {
                    break;
                }

                if (run.Rng.Next(total) < uniqueWeight)
                {
                    var owner = uniqueOwners[run.Rng.Next(uniqueOwners.Count)];
                    var pool = pools.UniqueByCharacter[owner.Id];
                    candidates.Add(new RewardCandidate(pool[run.Rng.Next(pool.Count)], owner.Id));
                }
                else
                {
                    var owner = living[run.Rng.Next(living.Count)];
                    candidates.Add(new RewardCandidate(
                        pools.Generic[run.Rng.Next(pools.Generic.Count)], owner.Id));
                }
            }

            return candidates;
        }

        /// <summary>Applies the picked candidate: the card joins its owner's run cards.
        /// Skipping is simply not calling Apply.</summary>
        public static void Apply(RunState run, RewardCandidate picked)
        {
            var owner = run.Party.First(m => m.Id == picked.OwnerId);
            owner.Cards.Add(picked.Card);
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RewardRollerTests`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/RewardRoller.cs Assets/Core/Tests/EditMode/RewardRollerTests.cs
git commit -m "feat(run): deterministic reward roll with unique/generic weighting"
```

---

### Task 3: RewardRoller.Apply

**Files:**
- Modify: `Assets/Core/Simulation/Run/RewardRoller.cs` (Task 2에서 이미 작성 — 테스트로 검증)
- Test: `Assets/Core/Tests/EditMode/RewardRollerTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: Task 2의 `Apply`
- Produces: 검증된 적용 규칙 — 획득 카드는 소유 캐릭터의 `RunMember.Cards`에 추가된다

- [ ] **Step 1: 테스트 작성 (추가)**

```csharp
[Test]
public void Apply_AddsCardToOwnersRunCards()
{
    var member = new RunMember("member_a", "A", 25, StarterDeck.Build());
    var run = RunWith(1, member);
    int before = member.Cards.Count;
    var card = PartyPrototypeDeck.Build()[0];

    RewardRoller.Apply(run, new RewardCandidate(card, "member_a"));

    Assert.That(member.Cards.Count, Is.EqualTo(before + 1));
    Assert.That(member.Cards[member.Cards.Count - 1].Id, Is.EqualTo(card.Id));
}
```

- [ ] **Step 2: 실행 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RewardRollerTests`
Expected: PASS (Task 2에서 구현 완료). 실패 시 테스트에 맞게 구현 수정.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Core/Tests/EditMode/RewardRollerTests.cs
git commit -m "test(run): verify reward apply attaches card to its owner"
```

---

### Task 4: RewardPanelController (Unity 보상 패널)

**Files:**
- Create: `Assets/Unity/RewardPanelController.cs`
- 참고(수정 없음): [Assets/Unity/CardView.cs](../../Assets/Unity/CardView.cs) — `Bind(CardPresentation, Action onClick)`, [Assets/Unity/PileView.cs](../../Assets/Unity/PileView.cs) — CardView 프리팹 인스턴스화 관례

**Interfaces:**
- Consumes: `CardView`(기존 프리팹 `Assets/Unity/Prefabs/CardView.prefab`), `CardPresentation`, `RewardCandidate`
- Produces: `RewardPanelController : MonoBehaviour`
  - `void Show(IReadOnlyList<RewardCandidate> candidates, IReadOnlyList<CardPresentation> presentations, Action<int> onPicked, Action onSkipped)` — 인덱스로 콜백. **카드 표현(CardPresentation) 생성은 호출자 책임** — 통합 계획(run-unity-flow)이 전투 컨트롤러의 표현 로직을 재사용해 만든 목록을 넘긴다. 이 경계 덕에 이 파일은 코어 설명 컴포저에 직접 의존하지 않는다.
  - `void Hide()`
- 프리팹 저작(패널 배치·버튼 연결)은 run-unity-flow의 씬 빌더 단계에서 한다. 여기서는 컴포넌트 코드만 만든다.

- [ ] **Step 1: 구현** (Unity 컴포넌트 — 헤드리스 테스트 불가. 컴파일 검증은 Step 2)

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Simulation.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Post-combat reward panel: shows rolled candidates as cards, pick one or skip.
    /// Presentation building is the caller's job — this panel only displays and reports the pick.</summary>
    public sealed class RewardPanelController : MonoBehaviour
    {
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private RectTransform _cardRow;
        [SerializeField] private Button _skipButton;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private GameObject _root;

        private readonly List<CardView> _spawned = new List<CardView>();
        private Action<int> _onPicked;
        private Action _onSkipped;

        private void Awake()
        {
            _skipButton.onClick.AddListener(OnSkip);
        }

        public void Show(
            IReadOnlyList<RewardCandidate> candidates,
            IReadOnlyList<CardPresentation> presentations,
            Action<int> onPicked,
            Action onSkipped)
        {
            _onPicked = onPicked;
            _onSkipped = onSkipped;
            Clear();
            _root.SetActive(true);
            _titleText.text = "보상 카드를 1장 선택하세요";

            for (int i = 0; i < candidates.Count; i++)
            {
                int index = i;
                var view = Instantiate(_cardPrefab, _cardRow);
                view.Bind(presentations[i], () => OnPick(index));
                _spawned.Add(view);
            }
        }

        public void Hide()
        {
            Clear();
            _root.SetActive(false);
        }

        private void OnPick(int index)
        {
            var callback = _onPicked;
            Hide();
            callback?.Invoke(index);
        }

        private void OnSkip()
        {
            var callback = _onSkipped;
            Hide();
            callback?.Invoke();
        }

        private void Clear()
        {
            foreach (var view in _spawned)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _spawned.Clear();
        }
    }
}
```

주의: `CardPresentation`의 네임스페이스가 `FateWeaver.Simulation.Presentation`이 아니면 실제 위치([Assets/Unity/CardPresentation.cs](../../Assets/Unity/CardPresentation.cs) 또는 Core Presentation 폴더)를 확인해 using을 맞춘다.

- [ ] **Step 2: 컴파일 검증**

워크트리에서 Unity `-batchmode` EditMode 테스트 1회 실행 (규칙 17 허용 범위) — 컴파일 에러 없음을 확인하고 `.meta`도 이때 생성된다. 로그는 `/private/tmp`에 저장.

- [ ] **Step 3: 헤드리스 전체 테스트 재확인** (코어 변경이 없는지 확인용)

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 PASS

- [ ] **Step 4: 커밋**

```bash
git add Assets/Unity/RewardPanelController.cs Assets/Unity/RewardPanelController.cs.meta
git commit -m "feat(unity): reward panel component for post-combat card pick/skip"
```

---

## 완료 기준

- 전체 헤드리스 테스트 통과 + Unity batchmode 컴파일 통과.
- 보상 패널의 씬 배치·프리팹 연결·수동 확인은 run-unity-flow 계획에서 통합한다.
- 머지는 사용자 승인 후 (규칙 19). 전투 노드/고용·회복 계획과 파일 겹침 없음 — 병렬 머지 가능.
