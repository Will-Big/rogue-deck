import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";
import { fileURLToPath } from "node:url";

const htmlUrl = new URL("./index.html", import.meta.url);

function loadCore() {
  assert.equal(existsSync(htmlUrl), true, "index.html must exist");
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const match = html.match(/<script data-card-idea-core>([\s\S]*?)<\/script>/);
  assert.ok(match, "index.html must expose the card idea core script");

  const context = {
    console,
    Date,
    JSON,
    Math,
    structuredClone,
  };
  context.globalThis = context;
  vm.runInNewContext(match[1], context, { filename: "card-idea-core.js" });
  assert.ok(context.CardIdeaNotebook, "core script must expose CardIdeaNotebook");
  return context.CardIdeaNotebook;
}

test("normalizes free text fields without inventing card decisions", () => {
  const core = loadCore();
  const card = core.normalizeCard({
    name: "  맹독 찌르기  ",
    role: "execution",
    cost: "",
    tags: "독, 성장\n방어",
    targets: { ally: "backOne", enemy: "frontTwo" },
    abilities: {
      enemy: "피해를 준다.\n\n독을 부여한다.",
      ally: ["방어를 부여한다."],
      none: "",
    },
  });

  assert.equal(card.name, "맹독 찌르기");
  assert.equal(card.cost, "");
  assert.deepEqual([...card.tags], ["독", "성장", "방어"]);
  assert.deepEqual([...card.abilities.enemy], ["피해를 준다.", "독을 부여한다."]);
  assert.deepEqual([...card.abilities.ally], ["방어를 부여한다."]);
});

test("renders facing ally and enemy ranges in Markdown", () => {
  const core = loadCore();
  const card = core.normalizeCard({
    name: "맹독 찌르기",
    role: "execution",
    cost: "1",
    executionOrder: "4",
    targets: { ally: "backOne", enemy: "frontTwo" },
    abilities: {
      ally: ["방어 4."],
      enemy: ["피해 5."],
      none: ["카드 1장 뽑기."],
    },
  });

  const markdown = core.cardMarkdown(card);
  assert.match(markdown, /아군 뒤 하나 `◆━━━━` │ `◆◆━━━` 적군 앞 둘/);
  assert.match(markdown, /- \[적군\] 피해 5\./);
  assert.match(markdown, /- \[아군\] 방어 4\./);
  assert.match(markdown, /- 카드 1장 뽑기\./);
});

test("omits blank optional metadata and empty sections", () => {
  const core = loadCore();
  const markdown = core.cardMarkdown(core.normalizeCard({
    name: "빈 초안",
    role: "unknown",
    targets: { ally: "none", enemy: "none" },
    abilities: { ally: "", enemy: "", none: "" },
  }));

  assert.equal(markdown.includes("비용:"), false);
  assert.equal(markdown.includes("실행순서:"), false);
  assert.equal(markdown.includes("태그:"), false);
  assert.equal(markdown.includes("### 능력"), false);
  assert.equal(markdown.includes("### 메모"), false);
  assert.match(markdown, /- 대상: 없음/);
});

test("separates blocking errors from idea-stage warnings", () => {
  const core = loadCore();
  const result = core.validateCard(core.normalizeCard({
    name: "",
    role: "execution",
    cost: "",
    executionOrder: "",
    targets: { ally: "none", enemy: "frontOne" },
    abilities: { ally: "방어 3.", enemy: "", none: "" },
  }));

  assert.deepEqual([...result.errors], ["카드 이름을 입력하세요."]);
  assert.deepEqual([...result.warnings], [
    "비용이 미정입니다.",
    "실행 카드의 실행순서가 미정입니다.",
    "아군 능력은 있지만 아군 위치 범위가 없습니다.",
    "적군 위치 범위는 있지만 적군 능력이 없습니다.",
  ]);
});

test("builds one AI handoff Markdown file from multiple cards", () => {
  const core = loadCore();
  const cards = [
    core.normalizeCard({ name: "첫 카드", role: "unknown" }),
    core.normalizeCard({ name: "둘째 카드", role: "intervention" }),
  ];

  const markdown = core.bundleMarkdown(cards, "2026-07-27");
  assert.match(markdown, /^# Fate Weaver 카드 아이디어/);
  assert.match(markdown, /- 생성일: 2026-07-27/);
  assert.match(markdown, /- 카드 수: 2/);
  assert.match(markdown, /## 첫 카드/);
  assert.match(markdown, /## 둘째 카드/);
});

class MemoryStorage {
  constructor(initial = {}) {
    this.values = new Map(Object.entries(initial));
    this.writeCount = 0;
  }

  getItem(key) {
    return this.values.has(key) ? this.values.get(key) : null;
  }

  setItem(key, value) {
    this.writeCount += 1;
    this.values.set(key, String(value));
  }
}

class ToggleStorage extends MemoryStorage {
  constructor(initial = {}) {
    super(initial);
    this.failWrites = false;
  }

  setItem(key, value) {
    if (this.failWrites) throw new Error("storage unavailable");
    super.setItem(key, value);
  }
}

test("writes local storage only through the explicit store operation", () => {
  const core = loadCore();
  const storage = new MemoryStorage();
  const state = core.initialState();
  state.cards.push(core.normalizeCard({ id: "a", name: "저장할 카드" }));

  assert.equal(storage.writeCount, 0);
  core.writeStore(storage, state);
  assert.equal(storage.writeCount, 1);

  const saved = JSON.parse(storage.getItem(core.STORAGE_KEY));
  assert.equal(saved.schemaVersion, 2);
  assert.equal(saved.cards[0].name, "저장할 카드");
});

test("round-trips the current schema, pruning incomplete selections, and rejects an unknown schema", () => {
  const core = loadCore();
  const storage = new MemoryStorage();
  const state = {
    ...core.initialState(),
    cards: [core.normalizeCard({
      id: "a",
      name: "보존 카드",
      tags: ["독"],
      completionStatus: "complete",
    })],
    activeCardId: "a",
    searchQuery: "독",
    exportSelection: ["a"],
  };
  core.writeStore(storage, state);

  const loaded = core.readStore(storage);
  assert.equal(loaded.cards[0].name, "보존 카드");
  assert.deepEqual([...loaded.cards[0].tags], ["독"]);
  assert.equal(loaded.activeCardId, "a");
  assert.equal(loaded.searchQuery, "독");
  assert.deepEqual([...loaded.exportSelection], ["a"]);

  const incomplete = core.editCard(state, "a", { notes: "수정됨" });
  core.writeStore(storage, { ...incomplete, exportSelection: ["a"] });
  assert.deepEqual([...core.readStore(storage).exportSelection], []);

  storage.setItem(core.STORAGE_KEY, JSON.stringify({ schemaVersion: 99, cards: [] }));
  assert.throws(() => core.readStore(storage), /지원하지 않는 저장 데이터 버전/);
});

test("keeps every card in one list without a separate draft state", () => {
  const core = loadCore();
  const state = core.initialState();

  assert.equal(Object.hasOwn(state, "draft"), false);
  const created = core.createCard(state, { id: "a" });
  assert.equal(Object.hasOwn(created, "draft"), false);
  assert.equal(created.cards.length, 1);
});

test("export includes selected complete cards and excludes incomplete cards", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({
        id: "a",
        name: "완성본",
        completionStatus: "complete",
      }),
      core.normalizeCard({
        id: "b",
        name: "미완성본",
        completionStatus: "incomplete",
      }),
    ],
    exportSelection: ["a", "b"],
  };

  assert.deepEqual([...core.cardsForExport(state).map((card) => card.name)], ["완성본"]);
});

test("duplicates into the list and deletes cards without mutating the source", () => {
  const core = loadCore();
  const saved = core.normalizeCard({
    id: "a",
    name: "원본",
    tags: ["독"],
    createdAt: "2026-07-27T10:00:00.000Z",
  });
  const state = {
    ...core.initialState(),
    cards: [saved],
    activeCardId: "a",
    exportSelection: ["a"],
  };

  const duplicated = core.duplicateCard(state, "a", {
    id: "b",
    now: "2026-07-27T11:00:00.000Z",
  });
  assert.equal(duplicated.cards.length, 2);
  assert.equal(duplicated.cards[1].name, "원본 복사본");
  assert.equal(duplicated.cards[1].id, "b");
  assert.equal(duplicated.cards[1].completionStatus, "incomplete");
  assert.equal(duplicated.activeCardId, "b");
  assert.deepEqual([...saved.tags], ["독"]);

  const deleted = core.deleteCard(state, "a");
  assert.equal(deleted.cards.length, 0);
  assert.deepEqual([...deleted.exportSelection], []);
  assert.equal(deleted.activeCardId, "");
});

test("searches saved cards by name or tag", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "맹독 찌르기", tags: ["독", "공격"] }),
      core.normalizeCard({ id: "b", name: "철벽", tags: ["방어"] }),
    ],
  };

  assert.deepEqual(core.filteredCards(state.cards, "맹독").map((card) => card.id), ["a"]);
  assert.deepEqual(core.filteredCards(state.cards, "방어").map((card) => card.id), ["b"]);
  assert.equal(core.filteredCards(state.cards, "").length, 2);
});

test("blocks export without selection and allows a selected complete card", () => {
  const core = loadCore();
  const saved = core.normalizeCard({
    id: "a",
    name: "저장 카드",
    completionStatus: "complete",
  });
  const base = {
    ...core.initialState(),
    cards: [saved],
    activeCardId: "a",
  };

  assert.deepEqual(
    { ...core.exportStatus(base) },
    { kind: "error", message: "내보낼 완성 카드를 선택하세요." },
  );

  const selected = {
    ...base,
    exportSelection: ["a"],
  };
  assert.deepEqual({ ...core.exportStatus(selected) }, { kind: "ready" });
});

test("blocks writes after rejecting unreadable or future storage data", () => {
  const core = loadCore();
  const raw = JSON.stringify({
    schemaVersion: 99,
    cards: [{ id: "future-card", name: "미래 카드" }],
  });
  const storage = new MemoryStorage({ [core.STORAGE_KEY]: raw });
  const session = core.readStoreSession(storage);
  assert.equal(session.writable, false);
  assert.match(session.error, /지원하지 않는 저장 데이터 버전/);
  assert.equal(session.state.cards.length, 0);
  assert.equal(storage.getItem(core.STORAGE_KEY), raw);
});

test("migrates schema 1 cards to complete schema 2 cards", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 1,
      cards: [{ id: "a", name: "기존 카드" }],
      activeCardId: "a",
      searchQuery: "",
      exportSelection: ["a"],
    }),
  });

  const state = core.readStore(storage);
  assert.equal(state.schemaVersion, 2);
  assert.equal(state.cards[0].completionStatus, "complete");
});

test("creates uniquely named incomplete cards directly in the list", () => {
  const core = loadCore();
  const first = core.createCard(core.initialState(), {
    id: "a",
    now: "2026-07-27T00:00:00.000Z",
  });
  const second = core.createCard(first, {
    id: "b",
    now: "2026-07-27T00:01:00.000Z",
  });

  assert.deepEqual([...first.cards.map((card) => card.name)], ["새 카드"]);
  assert.deepEqual([...second.cards.map((card) => card.name)], ["새 카드", "새 카드 (2)"]);
  assert.equal(second.cards[1].completionStatus, "incomplete");
  assert.equal(second.activeCardId, "b");
});

test("editing a complete card makes it incomplete and unselects it", () => {
  const core = loadCore();
  const complete = core.normalizeCard({
    id: "a",
    name: "완성 카드",
    completionStatus: "complete",
  });
  const state = {
    ...core.initialState(),
    cards: [complete],
    activeCardId: "a",
    exportSelection: ["a"],
  };

  const edited = core.editCard(state, "a", { notes: "수정됨" });
  assert.equal(edited.cards[0].notes, "수정됨");
  assert.equal(edited.cards[0].completionStatus, "incomplete");
  assert.deepEqual([...edited.exportSelection], []);
});

test("completes a valid card and rejects an empty card name", () => {
  const core = loadCore();
  const valid = core.createCard(core.initialState(), {
    id: "a",
    now: "2026-07-27T00:00:00.000Z",
  });
  const completed = core.completeCard(valid, "a", {
    now: "2026-07-27T00:02:00.000Z",
  });
  assert.equal(completed.cards[0].completionStatus, "complete");
  assert.equal(completed.cards[0].updatedAt, "2026-07-27T00:02:00.000Z");

  const emptyName = core.editCard(completed, "a", { name: "" });
  assert.throws(
    () => core.completeCard(emptyName, "a"),
    /카드 이름을 입력하세요/,
  );
});

test("round-trips exported cards through strict Markdown import", () => {
  const core = loadCore();
  const source = [
    core.normalizeCard({
      name: "맹독 호위",
      role: "execution",
      cost: "1",
      executionOrder: "4",
      tags: ["독", "방어"],
      targets: { ally: "backOne", enemy: "frontTwo" },
      abilities: {
        ally: ["방어 4."],
        enemy: ["독 2."],
        none: ["카드 1장 뽑기."],
      },
      notes: "왕복 확인",
      completionStatus: "complete",
    }),
  ];

  const parsed = core.parseBundleMarkdown(core.bundleMarkdown(source, "2026-07-27"));
  assert.equal(parsed.length, 1);
  assert.equal(parsed[0].name, "맹독 호위");
  assert.deepEqual([...parsed[0].tags], ["독", "방어"]);
  assert.deepEqual([...parsed[0].abilities.enemy], ["독 2."]);
  assert.equal(parsed[0].targets.ally, "backOne");
  assert.equal(parsed[0].targets.enemy, "frontTwo");
  assert.equal(parsed[0].completionStatus, "complete");
});

test("imports duplicate names as new numbered cards", () => {
  const core = loadCore();
  const existing = core.normalizeCard({
    id: "a",
    name: "맹독 호위",
    completionStatus: "complete",
  });
  const markdown = core.bundleMarkdown([
    core.normalizeCard({ name: "맹독 호위", completionStatus: "complete" }),
    core.normalizeCard({ name: "맹독 호위", completionStatus: "complete" }),
  ], "2026-07-27");

  const imported = core.importCards(
    { ...core.initialState(), cards: [existing] },
    markdown,
    { ids: ["b", "c"], now: "2026-07-27T01:00:00.000Z" },
  );
  assert.deepEqual([...imported.cards.map((card) => card.name)], [
    "맹독 호위",
    "맹독 호위 (2)",
    "맹독 호위 (3)",
  ]);
  assert.equal(imported.activeCardId, "b");
  assert.equal(imported.cards[2].completionStatus, "complete");
});

test("rejects a malformed bundle without changing existing state", () => {
  const core = loadCore();
  const existing = core.normalizeCard({
    id: "a",
    name: "보존 카드",
    completionStatus: "complete",
  });
  const state = { ...core.initialState(), cards: [existing] };

  assert.throws(
    () => core.importCards(state, "# 잘못된 파일"),
    /불러올 수 없는 Markdown/,
  );
  assert.deepEqual([...state.cards.map((card) => card.name)], ["보존 카드"]);
});

test("rejects a bundle whose declared card count is wrong", () => {
  const core = loadCore();
  const markdown = core.bundleMarkdown([
    core.normalizeCard({ name: "한 장", completionStatus: "complete" }),
  ], "2026-07-27").replace("- 카드 수: 1", "- 카드 수: 2");

  assert.throws(
    () => core.parseBundleMarkdown(markdown),
    /카드 수가 일치하지 않습니다/,
  );
});

test("bulk selection includes all and only complete cards", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "완성", completionStatus: "complete" }),
      core.normalizeCard({ id: "b", name: "미완성", completionStatus: "incomplete" }),
    ],
  };

  assert.deepEqual([...core.bulkSelection(state, true).exportSelection], ["a"]);
  assert.deepEqual([...core.bulkSelection({
    ...state,
    exportSelection: ["a"],
  }, false).exportSelection], []);
});

test("failed immediate persistence keeps memory state until retry succeeds", () => {
  const core = loadCore();
  const storage = new ToggleStorage();
  const state = core.createCard(core.initialState(), {
    id: "a",
    now: "2026-07-27T00:00:00.000Z",
  });

  storage.failWrites = true;
  const failed = core.tryWriteStore(storage, state);
  assert.equal(failed.persistFailed, true);
  assert.equal(failed.state.cards[0].name, "새 카드");
  assert.equal(storage.getItem(core.STORAGE_KEY), null);

  storage.failWrites = false;
  const recovered = core.tryWriteStore(storage, failed.state);
  assert.equal(recovered.persistFailed, false);
  assert.equal(
    JSON.parse(storage.getItem(core.STORAGE_KEY)).cards[0].name,
    "새 카드",
  );
});
