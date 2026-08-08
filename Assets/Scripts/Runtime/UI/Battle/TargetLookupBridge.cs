// 담당 모듈: C (전투 흐름 + 엔진 브릿지) — UNITY_PORTING_SPEC.md 4-4
// 의존: GameManager.TargetSelector 델리게이트

using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Data;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 레디 페이즈에 유저가 고른 타겟을 기억했다가, 액션 페이즈에 엔진이 물어보면 돌려주는 룩업 표.
    /// [왜] <b>엔진의 TargetSelector는 RunActionPhase 실행 중에 동기로 호출된다.</b> 그 안에서 클릭을
    ///      기다리면 메인 스레드가 멈춰 화면이 아예 안 그려진다(밴픽에서 이미 겪은 문제).
    ///      그래서 "미리 고르고 → 나중에 조회만" 하는 결과 인계 방식을 쓴다.
    /// [주의] 유저가 고른 타겟이 액션 시점에 이미 죽었거나 리치 밖이면 <b>여기서 판단하지 않는다</b>.
    ///        엔진이 리치·생존을 다시 보고 처리한다(EffectSystem.DealDamage). UI가 규칙을 재계산하면
    ///        엔진과 어긋난다.
    /// [주의] 매 레디 페이즈마다 <see cref="Clear"/>로 비워야 이전 턴 지정이 남지 않는다.
    /// </summary>
    public class TargetLookupBridge
    {
        // 키는 (사용 캐릭터, 스킬) 쌍. 같은 캐릭터가 여러 장을 낼 수 있으므로 스킬까지 봐야 한다.
        private readonly Dictionary<(CharacterUnit user, SkillData skill), CharacterUnit> _table =
            new Dictionary<(CharacterUnit, SkillData), CharacterUnit>();

        /// <summary>[무엇] 유저가 고른 타겟을 기록한다. [주의] target이 null이면 "지정 없음"으로 남긴다.</summary>
        public void Remember(CharacterUnit user, SkillData skill, CharacterUnit target)
        {
            if (user == null || skill == null) return;
            _table[(user, skill)] = target;
        }

        /// <summary>[무엇] 기록을 지운다 (해당 배치가 취소됐을 때).</summary>
        public void Forget(CharacterUnit user, SkillData skill)
        {
            if (user == null || skill == null) return;
            _table.Remove((user, skill));
        }

        /// <summary>[무엇] 레디 페이즈 시작 시 전부 비운다.</summary>
        public void Clear() => _table.Clear();

        /// <summary>
        /// [무엇] 엔진에 꽂을 TargetSelector 구현. 내 카드면 유저 지정값을, 아니면 폴백을 돌려준다.
        /// [왜] 엔진의 TargetSelector 하나로 양쪽 플레이어를 다 처리해야 해서, 소유자에 따라 갈라준다.
        /// [주의] fallback은 상대(AI)용이다. 내 카드인데 지정이 없으면(회복·리치 내 1명 등)
        ///        역시 fallback으로 넘겨 엔진/AI 기본 규칙을 따르게 한다.
        /// </summary>
        public CharacterUnit Resolve(CharacterUnit user, PlayerState opponent, SkillData skill,
                                     System.Func<CharacterUnit, PlayerState, SkillData, CharacterUnit> fallback)
        {
            if (user != null && skill != null && _table.TryGetValue((user, skill), out var target))
            {
                // 지정은 했지만 그 대상이 이미 죽었으면 엔진 기본 규칙에 맡긴다.
                if (target != null && !target.IsDead) return target;
            }

            return fallback?.Invoke(user, opponent, skill)
                   ?? opponent.CharacterZone.Where(u => !u.IsDead).OrderBy(u => u.Position).FirstOrDefault();
        }
    }
}
