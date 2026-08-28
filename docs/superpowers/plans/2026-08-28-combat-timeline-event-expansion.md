# 전투 타임라인 이벤트 확장 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-08-28
- 상태: `active` — 미착수
- 선행: [전투 상호작용 로그](../archive/plans/2026-07-31-combat-interaction-log.md) **완료·머지·보관 (2026-08-28)**
- 설계 승인: 2026-08-28 대화에서 A안(범용 HpChanged + 신규 이벤트) + "로그 1건 = 이벤트 1건" 승인됨

---

## 설계 개요 (사람 검수용)

이 절만 읽고 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은 읽지
않아도 된다.

**무엇을 만드나** — 턴 해석 중 전투 상태를 바꾸는 모든 사실(캐릭터별 HP 변화, 운명력 적립, 상태
소비, 카드 귀속 버프의 부여·소모, 대형 이동, 취소 카드의 부분 피해)을 타입 있는 이벤트로
타임라인에 남기고, 포매터가 **이벤트 1건 = 콘솔 로그 1건**으로 한국어 출력한다. 이벤트는 장차
카드/유물 발동 조건의 원천 데이터가 된다.

**구조**

| 객체 | 책임 (한 줄) | 이 객체가 모르는 것 |
|---|---|---|
| 신규 이벤트 record 6종 | 일어난 사실 하나를 타입과 데이터로 담는다 | 왜 일어났는지의 규칙, 표시 문구 |
| 효과 핸들러·`TurnResolver` | 상태를 바꾸는 그 지점에서 이벤트를 발행한다 | 이벤트가 어떻게 표시·소비되는지 |
| `TimelineTextFormatter.FormatEvent` | 이벤트 1건을 로그 문자열 1건으로 바꾼다 | 전투 규칙, Unity |
| Unity 배선 | 이벤트 수만큼 `Debug.Log`를 호출한다 | 무엇이 출력될지 |

**의존 방향** — `전투 규칙 → 타임라인 이벤트 → 포매터 → Unity Console` (기존과 동일, 한 방향)

**확장 축**
- *갈아끼울 수 있는 것* — 새 사실 = record 1개 + 발행 1줄 + 포매터 case 1개. 기존 이벤트는 안
  건드린다. 출력 문구·형식은 포매터만 고치면 된다.
- *한번 정하면 고정되는 것* — 사실 하나 = 이벤트 하나(페이로드에 사실을 묻지 않는다), 로그는 항상
  수집(모드 없음), **값이 실제로 바뀐 것만 남긴다**(HP 불변 명중·제자리 대형 이동은 침묵).

**대안과 기각 이유**

1. *기존 이벤트 페이로드 확장(B안)* — 기각. 사실이 다른 이벤트의 페이로드에 묻히면 장차 트리거
   조건 매칭이 이벤트마다 다른 내부를 뒤져야 하고, `CardResolved`가 계속 자란다는 기존 경고를
   악화시킨다.
2. *턴 전체를 한 문자열로 덤프 유지* — 기각. 사용자 결정으로 로그 1건 = 이벤트 1건. Console에서
   이벤트별 접기·필터가 되고 인게임 패널이 항목 단위로 붙기 쉽다.

**이 선택으로 나중에 어려워지는 것**

- **타임라인이 눈에 띄게 길어진다.** 광역 카드 1장이 HpChanged를 대상 수만큼 남긴다. `events[N]`
  인덱스 단언 테스트는 앞으로도 계속 밀린다 (전례: 2026-07-31 확장에서 27곳 중 실제 1곳 파손,
  OfType 전환 관례 확립).
- **트리거의 "즉시 반응"은 이 구조로 안 된다.** 이벤트는 해석이 끝난 뒤 목록으로만 존재한다.
  트리거 기능이 오면 발행 순간을 관찰하는 훅이 별도 작업으로 필요하다.
- **이벤트 순서는 카드 단위로만 발생 순서와 일치한다.** `ExtraEvents`가 `CardResolved` 뒤에
  몰리는 기존 설계를 유지한다. 정밀한 교차 순서가 필요해지면 발행 순서 재정렬이 별도 작업이다.
- **운명력의 충전·지출은 여전히 안 보인다.** 둘 다 세션 영역(`DeckCombatSession`)이라 이번
  범위(턴 해석) 밖이고, 개입 로그 확장(별도 계획)과 함께 다룬다.

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.

**Goal:** 턴 해석 중의 모든 상태 변화를 이벤트로 남기고, 이벤트당 로그 1건으로 Console에 출력한다.

**Architecture:** 사실 하나 = `ResolutionEvent` 파생 record 하나. 발행은 전부 기존 경로
(`ctx.ExtraEvents` 또는 `TurnResolver`의 events 목록)를 타고, 포매터는 이벤트만 읽는다(규칙 11).
신규 파일은 만들지 않는다 — record는 전부 `ResolutionEvent.cs`에, 테스트는 `CombatLogTests.cs`에
추가한다 (.meta 누락 사고 원천 차단).

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1), NUnit

**Spec:** 이 문서의 [`## 설계 개요 (사람 검수용)`](#설계-개요-사람-검수용). 선행 구현의
이벤트 순서와 포매터 경계는
[`2026-07-31-combat-interaction-log.md`](../archive/plans/2026-07-31-combat-interaction-log.md)의
`## 상세 (세션 인계용)`을 따른다.

## Global Constraints

- 헤드리스 테스트: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- **기준선: 533 passed / 0 failed (2026-08-28 master `05fbf92` 실측).** 착수 세션이 첫 실행에서 재실측할 것
- 새 이벤트가 끼면 `events[N]` 인덱스 단언이 밀릴 수 있다. **인덱스를 다시 세지 말고
  `OfType<X>().Single(...)`로 전환한다** (2026-07-31 계획 Task 2 관례)
- `FateWeaver.Core`에서 `UnityEngine` 참조 금지 (규칙 6)
- 결정론 보호 (규칙 7): 이벤트 발행이 규칙 판정·RNG 소비 순서를 바꾸면 안 된다
- 코어의 출력은 이벤트 타임라인뿐, 별도 채널 금지 (규칙 11)
- 상태 이름은 `KoreanDescriptionCatalog`에서 (규칙 10). 테스트 카탈로그는
  `KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses())`
- **값이 실제로 바뀐 경우에만 이벤트를 남긴다** (HP 불변 명중, 제자리 대형 이동은 발행하지 않는다)
- C# 9 한계: `record struct` 금지, 기본 인터페이스 구현 금지
- 커밋 메시지: 규칙 27 (`타입(범위): 한국어 현재형 제목`)
- 신규 .cs 파일 금지 — record·테스트를 기존 파일에 추가한다 (.meta 누락 방지)

## 파일 구조

| 파일 | 이번 계획에서의 책임 |
|---|---|
| `Assets/Core/Events/ResolutionEvent.cs` | 신규 record 6종 + `HpChangeSource` + `CardBuffIds`, `CardCancelled` 페이로드 |
| `Assets/Core/Effects/DamageHandler.cs` | HP 적용 6개 지점 → `HitEnemy`/`HitParty` 헬퍼 경유 + `HpChanged` 발행, 피해 보너스 소모 발행 |
| `Assets/Core/Effects/TriggerStatusHandler.cs` | 조기 발동 틱의 `HpChanged` 발행 (2곳) |
| `Assets/Core/Combat/TurnResolver.cs` | 턴 종료 틱의 `HpChanged`, 취소 페이로드 탑재, `ResolveTier`·`IsInterceptedByStatus`의 카드 버프 소모 발행 |
| `Assets/Core/Effects/GrantNextTurnFateHandler.cs` | `FateEnergyGained` 발행 |
| `Assets/Core/Effects/ConsumeStatusHandler.cs` | `StatusConsumed` + 보너스 `CardBuffGranted` 발행 (2곳) |
| `Assets/Core/Effects/GrantNextPlayerDamageCardBonusHandler.cs` | `CardBuffGranted` 발행 |
| `Assets/Core/Effects/NullifyNextPlayerConditionRewardHandler.cs` | `CardBuffGranted` 발행 |
| `Assets/Core/Effects/MoveFormationHandler.cs` | `FormationMoved` 발행 (4곳) |
| `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs` | `FormatEvent` 단건 API + 신규 case 6종 + 취소 피해 표시 |
| `Assets/Unity/Scripts/Battle/BattleScreenController.cs` | 이벤트당 `Debug.Log` 1건 |

테스트: `Assets/Core/Tests/EditMode/CombatLogTests.cs`에 추가. 밀리는 인덱스 테스트는 그때그때 OfType 전환.

---

### Task 1: HpChanged — 캐릭터별 HP 변화를 남긴다

카드 피해(단일·광역·스냅샷·파티), 상태 틱(턴 종료·조기 발동)의 모든 HP 변경 지점에서 발행한다.
After는 치명 버팀 클램프 이후 실측값이다.

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`, `Assets/Core/Effects/DamageHandler.cs`,
  `Assets/Core/Effects/TriggerStatusHandler.cs`, `Assets/Core/Combat/TurnResolver.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Produces: `HpChangeSource { CardDamage, StatusTick }`,
  `HpChanged(string HolderId, int Before, int After, HpChangeSource Source, string SourceId)`
  — SourceId는 CardDamage면 카드 id, StatusTick이면 상태 키

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`CombatLogTests.cs`에 추가한다. 기존 `Statuses()` 헬퍼에 `r.Register(new PoisonBehavior());`를
한 줄 추가한다 (독 틱 테스트용, 기존 테스트에 무해).

```csharp
        [Test]
        public void Damage_emits_hp_changed_per_target_with_before_and_after()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin_a", 10));
            state.Enemies.Add(new Enemy("goblin_b", 10));
            var def = new CardDefinition("sweep", "sweep", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 4) { TargetSelector = TargetSelector.All } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var changes = events.OfType<HpChanged>().ToArray();

            Assert.AreEqual(2, changes.Length);
            Assert.AreEqual(("goblin_a", 10, 6, HpChangeSource.CardDamage, "sweep"),
                (changes[0].HolderId, changes[0].Before, changes[0].After, changes[0].Source, changes[0].SourceId));
            Assert.AreEqual(("goblin_b", 10, 6), (changes[1].HolderId, changes[1].Before, changes[1].After));
        }

        [Test]
        public void Deaths_door_hp_change_shows_the_clamp_to_one()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(30);
            player.Hp = 4;
            player.SurviveCharges = 1;
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("jab", "jab", Side.Enemy, 1,
                new[] { new EffectData(EffectKeys.Damage, 6) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = "goblin" });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var change = events.OfType<HpChanged>().Single();

            Assert.AreEqual((CombatState.SoloPlayerId, 4, 1), (change.HolderId, change.Before, change.After));
            Assert.IsTrue(events.OfType<DeathsDoorSurvived>().Any());
        }

        [Test]
        public void Turn_end_poison_tick_emits_hp_changed_with_status_source()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 10);
            enemy.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            state.Enemies.Add(enemy);

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var change = events.OfType<HpChanged>().Single();

            Assert.AreEqual(("goblin", 10, 7, HpChangeSource.StatusTick, "poison"),
                (change.HolderId, change.Before, change.After, change.Source, change.SourceId));
        }

        [Test]
        public void A_fully_blocked_hit_leaves_no_hp_changed()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 10);
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), 10);
            state.Enemies.Add(enemy);
            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 4) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<HpChanged>());
        }
```

위 arrange의 `Stack`/`Add` 서명과 `TargetSelector` 프로퍼티는 2026-08-28에
`StatusBag.cs`·`ConsumeStatusTests.cs`·`DamageHandler.cs`에서 다시 확인했다.

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~CombatLogTests"`

Expected: 컴파일 실패 — `HpChanged`, `HpChangeSource` 없음 (CS0246).

- [ ] **Step 3: 이벤트를 추가한다**

`ResolutionEvent.cs`의 `TurnEnded` 위에 추가한다.

```csharp
    /// <summary>HP 변화의 원인 종류. 새 원인(회복 등)이 생기면 멤버를 추가한다.</summary>
    public enum HpChangeSource { CardDamage, StatusTick }

    /// <summary>보유자의 HP가 실제로 바뀌었다. Before/After는 치명 버팀 클램프 이후의 실측값이고,
    /// SourceId는 원인 카드 id(CardDamage) 또는 상태 키(StatusTick)다. HP가 안 바뀐 명중은
    /// 남기지 않는다.</summary>
    public sealed record HpChanged(
        string HolderId, int Before, int After, HpChangeSource Source, string SourceId) : ResolutionEvent;
```

- [ ] **Step 4: DamageHandler의 HP 적용을 헬퍼로 모은다**

`DamageHandler.cs`에 private 헬퍼 2개를 추가하고, HP를 직접 바꾸는 6개 지점을 전부 교체한다.

```csharp
        /// <summary>적에게 피해를 적용하고, HP가 실제로 바뀌었으면 HpChanged를 남긴다.</summary>
        private static void HitEnemy(EffectContext ctx, Enemy target, int dealt)
        {
            var before = target.Hp;
            target.Hp -= dealt;
            if (target.Hp != before)
            {
                ctx.ExtraEvents.Add(new Events.HpChanged(
                    target.Id, before, target.Hp, Events.HpChangeSource.CardDamage, ctx.Card.Def.Id));
            }
        }

        /// <summary>파티원에게 피해를 적용하고(치명 버팀 경유), HP가 실제로 바뀌었으면 HpChanged를
        /// 남긴다. After는 클램프 이후 실측값이라 치명 버팀 발동 시 1로 남는다.</summary>
        private static void HitParty(EffectContext ctx, PartyMember target, int dealt)
        {
            var before = target.Hp;
            target.TakeDamage(dealt);
            if (target.Hp != before)
            {
                ctx.ExtraEvents.Add(new Events.HpChanged(
                    target.Id, before, target.Hp, Events.HpChangeSource.CardDamage, ctx.Card.Def.Id));
            }
        }
```

교체 지점 (2026-08-28 실측 줄번호):
- `Apply` 광역 적 루프의 `each.Hp -= dealt;` (L61) → `HitEnemy(ctx, each, dealt);`
- `Apply` 단일 적의 `target.Hp -= damage;` (L79) → `HitEnemy(ctx, target, damage);`
- `Apply` 광역 파티 루프의 `each.TakeDamage(dealt);` (L101) → `HitParty(ctx, each, dealt);`
- `Apply` 단일 파티의 `target.TakeDamage(damage);` (L120) → `HitParty(ctx, target, damage);`
- `ApplySnapshotTargets` 적 루프의 `target.Hp -= dealt;` (L142) → `HitEnemy(ctx, target, dealt);`
- `ApplySnapshotTargets` 파티 루프의 `target.TakeDamage(dealt);` (L168) → `HitParty(ctx, target, dealt);`

기존 주석("Routed through PartyMember.TakeDamage …")은 헬퍼의 XML 주석으로 흡수됐으므로 교체
지점의 낡은 인라인 주석은 지운다.

- [ ] **Step 5: 턴 종료 틱이 HpChanged를 남기게 한다**

`TurnResolver.cs`의 `TickHolder`에 `Func<int> getHp`를 추가하고 `OnTurnEnd` 앞뒤로 HP를 잰다.
발행 위치가 `OnTurnEnd` 호출 뒤이므로 `HpChanged`는 그 틱의 `StatusTicked` **뒤에** 놓인다.

```csharp
        private void TickHolder(
            StatusBag bag, string holderId, Func<int> getHp, Action<int> dealDamage,
            List<ResolutionEvent> events, Authoring.Statuses.StatusContentCatalog content)
        {
            // Snapshot: a hook may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior))
                {
                    var hpBefore = getHp();
                    behavior.OnTurnEnd(new StatusTickContext
                    {
                        Instance = status,
                        HolderBag = bag,
                        HolderId = holderId,
                        DealDamage = dealDamage,
                        Events = events,
                        Content = content
                    });
                    var hpAfter = getHp();
                    if (hpAfter != hpBefore)
                    {
                        events.Add(new HpChanged(
                            holderId, hpBefore, hpAfter, HpChangeSource.StatusTick, status.Key.Id));
                    }
                }
            }
        }
```

`RunTurnEndTicks`의 호출 2곳을 맞춘다:

```csharp
                TickHolder(target.Statuses, target.Id, () => target.Hp,
                    damage => target.TakeDamage(damage), events, state.StatusContent);
```

```csharp
                TickHolder(target.Statuses, target.Id, () => target.Hp,
                    damage => target.Hp -= damage, events, state.StatusContent);
```

- [ ] **Step 6: 조기 발동 틱(TriggerStatus)도 남긴다**

`TriggerStatusHandler.cs`는 이미 `hpBefore`를 재고 있다 (L50, L94). 두 곳 모두
`ctx.DamageDealt` 갱신 직후에 추가한다:

```csharp
                    if (target.Hp != hpBefore)
                    {
                        ctx.ExtraEvents.Add(new Events.HpChanged(
                            target.Id, hpBefore, target.Hp,
                            Events.HpChangeSource.StatusTick, payload.Key.Id));
                    }
```

(스냅샷 쪽은 변수명이 `enemy`다 — `enemy.Hp != hpBefore` 형태로 맞춘다.)

- [ ] **Step 7: 전체 테스트를 확인하고, 밀린 인덱스 단언을 OfType으로 옮긴다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: **537 passed / 0 failed**. `events[N]`이 깨지면 인덱스를 다시 세지 말고 `OfType<X>().Single(...)`로
바꾼다. 어느 파일 몇 곳을 옮겼는지 보고서에 남긴다.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Core/Events/ResolutionEvent.cs \
  Assets/Core/Effects/DamageHandler.cs \
  Assets/Core/Effects/TriggerStatusHandler.cs \
  Assets/Core/Combat/TurnResolver.cs \
  Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "feat(core): 캐릭터별 HP 변화를 타임라인 이벤트로 남긴다"
```

---

### Task 2: 취소된 카드의 부분 피해를 보존한다

효과 도중 취소된 카드가 그때까지 실제로 준 피해와 단계 내역을 `CardCancelled`에 싣는다.
이벤트 수 불변(init 프로퍼티 + 기본값).

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`, `Assets/Core/Combat/TurnResolver.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Produces: `CardCancelled.DamageDealt` (`int`, 기본 0), `CardCancelled.DamageSteps`
  (`IReadOnlyList<DamageStep>`, 기본 빈 목록)

- [ ] **Step 1: 실패하는 테스트를 작성한다**

```csharp
        [Test]
        public void A_card_cancelled_mid_effects_keeps_its_dealt_damage_on_the_event()
        {
            // 효과 1(피해 5)이 마지막 적을 죽이고, 효과 2(피해)가 대상을 못 찾아 카드가 취소된다.
            // 취소는 이미 준 피해를 되돌리지 않으므로 로그에서도 사라지면 안 된다.
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 3));
            var def = new CardDefinition("double_strike", "double_strike", Side.Player, 1,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 5),
                    new EffectData(EffectKeys.Damage, 5)
                });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var cancelled = events.OfType<CardCancelled>().Single();

            Assert.IsEmpty(events.OfType<CardResolved>());
            Assert.AreEqual(5, cancelled.DamageDealt);
            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "goblin"));
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~cancelled_mid_effects"`

Expected: 컴파일 실패 — `CardCancelled.DamageDealt` 없음 (CS1061).

- [ ] **Step 3: 페이로드를 싣는다**

`ResolutionEvent.cs`의 `CardCancelled`를 본문 있는 record로 바꾼다 (생성자 서명 불변):

```csharp
    public sealed record CardCancelled(
        int InstanceId,
        string CardId,
        string OwnerId,
        CardCancellationReason Reason) : ResolutionEvent
    {
        /// <summary>취소 전에 이미 적용된 효과가 준 실제 피해와 그 단계 내역. 취소가 피해를
        /// 되돌리지 않으므로 로그에서도 사라지면 안 된다. 효과 실행 전에 취소된 카드는 기본값
        /// (0, 빈 목록)이다.</summary>
        public int DamageDealt { get; init; }
        public System.Collections.Generic.IReadOnlyList<DamageStep> DamageSteps { get; init; }
            = System.Array.Empty<DamageStep>();
    }
```

`TurnResolver.ResolveCard`의 **미드-이펙트 취소 분기**(현재 L164, "Step 6" 주석 아래)만 바꾼다.
효과 실행 전 취소 분기(현재 L56)는 피해가 있을 수 없으므로 그대로 둔다.

```csharp
                events.Add(new CardCancelled(
                    card.InstanceId, card.Def.Id, card.OwnerId, card.CancellationReason.Value)
                {
                    DamageDealt = totalDamage,
                    DamageSteps = damageSteps
                });
```

- [ ] **Step 4: 전체 테스트 확인** — Expected: **538 passed / 0 failed**

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Events/ResolutionEvent.cs \
  Assets/Core/Combat/TurnResolver.cs \
  Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "feat(core): 취소된 카드의 부분 피해를 이벤트에 보존한다"
```

---

### Task 3: 운명력 적립·상태 소비 이벤트

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`, `Assets/Core/Effects/GrantNextTurnFateHandler.cs`,
  `Assets/Core/Effects/ConsumeStatusHandler.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Produces: `FateEnergyGained(string SourceCardId, int Amount)`,
  `StatusConsumed(string HolderId, string StatusId, int Amount)`

- [ ] **Step 1: 실패하는 테스트를 작성한다**

테스트 전용 레지스트리는 각 테스트 안에서 만든다. 아래 코드는 `ConsumeStatusTests`의 현재
생성자·레지스트리 서명을 반영한 완성된 arrange다.

```csharp
        [Test]
        public void Granting_next_turn_fate_emits_fate_energy_gained()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("distill", "증류", Side.Player, 5,
                new[] { new EffectData(EffectKeys.GrantNextTurnFate, 1) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new GrantNextTurnFateHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);
            var gained = events.OfType<FateEnergyGained>().Single();

            Assert.AreEqual(("distill", 1), (gained.SourceCardId, gained.Amount));
        }

        [Test]
        public void Consuming_a_status_emits_status_consumed_with_the_amount()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            var enemy = new Enemy("goblin", 20);
            enemy.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 2);
            state.Enemies.Add(enemy);
            var def = new CardDefinition("drain", "흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 0) }
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new ConsumeStatusHandler());
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior());

            var events = new TurnResolver(effects, statuses).Resolve(state, 0);
            var consumed = events.OfType<StatusConsumed>().Single();

            Assert.AreEqual(("goblin", "poison", 2), (consumed.HolderId, consumed.StatusId, consumed.Amount));
        }

        [Test]
        public void Zero_fate_gain_emits_no_state_change_event()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("empty_distill", "빈 증류", Side.Player, 5,
                new[] { new EffectData(EffectKeys.GrantNextTurnFate, 0) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new GrantNextTurnFateHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);

            Assert.AreEqual(0, state.PendingNextTurnFateEnergy);
            Assert.IsEmpty(events.OfType<FateEnergyGained>());
        }

        [Test]
        public void Consuming_a_missing_status_emits_no_state_change_event()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("empty_drain", "빈 흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 0) }
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            var effects = new EffectRegistry();
            effects.Register(new ConsumeStatusHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<StatusConsumed>());
        }
```

- [ ] **Step 2: 실패 확인** — Expected: 컴파일 실패 (CS0246).

- [ ] **Step 3: 이벤트를 추가한다** (`ResolutionEvent.cs`, `TurnEnded` 위)

```csharp
    /// <summary>다음 플레이어 턴에 지급될 운명력이 적립되었다 (증류). 실제 지급(턴 시작 리필)과
    /// 지출은 세션 영역이라 이 타임라인에 없다 — 개입 로그 확장에서 다룬다.</summary>
    public sealed record FateEnergyGained(string SourceCardId, int Amount) : ResolutionEvent;

    /// <summary>효과가 보유자의 상태 수치를 능동 소비했다 (수명 만료·자동 소진과 구분).</summary>
    public sealed record StatusConsumed(string HolderId, string StatusId, int Amount) : ResolutionEvent;
```

- [ ] **Step 4: 발행한다**

`GrantNextTurnFateHandler.Apply` 끝:

```csharp
            var before = ctx.State.PendingNextTurnFateEnergy;
            ctx.State.PendingNextTurnFateEnergy += ctx.EffectValue;
            var gained = ctx.State.PendingNextTurnFateEnergy - before;
            if (gained != 0)
            {
                ctx.ExtraEvents.Add(new Events.FateEnergyGained(ctx.Card.Def.Id, gained));
            }
```

`ConsumeStatusHandler`의 `if (consumed > 0)` 블록 **2곳**(레거시 `Apply` L49-62,
`ApplySnapshotTargets` L83-96) 각각에서 `RecordConsumedStatus` 호출 직후:

```csharp
                ctx.ExtraEvents.Add(new Events.StatusConsumed(enemy.Id, payload.Key.Id, consumed));
```

- [ ] **Step 5: 전체 테스트 확인** — Expected: **542 passed / 0 failed**. 밀린 인덱스 단언은
  OfType 전환.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Events/ResolutionEvent.cs \
  Assets/Core/Effects/GrantNextTurnFateHandler.cs \
  Assets/Core/Effects/ConsumeStatusHandler.cs \
  Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "feat(core): 운명력 적립과 상태 소비를 타임라인에 남긴다"
```

---

### Task 4: 카드 귀속 버프의 부여·소모 이벤트

피해 보너스(`_pendingDamageBonus`)와 카드 귀속 상태(`reward_nullified`, 카드 요격형)의 부여·소모를
남긴다.

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`,
  `Assets/Core/Effects/GrantNextPlayerDamageCardBonusHandler.cs`,
  `Assets/Core/Effects/NullifyNextPlayerConditionRewardHandler.cs`,
  `Assets/Core/Effects/ConsumeStatusHandler.cs`, `Assets/Core/Effects/DamageHandler.cs`,
  `Assets/Core/Combat/TurnResolver.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Produces: `CardBuffIds.DamageBonus` (`"damage_bonus"`),
  `CardBuffGranted(int CardInstanceId, string CardId, string BuffId, int Amount)`,
  `CardBuffConsumed(int CardInstanceId, string CardId, string BuffId, int Amount)`
  — BuffId는 상태 키 또는 `CardBuffIds` 상수

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`CombatLogTests.cs`의 using 목록에 `using FateWeaver.Core.Conditions;`를 추가하고 아래 테스트를
클래스에 넣는다.

```csharp
        [Test]
        public void Damage_bonus_grant_and_spend_both_reach_the_timeline()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var grant = new CardDefinition("empower", "empower", Side.Player, 1,
                new[] { new EffectData(EffectKeys.GrantNextPlayerDamageCardBonus, 2) });
            var strike = new CardDefinition("strike", "strike", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 3) });
            state.Zone.Add(new ExecutionCardInstance(grant) { OwnerId = CombatState.SoloPlayerId, InstanceId = 1 });
            state.Zone.Add(new ExecutionCardInstance(strike) { OwnerId = CombatState.SoloPlayerId, InstanceId = 2 });
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new GrantNextPlayerDamageCardBonusHandler());

            var events = new TurnResolver(effects, Statuses()).Resolve(state, 0);
            var granted = events.OfType<CardBuffGranted>().Single();
            var spent = events.OfType<CardBuffConsumed>().Single();

            Assert.AreEqual((2, "strike", CardBuffIds.DamageBonus, 2),
                (granted.CardInstanceId, granted.CardId, granted.BuffId, granted.Amount));
            Assert.AreEqual((2, "strike", CardBuffIds.DamageBonus, 2),
                (spent.CardInstanceId, spent.CardId, spent.BuffId, spent.Amount));
            Assert.AreEqual(5, events.OfType<CardResolved>().Single(e => e.CardId == "strike").DamageDealt);
        }

        [Test]
        public void Reward_nullify_grant_reaches_the_timeline()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var nullify = new CardDefinition("disrupt", "disrupt", Side.Enemy, 1,
                new[] { new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 1) });
            var strike = new CardDefinition("strike", "strike", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 3) });
            state.Zone.Add(new ExecutionCardInstance(nullify) { OwnerId = "goblin", InstanceId = 1 });
            state.Zone.Add(new ExecutionCardInstance(strike) { OwnerId = CombatState.SoloPlayerId, InstanceId = 2 });
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());

            var events = new TurnResolver(effects, Statuses()).Resolve(state, 0);
            var granted = events.OfType<CardBuffGranted>().Single();

            Assert.AreEqual((2, "strike", StatusKeys.RewardNullified.Id, 1),
                (granted.CardInstanceId, granted.CardId, granted.BuffId, granted.Amount));
        }

        [Test]
        public void Reward_nullified_consumption_reaches_the_timeline()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var nullify = new CardDefinition("disrupt", "disrupt", Side.Enemy, 1,
                new[] { new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0) });
            var strike = new CardDefinition("quick_cut", "quick_cut", Side.Player, 2,
                new[]
                {
                    EffectData.Conditional(
                        EffectKeys.Damage, 2, new WithinNth(2), successEffectValue: 10)
                });
            state.Zone.Add(new ExecutionCardInstance(nullify) { OwnerId = "goblin", InstanceId = 1 });
            state.Zone.Add(new ExecutionCardInstance(strike)
                { OwnerId = CombatState.SoloPlayerId, InstanceId = 2 });
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());

            var events = new TurnResolver(effects, Statuses()).Resolve(state, 0);
            var spent = events.OfType<CardBuffConsumed>()
                .Single(e => e.BuffId == StatusKeys.RewardNullified.Id);

            Assert.AreEqual((2, "quick_cut", StatusKeys.RewardNullified.Id, 1),
                (spent.CardInstanceId, spent.CardId, spent.BuffId, spent.Amount));
            Assert.AreEqual(
                ConditionTier.Basic,
                events.OfType<CardResolved>().Single(e => e.CardId == "quick_cut").ConditionTier);
        }

        [Test]
        public void Card_intercept_consumption_reaches_the_timeline()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            var card = new ExecutionCardInstance(def)
                { OwnerId = CombatState.SoloPlayerId, InstanceId = 7 };
            card.Statuses.Add(TimelineNullifyingBehavior.TestKey, StatusLifetime.UntilConsumed(1));
            state.Zone.Add(card);

            var statuses = Statuses();
            statuses.Register(new TimelineNullifyingBehavior());
            var events = new TurnResolver(Effects(), statuses).Resolve(state, 0);
            var spent = events.OfType<CardBuffConsumed>().Single();

            Assert.AreEqual((7, "strike", TimelineNullifyingBehavior.TestKey.Id, 1),
                (spent.CardInstanceId, spent.CardId, spent.BuffId, spent.Amount));
            Assert.AreEqual(
                CardCancellationReason.StatusIntercepted,
                events.OfType<CardCancelled>().Single().Reason);
        }

        [Test]
        public void Zero_damage_bonus_grant_emits_no_state_change_event()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var grant = new CardDefinition("empty_empower", "empty_empower", Side.Player, 1,
                new[] { new EffectData(EffectKeys.GrantNextPlayerDamageCardBonus, 0) });
            var strike = new CardDefinition("strike", "strike", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 3) });
            state.Zone.Add(new ExecutionCardInstance(grant));
            state.Zone.Add(new ExecutionCardInstance(strike));
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new GrantNextPlayerDamageCardBonusHandler());

            var events = new TurnResolver(effects, Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<CardBuffGranted>());
            Assert.IsEmpty(events.OfType<CardBuffConsumed>());
        }

        [Test]
        public void Reapplying_identical_reward_nullify_emits_no_grant_event()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var nullify = new CardDefinition("disrupt", "disrupt", Side.Enemy, 1,
                new[] { new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0) });
            var strike = new CardDefinition("strike", "strike", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 3) });
            var strikeCard = new ExecutionCardInstance(strike)
                { OwnerId = CombatState.SoloPlayerId, InstanceId = 2 };
            strikeCard.Statuses.Add(StatusKeys.RewardNullified, StatusLifetime.UntilConsumed(1));
            state.Zone.Add(new ExecutionCardInstance(nullify) { OwnerId = "goblin", InstanceId = 1 });
            state.Zone.Add(strikeCard);
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());

            var events = new TurnResolver(effects, Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<CardBuffGranted>());
            Assert.IsTrue(strikeCard.Statuses.Has(StatusKeys.RewardNullified));
        }

        [Test]
        public void Replacing_a_different_reward_nullify_state_emits_a_grant_event()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var nullify = new CardDefinition("disrupt", "disrupt", Side.Enemy, 1,
                new[] { new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0) });
            var strike = new CardDefinition("strike", "strike", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 3) });
            var strikeCard = new ExecutionCardInstance(strike)
                { OwnerId = CombatState.SoloPlayerId, InstanceId = 2 };
            strikeCard.Statuses.Add(StatusKeys.RewardNullified, StatusLifetime.Permanent);
            state.Zone.Add(new ExecutionCardInstance(nullify) { OwnerId = "goblin", InstanceId = 1 });
            state.Zone.Add(strikeCard);
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new NullifyNextPlayerConditionRewardHandler());

            var events = new TurnResolver(effects, Statuses()).Resolve(state, 0);
            var granted = events.OfType<CardBuffGranted>().Single();
            var stored = strikeCard.Statuses.Get(StatusKeys.RewardNullified);

            Assert.AreEqual(StatusLifetimeKind.UntilConsumed, stored.Kind);
            Assert.AreEqual(1, stored.Count);
            Assert.AreEqual(StatusKeys.RewardNullified.Id, granted.BuffId);
        }

        [Test]
        public void Permanent_reward_nullify_downgrades_but_emits_no_consumption()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("quick_cut", "quick_cut", Side.Player, 1,
                new[]
                {
                    EffectData.Conditional(
                        EffectKeys.Damage, 2, new WithinNth(1), successEffectValue: 10)
                });
            var card = new ExecutionCardInstance(def) { InstanceId = 4 };
            card.Statuses.Add(StatusKeys.RewardNullified, StatusLifetime.Permanent);
            state.Zone.Add(card);

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<CardBuffConsumed>());
            Assert.IsTrue(card.Statuses.Has(StatusKeys.RewardNullified));
            Assert.AreEqual(
                ConditionTier.Basic,
                events.OfType<CardResolved>().Single().ConditionTier);
        }

        [Test]
        public void Permanent_intercept_status_emits_no_consumption()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            var card = new ExecutionCardInstance(def) { InstanceId = 8 };
            card.Statuses.Add(TimelineNullifyingBehavior.TestKey, StatusLifetime.Permanent);
            state.Zone.Add(card);
            var statuses = Statuses();
            statuses.Register(new TimelineNullifyingBehavior());

            var events = new TurnResolver(Effects(), statuses).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<CardBuffConsumed>());
            Assert.IsTrue(card.Statuses.Has(TimelineNullifyingBehavior.TestKey));
            Assert.AreEqual(
                CardCancellationReason.StatusIntercepted,
                events.OfType<CardCancelled>().Single().Reason);
        }
```

같은 `CombatLogTests` 클래스의 기존 `Statuses()` 헬퍼 아래에 카드 요격 픽스처를 추가한다.
`StatusTests.cs`의 `NullifyingBehavior`와 같은 CardInstance 스코프·항상 요격 형태이되, 테스트 파일
사이의 private 타입에 의존하지 않도록 이 파일 전용 키를 쓴다.

```csharp
        private sealed class TimelineNullifyingBehavior : StatusBehavior
        {
            public static readonly StatusKey TestKey = new StatusKey("timeline_test_nullify");
            public override StatusKey Key => TestKey;
            public override StatusScope Scope => StatusScope.CardInstance;
            public override bool InterceptCardResolve(StatusContext ctx) => true;
        }
```

- [ ] **Step 2: 실패 확인** — Expected: 컴파일 실패 (CS0246).

- [ ] **Step 3: 이벤트와 상수를 추가한다** (`ResolutionEvent.cs`, `TurnEnded` 위)

```csharp
    /// <summary>카드 귀속 버프 식별자. 상태 키가 아닌 버프(피해 보너스)만 여기 둔다.</summary>
    public static class CardBuffIds
    {
        public const string DamageBonus = "damage_bonus";
    }

    /// <summary>카드 인스턴스에 버프나 카드 귀속 상태가 부여되었다. BuffId는 상태 키
    /// (reward_nullified 등) 또는 CardBuffIds 상수다.</summary>
    public sealed record CardBuffGranted(
        int CardInstanceId, string CardId, string BuffId, int Amount) : ResolutionEvent;

    /// <summary>카드 인스턴스의 버프나 카드 귀속 상태가 소모되었다.</summary>
    public sealed record CardBuffConsumed(
        int CardInstanceId, string CardId, string BuffId, int Amount) : ResolutionEvent;
```

- [ ] **Step 4: 부여를 발행한다**

`GrantNextPlayerDamageCardBonusHandler.Apply` (L20 직후):

```csharp
                    card.AddPendingDamageBonus(ctx.EffectValue);
                    if (ctx.EffectValue != 0)
                    {
                        ctx.ExtraEvents.Add(new Events.CardBuffGranted(
                            card.InstanceId, card.Def.Id,
                            Events.CardBuffIds.DamageBonus, ctx.EffectValue));
                    }
```

`NullifyNextPlayerConditionRewardHandler.Apply` (L21 직후):

```csharp
                    var previous = card.Statuses.Get(StatusKeys.RewardNullified);
                    var changed = previous == null
                        || previous.Kind != StatusLifetimeKind.UntilConsumed
                        || previous.Count != 1
                        || previous.Magnitude != 0;
                    card.Statuses.Add(StatusKeys.RewardNullified, StatusLifetime.UntilConsumed(1));
                    if (changed)
                    {
                        ctx.ExtraEvents.Add(new Events.CardBuffGranted(
                            card.InstanceId, card.Def.Id, StatusKeys.RewardNullified.Id, 1));
                    }
```

`ConsumeStatusHandler`의 보너스 적립 **2곳** (`DamageBonusPerConsumed != 0` 블록):

```csharp
                if (payload.DamageBonusPerConsumed != 0)
                {
                    var bonus = consumed * payload.DamageBonusPerConsumed;
                    ctx.Card.AddPendingDamageBonus(bonus);
                    ctx.ExtraEvents.Add(new Events.CardBuffGranted(
                        ctx.Card.InstanceId, ctx.Card.Def.Id, Events.CardBuffIds.DamageBonus, bonus));
                }
```

- [ ] **Step 5: 소모를 발행한다**

`DamageHandler.Apply`의 보너스 소모 (현재 L35):

```csharp
            var bonus = ctx.Card.ConsumePendingDamageBonus();
            if (bonus != 0)
            {
                ctx.ExtraEvents.Add(new Events.CardBuffConsumed(
                    ctx.Card.InstanceId, ctx.Card.Def.Id, Events.CardBuffIds.DamageBonus, bonus));
            }

            var amount = FoldOutgoing(ctx, ctx.EffectValue + bonus);
```

`TurnResolver.ResolveTier`에 `List<ResolutionEvent> pending` 파라미터를 추가하고 (호출부:
`ResolveTier(effect, card, resolutionContext, pendingDeathEvents)`), 성공 강등 소모 지점:

```csharp
                var nullified = card.Statuses.Get(StatusKeys.RewardNullified);
                if (nullified != null)
                {
                    var countBefore = nullified.Count;
                    card.Statuses.Consume(nullified);
                    var remaining = card.Statuses.Get(StatusKeys.RewardNullified);
                    var consumed = countBefore - (remaining?.Count ?? 0);
                    if (consumed > 0)
                    {
                        pending.Add(new CardBuffConsumed(
                            card.InstanceId, card.Def.Id,
                            StatusKeys.RewardNullified.Id, consumed));
                    }

                    return ConditionTier.Basic;
                }
```

`TurnResolver.IsInterceptedByStatus`에 `List<ResolutionEvent> events` 파라미터를 추가하고 (호출부
L49), 소모 지점:

```csharp
                    var countBefore = status.Count;
                    card.Statuses.Consume(status);
                    var remaining = card.Statuses.Get(status.Key);
                    var consumed = countBefore - (remaining?.Count ?? 0);
                    if (consumed > 0)
                    {
                        events.Add(new CardBuffConsumed(
                            card.InstanceId, card.Def.Id, status.Key.Id, consumed));
                    }

                    return true;
```

(요격 소모 이벤트는 뒤따르는 `CardCancelled(StatusIntercepted)` 앞에 놓인다 — 발생 순서 그대로다.)

- [ ] **Step 6: 전체 테스트 확인** — Expected: **551 passed / 0 failed**. 밀린 인덱스 단언은
  OfType 전환.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Core/Events/ResolutionEvent.cs \
  Assets/Core/Effects/GrantNextPlayerDamageCardBonusHandler.cs \
  Assets/Core/Effects/NullifyNextPlayerConditionRewardHandler.cs \
  Assets/Core/Effects/ConsumeStatusHandler.cs \
  Assets/Core/Effects/DamageHandler.cs \
  Assets/Core/Combat/TurnResolver.cs \
  Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "feat(core): 카드 귀속 버프의 부여와 소모를 타임라인에 남긴다"
```

---

### Task 5: 대형 이동 이벤트

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`, `Assets/Core/Effects/MoveFormationHandler.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Produces: `FormationMoved(string MemberId, Side Side, int FromIndex, int ToIndex)` — 인덱스 0이 맨 앞

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`FormationTargetingIntegrationTests.Player_move_changes_only_party_order`의 파티 구성과
`Later_frontmost_attack_uses_formation_after_earlier_move`의 `TurnResolver` 경로를 합친다.

```csharp
        [Test]
        public void Formation_move_emits_formation_moved_with_from_and_to()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var front = new PartyMember("member_a", "A", 10);
            var back = new PartyMember("member_b", "B", 10);
            state.Party.Add(front);
            state.Party.Add(back);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("advance", "advance", Side.Player, 1,
                new[] { new EffectData(EffectKeys.MoveFormation, -1) });
            state.Zone.Add(new ExecutionCardInstance(def)
                { OwnerId = back.Id, InstanceId = 3 });
            var effects = new EffectRegistry();
            effects.Register(new MoveFormationHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);
            var moved = events.OfType<FormationMoved>().Single();

            Assert.AreEqual((back.Id, Side.Player, 1, 0),
                (moved.MemberId, moved.Side, moved.FromIndex, moved.ToIndex));
            Assert.AreSame(back, state.Party[0]);
        }

        [Test]
        public void A_clamped_in_place_formation_move_emits_nothing()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var front = new PartyMember("member_a", "A", 10);
            state.Party.Add(front);
            state.Party.Add(new PartyMember("member_b", "B", 10));
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("advance", "advance", Side.Player, 1,
                new[] { new EffectData(EffectKeys.MoveFormation, -1) });
            state.Zone.Add(new ExecutionCardInstance(def)
                { OwnerId = front.Id, InstanceId = 3 });
            var effects = new EffectRegistry();
            effects.Register(new MoveFormationHandler());

            var events = new TurnResolver(effects).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<FormationMoved>());
            Assert.AreSame(front, state.Party[0]);
        }
```

- [ ] **Step 2: 실패 확인** — Expected: 컴파일 실패 (CS0246).

- [ ] **Step 3: 이벤트를 추가한다** (`ResolutionEvent.cs`, `TurnEnded` 위)

```csharp
    /// <summary>대형 내 위치가 이동했다. Side는 어느 진영의 대형인지, 인덱스 0이 맨 앞이다.
    /// 클램프로 제자리에 남은 이동은 남기지 않는다.</summary>
    public sealed record FormationMoved(
        string MemberId, Side Side, int FromIndex, int ToIndex) : ResolutionEvent;
```

- [ ] **Step 4: 발행한다**

`MoveFormationHandler.cs`의 이동 4곳(`MoveSnapshotPartyOwner`·`MovePartyOwner`는 `Side.Player`,
`MoveSnapshotEnemyOwner`·`MoveEnemyOwner`는 `Side.Enemy`) 각각 `Insert` 직후:

```csharp
            if (destinationIndex != currentIndex)
            {
                ctx.ExtraEvents.Add(new Events.FormationMoved(
                    owner.Id, Side.Player, currentIndex, destinationIndex));
            }
```

- [ ] **Step 5: 전체 테스트 확인** — Expected: **553 passed / 0 failed**.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Events/ResolutionEvent.cs \
  Assets/Core/Effects/MoveFormationHandler.cs \
  Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "feat(core): 대형 이동을 타임라인에 남긴다"
```

---

### Task 6: 포매터 — 이벤트당 로그 1건 + 신규 case 6종

**Files:**
- Modify: `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Consumes: Task 1–5의 모든 신규 이벤트
- Produces: `TimelineTextFormatter.FormatEvent(ResolutionEvent, KoreanDescriptionCatalog) -> string`
  (후행 개행 없는 로그 1건; 기존 `Format`은 유지되며 내부적으로 같은 렌더링을 쓴다)

- [ ] **Step 1: 실패하는 테스트를 작성한다**

```csharp
        [Test]
        public void Format_event_renders_one_entry_without_trailing_newline()
        {
            var text = TimelineTextFormatter.FormatEvent(new TurnStarted(0), Korean);
            Assert.AreEqual("== 1턴 시작 ==", text);
        }

        [Test]
        public void Format_event_spells_out_hp_change_with_its_source()
        {
            var cardHit = TimelineTextFormatter.FormatEvent(
                new HpChanged("goblin", 10, 6, HpChangeSource.CardDamage, "jab"), Korean);
            StringAssert.Contains("goblin", cardHit);
            StringAssert.Contains("10", cardHit);
            StringAssert.Contains("6", cardHit);
            StringAssert.Contains("jab", cardHit);

            var tick = TimelineTextFormatter.FormatEvent(
                new HpChanged("goblin", 10, 7, HpChangeSource.StatusTick, "poison"), Korean);
            StringAssert.Contains(Korean.Statuses.Resolve(StatusKeys.Poison), tick);
        }

        [Test]
        public void Format_event_covers_every_new_event_without_falling_to_default()
        {
            var samples = new ResolutionEvent[]
            {
                new FateEnergyGained("distill", 1),
                new StatusConsumed("goblin", "poison", 2),
                new CardBuffGranted(2, "strike", CardBuffIds.DamageBonus, 2),
                new CardBuffConsumed(2, "strike", StatusKeys.RewardNullified.Id, 1),
                new FormationMoved("member_a", Side.Player, 1, 0)
            };
            foreach (var evt in samples)
            {
                StringAssert.DoesNotContain("[미처리 이벤트]",
                    TimelineTextFormatter.FormatEvent(evt, Korean));
            }
        }

        [Test]
        public void Cancelled_card_entry_includes_pre_cancel_damage()
        {
            var text = TimelineTextFormatter.FormatEvent(
                new CardCancelled(1, "double_strike", "member_a", CardCancellationReason.NoValidTarget)
                {
                    DamageDealt = 5
                }, Korean);
            StringAssert.Contains("5", text);
        }
```

(`Korean.Statuses.Resolve`의 반환이 표시명이 아니면 — 실측 후 — 해당 단언만 실제 API에 맞춘다.)

- [ ] **Step 2: 실패 확인** — Expected: 컴파일 실패 — `FormatEvent` 없음.

- [ ] **Step 3: 구현한다**

`TimelineTextFormatter.cs`에 추가·수정한다. `Format`은 그대로 두고 `FormatEvent`를 더한다:

```csharp
        /// <summary>이벤트 1건을 로그 1건으로 바꾼다. Unity는 이벤트마다 이것을 호출해
        /// Debug.Log 1건씩 남긴다 (로그 1건 = 이벤트 1건).</summary>
        public static string FormatEvent(ResolutionEvent evt, KoreanDescriptionCatalog catalog)
        {
            if (evt == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            AppendEvent(sb, evt, catalog);
            return sb.ToString().TrimEnd('\r', '\n');
        }
```

`AppendEvent`의 `default` 위에 case 6개를 추가한다:

```csharp
                case HpChanged e:
                    sb.Append("  ").Append(e.HolderId).Append(" HP ")
                      .Append(e.Before).Append(" → ").Append(e.After)
                      .Append(" (").Append(HpSourceName(catalog, e)).AppendLine(")");
                    break;
                case FateEnergyGained e:
                    sb.Append("  다음 턴 운명력 +").Append(e.Amount)
                      .Append(" (").Append(e.SourceCardId).AppendLine(")");
                    break;
                case StatusConsumed e:
                    sb.Append("  ").Append(e.HolderId).Append('의')
                      .Append(StatusName(catalog, e.StatusId))
                      .Append(' ').Append(e.Amount).AppendLine(" 소비");
                    break;
                case CardBuffGranted e:
                    sb.Append("  카드 ").Append(e.CardId).Append("(#").Append(e.CardInstanceId)
                      .Append(")에 ").Append(BuffName(catalog, e.BuffId))
                      .Append(" +").Append(e.Amount).AppendLine(" 부여");
                    break;
                case CardBuffConsumed e:
                    sb.Append("  카드 ").Append(e.CardId).Append("(#").Append(e.CardInstanceId)
                      .Append(")의 ").Append(BuffName(catalog, e.BuffId))
                      .Append(' ').Append(e.Amount).AppendLine(" 소모");
                    break;
                case FormationMoved e:
                    sb.Append("  ").Append(e.MemberId).Append(" 대형 이동: ")
                      .Append(e.FromIndex + 1).Append("열 → ")
                      .Append(e.ToIndex + 1).AppendLine("열");
                    break;
```

`CardCancelled` case를 확장한다 (기존 case 교체):

```csharp
                case CardCancelled e:
                    sb.Append("  ").Append(e.CardId).Append(" 취소 (").Append(e.Reason).Append(')');
                    if (e.DamageDealt > 0)
                    {
                        sb.Append(" — 취소 전 피해 ").Append(e.DamageDealt);
                    }

                    sb.AppendLine();
                    foreach (var step in e.DamageSteps)
                    {
                        sb.Append("      ").Append(step.HolderId).Append('의')
                          .Append(StatusName(catalog, step.StatusId))
                          .Append(": ").Append(step.Before).Append(" → ").AppendLine(step.After.ToString());
                    }

                    break;
```

헬퍼 2개를 `StatusName` 옆에 추가한다:

```csharp
        private static string HpSourceName(KoreanDescriptionCatalog catalog, HpChanged e)
            => e.Source == HpChangeSource.StatusTick ? StatusName(catalog, e.SourceId) : e.SourceId;

        /// <summary>버프 이름: 상태 키면 설명 레지스트리, 아니면 카드 버프 상수의 고정 문구.</summary>
        private static string BuffName(KoreanDescriptionCatalog catalog, string buffId)
            => buffId == CardBuffIds.DamageBonus ? "피해 보너스" : StatusName(catalog, buffId);
```

- [ ] **Step 4: 전체 테스트 확인** — Expected: **557 passed / 0 failed**.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs \
  Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "feat(core): 포매터를 이벤트당 로그 1건으로 바꾸고 신규 이벤트를 다룬다"
```

---

### Task 7: Unity 배선 — 이벤트당 Debug.Log 1건

**Files:**
- Modify: `Assets/Unity/Scripts/Battle/BattleScreenController.cs`

- [ ] **Step 1: 덤프를 루프로 바꾼다**

`OnTurnButton`의 통짜 덤프 (현재 L299):

```csharp
                _session.ResolveTurn();
                foreach (var evt in _session.LastTimeline)
                {
                    Debug.Log(TimelineTextFormatter.FormatEvent(evt, _korean));
                }
```

- [ ] **Step 2: 헤드리스 회귀 + Unity 컴파일 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Unity 배치 컴파일 (`ProjectSettings/ProjectVersion.txt`의 **6000.5.2f1**, 라이선싱 좀비 대응은
규칙 25, 다른 워크트리 프로세스 금지는 규칙 26):

```bash
'/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity' -batchmode -quit -projectPath "$PWD" -logFile /private/tmp/timeline-expansion-unity.log
```

Expected: 헤드리스 **557 passed / 0 failed**, Unity 로그의 `error CS` 0건. 배치가 남긴
부산물은 스테이징하지 않는다.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/Scripts/Battle/BattleScreenController.cs
git commit -m "feat(ui): 타임라인을 이벤트당 로그 1건으로 콘솔에 남긴다"
```

---

### Task 8: 완료한 계획을 보관하고 색인을 갱신한다

**Files:**
- Move: `docs/superpowers/plans/2026-08-28-combat-timeline-event-expansion.md` →
  `docs/superpowers/archive/plans/2026-08-28-combat-timeline-event-expansion.md`
- Modify: `docs/superpowers/README.md`

- [ ] **Step 1: 계획 상태를 완료로 바꾸고 보관한다**

먼저 `date +%F`로 완료일을 얻는다. 이 문서 머리말의 상태를
`completed — 구현·검증 완료 (` + 명령 출력값 + `)` 형태로 바꾸고, 설명용 문구를 남기지
않는다. 그다음 이동한다.

```bash
git mv docs/superpowers/plans/2026-08-28-combat-timeline-event-expansion.md \
  docs/superpowers/archive/plans/2026-08-28-combat-timeline-event-expansion.md
```

- [ ] **Step 2: README 색인의 행을 완료 상태로 바꾼다**

활성 계획 표의 링크를 `archive/plans/2026-08-28-combat-timeline-event-expansion.md`로 바꾸고,
상태를 `완료·머지·보관 (` + Step 1의 날짜 + `)` 형태로 바꾼다. 범위 칸은 그대로 둔다.

- [ ] **Step 3: 최종 회귀와 변경 범위를 확인한다**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
git status --short
```

Expected: **557 passed / 0 failed**. 상태에는 이 Task의 문서 이동과 README 수정만 추가로
나타나며, Unity 배치가 만든 폰트 아틀라스·로그·캐시는 없어야 한다.

- [ ] **Step 4: 커밋**

```bash
git add docs/superpowers/README.md \
  docs/superpowers/archive/plans/2026-08-28-combat-timeline-event-expansion.md
git commit -m "docs: 완료한 타임라인 이벤트 확장 계획을 보관한다"
```

## 범위 밖

| 항목 | 이유 |
|---|---|
| 개입 카드·운명력 충전/지출 로그 | 턴 해석 밖(세션·개입 해석기). 타임라인 정의 확장이 필요해 별도 계획 |
| 트리거(발동 조건) 구독 메커니즘 | 기능이 올 때 발행 순간 훅과 함께 설계 |
| 인게임 로그 패널 | Console 한정 (사용자 결정 2026-08-28). 포매터는 그대로 재사용 가능 |
| 이벤트 발생 순서 정밀화 | `ExtraEvents`의 카드 단위 배치 유지. 필요해질 때 별도 작업 |
