using System;
using System.Collections.Generic;
using CrossAccel.Data;

namespace CrossAccel.Battle
{
    /// <summary>
    /// 필드에 배치된 캐릭터의 런타임 상태. CharacterData(원본, 불변)를 감싸고
    /// 변하는 값(HP, 방어도, 리치 보정)만 여기서 관리한다 (CLAUDE.md 원칙 3).
    /// </summary>
    public class CharacterUnit
    {
        /// <summary>원본 캐릭터 데이터 (읽기 전용).</summary>
        public CharacterData Data { get; }

        /// <summary>소유 플레이어 식별자 (Phase 3에서 캐릭터 존/타겟팅에 사용).</summary>
        public int OwnerId { get; }

        /// <summary>캐릭터 존 내 위치. 0 = 최전선, 3 = 최후방 (RULES.md R4).</summary>
        public int Position { get; set; }

        /// <summary>현재 HP. 0 이하면 사망.</summary>
        public int CurrentHp { get; private set; }

        /// <summary>현재 방어도. 턴 종료 시 소멸 (RULES.md R3).</summary>
        public int Defense { get; private set; }

        /// <summary>효과로 부여되는 리치 보정치 (기본 0). 최종 리치 = Data.Reach + ReachBonus.</summary>
        public int ReachBonus { get; set; }

        // ---------------- 카드 효과용 런타임 상태 (Phase 4) ----------------

        /// <summary>이번 턴에만 유효한 스킬 데미지 보너스. 턴 시작 시 리셋 (C41, C02, C29 등).</summary>
        public int TurnDamageBonus { get; set; }

        /// <summary>게임 내내 유지되는 데미지 보너스 (C25 실비아).</summary>
        public int PermanentDamageBonus { get; set; }

        /// <summary>이번 턴에 공격한 횟수. 턴 시작 시 리셋 (C15 가르샤의 "두 번 이상 공격" 조건).</summary>
        public int AttacksThisTurn { get; set; }

        /// <summary>"턴에 한번" 제한이 붙은 효과를 이번 턴에 이미 썼는지 (C14, C31, C19).</summary>
        public bool EffectUsedThisTurn { get; set; }

        /// <summary>
        /// 보유한 엑셀 토큰 수 (C07, C14, C04가 부여).
        /// ⚠️ RULES.md는 엑셀 지정에 토큰이 필요하다고 정하지 않았으므로, 카운터만 유지하고
        /// SubmitActiveCard를 제한하지는 않는다. 소모 규칙이 확정되면 그때 게이트를 건다.
        /// </summary>
        public int AccelTokens { get; set; }

        /// <summary>원래 무기 외에 추가로 쓸 수 있게 된 무기 (C24 레안: 활).</summary>
        public List<string> ExtraWeapons { get; } = new List<string>();

        /// <summary>게임 중 1회만 적용되는 효과의 기록 (C25).</summary>
        public HashSet<string> OneTimeFlags { get; } = new HashSet<string>();

        public CharacterUnit(CharacterData data, int ownerId, int position)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            OwnerId = ownerId;
            Position = position;
            CurrentHp = data.MaxHp;
            Defense = 0;
            ReachBonus = 0;
        }

        /// <summary>턴 시작 시 리셋되는 상태 (턴 한정 버프·카운터).</summary>
        public void ResetTurnState()
        {
            TurnDamageBonus = 0;
            AttacksThisTurn = 0;
            EffectUsedThisTurn = false;
        }

        /// <summary>HP가 0 이하면 사망.</summary>
        public bool IsDead => CurrentHp <= 0;

        /// <summary>최종 리치 (원본 리치 + 버프 보정).</summary>
        public int EffectiveReach => Data.Reach + ReachBonus;

        /// <summary>
        /// 거리 공식 (RULES.md R4): 공격자 위치 + 대상 위치 + 1.
        /// 예) 내0→적0=1, 내0→적3=4, 내3→적3=7(최대).
        /// </summary>
        public static int CalculateDistance(int attackerPosition, int targetPosition) =>
            attackerPosition + targetPosition + 1;

        /// <summary>대상 위치까지 이 캐릭터의 리치가 닿는지 (거리 &lt;= EffectiveReach).</summary>
        public bool CanReach(int targetPosition) =>
            CalculateDistance(Position, targetPosition) <= EffectiveReach;

        /// <summary>대상 캐릭터까지 리치가 닿는지.</summary>
        public bool CanReach(CharacterUnit target) => CanReach(target.Position);

        /// <summary>방어도를 더한다 (음수/0은 무시).</summary>
        public void AddDefense(int amount)
        {
            if (amount <= 0) return;
            Defense += amount;
        }

        /// <summary>턴 종료 시 방어도를 소멸시킨다 (RULES.md R3).</summary>
        public void ClearDefense() => Defense = 0;

        /// <summary>
        /// 데미지를 받는다. 관통(piercing)이 아니면 방어도가 먼저 흡수하고, 남는 만큼만 HP에 적용된다.
        /// 관통이면 방어도를 무시하고 HP에 직접 적용한다 (RULES.md 7번 키워드: 관통).
        /// </summary>
        /// <returns>실제로 잃은 HP (방어도가 흡수한 분과 초과 데미지는 제외). S13 카운터가 이 값을 쓴다.</returns>
        public int TakeDamage(int amount, bool piercing = false)
        {
            if (amount <= 0) return 0;

            int remaining = amount;
            if (!piercing && Defense > 0)
            {
                int absorbed = Math.Min(Defense, remaining);
                Defense -= absorbed;
                remaining -= absorbed;
            }

            if (remaining <= 0) return 0;

            int before = CurrentHp;
            CurrentHp = Math.Max(0, CurrentHp - remaining);
            return before - CurrentHp;
        }

        /// <summary>HP를 회복한다. 최대 HP를 넘지 않는다.</summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            CurrentHp = Math.Min(Data.MaxHp, CurrentHp + amount);
        }

        /// <summary>
        /// HP를 비용으로 지불한다. 지불 후 HP가 1 미만이 되면 실패하고 상태를 바꾸지 않는다
        /// (RULES.md R11: HP 비용으로 자멸 불가).
        /// </summary>
        public bool PayHp(int amount)
        {
            if (amount <= 0) return true;
            if (CurrentHp - amount < 1) return false;

            CurrentHp -= amount;
            return true;
        }

        /// <summary>
        /// HP를 비용으로 지불하되 모자라면 HP 1을 남기고 낼 수 있는 만큼만 낸다 (지불 실패가 없다).
        /// ⚠️ RULES.md R11(자멸 불가·지불 실패)의 **예외**이며, 현재 S84 "마지막 저항" skill1 전용이다.
        /// 다른 카드의 HP 비용은 <see cref="PayHp"/>를 쓸 것.
        /// </summary>
        /// <returns>실제로 지불한 HP.</returns>
        public int PayHpDownToOne(int amount)
        {
            if (amount <= 0) return 0;

            int paid = Math.Min(amount, Math.Max(0, CurrentHp - 1));
            CurrentHp -= paid;
            return paid;
        }
    }
}
