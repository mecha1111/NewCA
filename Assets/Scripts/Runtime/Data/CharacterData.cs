using System.Collections.Generic;
using Newtonsoft.Json;

namespace CrossAccel.Data
{
    /// <summary>
    /// 캐릭터 카드 원본 데이터 (읽기 전용). Assets/StreamingAssets/DataJsons/CharacterCardData.json에서 로드.
    /// 원본 데이터 불변 원칙(CLAUDE.md) — 런타임 상태(HP, 버프 등)는 CharacterUnit에 둔다.
    /// 필드 설명: docs/DATA_SCHEMA.md 참고.
    /// </summary>
    public class CharacterData
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("race")] public string Race { get; set; }
        [JsonProperty("weaponType")] public string WeaponType { get; set; }
        [JsonProperty("maxHp")] public int MaxHp { get; set; }
        [JsonProperty("reach")] public int Reach { get; set; }
        [JsonProperty("effectTiming")] public string EffectTiming { get; set; }
        [JsonProperty("effectText")] public string EffectText { get; set; }

        /// <summary>
        /// [무엇] 이 캐릭터가 파티에 제공하는 신속 자원 수 (RULES.md 7번 신속).
        /// [왜] 신속 자원은 "파티 캐릭터가 제공하는 수의 합"이라 캐릭터 단위 데이터가 필요하다.
        /// [주의] 현재 전 캐릭터 0이다. 어느 캐릭터가 몇 개를 주는지는 밸런스 사항이라 아직 정해지지
        ///        않았고, 출처 없는 값을 임의로 넣지 않는다. 값이 정해지면 JSON만 채우면 된다.
        /// </summary>
        [JsonProperty("swift")] public int Swift { get; set; }

        /// <summary>effectText가 비어있거나 "없음"이면 무효과 캐릭터 (DATA_SCHEMA.md).</summary>
        public bool HasEffect => !string.IsNullOrEmpty(EffectText) && EffectText != "없음";
    }

    /// <summary>CharacterCardData.json의 최상위 래퍼 ({ "characters": [...] }). 로더 내부 전용.</summary>
    internal class CharacterDataFile
    {
        [JsonProperty("characters")] public List<CharacterData> Characters { get; set; }
    }
}
