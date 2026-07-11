# 기본 플레이어 캐릭터 후보 5종 디자인

## 목표

`art_style_prompts_2026-07-07/style_test_prompts/`의 01~06 이미지를 스타일 레퍼런스로 사용해, 아직 직업·능력·서사가 정해지지 않은 기본 플레이어 캐릭터 외형 후보 5종을 만든다. 각 후보는 이후 공격, 방어, 이동, 개입 등 서로 다른 카드 콘셉트를 자연스럽게 수용해야 한다.

## 결과물

- 3:2 가로형 캐릭터 일러스트 5장
- 저장 폴더: `art_style_prompts_2026-07-07/player_character_candidates/`
- 파일명:
  - `01_female_athletic.png`
  - `02_female_tall.png`
  - `03_female_compact.png`
  - `04_male_lean.png`
  - `05_male_broad.png`
- 최종 프롬프트를 같은 폴더의 `prompts.md`에 기록한다.

## 공통 아트 디렉션

- 모든 인물은 명확한 성인이다.
- 특정 직업, 능력, 진영, 종교, 서사를 암시하지 않는 중립적인 다크 판타지 여행복을 입는다.
- 무기, 방패, 마법 효과, 문장, 계급장, 과도한 장신구는 넣지 않는다.
- 여성 후보는 노출이나 과장된 자세가 아니라 성숙한 체형과 자연스러운 비율에서 이성적인 매력이 느껴지게 한다. 실용적인 의복을 착용한다.
- 인물을 알아보기 쉬운 중립적인 3/4 전신 자세로 보여준다. 손과 팔은 몸의 실루엣에서 분리한다.
- `ArtViewport`의 168×112 비율에 맞는 3:2 가로 구도다. 인물은 중앙에 두되 머리, 손, 발이 잘리지 않게 한다.
- 텍스트, 카드 프레임, UI, 로고, 워터마크를 넣지 않는다.

## 고정 스타일

레퍼런스 01~06에서 다음 요소를 유지한다.

- 외곽선 없는 2D 디지털 일러스트
- 명도 차로 구분되는 넓은 플랫 색면
- 2~3단계의 하드엣지 셀 명암
- 그라데이션과 부드러운 에어브러시 없음
- 단순하고 읽기 쉬운 실루엣, 최소한의 내부 디테일
- 종이를 찢거나 잘라 붙인 듯한 약간 거친 가장자리
- 미세한 종이·그레인 질감
- 어두운 저명도 배경에서 인물이 떠오르는 포스터형 구도
- 작은 밝은 포인트는 후보당 1~2곳만 사용

## 공통 네거티브 제약

`child, teenager, young-looking, weapon, sword, shield, bow, staff, wand, spell effect, magic aura, class emblem, faction insignia, religious symbol, ornate costume, armor suit, excessive accessories, revealing outfit, lingerie, fetishwear, exaggerated breasts, exaggerated hips, pin-up pose, provocative pose, outlines, line art, sketch lines, gradient shading, soft airbrush, smooth blending, rendered lighting, photorealistic, 3D, intricate details, high-detail texture, painterly brushstrokes, cross-hatching, text, card frame, UI, logo, watermark, cropped head, cropped hands, cropped feet`

## 후보별 디자인

### 1. 여성 A — 균형 잡힌 운동형

- 평균보다 약간 큰 키, 긴 팔다리, 탄탄한 어깨와 하체, 자연스러운 허리와 굴곡
- 짧고 약간 헝클어진 머리, 성숙하고 차분한 얼굴
- 짙은 튜닉, 몸에 맞는 바지, 짧은 천 겉옷, 단순한 가죽 벨트와 부츠
- 암적색·짙은 회색, 작은 옅은 금색 버클

핵심 프롬프트: `an adult woman with a balanced athletic build, slightly tall, long limbs, toned shoulders and legs, a naturally defined waist and mature feminine curves, short tousled hair, calm mature face, practical layered travel tunic and fitted trousers with a short cloth overlayer, simple belt and boots, dark crimson and charcoal palette with one pale gold buckle`

### 2. 여성 B — 장신의 유연한 체형

- 큰 키, 긴 목과 팔다리, 가는 허리, 부드러운 곡선과 단단한 하체
- 긴 머리를 낮게 묶고 몇 가닥이 얼굴 옆으로 흐름
- 긴 소매의 단순한 상의, 허벅지 중간까지 오는 비대칭 천 겉옷, 바지와 부츠
- 어두운 청록·먹청색, 작은 창백한 시안 장식

핵심 프롬프트: `a tall adult woman with long elegant proportions, long neck and limbs, slender defined waist, subtle mature curves and strong legs, long hair tied low with a few loose strands, composed mature face, practical long-sleeved travel clothes with an asymmetric thigh-length cloth overlayer, trousers and boots, dark teal and ink-navy palette with one pale cyan clasp`

### 3. 여성 C — 작고 풍만한 체형

- 비교적 작은 키, 안정적인 골반과 허벅지, 선명한 허리, 단단한 팔과 다리
- 턱선 길이의 자연스러운 웨이브 머리, 자신감 있지만 과장되지 않은 표정
- 허리선이 잡힌 겹쳐 입는 튜닉, 편한 바지, 짧은 망토 조각과 부츠
- 모래색·짙은 갈색, 작은 탁한 금색 장식

핵심 프롬프트: `a short adult woman with a compact strong build, broad stable hips, powerful thighs, a clearly defined waist, mature natural curves, sturdy arms and legs, chin-length softly wavy hair, quietly confident mature face, practical belted layered tunic, comfortable trousers, a short cloth shoulder layer and boots, sand and deep brown palette with one muted gold fastening`

### 4. 남성 A — 날렵한 체형

- 중간 키, 좁은 허리, 긴 팔다리, 가볍고 선명한 근육
- 한쪽으로 흐르는 중간 길이 머리, 부드럽고 중성적인 인상
- 단순한 목 높은 상의, 가벼운 겹천, 바지, 손목 감개와 부츠
- 짙은 자주색·암회색, 작은 회백색 버클

핵심 프롬프트: `an adult man of medium height with a lean agile build, narrow waist, long limbs and light defined muscle, medium-length hair swept to one side, calm softly androgynous mature features, simple high-neck travel top with a light layered cloth panel, trousers, plain wrist wraps and boots, deep muted purple and charcoal palette with one pale grey fastening`

### 5. 남성 B — 크고 단단한 체형

- 큰 키, 넓은 어깨, 두꺼운 몸통과 팔, 과장되지 않은 실용적인 근육
- 짧은 머리와 정돈된 가벼운 수염, 안정적인 인상
- 넉넉한 튜닉, 단순한 어깨 천, 튼튼한 바지, 넓은 벨트와 부츠
- 청회색·짙은 남색, 작은 시안색 바느질 포인트

핵심 프롬프트: `a tall adult man with a broad heavy build, wide shoulders, thick torso and arms, believable functional muscle rather than exaggerated bodybuilding, short hair and neatly trimmed light beard, steady mature face, roomy practical travel tunic, simple shoulder cloth, sturdy trousers, broad plain belt and boots, blue-grey and deep navy palette with one small cyan stitch accent`

## 생성 프롬프트 조립 규칙

각 후보의 핵심 프롬프트 뒤에 아래 공통 블록을 붙인다.

`full-body neutral three-quarter standing pose, hands and arms clearly separated from the torso, centered figure with generous horizontal breathing room, entire head hands and feet visible, 3:2 landscape composition designed for a game card art viewport, dark low-key abstract background, flat 2D digital illustration, lineless cut-paper painting style, shapes defined purely by value contrast between broad flat color planes, hard-edged cel shading with only 2-3 value steps, no gradients, no soft shading, no outlines, bold simplified shapes with minimal interior detail, slightly rough torn-paper edges, subtle paper grain texture, one or two small bright accents only, poster-like readable silhouette`

그 뒤에 공통 네거티브 제약을 `Avoid:` 항목으로 넣는다.

## 레퍼런스 이미지 역할

- `01_crimson_knight.png`: 암적색·금속 회색 팔레트와 찢긴 색면
- `02_swamp_witch.png`: 천의 거친 가장자리와 어두운 녹색 분리
- `03_frost_golem.png`: 청회색의 큰 명암 덩어리
- `04_desert_wanderer.png`: 밝은 모래색 의복과 단순한 얼굴 처리
- `05_serpent_priest.png`: 제한된 2색 팔레트와 작은 금색 포인트
- `06_silhouette_card_art.png`: 강한 실루엣과 어둠 속 저명도 구도

이미지는 편집 대상이 아니라 스타일 레퍼런스다. 원본 캐릭터의 갑옷, 지팡이, 가면, 뱀 몸체, 두개골 장식은 새 후보에 복제하지 않는다.

## 검증 기준

1. 5종 모두 명확한 성인이며 성별·체형의 차이가 작은 카드 크기에서도 읽힌다.
2. 여성 후보는 모두 성숙하고 매력적인 비율이지만 노출·과장·선정적 자세에 의존하지 않는다.
3. 특정 무기, 직업, 능력, 진영이 외형에서 결정되지 않는다.
4. 머리, 손, 발이 잘리지 않고 3:2 구도 안에 들어온다.
5. 외곽선 없이 2~3단계 하드엣지 색면과 거친 종이 질감이 유지된다.
6. 각 후보의 팔레트와 실루엣이 서로 분명히 구분된다.
7. 텍스트, 카드 프레임, UI, 로고, 워터마크가 없다.

