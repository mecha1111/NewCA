# 프로토타입 참고 코드

이 폴더의 `.cs.txt` 파일들은 **이전 단계에서 만들어 콘솔로 검증 완료된** 순수 C# 구현입니다.
(확장자를 .txt로 둔 이유: Unity가 이걸 프로젝트 코드로 컴파일하지 않게 하려고.)

## 검증 상태
- 10개 시드에서 AI vs AI 게임이 예외 없이 종료됨을 콘솔에서 확인
- RULES.md의 확정 규칙(R1~R8) 전부 반영된 상태

## 포팅 시 주의
1. **로더는 다시 쓸 것** — 이 코드는 `JsonUtility` 전제. 실제 프로젝트는 **Newtonsoft** 사용.
   `CardDatabase.cs.txt`의 로드 부분은 참고만 하고 Newtonsoft로 교체.
2. **asmdef 네임스페이스/폴더 구조**에 맞게 재배치 (Runtime/Data, Core, Battle, Effects).
3. **`GameSimulator.cs.txt`는 MonoBehaviour 시뮬레이터** — 실제 프로젝트에선 EditMode
   IntegrationTests로 대체. 로직 흐름 참고용.
4. `Debug.Log` 다수 — 유지하되, 테스트에서 시끄러우면 로그 레벨 정리 고려.

## 파일별 역할
| 파일 | 역할 | 포팅 위치 |
|------|------|-----------|
| CardModels.cs.txt | 데이터 모델 | Runtime/Data |
| CardDatabase.cs.txt | 로더 (⚠️ Newtonsoft로 교체) | Runtime/Data |
| GameRules.cs.txt | 상수·enum | Runtime/Core |
| CharacterUnit.cs.txt | 캐릭터 런타임 상태 | Runtime/Battle |
| PlayerState.cs.txt | 존 관리 | Runtime/Battle |
| GameManager.cs.txt | 페이즈 상태머신·챌린지 | Runtime/Battle |
| EffectSystem.cs.txt | 스킬 효과 | Runtime/Effects |
| CharacterEffectSystem.cs.txt | 캐릭터 효과 | Runtime/Effects |
| AIController.cs.txt | 휴리스틱 AI | Runtime/Battle |
| GameSimulator.cs.txt | 콘솔 시뮬레이터 | → IntegrationTests로 대체 |

**중요**: 이 코드를 그대로 복붙하지 말고, RULES.md와 ARCHITECTURE.md 원칙에 맞게 다듬으며 포팅할 것.
특히 규칙은 항상 RULES.md가 우선입니다.

## ⚠️ 프로토타입의 알려진 오류 (포팅 시 반드시 수정)

- `GameRules.cs.txt`의 `CharacterDeckSize = 8` → **10이 맞음**. 캐릭터 덱은 10장이고
  밴픽에서 2장이 밴되어 8장이 됨. 프로토타입은 8을 덱 크기로 잘못 잡았으니 포팅 시 10으로 고칠 것.
- `GameManager.cs.txt`의 "8장" 주석도 동일하게 10장 기준으로 수정.
- 밴픽 로직: 10장에서 라운드당 1장씩 총 2장 밴 → 8장. 그중 4장 배치 + 4장 HP표시.
