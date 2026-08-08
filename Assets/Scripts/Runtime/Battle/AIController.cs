using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CrossAccel.Core;
using CrossAccel.Data;

namespace CrossAccel.Battle
{
    /// <summary>
    /// 휴리스틱 AI. GameManager의 모든 델리게이트를 채워 한 플레이어를 자동으로 운영한다.
    /// 목표는 강함이 아니라 "합법적인 수를 계속 두어 규칙을 끝까지 검증하는 것"
    /// (docs/PROTOTYPE_REFERENCE/README.md 설계 철학 그대로).
    ///
    /// 카드 효과 텍스트를 정규식으로 훑어 대략적인 가치를 매기는 부분(EstimateEffectValue 등)은
    /// "카드 효과는 파싱하지 않는다"(CLAUDE.md 원칙 2)에 대한 예외가 아니다 — 그 원칙은 게임 규칙을
    /// 텍스트에서 뽑아내 룰 엔진에 반영하지 말라는 것이고, 여기서는 이미 EffectSystem이 구현한
    /// 효과의 "대략 얼마나 좋은가"를 AI가 어림짐작하는 것뿐이라 오차가 있어도 게임이 깨지지 않는다.
    /// </summary>
    public class AIController
    {
        private static readonly Regex DamagePattern = new Regex(@"데미지\s*\+?\s*(\d+)");
        private static readonly Regex HealPattern = new Regex(@"회복\s*\+?\s*(\d+)");
        private static readonly Regex DefensePattern = new Regex(@"방어도\s*\+?\s*(\d+)");
        private static readonly Regex DrawPattern = new Regex(@"(\d+)\s*장\s*드로우");

        public int PlayerId { get; }

        private readonly CardDatabase _database;
        private readonly Random _rng;

        /// <summary>
        /// ChoosePicks가 몇 번째로 불렸는지 (RULES.md 4번: 밴픽 2라운드, 라운드마다 2장 픽).
        /// 이번에 고른 2장이 앞줄(0,1)인지 뒷줄(2,3)인지를 이 값으로 판단한다.
        /// </summary>
        private int _pickRoundCount;

        public AIController(int playerId, CardDatabase database, Random rng)
        {
            PlayerId = playerId;
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>
        /// GameManager의 모든 델리게이트를 ai[user/player의 OwnerId]로 라우팅한다.
        /// ai.Length는 GameRules.PlayerCount와 같아야 한다(2인 전제).
        /// </summary>
        public static void Attach(GameManager game, AIController[] ai)
        {
            game.BanSelector = (playerId, opponentAvailable) => ai[playerId].ChooseBan(opponentAvailable);
            game.PickSelector = (playerId, available, count) => ai[playerId].ChoosePicks(available, count);
            game.TargetSelector = (user, opponent, skill) => ai[user.OwnerId].ChooseTarget(user, opponent, skill);
            game.EffectActivationPolicy = unit => ai[unit.OwnerId].ShouldActivate(unit);
            game.SkillOptionSelector = (user, skill) =>
                ai[user.OwnerId].ChooseSkillOption(user, skill, game.Players[user.OwnerId]);
            game.VariableCostPolicy = (payer, max) => ai[payer.PlayerId].ChooseVariableCost(max);
        }

        // ===================== 밴픽 =====================

        /// <summary>상대 캐릭터 덱에서 1장 밴 — 가장 위협적인(고HP·긴 리치·효과 보유) 카드.</summary>
        public string ChooseBan(IReadOnlyList<string> opponentAvailable) =>
            RankCharacters(opponentAvailable, ScoreThreat).Select(x => x.Id).FirstOrDefault();

        /// <summary>
        /// 자기 덱에서 n장 픽. GameManager가 픽 순서를 그대로 Position에 배치하므로(별도 재배치 단계 없음),
        /// 몇 번째 라운드인지로 앞줄/뒷줄을 나눠 다른 기준을 쓴다:
        ///   1라운드(앞줄 0,1) — 먼저 맞는 자리이므로 오래 버틸 HP 우선.
        ///   2라운드(뒷줄 2,3) — 거리(R4)가 먼 자리이므로 그래도 공격이 닿게 리치 우선.
        /// 순수 총점으로만 뽑는 것보다 낫다는 정도의 단순 휴리스틱이며, R9(앞당김) 아래선 뒷줄도
        /// 결국 전진하므로 승부에 결정적이진 않다.
        /// </summary>
        public IReadOnlyList<string> ChoosePicks(IReadOnlyList<string> available, int count)
        {
            bool frontRound = _pickRoundCount == 0;
            _pickRoundCount++;

            var score = frontRound ? (Func<CharacterData, int>)ScoreFrontline : ScoreBackline;
            return RankCharacters(available, score).Take(count).Select(x => x.Id).ToList();
        }

        private IEnumerable<(string Id, CharacterData Data)> RankCharacters(
            IReadOnlyList<string> ids, Func<CharacterData, int> score) =>
            ids.Select(id => (Id: id, Data: _database.Characters.TryGetValue(id, out var d) ? d : null))
               .Where(x => x.Data != null)
               .OrderByDescending(x => score(x.Data));

        private static int ScoreThreat(CharacterData data) =>
            data.MaxHp + data.Reach + (data.HasEffect ? 4 : 0);

        private static int ScoreFrontline(CharacterData data) =>
            data.MaxHp * 2 + data.Reach + (data.HasEffect ? 4 : 0);

        private static int ScoreBackline(CharacterData data) =>
            data.Reach * 3 + data.MaxHp + (data.HasEffect ? 4 : 0);

        // ===================== 코스트 배치 =====================

        /// <summary>멀리건: 패 6장 중 코스트존으로 보낼 2장 — 내 파티가 못 쓰는 카드 우선, 그다음 무거운 카드.</summary>
        public IReadOnlyList<SkillData> ChooseMulliganCost(PlayerState me) =>
            me.Hand
                .OrderBy(skill => IsUsableByParty(me, skill) ? 1 : 0)
                .ThenByDescending(skill => EffectiveCost(skill, 1))
                .Take(GameRules.MulliganCostCount)
                .ToList();

        /// <summary>
        /// 드로우 페이즈: 이번에 뽑은 카드 중 몇 장을 코스트로 보낼까.
        /// 못 쓰는 카드는 무조건 보내고, 코스트가 아직 적으면(4장 미만) 적극적으로 보충한다.
        /// </summary>
        public IReadOnlyList<SkillData> ChooseDrawCost(PlayerState me, IReadOnlyList<SkillData> drawn)
        {
            var result = new List<SkillData>();
            bool needRamp = me.CostZone.Count < 4;

            foreach (var skill in drawn)
            {
                if (result.Count >= GameRules.DrawPhaseMaxToCost) break;

                bool unusable = !IsUsableByParty(me, skill);
                if (unusable || needRamp)
                    result.Add(skill);
            }
            return result;
        }

        private bool IsUsableByParty(PlayerState me, SkillData skill) =>
            me.CharacterZone.Any(u => !u.IsDead && CanUse(skill, u));

        // ===================== 레디 페이즈: 카드 플레이 =====================

        /// <summary>레디 페이즈에 제출할 카드 한 장의 결정 — 스킬, 사용 캐릭터, 엑셀 여부, skill1/skill2 선택.</summary>
        public struct PlayDecision
        {
            public SkillData Skill;
            public CharacterUnit User;
            public bool AsAccel;
            public int SkillOption;
        }

        /// <summary>
        /// 이번 턴에 낼 카드들 — 지불 가능한 코스트 안에서 가치가 높은 순으로 그리디하게 고른다.
        /// skill1/skill2가 있는 카드는 여기서 어느 쪽을 낼지도 확정한다 (RULES.md R14: 레디 페이즈에 확정).
        /// </summary>
        public List<PlayDecision> ChoosePlays(PlayerState me, PlayerState opponent)
        {
            var plays = new List<PlayDecision>();
            var aliveParty = me.CharacterZone.Where(u => !u.IsDead).ToList();
            if (aliveParty.Count == 0) return plays;

            var candidates = new List<(SkillData skill, CharacterUnit user, int option, int cost, float value)>();
            foreach (var skill in me.Hand)
            {
                var user = aliveParty.FirstOrDefault(u => CanUse(skill, u));
                if (user == null) continue; // 이 카드를 쓸 수 있는 캐릭터가 파티에 없음

                int option = ChooseSkillOption(user, skill, me);
                int cost = EffectiveCost(skill, option);
                float value = ScoreSkillOption(skill, option) - cost * 0.5f;

                candidates.Add((skill, user, option, cost, value));
            }

            int budget = me.AvailableCost;
            int accelBudget = aliveParty.Sum(u => u.AccelTokens);

            // RULES.md는 턴당 낼 수 있는 카드 수를 캐릭터 수로 제한하지 않는다 — 유일한 한도는
            // 코스트(7번 키워드)뿐이므로, 예산이 허락하는 한 가치 높은 순으로 계속 낸다.
            foreach (var candidate in candidates.OrderByDescending(c => c.value))
            {
                if (candidate.cost > budget) continue;

                budget -= candidate.cost;
                bool accel = accelBudget > 0;
                if (accel) accelBudget--;

                plays.Add(new PlayDecision
                {
                    Skill = candidate.skill,
                    User = candidate.user,
                    AsAccel = accel,
                    SkillOption = candidate.option
                });
            }
            return plays;
        }

        // ===================== 타겟팅 =====================

        /// <summary>리치 내에서 처치 가능한 적 우선(가장 HP 큰 것부터), 없으면 최저 HP.</summary>
        public CharacterUnit ChooseTarget(CharacterUnit user, PlayerState opponent, SkillData skill)
        {
            var reachable = opponent.CharacterZone.Where(u => !u.IsDead && user.CanReach(u.Position)).ToList();
            if (reachable.Count == 0) return null;

            int expectedDamage = ExtractDamageAmount(skill?.Skill1Effect);
            if (expectedDamage <= 0) expectedDamage = 2; // 텍스트에서 못 뽑으면 대략치

            var killable = reachable
                .Where(u => u.CurrentHp + u.Defense <= expectedDamage)
                .OrderByDescending(u => u.Data.MaxHp)
                .FirstOrDefault();

            return killable ?? reachable.OrderBy(u => u.CurrentHp).First();
        }

        // ===================== 선택 발동형 효과 =====================

        /// <summary>
        /// 선택 발동형 캐릭터 효과를 시도할지. 항상 true — 실제 지불 가능 여부는 각 핸들러가
        /// PayCost/PayHp로 자체 검사해 실패하면 조용히 넘어가므로, AI는 "일단 시도"만 하면 된다.
        /// </summary>
        public bool ShouldActivate(CharacterUnit unit) => true;

        /// <summary>
        /// skill1/skill2 중 어느 쪽이 나은지. 소모 코스트 대비 가치를 비교해 skill2가 명확히
        /// 유리하고 지불 가능할 때만 2를 고르고, 그 외(모호하거나 감당 못 함)엔 기본 1.
        /// </summary>
        public int ChooseSkillOption(CharacterUnit user, SkillData skill, PlayerState me)
        {
            if (skill == null || !skill.HasSkill2) return 1;

            int cost2 = EffectiveCost(skill, 2);
            if (cost2 > me.AvailableCost) return 1; // 애초에 못 냄

            float value1 = ScoreSkillOption(skill, 1);
            float value2 = ScoreSkillOption(skill, 2);
            return value2 > value1 ? 2 : 1;
        }

        /// <summary>가변 지불량(S79 X코스트, C21 HP→코스트 변환) — 절반쯤 쓰고 나머지는 다음 턴 대비.</summary>
        public int ChooseVariableCost(int maxAffordable)
        {
            if (maxAffordable <= 0) return 0;
            return Math.Max(1, Math.Min(4, maxAffordable / 2 + 1));
        }

        // ===================== 공용 휴리스틱 =====================

        /// <summary>
        /// 캐릭터가 이 스킬을 쓸 수 있는지 — 무기 타입이 겹치거나(공용은 누구나), 효과로 얻은
        /// 추가 무기(C24의 "활" 등)에 해당하면 사용 가능. GameManager는 이를 강제하지 않으므로
        /// (SubmitActiveCard가 무기 검사를 안 함) AI가 항상 합법적인 조합만 골라 제출해야 한다.
        /// </summary>
        public static bool CanUse(SkillData skill, CharacterUnit unit)
        {
            var skillTypes = WeaponTypeParser.Parse(skill.WeaponType);
            if (skillTypes.Contains(WeaponTypeParser.Common)) return true;

            var userTypes = WeaponTypeParser.Parse(unit.Data.WeaponType);
            if (skillTypes.Any(t => userTypes.Contains(t))) return true;

            return unit.ExtraWeapons.Any(extra => skillTypes.Contains(extra));
        }

        /// <summary>DATA_SCHEMA.md: skill1Cost -1 = X코스트(데이터 미기재) → RULES.md 11번 가정대로 0 취급.</summary>
        private static int EffectiveCost(SkillData skill, int option)
        {
            int raw = option == 2 ? skill.Skill2Cost ?? 0 : skill.Skill1Cost;
            return raw < 0 ? 0 : raw;
        }

        /// <summary>스킬 한 장(선택한 옵션 기준)의 대략적인 가치 — 코스트 미포함.</summary>
        private static float ScoreSkillOption(SkillData skill, int option)
        {
            string text = option == 2 ? skill.Skill2Effect : skill.Skill1Effect;
            return 1f + EstimateEffectValue(text) + skill.Speed * 0.3f; // 속도 높음 = 선공 이점 (RULES.md R1)
        }

        /// <summary>효과 텍스트에서 데미지/회복/방어도/드로우 수치를 대략 훑어 가치로 환산.</summary>
        private static float EstimateEffectValue(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            float value = 0f;
            value += SumMatches(DamagePattern, text) * 2f;
            value += SumMatches(HealPattern, text) * 1f;
            value += SumMatches(DefensePattern, text) * 1f;
            value += SumMatches(DrawPattern, text) * 1.5f;
            return value;
        }

        private static int ExtractDamageAmount(string text) => (int)SumMatches(DamagePattern, text);

        private static float SumMatches(Regex pattern, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            float sum = 0f;
            foreach (Match match in pattern.Matches(text))
                sum += int.Parse(match.Groups[1].Value);
            return sum;
        }
    }
}
