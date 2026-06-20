# Fate Weaver Unity Playtest

## 실행

1. Unity에서 `Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity`를 연다.
2. Play 버튼을 누른다.
3. 상단에서 시나리오를 선택한다.
4. 미래 영역의 카드를 눌러 Primary와 Secondary를 선택한다.
5. 운명 액션을 적용하고 `RESOLVE TURN`을 누른다.

## 빠른 확인

### quick-cut-swap

1. `enemy_jab`을 Primary로 선택한다.
2. `quick_cut`을 Secondary로 선택한다.
3. `Swap Selected`를 누른다.
4. 턴을 실행한다.

기대 결과: `quick_cut | Success | damage 10`.

### chapter-8-auto-combo-guard

1. `wrist_cut`을 Primary로 선택한다.
2. `Initiative +2`를 누른다.
3. 턴을 실행한다.

기대 결과: `mark_target | Success`, `chain_slash | damage 12`.
조작하지 않으면 표식 보상이 차단되어 `mark_target | Basic`, `chain_slash | damage 6`이다.

## 현재 범위

- 단일 턴 고정 시나리오
- 주도력 ±2, 카드 주도력 교환, 잠금
- 조건 tier, 피해, HP, 승패 출력
- 임시 IMGUI 화면이며 연출·드래그앤드롭·덱 구성은 포함하지 않는다.
