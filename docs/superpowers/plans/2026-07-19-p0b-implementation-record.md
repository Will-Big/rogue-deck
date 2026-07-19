# P0-B — 열린 카드 저작 구조 (구현 기록)

- 작성일: 2026-07-19
- 상태: 구현 완료 (헤드리스 검증 통과, Unity 컴파일·Play 검증은 사용자 확인 대기)
- 원 백로그: [`2026-07-16-architecture-refactor-backlog.md`](2026-07-16-architecture-refactor-backlog.md) §4
- 설계 문서: [`../specs/2026-07-19-open-card-authoring-design.md`](../specs/2026-07-19-open-card-authoring-design.md)
- 선행 완료: P0-A RNG 단일화 (2026-07-18)

## 설계 결정 요약

설계 문서(§3)에서 사용자와 합의한 두 결정을 그대로 구현했다.

1. **저작 구조는 다형 spec 클래스** (백로그 §4 대안 1). `FateWeaver.Simulation.Authoring`에 추상
   `[Serializable] EffectSpec`을 두고, 기존 5종 효과(Damage/ApplyStatus/GrantNextAttackBonus/
   NullifyNextReward/MoveFormation)를 서브클래스로 이관했다. 각 서브클래스가 자기 파라미터, 코어
   키 매핑, 검증을 소유한다. Unity 층은 `[SerializeReference]` + 카탈로그 드로어로 서브클래스를
   선택한다. 리플렉션 자동 등록이나 raw string dictionary는 채택하지 않았다.
2. **코어 `EffectData`의 ApplyStatus 전용 필드(`StatusKey/StatusLifetime/StatusTarget` 등)도 이번에
   payload로 이관했다.** 저작 층만 열고 공용 모델을 그대로 두면 새 효과가 결국 공용 모델을 다시
   키우기 때문이다.

## Task 1의 접근 변경 — 오라클 → golden 고정

계획 시점에는 "동등성 오라클"(기존 mapper 출력과 신규 경로 출력을 비교) 방식을 상정했으나, Task 1
실행 중 실제 콘텐츠에서 핸드코딩 값과 SO/spec 저작값 사이의 드리프트가 발견되어 사용자와 함께
접근을 **golden 시그니처 고정** 방식으로 변경했다. 발견된 드리프트 3건:

1. `pull_forward` 이동 거리 — 핸드코딩 경로 `-2` vs spec/SO 경로 `-1`.
2. `push_back` — 핸드코딩 경로에는 존재하지 않음.
3. 이름 드리프트 — "밀어내기"(specs) vs "미룸"(SO/생성 코드).

세 건 모두 이번 P0-B 범위(저작 구조 개편)가 아니라 콘텐츠 정합성 문제이므로, 수정하지 않고
**P1-A(SO 단일 원본화)로 이관**하기로 사용자와 합의했다. 대신 리팩터링이 안전하도록 현재 동작을
golden 시그니처로 고정해 회귀 감지만 확보했다. 이 결정으로 헤드리스 베이스라인이 계획 문서상 260이
아니라 실제 292(사전 존재)였음도 함께 확인되었고, golden 테스트 추가 후 298/298로 갱신되었다.

## 커밋 목록

- `cbab0f3` test(core): pin authored card content with golden signatures (P0-B prep)
- `85514fe` chore(unity): add meta for content golden test
- `37c7ccb` refactor(core): move apply-status fields into EffectData payload (P0-B)
- `825f4f2` refactor(authoring): polymorphic effect specs replace closed enums (P0-B)
- `b395583` feat(authoring): authoring-time validation walk for card specs (P0-B)
- `92987e6` test(authoring): prove new-effect locality with sample heal package (P0-B)
- `d38d329` feat(unity): SerializeReference effect authoring with catalog drawer (P0-B)

## 완료 조건 검증 (설계 문서 §7 / 백로그 §4 대응)

- [x] 샘플 신규 효과 추가 시 중앙 enum/mapper 수정 없음 — Task 5(`92987e6`)에서 Heal 샘플 효과를
      단일 클래스 + 등록만으로 추가해 증명 (단일 파일 diff로 검증됨).
- [x] 신규 효과의 실행·설명·저작·검증 경로가 클래스/등록 단위로 국소화 — 위 Heal 샘플이 실행
      핸들러, 설명 컴포저, authoring spec, 검증 로직 모두 클래스 단위로 국소화됨을 함께 증명.
- [x] 기존 시작덱·적덱·파티 검증 카드의 export 등가성 유지 — Task 1의 golden 시그니처 테스트가
      이관 전후 콘텐츠를 고정해 회귀 없음을 확인 (드리프트 3건은 위와 같이 P1-A로 별도 이관).
- [x] 잘못된 키·필드·파라미터가 에디터/부팅 검증에서 실패 — Task 4(`b395583`)에서 카드 spec
      저작-시점 검증 워크을 추가, 부팅/에디터 검증 경로에서 실패하도록 구현.
- [ ] 생성 파일과 런타임 SO가 동일 `CardDefinition`을 생성 — 코드·검증 경로상으로는 동일 스키마를
      통해 생성되도록 구현했으나(Task 3/6), 실제 코드 생성 재실행과 SO 로드 결과의 동등성은 Unity
      에디터 안에서만 확인 가능하다. **사용자의 GeneratedCards 재생성 및 diff 확인 대기.**

## 워크트리 격리 사유

Task 2 완료 시점에 메인 체크아웃이 사용자의 별도 병렬 브랜치(`card-selection-placement-motion`)
작업으로 전환되어, 이후 P0-B 작업이 메인 체크아웃의 상태와 충돌하지 않도록 전용 워크트리
`/Users/ish/Git/rogue-deck-p0b`(브랜치 `p0b-open-card-authoring`)로 옮겨 진행했다. Task 3부터의
정본 실행 기록은 이 워크트리의 `.superpowers/sdd/progress.md`에 있다. Unity 층(Task 6) 검증은
사용자가 메인 체크아웃에서 이 브랜치를 병합·전환한 뒤에만 가능하므로 그때까지 보류되었다.

## 사용자 검증 대기 항목

- Unity 에디터에서 이 브랜치 컴파일 확인 (Task 6까지 컴파일러 대신 API/문법 수동 대조로 자가 검증만
  수행함).
- 시드 메뉴 4종 재실행 — 결과가 골든 시그니처·기존 동작과 일치하는지 확인.
- `GeneratedCards` 재생성 후 SO 기반 `CardDefinition`과의 diff 확인 (완료 조건 마지막 항목).
- 카드 저작 드로어에서 `[SerializeReference]` 카탈로그 선택 UI와 Play 동작 확인.
- Task 1·4·5에서 추가된 신규 `.cs` 파일의 `.meta` 커밋 여부 확인 (에디터가 자동 생성하는 `.meta`는
  에디터 세션에서만 생성 가능하여 이번 구현에는 포함되지 않음).
