# Fate Weaver — AI 구현 규칙

이 저장소에서 AI가 코드를 작성·수정할 때 반드시 지켜야 하는 규칙이다.
아키텍처 배경: 게임 규칙은 순수 C# 코어(`FateWeaver.Core`, UnityEngine 미참조)에, 표현·저작은 Unity 레이어에 둔다. 상세는 [`docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md`](docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md) 참고.

## Unity 레이어

1. **객체를 즉석에서 만들지 않는다.** 프리팹으로 저장하여 재사용한다.
2. **참조와 파일 경로를 하드코딩하지 않는다.** 인스펙터에서 할당하거나 프리팹을 사용한다.
3. **런타임 문자열 탐색을 금지한다.** `GameObject.Find`, `FindObjectOfType`, 태그·레이어 이름 문자열 비교, `Resources.Load("magic/string")` 모두 사용하지 않는다.
4. **`public` 필드 대신 `[SerializeField] private`을 쓴다.** 인스펙터 노출과 캡슐화를 분리한다.
5. **콘텐츠는 코드가 아니라 ScriptableObject로 저작한다.** 새 카드·적을 C# 상수로 박지 말고 SO 에셋으로 만들어 로드 시 코어 데이터로 변환한다 (기존 SO 카드 저작 파이프라인을 따른다).

## 코어 (규칙 레이어)

6. **`FateWeaver.Core`에서 UnityEngine을 참조하지 않는다.** asmdef(`noEngineReferences`)가 컴파일로 막지만, 우회(리플렉션, 코드의 어셈블리 이동)도 금지다. 게임 규칙은 반드시 코어에, 표현은 Unity 레이어에 둔다.
7. **결정론을 보호한다.** 모든 무작위는 `CombatState`의 시드 RNG를 경유한다. 규칙 로직에 `System.Random` 즉석 생성, `DateTime`, `Guid.NewGuid()` 사용 금지. "같은 시나리오+시드 = 같은 타임라인" 테스트를 깨는 변경 금지.
8. **튜닝 수치를 하드코딩하지 않는다.** 운명력, 드로우 수, HP 같은 값은 변수/데이터로 둔다. 어디에도 매직 넘버를 박지 않는다.
9. **확장은 레지스트리로 한다.** 새 효과/상태 이상/개입 액션 = 핸들러 클래스 1개 + 키 등록. 중앙 switch를 키우지 않는다. 키는 타입 안전 래퍼를 쓰고 부팅 검증에 등록한다.
10. **카드 설명을 하드코딩하지 않는다.** 설명은 EffectData에서 컴포저가 자동 생성한다.
11. **코어의 출력은 이벤트 타임라인뿐이다.** UI·연출은 이벤트 시퀀스를 재생하며, 해석 도중 코어 내부 상태를 직접 뒤지지 않는다.

## 작업 방식

12. **새 규칙 로직에는 헤드리스 테스트가 필수다.** Unity 에디터 없이 `dotnet test`로 검증 가능해야 한다. 밸런스 관련 변경은 Compare 하니스(무조작 vs 조작 비교)를 활용한다.
13. **새 패턴을 발명하기 전에 기존 패턴을 검색한다.** 비슷한 핸들러/상태/조건 구현을 먼저 찾아 그 형태를 따른다. 검색은 규칙 21의 graphify 그래프로 시작한다.
14. **외부 패키지·에셋을 임의로 추가하지 않는다.** 의존성 추가는 사전 승인이 필요하다.

## 병렬 작업 (여러 세션/AI가 동시에 작업하는 저장소다)

15. **메인 체크아웃(`/Users/ish/Git/rogue-deck`)의 브랜치를 전환하지 않는다.** 이 폴더는 사용자와 Unity 에디터 전용이다. 코드 작업은 `git worktree add ../rogue-deck-<작업명> -b <접두사>/<작업명>`으로 만든 전용 워크트리에서 한다. 브랜치 접두사는 `feat/`, `refactor/`, `fix/`를 쓴다.
16. **단순 독립 변경은 `master`에 직접 커밋할 수 있다.** 다른 브랜치·워크트리 작업의 연장선이 아니고 메인 체크아웃이 깨끗하며 사용자 요청 범위가 명확한 경우, Markdown 문서와 코드·Prefab·Scene·ScriptableObject·생성 파일·프로젝트 설정 변경을 동반하지 않는 독립 이미지·오디오 파일은 별도 워크트리나 브랜치 없이 수정할 수 있다. 새 Unity 에셋에는 1:1로 대응하는 `.meta` 파일만 함께 포함할 수 있다. 커밋 전 `git status`를 확인하고 요청받은 경로만 스테이징하며, 조건을 하나라도 충족하지 못하면 전용 워크트리를 사용한다.
17. **Unity 검증 범위를 구분한다.** 전용 워크트리에서는 기본적으로 Unity GUI Editor를 열어 Play·Inspector·시드 메뉴·씬 저작을 수행하지 않는다. 단, 컴파일과 자동화된 RED/GREEN 검증을 위한 Unity `-batchmode` EditMode 테스트는 전용 워크트리에서 실행할 수 있다. 또한 사용자가 병합 전 현재 워크트리 브랜치의 수동 검증을 명시적으로 요청한 경우에는 해당 워크트리를 `-projectPath`로 열어 기존 전투를 Play·Inspector로 확인할 수 있다. 이 예외에서는 시드 메뉴·씬/Prefab/ScriptableObject/프로젝트 설정 저작을 수행하지 않으며, 종료 후 `git status`로 생성 파일과 의도하지 않은 변경을 확인해 스테이징하지 않는다. 배치 테스트 결과와 로그는 `/private/tmp`에 저장하며, 모든 Unity 실행은 메인 체크아웃이 아니라 해당 워크트리를 사용한다.
18. **세션을 마칠 때 워킹 트리를 깨끗이 남긴다.** 커밋하거나, 못 하면 stash + 사유 기록. 시드 산출물·씬 파일 같은 생성물을 커밋되지 않은 채 방치하지 않는다.
19. **master 머지는 사용자 승인 후에만 한다.** 머지 전 전체 헤드리스 테스트 통과를 확인하고, 머지가 끝난 작업 브랜치와 워크트리는 정리한다.
20. **문서 색인을 같은 커밋에서 갱신한다.** 새 스펙·계획을 추가하거나 완료·대체할 때
    `docs/superpowers/README.md`를 함께 수정한다. 완료된 계획·구현 기록은 `docs/superpowers/archive/plans/`로
    옮기고, 현행 `specs/`와 `plans/`에는 `current` 또는 `active` 문서만 둔다.

## 코드베이스 탐색 (graphify 지식 그래프)

21. **코드베이스 질문은 graphify 그래프를 먼저 조회한다.** 이 저장소에는 코어·Unity 레이어·스펙·플랜을 한 그래프로 묶은
    `graphify-out/`이 커밋되어 있다. 규모와 갱신 시각은 `GRAPH_REPORT.md` 머리말에서 확인한다.
    - `graphify query "<질문>"` — 질문에 걸리는 부분 그래프. 넓은 맥락이 필요할 때의 기본 진입점.
    - `graphify path "<A>" "<B>"` — 두 개념 사이의 최단 경로. "이게 저기까지 어떻게 이어지지"에 쓴다.
    - `graphify explain "<개념>"` — 한 노드 중심의 설명.

    grep이나 파일 전수 읽기보다 먼저 쓴다. 반환되는 부분 그래프가 `GRAPH_REPORT.md` 전문이나 raw grep보다 훨씬 좁다.
    `GRAPH_REPORT.md`는 전체 아키텍처를 훑거나 위 세 명령으로 맥락이 부족할 때만 연다.

22. **코드 그래프 갱신은 post-commit 훅이 자동으로 한다.** `graphify hook install`로 설치되어 있다.
    커밋할 때마다 변경된 코드 파일만 AST 재추출해 `graph.json`·`GRAPH_REPORT.md`를 갱신한다.
    LLM을 쓰지 않아 비용이 없고, 백그라운드로 분리 실행되어 `git commit`은 즉시 반환한다
    (로그: `~/.cache/graphify-rebuild.log`). 끄려면 `GRAPHIFY_SKIP_HOOK=1`.

    훅은 **메인 체크아웃에서만** 동작한다 — 링크된 워크트리에서는 스스로 빠진다
    (`git-dir != git-common-dir`이면 즉시 종료). 그래서 워크트리 작업은 훅의 영향을 받지 않는다.
    워크트리에서 최신 구조가 필요하면 직접 `graphify update .`를 돌린다. 커밋된 `manifest.json`이
    상대 경로 기반이라 다른 워크트리·클론에서도 증분이 그대로 맞물린다 — 전체 리빌드가 필요 없다.

    브랜치에서 `graph.json`을 커밋해도 된다. `.gitattributes`에 등록된 union merge driver가
    자동 병합하므로 충돌하지 않는다. 다만 union 병합은 양쪽 노드를 합치므로 **코드를 대량 삭제한 뒤에는**
    `graphify update . --force`로 한 번 온전히 재빌드해 사라진 노드를 정리한다.

23. **문서·스펙이 바뀌면 시맨틱 재추출을 사용자에게 제안한다.** 훅과 `graphify update .`는 코드(AST)만 본다.
    문서·이미지는 `/graphify --update` 경로라야 반영되고, 이건 서브에이전트를 쓰는 **유료** 작업이다.
    임의로 돌리지 말고 제안만 한다.

24. **`graphify-out/cache/semantic/`은 커밋 대상이다.** 여기만 유료 산출물이다(문서·이미지 추출 2.28M 토큰).
    저장소 상대 경로만 담고 있어 이식 가능하고, 이게 있으면 누가 클론하든 전체 재빌드가 무료가 된다.
    반대로 `cache/ast/`·`graph.html`·`cost.json`은 무료로 재생성되거나 머신 로컬이라 `.gitignore`에 있다.
    캐시 키에 graphify의 추출 프롬프트 해시가 들어가므로(`cache/semantic/p<해시>/`), graphify를 올려
    프롬프트가 바뀌면 캐시가 무효화되고 재추출은 유료다.

## Unity 실행 장애 대응

25. **"Unity Licensing Client 연결 상실"은 좀비 라이선싱 클라이언트부터 의심한다.** Unity Hub에서 프로젝트를 열 때
    `The connection with the Unity Licensing Client has been lost.`가 뜨거나 `-batchmode` 실행이
    `Licensing is not yet initialized`에서 멈추면, 원인은 라이선스·Hub 버전이 아니라 **기동 중 행(hang)에 걸린
    라이선싱 클라이언트가 글로벌 뮤텍스를 점유**한 것이다. 2026-07-20, 2026-07-31 두 번 같은 원인으로 확인됐다.

    판별:
    ```bash
    pgrep -lf Unity.Licensing.Client
    ```
    Hub 자체 클라이언트(`--namedPipe Unity-LicenseClient-ish --cloudEnvironment production`)는 정상이므로
    남겨둔다. 문제는 **에디터 버전 전용 클라이언트**(`--namedPipe Unity-LicenseClient-ish-<버전>`) 쪽이다.
    좀비는 로깅 초기화 전에 멈추므로 `~/Library/Logs/Unity/Unity.Licensing.Client.log`에 **자기 PID 로그를
    한 줄도 남기지 않는다.** 로그에 PID가 없는데 `ps`에는 살아 있고, 뒤이어 뜬 클라이언트들이
    `Failed to acquire global mutex Unity-LicenseClient-ish-<버전>`을 남기면 확정이다.
    (확보된 행 스택: 메인 스레드가 부팅 중 `Monitor.Wait`에서 영구 대기. 재발 시 `sample`을 다시 뜰 필요는 없다.)

    해결:
    ```bash
    kill <좀비 PID>
    ```
    - 죽인 직후 5~10초는 새 클라이언트도 뮤텍스 획득에 실패할 수 있다. 바로 재시도해 실패했다고 오진하지 말 것.
    - 뮤텍스가 풀렸는지는 클라이언트를 직접 띄워 확인한다. `Failed to acquire` 없이 `Waiting for a connection`이
      찍히면 정상이며, 확인 후 그 프로세스는 반드시 죽인다.
      ```bash
      '/Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents/Helpers/UnityLicensingClient.app/Contents/MacOS/Unity.Licensing.Client' --namedPipe Unity-LicenseClient-ish-<버전> &
      ```
    - **이미 행에 걸린 Editor·batchmode 실행은 좀비를 죽여도 회복되지 않는다.** 해당 실행은 재시작해야 한다.

    오진 금지 — 다음 둘은 원인이 아니다:
    - 로그의 `Unsupported protocol version '1.18.1'` **[505] 거부는 정상 동작**이다. Hub은 구버전용 공용
      클라이언트를, Unity 6 계열 Editor는 자기 버전 전용 클라이언트를 따로 띄우는 설계다. 505를 보고 Hub
      업데이트를 권하지 말 것.
    - 라이선스 자체는 멀쩡하다. `ULF license activated successfully` / `Found 1 entitlements`가 찍히면
      재로그인·캐시 삭제는 불필요하다.

26. **다른 세션의 Unity 프로세스를 죽이지 않는다.** 규칙 15·17대로 여러 워크트리가 동시에 `-batchmode`를 돌린다.
    좀비를 정리할 때는 `ps`의 `-projectPath` 인자로 소유 워크트리를 확인하고, **라이선싱 클라이언트만** 죽인다.
    남의 Editor·batchmode 프로세스가 같은 좀비에 막혀 있더라도 직접 죽이지 말고, 실패 사실과 로그 경로를
    사용자에게 보고해 해당 세션이 재실행하게 한다.
