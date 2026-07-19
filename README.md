# Fate Weaver (working title: Rogue-deck)

턴제 덱빌딩 로그라이크 전투 프로토타입. 카드를 "즉시 사용"하는 대신 **미래 영역(future zone)에 실행 예약**하고, 개입 카드로 **실행 순서 자체를 조작**하는 것이 핵심 재미다.

- 엔진: Unity 6000.5.2f1 (UGUI)
- 규칙 코어: 순수 C# (UnityEngine 무참조, 결정론)
- 상태: 전투 슬라이스 프로토타입 (상용화 목표로 개발 중)
- 공개용 기술 소개: [docs/PORTFOLIO.md](docs/PORTFOLIO.md)

## 게임 개요

매 턴 적의 카드가 미래 영역에 공개되고, 플레이어는 **운명력(fate energy)** 을 소모해 실행 카드를 배치한다. 모든 카드는 실행 순서에 따라 차례로 해결되며, 카드의 **조건부 보상**(예: "첫 번째로 발동하면 피해 8")이 순서에 따라 갈리기 때문에, 개입 카드(앞당김·미룸·자리 교환·잠금)로 순서를 비트는 것이 전략의 중심이 된다. 파티(다인 구성), 상태이상(기절·취약·방어·둔화·가속), 적 아키타입(고블린, 잠금을 쓰는 워든)이 구현되어 있다.

밸런스 원칙: [Fate_Weaver_card_balance_principles_v2.md](Fate_Weaver_card_balance_principles_v2.md)

## 빠른 시작

### Unity에서 플레이

1. Unity 6000.5.2f1로 프로젝트 열기
2. `Assets/Scenes/FateWeaverBattle.unity` 씬 열고 Play
   - 카드 저작 데이터가 깨져 보이면 메뉴 `Fate Weaver/Seed Starter Card Assets` → `Seed Enemy Card Assets` → `Seed Party Prototype Assets` → `Generate Cards from SO` 순서로 재시드

### 헤드리스 테스트 (Unity 불필요)

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

규칙 코어 전체를 Unity 없이 검증한다 (300+ 테스트). `-p:TargetFramework=net5.0`은 로컬 .NET 5 SDK 환경용 오버라이드다.

### CLI 시나리오 시뮬레이터

```bash
dotnet run --project Tools/FateWeaver.Headless -- <시나리오-id>
```

## 아키텍처

```
Assets/Core/            FateWeaver.Core — 순수 C# 규칙 (UnityEngine 무참조, asmdef로 강제)
Assets/Core/Simulation/ FateWeaver.Simulation — 덱/세션/적 정책/저작 스키마/설명 생성/시나리오 러너
Assets/Unity/           표현 레이어 — 컨트롤러, 뷰, ScriptableObject 저작(CardAsset/DeckAsset)
Assets/Core/Tests/      헤드리스로 실행되는 NUnit 테스트 (Unity EditMode와 소스 공유)
Tests/Headless/         dotnet test 하니스 (Unity 컴파일 제약을 LangVersion 9로 프록시)
Tools/FateWeaver.Headless/  CLI 시나리오 리포트
docs/superpowers/       설계 스펙(specs/)과 구현 계획(plans/) — 모든 기능의 결정 기록
```

핵심 설계 원칙 (상세·강제 규칙은 [AGENTS.md](AGENTS.md)):

- **결정론**: 모든 무작위는 `CombatState`의 시드 RNG 하나를 경유한다. 같은 시나리오+시드 = 같은 이벤트 타임라인이며, 이것이 테스트로 고정되어 있다.
- **코어의 출력은 이벤트 타임라인뿐**: UI는 이벤트 시퀀스를 재생한다.
- **레지스트리 확장**: 새 효과/상태/개입 액션 = 핸들러 클래스 1개 + 키 등록. 중앙 switch를 키우지 않는다. 새 효과의 실행·저작·설명·검증이 전부 클래스 단위로 국소화된다.
- **콘텐츠는 ScriptableObject로 저작**: SO에서 순수 C# spec을 생성해 헤드리스 시뮬레이션과 Unity가 같은 스키마를 소비한다. 콘텐츠 값은 golden 서명 테스트로 변질이 방지된다.

전체 설계 배경: [docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md](docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md)

## 개발 워크플로

- 기능/리팩토링은 **브레인스토밍 → 설계 스펙 → 구현 계획 → 태스크별 구현+리뷰** 순서로 진행하며, 산출 문서가 전부 `docs/superpowers/`에 남는다 (스펙 20+, 계획 31+).
- 여러 세션/AI가 병렬로 작업하므로 **워크트리 격리 규칙**을 따른다 — AGENTS.md 규칙 15~18 (메인 체크아웃 브랜치 전환 금지, Unity 검증은 머지 후 메인 폴더에서).
- 새 규칙 로직에는 헤드리스 테스트가 필수다. 밸런스 변경은 Compare 하니스로 비교 검증한다.

## 로드맵

아키텍처 로드맵: [docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md](docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md)

- ✅ P0-A 전투 RNG 단일화 · P0-B 열린 카드 저작 구조
- ⏳ P0-C 대상 선택 메타데이터 → P1-A SO 단일 원본화 → P1-B 프리팹화 → P1-C 튜닝 데이터화 → P2 표현 경계 정리

## 라이선스

© 2026 Sanghak Im. All rights reserved.

상용화를 목표로 하는 비공개 프로젝트다. 코드·에셋·문서의 무단 복제, 배포, 2차 사용을 금한다.
