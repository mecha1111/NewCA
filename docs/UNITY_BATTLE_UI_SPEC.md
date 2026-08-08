# Cross Accel — 배틀 화면 Unity 구현 명세

`battle_ui_v32.html` 목업을 uGUI로 옮기기 위한 좌표·구조 명세.
**HTML을 그대로 쓰는 게 아니라 이 표를 보고 Unity UI를 새로 조립한다.**

---

## 1. Canvas 세팅

```
Canvas
  Render Mode      : Screen Space - Overlay
  Canvas Scaler
    UI Scale Mode  : Scale With Screen Size
    Reference Res  : 1920 x 1080
    Screen Match   : Match Width Or Height = 0.5
  Graphic Raycaster: 기본
```

목업의 `fit()` 자동 축소와 동일하게 동작한다.

## 2. 좌표 변환 규칙 (중요)

HTML은 좌상단 원점, Unity UI는 앵커 기준이다. **모든 요소를 아래로 통일**하면 표의 값을 그대로 쓸 수 있다.

```
RectTransform
  Anchor Min = (0, 1)      // 좌상단
  Anchor Max = (0, 1)
  Pivot      = (0, 1)
  anchoredPosition = ( left,  -top )     // HTML의 left/top 그대로, top만 음수
  sizeDelta        = ( width, height )
```

예) `.m-trash { top:735; right:24; w140 h95 }` → left = 1920-24-140 = **1756**
→ `anchoredPosition = (1756, -735)`, `sizeDelta = (140, 95)`

`right`/`bottom`으로 잡힌 것은 위 방식으로 left/top으로 환산해서 넣는다.

---

## 3. 요소 좌표표 (1920×1080 기준)

| 요소 | left | top | width | height | 비고 |
|---|---|---|---|---|---|
| **상대 매트** | 946 | 2 | 580 | 430 | 빨강 그라데이션, 위쪽 테두리 |
| **내 매트** | 394 | 444 | 580 | 430 | 청록 그라데이션, 아래쪽 테두리 |
| **중앙선** | 394 | 437 | 1132 | 2 | 가로 그라데이션 |
| **상대 덱** | -30 | -175 | 250 | 385 | **Z회전 162°** |
| **내 덱** | 1700 | 870 | 250 | 385 | **Z회전 -18°** |
| **상대 트래쉬** | 24 | 250 | 140 | 95 | |
| **내 트래쉬** | 1756 | 735 | 140 | 95 | |
| **상대 코스트** | 204 | 250 | 140 | 95 | |
| **내 코스트** | 1576 | 735 | 140 | 95 | |
| **상대 프로필** | 1656 | 20 | 240 | 80 | |
| **내 프로필** | 24 | 980 | 240 | 80 | |
| **Phase 패널** | 1756 | 355 | 140 | 340 | |
| **프리뷰 패널** | 30 | 278 | 340 | 524 | 기본 비활성 |

### 캐릭터 존 / 액트존 (카드 126×194)

| 슬롯 | left | top |
|---|---|---|
| 상대 액트 0~3 | 960 / 1102 / 1244 / 1386 | 16 |
| 상대 캐릭터 0~3 | 960 / 1102 / 1244 / 1386 | 224 |
| 내 캐릭터 0~3 | 408 / 550 / 692 / 834 | 458 |
| 내 액트 0~3 | 408 / 550 / 692 / 834 | 666 |

- 열 간격 142 (카드 126 + 여백 16)
- **접점**: 내 P3(셀리아) 우측끝 960 = 상대 P0(아르케아) 좌측끝 960 → 두 진영이 대각으로 어긋나 맞닿는다. 이 배치는 의도된 것이므로 임의로 정렬하지 말 것.

### 손패 (카드 132×203, 최대 8장)

컨테이너: `top 876`, 가로 중앙 정렬. 카드 좌우 마진 -6px.

부채꼴 값 (i = 0~7):

| i | Z회전 | Y오프셋 |
|---|---|---|
| 0 | -12.0° | 0 |
| 1 | -8.6° | -9 |
| 2 | -5.1° | -15 |
| 3 | -1.7° | -18 |
| 4 | +1.7° | -18 |
| 5 | +5.1° | -15 |
| 6 | +8.6° | -9 |
| 7 | +12.0° | 0 |

일반식: `t = (i - (n-1)/2) / ((n-1)/2)`, `rot = t * 12`, `lift = -(1 - t²) * 18`
**Pivot = (0.5, 0)** (카드 하단 중심에서 회전)

---

## 4. 프리팹 구조

### CharacterCard (126×194)
```
CharacterCard (Image: 카드 배경)
├─ Portrait      (Image, 상단 56%)
├─ HpHex         (Image 육각, left 5, top 6, 37×43)  └ Value(TMP) / Label(TMP)
├─ ReachHex      (Image 육각, right 5, top 6, 37×43) └ Value(TMP) / Label(TMP)
├─ PosTag        (TMP, 상단 중앙, "P0")
├─ NameText      (TMP, bottom 28)
└─ WeaponText    (TMP, bottom 9)
```

### ActSlot (126×194)
```
ActSlot
├─ EmptyState  (점선 테두리 Image + "스킬 대기" TMP)   ← 비었을 때
└─ FilledState (카드 배경)                              ← 스킬 놓였을 때
   ├─ CostCircle (30×30, 좌상단)
   ├─ SpeedCircle(30×30, 우상단)
   ├─ ArtIcon    (top 56)
   ├─ NameText   (bottom 30)
   └─ AccelBadge (상단 중앙, 엑셀일 때만 활성)
```

### SkillCard (132×203) — 손패용
```
SkillCard
├─ ArtFrame    (상단 46%)
├─ CostCircle  (35×35, 좌상단)
├─ SpeedCircle (35×35, 우상단)
├─ NameText    (bottom 50)
└─ EffectText  (bottom 8, 배경 있는 박스)
```

상대 손패/액트존은 **뒷면 프리팹**(사선 패턴 + `?`)으로 교체.

---

## 5. 필요한 스크립트

| 스크립트 | 역할 |
|---|---|
| `BattleUIController` | GameManager 상태 → UI 갱신 총괄 |
| `HandFanLayout` | 손패 부채꼴 배치 (위 일반식). 카드 수 변할 때 재계산 |
| `CardHoverPreview` | `IPointerEnterHandler/IPointerExitHandler` → 프리뷰 패널 표시/숨김 + 카드 확대 |
| `CharacterCardView` | CharacterUnit 바인딩 (HP/리치/이름/무기/위치) |
| `SkillCardView` | SkillData 바인딩 (코스트/속도/이름/효과) |
| `ActSlotView` | ActiveSlot 바인딩 (빈칸/스킬/엑셀 뱃지) |
| `PhasePanelView` | GamePhase 표시, 다음 버튼 |

호버 확대는 CSS transition 대신 **DOTween 또는 코루틴**으로 처리.

---

## 6. 색상 토큰

```
HP(체력)   #E0512F      리치       #3A90C8
코스트     #F2C934      엑셀/강조  #31D0F0
카드 상단  #28324D      카드 중간  #161D30    카드 하단  #0A0E18
상대 진영  #E0512F      내 진영    #31C5E0
본문 텍스트 #EEF3FA     보조 텍스트 #9AA6B8
패널 배경  rgba(22,28,42,0.85)   테두리 rgba(255,255,255,0.14)
```

## 7. 카드 아트 비율

공식 카드 템플릿 **1000×1540 (w/h = 0.6494)**.
UI의 모든 카드가 이 비율을 따른다: 126×194, 132×203, 250×385, 340×524.
새 카드 UI를 추가할 때도 이 비율을 지킬 것.

---

## 8. 구현 순서 / 진행 상황

1. ✅ Canvas + Canvas Scaler 세팅, 배경
2. ✅ 정적 존 배치 (매트, 중앙선, 덱, 트래쉬, 코스트, 프로필, Phase 패널) — 좌표표 그대로
3. ✅ CharacterCard 8칸 배치
4. ✅ ActSlot 8칸 배치
5. ⬜ **SkillCard + HandFanLayout (손패)** — 미구현. 3~4번 좌표·부채꼴 공식이 아직 씬에 반영 안 됨
6. ⬜ CardHoverPreview — 미구현 (프리뷰 패널 자리만 있고 비활성)
7. 🔶 BattleUIController로 GameManager 연결 — **부분 구현**
   - ✅ 파티 바인딩: 밴픽 결과(`CharacterZone`)를 카드 8칸에 실시간 반영, HP/방어도/리치/Position 갱신
   - ✅ 페이즈 진행: "다음 ▶" 버튼 1회 = 1페이즈 (`Setup→멀리건→Draw→Ready→Action+End→다음 턴`)
   - ✅ `OnPhaseChanged`/`OnGameOver` 구독 → Phase 패널·승패 표시
   - ⬜ 손패에서 직접 카드 선택 → 액트존 배치 (5번 선행 필요). **지금은 양쪽 다 AI 휴리스틱이 카드를 낸다**
   - ⬜ 액트존 `FilledState` 표시(ActSlotView), 데미지 연출

> `RunActionPhase()`가 액션과 엔드를 한 호출에 처리하고 반환하므로, 카드별 단계 연출을 하려면
> GameManager 쪽에 중간 훅(이벤트)이 필요하다. 현재는 호출 후 최종 상태를 읽어 일괄 갱신한다.

각 단계마다 Game 뷰에서 목업 스크린샷과 대조.

## 9. 씬 진입 경로 (BattleSession)

BattleScene은 두 경로 모두를 지원한다:

| 경로 | 동작 |
|---|---|
| 밴픽 경유 (정상) | `BanPickState.Game`(세팅 완료된 GameManager)을 그대로 이어받음 |
| BattleScene 단독 Play | AI끼리 `RunBanPick()`을 돌려 즉시 플레이 가능한 판을 생성 |

두 경로 모두 마지막에 **효과 시스템(`StarterDeckEffects.Install`)과 AI 델리게이트(`AIController.Attach`)를
설치**한다. 밴픽 단계에서는 이것들이 붙어있지 않아, 건너뛰면 액션 페이즈에 효과가 하나도 발동하지 않는다.
