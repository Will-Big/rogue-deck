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

test("exposes every card grade in the authoring form", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");

  assert.match(html, /id="card-grade"/);
  assert.match(html, /data-card-field="grade"/);
  for (const label of ["없음", "일반", "고급", "희귀", "기타"]) {
    assert.match(html, new RegExp(`>${label}<\\/option>`));
  }
});

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

test("normalizes card factions and derives completion from core information", () => {
  const core = loadCore();
  const defaultCard = core.emptyCard();
  const enemy = core.normalizeCard({
    faction: "enemy",
    role: "intervention",
    cost: "9",
  });
  const allyWithoutCost = core.normalizeCard({
    name: "비용 없는 아군",
    faction: "ally",
    role: "intervention",
  });
  const completeAlly = core.normalizeCard({
    name: "준비된 아군",
    faction: "ally",
    role: "intervention",
    cost: "1",
  });
  const enemyWithoutOrder = core.normalizeCard({
    name: "순서 없는 적",
    faction: "enemy",
  });
  const completeEnemy = core.normalizeCard({
    name: "준비된 적",
    faction: "enemy",
    executionOrder: "3",
  });

  assert.equal(defaultCard.faction, "ally");
  assert.deepEqual(
    { faction: enemy.faction, role: enemy.role, cost: enemy.cost },
    { faction: "enemy", role: "execution", cost: "" },
  );
  assert.equal(core.isCardComplete(allyWithoutCost), false);
  assert.equal(core.isCardComplete(completeAlly), true);
  assert.equal(core.isCardComplete(enemyWithoutOrder), false);
  assert.equal(core.isCardComplete(completeEnemy), true);
});

test("normalizes grades and resets grade with faction transitions", () => {
  const core = loadCore();

  assert.equal(core.emptyCard().grade, "common");
  assert.equal(core.normalizeCard({ faction: "ally", grade: "rare" }).grade, "rare");
  assert.equal(core.normalizeCard({ faction: "enemy", grade: "rare" }).grade, "none");
  assert.equal(core.normalizeCard({ faction: "ally", grade: "invalid" }).grade, "common");

  const enemy = core.changeCardFaction({
    faction: "ally",
    grade: "advanced",
    role: "intervention",
    cost: "2",
  }, "enemy");
  assert.deepEqual(
    {
      faction: enemy.faction,
      grade: enemy.grade,
      role: enemy.role,
      cost: enemy.cost,
    },
    {
      faction: "enemy",
      grade: "none",
      role: "execution",
      cost: "",
    },
  );

  const allyAgain = core.changeCardFaction(enemy, "ally");
  assert.deepEqual(
    {
      faction: allyAgain.faction,
      grade: allyAgain.grade,
      role: allyAgain.role,
      cost: allyAgain.cost,
    },
    {
      faction: "ally",
      grade: "common",
      role: "unknown",
      cost: "",
    },
  );
});

test("requires integer cost and execution order values for completion", () => {
  const core = loadCore();
  const ally = {
    name: "아군 수치",
    faction: "ally",
    role: "intervention",
  };
  const enemy = {
    name: "적군 수치",
    faction: "enemy",
  };

  assert.equal(core.isCardComplete({ ...ally, cost: "0" }), true);
  assert.equal(core.isCardComplete({ ...ally, cost: "" }), false);
  assert.equal(core.isCardComplete({ ...ally, cost: "abc" }), false);
  assert.equal(core.isCardComplete({ ...ally, cost: "1.5" }), false);
  assert.equal(core.isCardComplete({ ...ally, cost: "-1" }), false);

  assert.equal(core.isCardComplete({ ...enemy, executionOrder: "0" }), true);
  assert.equal(core.isCardComplete({ ...enemy, executionOrder: "-1" }), true);
  assert.equal(core.isCardComplete({ ...enemy, executionOrder: "" }), false);
  assert.equal(core.isCardComplete({ ...enemy, executionOrder: "abc" }), false);
  assert.equal(core.isCardComplete({ ...enemy, executionOrder: "1.5" }), false);
});

test("resets ally-only fields when changing card faction", () => {
  const core = loadCore();
  const ally = core.normalizeCard({
    name: "전환 카드",
    faction: "ally",
    role: "intervention",
    cost: "2",
    notes: "유지할 메모",
  });
  const enemy = core.changeCardFaction(ally, "enemy");
  const allyAgain = core.changeCardFaction(enemy, "ally");

  assert.deepEqual(
    { faction: enemy.faction, role: enemy.role, cost: enemy.cost, notes: enemy.notes },
    { faction: "enemy", role: "execution", cost: "", notes: "유지할 메모" },
  );
  assert.deepEqual(
    {
      faction: allyAgain.faction,
      role: allyAgain.role,
      cost: allyAgain.cost,
      notes: allyAgain.notes,
    },
    { faction: "ally", role: "unknown", cost: "", notes: "유지할 메모" },
  );
});

test("saves one or every card without forcing incomplete cards complete", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({
        id: "ally",
        name: "준비된 아군",
        faction: "ally",
        role: "intervention",
        cost: "1",
      }),
      core.normalizeCard({
        id: "enemy",
        name: "순서 없는 적",
        faction: "enemy",
      }),
    ],
    activeCardId: "enemy",
    selection: ["ally", "enemy"],
  };

  const one = core.saveCard(state, "ally", {
    now: "2026-07-28T01:00:00.000Z",
  });
  assert.equal(one.cards[0].completionStatus, "complete");
  assert.equal(one.cards[1].completionStatus, "incomplete");

  const all = core.saveAllCards(state, {
    now: "2026-07-28T02:00:00.000Z",
  });
  assert.deepEqual(
    [...all.cards.map((card) => card.completionStatus)],
    ["complete", "incomplete"],
  );
  assert.equal(all.activeCardId, "enemy");
  assert.deepEqual([...all.selection], ["ally", "enemy"]);
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

test("renders self for either faction and rejects two self targets", () => {
  const core = loadCore();
  const allySelf = core.normalizeCard({
    name: "자기 방어",
    targets: { ally: "self", enemy: "frontOne" },
    abilities: { ally: ["방어 4."], enemy: ["피해 2."] },
  });
  const enemySelf = core.normalizeCard({
    name: "적의 태세",
    targets: { ally: "backOne", enemy: "self" },
    abilities: { ally: ["약화 1."], enemy: ["방어 4."] },
  });
  const twoSelf = core.normalizeCard({
    name: "잘못된 카드",
    targets: { ally: "self", enemy: "self" },
    abilities: { ally: ["방어 1."], enemy: ["방어 1."] },
  });

  assert.equal(core.TARGETS.self.label, "자신");
  assert.equal(
    core.targetSummary(allySelf),
    "아군 자신 `◎` │ `◆━━━━` 적군 앞 하나",
  );
  assert.equal(
    core.targetSummary(enemySelf),
    "아군 뒤 하나 `◆━━━━` │ `◎` 적군 자신",
  );
  assert.deepEqual([...core.validateCard(twoSelf).errors], [
    "아군과 적군에 자신을 동시에 지정할 수 없습니다.",
  ]);

  const state = {
    ...core.initialState(),
    cards: [{ ...twoSelf, id: "two-self" }],
    activeCardId: "two-self",
  };
  assert.throws(
    () => core.saveCard(state, "two-self"),
    /아군과 적군에 자신을 동시에 지정할 수 없습니다/,
  );
});

test("round-trips ally and enemy self targets through strict Markdown", () => {
  const core = loadCore();
  const source = [
    core.normalizeCard({
      name: "계승자의 방어",
      role: "execution",
      targets: { ally: "self", enemy: "frontOne" },
      abilities: { ally: ["방어 4."], enemy: ["피해 2."] },
      completionStatus: "complete",
    }),
    core.normalizeCard({
      name: "적의 자기 강화",
      role: "execution",
      targets: { ally: "backOne", enemy: "self" },
      abilities: { ally: ["약화 1."], enemy: ["공격 3."] },
      completionStatus: "complete",
    }),
  ];

  const parsed = core.parseBundleMarkdown(
    core.bundleMarkdown(source, "2026-07-28"),
  );
  assert.deepEqual(
    [...parsed.map((card) => [card.targets.ally, card.targets.enemy])],
    [["self", "frontOne"], ["backOne", "self"]],
  );

  const invalid = core.bundleMarkdown([
    core.normalizeCard({
      name: "양쪽 자신",
      role: "execution",
      targets: { ally: "self", enemy: "self" },
      abilities: { ally: ["방어 1."], enemy: ["방어 1."] },
      completionStatus: "complete",
    }),
  ], "2026-07-28");
  assert.throws(
    () => core.parseBundleMarkdown(invalid),
    /아군과 적군에 자신을 동시에 지정할 수 없습니다/,
  );
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

test("keeps structural messages without warning about blank core information", () => {
  const core = loadCore();
  const result = core.validateCard(core.normalizeCard({
    name: "",
    role: "execution",
    cost: "",
    executionOrder: "",
    targets: { ally: "none", enemy: "frontOne" },
    abilities: { ally: "방어 3.", enemy: "", none: "" },
  }));

  assert.deepEqual([...result.errors], []);
  assert.deepEqual([...result.warnings], [
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
  assert.equal(saved.schemaVersion, 6);
  assert.equal(saved.cards[0].faction, "ally");
  assert.equal(saved.cards[0].name, "저장할 카드");
});

test("round-trips the current schema with shared selection and rejects an unknown schema", () => {
  const core = loadCore();
  const storage = new MemoryStorage();
  const state = {
    ...core.initialState(),
    cards: [core.normalizeCard({
      id: "a",
      name: "보존 카드",
      grade: "rare",
      tags: ["독"],
      completionStatus: "complete",
    })],
    activeCardId: "a",
    searchQuery: "독",
    selection: ["a"],
  };
  core.writeStore(storage, state);

  const loaded = core.readStore(storage);
  assert.equal(loaded.cards[0].name, "보존 카드");
  assert.equal(loaded.cards[0].grade, "rare");
  assert.deepEqual([...loaded.cards[0].tags], ["독"]);
  assert.equal(loaded.activeCardId, "a");
  assert.equal(loaded.searchQuery, "독");
  assert.deepEqual([...loaded.selection], ["a"]);

  const incomplete = core.editCard(state, "a", { notes: "수정됨" });
  core.writeStore(storage, incomplete);
  assert.deepEqual([...core.readStore(storage).selection], ["a"]);

  storage.setItem(core.STORAGE_KEY, JSON.stringify({ schemaVersion: 99, cards: [] }));
  assert.throws(() => core.readStore(storage), /지원하지 않는 저장 데이터 버전/);
});

test("migrates schema 3 and round-trips the schema 6 default export file name", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 3,
      cards: [{ id: "a", name: "기존 카드", completionStatus: "complete" }],
      activeCardId: "a",
      searchQuery: "",
      selection: ["a"],
    }),
  });

  const migrated = core.readStore(storage);
  assert.equal(migrated.schemaVersion, 6);
  assert.equal(migrated.cards[0].faction, "ally");
  assert.equal(migrated.cards[0].grade, "none");
  assert.equal(migrated.cards[0].completionStatus, "incomplete");
  assert.equal(migrated.exportFileName, "");
  assert.deepEqual([...migrated.selection], ["a"]);

  core.writeStore(storage, { ...migrated, exportFileName: "독 카드풀" });
  assert.equal(core.readStore(storage).exportFileName, "독 카드풀");
});

test("migrates schema 4 cards to allies while preserving collection state", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 4,
      cards: [
        {
          id: "first",
          name: "기존 조작",
          role: "intervention",
          cost: "1",
          completionStatus: "complete",
        },
        {
          id: "second",
          name: "기존 초안",
          role: "unknown",
          completionStatus: "complete",
        },
      ],
      activeCardId: "second",
      searchQuery: "기존",
      selection: ["first", "second"],
      exportFileName: "기존 카드",
    }),
  });

  const migrated = core.readStore(storage);
  assert.deepEqual([...migrated.cards.map((card) => card.id)], ["first", "second"]);
  assert.deepEqual([...migrated.cards.map((card) => card.faction)], ["ally", "ally"]);
  assert.deepEqual(
    [...migrated.cards.map((card) => card.completionStatus)],
    ["complete", "incomplete"],
  );
  assert.equal(migrated.activeCardId, "second");
  assert.equal(migrated.searchQuery, "기존");
  assert.deepEqual([...migrated.selection], ["first", "second"]);
  assert.equal(migrated.exportFileName, "기존 카드");
});

test("migrates every schema 5 card to no grade while preserving faction state", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 5,
      cards: [
        {
          id: "ally",
          name: "아군 카드",
          faction: "ally",
          grade: "rare",
          role: "intervention",
          cost: "2",
          completionStatus: "complete",
        },
        {
          id: "enemy",
          name: "적군 카드",
          faction: "enemy",
          grade: "advanced",
          role: "execution",
          cost: "",
          executionOrder: "3",
          completionStatus: "complete",
        },
      ],
      activeCardId: "enemy",
      searchQuery: "",
      selection: ["enemy"],
      exportFileName: "",
    }),
  });

  const loaded = core.readStore(storage);
  assert.deepEqual(
    [...loaded.cards.map((card) => ({
      faction: card.faction,
      grade: card.grade,
      role: card.role,
      cost: card.cost,
      completionStatus: card.completionStatus,
    }))],
    [
      {
        faction: "ally",
        grade: "none",
        role: "intervention",
        cost: "2",
        completionStatus: "complete",
      },
      {
        faction: "enemy",
        grade: "none",
        role: "execution",
        cost: "",
        completionStatus: "complete",
      },
    ],
  );
  assert.equal(loaded.activeCardId, "enemy");
  assert.deepEqual([...loaded.selection], ["enemy"]);
});

test("aligns the active card with a preserved selection when loading storage", () => {
  const core = loadCore();

  for (const schemaVersion of [5, 6]) {
    const storage = new MemoryStorage({
      [core.STORAGE_KEY]: JSON.stringify({
        schemaVersion,
        cards: [
          {
            id: "a",
            name: "선택 카드",
            faction: "ally",
            grade: schemaVersion === 6 ? "rare" : undefined,
          },
          {
            id: "b",
            name: "과거 활성 카드",
            faction: "ally",
            grade: schemaVersion === 6 ? "advanced" : undefined,
          },
        ],
        activeCardId: "b",
        searchQuery: "",
        selection: ["a"],
        exportFileName: "",
      }),
    });

    const loaded = core.readStore(storage);
    assert.deepEqual([...loaded.selection], ["a"]);
    assert.equal(loaded.activeCardId, "a");
    assert.equal(core.editTargetCards(loaded)[0].id, "a");
  }
});

test("normalizes Markdown download names and permits an empty name", () => {
  const core = loadCore();
  assert.equal(core.downloadFileName(" 독 카드풀 ", "2026-07-28"), "독 카드풀.md");
  assert.equal(core.downloadFileName("독 카드풀.MD", "2026-07-28"), "독 카드풀.MD");
  assert.equal(core.downloadFileName("독 카드풀.txt", "2026-07-28"), "독 카드풀.txt.md");
  assert.equal(
    core.downloadFileName("   ", "2026-07-28"),
    "fate-weaver-card-ideas-2026-07-28.md",
  );
});

test("accepts only Markdown file names for import", () => {
  const core = loadCore();
  assert.equal(core.isMarkdownFileName("카드풀.md"), true);
  assert.equal(core.isMarkdownFileName("카드풀.MD"), true);
  assert.equal(core.isMarkdownFileName("카드풀.txt"), false);
  assert.equal(core.isMarkdownFileName("카드풀"), false);
});

test("keeps every card in one list without a separate draft state", () => {
  const core = loadCore();
  const state = core.initialState();

  assert.equal(Object.hasOwn(state, "draft"), false);
  const created = core.createCard(state, { id: "a" });
  assert.equal(Object.hasOwn(created, "draft"), false);
  assert.equal(created.cards.length, 1);
});

test("export includes every selected card when all selected cards are complete", () => {
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
    selection: ["a"],
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
    selection: ["a"],
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
  assert.deepEqual([...duplicated.selection], ["b"]);
  assert.deepEqual([...saved.tags], ["독"]);

  const deleted = core.deleteCard(state, "a");
  assert.equal(deleted.cards.length, 0);
  assert.deepEqual([...deleted.selection], []);
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
    { kind: "error", message: "내보낼 카드를 선택하세요." },
  );

  const selected = {
    ...base,
    selection: ["a"],
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

test("migrates schema 1 cards to incomplete schema 6 ally cards", () => {
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
  assert.equal(state.schemaVersion, 6);
  assert.equal(state.cards[0].faction, "ally");
  assert.equal(state.cards[0].grade, "none");
  assert.equal(state.cards[0].completionStatus, "incomplete");
  assert.deepEqual([...state.selection], ["a"]);
});

test("migrates schema 2 export selection into schema 6 shared selection", () => {
  const core = loadCore();
  const storage = new MemoryStorage({
    [core.STORAGE_KEY]: JSON.stringify({
      schemaVersion: 2,
      cards: [
        { id: "a", name: "완성", completionStatus: "complete" },
        { id: "b", name: "미완성", completionStatus: "incomplete" },
      ],
      activeCardId: "b",
      searchQuery: "",
      exportSelection: ["a", "b"],
    }),
  });

  const state = core.readStore(storage);
  assert.equal(state.schemaVersion, 6);
  assert.deepEqual([...state.cards.map((card) => card.faction)], ["ally", "ally"]);
  assert.deepEqual([...state.cards.map((card) => card.grade)], ["none", "none"]);
  assert.deepEqual(
    [...state.cards.map((card) => card.completionStatus)],
    ["incomplete", "incomplete"],
  );
  assert.deepEqual([...state.selection], ["a", "b"]);
});

test("editing a selected complete card keeps it selected while making it incomplete", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [core.normalizeCard({
      id: "a",
      name: "카드",
      completionStatus: "complete",
    })],
    selection: ["a"],
  };

  const edited = core.editCard(state, "a", { notes: "수정" });
  assert.equal(edited.cards[0].completionStatus, "incomplete");
  assert.deepEqual([...edited.selection], ["a"]);
});

test("bulk selection includes complete and incomplete cards", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "완성", completionStatus: "complete" }),
      core.normalizeCard({ id: "b", name: "미완성", completionStatus: "incomplete" }),
    ],
  };

  assert.deepEqual([...core.bulkSelection(state, true).selection], ["a", "b"]);
  assert.deepEqual([...core.bulkSelection(state, false).selection], []);
});

test("selects one card, toggles individuals, and replaces selection with a visible range", () => {
  const core = loadCore();
  const cards = ["a", "b", "c", "d", "e"]
    .map((id) => core.normalizeCard({ id, name: id }));
  const state = {
    ...core.initialState(),
    cards,
    activeCardId: "a",
    selection: ["a"],
  };

  const replaced = core.selectCard(
    state,
    ["a", "b", "c", "d", "e"],
    "b",
    "replace",
    "a",
  );
  assert.deepEqual([...replaced.state.selection], ["b"]);
  assert.equal(replaced.state.activeCardId, "b");
  assert.equal(replaced.anchorId, "b");

  const toggled = core.selectCard(
    replaced.state,
    ["a", "b", "c", "d", "e"],
    "d",
    "toggle",
    replaced.anchorId,
  );
  assert.deepEqual([...toggled.state.selection], ["b", "d"]);
  assert.equal(toggled.state.activeCardId, "d");
  assert.equal(toggled.anchorId, "d");

  const ranged = core.selectCard(
    toggled.state,
    ["b", "d", "e"],
    "e",
    "range",
    "b",
  );
  assert.deepEqual([...ranged.state.selection], ["b", "d", "e"]);
  assert.equal(ranged.state.activeCardId, "e");
  assert.equal(ranged.anchorId, "b");

  const reversed = core.selectCard(
    ranged.state,
    ["b", "d", "e"],
    "b",
    "range",
    "e",
  );
  assert.deepEqual([...reversed.state.selection], ["b", "d", "e"]);

  const fallback = core.selectCard(
    { ...ranged.state, activeCardId: "d" },
    ["b", "d", "e"],
    "b",
    "range",
    "missing",
  );
  assert.deepEqual([...fallback.state.selection], ["b", "d"]);

  const removedActive = core.selectCard(
    { ...state, activeCardId: "d", selection: ["b", "d"] },
    ["a", "b", "c", "d", "e"],
    "d",
    "toggle",
    "d",
  );
  assert.deepEqual([...removedActive.state.selection], ["b"]);
  assert.equal(removedActive.state.activeCardId, "b");

  const emptied = core.selectCard(
    removedActive.state,
    ["a", "b", "c", "d", "e"],
    "b",
    "toggle",
    removedActive.anchorId,
  );
  assert.deepEqual([...emptied.state.selection], []);
  assert.equal(emptied.state.activeCardId, "b");
});

function multiEditState(core) {
  return {
    ...core.initialState(),
    cards: [
      core.normalizeCard({
        id: "ally-a",
        name: "아군 실행",
        faction: "ally",
        grade: "common",
        role: "execution",
        cost: "1",
        executionOrder: "2",
        tags: ["독"],
        targets: { ally: "frontOne", enemy: "frontOne" },
        abilities: { ally: ["방어"], enemy: ["피해"], none: ["드로우"] },
        notes: "첫 메모",
        completionStatus: "complete",
      }),
      core.normalizeCard({
        id: "ally-b",
        name: "아군 조작",
        faction: "ally",
        grade: "rare",
        role: "intervention",
        cost: "2",
        executionOrder: "9",
        tags: ["독"],
        targets: { ally: "frontOne", enemy: "frontOne" },
        abilities: { ally: ["방어"], enemy: ["피해"], none: ["드로우"] },
        notes: "둘째 메모",
        completionStatus: "complete",
      }),
      core.normalizeCard({
        id: "enemy",
        name: "적 실행",
        faction: "enemy",
        grade: "rare",
        executionOrder: "4",
        tags: ["독"],
        targets: { ally: "frontOne", enemy: "frontOne" },
        abilities: { ally: ["방어"], enemy: ["피해"], none: ["드로우"] },
        notes: "셋째 메모",
        completionStatus: "complete",
      }),
    ],
    activeCardId: "ally-a",
    selection: ["ally-a", "ally-b", "enemy"],
  };
}

test("aggregates only cards that can edit each field", () => {
  const core = loadCore();
  const state = multiEditState(core);

  assert.deepEqual(
    [...core.editTargetCards(state).map((card) => card.id)],
    ["ally-a", "ally-b", "enemy"],
  );
  assert.deepEqual(
    [...core.editTargetCards({
      ...state,
      activeCardId: "ally-b",
      selection: [],
    }).map((card) => card.id)],
    ["ally-b"],
  );
  assert.deepEqual({ ...core.fieldAggregate(state, "grade") }, {
    kind: "mixed",
    value: "",
    applicableCount: 2,
  });
  assert.deepEqual({ ...core.fieldAggregate(state, "tags") }, {
    kind: "common",
    value: "독",
    applicableCount: 3,
  });
  assert.deepEqual({ ...core.fieldAggregate(state, "abilities.enemy") }, {
    kind: "common",
    value: "피해",
    applicableCount: 3,
  });
  assert.deepEqual({ ...core.fieldAggregate(state, "executionOrder") }, {
    kind: "mixed",
    value: "",
    applicableCount: 2,
  });
  assert.deepEqual({
    ...core.fieldAggregate({
      ...state,
      activeCardId: "enemy",
      selection: ["enemy"],
    }, "grade"),
  }, {
    kind: "empty",
    value: "",
    applicableCount: 0,
  });
  assert.deepEqual({
    ...core.fieldAggregate({
      ...state,
      activeCardId: "ally-b",
      selection: [],
    }, "grade"),
  }, {
    kind: "common",
    value: "rare",
    applicableCount: 1,
  });
});

test("bulk edits only compatible selected cards and preserves unchanged cards", () => {
  const core = loadCore();
  const state = multiEditState(core);

  const graded = core.editSelectedField(state, "grade", "rare");
  assert.deepEqual(
    [...graded.cards.map((card) => [card.grade, card.completionStatus])],
    [
      ["rare", "incomplete"],
      ["rare", "complete"],
      ["none", "complete"],
    ],
  );

  const renamed = core.editSelectedField(state, "name", "같은 이름");
  assert.deepEqual([...renamed.cards.map((card) => card.name)], [
    "같은 이름",
    "같은 이름",
    "같은 이름",
  ]);

  const factionChanged = core.editSelectedField(state, "faction", "enemy");
  assert.deepEqual(
    [...factionChanged.cards.map((card) => ({
      faction: card.faction,
      grade: card.grade,
      role: card.role,
      cost: card.cost,
      completionStatus: card.completionStatus,
    }))],
    [
      {
        faction: "enemy",
        grade: "none",
        role: "execution",
        cost: "",
        completionStatus: "incomplete",
      },
      {
        faction: "enemy",
        grade: "none",
        role: "execution",
        cost: "",
        completionStatus: "incomplete",
      },
      {
        faction: "enemy",
        grade: "none",
        role: "execution",
        cost: "",
        completionStatus: "complete",
      },
    ],
  );

  const reordered = core.editSelectedField(state, "executionOrder", "7");
  assert.deepEqual(
    [...reordered.cards.map((card) => [card.executionOrder, card.completionStatus])],
    [
      ["7", "incomplete"],
      ["9", "complete"],
      ["7", "incomplete"],
    ],
  );

  const recosted = core.editSelectedField(state, "cost", "5");
  assert.deepEqual(
    [...recosted.cards.map((card) => [card.cost, card.completionStatus])],
    [
      ["5", "incomplete"],
      ["5", "incomplete"],
      ["", "complete"],
    ],
  );

  const rerolled = core.editSelectedField(state, "role", "intervention");
  assert.deepEqual(
    [...rerolled.cards.map((card) => [card.role, card.completionStatus])],
    [
      ["intervention", "incomplete"],
      ["intervention", "complete"],
      ["execution", "complete"],
    ],
  );

  const retargeted = core.editSelectedField(state, "targets.ally", "backTwo");
  assert.deepEqual(
    [...retargeted.cards.map((card) => [card.targets.ally, card.targets.enemy])],
    [
      ["backTwo", "frontOne"],
      ["backTwo", "frontOne"],
      ["backTwo", "frontOne"],
    ],
  );

  const retagged = core.editSelectedField(state, "tags", "독, 소비");
  assert.deepEqual(
    [...retagged.cards.map((card) => [...card.tags])],
    [
      ["독", "소비"],
      ["독", "소비"],
      ["독", "소비"],
    ],
  );

  const reworded = core.editSelectedField(
    state,
    "abilities.none",
    "운명력을 얻는다.\n카드를 뽑는다.",
  );
  assert.deepEqual(
    [...reworded.cards.map((card) => ({
      none: [...card.abilities.none],
      enemy: [...card.abilities.enemy],
    }))],
    [
      { none: ["운명력을 얻는다.", "카드를 뽑는다."], enemy: ["피해"] },
      { none: ["운명력을 얻는다.", "카드를 뽑는다."], enemy: ["피해"] },
      { none: ["운명력을 얻는다.", "카드를 뽑는다."], enemy: ["피해"] },
    ],
  );

  assert.equal(core.editSelectedField(state, "tags", "독"), state);

  const activeOnly = core.editSelectedField({
    ...state,
    activeCardId: "ally-b",
    selection: [],
  }, "notes", "활성 카드만");
  assert.deepEqual([...activeOnly.cards.map((card) => card.notes)], [
    "첫 메모",
    "활성 카드만",
    "셋째 메모",
  ]);
});

test("inserts a dragged card before or after a target without changing selection or active card", () => {
  const core = loadCore();
  const cards = ["a", "b", "c", "d"].map((id) => core.normalizeCard({
    id,
    name: id,
    completionStatus: "complete",
  }));
  const state = {
    ...core.initialState(),
    cards,
    activeCardId: "b",
    selection: ["a", "c"],
  };

  const after = core.reorderCards(state, "a", "c", "after");
  assert.deepEqual([...after.cards.map((card) => card.id)], ["b", "c", "a", "d"]);
  assert.equal(after.activeCardId, "b");
  assert.deepEqual([...after.selection], ["a", "c"]);

  const before = core.reorderCards(state, "d", "b", "before");
  assert.deepEqual([...before.cards.map((card) => card.id)], ["a", "d", "b", "c"]);
  assert.strictEqual(core.reorderCards(state, "b", "b", "before"), state);
});

test("exports selected cards in the reordered list order", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: ["a", "b", "c"].map((id) => core.normalizeCard({
      id,
      name: id,
      completionStatus: "complete",
    })),
    selection: ["a", "c"],
  };

  const reordered = core.reorderCards(state, "c", "a", "before");
  assert.deepEqual(
    [...core.cardsForExport(reordered).map((card) => card.id)],
    ["c", "a"],
  );
});

test("chooses selected cards for deletion and falls back to the active card", () => {
  const core = loadCore();
  const base = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "첫 카드" }),
      core.normalizeCard({ id: "b", name: "둘째 카드" }),
      core.normalizeCard({ id: "c", name: "셋째 카드" }),
    ],
    activeCardId: "b",
  };

  assert.deepEqual([...core.deletionIds({ ...base, selection: ["a", "c"] })], ["a", "c"]);
  assert.deepEqual([...core.deletionIds(base)], ["b"]);
});

test("bulk deletion preserves an unselected active card and selects a fallback when active is deleted", () => {
  const core = loadCore();
  const cards = [
    core.normalizeCard({ id: "a", name: "첫 카드" }),
    core.normalizeCard({ id: "b", name: "둘째 카드" }),
    core.normalizeCard({ id: "c", name: "셋째 카드" }),
  ];
  const kept = core.deleteCards({
    ...core.initialState(),
    cards,
    activeCardId: "b",
    selection: ["a", "c"],
  }, ["a", "c"]);
  assert.equal(kept.activeCardId, "b");
  assert.deepEqual([...kept.cards.map((card) => card.id)], ["b"]);

  const fallback = core.deleteCards({
    ...core.initialState(),
    cards,
    activeCardId: "b",
    selection: ["b", "c"],
  }, ["b", "c"]);
  assert.equal(fallback.activeCardId, "a");
  assert.deepEqual([...fallback.selection], []);
});

test("blocks all export when selection contains an incomplete card", () => {
  const core = loadCore();
  const state = {
    ...core.initialState(),
    cards: [
      core.normalizeCard({ id: "a", name: "완성", completionStatus: "complete" }),
      core.normalizeCard({ id: "b", name: "미완성", completionStatus: "incomplete" }),
    ],
    selection: ["a", "b"],
  };

  assert.deepEqual({ ...core.exportStatus(state) }, {
    kind: "error",
    message: "미완성 카드는 내보낼 수 없습니다. 먼저 완성 상태로 저장하거나 선택을 해제하세요.",
  });
  assert.deepEqual([...core.cardsForExport(state)], []);
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
  assert.deepEqual([...second.selection], ["b"]);
});

test("keeps generated card IDs unique when an ID source collides", () => {
  const core = loadCore();
  const first = core.createCard(core.initialState(), { id: "card" });
  const second = core.createCard(first, { id: "card" });
  const duplicated = core.duplicateCard(second, "card", { id: "card" });
  const markdown = core.bundleMarkdown([
    core.normalizeCard({ name: "가져온 카드", completionStatus: "complete" }),
    core.normalizeCard({ name: "가져온 카드 둘", completionStatus: "complete" }),
  ], "2026-07-27");
  const imported = core.importCards(duplicated, markdown, { ids: ["card", "card"] });

  assert.deepEqual([...imported.cards.map((card) => card.id)], [
    "card",
    "card-2",
    "card-3",
    "card-4",
    "card-5",
  ]);
});

test("editing a complete card makes it incomplete and keeps it selected", () => {
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
    selection: ["a"],
  };

  const edited = core.editCard(state, "a", { notes: "수정됨" });
  assert.equal(edited.cards[0].notes, "수정됨");
  assert.equal(edited.cards[0].completionStatus, "incomplete");
  assert.deepEqual([...edited.selection], ["a"]);
});

test("saves complete and incomplete current cards from core information", () => {
  const core = loadCore();
  const created = core.createCard(core.initialState(), {
    id: "a",
    now: "2026-07-27T00:00:00.000Z",
  });
  const valid = core.editCard(created, "a", {
    role: "intervention",
    cost: "1",
  });
  const completed = core.saveCard(valid, "a", {
    now: "2026-07-27T00:02:00.000Z",
  });
  assert.equal(completed.cards[0].completionStatus, "complete");
  assert.equal(completed.cards[0].updatedAt, "2026-07-27T00:02:00.000Z");

  const emptyName = core.editCard(completed, "a", { name: "" });
  const incomplete = core.saveCard(emptyName, "a");
  assert.equal(incomplete.cards[0].completionStatus, "incomplete");
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

test("emits and round-trips ally and enemy faction and grade metadata", () => {
  const core = loadCore();
  const ally = core.normalizeCard({
    name: "아군 실행",
    faction: "ally",
    grade: "rare",
    cost: "1",
    role: "execution",
    executionOrder: "4",
    completionStatus: "complete",
  });
  const enemy = core.normalizeCard({
    name: "적군 실행",
    faction: "enemy",
    executionOrder: "2",
    completionStatus: "complete",
  });

  assert.match(
    core.cardMarkdown(ally),
    /- 진영: 아군\n- 등급: 희귀\n- 비용: 1\n- 역할: 실행\n- 실행순서: 4/,
  );
  assert.match(
    core.cardMarkdown(enemy),
    /- 진영: 적군\n- 등급: 없음\n- 비용: 없음\n- 역할: 실행\n- 실행순서: 2/,
  );

  const parsed = core.parseBundleMarkdown(
    core.bundleMarkdown([ally, enemy], "2026-07-28"),
  );
  assert.deepEqual(
    [...parsed.map((card) => ({
      faction: card.faction,
      grade: card.grade,
      cost: card.cost,
      role: card.role,
      completionStatus: card.completionStatus,
    }))],
    [
      {
        faction: "ally",
        grade: "rare",
        cost: "1",
        role: "execution",
        completionStatus: "complete",
      },
      {
        faction: "enemy",
        grade: "none",
        cost: "",
        role: "execution",
        completionStatus: "complete",
      },
    ],
  );
});

test("imports legacy Markdown without faction as an ally draft", () => {
  const core = loadCore();
  const legacy = `# Fate Weaver 카드 아이디어

- 생성일: 2026-07-27
- 카드 수: 1
- 대상 규칙: \`docs/superpowers/specs/2026-07-27-position-targeting-card-text-design.md\`

## 구형 초안

- 역할: 미정
- 대상: 없음
`;

  const parsed = core.parseBundleMarkdown(legacy);
  assert.equal(parsed[0].faction, "ally");
  assert.equal(parsed[0].grade, "none");
  assert.equal(parsed[0].completionStatus, "incomplete");

  const imported = core.importCards(core.initialState(), legacy, {
    ids: ["legacy"],
    now: "2026-07-28T00:00:00.000Z",
  });
  assert.equal(imported.cards[0].faction, "ally");
  assert.equal(imported.cards[0].grade, "none");
  assert.equal(imported.cards[0].completionStatus, "incomplete");
});

test("rejects an unknown Markdown grade", () => {
  const core = loadCore();
  const source = core.normalizeCard({
    name: "등급 오류",
    faction: "ally",
    grade: "common",
    role: "intervention",
    cost: "1",
    completionStatus: "complete",
  });
  const markdown = core.bundleMarkdown([source], "2026-07-29")
    .replace("- 진영: 아군", "- 진영: 아군\n- 등급: 전설");

  assert.throws(
    () => core.parseBundleMarkdown(markdown),
    /알 수 없는 등급: 전설/,
  );
});

test("rejects a non-none enemy Markdown grade", () => {
  const core = loadCore();
  const source = core.normalizeCard({
    name: "적군 등급 오류",
    faction: "enemy",
    executionOrder: "1",
    completionStatus: "complete",
  });
  const markdown = core.bundleMarkdown([source], "2026-07-29")
    .replace("- 등급: 없음", "- 등급: 고급");

  assert.throws(
    () => core.parseBundleMarkdown(markdown),
    /적군 등급은 없음이어야 합니다/,
  );
});

test("round-trips note lines that resemble card headings", () => {
  const core = loadCore();
  const notes = "첫 줄\n## 새 카드처럼 보이는 메모\n\\## 백슬래시가 있는 메모";
  const source = [
    core.normalizeCard({
      name: "메모 카드",
      role: "unknown",
      notes,
      completionStatus: "complete",
    }),
  ];

  const parsed = core.parseBundleMarkdown(core.bundleMarkdown(source, "2026-07-27"));
  assert.equal(parsed[0].notes, notes);
});

test("round-trips targetless abilities that resemble faction markers", () => {
  const core = loadCore();
  const targetless = ["[적군] 표식에 관한 설명.", "[아군] 표식에 관한 설명.", "\\[적군] 원문"];
  const source = [
    core.normalizeCard({
      name: "표식 설명",
      role: "unknown",
      abilities: { none: targetless },
      completionStatus: "complete",
    }),
  ];

  const parsed = core.parseBundleMarkdown(core.bundleMarkdown(source, "2026-07-27"));
  assert.deepEqual([...parsed[0].abilities.none], targetless);
  assert.deepEqual([...parsed[0].abilities.enemy], []);
  assert.deepEqual([...parsed[0].abilities.ally], []);
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
  assert.deepEqual([...imported.selection], ["b"]);
  assert.equal(imported.cards[2].completionStatus, "incomplete");
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
