using System;
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
    /// Phase 5 — AI 완비 상태에서 게임이 처음부터 끝까지(밴픽~게임오버) 예외 없이 도는지 검증한다
    /// (TESTING.md 5단계). docs/PROTOTYPE_REFERENCE/GameSimulator.cs.txt의 흐름을 그대로 따르되,
    /// MonoBehaviour 시뮬레이터 대신 EditMode 테스트로 대체한다 (README.md 포팅 지침).
    /// </summary>
    public class IntegrationTests
    {
        /// <summary>
        /// 무한 루프 방지 안전장치 — RULES.md에 턴 상한이 없으므로 테스트가 임의로 둔다.
        /// 정상 상황에서는 걸리지 않아야 한다 (실측: 6~8턴에 승부).
        /// </summary>
        private const int MaxTurns = 60;

        private static CardDatabase LoadRealDatabase()
        {
            string dir = Path.Combine(Application.dataPath, "StreamingAssets", "DataJsons");
            var db = new CardDatabase();
            db.LoadCharacters(File.ReadAllText(Path.Combine(dir, "CharacterCardData.json")));
            db.LoadSkills(File.ReadAllText(Path.Combine(dir, "SkillData.json")));
            db.LoadDecks(File.ReadAllText(Path.Combine(dir, "StarterDecks.json")));
            return db;
        }

        /// <summary>AI vs AI 한 판을 처음부터 끝까지(또는 maxTurns까지) 진행한다.</summary>
        private static GameManager RunFullGame(int seed, int maxTurns = MaxTurns, bool withEffects = true)
        {
            var db = LoadRealDatabase();
            var rng = new System.Random(seed);
            var game = new GameManager(db, rng);

            var ai = new[] { new AIController(0, db, rng), new AIController(1, db, rng) };
            AIController.Attach(game, ai);

            if (withEffects)
                StarterDeckEffects.Install(game);
            // withEffects=false면 SkillEffectResolver/CharacterEffects를 안 붙인 채로 진행 —
            // "미구현 카드 스킵돼도 게임이 안 멈추는지" 검증용 (모든 카드가 미구현인 극단적 경우).

            var deck0 = DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "Aggro"));
            var deck1 = DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "MidRange_Blood"));
            game.StartGame(deck0, deck1);

            game.RunBanPick(); // 세팅(캐릭터 배치 + 메인덱 구성)까지 이어서 수행됨

            game.RunMulligan();
            for (int p = 0; p < GameRules.PlayerCount; p++)
            {
                var cost = ai[p].ChooseMulliganCost(game.Players[p]);
                game.PlaceCostFromHand(p, cost);
            }

            while (!game.Winner.HasValue && game.TurnNumber < maxTurns)
            {
                game.BeginTurn();

                for (int p = 0; p < GameRules.PlayerCount; p++)
                {
                    var drawCost = ai[p].ChooseDrawCost(game.Players[p], game.LastDrawn[p]);
                    game.PlaceCostFromDraw(p, drawCost);
                }

                game.BeginReadyPhase();
                for (int p = 0; p < GameRules.PlayerCount; p++)
                {
                    var plays = ai[p].ChoosePlays(game.Players[p], game.Players[GameManager.Opponent(p)]);
                    foreach (var play in plays)
                        game.SubmitActiveCard(p, play.Skill, play.User, play.AsAccel, false, play.SkillOption);
                }

                game.RunActionPhase(); // 내부에서 엔드 페이즈까지 처리
            }

            return game;
        }

        // ---------------- 고정 시드 1판 ----------------

        [Test]
        public void AiVsAi_FixedSeed_CompletesWithoutException()
        {
            GameManager game = null;
            Assert.DoesNotThrow(() => game = RunFullGame(seed: 42));

            Assert.IsTrue(game.Winner.HasValue, $"{MaxTurns}턴 안에 승부가 나야 함 (턴 {game.TurnNumber}에서 중단)");
            Assert.AreEqual(GamePhase.GameOver, game.Phase);
        }

        [Test]
        public void AiVsAi_SameSeed_ProducesIdenticalResult()
        {
            // 결정론성 (ARCHITECTURE.md: 시드 주입, UnityEngine.Random 금지)
            var first = RunFullGame(seed: 42);
            var second = RunFullGame(seed: 42);

            Assert.AreEqual(first.TurnNumber, second.TurnNumber);
            Assert.AreEqual(first.Winner, second.Winner);
        }

        [Test]
        public void AiVsAi_MutualAnnihilation_ReportsNoWinner()
        {
            // 양측 동시 전멸은 승자 없음(-1)으로 본다 (RULES.md 11번 가정).
            // [주의] 시드는 발동 순서에 의존한다 — 신속 그룹핑 도입(IsAccel→IsSwift)으로 순서가 바뀌면서
            //        예전 시드 1이 더 이상 무승부가 아니게 되어 시드 4로 교체했다.
            //        규칙 자체는 GameFlowTests.EndPhase_BothSidesWipedOut_ReportsNoWinner가 시드 없이 검증한다.
            var game = RunFullGame(seed: 4);

            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.AreEqual(GameManager.NoWinner, game.Winner);
            foreach (var player in game.Players)
                Assert.AreEqual(0, player.CharacterZone.Count, "양쪽 다 캐릭터 존이 비어야 함");
        }

        [Test]
        public void AiVsAi_FixedSeed_OnGameOverEventFiresWithWinner()
        {
            var db = LoadRealDatabase();
            var rng = new System.Random(1);
            var game = new GameManager(db, rng);
            var ai = new[] { new AIController(0, db, rng), new AIController(1, db, rng) };
            AIController.Attach(game, ai);
            StarterDeckEffects.Install(game);

            int? reportedWinner = null;
            int eventFireCount = 0;
            game.OnGameOver += w => { reportedWinner = w; eventFireCount++; };

            var deck0 = DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "Aggro"));
            var deck1 = DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "MidRange_Blood"));
            game.StartGame(deck0, deck1);
            game.RunBanPick();
            game.RunMulligan();
            for (int p = 0; p < GameRules.PlayerCount; p++)
                game.PlaceCostFromHand(p, ai[p].ChooseMulliganCost(game.Players[p]));

            while (!game.Winner.HasValue && game.TurnNumber < MaxTurns)
            {
                game.BeginTurn();
                for (int p = 0; p < GameRules.PlayerCount; p++)
                    game.PlaceCostFromDraw(p, ai[p].ChooseDrawCost(game.Players[p], game.LastDrawn[p]));

                game.BeginReadyPhase();
                for (int p = 0; p < GameRules.PlayerCount; p++)
                    foreach (var play in ai[p].ChoosePlays(game.Players[p], game.Players[GameManager.Opponent(p)]))
                        game.SubmitActiveCard(p, play.Skill, play.User, play.AsAccel, false, play.SkillOption);

                game.RunActionPhase();
            }

            Assert.AreEqual(1, eventFireCount, "OnGameOver는 정확히 한 번만 발생해야 함");
            Assert.AreEqual(game.Winner, reportedWinner);
        }

        // ---------------- 여러 시드 ----------------

        /// <summary>
        /// 여러 시드에서 예외 없이, 그리고 안전장치에 걸리지 않고 승부가 나는지 확인한다.
        /// R9(앞당김) 덕분에 캐릭터가 죽을수록 거리가 줄어 공격이 닿기 쉬워지므로,
        /// 정상 상황에서는 최대 턴 안전장치가 발동할 일이 없어야 한다.
        /// 실측(2026-08-08): 10개 시드 전부 6~8턴에 GameOver 도달.
        /// (참고: R9가 "위치 고정"이던 시절엔 같은 시드들이 전부 60턴 안전장치에 걸렸다 — RULES.md R9 이력 참고)
        /// </summary>
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(100)]
        [TestCase(2026)]
        [TestCase(31337)]
        [TestCase(9999)]
        [TestCase(123456)]
        [TestCase(777)]
        [TestCase(555555)]
        public void AiVsAi_MultipleSeeds_ReachGameOverWellBeforeSafetyNet(int seed)
        {
            GameManager game = null;
            Assert.DoesNotThrow(() => game = RunFullGame(seed), $"시드 {seed}에서 예외 발생");

            Assert.AreEqual(GamePhase.GameOver, game.Phase,
                $"시드 {seed}: 승부가 나지 않고 멈춤 (turn={game.TurnNumber}, winner={game.Winner})");
            Assert.IsTrue(game.Winner.HasValue, $"시드 {seed}: 승자 정보가 없음");
            Assert.Less(game.TurnNumber, MaxTurns, $"시드 {seed}: 안전장치에 걸리면 안 됨");
        }

        [Test]
        public void AiVsAi_FormerDeadlockSeed_NowEndsDecisively()
        {
            // 회귀 방지: 시드 7은 R9가 "위치 고정"이던 시절 리치 교착으로 60턴을 다 채우던 시드다.
            // 앞당김으로 바로잡은 뒤에는 정상적으로 승부가 나야 한다.
            var game = RunFullGame(seed: 7);

            Assert.AreEqual(GamePhase.GameOver, game.Phase);
            Assert.IsTrue(game.Winner.HasValue);
            Assert.Less(game.TurnNumber, MaxTurns);
        }

        [Test]
        public void AiVsAi_SurvivorsAlwaysOccupyContiguousPositionsFromZero()
        {
            // R9 앞당김이 게임 전체에 걸쳐 유지되는지 — 종료 시점에 생존자는 0부터 연속이어야 한다
            foreach (int seed in new[] { 7, 42, 100, 9999 })
            {
                var game = RunFullGame(seed);
                foreach (var player in game.Players)
                {
                    var positions = player.CharacterZone.Select(u => u.Position).OrderBy(p => p).ToList();
                    CollectionAssert.AreEqual(
                        Enumerable.Range(0, positions.Count).ToList(), positions,
                        $"시드 {seed} P{player.PlayerId}: 생존자 Position이 0부터 연속이 아님");
                }
            }
        }

        // ---------------- 미구현 카드 스킵 ----------------

        [Test]
        public void AiVsAi_NoEffectsRegistered_HitsTurnCapWithoutExceptionOrWinner()
        {
            // 극단적 상황: 스킬/캐릭터 효과가 전혀 등록되지 않았다고 가정(모든 카드가 "미구현").
            // 데미지가 전혀 나지 않으니 아무도 죽지 않고, 게임은 크래시 없이 최대 턴까지 계속 진행되어야 한다.
            // "미구현 카드는 스킵하고 게임은 안 멈춘다"(CLAUDE.md)를 극단값에서 확인.
            GameManager game = null;
            Assert.DoesNotThrow(() => game = RunFullGame(seed: 42, maxTurns: 10, withEffects: false));

            Assert.AreEqual(10, game.TurnNumber);
            Assert.IsFalse(game.Winner.HasValue, "데미지가 전혀 없으니 아무도 죽지 않아야 함");
            foreach (var player in game.Players)
                Assert.AreEqual(GameRules.PartySize, player.CharacterZone.Count, "캐릭터가 하나도 죽지 않아야 함");
        }

        [Test]
        public void AiVsAi_PartiallyImplementedEffects_UnknownCardsAreSkippedButGameCompletes()
        {
            // 실제 스타터 덱 효과는 등록하되, EffectSystem 자체는 새로 만들어 아무 것도 등록하지 않은
            // "완전 미구현" 경로를 스킬 쪽만 별도로 확인 — 경고 로그를 세어 스킵이 실제로 일어났는지도 본다.
            var db = LoadRealDatabase();
            var rng = new System.Random(7);
            var game = new GameManager(db, rng);
            var ai = new[] { new AIController(0, db, rng), new AIController(1, db, rng) };
            AIController.Attach(game, ai);

            var emptyEffects = new EffectSystem();
            var warnings = new System.Collections.Generic.List<string>();
            emptyEffects.Log = message => warnings.Add(message);
            game.Log = message => warnings.Add(message);
            game.SkillEffectResolver = ctx => emptyEffects.Execute(ctx);

            var deck0 = DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "Aggro"));
            var deck1 = DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == "MidRange_Blood"));
            game.StartGame(deck0, deck1);
            game.RunBanPick();
            game.RunMulligan();
            for (int p = 0; p < GameRules.PlayerCount; p++)
                game.PlaceCostFromHand(p, ai[p].ChooseMulliganCost(game.Players[p]));

            Assert.DoesNotThrow(() =>
            {
                for (int turn = 0; turn < 5 && !game.Winner.HasValue; turn++)
                {
                    game.BeginTurn();
                    for (int p = 0; p < GameRules.PlayerCount; p++)
                        game.PlaceCostFromDraw(p, ai[p].ChooseDrawCost(game.Players[p], game.LastDrawn[p]));

                    game.BeginReadyPhase();
                    for (int p = 0; p < GameRules.PlayerCount; p++)
                        foreach (var play in ai[p].ChoosePlays(game.Players[p], game.Players[GameManager.Opponent(p)]))
                            game.SubmitActiveCard(p, play.Skill, play.User, play.AsAccel, false, play.SkillOption);

                    game.RunActionPhase();
                }
            });

            Assert.IsTrue(warnings.Any(w => w.Contains("미구현 효과")), "미구현 카드 경고가 실제로 남아야 함");
        }
    }
}
