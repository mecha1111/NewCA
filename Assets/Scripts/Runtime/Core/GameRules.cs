namespace CrossAccel.Core
{
    /// <summary>
    /// 게임 규칙 상수. 값은 전부 docs/RULES.md 확정본과 대조되어야 한다 (CLAUDE.md 원칙 4).
    /// 임의 추가 금지 — 새 상수가 필요하면 RULES.md에서 근거를 찾고, 없으면 사용자에게 확인.
    /// </summary>
    public static class GameRules
    {
        /// <summary>플레이어 수 (RULES.md 1번: 상대와 4:4 파티 배틀 — 2인).</summary>
        public const int PlayerCount = 2;

        /// <summary>캐릭터 덱 매수 (RULES.md 3번).</summary>
        public const int CharacterDeckSize = 10;

        /// <summary>밴픽 라운드 수 (RULES.md 4번 밴픽: 2라운드 진행).</summary>
        public const int BanPickRounds = 2;

        /// <summary>밴픽 라운드당 밴하는 캐릭터 수 (RULES.md 4번: 1장씩 밴).</summary>
        public const int BansPerRound = 1;

        /// <summary>밴픽 라운드당 각자 자기 덱에서 고르는 캐릭터 수 (RULES.md 4번 밴픽 2단계: 2장 선택).</summary>
        public const int PicksPerRound = 2;

        /// <summary>밴픽 총 밴 수 = 라운드 수 × 라운드당 밴 수 (2). 10장 → 8장.</summary>
        public const int TotalBans = BanPickRounds * BansPerRound;

        /// <summary>밴픽 후 남는 캐릭터 수 (RULES.md 4번: 10 - 2밴 = 8. 배치 4 + HP표시 4).</summary>
        public const int RemainingCharactersAfterBan = CharacterDeckSize - TotalBans;

        /// <summary>캐릭터 존에 배치하는 파티 인원 = 밴픽 후 남은 카드 중 HP표시용을 뺀 수 (RULES.md 4번 세팅).</summary>
        public const int PartySize = 4;

        /// <summary>메인 덱(스킬 카드) 매수 규칙 (RULES.md 3번). ⚠️ 현재 스타터 덱 데이터는 미달 — DATA_SCHEMA.md 참고, 로더는 강제하지 않음.</summary>
        public const int MainDeckSize = 24;

        /// <summary>메인 덱 내 카드 중복 최대 매수 (RULES.md 3번).</summary>
        public const int MaxDuplicateCopies = 2;

        /// <summary>캐릭터 카드 최대 HP (RULES.md 3번).</summary>
        public const int MaxHp = 15;

        /// <summary>캐릭터 카드 최대 리치 (RULES.md 3번, R4).</summary>
        public const int MaxReach = 7;

        /// <summary>멀리건 시 뽑는 장수 (RULES.md 4번 준비).</summary>
        public const int MulliganDrawCount = 6;

        /// <summary>멀리건 시 손패에서 코스트존으로 배치하는 장수 (RULES.md 4번 준비).</summary>
        public const int MulliganCostCount = 2;

        /// <summary>드로우 페이즈에 덱에서 뽑는 장수 (RULES.md 5-1번).</summary>
        public const int DrawPhaseDrawCount = 2;

        /// <summary>드로우 페이즈에 드로우한 카드 중 코스트존으로 넣을 수 있는 최대 장수 (RULES.md 5-1번).</summary>
        public const int DrawPhaseMaxToCost = 2;

        /// <summary>캐릭터 존 위치 인덱스: 최전선 (RULES.md R4 상세 배치 도식).</summary>
        public const int FrontPosition = 0;

        /// <summary>캐릭터 존 위치 인덱스: 최후방 (PartySize - 1).</summary>
        public const int BackPosition = PartySize - 1;
    }
}
