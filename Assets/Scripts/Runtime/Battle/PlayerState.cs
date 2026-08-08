using System;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Data;

namespace CrossAccel.Battle
{
    /// <summary>
    /// 한 플레이어의 존(zone) 상태. RULES.md 2번 존 구성을 그대로 반영한다:
    /// 캐릭터 존 / 액티브 존 / 코스트 존 / 메인덱 존 / 트래쉬 존 (+ 손패).
    /// 존 사이의 카드 이동(드로우, 재순환, 셔플, 코스트 지불)만 담당하고, 페이즈 진행·효과 적용은
    /// GameManager의 몫이다.
    /// </summary>
    public class PlayerState
    {
        private readonly Random _rng;

        /// <summary>플레이어 식별자 (0 또는 1).</summary>
        public int PlayerId { get; }

        /// <summary>캐릭터 존 — 배치된 파티 (RULES.md 4번: 최대 4장).</summary>
        public List<CharacterUnit> CharacterZone { get; } = new List<CharacterUnit>();

        /// <summary>밴픽에서 픽되지 않고 남은 캐릭터 카드 4장 — HP 표시용 (RULES.md 4번 세팅).</summary>
        public List<string> HpCounterCards { get; } = new List<string>();

        /// <summary>손패.</summary>
        public List<SkillData> Hand { get; } = new List<SkillData>();

        /// <summary>메인덱 존.</summary>
        public List<SkillData> Deck { get; } = new List<SkillData>();

        /// <summary>트래쉬 존.</summary>
        public List<SkillData> Trash { get; } = new List<SkillData>();

        /// <summary>코스트 존 (뒷면으로 배치, 레스트하여 사용).</summary>
        public List<CostCard> CostZone { get; } = new List<CostCard>();

        /// <summary>액티브 존 (액션 페이즈에 사용할 카드).</summary>
        public List<ActiveSlot> ActiveZone { get; } = new List<ActiveSlot>();

        /// <summary>난수는 시드 주입 (ARCHITECTURE.md: 결정론성 원칙, UnityEngine.Random 금지).</summary>
        public PlayerState(Random rng, int playerId = 0)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            PlayerId = playerId;
        }

        /// <summary>
        /// 패배 여부 — 캐릭터 존에 캐릭터 카드가 전부 없어졌는가 (RULES.md 1번/5-4).
        /// 사망 유닛은 엔드 페이즈에 코스트존으로 옮겨진 뒤 이 판정을 하므로, 존이 비었는지만 보면 된다.
        /// </summary>
        public bool HasLost => CharacterZone.Count == 0;

        /// <summary>
        /// 카드가 아닌 효과로 얻은 임시 코스트 (C21 광신도가 HP를 변환해 넣는다).
        /// 지속 기간이 RULES.md에 없어 이번 턴 한정으로 가정하고 턴 시작 시 0으로 되돌린다.
        /// </summary>
        public int TempCost { get; set; }

        /// <summary>이번 턴에 이 플레이어의 캐릭터들이 받은 총 데미지 (S13 카운터용). 턴 시작 시 리셋.</summary>
        public int DamageTakenThisTurn { get; set; }

        /// <summary>지금 지불 가능한 코스트 = 언레스트된 코스트 카드 수 + 임시 코스트 (RULES.md 2번).</summary>
        public int AvailableCost => CostZone.Count(c => !c.IsRested) + TempCost;

        /// <summary>주어진 리스트를 제자리에서 Fisher-Yates 셔플한다.</summary>
        public void Shuffle(List<SkillData> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// 덱에서 count장을 뽑아 손패로 옮긴다. 덱이 0장이 되면 트래쉬를 셔플해 메인덱으로
        /// 재순환한 뒤 계속 뽑는다 (RULES.md 3번). 트래쉬까지 비어있으면 더 뽑지 않고 멈춘다.
        /// </summary>
        public List<SkillData> Draw(int count)
        {
            var drawn = new List<SkillData>();
            for (int i = 0; i < count; i++)
            {
                if (Deck.Count == 0)
                    RecycleTrashIntoDeck();

                if (Deck.Count == 0)
                    break; // 덱·트래쉬 모두 비어 더 뽑을 카드 없음

                var card = Deck[0];
                Deck.RemoveAt(0);
                Hand.Add(card);
                drawn.Add(card);
            }
            return drawn;
        }

        private void RecycleTrashIntoDeck()
        {
            if (Trash.Count == 0) return;

            Deck.AddRange(Trash);
            Trash.Clear();
            Shuffle(Deck);
        }

        /// <summary>
        /// 챌린지용 덱 탑 공개. RULES.md R8: 챌린지 중에는 트래쉬 재순환이 없으므로,
        /// 덱이 비어있으면 재순환하지 않고 null을 반환한다 (호출자가 자동 패배로 처리).
        /// RULES.md R5: 공개한 카드는 트래쉬로 간다.
        /// </summary>
        public SkillData RevealTopForChallenge()
        {
            if (Deck.Count == 0) return null;

            var card = Deck[0];
            Deck.RemoveAt(0);
            Trash.Add(card);
            return card;
        }

        // ---------------- 코스트 ----------------

        /// <summary>손패의 카드를 코스트존에 뒷면으로 배치한다 (RULES.md 4번 준비 / 5-1 드로우 페이즈).</summary>
        public bool PlaceCostFromHand(SkillData card)
        {
            if (card == null || !Hand.Remove(card)) return false;

            CostZone.Add(new CostCard(card.Id));
            return true;
        }

        /// <summary>
        /// 사망한 캐릭터 카드를 코스트존에 배치한다 (RULES.md 5-4 엔드 페이즈).
        /// 레스트 여부는 RULES.md 11번 임시 가정에 따라 언레스트로 들어간다.
        /// </summary>
        public void PlaceCharacterInCostZone(string characterId) =>
            CostZone.Add(new CostCard(characterId));

        /// <summary>
        /// 코스트를 지불한다. 지불할 만큼 여력이 없으면 아무것도 꺾지 않고 false
        /// — 호출자는 그 카드 효과를 불발 처리해야 한다 (RULES.md 7번 코스트).
        /// 임시 코스트를 먼저 쓰고, 모자란 만큼 코스트존 카드를 꺾는다.
        /// </summary>
        public bool PayCost(int amount)
        {
            if (amount <= 0) return true;
            if (AvailableCost < amount) return false;

            int fromTemp = Math.Min(TempCost, amount);
            TempCost -= fromTemp;

            int remaining = amount - fromTemp;
            foreach (var card in CostZone)
            {
                if (remaining == 0) break;
                if (card.IsRested) continue;

                card.IsRested = true;
                remaining--;
            }
            return true;
        }

        /// <summary>이번 턴 받은 데미지를 누적한다 (S13 카운터가 참조).</summary>
        public void AddDamageTaken(int amount)
        {
            if (amount > 0) DamageTakenThisTurn += amount;
        }

        /// <summary>매 턴 시작 시 레스트된 코스트 카드를 전부 다시 세운다 (RULES.md R2).</summary>
        public void UnrestAllCost()
        {
            foreach (var card in CostZone)
                card.IsRested = false;
        }

        /// <summary>턴 시작 리셋 — 코스트 언레스트(R2) + 턴 한정 효과 상태 초기화.</summary>
        public void ResetForNewTurn()
        {
            UnrestAllCost();
            TempCost = 0;
            DamageTakenThisTurn = 0;

            foreach (var unit in CharacterZone)
                unit.ResetTurnState();
        }
    }
}
