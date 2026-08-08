using System;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;
using UnityEngine;

namespace CrossAccel.UI
{
    public enum BanPickStepKind { Ban, Pick }

    /// <summary>진행 순서 한 스텝. Count는 그 스텝에서 몇 장을 확정해야 하는지.</summary>
    public readonly struct BanPickStep
    {
        public readonly BanPickStepKind Kind;
        public readonly int Count;

        public BanPickStep(BanPickStepKind kind, int count)
        {
            Kind = kind;
            Count = count;
        }
    }

    /// <summary>
    /// 밴픽 세션 — BanScene ↔ PickScene을 오가도 유지돼야 하므로 static으로 둔다.
    ///
    /// GameManager 연결 구조 (중요):
    /// GameManager.RunBanPick()은 BanSelector/PickSelector 델리게이트를 동기로 호출하며 2라운드를
    /// 통째로 도는 블로킹 경로라, UI 클릭을 그 안에서 기다릴 수 없다(메인 스레드가 멈춰 화면이 안 그려짐).
    /// 그래서 UI는 여기서 자기 흐름대로 밴픽을 끝내고, 확정 결과만
    /// <see cref="GameManager.ApplyBanPickResult"/>로 인계한다. 밴픽 계산이 한 곳에서만 일어나므로
    /// 두 경로(AI 대전용 RunBanPick / UI용 인계)가 어긋날 수 없다.
    ///
    /// 상대(AI) 결정은 각 스텝에서 즉시 AIController로 처리한다 — 그래야 "상대가 밴한 내 카드"를
    /// 그 자리에서 화면에 표시할 수 있다 (UNITY_BANPICK_UI_SPEC.md 3번).
    ///
    /// 밴/픽은 전부 **카드 id 문자열**로 기록한다(GameManager와 같은 단위). UI가 쓰는 풀 인덱스는
    /// 표시용일 뿐이며 <see cref="MyPool"/>/<see cref="EnemyPool"/>을 통해 id로 변환된다.
    /// </summary>
    public static class BanPickState
    {
        public const int MyPlayerId = 0;
        public const int EnemyPlayerId = 1;

        private const string MyDeckName = "Aggro";
        private const string EnemyDeckName = "MidRange_Blood";

        /// <summary>
        /// 진행 순서 — RULES.md 4번 그대로: 2라운드, 라운드마다 (상대 덱에서 1장 밴) → (자기 덱에서 2장 픽).
        /// 밴 2 + 픽 4 + 잔여 4 = 10장.
        /// </summary>
        public static readonly BanPickStep[] Sequence =
        {
            new BanPickStep(BanPickStepKind.Ban, GameRules.BansPerRound),
            new BanPickStep(BanPickStepKind.Pick, GameRules.PicksPerRound),
            new BanPickStep(BanPickStepKind.Ban, GameRules.BansPerRound),
            new BanPickStep(BanPickStepKind.Pick, GameRules.PicksPerRound),
        };

        public static int StepIndex { get; private set; }
        public static BanPickStep CurrentStep => Sequence[StepIndex];
        public static bool IsFinished => StepIndex >= Sequence.Length;

        /// <summary>내 캐릭터 풀 10장 (표시용 고정 목록).</summary>
        public static List<CharacterData> MyPool { get; private set; } = new List<CharacterData>();

        /// <summary>상대 캐릭터 풀 10장.</summary>
        public static List<CharacterData> EnemyPool { get; private set; } = new List<CharacterData>();

        /// <summary>이 세션이 구동 중인 GameManager. 밴픽 확정 후 BattleScene이 이어받는다.</summary>
        public static GameManager Game { get; private set; }

        // 확정 기록 — 전부 카드 id
        private static readonly List<string> _myBans = new List<string>();      // 내가 밴한 상대 카드
        private static readonly List<string> _enemyBans = new List<string>();   // 상대가 밴한 내 카드
        private static readonly List<string> _myPicks = new List<string>();
        private static readonly List<string> _enemyPicks = new List<string>();

        /// <summary>
        /// [무엇] 배치 화면(PlaceScene)에서 확정한 출전 순서 — P1~P4 자리의 카드 id. 길이 4.
        /// [왜] 모듈 A의 출력 계약(UNITY_PORTING_SPEC 5절). 이 순서가 그대로 엔진의 Position이 된다:
        ///      PlacedOrder[0](P1·전방) → Position 0. 모듈 C가 리치 계산에 이 순서를 쓴다.
        /// [주의] 배치 전에는 <b>비어 있다</b>. 비었는데 배틀로 진입하면 파티 순서를 알 수 없으므로
        ///        조용히 기본값으로 넘어가지 말고 에러를 내야 한다 (CompleteAndSetup 참고).
        /// [주의] Reset()에서 반드시 비운다 — static 값이 남아 재시작 때 터진 전례가 있다.
        /// </summary>
        public static IReadOnlyList<string> PlacedOrder => _placedOrder;
        private static readonly List<string> _placedOrder = new List<string>();

        private static AIController _enemyAi;
        private static bool _started;

        // ===================== 세션 시작 =====================

        /// <summary>
        /// 세션이 아직 없으면 시작한다 (씬 컨트롤러가 Start마다 안전하게 호출).
        /// GameManager를 만들고 두 스타터 덱을 넣어 밴픽 준비 상태로 만든다.
        /// </summary>
        public static void EnsureStarted()
        {
            if (_started) return;

            var db = CardDatabaseProvider.Instance;

            // ARCHITECTURE.md: 난수는 시드 주입. UI는 매판 달라야 하므로 시드를 만들어 쓰되 로그로 남겨 재현 가능하게.
            int seed = Environment.TickCount;
            var rng = new System.Random(seed);
            Debug.Log($"[BanPick] 세션 시작 (seed={seed})");

            MyPool = LoadPool(db, MyDeckName);
            EnemyPool = LoadPool(db, EnemyDeckName);

            Game = new GameManager(db, rng) { Log = message => Debug.Log($"[GameManager] {message}") };
            Game.StartGame(
                DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == MyDeckName)),
                DeckSelection.FromDeckData(db.Decks.First(d => d.DeckName == EnemyDeckName)));

            _enemyAi = new AIController(EnemyPlayerId, db, rng);
            _started = true;
        }

        /// <summary>스타터 덱의 Character 카드만 뽑아 순서대로 (RULES.md 3번: 캐릭터 덱 10장).</summary>
        private static List<CharacterData> LoadPool(CardDatabase db, string deckName)
        {
            var deck = db.Decks.FirstOrDefault(d => d.DeckName == deckName);
            if (deck == null) return new List<CharacterData>();

            var pool = new List<CharacterData>();
            foreach (var entry in deck.Cards)
            {
                if (entry.CardType != "Character") continue;
                if (!db.Characters.TryGetValue(entry.CardId, out var data)) continue;

                // 실데이터는 캐릭터마다 count=1이라 인덱스↔id가 1:1이다. count>1이 생기면
                // 같은 id가 두 칸을 차지해 인덱스→id 변환은 되지만 id→인덱스는 모호해진다.
                for (int i = 0; i < entry.Count; i++)
                    pool.Add(data);
            }
            return pool;
        }

        // ===================== 조회 (UI 표시용) =====================

        /// <summary>
        /// [무엇] 내 풀의 이 카드를 상대가 밴했는가.
        /// [왜] 픽 화면에서 "상대 밴" 라벨 + 회색 + 픽 불가로 표시해야 한다 (전체 공개).
        /// </summary>
        public static bool IsMyCardBanned(int myPoolIndex) => _enemyBans.Contains(MyPool[myPoolIndex].Id);

        /// <summary>[무엇] 상대 풀의 이 카드를 내가 밴했는가. [왜] 밴 화면에서 회색 + "BAN" 표시.</summary>
        public static bool IsEnemyCardBanned(int enemyPoolIndex) => _myBans.Contains(EnemyPool[enemyPoolIndex].Id);

        /// <summary>
        /// [무엇] 상대 풀의 이 카드를 상대가 이미 픽했는가.
        /// [왜] 밴픽 전체 공개(UNITY_PORTING_SPEC 4-1)에서 "상대가 픽한 카드는 내가 밴할 수 없다"는
        ///      상호 배제 규칙을 화면과 클릭 양쪽에 반영하기 위해 필요하다.
        /// [주의] 진행 순서가 밴1→픽2→밴1→픽2라, 첫 밴 시점엔 상대 픽이 0장이고
        ///        두 번째 밴 시점엔 2장이다. 즉 이 판정은 두 번째 밴에서만 실제로 걸린다.
        /// </summary>
        public static bool IsEnemyCardPicked(int enemyPoolIndex) => _enemyPicks.Contains(EnemyPool[enemyPoolIndex].Id);

        /// <summary>[무엇] 내 풀의 이 카드를 이미 픽했는가. [왜] 픽 화면에서 ✓ 표시 + 재선택 방지.</summary>
        public static bool IsMyCardPicked(int myPoolIndex) => _myPicks.Contains(MyPool[myPoolIndex].Id);

        /// <summary>지금까지 픽한 내 캐릭터들 (픽 슬롯 표시용).</summary>
        public static IReadOnlyList<CharacterData> MyPickedCharacters =>
            _myPicks.Select(id => MyPool.First(c => c.Id == id)).ToList();

        // ===================== 확정 =====================

        /// <summary>
        /// 밴 스텝 확정 — 내가 고른 상대 카드를 밴하고, 같은 시점에 상대 AI도 내 카드 1장을 밴한다
        /// (RULES.md 4번: 서로 덱을 교환해 동시에 밴).
        /// </summary>
        public static void SubmitMyBan(int enemyPoolIndex)
        {
            string myBanId = EnemyPool[enemyPoolIndex].Id;
            _myBans.Add(myBanId);

            var myRemaining = AvailableIds(MyPlayerId);
            string enemyBanId = _enemyAi.ChooseBan(myRemaining);
            if (enemyBanId != null) _enemyBans.Add(enemyBanId);

            Debug.Log($"[BanPick] 내 밴 → {myBanId} / 상대 밴 → {enemyBanId}");
            StepIndex++;
        }

        /// <summary>픽 스텝 확정 — 내가 고른 카드들을 픽하고, 상대 AI도 같은 수만큼 자기 덱에서 픽한다.</summary>
        public static void SubmitMyPicks(IEnumerable<int> myPoolIndices)
        {
            foreach (int index in myPoolIndices)
                _myPicks.Add(MyPool[index].Id);

            var enemyRemaining = AvailableIds(EnemyPlayerId);
            var enemyChoice = _enemyAi.ChoosePicks(enemyRemaining, CurrentStep.Count);
            if (enemyChoice != null) _enemyPicks.AddRange(enemyChoice);

            Debug.Log($"[BanPick] 내 픽 → {string.Join(", ", _myPicks)} / 상대 픽 → {string.Join(", ", _enemyPicks)}");
            StepIndex++;
        }

        /// <summary>
        /// [무엇] 남은 카드 id — 밴되지도 픽되지도 않은 것. 마지막에 그대로 leftovers(HP 표시용 4장)가 된다.
        /// [왜] 상대 AI에게 넘길 후보 목록이자 최종 잔여 카드 계산에 같은 정의를 써서 어긋남을 막는다.
        /// [주의] <b>상호 배제가 여기서 성립한다</b> — 상대 후보에서 내가 밴한 카드(_myBans)를 빼기 때문에
        ///        상대 AI는 내가 밴한 카드를 픽할 수 없다 (UNITY_PORTING_SPEC 4-1 상호 배제).
        ///        반대 방향(내가 상대 픽 카드를 밴 못 함)은 밴 화면에서 클릭을 막아 처리한다.
        /// </summary>
        private static IReadOnlyList<string> AvailableIds(int playerId)
        {
            var pool = playerId == MyPlayerId ? MyPool : EnemyPool;
            var banned = playerId == MyPlayerId ? _enemyBans : _myBans;
            var picked = playerId == MyPlayerId ? _myPicks : _enemyPicks;

            return pool.Select(c => c.Id)
                       .Where(id => !banned.Contains(id) && !picked.Contains(id))
                       .ToList();
        }

        /// <summary>
        /// [무엇] 배치 화면에서 확정한 출전 순서를 기록한다 (P1~P4).
        /// [왜] 이 순서가 곧 엔진의 Position이므로, 배치 확정 시점에 단 한 번 저장해 둔다.
        /// [주의] 길이가 PartySize(4)가 아니면 배치가 덜 끝난 것이라 받지 않는다.
        /// </summary>
        public static void SetPlacedOrder(IEnumerable<string> cardIdsFrontToBack)
        {
            var order = cardIdsFrontToBack?.ToList() ?? new List<string>();
            if (order.Count != GameRules.PartySize)
            {
                Debug.LogError($"[BanPick] 배치 순서가 {order.Count}장 — {GameRules.PartySize}장이어야 한다. 무시한다.");
                return;
            }

            _placedOrder.Clear();
            _placedOrder.AddRange(order);
            Debug.Log($"[BanPick] 배치 순서 확정 (P1→P4): {string.Join(" → ", _placedOrder)}");
        }

        /// <summary>
        /// [무엇] 밴픽+배치 결과를 GameManager에 인계해 세팅(파티 배치 + 메인덱 구성)까지 끝낸다.
        /// [왜] UI가 계산을 끝내고 결과만 엔진에 넘기는 "결과 인계" 방식이다. 엔진의 RunBanPick은
        ///      델리게이트를 동기로 호출하는 블로킹 경로라 UI 클릭을 기다릴 수 없기 때문
        ///      (UNITY_PORTING_SPEC 1절 3번 — 같은 계산을 두 곳에서 하면 어긋난다).
        /// [주의] 호출 시점이 <b>픽 확정이 아니라 배치 확정</b>이다. 엔진의 RunSetup이
        ///        picks[p][i]를 그대로 Position i로 만들기 때문에, 배치 순서를 알기 전에 넘기면
        ///        파티 순서가 픽한 순서로 굳어버린다.
        /// [주의] PlacedOrder가 비어 있으면 <b>조용히 기본 순서로 진행하지 않고 에러 후 중단</b>한다.
        ///        잘못된 순서로 전투에 들어가면 리치 계산이 전부 틀어지는데 화면만 봐서는 눈치채기 어렵다.
        /// </summary>
        /// <returns>인계에 성공했으면 true. 배치 순서가 없어 중단했으면 false.</returns>
        public static bool CompleteAndSetup()
        {
            if (_placedOrder.Count != GameRules.PartySize)
            {
                Debug.LogError($"[BanPick] 배치 순서(PlacedOrder)가 {_placedOrder.Count}장이라 세팅을 중단한다. " +
                               "PlaceScene에서 4장을 모두 배치한 뒤에 확정해야 한다.");
                return false;
            }

            var picks = new IReadOnlyList<string>[GameRules.PlayerCount];
            // [왜] 내 파티는 픽한 순서가 아니라 배치 순서로 넘긴다 — 그래야 P1이 Position 0(최전선)이 된다.
            picks[MyPlayerId] = _placedOrder;
            picks[EnemyPlayerId] = _enemyPicks;

            var leftovers = new IReadOnlyList<string>[GameRules.PlayerCount];
            leftovers[MyPlayerId] = AvailableIds(MyPlayerId);
            leftovers[EnemyPlayerId] = AvailableIds(EnemyPlayerId);

            Game.ApplyBanPickResult(picks, leftovers);

            Debug.Log($"[BanPick] 완료 — 내 파티: {string.Join(", ", Game.Players[MyPlayerId].CharacterZone.Select(u => $"P{u.Position + 1}:{u.Data.Name}"))}");
            return true;
        }

        /// <summary>
        /// [무엇] 새 판을 위해 세션을 통째로 초기화한다.
        /// [왜] static이라 씬을 갈아도 값이 남는다. 초기화를 빠뜨리면 이전 판의 밴/픽/배치가 그대로
        ///      남아 "밴픽을 건너뛰거나" 엉뚱한 파티로 시작한다 — 실제로 겪은 버그다.
        /// [주의] 새 필드를 추가하면 <b>여기에도 반드시 추가</b>할 것. PlacedOrder 포함.
        /// </summary>
        public static void Reset()
        {
            StepIndex = 0;
            _myBans.Clear();
            _enemyBans.Clear();
            _myPicks.Clear();
            _enemyPicks.Clear();
            _placedOrder.Clear();
            _enemyAi = null;
            Game = null;
            _started = false;
        }
    }
}
