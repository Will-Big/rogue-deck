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
17. **Unity 작업은 직접 하되, 눈으로 판단할 것만 사용자에게 맡긴다.** 전용 워크트리에서
    씬·프리팹·ScriptableObject·프로젝트 설정을 **직접 저작한다.** 특히 **관리자 객체**(화면에
    보이지 않고 위치가 무의미하며 역할이 로직뿐인 GameObject)는 제약 없이 만들고 배선한다 —
    컴포넌트 추가, 필드 연결, 빌더 스크립트 실행도 같다. GUI 에디터를 열어야 하면 열고, 배치로
    끝낼 수 있으면 배치로 한다.

    사용자 몫은 **보이는 결과가 기준인 것**뿐이다: 레이아웃·크기·색·연출처럼 눈으로 맞춰야 하는
    저작과, 조작감을 확인하는 Play. 어느 쪽인지 판단이 서지 않으면 손대기 전에 묻는다.

    씬을 건드렸으면 `-batchmode` EditMode로 회귀를 확인한다. **종료 후 `git status`로 의도한
    변경만 스테이징한다** — Play는 폰트 아틀라스 같은 런타임 부산물을 남기고 그것은 소스 변경이
    아니다(2026-08-03 `KoreanTMP.asset` 121줄로 실증). 배치 결과와 로그는 `/private/tmp`에
    저장하며, 모든 Unity 실행은 메인 체크아웃이 아니라 해당 워크트리를 사용한다.
18. **세션을 마칠 때 워킹 트리를 깨끗이 남긴다.** 커밋하거나, 못 하면 stash + 사유 기록. 시드 산출물·씬 파일 같은 생성물을 커밋되지 않은 채 방치하지 않는다.
19. **master 머지는 사용자 승인 후에만 한다.** 머지 전 전체 헤드리스 테스트 통과를 확인하고, 머지가 끝난 작업 브랜치와 워크트리는 정리한다.
20. **문서 색인을 같은 커밋에서 갱신한다.** 새 스펙·계획을 추가하거나 완료·대체할 때
    `docs/superpowers/README.md`를 함께 수정한다. 완료된 계획·구현 기록은 `docs/superpowers/archive/plans/`로
    옮기고, 현행 `specs/`와 `plans/`에는 `current` 또는 `active` 문서만 둔다.

## 코드베이스 탐색 (graphify 지식 그래프)

21. **코드베이스 질문은 아는 이름이 있으면 `graphify explain`부터 조회한다.** 이 저장소에는 코어·Unity 레이어·스펙·플랜을
    한 그래프로 묶은 `graphify-out/`이 커밋되어 있다. 규모와 갱신 시각은 `GRAPH_REPORT.md` 머리말에서 확인한다.
    갈림길은 코드냐 문서냐가 아니라 **노드 이름을 아느냐**다 (2026-07-31 실측):
    - **심볼·문서 노드 이름을 알면** → `graphify explain "<이름>"`. 호출자·메서드·관계를 file:line으로 돌려주며
      grep·파일 통독보다 훨씬 싸다 (실측: 코드 질의당 ~3천, 문서 질의당 ~1.1만 토큰 절약).
    - **개념만 알면** → `graphify query "<질문>"`으로 **진입점 이름만 얻고** 즉시 `explain`으로 들어간다.
      query 결과를 코드 질문의 답으로 삼지 않는다 — 코드↔문서 엣지가 전체의 1.4%뿐이라 자연어 질의는
      문서 노드에 착지한 뒤 코드로 건너가지 못하고, ~2000토큰 예산에서 매번 잘린다.
    - **파일 위치만 필요하면** grep이 더 싸다.
    - `graphify path "<A>" "<B>"`는 문서↔문서 관계(cites/implements)에서만 신뢰한다. 코드↔코드는
      허브 노드(`CombatState` 등)를 경유한 무의미한 경로가 나오기 쉽다.

    `GRAPH_REPORT.md`는 전체 아키텍처를 훑거나 위 명령으로 맥락이 부족할 때만 연다.

22. **코드 그래프 갱신은 post-commit 훅이 자동으로 한다.** `graphify hook install`로 설치되어 있다.
    커밋할 때마다 변경된 코드 파일만 AST 재추출해 `graph.json`·`GRAPH_REPORT.md`를 갱신한다.
    LLM을 쓰지 않아 비용이 없고, 백그라운드로 분리 실행되어 `git commit`은 즉시 반환한다
    (로그: `~/.cache/graphify-rebuild.log`). 끄려면 `GRAPHIFY_SKIP_HOOK=1`.

    훅은 **메인 체크아웃에서만** 동작한다 — 링크된 워크트리에서는 스스로 빠진다
    (`git-dir != git-common-dir`이면 즉시 종료). 그래서 워크트리 작업은 훅의 영향을 받지 않는다.
    워크트리에서 최신 구조가 필요하면 직접 `graphify update .`를 돌린다. 커밋된 `manifest.json`이
    상대 경로 기반이라 다른 워크트리·클론에서도 증분이 그대로 맞물린다 — 전체 리빌드가 필요 없다.

    **`git merge`는 post-commit을 발동시키지 않는다.** `graphify hook install`은 post-commit·post-checkout만
    설치하므로, 워크트리에서 작업해 master로 머지하는 이 저장소의 기본 워크플로에서는 두 경로가 모두 막혀
    그래프가 조용히 낡는다 (2026-07-31에 20커밋 누락으로 실증). 메인 체크아웃의 로컬 `.git/hooks/post-merge`가
    이를 메운다 — post-commit에서 MERGE_HEAD 가드만 제거해 실행하는 래퍼다 (post-merge 시점엔 MERGE_HEAD가
    아직 남아 있어 그대로 exec하면 가드에서 조용히 빠진다). 이 훅은 git 추적이 안 되므로 `graphify hook install`
    재실행·재클론 후에는 사라진다. **그래프가 낡아 보이면** `GRAPH_REPORT.md`의 `Built from commit`을 HEAD와
    대조하고 이 훅의 존재부터 확인한다. 재설치:
    ```sh
    cat > .git/hooks/post-merge <<'EOF'
    #!/bin/sh
    _PC="$(dirname "$0")/post-commit"
    [ -x "$_PC" ] || exit 0
    _T=$(mktemp) || exit 0
    sed '/MERGE_HEAD/d' "$_PC" > "$_T"
    sh "$_T"
    rm -f "$_T"
    EOF
    chmod +x .git/hooks/post-merge
    ```

    브랜치에서 `graph.json`을 커밋해도 된다. `.gitattributes`에 등록된 union merge driver가
    자동 병합하므로 충돌하지 않는다. 다만 union 병합은 양쪽 노드를 합치므로 **코드를 대량 삭제한 뒤에는**
    `graphify update . --force`로 한 번 온전히 재빌드해 사라진 노드를 정리한다.

23. **문서 시맨틱 재추출은 에포크 경계에서만 제안한다.** 훅과 `graphify update .`는 코드(AST)만 본다.
    문서·이미지는 `/graphify --update` 경로라야 반영되고, 이건 서브에이전트를 쓰는 **유료** 작업이다
    (갱신당 약 25만~40만 토큰). 문서가 바뀔 때마다 돌리면 손해다 — 활발히 편집 중인 문서는 세션이 어차피
    직접 읽으므로 그래프의 낡음이 아프지 않고, 그래프가 값을 하는 안정된 코어 스펙은 낡지 않는다.
    기본값은 **갱신하지 않음**이고, 완료된 플랜을 `archive/`로 옮기는 커밋(규칙 20) 이후 문서들이 참조
    자료로 안정화된 시점에만 재추출을 제안한다. 임의로 돌리지 않으며, 돌릴 때는 `.superpowers/`·
    `graphify-out/memory/` 같은 스크래치 산출물을 대상에서 제외한다.

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

## 커밋 메시지

27. **커밋 메시지 제목과 본문은 한국어로 쓴다.** 형식은 `타입(범위): 한국어 제목`이다. 타입과 범위
    (`feat`·`fix`·`docs`·`refactor`·`test`·`chore`, `(ui)`·`(core)`)만 Conventional Commits 규약대로
    영어를 유지하고, **콜론 뒤부터는 한국어로 고정한다.** 제목은 "…한다"로 끝나는 현재형 평서문으로 쓴다.

    ```
    쓴다:     refactor: 카드 SO를 제거하고 카드 원본을 JSON 하나로 만든다
    쓴다:     fix(ui): 실행순서 배지의 가독성을 높인다
    쓰지 않는다: docs: record card frame merge handoff
    쓰지 않는다: fix(ui): improve execution order badge visibility
    ```

    영어 제목은 과거 커밋에 섞여 들어간 것이며 새 커밋에는 쓰지 않는다. 이미 푸시된 히스토리는
    재작성해 고치지 않는다. 브랜치 이름의 접두사·경로(규칙 15)는 이 규칙과 무관하게 영어를 쓴다.
