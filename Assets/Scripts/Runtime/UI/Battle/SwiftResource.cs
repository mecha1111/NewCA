// 담당 모듈: C (전투 흐름 + 엔진 브릿지) — UNITY_PORTING_SPEC.md 4-5
// 의존: CharacterData.Swift (파티 캐릭터가 제공하는 신속 수)

using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 신속 자원 보유/소모를 추적한다. 최대치 = 파티 캐릭터가 제공하는 신속 수의 합.
    /// [왜] 엔진(GameManager)에는 신속 <b>자원</b> 개념이 없다. 엔진이 아는 것은 카드 한 장이
    ///      신속인지(ActiveSlot.IsSwift)와 그에 따른 발동 순서뿐이다. "몇 개까지 지정할 수 있는가"는
    ///      UI 단계의 제약이라 여기서 센다.
    /// [주의] 게임 규칙 재계산이 아니다 — 발동 순서·HP·코스트는 전부 엔진이 진실이고, 여기는
    ///        "지정 가능 횟수"만 센다. 매 턴 <see cref="ResetForTurn"/>으로 되돌려야 한다(RULES.md 7번: 매 턴 리셋).
    /// [주의] 현재 모든 캐릭터의 swift가 0이라 실사용 경로가 없다. 값이 정해지면 JSON만 채우면 동작한다.
    /// </summary>
    public class SwiftResource
    {
        /// <summary>[무엇] 이번 판의 신속 최대치 (파티 확정 시 결정).</summary>
        public int Max { get; private set; }

        /// <summary>[무엇] 지금 남은 신속 수.</summary>
        public int Remaining { get; private set; }

        /// <summary>[무엇] 파티에서 최대치를 계산해 세팅한다. [왜] 파티가 확정된 뒤(배틀 진입) 한 번 부른다.</summary>
        public void InitializeFrom(IEnumerable<CharacterUnit> party)
        {
            Max = party?.Sum(u => u.Data.Swift) ?? 0;
            Remaining = Max;
        }

        /// <summary>[무엇] 매 턴 시작 시 전량 회복. [왜] RULES.md 7번 "매 턴 리셋"(코스트 언탭과 같은 취급).</summary>
        public void ResetForTurn() => Remaining = Max;

        /// <summary>[무엇] 신속 1개 사용. [주의] 남은 게 없으면 false — 호출자가 토글을 막아야 한다.</summary>
        public bool TrySpend()
        {
            if (Remaining <= 0) return false;
            Remaining--;
            return true;
        }

        /// <summary>[무엇] 신속 1개 환급 (지정 해제·배치 취소 시). [주의] 최대치를 넘겨 돌려주지 않는다.</summary>
        public void Refund()
        {
            if (Remaining < Max) Remaining++;
        }
    }
}
