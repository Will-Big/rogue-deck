# 에셋 폴더 재정리 구현 계획 (1단계 — 프리팹화 없이 가능한 범위)

> **에이전트 작업자에게:** 필수 서브 스킬 — `superpowers:subagent-driven-development`(권장) 또는
> `superpowers:executing-plans`로 태스크 단위로 실행한다. 단계는 체크박스(`- [ ]`)로 추적한다.

- 작성일: 2026-08-05
- 상태: `archived` — 2026-08-05 완료·머지. 헤드리스 511/511, Unity EditMode 659 total / 652 passed /
  0 failed / 7 skipped (기준선과 동일)
- 근거: [백로그 §7 P1-B](../../plans/2026-07-16-architecture-refactor-backlog.md)의 "2026-08-04
  실측과 착수 결정" 절에 기록된 **승인된 목표 구조**
- 선행: 계획 3d [C# 카드 스펙 제거](2026-08-05-card-spec-removal.md) **완료·머지**

**목표:** `Assets/Unity/`를 유형별 구조로 재배치하고, 고아 자산과 SO 시절의 폴더 이름을 없앤다.
**프리팹화(P1-B)를 기다리지 않고 지금 옮길 수 있는 전부**를 옮긴다.

**접근:** 이 작업의 위험은 코드가 아니라 **참조가 끊기는 것**이다. 참조 경로가 둘이라 각각 다르게
다룬다 — 씬·프리팹·SO는 **GUID**로 가리키므로 `.meta`만 파일과 함께 움직이면 안 깨지고, 에디터
스크립트는 **문자열 경로**로 가리키므로 이동과 같은 커밋에서 상수를 고쳐야 한다. 후자가 실측으로
드러난 10곳이며 이 계획의 핵심 위험이다.

**기술 스택:** Unity 6000.5.2f1, git, C# (에디터 스크립트 상수만 수정)

## 전역 제약

- **규칙 15:** 메인 체크아웃(`/Users/ish/Git/rogue-deck`)의 브랜치를 전환하지 않는다. 전용 워크트리
  `/Users/ish/Git/rogue-deck-asset-folders`(브랜치 `refactor/asset-folders`)에서 작업한다.
- **`.cs`/`.asset`/`.png`는 `.meta` 형제와 반드시 함께 움직인다.** `.meta`를 두고 파일만 옮기면
  GUID가 새로 발급되어 씬·프리팹의 참조가 전부 끊긴다. 항상 `git mv` 두 번을 한 쌍으로 실행한다.
- **폴더에도 `.meta`가 있다.** 폴더를 지우면 그 폴더의 `.meta`도 지운다. 남기면 Unity가 다음 임포트에서
  빈 폴더를 되살린다(2026-08-05 `Resources/Cards/Frame`으로 실증).
- **Unity 에디터를 닫고 작업한다.** 열린 채로 파일을 옮기면 임포터가 중간 상태를 보고 `.meta`를
  재발급할 수 있다. 배치 실행은 파일 이동이 **끝난 뒤에** 한다.
- **규칙 6:** `FateWeaver.Core`는 UnityEngine을 참조하지 않는다. 이 계획은 코어를 건드리지 않는다.
- **asmdef는 옮기지 않는다.** `Assets/Unity/FateWeaver.Unity.asmdef`가 하위 폴더 전체를 덮으므로
  `.cs`를 하위 폴더로 옮겨도 어셈블리가 바뀌지 않는다. `Assets/Unity/Editor/`는 자체 asmdef가 있고
  그대로 둔다.
- **규칙 27:** 커밋 메시지 제목과 본문은 한국어로 쓴다.

## 검증 명령

**헤드리스** (코어 전용이라 이 계획의 영향을 받지 않는다. 회귀가 없음을 확인하는 용도):

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

**Unity EditMode** (에셋을 옮긴 모든 태스크 끝에서 실행. `-quit`를 붙이면 테스트 없이 exit 0이
되므로 절대 붙이지 않는다. **반드시 포그라운드로** 실행한다 — 백그라운드로 띄우면 호출자가
반환될 때 프로세스가 죽는다):

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-asset-folders -runTests -testPlatform EditMode -testResults /private/tmp/asset-folders.xml -logFile /private/tmp/asset-folders.log
```

결과는 XML 루트의 `result=` / `total=` / `passed=` / `failed=` 속성으로 확인한다.

**시작 시점 기준선 (2026-08-05 실측, master `1b292da`):**
헤드리스 **511/511**, Unity EditMode **659 total / 652 passed / 0 failed / 7 skipped**.

**참조 무결성 검사** (모든 태스크 끝에서 실행. 출력이 없어야 한다):

```bash
# .meta 없는 자산과 짝 잃은 .meta
find Assets -type f ! -name '*.meta' | while read f; do [ -f "$f.meta" ] || echo "meta 없음: $f"; done
find Assets -name '*.meta' | while read m; do [ -e "${m%.meta}" ] || echo "고아 meta: $m"; done
```

## 이번에 옮기지 못하는 것과 그 이유

`Unity/Resources/`는 **두 파일 때문에** 남는다. 둘 다 런타임 `Resources.Load`가 문자열 경로로
찾으므로 옮기면 조용히 `null`이 된다(규칙 3 위반이며, P1-B 프리팹화가 해소한다).

| 남는 파일 | 붙잡는 코드 |
|---|---|
| `Unity/Resources/Fonts/KoreanTMP.asset` | `BattleUiKit.cs:19` — `Resources.Load<TMP_FontAsset>("Fonts/KoreanTMP")` |
| `Unity/Resources/Status/icon_lock.png` | `PlaytestCardArt.cs` — `Resources.Load<Sprite>("Status/icon_lock")` |

이 계획이 끝나면 `Unity/Resources/`에는 **이 둘만** 남는다. P1-B가 `BattleUiKit`과
`PlaytestCardArt`를 없애면 폴더 자체가 사라진다.

## 목표 구조

```text
Assets/Unity/
  FateWeaver.Unity.asmdef        (제자리)
  Scripts/
    Battle/    전투 화면의 컨트롤러·프레젠터·뷰 11개
    Cards/     카드 표현 13개
    Content/   콘텐츠 카탈로그·경로 4개
    Text/      한국어 텍스트 1개
  Editor/                        (제자리, 자체 asmdef)
  Prefabs/                       (제자리)
  Art/
    Cards/Enemies/  ← Resources/Cards/goblins
    Enemies/                     (제자리)
  Fonts/                         (제자리, Pretendard ttf + OFL)
  Data/          ← CardSO/ + CharacterSO/
  Input/         ← Resources/UIInputActions.inputactions
  Resources/
    Fonts/KoreanTMP.asset        (P1-B까지 잠김)
    Status/icon_lock.png         (P1-B까지 잠김)
```

## 파일 구조

| 파일 | 이 계획에서의 책임 |
|---|---|
| `Assets/Unity/*.cs` 30개 | `Scripts/{Battle,Cards,Content,Text}/`로 분산 |
| `Assets/Unity/CardSO/`, `CharacterSO/` | `Data/`로 통합, 폴더 `.meta` 삭제 |
| `Assets/Unity/Resources/Cards/goblins/` | `Art/Cards/Enemies/`로 이동 |
| `Assets/Unity/Resources/Cards/Player/` | **삭제** — 7개 PNG 전부 참조처 0 |
| `Assets/Unity/Resources/Cards/Frame/` | **삭제** — 빈 폴더 + 추적된 `.meta` |
| `Assets/Unity/Resources/UIInputActions.inputactions` | `Input/`으로 이동 |
| `Assets/Unity/Editor/BattleSceneBuilder.cs` | 경로 상수 3개 갱신 |
| `Assets/Unity/Editor/KoreanTmpFontCreator.cs` | 경로 상수 1개 갱신 |
| `docs/superpowers/README.md`, 백로그 §7 | 완료 반영 |

## 하드코딩된 에디터 경로 (실측 2026-08-05)

이동과 **같은 커밋에서** 고쳐야 하는 전부다. 놓치면 컴파일은 되고 메뉴 실행 시점에 실패한다.

| 파일:줄 | 상수 | 이 계획에서 |
|---|---|---|
| `BattleSceneBuilder.cs:16` | `ScenePath` | 변화 없음 |
| `BattleSceneBuilder.cs:17` | `UnitPrefabPath` | 변화 없음 |
| `BattleSceneBuilder.cs:18` | `RailCardPrefabPath` | 변화 없음 |
| `BattleSceneBuilder.cs:19` | `TargetingArrowPrefabPath` | 변화 없음 |
| `BattleSceneBuilder.cs:20` | `MemberAPath` | **`CharacterSO` → `Data`** |
| `BattleSceneBuilder.cs:21` | `MemberBPath` | **`CharacterSO` → `Data`** |
| `BattleSceneBuilder.cs:22` | `InputActionsPath` | **`Resources` → `Input`** |
| `BattleSceneBuilder.cs:24` | `CardArtCatalogPath` | **`CardSO` → `Data`** |
| `KoreanTmpFontCreator.cs:23` | `SourceTtfAssetPath` | 변화 없음 (`Fonts/` 제자리) |
| `KoreanTmpFontCreator.cs:24` | `FontFolder` | 변화 없음 (`Resources/Fonts` 잠김) |

---

### Task 1: 스크립트를 역할별 하위 폴더로 옮긴다

30개 `.cs`와 각 `.meta`를 옮긴다. asmdef가 상위에 있어 어셈블리는 바뀌지 않고, 스크립트는 씬·프리팹이
GUID로 참조하므로 `.meta`만 따라가면 배선이 유지된다.

**Files:**
- Move: `Assets/Unity/*.cs` 30개 (+ 각 `.cs.meta`)

**Interfaces:**
- Produces: `Assets/Unity/Scripts/{Battle,Cards,Content,Text}/` 네 폴더

- [ ] **Step 1: 워크트리를 만들고 기준선을 실측한다**

```bash
cd /Users/ish/Git/rogue-deck
git worktree add /Users/ish/Git/rogue-deck-asset-folders -b refactor/asset-folders
cd /Users/ish/Git/rogue-deck-asset-folders
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 511/511. 다르면 계획의 기준선이 낡은 것이므로 실제 수치를 기록하고 진행한다.

- [ ] **Step 2: 이동 전 파일 수를 세어 둔다**

```bash
ls Assets/Unity/*.cs | wc -l
```

예상: 30. 이 수가 Step 4 뒤에 하위 폴더 합계와 같아야 한다.

- [ ] **Step 3: 네 폴더를 만든다**

```bash
mkdir -p Assets/Unity/Scripts/Battle Assets/Unity/Scripts/Cards Assets/Unity/Scripts/Content Assets/Unity/Scripts/Text
```

폴더 `.meta`는 만들지 않는다 — Unity가 다음 임포트에서 생성하고, Task 5의 배치 실행이 그것을
커밋 대상으로 드러낸다.

- [ ] **Step 4: 파일을 옮긴다**

각 파일과 `.meta`를 한 쌍으로 옮긴다. 아래 스크립트가 `.meta`를 자동으로 동반한다:

```bash
mv_pair() {  # $1=파일명(확장자 제외) $2=대상 폴더
  git mv "Assets/Unity/$1.cs" "$2/$1.cs"
  git mv "Assets/Unity/$1.cs.meta" "$2/$1.cs.meta"
}

B=Assets/Unity/Scripts/Battle
for f in BattleScreenController BattlePresenter BattleUnitsView BattlePilesView BattleHudView \
         BattleUiKit UnitView PileView ExecutionRailView TargetingArrowView CardSelectionController; do
  mv_pair "$f" "$B"
done

C=Assets/Unity/Scripts/Cards
for f in CardView CardBackView RailCardView CardStatusIconView CardStatusTooltipView \
         CardStatusIcon CardStatusPresentation CardPresentation DescriptionLineView \
         TargetGlyphView HandFanView HandCardHoverEffect PlacementFlightPath PlaytestCardArt; do
  mv_pair "$f" "$C"
done

N=Assets/Unity/Scripts/Content
for f in CardArtCatalog CardPrefabCatalog CharacterAsset UnityContentRoot; do
  mv_pair "$f" "$N"
done

mv_pair PlaytestKoreanText Assets/Unity/Scripts/Text
```

분류 근거: `Battle/`은 전투 화면을 조립·구동하는 것(컨트롤러·프레젠터·유닛·레일·더미), `Cards/`는
카드 한 장의 표현과 손패 배치, `Content/`는 콘텐츠 자원을 들고 있는 SO와 경로 상수, `Text/`는
한국어 문자열 매핑이다. `PlaytestCardArt`는 카드 상태 아이콘을 푸므로 `Cards/`에 둔다(P1-B가 지운다).

- [ ] **Step 5: 남은 것이 없는지 확인한다**

```bash
ls Assets/Unity/*.cs 2>/dev/null && echo "남았다 — 위 목록에 빠진 파일이 있다" || echo "이동 완료"
find Assets/Unity/Scripts -name '*.cs' | wc -l
```

예상: "이동 완료"와 `30`. 수가 안 맞으면 Step 4의 목록에 빠진 파일이 있으므로 `git status`로 찾아
같은 방식으로 옮긴다.

- [ ] **Step 6: `.meta` 짝을 검사한다**

```bash
find Assets/Unity/Scripts -type f ! -name '*.meta' | while read f; do [ -f "$f.meta" ] || echo "meta 없음: $f"; done
find Assets/Unity/Scripts -name '*.meta' | while read m; do [ -e "${m%.meta}" ] || echo "고아 meta: $m"; done
```

예상: 출력 없음.

- [ ] **Step 7: 헤드리스로 회귀가 없음을 확인한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

예상: 511/511. 헤드리스는 `Assets/Core`만 컴파일하므로 이 이동에 영향받지 않는다 — 숫자가
변했다면 잘못된 파일을 옮긴 것이다.

- [ ] **Step 8: 커밋**

```bash
git add -A Assets/Unity && git commit -m "refactor(ui): Unity 스크립트를 역할별 폴더로 나눈다"
```

---

### Task 2: 데이터 에셋 폴더 이름을 바로잡는다

`CardSO`·`CharacterSO`는 SO 카드 저작 파이프라인 시절의 이름이다. 그 파이프라인은 계획 3b가 지웠고
지금 남은 것은 표현 자원뿐이므로 `Data/`로 합친다.

**Files:**
- Move: `Assets/Unity/CardSO/CardArt.asset` (+ `.meta`) → `Assets/Unity/Data/`
- Move: `Assets/Unity/CharacterSO/member_a.asset`, `member_b.asset` (+ 각 `.meta`) → `Assets/Unity/Data/`
- Delete: `Assets/Unity/CardSO.meta`, `Assets/Unity/CharacterSO.meta`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs:20,21,24`

**Interfaces:**
- Produces: `Assets/Unity/Data/{CardArt,member_a,member_b}.asset`

- [ ] **Step 1: 옮긴다**

```bash
mkdir -p Assets/Unity/Data
git mv Assets/Unity/CardSO/CardArt.asset Assets/Unity/Data/CardArt.asset
git mv Assets/Unity/CardSO/CardArt.asset.meta Assets/Unity/Data/CardArt.asset.meta
git mv Assets/Unity/CharacterSO/member_a.asset Assets/Unity/Data/member_a.asset
git mv Assets/Unity/CharacterSO/member_a.asset.meta Assets/Unity/Data/member_a.asset.meta
git mv Assets/Unity/CharacterSO/member_b.asset Assets/Unity/Data/member_b.asset
git mv Assets/Unity/CharacterSO/member_b.asset.meta Assets/Unity/Data/member_b.asset.meta
git rm Assets/Unity/CardSO.meta Assets/Unity/CharacterSO.meta
rmdir Assets/Unity/CardSO Assets/Unity/CharacterSO 2>/dev/null || true
```

- [ ] **Step 2: 에디터 경로 상수를 고친다**

`Assets/Unity/Editor/BattleSceneBuilder.cs`에서 세 줄을 바꾼다:

```csharp
        private const string MemberAPath = "Assets/Unity/Data/member_a.asset";
        private const string MemberBPath = "Assets/Unity/Data/member_b.asset";
```

```csharp
        private const string CardArtCatalogPath = "Assets/Unity/Data/CardArt.asset";
```

- [ ] **Step 3: 낡은 경로가 남지 않았는지 확인한다**

```bash
/usr/bin/grep -rn "CardSO\|CharacterSO" --include='*.cs' Assets
```

예상: 출력 없음. 남으면 그 줄도 `Data/`로 고친다.

- [ ] **Step 4: `.meta` 짝을 검사한다**

```bash
find Assets/Unity/Data -type f ! -name '*.meta' | while read f; do [ -f "$f.meta" ] || echo "meta 없음: $f"; done
find Assets -name '*.meta' | while read m; do [ -e "${m%.meta}" ] || echo "고아 meta: $m"; done
```

예상: 출력 없음.

- [ ] **Step 5: 커밋**

```bash
git add -A Assets/Unity && git commit -m "refactor(ui): SO 시절 폴더 이름을 Data로 합친다"
```

---

### Task 3: 아트를 Resources 밖으로 꺼내고 고아 자산을 지운다

`Cards/goblins/`는 `CardArt.asset`이 GUID로 참조하므로 지금 옮길 수 있다. `Cards/Player/`는
플레이어 카드가 색상 틴트만 쓰기로 한 뒤 남은 잔재로, 참조처가 하나도 없다.

**Files:**
- Move: `Assets/Unity/Resources/Cards/goblins/*.png` 3개 (+ 각 `.meta`) → `Assets/Unity/Art/Cards/Enemies/`
- Delete: `Assets/Unity/Resources/Cards/Player/` 7개 PNG (+ 각 `.meta`) + 폴더 `.meta`
- Delete: `Assets/Unity/Resources/Cards/Frame.meta` (폴더가 비어 있다)
- Delete: `Assets/Unity/Resources/Cards.meta`, `Cards/goblins.meta`

- [ ] **Step 1: 삭제 전에 참조가 정말 없는지 재확인한다**

```bash
cd /Users/ish/Git/rogue-deck-asset-folders
for f in Assets/Unity/Resources/Cards/Player/*.png; do
  G=$(/usr/bin/grep -h "guid" "$f.meta" | sed 's/.*guid: \([a-f0-9]*\).*/\1/')
  echo "$(basename $f): $(/usr/bin/grep -rl "$G" --include='*.asset' --include='*.prefab' --include='*.unity' Assets | tr '\n' ' ')"
done
```

예상: 7줄 전부 파일명 뒤가 비어 있다(참조 없음). **하나라도 참조가 나오면 그 파일은 지우지 말고
보고한다** — 2026-08-05 실측에서는 전부 참조 0이었다.

- [ ] **Step 2: 고블린 아트를 옮긴다**

```bash
mkdir -p Assets/Unity/Art/Cards/Enemies
for f in goblin_jab goblin_sly_jab goblin_crude_guard; do
  git mv "Assets/Unity/Resources/Cards/goblins/$f.png" "Assets/Unity/Art/Cards/Enemies/$f.png"
  git mv "Assets/Unity/Resources/Cards/goblins/$f.png.meta" "Assets/Unity/Art/Cards/Enemies/$f.png.meta"
done
```

- [ ] **Step 3: 고아 자산과 빈 폴더를 지운다**

```bash
git rm -r Assets/Unity/Resources/Cards/Player
git rm Assets/Unity/Resources/Cards/Player.meta
git rm Assets/Unity/Resources/Cards/Frame.meta
git rm Assets/Unity/Resources/Cards/goblins.meta
git rm Assets/Unity/Resources/Cards.meta
rmdir Assets/Unity/Resources/Cards/Frame Assets/Unity/Resources/Cards/goblins Assets/Unity/Resources/Cards 2>/dev/null || true
```

- [ ] **Step 4: `CardArt.asset`의 참조가 살아 있는지 확인한다**

GUID 참조이므로 이동해도 유지되어야 한다. 고블린 아트 GUID 셋이 여전히 `CardArt.asset`에 있는지 본다:

```bash
for f in Assets/Unity/Art/Cards/Enemies/*.png; do
  G=$(/usr/bin/grep -h "guid" "$f.meta" | sed 's/.*guid: \([a-f0-9]*\).*/\1/')
  /usr/bin/grep -q "$G" Assets/Unity/Data/CardArt.asset && echo "  ✓ $(basename $f)" || echo "  ✗ $(basename $f) 참조 끊김"
done
```

예상: 세 줄 모두 `✓`. 하나라도 `✗`면 `.meta`가 따라오지 않은 것이므로 되돌리고 다시 옮긴다.

- [ ] **Step 5: Resources에 남은 것을 확인한다**

```bash
find Assets/Unity/Resources -type f | sort
```

예상: `Fonts/KoreanTMP.asset`, `Status/icon_lock.png`와 각 `.meta`, 그리고 두 폴더의 `.meta`뿐이다.

- [ ] **Step 6: 커밋**

```bash
git add -A Assets/Unity && git commit -m "refactor(ui): 카드 아트를 Resources 밖으로 옮기고 고아 자산을 지운다"
```

---

### Task 4: 입력 에셋을 옮기고 에디터 경로를 갱신한다

`UIInputActions.inputactions`는 씬이 GUID로 참조하므로 이동은 안전하다. 붙잡고 있는 것은
`BattleSceneBuilder`의 문자열 상수 하나뿐이다.

**Files:**
- Move: `Assets/Unity/Resources/UIInputActions.inputactions` (+ `.meta`) → `Assets/Unity/Input/`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs:22`

- [ ] **Step 1: 옮긴다**

```bash
mkdir -p Assets/Unity/Input
git mv Assets/Unity/Resources/UIInputActions.inputactions Assets/Unity/Input/UIInputActions.inputactions
git mv Assets/Unity/Resources/UIInputActions.inputactions.meta Assets/Unity/Input/UIInputActions.inputactions.meta
```

- [ ] **Step 2: 에디터 경로 상수를 고친다**

`Assets/Unity/Editor/BattleSceneBuilder.cs`:

```csharp
        private const string InputActionsPath = "Assets/Unity/Input/UIInputActions.inputactions";
```

- [ ] **Step 3: 씬의 참조가 살아 있는지 확인한다**

```bash
G=$(/usr/bin/grep -h "guid" Assets/Unity/Input/UIInputActions.inputactions.meta | sed 's/.*guid: \([a-f0-9]*\).*/\1/')
/usr/bin/grep -q "$G" Assets/Scenes/FateWeaverBattle.unity && echo "  ✓ 씬 참조 유지" || echo "  ✗ 끊김"
```

예상: `✓`.

- [ ] **Step 4: 낡은 Resources 경로가 코드에 남지 않았는지 확인한다**

```bash
/usr/bin/grep -rn "Resources/UIInputActions\|Resources/Cards" --include='*.cs' Assets
```

예상: 출력 없음.

- [ ] **Step 5: 커밋**

```bash
git add -A Assets/Unity && git commit -m "refactor(ui): 입력 에셋을 Input 폴더로 옮긴다"
```

---

### Task 5: Unity로 검증하고 문서를 갱신한다

여기서 처음 Unity를 돌린다. 지금까지의 이동이 임포트를 깨지 않았는지, 폴더 `.meta`가 생성되었는지
확인하고 커밋에 포함한다.

**Files:**
- Add: Unity가 생성한 폴더 `.meta` (Scripts/·Battle/·Cards/·Content/·Text/·Data/·Input/·Art/Cards/·Art/Cards/Enemies/)
- Modify: `docs/superpowers/README.md`, `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md`
- Move: 이 문서를 `docs/superpowers/archive/plans/`로

- [ ] **Step 1: Unity EditMode를 포그라운드로 돌린다**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck-asset-folders -runTests -testPlatform EditMode -testResults /private/tmp/asset-folders.xml -logFile /private/tmp/asset-folders.log
```

```bash
/usr/bin/grep -o 'result="[^"]*" total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*" skipped="[0-9]*"' /private/tmp/asset-folders.xml | head -1
```

예상: `failed="0"`, 총계는 기준선 **659 total / 652 passed / 7 skipped** 그대로. 이 계획은 테스트를
추가·삭제하지 않으므로 **총계가 변하면 무언가 컴파일에서 빠진 것이다** — 그 경우 로그에서 원인을
찾아 보고한다.

- [ ] **Step 2: 임포트 경고를 확인한다**

```bash
/usr/bin/grep -E "error CS|can't be found|Missing|orphan" /private/tmp/asset-folders.log | sort -u | head -10
```

예상: 출력 없음. `A meta data file (.meta) exists but its folder ... can't be found`가 나오면 폴더
`.meta`를 지우지 않고 남긴 것이므로 해당 `.meta`를 `git rm`한다.

- [ ] **Step 3: 생성된 폴더 `.meta`를 커밋에 넣는다**

```bash
git status --short
```

새 폴더의 `.meta`만 나와야 한다. 폰트 아틀라스(`KoreanTMP.asset`) 같은 런타임 부산물이 섞였으면
`git checkout --` 으로 되돌린다 — 그것은 소스 변경이 아니다(규칙 17).

- [ ] **Step 4: 참조 무결성을 마지막으로 검사한다**

```bash
find Assets -type f ! -name '*.meta' | while read f; do [ -f "$f.meta" ] || echo "meta 없음: $f"; done
find Assets -name '*.meta' | while read m; do [ -e "${m%.meta}" ] || echo "고아 meta: $m"; done
```

예상: 출력 없음.

- [ ] **Step 5: 씬 빌더가 여전히 도는지 확인한다**

경로 상수를 넷 고쳤으므로 씬 빌더 메뉴가 자산을 찾는지 확인한다. 배치로 실행할 수 없으면
로그에 경고가 없는 것으로 갈음하고, **사용자에게 `Fate Weaver/Build Battle Scene` 메뉴를 한 번
실행해 달라고 보고에 적는다** — 눈으로 판단할 것은 사용자 몫이다(규칙 17).

- [ ] **Step 6: 문서를 갱신한다 (규칙 20)**

`docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md` §7의 "2026-08-04 실측과 착수
결정" 절에서 목표 구조 블록 아래에 결과를 적는다 — 1단계가 끝났고, `Unity/Resources/`에 두 파일만
남았으며, P1-B가 `BattleUiKit`·`PlaytestCardArt`를 없애면 폴더가 사라진다는 것.

`docs/superpowers/README.md`의 "활성 계획과 로드맵" 표에서 이 계획 행을 지운다.

이 문서를 `docs/superpowers/archive/plans/`로 옮기고 머리말 상태를 `archived`로, 완료일과 실측
수치를 적는다. `docs/superpowers/archive/README.md`에 한 줄 추가한다. **문서를 옮기면 그 안의
`../` 상대 경로 깊이가 달라지므로** 모든 링크가 해결되는지 확인한다.

- [ ] **Step 7: 커밋**

```bash
git add -A && git commit -m "refactor(ui): 에셋 폴더 재정리 1단계를 마치고 문서를 갱신한다"
```

---

## 완료 기준

1. `Assets/Unity/`에 `.cs` 파일이 직접 놓여 있지 않다 — 전부 `Scripts/` 아래 또는 `Editor/`에 있다.
2. `Assets/Unity/CardSO/`·`CharacterSO/`가 존재하지 않고 `Data/`에 세 에셋이 있다.
3. `Assets/Unity/Resources/`에 `Fonts/KoreanTMP.asset`과 `Status/icon_lock.png`만 남는다.
4. `.meta` 짝 검사가 양방향 모두 출력 없음.
5. `BattleSceneBuilder`·`KoreanTmpFontCreator`의 경로 상수가 전부 실재하는 파일을 가리킨다.
6. 헤드리스 511/511, Unity EditMode failed=0이고 총계가 기준선과 같다.

## 이 계획이 열어주는 것

- **P1-B 프리팹화**가 끝나면 `Unity/Resources/`가 통째로 사라진다. 남은 두 파일은
  `Fonts/KoreanTMP.asset` → `Unity/Fonts/`, `Status/icon_lock.png` → `Unity/Art/Icons/`로 가는
  것이 자연스럽다.
- 새 스크립트를 어디 둘지가 폴더 이름으로 답해진다 — 지금은 30개가 한 폴더에 섞여 있어 기준이 없다.

## 범위 밖

- **`BattleUiKit`·`PlaytestCardArt` 제거와 프리팹화.** 백로그 §7 P1-B의 몫이며, 레이아웃을 눈으로
  맞추는 저작이 딸려 온다.
- **`Assets/Core`의 구조.** 이미 도메인별로 나뉘어 있고 이 계획의 대상이 아니다.
- **`Assets/Scenes`·`Assets/Settings`·`Assets/Plugins`.** Unity 관례대로 최상위에 두는 것이 맞다.
- **asmdef 재배치.** 어셈블리 경계는 지금 구조가 옳다.
