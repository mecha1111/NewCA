# ARCHITECTURE.md — 클래스 설계 및 의존 방향

## 핵심 원칙: 로직-뷰 분리

```
┌─────────────────────────────────────────┐
│  MonoBehaviour 층 (얇게)                  │
│  GameController, UI (나중) — Unity 의존    │
└───────────────┬─────────────────────────┘
                │ 호출만 (로직 없음)
┌───────────────▼─────────────────────────┐
│  순수 C# 로직 층 (POCO) — 테스트 대상      │
│  GameManager, PlayerState, CharacterUnit  │
│  EffectSystem, CharacterEffectSystem      │
└───────────────┬─────────────────────────┘
                │ 읽기만
┌───────────────▼─────────────────────────┐
│  데이터 층 (불변)                          │
│  CardDatabase, CharacterData, SkillData   │
└─────────────────────────────────────────┘
```

**의존 방향은 아래로만.** 데이터 층은 로직을 모르고, 로직 층은 MonoBehaviour를 모른다.
이래야 EditMode 테스트가 씬·GameObject 없이 로직을 직접 검증할 수 있다.

## 층별 상세

### 데이터 층 (`Runtime/Data`)
- `CharacterData`, `SkillData`, `DeckData` — 순수 데이터 홀더 (불변)
- `CardDatabase` — JSON 로드 후 id→데이터 딕셔너리 제공. **정적 or 주입식 싱글턴**.
  - Unity 의존 최소화: 파일 경로만 받고, 파일 읽기는 주입 가능하게 하면 테스트에서
    StreamingAssets 없이도 문자열로 로드 가능 (권장)

### Core (`Runtime/Core`)
- `GameRules` — 상수 (파티 4, 메인덱 24, 최대HP 15 등). RULES.md 수치와 일치시킬 것
- `GamePhase` enum — BanPick/Setup/Mulligan/Draw/Ready/Action/End/GameOver
- `EffectTiming` enum — 캐릭터 효과 타이밍 (레디 전, 액션 전, 배틀 개시, 턴 종료 등)
- `WeaponType` — 필요 시 enum (문자열 비교로 충분하면 생략 가능)

### Battle (`Runtime/Battle`)
순수 로직:
- `CharacterUnit` — 필드 캐릭터 런타임 상태 (HP, 방어도, 버프, 리치보정). RULES R3/R4 반영
- `PlayerState` — 존 관리 (덱/패/트래쉬/코스트/액티브/파티). 드로우, 코스트 지불, 셔플
  - `Draw()` — 덱 0장 시 트래쉬 셔플 재순환 (RULES 3)
  - `RevealTopForChallenge()` — 챌린지 전용. **재순환 없음** (RULES R8)
- `GameManager` — 페이즈 상태머신. 밴픽→…→게임오버. 챌린지 해결. 데미지·타이밍 조율
  - UI/AI 위임: `TargetSelector`, `EffectActivationPolicy` 델리게이트로 주입
  - 이벤트: `OnPhaseChanged`, `OnGameOver` — 뷰가 구독

MonoBehaviour 래퍼 (UI 붙일 때 작성, 지금은 아님):
- `GameController : MonoBehaviour` — GameManager를 생성·구동, 씬 이벤트를 로직에 전달

### Effects (`Runtime/Effects`)
- `EffectSystem` — 스킬 효과. `Register(cardId, handler)`. `DealDamage` 공통 처리
  (리치 검사 R4, 버프 합산, 관통 R7)
- `CharacterEffectSystem` — 캐릭터 효과. `(id, timing) → handler` + 조건부 데미지 보정
  - `FireTiming(game, timing)` — 특정 타이밍에 양 파티 효과 발동
  - `GetDamageBonus(ctx)` — 공격 시 조건부 보너스 (C05 노예 시너지, C15 연타 등)
- `EffectContext` / `CharacterEffectContext` / `DamageModContext` — 효과 실행 컨텍스트

## 델리게이트 주입 패턴 (중요)

`GameManager`는 "누가 타겟을 고르는가", "선택 효과를 발동할까"를 직접 결정하지 않고
델리게이트로 위임한다. 이래야 같은 로직을 AI/사람/테스트가 공유한다.

```csharp
game.TargetSelector = (user, opp, skillId) => /* AI or UI or 테스트 */;
game.EffectActivationPolicy = unit => /* 발동 여부 */;
```

- **테스트**: 결정론적 델리게이트 주입 (항상 특정 대상, 항상 발동) → 재현 가능
- **AI**: `AIController`가 휴리스틱으로 결정
- **UI**: 플레이어 입력 대기 (나중)

## 난수 (결정론성)

`System.Random`을 시드와 함께 주입. 테스트는 고정 시드로 재현.
`UnityEngine.Random` 쓰지 말 것 (전역 상태라 테스트 격리 깨짐).

## 참고: 기존 프로토타입

`docs/PROTOTYPE_REFERENCE/`에 이전 단계에서 만든 순수 C# 구현이 있습니다 (콘솔에서 검증됨).
포팅 시 참고하되, 위 원칙(Newtonsoft, asmdef 분리, 테스트 가능성)에 맞게 다듬을 것.
기존 코드는 JsonUtility 전제라 로더는 Newtonsoft로 다시 쓸 것.
