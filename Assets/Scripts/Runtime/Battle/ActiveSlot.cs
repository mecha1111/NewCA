using CrossAccel.Data;

namespace CrossAccel.Battle
{
    /// <summary>
    /// 레디 페이즈에 액티브 존으로 뒷면 배치한 카드 한 장 (RULES.md 5-2).
    /// 액션 페이즈에 속도 순서대로 뒤집혀 발동된다 (RULES.md 5-3, R1).
    /// </summary>
    public class ActiveSlot
    {
        /// <summary>이 카드를 낸 플레이어.</summary>
        public int OwnerId { get; }

        /// <summary>사용할 스킬 카드 원본 데이터.</summary>
        public SkillData Skill { get; }

        /// <summary>이 카드를 사용하는 캐릭터 (챌린지 무기 매칭·리치 계산의 기준).</summary>
        public CharacterUnit User { get; }

        /// <summary>엑셀 지정 여부. 엑셀 카드가 일반 카드보다 먼저 발동된다 (RULES.md 5-3).</summary>
        public bool IsAccel { get; }

        /// <summary>
        /// 신속 지정 여부 (RULES.md 7번). ⚠️ 스타터 덱에 신속을 부여하는 카드가 없어 실사용 경로가 없다.
        /// 토큰 구조만 유지하고 발동 순서에는 아직 반영하지 않는다 — 실제로 쓰는 카드가 생기면
        /// 엑셀과의 우선순위를 RULES.md에서 확정한 뒤 반영할 것.
        /// </summary>
        public bool IsSwift { get; }

        /// <summary>
        /// 두 번째 스킬을 가진 카드에서 어느 쪽으로 낼지. 1 = skill1, 2 = skill2.
        /// 레디 페이즈에 카드를 낼 때 확정되며 액션 페이즈에는 이 값대로 발동한다 (RULES.md R14).
        /// </summary>
        public int SkillOption { get; }

        /// <summary>액션 페이즈에서 처리가 끝났는지 (발동됐든 무효가 됐든).</summary>
        public bool Resolved { get; set; }

        /// <summary>효과가 실제로 발동됐는지. 챌린지 패배·코스트 불발이면 false.</summary>
        public bool Activated { get; set; }

        public ActiveSlot(int ownerId, SkillData skill, CharacterUnit user,
                          bool isAccel = false, bool isSwift = false, int skillOption = 1)
        {
            OwnerId = ownerId;
            Skill = skill;
            User = user;
            IsAccel = isAccel;
            IsSwift = isSwift;
            SkillOption = skillOption;
        }

        /// <summary>이 슬롯이 실제로 지불해야 하는 코스트 (선택한 스킬 쪽 코스트).</summary>
        public int Cost
        {
            get
            {
                // DATA_SCHEMA.md: skill1Cost -1 = 데이터 미기재(X코스트) → RULES.md 11번 가정대로 0으로 취급
                int raw = SkillOption == 2 ? Skill.Skill2Cost ?? 0 : Skill.Skill1Cost;
                return raw < 0 ? 0 : raw;
            }
        }
    }
}
