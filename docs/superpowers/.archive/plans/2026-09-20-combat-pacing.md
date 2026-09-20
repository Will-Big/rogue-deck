# 전투 템포 개선 구현 계획

> **에이전트 작업자용:** 이 계획은 `superpowers:subagent-driven-development` 또는
> `superpowers:executing-plans`로 과제 단위 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

설계는 [전투 템포 개선 — 상세](../specs/2026-09-20-combat-pacing-design.md)이고 개요는
[HTML](../specs/2026-09-20-combat-pacing-design.html)이다. **실행 근거는 설계의 `## 상세`에서만 얻는다.
개요와 상세가 어긋나면 멈추고 묻는다**(규칙 29).

**목표:** 단독 고블린 전투를 4~5턴에 끝나게 하고, 매 턴 눈에 보이는 변화가 생기게 한다.

**접근:** 규칙(코드)은 건드리지 않는다. `Assets/StreamingAssets/Content/`의 JSON만 고친다 — 카드 수치,
파티원 B의 덱, 적 묶음과 정책, 짝 전투용 약체. 콘텐츠를 전제한 기존 테스트를 함께 옮기고, 턴 수를 재는
스크립트 하네스를 헤드리스에 추가한다.

**도구:** C# 9 / net5.0 헤드리스 NUnit, `Tools/verify.sh`, Unity 6000.5.2f1 배치(EditMode).

## 전역 제약

- **작업은 전용 워크트리에서 한다**(규칙 15). 메인 체크아웃 `/Users/ish/Git/rogue-deck`의 브랜치를
  전환하지 않는다. 시작할 때 `EnterWorktree`로 `combat-pacing` 워크트리를 만들고 그 안에서 전부 진행한다.
- **코드(`Assets/Core/**/*.cs`)의 규칙 로직을 바꾸지 않는다.** 이번 범위는 콘텐츠 JSON과 테스트다.
  규칙을 바꿔야 할 이유가 생기면 멈추고 사용자에게 묻는다.
- **카드 JSON의 키 순서와 생략 규칙을 기존 파일과 똑같이 맞춘다.**
  `CardContentJsonTests.Repository_cards_round_trip_byte_identically`가 `Content/Cards/*.json` 전부를
  바이트 단위로 왕복 검증하므로, 키 하나만 순서가 달라도 실패한다. 플레이어 카드 키 순서는
  `cardFormat` → `id` → `name` → `side` → `category` → `energyCost` → `baseExecutionOrder` → `targets` →
  `effects` → `grade` → `tags`다. **적 카드는 `energyCost`·`grade`·`tags`를 쓰지 않는다**
  (`Cards/goblin_jab.json`이 그 형태다).
- **새 `.json`마다 `.meta`를 같은 커밋에 만든다.** 형식은 아래와 같고 `guid`는 32자리 hex 난수다.
  `userData:` 뒤의 공백 한 칸까지 그대로다.
  ```
  fileFormatVersion: 2
  guid: <32 hex>
  DefaultImporter:
    externalObjects: {}
    userData: 
    assetBundleName: 
    assetBundleVariant: 
  ```
- **커밋 메시지는 한국어**로 `타입(범위): 제목` 형식, 제목은 `-ㄴ다`로 끝낸다(규칙 27). `commit-msg`
  훅이 강제한다. 본문 끝에 `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`를 붙인다.
- 헤드리스 단일 테스트 실행 명령(타깃 오버라이드 필수):
  ```bash
  dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~<테스트이름>"
  ```
- 전체 검증은 `Tools/verify.sh` 하나다. **과제마다 커밋 전에 돌린다** — `pre-commit` 훅이 어차피 돌린다.
- **시작 시점 실측값**(2026-09-20): `Content/Cards/*.json` **29**개, 헤드리스 테스트 **756**개.

---

## 과제 순서와 의존

| 과제 | 내용 | 선행 |
|---|---|---|
| 1 | 새 공격 카드 5종 저작 | 없음 |
| 2 | `striker` 덱 신설, `member_b` 전환, 덱 전제 테스트 이전 | 1 |
| 3 | 상쇄 해소 수치 4건 | 없음(2와 독립) |
| 4 | 고블린 단독의 묶음·정책 교체 | 3 (골든을 연달아 고치지 않기 위해) |
| 5 | `goblin_runt`·`runt_jab` 저작과 짝 편성 전환 | 4 |
| 6 | 턴 수 측정 하네스, 조정, 문서·색인 갱신 | 1~5 |

---

### 과제 1: 새 공격 카드 5종 저작

**파일**
- 생성: `Assets/StreamingAssets/Content/Cards/cleave.json` (+ `.meta`)
- 생성: `Assets/StreamingAssets/Content/Cards/flank_jab.json` (+ `.meta`)
- 생성: `Assets/StreamingAssets/Content/Cards/heavy_swing.json` (+ `.meta`)
- 생성: `Assets/StreamingAssets/Content/Cards/shield_bash.json` (+ `.meta`)
- 생성: `Assets/StreamingAssets/Content/Cards/brace.json` (+ `.meta`)
- 생성(테스트): `Assets/Core/Tests/EditMode/CardContentAssertions.cs` (+ `.meta`) — 공유 헬퍼
- 생성(테스트): `Assets/Core/Tests/EditMode/StrikerCardContentTests.cs` (+ `.meta`)
- 수정: `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs:19` (카드 수 29 → 34)

**인터페이스**
- 소비: `TestContent.Content()` — 저장소 콘텐츠를 읽어 `GameContent`를 돌려준다. 호출마다 새로 만든다.
- 생산: 카드 id `cleave`·`flank_jab`·`heavy_swing`·`shield_bash`·`brace`. 과제 2의 `striker` 덱이 쓴다.
- 생산: `CardContentAssertions.DamageOf(CardDefinition)`·`.BlockOf(CardDefinition)` — 과제 3이 그대로 쓴다.
  **과제 3에서 같은 헬퍼를 다시 정의하지 않는다**(2026-09-20 사용자 결정: 공유 헬퍼로 뽑는다).

- [ ] **단계 1: 실패하는 테스트를 쓴다**

먼저 공유 헬퍼 `Assets/Core/Tests/EditMode/CardContentAssertions.cs`:

```csharp
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>저작된 카드에서 수치를 꺼내는 공용 헬퍼. 카드 수치를 단언하는 테스트가 둘 이상이라
    /// 한곳에 둔다(2026-09-20 사용자 결정).</summary>
    public static class CardContentAssertions
    {
        /// <summary>이 카드가 주는 직접 피해의 합.</summary>
        public static int DamageOf(CardDefinition card)
            => card.Effects.Where(e => e.Key == EffectKeys.Damage).Sum(e => e.EffectValue);

        /// <summary>이 카드가 거는 방어의 합. 카드가 상태에 주는 것은 count 하나이고 그것이
        /// EffectValue에 실린다(EffectData 주석). 어떤 상태인지는 Payload가 든다.</summary>
        public static int BlockOf(CardDefinition card)
            => card.Effects
                .Where(e => e.Key == EffectKeys.ApplyStatus
                    && e.Payload is ApplyStatusPayload payload && payload.Key == StatusKeys.Block)
                .Sum(e => e.EffectValue);
    }
}
```

그리고 `Assets/Core/Tests/EditMode/StrikerCardContentTests.cs`:

```csharp
using FateWeaver.Core.Cards;
using NUnit.Framework;
using static FateWeaver.Tests.CardContentAssertions;

namespace FateWeaver.Tests
{
    /// <summary>파티원 B의 직접 피해 카드 5종이 설계한 수치대로 저작됐는지 잠근다(전투 템포 설계 변경 1).
    /// 수치를 바꾸려면 설계 문서와 함께 바꾼다.</summary>
    public class StrikerCardContentTests
    {
        private static CardDefinition Card(string id) => TestContent.Content().Cards.Get(id);

        [TestCase("cleave", 4, 1, 4)]
        [TestCase("flank_jab", 2, 1, 3)]
        [TestCase("heavy_swing", 6, 2, 7)]
        [TestCase("shield_bash", 5, 1, 3)]
        public void Attack_cards_have_the_designed_order_cost_and_damage(
            string id, int order, int cost, int damage)
        {
            var card = Card(id);

            Assert.AreEqual(Side.Player, card.Side);
            Assert.AreEqual(CardCategory.Execution, card.Category);
            Assert.AreEqual(order, card.BaseExecutionOrder, id + "의 실행 순서");
            Assert.AreEqual(cost, card.EnergyCost, id + "의 비용");
            Assert.AreEqual(damage, DamageOf(card), id + "의 피해");
        }

        [Test]
        public void Shield_bash_also_grants_block_two()
        {
            Assert.AreEqual(2, BlockOf(Card("shield_bash")));
        }

        /// <summary>짝 전투의 표적 선택이 이 한 장에 걸려 있다(설계 변경 1, 사용자 결정 A안).
        /// 시작 덱의 다른 적 대상 카드는 전부 FrontOne이고 BackOne·All 카드는 풀에만 있다.</summary>
        [Test]
        public void Flank_jab_is_the_only_back_row_attack_in_the_striker_deck()
        {
            Assert.AreEqual(CardTargetRange.BackOne, Card("flank_jab").EnemyTarget);
            Assert.AreEqual(CardTargetRange.FrontOne, Card("cleave").EnemyTarget);
            Assert.AreEqual(CardTargetRange.FrontOne, Card("heavy_swing").EnemyTarget);
            Assert.AreEqual(CardTargetRange.FrontOne, Card("shield_bash").EnemyTarget);
        }

        [Test]
        public void Brace_is_a_block_three_card_with_no_damage()
        {
            var brace = Card("brace");

            Assert.AreEqual(3, BlockOf(brace));
            Assert.AreEqual(0, DamageOf(brace));
            Assert.AreEqual(1, brace.EnergyCost);
        }
    }
}
```

속성 이름은 2026-09-20에 확인한 실제 값이다 — `EffectData`는 `record EffectData(EffectKey Key,
int EffectValue)`이고 상태 키는 `Payload`의 `ApplyStatusPayload(StatusKey Key)`가 든다
(`Assets/Core/Cards/CardDefinition.cs:10`·`Assets/Core/Effects/ApplyStatusPayload.cs:10`).
`EnemyTarget`은 `CardTargetRange?`라 `CardTargetRange.BackOne`과 그대로 비교된다
(`CardDefinition.cs:67`).

- [ ] **단계 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~StrikerCardContentTests"
```

기대: 실패. 카드 카탈로그에 `cleave`가 없어 `Get`이 던진다.

`CardTargetRange`는 `FateWeaver.Core.Cards`에 있다 — 테스트의 `using`에 이미 들어 있다.

- [ ] **단계 3: 카드 5개를 저작한다**

`cleave.json`:
```json
{
  "cardFormat": 2,
  "id": "cleave",
  "name": "베기",
  "side": "Player",
  "category": "Execution",
  "energyCost": 1,
  "baseExecutionOrder": 4,
  "targets": {
    "enemy": "FrontOne"
  },
  "effects": [
    {
      "kind": "damage",
      "id": "e0",
      "targetFaction": "Enemy",
      "value": 4
    }
  ],
  "grade": "Common",
  "tags": [
    "시작",
    "공격"
  ]
}
```

`flank_jab.json` — **이 한 장만 `BackOne`이다. 짝 전투의 표적 선택이 여기 걸려 있다:**
```json
{
  "cardFormat": 2,
  "id": "flank_jab",
  "name": "파고들기",
  "side": "Player",
  "category": "Execution",
  "energyCost": 1,
  "baseExecutionOrder": 2,
  "targets": {
    "enemy": "BackOne"
  },
  "effects": [
    {
      "kind": "damage",
      "id": "e0",
      "targetFaction": "Enemy",
      "value": 3
    }
  ],
  "grade": "Common",
  "tags": [
    "시작",
    "공격"
  ]
}
```

`heavy_swing.json`:
```json
{
  "cardFormat": 2,
  "id": "heavy_swing",
  "name": "내려찍기",
  "side": "Player",
  "category": "Execution",
  "energyCost": 2,
  "baseExecutionOrder": 6,
  "targets": {
    "enemy": "FrontOne"
  },
  "effects": [
    {
      "kind": "damage",
      "id": "e0",
      "targetFaction": "Enemy",
      "value": 7
    }
  ],
  "grade": "Common",
  "tags": [
    "공격"
  ]
}
```

`shield_bash.json`:
```json
{
  "cardFormat": 2,
  "id": "shield_bash",
  "name": "방패 치기",
  "side": "Player",
  "category": "Execution",
  "energyCost": 1,
  "baseExecutionOrder": 5,
  "targets": {
    "ally": "Self",
    "enemy": "FrontOne"
  },
  "effects": [
    {
      "kind": "damage",
      "id": "e0",
      "targetFaction": "Enemy",
      "value": 3
    },
    {
      "kind": "apply_status",
      "id": "e1",
      "targetFaction": "Ally",
      "status": "block",
      "count": 2
    }
  ],
  "grade": "Common",
  "tags": [
    "공격",
    "방어"
  ]
}
```

`brace.json`:
```json
{
  "cardFormat": 2,
  "id": "brace",
  "name": "버티기",
  "side": "Player",
  "category": "Execution",
  "energyCost": 1,
  "baseExecutionOrder": 4,
  "targets": {
    "ally": "FrontOne"
  },
  "effects": [
    {
      "kind": "apply_status",
      "id": "e0",
      "targetFaction": "Ally",
      "status": "block",
      "count": 3
    }
  ],
  "grade": "Common",
  "tags": [
    "방어"
  ]
}
```

`.meta` 5개를 전역 제약의 형식으로 만든다.

- [ ] **단계 4: 부팅 카드 수를 34로 고친다**

`Assets/Core/Tests/EditMode/ContentBootstrapTests.cs:19`
```csharp
            Assert.AreEqual(34, result.Content.Cards.Ids.Count);
```

- [ ] **단계 5: 통과를 확인한다**

```bash
Tools/verify.sh
```

기대: 모두 통과. 헤드리스 756 → 762(새 테스트 6개: TestCase 4 + 2). 왕복 바이트 테스트가 새 카드
5장까지 자동으로 덮으므로, 키 순서를 틀렸다면 여기서
`Repository_cards_round_trip_byte_identically`가 파일 이름과 함께 실패한다.

- [ ] **단계 6: 커밋한다**

```bash
git add Assets/StreamingAssets/Content/Cards Assets/Core/Tests/EditMode/StrikerCardContentTests.cs Assets/Core/Tests/EditMode/StrikerCardContentTests.cs.meta Assets/Core/Tests/EditMode/ContentBootstrapTests.cs
git commit -F- <<'MSG'
feat(content): 파티원 B가 쓸 직접 피해 카드 다섯 장을 저작한다

베기·파고들기·내려찍기·방패 치기·버티기다. 실행 순서를 2·4·5·6으로
흩어 고블린의 조잡한 방어(순서 4) 앞뒤를 플레이어가 고를 수 있게 했다.

파고들기만 뒷줄을 때린다. 시작 덱의 나머지 적 대상 카드가 전부 앞줄이고
뒷줄·전체 카드는 풀에만 있어, 이 한 장이 없으면 적이 둘인 전투에 표적
선택이 아예 없다. 아직 어느 덱에도 넣지 않았다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
MSG
```

---

### 과제 2: `striker` 덱으로 파티원 B를 옮긴다

**파일**
- 생성: `Assets/StreamingAssets/Content/Decks/striker.json` (+ `.meta`)
- 삭제: `Assets/StreamingAssets/Content/Decks/party_prototype.json` (+ `.meta`)
- 수정: `Assets/StreamingAssets/Content/Characters/member_b.json` (`deck` 값)
- 수정: `Assets/Core/Tests/EditMode/DeckPoolCharacterContentTests.cs:20,31-37,101-105,142`
- 수정: `Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs` → `StrikerDeckDataTests.cs`로 이름 변경
- 수정: `Assets/Core/Tests/EditMode/DescriptionCatalogValidatorTests.cs:18,21-28`
- 수정: `Assets/Core/Tests/EditMode/RunSetupTests.cs:37-39`

**인터페이스**
- 소비: 과제 1의 카드 id 다섯.
- 생산: 덱 id `striker`. 과제 6의 측정 하네스가 `member_b`를 통해 쓴다.

- [ ] **단계 1: 실패하는 테스트로 먼저 옮긴다**

`DeckPoolCharacterContentTests.cs`의 상수와 골든을 새 덱으로 바꾼다.

```csharp
        private const string StrikerDeckId = "striker";
```
```csharp
        /// <summary>직접 피해 6장. Decks/striker.json에서 그대로 옮겨 적었다.</summary>
        private static readonly string[] StrikerDeckGolden =
        {
            "cleave", "cleave", "flank_jab", "heavy_swing", "shield_bash", "brace"
        };
```

`PartyPrototypeDeckJsonMatchesTheGoldenDeck`를 `StrikerDeckJsonMatchesTheGoldenDeck`로 바꾸고 본문의
`PartyPrototypeDeckGolden`·`PartyPrototypeDeckId`를 새 이름으로 바꾼다. `:142`의 덱 id 목록도
`StrikerDeckId`로 바꾼다. 클래스 주석 `:16`의 파일 이름도 `Decks/striker.json`으로 고친다.

`PartyPrototypeDataTests.cs`는 파일 이름을 `StrikerDeckDataTests.cs`로, 클래스 이름을
`StrikerDeckDataTests`로 바꾸고(`.meta`는 파일과 함께 `git mv`) 내용을 이렇게 바꾼다.

```csharp
        private static IReadOnlyList<CardDefinition> StrikerDeckCards()
        {
            var content = TestContent.Content();
            var cards = new List<CardDefinition>();
            foreach (var id in content.Decks.Get("striker"))
            {
                cards.Add(content.Cards.Get(id));
            }

            return cards;
        }

        [Test]
        public void Striker_deck_is_direct_damage_with_one_guard()
        {
            var cards = StrikerDeckCards();

            Assert.AreEqual(6, cards.Count);
            Assert.AreEqual(2, cards.Count(card => card.Id == "cleave"));
            Assert.IsTrue(
                cards.All(card => !card.Name.StartsWith("[검증]")),
                "픽스처 카드가 실제 덱에 남아 있으면 안 된다.");
        }
```

`Prototype_deck_contains_only_validation_prefixed_cards`와
`Prototype_deck_has_six_cards_and_expected_duplicates`는 위 테스트가 대체하므로 지운다.

`DescriptionCatalogValidatorTests.cs`의 `PartyPrototypeCards()`를 `StrikerCards()`로 바꾸고
`content.Decks.Get("party_prototype")`을 `content.Decks.Get("striker")`로, 호출부 `:18`도 함께 바꾼다.

`RunSetupTests.cs:37-39`:
```csharp
            var attacks = run.Party[0].Cards.Where(card => card.Id == "cleave").ToArray();

            Assert.AreEqual(2, attacks.Length, "striker 덱은 cleave를 둘 갖는다.");
```

- [ ] **단계 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~StrikerDeck"
```

기대: 실패. 덱 카탈로그에 `striker`가 없어 `Get`이 던진다.

- [ ] **단계 3: 덱을 저작하고 캐릭터를 옮긴다**

`Assets/StreamingAssets/Content/Decks/striker.json`:
```json
{
  "id": "striker",
  "cards": [
    "cleave",
    "cleave",
    "flank_jab",
    "heavy_swing",
    "shield_bash",
    "brace"
  ]
}
```

`.meta`를 만들고, `party_prototype.json`과 그 `.meta`를 `git rm`으로 지운다.

`Assets/StreamingAssets/Content/Characters/member_b.json`의 `"deck": "party_prototype"`을
`"deck": "striker"`로 바꾼다.

**픽스처 카드 JSON 4장(`fixture_*.json`)은 지우지 않는다.** 테스트가 합성 카드로 쓴다.

- [ ] **단계 4: 통과를 확인한다**

```bash
Tools/verify.sh
```

기대: 모두 통과. 덱 수는 2 그대로이므로 `ContentBootstrapTests`는 손대지 않는다.

- [ ] **단계 5: 커밋한다**

```bash
git add -A
git commit -F- <<'MSG'
feat(content): 파티원 B의 덱을 픽스처에서 직접 피해로 바꾼다

party_prototype 덱을 지우고 striker 덱 6장을 넣는다. 전투 절반이
[검증] 카드로 돌아가던 것을 끝낸다. 픽스처 카드 JSON 자체는 테스트가
합성 카드로 쓰므로 남긴다.

덱을 전제하던 테스트 넷을 새 덱으로 옮기고 이름도 함께 바꿨다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
MSG
```

---

### 과제 3: 상쇄를 깨는 수치 네 개

**파일**
- 수정: `Assets/StreamingAssets/Content/Cards/quick_cover.json` (방어 4 → 3)
- 수정: `Assets/StreamingAssets/Content/Cards/early_guard.json` (방어 4 → 3)
- 수정: `Assets/StreamingAssets/Content/Cards/toxic_reclaim.json` (방어 4 → 3)
- 수정: `Assets/StreamingAssets/Content/Cards/goblin_jab.json` (피해 4 → 5)
- 생성(테스트): `Assets/Core/Tests/EditMode/DefenseBalanceContentTests.cs` (+ `.meta`) — 과제 1의 `CardContentAssertions`를 쓴다
- 수정: `Assets/Core/Tests/EditMode/GoblinParityTests.cs` (실패 메시지에 SHA 추가, 골든 갱신)

**인터페이스**
- 소비: 과제 1의 `CardContentAssertions.DamageOf`·`BlockOf`. **다시 정의하지 않는다.**
- 생산: 없음. 과제 4가 같은 골든 상수를 다시 고친다.

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/DefenseBalanceContentTests.cs`:

```csharp
using FateWeaver.Core.Cards;
using NUnit.Framework;
using static FateWeaver.Tests.CardContentAssertions;

namespace FateWeaver.Tests
{
    /// <summary>방어 한 장이 적 공격 한 장을 정확히 0으로 만들지 않는다는 것이 이 설계의 축이다
    /// (전투 템포 설계 변경 2). 수치를 되돌리면 전투가 다시 상쇄로 굳는다.</summary>
    public class DefenseBalanceContentTests
    {
        private static CardDefinition Card(string id) => TestContent.Content().Cards.Get(id);

        private static int Block(string id) => BlockOf(Card(id));

        private static int Damage(string id) => DamageOf(Card(id));

        [TestCase("quick_cover", 3)]
        [TestCase("early_guard", 3)]
        [TestCase("toxic_reclaim", 3)]
        public void Guard_cards_grant_three_block(string id, int expected)
        {
            Assert.AreEqual(expected, Block(id));
        }

        [Test]
        public void Goblin_jab_out_damages_a_single_guard_card()
        {
            Assert.AreEqual(5, Damage("goblin_jab"));
            Assert.AreEqual(
                2, Damage("goblin_jab") - Block("quick_cover"),
                "방어 한 장을 뚫고 2가 들어와야 한다.");
        }
    }
}
```

- [ ] **단계 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~DefenseBalanceContentTests"
```

기대: 실패. `quick_cover`의 방어가 4, `goblin_jab`의 피해가 4다.

- [ ] **단계 3: 네 수치를 바꾼다**

`quick_cover.json`·`early_guard.json`의 `"count": 4` → `"count": 3`.
`toxic_reclaim.json`에서 `"status": "block"`인 효과의 `"count": 4` → `"count": 3`
(**독 효과의 `count`는 건드리지 않는다** — 같은 파일에 `poison` 효과가 따로 있다).
`goblin_jab.json`의 `"value": 4` → `"value": 5`.

- [ ] **단계 4: 골든 실패 메시지에 SHA를 넣는다**

`GoblinParityTests.cs`의 단언 메시지를 고쳐, 다음부터 실측 SHA를 바로 읽을 수 있게 한다.

```csharp
            Assert.AreEqual(
                ExpectedSignatureSha256, Sha256(signature),
                "실측 SHA: " + Sha256(signature) + "\n실측 서명:\n" + signature);
```

- [ ] **단계 5: 골든을 갱신한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~GoblinParityTests"
```

실패 메시지의 `실측 SHA:` 값을 `ExpectedSignatureSha256` 상수에 넣고, 기존 주석 형식대로 한 줄 더한다.

```csharp
        /// 2026-09-20 갱신(전투 템포 변경 2): 방어 카드 셋이 4에서 3으로, goblin_jab이 4에서 5로 바뀌어
        /// 피해·방어 수치와 HP 추이가 달라진다. 카드 순서와 이벤트 종류는 같다.
```

**서명 전문을 읽고 "카드 순서와 이벤트 종류가 같은지"를 눈으로 확인한 뒤** 상수를 바꾼다. 이벤트 종류가
바뀌었다면 수치 변경이 아닌 다른 일이 일어난 것이므로 멈추고 원인을 찾는다.

- [ ] **단계 6: 통과를 확인하고 커밋한다**

```bash
Tools/verify.sh
```

```bash
git add -A
git commit -F- <<'MSG'
balance(content): 방어와 적 공격의 상쇄를 깬다

방어 카드 셋을 4에서 3으로 내리고 고블린의 찌르기를 4에서 5로 올린다.
방어 한 장이 공격 한 장을 정확히 0으로 만들던 것이 매 턴 2씩 새어
들어오게 된다.

고블린 동등성 골든은 의도한 수치 변경이라 갱신했다. 서명 전문을 확인해
카드 순서와 이벤트 종류가 같음을 보았다. 앞으로 같은 작업이 쉽도록 실패
메시지에 실측 SHA를 함께 출력한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
MSG
```

---

### 과제 4: 고블린 단독의 묶음과 정책

**파일**
- 수정: `Assets/StreamingAssets/Content/Enemies/goblin.json`
- 생성(테스트): `Assets/Core/Tests/EditMode/GoblinBundleContentTests.cs` (+ `.meta`)
- 수정: `Assets/Core/Tests/EditMode/GoblinParityTests.cs` (골든 갱신)

**인터페이스**
- 소비: `EnemyPolicyKeys.ShuffleBag` (값 `"shuffle_bag"`), `EnemyDefinition.Policy`·`.Bundles`.
- 생산: 없음.

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/GoblinBundleContentTests.cs`:

```csharp
using System.Linq;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>고블린의 묶음 목록이 "덱"으로 동작하는지 잠근다(전투 템포 설계 변경 3).
    /// 공격 없는 묶음이 있으면 적이 쉬는 턴이 생기고, 복원 추출 정책이면 순서 개념이 없어진다.</summary>
    public class GoblinBundleContentTests
    {
        [Test]
        public void Every_bundle_has_at_least_one_attack()
        {
            var goblin = TestContent.Content().Enemies.Get("goblin");

            Assert.IsTrue(
                goblin.Bundles.All(bundle => bundle.Cards.Any(card => card.Id != "crude_guard")),
                "방어만 있는 묶음이 있으면 적이 아무것도 하지 않는 턴이 생긴다.");
        }

        [Test]
        public void Goblin_draws_its_bundles_without_replacement()
        {
            Assert.AreEqual(
                EnemyPolicyKeys.ShuffleBag, TestContent.Content().Enemies.Get("goblin").Policy);
        }
    }
}
```

`EnemyDefinition`의 속성 이름이 다르면 `Assets/Core/Authoring/Enemies/`를 열어 실제 이름으로 맞춘다.

- [ ] **단계 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~GoblinBundleContentTests"
```

기대: 두 테스트 모두 실패. 네 번째 묶음이 `crude_guard` 둘이고 정책이 `random_pick`이다.

- [ ] **단계 3: 적 정의를 고친다**

`Assets/StreamingAssets/Content/Enemies/goblin.json`:
```json
{
  "id": "goblin",
  "displayName": "고블린",
  "maxHp": 28,
  "policy": "shuffle_bag",
  "bundles": [
    ["goblin_jab"],
    ["sly_jab", "crude_guard"],
    ["sly_jab", "goblin_jab"],
    ["crude_guard", "goblin_jab"]
  ]
}
```

- [ ] **단계 4: 골든을 갱신한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~GoblinParityTests"
```

실패 메시지의 `실측 SHA:`를 상수에 넣고 주석을 한 줄 더한다.

```csharp
        /// 2026-09-20 갱신(전투 템포 변경 3): 고블린 정책이 shuffle_bag이 되어 턴마다 나오는 묶음이
        /// 달라지고, 방어 전용 묶음이 crude_guard+goblin_jab으로 바뀌었다.
```

- [ ] **단계 5: 통과를 확인하고 커밋한다**

```bash
Tools/verify.sh
```

```bash
git add -A
git commit -F- <<'MSG'
balance(content): 고블린이 묶음을 덱처럼 쓰고 쉬는 턴을 없앤다

방어만 두 장이던 묶음을 방어와 찌르기로 바꿔 네 묶음 전부에 공격이
하나씩 들어간다. 정책을 복원 추출에서 shuffle_bag으로 바꿔 한 주기에
모든 묶음이 정확히 한 번씩 나오게 한다.

피해 총량은 주기당 27로 같고 분산만 사라진다. 플레이어가 "이번 주기에
아직 안 나온 묶음"을 읽을 수 있게 하려는 것이다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
MSG
```

---

### 과제 5: 짝 전투용 약체 `goblin_runt`

**파일**
- 생성: `Assets/StreamingAssets/Content/Cards/runt_jab.json` (+ `.meta`)
- 생성: `Assets/StreamingAssets/Content/Enemies/goblin_runt.json` (+ `.meta`)
- 수정: `Assets/StreamingAssets/Content/Battles/goblin_pair.json`
- 생성(테스트): `Assets/Core/Tests/EditMode/GoblinRuntContentTests.cs` (+ `.meta`)
- 수정: `Assets/Core/Tests/EditMode/ContentBootstrapTests.cs:19,23` (카드 34 → 35, 적 목록)

**인터페이스**
- 소비: `ContentEncounterSource.For(string battleId)` — 추첨 없이 편성 하나를 만든다.
- 생산: 적 id `goblin_runt`, 카드 id `runt_jab`.

- [ ] **단계 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/GoblinRuntContentTests.cs`:

```csharp
using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Enemies;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>짝 전투는 약체 둘이다(전투 템포 설계 변경 4). 총 HP는 단독 고블린과 같고,
    /// 두 개체는 같은 덱의 사본을 각자 섞어 쓴다.</summary>
    public class GoblinRuntContentTests
    {
        private static ContentEncounterSource Source()
            => new ContentEncounterSource(TestContent.Content(), CombatRegistries.EnemyPolicies());

        [Test]
        public void The_pair_is_two_runts_with_the_same_total_hp_as_one_goblin()
        {
            var setup = Source().For("goblin_pair");

            CollectionAssert.AreEqual(
                new[] { "goblin_runt#0", "goblin_runt#1" },
                setup.Enemies.Select(e => e.Enemy.Id).ToArray());
            Assert.AreEqual(28, setup.Enemies.Sum(e => e.Enemy.Hp));
        }

        [Test]
        public void Each_runt_gets_its_own_shuffle_bag_over_one_shared_deck()
        {
            var definition = TestContent.Content().Enemies.Get("goblin_runt");
            var setup = Source().For("goblin_pair");

            Assert.AreEqual(EnemyPolicyKeys.ShuffleBag, definition.Policy);
            Assert.AreNotSame(
                setup.Enemies[0].Policy, setup.Enemies[1].Policy,
                "개체마다 정책 인스턴스가 따로여야 가방이 갈린다.");
            Assert.AreEqual(4, definition.Bundles.Count);
        }

        [Test]
        public void Runt_jab_hits_the_front_party_member_for_three()
        {
            var card = TestContent.Content().Cards.Get("runt_jab");

            Assert.AreEqual(Side.Enemy, card.Side);
            Assert.AreEqual(6, card.BaseExecutionOrder);
            Assert.AreEqual(CardTargetRange.FrontOne, card.AllyTarget);
            Assert.AreEqual(
                3, card.Effects.Where(e => e.Key == EffectKeys.Damage).Sum(e => e.EffectValue));
        }
    }
}
```

- [ ] **단계 2: 실패를 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~GoblinRuntContentTests"
```

기대: 실패. 적 카탈로그에 `goblin_runt`가 없다.

- [ ] **단계 3: 적 카드와 적 정의를 저작한다**

`Assets/StreamingAssets/Content/Cards/runt_jab.json` (**적 카드라 `energyCost`·`grade`·`tags`가 없다**):
```json
{
  "cardFormat": 2,
  "id": "runt_jab",
  "name": "서툰 찌르기",
  "side": "Enemy",
  "category": "Execution",
  "baseExecutionOrder": 6,
  "targets": {
    "ally": "FrontOne"
  },
  "effects": [
    {
      "kind": "damage",
      "id": "e0",
      "targetFaction": "Ally",
      "value": 3
    }
  ]
}
```

`Assets/StreamingAssets/Content/Enemies/goblin_runt.json`:
```json
{
  "id": "goblin_runt",
  "displayName": "고블린 새끼",
  "maxHp": 14,
  "policy": "shuffle_bag",
  "bundles": [
    ["runt_jab"],
    ["runt_jab"],
    ["crude_guard"],
    ["runt_jab", "crude_guard"]
  ]
}
```

`Assets/StreamingAssets/Content/Battles/goblin_pair.json`:
```json
{
  "id": "goblin_pair",
  "enemies": ["goblin_runt", "goblin_runt"]
}
```

- [ ] **단계 4: 부팅 단언을 고친다**

`ContentBootstrapTests.cs:19`를 `35`로, `:23`을 이렇게 바꾼다.
```csharp
            CollectionAssert.AreEqual(new[] { "goblin", "goblin_runt" }, result.Content.Enemies.Ids);
```

- [ ] **단계 5: 통과를 확인하고 커밋한다**

```bash
Tools/verify.sh
```

```bash
git add -A
git commit -F- <<'MSG'
feat(content): 짝 전투를 고블린 새끼 둘로 저작한다

고블린 2마리는 턴당 13.5, 최악 22가 앞줄 한 명에게 집중돼 HP 25인
파티원이 2~3턴에 죽는다. 독은 대상 하나에만 쌓여 효율까지 절반이 된다.
그래서 짝 전투용 약체를 따로 저작한다.

고블린 새끼는 HP 14에 서툰 찌르기 3이고, 묶음 넷 중 하나는 방어뿐이다.
둘이면 총 HP 28에 턴당 4.5로 단독 고블린과 총량이 비슷하다. 한 마리가
쉬어도 다른 한 마리가 때리므로 빈 턴은 생기지 않는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
MSG
```

---

### 과제 6: 턴 수를 재고 목표에 맞춘다

**파일**
- 생성(테스트): `Assets/Core/Tests/EditMode/FixedBattleEncounter.cs` (+ `.meta`) — 공유 헬퍼
- 생성(테스트): `Assets/Core/Tests/EditMode/CombatPacingTests.cs` (+ `.meta`)
- 수정: `Assets/Core/Tests/EditMode/GoblinParityTests.cs` — 자기 안의 `FixedBattle` 중첩 클래스를 지우고
  공유 `FixedBattleEncounter`를 쓴다
- 수정(필요 시): 과제 3~5가 만진 콘텐츠 수치
- 수정: `docs/superpowers/specs/2026-09-20-combat-pacing-design.md` (실측 결과 기록)
- 수정: `docs/superpowers/README.md` (색인 수치와 계획 행)

**인터페이스**
- 소비: `CombatNode.Begin(RunState, CombatNodeContext)`, `DeckCombatSession`의
  `Hand`·`FateEnergy`·`PlayExecutionCard(int)`·`ResolveTurn()`·`BeginNextTurn()`·`Outcome`,
  과제 5의 `ContentEncounterSource.For(string)`.
- 생산: 없음(마지막 과제).

- [ ] **단계 1: 측정 하네스를 테스트로 쓴다**

먼저 공유 헬퍼 `Assets/Core/Tests/EditMode/FixedBattleEncounter.cs`. `GoblinParityTests`가 자기 안에
같은 클래스를 들고 있으므로 **그것을 지우고 이 파일을 쓰게 바꾼다**(2026-09-20 사용자 결정: 공유 헬퍼로 뽑는다).

```csharp
using System;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    /// <summary>편성을 이름으로 고정하는 테스트용 공급자. 추첨을 거치지 않으므로 편성 후보가 늘어도
    /// 결과가 변하지 않는다 — 특정 편성을 전제하는 테스트가 쓴다.</summary>
    public sealed class FixedBattleEncounter : IEncounterSource
    {
        private readonly ContentEncounterSource _source;
        private readonly string _battleId;

        public FixedBattleEncounter(ContentEncounterSource source, string battleId)
        {
            _source = source;
            _battleId = battleId;
        }

        public EncounterSetup Pick(Random encounterRng) => _source.For(_battleId);
    }
}
```

`GoblinParityTests.cs`에서는 중첩 `FixedBattle` 클래스와 그 주석을 지우고, `BeginNode()`의
`new FixedBattle(...)`를 `new FixedBattleEncounter(...)`로 바꾼다. 주석의 근거("이 골든이 잠그는 것은
이관이지 편성 목록이 아니다")는 공유 파일이 아니라 **`BeginNode()` 옆에 남긴다** — 그 이유는 그
테스트의 것이다.

그리고 `Assets/Core/Tests/EditMode/CombatPacingTests.cs`:

```csharp
using System;
using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>전투 길이를 잰다(전투 템포 설계 「검증 절차」). Compare는 무조작 대 조작을 보는
    /// 도구라 "몇 턴에 끝나는가"를 재지 못하므로, 탐욕 스크립트로 한 판을 끝까지 돌려 턴 수를 센다.
    ///
    /// 경계가 넓은 이유는 이것이 밸런스 목표의 가드이지 정확한 값의 골든이 아니기 때문이다.
    /// 카드 한 장을 조정할 때마다 깨지면 의미가 없다. 목표(4~5턴)에서 크게 벗어날 때만 걸린다.</summary>
    public class CombatPacingTests
    {
        /// <summary>매 턴 손패의 실행 카드를 비용이 큰 것부터 에너지가 다할 때까지 낸다.
        /// 개입 카드는 건너뛴다 — 대상 선택이 필요해 스크립트로 표현할 수 없다.</summary>
        private static int TurnsToWin(string battleId, int runSeed, int maxTurns = 20)
        {
            var content = TestContent.Content();
            var run = RunSetup.NewRun(content, new[] { "member_a", "member_b" }, runSeed);
            var context = new CombatNodeContext(
                content.Statuses,
                content.CombatRules,
                new FixedBattleEncounter(
                    new ContentEncounterSource(content, CombatRegistries.EnemyPolicies()), battleId),
                new CharacterPoolRewardSource(content));
            var node = CombatNode.Begin(run, context);

            for (int turn = 0; turn < maxTurns; turn++)
            {
                PlayGreedily(node.Session);
                node.Session.ResolveTurn();
                if (node.Session.IsComplete)
                {
                    Assert.AreEqual(
                        Outcome.Win, node.Session.Outcome,
                        battleId + " 시드 " + runSeed + ": 탐욕 스크립트가 졌다.");
                    return turn + 1;
                }

                Assert.IsTrue(node.Session.BeginNextTurn());
            }

            Assert.Fail(battleId + " 시드 " + runSeed + ": " + maxTurns + "턴 안에 끝나지 않았다.");
            return -1;
        }

        private static void PlayGreedily(DeckCombatSession session)
        {
            bool played = true;
            while (played)
            {
                played = false;
                var order = session.Hand
                    .Select((card, index) => (card, index))
                    .Where(pair => pair.card.Def.Category == CardCategory.Execution
                        && pair.card.Def.EnergyCost <= session.FateEnergy)
                    .OrderByDescending(pair => pair.card.Def.EnergyCost)
                    .ToList();

                foreach (var pair in order)
                {
                    if (session.PlayExecutionCard(pair.index))
                    {
                        played = true;
                        break;
                    }
                }
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void A_single_goblin_falls_in_three_to_six_turns(int runSeed)
        {
            var turns = TurnsToWin("goblin_single", runSeed);

            Assert.That(turns, Is.InRange(3, 6), "단독 고블린 전투 턴 수: " + turns);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void The_runt_pair_falls_in_three_to_seven_turns(int runSeed)
        {
            var turns = TurnsToWin("goblin_pair", runSeed);

            Assert.That(turns, Is.InRange(3, 7), "짝 전투 턴 수: " + turns);
        }
    }
}
```

`PlayExecutionCard`가 손패 인덱스를 받고 성공하면 손패가 줄어들므로, 한 장 낼 때마다 목록을 다시
만든다(위 `while` 구조가 그 이유다).

- [ ] **단계 2: 돌려서 실제 턴 수를 본다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~CombatPacingTests"
```

통과하면 실패 메시지가 안 보이므로, **실측 턴 수를 확인하려면 경계를 일부러 좁혀 한 번 실패시킨다**
(`Is.InRange(3, 3)`). 여섯 경우의 턴 수를 적어 두고 경계를 원래대로 되돌린다.

- [ ] **단계 3: 목표에서 벗어나면 조정한다**

단독 전투가 6턴을 넘거나 3턴 미만이면 설계의 조정 순서를 따른다: **고블린 HP → `heavy_swing` 피해 →
방어 수치.** 한 번에 하나만 바꾸고 단계 2를 다시 돈다. 짝 전투만 어긋나면 `goblin_runt`의 HP나
`runt_jab` 피해를 조정한다 — 단독 쪽 수치는 건드리지 않는다.

수치를 바꿨다면 과제 3~5의 콘텐츠 테스트 기대값과 `GoblinParityTests` 골든도 함께 갱신한다.

- [ ] **단계 3.5: 짝 전투에 표적 선택이 실제로 있는지 잠근다**

설계 「검증 절차」 3번이다. `flank_jab`이 뒷줄을 때리지 못하면 짝 전투는 선택 없는 전투가 되므로,
카드 저작(과제 1)이 아니라 **전투 결과**로 확인한다. `CombatPacingTests`에 더한다.

```csharp
        [Test]
        public void Flank_jab_hits_the_back_runt_while_cleave_hits_the_front()
        {
            var content = TestContent.Content();
            var runts = new ContentEncounterSource(content, CombatRegistries.EnemyPolicies())
                .For("goblin_pair").Enemies;
            var session = new DeckCombatSession(
                content.Statuses,
                new[]
                {
                    new PartyMemberLoadout(
                        "hero", "용사", 40,
                        new[] { content.Cards.Get("flank_jab"), content.Cards.Get("cleave") })
                },
                runts,
                content.CombatRules.Party,
                partyCards: null,
                fateEnergyPerTurn: 3,
                seed: 1);
            var backHpBefore = session.State.Enemies[1].Hp;
            var frontHpBefore = session.State.Enemies[0].Hp;

            while (session.PlayExecutionCard(0))
            {
            }

            session.ResolveTurn();

            Assert.AreEqual(backHpBefore - 3, session.State.Enemies[1].Hp, "파고들기는 뒷줄을 때린다.");
            Assert.AreEqual(frontHpBefore - 4, session.State.Enemies[0].Hp, "베기는 앞줄을 때린다.");
        }
```

파티 튜닝이 생존 1명일 때 3장을 뽑으므로 두 장이 모두 손에 들어온다. `PlayExecutionCard(0)`을
더 낼 수 없을 때까지 반복해 둘 다 낸다. 적의 피해가 섞이지 않도록 **적 HP만** 단언한다.

기대: 통과. 실패하면 `flank_jab`의 `targets.enemy`가 `BackOne`이 아니거나, 적이 둘일 때 대상 선택이
설계와 다르게 도는 것이다 — 후자면 규칙 문제이므로 멈추고 사용자에게 보고한다.

- [ ] **단계 4: 설계 문서에 실측을 기록한다**

`docs/superpowers/specs/2026-09-20-combat-pacing-design.md`의 「검증 절차」 아래에 실측표를 더한다.
시드별 턴 수 여섯 개와, 조정했다면 무엇을 왜 바꿨는지 한 줄.

- [ ] **단계 5: 색인을 갱신한다**

`docs/superpowers/README.md`:
- 「현재 수치」 절의 카드 JSON 수를 **35**로, 적 JSON 수를 **2**로 고친다. 그 절의 "카드 JSON 26"은
  2026-08-28 기준이라 이미 낡았으므로 측정일(2026-09-20)을 함께 적는다.
- 「활성 계획과 로드맵」의 전투 템포 행에 계획 문서 링크를 더한다.

- [ ] **단계 6: Unity 배치로 회귀를 본다**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath <워크트리 절대경로> -runTests -testPlatform EditMode -testResults /private/tmp/pacing-editmode.xml -logFile /private/tmp/pacing-editmode.log
```

**`-quit`를 붙이지 않는다.** 결과 XML의 `test-run` 속성에서 `failed="0"`을 직접 확인한다. 종료 코드만
보지 않는다.

- [ ] **단계 7: 커밋한다**

```bash
git add -A
git commit -F- <<'MSG'
test(core): 전투 길이를 탐욕 스크립트로 재는 가드를 둔다

Compare는 무조작 대 조작을 보는 도구라 "몇 턴에 끝나는가"를 재지 못한다.
매 턴 낼 수 있는 실행 카드를 비용 큰 것부터 내는 스크립트로 한 판을
끝까지 돌려 턴 수를 센다.

경계를 넓게 잡은 것은 이것이 목표의 가드이지 정확한 값의 골든이 아니기
때문이다. 카드 한 장을 조정할 때마다 깨지면 의미가 없다.

실측 턴 수를 설계 문서에 적고 색인 수치를 갱신했다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
MSG
```

---

## 완료 조건

- `Tools/verify.sh` 전체 통과.
- Unity 배치 EditMode `failed="0"`.
- 단독 전투와 짝 전투가 각각 목표 범위 안에서 끝난다(`CombatPacingTests`).
- 짝 전투에서 `flank_jab`이 뒷줄을, `cleave`가 앞줄을 때린다 — 표적 선택이 실제로 존재한다.
- 설계 문서에 실측 턴 수가 적혀 있고, 색인 수치가 실제와 맞는다.
- **화면 확인은 사용자 몫이다**(규칙 17): 적 둘의 레이아웃, 새 카드 6장의 프레임과 자동 생성 설명,
  그리고 실제로 전투가 루즈하지 않게 느껴지는지. 머지 전에 사용자에게 Play를 요청한다.
- 머지는 사용자 승인 후에만 한다(규칙 19). 머지 뒤 워크트리와 브랜치를 정리하고, 계획 문서를
  `docs/superpowers/.archive/plans/`로 옮기며 색인에서 그 행을 지운다(규칙 20).
