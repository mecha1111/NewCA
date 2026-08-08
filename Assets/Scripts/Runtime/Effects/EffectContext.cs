using CrossAccel.Battle;
using CrossAccel.Data;

namespace CrossAccel.Effects
{
    /// <summary>
    /// 스킬 카드 효과 한 번의 실행 컨텍스트. GameManager가 채워서
    /// <see cref="CrossAccel.Battle.GameManager.SkillEffectResolver"/>로 넘긴다.
    ///
    /// Phase 3 시점에는 효과 구현이 없으므로 이 컨텍스트를 소비하는 쪽이 비어 있다.
    /// Phase 4에서 EffectSystem이 이걸 받아 카드 id별 핸들러를 실행한다 (ARCHITECTURE.md).
    /// </summary>
    public class EffectContext
    {
        /// <summary>진행 중인 게임 (드로우·데미지 등 공통 처리를 되부를 때 사용).</summary>
        public GameManager Game { get; set; }

        /// <summary>카드를 사용한 플레이어.</summary>
        public PlayerState Owner { get; set; }

        /// <summary>상대 플레이어.</summary>
        public PlayerState Opponent { get; set; }

        /// <summary>카드를 사용하는 캐릭터 (리치 계산 기준).</summary>
        public CharacterUnit User { get; set; }

        /// <summary>선택된 대상. TargetSelector 결과이며 없을 수 있다.</summary>
        public CharacterUnit Target { get; set; }

        /// <summary>사용된 스킬 카드 원본 데이터.</summary>
        public SkillData Skill { get; set; }

        /// <summary>엑셀로 지정된 카드인지 (RULES.md 5-2).</summary>
        public bool IsAccel { get; set; }

        /// <summary>이 카드에 실린 추가 데미지 (C14/C31/C19 등이 걸어둔 "다음 스킬 데미지 +N").</summary>
        public int DamageBonus { get; set; }

        /// <summary>이 카드가 관통을 가지는지 (RULES.md 7번 키워드: 관통 — 방어도 무시).</summary>
        public bool Piercing { get; set; }

        /// <summary>
        /// 두 번째 스킬(선택 발동형)을 가진 카드에서 어느 쪽을 쓰는지. 1 = skill1(기본), 2 = skill2.
        /// 레디 페이즈에 카드를 낼 때 확정되며(RULES.md R14), 코스트도 이 값에 맞춰 지불된 상태로 넘어온다.
        /// </summary>
        public int SkillOption { get; set; } = 1;
    }

    /// <summary>캐릭터 고유 효과가 타이밍 훅에서 실행될 때의 컨텍스트.</summary>
    public class CharacterEffectContext
    {
        public GameManager Game { get; set; }

        /// <summary>효과의 주인 캐릭터.</summary>
        public CharacterUnit Self { get; set; }

        public PlayerState Owner { get; set; }
        public PlayerState Opponent { get; set; }
    }

    /// <summary>
    /// 데미지 계산 시점에 캐릭터별 조건부 보정을 묻는 컨텍스트 (C05, C15, C22, C30).
    /// 카드 데이터의 effectTiming "카드 스킬 발동 전"에 해당한다.
    /// </summary>
    public class DamageModContext
    {
        public GameManager Game { get; set; }

        /// <summary>공격하는 캐릭터.</summary>
        public CharacterUnit Attacker { get; set; }

        /// <summary>맞는 캐릭터.</summary>
        public CharacterUnit Target { get; set; }

        public PlayerState Owner { get; set; }

        /// <summary>발동 중인 스킬의 코스트 (C03이 참조).</summary>
        public int SkillCost { get; set; }
    }
}
