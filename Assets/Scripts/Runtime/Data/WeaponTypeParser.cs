using System.Collections.Generic;
using System.Linq;

namespace CrossAccel.Data
{
    /// <summary>
    /// weaponType 원문 문자열 파싱 유틸.
    /// - 콤마로 구분된 복수 무기 표기를 분리 (예: "스태프, 활" → ["스태프", "활"])
    /// - "한손 검" 같은 공백 흔들림을 정규화 (예: "한손 검" → "한손검")
    /// docs/DATA_SCHEMA.md 참고.
    /// </summary>
    public static class WeaponTypeParser
    {
        /// <summary>
        /// 무기 무제한(공용) 표기. 스킬 카드에만 쓰인다.
        /// 챌린지에서는 이 표기가 붙은 카드가 무조건 매칭 실패로 처리된다 (RULES.md R7).
        /// </summary>
        public const string Common = "공용";

        /// <summary>weaponType 원문을 콤마로 분리하고 각 토큰을 정규화해서 반환한다.</summary>
        public static List<string> Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            return raw
                .Split(',')
                .Select(Normalize)
                .Where(token => token.Length > 0)
                .ToList();
        }

        /// <summary>
        /// 토큰 하나를 정규화한다: 내부 공백을 전부 제거한다 (예: "한손 검" → "한손검").
        /// 무기 이름 자체에는 공백이 없는 게 정상이므로, 공백 제거만으로 흔들림을 안전하게 정규화할 수 있다.
        /// </summary>
        public static string Normalize(string token)
        {
            if (string.IsNullOrEmpty(token))
                return string.Empty;

            return new string(token.Where(c => !char.IsWhiteSpace(c)).ToArray());
        }
    }
}
