namespace CrossAccel.Core
{
    /// <summary>
    /// 게임 진행 페이즈. RULES.md 4번(게임 시작 전)·5번(턴 진행) 순서를 그대로 반영한다.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>밴픽 (RULES.md 4번).</summary>
        BanPick,

        /// <summary>세팅 — 캐릭터 배치/HP 표시 (RULES.md 4번).</summary>
        Setup,

        /// <summary>준비(멀리건) — 6장 드로우, 2장 코스트존 배치 (RULES.md 4번).</summary>
        Mulligan,

        /// <summary>드로우 페이즈 (RULES.md 5-1).</summary>
        Draw,

        /// <summary>레디 페이즈 (RULES.md 5-2).</summary>
        Ready,

        /// <summary>액션 페이즈 (RULES.md 5-3).</summary>
        Action,

        /// <summary>엔드 페이즈 (RULES.md 5-4).</summary>
        End,

        /// <summary>게임 종료 (RULES.md 1번: 캐릭터 존이 전부 비면 패배).</summary>
        GameOver
    }
}
