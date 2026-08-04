# 전투 화면 컴포넌트 분해 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-08-04
- 상태: `active`
- 권위 문서: [`specs/2026-08-04-battle-screen-decomposition-design.md`](../specs/2026-08-04-battle-screen-decomposition-design.md)
- 브랜치: `refactor/battle-screen-decomposition`

**Goal:** `BattleScreenController` 467줄을 컴포넌트 넷으로 나눠, 캐릭터 아트 도입과 표현 변경이
한 컴포넌트만 건드리게 만든다.

**Architecture:** 경계선은 **"세션을 변경하는가"**다. 표현 변환(`BattlePresenter`)과 뷰
셋(`BattleUnitsView`·`BattlePilesView`·`BattleHudView`)은 세션을 읽기만 하므로 분리하고, 세션을
변경하는 입력 핸들러는 컨트롤러에 남는다. 씬 배선은 `BattleSceneBuilder`가 코드로 하므로 손으로
드래그하지 않는다.

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), Unity 6000.5.2f1, NUnit, TextMeshPro, uGUI

## Global Constraints

- 헤드리스 테스트: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- Unity 배치 (`-runTests`와 `-quit`를 **함께 쓰지 않는다**):
  ```
  /Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode \
    -projectPath $(pwd) -runTests -testPlatform EditMode \
    -testResults /private/tmp/<이름>.xml -logFile /private/tmp/<이름>.log
  ```
- 착수 기준선: 헤드리스 **499/499**, Unity EditMode **557/558**(skipped 1 = `[Explicit]`)
- `[SerializeField] private`을 쓰고 `public` 필드를 만들지 않는다 (규칙 4)
- 런타임 문자열 탐색 금지 (규칙 3) — `FindObjectOfType`·`GameObject.Find` 금지, 협력자는 인스펙터 할당
- 객체를 즉석에서 만들지 않는다 (규칙 1) — 유닛은 기존 프리팹을 `Instantiate`
- 튜닝 수치를 하드코딩하지 않는다 (규칙 8)
- 워킹 트리를 깨끗이 남긴다 (규칙 18) · 문서 색인을 같은 커밋에서 (규칙 20)
- C# 9 한계: `record struct`·기본 인터페이스 구현·파일 범위 네임스페이스 금지
- **씬 저작은 AI가 직접 한다** (규칙 17, 2026-08-04 개정). Play만 사용자 몫이다

## 중간 상태에 대한 경고

**Task 1~4 동안 `FateWeaverBattle.unity`는 낡은 배선을 갖는다.** 컨트롤러에서 필드를 떼어내는
순간 씬의 직렬화 값이 갈 곳을 잃기 때문이다. Unity EditMode 테스트는 이 씬을 로드하지 않으므로
초록을 유지하지만, **Task 5에서 씬을 재생성하기 전까지 Play는 깨진다.** 이는 의도된 순서다 —
코드가 다 자리를 잡은 뒤 씬을 한 번만 다시 만든다.

각 Task는 `BattleSceneBuilder`도 함께 고쳐 **컴파일 가능한 상태**를 유지한다.

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Unity/BattlePresenter.cs` (신설) | `OwnedCard`·`ExecutionCardInstance` → `CardPresentation`. 아트·소유자 색·소유자 이름 |
| `Assets/Unity/BattleUnitsView.cs` (신설) | 유닛 스폰과 HP·상태·정렬 갱신. **캐릭터 아트의 미래 진입점** |
| `Assets/Unity/BattlePilesView.cs` (신설) | 파일 3개의 내용 바인딩·개수·입력 활성화 |
| `Assets/Unity/BattleHudView.cs` (신설) | 운명력·메시지 텍스트, 턴 버튼·라벨, 리셋 버튼 |
| `Assets/Unity/BattleScreenController.cs` (축소) | 세션 소유, 입력, 조립 |
| `Assets/Unity/CardSelectionController.cs` (수정) | 클릭 캐처 둘을 흡수 |
| `Assets/Unity/Editor/BattleSceneBuilder.cs` (수정) | 새 컴포넌트 생성·배선, 컨테이너 둘 신설 |

---

## Task 1: `BattlePresenter`로 표현 변환을 뽑는다

**Files:**
- Create: `Assets/Unity/BattlePresenter.cs` (+ `.meta`)
- Create: `Assets/Tests/UnityEditMode/BattlePresenterTests.cs` (+ `.meta`)
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`

**Interfaces:**
- Produces: `BattlePresenter.Initialize(Func<string, string> ownerName)`,
  `BattlePresenter.For(OwnedCard) → CardPresentation`,
  `BattlePresenter.For(ExecutionCardInstance) → CardPresentation`

설계 §4.3대로 이름 조회를 델리게이트로 받아 세션 타입을 모르게 한다. 색은 `CharacterAsset[]`에서
직접 찾는다.

- [ ] **Step 1: 기준선을 기록한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 499`

- [ ] **Step 2: 테스트를 먼저 쓴다 (RED)**

Create `Assets/Tests/UnityEditMode/BattlePresenterTests.cs`:

```csharp
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>표현 변환이 소유자 분기 셋(적 / 파티 공용 / 개별 캐릭터)을 옳게 가르는지 잠근다.
    /// 이름 조회가 델리게이트라 세션 없이 전 분기를 돌린다.</summary>
    public class BattlePresenterTests
    {
        private const string MemberId = "member_a";
        private static readonly Color MemberColor = new Color(0.2f, 0.4f, 0.6f, 1f);

        private BattlePresenter _presenter;
        private GameObject _go;

        private static CardDefinition PlayerCard() => new CardDefinition(
            "probing_strike", "견제타", Side.Player, 4,
            new[] { new EffectData(EffectKeys.Damage, 4) })
            { EnergyCost = 1, Category = CardCategory.Execution };

        private static CardDefinition EnemyCard() => new CardDefinition(
            "goblin_jab", "잽", Side.Enemy, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("presenter");
            _presenter = _go.AddComponent<BattlePresenter>();

            var member = ScriptableObject.CreateInstance<CharacterAsset>();
            var so = new UnityEditor.SerializedObject(member);
            so.FindProperty("_id").stringValue = MemberId;
            so.FindProperty("_color").colorValue = MemberColor;
            so.ApplyModifiedPropertiesWithoutUndo();

            var presenterSo = new UnityEditor.SerializedObject(_presenter);
            var party = presenterSo.FindProperty("_party");
            party.arraySize = 1;
            party.GetArrayElementAtIndex(0).objectReferenceValue = member;
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            _presenter.Initialize(id => id == MemberId ? "파티원 A" : null);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void EnemyCardHasNoOwnerPresentation()
        {
            var card = new OwnedCard(EnemyCard(), null);

            var presentation = _presenter.For(card);

            Assert.IsFalse(presentation.IsPartyOwned);
            Assert.IsNull(presentation.OwnerDisplayName);
        }

        [Test]
        public void PartySharedCardUsesTheSharedOwnerName()
        {
            var card = new OwnedCard(PlayerCard(), null);

            var presentation = _presenter.For(card);

            Assert.IsTrue(presentation.IsPartyOwned);
            Assert.AreEqual(
                PlaytestKoreanText.PartyOwnerName(), presentation.OwnerDisplayName);
        }

        [Test]
        public void OwnedCardUsesTheCharacterNameAndColor()
        {
            var card = new OwnedCard(PlayerCard(), MemberId);

            var presentation = _presenter.For(card);

            Assert.AreEqual("파티원 A", presentation.OwnerDisplayName);
            Assert.AreEqual(MemberColor, presentation.OwnerColor);
            Assert.IsFalse(
                presentation.IsPartyOwned,
                "개별 소유 카드는 원본에서 isPartyOwned=false다.");
        }

        [Test]
        public void MissingArtCatalogResolvesToNullSprite()
        {
            var presentation = _presenter.For(new OwnedCard(EnemyCard(), null));

            Assert.IsNull(presentation.Art, "아트 카탈로그가 없으면 조용히 null이어야 한다.");
        }
    }
}
```

시그니처는 실측했다: `OwnedCard(CardDefinition def, string ownerId)`이고 `OwnerId`는 읽기 전용이라
생성자로만 정한다. `CardPresentation`의 소유자 이름 프로퍼티는 `OwnerName`이 아니라
**`OwnerDisplayName`**이다.

Run: Unity 배치 EditMode
Expected: 컴파일 실패 — `BattlePresenter`가 없다

- [ ] **Step 3: `BattlePresenter`를 만든다 (GREEN)**

Create `Assets/Unity/BattlePresenter.cs`:

```csharp
using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>카드를 표현 모델로 옮긴다. 세션 타입도 콘텐츠 타입도 모르고, 이름 조회만
    /// 델리게이트로 받는다(설계 §4.3) — 그래야 테스트가 세션 없이 전 분기를 돌린다.</summary>
    public sealed class BattlePresenter : MonoBehaviour
    {
        [Tooltip("카드 앞면 아트. 비어 있으면 아트 없이 그린다.")]
        [SerializeField] private CardArtCatalog _cardArt;

        [Tooltip("캐릭터 색 원본. 표시명은 콘텐츠(JSON)에서 온다.")]
        [SerializeField] private CharacterAsset[] _party = Array.Empty<CharacterAsset>();

        private static readonly Color PartyOwnerColor = new Color(0.55f, 0.48f, 0.75f, 1f);

        private Func<string, string> _ownerName;

        /// <summary>세션의 파티에서 표시명을 읽는 델리게이트를 주입한다. 없으면 소유자 이름이
        /// 비어 있는 표현이 나온다.</summary>
        public void Initialize(Func<string, string> ownerName) => _ownerName = ownerName;

        public CardPresentation For(OwnedCard card)
        {
            Resolve(card.OwnerId, card.Def.Side, out var name, out var color, out var isParty);
            return CardPresentation.From(card, ArtFor, name, color, isParty);
        }

        public CardPresentation For(ExecutionCardInstance card)
        {
            Resolve(card.OwnerId, card.Def.Side, out var name, out var color, out var isParty);
            return CardPresentation.FromDefinition(card.Def, ArtFor, name, color, isParty);
        }

        private Sprite ArtFor(string id) => _cardArt != null ? _cardArt.ArtFor(id) : null;

        /// <summary>소유자 분기 셋. **원본 OwnerPresentation의 동작을 그대로 옮긴 것이다:**
        /// 적은 표현 없음, 소유자 없는 파티 카드만 isPartyOwned=true, 개별 소유는 이름은 채우되
        /// isPartyOwned는 false로 남는다(카드 테두리 표현이 갈린다).</summary>
        private void Resolve(
            string ownerId, Side side, out string name, out Color color, out bool isPartyOwned)
        {
            name = null;
            color = default;
            isPartyOwned = false;
            if (side == Side.Enemy)
            {
                return;
            }

            if (ownerId == null)
            {
                name = PlaytestKoreanText.PartyOwnerName();
                color = PartyOwnerColor;
                isPartyOwned = true;
                return;
            }

            name = _ownerName != null ? _ownerName(ownerId) : null;
            var character = Find(ownerId);
            if (character != null)
            {
                color = character.Color;
            }
        }

        /// <summary>유닛 틴트. 카드 표현과 폴백이 다르다 — 원본 SpawnUnits는 캐릭터를 못 찾으면
        /// 공용 색을 썼고, 원본 OwnerPresentation은 색을 건드리지 않았다.</summary>
        public Color OwnerColor(string ownerId)
        {
            var character = Find(ownerId);
            return character != null ? character.Color : PartyOwnerColor;
        }

        private CharacterAsset Find(string ownerId)
        {
            foreach (var character in _party)
            {
                if (character != null && character.Id == ownerId)
                {
                    return character;
                }
            }

            return null;
        }
    }
}
```

**이 분기는 원본과 한 글자도 다르면 안 된다.** `isPartyOwned`와 색 폴백 둘 다 호출 지점마다 다르게
동작하며, 어긋나면 카드 테두리와 유닛 틴트가 조용히 바뀐다. 구현 전에
`Assets/Unity/BattleScreenController.cs`의 `OwnerPresentation`과 `SpawnUnits`를 나란히 놓고
대조한다.

`.meta`는 손으로 만든다:

```bash
guid=$(uuidgen | tr -d - | tr 'A-Z' 'a-z')
printf 'fileFormatVersion: 2\nguid: %s' "$guid" > Assets/Unity/BattlePresenter.cs.meta
```

- [ ] **Step 4: 컨트롤러에서 표현 코드를 지우고 위임한다**

`BattleScreenController`에서 지운다: `_cardArt`·`_party` 필드, `ArtFor`, `PresentationFor` 둘,
`OwnerPresentation`, `CharacterFor`, `PartyOwnerColor` 상수.

더한다:

```csharp
        [SerializeField] private BattlePresenter _presenter;
```

`StartSession`에서 세션을 만든 직후 주입한다:

```csharp
            _presenter.Initialize(OwnerNameOf);
```

그리고 헬퍼를 더한다:

```csharp
        /// <summary>표시명은 콘텐츠에서 왔고 세션이 들고 있다.</summary>
        private string OwnerNameOf(string ownerId)
        {
            foreach (var member in _session.State.Party)
            {
                if (member.Id == ownerId)
                {
                    return member.Name;
                }
            }

            return null;
        }
```

`Presentations`·`RefreshAll`·`OnHandClicked`·`OnHandHovered`의 `PresentationFor(x)` 호출을
`_presenter.For(x)`로 바꾼다.

`SpawnUnits`가 `CharacterFor(member.Id)?.Color`를 쓰고 있으므로, 그 자리는 **Task 2가 가져간다.**
지금은 임시로 `_presenter`에 색 조회를 노출하지 말고, `SpawnUnits`가 쓰던 색을
`PartyOwnerColor` 상수로 두어 컴파일만 통과시킨다 — Task 2에서 `BattleUnitsView`가 올바른 색을
받는다. **이 임시 상태를 커밋 메시지에 적는다.**

- [ ] **Step 5: 빌더를 고친다**

`Assets/Unity/Editor/BattleSceneBuilder.cs`의 컨트롤러 배선 블록(183줄 부근)에서
`_cardArt`·`_party` 배선을 지우고, 새 GameObject에 프레젠터를 붙여 배선한다:

```csharp
            var presenterGo = new GameObject("BattlePresenter");
            presenterGo.transform.SetParent(controllerGo.transform, false);
            var presenter = presenterGo.AddComponent<BattlePresenter>();
            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("_cardArt").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardArtCatalog>(CardArtCatalogPath);
            var presenterParty = presenterSo.FindProperty("_party");
            presenterParty.arraySize = party.Length;
            for (int i = 0; i < party.Length; i++)
            {
                presenterParty.GetArrayElementAtIndex(i).objectReferenceValue = party[i];
            }
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            so.FindProperty("_presenter").objectReferenceValue = presenter;
```

- [ ] **Step 6: 검증하고 커밋한다**

Run: Unity 배치 EditMode
Expected: XML의 `failed="0"`, 새 테스트 4개 증가

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 499` (헤드리스는 Unity 어셈블리를 컴파일하지 않아 불변)

```bash
git status --short
git add -A Assets
git commit -m "refactor: 표현 변환을 BattlePresenter로 뽑는다

카드 → CardPresentation 변환과 아트·소유자 색·소유자 이름 해석이 컨트롤러를
떠난다. 이름 조회는 델리게이트로 받아 세션 타입을 모른다.

SpawnUnits의 캐릭터 색은 아직 임시로 공용 색을 쓴다 — Task 2의
BattleUnitsView가 올바른 색을 받는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: `BattleUnitsView`로 유닛을 뽑는다

**Files:**
- Create: `Assets/Unity/BattleUnitsView.cs` (+ `.meta`)
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`

**Interfaces:**
- Produces: `BattleUnitsView.Spawn(CombatState state, Func<string, Color> colorFor, Func<string, string> enemyNameFor)`,
  `BattleUnitsView.Refresh(CombatState state)`

설계 §4.6대로 **`UnitView.Bind`의 유일한 호출자**가 된다. 캐릭터 애니메이션이 들어올 자리다.

- [ ] **Step 1: `BattleUnitsView`를 만든다**

Create `Assets/Unity/BattleUnitsView.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>유닛 뷰의 스폰과 갱신을 맡는다. UnitView.Bind의 유일한 호출자이므로 캐릭터 아트가
    /// 스프라이트 시트 애니메이션으로 바뀔 때 이 컴포넌트만 바뀐다(설계 §4.6).</summary>
    public sealed class BattleUnitsView : MonoBehaviour
    {
        [SerializeField] private UnitView _unitPrefab;
        [SerializeField] private RectTransform _playerUnitsRow;
        [SerializeField] private RectTransform _enemyUnitsRow;

        private static readonly Color EnemyUnitTint = new Color(0.55f, 0.25f, 0.25f, 1f);

        private readonly Dictionary<string, UnitView> _partyUnits =
            new Dictionary<string, UnitView>();
        private readonly Dictionary<string, UnitView> _enemyUnits =
            new Dictionary<string, UnitView>();
        private readonly Dictionary<string, int> _enemyMaxHp = new Dictionary<string, int>();

        public bool IsBound => _unitPrefab != null
            && _playerUnitsRow != null && _enemyUnitsRow != null;

        /// <summary>기존 유닛을 지우고 상태에 맞춰 다시 만든다. 색과 적 이름은 표현 관심사라
        /// 바깥에서 받는다.</summary>
        public void Spawn(
            CombatState state, Func<string, Color> colorFor, Func<string, string> enemyNameFor)
        {
            foreach (Transform child in _playerUnitsRow) Destroy(child.gameObject);
            foreach (Transform child in _enemyUnitsRow) Destroy(child.gameObject);
            _partyUnits.Clear();
            _enemyUnits.Clear();
            _enemyMaxHp.Clear();

            foreach (var member in state.Party)
            {
                var view = Instantiate(_unitPrefab, _playerUnitsRow);
                view.Bind(member.Name, colorFor(member.Id));
                _partyUnits.Add(member.Id, view);
            }

            foreach (var enemy in state.Enemies)
            {
                var view = Instantiate(_unitPrefab, _enemyUnitsRow);
                view.Bind(enemyNameFor(enemy.Id), EnemyUnitTint);
                _enemyUnits.Add(enemy.Id, view);
                _enemyMaxHp.Add(enemy.Id, enemy.Hp);
            }
        }

        public void Refresh(CombatState state)
        {
            int partyCount = state.Party.Count;
            for (int i = 0; i < partyCount; i++)
            {
                var member = state.Party[i];
                if (_partyUnits.TryGetValue(member.Id, out var view))
                {
                    view.SetHp(member.Hp, member.MaxHp);
                    view.SetStatuses(member.Statuses.All);
                    view.transform.SetSiblingIndex(partyCount - 1 - i);
                }
            }

            int enemyCount = state.Enemies.Count;
            for (int i = 0; i < enemyCount; i++)
            {
                var enemy = state.Enemies[i];
                if (_enemyUnits.TryGetValue(enemy.Id, out var view)
                    && _enemyMaxHp.TryGetValue(enemy.Id, out var maxHp))
                {
                    view.SetHp(enemy.Hp, maxHp);
                    view.SetStatuses(enemy.Statuses.All);
                    view.transform.SetSiblingIndex(i);
                }
            }
        }
    }
}
```

`.meta`는 Task 1 Step 3과 같은 방식으로 만든다.

- [ ] **Step 2: 컨트롤러를 위임으로 바꾼다**

지운다: `_unitPrefab`·`_playerUnitsRow`·`_enemyUnitsRow` 필드, `_partyUnits`·`_enemyUnits`·
`_enemyMaxHp` 사전, `SpawnUnits`, `RefreshUnits`, `EnemyUnitTint` 상수.

더한다:

```csharp
        [SerializeField] private BattleUnitsView _units;
```

색 조회는 `BattlePresenter`가 이미 `CharacterAsset[]`을 갖고 있으므로 거기에 공개 메서드를 더한다
(`Assets/Unity/BattlePresenter.cs`):

```csharp
        /// <summary>유닛 틴트. BattleUnitsView가 쓴다.</summary>
        public Color OwnerColor(string ownerId) => ColorFor(ownerId);
```

`StartSession`의 `SpawnUnits()` 호출을 바꾼다:

```csharp
            _units.Spawn(
                _session.State,
                _presenter.OwnerColor,
                id => PlaytestKoreanText.EnemyName(id, id));
```

`RefreshAll`의 `RefreshUnits()`를 `_units.Refresh(_session.State);`로 바꾼다.

Task 1 Step 4에서 임시로 넣은 공용 색 사용은 이 단계에서 사라진다.

- [ ] **Step 3: 빌더를 고친다**

`stage`가 이미 `playerRow`·`enemyRow`의 부모이므로 거기에 붙인다:

```csharp
            var units = stage.gameObject.AddComponent<BattleUnitsView>();
            var unitsSo = new SerializedObject(units);
            unitsSo.FindProperty("_unitPrefab").objectReferenceValue = unitPrefab;
            unitsSo.FindProperty("_playerUnitsRow").objectReferenceValue = playerRow;
            unitsSo.FindProperty("_enemyUnitsRow").objectReferenceValue = enemyRow;
            unitsSo.ApplyModifiedPropertiesWithoutUndo();

            so.FindProperty("_units").objectReferenceValue = units;
```

컨트롤러 배선에서 `_unitPrefab`·`_playerUnitsRow`·`_enemyUnitsRow` 줄을 지운다.

- [ ] **Step 4: 검증하고 커밋한다**

Run: Unity 배치 EditMode
Expected: `failed="0"`

```bash
git status --short
git add -A Assets
git commit -m "refactor: 유닛 스폰·갱신을 BattleUnitsView로 뽑는다

UnitView.Bind의 유일한 호출자가 된다 — 캐릭터 아트가 스프라이트 시트
애니메이션으로 바뀔 때 이 컴포넌트만 바뀐다(설계 §4.6).

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: `BattlePilesView`로 파일 셋을 뽑는다

**Files:**
- Create: `Assets/Unity/BattlePilesView.cs` (+ `.meta`)
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`

**Interfaces:**
- Produces: `BattlePilesView.Bind(Func<IReadOnlyList<CardPresentation>> draw, Func<...> discard, Func<...> full)`,
  `BattlePilesView.Refresh(int drawCount, int discardCount, int fullCount)`,
  `BattlePilesView.SetInputEnabled(bool value)`

설계 §4.2대로 **내용 제공(`Bind`)과 개수 갱신(`Refresh`)이 별개다.**

- [ ] **Step 1: `BattlePilesView`를 만든다**

Create `Assets/Unity/BattlePilesView.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>덱 파일 셋. 내용은 지연 평가 제공자로 한 번 꽂고(Bind), 이후에는 개수만
    /// 갱신한다(Refresh). 선택 중에는 입력을 막는다.</summary>
    public sealed class BattlePilesView : MonoBehaviour
    {
        [SerializeField] private PileView _drawPile;
        [SerializeField] private PileView _discardPile;
        [SerializeField] private PileView _fullDeck;

        public bool IsBound => _drawPile != null && _discardPile != null && _fullDeck != null;

        public void Bind(
            Func<IReadOnlyList<CardPresentation>> draw,
            Func<IReadOnlyList<CardPresentation>> discard,
            Func<IReadOnlyList<CardPresentation>> full)
        {
            _drawPile.Bind(draw);
            _discardPile.Bind(discard);
            _fullDeck.Bind(full);
        }

        public void Refresh(int drawCount, int discardCount, int fullCount)
        {
            _drawPile.SetCount(drawCount);
            _discardPile.SetCount(discardCount);
            _fullDeck.SetCount(fullCount);
        }

        public void SetInputEnabled(bool value)
        {
            _drawPile.SetInputEnabled(value);
            _discardPile.SetInputEnabled(value);
            _fullDeck.SetInputEnabled(value);
        }
    }
}
```

- [ ] **Step 2: 컨트롤러를 위임으로 바꾼다**

지운다: `_drawPile`·`_discardPile`·`_fullDeck` 필드, `BindPiles`.

더한다: `[SerializeField] private BattlePilesView _piles;`

`StartSession`의 `BindPiles()`를 바꾼다:

```csharp
            _piles.Bind(
                () => Presentations(_session.DrawPile)
                    .OrderBy(p => p.DisplayName, StringComparer.Ordinal).ToList(),
                () => Presentations(_session.DiscardPile),
                () => Presentations(_session.AllDeckCards));
```

`RefreshHudTexts`의 파일 개수 세 줄을 지우고 `RefreshAll`에 더한다:

```csharp
            _piles.Refresh(
                _session.DrawCount, _session.DiscardCount, _session.AllDeckCards.Count);
```

`RefreshSelections`의 파일 세 줄을 `_piles.SetInputEnabled(!selectionActive);`로 바꾼다.

- [ ] **Step 3: 빌더를 고친다**

파일 셋은 캔버스 직속이라 공통 부모가 없다. **전체 화면으로 늘린 컨테이너를 만들어** 자식들의
앵커가 그대로 동작하게 한다:

```csharp
            var pilesRoot = BattleUiKit.Rect(canvasRect, "Piles");
            pilesRoot.anchorMin = Vector2.zero;
            pilesRoot.anchorMax = Vector2.one;
            pilesRoot.offsetMin = Vector2.zero;
            pilesRoot.offsetMax = Vector2.zero;
```

`PileView.Create(canvasRect, ...)` 호출의 첫 인자를 `pilesRoot`로 바꾼다(세 곳). 컨테이너가 캔버스와
정확히 같은 사각형이므로 `Place(...)`의 앵커 계산 결과가 바뀌지 않는다.

```csharp
            var piles = pilesRoot.gameObject.AddComponent<BattlePilesView>();
            var pilesSo = new SerializedObject(piles);
            pilesSo.FindProperty("_drawPile").objectReferenceValue = drawPile;
            pilesSo.FindProperty("_discardPile").objectReferenceValue = discardPile;
            pilesSo.FindProperty("_fullDeck").objectReferenceValue = fullDeck;
            pilesSo.ApplyModifiedPropertiesWithoutUndo();

            so.FindProperty("_piles").objectReferenceValue = piles;
```

컨트롤러 배선에서 `_drawPile`·`_discardPile`·`_fullDeck` 줄을 지운다.

- [ ] **Step 4: 검증하고 커밋한다**

Run: Unity 배치 EditMode
Expected: `failed="0"`

```bash
git status --short
git add -A Assets
git commit -m "refactor: 덱 파일 셋을 BattlePilesView로 뽑는다

내용 제공(Bind)과 개수 갱신(Refresh)을 분리해 유지한다. 파일들에 전용
컨테이너를 만들되 캔버스와 같은 사각형으로 늘려 배치가 바뀌지 않게 한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: `BattleHudView`를 뽑고 클릭 캐처를 선택 컨트롤러로 옮긴다

**Files:**
- Create: `Assets/Unity/BattleHudView.cs` (+ `.meta`)
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/CardSelectionController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`

**Interfaces:**
- Produces: `BattleHudView.Initialize(UnityAction onTurn, UnityAction onReset)`,
  `BattleHudView.SetMessage(string)`, `BattleHudView.Refresh(int fateEnergy, bool turnResolved)`,
  `BattleHudView.SetInputEnabled(bool resetEnabled, bool turnEnabled)`
- Produces: `CardSelectionController`가 클릭 캐처 둘을 직접 구독해 선택을 취소한다

설계 §4.4대로 클릭 캐처는 HUD가 아니라 선택 UX다.

- [ ] **Step 1: `BattleHudView`를 만든다**

Create `Assets/Unity/BattleHudView.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>운명력·안내 문구와 턴 조작. 턴 라벨과 버튼 활성화가 한 상태에서 나오므로
    /// 함께 둔다.</summary>
    public sealed class BattleHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _energyText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _turnButton;
        [SerializeField] private TMP_Text _turnButtonLabel;
        [SerializeField] private Button _resetButton;

        public bool IsBound => _energyText != null && _messageText != null
            && _turnButton != null && _turnButtonLabel != null && _resetButton != null;

        public void Initialize(UnityAction onTurn, UnityAction onReset)
        {
            _turnButton.onClick.AddListener(onTurn);
            _resetButton.onClick.AddListener(onReset);
        }

        public void SetMessage(string message) => _messageText.text = message;

        public void Refresh(int fateEnergy, bool turnResolved)
        {
            _energyText.text = "운명력 " + fateEnergy;
            _turnButtonLabel.text = turnResolved ? "다음 턴" : "턴 실행";
        }

        public void SetInputEnabled(bool resetEnabled, bool turnEnabled)
        {
            _resetButton.interactable = resetEnabled;
            _turnButton.interactable = turnEnabled;
        }
    }
}
```

- [ ] **Step 2: 클릭 캐처를 `CardSelectionController`로 옮긴다**

`Assets/Unity/CardSelectionController.cs`에 필드 둘을 더한다:

```csharp
        [Tooltip("빈 곳 클릭으로 선택을 취소한다.")]
        [SerializeField] private Button _emptyClickCatcher;
        [SerializeField] private Button _dimClickCatcher;
```

`Initialize`의 끝에 구독을 더한다 (기존 인자는 그대로 둔다):

```csharp
            if (_emptyClickCatcher != null) _emptyClickCatcher.onClick.AddListener(CancelIfActive);
            if (_dimClickCatcher != null) _dimClickCatcher.onClick.AddListener(CancelIfActive);
```

그리고 메서드를 더한다:

```csharp
        /// <summary>빈 곳 클릭. 선택 중이 아니면 아무 일도 하지 않는다.</summary>
        private void CancelIfActive()
        {
            if (!SelectionActive)
            {
                return;
            }

            CancelSelection();
            _onApplied?.Invoke();
        }
```

`_onApplied`는 `Initialize(tryApply, currentTargets, onApplied)`가 저장하는 갱신 콜백의 실제
필드명이다(실측). 컨트롤러는 이것을 `RefreshAll`로 넘기므로 취소 후 화면이 갱신된다.

**"선택 취소." 문구는 사라진다.** 원본 `OnEmptyClicked`가 `SetMessage("선택 취소.")`를 했는데,
선택 컨트롤러는 메시지 싱크를 모른다. 문구를 유지하려면 `Initialize`에 콜백을 하나 더 받아야 하고
그건 인자를 넷으로 늘린다 — **문구를 포기한다.** 취소는 화면 갱신으로 이미 드러나고, 이 문구만을
위해 선택 컨트롤러에 메시지 의존을 넣는 것은 §4.4의 취지에 어긋난다. 이 판단을 커밋 메시지에 적는다.

- [ ] **Step 3: 컨트롤러를 위임으로 바꾼다**

지운다: `_energyText`·`_messageText`·`_turnButton`·`_turnButtonLabel`·`_resetButton`·
`_emptyClickCatcher`·`_dimClickCatcher` 필드, `RefreshHudTexts`, `SetMessage` 본문,
`OnEmptyClicked`.

더한다: `[SerializeField] private BattleHudView _hud;`

`Start`의 버튼 구독 넷을 바꾼다:

```csharp
            _hud.Initialize(OnTurnButton, StartSession);
            _selection.Initialize(TryApplySelection, CurrentValidTargets, RefreshAll);
```

`SetMessage`를 위임으로 바꾼다:

```csharp
        private void SetMessage(string message) => _hud.SetMessage(message);
```

`RefreshAll`에서 `RefreshHudTexts()`를 바꾼다:

```csharp
            _hud.Refresh(_session.FateEnergy, _session.CurrentTurnResolved);
```

`RefreshSelections`를 바꾼다:

```csharp
        private void RefreshSelections()
        {
            bool active = _selection.SelectionActive;
            _piles.SetInputEnabled(!active);
            _hud.SetInputEnabled(!active, !active && !_session.IsComplete);
        }
```

- [ ] **Step 4: 빌더를 고친다**

HUD 위젯도 캔버스 직속이므로 Task 3과 같은 방식으로 컨테이너를 만든다:

```csharp
            var hudRoot = BattleUiKit.Rect(canvasRect, "Hud");
            hudRoot.anchorMin = Vector2.zero;
            hudRoot.anchorMax = Vector2.one;
            hudRoot.offsetMin = Vector2.zero;
            hudRoot.offsetMax = Vector2.zero;
```

`energy`·`message`·`turnButton`·`resetButton` 생성의 부모를 `hudRoot`로 바꾼다.

```csharp
            var hud = hudRoot.gameObject.AddComponent<BattleHudView>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("_energyText").objectReferenceValue = energy;
            hudSo.FindProperty("_messageText").objectReferenceValue = message;
            hudSo.FindProperty("_turnButton").objectReferenceValue = turnButton;
            hudSo.FindProperty("_turnButtonLabel").objectReferenceValue = turnLabel;
            hudSo.FindProperty("_resetButton").objectReferenceValue = resetButton;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            so.FindProperty("_hud").objectReferenceValue = hud;
```

선택 컨트롤러 배선(172줄 부근)에 캐처 둘을 더한다:

```csharp
            selectionSo.FindProperty("_emptyClickCatcher").objectReferenceValue = emptyClickCatcher;
            selectionSo.FindProperty("_dimClickCatcher").objectReferenceValue = dimClickCatcher;
```

컨트롤러 배선에서 HUD 위젯 다섯 줄과 캐처 두 줄을 지운다.

- [ ] **Step 5: 검증하고 커밋한다**

Run: Unity 배치 EditMode
Expected: `failed="0"`

```bash
git status --short
git add -A Assets
git commit -m "refactor: HUD를 BattleHudView로 뽑고 클릭 캐처를 선택 컨트롤러로 옮긴다

턴 라벨과 버튼 활성화가 한 상태에서 나오므로 함께 둔다. 클릭 캐처 둘은
HUD가 아니라 선택 취소 UX이므로 선택을 소유한 컴포넌트가 갖는다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: 배선 누락 가드를 넣고 씬을 재생성한다

**Files:**
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Scenes/FateWeaverBattle.unity` (빌더가 재생성)

**Interfaces:**
- Consumes: Task 1~4의 컴포넌트 넷

설계 §6대로 협력자 참조가 비면 조용한 `NullReference`가 아니라 같은 실패 경로로 보고한다.

- [ ] **Step 1: 가드를 넣는다**

`StartSession`의 기존 프리팹 검사 자리를 바꾼다:

```csharp
            if (_presenter == null || _units == null || !_units.IsBound
                || _piles == null || !_piles.IsBound
                || _hud == null || !_hud.IsBound
                || _hand == null || _rail == null || _selection == null)
            {
                Debug.LogError("전투 화면 컴포넌트 배선이 비어 있습니다.");
                return;
            }
```

`_hud`가 null이면 `SetMessage`도 못 쓰므로 **`Debug.LogError`만 쓴다.**

- [ ] **Step 2: 씬을 재생성한다**

규칙 17 개정으로 AI가 직접 한다. `-executeMethod`로 빌더를 부른다:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath $(pwd) \
  -executeMethod FateWeaver.Unity.Editor.BattleSceneBuilder.Build \
  -logFile /private/tmp/decomp-build.log
```

**여기서는 `-quit`를 써도 된다** — `-runTests`가 아니기 때문이다. 규칙의 금지는 둘을 함께 쓰는
경우다.

Expected: `git status`에 `Assets/Scenes/FateWeaverBattle.unity`만 수정으로 나타난다.

- [ ] **Step 3: 배선을 확인한다**

```bash
/usr/bin/grep -c "_presenter:\|_units:\|_piles:\|_hud:" Assets/Scenes/FateWeaverBattle.unity
```
Expected: `4`

없어진 필드가 남아 있지 않은지도 본다:

```bash
/usr/bin/grep -c "_cardArt:\|_unitPrefab:\|_drawPile:\|_energyText:" Assets/Scenes/FateWeaverBattle.unity
```
Expected: `0` — 컨트롤러에서는 사라지고 각 뷰로 옮겨갔으므로 컨트롤러 블록에는 없어야 한다.
(뷰 컴포넌트 블록에는 존재한다. 컨트롤러의 `m_Script` 블록 안쪽만 보고 판단한다.)

- [ ] **Step 4: 검증하고 커밋한다**

Run: Unity 배치 EditMode
Expected: `failed="0"`

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0, Passed: 499`

**폰트 아틀라스 같은 런타임 부산물은 스테이징하지 않는다** (규칙 17).

```bash
git status --short
git add -A Assets
git commit -m "chore: 분해된 컴포넌트로 전투 씬을 재생성한다

BattleSceneBuilder가 컴포넌트 넷을 만들고 배선한다. 협력자 참조가 비면
시작 시점에 콘솔로 보고하고 멈춘다 — 배선 누락이 조용한 NullReference가
되지 않게 한다.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: 문서 색인을 갱신한다

**Files:**
- Modify: `docs/superpowers/README.md`
- Move: `docs/superpowers/plans/2026-08-04-battle-screen-decomposition.md` → `archive/plans/`
- Modify: `docs/superpowers/archive/README.md`

- [ ] **Step 1: 계획을 완료로 표시하고 보관으로 옮긴다**

머리말의 상태를 `완료`로 고치고 `구현 결과` 절을 더한다. **`BattleScreenController`의 실제 최종
줄 수와 참조 수를 측정해 적는다** (설계 §8의 열린 항목).

```bash
wc -l Assets/Unity/BattleScreenController.cs
/usr/bin/grep -c "SerializeField" Assets/Unity/BattleScreenController.cs
```

상대 링크 깊이를 한 단계 늘린다 (`../specs/` → `../../specs/`).

- [ ] **Step 2: README를 갱신한다**

1. `활성 계획과 로드맵` 표에서 이 계획 줄을 지운다
2. `넘어온 부채`의 `BattleScreenController에 책임이 몰려 있다` 항목을 해결로 표시하고, 남은
   후속(입력 분리는 P2 이후)을 적는다
3. `현재 수치`에 Unity EditMode 총계를 갱신한다

- [ ] **Step 3: 최종 검증하고 커밋한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
Expected: `Failed: 0`

Run: Unity 배치 EditMode
Expected: `failed="0"`

```bash
git status --short
git commit -am "docs: 전투 화면 분해 계획을 완료로 보관한다

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## 사용자 확인이 필요한 것

**Play 검증은 사용자 몫이다** (규칙 17). 배치 EditMode는 씬을 로드하지 않으므로 조작 흐름을
검증하지 못한다. 머지 전에 다음을 확인받는다:

| 확인 | 무엇이 깨졌다는 뜻인가 |
|---|---|
| 손패 카드 클릭 → 레일에 배치 | 입력 경로 또는 `BattlePresenter` |
| 개입 카드 → 대상 선택 → 적용 | `CardSelectionController` 배선 |
| 빈 곳 클릭 → 선택 취소 | Task 4의 클릭 캐처 이전 |
| 턴 실행 / 다음 턴 · 초기화 | `BattleHudView.Initialize` |
| 파일 3개 열기 · 개수 표시 | `BattlePilesView.Bind`/`Refresh` |
| 유닛 HP·상태·정렬 | `BattleUnitsView.Refresh` |
| 고블린 카드 아트 | `BattlePresenter`의 아트 경로 |
| 파티원 이름·색 | `OwnerNameOf` 델리게이트와 `OwnerColor` |

## 열린 항목

- **입력 핸들러 분리.** 설계 §4.1대로 P2(코어 이벤트 확충) 이후로 미룬다.
- **아트 이음매.** 설계 §4.5대로 실제 리소스 형태가 확정된 뒤 정한다.
- **`Presentations` 헬퍼의 거처.** 컨트롤러에 남지만 `_presenter.For`를 감싸기만 한다. 파일 뷰가
  유일한 소비자가 되면 그쪽으로 옮길 수 있다 — 구현 중 판단한다.
