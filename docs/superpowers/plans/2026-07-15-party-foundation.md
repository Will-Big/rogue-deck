# 파티 기반 전투 확장 (2인 파티 수직 슬라이스) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task. Every task follows red-green-refactor and ends with the full headless suite.

**Goal:** [파티 기반 전투 설계](../specs/2026-07-15-party-foundation-design.md)의 전투 장면 우선 범위를 구현한다. 첫 슬라이스는 2인 파티이며, 1~3인 도메인 모델, 개인 HP·상태, 독립된 양 진영 대형, 합성 덱과 카드 소유자, 위치/무작위/직접 대상, 치명 버팀, 사망 후 카드 제거·취소, 인원 비례 드로우, 전투 UI를 포함한다.

**Architecture:** `CombatState.Party`와 `CombatState.Enemies`는 서로 독립된 대형이며 양쪽 모두 index 0이 자기 진영 최전방이다. 유닛 대상 버프·디버프는 각 대상 유닛의 `StatusBag`에 저장하고, 기존 카드 인스턴스 한정 상태는 카드에 남긴다. 직접 대상 카드는 플레이 시 대상을 검증하고, 배치 후 실행 전 대상이 사라지면 폴백 없이 취소한다. 위치 셀렉터는 실행 시점 대형을 읽는다. 취소는 상태 효과가 아니라 `ExecutionCardInstance.CancellationReason`에 영속 기록하며, 취소된 카드는 “직전에 실행한 카드” 판정에서 제외한다. `OwnedCard.OwnerId == null`은 파티 소유, 값이 있으면 캐릭터 소유를 뜻한다.

**Tech Stack:** 순수 C# 코어 + NUnit 헤드리스, Unity 6 uGUI/TMP, ScriptableObject 카드 저작 파이프라인.

**스펙보다 우선하는 최신 피드백:** 설계 스펙 §2의 “운명 카드도 항상 캐릭터 귀속”은 미정으로 되돌린다. §4와 §9의 “기존 무효화 상태 재사용”은 영속 취소 사유로 교체한다. §6의 동료 제품 카드 4~6장 확정은 중립 검증 fixture 6장으로 대체한다. 스펙 본문을 별도로 갱신하기 전까지 구현자는 이 계획의 규칙을 우선한다.

## 확정 규칙

- 플레이어와 적은 별도 대형 리스트를 사용한다. 화면에서는 플레이어 index 0이 오른쪽 끝, 적 index 0이 왼쪽 끝이어서 양쪽 전열이 중앙을 마주본다.
- `FrontMost`, `SecondFromFront`, `BackMost`, `Random`은 유닛 ID를 미리 고정하지 않고 실행 시점의 생존 대형에서 대상을 찾는다.
- 직접 지정 대상이 플레이 시점에 없거나 죽었으면 카드 사용을 거부한다. 손패, 운명력, 미래 영역은 변하지 않는다.
- 직접 지정 대상이 배치 후 실행 전에 사라지면 `NoValidTarget`으로 취소한다. 카드 소유자나 전열로 자동 적용하지 않는다.
- 위치 셀렉터가 실행 시 유효 대상을 찾지 못해도 `NoValidTarget`으로 취소한다.
- 모든 유닛 대상 버프·디버프는 개인 상태다. 전체 대상 카드는 동일한 상태를 공유하지 않고 각 대상의 `StatusBag`에 별도 인스턴스를 적용한다. 실행 카드 자체에 붙는 기존 상태는 `ExecutionCardInstance.Statuses`에 남는다.
- 사망으로 취소된 카드와 상태에 의해 차단된 카드는 실행된 카드가 아니다. 연계 조건은 이들을 건너뛰고 실제로 효과 해석을 마친 직전 카드를 본다.
- 무작위 대상은 실행 시점에 시드 RNG로 정한다. 결과를 사전 고정하거나 피해 예방 가능성을 보장하지 않는다.
- 파티원 UI에는 이름, HP, 개인 버프·디버프를 표시한다. 전열/후열 문구와 치명 버팀 잔여 횟수는 표시하지 않는다.
- 카드 소유자 표시는 필요하다. 첫 슬라이스는 카드 아트 좌하단의 임시 소유자 칩을 사용하며 최종 위치·아트는 별도 논의한다.
- 제품용 신규 능력·캐릭터 콘셉트는 이 계획에서 확정하지 않는다. 테스트와 장면 검증 데이터는 `[검증]` 접두사의 중립 fixture만 사용한다.

## 전역 제약

- `Assets/Core/**`는 `UnityEngine`을 참조하지 않는다. C# 9 문법만 사용한다.
- 모든 무작위는 `CombatState.Rng` 또는 기존 시드 RNG를 사용한다. `new Random()`, `DateTime`, `Guid.NewGuid()`로 결과를 만들지 않는다.
- 헤드리스 명령은 항상 다음과 같다.

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0
```

- 기존 결정론 테스트가 깨지면 이 계획이 명시한 의미 변경인지 확인한다. 의미 변경이 아니면 구현을 고치고 기존 기대값을 수정하지 않는다.
- 맵, 노드, 영입, 전투 후 카드 인계, 사망 후 회복 기회, 운명 카드의 최종 귀속 정책은 범위 밖이다. 단, 도메인은 캐릭터 소유와 파티 소유를 모두 표현한다.
- 각 태스크는 해당 테스트를 먼저 실패시킨 뒤 최소 구현을 하고 전체 헤드리스 테스트를 통과시킨다.

---

### Task 1: 파티원, 독립 진영 대형, 개인 상태

**Files**

- Create: `Assets/Core/Combat/PartyMember.cs`
- Modify: `Assets/Core/Combat/CombatState.cs`
- Modify: `Assets/Core/Combat/TurnResolver.cs`
- Test: `Assets/Core/Tests/EditMode/PartyMemberTests.cs`

**Interfaces**

```csharp
public enum DamageOutcome { Damaged, DeathsDoor, Died }

public sealed class PartyMember : IStatusHolder
{
    public string Id { get; }
    public string Name { get; }
    public int MaxHp { get; set; }
    public int Hp { get; set; }
    public int SurviveCharges { get; set; }
    public bool IsAlive { get; }
    public StatusBag Statuses { get; }
    public DamageOutcome TakeDamage(int amount);
}
```

`CombatState.Party`와 `CombatState.Enemies`는 각각 자기 진영 기준 index 0이 최전방이다. 기존 `PlayerHp`와 `PlayerStatuses`는 `LegacyPlayerId == "player"`인 단일 파티원에게 위임하는 레거시 심으로 유지한다. 파티 모드 코드가 이 심을 사용해서는 안 된다.

**실패 테스트**

- `Lethal_damage_is_survived_once_at_one_hp`: 버팀 1회가 있으면 첫 치명 피해 후 HP 1, 잔여 0이다.
- `Second_lethal_damage_kills`: 다음 치명 피해는 `Died`이고 `IsAlive == false`다.
- `Outcome_is_ongoing_while_any_party_member_lives`: 한 명이 죽어도 생존자가 있으면 전투는 계속된다.
- `Status_bags_are_isolated_between_party_members`: A에게 방어·가속을 넣어도 B의 수치는 0이다.
- `End_of_turn_clears_this_turn_statuses_for_every_party_member`: A의 방어와 B의 둔화가 같은 턴 종료에 각각 정리된다.
- `Legacy_player_shim_keeps_existing_single_player_tests`: 기존 단일 플레이어 읽기·쓰기가 첫 레거시 파티원과 동일하다.

**구현 순서**

1. `PartyMember`와 `DamageOutcome`을 추가한다.
2. `CombatState`에 `Party`, 독립된 `Enemies`, 시드 기반 `Rng`를 둔다.
3. `TurnResolver.EndOfTurnMaintenance`가 모든 파티원과 모든 적의 `Statuses.EndOfTurn()`을 호출하게 한다.
4. `ComputeOutcome`은 생존 파티원이 없으면 패배, 생존 적이 없으면 승리로 판정한다.
5. 전체 헤드리스 테스트를 실행한다.

---

### Task 2: 타겟 규칙과 취소 모델

**Files**

- Create: `Assets/Core/Cards/TargetSelector.cs`
- Create: `Assets/Core/Combat/CardCancellationReason.cs`
- Create: `Assets/Core/Combat/PartyTargeting.cs`
- Create: `Assets/Core/Combat/PartyTargetRules.cs`
- Modify: `Assets/Core/Cards/CardDefinition.cs`
- Modify: `Assets/Core/Combat/ExecutionCardInstance.cs`
- Modify: `Assets/Core/Effects/IEffectHandler.cs` — EffectContext 타입
- Modify: `Assets/Core/Effects/DamageHandler.cs`
- Modify: `Assets/Core/Effects/ApplyStatusHandler.cs`
- Test: `Assets/Core/Tests/EditMode/PartyTargetingTests.cs`

**Interfaces**

```csharp
public enum TargetSelector { FrontMost, SecondFromFront, BackMost, Random }

public enum CardCancellationReason
{
    NoValidTarget,
    OwnerDied,
    StatusIntercepted
}

public static class PartyTargeting
{
    public static PartyMember Select(CombatState state, TargetSelector selector);
    public static PartyMember ById(CombatState state, string memberId);
    public static PartyMember LivingById(CombatState state, string memberId);
}

public static class PartyTargetRules
{
    public static bool RequiresExplicitAllyTarget(CardDefinition definition);
    public static bool IsValidExplicitAllyTarget(CombatState state, string targetId);
}
```

첫 슬라이스에서 `RequiresExplicitAllyTarget`은 `ApplyStatus` 효과 중 `StatusApplyTarget.PartyMember`가 하나라도 있을 때만 true다. `Self`, `AllPartyMembers`, 위치 셀렉터, 무작위 셀렉터는 직접 클릭을 요구하지 않는다.

- `EffectData.TargetSelector`는 nullable이다. 적의 플레이어 공격에서 null은 기존 호환을 위해 `FrontMost`다.
- `StatusApplyTarget`에 `PartyMember`와 `AllPartyMembers`를 추가한다.
- `ExecutionCardInstance`에 `public int InstanceId { get; set; } = -1`, `OwnerId`, `TargetId`, nullable `CancellationReason`을 둔다. 코어 단위 테스트는 서로 다른 ID를 직접 주입할 수 있고, 세션은 Task 4부터 증가 카운터로 실제 ID를 배정한다.
- `EffectContext.Cancel(CardCancellationReason reason)`은 첫 취소 사유만 기록한다. 핸들러는 취소 후 상태나 HP를 변경하지 않는다.

**엄격한 대상 해석**

- 플레이어 카드의 `Self`: `OwnerId`와 일치하는 생존 파티원만 사용한다. 다인 파티에서 소유자가 없으면 전열로 폴백하지 않는다. 단일 `LegacyPlayerId` 전투만 레거시 첫 파티원을 허용한다.
- 적 카드의 `Self`: `OwnerId`가 있으면 일치하는 생존 적만 사용한다. `OwnerId`가 없고 전투에 적이 정확히 한 명이면 기존 러너 호환을 위해 `Enemies[0]`을 사용한다. `OwnerId`가 없는데 적이 둘 이상이면 전열 폴백 없이 `NoValidTarget`으로 취소한다. 이 폴백은 Task 2 시점의 `ScenarioRunner`, `MultiTurnRunner`, `PlaytestSession`, `MultiTurnPlaytestSession`, `DeckCombatSession` 회귀를 보호한다.
- 파티 소유 실행 카드(`OwnerId == null`)가 `Self` 효과를 가지면 의미가 정의되지 않은 조합이므로 `NoValidTarget`으로 취소한다. 첫 슬라이스의 파티 소유 카드는 개입 카드로만 만든다.
- `PartyMember`: `TargetId`와 일치하는 생존 파티원만 사용한다. 없거나 사망이면 `NoValidTarget`이다.
- `AllPartyMembers`: 실행 시점의 생존자 스냅샷을 순회하고 각자의 `StatusBag`에 별도 적용한다. 생존자가 0명이면 `NoValidTarget`이다.
- 적 공격의 위치 셀렉터: 생존 파티 대형에서 실행 시 선택한다. `SecondFromFront`는 생존자 2명 미만이면 null이다.
- 플레이어의 적 직접 대상: 기존 적 ID 규칙을 유지하되 실행 전에 대상이 사라지면 `NoValidTarget`이다.
- `DamageHandler`는 선택된 파티원의 `Statuses`로 방어·취약 등 피격 수정을 계산한다. A의 상태를 B의 피해 계산에 사용하지 않는다.
- `ApplyStatusHandler`는 `PartyMember`와 `AllPartyMembers`에서 대상별 bag을 직접 선택한다. `OwnerBag`/`PartyMemberBag` 어느 쪽도 전열 폴백을 두지 않는다.

**실패 테스트**

- `Second_from_front_selects_the_second_living_member`와 `Second_from_front_returns_null_with_one_living_member`.
- `Dead_explicit_ally_does_not_fall_back_to_owner_or_front`.
- `Missing_owner_in_multi_party_self_effect_does_not_fall_back_to_front`.
- `Enemy_self_without_owner_uses_the_only_enemy_for_legacy_runners`.
- `Enemy_self_without_owner_cancels_when_multiple_enemies_exist`.
- `Party_owned_execution_self_effect_cancels_as_no_valid_target`.
- `All_party_status_creates_independent_instances`: 전체 방어 후 A의 방어를 소비해도 B의 방어는 남는다.
- `Block_and_vulnerable_on_a_do_not_modify_damage_to_b`.
- `Random_target_is_deterministic_for_equal_seed`.
- `Position_selector_ignores_dead_members_without_reindexing_the_other_side`: 플레이어 대형 변화가 적 대형 index에 영향을 주지 않는다.

---

### Task 3: 실행 카드 취소와 “직전에 실행한 카드” 연계

**Files**

- Modify: `Assets/Core/Events/ResolutionEvent.cs`
- Modify: `Assets/Core/Conditions/ResolutionContext.cs`
- Modify: `Assets/Core/Combat/TurnResolver.cs`
- Modify: `Assets/Core/Conditions/ConditionEvaluator.cs`
- Modify: `Assets/Core/Conditions/Condition.cs`
- Modify: `Assets/Core/Simulation/Authoring/CardSpecMapper.cs`
- Modify: `Assets/Core/Simulation/StarterDeck.cs`
- Modify: `Assets/Core/Simulation/SampleScenarios.cs`
- Modify: `Assets/Core/Simulation/SampleMultiTurnScenarios.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionVocabulary.cs`
- Test: `Assets/Core/Tests/EditMode/CardCancellationTests.cs`
- Test: `Assets/Core/Tests/EditMode/PreviousExecutedCardConditionTests.cs`
- Test: `Assets/Core/Tests/EditMode/ConditionEvaluatorTests.cs`
- Test: `Assets/Core/Tests/EditMode/StatusTests.cs`
- Test: `Assets/Core/Tests/EditMode/CounterStanceTests.cs`
- Test: `Assets/Core/Tests/EditMode/ChainSlashTests.cs`
- Test: `Assets/Core/Tests/EditMode/DescriptionComposerTests.cs`

**Events and condition**

```csharp
public sealed record CardCancelled(
    int InstanceId,
    string CardId,
    string OwnerId,
    CardCancellationReason Reason) : ResolutionEvent;

public sealed record PartyMemberDied(string MemberId) : ResolutionEvent;
public sealed record DeathsDoorSurvived(string MemberId) : ResolutionEvent;
```

기존 `CardResolved`에도 `InstanceId`와 `OwnerId`를 추가한다. 기존 테스트용 생성자 호출이 많으면 기존 인자를 받는 호환 생성자를 두되, 실제 `TurnResolver`는 항상 카드 인스턴스의 식별자를 채운다. Task 3 단위 테스트는 Task 2의 settable `InstanceId`를 직접 지정하고, 실제 세션은 Task 4부터 증가 카운터로 생성한다. GUID는 쓰지 않는다.

`PreviousExecutedCardIs(Side Side, CardType? Type = null)`를 추가한다. 기존 `AdjacentDirection.Previous` 조건을 직접 생성하는 코드는 이 새 조건으로 이전한다. Task 3에서는 `ConditionKind.PrevIsPlayerAttack`/`PrevIsEnemyAttack` 이름을 유지하되 `CardSpecMapper`의 결과만 `PreviousExecutedCardIs`로 바꾼다. enum 이름과 체크인된 생성 파일의 기계적 rename은 Task 7에서 같은 태스크로 처리한다. `AdjacentDirection.Next` 조건은 미래 슬롯을 보는 별도 규칙으로 유지한다.

`SameTarget`은 취소된 플레이어 카드를 건너뛴 마지막 실행 플레이어 카드를 사용한다. `ResolutionContext`는 `ExecutedCards`, `LastExecutedCard`, `LastExecutedPlayerCard`를 유지하며 다음 경우에만 갱신한다.

- 모든 효과 해석을 마치고 `CardResolved`를 방출한 카드: 갱신한다.
- `OwnerDied`, `NoValidTarget`, `StatusIntercepted`로 `CardCancelled`를 방출한 카드: 갱신하지 않는다.

**다른 순서 조건의 취소 카드 취급**

- `NoPrecedingCardOfSide`는 현재 카드보다 먼저 `CardResolved`된 카드만 센다. 먼저 배치됐지만 취소된 카드는 제외한다.
- `NoFollowingCardOfSide`는 미래 결과를 소급할 수 없으므로 기존처럼 frozen resolution order의 뒤쪽 슬롯을 센다. 뒤의 카드가 나중에 취소되더라도 현재 카드의 판정을 되돌리지 않는다.
- `AdjacentDirection.Next`도 frozen resolution order의 바로 다음 슬롯을 본다. 다음 카드의 향후 취소 여부는 현재 판정에 반영하지 않는다.

**카드 해석과 사망 스윕 순서**

1. 각 효과 직전에 파티원별 `IsAlive`와 `SurviveCharges` 스냅샷을 잡고 효과를 적용한다.
2. 효과 직후 `SurviveCharges`가 감소했고 파티원이 살아 있으면 `DeathsDoorSurvived`를 대기 이벤트에 넣는다. HP가 1이라는 사실만으로 버팀을 판정하지 않는다.
3. 직전 스냅샷에서 살아 있었고 효과 후 죽었으면 `PartyMemberDied`를 대기 이벤트에 넣는다. 효과가 여러 개라면 스냅샷을 효과마다 갱신해 한 카드 안의 버팀 후 사망도 순서대로 포착한다.
4. 현재 카드가 취소되지 않았다면 `CardResolved`를 방출하고 `LastExecutedCard`를 갱신한 뒤, 대기 중인 생존/사망 이벤트를 발생 순서대로 방출한다. 따라서 사망을 일으킨 `CardResolved` 바로 뒤에 사망 이벤트가 온다.
5. 새로 죽은 파티원 소유의 아직 실행하지 않은 카드에 `OwnerDied`를 기록한다.
6. 해석 시작 전에 이미 취소 사유가 있거나 효과 도중 `NoValidTarget`이 기록된 카드는 `CardResolved` 없이 `CardCancelled`만 한 번 방출한다. 취소 뒤의 나머지 효과도 실행하지 않는다.

카드 취소 전용 상태 키나 상태 행동은 만들지 않는다. 기절 등 기존 실행 차단도 `StatusIntercepted` 취소 사유를 사용한다.

**실패 테스트**

- `Owner_death_marks_only_pending_cards_owned_by_that_member`.
- `Cancelled_card_emits_no_card_resolved_event`.
- `Card_cancelled_event_contains_instance_owner_and_reason`.
- `Previous_executed_condition_skips_owner_died_card`: A 실행, B 사망 취소, C 조건 순서에서 C는 A를 직전 실행 카드로 본다.
- `Previous_executed_condition_skips_no_target_and_status_intercepted_cards`.
- `Next_adjacent_condition_keeps_existing_frozen_order_semantics`.
- `Same_target_uses_last_executed_player_card`.
- `No_preceding_ignores_cancelled_cards_but_no_following_keeps_frozen_future_slots`.
- `Hp_reaching_exactly_one_without_spending_a_charge_emits_no_deaths_door_event`.
- `Charge_decrease_emits_deaths_door_even_when_hp_was_already_one_before_a_later_effect`.
- `Duplicate_card_ids_are_distinguished_by_instance_id`.

**기존 회귀 수정 허용 범위**

- `CounterStanceTests`, `ChainSlashTests`, `ConditionEvaluatorTests`, `DescriptionComposerTests`, `StarterDeck.cs`, `SampleScenarios.cs`, `SampleMultiTurnScenarios.cs`: `AdjacentDirection.Previous`를 `PreviousExecutedCardIs`로 바꾸고 새 문구를 반영하는 기계적 수정만 허용한다. 취소 카드가 사이에 없는 기존 시나리오의 피해량·조건 tier 기대값은 바꾸지 않는다.
- `StatusTests.Stun_until_consumed_nullifies_one_resolution_then_is_gone`: 기존 `CardResolved(damage 0)` 기대를 `CardCancelled(StatusIntercepted)`로 바꾸는 것만 허용한다. 적 HP와 기절 소비 기대값은 유지한다.
- `CardSpecMapper`는 기존 `ConditionKind.PrevIsPlayerAttack`/`PrevIsEnemyAttack`을 새 조건 객체로 매핑하되 enum 이름은 Task 7까지 유지한다.
- `GeneratedCardsTests`, `StarterDeckSpecEquivalenceTests`, `DesignInvariantTests`, `ScenarioRunnerTests`, `MultiTurnRunnerTests`, `MultiTurnPlaytestSessionTests`, `PlaytestSessionTests`의 숫자·tier·카드 수 기대값은 수정 금지다. 실패하면 기대값을 고치지 말고 타임라인을 비교해 구현 회귀를 수정한다.

---

### Task 4: 캐릭터 소유와 파티 소유 카드

**Files**

- Create: `Assets/Core/Cards/OwnedCard.cs`
- Modify: `Assets/Core/Combat/Deck.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`
- Test: `Assets/Core/Tests/EditMode/OwnedCardDeckTests.cs`

**Interface**

```csharp
public sealed class OwnedCard
{
    public CardDefinition Def { get; }
    public string OwnerId { get; }
    public bool IsPartyOwned => OwnerId == null;
    public OwnedCard(CardDefinition def, string ownerId);
}
```

`Deck`의 draw/discard/hand 원소를 모두 `OwnedCard`로 바꾼다. `Deck(IEnumerable<CardDefinition>, seed)` 레거시 생성자는 `LegacyPlayerId`로 감싼다. `RemoveOwnedBy(ownerId)`는 세 pile에서 해당 캐릭터 소유 카드만 제거하고 `OwnerId == null`인 파티 소유 카드는 남긴다.

`DeckCombatSession`에 전투 동안 증가하는 `_nextInstanceId`를 두고 플레이어 배치 카드와 `BeginTurn`에서 생성하는 적 카드 모두에 `InstanceId = _nextInstanceId++`를 기록한다. 리셋은 새 세션 생성 시에만 한다. Task 3 코어 테스트는 세션 없이 settable `InstanceId`를 직접 지정한다.

**실패 테스트**

- `Remove_owned_by_removes_matching_cards_from_all_three_piles`.
- `Remove_owned_by_keeps_other_character_cards`.
- `Remove_owned_by_keeps_party_owned_cards`.
- `Placement_copies_owner_and_assigns_unique_instance_id`.
- `Legacy_definition_deck_assigns_legacy_player_owner`.

운명 카드의 최종 귀속 정책은 결정하지 않는다. 이 태스크는 두 소유 형태를 손실 없이 표현하는 것까지만 책임진다.

---

### Task 5: 파티 세션, 직접 대상 사용 거부, 사망 후 덱 제거

**Files**

- Create: `Assets/Core/Simulation/PartyMemberLoadout.cs`
- Create: `Assets/Core/Simulation/PartyTuning.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`
- Test: `Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs`

**Interfaces**

```csharp
public sealed class PartyMemberLoadout
{
    public string Id { get; }
    public string Name { get; }
    public int MaxHp { get; }
    public IReadOnlyList<CardDefinition> Cards { get; }
}

public sealed class PartyTuning
{
    public int MinPartySize { get; init; } = 1;
    public int MaxPartySize { get; init; } = 3;
    public int DefaultMemberMaxHp { get; init; }
    public int SurviveChargesPerCombat { get; init; }
    public IReadOnlyDictionary<int, int> DrawByLivingCount { get; init; }
    public int DrawFor(int livingCount);
    public static PartyTuning Prototype { get; }
}

public bool PlayExecutionCard(int handIndex, string targetId = null);
```

**생성자 검증**

- 파티 수는 1~3명이다.
- ID는 null/빈 문자열이 아니고 서로 중복되지 않는다.
- `MaxHp > 0`이고 카드 목록과 카드 정의는 null이 아니다.
- `DefaultMemberMaxHp > 0`, `SurviveChargesPerCombat >= 0`이어야 한다.
- `DrawByLivingCount`는 null이 아니며 현재 파티 크기까지의 모든 키 `1..party.Count`를 가지고 각 값이 1 이상이어야 한다. 누락 키를 런타임 `KeyNotFoundException`으로 넘기지 않는다.
- 위 조건 위반은 `ArgumentException`으로 즉시 실패한다.

**첫 슬라이스 튜닝 공급처**

`PartyTuning.Prototype`을 코드 기본 공급처로 두고 다음 값을 정확히 사용한다. Unity 직렬화 필드는 이번 범위에 추가하지 않는다.

```csharp
public static PartyTuning Prototype => new PartyTuning
{
    DefaultMemberMaxHp = 25,
    SurviveChargesPerCombat = 1,
    DrawByLivingCount = new Dictionary<int, int>
    {
        { 1, 3 },
        { 2, 4 },
        { 3, 5 }
    }
};
```

`PartyPrototypeRoster`와 `BattleScreenController.StartSession`은 별도 값 생성 없이 이 공급처를 사용한다.

**직접 대상 카드 사용 절차**

1. 인덱스, 턴 상태, 비용을 검증한다.
2. `PartyTargetRules.RequiresExplicitAllyTarget(def)`이면 `targetId`가 현재 생존 파티원인지 검증한다.
3. 대상이 무효면 `false`를 반환한다. 에너지 차감, 손패 버림, 미래 영역 추가, instance counter 증가는 모두 하지 않는다.
4. 유효하면 `TargetId`를 카드 인스턴스에 저장하고 비용 지불과 배치를 수행한다.
5. 배치 뒤 대상이 죽는 경우는 Task 3의 실행 시 취소가 처리한다.

캐릭터 소유 실행 카드를 배치할 때 가속·둔화에 따른 실행 순서 계산은 해당 `OwnerId`의 `PartyMember.Statuses`만 사용한다. 다른 파티원의 상태와 레거시 `PlayerStatuses`를 사용하지 않는다. 파티 소유 개입 카드는 개인 가속·둔화의 영향을 받지 않는다.

현재 `IEnemyTurnPolicy.CardsForTurn`은 행동 원본 적을 반환하지 않으므로, 첫 슬라이스의 `BeginTurn`은 생성하는 각 적 카드 인스턴스에 `Enemies[0].Id`를 `OwnerId`로 기록한다. 다중 적이 각자 행동을 생성하는 정책 확장은 별도 범위지만, 코어의 적 대형과 효과 핸들러는 어느 적 ID든 처리할 수 있어야 한다.

`ResolveTurn`은 `PartyMemberDied` 이벤트를 모아 `Deck.RemoveOwnedBy`를 호출한다. 손패를 포함한 죽은 소유자의 미사용 카드는 제거하고 파티 소유 카드는 유지한다. 다음 드로우는 생존자 수에 대응하는 `DrawByLivingCount`를 사용한다.

**실패 테스트**

- `Constructor_rejects_empty_oversized_duplicate_or_invalid_party`.
- `Constructor_rejects_missing_or_non_positive_draw_tuning_entries`.
- `Prototype_tuning_is_hp_25_survive_1_and_draw_3_4_5`.
- `Dead_or_missing_direct_target_rejects_play_without_mutation`: 반환 false이며 에너지·손패·미래 영역·instance counter가 동일하다.
- `Valid_target_can_be_placed_and_later_cancelled_if_target_dies`.
- `Death_removes_owned_cards_but_keeps_party_owned_cards`.
- `Draw_count_uses_living_member_count`.
- `One_survivor_can_continue_the_next_turn`.
- `Haste_and_slow_on_a_do_not_change_b_card_execution_order`.

---

### Task 6: 대형 이동과 실행 시점 타겟팅 통합 검증

**Files**

- Create: `Assets/Core/Effects/MoveFormationHandler.cs`
- Modify: `Assets/Core/Effects/EffectKey.cs`
- Modify: `Assets/Core/Simulation/CombatRegistries.cs`
- Test: `Assets/Core/Tests/EditMode/FormationTargetingIntegrationTests.cs`

`EffectKeys.MoveFormation`의 값은 이동 칸 수다. 음수는 자기 진영 전방(index 0 방향), 양수는 후방이며 범위를 벗어나면 끝에서 고정한다. 플레이어 카드면 `OwnerId`의 파티원을 `Party` 안에서, 적 카드면 `OwnerId`의 적을 `Enemies` 안에서 이동한다. 어느 쪽도 상대 진영 index나 화면 좌표로 이동량을 계산하지 않는다. 대상 소유자가 없거나 죽었으면 `NoValidTarget`으로 취소한다.

**핸들러 테스트**

- `Player_move_changes_only_party_order`.
- `Enemy_move_changes_only_enemy_order`.
- `Movement_clamps_to_own_formation_bounds`.
- `Dead_or_missing_owner_cancels_instead_of_moving_front_member`.

**TurnResolver 통합 테스트**

`FormationTargetingIntegrationTests.Later_frontmost_attack_uses_formation_after_earlier_move`를 다음 중립 fixture로 작성한다.

- 플레이어 A/B, 적 E를 만든다.
- order 2의 `[검증] 대형 이동` 카드가 B를 플레이어 전열로 이동시킨다.
- order 5의 `[검증] 전열 공격` 적 카드가 `FrontMost`로 피해를 준다.
- 전체 `TurnResolver.Resolve` 후 B만 피해를 받고 A는 피해를 받지 않았음을 확인한다.
- 이벤트 순서가 이동 카드 `CardResolved` 다음 공격 카드 `CardResolved`인지 확인한다.

이 테스트에는 제품용 능력 이름을 사용하지 않고 검증 목적만 드러내는 중립 ID를 사용한다.

---

### Task 7: 카드 저작 스펙과 설명 문구

**Files**

- Modify: `Assets/Core/Simulation/Authoring/EffectSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/CardSpecMapper.cs`
- Modify: `Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs`
- Modify: `Assets/Core/Simulation/Generated/GeneratedCards.cs`
- Modify: `Assets/Core/Simulation/Descriptions/IDescriptionVocabulary.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionVocabulary.cs`
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs`
- Modify: `Assets/Unity/Editor/CardCodeGenerator.cs`
- Test: `Assets/Core/Tests/EditMode/PartyDescriptionTests.cs`
- Test: `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`
- Test: `Assets/Core/Tests/EditMode/GeneratedCardsTests.cs`
- Test: `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs`

**저작 모델**

```csharp
public enum TargetSelectorRef
{
    None,
    FrontMost,
    SecondFromFront,
    BackMost,
    Random
}

public enum EffectKind
{
    Damage,
    ApplyStatus,
    GrantNextAttackBonus,
    NullifyNextReward,
    MoveFormation
}
```

`EffectSpec.Selector`를 `EffectData.TargetSelector`로 매핑한다. `CardSpecMapper`뿐 아니라 `CardCodeGenerator.EmitEffect`도 `Selector` 값을 생성 코드에 포함해야 한다. 직접 아군 대상 여부와 전체 아군 대상 여부도 기존 `StatusApplyTarget` 매핑을 통해 보존한다.

`ConditionKind.PrevIsPlayerAttack`과 `PrevIsEnemyAttack`은 각각 `PrevExecutedIsPlayerAttack`, `PrevExecutedIsEnemyAttack`으로 이름을 바꾸고 `PreviousExecutedCardIs`로 매핑한다. `NextIsEnemyAttack`은 기존 `AdjacentDirection.Next` 매핑을 유지한다.

**체크인된 생성 파일 rename 절차**

1. `EffectSpec.cs`, `CardSpecMapper.cs`, `StarterDeckSpecs.cs`에서 두 enum 식별자를 rename한다.
2. Unity 에디터를 실행하지 않고도 이 태스크의 헤드리스 게이트를 통과하도록 `Assets/Core/Simulation/Generated/GeneratedCards.cs`의 `ConditionKind.PrevIsEnemyAttack`을 `ConditionKind.PrevExecutedIsEnemyAttack`으로 같은 커밋에서 손으로 바꾼다. 이 파일은 생성 파일이지만 헤드리스 csproj 입력이므로 이번 기계적 수정은 예외로 허용한다.
3. `GeneratedCardsTests`의 enum 기대값도 새 이름으로 바꾼다. 피해량, 실행 순서, 성공 수치 9는 바꾸지 않는다.
4. `dotnet test`를 실행해 생성 파일까지 컴파일되는지 확인한다.
5. Task 9에서 Unity 메뉴 `Fate Weaver ▸ Generate Cards from SO`를 실행해 손수 바꾼 생성 파일을 에디터 출력으로 덮어쓰고 동일한 diff인지 확인한다.

**고정 한국어 문구**

- `FrontMost`: `가장 앞의 대상에게`
- `SecondFromFront`: `전열에서 두 번째 대상에게`
- `BackMost`: `가장 뒤의 대상에게`
- `Random`: `무작위 대상에게`
- `StatusApplyTarget.PartyMember`: `선택한 아군에게`
- `StatusApplyTarget.AllPartyMembers`: `모든 아군에게`
- 이전 실행 카드 조건: 카드명이 없는 현재 타입 기반 조건은 `직전에 실행한 카드가 {진영} {카드 타입}이면`을 사용한다.
- `NoPrecedingCardOfSide`: `이전에 실행한 {진영} 카드가 없으면`
- `NoFollowingCardOfSide`: frozen order 의미가 드러나도록 `뒤에 배치된 {진영} 카드가 없으면`

`직전 카드`, `바로 앞 카드`, `인접 카드`라는 사용자 노출 문구는 남기지 않는다.

**실패 테스트**

- 네 위치 셀렉터 설명이 정확히 위 문구와 일치한다.
- 직접 대상과 전체 대상 설명이 구분된다.
- `PreviousExecutedCardIs` 설명이 `직전에 실행한 카드`를 사용한다.
- `NoPrecedingCardOfSide`와 `NoFollowingCardOfSide` 설명이 각각 실행 이력과 frozen 배치 순서를 구분한다.
- `CardSpecMapper`가 `SecondFromFront`와 `AllPartyMembers`를 보존한다.
- `GeneratedCardsTests`와 `StarterDeckSpecEquivalenceTests`가 새 enum 이름으로 통과한다.

Unity 전용 `CardCodeGenerator.EmitEffect`는 이 태스크에서 `sb.Append("Selector = TargetSelectorRef.").Append(e.Selector)`에 해당하는 출력을 추가하고 파일을 정독한다. 실제 생성 결과의 `Selector = TargetSelectorRef` 포함 여부는 Task 9의 에디터 재생성과 Task 10의 positive `rg`로 검증한다.

회복 효과와 제품용 신규 능력 설명은 이 태스크에 추가하지 않는다.

---

### Task 8: 중립 검증 fixture와 2인 장면 데이터

**Files**

- Create: `Assets/Core/Simulation/PartyPrototypeRoster.cs`
- Create: `Assets/Core/Simulation/PartyPrototypeDeck.cs`
- Create: `Assets/Core/Simulation/Authoring/PartyPrototypeDeckSpecs.cs`
- Test: `Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs`

**검증 데이터**

- 파티원 ID는 `member_a`, `member_b`, 표시명은 `파티원 A`, `파티원 B`다.
- A는 기존 스타터 덱을 소유한다.
- B의 검증 덱은 정확히 6장이다.
  - `[검증] 공격` 2장: 기존 피해 효과만 사용한다.
  - `[검증] 선택 방어` 2장: 선택한 생존 아군 한 명에게 방어를 적용한다.
  - `[검증] 전체 방어` 1장: 모든 생존 아군에게 각자 방어를 적용한다.
  - `[검증] 대형 이동` 1장: 소유자를 한 칸 전방으로 이동한다.
- 카드 ID도 `fixture_attack`, `fixture_selected_block`, `fixture_all_block`, `fixture_move_forward`처럼 검증 목적을 드러낸다.
- 새 고블린 제품 카드는 만들지 않는다. 적 위치 셀렉터 검증은 Task 6의 테스트 fixture를 사용한다.

**실패 테스트**

- `Prototype_deck_contains_only_validation_prefixed_cards`.
- `Prototype_deck_has_six_cards_and_expected_duplicates`.
- `Hand_coded_and_authored_specs_map_to_equal_definitions`.
- `Selected_block_requires_explicit_ally_target`.
- `All_block_does_not_open_direct_target_selection`.
- `Roster_assigns_distinct_character_owners`.

---

### Task 9: Unity 전투 UI, 대상 선택, 카드 소유자, 씬 빌더

**Files**

- Create: `Assets/Unity/CharacterAsset.cs`
- Modify: `Assets/Unity/CardPresentation.cs`
- Modify: `Assets/Unity/CardView.cs`
- Modify: `Assets/Unity/UnitView.cs`
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/DeckPlaytestController.cs`
- Modify: `Assets/Unity/PlaytestKoreanText.cs`
- Modify: `Assets/Unity/Editor/CardCodeGenerator.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Create when rebuilding: `Assets/Scenes/FateWeaverBattle.unity`

**CharacterAsset**

```csharp
[CreateAssetMenu(menuName = "Fate Weaver/Character")]
public sealed class CharacterAsset : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private Color _color;
    [SerializeField] private DeckAsset _deck;

    public string Id => _id;
    public string DisplayName => _displayName;
    public Color Color => _color;
    public DeckAsset Deck => _deck;
}
```

`BattleScreenController`의 단일 `_deck` 필드를 제거하고 `CharacterAsset[] _party`를 사용한다. `BuildArtLookup`, `StartSession`, pile 표시도 `_party`의 각 덱을 순회하도록 바꾼다. `DeckPlaytestController`는 `OwnedCard.Def`을 사용한다.

**UnitView**

- 기존 `Bind(string displayName, Color portraitTint)`와 `SetHp`는 적 UI 호환을 위해 유지한다. 파티 대상 입력을 위해 `BindTarget(string memberId, Action<string> onClick)`, `SetStatuses(IReadOnlyList<StatusInstance> statuses)`, `SetTargetable(bool value)`를 추가한다.
- 상태 행은 `member.Statuses.All`을 순회해 현재 존재하는 개인 버프·디버프의 이름과 수치를 표시한다. 이름은 기존 `PlaytestKoreanText.StatusName`을 사용한다. 카드 인스턴스에만 붙은 상태는 유닛 상태 행에 표시하지 않는다.
- 대형 위치 문구와 `SurviveCharges`는 표시하지 않는다.
- 죽은 유닛은 직접 대상 버튼이 비활성화된다.

**입력 모드**

`BattleScreenController`는 `Normal`, `InterventionTargeting`, `AllyTargeting` 세 모드를 상호 배타적으로 관리한다.

- `InterventionTargeting`: 기존 전체 dim과 실행 레일 선택을 사용한다.
- `AllyTargeting`: 전체 dim을 유닛 위에 올리지 않는다. 생존 아군 유닛만 강조·클릭 가능하게 하고 손패, 레일, 턴 종료 입력을 잠근다.
- 취소 버튼은 두 선택 모드에서 항상 dim보다 위에 있어야 한다.
- 선택한 유닛이 클릭 직전에 사망했거나 세션 검증에 실패하면 카드를 소비하지 않고 메시지만 갱신한다.
- 모드 종료 시 `_armedAllyTargetHandIndex`, 개입 선택 상태, 강조 표시를 모두 초기화한다.

**카드 소유자**

- `CardPresentation`에 `OwnerDisplayName`, `OwnerColor`, `IsPartyOwned`를 추가한다.
- `PlaytestKoreanText`에 `public static string PartyOwnerName() => "파티";`를 추가한다. 컨트롤러와 뷰에 `"파티"` 문자열을 직접 쓰지 않는다.
- 손패용 `FromDefinition`과 레일용 `From`에 `ownerDisplayName`, `ownerColor`, `isPartyOwned` 인자를 추가한다. `BattleScreenController`가 `OwnerId`로 `CharacterAsset`을 찾아 이 값을 넘기고, null 소유자는 `PlaytestKoreanText.PartyOwnerName()`과 파티 공용 색을 넘긴다.
- `CardView`는 카드 아트 좌하단에 작은 소유자 칩을 표시한다. 캐릭터 소유면 이름과 색, 파티 소유면 `파티`를 표시한다.
- 적 카드는 합성 플레이어 덱 소유자 구분 대상이 아니므로 소유자 칩을 숨기고 기존 적 카드 스타일을 유지한다.
- 손패와 실행 레일 모두 동일한 칩을 사용한다.
- 칩의 최종 위치·아트는 확정하지 않으며 이 슬라이스에서는 가독성만 검증한다.

**대형 렌더링**

- 플레이어 `Party[0]`은 플레이어 행의 오른쪽 끝에 둔다. 구현은 `SetSiblingIndex(count - 1 - i)` 또는 동일 결과를 내는 명시적 좌표를 사용한다.
- 적 `Enemies[0]`은 적 행의 왼쪽 끝에 둔다. 구현은 `SetSiblingIndex(i)`를 사용한다.
- 갱신 때마다 두 진영을 각각 자기 리스트 기준으로 정렬한다.

**BattleSceneBuilder 필수 수정**

- 제거된 `_deck`을 찾거나 직렬화하는 모든 코드를 삭제한다.
- `_party` 배열에 `member_a`, `member_b`의 `CharacterAsset`을 연결한다. 로드 경로는 각각 `Assets/Unity/CharacterSO/member_a.asset`, `Assets/Unity/CharacterSO/member_b.asset`이다.
- 개인 상태 텍스트, 소유자 칩, 아군 대상 강조 레이어, 선택 취소 버튼을 생성·연결한다.
- `Fate Weaver ▸ Build Battle Scene`으로 `Assets/Scenes/FateWeaverBattle.unity`를 재생성한 뒤 Missing Script와 누락된 SerializeField가 없는지 Inspector에서 확인한다.

**CardCodeGenerator 검증 에셋 시딩**

`CardCodeGenerator`에 `[MenuItem("Fate Weaver/Seed Party Prototype Assets")] public static void SeedPartyPrototype()`를 추가한다. 메뉴는 재실행 가능해야 하며 기존 `CardAsset.Art`는 보존한다.

- 검증 카드 폴더: `Assets/Unity/CardSO/Validation`
- 검증 덱: `Assets/Unity/CardSO/Validation/PartyPrototypeDeck.asset`
- 캐릭터 폴더: `Assets/Unity/CharacterSO`
- `PartyPrototypeDeckSpecs.Build()`를 ID별로 묶어 검증 `CardAsset`과 정확한 count의 `DeckAsset.Entry`를 만든다.
- `member_a.asset`: ID `member_a`, 표시명 `파티원 A`, 색 `(0.35, 0.65, 0.95, 1)`, 덱 `Assets/Unity/CardSO/Player/StarterDeck.asset`.
- `member_b.asset`: ID `member_b`, 표시명 `파티원 B`, 색 `(0.90, 0.62, 0.25, 1)`, 덱 `PartyPrototypeDeck.asset`.
- private 직렬화 필드는 `SerializedObject.FindProperty("_id")`, `_displayName`, `_color`, `_deck`으로 채우고 `ApplyModifiedPropertiesWithoutUndo()` 후 저장한다.
- `BattleSceneBuilder.Build()`은 두 캐릭터 에셋 중 하나라도 없으면 씬을 만들지 않고 `Fate Weaver/Seed Party Prototype Assets`를 먼저 실행하라는 오류를 출력한다.

Task 9 에디터 실행 순서는 다음으로 고정한다.

1. `Fate Weaver ▸ Generate Cards from SO`: Task 7에서 손으로 rename한 `GeneratedCards.cs`를 재생성하고 diff가 enum 이름·`Selector` 출력과 일치하는지 확인한다.
2. `Fate Weaver ▸ Seed Party Prototype Assets`: 검증 카드·덱·두 `CharacterAsset`을 생성한다.
3. `Fate Weaver ▸ Build Battle Scene`: `_party`가 연결된 전투 씬을 생성한다.

**Unity 수동 검증**

- 아군 대상 모드에서 살아 있는 유닛을 실제로 클릭할 수 있다.
- 죽은 유닛을 클릭할 수 없고 카드가 소비되지 않는다.
- 개입 모드와 아군 대상 모드가 동시에 보이지 않는다.
- A에게만 방어를 주면 A의 상태 행만 변한다. 전체 방어는 A/B 각각에 표시된다.
- 플레이어 전열은 중앙에 가까운 오른쪽 끝, 적 전열은 중앙에 가까운 왼쪽 끝이다.
- 손패와 실행 레일에서 카드 소유자를 식별할 수 있다.

---

### Task 10: 회귀 검증과 문서 일치 확인

**Files**

- Modify when generated: `Assets/**/*.meta`
- Verify: `docs/superpowers/specs/2026-07-15-party-foundation-design.md`
- Verify: all files changed by Tasks 1–9

**자동 검증**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0
rg -n "NullifiedBehavior|StatusKeys\.Nullified|끼어들기|step_in|응급 처치|mend|엄호 지시|guard_order" Assets
rg -n '"직전 카드|"바로 앞이|"인접 카드' Assets/Core Assets/Unity
rg -n "FindProperty\(\"_deck\"\)|_deck" Assets/Unity/Editor/BattleSceneBuilder.cs Assets/Unity/BattleScreenController.cs
rg -n "ConditionKind\.(PrevIsPlayerAttack|PrevIsEnemyAttack)" Assets/Core Assets/Unity
rg -n "PrevExecutedIsEnemyAttack" Assets/Core/Simulation/Generated/GeneratedCards.cs
rg -n "Selector = TargetSelectorRef" Assets/Core/Simulation/Generated/GeneratedCards.cs
test -f Assets/Unity/CardSO/Validation/PartyPrototypeDeck.asset
test -f Assets/Unity/CharacterSO/member_a.asset
test -f Assets/Unity/CharacterSO/member_b.asset
test -f Assets/Scenes/FateWeaverBattle.unity
```

`dotnet test`와 네 `test -f`는 성공해야 한다. 앞의 네 금지 문자열 `rg`는 결과가 없어야 하고, 뒤의 두 생성 파일 확인 `rg`는 각각 새 enum 이름과 `Selector` 출력을 찾아야 한다. 기존 제품 코드에 이미 존재하는 `_deck` 지역 필드가 있고 이번에 제거할 직렬화 필드와 무관하다면 해당 줄을 직접 읽어 그 이유를 검증 기록에 남긴다.

**최종 플레이 모드 체크리스트**

- 2인 파티가 각자 HP와 개인 상태를 표시한다.
- 플레이어/적 index 0이 화면 중앙을 마주본다.
- 위치 대상은 실행 시점 대형을 따른다.
- 무효 직접 대상은 사용 단계에서 거부되어 손패와 비용이 유지된다.
- 배치 후 사라진 대상은 `NoValidTarget`으로 취소되고 다른 유닛에게 폴백하지 않는다.
- 죽은 소유자의 대기 카드는 `OwnerDied`로 취소되고 연계 판정에서 제외된다.
- 사망한 소유자의 미사용 카드는 덱·손패·버림에서 제거되고 파티 소유 카드는 유지된다.
- 상태 차단 카드도 “직전에 실행한 카드”로 취급되지 않는다.
- 아군 대상 선택 중 유닛 클릭과 취소 버튼이 작동한다.
- 손패와 실행 레일의 소유자 칩이 읽힌다.
- 파티 소유 카드 칩은 `PlaytestKoreanText.PartyOwnerName()`의 `파티` 문구를 사용한다.
- `member_a`, `member_b` CharacterAsset과 검증 DeckAsset이 Missing 없이 `_party`에 연결되어 있다.
- 콘솔에 예외, MissingReference, 누락 직렬화 경고가 없다.

**완료 조건**

자동 테스트, 금지 문자열 스캔, 플레이 모드 체크리스트를 모두 통과한 뒤에만 구현 완료로 간주한다. 맵/노드/영입/회복 기회와 운명 카드 최종 귀속은 별도 계획으로 남긴다.
