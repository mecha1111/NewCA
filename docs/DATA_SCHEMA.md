# DATA_SCHEMA.md — 카드 데이터 JSON 명세

카드 데이터는 `Assets/StreamingAssets/DataJsons/`에 JSON 3종으로 있습니다.
원본 엑셀(스타터_덱.xlsx, CrossAccel_DataTable.xlsx)에서 변환된 것으로, **이 JSON이 런타임의 근거**입니다.

로드는 **Newtonsoft.Json** 사용 (`JsonUtility` 아님).

---

## CharacterCardData.json

```json
{
  "characters": [
    {
      "id": "C01",
      "name": "화가 많은 엘프",
      "race": "엘프",
      "weaponType": "활",
      "maxHp": 3,
      "reach": 6,
      "effectTiming": "레디 페이즈 전",
      "effectText": "[3 코스트] 엑셀, 그 후에 엑셀로 사용된 카드 데미지 +1."
    }
  ]
}
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `id` | string | C01~C62 |
| `name` | string | 카드 이름 |
| `race` | string | 인간/엘프/노예/추격자/정령/뱀파이어/천사/악마/고블린/오크/드워프 등 |
| `weaponType` | string | 한손검/두손검/방패/스태프/활/책/악기 |
| `maxHp` | int | 최대 15 |
| `reach` | int | 최대 7 |
| `effectTiming` | string | 효과 발동 타이밍 (자연어. enum 매핑은 Core에서) |
| `effectText` | string | 효과 원문 (표시용 + 구현 참조) |

- 총 62종. `effectText`가 "없음"이거나 빈 문자열이면 무효과 캐릭터.

## SkillData.json

```json
{
  "skills": [
    {
      "id": "S01",
      "name": "치고 빠자기",
      "weaponType": "공용",
      "speed": 4,
      "skill1Cost": 0,
      "skill1Effect": "데미지 2, 이전에 사용된 스킬이 있으면 실패."
    },
    {
      "id": "S20",
      "name": "잔혹한 운명",
      "weaponType": "공용",
      "speed": 1,
      "skill1Cost": 6,
      "skill1Effect": "(노예 종족 전용) 데미지 8, 자신 사망",
      "skill2Cost": 6,
      "skill2Effect": "(추격자 종족 전용) 데미지 8을 주고 아군 전체에게 2 데미지"
    }
  ]
}
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `id` | string | S01~S84 |
| `name` | string | 카드 이름 |
| `weaponType` | string | "공용" 또는 복수 표기 가능 ("한손검, 활") |
| `speed` | int | **높을수록 먼저 발동** (RULES R1) |
| `skill1Cost` | int | 코스트. **-1 = 데이터 미정** (예: S79) |
| `skill1Effect` | string | 효과 원문 |
| `skill2Cost` | int? (nullable) | 두 번째 스킬 코스트. **필드 자체가 없으면 두 번째 스킬 없음** |
| `skill2Effect` | string | 두 번째 스킬 효과 (선택 발동형 카드). skill2Cost 없으면 이 필드도 없음 |

- 총 84종.
- **무기 타입 파싱 주의**: "한손검, 활"처럼 콤마 구분 복수. "한손 검"처럼 공백 흔들림 있음 → 정규화 필요.
- **skill2 판별 (2026-08-07 실측 확정)**: 원본 84종 중 skill2가 있는 카드는 5개(S20, S52, S61, S64, S84)뿐이고,
  이 5개만 JSON에 `skill2Cost`/`skill2Effect` 키가 존재한다. 나머지 79개는 **키 자체가 JSON에서 생략**되어 있다
  (예전에 문서에 있던 "`skill2Cost == int.MinValue`" sentinel 인코딩은 실측과 다름 — 실제로 그 값이 들어있는
  카드는 0건. 정정함). 로더는 `skill2Cost`를 `int?`로 매핑하고, **필드 존재 여부(= null 아님)** 로 skill2 유무를
  판별할 것.

## StarterDecks.json

```json
{
  "decks": [
    {
      "deckName": "Aggro",
      "cards": [
        { "cardId": "C05", "cardType": "Character", "count": 1 },
        { "cardId": "S01", "cardType": "Skill", "count": 2 },
        { "cardId": "I13", "cardType": "Item", "count": 1 }
      ]
    }
  ]
}
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `deckName` | string | "Aggro" 또는 "MidRange_Blood" |
| `cards[].cardId` | string | C/S/I 접두 ID |
| `cards[].cardType` | string | Character / Skill / Item |
| `cards[].count` | int | 매수 (중복 최대 2) |

- **Character 카드는 캐릭터 덱**(10장, 밴픽으로 2장 밴), **Skill 카드는 메인 덱**(24장)으로 분리해서 사용.
- **Item(I13/I14)은 데이터 미완 → 로드 시 건너뛰거나 경고**. 현재 범위 밖.

---

## 데이터 이슈 (원본 엑셀에서 넘어온 것 — 알고 있을 것)

| 이슈 | 내용 | 대응 |
|------|------|------|
| 아이템 미완 | I13/I14가 스타터 덱엔 있으나 데이터 테이블에 없음 | 범위 밖, 스킵 |
| S79 코스트 | `skill1Cost: -1` (원본 공란) | X코스트로 해석 (RULES 11) |
| S52/S53 불일치 | 스타터덱 "희생의 등불"=S53인데 테이블은 S52 | 임시 양쪽 등록 |
| 헤더 오타 | Character 시트 헤더 `Numㅌ` | 변환 시 이미 처리됨 |

## 검증 체크리스트 (로더 구현 후 테스트로 확인)

- [ ] CharacterCardData.json 62개 전부 로드, id 중복 없음
- [ ] SkillData.json 84개 전부 로드
- [ ] skill2가 있는 카드(예: S20, S52, S61, S64, S84)의 skill2Effect가 비어있지 않음
- [ ] skill2가 없는 카드는 skill2Cost 필드 자체가 없음 (nullable int로 로드 시 null)
- [ ] 무기 복수 표기("한손검, 활") 파싱 시 2개로 분리됨
- [ ] "한손 검" 공백 흔들림이 "한손검"으로 정규화됨
- [ ] starter_decks 2개 로드, 각 덱의 Character/Skill 분리됨
- [ ] ⚠️ **실측 카운트가 규칙과 불일치** — 아래 표 참고. 로더는 이걸 강제하지 말고 그대로 로드할 것

### 스타터 덱 실측 카운트 (검증됨)

| 덱 | Character | Skill | Item | 규칙 |
|----|-----------|-------|------|------|
| Aggro | 10 | 18 | 2 | 캐릭 10 ✅ / 스킬 24 |
| MidRange_Blood | 10 | 20 | 0 | 캐릭 10 ✅ / 스킬 24 |

- **캐릭터 10장 = 규칙과 일치.** 밴픽에서 2장 밴 → 8장(배치4+HP4). 문제없음.
- **스킬 18/20장 = 24장 미달 → 데이터 미완성.** 24장 규칙은 확정. 스타터 덱을 나중에 채워야 함.
- 로더는 데이터를 있는 그대로 로드하고, **스킬 24장 미달은 테스트에서 경고로만** 표시(강제 실패 아님).
- 밴픽 로직은 캐릭터 10장 기준으로 구현.
