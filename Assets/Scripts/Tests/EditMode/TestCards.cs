using CrossAccel.Data;

namespace CrossAccel.Tests
{
    /// <summary>
    /// 테스트용 카드 데이터 팩토리. 실제 JSON 대신 규칙 검증에 필요한 수치만 담은 최소 카드를 만든다.
    /// (실데이터 로드 검증은 DataLoadTests가 담당)
    /// </summary>
    internal static class TestCards
    {
        public static CharacterData Character(string id, string weapon = "한손검", int maxHp = 15, int reach = 7,
                                              string race = "인간") =>
            new CharacterData
            {
                Id = id,
                Name = id,
                Race = race,
                WeaponType = weapon,
                MaxHp = maxHp,
                Reach = reach,
                EffectTiming = "",
                EffectText = ""
            };

        /// <summary>skill2Cost를 주면 두 번째 스킬(선택 발동형)을 가진 카드가 된다 (SkillData.HasSkill2).</summary>
        public static SkillData Skill(string id, string weapon = "공용", int speed = 1, int cost = 0,
                                      int? skill2Cost = null) =>
            new SkillData
            {
                Id = id,
                Name = id,
                WeaponType = weapon,
                Speed = speed,
                Skill1Cost = cost,
                Skill1Effect = "테스트 효과",
                Skill2Cost = skill2Cost,
                Skill2Effect = skill2Cost.HasValue ? "테스트 효과2" : null
            };
    }
}
