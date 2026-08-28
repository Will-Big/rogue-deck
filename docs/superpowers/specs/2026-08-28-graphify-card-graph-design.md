# graphify 카드 그래프 통합 설계

- 작성일: 2026-08-28
- 상태: current
- 범위: 카드·상태·풀·덱·캐릭터 JSON의 graphify 그래프 편입, 재생성 단일 명령, 사람용 시각화 2장, AGENTS.md 규칙 21·22 개정

## 설계 개요 (사람 검수용)

이 절만 읽고 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은
읽지 않아도 된다.

- **무엇을 만드나** — 카드·상태·풀·덱·캐릭터 JSON을 graphify 그래프에 결정론적으로 편입시키는
  추출기와, 그래프에서 잡음(참조 스텁·테스트·외부 패키지)을 걷어내는 프루너, 재생성 전 과정
  (삭제→AST 재생성→프루닝→카드 추출→병합→뷰 생성)을 한 줄로 묶는 스크립트, 사람이 보는 소형
  시각화 2장(카드 관계망, 코드 아키텍처)을 만든다. LLM 토큰 0, 총 십수 초.
  태그는 그래프에서 제외한다 — 모든 엣지가 엔진이 실행하는 데이터에서만 나온다.

- **구조**

  | 객체 | 책임 (한 줄) | 이 객체가 모르는 것 |
  |---|---|---|
  | 카드 그래프 추출기 | Content JSON과 코어 키 정의를 읽어 카드 서브그래프 JSON을 만든다 | graphify 내부, C# AST 추출 방식 |
  | 그래프 프루너 | 스텁·테스트·외부 패키지 노드와 그에 딸린 엣지를 걷어낸다 | 노드가 왜 만들어졌는지 |
  | 재생성 스크립트 | 삭제→AST 재생성→프루닝→카드 추출→병합→뷰 생성을 순서대로 호출만 한다 | 각 단계의 내부 로직 |
  | 뷰 생성기 | 서브그래프 JSON을 graphify cluster-only로 HTML 한 장씩 렌더한다 | 그래프가 어떻게 만들어졌는지 |
  | 아키텍처 접기 | 게임 로직 코드 그래프를 커뮤니티 단위 노드로 접은 소형 그래프를 만든다 | 커뮤니티가 어떻게 계산됐는지 |
  | AGENTS.md 규칙 개정 | 규칙 21·22의 재생성 명령과 조회 경제성 서술을 갱신한다 | — (문서) |

- **의존 방향** — `재생성 스크립트 → graphify CLI → 프루너 → 추출기 → 접기 → 뷰 생성기`
  (일렬 호출, 역방향 없음). 추출기의 입력은 `Content/*.json` + `EffectKey.cs`·레지스트리 +
  AST graph.json.

- **그래프에 남는 것** — 게임 로직 코드(코어+표현, 실측 1,231노드) + 카드 서브그래프(~60) +
  문서(1,743). 걷어내는 것: 참조 스텁 751, 테스트 1,196, 외부 패키지·잠금 파일 531 (실측,
  전체의 45%). 테스트 제외로 "이 클래스의 테스트 찾기" 질의는 포기한다 — 원래 grep이 더 싼
  질의라 손실이 미미하다(사용자 승인, 2026-08-28).

- **뽑는 엣지** — 카드→상태(적용/소모/발동), 풀·덱→카드, 캐릭터→덱, 카드→효과 kind,
  효과 kind→핸들러 클래스(코드 노드), 상태→행동 클래스(코드 노드). 마지막 두 다리가
  "이 핸들러를 고치면 어느 카드가 영향받나"를 양방향으로 답하게 한다.

- **확장 축** — 갈아끼울 수 있는 것: 엣지 추출 규칙(새 JSON 필드·새 콘텐츠 종류마다 규칙 추가),
  뷰 필터. 한번 정하면 고정되는 것: 노드 ID 규약(카드 id 기반), graphify graph.json 포맷 의존.

- **대안과 기각 이유**
  - 유료 시맨틱 추출로 JSON 커버: 회당 25만~40만 토큰인데 카드는 밸런스 튜닝으로 자주 바뀜.
    AGENTS.md 규칙 23이 이미 100배 적자로 판정한 경로.
  - markdown 카드 도감 생성: 관계 질의와 시각화가 안 됨. 요청의 본질(관계망)을 못 채움.
  - 자체 HTML 렌더러 작성: 불필요 확인됨 — `graphify cluster-only <dir> --no-label`이 임의
    graph.json에서 graph.html을 LLM 없이 생성함을 합성 그래프로 실증했다(2026-08-28).

- **이 선택으로 나중에 어려워지는 것**
  - graphify를 업그레이드하면 graph.json 포맷·병합 동작이 바뀔 수 있고, 그때 추출기·접기
    스크립트가 따라가야 한다.
  - kind→핸들러, 상태→행동 매핑이 코어 파일의 현재 작성 규약(`Key => EffectKeys.X`,
    `Key => StatusKeys.X`)에 묶인다. 그 규약을 리팩토링하면 추출기 파싱도 고쳐야 한다.
  - 추출기는 아는 필드만 엣지로 만든다. 조건부 효과 같은 새 JSON 구조가 생기면 규칙을 추가하기
    전까지 그 관계는 그래프에서 침묵으로 빠진다 — 미인식 필드 경고(상세 §검증)로 침묵을 잡는다.

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.
아래 파일·줄 인용은 전부 2026-08-28에 직접 읽고 확인한 것이다.

### 배경 실측 (왜 이 설계인가)

- `graphify update .` 재생성 로그가 카드 JSON 48개를 "produced zero nodes"로 명시했다
  (breather.json, crossover.json 등). 무료 AST 추출기는 데이터 JSON에서 노드를 만들지 못한다.
  카드 관계가 그래프에 없는 원인은 환경이 아니라 **추출기 공백**이다.
- 코드 그래프는 5,479노드·11,475엣지·351커뮤니티로 정상 생성되나, HTML viz 한계(5,000노드)를
  넘어 graph.html이 생략된다. 사람 시야가 없는 원인이다.
- 노드 구성 실측(2026-08-28, graph.json 직접 분석): 코드 3,736 + 문서 1,654 + 개념 89.
  코드 3,736의 분해 — 테스트 1,196(113파일), 게임 코어 938(183파일), **참조 스텁 751**
  (`source_file`이 빈 코드 노드: `int`×35, `IReadOnlyList`×74, NUnit `test`×104 등이
  파일마다 복제되고 실제 클래스 노드와 연결되지 않는 막다른 노드), 외부 531
  (`Packages/packages-lock.json` 단독 344, `manifest.json` 54, `Assets/Plugins/` DOTween
  ~133), Unity 표현 293(35파일), 도구 27. 실제 코드 규모는 .cs 339파일·33,719줄.
  진짜 관계망은 스텁 없이 성립한다 — 실제 노드끼리 잇는 파일 간 엣지 4,036개(calls 2,608,
  inherits 92, implements 61, imports 784 포함)가 별도로 존재함을 확인했다.
- 사용자 확인 사항: 카드 질의 목적은 밸런스·시너지 탐색 + 사람 시각화. 코드 측 병목은
  "AI가 안 쓰는 것" + "사람 시야 없음". 시각화는 카드 관계망 소형 + 코드 아키텍처 수준.
  재생성은 훅·watch 없이 **수동 명령 하나**. 태그는 그래프에서 **완전 제외**(사람이 임의로
  쓰는 데이터라 부정확할 수 있음 — 사용자 판단).

### 입력 데이터 형태 (실측)

- 카드: `Assets/StreamingAssets/Content/Cards/*.json`. 예 `venom_thrust.json` — `id`, `name`,
  `side`, `category`, `energyCost`, `baseExecutionOrder`, `effects[]`, `grade`, `tags[]`.
  effect 항목: `kind`, `value`, `selector`, `status`, `count`, `target`.
- 상태: `Content/Statuses/*.json` 11개. 예 `poison.json` — `key`, `displayName`, `lifetime`,
  `growthPerTurn`. 상태 JSON은 다른 상태를 참조하지 않는다(플랫). 상태 간 전이(잠복독→독 등)는
  코드 스펙에 있다: `Assets/Core/Authoring/Statuses/Specs/PoisonStatusSpec.cs`.
- 풀: `Content/Pools/starter.json` — `id`, `cards[]` (카드 id 문자열 배열).
- 덱: `Content/Decks/*.json` — 풀과 같은 모양(`id`, `cards[]`). 역할 차이는 풀=캐릭터 소유
  오리지널, 덱=런 중 커스텀 결과.
- 캐릭터: `Content/Characters/member_a.json` — `id`, `displayName`, `deck` (덱 id 참조).
- 효과 kind 전집: `Assets/Core/Effects/EffectKey.cs` L22-34 `EffectKeys` 정적 필드 8개
  (`damage`, `apply_status`, `consume_status`, `trigger_status`, `move_formation`,
  `grant_next_turn_fate`, `nullify_next_player_condition_reward`,
  `grant_next_player_damage_card_bonus`).
- kind→핸들러 매핑 근거: 각 핸들러가 `public EffectKey Key => EffectKeys.Damage;` 패턴을 갖는다
  (`Assets/Core/Effects/DamageHandler.cs` L17). 등록부는
  `Assets/Core/Registries/CombatRegistries.cs` L14-21.
- 태그는 `Assets/Core/Authoring/CardSpec.cs` L46에 데이터로 실리지만 `Assets/Core/Combat`에서
  참조 0건(grep 실측) — 순수 사람 메모라서 제외해도 규칙 정보 손실이 없다.

### graphify graph.json 포맷 (실측)

최상위 키: `directed`(false), `multigraph`, `graph`, `nodes`(list), `links`, `hyperedges`,
`built_at_commit`. 노드 필드: `id`, `label`, `file_type`, `source_file`, `source_location`,
`_origin`, `community`, `community_name`, `norm_label`, (선택) `metadata`. 엣지(`links`) 필드:
`relation`, `confidence`("EXTRACTED"), `confidence_score`(1.0), `source_file`,
`source_location`, `weight`, `_origin`, `source`, `target`. AST 노드 id 규약은
소문자·언더스코어 경로 기반(예 `assets_core_authoring_statuses_specs_poisonstatusspec`).

### 객체별 상세

**1. 카드 그래프 추출기** — `tools/graph/extract_card_graph.py` (Python 3 stdlib 전용, 규칙 14
준수·의존성 0).

- 입력: `Assets/StreamingAssets/Content/{Cards,Statuses,Pools,Decks,Characters}/*.json`,
  `Assets/Core/Effects/EffectKey.cs`, `Assets/Core/Effects/*Handler.cs`,
  `Assets/Core/Status/StatusKey.cs`(L24-34 정적 필드, EffectKeys와 동일 패턴),
  `Assets/Core/Status/*Behavior.cs`, `graphify-out/graph.json`(AST 결과, 코드 노드 id 조회용).
- 출력: `graphify-out/card-graph.json` (위 graph.json 포맷 준수, `_origin: "card_extractor"`).
- 노드: 카드(`card:<id>`, label=name), 상태(`status:<key>`, label=displayName), 풀·덱·캐릭터,
  효과 kind(`effect_kind:<id>`). `file_type`은 전부 `"concept"`(추출 스펙의 허용 6종 중 하나),
  `source_file`은 원본 JSON 경로.
- 엣지(전부 EXTRACTED·1.0):
  - `applies_status`: effect가 `kind=apply_status`이고 `status` 필드가 있을 때 카드→상태.
  - `consumes_status` / `triggers_status`: `consume_status`·`trigger_status` 동형.
  - `uses_effect`: 카드→효과 kind (effects[].kind마다).
  - `contains_card`: 풀·덱→카드 (cards[]마다).
  - `owns_deck`: 캐릭터→덱 (`deck` 필드).
  - `handled_by`: 효과 kind→핸들러 클래스 코드 노드. 매핑은 EffectKey.cs에서
    필드명→문자열 id를 파싱한 뒤, `*Handler.cs`에서 `Key => EffectKeys.<필드명>` 정규식으로
    클래스명을 얻고, AST graph.json에서 그 클래스 노드 id를 label+source_file로 찾는다.
  - `handled_by`(상태): 상태→행동 클래스 코드 노드. `Assets/Core/Status/StatusKey.cs`의
    정적 필드(필드명→문자열 key)와 `*Behavior.cs`의 `Key => StatusKeys.<필드명>` 패턴으로
    효과 핸들러와 동일하게 파싱한다. (당초 `<Pascal(key)>StatusSpec` 규약으로 설계했으나
    2026-08-28 확인 결과 스펙 클래스는 3개를 11개 상태가 공유해 1:1 매핑이 성립하지 않았다.
    상태의 게임 로직은 행동 클래스가 담당하므로 다리도 행동 클래스로 잇는다 —
    `StatusSpecJsonConverter.cs` L10-11, L48-49 주석이 근거.)
- 태그(`tags[]`)는 읽되 무시한다. 노드·엣지를 만들지 않는다.

**2. 그래프 프루너** — `tools/graph/prune_graph.py` (stdlib 전용). graph.json에서 다음 노드와
그 노드에 닿는 모든 엣지·하이퍼엣지를 제거한다 (2026-08-28 사용자 지시: 테스트 코드 불포함,
중요한 것은 게임 로직 아키텍처):

  - 참조 스텁: `file_type=code`이고 `source_file`이 빈 노드 (실측 751개)
  - 테스트: `source_file`에 `/Tests/`가 포함되거나 파일명이 `*Tests.cs`·`*Test.cs`인 노드
    (실측 1,196개)
  - 외부: `source_file`이 `Packages/`·`Assets/Plugins/`로 시작하는 노드 (실측 531개)

  결과는 게임 로직 1,231 + 문서 1,743 ≈ 3,000노드. 트레이드오프: "이 클래스의 테스트 찾기"
  질의를 포기한다 — grep이 더 싼 질의라 손실 미미(사용자 승인). 제거 규칙은 함수 하나에 모아
  새 잡음 유형이 나타나면 한 곳만 고치게 한다.

**3. 재생성 스크립트** — `tools/graph/rebuild-graph.sh` (조정자, 로직 없음. 규칙 30).

  1. `rm -f graphify-out/graph.json graphify-out/manifest.json`
  2. `graphify update .` (AST 재생성, 실측 8.7초·LLM 0토큰)
  3. `python3 tools/graph/prune_graph.py` (in-place)
  4. `python3 tools/graph/extract_card_graph.py` — card-graph.json을 쓰고 **직접 graph.json에
     append-merge한다.** `graphify merge-graphs`는 쓰지 않는다: 크로스 저장소 병합용이라 모든
     노드에 `repo::` 접두사를 붙여 카드→핸들러 엣지가 진짜 AST 노드와 연결되지 않음을 합성
     그래프로 실증했다(2026-08-28). 직접 병합은 ID 충돌이 없고(`card:` 접두사 계열), 재실행 시
     `_origin=card_extractor`인 기존 노드·엣지를 먼저 제거해 멱등이다.
  5. 뷰 2장 생성 (아래 4·5).

  AGENTS.md 규칙 21의 재생성 명령을 이 스크립트 한 줄로 교체한다.

**4. 뷰 생성기** — 카드 서브그래프(card-graph.json + handled_by가 가리키는 코드 노드)만 담은
단독 HTML 한 장. `graphify-out/card-graph.html`. 규모 ~60노드(카드 27 + 상태 11 +
kind 8 + 풀·덱·캐릭터 ~7 + 핸들러·스펙 ~12)라 즉시 열린다.

- 렌더 경로(갈림길 해소됨, 2026-08-28 실증): 임시 디렉터리 `<tmp>/graphify-out/graph.json`에
  서브그래프를 놓고 `graphify cluster-only <tmp> --no-label`을 실행하면 graph.html이 LLM 없이
  생성된다(합성 6노드 그래프로 확인). 생성된 graph.html을 목적 경로로 복사한다.

**5. 아키텍처 접기** — `tools/graph/collapse_architecture.py`. 프루닝된 코드 그래프의 노드를
`community` 값으로 묶어 커뮤니티당 노드 1개(label=community_name, 크기=멤버 수), 커뮤니티 간
엣지 수를 weight로 하는 소형 그래프를 만들어 4와 같은 경로로
`graphify-out/architecture.html`로 렌더한다. 문서 노드는 접기 대상에서 제외하고 게임 로직
코드만 접는다.

**6. AGENTS.md 규칙 개정** — 같은 커밋 아님, 구현 완료 후 별도 커밋.

- 규칙 21: 재생성 명령을 `tools/graph/rebuild-graph.sh`로 교체. 카드 서브그래프가 함께
  생성됨을 명시.
- 규칙 22: 갈림길 목록에 카드 질의 항목 추가 — "카드·상태·시너지 관계 질문(어느 카드가 X를
  쌓나/소모하나, 이 핸들러 영향 카드)은 그래프가 1순위다. 엣지가 엔진 실행 데이터에서 나오므로
  태그와 달리 정확하다." 기존의 "기본값은 Read·grep" 서술은 코드 심볼 질의에 한정해 유지.

### 검증

- **미인식 필드 경고:** 추출기는 카드 effect에서 자기가 아는 필드(`kind`, `value`, `selector`,
  `status`, `count`, `target`) 외의 키를 만나면 stderr로 경고한다. 새 JSON 구조가 그래프에서
  침묵으로 빠지는 것을 잡는 장치다.
- **정합성 경고:** 존재하지 않는 상태 key·카드 id·덱 id를 참조하는 JSON, EffectKeys에 없는
  kind, 핸들러를 못 찾은 kind를 전부 경고한다(실패 아님 — 그래프는 만들되 알린다).
- **테스트:** 추출기는 순수 함수형으로 작성해 `python3 -m unittest`로 검증한다(픽스처 JSON →
  기대 노드·엣지). 헤드리스 dotnet 테스트와는 무관한 도구라 규칙 12의 dotnet 경로는 해당 없음.
- **수용 기준:**
  1. `rebuild-graph.sh` 1회 실행으로 graph.json에 카드·상태 노드와 위 엣지 7종이 존재한다.
  2. `graphify explain "맹독 찌르기"`가 poison·damage 관계를 답한다.
  3. `graphify path "PoisonBehavior" "맹독 찌르기"` 류 카드↔코드 경로가 성립한다.
  4. 프루닝 후 graph.json에 스텁·테스트·`Packages/`·`Assets/Plugins/` 노드가 0개다
     (총 노드 ~3,000).
  5. card-graph.html과 architecture.html이 브라우저에서 열린다.
  6. 전 과정 LLM 토큰 0, 벽시계 ~15초 이내.

### 구현 순서 제안

1. 프루너 + 단위 테스트 (독립, 즉시 가치 — 전체 viz 한계 회복)
2. 추출기(직접 병합 포함) + 단위 테스트 (핵심 가치, 프루너와 독립)
3. rebuild-graph.sh (조정자)
4. 뷰 생성기 + 아키텍처 접기 (렌더 경로는 실증된 cluster-only 사용)
5. AGENTS.md 규칙 개정 + docs/superpowers/README.md 색인 갱신
