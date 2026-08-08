# CLAUDE.md — Cross Accel 프로젝트 작업 지침

이 파일은 Claude Code가 이 프로젝트에서 작업할 때 **가장 먼저 읽는 문서**입니다.
작업 전에 이 문서와 `docs/` 폴더의 관련 문서를 반드시 확인하세요.

## 프로젝트 한 줄 요약

크로스 엑셀(Cross Accel) — 캐릭터덱·메인덱을 구성해 밴픽 후 4:4 파티 배틀을 벌이는
속도 기반 카드게임의 Unity 구현. 현재 목표는 **플레이어 vs AI 싱글플레이**, 범위는
**스타터 덱 2종(Aggro, MidRange_Blood)에 포함된 카드만**.

## 환경

- **Unity 6000.4.6f1** (6.4.6f1) — `ProjectSettings/ProjectVersion.txt`에 고정
- 렌더 파이프라인: 기본(Built-in) — UI 위주라 URP 불필요
- JSON: **Newtonsoft.Json** 사용 (`com.unity.nuget.newtonsoft-json`). `JsonUtility` 아님 —
  `skill2` 부재 판별 등에서 JsonUtility가 부정확하기 때문
- 테스트: **Unity Test Framework (EditMode)**

## 아키텍처 원칙 (반드시 지킬 것)

1. **로직-뷰 분리**. 게임 규칙 계산은 순수 C# 클래스(POCO)에 두고, MonoBehaviour는
   그것을 감싸는 얇은 층으로만 쓴다. 이유: EditMode 테스트가 씬 없이 로직을 검증할 수 있어야 함.
   - 순수 로직: `Runtime/Battle`, `Runtime/Effects`, `Runtime/Core`, `Runtime/Data`
   - MonoBehaviour 진입점: `Runtime/Battle/GameController.cs` 같은 얇은 래퍼 (UI 연결 시 작성)
2. **효과는 파싱하지 않는다**. 카드 효과 텍스트는 자연어라 파싱 불가. 카드 ID → C# 핸들러를
   `Runtime/Effects`에 등록하는 방식. 미구현 카드는 경고 로그 후 스킵(게임 안 멈춤).
3. **원본 데이터 불변**. `CharacterData`/`SkillData`는 읽기 전용. 변하는 값(HP, 방어도, 버프)은
   런타임 상태 클래스(`CharacterUnit`, `PlayerState`)에.
4. **규칙은 `docs/RULES.md`가 유일한 근거**. 규칙을 코드에 하드코딩하기 전에 RULES.md를 확인하고,
   RULES.md에 없으면 임의로 정하지 말고 사용자에게 물어본다.

## 폴더 구조

```
Assets/
├── StreamingAssets/DataJsons/       # 카드 데이터 JSON (아래 DATA_SCHEMA.md 참고)
│   ├── CharacterCardData.json  # 캐릭터 62종
│   ├── SkillData.json          # 스킬 84종
│   └── StarterDecks.json       # Aggro / MidRange_Blood
└── Scripts/
    ├── Runtime/                # 게임 본체 (asmdef: CrossAccel.Runtime)
    │   ├── Data/               # 데이터 모델 + 로더 (순수 C#)
    │   ├── Core/               # 상수, enum (GameRules, GamePhase 등)
    │   ├── Battle/             # 게임 로직 (순수 C#) + MonoBehaviour 래퍼
    │   └── Effects/            # 카드 효과 핸들러 등록
    ├── Editor/                 # 에디터 전용 도구 (데이터 검증 등)
    └── Tests/EditMode/         # EditMode 테스트 (asmdef: CrossAccel.Tests.EditMode)
docs/
├── RULES.md                    # ✅ 게임 규칙 (확정 규칙의 유일 근거)
├── DATA_SCHEMA.md              # JSON 데이터 구조 명세
├── ARCHITECTURE.md             # 클래스 설계·의존 방향
├── TESTING.md                  # 테스트 실행 방법·전략
└── TASKS.md                    # 현재 작업 목록·우선순위
```

## 작업 순서 (현재 단계)

지금은 **로직 구현 + 카드 데이터/효과 검증** 단계입니다. UI는 나중입니다.

1. `docs/TASKS.md`를 열어 현재 우선순위 작업을 확인
2. 데이터 모델·로더부터 (Newtonsoft로 JSON 로드)
3. 게임 로직 포팅 (순수 C#, 테스트 가능하게)
4. 효과 핸들러 (스타터 덱 카드만)
5. **각 단계마다 EditMode 테스트를 작성/실행해서 사용자와 함께 통과 확인**

## 테스트 실행 (CLI, 헤드리스)

```bash
# 프로젝트 루트에서. <UNITY>는 유니티 에디터 실행파일 경로.
<UNITY> -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testResults ./TestResults.xml \
  -logFile ./unity_test.log
# 종료 코드 0 = 전부 통과. TestResults.xml에 상세 결과.
```

자세한 건 `docs/TESTING.md` 참고.

## 하지 말 것

- `docs/RULES.md`에 없는 규칙을 임의로 코드에 넣지 말 것 → 사용자에게 질문
- 스타터 덱 범위 밖 카드(62캐릭/84스킬 전체)를 미리 구현하지 말 것
- MonoBehaviour에 게임 로직을 직접 넣지 말 것 (테스트 불가해짐)
- `JsonUtility` 쓰지 말 것 (Newtonsoft 사용)
- 한 번에 대량 파일을 만들지 말고, 단계별로 만들어 사용자가 확인할 수 있게 할 것
