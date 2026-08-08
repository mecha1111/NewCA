using System;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Data;

namespace CrossAccel.Effects
{
    /// <summary>
    /// 스킬 카드 효과: 카드 id → C# 핸들러. 효과 텍스트는 자연어라 파싱하지 않는다 (CLAUDE.md 원칙 2).
    /// 미등록 카드는 경고만 남기고 스킵하므로 게임이 멈추지 않는다.
    ///
    /// 정적 상태를 쓰지 않는다 — 인스턴스로 만들어 GameManager에 붙인다.
    /// </summary>
    public class EffectSystem
    {
        private readonly Dictionary<string, Action<EffectContext>> _handlers =
            new Dictionary<string, Action<EffectContext>>();

        private readonly CharacterEffectSystem _characterEffects;

        /// <summary>미구현 카드 경고 등 진행 로그. GameManager.Log와 같은 대상을 연결해 쓴다.</summary>
        public Action<string> Log;

        /// <summary>스킵된(미구현) 카드 id 기록 — 테스트·데이터 점검용.</summary>
        public HashSet<string> SkippedCardIds { get; } = new HashSet<string>();

        public EffectSystem(CharacterEffectSystem characterEffects = null)
        {
            _characterEffects = characterEffects;
        }

        public void Register(string cardId, Action<EffectContext> handler) => _handlers[cardId] = handler;

        public bool IsImplemented(string cardId) => _handlers.ContainsKey(cardId);

        /// <summary>효과를 실행한다. 미구현이면 경고 로그 후 false를 반환하고 게임은 계속 진행된다.</summary>
        public bool Execute(EffectContext ctx)
        {
            if (_handlers.TryGetValue(ctx.Skill.Id, out var handler))
            {
                handler(ctx);
                return true;
            }

            SkippedCardIds.Add(ctx.Skill.Id);
            Log?.Invoke($"[EffectSystem] 미구현 효과: {ctx.Skill.Id} {ctx.Skill.Name} — 스킵");
            return false;
        }

        // =====================================================================
        //  공통 데미지 처리
        // =====================================================================

        /// <summary>
        /// 공통 데미지 처리 — 리치 검사(R4), 버프 합산, 관통(RULES.md 7번 키워드) 적용을 한곳에서 한다.
        /// 리치가 모자라면 공격이 실패하고 아무 일도 일어나지 않는다.
        /// </summary>
        /// <returns>실제로 준 데미지. 리치 부족·대상 없음이면 0.</returns>
        public int DealDamage(EffectContext ctx, int baseAmount)
        {
            var target = ctx.Target;
            if (target == null || target.IsDead)
            {
                Log?.Invoke($"[{ctx.Skill.Id}] 유효한 대상 없음 — 스킵");
                return 0;
            }

            // 리치 검사 (RULES.md R4). C24 레안은 활 스킬을 쓸 때 리치가 1 늘어난다.
            int reachBonus = BowReachBonus(ctx.User, ctx.Skill);
            int distance = CharacterUnit.CalculateDistance(ctx.User.Position, target.Position);
            if (ctx.User.EffectiveReach + reachBonus < distance)
            {
                Log?.Invoke($"[{ctx.Skill.Id}] 리치 부족 (거리 {distance} > 리치 {ctx.User.EffectiveReach + reachBonus}) — 공격 실패");
                return 0;
            }

            int characterBonus = _characterEffects?.GetDamageBonus(new DamageModContext
            {
                Game = ctx.Game,
                Attacker = ctx.User,
                Target = target,
                Owner = ctx.Owner,
                SkillCost = Math.Max(0, ctx.Skill.Skill1Cost)
            }) ?? 0;

            int total = baseAmount
                        + ctx.DamageBonus
                        + ctx.User.TurnDamageBonus
                        + ctx.User.PermanentDamageBonus
                        + characterBonus;

            int dealt = target.TakeDamage(Math.Max(0, total), ctx.Piercing);

            ctx.User.AttacksThisTurn++;
            ctx.Game?.Players[target.OwnerId].AddDamageTaken(dealt);
            return dealt;
        }

        /// <summary>C24 피로 물든 파수꾼 레안 — 활 스킬 사용 시 리치 +1.</summary>
        private static int BowReachBonus(CharacterUnit user, SkillData skill)
        {
            if (user.Data.Id != "C24") return 0;
            return WeaponTypeParser.Parse(skill.WeaponType).Contains("활") ? 1 : 0;
        }

        // =====================================================================
        //  피 카드
        // =====================================================================

        /// <summary>
        /// '피 카드' id 목록 (RULES.md R13, 사용자 확정). 이름 검색 대신 명시적 id 집합으로 판별한다 —
        /// 이름에 "피"/"핏빛"이 들어가는 카드가 84종 전체로 보면 이 10종 밖에도 있어(S37, S47 등)
        /// 이름 검색은 오탐을 낸다.
        /// </summary>
        public static readonly IReadOnlyCollection<string> BloodCardIds = new HashSet<string>
        {
            "S55", "S57", "S59", "S77", "S78", "S79", "S80", "S81", "S82", "S83"
        };

        /// <summary>'피 카드' 판별 — id가 <see cref="BloodCardIds"/>에 속하면 피 카드 (RULES.md R13).</summary>
        public static bool IsBloodCard(SkillData skill) =>
            skill != null && BloodCardIds.Contains(skill.Id);

        /// <summary>트래쉬 존의 피 카드 수.</summary>
        public static int CountBloodCardsInTrash(PlayerState player) => player.Trash.Count(IsBloodCard);

        /// <summary>덱에서 피 카드 한 장을 찾아 트래쉬로 보낸다 (덤핑). 없으면 아무 일도 안 한다.</summary>
        private static SkillData DumpBloodCardFromDeck(PlayerState player)
        {
            int index = player.Deck.FindIndex(IsBloodCard);
            if (index < 0) return null;

            var card = player.Deck[index];
            player.Deck.RemoveAt(index);
            player.Trash.Add(card);
            return card;
        }

        // =====================================================================
        //  스타터 덱 스킬 등록
        // =====================================================================

        /// <summary>
        /// 스타터 덱 2종(Aggro / MidRange_Blood)에 들어있는 스킬만 등록한다.
        /// 범위 밖 카드는 구현하지 않는다 (CLAUDE.md 하지 말 것).
        /// </summary>
        public void RegisterStarterDeckSkills()
        {
            RegisterAggroSkills();
            RegisterMidRangeSkills();
        }

        private void RegisterAggroSkills()
        {
            // S01 치고 빠지기 — 데미지 2, 이전에 사용된 스킬이 있으면 실패
            //  "이전에 사용된 스킬" = 이번 턴 자신이 이미 발동한 스킬 (프로토타입 해석)
            Register("S01", ctx =>
            {
                if (ctx.Game.SkillsResolvedThisTurn[ctx.Owner.PlayerId] > 0)
                {
                    Log?.Invoke("[S01] 이번 턴에 이미 발동한 스킬이 있음 — 실패");
                    return;
                }
                DealDamage(ctx, 2);
            });

            // S06 회복 — HP 1 회복 (대상 미명시 → 사용 캐릭터 자신, 사용자 확정)
            Register("S06", ctx => ctx.User.Heal(1));

            // S20 잔혹한 운명 — 종족 전용. 노예: 데미지 8 + 자신 사망 / 추격자: 데미지 8 + 아군 전체 2 데미지
            //  skill1/skill2 중 무엇을 쓰는지는 사용 캐릭터의 종족으로 갈린다 (원문의 "종족 전용" 표기)
            Register("S20", ctx =>
            {
                string race = ctx.User.Data.Race;
                if (race != "노예" && race != "추격자")
                {
                    Log?.Invoke($"[S20] 종족 조건 불충족 ({race}) — 실패");
                    return;
                }

                DealDamage(ctx, 8);

                if (race == "노예")
                {
                    ctx.User.TakeDamage(ctx.User.CurrentHp, piercing: true); // 자신 사망
                }
                else
                {
                    foreach (var ally in ctx.Owner.CharacterZone.Where(u => !u.IsDead).ToList())
                        ally.TakeDamage(2, piercing: true);
                }
            });

            // S25 신념의 일격 — 데미지 2
            Register("S25", ctx => DealDamage(ctx, 2));

            // S26 해방의 분노 — 데미지 3
            Register("S26", ctx => DealDamage(ctx, 3));

            // S53 바드의 분노 — 리치 내 캐릭터에게 데미지 1, 그 후 [버프] 1턴 동안 데미지 +2
            //  ※ "리치 내 캐릭터"의 범위가 원문에 없음 → 데미지를 주는 카드이므로 리치 내 '적' 전체로 해석.
            //     버프 대상도 미명시 → 사용 캐릭터 본인으로 해석 (RULES.md 11번에 기록).
            Register("S53", ctx =>
            {
                foreach (var enemy in ctx.Opponent.CharacterZone.Where(u => !u.IsDead).ToList())
                {
                    if (!ctx.User.CanReach(enemy.Position)) continue;
                    enemy.TakeDamage(1);
                    ctx.Game?.Players[enemy.OwnerId].AddDamageTaken(1);
                }
                ctx.User.TurnDamageBonus += 2;
            });

            // S74 고독한 결의 — 데미지 1, HP 회복 1 (회복 대상: 사용 캐릭터)
            Register("S74", ctx =>
            {
                DealDamage(ctx, 1);
                ctx.User.Heal(1);
            });

            // S75 반란의 시작 — 데미지 1, 2회 발동
            Register("S75", ctx =>
            {
                for (int i = 0; i < 2; i++) DealDamage(ctx, 1);
            });

            // S76 어수선한 전투 — 데미지 1, 3회 발동
            Register("S76", ctx =>
            {
                for (int i = 0; i < 3; i++) DealDamage(ctx, 1);
            });

            // S84 마지막 저항 — 두 선택지 (사용자 확정 해석, RULES.md 11번 표)
            //  skill1 [코스트 0]: [아군 전체 HP-3] → 그 후 HP 3 이하가 된 노예/추격자에게 이번 턴 데미지 +2
            //  skill2 [코스트 2]: [사용자 HP-1] 카드를 두 장 드로우
            Register("S84", ctx =>
            {
                if (ctx.SkillOption == 2)
                {
                    // skill2의 HP 비용은 일반 규칙(R11) — HP 1 미만이 되면 지불 실패
                    if (!ctx.User.PayHp(1))
                    {
                        Log?.Invoke("[S84-2] HP 1 지불 불가 — 실패");
                        return;
                    }
                    ctx.Game.DrawToHand(ctx.Owner.PlayerId, 2);
                    return;
                }

                // skill1의 "아군 전체 HP-3"은 R11의 예외로 HP 1을 바닥으로 두고 지불된다 (자멸 없음).
                foreach (var ally in ctx.Owner.CharacterZone.Where(u => !u.IsDead).ToList())
                    ally.PayHpDownToOne(3);

                foreach (var ally in ctx.Owner.CharacterZone.Where(u => !u.IsDead))
                {
                    bool eligibleRace = ally.Data.Race == "노예" || ally.Data.Race == "추격자";
                    if (eligibleRace && ally.CurrentHp <= 3)
                        ally.TurnDamageBonus += 2;
                }
            });
        }

        private void RegisterMidRangeSkills()
        {
            // S07 공격 — 데미지 2
            Register("S07", ctx => DealDamage(ctx, 2));

            // S08 시원한 맥주 — HP 2 회복 (사용 캐릭터)
            Register("S08", ctx => ctx.User.Heal(2));

            // S10 의자 던지기 — 데미지 2
            Register("S10", ctx => DealDamage(ctx, 2));

            // S13 카운터 — 이번 턴에 받은 데미지를 리치 내의 적 1명에게 줌
            Register("S13", ctx =>
            {
                int taken = ctx.Owner.DamageTakenThisTurn;
                if (taken <= 0)
                {
                    Log?.Invoke("[S13] 이번 턴에 받은 데미지 없음 — 효과 없음");
                    return;
                }
                DealDamage(ctx, taken);
            });

            // S14 방어 — 방어도 3 (대상 미명시 → 사용 캐릭터)
            Register("S14", ctx => ctx.User.AddDefense(3));

            // S55 핏빛 가시 — 적군 3명 동시타격, 가운데 데미지 2 / 사이드 데미지 1
            //  ※ "가운데/사이드"의 정의가 원문에 없음. 프로토타입 해석 채택:
            //     선택한 대상이 가운데, Position이 ±1인 적이 사이드.
            Register("S55", ctx =>
            {
                if (ctx.Target == null) return;

                int center = ctx.Target.Position;
                foreach (var enemy in ctx.Opponent.CharacterZone.Where(u => !u.IsDead).ToList())
                {
                    int offset = Math.Abs(enemy.Position - center);
                    if (offset > 1) continue;

                    if (offset == 0) DealDamage(ctx, 2);
                    else
                    {
                        enemy.TakeDamage(1);
                        ctx.Game?.Players[enemy.OwnerId].AddDamageTaken(1);
                    }
                }
            });

            // S57 갈망하는 피 — 턴 종료 시 덱에서 피 카드 한 장 덤핑
            //  ※ "턴 종료 시" 예약 구조가 아직 없어 즉시 덤핑으로 단순화 (프로토타입과 동일).
            Register("S57", ctx =>
            {
                var dumped = DumpBloodCardFromDeck(ctx.Owner);
                Log?.Invoke(dumped != null ? $"[S57] 피 카드 덤핑: {dumped.Name}" : "[S57] 덱에 피 카드 없음");
            });

            // S77 피의 손길 — 데미지 2, 방어도 2 획득
            Register("S77", ctx =>
            {
                DealDamage(ctx, 2);
                ctx.User.AddDefense(2);
            });

            // S78 피의 마도서 — 트래쉬 존의 피 카드당 아군 HP 1 회복 (최대 7)
            //  회복 대상은 미명시 → 사용 캐릭터 (사용자 확정), 상한은 보너스에만 적용
            Register("S78", ctx =>
            {
                int heal = Math.Min(7, CountBloodCardsInTrash(ctx.Owner));
                if (heal <= 0) return;
                ctx.User.Heal(heal);
            });

            // S79 피의 계약 — 소모한 코스트당 아군 HP 회복 (X 코스트, 데이터 미기재)
            Register("S79", ctx =>
            {
                int x = ctx.Game.DecideVariableCost(ctx.Owner, ctx.Owner.AvailableCost);
                if (x <= 0 || !ctx.Owner.PayCost(x)) return;

                var wounded = ctx.Owner.CharacterZone
                    .Where(u => !u.IsDead && u.CurrentHp < u.Data.MaxHp)
                    .OrderBy(u => u.CurrentHp)
                    .FirstOrDefault() ?? ctx.User;

                wounded.Heal(x);
                ctx.Game.CharacterEffects?.FireAllyTargeted(wounded, x); // C03 루트 트리거
            });

            // S80 핏빛 탄환 — 데미지 2, 트래쉬 피 카드 장당 데미지 +1 (최대 5)
            Register("S80", ctx =>
                DealDamage(ctx, 2 + Math.Min(5, CountBloodCardsInTrash(ctx.Owner))));

            // S81 피의 축복 — 데미지 2, HP 2 회복 (HP가 최대면 방어도로 전환)
            Register("S81", ctx =>
            {
                DealDamage(ctx, 2);
                if (ctx.User.CurrentHp >= ctx.User.Data.MaxHp) ctx.User.AddDefense(2);
                else ctx.User.Heal(2);
            });

            // S82 피의 활시위 — 데미지 3, 이번 턴 보유 코스트 2당 데미지 +1
            //  ※ "보유 코스트"를 코스트존 총 매수로 해석 (레스트 여부와 무관 — 이 카드 자신의 지불로
            //     수치가 흔들리지 않게). RULES.md 11번에 기록.
            Register("S82", ctx => DealDamage(ctx, 3 + ctx.Owner.CostZone.Count / 2));

            // S83 피의 참격 — 데미지 6
            Register("S83", ctx => DealDamage(ctx, 6));

            // S59 핏빛 칼날 — 데미지 2, 덱에서 피 카드 한 장 덤핑 (활 캐릭터가 쓰면 데미지 +1, 리치 -1)
            Register("S59", ctx =>
            {
                bool isBowUser = WeaponTypeParser.Parse(ctx.User.Data.WeaponType).Contains("활");

                if (isBowUser) ctx.User.ReachBonus -= 1;
                try
                {
                    DealDamage(ctx, isBowUser ? 3 : 2);
                }
                finally
                {
                    if (isBowUser) ctx.User.ReachBonus += 1; // 리치 -1은 이 공격에만 적용
                }

                DumpBloodCardFromDeck(ctx.Owner);
            });
        }
    }
}
