using System.Collections.Generic;
using Newtonsoft.Json;

namespace CrossAccel.Data
{
    /// <summary>
    /// 스킬(메인) 카드 원본 데이터 (읽기 전용). Assets/StreamingAssets/DataJsons/SkillData.json에서 로드.
    /// 필드 설명: docs/DATA_SCHEMA.md 참고.
    /// </summary>
    public class SkillData
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("weaponType")] public string WeaponType { get; set; }
        [JsonProperty("speed")] public int Speed { get; set; }

        /// <summary>스킬1 코스트. -1 = 데이터 미정(X코스트로 해석, 예: S79. RULES.md 11번 참고).</summary>
        [JsonProperty("skill1Cost")] public int Skill1Cost { get; set; }
        [JsonProperty("skill1Effect")] public string Skill1Effect { get; set; }

        /// <summary>
        /// 스킬2 코스트. 실측 확인(2026-08-07): 두 번째 스킬이 없는 카드는 이 필드 자체가 JSON에 없다
        /// (예전 문서의 "int.MinValue sentinel" 인코딩은 실제 데이터와 다름 — DATA_SCHEMA.md 정정 참고).
        /// null이면 두 번째 스킬 없음.
        /// </summary>
        [JsonProperty("skill2Cost")] public int? Skill2Cost { get; set; }
        [JsonProperty("skill2Effect")] public string Skill2Effect { get; set; }

        /// <summary>두 번째 스킬(선택 발동형) 보유 여부.</summary>
        public bool HasSkill2 => Skill2Cost.HasValue;
    }

    /// <summary>SkillData.json의 최상위 래퍼 ({ "skills": [...] }). 로더 내부 전용.</summary>
    internal class SkillDataFile
    {
        [JsonProperty("skills")] public List<SkillData> Skills { get; set; }
    }
}
