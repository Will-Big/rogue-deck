# 노트북 저장소 반영·정리 (계획 D) 구현 계획

> **에이전트에게:** 이 계획은 `superpowers:subagent-driven-development` 또는
> `superpowers:executing-plans`로 작업 단위마다 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

- 작성일: 2026-08-11
- 상태: `active`
- 범위: [카드 저작 노트북 JSON 전환](../specs/2026-08-05-card-authoring-json-notebook-design.md) §10.3·§12·§14와
  Markdown 경로 제거, 관련 문서 정리
- 선행: 계획 A(코어)·B(읽기 UI)·C(편집) 완료 (`master` eda06bf)
- 워크트리: `/Users/ish/Git/rogue-deck-notebook-repo-write`, 브랜치 `feat/notebook-repo-write`

---

## 설계 개요 (사람 검수용)

이 절만 읽고 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은 읽지
않아도 된다.

**무엇을 만드나**

계획 C가 만든 미반영 편집분을 저장소 파일로 되돌려 쓴다. 쓰기 직전 재읽기로 외부 변경을 감지해
충돌을 사용자에게 넘기고, diff 요약으로 무엇이 나가는지 확인시킨 뒤 `Content/Cards`·`Content/Pools`의
JSON만 덮어쓴다. 이어서 Markdown 저작 경로 약 3,300줄을 걷어내 노트북을 저장소 저작 하나로 만든다.

**구조**

| 객체 | 책임 (한 줄) | 이 객체가 모르는 것 |
|---|---|---|
| `diffLines(base, current)` | 두 JSON 원문의 줄 단위 차이를 낸다 | 어느 화면에 그리는지, 왜 달라졌는지 |
| `resolveConflict(card, choice)` | 충돌 카드에 `저장소` 또는 `내 것` 하나를 골라 새 모델을 낸다 | 왜 충돌했는지, 누가 눌렀는지 |
| `exportPlan({...})` | 재읽기 결과와 미반영을 받아 쓸 파일 목록·미참조 목록·차단 사유를 낸다 | 파일 시스템, 권한, DOM |
| `repairRawEntry(text, schema, scope)` | 읽기 오류 파일의 고친 원문을 다시 파싱해 모델로 승격시킨다 | 그 원문이 어디서 왔는지 |
| `migrateLegacyStore(storage)` | 옛 `STORAGE_KEY` 값을 백업 키로 한 번 옮긴다 | 옛 데이터의 의미 |
| 쓰기 게이트 (UI) | 재읽기 → 계획 계산 → 요약 확인 → 파일 쓰기 순서를 잡는다 | 무엇이 바뀌었는지의 판정 |

**의존 방향**

`쓰기 게이트(UI) → exportPlan · diffLines · resolveConflict · repairRawEntry (코어 순수 함수) → 없음`

파일 시스템 접근은 게이트에만 있고 코어는 문자열만 다룬다. 계획 A~C가 잡은 경계 그대로다.

**확장 축**

- 갈아끼울 수 있는 것: 쓰기 대상 폴더(`CONTENT_PATHS`), diff 표현, 충돌 해결 선택지.
- 한번 정하면 고정되는 것: **노트북은 파일을 지우지 않는다**(§12). 삭제가 필요해지면 Unity `.meta`
  고아 문제부터 다시 설계해야 한다.

**대안과 기각 이유**

- *충돌 자동 병합* — 기각. 카드 한 장이 작아 통째로 고르는 편이 명확하고, 병합은 어느 쪽도
  의도하지 않은 카드를 만든다(§10.3).
- *Markdown 경로를 남긴 채 쓰기만 추가* — 기각. `SCHEMA_VERSION`·`STORAGE_KEY`를 Markdown 경로가
  쓰고 있어 버전을 올리는 순간 그 자리가 깨진다. 지우는 것이 마이그레이션의 선행이다.
- *쓰기 시점에 `read` → `readwrite` 승격* — 기각. 권한 요청이 사용자 제스처를 요구해 내보내기
  흐름이 두 번 끊긴다. 폴더 연결부터 `readwrite`로 연다.
- *"저장소에만 있음" 한 줄* — 기각. 읽기 오류 파일은 고쳐야 하고 미참조 파일은 지워도 되는데,
  한 줄로 묶으면 같은 문구가 반대 조언을 한다. 두 목록으로 가른다.

**이 선택으로 나중에 어려워지는 것**

1. **파일을 지우지 못한다.** 카드 id를 바꾸면 옛 파일이 저장소에 남고 노트북은 이름만 알려 준다.
   id 변경이 잦아지면 손으로 지우는 일이 쌓인다.
2. **옛 Markdown 아이디어 카드를 노트북에서 다시 열 수 없다.** 백업 키의 JSON을 손으로 봐야 한다.
   `시작 카드 풀.md`의 22장은 이미 JSON에 있어 안전하지만, `적 타입 A.md`의 적 카드 7장은 노트북
   밖의 메모로 남는다.
3. **`readwrite` 폴더 권한을 노트북이 항상 쥔다.** 저장소 전체에 대한 쓰기 권한이라, 버그가 카드
   외 파일을 건드릴 여지가 원리상 열린다 — 쓰기 경로를 `Content/Cards`·`Content/Pools`로 좁히는
   것으로만 막는다.
4. **읽기 오류 하나가 저작 전체를 막는다.** 원문 편집으로 노트북 안에서 고칠 수는 있지만, 폼이
   아니라 JSON을 직접 만지는 일이라 저작 난이도가 그 순간만 올라간다.

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.

**목표:** 노트북의 미반영 편집분을 저장소 JSON으로 안전하게 쓰고, Markdown 저작 경로를 제거한다.

**접근:** 판정은 전부 코어의 순수 함수로 만들어 `node --test`가 잠그고, 파일 시스템·권한·DOM은
UI 스크립트에만 둔다. 작업 1~5가 쓰기를 완성하고, 작업 6이 Markdown을 지우며, 작업 7이 그 위에서
버전을 올리고, 작업 8이 문서를 맞춘다.

**기술 스택:** 단일 파일 `Tools/card-idea-notebook/index.html`(코어 스크립트 + 저장소 UI 스크립트),
테스트 `Tools/card-idea-notebook/index.test.mjs`(Node `node:test`), File System Access API
(Chrome·Edge 전용), `localStorage`, IndexedDB(폴더 핸들 기억).

### 전역 제약

- **모든 커밋 메시지는 한국어다.** 형식 `타입(범위): 한국어 제목`, 제목은 "…한다"로 끝난다
  (AGENTS 규칙 27). 이 계획의 커밋 범위는 전부 `(tools)`다.
- **작업은 워크트리 `/Users/ish/Git/rogue-deck-notebook-repo-write`에서만 한다.** 메인 체크아웃
  `/Users/ish/Git/rogue-deck`의 브랜치를 전환하지 않는다 (규칙 15).
- **노트북 테스트 명령에 글로브가 필요하다.** `node --test "Tools/card-idea-notebook/*.test.mjs"`.
  디렉터리를 주면 Node 24가 모듈 경로로 해석해 `MODULE_NOT_FOUND`로 죽는다.
- **헤드리스 테스트에 프레임워크 지정이 필요하다.**
  `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`.
- **코어 스크립트는 DOM을 모른다.** `<script data-card-idea-core>` 안에서 `document`·`window`를
  참조하지 않는다. 테스트가 `new Function("globalThis", …)`으로 그 블록만 떼어 실행하므로 참조하면
  즉시 깨진다(`index.test.mjs:8-23`).
- **새 순수 함수는 `globalThis.CardIdeaNotebook`의 `Object.freeze` 목록에 등록해야** 테스트와 UI가
  볼 수 있다(`index.html:2621`).
- **외부 패키지를 추가하지 않는다** (규칙 14). diff도 직접 구현한다.
- **용어 고정.** 화면과 코드 모두에서 `읽기 오류`(파싱 실패로 못 읽은 파일)와 `미참조 파일`(아무
  카드도 대응하지 않게 된 저장소 파일)을 쓴다. 좌측 필터의 `고아 카드`(어느 풀에도 없는 아군 카드)와
  다른 개념이므로 UI 문구에서 `미참조 파일` / `고아 카드`처럼 뒤 단어를 항상 붙인다.

### 파일 구조

| 파일 | 이 계획에서의 책임 |
|---|---|
| `Tools/card-idea-notebook/index.html` | 코어 스크립트에 순수 함수 5개 추가, 저장소 UI에 쓰기 게이트·충돌 해결·diff 토글·원문 편집 추가, Markdown 마크업·CSS·UI 스크립트·코어 제거 |
| `Tools/card-idea-notebook/index.test.mjs` | 새 순수 함수의 단위 테스트 추가, Markdown 테스트 1,664줄 제거 |
| `Tools/card-idea-notebook/시작 카드 풀.md` | 삭제 |
| `docs/superpowers/specs/2026-07-20-character-card-pools-design.md` | §1 개정(저작 중 풀 공유 허용) |
| `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md` | `archive/specs/`로 이동 |
| `docs/superpowers/README.md` | 색인 갱신 |

`index.html`은 4,618줄로 이미 크지만, 이 계획은 순증이 아니라 **순감**이다(추가 약 700줄, 제거 약
1,800줄). 단일 파일 배포가 이 도구의 전제이므로(스펙 §3) 파일을 쪼개지 않는다.

---

### 작업 1: 원문 diff 코어와 `저장소 ↔ 현재` 토글

**파일**
- 수정: `Tools/card-idea-notebook/index.html` (코어 — `resolvePoolState` 뒤, 약 2,330줄)
- 수정: `Tools/card-idea-notebook/index.html` (저장소 UI — `renderCardDetail`·`renderPoolDetail`,
  약 4,325줄·4,434줄, 마크업 `#repo-source` 근처 약 706~713줄)
- 테스트: `Tools/card-idea-notebook/index.test.mjs`

**인터페이스**
- 소비: `core.writeCardJson(card, schema)`, `core.writePoolJson(pool)` — 이미 있다.
- 생산:
  - `diffLines(base, current)` → `{ same: boolean, rows: Array<{ kind: "same"|"add"|"remove", text: string }> }`
    `base`가 `null`·`undefined`이면 모든 줄이 `add`이고 `same: false`.
  - 마크업 요소 `#repo-source-toggle`(버튼), `#repo-source-diff`(`<pre>`).

- [ ] **1단계: 실패하는 테스트를 쓴다**

`index.test.mjs` 맨 아래에 붙인다.

```js
test("같은 원문은 차이가 없다", () => {
  const core = loadCore();
  const text = "{\n  \"id\": \"a\"\n}\n";

  const diff = core.diffLines(text, text);

  assert.equal(diff.same, true);
  assert.deepEqual(diff.rows.map((row) => row.kind), ["same", "same", "same", "same"]);
});

test("바뀐 줄을 삭제와 추가로 낸다", () => {
  const core = loadCore();
  const base = "{\n  \"id\": \"a\"\n}\n";
  const current = "{\n  \"id\": \"b\"\n}\n";

  const diff = core.diffLines(base, current);

  assert.equal(diff.same, false);
  assert.deepEqual(diff.rows, [
    { kind: "same", text: "{" },
    { kind: "remove", text: "  \"id\": \"a\"" },
    { kind: "add", text: "  \"id\": \"b\"" },
    { kind: "same", text: "}" },
    { kind: "same", text: "" },
  ]);
});

test("줄이 늘면 추가만 낸다", () => {
  const core = loadCore();
  const base = "a\nb\n";
  const current = "a\nx\nb\n";

  const diff = core.diffLines(base, current);

  assert.deepEqual(diff.rows.map((row) => `${row.kind}:${row.text}`), [
    "same:a", "add:x", "same:b", "same:",
  ]);
});

test("저장소에 없던 것은 전부 추가다", () => {
  const core = loadCore();

  const diff = core.diffLines(null, "a\nb\n");

  assert.equal(diff.same, false);
  assert.equal(diff.rows.every((row) => row.kind === "add"), true);
});
```

- [ ] **2단계: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 네 테스트가 `TypeError: core.diffLines is not a function`으로 실패.

- [ ] **3단계: 코어에 `diffLines`를 넣는다**

`index.html`의 `resolvePoolState` 함수가 끝난 직후(`CONTENT_PATHS` 선언 바로 앞)에 넣는다.

```js
    /// LCS 기반 줄 diff. 카드 JSON은 길어야 60줄 남짓이라 O(n*m) 표로 충분하고, 외부 패키지를
    /// 들이지 않는다(규칙 14). 삭제를 추가보다 먼저 내보내 "무엇이 무엇으로 바뀌었나"로 읽힌다.
    function diffLines(base, current) {
      const left = base === null || base === undefined ? [] : String(base).split("\n");
      const right = String(current ?? "").split("\n");

      const table = Array.from({ length: left.length + 1 }, () => new Array(right.length + 1).fill(0));
      for (let i = left.length - 1; i >= 0; i -= 1) {
        for (let j = right.length - 1; j >= 0; j -= 1) {
          table[i][j] = left[i] === right[j]
            ? table[i + 1][j + 1] + 1
            : Math.max(table[i + 1][j], table[i][j + 1]);
        }
      }

      const rows = [];
      let i = 0;
      let j = 0;
      while (i < left.length && j < right.length) {
        if (left[i] === right[j]) {
          rows.push({ kind: "same", text: left[i] });
          i += 1;
          j += 1;
        } else if (table[i + 1][j] >= table[i][j + 1]) {
          rows.push({ kind: "remove", text: left[i] });
          i += 1;
        } else {
          rows.push({ kind: "add", text: right[j] });
          j += 1;
        }
      }
      while (i < left.length) {
        rows.push({ kind: "remove", text: left[i] });
        i += 1;
      }
      while (j < right.length) {
        rows.push({ kind: "add", text: right[j] });
        j += 1;
      }

      return { same: rows.every((row) => row.kind === "same"), rows };
    }
```

`globalThis.CardIdeaNotebook` 목록의 `resolvePoolState` 다음 줄에 `diffLines,`를 넣는다.

- [ ] **4단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 172 pass / 0 fail.

- [ ] **5단계: 토글 마크업과 스타일을 넣는다**

`index.html`의 우측 창(`repo-source-title` 블록, 약 707~712줄)을 이렇게 바꾼다.

```html
        <div class="pane-header">
          <div class="pane-title"><strong id="repo-source-title">JSON 원문</strong></div>
          <button type="button" id="repo-source-toggle" hidden>저장소 ↔ 현재</button>
        </div>
        <pre class="json-preview" id="repo-source"></pre>
        <pre class="json-preview" id="repo-source-diff" hidden></pre>
```

CSS의 `.markdown-preview`(318줄)를 `.json-preview`로 바꾸고, 그 규칙 바로 아래에 diff 색을 넣는다.
`#markdown-preview`(648줄)는 작업 6이 마크업째 지우므로 지금은 클래스 이름만 함께 바꾼다.

```css
    .diff-row { display: block; white-space: pre-wrap; }
    .diff-row.is-add { color: #9ede9e; background: rgba(80, 160, 80, .12); }
    .diff-row.is-remove { color: #e8a0a0; background: rgba(180, 70, 70, .12); }
    .diff-row.is-same { color: #8a8a80; }
```

- [ ] **6단계: 마크업 존재 테스트를 쓴다**

```js
test("저장소 ↔ 현재 토글 자리가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");

  assert.match(html, /id="repo-source-toggle"/);
  assert.match(html, /id="repo-source-diff"/);
  assert.match(html, /\.diff-row\.is-add/);
  assert.match(html, /\.diff-row\.is-remove/);
});
```

- [ ] **7단계: UI에 토글을 배선한다**

`elements` 객체(약 3,502줄)에 두 줄을 더한다.

```js
      sourceToggle: byId("repo-source-toggle"),
      sourceDiff: byId("repo-source-diff"),
```

`selection` 선언 근처에 상태 하나를 더한다.

```js
    let showDiff = false;
```

`renderCardDetail`·`renderPoolDetail`이 우측을 직접 채우던 두 줄을 공통 함수로 뺀다.
`renderCardDetail`의 다음 두 줄을 지우고

```js
      elements.sourceTitle.textContent = card.id ? `Cards/${card.id}.json` : "Cards/(id 없음).json";
      elements.source.textContent = card.base ?? core.writeCardJson(card, repoStore.schema);
```

아래 호출로 바꾼다.

```js
      renderSource({
        title: card.id ? `Cards/${card.id}.json` : "Cards/(id 없음).json",
        stored: repoStore.storedCardText.get(card.uid) ?? null,
        current: core.writeCardJson(card, repoStore.schema),
      });
```

`renderPoolDetail`에서 우측을 채우는 두 줄도 같은 형태로 바꾼다.

```js
      renderSource({
        title: `Pools/${pool.id}.json`,
        stored: repoStore.storedPoolText.get(pool.id) ?? null,
        current: core.writePoolJson(pool),
      });
```

`renderCardDetail` 앞에 `renderSource`를 넣는다.

```js
    /// 우측은 언제나 "지금 쓰면 나올 원문"을 보여준다. 저장소와 다를 때만 토글이 나오고,
    /// 토글을 켜면 diff가 원문 자리를 대신한다 - 둘을 동시에 띄우면 어느 쪽이 나갈 것인지
    /// 헷갈린다(설계 11.3).
    function renderSource({ title, stored, current }) {
      elements.sourceTitle.textContent = title;
      elements.source.textContent = current;

      const diff = core.diffLines(stored, current);
      elements.sourceToggle.hidden = diff.same;
      if (diff.same) showDiff = false;

      elements.sourceToggle.classList.toggle("is-active", showDiff);
      elements.source.hidden = showDiff;
      elements.sourceDiff.hidden = !showDiff;
      elements.sourceDiff.replaceChildren();
      if (!showDiff) return;

      for (const row of diff.rows) {
        const line = document.createElement("span");
        line.className = `diff-row is-${row.kind}`;
        line.textContent = `${row.kind === "add" ? "+" : row.kind === "remove" ? "-" : " "} ${row.text}`;
        elements.sourceDiff.append(line);
      }
    }
```

`render()`가 우측을 비우는 부분(약 4,521줄)에 두 줄을 더한다.

```js
      elements.sourceDiff.replaceChildren();
      elements.sourceToggle.hidden = true;
```

이벤트 배선(`elements.reload.addEventListener` 근처)에 붙인다.

```js
    elements.sourceToggle.addEventListener("click", () => {
      showDiff = !showDiff;
      render();
    });
```

- [ ] **8단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 173 pass / 0 fail.

- [ ] **9단계: 커밋한다**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs && git commit -m "feat(tools): 노트북이 저장소와 현재 원문의 차이를 보여준다"
```

---

### 작업 2: 충돌 해결 — `저장소 것으로 되돌리기` / `내 변경 유지`

**파일**
- 수정: `Tools/card-idea-notebook/index.html` (코어 — `diffLines` 뒤)
- 수정: `Tools/card-idea-notebook/index.html` (저장소 UI — `renderCardDetail`·`renderPoolDetail`)
- 테스트: `Tools/card-idea-notebook/index.test.mjs`

**인터페이스**
- 소비: `core.readCardJson(text, schema)`, `core.readPoolJson(text)`, `core.resolveCardState`,
  `core.resolvePoolState`, `core.dropPendingCard`, `core.putPendingCard` — 이미 있다.
- 생산: 없음(코어에는 함수를 더하지 않는다). 충돌 해결은 **기존 함수 둘의 조합**이므로 UI 함수
  `resolveCardConflict(card, choice)` / `resolvePoolConflict(pool, choice)`만 UI 스크립트에 둔다.
  `choice`는 `"stored"` 또는 `"mine"`.

**왜 코어에 새 함수를 두지 않나** — `저장소 것으로 되돌리기`는 `dropPendingCard`(미반영에서 뺀다)와
같고, `내 변경 유지`는 `storedCardText`를 새 원문으로 갈아 끼우는 것과 같다. 둘 다 이미 있는 코어
연산이고, 새 이름을 붙이면 같은 일에 두 개의 이름이 생긴다(규칙 13).

- [ ] **1단계: 실패하는 테스트를 쓴다**

충돌 판정 자체는 이미 `resolveCardState`가 잠근다. 여기서는 **해결 후의 상태**를 잠근다.

```js
test("저장소 것으로 되돌리면 미반영이 사라진다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const storedText = readFileSync(repoCardPath("vanguard_slash"), "utf8");
  const { card } = core.readCardJson(storedText, schema);
  const mine = core.setCardField(card, "name", "내가 고친 이름");
  const outside = storedText.replace(/"energyCost": \d+/, '"energyCost": 9');

  assert.equal(
    core.resolveCardState({ stored: outside, pending: mine, schema }), "conflict");

  const pending = core.dropPendingCard(core.putPendingCard(core.emptyPending(), mine), mine.uid);
  const { card: reread } = core.readCardJson(outside, schema);

  assert.deepEqual(pending.cards, {});
  assert.equal(core.resolveCardState({ stored: outside, pending: reread, schema }), "same");
});

test("내 변경을 유지하면 새 원본 위에서 수정됨이 된다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const storedText = readFileSync(repoCardPath("vanguard_slash"), "utf8");
  const { card } = core.readCardJson(storedText, schema);
  const mine = core.setCardField(card, "name", "내가 고친 이름");
  const outside = storedText.replace(/"energyCost": \d+/, '"energyCost": 9');

  const kept = { ...mine, base: outside };

  assert.equal(core.resolveCardState({ stored: outside, pending: kept, schema }), "modified");
});
```

`repoSchema()`와 `repoCardPath()`는 이미 테스트 파일에 있는 헬퍼다. 없으면 아래를 쓴다.

```js
const repoRoot = new URL("../../", import.meta.url);
const repoCardPath = (id) =>
  fileURLToPath(new URL(`Assets/StreamingAssets/Content/Cards/${id}.json`, repoRoot));
const repoSchema = () => loadCore().parseAuthoringSchema(
  readFileSync(fileURLToPath(new URL("./authoring-schema.json", htmlUrl)), "utf8"));
```

- [ ] **2단계: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 헬퍼가 없으면 `ReferenceError`, 있으면 통과. **통과하면 그대로 두고 3단계로 간다** — 이
테스트는 기존 코어 계약을 잠그는 회귀 테스트이며, 새 코드가 필요한 것은 UI뿐이다.

- [ ] **3단계: 충돌 배너 마크업 스타일을 넣는다**

CSS의 `.repo-readonly` 규칙 근처에 넣는다.

```css
    .repo-conflict {
      display: flex; gap: 8px; align-items: center; flex-wrap: wrap;
      margin: 0 0 10px; padding: 9px 11px;
      border: 1px solid #8a5a2a; border-radius: 6px; background: rgba(160, 100, 40, .14);
      color: #e8c79a; font-size: 12px;
    }
    .repo-conflict strong { color: var(--gold-bright); }
```

- [ ] **4단계: UI에 해결 버튼을 넣는다**

`renderCardDetail` 안, `unknownKeys` 이른 반환 **뒤**이고 폼 생성 **앞**에 넣는다.

```js
      if ((repoStore.cardStates.get(card.uid) ?? "same") === "conflict") {
        elements.detail.append(conflictBanner({
          message: "이 카드를 편집하는 동안 저장소 파일도 바뀌었습니다.",
          onStored: () => revertCardToStored(card),
          onMine: () => keepCardMine(card),
        }));
      }
```

`renderPoolDetail`의 편성 목록 앞에도 같은 형태를 넣는다.

```js
      if ((repoStore.poolStates.get(pool.id) ?? "same") === "conflict") {
        elements.detail.append(conflictBanner({
          message: "이 풀을 편집하는 동안 저장소 파일도 바뀌었습니다.",
          onStored: () => revertPoolToStored(pool),
          onMine: () => keepPoolMine(pool),
        }));
      }
```

`renderCardDetail` 앞에 넷을 넣는다.

```js
    function conflictBanner({ message, onStored, onMine }) {
      const banner = document.createElement("div");
      banner.className = "repo-conflict";

      const label = document.createElement("strong");
      label.textContent = "⚠ 충돌";
      const text = document.createElement("span");
      text.textContent = message;
      banner.append(label, text);

      const stored = document.createElement("button");
      stored.type = "button";
      stored.textContent = "저장소 것으로 되돌리기";
      stored.addEventListener("click", onStored);

      const mine = document.createElement("button");
      mine.type = "button";
      mine.className = "primary";
      mine.textContent = "내 변경 유지";
      mine.addEventListener("click", onMine);

      banner.append(stored, mine);
      return banner;
    }

    /// 되돌리기는 미반영에서 빼는 것과 같다. 저장소 원문은 이미 storedCardText에 새로 들어와
    /// 있으므로, 미반영만 사라지면 화면이 저장소 것을 그대로 보여준다(설계 10.3).
    function revertCardToStored(card) {
      pending = core.dropPendingCard(pending, card.uid);
      persistPending();
      refreshFromPending();
      render();
    }

    /// 유지는 base를 새 저장소 원문으로 갈아 끼우는 것이다. 그래야 충돌이 '수정됨'으로 내려가고,
    /// 우측 diff가 "내 것 ↔ 새 저장소 원문"을 비교한다.
    function keepCardMine(card) {
      const stored = repoStore.storedCardText.get(card.uid) ?? null;
      pending = core.putPendingCard(pending, { ...card, base: stored });
      persistPending();
      refreshFromPending();
      render();
    }

    function revertPoolToStored(pool) {
      pending = core.dropPendingPool(pending, pool.id);
      persistPending();
      refreshFromPending();
      render();
    }

    function keepPoolMine(pool) {
      const stored = repoStore.storedPoolText.get(pool.id) ?? null;
      pending = core.putPendingPool(pending, { ...pool, base: stored });
      persistPending();
      refreshFromPending();
      render();
    }
```

- [ ] **5단계: 마크업 존재 테스트를 쓴다**

```js
test("충돌 해결 버튼이 저장소 UI에 배선된다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const ui = html.match(/<script data-repo-ui>([\s\S]*?)<\/script>/);

  assert.ok(ui, "저장소 UI 스크립트가 있어야 한다");
  assert.match(ui[1], /저장소 것으로 되돌리기/);
  assert.match(ui[1], /내 변경 유지/);
  assert.match(ui[1], /function revertCardToStored/);
  assert.match(ui[1], /function keepCardMine/);
  assert.match(html, /\.repo-conflict/);
});
```

- [ ] **6단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 176 pass / 0 fail.

- [ ] **7단계: 커밋한다**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs && git commit -m "feat(tools): 노트북이 충돌을 저장소와 내 변경 중 하나로 해결한다"
```

---

### 작업 3: 내보내기 계획 코어 — 쓸 파일·미참조·차단 사유

**파일**
- 수정: `Tools/card-idea-notebook/index.html` (코어 — `diffLines` 뒤)
- 테스트: `Tools/card-idea-notebook/index.test.mjs`

**인터페이스**
- 소비: `writeCardJson`, `writePoolJson`, `CONTENT_PATHS` — 이미 있다.
- 생산:

```
exportPlan({ cards, pools, storedCards, storedPools, cardStates, poolStates, readErrors, validation, schema })
  → {
      writes:   Array<{ segments: string[], name: string, text: string }>,
      created:  string[],      // 파일 이름, 오름차순
      updated:  string[],
      unchanged: string[],
      unreferenced: string[],  // 저장소에 있으나 이번에 쓸 것도 읽기 오류도 아닌 파일
      readErrors:   string[],
      blocked:  string[],      // 사유 문장. 비면 내보낼 수 있다
      summaryLine: string,
    }
```

- `readErrors`는 `[{ scope, name, messages }]` 형태 그대로 받는다(현재 `repoStore.quarantined`).
- `segments`는 `CONTENT_PATHS.cards`/`.pools`에 파일 이름을 붙인 배열이다.
- `writes`는 `created`와 `updated`만 담는다. **`unchanged`는 쓰지 않는다** — 내용이 같은데
  덮어쓰면 Unity가 재임포트만 한다.
- `blocked`가 비지 않으면 UI가 쓰기 버튼을 비활성으로 둔다.

- [ ] **1단계: 실패하는 테스트를 쓴다**

```js
function planFixture(core, schema) {
  const storedText = readFileSync(repoCardPath("vanguard_slash"), "utf8");
  const { card } = core.readCardJson(storedText, schema);
  return { storedText, card };
}

test("변경 없는 카드는 쓰지 않는다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);

  const plan = core.exportPlan({
    cards: [card], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[card.uid, "same"]]), poolStates: new Map(),
    readErrors: [], validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.writes, []);
  assert.deepEqual(plan.unchanged, ["vanguard_slash.json"]);
  assert.deepEqual(plan.blocked, []);
});

test("수정된 카드는 갱신으로, 새 카드는 신규로 나간다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);
  const edited = core.setCardField(card, "name", "고친 이름");
  const fresh = core.setCardField(
    core.createCardModel({ schema, uid: core.newUid(1) }), "id", "brand_new");

  const plan = core.exportPlan({
    cards: [edited, fresh], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[edited.uid, "modified"], [fresh.uid, "new"]]),
    poolStates: new Map(), readErrors: [],
    validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.updated, ["vanguard_slash.json"]);
  assert.deepEqual(plan.created, ["brand_new.json"]);
  assert.deepEqual(plan.writes.map((entry) => entry.name),
    ["brand_new.json", "vanguard_slash.json"]);
  assert.deepEqual(plan.writes[0].segments,
    ["Assets", "StreamingAssets", "Content", "Cards", "brand_new.json"]);
});

test("id를 바꾸면 옛 파일이 미참조가 된다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);
  const renamed = core.setCardField(card, "id", "vanguard_cleave");

  const plan = core.exportPlan({
    cards: [renamed], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[renamed.uid, "modified"]]), poolStates: new Map(),
    readErrors: [], validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.created, ["vanguard_cleave.json"]);
  assert.deepEqual(plan.unreferenced, ["vanguard_slash.json"]);
  assert.deepEqual(plan.blocked, []);
});

test("읽기 오류가 있으면 내보내기를 막는다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);

  const plan = core.exportPlan({
    cards: [card], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[card.uid, "same"]]), poolStates: new Map(),
    readErrors: [{ scope: "card", name: "broken.json", messages: ["JSON을 읽을 수 없습니다"] }],
    validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.readErrors, ["broken.json"]);
  assert.deepEqual(plan.unreferenced, []);
  assert.equal(plan.blocked.length, 1);
  assert.match(plan.blocked[0], /읽기 오류 1개/);
});

test("충돌과 검증 오류도 내보내기를 막는다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);

  const plan = core.exportPlan({
    cards: [card], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[card.uid, "conflict"]]), poolStates: new Map(),
    readErrors: [],
    validation: { errors: [{ scope: "pool", id: "starter", message: "없는 카드입니다" }], warnings: [] },
    schema,
  });

  assert.equal(plan.blocked.length, 2);
  assert.match(plan.blocked[0], /충돌 1개/);
  assert.match(plan.blocked[1], /검증 오류 1개/);
});

test("경고는 내보내기를 막지 않는다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);

  const plan = core.exportPlan({
    cards: [card], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[card.uid, "same"]]), poolStates: new Map(),
    readErrors: [],
    validation: { errors: [], warnings: [{ scope: "card", id: "x", message: "어느 풀에도 없습니다." }] },
    schema,
  });

  assert.deepEqual(plan.blocked, []);
});

test("요약 한 줄이 신규·수정·변경 없음을 센다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);
  const edited = core.setCardField(card, "name", "고친 이름");

  const plan = core.exportPlan({
    cards: [edited], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[edited.uid, "modified"]]), poolStates: new Map(),
    readErrors: [], validation: { errors: [], warnings: [] }, schema,
  });

  assert.equal(plan.summaryLine, "신규 0 · 수정 1 · 변경 없음 0");
});

test("풀도 카드와 같은 규칙으로 나간다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const poolText = readFileSync(fileURLToPath(new URL(
    "Assets/StreamingAssets/Content/Pools/starter.json", repoRoot)), "utf8");
  const { pool } = core.readPoolJson(poolText);
  const edited = core.removeFromPool(pool, 0);

  const plan = core.exportPlan({
    cards: [], pools: [edited], storedCards: [], storedPools: [pool],
    cardStates: new Map(), poolStates: new Map([[edited.id, "modified"]]),
    readErrors: [], validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.updated, ["starter.json"]);
  assert.deepEqual(plan.writes[0].segments,
    ["Assets", "StreamingAssets", "Content", "Pools", "starter.json"]);
  assert.equal(plan.writes[0].text, core.writePoolJson(edited));
});
```

- [ ] **2단계: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 여덟 테스트가 `core.exportPlan is not a function`으로 실패.

- [ ] **3단계: 코어에 `exportPlan`을 넣는다**

`diffLines` 바로 뒤에 넣는다.

```js
    /// 내보내기 계획(설계 12). 저장소 파일 이름 집합에서 이번에 쓸 이름을 빼면 남는 것이 둘이다 -
    /// 읽지 못해 제외한 것과, 아무도 대응하지 않게 된 것. 앞은 고쳐야 하고 뒤는 지워도 되므로
    /// 한 줄로 묶지 않는다.
    function exportPlan({
      cards, pools, storedCards, storedPools,
      cardStates, poolStates, readErrors, validation, schema,
    }) {
      const cardFile = (id) => `${id}.json`;
      const states = cardStates ?? new Map();
      const poolStateOf = poolStates ?? new Map();
      const problems = readErrors ?? [];

      const created = [];
      const updated = [];
      const unchanged = [];
      const writes = [];

      const storedCardNames = new Set((storedCards ?? []).map((card) => cardFile(card.id)));
      const storedPoolNames = new Set((storedPools ?? []).map((pool) => cardFile(pool.id)));
      const writtenNames = new Set();

      /// 신규와 수정을 상태가 아니라 '저장소에 그 이름의 파일이 있었나'로 가른다. id를 바꾼
      /// 카드는 상태가 modified지만 파일 이름이 새것이라 신규로 나가야 하고, 옛 이름은 아래에서
      /// 미참조가 된다.
      const record = (name, state, segments, storedNames, text) => {
        writtenNames.add(name);
        if (state === "same") {
          unchanged.push(name);
          return;
        }
        writes.push({ segments: [...segments, name], name, text });
        (storedNames.has(name) ? updated : created).push(name);
      };

      for (const card of cards ?? []) {
        record(
          cardFile(card.id), states.get(card.uid) ?? "same",
          CONTENT_PATHS.cards, storedCardNames, writeCardJson(card, schema));
      }

      for (const pool of pools ?? []) {
        record(
          cardFile(pool.id), poolStateOf.get(pool.id) ?? "same",
          CONTENT_PATHS.pools, storedPoolNames, writePoolJson(pool));
      }

      const errorNames = problems.map((entry) => entry.name);
      const errorNameSet = new Set(errorNames);
      const unreferenced = [...storedCardNames, ...storedPoolNames]
        .filter((name) => !writtenNames.has(name) && !errorNameSet.has(name));

      const blocked = [];
      if (errorNames.length) {
        blocked.push(`읽기 오류 ${errorNames.length}개를 고쳐야 내보낼 수 있습니다.`);
      }
      const conflicts = [...states.values(), ...poolStateOf.values()]
        .filter((state) => state === "conflict").length;
      if (conflicts) blocked.push(`충돌 ${conflicts}개를 해결해야 내보낼 수 있습니다.`);
      const errorCount = (validation?.errors ?? []).length;
      if (errorCount) blocked.push(`검증 오류 ${errorCount}개를 고쳐야 내보낼 수 있습니다.`);

      const sorted = (list) => [...list].sort();
      return {
        writes: writes.sort((a, b) => a.name.localeCompare(b.name)),
        created: sorted(created),
        updated: sorted(updated),
        unchanged: sorted(unchanged),
        unreferenced: sorted(unreferenced),
        readErrors: sorted(errorNames),
        blocked,
        summaryLine:
          `신규 ${created.length} · 수정 ${updated.length} · 변경 없음 ${unchanged.length}`,
      };
    }
```

`globalThis.CardIdeaNotebook` 목록에 `exportPlan,`을 더한다.

- [ ] **4단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 184 pass / 0 fail.

- [ ] **5단계: 커밋한다**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs && git commit -m "feat(tools): 노트북이 내보낼 파일과 차단 사유를 계산한다"
```

---

### 작업 4: 읽기 오류 파일의 원문 편집

**파일**
- 수정: `Tools/card-idea-notebook/index.html` (저장소 UI — `loadRepo`, `renderProblems`,
  `renderCardDetail` 주변)
- 테스트: `Tools/card-idea-notebook/index.test.mjs`

**인터페이스**
- 소비: `core.readCardJson`, `core.readPoolJson` — 이미 있다.
- 생산: `repoStore.readErrors` (기존 `repoStore.quarantined`를 이름과 형태 모두 바꾼 것).
  형태는 `[{ scope, name, messages, text }]` — **`text`가 새로 붙는다.**
  선택 범위에 `"readError"`가 추가된다: `selection = { scope: "readError", id: "<파일 이름>" }`.

- [ ] **1단계: 실패하는 테스트를 쓴다**

```js
test("읽기 오류 원문 편집 자리가 저장소 UI에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const ui = html.match(/<script data-repo-ui>([\s\S]*?)<\/script>/);

  assert.ok(ui);
  assert.match(ui[1], /function renderReadErrorDetail/);
  assert.match(ui[1], /readErrors/);
  assert.doesNotMatch(ui[1], /quarantined/);
  assert.match(html, /\.repo-raw-editor/);
});

test("고친 원문이 파싱되면 카드로 승격한다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const broken = "{ \"id\": \"x\", ";

  assert.equal(core.readCardJson(broken, schema).card, null);

  const fixed = readFileSync(repoCardPath("vanguard_slash"), "utf8");
  const { card, errors } = core.readCardJson(fixed, schema);

  assert.deepEqual(errors, []);
  assert.equal(card.id, "vanguard_slash");
});
```

- [ ] **2단계: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 첫 테스트가 `renderReadErrorDetail` 부재로 실패.

- [ ] **3단계: `quarantined`를 `readErrors`로 바꾸고 원문을 싣는다**

`loadRepo` 안에서 이름을 전부 바꾼다. `readJsonFolder`가 이미 `{ name, text }`를 주므로 파싱 실패
분기에 `text`를 더한다.

```js
      const cardFolder = await readJsonFolder(root, core.CONTENT_PATHS.cards);
      const cards = [];
      for (const file of cardFolder.files) {
        const { card, errors } = core.readCardJson(file.text, schema);
        if (card) cards.push(card);
        else readErrors.push({ scope: "card", name: file.name, messages: errors, text: file.text });
      }
```

상태·풀도 같은 형태로 바꾼다. `readJsonFolder`의 `failures`(파일 자체를 못 연 경우)는 `text`가
없으므로 `text: null`로 넣는다.

```js
      readErrors.push(...statusFolder.failures.map((entry) => ({ ...entry, text: null })),
        ...cardFolder.failures.map((entry) => ({ ...entry, text: null })),
        ...poolFolder.failures.map((entry) => ({ ...entry, text: null })));
```

반환 객체의 `quarantined`를 `readErrors`로 바꾸고, `repoStore` 초기값(약 3,524줄)의
`quarantined: []`도 `readErrors: []`로 바꾼다. `renderProblems`(약 3,874줄)의
`repoStore.quarantined` 순회도 바꾸고, 문구를 `(읽지 못해 격리했습니다)`에서
`(읽지 못했습니다 · 고쳐야 내보낼 수 있습니다)`로 바꾼다.

`problemButton`의 클릭 처리에 읽기 오류 분기를 더한다.

```js
      button.addEventListener("click", () => {
        if (scope === "readError") {
          selectEntry("readError", id);
          return;
        }
        …
      });
```

`renderProblems`가 읽기 오류를 만들 때 `scope: "readError"`를 쓰고, 표시 이름은 원래 `scope`를
문구에 담는다.

```js
      for (const entry of repoStore.readErrors) {
        elements.problems.append(problemButton({
          level: "error",
          scope: "readError",
          id: entry.name,
          message: `${PROBLEM_LABELS[entry.scope] ?? entry.scope} · `
            + `${entry.messages.join(" ")} (읽지 못했습니다 · 고쳐야 내보낼 수 있습니다)`,
        }));
      }
```

`PROBLEM_LABELS`에 `readError: "읽기 오류"`를 더한다.

- [ ] **4단계: 원문 편집기를 그린다**

CSS에 넣는다.

```css
    .repo-raw-editor { display: grid; gap: 8px; }
    .repo-raw-editor textarea {
      min-height: 340px; padding: 10px; border: 1px solid #3a3a33; border-radius: 6px;
      background: #161815; color: #ded9c8;
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; line-height: 1.55;
    }
    .repo-raw-error { margin: 0; color: #e8a0a0; font-size: 12px; }
```

`renderCardDetail` 앞에 넣는다.

```js
    let rawDraft = "";
    let rawError = "";

    /// 읽기 오류 파일은 폼으로 열 수 없다 - 폼이 표현하지 못하는 것을 망가뜨린다(설계 10.2).
    /// 대신 원문을 직접 고치게 하고, 다시 파싱해 성공할 때만 모델로 올린다. 이렇게 하지 않으면
    /// "반드시 고쳐야 내보낼 수 있다"는 규칙이 노트북 밖으로 나가야만 풀리는 막다른 길이 된다.
    function renderReadErrorDetail(entry) {
      elements.detailTitle.textContent = `읽기 오류 · ${entry.name}`;
      renderSource({ title: entry.name, stored: entry.text, current: entry.text ?? "" });

      const box = document.createElement("div");
      box.className = "repo-raw-editor";

      const why = document.createElement("p");
      why.className = "repo-raw-error";
      why.textContent = entry.messages.join(" ");
      box.append(why);

      if (entry.text === null) {
        const note = document.createElement("p");
        note.className = "repo-readonly";
        note.textContent = "파일을 열지 못해 원문을 보여줄 수 없습니다."
          + " 파일 권한을 확인한 뒤 저장소 다시 읽기를 눌러 주세요.";
        box.append(note);
        elements.detail.append(box);
        return;
      }

      const area = document.createElement("textarea");
      area.spellcheck = false;
      area.value = rawDraft || entry.text;
      area.addEventListener("input", () => { rawDraft = area.value; });
      box.append(area);

      if (rawError) {
        const failed = document.createElement("p");
        failed.className = "repo-raw-error";
        failed.textContent = rawError;
        box.append(failed);
      }

      const apply = document.createElement("button");
      apply.type = "button";
      apply.className = "primary";
      apply.textContent = "고친 원문 적용";
      apply.addEventListener("click", () => applyRawRepair(entry, area.value));
      box.append(apply);

      elements.detail.append(box);
    }

    /// 승격에 성공하면 base를 '원래의 깨진 원문'으로 둔다. 그래야 상태가 '수정됨'이 되어
    /// 내보내기에 포함되고, 우측 diff가 무엇을 고쳤는지 보여준다.
    function applyRawRepair(entry, text) {
      if (entry.scope === "pool") {
        const { pool, errors } = core.readPoolJson(text);
        if (!pool) { rawError = errors.join(" "); render(); return; }
        repoStore.storedPoolText.set(pool.id, entry.text);
        repoStore.storedPools = [...repoStore.storedPools, { ...pool, base: entry.text }];
        repoStore.readErrors = repoStore.readErrors.filter((row) => row.name !== entry.name);
        rawDraft = "";
        rawError = "";
        pending = core.putPendingPool(pending, { ...pool, base: entry.text });
        persistPending();
        refreshFromPending();
        selectWithTab("pool", pool.id);
        render();
        return;
      }

      const { card, errors } = core.readCardJson(text, repoStore.schema);
      if (!card) { rawError = errors.join(" "); render(); return; }

      repoStore.storedCardText.set(card.uid, entry.text);
      repoStore.storedCards = [...repoStore.storedCards, { ...card, base: entry.text }];
      repoStore.readErrors = repoStore.readErrors.filter((row) => row.name !== entry.name);
      rawDraft = "";
      rawError = "";
      pending = core.putPendingCard(pending, { ...card, base: entry.text });
      persistPending();
      refreshFromPending();
      selectWithTab("card", card.uid);
      render();
    }
```

`selectWithTab`은 `scope`가 `card`·`pool`이 아니면 탭을 바꾸지 않으므로 `readError` 선택은 그대로
통과한다. `render()`의 상세 분기에 한 줄을 더한다.

```js
      if (selection.scope === "readError") {
        const entry = repoStore.readErrors.find((row) => row.name === selection.id);
        if (entry) renderReadErrorDetail(entry);
      }
```

`selectEntry`가 다른 항목으로 옮길 때 초안을 비운다 — `selectWithTab` 첫 줄에 넣는다.

```js
      if (scope !== "readError" || id !== selection.id) { rawDraft = ""; rawError = ""; }
```

- [ ] **5단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 186 pass / 0 fail.

- [ ] **6단계: 커밋한다**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs && git commit -m "feat(tools): 노트북이 읽기 오류 파일을 원문으로 고친다"
```

---

### 작업 5: 쓰기 게이트 — `readwrite` 권한, 재읽기, 요약 확인, 파일 쓰기

**파일**
- 수정: `Tools/card-idea-notebook/index.html` (마크업 — 헤더 버튼과 새 `<dialog>`, 저장소 UI 스크립트)
- 테스트: `Tools/card-idea-notebook/index.test.mjs`

**인터페이스**
- 소비: `core.exportPlan` (작업 3), `repoStore.readErrors` (작업 4), `loadRepo`, `directoryAt`.
- 생산: 없음(코어 추가 없음). UI 함수 `exportToRepo()`, `writePlanFiles(root, writes)`.

**흐름 (순서를 바꾸지 않는다)**

1. 클릭 즉시 `ensureWritePermission(root, true)` — 사용자 제스처 안에서 권한을 확보한다.
2. `loadRepo(root)`로 **재읽기**(§10.1의 세 번째 읽기 시점). 결과를 `repoStore`에 반영하고
   `refreshFromPending()`으로 상태를 다시 계산한다.
3. `core.exportPlan(...)`으로 계획을 만든다.
4. 다이얼로그를 띄운다. `plan.blocked`가 비지 않으면 사유만 보여주고 확인 버튼을 비활성으로 둔다.
5. 확인하면 `writePlanFiles`가 `plan.writes`를 순서대로 쓴다.
6. 쓴 뒤 다시 `loadRepo` → 쓴 항목의 미반영을 비운다 → `render()`.

- [ ] **1단계: 실패하는 테스트를 쓴다**

```js
test("내보내기 버튼과 요약 다이얼로그가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");

  assert.match(html, /id="repo-export"/);
  assert.match(html, /id="repo-export-dialog"/);
  assert.match(html, /id="repo-export-summary"/);
  assert.match(html, /id="repo-export-confirm"/);
  assert.match(html, /미참조 파일/);
});

test("쓰기 게이트가 재읽기 뒤에 계획을 만든다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const ui = html.match(/<script data-repo-ui>([\s\S]*?)<\/script>/);

  assert.ok(ui);
  assert.match(ui[1], /function exportToRepo/);
  assert.match(ui[1], /function writePlanFiles/);
  assert.match(ui[1], /mode: "readwrite"/);

  // 재읽기가 계획보다 먼저여야 외부 변경을 덮어쓰지 않는다(설계 10.1).
  const body = ui[1].slice(ui[1].indexOf("async function exportToRepo"));
  assert.ok(body.indexOf("await loadRepo") < body.indexOf("core.exportPlan"));
});
```

- [ ] **2단계: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 두 테스트 실패.

- [ ] **3단계: 마크업을 넣는다**

`repo-header-actions`(약 662줄)에 버튼을 더한다.

```html
            <button type="button" id="repo-new-card">＋ 새 카드</button>
            <button type="button" id="repo-export" disabled>저장소에 반영</button>
            <button type="button" class="primary" id="repo-connect">폴더 연결</button>
```

`#delete-dialog` 앞에 다이얼로그를 넣는다.

```html
  <dialog id="repo-export-dialog">
    <div class="dialog-body">
      <h2>저장소에 반영</h2>
      <div id="repo-export-summary"></div>
    </div>
    <div class="dialog-actions">
      <button type="button" class="ghost" data-repo-export-action="cancel">취소</button>
      <button type="button" class="primary" id="repo-export-confirm">반영</button>
    </div>
  </dialog>
```

CSS에 넣는다.

```css
    #repo-export-dialog .dialog-body { min-width: 420px; max-height: 62vh; overflow: auto; }
    .export-line { margin: 0 0 8px; color: var(--gold-bright); font-size: 13px; }
    .export-group { margin: 0 0 10px; }
    .export-group h3 { margin: 0 0 4px; font-size: 12px; color: #cfc9b6; }
    .export-group ul { margin: 0; padding-left: 18px; color: #9a958a; font-size: 12px; }
    .export-blocked { margin: 0 0 8px; color: #e8a0a0; font-size: 12px; }
```

- [ ] **4단계: 권한을 `readwrite`로 올린다**

`hasReadPermission`을 이름과 모드 모두 바꾼다.

```js
    /// 폴더는 처음부터 readwrite로 연다. 쓰기 시점에 승격하면 권한 요청이 사용자 제스처를
    /// 요구해 내보내기 흐름이 두 번 끊긴다. 대신 연결 단계에서 한 번만 묻는다.
    async function hasWritePermission(handle, interactive) {
      const options = { mode: "readwrite" };
      if ((await handle.queryPermission(options)) === "granted") return true;
      if (!interactive) return false;
      return (await handle.requestPermission(options)) === "granted";
    }
```

`connectRepo`의 `showDirectoryPicker`를 바꾼다.

```js
        const root = await window.showDirectoryPicker({
          id: "fateWeaverRepoRoot",
          mode: "readwrite",
        });
```

`restoreRoot`의 `hasReadPermission(root, false)` 호출을 `hasWritePermission(root, false)`로 바꾼다.
`read`로만 기억된 옛 핸들은 여기서 걸러져 "폴더 권한이 만료되었습니다" 안내가 나오고, 사용자가
`폴더 연결`을 다시 눌러 `readwrite`를 준다.

- [ ] **5단계: 쓰기 게이트를 넣는다**

`connectRepo` 뒤에 넣는다.

```js
    /// §12: 노트북은 파일을 지우지 않는다. createWritable은 자르고 다시 쓰므로 덮어쓰기만 한다.
    async function writePlanFiles(root, writes) {
      for (const entry of writes) {
        const folder = await directoryAt(root, entry.segments.slice(0, -1));
        const handle = await folder.getFileHandle(entry.name, { create: true });
        const writable = await handle.createWritable();
        await writable.write(entry.text);
        await writable.close();
      }
    }

    function currentExportPlan() {
      return core.exportPlan({
        cards: repoStore.cards,
        pools: repoStore.pools,
        storedCards: repoStore.storedCards,
        storedPools: repoStore.storedPools,
        cardStates: repoStore.cardStates,
        poolStates: repoStore.poolStates,
        readErrors: repoStore.readErrors,
        validation: repoStore.validation,
        schema: repoStore.schema,
      });
    }

    function fileGroup(title, names, note) {
      if (!names.length) return null;
      const group = document.createElement("div");
      group.className = "export-group";
      const heading = document.createElement("h3");
      heading.textContent = note ? `${title} ${names.length} — ${note}` : `${title} ${names.length}`;
      const list = document.createElement("ul");
      for (const name of names) {
        const item = document.createElement("li");
        item.textContent = name;
        list.append(item);
      }
      group.append(heading, list);
      return group;
    }

    function renderExportSummary(plan) {
      elements.exportSummary.replaceChildren();

      for (const reason of plan.blocked) {
        const line = document.createElement("p");
        line.className = "export-blocked";
        line.textContent = `⚠ ${reason}`;
        elements.exportSummary.append(line);
      }

      const line = document.createElement("p");
      line.className = "export-line";
      line.textContent = plan.summaryLine;
      elements.exportSummary.append(line);

      const groups = [
        fileGroup("신규", plan.created),
        fileGroup("수정", plan.updated),
        fileGroup("읽기 오류", plan.readErrors, "고쳐야 내보낼 수 있습니다"),
        fileGroup("미참조 파일", plan.unreferenced,
          "노트북은 지우지 않습니다. 그대로 두어도 됩니다"),
      ];
      for (const group of groups) if (group) elements.exportSummary.append(group);

      elements.exportConfirm.disabled = plan.blocked.length > 0 || plan.writes.length === 0;
    }

    let plannedExport = null;

    async function exportToRepo() {
      if (!repoStore.root) return;

      // 권한을 먼저 확보한다 - await가 사용자 제스처를 소진하므로 재읽기보다 앞이어야 한다.
      if (!(await hasWritePermission(repoStore.root, true))) {
        repoStore.message = "쓰기 권한을 얻지 못했습니다. 폴더 연결을 다시 눌러 주세요.";
        render();
        return;
      }

      // 내보내기 직전에 다시 읽는다(설계 10.1). 외부 편집을 덮어쓰는 사고를 여기서 막는다.
      try {
        Object.assign(repoStore, await loadRepo(repoStore.root), { connected: true });
      } catch (error) {
        failWith(error);
        return;
      }
      refreshFromPending();
      render();

      plannedExport = currentExportPlan();
      renderExportSummary(plannedExport);
      elements.exportDialog.showModal();
    }

    /// 쓴 파일의 미반영을 비운다. 다시 읽으면 base가 방금 쓴 내용이 되므로 상태가 '저장소와 동일'로
    /// 내려가지만, 미반영을 남겨 두면 localStorage가 영원히 자라고 다음 세션에서 되살아난다.
    async function confirmExport() {
      if (!plannedExport || plannedExport.blocked.length) return;

      elements.exportConfirm.disabled = true;
      try {
        await writePlanFiles(repoStore.root, plannedExport.writes);
      } catch (error) {
        repoStore.message = `저장소에 쓰지 못했습니다: ${error?.message ?? error}`;
        elements.exportDialog.close();
        render();
        return;
      }

      const written = plannedExport.writes.length;
      elements.exportDialog.close();
      plannedExport = null;
      pending = core.emptyPending();
      persistPending();

      try {
        Object.assign(repoStore, await loadRepo(repoStore.root), { connected: true });
        repoStore.message = `${written}개 파일을 저장소에 반영했습니다.`;
      } catch (error) {
        failWith(error);
        return;
      }
      refreshFromPending();
      render();
    }
```

`elements`에 셋을 더한다.

```js
      export: byId("repo-export"),
      exportDialog: byId("repo-export-dialog"),
      exportSummary: byId("repo-export-summary"),
      exportConfirm: byId("repo-export-confirm"),
```

`render()`의 첫 줄 근처에 한 줄을 더한다.

```js
      elements.export.disabled = !repoStore.connected;
```

이벤트를 배선한다.

```js
    elements.export.addEventListener("click", exportToRepo);
    elements.exportConfirm.addEventListener("click", confirmExport);
    elements.exportDialog.addEventListener("click", (event) => {
      if (event.target.closest("[data-repo-export-action]")) {
        plannedExport = null;
        elements.exportDialog.close();
      }
    });
```

- [ ] **6단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 188 pass / 0 fail.

- [ ] **7단계: 사용자에게 눈 검증을 요청한다**

규칙 17에 따라 브라우저 조작 결과는 사용자가 확인한다. 다음을 요청한다.

1. Chrome에서 `Tools/card-idea-notebook/index.html`을 열고 `저장소` 모드로 폴더를 연결한다
   (권한 대화상자에 **편집 허용**이 나와야 한다).
2. 카드 하나의 이름을 고치고 `저장소에 반영` → 요약에 `신규 0 · 수정 1 · 변경 없음 25`가 나오는지.
3. `반영` 후 `git status`에 그 카드 파일 하나만 뜨는지.
4. `git checkout` 으로 되돌린 뒤, 아무것도 고치지 않고 `저장소에 반영` → 확인 버튼이 비활성이고
   `신규 0 · 수정 0 · 변경 없음 26`이 나오는지 (설계 §16 검수 기준 1).

- [ ] **8단계: 커밋한다**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs && git commit -m "feat(tools): 노트북이 편집분을 저장소 JSON으로 반영한다"
```

---

### 작업 6: Markdown 저작 경로 제거

**파일**
- 수정: `Tools/card-idea-notebook/index.html` (마크업 487~488·493~653·717~745줄, CSS, 코어 749~1659줄,
  UI 스크립트 2718~3473줄)
- 수정: `Tools/card-idea-notebook/index.test.mjs` (1~1664줄)
- 삭제: `Tools/card-idea-notebook/시작 카드 풀.md`

**남기는 것**
- `Tools/card-idea-notebook/적 타입 A.md` — 적 카드 7장은 아직 JSON에 없는 아이디어다(§14).
- `SCHEMA_VERSION`·`STORAGE_KEY` 상수 — `HANDLE_DB_NAME`이 `core.STORAGE_KEY`를 이름 공간으로 쓴다.
  값은 작업 7에서 올린다.

**지우는 코어 심볼** (`globalThis.CardIdeaNotebook` 목록 기준, `SAVE_PICKER_ID`부터 `exportStatus`까지
전부와 `ROLE_LABELS`·`FACTION_LABELS`·`GRADE_LABELS`·`TARGETS`):
`SAVE_PICKER_ID` `emptyCard` `normalizeCard` `changeCardFaction` `validateCard` `isCardComplete`
`targetSummary` `cardMarkdown` `bundleMarkdown` `parseBundleMarkdown` `initialState` `writeStore`
`readStore` `readStoreSession` `tryWriteStore` `selectedCards` `cardsForExport` `downloadFileName`
`isMarkdownFileName` `saveFilePickerOptions` `isPickerCancel` `supportsSavePicker` `uniqueCardName`
`createCard` `editCard` `editTargetCards` `isFieldApplicable` `fieldAggregate` `editSelectedField`
`saveCard` `saveAllCards` `importCards` `bulkSelection` `selectCard` `reorderCards` `duplicateCard`
`deletionIds` `deleteCards` `deleteCard` `filteredCards` `exportStatus`

저장소 UI가 쓰는 코어 심볼은 39개이며 위 목록과 겹치지 않는다. 확인 명령:

```bash
awk '/<script data-repo-ui>/,/<\/script>/' Tools/card-idea-notebook/index.html | grep -o "core\.[A-Za-z_]*" | sort -u
```

- [ ] **1단계: 지워질 것을 잠그는 테스트를 쓴다**

`index.test.mjs`의 **맨 아래**에 넣는다(기존 Markdown 테스트는 3단계에서 지운다).

```js
test("Markdown 저작 경로가 남아 있지 않다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");

  assert.doesNotMatch(html, /bundleMarkdown/);
  assert.doesNotMatch(html, /parseBundleMarkdown/);
  assert.doesNotMatch(html, /id="mode-markdown"/);
  assert.doesNotMatch(html, /id="markdown-preview"/);
  assert.doesNotMatch(html, /id="export-dialog"/);
  assert.equal(html.match(/<script/g).length, 2, "코어와 저장소 UI 둘만 남는다");
});

test("시작 카드 풀 Markdown이 지워졌다", () => {
  const legacy = new URL("./시작 카드 풀.md", htmlUrl);

  assert.equal(existsSync(legacy), false);
});

test("적 타입 A 메모는 남는다", () => {
  const memo = new URL("./적 타입 A.md", htmlUrl);

  assert.equal(existsSync(memo), true);
});
```

- [ ] **2단계: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 앞 두 테스트 실패.

- [ ] **3단계: 마크업을 지운다**

- 모드 토글 두 버튼(약 487~488줄)과 그것을 감싼 `<div>`를 지운다. 저장소 화면 하나만 남으므로
  모드 전환 자체가 없어진다.
- `<main class="workspace">` … `</main>` 블록(약 493~653줄)을 통째로 지운다.
- `#delete-dialog`와 `#export-dialog`(약 717~745줄)를 지운다. 저장소 UI는 둘 다 쓰지 않는다.
- `<main class="workspace repo-workspace" id="repo-workspace" hidden>`에서 `hidden`을 지운다.

- [ ] **4단계: CSS를 지운다**

Markdown 화면 전용 규칙을 지운다: `.card-row`, `.drag-handle`, `.selection-check`, `.bulk-select`,
`.form-section`, `.mode-button`(저장소 탭이 쓰므로 **남긴다**), `.workspace[hidden]`(더 이상 숨기지
않으므로 지운다). 지우기 전에 저장소 마크업이 그 클래스를 쓰지 않는지 확인한다.

```bash
awk '/<main class="workspace repo-workspace"/,/<\/main>/' Tools/card-idea-notebook/index.html | grep -o 'class="[^"]*"' | sort -u
```

- [ ] **5단계: 코어와 UI 스크립트를 지운다**

- `<script data-card-idea-core>` 안에서 `ROLE_LABELS` 선언(약 751줄)부터 `parseAuthoringSchema`
  바로 앞(약 1,659줄)까지 지운다. **단 `SCHEMA_VERSION`(777줄)과 `STORAGE_KEY`(778줄)는 남긴다** —
  두 줄을 파일 위쪽 `CARD_REQUIRED_KEYS` 근처로 옮긴다.
- `globalThis.CardIdeaNotebook` 목록에서 위 "지우는 코어 심볼" 42개를 지운다.
- `<script>` … `</script>`(2,718~3,473줄) 블록을 통째로 지운다.

- [ ] **6단계: 테스트를 지운다**

`index.test.mjs`의 1번째 `test(` 부터 `test("failed immediate persistence keeps memory state until
retry succeeds"` 블록 끝까지(약 1~1,664줄) 지운다. `loadCore` 헬퍼와 import는 남긴다.
첫 Korean 테스트 `test("생성된 스키마에서 효과 여덟 종을 읽는다"` 앞이 새 시작점이다.

- [ ] **7단계: `시작 카드 풀.md`를 지운다**

```bash
git rm "Tools/card-idea-notebook/시작 카드 풀.md"
```

- [ ] **8단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 약 120 pass / 0 fail (Markdown 테스트가 빠져 총수가 줄어든다). 실제 수치를 기록해 README에
반영한다.

- [ ] **9단계: 사용자에게 눈 검증을 요청한다**

Chrome에서 노트북을 열어 **모드 전환 없이 바로 저장소 화면**이 나오고, 폴더 연결·편집·반영이
그대로 되는지 확인을 요청한다.

- [ ] **10단계: 커밋한다**

```bash
git add -A Tools/card-idea-notebook && git commit -m "refactor(tools): 노트북에서 Markdown 저작 경로를 걷어낸다"
```

---

### 작업 7: 마이그레이션 — `SCHEMA_VERSION` 7과 옛 키 백업

**파일**
- 수정: `Tools/card-idea-notebook/index.html` (코어 — `SCHEMA_VERSION` 선언, `readPending`,
  새 함수 `migrateLegacyStore`)
- 수정: `Tools/card-idea-notebook/index.html` (저장소 UI — 부팅 순서)
- 테스트: `Tools/card-idea-notebook/index.test.mjs`

**인터페이스**
- 생산:
  - `SCHEMA_VERSION = 7` (값 변경). `PENDING_VERSION`은 지우고 `SCHEMA_VERSION`을 쓴다.
  - `LEGACY_BACKUP_KEY = "fate-weaver.card-idea-notebook.v6-backup"`
  - `migrateLegacyStore(storage)` → `{ moved: boolean, note: string }`
- `readPending`은 버전 `1`(계획 C가 쓴 값)과 `SCHEMA_VERSION`(7) 둘 다 받아들이고, 읽은 뒤
  `version: SCHEMA_VERSION`으로 올려서 돌려준다. **1을 거부하면 계획 C 시절의 미반영 편집이
  조용히 사라진다.**

- [ ] **1단계: 실패하는 테스트를 쓴다**

```js
function fakeStorage(seed = {}) {
  const map = new Map(Object.entries(seed));
  return {
    getItem: (key) => (map.has(key) ? map.get(key) : null),
    setItem: (key, value) => { map.set(key, String(value)); },
    removeItem: (key) => { map.delete(key); },
    snapshot: () => Object.fromEntries(map),
  };
}

test("스키마 버전이 7이다", () => {
  const core = loadCore();

  assert.equal(core.SCHEMA_VERSION, 7);
});

test("옛 Markdown 데이터를 백업 키로 옮기고 지운다", () => {
  const core = loadCore();
  const storage = fakeStorage({
    [core.STORAGE_KEY]: JSON.stringify({ schemaVersion: 6, cards: [{ name: "옛 카드" }] }),
  });

  const result = core.migrateLegacyStore(storage);

  assert.equal(result.moved, true);
  assert.match(result.note, /백업/);
  assert.equal(storage.getItem(core.STORAGE_KEY), null);
  assert.match(storage.getItem(core.LEGACY_BACKUP_KEY), /옛 카드/);
});

test("옛 데이터가 없으면 아무것도 하지 않는다", () => {
  const core = loadCore();
  const storage = fakeStorage();

  const result = core.migrateLegacyStore(storage);

  assert.equal(result.moved, false);
  assert.equal(result.note, "");
  assert.deepEqual(storage.snapshot(), {});
});

test("백업이 이미 있으면 덮어쓰지 않는다", () => {
  const core = loadCore();
  const storage = fakeStorage({
    [core.STORAGE_KEY]: JSON.stringify({ schemaVersion: 6 }),
    [core.LEGACY_BACKUP_KEY]: "먼저 백업된 것",
  });

  const result = core.migrateLegacyStore(storage);

  assert.equal(result.moved, false);
  assert.equal(storage.getItem(core.LEGACY_BACKUP_KEY), "먼저 백업된 것");
  assert.notEqual(storage.getItem(core.STORAGE_KEY), null);
});

test("계획 C 시절 미반영(버전 1)을 버리지 않는다", () => {
  const core = loadCore();
  const storage = fakeStorage({
    [core.PENDING_STORAGE_KEY]: JSON.stringify({
      version: 1, cards: { "repo:x": { id: "x" } }, pools: {},
    }),
  });

  const result = core.readPending(storage);

  assert.deepEqual(result.errors, []);
  assert.equal(result.pending.version, 7);
  assert.deepEqual(Object.keys(result.pending.cards), ["repo:x"]);
});
```

- [ ] **2단계: 실패를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 다섯 테스트 실패.

- [ ] **3단계: 코어를 고친다**

`SCHEMA_VERSION` 값을 7로 올리고, `PENDING_VERSION` 선언을 지운 뒤 `emptyPending`이
`SCHEMA_VERSION`을 쓰게 한다.

```js
    const SCHEMA_VERSION = 7;
    const STORAGE_KEY = "fate-weaver.card-idea-notebook";
    const LEGACY_BACKUP_KEY = `${STORAGE_KEY}.v6-backup`;
    const PENDING_STORAGE_KEY = `${STORAGE_KEY}.pending`;

    /// 설계 14: 옛 데이터는 능력이 자유 텍스트라 자동 변환이 불가능하다. 지우지 않고 옮긴 뒤
    /// 안내한다 - 저장소의 카드 26장을 읽으면 대부분 복원된다.
    /// 백업이 이미 있으면 아무것도 하지 않는다. 두 번째 백업으로 첫 백업을 덮으면 정작 지키려던
    /// 것을 잃는다.
    const LEGACY_MIGRATION_NOTE =
      "Markdown 시절의 저작 데이터를 백업으로 옮겼습니다."
      + " 노트북은 이제 저장소 JSON만 저작합니다 - 폴더를 연결해 주세요.";

    function migrateLegacyStore(storage) {
      const legacy = storage.getItem(STORAGE_KEY);
      if (legacy === null) return { moved: false, note: "" };
      if (storage.getItem(LEGACY_BACKUP_KEY) !== null) return { moved: false, note: "" };

      try {
        storage.setItem(LEGACY_BACKUP_KEY, legacy);
        storage.removeItem(STORAGE_KEY);
      } catch (error) {
        return { moved: false, note: `옛 데이터를 옮기지 못했습니다: ${error?.message ?? error}` };
      }

      return { moved: true, note: LEGACY_MIGRATION_NOTE };
    }
```

`emptyPending`을 고친다.

```js
    function emptyPending() {
      return { version: SCHEMA_VERSION, cards: {}, pools: {} };
    }
```

`readPending`의 버전 검사를 고친다.

```js
      /// 계획 C가 쓴 버전 1은 모양이 지금과 같다. 거부하면 그때의 미반영 편집이 조용히 사라지므로
      /// 받아들이고 올린다.
      if (!saved || ![1, SCHEMA_VERSION].includes(saved.version)) {
        return {
          pending: emptyPending(),
          errors: [`지원하지 않는 미반영 데이터 버전: ${saved?.version}`],
        };
      }

      return {
        pending: {
          version: SCHEMA_VERSION,
          cards: { ...(saved.cards ?? {}) },
          pools: { ...(saved.pools ?? {}) },
        },
        errors: [],
      };
```

`globalThis.CardIdeaNotebook`에 `LEGACY_BACKUP_KEY,`와 `migrateLegacyStore,`를 더한다.

- [ ] **4단계: UI 부팅에 배선한다**

저장소 UI 스크립트 끝의 `core.readPending` 호출 **앞**에 넣는다.

```js
    const migration = core.migrateLegacyStore(window.localStorage);
    if (migration.note) pendingNote = migration.note;

    const restored = core.readPending(window.localStorage);
    pending = restored.pending;
    if (restored.errors.length) {
      pendingNote = [pendingNote, ...restored.errors].filter(Boolean).join(" ");
    }
```

- [ ] **5단계: 통과를 확인한다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

기대: 약 125 pass / 0 fail.

- [ ] **6단계: 커밋한다**

```bash
git add Tools/card-idea-notebook/index.html Tools/card-idea-notebook/index.test.mjs && git commit -m "feat(tools): 노트북이 옛 Markdown 데이터를 백업으로 넘긴다"
```

---

### 작업 8: 문서 정리

**파일**
- 수정: `docs/superpowers/specs/2026-07-20-character-card-pools-design.md` (§1)
- 이동: `docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md` → `docs/superpowers/archive/specs/`
- 이동: `docs/superpowers/plans/2026-08-11-notebook-repo-write.md` → `docs/superpowers/archive/plans/`
- 수정: `docs/superpowers/README.md`

- [ ] **1단계: 카드풀 설계 §1을 읽는다**

```bash
sed -n '1,80p' docs/superpowers/specs/2026-07-20-character-card-pools-design.md
```

- [ ] **2단계: §1을 개정한다**

"풀은 캐릭터가 배타적으로 소유한다"는 취지의 문장에 **저작 중 예외**를 더한다. 노트북의 풀 편성이
같은 카드를 여러 풀에 담는 것을 허용하기 때문이다(스펙 §7). 다음 문단을 §1 끝에 붙인다.

```markdown
**저작 중에는 풀이 카드를 공유할 수 있다.** 런타임의 소유 관계는 그대로지만, 노트북(카드 저작
노트북 JSON 전환 설계 §7)은 카드 하나를 여러 풀에 담는 것을 막지 않는다. 어느 카드가 어느
캐릭터에게 갈지는 저작 과정에서 정해지며, 그 과정에서 후보를 여러 풀에 걸쳐 두는 것이 정상적인
작업 중 상태다. 배타성은 런타임 로더가 아니라 저작자의 판단이 정한다.
```

디렉터리가 없으면 만든다.

```bash
mkdir -p docs/superpowers/archive/specs
```

- [ ] **3단계: 옛 노트북 스펙을 옮긴다**

```bash
git mv docs/superpowers/specs/2026-07-27-card-idea-notebook-design.md docs/superpowers/archive/specs/
```

옮긴 파일 머리말의 `상태`를 `superseded`로 두고, 대체 문서 링크를 상대 경로로 고친다
(`../../specs/2026-08-05-card-authoring-json-notebook-design.md`).

- [ ] **4단계: 스펙 §16 검수 기준을 실제로 돌린다**

```bash
node --test "Tools/card-idea-notebook/*.test.mjs"
```

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

기준 2·4·5·8은 브라우저 조작이 필요하므로 사용자에게 확인을 요청한다.

| 기준 | 확인 방법 |
|---|---|
| 2. 새 카드를 만들어 내보낸 뒤 `dotnet test` 통과 | 노트북에서 카드 생성 → 반영 → 위 명령 |
| 4. 등급 없는 카드를 풀에 담으면 내보내기가 막힘 | 요약 다이얼로그의 `검증 오류` 사유 확인 |
| 5. 노트북이 카드 파일을 지우지 않음 | 카드 id 변경 후 반영 → 옛 파일이 남고 `미참조 파일`에 뜸 |
| 8. 없는 카드 id를 손으로 넣으면 유지한 채 내보내기 차단 | 풀 JSON에 `ghost_card` 추가 → 다시 읽기 → 반영 시도 |

- [ ] **5단계: README 색인을 갱신한다**

- 60줄의 옛 노트북 스펙 행을 `archive/specs/` 경로로 고치고 비고를 "구현 완료로 보관"으로 바꾼다.
- 61줄 JSON 전환 설계 행의 상태를 `current` → `implemented`로 바꾸고, 비고를
  "계획 A~D 완료"로 고친다.
- 208~220줄의 후속 작업 대기열 `노트북 반영·정리 (계획 D)` 항목을 지운다.
- "현재 수치" 절의 노트북 테스트 수를 4단계 실측값으로 갱신하고, Markdown 제거로 줄어든 이유를
  한 문장 적는다.
- 완료된 이 계획을 `archive/plans/`로 옮긴 경로로 링크한다.

```bash
git mv docs/superpowers/plans/2026-08-11-notebook-repo-write.md docs/superpowers/archive/plans/
```

- [ ] **6단계: 커밋한다**

```bash
git add -A docs && git commit -m "docs: 노트북 저장소 반영 완료를 색인에 반영한다"
```

---

## 자기 검토

**스펙 범위 대응**

| 스펙 | 담당 작업 |
|---|---|
| §10.3 충돌 해결 UI | 작업 2 |
| §12 diff 요약과 파일 쓰기 | 작업 3(계획)·작업 5(쓰기) |
| §12 "저장소에만 있음" → 읽기 오류 / 미참조 파일 분리 | 작업 3·4·5 |
| §11.3 우측 `저장소 ↔ 현재` diff 토글 | 작업 1 |
| §11.3 오류 카드 원문 표시 → 원문 편집으로 확장 | 작업 4 |
| §14 `SCHEMA_VERSION` 7과 옛 키 백업 | 작업 7 |
| §14 `시작 카드 풀.md` 삭제, `적 타입 A.md` 유지 | 작업 6 |
| §13 Markdown 테스트 제거 | 작업 6 |
| 카드풀 설계 §1 개정, 옛 스펙 `archive/` 이동 | 작업 8 |
| §16 검수 기준 2·4·5·8 | 작업 8 4단계 |

**순서 제약**

- 작업 6(Markdown 제거)이 작업 7(버전 상향)보다 **먼저**여야 한다. `SCHEMA_VERSION`을 Markdown
  경로가 읽고 있어 순서를 바꾸면 그 자리에서 깨진다.
- 작업 3(`exportPlan`)이 작업 5(쓰기 게이트)보다 먼저다. 게이트가 계획을 소비한다.
- 작업 4(`readErrors` 개명)가 작업 5보다 먼저다. `exportPlan` 호출이 그 이름을 쓴다.
- 작업 1(`renderSource`)이 작업 4보다 먼저다. 원문 편집기가 `renderSource`를 부른다.

**이름 일관성** — `readErrors`(코어 인자·`repoStore` 필드·`exportPlan` 반환), `unreferenced`
(`exportPlan` 반환), `diffLines`, `exportPlan`, `migrateLegacyStore`, `LEGACY_BACKUP_KEY`,
`renderSource`, `renderReadErrorDetail`, `applyRawRepair`, `exportToRepo`, `writePlanFiles`,
`hasWritePermission`. 작업 4가 `quarantined`를 전부 `readErrors`로 바꾸므로 잔재가 없어야 한다 —
작업 4의 마크업 테스트가 `assert.doesNotMatch(ui[1], /quarantined/)`로 잠근다.

**규칙 30 점검** — 저장소 UI 스크립트는 이미 단일 IIFE이고 이 계획이 함수 11개를 더한다.
`[SerializeField]` 기준은 Unity 컴포넌트용이라 여기에 적용되지 않지만, `renderSource`·
`renderReadErrorDetail`·`renderExportSummary`를 각각 독립 렌더 함수로 두어 `render()`가 조정만
하도록 유지한다. `render()`가 판정을 직접 하기 시작하면 그때 분해를 사용자에게 보고한다.
