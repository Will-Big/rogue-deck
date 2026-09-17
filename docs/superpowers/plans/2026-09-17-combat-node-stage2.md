# 전투 노드 2단계 — 구성 저작 구현 계획

사람 검수용 개요는 [`2026-09-17-combat-node-stage2.html`](2026-09-17-combat-node-stage2.html)에 있다. 이 문서는 세션
인계용이며 `## 상세`만 담는다. 개요와 상세가 어긋나면 상세를 따르지 않고 멈추고 묻는다(규칙 29).

설계 근거: [전투 노드 한 사이클 — 상세](../specs/2026-09-15-combat-node-cycle-design.md) `### 2단계 — 구성 저작`(2.1~2.10)과
결정 14~16. **이 계획과 설계가 어긋나면 멈추고 묻는다.**

## 상세

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 고블린·파티 수치·전투 규칙의 C# 원본(`GoblinDeck`·`GoblinEncounterSource`·`PartyPrototypeRoster`·`PartyTuning.Prototype`·`PlaytestKoreanText.EnemyName`)을 JSON 저작으로 옮기고, 같은 입력이면 전투 서명이 1단계와 바이트 단위로 같음을 증명한 뒤 원본을 지운다.

**Architecture:** 적 정책 3종·`IEnemyTurnPolicy`·`EnemyCardBundle`·`PartyTuning`을 `FateWeaver.Core`로 옮겨 콘텐츠 로더가 참조할 수 있게 한다. `ContentBootstrap.Load` 한 번이 적·편성·전투 규칙까지 읽고 정책 키를 `AuthoringContext`로 검증한다. Simulation의 `ContentEncounterSource`가 편성 스트림으로 편성을 고르고 레지스트리로 정책을 매번 새로 만든다.

**Tech Stack:** Unity 6000.5.2f1(C# 9), 순수 C# 코어, Newtonsoft.Json(`ContentJson`), NUnit 헤드리스(`dotnet test`), Unity EditMode 배치.

## Global Constraints

- 작업 장소: 워크트리 `/Users/ish/Git/rogue-deck/.claude/worktrees/combat-node-stage2`, 브랜치 `combat-node-stage2`. **메인 체크아웃에서 브랜치를 전환하지 않는다**(규칙 15). 아래 모든 경로는 워크트리 루트 기준.
- 헤드리스 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter <이름>` (이 머신은 .NET 5 SDK뿐이라 타깃 오버라이드 필수 — `docs/agents/commands.md`). 전체는 `Tools/verify.sh`.
- **헤드리스는 어셈블리 경계를 잡지 못한다.** `Tests/Headless/FateWeaver.Tests.Headless.csproj`가 Core·Simulation·테스트를 한 어셈블리로 컴파일한다. Core가 Simulation을 참조하는 실수는 Unity에서만 드러나므로, Core 파일을 만들거나 옮긴 태스크 끝에 아래 검사를 돌린다. 출력이 비어야 한다:
  `/usr/bin/grep -rln "FateWeaver.Simulation" Assets/Core --include='*.cs' | /usr/bin/grep -v "^Assets/Core/Simulation/\|^Assets/Core/Tests/"`
- 커밋 메시지는 한국어, `타입(범위): 제목`, 제목은 `-ㄴ다`로 끝낸다(규칙 27). 본문 끝에 `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- 커밋마다 `Tools/verify.sh --quick`가 통과해야 한다. 9단계 끝에서 `Tools/verify.sh` 전체.
- **새 `.cs`·`.json`·폴더의 `.meta`는 Task 8의 Unity 배치가 만든다.** 1단계와 같은 방식이다(`e2fb207`이 1단계 신규 파일의 `.meta`를 한꺼번에 커밋). Task 1~7은 `.meta` 없이 커밋한다. **옮기는 파일은 `git mv`로 `.meta`를 함께 옮긴다**(GUID 유지).
- 튜닝 수치를 C#에 새로 박지 않는다(규칙 8). 결정론: 무작위는 전달받은 `Random`에서만(규칙 7).
- 런타임 C#에 새 한글 문자열을 넣지 않는다(오류 메시지는 영어, 기존 로더와 같은 형식).
- 머지는 사용자 승인 후(규칙 19). Task 8의 Play 확인은 사용자 몫(규칙 17).

## 파일 구조

| 파일 | 책임 | 태스크 |
|---|---|---|
| `Assets/Core/Tests/EditMode/GoblinParityTests.cs` (신규) | 노드 0 전투 서명의 SHA-256 골든 | 1, 5, 6, 7, 9 |
| `Assets/Core/Enemies/{IEnemyTurnPolicy,RandomPickPolicy,ShuffleBagPolicy,SequencePolicy,EnemyCardBundle}.cs` (이동) | 적 턴 정책 | 2 |
| `Assets/Core/Combat/PartyTuning.cs` (이동) | 파티 규모·드로우 표 | 2, 6, 9 |
| `Assets/StreamingAssets/Content/Cards/{goblin_jab,crude_guard,sly_jab}.json` (신규) | 적 카드 | 3 |
| `Assets/Core/Tests/EditMode/EnemyContentTests.cs` (신규) | 저장소 적 콘텐츠 골든 | 3, 4, 5, 7 |
| `Assets/Core/Enemies/EnemyPolicyKey.cs`·`EnemyPolicyRegistry.cs` (신규) | 정책 키와 생성 레지스트리 | 4 |
| `Assets/Core/Registries/CombatRegistries.cs` (수정) | 기본 정책 등록 | 4 |
| `Assets/Core/Authoring/AuthoringContext.cs` (수정) | `HasEnemyPolicy` | 4 |
| `Assets/Core/Authoring/Enemies/{EnemySpec,EnemyContentCatalog,EnemyContentLoader}.cs` (신규) | 적 JSON 로드·검증 | 4 |
| `Assets/StreamingAssets/Content/Enemies/goblin.json` (신규) | 고블린 | 4 |
| `Assets/Core/Tests/EditMode/EnemyContentLoaderTests.cs` (신규) | 적 로더 검증 | 4 |
| `Assets/Core/Authoring/Battles/{BattleSpec,BattleContentCatalog,BattleContentLoader}.cs` (신규) | 편성 JSON 로드·검증 | 5 |
| `Assets/StreamingAssets/Content/Battles/goblin_single.json` (신규) | 편성 후보 | 5 |
| `Assets/Core/Authoring/{CardContentFiles,GameContent,ContentBootstrap}.cs` (수정) | 폴더 상수·번들·부팅 | 5, 7 |
| `Assets/Core/Simulation/Run/ContentEncounterSource.cs` (신규) | 편성 선택·적·정책 생성 | 5 |
| `Assets/Core/Tests/EditMode/BattleContentLoaderTests.cs`·`ContentEncounterSourceTests.cs` (신규) | 편성 로더·공급자 | 5 |
| `Assets/Core/Authoring/Characters/*` + `Content/Characters/*.json` (수정) | 멤버 `maxHp`·`surviveCharges` | 6 |
| `Assets/Core/Simulation/{PartyMemberLoadout,DeckCombatSession}.cs`, `Run/{RunMember,RunSetup,CombatNode}.cs` (수정) | 멤버별 생존 충전 | 6 |
| `Assets/Core/Authoring/Rules/{CombatRulesSpec,CombatRules,CombatRulesLoader}.cs` (신규) + `Content/combat_rules.json` | 전투 규칙 | 7 |
| `Assets/Core/Tests/EditMode/CombatRulesLoaderTests.cs` (신규) | 규칙 로더 | 7 |
| `Assets/Unity/Scripts/Battle/{CombatNodeFlow,BattleScreenController}.cs`, `Scripts/Text/PlaytestKoreanText.cs`, 씬 | Unity 배선 | 6, 7, 8 |
| 삭제: `GoblinDeck.cs`, `GoblinEncounterSource.cs`, `PartyPrototypeRoster.cs`, `GoblinDeckTests.cs`, `EncounterSourceTests.cs`의 고블린 테스트 | C# 원본 | 9 |

---

### Task 1: 동등성 골든 캡처

**Files:**
- Create: `Assets/Core/Tests/EditMode/GoblinParityTests.cs`

**Interfaces:**
- Consumes: 기존 `RunSetup.NewRun(GameContent, IReadOnlyList<string>, PartyTuning, int)`, `CombatNodeContext(StatusContentCatalog, PartyTuning, int, int, IEncounterSource, IRewardCandidateSource)`, `GoblinEncounterSource`, `CharacterPoolRewardSource(GameContent)`, `DeckCombatSession.{Hand, FateEnergy, PlayExecutionCard, ResolveTurn, BeginNextTurn, IsComplete, Outcome}`.
- Produces: `GoblinParityTests.BeginNode()`(private, Task 5·6·7·9가 이 메서드 본문만 고친다), 상수 `ExpectedSignatureSha256`.

- [ ] **Step 1: 워크트리 만들기**

```bash
cd /Users/ish/Git/rogue-deck
git worktree add .claude/worktrees/combat-node-stage2 -b combat-node-stage2 master
cd .claude/worktrees/combat-node-stage2
Tools/setup-dev.sh
Tools/verify.sh --quick
```
Expected: 헤드리스 전체 통과(실패 0). 실패하면 멈추고 보고한다.

- [ ] **Step 2: 서명 테스트를 빈 골든으로 쓴다**

`Assets/Core/Tests/EditMode/GoblinParityTests.cs`:

```csharp
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>전투 노드 2단계 동등성(설계 결정 16). 고블린·파티 수치를 C#에서 JSON으로 옮겨도 같은
    /// 런 시드·같은 입력이면 노드 0의 전투 서명이 바이트 단위로 같아야 한다. 골든은 C# 원본이
    /// 살아 있을 때 잡았고, 원본을 지운 뒤에도 JSON 경로가 이 값과 계속 비교된다.
    ///
    /// 서명 = 턴마다 (손패 카드 id@소유자, 이번 턴에 배치한 카드, 해석 이벤트 ToString) + 결과 + 보상 후보.
    /// CombatRngDeterminismTests.RunSignature와 같은 형식에 소유자·배치·보상을 더했다.</summary>
    public class GoblinParityTests
    {
        private const int RunSeed = 20260917;
        private const int MaxTurns = 40;

        /// <summary>C# 원본 경로에서 실측한 서명의 SHA-256(소문자 hex). 바꾸지 않는다 — 바뀌면 이관이 동작을 바꾼 것이다.</summary>
        private const string ExpectedSignatureSha256 = "";

        private static CombatNode BeginNode()
        {
            var content = TestContent.Content();
            var run = RunSetup.NewRun(
                content, new[] { "member_a", "member_b" }, PartyTuning.Prototype, RunSeed);
            var context = new CombatNodeContext(
                content.Statuses,
                PartyTuning.Prototype,
                fateEnergyPerTurn: 3,
                rewardChoices: 3,
                encounters: new GoblinEncounterSource(),
                rewardCandidates: new CharacterPoolRewardSource(content));
            return CombatNode.Begin(run, context);
        }

        [Test]
        public void Node_zero_signature_matches_the_csharp_golden()
        {
            var signature = Signature(BeginNode());

            Assert.AreEqual(ExpectedSignatureSha256, Sha256(signature), "실측 서명:\n" + signature);
        }

        [Test]
        public void Signature_reaches_an_outcome_and_places_player_cards()
        {
            var signature = Signature(BeginNode());

            StringAssert.Contains("play:", signature, "전제: 운명력을 쓰는 플레이어 카드가 한 장 이상 배치돼야 한다.");
            StringAssert.DoesNotContain("outcome:Ongoing", signature, "전제: 전투가 끝나야 한다.");
        }

        private static string Signature(CombatNode node)
        {
            var session = node.Session;
            var signature = new StringBuilder();
            for (int turn = 0; turn < MaxTurns; turn++)
            {
                signature.Append("hand:");
                signature.AppendLine(string.Join(",", session.Hand.Select(card => card.Def.Id + "@" + card.OwnerId)));
                PlayEveryAffordableExecutionCard(session, signature);
                foreach (var resolutionEvent in session.ResolveTurn())
                {
                    signature.AppendLine(resolutionEvent.ToString());
                }

                if (!session.BeginNextTurn())
                {
                    break;
                }
            }

            signature.Append("outcome:").AppendLine(session.Outcome.ToString());
            if (session.IsComplete)
            {
                node.Conclude();
                if (node.Offer != null)
                {
                    signature.Append("offer:");
                    signature.AppendLine(string.Join(
                        ",", node.Offer.Candidates.Select(c => c.Card.Id + "@" + c.OwnerId)));
                }
            }

            return signature.ToString();
        }

        /// <summary>고정 입력: 손패 앞에서부터, 실행 카드이고 운명력이 되는 첫 카드를 배치하기를 더 놓을
        /// 수 없을 때까지 반복한다. 배치가 거부되면(대상 요구 등) 다음 카드로 넘어간다.</summary>
        private static void PlayEveryAffordableExecutionCard(DeckCombatSession session, StringBuilder signature)
        {
            var placed = true;
            while (placed)
            {
                placed = false;
                for (int i = 0; i < session.Hand.Count; i++)
                {
                    var def = session.Hand[i].Def;
                    if (def.Category != CardCategory.Execution || def.EnergyCost > session.FateEnergy)
                    {
                        continue;
                    }

                    if (session.PlayExecutionCard(i))
                    {
                        signature.Append("play:").AppendLine(def.Id);
                        placed = true;
                        break;
                    }
                }
            }
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return string.Concat(bytes.Select(b => b.ToString("x2")));
            }
        }
    }
}
```

- [ ] **Step 3: 실측값을 얻는다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter GoblinParityTests`
Expected: `Signature_reaches_an_outcome_and_places_player_cards` PASS, `Node_zero_signature_matches_the_csharp_golden` FAIL — `Expected: <string.Empty> But was: "<64자 hex>"`.
전제 테스트가 실패하면 `RunSeed`를 다른 값(예: `20260918`)으로 바꿔 두 조건이 모두 서는 시드를 찾는다. 찾은 시드를 상수에 남긴다.

- [ ] **Step 4: 골든을 박는다**

`ExpectedSignatureSha256`에 Step 3의 64자 hex를 그대로 넣는다. 같은 명령을 **두 번** 돌려 두 번 다 PASS인지 본다(한 프로세스 안의 우연한 일치 배제).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Tests/EditMode/GoblinParityTests.cs
git commit -m "test(core): 노드 0 전투 서명을 C# 고블린 경로에서 골든으로 잡는다" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: 적 정책과 `PartyTuning`을 Core로 옮긴다

**Files:**
- Move: `Assets/Core/Simulation/Enemies/{IEnemyTurnPolicy,RandomPickPolicy,ShuffleBagPolicy,SequencePolicy,EnemyCardBundle}.cs(.meta)` → `Assets/Core/Enemies/`
- Move: `Assets/Core/Simulation/PartyTuning.cs(.meta)` → `Assets/Core/Combat/PartyTuning.cs(.meta)`
- Modify: 옮긴 6개 파일의 `namespace`, 그리고 컴파일러가 가리키는 `using` 누락 파일

**Interfaces:**
- Produces: `namespace FateWeaver.Core.Enemies` { `IEnemyTurnPolicy`, `RandomPickPolicy`, `ShuffleBagPolicy`, `SequencePolicy`, `EnemyCardBundle` }, `namespace FateWeaver.Core.Combat` { `PartyTuning` }. 코드 본문은 바뀌지 않는다.

- [ ] **Step 1: 파일과 `.meta`를 함께 옮긴다**

```bash
mkdir -p Assets/Core/Enemies
for f in IEnemyTurnPolicy RandomPickPolicy ShuffleBagPolicy SequencePolicy EnemyCardBundle; do
  git mv Assets/Core/Simulation/Enemies/$f.cs Assets/Core/Enemies/$f.cs
  git mv Assets/Core/Simulation/Enemies/$f.cs.meta Assets/Core/Enemies/$f.cs.meta
done
git mv Assets/Core/Simulation/PartyTuning.cs Assets/Core/Combat/PartyTuning.cs
git mv Assets/Core/Simulation/PartyTuning.cs.meta Assets/Core/Combat/PartyTuning.cs.meta
ls Assets/Core/Simulation/Enemies
```
Expected: `ls`가 `Enemies.meta` 짝이 없는 빈 폴더를 보이거나 아무것도 없다. 빈 폴더와 `Assets/Core/Simulation/Enemies.meta`를 지운다:
```bash
rmdir Assets/Core/Simulation/Enemies && git rm -q Assets/Core/Simulation/Enemies.meta
```

- [ ] **Step 2: 네임스페이스를 바꾼다**

옮긴 5개 정책 파일의 `namespace FateWeaver.Simulation` → `namespace FateWeaver.Core.Enemies`.
`Assets/Core/Combat/PartyTuning.cs`의 `namespace FateWeaver.Simulation` → `namespace FateWeaver.Core.Combat`.
```bash
sed -i '' 's/^namespace FateWeaver.Simulation$/namespace FateWeaver.Core.Enemies/' Assets/Core/Enemies/*.cs
sed -i '' 's/^namespace FateWeaver.Simulation$/namespace FateWeaver.Core.Combat/' Assets/Core/Combat/PartyTuning.cs
/usr/bin/grep -n "^namespace" Assets/Core/Enemies/*.cs Assets/Core/Combat/PartyTuning.cs
```
Expected: 6줄 모두 새 네임스페이스.

- [ ] **Step 3: 컴파일 오류를 `using`으로 고친다**

Run: `dotnet build Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo 2>&1 | /usr/bin/grep -o "[A-Za-z/]*\.cs([0-9]*" | sort -u`
오류가 난 파일마다 맨 위 `using` 목록에 필요한 줄을 알파벳 순서 자리에 더한다:
- 정책 타입(`IEnemyTurnPolicy`·`*Policy`·`EnemyCardBundle`)을 쓰면 `using FateWeaver.Core.Enemies;`
- `PartyTuning`을 쓰면 `using FateWeaver.Core.Combat;`(이미 있으면 그대로)

2026-09-17 grep 기준 대상(컴파일러 출력이 최종 기준): 프로덕션 `DeckCombatSession.cs`·`GoblinDeck.cs`·`Run/Encounters.cs`·`Run/RunSetup.cs`·`PartyPrototypeRoster.cs`, 테스트 `CombatNodeTests`·`ConditionalCardRuleTests`·`DeckCombatSessionTests`·`GrantNextTurnFateTests`·`LockCardTests`·`LockedEnemyExecutionOrderTests`·`OwnedCardDeckTests`·`PartyDeckCombatSessionTests`·`RandomPickPolicyTests`·`SequencePolicyTests`·`ShuffleBagPolicyTests`·`SlowHasteStatusTests`·`RunSetupTests`.
**Unity 테스트 3개도 같은 줄을 더한다**(헤드리스는 컴파일하지 않는다): `Assets/Tests/UnityEditMode/{BattleStageTests,BattleUnitsViewIdentityTests,ResolutionEventPresenterTests}.cs`에 `using FateWeaver.Core.Enemies;`와 `using FateWeaver.Core.Combat;`.
`Assets/Unity` 아래 C#은 이 타입을 쓰지 않는다(2026-09-17 grep 0건) — 다시 확인:
`/usr/bin/grep -rln "Policy\b\|EnemyCardBundle\|PartyTuning" Assets/Unity --include='*.cs'` → 출력 없음.

- [ ] **Step 4: 검증**

Run: `Tools/verify.sh --quick`
Expected: 전체 통과, `GoblinParityTests` 포함.
Run: Global Constraints의 경계 검사 grep → 출력 없음.

- [ ] **Step 5: 커밋**

```bash
git add -A Assets/Core Assets/Tests
git commit -m "refactor(core): 적 정책과 파티 튜닝을 콘텐츠 검증이 쓰도록 코어 어셈블리로 옮긴다" -m "코드 본문은 바꾸지 않고 위치와 네임스페이스만 바꾼다(설계 결정 14·15)." -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: 적 카드 3장을 JSON으로 저작한다

**Files:**
- Create: `Assets/StreamingAssets/Content/Cards/goblin_jab.json`, `crude_guard.json`, `sly_jab.json`
- Create: `Assets/Core/Tests/EditMode/EnemyContentTests.cs`
- Modify: `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs:20`, `Assets/Core/Tests/EditMode/StructuredCardDescriptionTests.cs:106-110`

**Interfaces:**
- Produces: 카드 카탈로그 id `goblin_jab`·`crude_guard`·`sly_jab`(`Side.Enemy`). Task 4의 `goblin.json`이 참조한다.

- [ ] **Step 1: 실패하는 테스트**

`Assets/Core/Tests/EditMode/EnemyContentTests.cs`:

```csharp
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>저장소에 저작된 적 콘텐츠의 골든. GoblinDeckTests가 C# 원본에 걸던 단언을 JSON에 건다
    /// (전투 노드 설계 2.8 — 원본 삭제 뒤에도 남는다).</summary>
    public class EnemyContentTests
    {
        [Test]
        public void Goblin_cards_are_authored_as_enemy_json()
        {
            var cards = TestContent.Cards();
            var thrust = cards.Get("goblin_jab");
            var guard = cards.Get("crude_guard");
            var sly = cards.Get("sly_jab");

            foreach (var card in new[] { thrust, guard, sly })
            {
                Assert.AreEqual(Side.Enemy, card.Side, card.Id);
                Assert.AreEqual(CardCategory.Execution, card.Category, card.Id);
                Assert.AreEqual(0, card.EnergyCost, card.Id);
            }

            Assert.AreEqual("찌르기", thrust.Name);
            Assert.AreEqual(6, thrust.BaseExecutionOrder);
            Assert.AreEqual(EffectKeys.Damage, thrust.Effects.Single().Key);
            Assert.AreEqual(4, thrust.Effects.Single().EffectValue);

            Assert.AreEqual("조잡한 방어", guard.Name);
            Assert.AreEqual(4, guard.BaseExecutionOrder);
            Assert.AreEqual(3, guard.Effects.Single().EffectValue);
            var payload = (ApplyStatusPayload)guard.Effects.Single().Payload;
            Assert.AreEqual(StatusKeys.Block, payload.Key);
            Assert.AreEqual(StatusApplyTarget.Self, payload.Target);

            Assert.AreEqual("약삭빠른 찌르기", sly.Name);
            Assert.AreEqual(3, sly.BaseExecutionOrder);
            Assert.AreEqual(3, sly.Effects.Single().EffectValue);
            Assert.AreEqual(6, sly.Effects.Single().SuccessEffectValue);
            Assert.AreEqual(Side.Player, ((NoPrecedingCardOfSide)sly.Effects.Single().Condition).Side);
        }
    }
}
```

근거: `EffectData(EffectKey Key, int EffectValue)`·`SuccessEffectValue`·`Condition`·`Payload`(`Assets/Core/Cards/CardDefinition.cs:10-20`), `ApplyStatusPayload(StatusKey Key, StatusApplyTarget Target)`(`Assets/Core/Effects/ApplyStatusPayload.cs:10-12`), `CardDefinition(Id, Name, Side, BaseExecutionOrder, …)`(`CardDefinition.cs:54-58`).

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter EnemyContentTests`
Expected: FAIL — `KeyNotFoundException: No card content with id 'goblin_jab'.`

- [ ] **Step 3: JSON 3장**

값은 `Assets/Core/Simulation/GoblinDeck.cs:20-33`과 같다. `energyCost` 0과 `grade`·`tags` 없음은 기본값이라 `ContentJson`이 생략한다(`CardSpec.cs:33-46`, `ContentJson.cs:37`). `target: Self`도 enum 0이라 생략된다.

`goblin_jab.json`:
```json
{
  "id": "goblin_jab",
  "name": "찌르기",
  "side": "Enemy",
  "category": "Execution",
  "baseExecutionOrder": 6,
  "effects": [
    {
      "kind": "damage",
      "value": 4
    }
  ]
}
```

`crude_guard.json`:
```json
{
  "id": "crude_guard",
  "name": "조잡한 방어",
  "side": "Enemy",
  "category": "Execution",
  "baseExecutionOrder": 4,
  "effects": [
    {
      "kind": "apply_status",
      "status": "block",
      "count": 3
    }
  ]
}
```

`sly_jab.json`:
```json
{
  "id": "sly_jab",
  "name": "약삭빠른 찌르기",
  "side": "Enemy",
  "category": "Execution",
  "baseExecutionOrder": 3,
  "effects": [
    {
      "kind": "damage",
      "value": 3,
      "condition": {
        "kind": "NoPrecedingPlayerCard",
        "successEffectValue": 6
      }
    }
  ]
}
```
파일 끝에 줄바꿈 하나. 들여쓰기 2칸.

- [ ] **Step 4: 깨지는 기존 테스트 두 곳**

`ContentBootstrapTests.cs:20`: `Assert.AreEqual(26, ...)` → `Assert.AreEqual(29, ...)`.

`StructuredCardDescriptionTests.cs:104-110`을 다음으로 바꾼다(`GoblinDeck.AllCards()` 이어 붙이기 제거 — 이제 JSON에 들어 있어 두 번 들어간다):
```csharp
            // TestContent.Cards().Cards.Values는 Content/Cards/*.json 전부다 — 시작 풀 22장,
            // fixture_* 4종, 적 카드 3장(합 29장, 파일 개수와 실측 일치).
            var cards = TestContent.Cards().Cards.Values;
```
이 파일에서 `GoblinDeck`·`FateWeaver.Simulation` 참조가 더 없으면 그 `using`을 지운다.

- [ ] **Step 5: 검증**

Run: `dotnet test ... --filter "EnemyContentTests|CardContentJsonTests|ContentBootstrapTests|StructuredCardDescriptionTests"`
Expected: PASS. `Repository_cards_round_trip_byte_identically`가 실패하면 실패 메시지의 차이를 보고 **JSON 파일을 직렬화기 출력에 맞춘다**(키 순서·생략). 직렬화기를 고치지 않는다.
Run: `node --test "Tools/card-idea-notebook/"*.test.mjs`
Expected: 통과. 실패하면 멈추고 보고한다(노트북은 적 카드를 이미 지원한다 — 설계 2.3①).
Run: `Tools/verify.sh --quick`

- [ ] **Step 6: 커밋**

```bash
git add Assets/StreamingAssets/Content/Cards/goblin_jab.json Assets/StreamingAssets/Content/Cards/crude_guard.json Assets/StreamingAssets/Content/Cards/sly_jab.json Assets/Core/Tests/EditMode
git commit -m "feat(content): 고블린 카드 3장을 적 카드 JSON으로 저작한다" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: 정책 레지스트리와 적 로더

**Files:**
- Create: `Assets/Core/Enemies/EnemyPolicyKey.cs`, `Assets/Core/Enemies/EnemyPolicyRegistry.cs`
- Modify: `Assets/Core/Registries/CombatRegistries.cs`, `Assets/Core/Authoring/AuthoringContext.cs`, `Assets/Core/Tests/EditMode/StatusContentTests.cs:137-138`
- Create: `Assets/Core/Authoring/Enemies/EnemySpec.cs`, `EnemyContentCatalog.cs`, `EnemyContentLoader.cs`
- Create: `Assets/StreamingAssets/Content/Enemies/goblin.json`
- Create: `Assets/Core/Tests/EditMode/EnemyPolicyRegistryTests.cs`, `EnemyContentLoaderTests.cs`
- Modify: `Assets/Core/Tests/EditMode/EnemyContentTests.cs`

**Interfaces:**
- Consumes: Task 2의 `FateWeaver.Core.Enemies` 타입, Task 3의 적 카드.
- Produces:
  - `EnemyPolicyKey(string id)` — `Id`, 값 동등성. `EnemyPolicyKeys.{RandomPick, ShuffleBag, Sequence}`.
  - `EnemyPolicyRegistry.{Register(EnemyPolicyKey, Func<IReadOnlyList<EnemyCardBundle>, IEnemyTurnPolicy>), Contains(EnemyPolicyKey), Create(EnemyPolicyKey, IReadOnlyList<EnemyCardBundle>)}`
  - `CombatRegistries.EnemyPolicies()`
  - `AuthoringContext(EffectRegistry, StatusRegistry, InterventionActionRegistry, EnemyPolicyRegistry)`, `HasEnemyPolicy(EnemyPolicyKey)`
  - `EnemyDefinition.{Id, DisplayName, MaxHp, Policy (EnemyPolicyKey), Bundles (IReadOnlyList<EnemyCardBundle>)}`
  - `EnemyContentCatalog.{Ids, Get(string), Contains(string)}`
  - `EnemyContentLoader.Load(IEnumerable<CardContentSource>, CardContentCatalog, AuthoringContext) → EnemyContentLoadResult {Succeeded, Catalog, Errors}`

- [ ] **Step 1: 레지스트리 테스트**

`Assets/Core/Tests/EditMode/EnemyPolicyRegistryTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class EnemyPolicyRegistryTests
    {
        private static IReadOnlyList<EnemyCardBundle> OneBundle()
            => new[] { new EnemyCardBundle(CardFixtures.EnemyAttack("smash", 1, 3)) };

        [Test]
        public void Default_registry_creates_the_three_authored_policies()
        {
            var registry = CombatRegistries.EnemyPolicies();

            Assert.IsInstanceOf<RandomPickPolicy>(registry.Create(EnemyPolicyKeys.RandomPick, OneBundle()));
            Assert.IsInstanceOf<ShuffleBagPolicy>(registry.Create(EnemyPolicyKeys.ShuffleBag, OneBundle()));
            Assert.IsInstanceOf<SequencePolicy>(registry.Create(EnemyPolicyKeys.Sequence, OneBundle()));
            Assert.AreEqual("random_pick", EnemyPolicyKeys.RandomPick.Id);
            Assert.AreEqual("shuffle_bag", EnemyPolicyKeys.ShuffleBag.Id);
            Assert.AreEqual("sequence", EnemyPolicyKeys.Sequence.Id);
        }

        [Test]
        public void Create_returns_a_fresh_instance_every_call()
        {
            var registry = CombatRegistries.EnemyPolicies();
            var bundles = OneBundle();

            Assert.AreNotSame(
                registry.Create(EnemyPolicyKeys.ShuffleBag, bundles),
                registry.Create(EnemyPolicyKeys.ShuffleBag, bundles));
        }

        [Test]
        public void Unregistered_key_is_not_contained_and_cannot_be_created()
        {
            var registry = CombatRegistries.EnemyPolicies();
            var unknown = new EnemyPolicyKey("random_pik");

            Assert.IsFalse(registry.Contains(unknown));
            Assert.IsTrue(registry.Contains(new EnemyPolicyKey("random_pick")), "키는 값으로 비교한다.");
            Assert.Throws<KeyNotFoundException>(() => registry.Create(unknown, OneBundle()));
        }
    }
}
```
`CardFixtures.EnemyAttack(string id, int executionOrder, int damage)` — `Assets/Core/Tests/EditMode/CardFixtures.cs:79`.

- [ ] **Step 2: 실패 확인** — `dotnet test ... --filter EnemyPolicyRegistryTests` → 컴파일 오류(`EnemyPolicyKey` 없음).

- [ ] **Step 3: 키와 레지스트리**

`Assets/Core/Enemies/EnemyPolicyKey.cs` (`Assets/Core/Effects/EffectKey.cs`와 같은 모양):
```csharp
using System;

namespace FateWeaver.Core.Enemies
{
    /// <summary>적 정책 키. 적 JSON의 "policy" 값이다. EffectKey와 같은 이유로 record struct가 아닌
    /// 일반 readonly struct다(Unity 6의 C# 9).</summary>
    public readonly struct EnemyPolicyKey : IEquatable<EnemyPolicyKey>
    {
        public string Id { get; }

        public EnemyPolicyKey(string id) => Id = id;

        public bool Equals(EnemyPolicyKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is EnemyPolicyKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(EnemyPolicyKey a, EnemyPolicyKey b) => a.Equals(b);
        public static bool operator !=(EnemyPolicyKey a, EnemyPolicyKey b) => !a.Equals(b);
    }

    public static class EnemyPolicyKeys
    {
        public static readonly EnemyPolicyKey RandomPick = new EnemyPolicyKey("random_pick");
        public static readonly EnemyPolicyKey ShuffleBag = new EnemyPolicyKey("shuffle_bag");
        public static readonly EnemyPolicyKey Sequence = new EnemyPolicyKey("sequence");
    }
}
```

`Assets/Core/Enemies/EnemyPolicyRegistry.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Enemies
{
    /// <summary>정책 키 → 정책 생성 함수. 정책은 상태를 가지므로(ShuffleBagPolicy) 인스턴스가 아니라
    /// 생성 함수를 등록하고, Create가 매번 새로 만든다. 새 정책 = 클래스 1개 + 키 등록(규칙 9).</summary>
    public sealed class EnemyPolicyRegistry
    {
        private readonly Dictionary<EnemyPolicyKey, Func<IReadOnlyList<EnemyCardBundle>, IEnemyTurnPolicy>> _factories = new();

        public void Register(EnemyPolicyKey key, Func<IReadOnlyList<EnemyCardBundle>, IEnemyTurnPolicy> create)
            => _factories[key] = create ?? throw new ArgumentNullException(nameof(create));

        public bool Contains(EnemyPolicyKey key) => _factories.ContainsKey(key);

        public IEnemyTurnPolicy Create(EnemyPolicyKey key, IReadOnlyList<EnemyCardBundle> bundles)
            => _factories.TryGetValue(key, out var create)
                ? create(bundles)
                : throw new KeyNotFoundException($"No enemy policy registered for '{key}'");
    }
}
```

`Assets/Core/Registries/CombatRegistries.cs`: `using FateWeaver.Core.Enemies;`를 더하고 `InterventionActions()` 뒤에:
```csharp
        public static EnemyPolicyRegistry EnemyPolicies()
        {
            var policies = new EnemyPolicyRegistry();
            policies.Register(EnemyPolicyKeys.RandomPick, bundles => new RandomPickPolicy(bundles));
            policies.Register(EnemyPolicyKeys.ShuffleBag, bundles => new ShuffleBagPolicy(bundles));
            policies.Register(EnemyPolicyKeys.Sequence, bundles => new SequencePolicy(bundles));
            return policies;
        }
```
`SequencePolicy`는 생성자 오버로드가 둘이라(`SequencePolicy.cs:15·22`) 람다 인자 타입이 `IReadOnlyList<EnemyCardBundle>`로 고정돼 모호하지 않다.

- [ ] **Step 4: 레지스트리 테스트 통과** — `--filter EnemyPolicyRegistryTests` PASS.

- [ ] **Step 5: `AuthoringContext` 확장**

`Assets/Core/Authoring/AuthoringContext.cs` 전체:
```csharp
using System.Collections.Generic;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Enemies;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Registry lookups for authoring-time validation (editor and boot use the same checks).</summary>
    public sealed class AuthoringContext
    {
        private readonly EffectRegistry _effects;
        private readonly StatusRegistry _statuses;
        private readonly InterventionActionRegistry _interventions;
        private readonly EnemyPolicyRegistry _enemyPolicies;

        public AuthoringContext(
            EffectRegistry effects,
            StatusRegistry statuses,
            InterventionActionRegistry interventions,
            EnemyPolicyRegistry enemyPolicies)
        {
            _effects = effects;
            _statuses = statuses;
            _interventions = interventions;
            _enemyPolicies = enemyPolicies;
        }

        public static AuthoringContext Default()
            => new AuthoringContext(
                CombatRegistries.Effects(),
                CombatRegistries.Statuses(),
                CombatRegistries.InterventionActions(),
                CombatRegistries.EnemyPolicies());

        public IReadOnlyList<StatusKey> RegisteredStatusKeys => _statuses.RegisteredKeys;
        public bool HasStatus(StatusKey key) => _statuses.TryResolve(key, out _);
        public bool HasEffect(EffectKey key) => _effects.Contains(key);
        public bool HasIntervention(InterventionActionKey key) => _interventions.Contains(key);
        public bool HasEnemyPolicy(EnemyPolicyKey key) => _enemyPolicies.Contains(key);
    }
}
```
`StatusContentTests.cs:137-138`의 호출에 네 번째 인자 `CombatRegistries.EnemyPolicies()`를 더한다. 다른 호출부가 있는지 `/usr/bin/grep -rn "new AuthoringContext(" Assets Tools --include='*.cs'`로 확인한다(2026-09-17 기준 이 둘뿐).

- [ ] **Step 6: 적 로더 테스트**

`Assets/Core/Tests/EditMode/EnemyContentLoaderTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>적 로더가 저작 실수를 로드 시점에 거부하는지 잠근다. 다른 로더와 같이 실패하면
    /// 카탈로그를 내주지 않고 모든 이유를 모은다.</summary>
    public class EnemyContentLoaderTests
    {
        private static CardContentCatalog Cards()
        {
            var cards = new Dictionary<string, CardDefinition>();
            var specs = new Dictionary<string, CardSpec>();
            void Add(string id, Side side)
            {
                var spec = new ExecutionCardSpec { Id = id, Name = id, Side = side, Category = CardCategory.Execution };
                cards.Add(id, CardSpecMapper.ToDefinition(spec));
                specs.Add(id, spec);
            }

            Add("jab", Side.Enemy);
            Add("guard", Side.Enemy);
            Add("hero_strike", Side.Player);
            return new CardContentCatalog(cards, specs);
        }

        private static EnemyContentLoadResult Load(params CardContentSource[] sources)
            => EnemyContentLoader.Load(sources, Cards(), AuthoringContext.Default());

        private static CardContentSource Source(string name, string json) => new CardContentSource(name, json);

        private const string Valid =
            "{ \"id\": \"goblin\", \"displayName\": \"고블린\", \"maxHp\": 28, \"policy\": \"random_pick\", "
            + "\"bundles\": [[\"jab\"], [\"jab\", \"guard\"]] }";

        [Test]
        public void Loads_an_enemy_with_shared_card_definitions_and_policy_key()
        {
            var result = Load(Source("goblin.json", Valid));

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            var goblin = result.Catalog.Get("goblin");
            Assert.AreEqual("고블린", goblin.DisplayName);
            Assert.AreEqual(28, goblin.MaxHp);
            Assert.AreEqual(EnemyPolicyKeys.RandomPick, goblin.Policy);
            CollectionAssert.AreEqual(
                new[] { "jab", "jab,guard" },
                goblin.Bundles.Select(b => string.Join(",", b.Cards.Select(c => c.Id))).ToArray());
            Assert.AreSame(goblin.Bundles[0].Cards[0], goblin.Bundles[1].Cards[0], "같은 카드 id는 정의 하나를 공유한다.");
            CollectionAssert.AreEqual(new[] { "goblin" }, result.Catalog.Ids);
            Assert.IsTrue(result.Catalog.Contains("goblin"));
        }

        [TestCase("id")]
        [TestCase("displayName")]
        [TestCase("maxHp")]
        [TestCase("policy")]
        [TestCase("bundles")]
        public void Rejects_a_missing_required_key(string key)
        {
            var json = key switch
            {
                "id" => "{ \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
                "displayName" => "{ \"id\": \"goblin\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
                "maxHp" => "{ \"id\": \"goblin\", \"displayName\": \"G\", \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
                "policy" => "{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"bundles\": [[\"jab\"]] }",
                _ => "{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\" }"
            };

            var result = Load(Source("goblin.json", json));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "goblin.json: required key '" + key + "' is missing.");
        }

        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: requires a displayName.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 0, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: maxHp must be positive.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pik\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: unknown enemy policy 'random_pik'.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [] }",
            "goblin.json: requires at least one bundle.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"], []] }",
            "goblin.json: bundle 1 is empty.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"ghost\"]] }",
            "goblin.json: unknown card id 'ghost' in bundle 0.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"hero_strike\"]] }",
            "goblin.json: card 'hero_strike' in bundle 0 is not an enemy card.")]
        [TestCase("{ \"id\": \"\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: required key 'id' must be a non-empty string.")]
        public void Rejects_an_invalid_enemy(string json, string error)
        {
            var result = Load(Source("goblin.json", json));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Catalog);
            CollectionAssert.Contains(result.Errors, error);
        }

        [Test]
        public void Rejects_a_duplicate_id()
        {
            var result = Load(Source("a.json", Valid), Source("b.json", Valid));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "b.json: duplicate enemy id 'goblin' (already defined in a.json).");
        }

        [Test]
        public void Reports_every_reason_at_once()
        {
            var result = Load(Source("goblin.json",
                "{ \"id\": \"goblin\", \"displayName\": \"\", \"maxHp\": 0, \"policy\": \"random_pik\", \"bundles\": [[\"ghost\"]] }"));

            Assert.AreEqual(4, result.Errors.Count, string.Join("\n", result.Errors));
        }
    }
}
```

- [ ] **Step 7: 실패 확인** — `--filter EnemyContentLoaderTests` → 컴파일 오류.

- [ ] **Step 8: 스펙·카탈로그·로더**

`Assets/Core/Authoring/Enemies/EnemySpec.cs`:
```csharp
namespace FateWeaver.Core.Authoring.Enemies
{
    /// <summary>저작된 적 하나. 묶음은 적이 한 턴에 통째로 존에 올리는 카드 id 목록이다
    /// (적 카드 묶음 설계). 정책은 키만 들고, 인스턴스는 쓰는 쪽이 레지스트리로 만든다.</summary>
    public sealed class EnemySpec
    {
        public string Id;
        public string DisplayName;
        public int MaxHp;
        public string Policy;
        public string[][] Bundles;
    }
}
```

`Assets/Core/Authoring/Enemies/EnemyContentCatalog.cs`:
```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Enemies;

namespace FateWeaver.Core.Authoring.Enemies
{
    /// <summary>로드된 적 하나. 묶음의 카드는 카드 카탈로그의 정의 객체를 공유한다.</summary>
    public sealed class EnemyDefinition
    {
        public EnemyDefinition(
            string id, string displayName, int maxHp, EnemyPolicyKey policy, IReadOnlyList<EnemyCardBundle> bundles)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            Policy = policy;
            Bundles = bundles;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHp { get; }
        public EnemyPolicyKey Policy { get; }
        public IReadOnlyList<EnemyCardBundle> Bundles { get; }
    }

    /// <summary>부팅 시 한 번 만들어져 상주하는 id → EnemyDefinition 사전.</summary>
    public sealed class EnemyContentCatalog
    {
        private readonly Dictionary<string, EnemyDefinition> _enemies;
        private readonly List<string> _ids;

        public EnemyContentCatalog(Dictionary<string, EnemyDefinition> enemies)
        {
            _enemies = enemies;
            _ids = new List<string>(enemies.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>정렬된 id 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Ids => _ids;

        public bool Contains(string id) => id != null && _enemies.ContainsKey(id);

        public EnemyDefinition Get(string id)
        {
            if (id == null || !_enemies.TryGetValue(id, out var enemy))
            {
                throw new KeyNotFoundException("No enemy content with id '" + id + "'.");
            }

            return enemy;
        }
    }
}
```

`Assets/Core/Authoring/Enemies/EnemyContentLoader.cs`:
```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Enemies;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Enemies
{
    /// <summary>적 로드 한 번의 결과. 실패하면 카탈로그를 내주지 않고 모든 이유를 모아 보고한다.</summary>
    public sealed class EnemyContentLoadResult
    {
        private EnemyContentLoadResult(EnemyContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public EnemyContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static EnemyContentLoadResult Ok(EnemyContentCatalog catalog)
            => new EnemyContentLoadResult(catalog, new string[0]);

        public static EnemyContentLoadResult Failed(IReadOnlyList<string> errors)
            => new EnemyContentLoadResult(null, errors);
    }

    /// <summary>적 콘텐츠 소스를 파싱·검증해 카탈로그로 만든다. 카드 카탈로그와 정책 레지스트리
    /// (AuthoringContext)를 받으므로 카드 뒤에 온다. 정책 키 오타가 카드 효과 키 오타와 같은 부팅
    /// 오류 목록에 잡힌다(전투 노드 설계 결정 14).</summary>
    public static class EnemyContentLoader
    {
        private static readonly string[] RequiredKeys = { "id", "displayName", "maxHp", "policy", "bundles" };

        public static EnemyContentLoadResult Load(
            IEnumerable<CardContentSource> sources,
            CardContentCatalog cards,
            AuthoringContext context)
        {
            var errors = new List<string>();
            var enemies = new Dictionary<string, EnemyDefinition>();
            var origin = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                EnemySpec spec;
                try
                {
                    spec = ContentJson.Read<EnemySpec>(source.Json);
                }
                catch (JsonException ex)
                {
                    errors.Add(source.Name + ": " + ContentJsonError.Describe(ex));
                    continue;
                }

                if (string.IsNullOrEmpty(spec.Id))
                {
                    errors.Add(source.Name + ": required key 'id' must be a non-empty string.");
                    continue;
                }

                if (origin.TryGetValue(spec.Id, out var first))
                {
                    errors.Add(
                        source.Name + ": duplicate enemy id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                origin.Add(spec.Id, source.Name);

                var rejected = false;
                if (string.IsNullOrEmpty(spec.DisplayName))
                {
                    errors.Add(source.Name + ": requires a displayName.");
                    rejected = true;
                }

                if (spec.MaxHp <= 0)
                {
                    errors.Add(source.Name + ": maxHp must be positive.");
                    rejected = true;
                }

                var policy = new EnemyPolicyKey(spec.Policy);
                if (!context.HasEnemyPolicy(policy))
                {
                    errors.Add(source.Name + ": unknown enemy policy '" + spec.Policy + "'.");
                    rejected = true;
                }

                var bundles = new List<EnemyCardBundle>();
                if (spec.Bundles == null || spec.Bundles.Length == 0)
                {
                    errors.Add(source.Name + ": requires at least one bundle.");
                    rejected = true;
                }
                else
                {
                    for (int i = 0; i < spec.Bundles.Length; i++)
                    {
                        var ids = spec.Bundles[i];
                        if (ids == null || ids.Length == 0)
                        {
                            errors.Add(source.Name + ": bundle " + i + " is empty.");
                            rejected = true;
                            continue;
                        }

                        var bundleCards = new List<CardDefinition>(ids.Length);
                        foreach (var cardId in ids)
                        {
                            if (cardId == null || !cards.Cards.TryGetValue(cardId, out var card))
                            {
                                errors.Add(source.Name + ": unknown card id '" + cardId + "' in bundle " + i + ".");
                                rejected = true;
                                continue;
                            }

                            if (card.Side != Side.Enemy)
                            {
                                errors.Add(source.Name + ": card '" + cardId + "' in bundle " + i + " is not an enemy card.");
                                rejected = true;
                                continue;
                            }

                            bundleCards.Add(card);
                        }

                        bundles.Add(new EnemyCardBundle(bundleCards));
                    }
                }

                if (!rejected)
                {
                    enemies.Add(spec.Id, new EnemyDefinition(spec.Id, spec.DisplayName, spec.MaxHp, policy, bundles));
                }
            }

            if (errors.Count > 0)
            {
                return EnemyContentLoadResult.Failed(errors);
            }

            return EnemyContentLoadResult.Ok(new EnemyContentCatalog(enemies));
        }
    }
}
```
근거: `CardDefinition.Side`(`CardDefinition.cs:57`), `EnemyCardBundle(IReadOnlyList<CardDefinition>)`(이동 전 `Assets/Core/Simulation/Enemies/EnemyCardBundle.cs:17`).

- [ ] **Step 9: 로더 테스트 통과** — `--filter "EnemyContentLoaderTests|StatusContentTests"` PASS.

- [ ] **Step 10: `goblin.json`과 저장소 골든**

`Assets/StreamingAssets/Content/Enemies/goblin.json` (묶음 순서는 `GoblinDeck.cs:59-65`와 같다):
```json
{
  "id": "goblin",
  "displayName": "고블린",
  "maxHp": 28,
  "policy": "random_pick",
  "bundles": [
    ["goblin_jab"],
    ["sly_jab", "crude_guard"],
    ["sly_jab", "goblin_jab"],
    ["crude_guard", "crude_guard"]
  ]
}
```

`EnemyContentTests.cs`에 `using System.IO;`, `using FateWeaver.Core.Authoring;`, `using FateWeaver.Core.Authoring.Enemies;`, `using FateWeaver.Core.Enemies;`를 더하고 테스트 추가(부팅 연결은 Task 5라 로더를 직접 부른다):
```csharp
        [Test]
        public void Goblin_is_authored_with_four_bundles_and_random_pick()
        {
            var sources = CardContentFiles.ReadDirectory(Path.Combine(TestContent.Root(), "Enemies"));
            var result = EnemyContentLoader.Load(sources, TestContent.Cards(), AuthoringContext.Default());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            var goblin = result.Catalog.Get("goblin");
            Assert.AreEqual("고블린", goblin.DisplayName);
            Assert.AreEqual(28, goblin.MaxHp);
            Assert.AreEqual(EnemyPolicyKeys.RandomPick, goblin.Policy);
            CollectionAssert.AreEqual(
                new[]
                {
                    "goblin_jab",                 // A 늦은 단타
                    "sly_jab,crude_guard",        // B 선공 후 방어
                    "sly_jab,goblin_jab",         // C 앞뒤로 벌린 2연타
                    "crude_guard,crude_guard"     // D 농성 (방어 재부여는 합산 → 방어도 6)
                },
                goblin.Bundles.Select(b => string.Join(",", b.Cards.Select(c => c.Id))).ToArray());
        }
```

- [ ] **Step 11: 검증** — `Tools/verify.sh --quick` 통과, 경계 검사 grep 출력 없음.

- [ ] **Step 12: 커밋**

```bash
git add Assets/Core Assets/StreamingAssets/Content/Enemies/goblin.json
git commit -m "feat(core): 적 정책 레지스트리와 정책 키를 검증하는 적 콘텐츠 로더를 만든다" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: 편성 로더·부팅 연결·`ContentEncounterSource`

**Files:**
- Create: `Assets/Core/Authoring/Battles/BattleSpec.cs`, `BattleContentCatalog.cs`, `BattleContentLoader.cs`
- Create: `Assets/StreamingAssets/Content/Battles/goblin_single.json`
- Modify: `Assets/Core/Authoring/CardContentFiles.cs:12-16`, `Assets/Core/Authoring/GameContent.cs`, `Assets/Core/Authoring/ContentBootstrap.cs:30-91`
- Create: `Assets/Core/Simulation/Run/ContentEncounterSource.cs`
- Create: `Assets/Core/Tests/EditMode/BattleContentLoaderTests.cs`, `ContentEncounterSourceTests.cs`
- Modify: `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs`, `GoblinParityTests.cs`

**Interfaces:**
- Consumes: Task 4의 `EnemyContentCatalog`, `EnemyPolicyRegistry`, `CombatRegistries.EnemyPolicies()`.
- Produces:
  - `BattleDefinition.{Id, Enemies (IReadOnlyList<string>)}`, `BattleContentCatalog.{Ids, Get(string)}`
  - `BattleContentLoader.Load(IEnumerable<CardContentSource>, EnemyContentCatalog) → BattleContentLoadResult`
  - `CardContentFiles.EnemiesFolderName = "Enemies"`, `BattlesFolderName = "Battles"`
  - `GameContent(statuses, cards, decks, pools, characters, enemies, battles)`, 속성 `Enemies`·`Battles`
  - `ContentEncounterSource(GameContent content, EnemyPolicyRegistry policies) : IEncounterSource`

- [ ] **Step 1: 편성 로더 테스트**

`Assets/Core/Tests/EditMode/BattleContentLoaderTests.cs`:
```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Battles;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class BattleContentLoaderTests
    {
        private static EnemyContentCatalog Enemies()
            => new EnemyContentCatalog(new Dictionary<string, EnemyDefinition>
            {
                { "goblin", new EnemyDefinition("goblin", "G", 28, EnemyPolicyKeys.RandomPick, new EnemyCardBundle[0]) },
                { "rat", new EnemyDefinition("rat", "R", 10, EnemyPolicyKeys.RandomPick, new EnemyCardBundle[0]) }
            });

        private static BattleContentLoadResult Load(params CardContentSource[] sources)
            => BattleContentLoader.Load(sources, Enemies());

        private static CardContentSource Source(string name, string json) => new CardContentSource(name, json);

        [Test]
        public void Loads_battles_sorted_by_id()
        {
            var result = Load(
                Source("rat_single.json", "{ \"id\": \"rat_single\", \"enemies\": [\"rat\"] }"),
                Source("goblin_single.json", "{ \"id\": \"goblin_single\", \"enemies\": [\"goblin\"] }"));

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            CollectionAssert.AreEqual(new[] { "goblin_single", "rat_single" }, result.Catalog.Ids);
            CollectionAssert.AreEqual(new[] { "rat" }, result.Catalog.Get("rat_single").Enemies);
        }

        [Test]
        public void Requires_at_least_one_battle()
        {
            var result = Load();

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "Battles: at least one battle is required.");
        }

        [TestCase("{ \"enemies\": [\"goblin\"] }", "b.json: required key 'id' is missing.")]
        [TestCase("{ \"id\": \"b\" }", "b.json: required key 'enemies' is missing.")]
        [TestCase("{ \"id\": \"\", \"enemies\": [\"goblin\"] }", "b.json: required key 'id' must be a non-empty string.")]
        [TestCase("{ \"id\": \"b\", \"enemies\": [\"ghost\"] }", "b.json: unknown enemy id 'ghost'.")]
        [TestCase("{ \"id\": \"b\", \"enemies\": [] }", "b.json: exactly one enemy is supported until per-enemy policies land.")]
        [TestCase("{ \"id\": \"b\", \"enemies\": [\"goblin\", \"goblin\"] }", "b.json: exactly one enemy is supported until per-enemy policies land.")]
        public void Rejects_an_invalid_battle(string json, string error)
        {
            var result = Load(Source("b.json", json));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Catalog);
            CollectionAssert.Contains(result.Errors, error);
        }

        [Test]
        public void Rejects_a_duplicate_id()
        {
            var json = "{ \"id\": \"b\", \"enemies\": [\"goblin\"] }";
            var result = Load(Source("a.json", json), Source("c.json", json));

            CollectionAssert.Contains(result.Errors, "c.json: duplicate battle id 'b' (already defined in a.json).");
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — 컴파일 오류.

- [ ] **Step 3: 편성 스펙·카탈로그·로더**

`Assets/Core/Authoring/Battles/BattleSpec.cs`:
```csharp
namespace FateWeaver.Core.Authoring.Battles
{
    /// <summary>편성 후보 하나. 이번 노드에 어느 후보가 나올지는 편성 스트림이 정한다(설계 결정 6).</summary>
    public sealed class BattleSpec
    {
        public string Id;
        public string[] Enemies;
    }
}
```

`Assets/Core/Authoring/Battles/BattleContentCatalog.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Battles
{
    public sealed class BattleDefinition
    {
        public BattleDefinition(string id, IReadOnlyList<string> enemies)
        {
            Id = id;
            Enemies = enemies;
        }

        public string Id { get; }

        /// <summary>적 정의 id 목록. 전투 안 id는 "{적 id}#{이 목록의 순번}"이다.</summary>
        public IReadOnlyList<string> Enemies { get; }
    }

    public sealed class BattleContentCatalog
    {
        private readonly Dictionary<string, BattleDefinition> _battles;
        private readonly List<string> _ids;

        public BattleContentCatalog(Dictionary<string, BattleDefinition> battles)
        {
            _battles = battles;
            _ids = new List<string>(battles.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>서수 정렬된 id. 편성 스트림이 이 순서의 인덱스를 뽑으므로 순서가 시드 결과의 일부다.</summary>
        public IReadOnlyList<string> Ids => _ids;

        public BattleDefinition Get(string id)
        {
            if (id == null || !_battles.TryGetValue(id, out var battle))
            {
                throw new KeyNotFoundException("No battle content with id '" + id + "'.");
            }

            return battle;
        }
    }
}
```

`Assets/Core/Authoring/Battles/BattleContentLoader.cs`:
```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Authoring.Json;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Battles
{
    public sealed class BattleContentLoadResult
    {
        private BattleContentLoadResult(BattleContentCatalog catalog, IReadOnlyList<string> errors)
        {
            Catalog = catalog;
            Errors = errors;
        }

        public bool Succeeded => Catalog != null;
        public BattleContentCatalog Catalog { get; }
        public IReadOnlyList<string> Errors { get; }

        public static BattleContentLoadResult Ok(BattleContentCatalog catalog)
            => new BattleContentLoadResult(catalog, new string[0]);

        public static BattleContentLoadResult Failed(IReadOnlyList<string> errors)
            => new BattleContentLoadResult(null, errors);
    }

    /// <summary>편성 콘텐츠 소스를 파싱·검증한다. 적 카탈로그를 받으므로 적 뒤에 온다.</summary>
    public static class BattleContentLoader
    {
        private static readonly string[] RequiredKeys = { "id", "enemies" };

        public static BattleContentLoadResult Load(IEnumerable<CardContentSource> sources, EnemyContentCatalog enemies)
        {
            var errors = new List<string>();
            var battles = new Dictionary<string, BattleDefinition>();
            var origin = new Dictionary<string, string>();
            var count = 0;

            foreach (var source in sources)
            {
                count++;
                var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
                if (missing != null)
                {
                    errors.Add(source.Name + ": required key '" + missing + "' is missing.");
                    continue;
                }

                BattleSpec spec;
                try
                {
                    spec = ContentJson.Read<BattleSpec>(source.Json);
                }
                catch (JsonException ex)
                {
                    errors.Add(source.Name + ": " + ContentJsonError.Describe(ex));
                    continue;
                }

                if (string.IsNullOrEmpty(spec.Id))
                {
                    errors.Add(source.Name + ": required key 'id' must be a non-empty string.");
                    continue;
                }

                if (origin.TryGetValue(spec.Id, out var first))
                {
                    errors.Add(
                        source.Name + ": duplicate battle id '" + spec.Id
                        + "' (already defined in " + first + ").");
                    continue;
                }

                origin.Add(spec.Id, source.Name);

                var ids = spec.Enemies ?? new string[0];
                var rejected = false;

                // 세션이 정책 하나만 받는 동안의 제약이다. 다중 적 후속 작업에서 이 검사를 지운다.
                if (ids.Length != 1)
                {
                    errors.Add(source.Name + ": exactly one enemy is supported until per-enemy policies land.");
                    rejected = true;
                }

                foreach (var enemyId in ids)
                {
                    if (!enemies.Contains(enemyId))
                    {
                        errors.Add(source.Name + ": unknown enemy id '" + enemyId + "'.");
                        rejected = true;
                    }
                }

                if (!rejected)
                {
                    battles.Add(spec.Id, new BattleDefinition(spec.Id, ids));
                }
            }

            if (count == 0)
            {
                errors.Add("Battles: at least one battle is required.");
            }

            if (errors.Count > 0)
            {
                return BattleContentLoadResult.Failed(errors);
            }

            return BattleContentLoadResult.Ok(new BattleContentCatalog(battles));
        }
    }
}
```

- [ ] **Step 4: 로더 테스트 통과** — `--filter BattleContentLoaderTests` PASS.

- [ ] **Step 5: 부팅 테스트를 먼저 고친다**

`ContentBootstrapTests.BootstrapLoadsEveryCatalog`에 두 줄 추가:
```csharp
            CollectionAssert.AreEqual(new[] { "goblin" }, result.Content.Enemies.Ids);
            CollectionAssert.AreEqual(new[] { "goblin_single" }, result.Content.Battles.Ids);
```
테스트 추가(정책 키 오타가 부팅 오류 목록에 들어가는지 — 임시 콘텐츠 루트를 복사해 한 파일만 깨뜨린다):
```csharp
        [Test]
        public void BootstrapReportsAnUnknownEnemyPolicy()
        {
            var root = Path.Combine(Path.GetTempPath(), "fate-weaver-bad-policy");
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }

            try
            {
                CopyDirectory(ContentRoot(), root);
                var goblin = Path.Combine(root, "Enemies", "goblin.json");
                File.WriteAllText(goblin, File.ReadAllText(goblin).Replace("\"random_pick\"", "\"random_pik\""));

                var result = ContentBootstrap.Load(root);

                Assert.IsFalse(result.Succeeded);
                CollectionAssert.Contains(result.Errors, "goblin.json: unknown enemy policy 'random_pik'.");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void CopyDirectory(string from, string to)
        {
            foreach (var directory in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(from, to));
            }

            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from, "*.json", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(from, to));
            }
        }
```
임시 경로 이름에 `Guid.NewGuid()`를 쓰지 않는다 — `Tools/verify.sh`의 규칙 7 검사가 `Assets/Core` 전체(테스트 포함)를 훑는다(`verify.sh:242-244`). 이 파일 머리 주석의 "카드 → 덱·풀 → 캐릭터"를 "카드 → 덱·풀 → 캐릭터 → 적 → 편성"으로 고친다.

- [ ] **Step 6: 부팅 연결**

`CardContentFiles.cs:16` 뒤에:
```csharp
        public const string EnemiesFolderName = "Enemies";
        public const string BattlesFolderName = "Battles";
```

`GameContent.cs` 전체:
```csharp
using FateWeaver.Core.Authoring.Battles;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Authoring.Statuses;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 1회로 만들어져 상주하는 콘텐츠 번들. 상태 규칙의 유일한 원본은
    /// Content/Statuses/*.json이며 여기 실려 전투·설명 양쪽에 같은 인스턴스로 주입된다.</summary>
    public sealed class GameContent
    {
        public GameContent(
            StatusContentCatalog statuses,
            CardContentCatalog cards,
            DeckContentCatalog decks,
            PoolContentCatalog pools,
            CharacterContentCatalog characters,
            EnemyContentCatalog enemies,
            BattleContentCatalog battles)
        {
            Statuses = statuses;
            Cards = cards;
            Decks = decks;
            Pools = pools;
            Characters = characters;
            Enemies = enemies;
            Battles = battles;
        }

        public StatusContentCatalog Statuses { get; }
        public CardContentCatalog Cards { get; }
        public DeckContentCatalog Decks { get; }
        public PoolContentCatalog Pools { get; }
        public CharacterContentCatalog Characters { get; }
        public EnemyContentCatalog Enemies { get; }
        public BattleContentCatalog Battles { get; }
    }
}
```

`ContentBootstrap.cs`: `using FateWeaver.Core.Authoring.Battles;`, `using FateWeaver.Core.Authoring.Enemies;` 추가. 클래스 주석(`:30-32`)을 "콘텐츠 루트 하나를 받아 카탈로그를 만든다. 순서는 상태 → 카드 → 덱·풀 → 캐릭터 → 적 → 편성으로 고정이다 — 뒤 단계가 앞 단계의 카탈로그를 필요로 한다. 파일 I/O는 CardContentFiles가 맡으므로 Unity 없이 돈다."로 바꾼다. `:76-90`(캐릭터 로드부터 `return Ok`까지)을 다음으로 바꾼다:
```csharp
            var characters = CharacterContentLoader.Load(
                Read(contentRoot, CardContentFiles.CharactersFolderName, errors),
                decks.Catalog,
                pools.Catalog);
            if (!characters.Succeeded)
            {
                errors.AddRange(characters.Errors);
            }

            if (errors.Count > 0)
            {
                return ContentBootstrapResult.Failed(errors);
            }

            // 적·편성은 오류를 모두 모은 뒤 한 번에 실패한다. 편성은 적 카탈로그가 있어야 검증된다.
            var enemies = EnemyContentLoader.Load(
                Read(contentRoot, CardContentFiles.EnemiesFolderName, errors),
                cards.Catalog,
                AuthoringContext.Default());
            if (!enemies.Succeeded)
            {
                errors.AddRange(enemies.Errors);
            }

            BattleContentLoadResult battles = null;
            if (enemies.Succeeded)
            {
                battles = BattleContentLoader.Load(
                    Read(contentRoot, CardContentFiles.BattlesFolderName, errors), enemies.Catalog);
                if (!battles.Succeeded)
                {
                    errors.AddRange(battles.Errors);
                }
            }

            if (errors.Count > 0)
            {
                return ContentBootstrapResult.Failed(errors);
            }

            return ContentBootstrapResult.Ok(new GameContent(
                statuses.Catalog, cards.Catalog, decks.Catalog, pools.Catalog, characters.Catalog,
                enemies.Catalog, battles.Catalog));
```

`Assets/StreamingAssets/Content/Battles/goblin_single.json`:
```json
{
  "id": "goblin_single",
  "enemies": ["goblin"]
}
```

- [ ] **Step 7: 부팅 테스트 통과** — `--filter ContentBootstrapTests` PASS. Task 4의 `EnemyContentTests.Goblin_is_authored...`를 `TestContent.Content().Enemies.Get("goblin")`로 단순화하고 `CardContentFiles.ReadDirectory`·`EnemyContentLoader` 직접 호출과 불필요한 `using`을 지운다.

- [ ] **Step 8: 공급자 테스트**

`Assets/Core/Tests/EditMode/ContentEncounterSourceTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Battles;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Enemies;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class ContentEncounterSourceTests
    {
        private static ContentEncounterSource RepositorySource()
            => new ContentEncounterSource(TestContent.Content(), CombatRegistries.EnemyPolicies());

        [Test]
        public void Repository_goblin_single_gives_one_goblin_with_combat_and_spec_ids()
        {
            var setup = RepositorySource().Pick(new Random(1));

            Assert.AreEqual(1, setup.Enemies.Count);
            var pair = setup.Enemies[0];
            Assert.AreEqual("goblin#0", pair.Enemy.Id);
            Assert.AreEqual("goblin", pair.Enemy.SpecId);
            Assert.AreEqual(28, pair.Enemy.Hp);
            Assert.IsInstanceOf<RandomPickPolicy>(pair.Policy);
        }

        [Test]
        public void Every_pick_makes_fresh_enemies_and_policies()
        {
            var source = RepositorySource();

            var first = source.Pick(new Random(1)).Enemies[0];
            var second = source.Pick(new Random(1)).Enemies[0];

            Assert.AreNotSame(first.Enemy, second.Enemy);
            Assert.AreNotSame(first.Policy, second.Policy);
        }

        /// <summary>편성 후보 둘인 합성 콘텐츠. 쓰지 않는 카탈로그는 null이다.</summary>
        private static ContentEncounterSource TwoBattles()
        {
            var enemies = new EnemyContentCatalog(new Dictionary<string, EnemyDefinition>
            {
                { "goblin", new EnemyDefinition("goblin", "G", 28, EnemyPolicyKeys.Sequence, new EnemyCardBundle[0]) },
                { "rat", new EnemyDefinition("rat", "R", 10, EnemyPolicyKeys.Sequence, new EnemyCardBundle[0]) }
            });
            var battles = new BattleContentCatalog(new Dictionary<string, BattleDefinition>
            {
                { "goblin_single", new BattleDefinition("goblin_single", new[] { "goblin" }) },
                { "rat_single", new BattleDefinition("rat_single", new[] { "rat" }) }
            });
            var content = new GameContent(null, null, null, null, null, enemies, battles);
            return new ContentEncounterSource(content, CombatRegistries.EnemyPolicies());
        }

        private static string SpecAt(ContentEncounterSource source, int runSeed, int nodeIndex)
            => source.Pick(new Random(SeedDerivation.Stream(
                SeedDerivation.NodeSeed(runSeed, nodeIndex), SeedStream.Encounter))).Enemies[0].Enemy.SpecId;

        [Test]
        public void Same_node_seed_picks_the_same_battle()
        {
            var source = TwoBattles();

            for (int node = 0; node < 10; node++)
            {
                Assert.AreEqual(SpecAt(source, 7, node), SpecAt(source, 7, node));
            }
        }

        [Test]
        public void Different_nodes_reach_both_battles()
        {
            var source = TwoBattles();

            var picked = Enumerable.Range(0, 20).Select(node => SpecAt(source, 7, node)).Distinct().ToArray();

            CollectionAssert.AreEquivalent(new[] { "goblin", "rat" }, picked);
        }
    }
}
```

- [ ] **Step 9: 실패 확인** — 컴파일 오류(`ContentEncounterSource` 없음).

- [ ] **Step 10: 공급자**

`Assets/Core/Simulation/Run/ContentEncounterSource.cs`:
```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Enemies;

namespace FateWeaver.Simulation.Run
{
    /// <summary>편성 스트림으로 저작된 편성 후보 하나를 골라, 적마다 (적, 새 정책) 쌍을 만든다.
    /// 편성이 하나뿐이어도 스트림을 한 번 쓴다 — 스트림은 이름표로 파생되므로 전투·보상 값은 변하지
    /// 않는다(전투 노드 설계 1.2·2.5).</summary>
    public sealed class ContentEncounterSource : IEncounterSource
    {
        private readonly GameContent _content;
        private readonly EnemyPolicyRegistry _policies;

        public ContentEncounterSource(GameContent content, EnemyPolicyRegistry policies)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        }

        public EncounterSetup Pick(Random encounterRng)
        {
            var ids = _content.Battles.Ids;
            var battle = _content.Battles.Get(ids[encounterRng.Next(ids.Count)]);
            var enemies = new List<EncounterEnemy>(battle.Enemies.Count);
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                var definition = _content.Enemies.Get(battle.Enemies[i]);
                enemies.Add(new EncounterEnemy(
                    new Enemy(definition.Id + "#" + i, definition.Id, definition.MaxHp),
                    _policies.Create(definition.Policy, definition.Bundles)));
            }

            return new EncounterSetup(enemies);
        }
    }
}
```

- [ ] **Step 11: 공급자 테스트 통과** — `--filter ContentEncounterSourceTests` PASS.

- [ ] **Step 12: 동등성 테스트를 JSON 적으로 바꾼다**

`GoblinParityTests.BeginNode()`의 `encounters:` 인자만 바꾼다:
```csharp
                encounters: new ContentEncounterSource(content, CombatRegistries.EnemyPolicies()),
```
`using FateWeaver.Core;` 추가.
Run: `--filter GoblinParityTests` → **PASS, 골든 불변.** 실패하면 골든을 고치지 않는다 — 멈추고 서명 차이를 보고한다.

- [ ] **Step 13: 검증·커밋**

`Tools/verify.sh --quick`, 경계 검사 grep.
```bash
git add Assets/Core Assets/StreamingAssets/Content/Battles/goblin_single.json
git commit -m "feat(core): 편성 JSON을 부팅에서 읽고 편성 스트림으로 적과 정책을 만드는 공급자를 둔다" -m "노드 0 전투 서명이 C# 고블린 경로의 골든과 같다." -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: 캐릭터 스탯과 멤버별 생존 충전

**Files:**
- Modify: `Assets/Core/Authoring/Characters/{CharacterSpec,CharacterContentCatalog,CharacterContentLoader}.cs`
- Modify: `Assets/StreamingAssets/Content/Characters/member_a.json`, `member_b.json`
- Modify: `Assets/Core/Simulation/PartyMemberLoadout.cs`, `Assets/Core/Simulation/Run/RunMember.cs`, `Assets/Core/Simulation/Run/RunSetup.cs`, `Assets/Core/Simulation/Run/CombatNode.cs:106`, `Assets/Core/Simulation/DeckCombatSession.cs:127-131,455-461`, `Assets/Core/Combat/PartyTuning.cs`
- Modify tests: `DeckPoolCharacterLoaderTests`, `DeckPoolCharacterContentTests`, `PartyDeckCombatSessionTests`, `CombatNodeTests`, `RunSetupTests`, `RunStateTests`, `GoblinParityTests`, Unity `BattleStageTests`·`BattleUnitsViewIdentityTests`·`ResolutionEventPresenterTests`
- Modify: `Assets/Unity/Scripts/Battle/CombatNodeFlow.cs:79-80`

**Interfaces:**
- Produces:
  - `CharacterContent(string id, string displayName, string deck, string pool, int maxHp, int surviveCharges)`, 속성 `MaxHp`·`SurviveCharges`
  - `RunMember(string id, string name, int maxHp, int surviveCharges, IEnumerable<CardDefinition> cards)`, 속성 `SurviveCharges`
  - `PartyMemberLoadout(string id, string name, int maxHp, int surviveCharges, IReadOnlyList<CardDefinition> cards)`, 속성 `SurviveCharges`
  - `RunSetup.NewRun(GameContent content, IReadOnlyList<string> characterIds, int runSeed)`
  - `PartyTuning` = `MinPartySize`·`MaxPartySize`·`DrawByLivingCount`·`DrawFor`·`Prototype`(드로우 표만)

- [ ] **Step 1: 캐릭터 로더 테스트**

`DeckPoolCharacterLoaderTests.cs`의 인라인 캐릭터 JSON 전부에 `, \"maxHp\": 25, \"surviveCharges\": 1`을 `pool` 키 뒤에 더한다:
```bash
sed -i '' 's/\\"pool\\": \\"\([a-z_]*\)\\" }/\\"pool\\": \\"\1\\", \\"maxHp\\": 25, \\"surviveCharges\\": 1 }/g' Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs
/usr/bin/grep -c "surviveCharges" Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs
```
Expected: 9 (pool을 가진 줄 수 — `/usr/bin/grep -c '\\"pool\\"'`와 같아야 한다). `pool` 누락 테스트(`:381` 근처, `deck` 뒤에서 끝나는 JSON)는 `pool`이 없어야 하므로 이 치환에 걸리지 않는다.

`CharacterLoaderReadsIdNameAndDeck`에 단언 추가:
```csharp
            Assert.AreEqual(25, result.Catalog.Get("member_a").MaxHp);
            Assert.AreEqual(1, result.Catalog.Get("member_a").SurviveCharges);
```
테스트 추가:
```csharp
        [TestCase("{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\", \"pool\": \"starter\", \"surviveCharges\": 1 }",
            "member_a.json: required key 'maxHp' is missing.")]
        [TestCase("{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\", \"pool\": \"starter\", \"maxHp\": 25 }",
            "member_a.json: required key 'surviveCharges' is missing.")]
        [TestCase("{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\", \"pool\": \"starter\", \"maxHp\": 0, \"surviveCharges\": 1 }",
            "member_a.json: maxHp must be positive.")]
        [TestCase("{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\", \"pool\": \"starter\", \"maxHp\": 25, \"surviveCharges\": -1 }",
            "member_a.json: surviveCharges must not be negative.")]
        public void CharacterLoaderRejectsInvalidStats(string json, string error)
        {
            var result = CharacterContentLoader.Load(
                new[] { Source("member_a.json", json) },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")),
                StarterPool());

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, error);
        }
```

`DeckPoolCharacterContentTests.cs`에 테스트 추가:
```csharp
        [Test]
        public void CharacterJsonCarriesPrototypeStats()
        {
            var characters = Characters();

            foreach (var id in characters.Ids)
            {
                Assert.AreEqual(25, characters.Get(id).MaxHp, id);
                Assert.AreEqual(1, characters.Get(id).SurviveCharges, id);
            }
        }
```

- [ ] **Step 2: 실패 확인** — 컴파일 오류(`MaxHp` 없음).

- [ ] **Step 3: 캐릭터 저작 타입**

`CharacterSpec.cs`에 필드 추가:
```csharp
        /// <summary>최대 HP. 매 전투 이 값으로 시작한다(HP 인계는 후속 작업).</summary>
        public int MaxHp;

        /// <summary>죽을 피해를 버티는 횟수. 전투마다 전량 충전된다.</summary>
        public int SurviveCharges;
```
`CharacterContentCatalog.cs`의 `CharacterContent` 생성자에 `int maxHp, int surviveCharges`를 끝에 더하고 `public int MaxHp { get; }`, `public int SurviveCharges { get; }`를 추가한다.
`CharacterContentLoader.cs:35`: `RequiredKeys = { "id", "displayName", "deck", "pool", "maxHp", "surviveCharges" };`. `:99` 뒤(풀 검사 다음)에:
```csharp
                if (spec.MaxHp <= 0)
                {
                    errors.Add(source.Name + ": maxHp must be positive.");
                    rejected = true;
                }

                if (spec.SurviveCharges < 0)
                {
                    errors.Add(source.Name + ": surviveCharges must not be negative.");
                    rejected = true;
                }
```
`:105` 생성 호출을 `new CharacterContent(spec.Id, spec.DisplayName, spec.Deck, spec.Pool, spec.MaxHp, spec.SurviveCharges)`로.

`member_a.json`:
```json
{
  "id": "member_a",
  "displayName": "파티원 A",
  "deck": "starter",
  "pool": "starter",
  "maxHp": 25,
  "surviveCharges": 1
}
```
`member_b.json`: 같은 모양, `"id": "member_b"`, `"displayName": "파티원 B"`, `"deck": "party_prototype"`.

- [ ] **Step 4: 캐릭터 테스트 통과** — `--filter "DeckPoolCharacterLoaderTests|DeckPoolCharacterContentTests"` PASS.

- [ ] **Step 5: 멤버별 생존 충전 테스트**

`PartyDeckCombatSessionTests.cs`:
- `Loadout` 헬퍼(`:56-61`)를:
```csharp
        private static PartyMemberLoadout Loadout(
            string id,
            IReadOnlyList<CardDefinition> cards = null,
            int maxHp = 25,
            string name = null,
            int surviveCharges = 1)
            => new PartyMemberLoadout(id, name ?? id, maxHp, surviveCharges, cards ?? Array.Empty<CardDefinition>());
```
- `Tuning(int)`(`:63-77`)의 객체 초기화에서 `DefaultMemberMaxHp = 25,`·`SurviveChargesPerCombat = 1,` 두 줄을 지운다.
- `Constructor_rejects_empty_oversized_duplicate_or_invalid_party`(`:95-137`): `:113`을 `new PartyMemberLoadout("a", "A", 25, 1, null)`로. 끝의 `PartyTuning` HP·충전 검증 두 `Assert.Throws`(`:124-136`)를 다음으로 바꾼다:
```csharp
            Assert.Throws<ArgumentException>(() => Session(new[] { Loadout("a", surviveCharges: -1) }, tuning: Tuning(1)));
```
- `Constructor_rejects_missing_or_non_positive_draw_tuning_entries`(`:139-162`)의 세 초기화에서 `DefaultMemberMaxHp = 25,`·`SurviveChargesPerCombat = 1,`를 지운다.
- `Prototype_tuning_is_hp_25_survive_1_and_draw_3_4_5`(`:164-175`)를:
```csharp
        [Test]
        public void Prototype_tuning_is_party_1_to_3_and_draw_3_4_5()
        {
            var tuning = PartyTuning.Prototype;

            Assert.AreEqual(1, tuning.MinPartySize);
            Assert.AreEqual(3, tuning.MaxPartySize);
            Assert.AreEqual(3, tuning.DrawFor(1));
            Assert.AreEqual(4, tuning.DrawFor(2));
            Assert.AreEqual(5, tuning.DrawFor(3));
        }

        [Test]
        public void Each_member_starts_with_its_own_survive_charges()
        {
            var session = Session(new[] { Loadout("a", surviveCharges: 0), Loadout("b", surviveCharges: 2) });

            Assert.AreEqual(0, session.State.Party[0].SurviveCharges);
            Assert.AreEqual(2, session.State.Party[1].SurviveCharges);
        }
```

- [ ] **Step 6: 실패 확인** — 컴파일 오류.

- [ ] **Step 7: 세션·로드아웃·런 멤버**

`PartyMemberLoadout.cs`:
```csharp
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }

        /// <summary>이 전투에서 죽을 피해를 버티는 횟수. 캐릭터 JSON이 원본이다.</summary>
        public int SurviveCharges { get; }
        public IReadOnlyList<CardDefinition> Cards { get; }

        public PartyMemberLoadout(
            string id,
            string name,
            int maxHp,
            int surviveCharges,
            IReadOnlyList<CardDefinition> cards)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            SurviveCharges = surviveCharges;
            Cards = cards;
        }
```

`DeckCombatSession.cs:127-131`: `partyTuning.SurviveChargesPerCombat` → `loadout.SurviveCharges`.
`DeckCombatSession.cs:455-461`의 첫 검사를:
```csharp
            if (tuning == null)
            {
                throw new System.ArgumentException("Party tuning is invalid.");
            }
```
로드아웃 검사(`:474-478`)의 조건에 `|| loadout.SurviveCharges < 0`을 `loadout.MaxHp <= 0` 뒤에 더한다.

`Assets/Core/Combat/PartyTuning.cs`: `DefaultMemberMaxHp`·`SurviveChargesPerCombat` 속성과 `Prototype` 안의 두 줄을 지운다. 요약 주석을 `/// <summary>파티 규모 한계와 생존자 수별 드로우 표. 멤버 HP·생존 충전은 캐릭터 JSON이 원본이다.</summary>`로.

`RunMember.cs`:
```csharp
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }

        /// <summary>전투마다 로드아웃으로 전달되는 생존 충전 수. 전투 중 소모는 세션의 PartyMember가 들고, 이 값은 줄지 않는다.</summary>
        public int SurviveCharges { get; }
        public int Hp { get; set; }
        public List<CardDefinition> Cards { get; } = new();
        public bool IsAlive => Hp > 0;

        public RunMember(string id, string name, int maxHp, int surviveCharges, IEnumerable<CardDefinition> cards)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            SurviveCharges = surviveCharges;
            Hp = maxHp;
            if (cards != null)
            {
                Cards.AddRange(cards);
            }
        }
```

`CombatNode.cs:106`: `new PartyMemberLoadout(member.Id, member.Name, member.MaxHp, member.SurviveCharges, member.Cards.ToList())`.

`RunSetup.cs:11-27`:
```csharp
        public static RunState NewRun(
            GameContent content,
            IReadOnlyList<string> characterIds,
            int runSeed)
        {
            var party = new List<RunMember>();
            foreach (var id in characterIds)
            {
                var character = content.Characters.Get(id);
                party.Add(new RunMember(
                    character.Id,
                    character.DisplayName,
                    character.MaxHp,
                    character.SurviveCharges,
                    DeckCards(content, character.Deck)));
            }

            return new RunState(party, runSeed);
        }
```
`using FateWeaver.Core.Combat;`이 더 필요 없으면 지운다.

- [ ] **Step 8: 호출부 정리**

- `RunSetupTests.cs`: 네 곳의 `PartyTuning.Prototype, ` 인자를 지운다. `:25`를 `Assert.AreEqual(25, a.MaxHp);`로, 그 아래에 `Assert.AreEqual(1, a.SurviveCharges);` 추가. 쓰지 않게 된 `using`을 지운다.
- `RunStateTests.cs:11-12`: `new RunMember("member_a", "파티원 A", 25, 1, null)`, `new RunMember("member_b", "파티원 B", 25, 1, null)`.
- `CombatNodeTests.cs:19-20`: `new RunMember(id, id, 20, 0, Enumerable.Range(...))`. `Tuning()`(`:22-29`)에서 `DefaultMemberMaxHp = 20,`·`SurviveChargesPerCombat = 0,`를 지운다.
- `GoblinParityTests.BeginNode()`: `RunSetup.NewRun(content, new[] { "member_a", "member_b" }, RunSeed)`.
- Unity 테스트 3개(`BattleStageTests.cs:40-45·136-137`, `BattleUnitsViewIdentityTests.cs:50-55·123-124`, `ResolutionEventPresenterTests.cs:41-46·173-174`): `new PartyTuning { ... }`에서 `DefaultMemberMaxHp`·`SurviveChargesPerCombat` 줄을 지우고, `Loadout` 헬퍼를 `new PartyMemberLoadout(id, name, maxHp, 0, Array.Empty<CardDefinition>())`로. **이전 `SurviveChargesPerCombat` 값이 0이 아니었던 파일은 그 값을 넣는다** — 파일마다 지우기 전에 값을 읽는다(`BattleUnitsViewIdentityTests`는 0).
- `CombatNodeFlow.cs:79-80`: `_run = RunSetup.NewRun(_content, _party.Select(member => member.Id).ToList(), _runSeed);`

- [ ] **Step 9: 검증**

Run: `Tools/verify.sh --quick` → 전체 통과, **`GoblinParityTests` 골든 불변**.
Run: `/usr/bin/grep -rn "DefaultMemberMaxHp\|SurviveChargesPerCombat" Assets --include='*.cs'` → 출력 없음.

- [ ] **Step 10: 커밋**

```bash
git add Assets/Core Assets/StreamingAssets/Content/Characters Assets/Tests Assets/Unity/Scripts/Battle/CombatNodeFlow.cs
git commit -m "feat(core): 멤버 최대 HP와 생존 충전을 캐릭터 JSON에서 멤버별로 받는다" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: `combat_rules.json`과 `CombatRules`

**Files:**
- Create: `Assets/Core/Authoring/Rules/CombatRulesSpec.cs`, `CombatRules.cs`, `CombatRulesLoader.cs`
- Create: `Assets/StreamingAssets/Content/combat_rules.json`
- Create: `Assets/Core/Tests/EditMode/CombatRulesLoaderTests.cs`
- Modify: `Assets/Core/Authoring/{CardContentFiles,GameContent,ContentBootstrap}.cs`, `Assets/Core/Simulation/Run/CombatNode.cs:18-44,113-115,175`
- Modify tests: `CombatNodeTests.cs:108-112`, `GoblinParityTests`, `ContentBootstrapTests`, `EnemyContentTests`, `ContentEncounterSourceTests`(합성 `GameContent` 인자)
- Modify: `Assets/Unity/Scripts/Battle/CombatNodeFlow.cs:56-62`

**Interfaces:**
- Produces:
  - `CombatRules(PartyTuning party, int fateEnergyPerTurn, int rewardChoices)`, 속성 `Party`·`FateEnergyPerTurn`·`RewardChoices`
  - `CombatRulesLoader.Load(CardContentSource source) → CombatRulesLoadResult {Succeeded, Rules, Errors}` (`source == null`이면 파일 없음 오류)
  - `CardContentFiles.CombatRulesFileName = "combat_rules.json"`
  - `GameContent(..., enemies, battles, CombatRules combatRules)`, 속성 `CombatRules`
  - `CombatNodeContext(StatusContentCatalog statuses, CombatRules rules, IEncounterSource encounters, IRewardCandidateSource rewardCandidates)`, 속성 `Statuses`·`Rules`·`Encounters`·`RewardCandidates`

- [ ] **Step 1: 규칙 로더 테스트**

`Assets/Core/Tests/EditMode/CombatRulesLoaderTests.cs`:
```csharp
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Rules;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CombatRulesLoaderTests
    {
        private const string Valid =
            "{ \"fateEnergyPerTurn\": 3, \"minPartySize\": 1, \"maxPartySize\": 3, "
            + "\"drawByLivingCount\": { \"1\": 3, \"2\": 4, \"3\": 5 }, \"rewardChoices\": 3 }";

        private static CombatRulesLoadResult Load(string json)
            => CombatRulesLoader.Load(new CardContentSource("combat_rules.json", json));

        [Test]
        public void Loads_rules_with_party_tuning_inside()
        {
            var result = Load(Valid);

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            Assert.AreEqual(3, result.Rules.FateEnergyPerTurn);
            Assert.AreEqual(3, result.Rules.RewardChoices);
            Assert.AreEqual(1, result.Rules.Party.MinPartySize);
            Assert.AreEqual(3, result.Rules.Party.MaxPartySize);
            Assert.AreEqual(3, result.Rules.Party.DrawFor(1));
            Assert.AreEqual(4, result.Rules.Party.DrawFor(2));
            Assert.AreEqual(5, result.Rules.Party.DrawFor(3));
        }

        [Test]
        public void Missing_file_is_an_error()
        {
            var result = CombatRulesLoader.Load(null);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "combat_rules.json: file is missing.");
        }

        [TestCase("fateEnergyPerTurn")]
        [TestCase("minPartySize")]
        [TestCase("maxPartySize")]
        [TestCase("drawByLivingCount")]
        [TestCase("rewardChoices")]
        public void Rejects_a_missing_required_key(string key)
        {
            var json = Valid.Replace("\"" + key + "\"", "\"removed_" + key + "\"");

            var result = Load(json);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "combat_rules.json: required key '" + key + "' is missing.");
        }

        [TestCase("\"fateEnergyPerTurn\": 3", "\"fateEnergyPerTurn\": 0", "combat_rules.json: fateEnergyPerTurn must be positive.")]
        [TestCase("\"minPartySize\": 1", "\"minPartySize\": 0", "combat_rules.json: minPartySize must be at least 1.")]
        [TestCase("\"minPartySize\": 1", "\"minPartySize\": 4", "combat_rules.json: minPartySize must not exceed maxPartySize.")]
        [TestCase("\"rewardChoices\": 3", "\"rewardChoices\": 0", "combat_rules.json: rewardChoices must be positive.")]
        [TestCase("\"3\": 5", "\"3\": 0", "combat_rules.json: drawByLivingCount must give a positive draw for living count 3.")]
        [TestCase(", \"3\": 5", "", "combat_rules.json: drawByLivingCount must give a positive draw for living count 3.")]
        public void Rejects_invalid_values(string from, string to, string error)
        {
            var result = Load(Valid.Replace(from, to));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Rules);
            CollectionAssert.Contains(result.Errors, error);
        }
    }
}
```
주의: `Rejects_a_missing_required_key`는 키 이름을 바꿔 누락을 만든다. `ContentJson`은 `MissingMemberHandling.Error`라 바뀐 키가 역직렬화 오류를 내지만, 필수 키 검사가 파싱보다 먼저이므로 기대 오류가 목록에 들어간다(`CharacterContentLoader.cs:48-53`와 같은 순서).

- [ ] **Step 2: 실패 확인** — 컴파일 오류.

- [ ] **Step 3: 규칙 타입과 로더**

`Assets/Core/Authoring/Rules/CombatRulesSpec.cs`:
```csharp
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Rules
{
    /// <summary>combat_rules.json의 저작 모양. 파티 전체에 걸리는 규칙만 담는다 — 멤버별 수치는 캐릭터 JSON.</summary>
    public sealed class CombatRulesSpec
    {
        public int FateEnergyPerTurn;
        public int MinPartySize;
        public int MaxPartySize;
        public Dictionary<int, int> DrawByLivingCount;
        public int RewardChoices;
    }
}
```

`Assets/Core/Authoring/Rules/CombatRules.cs`:
```csharp
using System;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Authoring.Rules
{
    /// <summary>검증된 전투 규칙 묶음. 세션은 Party만, 노드는 운명력·보상 장수까지 쓴다(설계 결정 15).</summary>
    public sealed class CombatRules
    {
        public CombatRules(PartyTuning party, int fateEnergyPerTurn, int rewardChoices)
        {
            Party = party ?? throw new ArgumentNullException(nameof(party));
            FateEnergyPerTurn = fateEnergyPerTurn;
            RewardChoices = rewardChoices;
        }

        public PartyTuning Party { get; }
        public int FateEnergyPerTurn { get; }
        public int RewardChoices { get; }
    }
}
```

`Assets/Core/Authoring/Rules/CombatRulesLoader.cs`:
```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Combat;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring.Rules
{
    public sealed class CombatRulesLoadResult
    {
        private CombatRulesLoadResult(CombatRules rules, IReadOnlyList<string> errors)
        {
            Rules = rules;
            Errors = errors;
        }

        public bool Succeeded => Rules != null;
        public CombatRules Rules { get; }
        public IReadOnlyList<string> Errors { get; }

        public static CombatRulesLoadResult Ok(CombatRules rules) => new CombatRulesLoadResult(rules, new string[0]);

        public static CombatRulesLoadResult Failed(IReadOnlyList<string> errors) => new CombatRulesLoadResult(null, errors);
    }

    /// <summary>Content 루트의 단일 파일 combat_rules.json을 검증한다. 다른 카탈로그에 의존하지 않는다.</summary>
    public static class CombatRulesLoader
    {
        public const string FileName = "combat_rules.json";

        private static readonly string[] RequiredKeys =
            { "fateEnergyPerTurn", "minPartySize", "maxPartySize", "drawByLivingCount", "rewardChoices" };

        public static CombatRulesLoadResult Load(CardContentSource source)
        {
            if (source == null)
            {
                return CombatRulesLoadResult.Failed(new[] { FileName + ": file is missing." });
            }

            var missing = ContentKeys.FirstMissing(source.Json, RequiredKeys);
            if (missing != null)
            {
                return CombatRulesLoadResult.Failed(new[] { source.Name + ": required key '" + missing + "' is missing." });
            }

            CombatRulesSpec spec;
            try
            {
                spec = ContentJson.Read<CombatRulesSpec>(source.Json);
            }
            catch (JsonException ex)
            {
                return CombatRulesLoadResult.Failed(new[] { source.Name + ": " + ContentJsonError.Describe(ex) });
            }

            var errors = new List<string>();
            if (spec.FateEnergyPerTurn <= 0)
            {
                errors.Add(source.Name + ": fateEnergyPerTurn must be positive.");
            }

            if (spec.MinPartySize < 1)
            {
                errors.Add(source.Name + ": minPartySize must be at least 1.");
            }
            else if (spec.MinPartySize > spec.MaxPartySize)
            {
                errors.Add(source.Name + ": minPartySize must not exceed maxPartySize.");
            }

            for (int living = 1; living <= spec.MaxPartySize; living++)
            {
                if (spec.DrawByLivingCount == null
                    || !spec.DrawByLivingCount.TryGetValue(living, out var draw)
                    || draw <= 0)
                {
                    errors.Add(source.Name + ": drawByLivingCount must give a positive draw for living count " + living + ".");
                }
            }

            if (spec.RewardChoices <= 0)
            {
                errors.Add(source.Name + ": rewardChoices must be positive.");
            }

            if (errors.Count > 0)
            {
                return CombatRulesLoadResult.Failed(errors);
            }

            var party = new PartyTuning
            {
                MinPartySize = spec.MinPartySize,
                MaxPartySize = spec.MaxPartySize,
                DrawByLivingCount = spec.DrawByLivingCount
            };
            return CombatRulesLoadResult.Ok(new CombatRules(party, spec.FateEnergyPerTurn, spec.RewardChoices));
        }
    }
}
```
주의: `"minPartySize": 4` 케이스는 `maxPartySize` 3이라 `min > max` 오류와 함께 드로우 오류가 나지 않는다(1..3 모두 있음) — 테스트 기대와 일치.

- [ ] **Step 4: 로더 테스트 통과** — `--filter CombatRulesLoaderTests` PASS.

- [ ] **Step 5: 파일·부팅**

`Assets/StreamingAssets/Content/combat_rules.json`:
```json
{
  "fateEnergyPerTurn": 3,
  "minPartySize": 1,
  "maxPartySize": 3,
  "drawByLivingCount": {
    "1": 3,
    "2": 4,
    "3": 5
  },
  "rewardChoices": 3
}
```

`CardContentFiles.cs`에 `public const string CombatRulesFileName = "combat_rules.json";`.
`GameContent.cs`: `using FateWeaver.Core.Authoring.Rules;`, 생성자 끝 인자 `CombatRules combatRules`, 속성 `public CombatRules CombatRules { get; }`.
`ContentBootstrap.cs`: `using FateWeaver.Core.Authoring.Rules;`. Task 5에서 넣은 편성 블록 뒤, `if (errors.Count > 0)` 앞에:
```csharp
            var rules = CombatRulesLoader.Load(ReadFile(contentRoot, CardContentFiles.CombatRulesFileName));
            if (!rules.Succeeded)
            {
                errors.AddRange(rules.Errors);
            }
```
`Ok` 호출 끝 인자에 `rules.Rules`. `Read` 아래에 헬퍼 추가:
```csharp
        /// <summary>루트 단일 파일. 없으면 null — 파일 없음 오류는 로더가 자기 이름으로 낸다.</summary>
        private static CardContentSource ReadFile(string contentRoot, string fileName)
        {
            var path = Path.Combine(contentRoot, fileName);
            return File.Exists(path) ? new CardContentSource(fileName, File.ReadAllText(path)) : null;
        }
```
클래스 주석의 순서 문장 끝에 "→ 전투 규칙"을 더한다.

`ContentBootstrapTests.BootstrapLoadsEveryCatalog`에 `Assert.AreEqual(3, result.Content.CombatRules.RewardChoices);`. `ContentEncounterSourceTests.TwoBattles()`의 `new GameContent(...)` 끝에 `null` 인자 하나 추가.

`EnemyContentTests`에 저장소 규칙 골든 추가:
```csharp
        [Test]
        public void Combat_rules_json_matches_the_prototype_values()
        {
            var rules = TestContent.Content().CombatRules;

            Assert.AreEqual(3, rules.FateEnergyPerTurn);
            Assert.AreEqual(3, rules.RewardChoices);
            Assert.AreEqual(1, rules.Party.MinPartySize);
            Assert.AreEqual(3, rules.Party.MaxPartySize);
            Assert.AreEqual(3, rules.Party.DrawFor(1));
            Assert.AreEqual(4, rules.Party.DrawFor(2));
            Assert.AreEqual(5, rules.Party.DrawFor(3));
        }
```

- [ ] **Step 6: 노드 문맥**

`CombatNode.cs:18-44`:
```csharp
    /// <summary>전투 노드가 쓰는 규칙과 공급자. 규칙은 combat_rules.json 하나에서 온다.</summary>
    public sealed class CombatNodeContext
    {
        public CombatNodeContext(
            StatusContentCatalog statuses,
            CombatRules rules,
            IEncounterSource encounters,
            IRewardCandidateSource rewardCandidates)
        {
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            RewardCandidates = rewardCandidates ?? throw new ArgumentNullException(nameof(rewardCandidates));
        }

        public StatusContentCatalog Statuses { get; }
        public CombatRules Rules { get; }
        public IEncounterSource Encounters { get; }
        public IRewardCandidateSource RewardCandidates { get; }
    }
```
`using FateWeaver.Core.Authoring.Rules;` 추가. `Begin`의 세션 생성(`:113-115`): `context.Rules.Party,` / `fateEnergyPerTurn: context.Rules.FateEnergyPerTurn,`. 보상(`:175`): `_context.Rules.RewardChoices`.

`CombatNodeTests.cs:108-112`:
```csharp
        private static CombatNodeContext Context(
            IEncounterSource encounters, IRewardCandidateSource rewards, int rewardChoices = 3)
            => new CombatNodeContext(
                TestContent.Statuses(),
                new CombatRules(Tuning(), fateEnergyPerTurn: 3, rewardChoices: rewardChoices),
                encounters,
                rewards);
```
`using FateWeaver.Core.Authoring.Rules;` 추가.

`GoblinParityTests.BeginNode()`:
```csharp
            var context = new CombatNodeContext(
                content.Statuses,
                content.CombatRules,
                new ContentEncounterSource(content, CombatRegistries.EnemyPolicies()),
                new CharacterPoolRewardSource(content));
```

`CombatNodeFlow.cs:56-62`:
```csharp
            _context = new CombatNodeContext(
                _content.Statuses,
                _content.CombatRules,
                new GoblinEncounterSource(),
                new CharacterPoolRewardSource(_content));
```
(공급자 교체는 Task 8에서 한다 — 이 태스크는 컴파일만 유지한다.)

- [ ] **Step 7: 검증·커밋**

`Tools/verify.sh --quick` → 전체 통과, **`GoblinParityTests` 골든 불변**. 경계 검사 grep.
```bash
git add Assets/Core Assets/StreamingAssets/Content/combat_rules.json Assets/Unity/Scripts/Battle/CombatNodeFlow.cs
git commit -m "feat(core): 전투 규칙을 combat_rules.json에서 읽어 노드 문맥 하나로 넘긴다" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Unity 배선·씬 재생성·배치 검증

**Files:**
- Modify: `Assets/Unity/Scripts/Battle/CombatNodeFlow.cs:1-26,56-62`, `Assets/Unity/Scripts/Battle/BattleScreenController.cs:92-98`, `Assets/Unity/Scripts/Text/PlaytestKoreanText.cs:33-35,46-53`
- Modify: `Assets/Tests/UnityEditMode/CombatNodeFlowSceneTests.cs`
- Regenerate: `Assets/Scenes/FateWeaverBattle.unity`
- Add: Task 1~7 신규 파일·폴더의 `.meta`

**Interfaces:**
- Consumes: `GameContent.Enemies`·`CombatRules`, `ContentEncounterSource`, `CombatRegistries.EnemyPolicies()`.

- [ ] **Step 1: 씬 테스트를 먼저 고친다**

`CombatNodeFlowSceneTests.cs`의 마지막 `Assert.IsNull(...)` 뒤에:
```csharp
                foreach (var removed in new[] { "_fateEnergyPerTurn", "_rewardChoices" })
                {
                    Assert.IsNull(
                        typeof(CombatNodeFlow).GetField(removed, BindingFlags.Instance | BindingFlags.NonPublic),
                        removed + "는 combat_rules.json으로 옮겨졌다.");
                }
```

- [ ] **Step 2: 흐름·컨트롤러·텍스트**

`CombatNodeFlow.cs`:
- `:24-26`(Tooltip과 두 필드) 삭제.
- `using FateWeaver.Core;` 추가. `using FateWeaver.Simulation;`이 더 쓰이지 않으면 삭제.
- 컨텍스트 조립:
```csharp
            _context = new CombatNodeContext(
                _content.Statuses,
                _content.CombatRules,
                new ContentEncounterSource(_content, CombatRegistries.EnemyPolicies()),
                new CharacterPoolRewardSource(_content));
```

`BattleScreenController.cs:92-98`의 반환 줄:
```csharp
                    return _content.Enemies.Get(enemy.SpecId).DisplayName;
```
요약 주석은 "적 이름은 전투 안 id가 아니라 정의 id(SpecId)로 적 JSON의 displayName을 찾는다 — 같은 적이 여럿이어도 이름은 같다."로.

`PlaytestKoreanText.cs`: `:33-35`의 고블린 카드 세 `case`와 `EnemyName` 메서드(`:46-53`) 삭제. `using FateWeaver.Simulation;`(`:5`)도 삭제 — 이 파일에서 `GoblinDeck` 외에는 쓰지 않는다. 삭제 전 `/usr/bin/grep -rn "EnemyName(" Assets --include='*.cs'` → `BattleScreenController`만 나와야 했고 이제 0건.

- [ ] **Step 3: 헤드리스 확인** — `Tools/verify.sh --quick` 통과(Unity 코드는 컴파일되지 않지만 코어 회귀 확인).

- [ ] **Step 4: 씬 재생성(배치)**

좀비 라이선싱 클라이언트부터 확인한다(규칙 25): `pgrep -lf Unity.Licensing.Client` — 판별·조치는 `docs/agents/unity-batch-runs.md`. 다른 세션의 Unity 프로세스는 죽이지 않는다(규칙 26).
```bash
WT=/Users/ish/Git/rogue-deck/.claude/worktrees/combat-node-stage2
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$WT" \
  -executeMethod FateWeaver.Unity.Editor.BattleSceneBuilder.Build -quit \
  -logFile /private/tmp/combat-node-stage2-scene.log
echo "exit=$?"
/usr/bin/grep -n "error CS\|BattleSceneBuilder:" /private/tmp/combat-node-stage2-scene.log | head
```
Expected: `exit=0`, `error CS` 없음. 컴파일 오류가 있으면 로그의 파일·줄을 고치고 다시 돌린다(헤드리스가 잡지 못한 Unity 쪽 오류 — 어셈블리 경계, Unity 테스트의 `using`).
Run: `/usr/bin/grep -c "_fateEnergyPerTurn\|_rewardChoices" Assets/Scenes/FateWeaverBattle.unity` → `0`.

- [ ] **Step 5: EditMode 배치**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$WT" \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/combat-node-stage2-editmode.xml \
  -logFile /private/tmp/combat-node-stage2-editmode.log
head -c 600 /private/tmp/combat-node-stage2-editmode.xml
```
`-quit`를 붙이지 않는다. Expected: `test-run`의 `result="Passed"`, `failed="0"`. 종료 코드만 보지 않는다.

- [ ] **Step 6: `.meta`와 부산물 정리**

```bash
git status --short
```
- 커밋할 것: Task 1~7에서 만든 `.cs`·`.json`·폴더(`Assets/Core/Enemies`, `Assets/Core/Authoring/{Enemies,Battles,Rules}`, `Assets/StreamingAssets/Content/{Enemies,Battles}`)의 `.meta`, `Content/combat_rules.json.meta`, 적 카드 3장·`GoblinParityTests.cs`·신규 테스트 파일의 `.meta`, 수정한 씬.
- 되돌릴 것: 폰트 아틀라스(`KoreanTMP.asset` 등) 같은 런타임 부산물 — `git restore <경로>`. 판단이 서지 않는 변경은 멈추고 보고한다.
확인: `git ls-files --others --exclude-standard Assets | /usr/bin/grep -v "\.meta$"` → 출력 없음(메타 없는 신규 파일이 남지 않았다).

- [ ] **Step 7: 커밋**

```bash
git add Assets/Unity Assets/Tests Assets/Scenes/FateWeaverBattle.unity
git add $(git ls-files --others --exclude-standard Assets | /usr/bin/grep "\.meta$")
git commit -m "feat(ui): 전투 노드 흐름이 콘텐츠의 전투 규칙과 편성 공급자를 쓰고 적 이름을 JSON에서 읽는다" -m "씬을 다시 만들어 옮겨 간 인스펙터 값을 지우고, 2단계 신규 파일의 .meta를 함께 올린다." -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 8: 사용자 확인 요청**

사용자에게 워크트리 경로와 함께 요청한다: 이 워크트리를 Unity로 열어 Play — 승리 → 보상 → 다음 전투, 패배 → 처음부터, 적 이름 "고블린" 표시. 결과를 받을 때까지 Task 9로 가도 되지만 머지는 하지 않는다.

---

### Task 9: C# 원본 삭제·테스트 이전·문서

**Files:**
- Delete: `Assets/Core/Simulation/GoblinDeck.cs(.meta)`, `GoblinEncounterSource.cs(.meta)`, `PartyPrototypeRoster.cs(.meta)`, `Assets/Core/Tests/EditMode/GoblinDeckTests.cs(.meta)`
- Modify: `Assets/Core/Combat/PartyTuning.cs`(`Prototype` 삭제), `Assets/Core/Tests/EditMode/{EncounterSourceTests,DescriptionComposerTests,DescriptionCatalogValidatorTests,DeckPileVisibilityTests,CombatRngDeterminismTests,DeckPoolCharacterContentTests,PartyDeckCombatSessionTests,EnemyContentTests}.cs`
- Modify docs: `docs/agents/content-authoring.md`, `docs/superpowers/README.md`, `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md`, `docs/superpowers/specs/2026-09-15-combat-node-cycle-design.md`·`.html`

- [ ] **Step 1: 고블린 헬퍼를 테스트 콘텐츠에 둔다**

`Assets/Core/Tests/EditMode/TestContent.cs` 끝에:
```csharp
        /// <summary>저장소 고블린의 새 적·정책 쌍. C# GoblinDeck을 대체한다 — 원본은 Enemies/goblin.json.</summary>
        public static FateWeaver.Simulation.Run.EncounterEnemy Goblin()
            => new FateWeaver.Simulation.Run.ContentEncounterSource(
                    Content(), FateWeaver.Core.CombatRegistries.EnemyPolicies())
                .Pick(new System.Random(0)).Enemies[0];
```
(저장소 편성은 `goblin_single` 하나라 어떤 `Random`이든 고블린이다. 편성이 늘면 `EnemyContentTests`가 먼저 알린다.)

- [ ] **Step 2: 참조 테스트 이전**

- `DescriptionComposerTests.cs:262-274`: `GoblinDeck.Thrust()`·`CrudeGuard()`·`SlyJab()` → `TestContent.Cards().Get("goblin_jab")`·`Get("crude_guard")`·`Get("sly_jab")`. 기대 문자열은 그대로.
- `DescriptionCatalogValidatorTests.cs:17`: `.Concat(GoblinDeck.AllCards())` → `.Concat(new[] { "goblin_jab", "crude_guard", "sly_jab" }.Select(id => TestContent.Cards().Get(id)))`.
- `DeckPileVisibilityTests.cs:13-18`:
```csharp
        private static DeckCombatSession NewSession()
        {
            var deck = TestContent.StarterDeckCards();
            var goblin = TestContent.Goblin();
            return new DeckCombatSession(TestContent.Statuses(),
                deck, 30, new[] { new Enemy(goblin.Enemy.SpecId, goblin.Enemy.Hp) },
                goblin.Policy, 3, 5, 1);
        }
```
- `CombatRngDeterminismTests.cs:20-25`: 같은 방식 — `var goblin = TestContent.Goblin();` 후 `new[] { new Enemy(goblin.Enemy.SpecId, goblin.Enemy.Hp) }`, `goblin.Policy`. (솔로 세션 테스트라 적 id는 기존과 같은 `"goblin"`을 유지한다.)
- `EncounterSourceTests.cs`: 고블린 공급자 테스트 둘(`:27-50`)을 지운다(`ContentEncounterSourceTests`가 대체). `Enemy` id 분리 테스트 둘은 남긴다. 쓰지 않는 `using` 삭제.
- `DeckPoolCharacterContentTests.cs:118-156`: `PartyPrototypeRoster.MemberAId`/`MemberBId` → `"member_a"`/`"member_b"`, `MemberAName`/`MemberBName` → `"파티원 A"`/`"파티원 B"`.
- `PartyDeckCombatSessionTests.Prototype_tuning_is_party_1_to_3_and_draw_3_4_5` 삭제(`EnemyContentTests.Combat_rules_json_matches_the_prototype_values`가 대체).
- `GoblinDeckTests.cs`의 정책 동작 테스트 셋(`Policy_deploys_exactly_one_authored_bundle_each_turn`·`Every_bundle_is_reachable`·`Policy_is_deterministic_by_seed`)을 `EnemyContentTests`로 옮기되 `GoblinDeck.Policy()` → `TestContent.Goblin().Policy`(호출마다 새 인스턴스가 필요한 곳은 매번 `TestContent.Goblin()`), `GoblinDeck.Bundles()` → `TestContent.Content().Enemies.Get("goblin").Bundles`. 나머지 넷(HP·카드·AllCards·묶음)은 `EnemyContentTests`가 이미 덮으므로 옮기지 않는다.

- [ ] **Step 3: 원본 삭제**

```bash
git rm -q Assets/Core/Simulation/GoblinDeck.cs Assets/Core/Simulation/GoblinDeck.cs.meta \
  Assets/Core/Simulation/GoblinEncounterSource.cs Assets/Core/Simulation/GoblinEncounterSource.cs.meta \
  Assets/Core/Simulation/PartyPrototypeRoster.cs Assets/Core/Simulation/PartyPrototypeRoster.cs.meta \
  Assets/Core/Tests/EditMode/GoblinDeckTests.cs Assets/Core/Tests/EditMode/GoblinDeckTests.cs.meta
```
`GoblinEncounterSource.cs.meta`가 없으면(1단계 `.meta` 커밋 여부) 해당 경로만 빼고 다시 실행한다.
`PartyTuning.cs`에서 `Prototype` 속성 삭제.
Run: `/usr/bin/grep -rn "GoblinDeck\|GoblinEncounterSource\|PartyPrototypeRoster\|PartyTuning.Prototype" Assets --include='*.cs'` → 출력 없음.

- [ ] **Step 4: 검증**

`Tools/verify.sh` 전체 → 통과(`GoblinParityTests` 골든 불변 포함). Unity 쪽 C#도 바뀌었으므로(Task 8 이후 없음 — 이 태스크는 Unity 코드를 건드리지 않는다) 배치는 생략해도 되지만, `Assets/Tests/UnityEditMode`에서 삭제 타입 참조가 없는지 위 grep이 확인한다.

- [ ] **Step 5: 문서**

- `docs/agents/content-authoring.md` 「원본의 위치」: `Content/<종류>/*.json` 줄의 종류 목록에 `Enemies`·`Battles`를 더하고, 다음 문장을 추가: "전투 규칙(턴당 운명력·파티 규모·생존자 수별 드로우·보상 장수)은 종류 폴더가 아니라 루트 단일 파일 `Content/combat_rules.json`이다." 파일을 먼저 읽고 기존 문장 형식에 맞춘다.
- `docs/superpowers/README.md:168` 부근 "(b) **적 카드는 아직 JSON이 아니다** — `GoblinDeck`의 순수 C#에서 나오며, …" 문장을 "(b) 적 카드도 `Content/Cards/`의 JSON이며(`side: Enemy`), 적·편성은 `Content/Enemies/`·`Content/Battles/`에서 읽는다."로 바꾸고 문단 흐름을 맞춘다. 후속 작업 대기열의 `DeckCombatSession.cs:387` 인용을 실제 줄로 고친다(`/usr/bin/grep -n "OwnerId = _state.Enemies.Count == 1" Assets/Core/Simulation/DeckCombatSession.cs`). 전투 노드 색인 행 상태를 "2단계 구현 완료(날짜)"로.
- `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md` §14.2(`:588-601`)의 `PlaytestKoreanText` 항목에 "`EnemyName`과 고블린 카드 이름 3건은 전투 노드 2단계에서 제거(적 JSON `displayName`·카드 JSON `name`)"를 반영한다.
- 설계 `.md` 상태 줄을 "2단계 구현 완료(날짜)", `.html` 칩을 "2단계 구현 완료"로.
- 이 계획을 `docs/superpowers/.archive/`로 옮기고(`.md`·`.html` 둘 다) README 활성 계획 표에서 행을 지운다(규칙 20). 행이 없으면 지울 것 없음.

- [ ] **Step 6: 커밋**

```bash
git add -A Assets/Core docs
git commit -m "refactor(core): 고블린·파티 프로토타입의 C# 원본을 지우고 테스트를 JSON 콘텐츠로 옮긴다" -m "전투 노드 2단계 완료를 문서에 기록하고 구현 계획을 보관한다." -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 7: 마무리**

`superpowers:finishing-a-development-branch`로 넘어간다. **master 머지는 사용자 승인 후**(규칙 19), 머지 전 `Tools/verify.sh` 전체 통과 확인. 머지 후 Unity를 돌린 워크트리이므로 반드시 제거한다(`docs/agents/worktrees.md`).
