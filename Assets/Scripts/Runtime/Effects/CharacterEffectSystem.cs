using System;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;

namespace CrossAccel.Effects
{
    /// <summary>
    /// 캐릭터 고유 효과: (캐릭터 id, 타이밍) → 핸들러 + 데미지 계산 시 조건부 보정.
    /// 카드 텍스트는 자연어라 파싱하지 않고 id별 C# 핸들러로 등록한다 (CLAUDE.md 원칙 2).
    ///
    /// 정적 상태를 쓰지 않는다 — 인스턴스로 만들어 GameManager에 붙인다. 그래야 테스트가 서로 간섭하지 않는다.
    /// </summary>
    public class CharacterEffectSystem
    {
        private readonly Dictionary<(string id, EffectTiming timing), Action<CharacterEffectContext>> _triggers =
            new Dictionary<(string, EffectTiming), Action<CharacterEffectContext>>();

        private readonly Dictionary<string, Func<DamageModContext, int>> _damageMods =
            new Dictionary<string, Func<DamageModContext, int>>();

        /// <summary>아군 스킬의 대상이 되었을 때 반응하는 효과 (C03). (대상, 스킬 코스트) → 처리.</summary>
        private readonly Dictionary<string, Action<CharacterUnit, int>> _onAllyTargeted =
            new Dictionary<string, Action<CharacterUnit, int>>();

        public void RegisterTrigger(string characterId, EffectTiming timing, Action<CharacterEffectContext> handler) =>
            _triggers[(characterId, timing)] = handler;

        public void RegisterDamageMod(string characterId, Func<DamageModContext, int> modifier) =>
            _damageMods[characterId] = modifier;

        public void RegisterAllyTargeted(string characterId, Action<CharacterUnit, int> handler) =>
            _onAllyTargeted[characterId] = handler;

        /// <summary>특정 타이밍에 양쪽 파티의 살아있는 캐릭터 효과를 발동한다.</summary>
        public void FireTiming(GameManager game, EffectTiming timing)
        {
            foreach (var player in game.Players)
            {
                var opponent = game.Players[GameManager.Opponent(player.PlayerId)];

                // 효과가 캐릭터 존을 바꿀 수 있으므로 스냅샷을 떠서 순회한다.
                foreach (var unit in player.CharacterZone.Where(u => !u.IsDead).ToList())
                {
                    if (!_triggers.TryGetValue((unit.Data.Id, timing), out var handler)) continue;

                    handler(new CharacterEffectContext
                    {
                        Game = game,
                        Self = unit,
                        Owner = player,
                        Opponent = opponent
                    });
                }
            }
        }

        /// <summary>공격 시 캐릭터 조건부 데미지 보너스. 등록되지 않은 캐릭터는 0.</summary>
        public int GetDamageBonus(DamageModContext ctx) =>
            _damageMods.TryGetValue(ctx.Attacker.Data.Id, out var modifier) ? modifier(ctx) : 0;

        /// <summary>아군 스킬의 대상이 되었을 때 호출 (C03 루트).</summary>
        public void FireAllyTargeted(CharacterUnit target, int skillCost)
        {
            if (target != null && _onAllyTargeted.TryGetValue(target.Data.Id, out var handler))
                handler(target, skillCost);
        }

        // =====================================================================
        //  스타터 덱 캐릭터 20종 등록
        // =====================================================================

        /// <summary>진행 로그. GameManager.Log와 같은 대상을 연결해 쓴다.</summary>
        public Action<string> Log;

        /// <summary>
        /// effectTiming 원문 → EffectTiming 매핑 메모 (Core/EffectTiming.cs가 미완인 이유):
        ///   "레디 페이즈 전" / "레디 페이즈 시" / "" (C07)  → ReadyPhaseBefore
        ///   "액션 페이즈 전" / "배틀 페이즈 개시 시" / "배틀페이즈 게시 시"(오타) → ActionPhaseBefore
        ///     (RULES.md 11번: "배틀 페이즈 개시 시" = 매 턴 액션 페이즈 시작)
        ///   "턴 종료 시" → TurnEnd
        ///   "카드 스킬 발동 전" → 타이밍 훅이 아니라 데미지 계산 시 보정(RegisterDamageMod)
        ///   "아군의 스킬의 대상이 되었을 때" → RegisterAllyTargeted (리액티브)
        /// </summary>
        public void RegisterStarterDeckCharacters()
        {
            RegisterAggroCharacters();
            RegisterMidRangeCharacters();
        }

        private static bool HasWeapon(CharacterUnit unit, string weapon) =>
            WeaponTypeParser.Parse(unit.Data.WeaponType).Contains(weapon);

        private static IEnumerable<CharacterUnit> LivingAllies(PlayerState player) =>
            player.CharacterZone.Where(u => !u.IsDead);

        private void RegisterAggroCharacters()
        {
            // C05 추적하는 사냥개 레나 — 필드에 노예 종족 캐릭터가 존재할 시 데미지 +1
            //  "필드"는 양쪽 캐릭터 존 전체로 해석
            RegisterDamageMod("C05", ctx =>
                ctx.Game.Players.Any(p => LivingAllies(p).Any(u => u.Data.Race == "노예")) ? 1 : 0);

            // C07 자유로운 모험가 — [코스트 1] 이번 턴 자신의 카드 1장을 엑셀로 사용 가능
            //  effectTiming이 빈 문자열 → 엑셀 지정은 레디 페이즈이므로 그 직전으로 해석
            RegisterTrigger("C07", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (!ctx.Game.ShouldActivate(ctx.Self) || !ctx.Owner.PayCost(1)) return;
                ctx.Self.AccelTokens++;
                Log?.Invoke($"[C07] {ctx.Self.Data.Name} — 코스트 1, 엑셀 획득");
            });

            // C14 도망치는 검 카르멘 — [HP-2] 턴에 한번, 엑셀. 그 후 다음 스킬 카드의 데미지 +1
            RegisterTrigger("C14", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (ctx.Self.EffectUsedThisTurn || !ctx.Game.ShouldActivate(ctx.Self)) return;
                if (!ctx.Self.PayHp(2)) return;

                ctx.Self.EffectUsedThisTurn = true;
                ctx.Self.AccelTokens++;
                ctx.Game.AddNextSkillDamageBonus(ctx.Owner.PlayerId, 1);
                Log?.Invoke("[C14] 카르멘 — HP 2 지불, 엑셀 + 다음 스킬 데미지 +1");
            });

            // C15 추적하는 검 가르샤 — 한 턴에 두 번 이상 공격 시 추가 데미지 +1
            //  이 계산 시점의 AttacksThisTurn은 아직 이번 공격이 반영되기 전이므로 >= 1이면 두 번째 공격
            RegisterDamageMod("C15", ctx => ctx.Attacker.AttacksThisTurn >= 1 ? 1 : 0);

            // C22 물리 치료사 셀리아 — [HP-1] 필드 내 악기 캐릭터 1명당 데미지 +1
            RegisterDamageMod("C22", ctx =>
            {
                int instruments = ctx.Game.Players.Sum(p => LivingAllies(p).Count(u => HasWeapon(u, "악기")));
                if (instruments == 0 || !ctx.Game.ShouldActivate(ctx.Attacker)) return 0;
                return ctx.Attacker.PayHp(1) ? instruments : 0;
            });

            // C30 해방을 꿈꾸는 노예 타이론 — HP 3 이하의 적을 공격할 때 데미지 +2
            RegisterDamageMod("C30", ctx =>
                ctx.Target != null && ctx.Target.CurrentHp <= 3 ? 2 : 0);

            // C31 강철의 분노 바르도 — [HP-2] 턴에 한번, 다음 스킬 카드의 데미지 +2
            RegisterTrigger("C31", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (ctx.Self.EffectUsedThisTurn || !ctx.Game.ShouldActivate(ctx.Self)) return;
                if (!ctx.Self.PayHp(2)) return;

                ctx.Self.EffectUsedThisTurn = true;
                ctx.Game.AddNextSkillDamageBonus(ctx.Owner.PlayerId, 2);
                Log?.Invoke("[C31] 바르도 — HP 2 지불, 다음 스킬 데미지 +2");
            });

            // C39 추격하는 무희 라파엘 — [HP-2] 카드 1장 드로우
            RegisterTrigger("C39", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (!ctx.Game.ShouldActivate(ctx.Self) || !ctx.Self.PayHp(2)) return;
                ctx.Game.DrawToHand(ctx.Owner.PlayerId, 1);
                Log?.Invoke("[C39] 라파엘 — HP 2 지불, 1장 드로우");
            });

            // C41 상처입은 노예 리베카 — [HP-2] 리치 내 모든 캐릭터 스킬 데미지 +1
            //  ※ RULES.md 11번 가정: 범위는 아군. 아군끼리의 거리는 RULES.md에 정의가 없어
            //     리치 검사 없이 아군 전체에 적용한다 (프로토타입과 동일).
            RegisterTrigger("C41", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (!ctx.Game.ShouldActivate(ctx.Self) || !ctx.Self.PayHp(2)) return;
                foreach (var ally in LivingAllies(ctx.Owner))
                    ally.TurnDamageBonus += 1;
                Log?.Invoke("[C41] 리베카 — HP 2 지불, 아군 전체 스킬 데미지 +1");
            });

            // C42 음악가 아즐 — [2 코스트] 카드 1장 드로우
            RegisterTrigger("C42", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (!ctx.Game.ShouldActivate(ctx.Self) || !ctx.Owner.PayCost(2)) return;
                ctx.Game.DrawToHand(ctx.Owner.PlayerId, 1);
                Log?.Invoke("[C42] 아즐 — 2 코스트, 1장 드로우");
            });
        }

        private void RegisterMidRangeCharacters()
        {
            // C02 피의 화살 레인 — [Cost 3] 트래쉬 존의 피 카드당 데미지 +1 (최대 5)
            //  지속 기간이 원문에 없어 이번 턴 한정으로 가정
            RegisterTrigger("C02", EffectTiming.ActionPhaseBefore, ctx =>
            {
                int blood = EffectSystem.CountBloodCardsInTrash(ctx.Owner);
                if (blood == 0 || !ctx.Game.ShouldActivate(ctx.Self)) return;
                if (!ctx.Owner.PayCost(3)) return;

                int bonus = Math.Min(5, blood);
                ctx.Self.TurnDamageBonus += bonus;
                Log?.Invoke($"[C02] 레인 — 피 카드 {blood}장, 데미지 +{bonus}");
            });

            // C03 피의 후예 루트 — 아군의 스킬의 대상이 되었을 때 스킬의 소모 코스트당 데미지 +1 (최대 3)
            RegisterAllyTargeted("C03", (unit, skillCost) =>
            {
                int bonus = Math.Min(3, skillCost);
                if (bonus <= 0) return;
                unit.TurnDamageBonus += bonus;
                Log?.Invoke($"[C03] 루트 — 아군 스킬 대상, 데미지 +{bonus}");
            });

            // C04 궁사 아마리요 — [3 코스트] 엑셀 획득
            RegisterTrigger("C04", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (!ctx.Game.ShouldActivate(ctx.Self) || !ctx.Owner.PayCost(3)) return;
                ctx.Self.AccelTokens++;
                Log?.Invoke("[C04] 아마리요 — 3 코스트, 엑셀 획득");
            });

            // C12 피의 검 아르케아 — 턴 종료 시 [Cost 3] 트래쉬 존의 피 카드당 데미지 1 (최대 5)
            //  ※ 대상이 원문에 없음 → 리치 내 가장 앞선 적에게 직접 데미지로 해석 (프로토타입과 동일)
            RegisterTrigger("C12", EffectTiming.TurnEnd, ctx =>
            {
                int blood = EffectSystem.CountBloodCardsInTrash(ctx.Owner);
                if (blood == 0 || !ctx.Game.ShouldActivate(ctx.Self)) return;

                var target = LivingAllies(ctx.Opponent)
                    .Where(u => ctx.Self.CanReach(u.Position))
                    .OrderBy(u => u.Position)
                    .FirstOrDefault();
                if (target == null || !ctx.Owner.PayCost(3)) return;

                int damage = Math.Min(5, blood);
                int dealt = target.TakeDamage(damage);
                ctx.Opponent.AddDamageTaken(dealt);
                Log?.Invoke($"[C12] 아르케아 — 턴 종료, {target.Data.Name}에게 {dealt} 데미지");
            });

            // C16 주정뱅이 회복술사 — HP 1 소모 후 아군 HP 2 회복 (가장 다친 다른 아군)
            RegisterTrigger("C16", EffectTiming.ActionPhaseBefore, ctx =>
            {
                var wounded = LivingAllies(ctx.Owner)
                    .Where(u => u != ctx.Self && u.CurrentHp < u.Data.MaxHp)
                    .OrderBy(u => u.CurrentHp)
                    .FirstOrDefault();
                if (wounded == null || !ctx.Game.ShouldActivate(ctx.Self)) return;
                if (!ctx.Self.PayHp(1)) return;

                wounded.Heal(2);
                Log?.Invoke($"[C16] 회복술사 — HP 1 지불, {wounded.Data.Name} HP 2 회복");
            });

            // C19 핏빛 날개 레비아 — [Cost 2] 아군 데미지 2 증가
            //  ※ 발동 횟수 제한이 원문에 없어 턴 1회로 가정 (프로토타입과 동일)
            RegisterTrigger("C19", EffectTiming.ActionPhaseBefore, ctx =>
            {
                if (ctx.Self.EffectUsedThisTurn || !ctx.Game.ShouldActivate(ctx.Self)) return;
                if (!ctx.Owner.PayCost(2)) return;

                ctx.Self.EffectUsedThisTurn = true;
                ctx.Game.AddNextSkillDamageBonus(ctx.Owner.PlayerId, 2);
                Log?.Invoke("[C19] 레비아 — 2 코스트, 다음 아군 스킬 데미지 +2");
            });

            // C21 광신도 — [HP-?] 소모한 HP만큼 코스트로 획득 (HP는 1 이하가 될 수 없다)
            //  ※ effectTiming은 "카드 스킬 발동 전"이지만 코스트 확보는 레디 페이즈 전에 끝나야
            //     쓸모가 있어 그쪽으로 해석 (프로토타입과 동일). 변환량은 VariableCostPolicy에 위임.
            RegisterTrigger("C21", EffectTiming.ReadyPhaseBefore, ctx =>
            {
                if (!ctx.Game.ShouldActivate(ctx.Self)) return;

                int maxConvertible = ctx.Self.CurrentHp - 1; // HP 1 미만이 될 수 없음 (RULES.md 11번)
                int convert = ctx.Game.DecideVariableCost(ctx.Owner, maxConvertible);
                if (convert <= 0 || !ctx.Self.PayHp(convert)) return;

                ctx.Owner.TempCost += convert;
                Log?.Invoke($"[C21] 광신도 — HP {convert} → 임시 코스트 {convert}");
            });

            // C24 피로 물든 파수꾼 레안 — 활 스킬 사용 가능. 활 스킬 사용 시 리치 +1
            //  리치 +1은 EffectSystem.DealDamage에서 처리한다
            RegisterTrigger("C24", EffectTiming.ActionPhaseBefore, ctx =>
            {
                if (ctx.Self.ExtraWeapons.Contains("활")) return;
                ctx.Self.ExtraWeapons.Add("활");
                Log?.Invoke("[C24] 레안 — 활 스킬 사용 가능");
            });

            // C25 피의 사신 실비아 — 배틀 페이즈 개시 시 리치 1 감소, 데미지 2 증가
            //  ※ "영구"로 해석하고 게임 중 1회만 적용 (프로토타입과 동일)
            RegisterTrigger("C25", EffectTiming.ActionPhaseBefore, ctx =>
            {
                if (!ctx.Self.OneTimeFlags.Add("C25")) return;
                ctx.Self.ReachBonus -= 1;
                ctx.Self.PermanentDamageBonus += 2;
                Log?.Invoke("[C25] 실비아 — 리치 -1, 데미지 +2 (영구)");
            });

            // C29 핏빛 그림자 리퍼 — [Cost 3] 트래쉬 존에 피 카드가 4장 이상이면 데미지 +3
            RegisterTrigger("C29", EffectTiming.ActionPhaseBefore, ctx =>
            {
                if (EffectSystem.CountBloodCardsInTrash(ctx.Owner) < 4) return;
                if (!ctx.Game.ShouldActivate(ctx.Self) || !ctx.Owner.PayCost(3)) return;

                ctx.Self.TurnDamageBonus += 3;
                Log?.Invoke("[C29] 리퍼 — 피 카드 4장 이상, 데미지 +3");
            });
        }
    }
}
