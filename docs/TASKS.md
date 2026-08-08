# TASKS.md — 작업 목록 및 우선순위

현재 단계: **로직 구현 + 카드 데이터/효과 검증** (UI는 나중).
각 작업은 완료 시 EditMode 테스트를 함께 만들어 사용자와 통과 확인.

체크박스를 갱신하며 진행하세요.

---

## Phase 0 — 프로젝트 기동 확인
- [x] Unity 6000.4.6f1로 프로젝트 열림, 컴파일 에러 없음
- [x] manifest.json 패키지 복원됨 (Test Framework, Newtonsoft)
- [x] asmdef 2개(Runtime, Tests.EditMode) 인식됨
- [x] 빈 EditMode 테스트 1개가 CLI에서 통과 (파이프라인 검증)

## Phase 1 — 데이터 층
- [x] `CharacterData`, `SkillData`, `DeckData` 모델 (Newtonsoft 어트리뷰트)
- [x] `CardDatabase` — Newtonsoft로 JSON 로드. 파일읽기 주입 가능하게 (테스트용)
- [x] 무기 타입 파싱 유틸 (콤마 분리 + 공백 정규화)
- [x] `DataLoadTests` — DATA_SCHEMA.md 체크리스트 전부
- [x] 스타터 덱 카운트 불일치를 경고로 리포트 (실패 아님)

**Phase 1 완료 기준**: 62/84 카드 로드 + skill2 관례 파싱 테스트 통과.

## Phase 2 — Core & 런타임 상태
- [x] `GameRules` 상수 (RULES.md 수치와 대조)
- [x] `GamePhase`, `EffectTiming` enum
- [x] `CharacterUnit` — HP/방어도/버프/리치보정. `CanReach`(R4), `TakeDamage`(관통), `PayHp`
- [x] `PlayerState` — 존, `Draw`(재순환 R3), `RevealTopForChallenge`(재순환 없음 R8)
- [x] `RulesTests` — R3/R4, 관통, PayHp, 재순환 O/X

**Phase 2 완료 기준**: RULES.md의 R1~R4 각각 테스트로 검증됨.

## Phase 3 — 게임 로직 (GameManager)
- [x] 페이즈 상태머신 (밴픽~게임오버)
- [x] 드로우/레디/액션/엔드 페이즈 처리
- [x] 속도순 발동 (R1) + 엑셀 우선
- [x] 챌린지 해결 (R5~R8, R10) — `ChallengeTests`
- [x] 엔드 페이즈: 사망→코스트존, 방어도 클리어(R3), 위치 고정(R9), 승패 판정
- [x] 코스트 지불(레스트) + 매 턴 언레스트(R2) — `PlayerState.PayCost`
- [x] `TargetSelector`/`EffectActivationPolicy`/`SkillEffectResolver` 델리게이트 주입
- [x] `BanSelector`/`PickSelector` 델리게이트 주입 (밴픽)

**Phase 3 완료 기준**: 챌린지 4개 시나리오 테스트 통과 + 엔드 페이즈 규칙 검증.

## Phase 4 — 효과 (스타터 덱 카드만)
- [x] `EffectSystem` — 공통 `DealDamage`(리치 R4·버프 합산·관통)
- [x] 스킬 효과 26종: Aggro 10 + MidRange 16 (스타터 덱 전 스킬)
- [x] `CharacterEffectSystem` — `FireTiming` 훅 + `GetDamageBonus` 조건부 보정
- [x] 캐릭터 효과 20종 (양 덱 캐릭터)
- [x] `EffectTests` — 주요 카드 수치·조건 검증
- [x] 미구현 카드 스킵 동작 확인 (경고 로그 후 진행)

**Phase 4 완료 기준**: 스타터 덱 카드가 규칙대로 처리됨을 테스트로 확인.

## Phase 5 — AI & 통합
- [x] `AIController` — 밴픽/코스트/플레이/타겟팅 (프로토타입 참고, 델리게이트 전부 연결)
- [x] `IntegrationTests` — 고정 시드 AI vs AI 완주, 다중 시드(10개) 무크래시
- [x] 무한루프 방지 최대 턴 안전장치 (60턴) — 정상 상황에선 걸리지 않음
- [x] 미구현 카드 스킵돼도 게임 완주 확인 (효과 전무/부분 미구현 양쪽)
- [ ] (선택, 보류) MonoBehaviour 래퍼 `GameController` — 씬에서 한 판 구동. UI 붙일 때 작성

**Phase 5 완료 기준**: 여러 시드에서 예외 없이 게임 종료. ✅ 10개 시드 전부 6~8턴에 GameOver 도달.

> R9를 "앞당김"으로 바로잡기 전(위치 고정)에는 같은 10개 시드가 전부 60턴 안전장치에 걸렸다.
> 앞당김에서는 캐릭터가 죽을수록 거리가 좁혀져 교착이 구조적으로 생기지 않는다. RULES.md R9 이력 참고.

---

## 다음 단계 (Phase 1~5 전부 완료 후)
- UI (Unity 씬/캔버스) — 지금까지는 로직만, 화면은 없음
- MonoBehaviour 래퍼 `GameController`
- 스타터 덱 데이터 미완성분 채우기 (스킬 24장, 아이템 카드)
- 62캐릭/84스킬 전체로 범위 확장 시 `EffectSystem.BloodCardIds`(R13), `WeaponTypeParser` 등 재검토

---

## 참고 자료
- 규칙: `docs/RULES.md` (확정본, 유일 근거)
- 데이터: `docs/DATA_SCHEMA.md`
- 설계: `docs/ARCHITECTURE.md`
- 테스트: `docs/TESTING.md`
- 이전 프로토타입(콘솔 검증 완료): `docs/PROTOTYPE_REFERENCE/` — 포팅 시 참고

## 진행 원칙
- 한 Phase씩. 완료 시 테스트 통과를 사용자와 확인하고 다음으로.
- RULES.md에 없는 규칙이 필요하면 **멈추고 사용자에게 질문**.
- 스타터 덱 범위 밖 카드는 만들지 않는다.
