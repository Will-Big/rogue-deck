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

test("writes local storage only through the explicit store operation", () => {
  const core = loadCore();
  const storage = new MemoryStorage();
  const state = core.initialState();
  state.cards.push(core.normalizeCard({ id: "a", name: "저장할 카드" }));

  assert.equal(storage.writeCount, 0);
  core.writeStore(storage, state);
  assert.equal(storage.writeCount, 1);

  const saved = JSON.parse(storage.getItem(core.STORAGE_KEY));
  assert.equal(saved.schemaVersion, 1);
  assert.equal(saved.cards[0].name, "저장할 카드");
});

test("round-trips the current schema and rejects an unknown schema", () => {
  const core = loadCore();
  const storage = new MemoryStorage();
  const state = {
    ...core.initialState(),
    cards: [core.normalizeCard({ id: "a", name: "보존 카드", tags: ["독"] })],
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

  storage.setItem(core.STORAGE_KEY, JSON.stringify({ schemaVersion: 99, cards: [] }));
  assert.throws(() => core.readStore(storage), /지원하지 않는 저장 데이터 버전/);
});

test("detects an unsaved semantic card change", () => {
  const core = loadCore();
  const saved = core.normalizeCard({
    id: "a",
    name: "저장본",
    updatedAt: "2026-07-27T00:00:00.000Z",
  });
  const unchangedDraft = {
    ...saved,
    updatedAt: "2026-07-28T00:00:00.000Z",
  };
  const changedDraft = { ...saved, name: "미저장본" };

  assert.equal(core.isDirty(saved, unchangedDraft), false);
  assert.equal(core.isDirty(saved, changedDraft), true);
});

test("export uses saved cards rather than the unsaved draft", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [core.normalizeCard({ id: "a", name: "저장본" })],
    draft: core.normalizeCard({ id: "a", name: "미저장본" }),
    exportSelection: ["a"],
  };

  assert.equal(core.cardsForExport(state)[0].name, "저장본");
});

test("saves a new draft explicitly and updates an existing saved card", () => {
  const core = loadCore();
  const initial = {
    ...core.initialState(),
    draft: core.normalizeCard({ name: "새 카드", role: "unknown" }),
  };
  const created = core.saveDraft(initial, {
    now: "2026-07-27T10:00:00.000Z",
    id: "card-a",
  });

  assert.equal(created.cards.length, 1);
  assert.equal(created.cards[0].id, "card-a");
  assert.equal(created.activeCardId, "card-a");
  assert.equal(core.isDirty(created.cards[0], created.draft), false);

  const edited = {
    ...created,
    draft: { ...created.draft, name: "수정 카드" },
  };
  const updated = core.saveDraft(edited, { now: "2026-07-27T11:00:00.000Z" });
  assert.equal(updated.cards.length, 1);
  assert.equal(updated.cards[0].name, "수정 카드");
  assert.equal(updated.cards[0].createdAt, "2026-07-27T10:00:00.000Z");
  assert.equal(updated.cards[0].updatedAt, "2026-07-27T11:00:00.000Z");
});

test("reports dirty navigation and discards back to the saved card", () => {
  const core = loadCore();
  const saved = core.normalizeCard({ id: "a", name: "저장 카드" });
  const state = {
    ...core.initialState(),
    cards: [saved],
    activeCardId: "a",
    draft: { ...saved, name: "미저장 카드" },
  };

  assert.equal(core.navigationStatus(state), "dirty");
  const discarded = core.discardDraft(state);
  assert.equal(discarded.draft.name, "저장 카드");
  assert.equal(core.navigationStatus(discarded), "clean");
});

test("duplicates and deletes cards without mutating the source", () => {
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
    draft: saved,
    exportSelection: ["a"],
  };

  const duplicated = core.duplicateCard(state, "a");
  assert.equal(duplicated.cards.length, 1);
  assert.equal(duplicated.draft.name, "원본 복사본");
  assert.equal(duplicated.draft.id, "");
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

test("blocks export without selection and requires saving a selected dirty card", () => {
  const core = loadCore();
  const saved = core.normalizeCard({ id: "a", name: "저장 카드" });
  const base = {
    ...core.initialState(),
    cards: [saved],
    activeCardId: "a",
    draft: saved,
  };

  assert.deepEqual(
    { ...core.exportStatus(base) },
    { kind: "error", message: "내보낼 저장 카드를 선택하세요." },
  );

  const selectedDirty = {
    ...base,
    exportSelection: ["a"],
    draft: { ...saved, name: "미저장 카드" },
  };
  assert.deepEqual(
    { ...core.exportStatus(selectedDirty) },
    { kind: "dirty" },
  );
  assert.deepEqual(
    { ...core.exportStatus({ ...selectedDirty, draft: saved }) },
    { kind: "ready" },
  );
});
