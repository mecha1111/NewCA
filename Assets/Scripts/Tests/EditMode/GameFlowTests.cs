using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;
using NUnit.Framework;
using UnityEngine;

namespace CrossAccel.Tests
{
    /// <summary>
    /// Phase 3 — 페이즈 상태머신(밴픽/세팅/멀리건/턴 루프), 코스트 지불, 엔드 페이즈 규칙 검증.
    /// 근거: docs/RULES.md 4번, 5번, R2, R3, R9.
    /// </summary>
    public class GameFlowTests
    {
        private GameManager _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameManager(new CardDatabase(), new System.Random(1));
        }

        private CharacterUnit AddCharacter(int playerId, int position, int maxHp = 15)
        {
            var unit = new CharacterUnit(TestCards.Character($"C{playerId}_{position}", maxHp: maxHp), playerId, position);
            _game.Players[playerId].CharacterZone.Add(unit);
            return unit;
        }

        // ---------------- 엔드 페이즈: 방어도 (R3) ----------------

        [Test]
        public void EndPhase_ClearsDefenseOnAllSurvivingUnits()
        {
            // RULES.md R3: 방어도는 턴이 끝나면 사라진다 (엔드 페이즈 효과 처리 후 클리어)
            var u0 = AddCharacter(0, 0);
            var u1 = AddCharacter(1, 0);
            u0.AddDefense(4);
            u1.AddDefense(2);

            _game.RunEndPhase();

            Assert.AreEqual(0, u0.Defense);
            Assert.AreEqual(0, u1.Defense);
        }

        // ---------------- 엔드 페이즈: 사망 처리 ----------------

        [Test]
        public void EndPhase_DeadCharacter_MovesToCostZoneUnrested()
        {
            // RULES.md 5-4: HP가 0인 캐릭터 카드를 코스트존에 배치
            // 레스트 여부는 RULES.md 11번 임시 가정(언레스트)을 따른다
            var dead = AddCharacter(0, 0, maxHp: 3);
            AddCharacter(0, 1);
            AddCharacter(1, 0);
            dead.TakeDamage(3, piercing: true);

            _game.RunEndPhase();

            Assert.AreEqual(1, _game.Players[0].CharacterZone.Count, "사망 유닛은 캐릭터 존에서 빠짐");
            var placed = _game.Players[0].CostZone.SingleOrDefault(c => c.CardId == dead.Data.Id);
            Assert.IsNotNull(placed, "사망 캐릭터가 코스트존에 없음");
            Assert.IsFalse(placed.IsRested, "언레스트 상태로 들어가야 함");
        }

        // ---------------- 엔드 페이즈: 앞당김 (R9) ----------------

        [Test]
        public void EndPhase_FrontCharacterDies_SurvivorsAdvanceOneStep()
        {
            // RULES.md R9: 앞당김 — 뒤 캐릭터들이 한 칸씩 당겨진다
            var front = AddCharacter(0, 0, maxHp: 3);
            var second = AddCharacter(0, 1);
            var third = AddCharacter(0, 2);
            AddCharacter(1, 0);
            front.TakeDamage(3, piercing: true);

            _game.RunEndPhase();

            Assert.AreEqual(0, second.Position, "1번이 최전선으로 전진");
            Assert.AreEqual(1, third.Position, "2번이 1번으로 전진");
            CollectionAssert.AreEqual(new[] { 0, 1 }, _game.Players[0].CharacterZone.Select(u => u.Position));
        }

        [Test]
        public void EndPhase_MiddleCharacterDies_GapIsClosedByThoseBehind()
        {
            // 중간이 죽어도 뒤쪽이 당겨져 빈자리를 메운다
            var front = AddCharacter(0, 0);
            var middle = AddCharacter(0, 1, maxHp: 3);
            var back = AddCharacter(0, 2);
            AddCharacter(1, 0);
            middle.TakeDamage(3, piercing: true);

            _game.RunEndPhase();

            Assert.AreEqual(0, front.Position, "앞쪽은 그대로");
            Assert.AreEqual(1, back.Position, "뒤쪽이 빈자리로 당겨짐");
            CollectionAssert.AreEqual(new[] { 0, 1 }, _game.Players[0].CharacterZone.Select(u => u.Position));
        }

        [Test]
        public void EndPhase_MultipleDeaths_SurvivorsStayContiguousFromZero()
        {
            // 여러 명이 한 턴에 죽어도 생존자는 항상 Position 0부터 연속
            var a = AddCharacter(0, 0, maxHp: 3);
            var b = AddCharacter(0, 1);
            var c = AddCharacter(0, 2, maxHp: 3);
            var d = AddCharacter(0, 3);
            AddCharacter(1, 0);
            a.TakeDamage(3, piercing: true);
            c.TakeDamage(3, piercing: true);

            _game.RunEndPhase();

            Assert.AreEqual(0, b.Position);
            Assert.AreEqual(1, d.Position);
            CollectionAssert.AreEqual(new[] { 0, 1 }, _game.Players[0].CharacterZone.Select(u => u.Position));
        }

        [Test]
        public void EndPhase_Advancement_PreservesRelativeOrder()
        {
            // 앞당김은 번호만 당길 뿐, 앞뒤 순서는 뒤집히지 않아야 한다
            var a = AddCharacter(0, 0, maxHp: 3);
            var b = AddCharacter(0, 1);
            var c = AddCharacter(0, 2);
            var d = AddCharacter(0, 3);
            AddCharacter(1, 0);
            a.TakeDamage(3, piercing: true);

            _game.RunEndPhase();

            CollectionAssert.AreEqual(
                new[] { b.Data.Id, c.Data.Id, d.Data.Id },
                _game.Players[0].CharacterZone.OrderBy(u => u.Position).Select(u => u.Data.Id),
                "원래 앞에 있던 캐릭터가 계속 앞이어야 함");
        }

        [Test]
        public void EndPhase_NoDeaths_PositionsAreUntouched()
        {
            var a = AddCharacter(0, 0);
            var b = AddCharacter(0, 1);
            AddCharacter(1, 0);

            _game.RunEndPhase();

            Assert.AreEqual(0, a.Position);
            Assert.AreEqual(1, b.Position);
        }

        [Test]
        public void EndPhase_AdvancementShortensReachDistance()
        {
            // R9 앞당김의 핵심 효과: 죽을수록 거리가 줄어 공격이 닿기 쉬워진다 (리치 교착 방지)
            var front = AddCharacter(0, 0, maxHp: 3);
            var back = AddCharacter(0, 1);
            AddCharacter(1, 0);

            Assert.AreEqual(2, CharacterUnit.CalculateDistance(back.Position, 0), "사망 전 거리 1+0+1 = 2");

            front.TakeDamage(3, piercing: true);
            _game.RunEndPhase();

            Assert.AreEqual(1, CharacterUnit.CalculateDistance(back.Position, 0), "전진 후 거리 0+0+1 = 1");
        }

        // ---------------- 엔드 페이즈: 승패 판정 ----------------

        [Test]
        public void EndPhase_CharacterZoneEmptied_DeclaresOpponentWinner()
        {
            // RULES.md 1번/5-4: 자신의 캐릭터 존에 캐릭터 카드가 전부 없어지면 패배
            int? reported = null;
            _game.OnGameOver += winner => reported = winner;

            var only = AddCharacter(0, 0, maxHp: 3);
            AddCharacter(1, 0);
            only.TakeDamage(3, piercing: true);

            _game.RunEndPhase();

            Assert.AreEqual(GamePhase.GameOver, _game.Phase);
            Assert.AreEqual(1, _game.Winner);
            Assert.AreEqual(1, reported, "OnGameOver 이벤트가 승자와 함께 발생해야 함");
        }

        [Test]
        public void EndPhase_BothSidesWipedOut_ReportsNoWinner()
        {
            // RULES.md 미정의 — 11번 가정: 양측 동시 전멸은 승자 없음으로 본다
            var u0 = AddCharacter(0, 0, maxHp: 3);
            var u1 = AddCharacter(1, 0, maxHp: 3);
            u0.TakeDamage(3, piercing: true);
            u1.TakeDamage(3, piercing: true);

            _game.RunEndPhase();

            Assert.AreEqual(GamePhase.GameOver, _game.Phase);
            Assert.AreEqual(GameManager.NoWinner, _game.Winner);
        }

        [Test]
        public void EndPhase_BothSidesAlive_GameContinues()
        {
            AddCharacter(0, 0);
            AddCharacter(1, 0);

            _game.RunEndPhase();

            Assert.AreEqual(GamePhase.End, _game.Phase);
            Assert.IsNull(_game.Winner);
        }

        // ---------------- 코스트 (R2) ----------------

        [Test]
        public void PayCost_RestsExactlyTheRequestedNumberOfCards()
        {
            // RULES.md 2번: 코스트존 카드를 꺾어(레스트) 사용 여부를 판단
            var player = _game.Players[0];
            player.CostZone.Add(new CostCard("S01"));
            player.CostZone.Add(new CostCard("S02"));
            player.CostZone.Add(new CostCard("S03"));

            Assert.IsTrue(player.PayCost(2));

            Assert.AreEqual(1, player.AvailableCost);
            Assert.AreEqual(2, player.CostZone.Count(c => c.IsRested));
        }

        [Test]
        public void PayCost_NotEnoughUnrestedCards_FailsWithoutRestingAnything()
        {
            var player = _game.Players[0];
            player.CostZone.Add(new CostCard("S01"));

            Assert.IsFalse(player.PayCost(2));
            Assert.AreEqual(1, player.AvailableCost, "실패 시 아무것도 꺾지 않아야 함");
        }

        [Test]
        public void BeginTurn_UnrestsAllCostCards()
        {
            // RULES.md R2: 매 턴 시작 시 레스트된 코스트 카드 전부 다시 섬
            AddCharacter(0, 0);
            AddCharacter(1, 0);
            var player = _game.Players[0];
            player.CostZone.Add(new CostCard("S01", isRested: true));
            player.CostZone.Add(new CostCard("S02", isRested: true));

            _game.BeginTurn();

            Assert.AreEqual(2, player.AvailableCost);
            Assert.IsFalse(player.CostZone.Any(c => c.IsRested));
        }

        // ---------------- 드로우 페이즈 (5-1) ----------------

        [Test]
        public void BeginTurn_DrawsTwoCardsPerPlayer()
        {
            // RULES.md 5-1: 덱에서 카드 2장 드로우
            for (int p = 0; p < GameRules.PlayerCount; p++)
                for (int i = 0; i < 5; i++)
                    _game.Players[p].Deck.Add(TestCards.Skill($"S{p}{i}"));

            _game.BeginTurn();

            Assert.AreEqual(GamePhase.Draw, _game.Phase);
            Assert.AreEqual(1, _game.TurnNumber);
            foreach (var player in _game.Players)
            {
                Assert.AreEqual(2, player.Hand.Count);
                Assert.AreEqual(3, player.Deck.Count);
            }
        }

        [Test]
        public void PlaceCostFromDraw_OnlyAcceptsCardsDrawnThisTurn()
        {
            // RULES.md 5-1: 드로우한 카드에서 최대 2장까지 코스트존으로 넣을 수 있음
            var oldCard = TestCards.Skill("OLD");
            _game.Players[0].Hand.Add(oldCard);
            for (int i = 0; i < 3; i++)
                _game.Players[0].Deck.Add(TestCards.Skill($"NEW{i}"));

            _game.BeginTurn();
            var drawn = _game.LastDrawn[0];

            Assert.AreEqual(0, _game.PlaceCostFromDraw(0, new[] { oldCard }), "이번 턴에 뽑지 않은 카드는 불가");
            Assert.AreEqual(2, _game.PlaceCostFromDraw(0, drawn));
            CollectionAssert.Contains(_game.Players[0].Hand, oldCard, "코스트존에 놓지 않은 카드는 패에 남음");
        }

        [Test]
        public void PlaceCostFromDraw_CapsAtTwoCards()
        {
            for (int i = 0; i < 4; i++)
                _game.Players[0].Deck.Add(TestCards.Skill($"S{i}"));

            _game.BeginTurn();          // 2장 드로우
            var extra = _game.Players[0].Draw(2);  // 같은 턴에 효과로 2장 더 뽑았다고 가정
            var all = _game.LastDrawn[0].Concat(extra).ToList();

            Assert.AreEqual(GameRules.DrawPhaseMaxToCost, _game.PlaceCostFromDraw(0, all));
        }

        // ---------------- 밴픽 / 세팅 / 멀리건 (실데이터) ----------------

        private static CardDatabase LoadRealDatabase()
        {
            string dir = Path.Combine(Application.dataPath, "StreamingAssets", "DataJsons");
            var db = new CardDatabase();
            db.LoadCharacters(File.ReadAllText(Path.Combine(dir, "CharacterCardData.json")));
            db.LoadSkills(File.ReadAllText(Path.Combine(dir, "SkillData.json")));
            db.LoadDecks(File.ReadAllText(Path.Combine(dir, "StarterDecks.json")));
            return db;
        }

        private static GameManager StartRealGame(CardDatabase db)
        {
            var game = new GameManager(db, new System.Random(42));

            // 결정론적 델리게이트 (ARCHITECTURE.md: 테스트는 고정 선택으로 재현 가능하게)
            game.BanSelector = (playerId, opponentDeck) => opponentDeck[0];
            game.PickSelector = (playerId, ownDeck, count) => ownDeck.Take(count).ToList();

            game.StartGame(
                DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "Aggro")),
                DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "MidRange_Blood")));
            return game;
        }

        [Test]
        public void BanPick_TenCharacterDeck_YieldsFourPlacedAndFourHpCounters()
        {
            // RULES.md 4번: 10장 → 밴 2장 → 8장. 픽 4장 배치 + 잔여 4장 HP 표시
            var game = StartRealGame(LoadRealDatabase());

            game.RunBanPick();

            Assert.AreEqual(GamePhase.Setup, game.Phase);
            foreach (var player in game.Players)
            {
                Assert.AreEqual(GameRules.PartySize, player.CharacterZone.Count, "배치 캐릭터 4장");
                Assert.AreEqual(GameRules.PartySize, player.HpCounterCards.Count, "HP 표시용 4장");
            }
        }

        [Test]
        public void BanPick_PlacedCharacters_GetSequentialPositionsFromFront()
        {
            // RULES.md R4: Position 0 = 최전선, 배치 순서가 곧 Position
            var game = StartRealGame(LoadRealDatabase());

            game.RunBanPick();

            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3 },
                game.Players[0].CharacterZone.Select(u => u.Position));
        }

        [Test]
        public void BanPick_BannedAndPickedCardsDoNotOverlap()
        {
            // 밴 2 + 픽 4 + 잔여 4 = 10 산술이 성립하려면 밴 대상과 픽이 겹치면 안 된다
            var game = StartRealGame(LoadRealDatabase());

            game.RunBanPick();

            foreach (var player in game.Players)
            {
                var placed = player.CharacterZone.Select(u => u.Data.Id).ToList();
                CollectionAssert.IsEmpty(placed.Intersect(player.HpCounterCards).ToList());
                Assert.AreEqual(8, placed.Count + player.HpCounterCards.Count, "밴 2장을 뺀 8장이 남아야 함");
            }
        }

        [Test]
        public void Mulligan_DrawsSixCardsAndAcceptsTwoAsCost()
        {
            // RULES.md 4번 준비: 6장 드로우 → 2장 코스트존
            var game = StartRealGame(LoadRealDatabase());
            game.RunBanPick();

            game.RunMulligan();
            Assert.AreEqual(GamePhase.Mulligan, game.Phase);
            Assert.AreEqual(GameRules.MulliganDrawCount, game.Players[0].Hand.Count);

            int placed = game.PlaceCostFromHand(0, game.Players[0].Hand.Take(2).ToList());

            Assert.AreEqual(GameRules.MulliganCostCount, placed);
            Assert.AreEqual(4, game.Players[0].Hand.Count);
            Assert.AreEqual(GameRules.MulliganCostCount, game.Players[0].AvailableCost);
        }

        [Test]
        public void FullSetupThenFirstTurn_RunsThroughEveryPhaseWithoutError()
        {
            // 밴픽 → 세팅 → 멀리건 → 드로우 → 레디 → 액션 → 엔드 가 예외 없이 이어지는지
            var game = StartRealGame(LoadRealDatabase());
            var seen = new List<GamePhase>();
            game.OnPhaseChanged += phase => seen.Add(phase);

            game.RunBanPick();
            game.RunMulligan();
            game.PlaceCostFromHand(0, game.Players[0].Hand.Take(2).ToList());
            game.PlaceCostFromHand(1, game.Players[1].Hand.Take(2).ToList());

            game.BeginTurn();
            game.BeginReadyPhase();
            for (int p = 0; p < GameRules.PlayerCount; p++)
            {
                var player = game.Players[p];
                game.SubmitActiveCard(p, player.Hand.First(), player.CharacterZone.First());
            }
            game.RunActionPhase();

            CollectionAssert.IsSubsetOf(
                new[] { GamePhase.Setup, GamePhase.Mulligan, GamePhase.Draw, GamePhase.Ready, GamePhase.Action, GamePhase.End },
                seen);
            Assert.AreEqual(1, game.TurnNumber);
        }
    }
}
