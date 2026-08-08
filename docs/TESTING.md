# TESTING.md — 테스트 실행 및 전략

목표: 카드 데이터가 잘 로드되는지, 효과 처리가 규칙대로 되는지를 **Claude Code 안에서
실행하고 사용자가 결과를 보며** 검증한다.

## 테스트 프레임워크

**Unity Test Framework (EditMode)**. 씬 없이 순수 로직을 검증한다.
로직 층이 POCO라서 GameObject/씬 없이 `[Test]` 메서드로 직접 호출 가능.

## CLI 실행 (헤드리스)

```bash
# <UNITY> = 유니티 에디터 실행파일 경로
#   Windows: "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe"
#   Mac:     "/Applications/Unity/Hub/Editor/6000.4.6f1/Unity.app/Contents/MacOS/Unity"
#   Linux:   "~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity"

<UNITY> -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testResults ./TestResults.xml \
  -logFile ./unity_test.log

echo "종료 코드: $?"   # 0 = 전부 통과, 2 = 실패한 테스트 있음
```

- 결과는 `TestResults.xml` (NUnit XML). 실패 케이스·메시지 포함.
- 로그는 `unity_test.log`.
- **주의**: 같은 프로젝트를 에디터에서 열어둔 채 batchmode 실행하면 라이선스/락 충돌 가능.
  에디터 닫고 실행하거나 `-` 별도 라이선스 사용.

### Claude Code용 결과 요약 팁

```bash
# 통과/실패 개수만 빠르게
grep -oP 'total="\d+" passed="\d+" failed="\d+"' TestResults.xml | head -1
# 실패한 테스트 이름
grep -B1 'result="Failed"' TestResults.xml | grep 'name=' | head
```

## 테스트 전략 (우선순위 순)

### 1단계: 데이터 로드 (가장 먼저)
`Tests/EditMode/DataLoadTests.cs`
- characters 62개, skills 84개 로드 확인
- id 중복 없음
- skill2 인코딩 관례 (int.MinValue = 없음) 정상 파싱
- 무기 복수/공백 흔들림 파싱 (`DATA_SCHEMA.md` 체크리스트 그대로)
- 스타터 덱 카운트는 **경고만** (규칙 불일치가 있으므로 강제 실패 금지)

### 2단계: 규칙 단위 (효과 없이)
`Tests/EditMode/RulesTests.cs`
- **R4 리치**: `CanReach` 가 거리공식(내+상대+1)대로. 경계값(내0→적3=4, 내3→적3=7) 테스트
- **R1 속도**: 속도 높은 카드가 먼저 큐에 오는지
- **R2 코스트**: 턴 시작 언레스트
- **R3 방어도**: 엔드 페이즈 후 방어도 0
- **관통**: 방어도 무시하고 데미지
- 드로우 시 트래쉬 재순환 O / 챌린지 중 재순환 X (R8)

### 3단계: 챌린지
`Tests/EditMode/ChallengeTests.cs`
- 속도 동률 → 챌린지 진입
- 단독 성공자만 발동 / 양측 실패 → 양측 무효 (R6) / 공용 카드 무효 (R7)
- 챌린지 중 덱 소진 → 자동 패배, 재순환 없음 (R8)
- 공개 카드가 트래쉬로 (R5)

### 4단계: 카드 효과 (스타터 덱만)
`Tests/EditMode/EffectTests.cs`
- 스킬: 데미지/회복/방어/연타 등 수치 정확성
- 캐릭터: 타이밍 트리거 발동, 조건부 데미지 보너스
- 미구현 카드는 경고 로그 후 스킵되고 게임이 안 멈추는지

### 5단계: 통합 (한 게임 완주)
`Tests/EditMode/IntegrationTests.cs`
- 고정 시드로 AI vs AI 한 판이 예외 없이 종료되는지
- 여러 시드 루프 돌려 크래시 없음 확인

## 테스트 작성 규칙

- **결정론적으로**. 난수는 고정 시드 주입. `UnityEngine.Random` 금지.
- **씬 의존 금지**. 순수 로직만 테스트 (그래서 로직-뷰 분리가 중요).
- **하나의 규칙 = 하나의 테스트**. 실패 시 어느 규칙이 깨졌는지 바로 보이게.
- 테스트 이름은 `메서드_상황_기대결과` 형태. 예: `CanReach_BackToBack_Returns7`.

## 예시 (참고용 뼈대)

```csharp
using NUnit.Framework;
using CrossAccel.Battle;

namespace CrossAccel.Tests
{
    public class RulesTests
    {
        [Test]
        public void CanReach_FrontToFront_DistanceIsOne()
        {
            var data = /* reach=1 인 더미 캐릭터 */;
            var attacker = new CharacterUnit(data, ownerId: 0, position: 0);
            Assert.IsTrue(attacker.CanReach(0));   // 내0 → 적0 = 거리1, reach1이면 성공
        }

        [Test]
        public void CanReach_BackToBack_NeedsReach7()
        {
            var data6 = /* reach=6 */;  var data7 = /* reach=7 */;
            Assert.IsFalse(new CharacterUnit(data6, 0, 3).CanReach(3)); // 거리7 > reach6
            Assert.IsTrue (new CharacterUnit(data7, 0, 3).CanReach(3)); // 거리7 == reach7
        }
    }
}
```
