namespace CrossAccel.Battle
{
    /// <summary>
    /// 코스트 존에 뒷면으로 놓인 카드 한 장 (RULES.md 2번).
    /// 코스트로 쓰면 꺾어서(레스트) 사용했음을 표시하고, 매 턴 시작에 전부 다시 세운다 (RULES.md R2).
    ///
    /// 스킬 카드뿐 아니라 엔드 페이즈에 사망한 캐릭터 카드도 여기로 오므로(RULES.md 5-4),
    /// 카드 종류를 가리지 않도록 원본 데이터가 아닌 id만 들고 있는다.
    /// </summary>
    public class CostCard
    {
        /// <summary>카드 id (스킬 "S01" 또는 사망 캐릭터 "C05").</summary>
        public string CardId { get; }

        /// <summary>레스트(꺾인) 상태 = 이번 턴에 이미 코스트로 썼음.</summary>
        public bool IsRested { get; set; }

        public CostCard(string cardId, bool isRested = false)
        {
            CardId = cardId;
            IsRested = isRested;
        }
    }
}
