# Fate Weaver — 카드 아트 프롬프트 (gpt-image)

> 목표 화풍: **Slay the Spire + Pirates Outlaws**의 밝고 그래픽한 톤.
> Darkest Dungeon식의 어둡고 그릿(grit) 강한 호러 톤은 **지양**한다.
> **카드의 주체는 캐릭터가 아니라 "행위/효과"다.** Slay the Spire 카드처럼,
> 그 카드를 썼을 때 무슨 일이 벌어지는지가 **한눈에** 읽혀야 한다.
> 캐릭터는 손·팔·무기·실루엣 정도로만 (혹은 아예 없이) 등장한다.
> 기존 카드의 양피지 배경 · 붓터치 프레임 · "카드당 강조색 1개" 규칙은 **유지**한다.

---

## 0. 도구 설정 (gpt-image)

- **모델**: `gpt-image-1`
- **size**: `1024x1536` (세로 카드 비율). 정사각이 필요하면 `1024x1024`.
- **quality**: `high`
- gpt-image에는 Midjourney식 `--ar`, `--sref`, seed가 **없다.** 비율은 `size`로,
  화풍 고정은 아래 **STYLE 블록을 매 카드 그대로 반복** + **참조 이미지 첨부**로 한다.

### 톤 통일 워크플로우 (이 순서대로)

1. **마스터 1장 먼저 확정.** `찰나의 베기`를 먼저 뽑아 마음에 드는 1장을 고른다. → 이게 화풍·연출 기준.
2. **참조 이미지 첨부.** 이후 모든 카드 생성 시 입력 이미지로
   ① 1번에서 고른 마스터 카드, ② `SlayTheSpireCard0X.png`(행위 중심 구도 참고), ③ `PiratesOutlawsCharacters.png`(그래픽 톤 참고)를 함께 첨부하고
   "Match the drawing style and the action-focused framing of the attached references" 지시를 같이 준다.
3. **STYLE / AVOID 블록은 글자 하나 안 바꾸고** 매 카드 그대로 붙인다. (행위 중심·배경·프레임·강조색 규칙이 여기 박혀 있음)
4. 카드마다 바뀌는 건 **ACTION**(행위/효과 묘사)과 **ACCENT COLOR**(강조색) 둘뿐이다.

---

## 1. 공통 STYLE 블록 (모든 카드에 그대로)

```
STYLE: 2D digital card-game illustration where THE ACTION ITSELF IS THE SUBJECT — this is NOT
a character portrait. Depict the card's effect as a clear, instantly-readable action shot:
the weapon, the strike, the impact and the energy are the hero of the frame, like a Slay the
Spire card icon. A character may appear only partially (a hand, a forearm, the weapon, or a
faint silhouette) or not at all — never center or feature a full character. The motion and the
meaning of the action must read in a single glance.
Stylized semi-flat painterly look — bold clean confident ink linework, gouache / cel-shaded
coloring with chunky simplified shapes and clear flat color blocks (reduce fine detail and
cross-hatching), warm dramatic rim lighting, strong sense of motion and impact.
BACKGROUND (keep identical on every card in this set): a light cream parchment surface with a
loose, rough dark brush-stroke frame behind the action.
EFFECT RULE (keep identical on every card): use exactly ONE vibrant accent color for the
energy / motion / impact and its glow — never a second accent color.
PALETTE: muted earthy base (sepia, warm grey, slate blue, charcoal) lifted by saturated
highlights in the accent color.
MOOD: bright, punchy and graphic in the spirit of Slay the Spire and Pirates Outlaws (you may
reference the attached Slay the Spire and Pirates Outlaws images for the drawing style and the
action-focused framing); clean and lively — NOT the dark, gritty, grimdark horror tone of
Darkest Dungeon.
FORMAT: vertical portrait card art, no card frame, no UI, no text, no numbers.
```

## 2. 공통 AVOID 블록 (모든 카드에 그대로)

```
AVOID: a posed full-body character portrait, a centered hero character, a calm standing
figure; grimdark, horror, gore, realistic blood, muddy desaturated colors, heavy black
shadows, dense cross-hatching, gritty noise texture, oppressive darkness, photorealism,
realistic grim faces, cluttered background, multiple characters, card frame, UI border, text,
numbers, watermark, signature.
```

---

## 3. 카드별 완성 프롬프트 (각 블록을 통째로 복붙 → 바로 생성)

각 카드는 `STYLE` + `ACTION` + `AVOID`를 한 번에 붙여 넣으면 된다.
바뀌는 부분은 **ACTION**(행위/효과)과 **ACCENT COLOR**뿐.

### 3.1 찰나의 베기 (quick_cut) — *마스터로 먼저 생성*

```
[여기에 STYLE 블록]
ACTION: one lightning-fast single sword slash — a clean sweeping crescent blade arc cutting
diagonally across the frame, the sword caught at the very peak of the swing, crisp speed lines
trailing the edge. Reads instantly as a quick, precise cut.
ACCENT COLOR: cyan.
[여기에 AVOID 블록]
```

### 3.2 베기 (slash, 기본 공격)

```
[여기에 STYLE 블록]
ACTION: one heavy, decisive sideways chop with a broad crescent-bladed battle axe — the axe
head shown clearly in side profile at the peak of a powerful sweeping cut, with one thick
crescent impact trail following the entire cutting edge. Reads instantly as a committed
slashing / cleaving attack, never a thrust or stab.
ACCENT COLOR: bright golden yellow.
[여기에 AVOID 블록]
```

### 3.3 연쇄 베기 (chain_slash)

```
[여기에 STYLE 블록]
ACTION: a flurry of multiple overlapping slash arcs crisscrossing the frame in quick sequence,
the blade leaving several stacked after-trails. Reads instantly as repeated, chained
consecutive cuts.
ACCENT COLOR: warm orange.
[여기에 AVOID 블록]
```

### 3.4 반격 자세 (counter_stance)

```
[여기에 STYLE 블록]
ACTION: a defensive parry — a raised sword/guard catching and shattering an incoming dark
enemy strike at the exact point of contact, a bright burst of sparks radiating from the clash.
Reads instantly as block-then-counter.
ACCENT COLOR: teal.
[여기에 AVOID 블록]
```

### 3.5 손목 베기 (wrist_cut)

```
[여기에 STYLE 블록]
ACTION: a curved dagger slicing in a short, close, precise cut, a thin stylized wound-trail
and a small crown-and-rune sigil spinning off the blade's edge. Reads instantly as a quick
bleeding cut. Keep the cut stylized and graphic — a glowing ribbon-trail, NOT realistic blood.
ACCENT COLOR: crimson red.
[여기에 AVOID 블록]
```

### 3.6 표식 새기기 (mark_target)

```
[여기에 STYLE 블록]
ACTION: a glowing magic sigil / target mark being carved onto an enemy's dark silhouette by an
ink-brush stroke, with a floating rune-arrow pointing straight at the target. Reads instantly
as marking / branding a target.
ACCENT COLOR: violet purple.
[여기에 AVOID 블록]
```

### 3.7 고블린 찌르기 (goblin_jab)

```
[여기에 STYLE 블록]
ACTION: a small green goblin hand snapping a crude chipped bone dagger upward in one short,
treacherous close-range jab. The jagged point and compact upward impact burst dominate the
frame. Reads instantly as a weak but fast, scrappy enemy poke rather than a sweeping cut or
disciplined long-range thrust.
ACCENT COLOR: acid lime green.
[여기에 AVOID 블록]
```

### 3.8 선제 찌르기 (preemptive_thrust)

```
[여기에 STYLE 블록]
ACTION: one disciplined long-spear thrust reaching its target a split second before an
incoming enemy weapon can complete its swing. The spearhead, rigid shaft, compressed straight
speed trail, and first-contact impact dominate the frame. Reads instantly as striking first
through superior reach and timing.
ACCENT COLOR: vivid cobalt blue.
[여기에 AVOID 블록]
```

---

## 4. 새 카드를 추가할 때

위 패턴을 그대로 복제하고 **ACTION**과 **ACCENT COLOR**만 새로 쓴다.
ACTION은 항상 "무엇을 하는 카드인지"를 한 컷으로: 공격은 궤적·임팩트, 방어는 가드·튕겨냄,
버프/디버프는 빛나는 룬·표식·기운으로 표현한다. 강조색은 카드 기능과 묶어 일관되게:
빠른 검격=cyan 계열 / 무거운 기본 베기=golden yellow / 화염·연타=orange / 방어·반격=teal /
출혈·독=crimson / 표식·저주=purple / 고블린의 조악한 공격=acid lime /
선제·장거리 찌르기=cobalt blue.
색을 의미와 묶어두면 6장이 한 세트로 보인다.

## 5. 빠른 체크리스트

- [ ] `size = 1024x1536`, `quality = high`
- [ ] STYLE / AVOID 블록을 **그대로** 붙였는가
- [ ] **주체가 행위/효과인가** (캐릭터 전신 포트레이트가 아닌가)
- [ ] 한눈에 "무슨 카드인지" 읽히는가
- [ ] 강조색은 카드당 **딱 1개**인가
- [ ] 배경 = 크림 양피지 + 거친 붓터치 프레임인가
- [ ] 첨부 참조에 마스터 카드 + StS / Pirates Outlaws 샘플을 같이 넣었는가
