# 전투 수직 슬라이스 — 시그니처 적 설계 (브레인스토밍 진행 중 · WIP 핸드오프)

- 작성일: 2026-07-03
- 상태: **브레인스토밍 미완료.** 섹션 1(컨셉/핵심 딜레마) 제시 후 **사용자 확인 대기 중**. 섹션 2~5 미작성. 아직 승인된 스펙 아님 — writing-plans 이전 단계.
- 원천 흐름: `superpowers:brainstorming` (Warden 때와 동일하게 brainstorming → writing-plans → subagent-driven 예정)
- 관련: [`fate-weaver-core-design`](2026-06-18-fate-weaver-core-design.md), Warden 선례 [`warden-lock-enemy-design`](2026-06-27-warden-lock-enemy-design.md)

---

## 0. 재개 지점 (다음 세션은 여기부터)

브레인스토밍은 **섹션 1까지 제시했고, 사용자가 다음 두 질문에 답하면 섹션 2로 진행**한다:

1. **껍질(Carapace)이 매 턴 리셋인가, 전투 1회성인가?** — 제안: **매 턴 리셋**(매 턴 순서 퍼즐이 반복되어 슬라이스 재미 지속). 라가불린식 1회성은 페이싱 기믹이라 이 슬라이스의 "순서 조작" 축과 결이 다름.
2. **#3 보복을 적 카드 1장에만 붙일지 / 적 전체 패시브로 둘지** — 제안: **카드 1장**(가독성↑, 엔진 확장 0).

그 뒤 남은 섹션: **§2 정확한 메커니즘 + 적 카드 세트 → §3 필요한 플레이어 카드/상태이상 서브셋 → §4 인카운터 형태·튜닝(HP/턴/승패 목표) → §5 헤드리스 검증 + 범위 밖(UI).** 그다음 spec 확정 → 자가검토 → 사용자 검토 → `writing-plans`.

---

## 1. 목표 / 확정된 결정

### 목표
현재 컨셉("실행 카드는 자동 발동되지만 불완전 → 개입 카드로 미래 영역 순서(실행 순서) 조작해 완성") 위에 **"유저가 재미를 느낄 법한" 전투**를 만든다. 1차 목표는 **넓은 콘텐츠 어휘가 아니라 "하나의 완성도 있는 재미있는 전투(수직 슬라이스)"** — 사용자 선택.

### 확정된 결정 (브레인스토밍 대화에서)
- **1차 산출물 = 수직 슬라이스 1개.** 특정 적 + 그에 맞는 최소한의 플레이어 카드/상태이상만 골라 "이 전투가 재미있다"를 헤드리스 밸런스 + 플레이테스트로 증명.
- **중심 = 신규 시그니처 적** (기존 Goblin/Warden 승격 아님).
- **적 고유 기믹은 상태이상에 강제되지 않아도 된다** — 순수 기믹만으로 재미있으면 OK (사용자 명시). StS 몬스터 패턴 참고.
- **주 기믹 = 아키타입 #1 "첫 타격 반응형"(Skittish/Curl Up 계열) + 보조 #3 "카테고리 응징형"(Gremlin Nob 계열).** ← StS 1+2 전체 로스터 조사 후 사용자 선택.

### 엔진 관련 사실 (설계 제약)
- 코어는 순수 C#(`noEngineReferences`), `dotnet test` 헤드리스로 검증(`Tests/Headless/`). 밸런스/콘텐츠 작업 루프는 Unity 에디터 불필요.
- 상태이상 = `IStatusBehavior` (Scope+Hook) 레지스트리. **현재 훅: `ModifyIncomingDamage`(피격자), `InterceptCardResolve`(카드), `ModifyExecutionOrder`(카드 실행 순서). ⚠ *주는 피해(outgoing)* 훅은 없음** → "힘/약화" 같은 공격력 버프/디버프는 아직 표현 불가. 필요 시 첫 보강 지점.
- 조건부 효과: `EffectData.Conditional(key, base, condition, successAmount)` + 조건 레코드(`FirstToTrigger`/`WithinNth`/`BeforeNextEnemyAttack`/`AdjacentCardIs`/`NoPrecedingCardOfSide`/`NoFollowingCardOfSide`/`SameTarget`) 이미 존재.
- 적 행동 = `IEnemyTurnPolicy` 시밍 (`RandomMovesetPolicy`/`ShuffleBagPolicy`/`SelfLockPolicy` 데코레이터). 적 카드도 플레이어 카드와 같은 미래 영역에 실행 순서 갖고 배치됨.
- tie-break: 동률 시 **플레이어 우선** 발동 (`ad7d1c2`).
- 카드 설명은 효과-조합 컴포저가 자동 생성 → 신규 카드는 EffectData만 쓰면 설명 나옴(하드코딩 금지).

---

## 2. 시그니처 적 컨셉 (섹션 1에서 제시, 확인 대기)

**작업 이름:** "각질의 파수꾼" (가칭, id `carapace_sentinel`) — 이름 교체 가능.

**판타지:** 단단한 껍질을 두른 둔중한 파수병. 첫 일격은 껍질이 튕겨내고, 성급하게 굴수록 더 세게 되받아친다. → 플레이어에게 **템포 결정**을 강요.

**핵심 딜레마 (재미 기둥):** 이 게임의 본능("큰 카드를 앞으로 당겨 적보다 먼저 터뜨린다")을 양쪽에서 응징:
- **껍질(#1):** 이번 턴 적에게 **가장 먼저 닿는 플레이어 타격**을 흡수. → 작은 카드로 껍질을 먼저 벗기고, 진짜 한 방은 그 뒤에 배치해야 함.
- **보복(#3):** 적 시그니처 공격 = "**나보다 먼저 플레이어 카드가 발동했으면 피해 +N**". → 껍질 벗기려 앞으로 당기는 그 행동이 반격을 키움.

→ **"세게+빨리 치고 싶다 — 그런데 빨리 치면 (a) 첫 방이 껍질에 먹히고 (b) 반격이 강해진다."** 개입 카드로 *페인트/한 방/적 보복*의 순서를 어디에 끼우느냐가 매 턴 계산. 순수 순서 퍼즐, 상태이상 강제 없음.

**엔진 갭 회피 (중요):** #3 "적이 강해진다"를 **Strength 상태이상으로 만들지 않는다** (outgoing-damage 훅 부재). 대신 **기존 `EffectData.Conditional` + SuccessEffectValue**로 "앞에 플레이어 카드 있으면 +N 피해"를 적 카드에 직접 넣음 → 신규 훅 0. 껍질(#1)만 약간의 신규 작업 필요, 그것도 기존 `ModifyIncomingDamage` 훅 + "1회 소모" 수명으로 대부분 재사용 예정(§2에서 확정 예정).

---

## 부록 A — StS 적 아키타입 조사 (근거 자료, 재사용용)

StS 1(지식) + StS 2(웹) 전체 로스터를 행동 아키타입으로 분류. 각 아키타입 → 이 게임의 "공유 미래 영역 + 실행 순서 + 개입 조작" 축으로 번역 + 순서-퍼즐 적합도.

| # | 아키타입 | StS 예시 (1 / 2) | Fate Weaver 번역 | 적합도 |
|---|---|---|---|---|
| 1 | **첫 타격 반응형** | Curl Up, Louse / Phantasmal Gardener "Skittish" | "이번 턴 나에게 가장 먼저 발동한 플레이어 카드는 무효/반감" | ★★★★★ |
| 2 | **순서/카운트 반응형** | Time Eater / The Insatiable "Sandpit"(카운트다운 0=즉사) | 해석 순서 K번째에 응징. 순서 자체가 무기 | ★★★★☆ (가독성 리스크) |
| 3 | **카테고리 응징형** | Gremlin Nob, Awakened One / Test Subject, Fossil Stalker(미차단 피해로 힘) | "나보다 먼저 플레이어 [공격/방어] 발동하면 나 +피해" | ★★★★★ |
| 4 | **충전/타이머형** | Cultist, Lagavulin, Hexaghost / Lagavulin Matriarch, Tunneler, Slumbering Beetle, Devoted Sculptor | 잠긴 대형 카드 예고 후 N턴 뒤 폭발 | ★★★☆☆ (Warden 중복) |
| 5 | **모드 전환형** | The Guardian, The Champ / Test Subject 3페이즈 | 턴마다 배치 패턴·응징 조건 변화 | ★★★★☆ |
| 6 | **과확장 응징형** | Sharp Hide / Waterfall Giant, Toadpole/Spiny Toad(가시) | "같은 종류 연속 발동 시 반격" → 공/방 교대 강제 | ★★★☆☆ |
| 7 | **다중 몸체 타겟팅** | Sentries, 3 Gremlins / Kaiser Crab, Kin, Knight Gang, 소환사류 | 여러 적 카드가 서로 다른 실행 순서 | ★★☆☆☆ (다중 적 근사 한계) |
| 8 | **방해/손패 교란형** | Sentries Dazed, Slavers Entangle, Snecko / Knowledge Demon, Haunted Ship(덱 오염) | 조건 보상 무효화·잠금·강제 (Warden 계열) | ★★★☆☆ (Warden 겹침) |

조사 결론: StS 2까지 넓혀도 새 아키타입 없이 위 8개로 안정. StS 2의 "미차단 피해로 힘↑" / "가시로 다단히트 응징"이 모두 "무엇이·어떤 순서로·어떤 조합으로 해석되는가"라는 이 게임의 핵심 축과 겹쳐 **#1이 스위트 스폿**임을 강화.

**출처:** slaythespire.wiki.gg (StS1/StS2 목록), sts2front.com/enemies (StS2 노멀 몬스터 메커니즘), gameguidesbox (StS2 보스/엘리트 기믹), StS1은 기존 지식. (namu.wiki는 봇 403으로 접근 불가.)

---

## 부록 B — 향후 확장 메모 (이번 슬라이스 범위 밖, 잊지 말 것)
- **outgoing-damage 훅 부재** → "힘/약화" 류 버프/디버프를 나중에 넓히려면 `IStatusBehavior`에 `ModifyOutgoingDamage` 훅 1개 + 파이프라인 질의 1줄 추가(국소 변경, core-design §5 예견됨).
- 다른 아키타입(#2 Time Eater류, #5 모드 전환)은 UI 텔레그래프가 성숙한 뒤 재고.
- 슬라이스 검증 후: "시드 스윕 + 플레이어 정책/탐색" 밸런스 층(승률·지배전략·스킬표현력)은 별도 작업. 결정론 덕에 구현 가능하나 미구현.
