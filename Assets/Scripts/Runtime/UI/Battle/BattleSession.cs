using System;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;
using CrossAccel.Effects;
using UnityEngine;

namespace CrossAccel.UI
{
    /// <summary>
    /// BattleScene이 GameManager를 이어받는 지점.
    ///
    /// 두 가지 경로를 모두 지원한다:
    ///  1. 정상 — 밴픽을 거쳐 온 경우: <see cref="BanPickState.Game"/>(세팅까지 끝난 GameManager)을 그대로 받는다.
    ///  2. 폴백 — BattleScene을 단독으로 Play한 경우: CardDatabase를 로드해 AI끼리 밴픽(RunBanPick)을
    ///     돌려 즉시 플레이 가능한 상태를 만든다. 개발 중 배틀 화면만 열어 확인할 때를 위한 것.
    ///
    /// 두 경로 모두 마지막에 배틀용 설치(효과 시스템 + AI 델리게이트)를 거친다 — 밴픽 단계에서는
    /// 이것들이 붙어있지 않기 때문에, 이 과정을 건너뛰면 액션 페이즈에 효과가 하나도 발동되지 않는다.
    /// </summary>
    public static class BattleSession
    {
        public const int MyPlayerId = 0;
        public const int EnemyPlayerId = 1;

        private const string MyDeckName = "Aggro";
        private const string EnemyDeckName = "MidRange_Blood";

        public static GameManager Game { get; private set; }

        /// <summary>양쪽 AI. 지금 단계에선 내 쪽 수(코스트 배치·카드 제출)도 이 휴리스틱이 대신 둔다.</summary>
        public static AIController[] Ai { get; private set; }

        /// <summary>밴픽을 거쳐 왔는지 (false면 폴백으로 만든 판).</summary>
        public static bool CameFromBanPick { get; private set; }

        public static GameManager Acquire()
        {
            if (Game != null) return Game;

            var db = CardDatabaseProvider.Instance;
            int seed = Environment.TickCount;
            var rng = new System.Random(seed);

            // 밴픽을 "시작만" 하고 중간에 배틀로 넘어온 경우가 있다 — 그때 GameManager는 존재하지만
            // 세팅이 안 끝나 캐릭터 존이 비어 있고 Phase도 BanPick에 머문다. 그 상태로 이어받으면
            // 파티가 0장이라 화면이 비고 "다음 ▶"으로도 진행되지 않으므로, 준비된 판인지 확인한다.
            var inherited = BanPickState.Game;
            bool inheritedIsReady = inherited != null &&
                                    inherited.Players.All(p => p.CharacterZone.Count > 0);

            if (inheritedIsReady)
            {
                Game = inherited;
                CameFromBanPick = true;
                Debug.Log("[Battle] 밴픽 결과를 이어받음");
            }
            else
            {
                if (inherited != null)
                    Debug.LogWarning($"[Battle] 밴픽이 끝나지 않은 채 배틀에 진입함 (Phase={inherited.Phase}) — 새 판을 만든다");
                else
                    Debug.Log($"[Battle] 밴픽을 거치지 않은 진입 — AI끼리 밴픽을 돌려 판을 만든다 (seed={seed})");

                Game = new GameManager(db, rng) { Log = m => Debug.Log($"[GameManager] {m}") };
                CameFromBanPick = false;
            }

            Ai = new[] { new AIController(MyPlayerId, db, rng), new AIController(EnemyPlayerId, db, rng) };

            // 델리게이트 일괄 연결. 밴픽용 BanSelector/PickSelector도 덮이지만 이 시점엔 밴픽이 끝났거나
            // (폴백일 때) 아직 시작 전이라 문제 없다.
            AIController.Attach(Game, Ai);

            // 효과 시스템 설치 — 없으면 SkillEffectResolver가 null이라 액션 페이즈가 아무 일도 하지 않는다.
            StarterDeckEffects.Install(Game);

            if (!CameFromBanPick)
            {
                Game.StartGame(
                    DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == MyDeckName)),
                    DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == EnemyDeckName)));
                Game.RunBanPick(); // 세팅(배치 + 메인덱)까지 내부에서 수행 → Phase = Setup
            }

            return Game;
        }

        /// <summary>다음 판을 위해 세션을 비운다 (밴픽 세션도 함께).</summary>
        public static void Reset()
        {
            Game = null;
            Ai = null;
            CameFromBanPick = false;
            BanPickState.Reset();
        }
    }
}
