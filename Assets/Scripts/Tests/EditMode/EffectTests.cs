using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;
using CrossAccel.Effects;
using NUnit.Framework;
using UnityEngine;

namespace CrossAccel.Tests
{
    /// <summary>
    /// Phase 4 — 카드 효과 검증 (TESTING.md 4단계). 스타터 덱 범위만.
    /// 근거: docs/RULES.md (R4 리치, 관통, R3 방어도) + 카드 원문 + RULES.md 11번 해석 가정.
    /// </summary>
    public class EffectTests
    {
        private GameManager _game;
        private EffectSystem _effects;
        private readonly List<string> _log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _game = new GameManager(new CardDatabase(), new System.Random(1));
            _game.Log = message => _log.Add(message);
            _effects = StarterDeckEffects.Install(_game);
            _effects.Log = message => _log.Add(message);
            _game.CharacterEffects.Log = message => _log.Add(message);
        }

        private CharacterUnit AddCharacter(string id, int playerId, int position = 0,
                                           string weapon = "한손검", int maxHp = 15, int reach = 7,
                                           string race = "인간")
        {
            var unit = new CharacterUnit(TestCards.Character(id, weapon, maxHp, reach, race), playerId, position);
            _game.Players[playerId].CharacterZone.Add(unit);
            return unit;
        }

        /// <summary>스킬 효과를 직접 실행한다 (액티브 존·코스트 절차를 거치지 않는 단위 검증).</summary>
        private void Execute(string skillId, CharacterUnit user, CharacterUnit target,
                             int cost = 0, string weapon = "공용", int damageBonus = 0, bool piercing = false,
                             int skillOption = 1)
        {
            _effects.Execute(new EffectContext
            {
                Game = _game,
                Owner = _game.Players[user.OwnerId],
                Opponent = _game.Players[GameManager.Opponent(user.OwnerId)],
                User = user,
                Target = target,
                Skill = TestCards.Skill(skillId, weapon, speed: 1, cost: cost),
                DamageBonus = damageBonus,
                Piercing = piercing,
                SkillOption = skillOption
            });
        }

        // =====================================================================
        //  공통 DealDamage — 리치(R4) / 버프 합산 / 관통
        // =====================================================================

        [Test]
        public void DealDamage_TargetOutOfReach_DealsNothing()
        {
            // RULES.md R4: 거리 = 내 위치 + 상대 위치 + 1. 내3 → 적3 = 7 > 리치 6이면 실패
            var attacker = AddCharacter("A", 0, position: 3, reach: 6);
            var target = AddCharacter("B", 1, position: 3);

            Execute("S25", attacker, target);

            Assert.AreEqual(target.Data.MaxHp, target.CurrentHp, "리치가 모자라면 데미지가 들어가면 안 됨");
            Assert.IsTrue(_log.Any(m => m.Contains("리치 부족")));
        }

        [Test]
        public void DealDamage_ExactlyAtMaxReach_Succeeds()
        {
            // 거리 7 == 리치 7 → 성공
            var attacker = AddCharacter("A", 0, position: 3, reach: 7);
            var target = AddCharacter("B", 1, position: 3);

            Execute("S25", attacker, target);

            Assert.AreEqual(target.Data.MaxHp - 2, target.CurrentHp);
        }

        [Test]
        public void DealDamage_SumsCardBonusAndUnitBuffs()
        {
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);
            attacker.TurnDamageBonus = 1;
            attacker.PermanentDamageBonus = 2;

            Execute("S25", attacker, target, damageBonus: 3); // 기본 2 + 3 + 1 + 2 = 8

            Assert.AreEqual(target.Data.MaxHp - 8, target.CurrentHp);
        }

        [Test]
        public void DealDamage_Piercing_IgnoresDefense()
        {
            // RULES.md 7번 키워드 관통: 상대의 방어도를 무시하고 공격
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);
            target.AddDefense(5);

            Execute("S25", attacker, target, piercing: true);

            Assert.AreEqual(5, target.Defense, "관통은 방어도를 깎지 않음");
            Assert.AreEqual(target.Data.MaxHp - 2, target.CurrentHp);
        }

        [Test]
        public void DealDamage_NonPiercing_IsAbsorbedByDefense()
        {
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);
            target.AddDefense(5);

            Execute("S25", attacker, target);

            Assert.AreEqual(3, target.Defense);
            Assert.AreEqual(target.Data.MaxHp, target.CurrentHp);
        }

        [Test]
        public void DealDamage_RecordsDamageTakenForCounter()
        {
            // S13 카운터가 참조하는 누적값
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Execute("S26", attacker, target); // 데미지 3

            Assert.AreEqual(3, _game.Players[1].DamageTakenThisTurn);
        }

        // =====================================================================
        //  스킬 효과
        // =====================================================================

        [Test]
        public void S25_DealsTwoDamage()
        {
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Execute("S25", attacker, target);

            Assert.AreEqual(target.Data.MaxHp - 2, target.CurrentHp);
        }

        [Test]
        public void S75_HitsTwiceForOneEach()
        {
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Execute("S75", attacker, target);

            Assert.AreEqual(target.Data.MaxHp - 2, target.CurrentHp, "데미지 1 × 2회");
            Assert.AreEqual(2, attacker.AttacksThisTurn);
        }

        [Test]
        public void S76_HitsThreeTimesForOneEach()
        {
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Execute("S76", attacker, target);

            Assert.AreEqual(target.Data.MaxHp - 3, target.CurrentHp);
        }

        [Test]
        public void S01_FirstSkillOfTurn_DealsDamage()
        {
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Execute("S01", attacker, target);

            Assert.AreEqual(target.Data.MaxHp - 2, target.CurrentHp);
        }

        [Test]
        public void S01_WhenAnotherSkillAlreadyResolvedThisTurn_Fails()
        {
            // 원문: "이전에 사용된 스킬이 있으면 실패" — 이번 턴 자신이 이미 발동한 스킬 기준
            var attacker = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);
            _game.SkillsResolvedThisTurn[0] = 1;

            Execute("S01", attacker, target);

            Assert.AreEqual(target.Data.MaxHp, target.CurrentHp);
            Assert.IsTrue(_log.Any(m => m.Contains("[S01]")));
        }

        [Test]
        public void S06_HealsTheUsingCharacter()
        {
            // 대상 미명시 회복 → 사용 캐릭터 자신 (사용자 확정)
            var user = AddCharacter("A", 0, maxHp: 10);
            var target = AddCharacter("B", 1);
            user.TakeDamage(5, piercing: true);

            Execute("S06", user, target);

            Assert.AreEqual(6, user.CurrentHp);
        }

        [Test]
        public void S06_DoesNotHealAboveMaxHp()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            var target = AddCharacter("B", 1);

            Execute("S06", user, target);

            Assert.AreEqual(10, user.CurrentHp);
        }

        [Test]
        public void S14_GivesThreeDefenseToTheUser()
        {
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Execute("S14", user, target);

            Assert.AreEqual(3, user.Defense);
        }

        [Test]
        public void S74_DealsOneAndHealsUserOne()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            var target = AddCharacter("B", 1);
            user.TakeDamage(4, piercing: true);

            Execute("S74", user, target);

            Assert.AreEqual(target.Data.MaxHp - 1, target.CurrentHp);
            Assert.AreEqual(7, user.CurrentHp);
        }

        [Test]
        public void S80_AddsOneDamagePerBloodCardInTrash()
        {
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);
            _game.Players[0].Trash.Add(TestCards.Skill("S07", speed: 1)); // S07은 피 카드 id 목록에 없음
            _game.Players[0].Trash.Add(BloodCard("S77"));
            _game.Players[0].Trash.Add(BloodCard("S78"));

            Execute("S80", user, target);

            Assert.AreEqual(target.Data.MaxHp - 4, target.CurrentHp, "기본 2 + 피 카드 2장");
        }

        [Test]
        public void S80_BonusIsCappedAtFive_NotTheTotal()
        {
            // 사용자 확정: "(최대 N)"은 보너스에만 걸린다 → 총 데미지는 2 + 5 = 7
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1, maxHp: 15);
            for (int i = 0; i < 8; i++)
                _game.Players[0].Trash.Add(BloodCard());

            Execute("S80", user, target);

            Assert.AreEqual(15 - 7, target.CurrentHp);
        }

        [Test]
        public void S81_WhenUserAtFullHp_ConvertsHealIntoDefense()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            var target = AddCharacter("B", 1);

            Execute("S81", user, target);

            Assert.AreEqual(10, user.CurrentHp);
            Assert.AreEqual(2, user.Defense, "HP가 최대면 방어도로 전환");
        }

        [Test]
        public void S81_WhenUserWounded_HealsInstead()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            var target = AddCharacter("B", 1);
            user.TakeDamage(5, piercing: true);

            Execute("S81", user, target);

            Assert.AreEqual(7, user.CurrentHp);
            Assert.AreEqual(0, user.Defense);
        }

        [Test]
        public void S20_SlaveUser_DealsEightAndDies()
        {
            var user = AddCharacter("A", 0, maxHp: 10, race: "노예");
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S20", user, target);

            Assert.AreEqual(15 - 8, target.CurrentHp);
            Assert.IsTrue(user.IsDead, "노예는 발동 후 자신 사망");
        }

        [Test]
        public void S20_PursuerUser_DealsEightAndHurtsWholeParty()
        {
            var user = AddCharacter("A", 0, maxHp: 10, race: "추격자");
            var ally = AddCharacter("C", 0, position: 1, maxHp: 10);
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S20", user, target);

            Assert.AreEqual(15 - 8, target.CurrentHp);
            Assert.AreEqual(8, user.CurrentHp, "아군 전체 2 데미지");
            Assert.AreEqual(8, ally.CurrentHp);
        }

        [Test]
        public void S20_OtherRace_Fails()
        {
            var user = AddCharacter("A", 0, race: "인간");
            var target = AddCharacter("B", 1);

            Execute("S20", user, target);

            Assert.AreEqual(target.Data.MaxHp, target.CurrentHp);
            Assert.IsTrue(_log.Any(m => m.Contains("종족 조건 불충족")));
        }

        // ---------------- S84 마지막 저항 (선택지 2개) ----------------

        [Test]
        public void S84Skill1_ChargesThreeHpFromEveryAlly()
        {
            var user = AddCharacter("A", 0, maxHp: 10, race: "노예");
            var ally = AddCharacter("C", 0, position: 1, maxHp: 12);
            var target = AddCharacter("B", 1);

            Execute("S84", user, target);

            Assert.AreEqual(7, user.CurrentHp);
            Assert.AreEqual(9, ally.CurrentHp);
        }

        [Test]
        public void S84Skill1_FloorsHpAtOneInsteadOfKilling()
        {
            // 사용자 확정: 이 카드의 HP 비용은 R11의 예외 — HP를 최소 1로 바닥 처리하고 항상 발동
            var user = AddCharacter("A", 0, maxHp: 10, race: "노예");
            var frail = AddCharacter("C", 0, position: 1, maxHp: 10);
            var target = AddCharacter("B", 1);
            frail.TakeDamage(8, piercing: true); // HP 2

            Execute("S84", user, target);

            Assert.AreEqual(1, frail.CurrentHp, "자멸하지 않고 HP 1로 유지");
            Assert.IsFalse(frail.IsDead);
        }

        [Test]
        public void S84Skill1_BuffsOnlyLowHpSlavesAndPursuers()
        {
            var slave = AddCharacter("A", 0, maxHp: 5, race: "노예");        // 5-3 = 2 → 대상
            var pursuer = AddCharacter("C", 0, position: 1, maxHp: 6, race: "추격자"); // 6-3 = 3 → 대상
            var human = AddCharacter("D", 0, position: 2, maxHp: 5, race: "인간");     // 종족 불충족
            var healthySlave = AddCharacter("E", 0, position: 3, maxHp: 15, race: "노예"); // 15-3 = 12 → HP 초과
            var target = AddCharacter("B", 1);

            Execute("S84", slave, target);

            Assert.AreEqual(2, slave.TurnDamageBonus);
            Assert.AreEqual(2, pursuer.TurnDamageBonus);
            Assert.AreEqual(0, human.TurnDamageBonus, "인간은 종족 조건 불충족");
            Assert.AreEqual(0, healthySlave.TurnDamageBonus, "HP가 3을 넘으면 조건 불충족");
        }

        [Test]
        public void S84Skill2_PaysOneHpAndDrawsTwo()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            var ally = AddCharacter("C", 0, position: 1, maxHp: 10);
            var target = AddCharacter("B", 1);
            for (int i = 0; i < 3; i++)
                _game.Players[0].Deck.Add(TestCards.Skill($"D{i}"));

            Execute("S84", user, target, skillOption: 2);

            Assert.AreEqual(9, user.CurrentHp, "사용자만 HP 1 지불");
            Assert.AreEqual(10, ally.CurrentHp, "skill2는 아군 전체 비용이 아님");
            Assert.AreEqual(2, _game.Players[0].Hand.Count);
        }

        [Test]
        public void S84Skill2_AtOneHp_FailsWithoutDrawing()
        {
            // skill2의 HP 비용은 R11 그대로 (바닥 처리 예외는 skill1 한정)
            var user = AddCharacter("A", 0, maxHp: 10);
            var target = AddCharacter("B", 1);
            user.TakeDamage(9, piercing: true); // HP 1
            _game.Players[0].Deck.Add(TestCards.Skill("D0"));

            Execute("S84", user, target, skillOption: 2);

            Assert.AreEqual(1, user.CurrentHp);
            CollectionAssert.IsEmpty(_game.Players[0].Hand);
        }

        [Test]
        public void S84_DefaultsToSkill1WhenNoOptionGiven()
        {
            var user = AddCharacter("A", 0, maxHp: 10, race: "노예");
            var target = AddCharacter("B", 1);
            _game.Players[0].Deck.Add(TestCards.Skill("D0"));

            Execute("S84", user, target);

            Assert.AreEqual(7, user.CurrentHp, "skill1의 HP-3이 적용됨");
            CollectionAssert.IsEmpty(_game.Players[0].Hand, "skill2의 드로우가 일어나면 안 됨");
        }

        // ---------------- skill1/skill2 선택 (R14) ----------------

        [Test]
        public void SubmitActiveCard_ExplicitSkillOption_IsKeptUntilActionPhase()
        {
            // RULES.md R14: 레디 페이즈에 확정한 값이 액션 페이즈까지 유지
            var user = AddCharacter("A", 0, maxHp: 10);
            AddCharacter("B", 1);
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S84", "악기", speed: 5, cost: 0, skill2Cost: 2);
            _game.Players[0].Hand.Add(card);
            _game.Players[0].CostZone.Add(new CostCard("X1"));
            _game.Players[0].CostZone.Add(new CostCard("X2"));
            for (int i = 0; i < 3; i++) _game.Players[0].Deck.Add(TestCards.Skill($"D{i}"));

            _game.SubmitActiveCard(0, card, user, skillOption: 2);
            Assert.AreEqual(2, _game.Players[0].ActiveZone[0].SkillOption);

            _game.RunActionPhase();

            Assert.AreEqual(9, user.CurrentHp, "skill2의 HP-1이 적용됨");
            Assert.AreEqual(2, _game.Players[0].Hand.Count, "skill2로 2장 드로우");
        }

        [Test]
        public void ActivateSlot_SkillOptionTwo_PaysSkill2Cost()
        {
            // S84는 skill1 코스트 0, skill2 코스트 2 — 선택한 쪽의 코스트를 내야 한다
            var user = AddCharacter("A", 0, maxHp: 10);
            AddCharacter("B", 1);
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S84", "악기", speed: 5, cost: 0, skill2Cost: 2);
            _game.Players[0].Hand.Add(card);
            _game.Players[0].CostZone.Add(new CostCard("X1"));
            _game.Players[0].CostZone.Add(new CostCard("X2"));
            _game.Players[0].Deck.Add(TestCards.Skill("D0"));
            _game.Players[0].Deck.Add(TestCards.Skill("D1"));

            _game.SubmitActiveCard(0, card, user, skillOption: 2);
            _game.RunActionPhase();

            Assert.AreEqual(0, _game.Players[0].AvailableCost, "skill2 코스트 2가 지불되어야 함");
        }

        [Test]
        public void ActivateSlot_SkillOptionTwo_WithoutEnoughCost_Misfires()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            AddCharacter("B", 1);
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S84", "악기", speed: 5, cost: 0, skill2Cost: 2);
            _game.Players[0].Hand.Add(card);
            _game.Players[0].Deck.Add(TestCards.Skill("D0")); // 코스트존 비어 있음

            _game.SubmitActiveCard(0, card, user, skillOption: 2);
            var slot = _game.Players[0].ActiveZone[0];
            _game.RunActionPhase();

            Assert.IsFalse(slot.Activated, "skill2 코스트를 못 내면 불발");
            Assert.AreEqual(10, user.CurrentHp, "불발이면 HP도 안 나감");
        }

        [Test]
        public void SubmitActiveCard_WithoutExplicitOption_UsesSkillOptionSelector()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            AddCharacter("B", 1);
            _game.SkillOptionSelector = (unit, skill) => 2;
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S84", "악기", speed: 5, cost: 0, skill2Cost: 2);
            _game.Players[0].Hand.Add(card);
            _game.SubmitActiveCard(0, card, user);

            Assert.AreEqual(2, _game.Players[0].ActiveZone[0].SkillOption);
        }

        [Test]
        public void SubmitActiveCard_ExplicitOptionWins_OverSelector()
        {
            var user = AddCharacter("A", 0, maxHp: 10);
            AddCharacter("B", 1);
            _game.SkillOptionSelector = (unit, skill) => 2;
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S84", "악기", speed: 5, cost: 0, skill2Cost: 2);
            _game.Players[0].Hand.Add(card);
            _game.SubmitActiveCard(0, card, user, skillOption: 1);

            Assert.AreEqual(1, _game.Players[0].ActiveZone[0].SkillOption);
        }

        [Test]
        public void SubmitActiveCard_CardWithoutSkill2_AlwaysUsesSkillOneEvenIfSelectorSaysTwo()
        {
            var user = AddCharacter("A", 0);
            AddCharacter("B", 1);
            _game.SkillOptionSelector = (unit, skill) => 2;
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S25", "두손검", speed: 2, cost: 0); // skill2 없음
            _game.Players[0].Hand.Add(card);
            _game.SubmitActiveCard(0, card, user);

            Assert.AreEqual(1, _game.Players[0].ActiveZone[0].SkillOption);
        }

        [Test]
        public void SubmitActiveCard_DefaultsToSkillOneWhenNoSelectorSet()
        {
            var user = AddCharacter("A", 0);
            AddCharacter("B", 1);
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S84", "악기", speed: 5, cost: 0, skill2Cost: 2);
            _game.Players[0].Hand.Add(card);
            _game.SubmitActiveCard(0, card, user);

            Assert.AreEqual(1, _game.Players[0].ActiveZone[0].SkillOption);
        }

        [Test]
        public void S13_DealsDamageEqualToWhatWasTakenThisTurn()
        {
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1, maxHp: 15);
            _game.Players[0].DamageTakenThisTurn = 4;

            Execute("S13", user, target);

            Assert.AreEqual(15 - 4, target.CurrentHp);
        }

        [Test]
        public void S13_WithNoDamageTaken_DoesNothing()
        {
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Execute("S13", user, target);

            Assert.AreEqual(target.Data.MaxHp, target.CurrentHp);
        }

        [Test]
        public void S57_DumpsOneBloodCardFromDeckToTrash()
        {
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);
            var plain = TestCards.Skill("PLAIN", speed: 1);
            var blood = BloodCard();
            _game.Players[0].Deck.Add(plain);
            _game.Players[0].Deck.Add(blood);

            Execute("S57", user, target);

            CollectionAssert.Contains(_game.Players[0].Trash, blood);
            CollectionAssert.Contains(_game.Players[0].Deck, plain, "피 카드가 아닌 카드는 그대로");
            Assert.AreEqual(1, _game.Players[0].Deck.Count);
        }

        [Test]
        public void S82_AddsOneDamagePerTwoCostCardsHeld()
        {
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1, maxHp: 15);
            for (int i = 0; i < 5; i++)
                _game.Players[0].CostZone.Add(new CostCard($"S{i}"));

            Execute("S82", user, target); // 기본 3 + (5 / 2 = 2)

            Assert.AreEqual(15 - 5, target.CurrentHp);
        }

        [Test]
        public void S59_BowUser_DealsOneMoreButLosesOneReach()
        {
            var bowUser = AddCharacter("A", 0, weapon: "활", maxHp: 10, reach: 7);
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S59", bowUser, target);

            Assert.AreEqual(15 - 3, target.CurrentHp, "활 캐릭터는 데미지 +1");
            Assert.AreEqual(7, bowUser.EffectiveReach, "리치 -1은 그 공격에만 적용되고 원복되어야 함");
        }

        [Test]
        public void S59_NonBowUser_DealsBaseDamage()
        {
            var user = AddCharacter("A", 0, weapon: "한손검");
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S59", user, target);

            Assert.AreEqual(15 - 2, target.CurrentHp);
        }

        // =====================================================================
        //  캐릭터 효과 — 조건부 데미지 보정
        // =====================================================================

        [Test]
        public void C05_WithSlaveOnField_AddsOneDamage()
        {
            var rena = AddCharacter("C05", 0, weapon: "두손검", race: "추격자");
            AddCharacter("SLAVE", 0, position: 1, race: "노예");
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S25", rena, target);

            Assert.AreEqual(15 - 3, target.CurrentHp, "기본 2 + C05 조건 +1");
        }

        [Test]
        public void C05_WithoutSlaveOnField_AddsNothing()
        {
            var rena = AddCharacter("C05", 0, race: "추격자");
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S25", rena, target);

            Assert.AreEqual(15 - 2, target.CurrentHp);
        }

        [Test]
        public void C30_AgainstLowHpEnemy_AddsTwoDamage()
        {
            var tyron = AddCharacter("C30", 0, race: "노예");
            var target = AddCharacter("B", 1, maxHp: 3);

            Execute("S25", tyron, target);

            Assert.IsTrue(target.IsDead, "HP 3 이하 적에게 2+2 = 4 데미지");
        }

        [Test]
        public void C30_AgainstHealthyEnemy_AddsNothing()
        {
            var tyron = AddCharacter("C30", 0, race: "노예");
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S25", tyron, target);

            Assert.AreEqual(15 - 2, target.CurrentHp);
        }

        [Test]
        public void C15_SecondAttackInATurn_AddsOneDamage()
        {
            // S75는 데미지 1을 2회. 1타는 보너스 없음, 2타는 C15로 +1 → 총 3
            var garsha = AddCharacter("C15", 0, race: "추격자");
            var target = AddCharacter("B", 1, maxHp: 15);

            Execute("S75", garsha, target);

            Assert.AreEqual(15 - 3, target.CurrentHp);
        }

        // =====================================================================
        //  캐릭터 효과 — 타이밍 훅
        // =====================================================================

        [Test]
        public void C31_OnReadyPhase_PaysTwoHpForNextSkillBonus()
        {
            var bardo = AddCharacter("C31", 0, maxHp: 9, race: "추격자");
            AddCharacter("B", 1);

            _game.BeginReadyPhase();

            Assert.AreEqual(7, bardo.CurrentHp, "HP 2 지불");
            Assert.AreEqual(2, _game.NextSkillDamageBonus[0]);
            Assert.IsTrue(bardo.EffectUsedThisTurn);
        }

        [Test]
        public void C31_OnlyTriggersOncePerTurn()
        {
            var bardo = AddCharacter("C31", 0, maxHp: 9, race: "추격자");
            AddCharacter("B", 1);

            _game.BeginReadyPhase();
            _game.BeginReadyPhase(); // 같은 턴에 다시 불려도 재발동 금지

            Assert.AreEqual(7, bardo.CurrentHp);
            Assert.AreEqual(2, _game.NextSkillDamageBonus[0]);
        }

        [Test]
        public void C31_BonusIsConsumedByExactlyOneSkill()
        {
            var bardo = AddCharacter("C31", 0, maxHp: 9, race: "추격자");
            var target = AddCharacter("B", 1, maxHp: 15);
            _game.BeginReadyPhase();

            var card = TestCards.Skill("S25", speed: 3);
            _game.Players[0].Hand.Add(card);
            _game.SubmitActiveCard(0, card, bardo);
            _game.RunActionPhase();

            Assert.AreEqual(15 - 4, target.CurrentHp, "기본 2 + 다음 스킬 보너스 2");
            Assert.AreEqual(0, _game.NextSkillDamageBonus[0], "보너스는 1장에만 실리고 소모됨");
        }

        [Test]
        public void C42_OnReadyPhase_PaysTwoCostToDrawOne()
        {
            var azul = AddCharacter("C42", 0, weapon: "악기");
            AddCharacter("B", 1);
            _game.Players[0].CostZone.Add(new CostCard("X1"));
            _game.Players[0].CostZone.Add(new CostCard("X2"));
            _game.Players[0].Deck.Add(TestCards.Skill("D1"));

            _game.BeginReadyPhase();

            Assert.AreEqual(1, _game.Players[0].Hand.Count);
            Assert.AreEqual(0, _game.Players[0].AvailableCost, "코스트 2 소모");
        }

        [Test]
        public void C42_WithoutEnoughCost_DoesNotDraw()
        {
            AddCharacter("C42", 0, weapon: "악기");
            AddCharacter("B", 1);
            _game.Players[0].Deck.Add(TestCards.Skill("D1"));

            _game.BeginReadyPhase();

            Assert.AreEqual(0, _game.Players[0].Hand.Count);
        }

        [Test]
        public void C41_OnReadyPhase_BuffsWholeParty()
        {
            var rebecca = AddCharacter("C41", 0, weapon: "악기", maxHp: 8, race: "노예");
            var ally = AddCharacter("ALLY", 0, position: 1);
            AddCharacter("B", 1);

            _game.BeginReadyPhase();

            Assert.AreEqual(6, rebecca.CurrentHp, "HP 2 지불");
            Assert.AreEqual(1, rebecca.TurnDamageBonus);
            Assert.AreEqual(1, ally.TurnDamageBonus);
        }

        [Test]
        public void C25_OnActionPhase_AppliesReachAndDamageOnlyOnce()
        {
            var silvia = AddCharacter("C25", 0, maxHp: 9, reach: 3);
            AddCharacter("B", 1);

            _game.CharacterEffects.FireTiming(_game, EffectTiming.ActionPhaseBefore);
            _game.CharacterEffects.FireTiming(_game, EffectTiming.ActionPhaseBefore);

            Assert.AreEqual(2, silvia.EffectiveReach, "리치 3 - 1, 두 번 적용되면 안 됨");
            Assert.AreEqual(2, silvia.PermanentDamageBonus);
        }

        [Test]
        public void C12_OnTurnEnd_DamagesNearestReachableEnemyPerBloodCard()
        {
            var arkea = AddCharacter("C12", 0, maxHp: 9, reach: 7);
            var enemy = AddCharacter("B", 1, maxHp: 15);
            for (int i = 0; i < 3; i++)
                _game.Players[0].Trash.Add(BloodCard());
            for (int i = 0; i < 3; i++)
                _game.Players[0].CostZone.Add(new CostCard($"X{i}"));

            _game.RunEndPhase();

            Assert.AreEqual(15 - 3, enemy.CurrentHp, "피 카드 3장 → 데미지 3");
            Assert.AreEqual(0, _game.Players[0].AvailableCost, "코스트 3 소모");
        }

        [Test]
        public void C21_OnReadyPhase_ConvertsHpIntoTempCost()
        {
            var fanatic = AddCharacter("C21", 0, weapon: "책", maxHp: 6);
            AddCharacter("B", 1);
            _game.VariableCostPolicy = (player, max) => System.Math.Min(2, max);

            _game.BeginReadyPhase();

            Assert.AreEqual(4, fanatic.CurrentHp);
            Assert.AreEqual(2, _game.Players[0].TempCost);
            Assert.AreEqual(2, _game.Players[0].AvailableCost, "임시 코스트도 지불에 쓸 수 있어야 함");
        }

        [Test]
        public void C21_NeverDropsHpBelowOne()
        {
            // RULES.md 11번: HP를 비용으로 낼 때 자멸 불가
            var fanatic = AddCharacter("C21", 0, weapon: "책", maxHp: 6);
            AddCharacter("B", 1);
            fanatic.TakeDamage(5, piercing: true); // HP 1
            _game.VariableCostPolicy = (player, max) => max;

            _game.BeginReadyPhase();

            Assert.AreEqual(1, fanatic.CurrentHp);
            Assert.AreEqual(0, _game.Players[0].TempCost);
        }

        [Test]
        public void C24_OnActionPhase_GrantsBowUsageAndReachBonus()
        {
            var lean = AddCharacter("C24", 0, position: 3, weapon: "한손검", reach: 3);
            var target = AddCharacter("B", 1, position: 3, maxHp: 15);

            _game.CharacterEffects.FireTiming(_game, EffectTiming.ActionPhaseBefore);
            CollectionAssert.Contains(lean.ExtraWeapons, "활");

            // 거리 3+3+1 = 7. 리치 3 + 활 보너스 1 = 4 → 여전히 부족
            Execute("S25", lean, target, weapon: "활");
            Assert.AreEqual(15, target.CurrentHp);

            // 거리 0+0+1 = 1 이면 리치 안. 활 스킬 리치 보너스가 실제로 적용되는지는 경계에서 확인
            lean.Position = 0;
            target.Position = 3; // 거리 4, 리치 3 + 활 1 = 4 → 성공
            Execute("S25", lean, target, weapon: "활");
            Assert.AreEqual(15 - 2, target.CurrentHp);
        }

        [Test]
        public void C24_WithoutBowSkill_GetsNoReachBonus()
        {
            var lean = AddCharacter("C24", 0, position: 0, weapon: "한손검", reach: 3);
            var target = AddCharacter("B", 1, position: 3, maxHp: 15);

            Execute("S25", lean, target, weapon: "한손검"); // 거리 4 > 리치 3

            Assert.AreEqual(15, target.CurrentHp);
        }

        // =====================================================================
        //  미구현 카드 / 데이터 정합성
        // =====================================================================

        [Test]
        public void Execute_UnimplementedCard_LogsWarningAndKeepsGoing()
        {
            var user = AddCharacter("A", 0);
            var target = AddCharacter("B", 1);

            Assert.DoesNotThrow(() => Execute("S99", user, target));

            Assert.AreEqual(target.Data.MaxHp, target.CurrentHp);
            CollectionAssert.Contains(_effects.SkippedCardIds, "S99");
            Assert.IsTrue(_log.Any(m => m.Contains("미구현 효과")));
        }

        [Test]
        public void ActionPhase_WithUnimplementedCard_StillCompletesTheTurn()
        {
            var user = AddCharacter("A", 0);
            AddCharacter("B", 1);
            _game.BeginReadyPhase();

            var unknown = TestCards.Skill("S99", speed: 3);
            _game.Players[0].Hand.Add(unknown);
            _game.SubmitActiveCard(0, unknown, user);

            Assert.DoesNotThrow(() => _game.RunActionPhase());
            Assert.AreEqual(GamePhase.End, _game.Phase, "미구현 카드가 게임을 멈추면 안 됨");
        }

        [Test]
        public void EveryStarterDeckSkill_HasAnEffectHandler()
        {
            var db = LoadRealDatabase();
            var starterSkillIds = db.Decks
                .SelectMany(d => d.Cards)
                .Where(c => c.CardType == "Skill")
                .Select(c => c.CardId)
                .Distinct()
                .ToList();

            var missing = starterSkillIds.Where(id => !_effects.IsImplemented(id)).ToList();

            CollectionAssert.IsEmpty(missing, $"핸들러가 없는 스타터 덱 스킬: {string.Join(", ", missing)}");
        }

        [Test]
        public void BloodCardDetection_MatchesTenStarterCards()
        {
            // RULES.md 11번 가정(이름에 "피"/"핏빛" 포함)이 스타터 범위에서 정확한지
            var db = LoadRealDatabase();
            var starterSkillIds = db.Decks
                .SelectMany(d => d.Cards)
                .Where(c => c.CardType == "Skill")
                .Select(c => c.CardId)
                .Distinct();

            var bloodIds = starterSkillIds
                .Where(id => EffectSystem.IsBloodCard(db.Skills[id]))
                .OrderBy(id => id)
                .ToList();

            CollectionAssert.AreEqual(
                new[] { "S55", "S57", "S59", "S77", "S78", "S79", "S80", "S81", "S82", "S83" },
                bloodIds);
        }

        // =====================================================================
        //  헬퍼
        // =====================================================================

        /// <summary>피 카드 id 집합(EffectSystem.BloodCardIds)에 속하는 카드 하나를 만든다.</summary>
        private static SkillData BloodCard(string bloodCardId = "S77") => TestCards.Skill(bloodCardId, speed: 1);

        private static CardDatabase LoadRealDatabase()
        {
            string dir = Path.Combine(Application.dataPath, "StreamingAssets", "DataJsons");
            var db = new CardDatabase();
            db.LoadSkills(File.ReadAllText(Path.Combine(dir, "SkillData.json")));
            db.LoadDecks(File.ReadAllText(Path.Combine(dir, "StarterDecks.json")));
            return db;
        }
    }
}
