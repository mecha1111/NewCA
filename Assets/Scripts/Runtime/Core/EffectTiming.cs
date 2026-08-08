namespace CrossAccel.Core
{
    /// <summary>
    /// 캐릭터 효과 발동 타이밍.
    ///
    /// ⚠️ 의도적으로 미완성: CharacterCardData.json의 effectTiming은 자연어 원문이고 실측 결과 21종의
    /// 서로 다른 표기(오타 포함 — "개시"/"게시" 흔들림, "레디 페이즈  전"의 이중 공백 등)와
    /// 리액티브 트리거("적이 공격 시", "아군의 HP가 감소할때", "전직 시" 등 페이즈가 아닌 이벤트)가 섞여 있다.
    /// RULES.md는 이 전체 목록을 확정하지 않았으므로, 지금 임의로 다 매핑하지 않는다.
    ///
    /// 여기 정의한 값은 RULES.md에서 이미 확정된 것만이다:
    /// - ReadyPhaseBefore / ActionPhaseAfter / TurnEnd: RULES.md 5번 턴 진행 페이즈 구조와 직접 대응
    /// - ActionPhaseBefore: RULES.md 11번 "배틀 페이즈 개시 시 = 매 턴 액션 페이즈 시작" 가정과 대응
    ///
    /// Phase 4에서 스타터 덱 20종의 원문을 확인한 결과, 이 4개 값으로 전부 커버됐다.
    /// 원문 문자열 → 이 enum의 실제 매핑표는 CharacterEffectSystem.RegisterStarterDeckCharacters의 주석 참고.
    /// ("카드 스킬 발동 전"은 타이밍 훅이 아니라 데미지 계산 시 보정이라 여기 값이 아니다.)
    /// 스타터 덱 밖 카드를 구현할 때 나머지 원문을 추가한다.
    /// </summary>
    public enum EffectTiming
    {
        /// <summary>아직 매핑되지 않은 원문. Phase 4에서 카드별로 확인 후 채움.</summary>
        Unknown = 0,

        /// <summary>"레디 페이즈 전".</summary>
        ReadyPhaseBefore,

        /// <summary>"액션 페이즈 전" / "배틀 페이즈 개시 시" (RULES.md 11번: 매 턴 액션 페이즈 시작과 동일 — 확정).</summary>
        ActionPhaseBefore,

        /// <summary>"액션 페이즈 종료 후".</summary>
        ActionPhaseAfter,

        /// <summary>"턴 종료 시" / "엔드 페이즈".</summary>
        TurnEnd
    }
}
