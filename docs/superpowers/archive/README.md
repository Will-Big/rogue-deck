# Fate Weaver 보관 문서 색인

- 개정일: 2026-07-28
- 상태: `archived`

이 디렉터리의 문서는 완료되었거나 현재 기준에서 대체된 역사 기록이다. 현재 규칙과 활성 계획은
[중앙 문서 색인](../README.md)을 따른다.

## 보관된 설계 스펙

| 분류 | 문서 |
|---|---|
| 초기 UI | [이미지 기반 카드 UI](specs/2026-06-22-ugui-card-ui-design.md) |
| 초기 UI | [덱·손패 플레이테스트 UI](specs/2026-06-23-deck-playtest-ui-design.md) |
| 초기 저작 | [하이브리드 SO 카드 저작](specs/2026-06-23-so-card-authoring-design.md) |
| 초기 규칙 | [핵심 상태이상 시스템](specs/2026-06-24-status-effects-design.md) |
| 초기 아키텍처 | [효과 조합 카드 설명](specs/2026-06-26-card-descriptions-design.md) |
| 입력 UX | [명시적 대상 선택 통합](specs/2026-07-16-unified-target-selection-ux-design.md) |
| 입력 UX | [실행 카드 자동 위치 프리뷰](specs/2026-07-17-execution-placement-preview-design.md) |
| 입력 UX | [다중 대상 선택 토글](specs/2026-07-17-multi-target-toggle-selection-design.md) |
| 입력 UX | [실행 카드 무대상 배치](specs/2026-07-18-execution-card-placement-flow-design.md) |
| 입력 UX | [카드 외곽선과 곡선 비행](specs/2026-07-19-card-outline-and-curved-placement-flight-design.md) |
| 입력 UX | [카드 선택과 배치 모션](specs/2026-07-19-card-selection-placement-motion-design.md) |
| 완료 아키텍처 | [`CardType` 제거](specs/2026-07-19-card-type-removal-design.md) |
| 입력 UX | [배치 비행 카드 플립](specs/2026-07-19-placement-flight-flip-design.md) |
| 재설계 필요 | [과거 런 원 사이클 뼈대](specs/2026-07-20-run-cycle-skeleton-design.md) |
| 완료 저작 UX | [상태 키 드롭다운](specs/2026-07-20-status-key-dropdown-authoring-design.md) |

## 보관된 구현 계획과 기록

### 전투 코어 M0~M5

- [Core Foundation M0–M1](plans/2026-06-18-fate-weaver-core-foundation.md)
- [M2 조건](plans/2026-06-18-fate-weaver-m2-conditions.md)
- [M2.1 조건부 효과](plans/2026-06-18-fate-weaver-m2-conditional-effects.md)
- [M3.1 실행 순서 변경](plans/2026-06-18-fate-weaver-m3-change-initiative.md)
- [M3.2 개입 카드 해결](plans/2026-06-18-fate-weaver-m3-fate-play-resolver.md)
- [M3.3 실행 순서 교환](plans/2026-06-18-fate-weaver-m3-swap-initiative.md)
- [M3.4 잠금](plans/2026-06-18-fate-weaver-m3-lock.md)
- [M4.1 조건 보상 무효](plans/2026-06-18-fate-weaver-m4-reward-nullified.md)
- [M5.1 헤드리스 시나리오](plans/2026-06-18-fate-weaver-m5-headless-scenario.md)
- [M5.2 비교 모드](plans/2026-06-18-fate-weaver-m5-compare-mode.md)
- [M5.3 시나리오 선택](plans/2026-06-18-fate-weaver-m5-scenario-selection.md)

### 덱·저작·상태·설명·적

- [덱 코어 루프 Phase 1](plans/2026-06-22-deck-loop-phase1.md)
- [uGUI 카드 UI](plans/2026-06-22-ugui-card-ui.md)
- [덱·손패 플레이테스트 UI](plans/2026-06-23-deck-playtest-ui.md)
- [SO 카드 저작](plans/2026-06-23-so-card-authoring.md)
- [핵심 상태이상](plans/2026-06-24-status-effects.md)
- [동적 카드 설명](plans/2026-06-26-dynamic-card-descriptions.md)
- [간수 적](plans/2026-06-27-warden-lock-enemy.md)

### 전투 화면·파티·입력 UX

- [전투 화면 골격](plans/2026-07-10-battle-screen-skeleton.md)
- [카드 선택 입력](plans/2026-07-12-card-selection-input.md)
- [파티 기반 전투](plans/2026-07-15-party-foundation.md)
- [파티 카드 선택 통합](plans/2026-07-16-card-selection-party-integration.md)
- [카드 설명 레지스트리](plans/2026-07-16-description-registry.md)
- [명시적 대상 선택 통합](plans/2026-07-16-unified-target-selection-ux.md)
- [실행 카드 위치 프리뷰](plans/2026-07-17-execution-placement-preview.md)
- [다중 대상 선택 토글](plans/2026-07-17-multi-target-toggle-selection.md)
- [실행 카드 무대상 배치](plans/2026-07-18-execution-card-placement-flow.md)
- [카드 외곽선과 곡선 비행](plans/2026-07-19-card-outline-and-curved-placement-flight.md)
- [카드 선택과 배치 모션](plans/2026-07-19-card-selection-placement-motion.md)
- [배치 비행 카드 플립](plans/2026-07-19-placement-flight-flip.md)

### 아키텍처 개선 P0

- [P0-A RNG 단일화 기록](plans/2026-07-18-p0a-rng-unification.md)
- [P0-B 열린 카드 저작 계획](plans/2026-07-19-open-card-authoring.md)
- [P0-B 열린 카드 저작 기록](plans/2026-07-19-p0b-implementation-record.md)
- [P0-B2 `CardType` 제거 계획](plans/2026-07-19-card-type-removal.md)
- [P0-B2 구현 기록](plans/2026-07-19-p0b2-implementation-record.md)
- [상태 키 드롭다운 저작](plans/2026-07-20-status-key-dropdown-authoring.md)

### 전투 시스템 정합성 정리

- [전투 시스템 정합성 정리 (설계 + 구현 계획)](plans/2026-07-25-combat-consistency-cleanup.md)

### P0-C 대상 선택 메타데이터

- [P0-C 구현 계획 (구현 기록 포함)](plans/2026-07-28-p0c-targeting-metadata.md)

### 과거 런 원 사이클

- [런 계획 인덱스](plans/2026-07-20-run-cycle-plan-index.md)
- [런 코어 기반](plans/2026-07-20-run-core-foundation.md)
- [전투 노드](plans/2026-07-20-run-combat-node.md)
- [고용·회복 노드](plans/2026-07-20-run-recruit-heal-node.md)
- [전투 보상](plans/2026-07-20-run-combat-reward.md)
- [런 Unity 흐름](plans/2026-07-20-run-unity-flow.md)

### 문서 거버넌스

- [문서 정리와 중앙 색인 구현 계획](plans/2026-07-24-document-index-cleanup.md)

### 외부 카드 저작 도구

- [카드 아이디어 노트 구현](plans/2026-07-27-card-idea-notebook.md)
- [카드 아이디어 노트 V2](plans/2026-07-27-card-idea-notebook-v2.md)
- [카드 노트 공용 선택](plans/2026-07-28-card-notebook-shared-selection.md)
- [카드 노트 파일명과 순서](plans/2026-07-28-card-notebook-export-ordering.md)
- [카드 노트 자신 대상](plans/2026-07-28-card-notebook-self-target.md)
