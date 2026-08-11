import assert from "node:assert/strict";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import test from "node:test";
import { fileURLToPath } from "node:url";

const htmlUrl = new URL("./index.html", import.meta.url);

function loadCore() {
  assert.equal(existsSync(htmlUrl), true, "index.html must exist");
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const match = html.match(/<script data-card-idea-core>([\s\S]*?)<\/script>/);
  assert.ok(match, "index.html must expose the card idea core script");

  // 코어를 이 realm에서 돌린다. vm.runInNewContext는 별도 realm을 만들어 코어가 만든 배열·객체가
  // 호스트의 Array.prototype·Object.prototype을 갖지 않고, node:assert/strict의 deepEqual이
  // 프로토타입 동일성까지 보므로 값이 같아도 실패한다. globalThis를 인자로 가려 코어의 export만
  // 받아내면 realm은 하나로 유지되고, DOM 접근 차단은 Node에 document·window가 없다는 사실이 맡는다.
  const context = { CardIdeaNotebook: null };
  new Function("globalThis", `${match[1]}\n//# sourceURL=card-idea-core.js`)(context);
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

test("builds save picker options from the export name", () => {
  const core = loadCore();
  const options = core.saveFilePickerOptions(" 독 카드풀 ", "2026-07-28");

  assert.equal(options.suggestedName, "독 카드풀.md");
  assert.equal(options.id, core.SAVE_PICKER_ID);
  assert.equal(options.types.length, 1);
  assert.equal(options.types[0].description, "Markdown 파일");
  assert.deepEqual(Object.keys(options.types[0].accept), ["text/markdown"]);
  assert.deepEqual([...options.types[0].accept["text/markdown"]], [".md"]);
  assert.equal(
    core.saveFilePickerOptions("", "2026-07-28").suggestedName,
    "fate-weaver-card-ideas-2026-07-28.md",
  );
});

test("keeps the save picker id within the browser limit", () => {
  const core = loadCore();

  assert.match(core.SAVE_PICKER_ID, /^[A-Za-z0-9_-]{1,32}$/);
});

test("treats only a cancelled picker as a silent abort", () => {
  const core = loadCore();

  assert.equal(core.isPickerCancel({ name: "AbortError" }), true);
  assert.equal(core.isPickerCancel(new Error("boom")), false);
  assert.equal(core.isPickerCancel({ name: "SecurityError" }), false);
  assert.equal(core.isPickerCancel(undefined), false);
});

test("detects save picker support without assuming a browser", () => {
  const core = loadCore();

  assert.equal(core.supportsSavePicker({ showSaveFilePicker: () => {} }), true);
  assert.equal(core.supportsSavePicker({ showSaveFilePicker: "nope" }), false);
  assert.equal(core.supportsSavePicker({}), false);
  assert.equal(core.supportsSavePicker(undefined), false);
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

const schemaUrl = new URL("./authoring-schema.json", import.meta.url);

function loadSchema() {
  const core = loadCore();
  return core.parseAuthoringSchema(readFileSync(fileURLToPath(schemaUrl), "utf8"));
}

test("생성된 스키마에서 효과 여덟 종을 읽는다", () => {
  const schema = loadSchema();
  assert.equal(schema.effectOrder.length, 8);
  assert.ok(schema.effects.apply_status, "apply_status가 있어야 한다");
  assert.equal(schema.effects.apply_status.label, "상태 부여");
});

test("효과 필드의 이름과 순서를 저작 파일과 같게 읽는다", () => {
  const schema = loadSchema();
  const names = schema.effects.apply_status.fields.map((field) => field.name);
  assert.deepEqual(names, ["status", "count", "target", "selector"]);
});

test("필드 타입과 열거 항목을 읽는다", () => {
  const schema = loadSchema();
  const fields = schema.effects.apply_status.fields;
  assert.equal(fields.find((f) => f.name === "count").type, "int");
  assert.equal(fields.find((f) => f.name === "status").type, "status");
  const target = fields.find((f) => f.name === "target");
  assert.equal(target.type, "enum");
  assert.ok(target.options.includes("TargetEnemy"));
});

test("분류별 카드 키 순서를 저작 파일과 같게 읽는다", () => {
  const schema = loadSchema();
  assert.deepEqual(schema.cardFields.Execution, [
    "id", "name", "side", "category", "energyCost", "baseExecutionOrder",
    "effects", "grade", "tags",
  ]);
  assert.deepEqual(schema.cardFields.Intervention, [
    "id", "name", "side", "category", "energyCost", "intervention", "grade", "tags",
  ]);
});

test("개입 세 종을 효과와 같은 모양으로 읽는다", () => {
  const schema = loadSchema();
  assert.deepEqual(schema.interventionOrder,
    ["change_execution_order", "swap_execution_order", "lock"]);

  const change = schema.interventions.change_execution_order;
  assert.equal(change.label, "실행 순서 변경");
  assert.deepEqual(change.fields.map((f) => f.name), ["delta", "targetSide"]);
  assert.equal(change.fields.find((f) => f.name === "delta").type, "int");
  assert.deepEqual(change.fields.find((f) => f.name === "targetSide").options,
    ["Any", "Player", "Enemy"]);

  assert.deepEqual(schema.interventions.lock.fields, [],
    "lock은 파라미터가 없다 — 계획 3.5의 결과다");
});

test("스키마가 깨지면 이유를 던진다", () => {
  const core = loadCore();
  assert.throws(() => core.parseAuthoringSchema("{}"), /effects/);
  assert.throws(() => core.parseAuthoringSchema("not json"), /스키마/);
});

const cardsDir = new URL("../../Assets/StreamingAssets/Content/Cards/", import.meta.url);

function readCardFile(name) {
  return readFileSync(fileURLToPath(new URL(name, cardsDir)), "utf8");
}

test("실행 카드를 모델로 읽는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card, errors } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.deepEqual(errors, []);
  assert.equal(card.id, "vanguard_slash");
  assert.equal(card.name, "선봉 베기");
  assert.equal(card.side, "Player");
  assert.equal(card.category, "Execution");
  assert.equal(card.energyCost, 1);
  assert.equal(card.baseExecutionOrder, 3);
  assert.equal(card.grade, "Common");
  assert.deepEqual(card.tags, ["시작", "공격"]);
  assert.equal(card.effects.length, 1);
  assert.deepEqual(card.effects[0].params, { value: 5, selector: "FrontOne" });
  assert.equal(card.effects[0].condition, null);
});

test("조건부 효과의 조건을 읽는다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("riposte.json"), loadSchema());
  assert.deepEqual(card.effects[0].condition, {
    kind: "PrevExecutedIsEnemyDamageCard", n: 0, successEffectValue: 7, skipOnBasic: false,
  });
});

test("개입 카드를 중첩 스펙으로 읽는다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("hasten.json"), loadSchema());
  assert.equal(card.category, "Intervention");
  assert.equal(card.effects, null, "개입 카드에는 effects가 없다");
  assert.equal(card.intervention.kind, "change_execution_order");
  assert.deepEqual(card.intervention.params, { delta: -1, targetSide: "Player" });
});

test("파라미터가 없는 개입도 읽는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = JSON.stringify({
    id: "seal", name: "봉인", side: "Player", category: "Intervention",
    energyCost: 1, intervention: { kind: "lock" },
  }, null, 2);
  const { card, errors } = core.readCardJson(text, schema);
  assert.deepEqual(errors, []);
  assert.equal(card.intervention.kind, "lock");
  assert.deepEqual(card.intervention.params, {});
});

test("생략된 개입 파라미터는 모델에 나타나지 않는다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("crossover.json"), loadSchema());
  assert.equal(card.intervention.kind, "swap_execution_order");
  assert.deepEqual(card.intervention.params, { requireAdjacent: true },
    "targetSide는 Any라 파일에 없고 모델에도 없어야 한다");
});

test("빈 배열과 없는 배열을 구분한다", () => {
  const core = loadCore();
  const { card } = core.readCardJson(readCardFile("fixture_attack.json"), loadSchema());
  assert.deepEqual(card.tags, [], "tags는 빈 배열로 저작되어 있다");
  assert.equal(card.grade, "None", "grade는 생략되어 있다");
});

test("모르는 효과 kind를 버리지 않고 보존한다", () => {
  const core = loadCore();
  const text = JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution",
    effects: [{ kind: "teleport", distance: 3 }],
  }, null, 2);
  const { card, errors } = core.readCardJson(text, loadSchema());
  assert.deepEqual(errors, []);
  assert.equal(card.effects[0].kind, "teleport");
  assert.deepEqual(card.effects[0].raw, { kind: "teleport", distance: 3 });
});

test("모르는 최상위 키를 보존하고 이름을 알려준다", () => {
  const core = loadCore();
  const text = JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution", flavour: "설명",
  }, null, 2);
  const { card } = core.readCardJson(text, loadSchema());
  assert.deepEqual(card.unknownKeys, ["flavour"]);
  assert.equal(card.extra.flavour, "설명");
});

test("깨진 JSON은 카드를 만들지 않고 이유를 준다", () => {
  const core = loadCore();
  const { card, errors } = core.readCardJson("{ 이건 JSON이 아니다", loadSchema());
  assert.equal(card, null);
  assert.equal(errors.length, 1);
});

test("필수 키가 빠지면 이유를 준다", () => {
  const core = loadCore();
  const { card, errors } = core.readCardJson('{"id":"x","name":"y"}', loadSchema());
  assert.equal(card, null);
  assert.ok(errors.some((message) => message.includes("side")));
});

test("저장소의 모든 카드가 바이트 그대로 왕복한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const names = readdirSync(fileURLToPath(cardsDir)).filter((n) => n.endsWith(".json"));
  assert.ok(names.length >= 26, `카드가 26장 이상이어야 한다. 실제 ${names.length}`);

  const broken = [];
  for (const name of names) {
    const original = readCardFile(name);
    const { card, errors } = core.readCardJson(original, schema);
    if (errors.length) {
      broken.push(`${name}: ${errors.join(", ")}`);
      continue;
    }
    const written = core.writeCardJson(card, schema);
    if (written !== original) broken.push(name);
  }

  assert.deepEqual(broken, [], "왕복에서 바뀐 카드가 없어야 한다");
});

test("파일 끝에 개행을 하나 붙인다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const written = core.writeCardJson(card, schema);
  assert.ok(written.endsWith("}\n"));
  assert.ok(!written.endsWith("}\n\n"));
});

test("기본값 멤버를 생략하되 side와 category는 항상 쓴다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution",
  }, null, 2), schema);
  const written = JSON.parse(core.writeCardJson(card, schema));
  assert.deepEqual(Object.keys(written), ["id", "name", "side", "category"]);
});

test("분류에 없는 키는 모델에 있어도 나가지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  // 실행 카드 모델에 개입을 억지로 넣어도 실행 카드의 키 목록에 없으므로 무시된다.
  card.intervention = { kind: "lock", params: {}, raw: null };
  const written = JSON.parse(core.writeCardJson(card, schema));

  assert.equal(written.intervention, undefined);
  assert.ok(written.effects, "실행 카드의 효과는 그대로 나간다");
});

test("파라미터 없는 개입은 kind만 쓴다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "seal", name: "봉인", side: "Player", category: "Intervention",
    energyCost: 1, intervention: { kind: "lock" },
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original);
});

test("모르는 효과 kind를 원본 그대로 되돌린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution",
    effects: [{ kind: "teleport", distance: 3 }],
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original);
});

test("모르는 최상위 키를 원본 그대로 되돌린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution", flavour: "설명",
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original);
});

const poolsDir = new URL("../../Assets/StreamingAssets/Content/Pools/", import.meta.url);

test("풀을 읽고 카드 순서를 그대로 보존한다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool, errors } = core.readPoolJson(text);
  assert.deepEqual(errors, []);
  assert.equal(pool.id, "starter");
  assert.equal(pool.cards.length, 22);
  assert.equal(pool.cards[0], "vanguard_slash");
  assert.equal(pool.cards[21], "posthumous_spread");
});

test("저장소의 모든 풀이 바이트 그대로 왕복한다", () => {
  const core = loadCore();
  const names = readdirSync(fileURLToPath(poolsDir)).filter((n) => n.endsWith(".json"));
  assert.ok(names.length >= 1);
  for (const name of names) {
    const original = readFileSync(fileURLToPath(new URL(name, poolsDir)), "utf8");
    const { pool } = core.readPoolJson(original);
    assert.equal(core.writePoolJson(pool), original, name);
  }
});

test("중복 카드를 지우지 않고 그대로 들고 있는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","a","b"]}');
  assert.deepEqual(pool.cards, ["a", "a", "b"]);
});

test("깨진 풀은 이유를 준다", () => {
  const core = loadCore();
  const broken = core.readPoolJson("{ 아님");
  assert.equal(broken.pool, null);
  assert.equal(broken.errors.length, 1);

  const missing = core.readPoolJson('{"id":"p"}');
  assert.equal(missing.pool, null);
  assert.ok(missing.errors.some((message) => message.includes("cards")));
});

function cardOf(core, schema, overrides) {
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution", ...overrides,
  }, null, 2), schema);
  return card;
}

function poolOf(core, cards) {
  return core.readPoolJson(JSON.stringify({ id: "starter", cards })).pool;
}

const STATUS_KEYS = ["poison", "block", "haste"];

test("id 형식과 중복을 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const bad = core.validateContent({
    cards: [cardOf(core, schema, { id: "Vanguard Slash" })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(bad.errors.some((e) => e.message.includes("형식")));

  const dupe = core.validateContent({
    cards: [cardOf(core, schema, {}), cardOf(core, schema, {})],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(dupe.errors.some((e) => e.message.includes("중복")));
});

test("개입 카드의 액션을 검사한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const missing = core.validateContent({
    cards: [cardOf(core, schema, { category: "Intervention" })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(missing.errors.some((e) => e.message.includes("개입 액션")));

  const unknown = core.validateContent({
    cards: [cardOf(core, schema, {
      category: "Intervention", intervention: { kind: "teleport" },
    })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(unknown.errors.some((e) => e.message.includes("teleport")));
});

test("등록되지 않은 상태 키를 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, {
      effects: [{ kind: "apply_status", status: "posion", count: 1 }],
    })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("posion")));
});

test("consume_status의 maxAmount 하한을 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, {
      effects: [{ kind: "consume_status", status: "poison", maxAmount: 0 }],
    })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("maxAmount")));
});

test("풀 소속 카드에만 등급과 태그를 요구한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const card = cardOf(core, schema, { tags: [] });

  const free = core.validateContent({
    cards: [card], pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.deepEqual(free.errors, [], "풀에 없으면 등급·태그가 없어도 정상이다");

  const pooled = core.validateContent({
    cards: [card], pools: [poolOf(core, ["probe"])], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(pooled.errors.some((e) => e.message.includes("등급")));
  assert.ok(pooled.errors.some((e) => e.message.includes("태그")));
});

test("풀의 없는 카드와 중복을 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, { grade: "Common", tags: ["시작"] })],
    pools: [poolOf(core, ["probe", "probe", "ghost"])],
    statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("ghost")));
  assert.ok(result.errors.some((e) => e.message.includes("중복")));
});

test("효과 없는 실행 카드와 고아 카드는 경고에 그친다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, { effects: [] })],
    pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.deepEqual(result.errors, []);
  assert.ok(result.warnings.length >= 2, "효과 0개 경고와 고아 경고");
});

test("모르는 최상위 키는 부팅 거부라고 알린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution", flavour: "설명",
  }, null, 2), schema);
  const result = core.validateContent({
    cards: [card], pools: [], statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("flavour")));
});

test("풀 소속 카드의 중복 태그와 빈 태그를 잡는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const result = core.validateContent({
    cards: [cardOf(core, schema, { grade: "Common", tags: ["시작", "시작", " "] })],
    pools: [poolOf(core, ["probe"])],
    statusKeys: STATUS_KEYS, schema,
  });
  assert.ok(result.errors.some((e) => e.message.includes("중복 태그")));
  assert.ok(result.errors.some((e) => e.message.includes("빈 태그")));
});

test("저장소의 실제 콘텐츠가 검증을 통과한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const statusesDir = new URL("../../Assets/StreamingAssets/Content/Statuses/", import.meta.url);
  const statusKeys = readdirSync(fileURLToPath(statusesDir))
    .filter((n) => n.endsWith(".json"))
    .map((n) => JSON.parse(readFileSync(fileURLToPath(new URL(n, statusesDir)), "utf8")).key);

  const cards = readdirSync(fileURLToPath(cardsDir))
    .filter((n) => n.endsWith(".json"))
    .map((n) => core.readCardJson(readCardFile(n), schema).card);
  const pools = readdirSync(fileURLToPath(poolsDir))
    .filter((n) => n.endsWith(".json"))
    .map((n) => core.readPoolJson(
      readFileSync(fileURLToPath(new URL(n, poolsDir)), "utf8")).pool);

  const result = core.validateContent({ cards, pools, statusKeys, schema });
  assert.deepEqual(result.errors, [], "부팅이 받아들이는 콘텐츠는 오류가 없어야 한다");
});

test("저장소와 같으면 same이다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  assert.equal(core.resolveCardState({ stored: text, pending: card, schema }), "same");
});

test("노트북 쪽이 다르면 modified다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  card.name = "바뀐 이름";
  assert.equal(core.resolveCardState({ stored: text, pending: card, schema }), "modified");
});

test("저장소에 없으면 new다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  card.base = null;
  assert.equal(core.resolveCardState({ stored: null, pending: card, schema }), "new");
});

test("양쪽이 다 바뀌었으면 conflict다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  card.name = "내 변경";
  const stored = text.replace("선봉 베기", "남의 변경");
  assert.equal(core.resolveCardState({ stored, pending: card, schema }), "conflict");
});

test("저장소만 바뀌었고 미반영이 없으면 same으로 받아들인다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(text, schema);
  const stored = text.replace("선봉 베기", "남의 변경");
  assert.equal(core.resolveCardState({ stored, pending: card, schema }), "same");
});

test("노트북에 없고 저장소에만 있으면 missing이다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const text = readCardFile("vanguard_slash.json");
  assert.equal(core.resolveCardState({ stored: text, pending: null, schema }), "missing");
});

test("풀도 같은 다섯 상태로 판정한다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool } = core.readPoolJson(text);

  assert.equal(core.resolvePoolState({ stored: text, pending: pool }), "same");
  assert.equal(core.resolvePoolState({ stored: text, pending: null }), "missing");

  const edited = core.readPoolJson(text).pool;
  edited.cards.push("새_카드");
  assert.equal(core.resolvePoolState({ stored: text, pending: edited }), "modified");

  const moved = text.replace("vanguard_slash", "남이_바꾼_카드");
  assert.equal(core.resolvePoolState({ stored: moved, pending: edited }), "conflict");
  assert.equal(core.resolvePoolState({ stored: moved, pending: pool }), "same");
});

const statusesDir = new URL("../../Assets/StreamingAssets/Content/Statuses/", import.meta.url);

function readStatusFile(name) {
  return readFileSync(fileURLToPath(new URL(name, statusesDir)), "utf8");
}

test("콘텐츠 경로 넷을 세그먼트 배열로 노출한다", () => {
  const core = loadCore();
  assert.deepEqual(core.CONTENT_PATHS.schema,
    ["Tools", "card-idea-notebook", "authoring-schema.json"]);
  assert.deepEqual(core.CONTENT_PATHS.statuses,
    ["Assets", "StreamingAssets", "Content", "Statuses"]);
  assert.deepEqual(core.CONTENT_PATHS.cards,
    ["Assets", "StreamingAssets", "Content", "Cards"]);
  assert.deepEqual(core.CONTENT_PATHS.pools,
    ["Assets", "StreamingAssets", "Content", "Pools"]);
});

test("상태 파일에서 키와 표시 이름만 읽는다", () => {
  const core = loadCore();
  const { status, errors } = core.readStatusJson(readStatusFile("poison.json"));
  assert.deepEqual(errors, []);
  assert.equal(status.key, "poison");
  assert.equal(status.displayName, "독");
  assert.equal(status.base, readStatusFile("poison.json"));
  assert.deepEqual(Object.keys(status), ["key", "displayName", "base"],
    "수명·성장치는 읽지 않는다 - 노트북이 상태를 쓰지 않는다");
});

test("표시 이름이 없으면 키를 대신 쓴다", () => {
  const core = loadCore();
  const { status } = core.readStatusJson('{"key":"mystery"}');
  assert.equal(status.displayName, "mystery");
});

test("저장소의 상태 열한 개를 전부 읽는다", () => {
  const core = loadCore();
  const names = readdirSync(fileURLToPath(statusesDir)).filter((n) => n.endsWith(".json"));
  assert.ok(names.length >= 11, `상태가 11개 이상이어야 한다. 실제 ${names.length}`);

  const keys = [];
  for (const name of names) {
    const { status, errors } = core.readStatusJson(readStatusFile(name));
    assert.deepEqual(errors, [], name);
    keys.push(status.key);
  }

  assert.ok(keys.includes("poison"));
  assert.ok(keys.includes("block"));
  assert.equal(new Set(keys).size, keys.length, "상태 키가 중복이면 안 된다");
});

test("깨진 상태 파일과 키 없는 상태는 이유를 준다", () => {
  const core = loadCore();
  const broken = core.readStatusJson("{ 아님");
  assert.equal(broken.status, null);
  assert.equal(broken.errors.length, 1);

  const missing = core.readStatusJson('{"displayName":"독"}');
  assert.equal(missing.status, null);
  assert.ok(missing.errors.some((message) => message.includes("key")));
});

function cardsByIdOf(core, schema, specs) {
  const map = new Map();
  for (const spec of specs) {
    const { card } = core.readCardJson(JSON.stringify({
      id: spec.id, name: spec.id, side: "Player", category: spec.category ?? "Execution",
      ...spec,
    }, null, 2), schema);
    map.set(card.id, card);
  }
  return map;
}

test("풀의 등급·태그를 개수 내림차순으로 집계한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [
    { id: "a", grade: "Common", tags: ["시작", "공격"] },
    { id: "b", grade: "Common", tags: ["시작"] },
    { id: "c", grade: "Rare", tags: ["시작", "공격"] },
  ]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b","c"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.grades, [
    { value: "Common", count: 2 },
    { value: "Rare", count: 1 },
  ]);
  assert.deepEqual(distribution.tags, [
    { value: "시작", count: 3 },
    { value: "공격", count: 2 },
  ]);
});

test("비용과 실행 순서는 값 오름차순으로 집계한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [
    { id: "a", energyCost: 2, baseExecutionOrder: 5 },
    { id: "b", energyCost: 1, baseExecutionOrder: 3 },
    { id: "c", energyCost: 1, baseExecutionOrder: 5 },
  ]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b","c"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.costs, [
    { value: 1, count: 2 },
    { value: 2, count: 1 },
  ]);
  assert.deepEqual(distribution.orders, [
    { value: 3, count: 1 },
    { value: 5, count: 2 },
  ]);
});

test("개입 카드는 실행 순서 분포에서 빠진다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [
    { id: "a", baseExecutionOrder: 4 },
    { id: "b", category: "Intervention", intervention: { kind: "lock" } },
  ]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.orders, [{ value: 4, count: 1 }],
    "개입 카드에는 baseExecutionOrder가 없다");
  assert.equal(distribution.total, 2, "그래도 편성 장수에는 들어간다");
});

test("없는 카드는 세지 않고 따로 담는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = cardsByIdOf(core, schema, [{ id: "a", grade: "Common", tags: ["시작"] }]);
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","ghost"]}');

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.equal(distribution.total, 2);
  assert.equal(distribution.present, 1);
  assert.deepEqual(distribution.missing, ["ghost"]);
  assert.deepEqual(distribution.grades, [{ value: "Common", count: 1 }],
    "없는 카드를 분포에 섞으면 집계가 거짓이 된다");
});

test("저장소의 starter를 없는 카드 없이 집계한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cardsById = new Map();
  for (const name of readdirSync(fileURLToPath(cardsDir)).filter((n) => n.endsWith(".json"))) {
    const { card } = core.readCardJson(readCardFile(name), schema);
    cardsById.set(card.id, card);
  }
  const { pool } = core.readPoolJson(
    readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8"));

  const distribution = core.poolDistribution({ pool, cardsById });
  assert.deepEqual(distribution.missing, [], "저장소 풀에는 없는 카드가 없어야 한다");
  assert.equal(distribution.present, distribution.total);
  assert.equal(distribution.total, pool.cards.length);
  assert.ok(distribution.tags.length > 0);
  assert.equal(distribution.grades.reduce((sum, e) => sum + e.count, 0), distribution.present,
    "등급 개수의 합이 카드 수와 같아야 한다");
});

test("카드 소속 풀을 역방향 표로 만든다", () => {
  const core = loadCore();
  const pools = [
    core.readPoolJson('{"id":"starter","cards":["a","b"]}').pool,
    core.readPoolJson('{"id":"mycologist","cards":["b","b","c"]}').pool,
  ];

  const membership = core.poolMembership(pools);
  assert.deepEqual(membership.get("a"), ["starter"]);
  assert.deepEqual(membership.get("b"), ["starter", "mycologist"],
    "같은 풀 안의 중복은 한 번만 센다");
  assert.deepEqual(membership.get("c"), ["mycologist"]);
  assert.equal(membership.get("ghost"), undefined);
});

function viewCards(core, schema, specs) {
  return specs.map((spec) => core.readCardJson(JSON.stringify({
    id: spec.id, name: spec.name ?? spec.id, side: spec.side ?? "Player",
    category: "Execution", ...spec,
  }, null, 2), schema).card);
}

test("검색이 이름·id·태그를 훑는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [
    { id: "vanguard_slash", name: "선봉 베기", tags: ["시작", "공격"] },
    { id: "spore_veil", name: "포자 장막", tags: ["독"] },
  ]);
  const view = (query) => core.cardListView({ cards, query, filter: "all" })
    .map((row) => row.card.id);

  assert.deepEqual(view("선봉"), ["vanguard_slash"], "이름으로 찾는다");
  assert.deepEqual(view("spore"), ["spore_veil"], "id로 찾는다");
  assert.deepEqual(view("독"), ["spore_veil"], "태그로 찾는다");
  assert.deepEqual(view("VANGUARD"), ["vanguard_slash"], "대소문자를 무시한다");
  assert.deepEqual(view("  "), ["vanguard_slash", "spore_veil"], "공백만이면 거르지 않는다");
  assert.deepEqual(view("없는말"), []);
});

test("필터가 상태·오류·고아를 가른다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [
    { id: "same_card" }, { id: "edited" }, { id: "clashing" },
    { id: "broken" }, { id: "enemy_card", side: "Enemy" },
  ]);
  // 상태는 uid로 색인한다. id는 저작 중에 바뀌므로 상태가 카드를 따라가야 한다.
  const states = new Map([
    [core.repoUid("edited"), "modified"],
    [core.repoUid("clashing"), "conflict"],
  ]);
  const errorIds = new Set(["broken"]);
  const membership = new Map([["same_card", ["starter"]]]);
  const view = (filter) => core.cardListView({ cards, states, errorIds, membership, filter })
    .map((row) => row.card.id);

  assert.deepEqual(view("all"),
    ["same_card", "edited", "clashing", "broken", "enemy_card"]);
  assert.deepEqual(view("modified"), ["edited"]);
  assert.deepEqual(view("conflict"), ["clashing"]);
  assert.deepEqual(view("error"), ["broken"]);
  assert.deepEqual(view("orphan"), ["edited", "clashing", "broken"],
    "적군 카드는 고아가 아니다 - 풀은 아군 것이다");
});

test("풀별 필터가 소속만 남긴다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [{ id: "a" }, { id: "b" }]);
  const membership = new Map([["a", ["starter"]], ["b", ["mycologist"]]]);

  const rows = core.cardListView({ cards, membership, filter: "all", poolId: "starter" });
  assert.deepEqual(rows.map((row) => row.card.id), ["a"]);
});

test("줄마다 상태·오류·소속을 붙여 준다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const cards = viewCards(core, schema, [{ id: "a" }]);

  const [plain] = core.cardListView({ cards, filter: "all" });
  assert.equal(plain.state, "same", "상태를 모르면 저장소와 같은 것으로 본다");
  assert.equal(plain.hasError, false);
  assert.deepEqual(plain.pools, []);

  const [marked] = core.cardListView({
    cards,
    states: new Map([[core.repoUid("a"), "conflict"]]),
    errorIds: new Set(["a"]),
    membership: new Map([["a", ["starter", "mycologist"]]]),
    filter: "all",
  });
  assert.equal(marked.state, "conflict");
  assert.equal(marked.hasError, true);
  assert.deepEqual(marked.pools, ["starter", "mycologist"]);
});

test("필터 목록을 노출한다", () => {
  const core = loadCore();
  assert.deepEqual([...core.CARD_FILTERS],
    ["all", "modified", "conflict", "error", "orphan"]);
});

test("읽은 수치와 문제 개수를 센다", () => {
  const core = loadCore();
  const summary = core.contentSummary({
    cards: [{}, {}, {}],
    pools: [{}],
    statuses: [{}, {}],
    validation: { errors: [{ message: "x" }], warnings: [{ message: "y" }, { message: "z" }] },
    cardStates: new Map([["a", "same"], ["b", "modified"], ["c", "conflict"]]),
    poolStates: new Map([["starter", "new"]]),
  });

  assert.deepEqual(summary.counts, { cards: 3, pools: 1, statuses: 2 });
  assert.equal(summary.errors, 1);
  assert.equal(summary.warnings, 2);
  assert.equal(summary.conflicts, 1);
  assert.equal(summary.pending, 2, "modified와 new가 미반영이다");
});

test("상태 표가 없어도 0으로 센다", () => {
  const core = loadCore();
  const summary = core.contentSummary({
    cards: [], pools: [], statuses: [],
    validation: { errors: [], warnings: [] },
  });
  assert.equal(summary.conflicts, 0);
  assert.equal(summary.pending, 0);
});

test("요약을 두 줄 문장으로 만든다", () => {
  const core = loadCore();
  const summary = core.contentSummary({
    cards: new Array(26).fill({}),
    pools: [{}],
    statuses: new Array(11).fill({}),
    validation: { errors: [], warnings: [] },
  });

  assert.deepEqual(core.summaryLines(summary), [
    "카드 26 · 풀 1 · 상태 11 을 읽었습니다",
    "오류 0 · 충돌 0 · 미반영 0",
  ]);
});

test("저장소 모드 전환과 연결 버튼이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="mode-repo"/);
  assert.match(html, /id="mode-markdown"/);
  assert.match(html, /id="repo-connect"/);
  assert.match(html, /id="repo-reload"/);
  assert.match(html, /id="repo-unsupported"/);
});

test("저장소 UI 스크립트가 코어와 분리되어 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /<script data-repo-ui>/);

  const core = html.match(/<script data-card-idea-core>([\s\S]*?)<\/script>/)[1];
  assert.equal(core.includes("showDirectoryPicker"), false,
    "브라우저 API는 코어에 들어가지 않는다 - 코어는 node:test가 돌린다");
  assert.equal(core.includes("document."), false,
    "DOM도 코어에 들어가지 않는다");
});

test("요약과 문제 목록 자리가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="repo-summary"/);
  assert.match(html, /id="repo-problems"/);
});

test("저장소 탭과 검색·필터가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="repo-tab-cards"/);
  assert.match(html, /id="repo-tab-pools"/);
  assert.match(html, /id="repo-search"/);
  assert.match(html, /id="repo-filter"/);
  for (const label of ["전체", "수정됨만", "충돌만", "오류만", "고아만"]) {
    assert.match(html, new RegExp(`>${label}<\\/option>`));
  }
});

test("풀 편성·분포 자리의 스타일이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /\.pool-roster\b/);
  assert.match(html, /\.pool-distribution\b/);
});

function pendingStorage() {
  const items = new Map();
  return {
    getItem: (key) => (items.has(key) ? items.get(key) : null),
    setItem: (key, value) => { items.set(key, String(value)); },
    removeItem: (key) => { items.delete(key); },
  };
}

test("미반영이 없으면 빈 상태를 준다", () => {
  const core = loadCore();
  const { pending, errors } = core.readPending(pendingStorage());
  assert.deepEqual(errors, []);
  assert.deepEqual(pending.cards, {});
  assert.deepEqual(pending.pools, {});
});

test("미반영을 쓰고 다시 읽는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const storage = pendingStorage();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  card.name = "바뀐 이름";

  const written = core.writePending(storage, core.putPendingCard(core.emptyPending(), card));
  assert.equal(written.persisted, true);

  const { pending } = core.readPending(storage);
  assert.equal(pending.cards[card.uid].name, "바뀐 이름");
});

test("미반영에서 항목을 뺀다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const added = core.putPendingCard(core.emptyPending(), card);
  assert.equal(Object.keys(added.cards).length, 1);

  const removed = core.dropPendingCard(added, card.uid);
  assert.deepEqual(removed.cards, {});
  assert.equal(Object.keys(added.cards).length, 1, "원본을 제자리에서 고치지 않는다");
});

test("미반영 편집분을 저장소 위에 얹는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const edited = { ...card, name: "덮어쓴 이름" };
  const fresh = { ...card, uid: "new:1", id: "brand_new", name: "새 카드", base: null };

  const merged = core.applyPending({
    cards: [card],
    pools: [],
    pending: core.putPendingCard(core.putPendingCard(core.emptyPending(), edited), fresh),
  });

  assert.equal(merged.cards.length, 2);
  assert.equal(merged.cards[0].name, "덮어쓴 이름", "같은 uid는 대체한다");
  assert.equal(merged.cards[1].id, "brand_new", "저장소에 없는 미반영은 뒤에 붙인다");
});

test("풀도 같은 방식으로 얹는다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool } = core.readPoolJson(text);
  const edited = { ...pool, cards: [...pool.cards, "새_카드"] };

  const merged = core.applyPending({
    cards: [],
    pools: [pool],
    pending: core.putPendingPool(core.emptyPending(), edited),
  });

  assert.equal(merged.pools.length, 1);
  assert.equal(merged.pools[0].cards.length, pool.cards.length + 1);
});

test("깨진 미반영 데이터는 버리고 이유를 준다", () => {
  const core = loadCore();
  const storage = pendingStorage();
  storage.setItem(core.PENDING_STORAGE_KEY, "{ 아님");

  const { pending, errors } = core.readPending(storage);
  assert.deepEqual(pending.cards, {});
  assert.equal(errors.length, 1);
});

test("모르는 미반영 버전은 버리고 이유를 준다", () => {
  const core = loadCore();
  const storage = pendingStorage();
  storage.setItem(core.PENDING_STORAGE_KEY, JSON.stringify({ version: 99, cards: {}, pools: {} }));

  const { pending, errors } = core.readPending(storage);
  assert.deepEqual(pending.cards, {});
  assert.ok(errors.some((message) => message.includes("99")));
});

test("저장에 실패해도 던지지 않고 알린다", () => {
  const core = loadCore();
  const storage = {
    getItem: () => null,
    setItem: () => { throw new Error("quota"); },
    removeItem: () => {},
  };

  const result = core.writePending(storage, core.emptyPending());
  assert.equal(result.persisted, false);
  assert.ok(result.error.includes("quota"));
});

test("저장소에서 읽은 카드에 파일 uid를 붙인다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.equal(card.uid, "file:vanguard_slash");
  assert.equal(core.isNewUid(card.uid), false);
});

test("신규 uid는 접두사로 구분된다", () => {
  const core = loadCore();
  assert.equal(core.newUid(1), "new:1");
  assert.equal(core.isNewUid(core.newUid(1)), true);
  assert.notEqual(core.newUid(1), core.repoUid("1"),
    "접두사가 없으면 새 카드의 id가 저장소 카드의 uid와 겹칠 수 있다");
});

test("id를 바꿔도 uid는 그대로다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const renamed = { ...card, id: "renamed_card" };
  assert.equal(renamed.uid, "file:vanguard_slash");
});

test("목록의 상태는 uid로, 오류는 id로 색인한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const renamed = { ...card, id: "renamed_card" };

  const [row] = core.cardListView({
    cards: [renamed],
    states: new Map([[renamed.uid, "modified"]]),
    errorIds: new Set(["renamed_card"]),
    filter: "all",
  });

  assert.equal(row.state, "modified", "id를 바꿔도 상태가 따라온다");
  assert.equal(row.hasError, true, "검증 오류는 현재 id로 붙는다");
});

test("uid를 모르면 저장소와 같은 것으로 본다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  const [row] = core.cardListView({ cards: [card], filter: "all" });
  assert.equal(row.state, "same");
});

test("왕복은 uid에 영향받지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = readCardFile("vanguard_slash.json");
  const { card } = core.readCardJson(original, schema);
  assert.equal(core.writeCardJson(card, schema), original,
    "uid는 노트북 내부 식별자이고 파일에 나가지 않는다");
});

test("편집 가능한 기본 필드 목록을 노출한다", () => {
  const core = loadCore();
  assert.deepEqual([...core.EDITABLE_CARD_FIELDS], [
    "id", "name", "side", "category", "energyCost", "baseExecutionOrder", "grade", "tags",
  ]);
});

test("숫자 필드를 문자열로 받아도 숫자로 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const edited = core.setCardField(card, "energyCost", "3");
  assert.equal(edited.energyCost, 3);
  assert.equal(typeof edited.energyCost, "number");

  assert.equal(core.setCardField(card, "energyCost", "").energyCost, 0,
    "빈 값은 0이다 - 기본값이라 파일에서 생략된다");
  assert.equal(core.setCardField(card, "baseExecutionOrder", "-2").baseExecutionOrder, -2);
  assert.equal(core.setCardField(card, "energyCost", "abc").energyCost, 0,
    "숫자가 아니면 0으로 떨어뜨린다 - NaN이 모델에 들어가면 왕복이 깨진다");
});

test("태그를 쉼표와 줄바꿈으로 나눈다", () => {
  const core = loadCore();
  assert.deepEqual(core.parseTagsInput("시작, 공격\n독"), ["시작", "공격", "독"]);
  assert.deepEqual(core.parseTagsInput("  시작 ,, 공격  "), ["시작", "공격"]);
  assert.deepEqual(core.parseTagsInput(""), []);
  assert.deepEqual(core.parseTagsInput("시작, 시작"), ["시작", "시작"],
    "중복을 여기서 지우지 않는다 - 검증기가 오류로 잡아 사용자가 고친다");
});

test("문자열 필드는 다듬어 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.equal(core.setCardField(card, "name", "  새 이름  ").name, "새 이름");
  assert.equal(core.setCardField(card, "id", " new_id ").id, "new_id");
});

test("모르는 필드는 무시하고 원본을 그대로 준다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);
  assert.equal(core.setCardField(card, "flavour", "설명"), card);
});

test("편집은 원본을 제자리에서 고치지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const edited = core.setCardField(card, "name", "바뀐 이름");
  assert.equal(card.name, "선봉 베기");
  assert.equal(edited.uid, card.uid, "uid는 편집으로 바뀌지 않는다");
});

test("분류를 바꿔도 반대쪽 값이 사라지지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const { card } = core.readCardJson(readCardFile("vanguard_slash.json"), schema);

  const asIntervention = core.setCardField(card, "category", "Intervention");
  assert.equal(asIntervention.category, "Intervention");
  assert.equal(asIntervention.effects.length, 1,
    "효과가 모델에 남는다 - 되돌릴 때 값이 살아 있어야 한다(설계 5)");

  const written = JSON.parse(core.writeCardJson(asIntervention, schema));
  assert.equal(written.effects, undefined, "그래도 파일에는 나가지 않는다");
});

test("새 카드는 스키마의 기본값으로 시작한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const card = core.createCardModel({ schema, uid: core.newUid(1) });

  assert.equal(card.uid, "new:1");
  assert.equal(card.id, "");
  assert.equal(card.side, "Player");
  assert.equal(card.category, "Execution");
  assert.equal(card.grade, "None");
  assert.deepEqual(card.effects, []);
  assert.equal(card.intervention, null);
  assert.deepEqual(card.tags, []);
  assert.equal(card.base, null, "저장소에 없으므로 base가 없다");
  assert.equal(core.resolveCardState({ stored: null, pending: card, schema }), "new");
});

function probeCard(core, schema, overrides) {
  const { card } = core.readCardJson(JSON.stringify({
    id: "probe", name: "탐침", side: "Player", category: "Execution", ...overrides,
  }, null, 2), schema);
  return card;
}

test("새 효과 행은 스키마의 기본값으로 시작한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const added = core.addEffect(probeCard(core, schema, {}), "apply_status", schema);

  assert.equal(added.effects.length, 1);
  assert.deepEqual(added.effects[0].params,
    { status: "", count: 0, target: "Self", selector: "None" });
  assert.equal(added.effects[0].condition, null);
  assert.equal(added.effects[0].raw, null);
});

test("기본값뿐인 효과는 kind만 파일로 나간다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const added = core.addEffect(probeCard(core, schema, {}), "apply_status", schema);
  const written = JSON.parse(core.writeCardJson(added, schema));

  assert.deepEqual(written.effects, [{ kind: "apply_status" }],
    "기본값은 생략된다 - 폼이 값을 다 채워도 왕복 규칙은 그대로다");
});

test("효과를 삭제·복제·이동한다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, {
    effects: [{ kind: "damage", value: 5 }, { kind: "grant_next_turn_fate", value: 1 }],
  });

  assert.deepEqual(core.removeEffect(base, 0).effects.map((e) => e.kind),
    ["grant_next_turn_fate"]);

  const duplicated = core.duplicateEffect(base, 0);
  assert.deepEqual(duplicated.effects.map((e) => e.kind),
    ["damage", "damage", "grant_next_turn_fate"]);
  assert.notEqual(duplicated.effects[0], duplicated.effects[1],
    "복제본이 같은 객체를 가리키면 한쪽을 고칠 때 둘 다 바뀐다");

  assert.deepEqual(core.moveEffect(base, 0, 1).effects.map((e) => e.kind),
    ["grant_next_turn_fate", "damage"]);
  assert.deepEqual(core.moveEffect(base, 0, 9).effects.map((e) => e.kind),
    ["grant_next_turn_fate", "damage"], "범위를 넘으면 끝으로 보낸다");
  assert.equal(base.effects.length, 2, "원본을 제자리에서 고치지 않는다");
});

test("효과 종류를 바꾸면 파라미터가 통째로 갈린다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, {
    effects: [{ kind: "damage", value: 5, selector: "FrontOne" }],
  });

  const changed = core.setEffectKind(base, 0, "grant_next_turn_fate", schema);
  assert.equal(changed.effects[0].kind, "grant_next_turn_fate");
  assert.deepEqual(changed.effects[0].params, { value: 0 },
    "새 종류의 필드만 남는다 - selector는 이 효과에 없다");
});

test("효과 종류를 바꿔도 조건은 남는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, {
    effects: [{ kind: "damage", value: 5, condition: { kind: "FirstToTrigger" } }],
  });

  const changed = core.setEffectKind(base, 0, "move_formation", schema);
  assert.equal(changed.effects[0].condition.kind, "FirstToTrigger",
    "조건은 효과 종류와 독립이다");
});

test("파라미터를 타입에 맞게 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = core.addEffect(probeCard(core, schema, {}), "consume_status", schema);

  assert.equal(core.setEffectParam(base, 0, "maxAmount", "3", schema)
    .effects[0].params.maxAmount, 3);
  assert.equal(core.setEffectParam(base, 0, "status", "poison", schema)
    .effects[0].params.status, "poison");
  assert.equal(core.setEffectParam(base, 0, "selector", "All", schema)
    .effects[0].params.selector, "All");

  const swap = core.addEffect(probeCard(core, schema, {}), "damage", schema);
  assert.equal(core.setEffectParam(swap, 0, "value", "abc", schema).effects[0].params.value, 0);
});

test("불리언 파라미터를 넣는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = core.setIntervention(
    probeCard(core, schema, { category: "Intervention" }), "swap_execution_order", schema);

  assert.equal(base.intervention.params.requireAdjacent, false);
  assert.equal(core.setInterventionParam(base, "requireAdjacent", true, schema)
    .intervention.params.requireAdjacent, true);
});

test("조건을 붙이고 뗀다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = core.addEffect(probeCard(core, schema, {}), "damage", schema);

  const withCondition = core.setEffectCondition(base, 0, { kind: "WithinNth", n: "3" });
  assert.deepEqual(withCondition.effects[0].condition,
    { kind: "WithinNth", n: 3, successEffectValue: 0, skipOnBasic: false });

  const bumped = core.setEffectCondition(withCondition, 0, { successEffectValue: "7" });
  assert.equal(bumped.effects[0].condition.kind, "WithinNth", "다른 칸은 유지된다");
  assert.equal(bumped.effects[0].condition.successEffectValue, 7);

  assert.equal(core.setEffectCondition(withCondition, 0, null).effects[0].condition, null);
});

test("개입을 걸고 종류를 바꾸고 뗀다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const base = probeCard(core, schema, { category: "Intervention" });

  const locked = core.setIntervention(base, "lock", schema);
  assert.equal(locked.intervention.kind, "lock");
  assert.deepEqual(locked.intervention.params, {});

  const changed = core.setIntervention(locked, "change_execution_order", schema);
  assert.deepEqual(changed.intervention.params, { delta: 0, targetSide: "Any" });

  assert.equal(core.setIntervention(changed, "", schema).intervention, null);
});

test("모르는 효과 행은 파라미터도 종류도 바뀌지 않는다", () => {
  const core = loadCore();
  const schema = loadSchema();
  const original = `${JSON.stringify({
    id: "x", name: "실험", side: "Player", category: "Execution",
    effects: [{ kind: "teleport", distance: 3 }],
  }, null, 2)}\n`;
  const { card } = core.readCardJson(original, schema);

  assert.equal(core.setEffectParam(card, 0, "distance", "9", schema), card);
  assert.equal(core.setEffectKind(card, 0, "damage", schema), card);
  assert.equal(core.writeCardJson(card, schema), original,
    "보존하기로 한 것이 편집 명령으로 깨지면 안 된다");

  assert.deepEqual(core.removeEffect(card, 0).effects, [],
    "삭제는 허용한다 - 사용자가 명시적으로 지시한 변경이다");
});

test("풀 맨 끝에 카드를 담는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b"]}');

  assert.deepEqual(core.addCardsToPool(pool, ["c", "d"]).cards, ["a", "b", "c", "d"]);
  assert.deepEqual(pool.cards, ["a", "b"], "원본을 제자리에서 고치지 않는다");
});

test("이미 있는 카드는 다시 담지 않는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b"]}');
  assert.deepEqual(core.addCardsToPool(pool, ["b", "c"]).cards, ["a", "b", "c"]);
  assert.deepEqual(core.addCardsToPool(pool, ["c", "c"]).cards, ["a", "b", "c"],
    "같은 요청 안의 중복도 한 번만 담는다");
});

test("이미 있는 중복은 담기로 사라지지 않는다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","a"]}');
  assert.deepEqual(core.addCardsToPool(pool, ["b"]).cards, ["a", "a", "b"],
    "저장소에 있던 중복은 그대로 둔다 - 검증기가 오류로 잡고 사용자가 뺀다");
});

test("인덱스로 뺀다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","a","b"]}');

  assert.deepEqual(core.removeFromPool(pool, 0).cards, ["a", "b"],
    "같은 카드가 둘일 때 id로는 어느 쪽을 뺄지 정할 수 없다");
  assert.deepEqual(core.removeFromPool(pool, 9).cards, ["a", "a", "b"]);
});

test("편성 순서를 바꾼다", () => {
  const core = loadCore();
  const { pool } = core.readPoolJson('{"id":"p","cards":["a","b","c"]}');

  assert.deepEqual(core.moveInPool(pool, 0, 2).cards, ["b", "c", "a"]);
  assert.deepEqual(core.moveInPool(pool, 2, 0).cards, ["c", "a", "b"]);
  assert.deepEqual(core.moveInPool(pool, 0, 9).cards, ["b", "c", "a"], "범위를 넘으면 끝으로");
  assert.deepEqual(core.moveInPool(pool, 9, 0).cards, ["a", "b", "c"], "없는 자리는 무시한다");
});

test("편성을 고쳐도 왕복 형식이 유지된다", () => {
  const core = loadCore();
  const text = readFileSync(fileURLToPath(new URL("starter.json", poolsDir)), "utf8");
  const { pool } = core.readPoolJson(text);

  const edited = core.removeFromPool(core.addCardsToPool(pool, ["새_카드"]), 0);
  const written = core.writePoolJson(edited);

  assert.ok(written.endsWith("]\n}\n"));
  assert.deepEqual(JSON.parse(written).cards, edited.cards);
  assert.equal(core.writePoolJson(pool), text, "원본은 그대로 왕복한다");
});

test("새 카드 버튼과 편집 폼 자리가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /id="repo-new-card"/);
  assert.match(html, /id="repo-pending-note"/);
});

test("편집 명령이 저장소 UI에만 배선된다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const markdownUi = html.split("<script data-repo-ui>")[0].split("</script>").pop();
  assert.equal(markdownUi.includes("applyCardEdit"), false,
    "Markdown UI 스크립트는 이 계획에서 바뀌지 않는다");
});

test("효과 편집기의 스타일이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /\.effect-row\b/);
  assert.match(html, /\.effect-params\b/);
  assert.match(html, /\.effect-condition\b/);
});

test("풀 담기와 소속 표시의 스타일이 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  assert.match(html, /\.pool-membership\b/);
  assert.match(html, /\.pool-picker\b/);
});

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

test("저장소 ↔ 현재 토글 자리가 마크업에 있다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");

  assert.match(html, /id="repo-source-toggle"/);
  assert.match(html, /id="repo-source-diff"/);
  assert.match(html, /\.diff-row\.is-add/);
  assert.match(html, /\.diff-row\.is-remove/);
});

test("우측 창은 선택이 사라지면 원문 자리로 돌아간다", () => {
  const html = readFileSync(fileURLToPath(htmlUrl), "utf8");
  const ui = html.match(/<script data-repo-ui>([\s\S]*?)<\/script>/);

  assert.ok(ui);
  assert.match(ui[1], /elements\.source\.hidden = false;/);
  assert.match(ui[1], /elements\.sourceDiff\.hidden = true;/);
  assert.match(html, /#repo-source-toggle\.is-active/);
});

const repoRoot = new URL("../../", import.meta.url);
const repoCardPath = (id) =>
  fileURLToPath(new URL(`Assets/StreamingAssets/Content/Cards/${id}.json`, repoRoot));
const repoSchema = () => loadCore().parseAuthoringSchema(
  readFileSync(fileURLToPath(new URL("./authoring-schema.json", htmlUrl)), "utf8"));

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

test("카드와 풀이 같은 id를 가져도 미참조를 놓치지 않는다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);
  const poolText = readFileSync(fileURLToPath(new URL(
    "Assets/StreamingAssets/Content/Pools/starter.json", repoRoot)), "utf8");
  const { pool } = core.readPoolJson(poolText);
  const sharedId = card.id;
  const sharedPool = { ...pool, id: sharedId };
  const renamedPool = { ...sharedPool, id: `${sharedId}_renamed` };

  const plan = core.exportPlan({
    cards: [card], pools: [renamedPool], storedCards: [card], storedPools: [sharedPool],
    cardStates: new Map([[card.uid, "same"]]),
    poolStates: new Map([[renamedPool.id, "modified"]]),
    readErrors: [], validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.unreferenced, [`${sharedId}.json`]);
  assert.deepEqual(plan.unchanged, [`${sharedId}.json`]);
});

test("충돌 카드는 쓰기 목록에 들어가지 않는다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);

  const plan = core.exportPlan({
    cards: [card], pools: [], storedCards: [card], storedPools: [],
    cardStates: new Map([[card.uid, "conflict"]]), poolStates: new Map(),
    readErrors: [], validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.writes, []);
  assert.deepEqual(plan.created, []);
  assert.deepEqual(plan.updated, []);
  assert.deepEqual(plan.unchanged, []);
  assert.equal(plan.blocked.length, 1);
  assert.match(plan.blocked[0], /충돌 1개/);
});

test("카드를 풀보다 먼저 쓴다", () => {
  const core = loadCore();
  const schema = repoSchema();
  const { card } = planFixture(core, schema);
  const editedCard = core.setCardField(card, "name", "고친 이름");
  const poolText = readFileSync(fileURLToPath(new URL(
    "Assets/StreamingAssets/Content/Pools/starter.json", repoRoot)), "utf8");
  const { pool } = core.readPoolJson(poolText);
  const editedPool = core.removeFromPool(pool, 0);

  const plan = core.exportPlan({
    cards: [editedCard], pools: [editedPool], storedCards: [card], storedPools: [pool],
    cardStates: new Map([[editedCard.uid, "modified"]]),
    poolStates: new Map([[editedPool.id, "modified"]]),
    readErrors: [], validation: { errors: [], warnings: [] }, schema,
  });

  assert.deepEqual(plan.writes.map((entry) => entry.name),
    ["vanguard_slash.json", "starter.json"]);
});

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
