# Fate Weaver Unity Playtest

## 실행

1. Unity에서 `Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity`를 연다.
2. Play 버튼을 누른다.
3. 상단에서 멀티턴 시나리오를 선택한다.
4. 미래 영역의 카드를 눌러 Primary/Secondary를 선택한다.
5. 운명 액션(주도력 ±2 / 선택 교환 / 잠금)을 적용한다.
6. `RESOLVE TURN`으로 이번 턴을 해석한다.
7. `NEXT TURN`으로 다음 턴으로 진행한다(HP·상태가 이월된다). 승패가 나거나 마지막 턴이면 종료.

## 빠른 확인

### mark-combo (1턴)

- 조작 없이 `RESOLVE TURN` → `mark | Basic`, `slash | damage 2` (적이 먼저라 콤보 미완성).
- `goblin_jab`을 Primary로 선택 → `Initiative +2` → `RESOLVE TURN` → `mark | Success`, `slash | damage 8`.

### chapter-8-three-turn-opening (3턴)

- 매 턴 `quick_cut_*`을 Primary로 선택 → `Initiative -2`로 적보다 앞당김 → `RESOLVE TURN` → `Success`.
- `NEXT TURN`으로 3턴까지 진행하며 적 HP가 누적 감소하는지 확인.

### counter-stance / chain-slash (1턴)

- 조작 없이 `RESOLVE TURN` 후 반격/연쇄가 조건에 따라 발동하는지 확인.

## 현재 범위

- 멀티턴 시나리오, 턴 간 HP·상태 이월, 승패 정지
- 주도력 ±2, 카드 주도력 교환, 잠금
- 조건 tier, 피해, HP, 상태(방어/취약 등), 승패 출력
- 임시 IMGUI 화면이며 연출·드래그앤드롭·덱 구성은 포함하지 않는다.

## 검증 메모

- 멀티턴 진행 로직은 `MultiTurnPlaytestSession`(순수 C#)에 있고 헤드리스 테스트로 검증된다.
- 이 컨트롤러(MonoBehaviour)는 Unity Play에서만 동작/컴파일을 확인할 수 있다.
