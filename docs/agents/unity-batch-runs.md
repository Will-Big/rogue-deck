# Unity 배치 실행

**언제 읽나:** Unity를 `-batchmode`로 돌려 EditMode 테스트나 씬·프리팹·에셋 저작을 검증할 때,
그리고 `Unity Licensing Client 연결 상실`·`Licensing is not yet initialized`로 실행이 멈췄을 때.

AGENTS.md 규칙 17이 씬을 건드린 뒤 `-batchmode` EditMode 회귀 확인을 요구한다. 그 명령과, 그것이
멈췄을 때의 대응이 여기 있다. 도구에 매이지 않는 저장소 문서이며, Claude Code에서는
`.claude/skills/unity-batch-runs/`가 이 파일을 자동으로 물어 온다.

## EditMode 테스트 배치 실행

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath <워크트리 절대 경로> \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/<이름>.xml \
  -logFile /private/tmp/<이름>.log
```

- **`-quit`를 붙이지 않는다.** `-runTests`와 함께 주면 테스트를 실행하지 않고 exit 0으로 즉시
  끝난다 — 결과 XML이 안 생기는데 종료 코드는 정상이라 통과로 오진하기 쉽다(2026-07-29 실측).
  `-runTests`는 테스트가 끝나면 스스로 종료한다.
- **종료 코드만 보지 않는다.** 결과 XML 최상위 `test-run`의
  `result=`/`total=`/`passed=`/`failed=`/`skipped=` 속성을 직접 확인한다.
- `-projectPath`는 **항상 작업 중인 워크트리**를 가리킨다. 메인 체크아웃
  (`/Users/ish/Git/rogue-deck`)은 사용자와 에디터 전용이다(규칙 15).
- 실행이 끝나면 `git status`로 의도한 변경만 스테이징한다. Play·배치는 폰트 아틀라스 같은 런타임
  부산물을 남기고 그것은 소스 변경이 아니다(2026-08-03 `KoreanTMP.asset` 121줄로 실증).
- Unity가 필요 없는 코어 로직은 `Tools/verify.sh`가 10초 안에 답한다. 배치는 씬·프리팹·에셋을
  건드렸을 때만 돌린다.

## "Unity Licensing Client 연결 상실" — 좀비 라이선싱 클라이언트

Unity Hub에서 프로젝트를 열 때 `The connection with the Unity Licensing Client has been lost.`가
뜨거나 `-batchmode` 실행이 `Licensing is not yet initialized`에서 멈추면, 원인은 라이선스·Hub
버전이 아니라 **기동 중 행(hang)에 걸린 라이선싱 클라이언트가 글로벌 뮤텍스를 점유**한 것이다.
2026-07-20, 2026-07-31 두 번 같은 원인으로 확인됐다.

판별:

```bash
pgrep -lf Unity.Licensing.Client
```

Hub 자체 클라이언트(`--namedPipe Unity-LicenseClient-ish --cloudEnvironment production`)는
정상이므로 남겨둔다. 문제는 **에디터 버전 전용 클라이언트**
(`--namedPipe Unity-LicenseClient-ish-<버전>`) 쪽이다. 좀비는 로깅 초기화 전에 멈추므로
`~/Library/Logs/Unity/Unity.Licensing.Client.log`에 **자기 PID 로그를 한 줄도 남기지 않는다.**
로그에 PID가 없는데 `ps`에는 살아 있고, 뒤이어 뜬 클라이언트들이
`Failed to acquire global mutex Unity-LicenseClient-ish-<버전>`을 남기면 확정이다. (확보된 행
스택: 메인 스레드가 부팅 중 `Monitor.Wait`에서 영구 대기. 재발 시 `sample`을 다시 뜰 필요는 없다.)

해결:

```bash
kill <좀비 PID>
```

- 죽인 직후 5~10초는 새 클라이언트도 뮤텍스 획득에 실패할 수 있다. 바로 재시도해 실패했다고
  오진하지 말 것.
- 뮤텍스가 풀렸는지는 클라이언트를 직접 띄워 확인한다. `Failed to acquire` 없이
  `Waiting for a connection`이 찍히면 정상이며, 확인 후 그 프로세스는 반드시 죽인다.

  ```bash
  '/Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents/Helpers/UnityLicensingClient.app/Contents/MacOS/Unity.Licensing.Client' --namedPipe Unity-LicenseClient-ish-<버전> &
  ```

- **이미 행에 걸린 Editor·batchmode 실행은 좀비를 죽여도 회복되지 않는다.** 해당 실행은
  재시작해야 한다.

### 오진 금지 — 다음 둘은 원인이 아니다

- 로그의 `Unsupported protocol version '1.18.1'` **[505] 거부는 정상 동작**이다. Hub은 구버전용
  공용 클라이언트를, Unity 6 계열 Editor는 자기 버전 전용 클라이언트를 따로 띄우는 설계다. 505를
  보고 Hub 업데이트를 권하지 말 것.
- 라이선스 자체는 멀쩡하다. `ULF license activated successfully` / `Found 1 entitlements`가 찍히면
  재로그인·캐시 삭제는 불필요하다.

## 소유 워크트리 판별 (규칙 26)

여러 워크트리가 동시에 `-batchmode`를 돌리므로, 죽이기 전에 그 프로세스가 누구 것인지 본다.

```bash
ps -o pid,command -p <PID> | tr ' ' '\n' | /usr/bin/grep -A1 projectPath
```

`-projectPath`가 가리키는 워크트리가 소유자다. 내 워크트리가 아니면 손대지 않는다 — 무엇을 죽여도
되고 무엇을 보고해야 하는지는 AGENTS.md 규칙 26에 있다.
