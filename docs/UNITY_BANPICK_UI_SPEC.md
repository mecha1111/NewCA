# Cross Accel — 밴픽 화면 Unity 구현 명세

`banpick_ui_v2.html` 목업 기반. **유니티에선 씬 2개(BanScene, PickScene)로 분리**해서 구현한다.
좌표 변환 규칙·Canvas 세팅·색상 토큰은 `UNITY_BATTLE_UI_SPEC.md`와 동일하다.

---

## 0. 핵심 규칙 (밴픽 로직)

- 내 캐릭터 풀 10장 + 상대 캐릭터 풀 10장 (각자 별도)
- **밴 = 내가 상대 카드 1장 금지** / 동시에 상대도 내 카드 1장 금지
- **픽 = 내가 내 카드 선택** / 동시에 상대도 자기 카드 선택 (상대 픽은 화면에 뒷면/미표시)
- **진행 순서**: 밴1 → 픽2 → 밴1 → 픽2 (2라운드, 라운드마다 밴 1장 + 픽 2장)
- 최종: 내 파티 4장(P0~P3) 확정 → 배틀로
- 이 순서는 `BanPickState.Sequence` 배열로 관리 (추후 변경 가능)

> **정정 이력 (2026-08-08)**: 초안은 `밴1 → 픽1 → 밴1 → 픽1 → 픽2`였으나 RULES.md 4번
> ("각자의 덱에서 2장을 선택한다" × 2라운드)과 어긋나서 위와 같이 바로잡았다.
> 총합은 둘 다 밴2 + 픽4로 같지만 **묶음이 달라 2번째 밴 시점의 가용 카드가 달라진다** —
> 초안대로면 GameManager가 이미 픽된 카드를 밴 대상으로 받아 `Remove`가 실패하고 밴이
> 조용히 증발한다. RULES.md가 유일 근거이므로(CLAUDE.md) 그쪽에 맞췄다.

---

## 1. 씬 구성

```
BanScene.unity   — 밴 단계 화면 (상대 풀 10 + 내 풀 10 둘 다 앞면)
PickScene.unity  — 픽 단계 화면 (내 풀 10 + 하단 픽 슬롯 4칸)
```

진행 순서상 두 씬을 오간다: Ban→Pick→Ban→Pick→Pick.
**씬 전환 시 상태(밴/픽 목록, 현재 stepIdx)를 유지**해야 한다 → static 클래스나 DontDestroyOnLoad 매니저로 상태 보관.

```
BanPickState (씬 간 유지)
  int   stepIdx
  List  myBans        // 내가 밴한 상대 카드 인덱스
  List  enemyBansMine // 상대가 밴한 내 카드 인덱스
  List  myPicks       // 내가 픽한 내 카드 인덱스
  List  enemyPicks
  seq = [ban1, pick1, ban1, pick1, pick2]
```

## 2. Canvas / 좌표 규칙

`UNITY_BATTLE_UI_SPEC.md`와 동일:
- Screen Space Overlay + CanvasScaler(1920×1080, Match 0.5)
- **Main Camera 반드시 포함** (Orthographic) — 없으면 "No cameras rendering"
- RectTransform: Anchor(0,1) Pivot(0,1), anchoredPosition=(left, -top)

카드 비율 **0.6494** 유지.

---

## 3. BanScene 좌표 (카드 118×182)

| 요소 | left | top | 비고 |
|---|---|---|---|
| 진행바 | 중앙 | 28 | 밴1▶픽1▶밴1▶픽1▶픽2, 현재 강조 |
| 안내문 | 중앙 | 96 | "상대 카드 1장을 밴하세요" |
| "상대 진영(밴 대상)" 라벨 | 600 | 70 | 빨강 |
| **상대 풀 그리드** | 637~ | 100 / 296 | 5열, 2줄. 열 x = 637/769/901/1033/1165 |
| "내 진영(상대가 밴)" 라벨 | 600 | 502 | 청록 |
| **내 풀 그리드** | 637~ | 532 / 728 | 5열, 2줄. 같은 x |
| 밴 확정 버튼 | right 56 | bottom 56 | 빨강 톤, 1장 선택 시 활성 |
| 프리뷰 패널 | 40 | 200 | 280×431, 호버 시 |

- 상대 풀 카드: 클릭 가능(밴 대상), 빨강 테두리
- 내 풀 카드: 클릭 불가, 상대가 밴한 것만 BAN 표시
- 밴된 카드: 회색 + 대각 슬래시 + "BAN"

## 4. PickScene 좌표 (카드 150×231)

| 요소 | left | top | 비고 |
|---|---|---|---|
| 진행바 | 중앙 | 28 | 동일 |
| 안내문 | 중앙 | 96 | "내 카드 N장을 픽하세요 (n/N)" |
| "내 진영(픽)" 라벨 | 635 | 150 | 청록 |
| **내 풀 그리드** | 557~ | 180 / 425 | 5열 2줄. 열 x = 557/721/885/1049/1213 |
| **하단 픽 슬롯 4칸** | 중앙 | bottom 30 | 각 130×200, P0~P3 |
| 픽 확정 버튼 | right 56 | bottom 56 | 청록 톤, N장 선택 시 활성 |
| 프리뷰 패널 | 40 | 200 | 280×431 |

- 밴된 카드(상대가 밴한 내 카드): 회색 처리, 선택 불가
- 픽된 카드: 청록 발광 + "✓ PICK"
- 픽 단계는 매번 count=2 → 2장 선택해야 확정 활성 (RULES.md 4번)
- 픽 슬롯: 빈칸(점선) / 채워지면 초상화+이름

## 5. 카드 프리팹 (밴픽 공용, BattleCard와 유사)

```
BanPickCard
├─ Portrait   (상단 60%)
├─ HpHex      (좌상단 육각)  └ Value/Label
├─ ReachHex   (우상단 육각)  └ Value/Label
├─ NameText   (하단)
├─ TagRow     (종족/무기 태그 2개)
├─ BanOverlay (회색+슬래시+"BAN", 밴 시 활성)
└─ PickMark   ("✓ PICK", 픽 시 활성)
```

크기만 씬별로 다름(밴 118×182 / 픽 150×231) — 같은 프리팹에 스케일 or 별도 프리팹.

## 6. 스크립트

| 스크립트 | 역할 |
|---|---|
| `BanPickState` | 씬 간 상태 유지 (static/DontDestroyOnLoad) |
| `BanSceneController` | 밴 화면: 두 풀 렌더, 상대 카드 밴 처리, 확정 시 PickScene 로드 |
| `PickSceneController` | 픽 화면: 내 풀 렌더, 픽 처리, 슬롯 갱신, 확정 시 다음 단계 |
| `BanPickCardView` | 카드 데이터 바인딩 + 밴/픽/선택 상태 표시 |
| `BanPickFlow` | seq 진행 관리, 다음이 ban이면 BanScene, pick이면 PickScene 로드 |

확정 시 흐름:
```
현재 step 완료 → stepIdx++ → 다음 step 확인
  → ban이면  SceneManager.LoadScene("BanScene")
  → pick이면 SceneManager.LoadScene("PickScene")
  → 끝이면   SceneManager.LoadScene("BattleScene") + 확정된 파티 전달
```

## 7. GameManager 연결 ✅ 구현됨

> **정정 (2026-08-08)**: 초안은 `GameManager.SubmitBan(cardId)` / `SubmitPick(cardId)`가
> "이미 구현된 API"라고 적었으나 **그런 메서드는 없다**. 실제 구조는 아래와 같다.

GameManager의 밴픽 API는 `BanSelector`/`PickSelector` 델리게이트를 주입하고
`RunBanPick()`을 부르면 **내부에서 2라운드를 통째로 도는 동기·블로킹 경로**다:

```csharp
Func<int, IReadOnlyList<string>, string>                     BanSelector;   // (플레이어, 상대 남은 id들) → 밴할 id
Func<int, IReadOnlyList<string>, int, IReadOnlyList<string>> PickSelector;  // (플레이어, 자기 남은 id들, 매수) → 고른 id들
```

UI 클릭은 비동기라 이 델리게이트 안에서 기다릴 수 없다(메인 스레드가 멈춰 화면이 안 그려짐).
그래서 **결과 인계 방식**을 쓴다:

```
UI(BanPickState)가 자기 흐름대로 밴픽 진행
  ├ 내 선택   : BanScene/PickScene 클릭
  └ 상대 선택 : AIController.ChooseBan / ChoosePicks 를 그 자리에서 호출
       (그래야 "상대가 밴한 내 카드"를 즉시 화면에 표시할 수 있음)
  ↓ 확정된 picks[2], leftovers[2]
GameManager.ApplyBanPickResult(picks, leftovers)   ← 이번에 추가한 public API
  ↓ 내부적으로 기존 RunSetup 호출
파티 배치(Position 0~3) + HP표시용 4장 + 메인덱 구성·셔플
```

- 밴픽 계산이 UI 한 곳에서만 일어나므로 두 경로가 어긋날 수 없다.
- AI 대전(IntegrationTests)은 기존 `RunBanPick()` 경로를 그대로 쓴다 — 두 경로가 공존한다.
- 밴픽 완료 후 `BanPickState.Game`이 세팅 완료된 GameManager를 들고 있다 → BattleScene이 이어받으면 된다.

## 8. 데이터 소스

- 캐릭터 풀 10장 = 스타터 덱의 캐릭터 (CharacterCardData.json)
- 지금은 더미(baseChars 10장), 이후 `CardDatabase`에서 실제 스타터 덱 캐릭터 로드
- 내 풀 / 상대 풀 각각 어느 덱에서 오는지는 덱 선택 로직에 따름 (추후)

---

## 9. 구현 순서

1. `BanPickState` (상태 컨테이너)
2. BanScene 생성 (BanPickUIBuilder, 카메라 포함, 좌표표대로)
3. PickScene 생성
4. 씬 전환 흐름 (BanPickFlow) — 더미 데이터로 밴→픽→밴→픽→픽 완주
5. CardDatabase(JSON) 연결
6. GameManager 연결 (SubmitBan/SubmitPick, AI 밴픽)
7. 밴픽 완료 → BattleScene 전달까지 확인

각 단계 후 멈춰서 확인.
