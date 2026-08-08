# Cross Accel (Unity)

속도 기반 4:4 파티 카드게임의 Unity 구현. **Claude Code로 개발**하는 프로젝트입니다.

## 시작하기

1. **Unity Hub에서 6000.4.6f1(6.4.6f1) 설치** — 없으면 가장 가까운 6.4.x
2. Unity Hub → Add → 이 폴더 선택 → 열기
3. 패키지 자동 복원 대기 (Test Framework, Newtonsoft.Json)
4. 첫 작업은 Claude Code에게: **"CLAUDE.md 읽고 docs/TASKS.md의 Phase 0부터 시작해줘"**

## Claude Code로 작업할 때

Claude Code는 루트 `CLAUDE.md`를 먼저 읽습니다. 규칙·설계·데이터·테스트 지침이
`docs/`에 있습니다. 작업 지시는 Phase 단위로:

> "docs/TASKS.md의 Phase 1(데이터 층)을 구현하고 DataLoadTests까지 만들어줘.
>  끝나면 테스트 돌려서 결과 보여줘."

## 문서

| 문서 | 내용 |
|------|------|
| `CLAUDE.md` | Claude Code 작업 지침 (진입점) |
| `docs/RULES.md` | **게임 규칙 확정본** (유일 근거) |
| `docs/DATA_SCHEMA.md` | 카드 데이터 JSON 명세 |
| `docs/ARCHITECTURE.md` | 클래스 설계·의존 방향 |
| `docs/TESTING.md` | 테스트 실행·전략 |
| `docs/TASKS.md` | Phase별 작업 목록 |
| `docs/PROTOTYPE_REFERENCE/` | 검증 완료된 참고 구현 (포팅용) |

## 현재 상태

- ✅ 카드 데이터 JSON 준비됨 (캐릭터 62, 스킬 84, 스타터 덱 2)
- ✅ 게임 규칙 확정 (R1~R8)
- ✅ 프로토타입 콘솔 검증 완료 (참고 코드로 포함)
- ⬜ Unity 프로젝트로 포팅 (Phase 0~5 진행 예정)

## 범위

플레이어 vs AI 싱글플레이, **스타터 덱 2종(Aggro, MidRange_Blood) 카드만**.
전체 62캐릭/84스킬 구현은 범위 밖.

## 기술 스택

Unity 6.4 / C# / Newtonsoft.Json / Unity Test Framework (EditMode) / 로직-뷰 분리(POCO 중심)
