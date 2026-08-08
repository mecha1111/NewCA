using System;
using System.Collections.Generic;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;
using NUnit.Framework;

namespace CrossAccel.Tests
{
    /// <summary>
    /// Phase 2 — Core 상수 + CharacterUnit/PlayerState 규칙 검증.
    /// TESTING.md 2단계: R1~R4, 관통, 드로우 재순환 O / 챌린지 재순환 X.
    /// docs/RULES.md 확정본과 수치를 직접 대조한다.
    /// </summary>
    public class RulesTests
    {
        private static CharacterData MakeCharacter(int reach, int maxHp = 15) => new CharacterData
        {
            Id = "T00",
            Name = "테스트",
            Race = "인간",
            WeaponType = "한손검",
            MaxHp = maxHp,
            Reach = reach,
            EffectTiming = "",
            EffectText = ""
        };

        private static SkillData MakeSkill(string id) => new SkillData
        {
            Id = id,
            Name = id,
            WeaponType = "한손검",
            Speed = 1,
            Skill1Cost = 1,
            Skill1Effect = "테스트 효과"
        };

        // ---------------- GameRules 상수 대조 (RULES.md) ----------------

        [Test]
        public void GameRules_CharacterDeckSize_Is10()
        {
            // RULES.md 3번: 캐릭터 덱 = 10장
            Assert.AreEqual(10, GameRules.CharacterDeckSize);
        }

        [Test]
        public void GameRules_MainDeckSize_Is24()
        {
            // RULES.md 3번: 메인 덱은 총 24장
            Assert.AreEqual(24, GameRules.MainDeckSize);
        }

        [Test]
        public void GameRules_PartySize_Is4()
        {
            // RULES.md 4번 세팅: 배치 4 + HP표시 4
            Assert.AreEqual(4, GameRules.PartySize);
        }

        [Test]
        public void GameRules_MaxHp_Is15()
        {
            // RULES.md 3번: HP(최대 15)
            Assert.AreEqual(15, GameRules.MaxHp);
        }

        [Test]
        public void GameRules_MaxReach_Is7()
        {
            // RULES.md 3번/R4: 리치(최대 7)
            Assert.AreEqual(7, GameRules.MaxReach);
        }

        // ---------------- R4: 리치/거리 (CanReach) ----------------

        [Test]
        public void CanReach_FrontToFront_DistanceIsOneAndSucceeds()
        {
            // 내0 → 적0 = 0+0+1 = 1
            var unit = new CharacterUnit(MakeCharacter(reach: 1), ownerId: 0, position: 0);
            Assert.AreEqual(1, CharacterUnit.CalculateDistance(0, 0));
            Assert.IsTrue(unit.CanReach(0));
        }

        [Test]
        public void CanReach_FrontToBack_DistanceIsFour()
        {
            // 내0 → 적3 = 0+3+1 = 4
            var unit = new CharacterUnit(MakeCharacter(reach: 4), ownerId: 0, position: 0);
            Assert.AreEqual(4, CharacterUnit.CalculateDistance(0, 3));
            Assert.IsTrue(unit.CanReach(3));
        }

        [Test]
        public void CanReach_BackToBack_DistanceIsSeven()
        {
            // 내3 → 적3 = 3+3+1 = 7 (최대)
            var unit = new CharacterUnit(MakeCharacter(reach: 7), ownerId: 0, position: 3);
            Assert.AreEqual(7, CharacterUnit.CalculateDistance(3, 3));
            Assert.IsTrue(unit.CanReach(3));
        }

        [Test]
        public void CanReach_BackToBack_Reach6Fails()
        {
            // 거리7 > 리치6 → 실패
            var unit = new CharacterUnit(MakeCharacter(reach: 6), ownerId: 0, position: 3);
            Assert.IsFalse(unit.CanReach(3));
        }

        [Test]
        public void CanReach_BackToBack_Reach7Succeeds()
        {
            // 거리7 == 리치7 → 성공
            var unit = new CharacterUnit(MakeCharacter(reach: 7), ownerId: 0, position: 3);
            Assert.IsTrue(unit.CanReach(3));
        }

        // ---------------- 관통 (방어도 무시) ----------------

        [Test]
        public void TakeDamage_NonPiercing_AbsorbedByDefenseFirst()
        {
            var unit = new CharacterUnit(MakeCharacter(reach: 1), ownerId: 0, position: 0);
            unit.AddDefense(3);

            unit.TakeDamage(2, piercing: false);

            Assert.AreEqual(1, unit.Defense, "방어도가 먼저 흡수해야 함");
            Assert.AreEqual(15, unit.CurrentHp, "방어도 안에서 막혔으므로 HP는 그대로");
        }

        [Test]
        public void TakeDamage_NonPiercing_OverflowsToHpAfterDefenseDepleted()
        {
            var unit = new CharacterUnit(MakeCharacter(reach: 1), ownerId: 0, position: 0);
            unit.AddDefense(3);

            unit.TakeDamage(5, piercing: false);

            Assert.AreEqual(0, unit.Defense);
            Assert.AreEqual(13, unit.CurrentHp, "방어도 3 흡수 후 남은 2가 HP에 적용");
        }

        [Test]
        public void TakeDamage_Piercing_IgnoresDefenseAndHitsHpDirectly()
        {
            // RULES.md 7번: 관통 = 상대의 방어도를 무시하고 공격
            var unit = new CharacterUnit(MakeCharacter(reach: 1), ownerId: 0, position: 0);
            unit.AddDefense(3);

            unit.TakeDamage(2, piercing: true);

            Assert.AreEqual(3, unit.Defense, "관통은 방어도를 건드리지 않음");
            Assert.AreEqual(13, unit.CurrentHp, "관통 데미지는 방어도 무시하고 HP에 직접 적용");
        }

        // ---------------- 방어도 지속 (R3) ----------------

        [Test]
        public void ClearDefense_ResetsToZero()
        {
            // RULES.md R3: 방어도는 턴이 끝나면 사라진다
            var unit = new CharacterUnit(MakeCharacter(reach: 1), ownerId: 0, position: 0);
            unit.AddDefense(5);

            unit.ClearDefense();

            Assert.AreEqual(0, unit.Defense);
        }

        // ---------------- PayHp (HP 1 미만 지불 실패) ----------------

        [Test]
        public void PayHp_LeavesAtLeastOneHp_Succeeds()
        {
            var unit = new CharacterUnit(MakeCharacter(reach: 1, maxHp: 5), ownerId: 0, position: 0);

            bool result = unit.PayHp(4);

            Assert.IsTrue(result);
            Assert.AreEqual(1, unit.CurrentHp);
        }

        [Test]
        public void PayHp_WouldDropBelowOne_FailsAndLeavesHpUnchanged()
        {
            // RULES.md 11번 가정: HP를 비용으로 낼 때 자멸 불가 (HP 1 미만 지불 실패)
            var unit = new CharacterUnit(MakeCharacter(reach: 1, maxHp: 5), ownerId: 0, position: 0);

            bool result = unit.PayHp(5);

            Assert.IsFalse(result);
            Assert.AreEqual(5, unit.CurrentHp, "실패 시 상태 변화 없음");
        }

        // ---------------- PlayerState: Draw 재순환 O ----------------

        [Test]
        public void Draw_DeckEmpty_RecyclesTrashAndContinuesDrawing()
        {
            // RULES.md 3번: 메인덱 0장이 되면 트래쉬를 셔플하여 다시 메인덱으로
            var state = new PlayerState(new Random(1));
            state.Trash.AddRange(new[] { MakeSkill("S1"), MakeSkill("S2"), MakeSkill("S3") });

            var drawn = state.Draw(1);

            Assert.AreEqual(1, drawn.Count);
            Assert.AreEqual(0, state.Trash.Count, "트래쉬는 전부 덱으로 이동");
            Assert.AreEqual(2, state.Deck.Count, "3장 재순환 후 1장 드로우 = 덱 2장 남음");
            Assert.AreEqual(1, state.Hand.Count);
        }

        [Test]
        public void Draw_DeckAndTrashBothEmpty_StopsWithoutError()
        {
            var state = new PlayerState(new Random(1));

            var drawn = state.Draw(2);

            Assert.AreEqual(0, drawn.Count);
            Assert.AreEqual(0, state.Hand.Count);
        }

        // ---------------- PlayerState: RevealTopForChallenge 재순환 X ----------------

        [Test]
        public void RevealTopForChallenge_DeckEmpty_DoesNotRecycleTrash()
        {
            // RULES.md R8: 챌린지 중에는 트래쉬 재순환 없음. 재순환은 드로우 시에만.
            var state = new PlayerState(new Random(1));
            state.Trash.AddRange(new[] { MakeSkill("S1"), MakeSkill("S2") });

            var revealed = state.RevealTopForChallenge();

            Assert.IsNull(revealed, "덱이 비어있으면 재순환 없이 null (자동 패배 신호)");
            Assert.AreEqual(2, state.Trash.Count, "트래쉬는 그대로 유지되어야 함");
            Assert.AreEqual(0, state.Deck.Count);
        }

        [Test]
        public void RevealTopForChallenge_RevealedCardGoesToTrash()
        {
            // RULES.md R5: 챌린지 공개 카드는 트래쉬로 간다
            var state = new PlayerState(new Random(1));
            var top = MakeSkill("S1");
            state.Deck.Add(top);
            state.Deck.Add(MakeSkill("S2"));

            var revealed = state.RevealTopForChallenge();

            Assert.AreSame(top, revealed);
            Assert.AreEqual(1, state.Deck.Count, "공개된 카드는 덱에서 제거");
            CollectionAssert.Contains(state.Trash, top);
        }
    }
}
