using System;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Core;
using CrossAccel.Data;
using CrossAccel.Effects;

namespace CrossAccel.Battle
{
    /// <summary>
    /// 게임 흐름 상태머신 (순수 C#). 밴픽 → 세팅 → 멀리건 → 턴 루프(드로우/레디/액션/엔드)를 진행하고
    /// 챌린지를 해결한다. 규칙 근거는 전부 docs/RULES.md.
    ///
    /// 드라이버(테스트·AI·UI)가 페이즈 메서드를 순서대로 호출하는 구조다. "누가 무엇을 고르는가"는
    /// 전부 델리게이트로 위임하므로 같은 로직을 AI/사람/테스트가 공유한다 (ARCHITECTURE.md).
    /// MonoBehaviour를 모르며, 로그도 <see cref="Log"/> 델리게이트로만 낸다.
    /// </summary>
    public class GameManager
    {
        /// <summary>챌린지 승자가 없음 (양측 무효) — RULES.md R6.</summary>
        public const int NoWinner = -1;

        private readonly CardDatabase _database;
        private readonly Random _rng;
        private DeckSelection[] _deckSelections;

        public GameManager(CardDatabase database, Random rng)
        {
            _database = database;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));

            Players = new PlayerState[GameRules.PlayerCount];
            for (int i = 0; i < GameRules.PlayerCount; i++)
                Players[i] = new PlayerState(_rng, i);
        }

        // ===================== 상태 =====================

        public GamePhase Phase { get; private set; } = GamePhase.BanPick;
        public int TurnNumber { get; private set; }
        public PlayerState[] Players { get; }

        /// <summary>게임이 끝났으면 승자 playerId, 양측 동시 전멸이면 <see cref="NoWinner"/>. 진행 중이면 null.</summary>
        public int? Winner { get; private set; }

        /// <summary>이번 턴 드로우 페이즈에 뽑은 카드 — 코스트존 배치 제한 검사용 (RULES.md 5-1).</summary>
        public List<SkillData>[] LastDrawn { get; private set; }

        /// <summary>이번 액션 페이즈에 발생한 챌린지 기록 (RunActionPhase 시작 시 초기화).</summary>
        public List<ChallengeRecord> ChallengeLog { get; } = new List<ChallengeRecord>();

        /// <summary>이번 턴에 각 플레이어가 발동한 스킬 수 (S01 "이전에 사용된 스킬" 판정). 턴 시작 시 리셋.</summary>
        public int[] SkillsResolvedThisTurn { get; private set; } = new int[GameRules.PlayerCount];

        /// <summary>다음에 발동하는 스킬 1장에 실릴 추가 데미지 (C14/C31/C19가 걸어둔다). 사용 시 소모.</summary>
        public int[] NextSkillDamageBonus { get; private set; } = new int[GameRules.PlayerCount];

        /// <summary>캐릭터 고유 효과 시스템. null이면 캐릭터 효과 없이 진행한다 (Phase 3까지의 동작).</summary>
        public CharacterEffectSystem CharacterEffects { get; set; }

        // ===================== 이벤트 / 델리게이트 =====================

        /// <summary>페이즈가 바뀔 때마다 발생. 뷰가 구독한다.</summary>
        public event Action<GamePhase> OnPhaseChanged;

        /// <summary>게임 종료 시 승자 playerId와 함께 발생 (양측 동시 전멸이면 <see cref="NoWinner"/>).</summary>
        public event Action<int> OnGameOver;

        // ── 연출 훅 (모듈 D 전용) ────────────────────────────────────────────
        // [왜] RunActionPhase가 액션+엔드를 한 호출에 끝내고 반환하기 때문에, 끝난 뒤 상태만 봐서는
        //   "카드 한 장씩", "액셀 덱탑 한 장씩" 같은 단계별 연출을 붙일 수가 없었다.
        //   진행 중 발생 지점에서 이벤트를 쏴 주면 UI가 그 위에 연출을 얹을 수 있다.
        // [주의] 순수 가산이다 — 구독자가 없으면 아무 일도 일어나지 않고 기존 흐름·반환값도 그대로다.
        //   이벤트 핸들러에서 게임 상태를 바꾸지 말 것 (판정은 엔진이 진실).

        /// <summary>[무엇] 카드 한 장이 발동되기 직전. [주의] 코스트 지불 전이라 불발될 수도 있다.</summary>
        public event Action<ActiveSlot> OnSlotActivating;

        /// <summary>[무엇] 액셀(속도 동점) 판정 1건이 끝났을 때. [왜] 덱탑 공개 연출을 이 시점에 재생한다.</summary>
        public event Action<ChallengeRecord> OnChallengeResolved;

        /// <summary>[무엇] 엔드 페이즈에서 캐릭터가 사망해 캐릭터 존을 떠날 때. [왜] 사망·전방 당김 연출용.</summary>
        public event Action<CharacterUnit> OnCharacterDied;

        /// <summary>진행 로그. 기본 null(무음) — 테스트에서 시끄럽지 않게 하려고 UnityEngine.Debug를 쓰지 않는다.</summary>
        public Action<string> Log;

        /// <summary>
        /// 밴픽 1단계 — 상대 덱을 받아 밴할 카드를 고른다 (RULES.md 4번).
        /// 인자: (밴하는 플레이어, 상대 덱의 밴 가능 카드 목록) → 밴할 카드 id.
        /// </summary>
        public Func<int, IReadOnlyList<string>, string> BanSelector;

        /// <summary>
        /// 밴픽 2단계 — 자기 덱에서 배치할 캐릭터를 고른다 (RULES.md 4번).
        /// 인자: (고르는 플레이어, 자기 덱의 픽 가능 카드 목록, 골라야 하는 수) → 고른 카드 id들.
        /// </summary>
        public Func<int, IReadOnlyList<string>, int, IReadOnlyList<string>> PickSelector;

        /// <summary>타겟 선택 위임. null이면 상대 캐릭터 존에서 가장 앞(Position 최소)의 생존 캐릭터.</summary>
        public Func<CharacterUnit, PlayerState, SkillData, CharacterUnit> TargetSelector;

        /// <summary>선택 발동형 효과를 발동할지 결정. null이면 항상 발동. Phase 4의 캐릭터 효과가 사용한다.</summary>
        public Func<CharacterUnit, bool> EffectActivationPolicy;

        /// <summary>
        /// 두 번째 스킬을 가진 카드에서 어느 쪽으로 낼지 결정한다 (1 = skill1, 2 = skill2).
        /// 인자: (사용 캐릭터, 스킬 카드). null이면 항상 skill1.
        /// 레디 페이즈에 SubmitActiveCard가 호출하며, 그때 확정된 값이 액션 페이즈까지 유지된다 (RULES.md R14).
        /// </summary>
        public Func<CharacterUnit, SkillData, int> SkillOptionSelector;

        /// <summary>
        /// 스킬 효과 실행 위임. Phase 4에서 <c>EffectSystem.Execute</c>를 여기 연결한다.
        /// Phase 3에서는 비어 있어도 게임 흐름이 끝까지 돌아야 한다 (효과 없이 페이즈만 진행).
        /// </summary>
        public Action<EffectContext> SkillEffectResolver;

        /// <summary>
        /// 지불량이 정해져 있지 않은 효과(S79 X코스트, C21 HP→코스트 변환)에서 얼마를 낼지 결정한다.
        /// 인자: (지불하는 플레이어, 지불 가능한 최대) → 실제 지불량. null이면 기본 휴리스틱.
        /// </summary>
        public Func<PlayerState, int, int> VariableCostPolicy;

        /// <summary>선택 발동형 효과의 발동 여부 (기본: 항상 발동).</summary>
        public bool ShouldActivate(CharacterUnit unit) => EffectActivationPolicy?.Invoke(unit) ?? true;

        /// <summary>가변 지불량 결정. 기본값은 최대 3까지 (프로토타입 휴리스틱).</summary>
        public int DecideVariableCost(PlayerState payer, int maxAffordable)
        {
            int capped = Math.Max(0, maxAffordable);
            return VariableCostPolicy?.Invoke(payer, capped) ?? Math.Min(3, capped);
        }

        /// <summary>다음 스킬에 실릴 데미지 보너스를 누적한다 (C14, C31, C19).</summary>
        public void AddNextSkillDamageBonus(int playerId, int amount) =>
            NextSkillDamageBonus[playerId] += amount;

        /// <summary>효과로 인한 드로우 (덱 0장이면 트래쉬 재순환 — RULES.md 3번).</summary>
        public void DrawToHand(int playerId, int count) => Players[playerId].Draw(count);

        // ===================== 밴픽 / 세팅 =====================

        /// <summary>양쪽 덱을 받아 게임을 시작한다. 실제 밴픽 진행은 <see cref="RunBanPick"/>.</summary>
        public void StartGame(DeckSelection player0Deck, DeckSelection player1Deck)
        {
            _deckSelections = new[] { player0Deck, player1Deck };
            TurnNumber = 0;
            Winner = null;
            SetPhase(GamePhase.BanPick);
        }

        /// <summary>
        /// 밴픽 2라운드를 진행한다 (RULES.md 4번). 라운드마다
        /// (1) 서로 덱을 교환해 상대 덱에서 1장 밴 → (2) 각자 자기 덱에서 2장 픽.
        /// 2라운드 후 밴 2장 + 픽 4장 + 잔여 4장 = 10장이 딱 맞는다.
        ///
        /// 밴 대상은 아직 픽되지 않은 카드로 한정한다 — 픽한 카드까지 밴할 수 있으면 위 산술이 깨진다
        /// (RULES.md 11번에 도출 근거 기록).
        ///
        /// 밴픽이 끝나면 이어서 세팅(캐릭터 배치 + HP 표시 + 메인덱 셔플)까지 수행한다.
        /// </summary>
        public void RunBanPick()
        {
            if (_database == null) throw new InvalidOperationException("밴픽에는 CardDatabase가 필요합니다.");
            if (_deckSelections == null) throw new InvalidOperationException("StartGame을 먼저 호출하세요.");
            if (BanSelector == null || PickSelector == null)
                throw new InvalidOperationException("BanSelector / PickSelector를 주입하세요.");

            SetPhase(GamePhase.BanPick);

            var available = new List<string>[GameRules.PlayerCount];
            var picks = new List<string>[GameRules.PlayerCount];
            for (int p = 0; p < GameRules.PlayerCount; p++)
            {
                available[p] = new List<string>(_deckSelections[p].CharacterDeck);
                picks[p] = new List<string>();
            }

            for (int round = 0; round < GameRules.BanPickRounds; round++)
            {
                // (1) 덱 교환 — 각자 상대 덱에서 밴할 카드를 고른다. 고르는 건 동시이므로 먼저 다 고르고 나서 적용.
                var bans = new string[GameRules.PlayerCount];
                for (int p = 0; p < GameRules.PlayerCount; p++)
                    bans[p] = BanSelector(p, available[Opponent(p)]);

                for (int p = 0; p < GameRules.PlayerCount; p++)
                {
                    int victim = Opponent(p);
                    if (available[victim].Remove(bans[p]))
                        Log?.Invoke($"[밴픽 R{round + 1}] P{p} → P{victim}의 {bans[p]} 밴");
                    else
                        Log?.Invoke($"[밴픽 R{round + 1}] P{p}의 밴 대상 '{bans[p]}'이 P{victim} 덱에 없음 — 무시");
                }

                // (2) 각자 자기 덱에서 픽
                for (int p = 0; p < GameRules.PlayerCount; p++)
                {
                    var chosen = PickSelector(p, available[p], GameRules.PicksPerRound);
                    foreach (var id in chosen)
                    {
                        if (!available[p].Remove(id)) continue;
                        picks[p].Add(id);
                    }
                }
            }

            RunSetup(picks, available);
        }

        /// <summary>
        /// 이미 외부에서 끝낸 밴픽 결과를 받아 세팅만 수행한다 (UI 경로).
        ///
        /// <see cref="RunBanPick"/>은 델리게이트로 밴픽 진행 자체를 담당하는 동기 경로라
        /// AI 대전에는 맞지만, UI는 클릭을 기다려야 해서 그 안에서 블로킹할 수 없다.
        /// 그래서 UI는 자기 흐름대로 밴픽을 끝낸 뒤 확정된 결과만 이리로 넘긴다.
        /// 밴픽 계산이 한 곳에서만 일어나므로 두 경로가 어긋날 여지가 없다.
        ///
        /// picks/leftovers는 플레이어 id로 인덱싱한다 (picks[0] = P0가 배치할 캐릭터 id들).
        /// </summary>
        public void ApplyBanPickResult(IReadOnlyList<string>[] picks, IReadOnlyList<string>[] leftovers)
        {
            if (_database == null) throw new InvalidOperationException("세팅에는 CardDatabase가 필요합니다.");
            if (_deckSelections == null) throw new InvalidOperationException("StartGame을 먼저 호출하세요.");
            if (picks == null || picks.Length != GameRules.PlayerCount)
                throw new ArgumentException($"picks는 플레이어 수({GameRules.PlayerCount})만큼 필요합니다.", nameof(picks));
            if (leftovers == null || leftovers.Length != GameRules.PlayerCount)
                throw new ArgumentException($"leftovers는 플레이어 수({GameRules.PlayerCount})만큼 필요합니다.", nameof(leftovers));

            for (int p = 0; p < GameRules.PlayerCount; p++)
            {
                if (picks[p] == null || picks[p].Count != GameRules.PartySize)
                    Log?.Invoke($"[세팅] P{p} 픽 {picks[p]?.Count ?? 0}장 (규칙: {GameRules.PartySize}장) — 데이터 확인 필요");
            }

            RunSetup(picks, leftovers);
        }

        /// <summary>
        /// 세팅 — 픽한 캐릭터를 순서대로 캐릭터 존에 배치하고, 남은 카드를 HP 표시용으로 둔다 (RULES.md 4번).
        /// 배치 순서가 곧 Position이며 0 = 최전선이다 (RULES.md R4).
        /// </summary>
        private void RunSetup(IReadOnlyList<string>[] picks, IReadOnlyList<string>[] leftovers)
        {
            SetPhase(GamePhase.Setup);

            for (int p = 0; p < GameRules.PlayerCount; p++)
            {
                var player = Players[p];
                player.CharacterZone.Clear();

                for (int i = 0; i < picks[p].Count && i < GameRules.PartySize; i++)
                {
                    if (!_database.Characters.TryGetValue(picks[p][i], out var data))
                    {
                        Log?.Invoke($"[세팅] P{p}: 캐릭터 '{picks[p][i]}' 데이터 없음 — 스킵");
                        continue;
                    }
                    player.CharacterZone.Add(new CharacterUnit(data, p, position: i));
                }

                player.HpCounterCards.Clear();
                player.HpCounterCards.AddRange(leftovers[p]);

                BuildMainDeck(p, _deckSelections[p]);
            }
        }

        /// <summary>덱 구성에서 메인덱을 만들고 셔플한다 (RULES.md 4번 준비 1단계).</summary>
        private void BuildMainDeck(int playerId, DeckSelection selection)
        {
            var deck = Players[playerId].Deck;
            deck.Clear();

            foreach (var entry in selection.MainDeck)
            {
                if (!_database.Skills.TryGetValue(entry.CardId, out var skill))
                {
                    // DATA_SCHEMA.md 알려진 이슈: 스타터 덱의 S53이 스킬 테이블에 없음. 게임을 멈추지 않고 경고만.
                    Log?.Invoke($"[덱 구성] P{playerId}: 스킬 '{entry.CardId}' 데이터 없음 — 스킵");
                    continue;
                }
                for (int i = 0; i < entry.Count; i++)
                    deck.Add(skill);
            }

            Players[playerId].Shuffle(deck);
        }

        // ===================== 멀리건 =====================

        /// <summary>준비(멀리건) — 각자 6장을 뽑는다. 이후 드라이버가 2장을 코스트존에 배치한다 (RULES.md 4번).</summary>
        public void RunMulligan()
        {
            SetPhase(GamePhase.Mulligan);

            foreach (var player in Players)
                player.Draw(GameRules.MulliganDrawCount);
        }

        /// <summary>
        /// 손패의 카드를 코스트존에 배치한다 (RULES.md 4번 준비 2단계: 2장).
        /// 상한을 넘겨 요청하면 넘는 만큼은 배치하지 않는다.
        /// </summary>
        public int PlaceCostFromHand(int playerId, IEnumerable<SkillData> cards, int maxCount = GameRules.MulliganCostCount)
        {
            int placed = 0;
            foreach (var card in cards)
            {
                if (placed >= maxCount) break;
                if (Players[playerId].PlaceCostFromHand(card)) placed++;
            }
            return placed;
        }

        // ===================== 턴: 드로우 페이즈 =====================

        /// <summary>
        /// 새 턴을 시작한다. 매 턴 시작 시 코스트를 전부 언레스트하고 (RULES.md R2),
        /// 드로우 페이즈로 들어가 각자 2장을 뽑는다 (RULES.md 5-1).
        /// </summary>
        public void BeginTurn()
        {
            TurnNumber++;

            SkillsResolvedThisTurn = new int[GameRules.PlayerCount];
            NextSkillDamageBonus = new int[GameRules.PlayerCount];

            foreach (var player in Players)
                player.ResetForNewTurn();

            SetPhase(GamePhase.Draw);

            LastDrawn = new List<SkillData>[GameRules.PlayerCount];
            for (int p = 0; p < GameRules.PlayerCount; p++)
                LastDrawn[p] = Players[p].Draw(GameRules.DrawPhaseDrawCount);
        }

        /// <summary>
        /// 드로우 페이즈에 방금 뽑은 카드 중 최대 2장을 코스트존으로 보낸다 (RULES.md 5-1).
        /// 이번 턴에 뽑지 않은 카드는 보낼 수 없다. 코스트존에 놓지 않은 카드는 그대로 패에 남는다.
        /// </summary>
        public int PlaceCostFromDraw(int playerId, IEnumerable<SkillData> cards)
        {
            var drawnThisTurn = new List<SkillData>(LastDrawn?[playerId] ?? new List<SkillData>());

            int placed = 0;
            foreach (var card in cards)
            {
                if (placed >= GameRules.DrawPhaseMaxToCost) break;
                if (!drawnThisTurn.Remove(card))
                {
                    Log?.Invoke($"[드로우] P{playerId}: '{card?.Id}'는 이번 턴에 뽑은 카드가 아님 — 코스트 배치 불가");
                    continue;
                }
                if (Players[playerId].PlaceCostFromHand(card)) placed++;
            }
            return placed;
        }

        // ===================== 턴: 레디 페이즈 =====================

        /// <summary>레디 페이즈 시작 — 액티브 존을 비우고 카드 지정을 받는다 (RULES.md 5-2).</summary>
        public void BeginReadyPhase()
        {
            SetPhase(GamePhase.Ready);

            foreach (var player in Players)
                player.ActiveZone.Clear();

            // "레디 페이즈 전" 캐릭터 효과 (C07, C14, C31, C39, C41, C42, C04, C21)
            CharacterEffects?.FireTiming(this, EffectTiming.ReadyPhaseBefore);
        }

        /// <summary>
        /// 사용할 카드를 액티브 존에 뒷면으로 놓는다. 엑셀 지정 여부를 함께 받는다 (RULES.md 5-2).
        /// 두 번째 스킬이 있는 카드는 여기서 skill1/skill2를 확정한다 (RULES.md R14).
        /// </summary>
        /// <param name="skillOption">1 = skill1, 2 = skill2. 0(기본)이면 <see cref="SkillOptionSelector"/>에 위임.</param>
        public bool SubmitActiveCard(int playerId, SkillData skill, CharacterUnit user,
                                     bool asAccel = false, bool asSwift = false, int skillOption = 0)
        {
            var player = Players[playerId];
            if (skill == null || !player.Hand.Remove(skill)) return false;

            int resolved = ResolveSkillOption(user, skill, skillOption);
            player.ActiveZone.Add(new ActiveSlot(playerId, skill, user, asAccel, asSwift, resolved));
            return true;
        }

        /// <summary>
        /// [무엇] 레디 페이즈에 낸 카드를 취소해 손패로 되돌린다 (<see cref="SubmitActiveCard"/>의 역연산).
        /// [왜] 프로토타입의 레디 흐름은 배치를 되돌릴 수 있어야 한다. 존 사이 카드 이동은 엔진의 책임이라
        ///      UI가 ActiveZone/Hand를 직접 건드리지 않도록 짝이 되는 API를 둔다.
        /// [주의] 코스트는 배치 시점이 아니라 발동 시점(ActivateSlot)에 지불되므로 <b>환급할 코스트가 없다</b>.
        ///        신속 자원은 엔진이 관리하지 않으므로 UI가 되돌린다.
        /// [주의] 이미 해결된(Resolved) 슬롯은 취소할 수 없다 — 액션 페이즈가 시작된 뒤이기 때문.
        /// </summary>
        /// <returns>취소했으면 true. 슬롯이 없거나 이미 해결됐으면 false.</returns>
        public bool CancelActiveCard(int playerId, ActiveSlot slot)
        {
            if (slot == null || slot.Resolved) return false;

            var player = Players[playerId];
            if (!player.ActiveZone.Remove(slot)) return false;

            player.Hand.Add(slot.Skill);
            return true;
        }

        /// <summary>
        /// 낼 스킬 쪽을 확정한다. 두 번째 스킬이 없는 카드는 항상 1이고,
        /// 명시적으로 넘긴 값이 있으면 그것을, 없으면 SkillOptionSelector를 따른다.
        /// </summary>
        private int ResolveSkillOption(CharacterUnit user, SkillData skill, int requested)
        {
            if (!skill.HasSkill2) return 1;

            int option = requested != 0
                ? requested
                : SkillOptionSelector?.Invoke(user, skill) ?? 1;

            return option == 2 ? 2 : 1;
        }

        // ===================== 턴: 액션 페이즈 =====================

        /// <summary>
        /// 액션 페이즈 — 엑셀로 지정한 카드를 먼저 속도 순서대로 발동하고, 그 다음 나머지를 발동한다
        /// (RULES.md 5-3, 6번). 처리가 끝나면 엔드 페이즈로 이어진다.
        /// </summary>
        public void RunActionPhase()
        {
            SetPhase(GamePhase.Action);
            ChallengeLog.Clear();

            // "액션 페이즈 전" / "배틀 페이즈 개시 시" 캐릭터 효과 (C16, C19, C24, C02, C25, C29)
            // RULES.md 11번: "배틀 페이즈 개시 시" = 매 턴 액션 페이즈 시작
            CharacterEffects?.FireTiming(this, EffectTiming.ActionPhaseBefore);

            // 신속 그룹이 먼저, 그다음 일반 그룹 (RULES.md 7번 신속: "신속 카드는 보통 카드보다 먼저 적용").
            ResolveGroup(swiftGroup: true);
            ResolveGroup(swiftGroup: false);

            RunEndPhase();
        }

        /// <summary>
        /// [무엇] 신속 그룹 또는 일반 그룹을 속도 내림차순으로 해결한다 (RULES.md R1: 속도가 높을수록 먼저).
        /// [왜] 발동 순서는 [신속 그룹(속도순)] → [일반 그룹(속도순)]이다.
        /// [주의] <b>예전에는 이 그룹을 IsAccel(엑셀)로 나눴다.</b> 이름만 엑셀이었을 뿐 실제로 하던 역할은
        ///        "먼저 발동하는 그룹" = 신속이었다. 엑셀은 그룹이 아니라 <b>속도 동점 시 걸리는 판정</b>
        ///        (덱탑 무기 매칭, <see cref="ResolveChallenge"/>)이므로 그룹핑에서 분리했다.
        ///        따라서 신속 그룹 안에서도, 일반 그룹 안에서도 속도가 같으면 액셀 판정이 걸린다.
        /// [주의] 같은 속도에 양쪽 플레이어의 카드가 있으면 챌린지에 들어간다 (RULES.md 6-5).
        ///        한쪽에 여러 장이면 낸 순서대로 1:1로 짝짓고, 짝이 없는 카드는 챌린지 없이 발동한다 (R10).
        /// </summary>
        private void ResolveGroup(bool swiftGroup)
        {
            var pending = new List<ActiveSlot>();
            foreach (var player in Players)
                foreach (var slot in player.ActiveZone)
                    if (!slot.Resolved && slot.IsSwift == swiftGroup)
                        pending.Add(slot);

            if (pending.Count == 0) return;

            var speeds = pending
                .Select(slot => slot.Skill.Speed)
                .Distinct()
                .OrderByDescending(speed => speed);

            foreach (int speed in speeds)
            {
                // 낸 순서를 유지한 채 플레이어별로 나눈다 (R10의 "낸 순서대로 1:1").
                var sides = new List<ActiveSlot>[GameRules.PlayerCount];
                for (int p = 0; p < GameRules.PlayerCount; p++)
                    sides[p] = pending.Where(s => s.OwnerId == p && s.Skill.Speed == speed).ToList();

                int pairCount = Math.Min(sides[0].Count, sides[1].Count);
                for (int i = 0; i < pairCount; i++)
                    ResolveChallengePair(sides[0][i], sides[1][i]);

                for (int p = 0; p < GameRules.PlayerCount; p++)
                    for (int i = pairCount; i < sides[p].Count; i++)
                        ActivateSlot(sides[p][i]);
            }
        }

        /// <summary>속도 동률 두 카드를 챌린지로 해결한다. 단독 성공자만 효과를 발동한다 (RULES.md 8번).</summary>
        private void ResolveChallengePair(ActiveSlot slot0, ActiveSlot slot1)
        {
            // 라운드 기록을 함께 받는다 — 연출(모듈 D)이 재대결을 그대로 재생하기 위한 것.
            // 판정에는 아무 영향이 없다 (ResolveChallenge는 이 리스트에 append만 한다).
            var rounds = new List<ChallengeRound>();
            int winner = ResolveChallenge(slot0, slot1, rounds);
            var record = new ChallengeRecord(slot0, slot1, winner, rounds);
            ChallengeLog.Add(record);
            OnChallengeResolved?.Invoke(record);   // 연출 훅 (모듈 D)

            slot0.Resolved = true;
            slot1.Resolved = true;

            if (winner == slot0.OwnerId)
            {
                ActivateSlot(slot0);
                DiscardWithoutActivating(slot1);
            }
            else if (winner == slot1.OwnerId)
            {
                ActivateSlot(slot1);
                DiscardWithoutActivating(slot0);
            }
            else
            {
                // RULES.md R6: 양쪽 무효. 코스트는 발동 직전에 내므로 지불된 것이 없다 (반환 이슈 없음).
                Log?.Invoke("[챌린지] 양측 무효 — 두 카드 모두 발동 안 됨");
                DiscardWithoutActivating(slot0);
                DiscardWithoutActivating(slot1);
            }
        }

        /// <summary>
        /// 챌린지 (RULES.md 8번). 각자 덱 탑을 동시에 공개해 사용 캐릭터와 무기 타입이 같으면 성공.
        /// 둘 다 성공하면 실패자가 나올 때까지 반복하고, 둘 다 실패하면 양쪽 무효 (R6).
        /// 공개 카드는 트래쉬로 가며 (R5, PlayerState.RevealTopForChallenge가 처리),
        /// 챌린지 중에는 트래쉬 재순환이 없어 덱이 마르면 그 사람이 자동으로 진다 (R8, 8-5).
        ///
        /// [주의] roundLog는 <b>순수 기록</b>이다. 판정에는 전혀 쓰이지 않으므로 null이어도 결과가
        ///        똑같다 — 아래 판정 분기는 이 인자가 생기기 전과 한 글자도 다르지 않고,
        ///        <c>roundLog?.Add(...)</c> 호출만 끼워넣었다.
        /// </summary>
        /// <param name="roundLog">
        /// null이 아니면 라운드별 공개 결과를 순서대로 append한다 (재대결이면 여러 건).
        /// [왜] 연출(모듈 D)이 "둘 다 일치 → 재대결 N라운드"를 화면에 그대로 재현하려면 라운드마다
        ///      무엇이 공개됐는지 알아야 한다. 예전엔 UI가 트래쉬 증가분으로 이걸 역산하려 했는데,
        ///      스킬 부수효과가 트래쉬에 카드를 추가로 버리는 경우(피 계열 등) 증가분이 어긋나
        ///      복원이 깨졌다. 그걸 UI에서 흉내 내면 판정 재계산이 되어버리므로(UI는 엔진 값을
        ///      표시만 해야 한다 — UNITY_PORTING_SPEC 1절 4번), 엔진이 직접 기록을 남기게 했다.
        /// </param>
        /// <returns>승자 playerId, 또는 양측 무효면 <see cref="NoWinner"/>.</returns>
        public int ResolveChallenge(ActiveSlot slot0, ActiveSlot slot1, List<ChallengeRound> roundLog = null)
        {
            var stateA = Players[slot0.OwnerId];
            var stateB = Players[slot1.OwnerId];

            while (true)
            {
                // "각자 동시에 덱 탑을 오픈" — 한쪽이 마르더라도 반대쪽 카드는 이미 공개된 것으로 본다.
                var topA = stateA.RevealTopForChallenge();
                var topB = stateB.RevealTopForChallenge();

                bool aExhausted = topA == null;
                bool bExhausted = topB == null;

                if (aExhausted && bExhausted)
                {
                    Log?.Invoke("[챌린지] 양측 덱 소진 → 양쪽 무효");
                    roundLog?.Add(new ChallengeRound(topA, topB, successA: false, successB: false));
                    return NoWinner; // RULES.md 8-6
                }
                if (aExhausted)
                {
                    Log?.Invoke($"[챌린지] P{slot0.OwnerId} 덱 소진 → P{slot1.OwnerId} 자동 성공");
                    // 8-5의 "상대 자동 성공"을 기록에도 그대로 반영한다 (무기 매칭 여부와 무관).
                    roundLog?.Add(new ChallengeRound(topA, topB, successA: false, successB: true));
                    return slot1.OwnerId; // RULES.md 8-5
                }
                if (bExhausted)
                {
                    Log?.Invoke($"[챌린지] P{slot1.OwnerId} 덱 소진 → P{slot0.OwnerId} 자동 성공");
                    roundLog?.Add(new ChallengeRound(topA, topB, successA: true, successB: false));
                    return slot0.OwnerId;
                }

                bool aSuccess = MatchesWeapon(slot0.User, topA);
                bool bSuccess = MatchesWeapon(slot1.User, topB);
                roundLog?.Add(new ChallengeRound(topA, topB, aSuccess, bSuccess));

                if (aSuccess && !bSuccess) return slot0.OwnerId;
                if (bSuccess && !aSuccess) return slot1.OwnerId;
                if (!aSuccess && !bSuccess) return NoWinner; // RULES.md R6

                Log?.Invoke("[챌린지] 양측 성공 → 재공개");
            }
        }

        /// <summary>
        /// 공개된 카드가 사용 캐릭터의 무기 타입과 맞는지 (RULES.md 8-2).
        /// 공용 카드는 무기 이름이 겹치더라도 무조건 실패로 처리한다 (RULES.md R7).
        /// </summary>
        private static bool MatchesWeapon(CharacterUnit user, SkillData revealed)
        {
            if (user == null || revealed == null) return false;

            var revealedTypes = WeaponTypeParser.Parse(revealed.WeaponType);
            if (revealedTypes.Contains(WeaponTypeParser.Common)) return false;

            var userTypes = WeaponTypeParser.Parse(user.Data.WeaponType);
            return revealedTypes.Any(type => userTypes.Contains(type));
        }

        /// <summary>코스트를 지불하고 효과를 발동한다. 코스트가 모자라면 불발 (RULES.md 7번 코스트).</summary>
        private void ActivateSlot(ActiveSlot slot)
        {
            slot.Resolved = true;
            OnSlotActivating?.Invoke(slot);   // 연출 훅 (모듈 D)

            var owner = Players[slot.OwnerId];
            var opponent = Players[Opponent(slot.OwnerId)];

            // 레디 페이즈에 확정한 skill1/skill2 쪽 코스트를 낸다 (RULES.md R14).
            int cost = slot.Cost;
            if (!owner.PayCost(cost))
            {
                Log?.Invoke($"[P{slot.OwnerId}] {slot.Skill.Name}(skill{slot.SkillOption}) — 코스트 {cost} 부족, 불발");
                DiscardWithoutActivating(slot);
                return;
            }

            var target = TargetSelector != null
                ? TargetSelector(slot.User, opponent, slot.Skill)
                : DefaultTarget(opponent);

            // "다음 스킬 데미지 +N" 버프는 이 카드 1장에만 실리고 소모된다 (C14, C31, C19).
            int pendingBonus = NextSkillDamageBonus[slot.OwnerId];
            NextSkillDamageBonus[slot.OwnerId] = 0;

            SkillEffectResolver?.Invoke(new EffectContext
            {
                Game = this,
                Owner = owner,
                Opponent = opponent,
                User = slot.User,
                Target = target,
                Skill = slot.Skill,
                IsAccel = slot.IsAccel,
                DamageBonus = pendingBonus,
                SkillOption = slot.SkillOption
            });

            SkillsResolvedThisTurn[slot.OwnerId]++;
            slot.Activated = true;
            owner.Trash.Add(slot.Skill);
        }

        /// <summary>발동하지 못한 카드를 트래쉬로 보낸다 (RULES.md 2번: 트래쉬 = 버려지거나 사용된 카드).</summary>
        private void DiscardWithoutActivating(ActiveSlot slot)
        {
            slot.Resolved = true;
            slot.Activated = false;
            Players[slot.OwnerId].Trash.Add(slot.Skill);
        }

        /// <summary>기본 타겟 — 상대 캐릭터 존에서 가장 앞(Position 최소)의 생존 캐릭터.</summary>
        private static CharacterUnit DefaultTarget(PlayerState opponent) =>
            opponent.CharacterZone.Where(u => !u.IsDead).OrderBy(u => u.Position).FirstOrDefault();

        // ===================== 턴: 엔드 페이즈 =====================

        /// <summary>
        /// 엔드 페이즈 (RULES.md 5-4). HP가 0인 캐릭터를 코스트존으로 보내고 승패를 판정한 뒤,
        /// 방어도를 클리어한다 (RULES.md R3: 엔드 페이즈 효과 처리 후 소멸).
        /// 사망으로 생긴 빈자리는 뒤 캐릭터를 당겨서 메운다 — 생존자는 항상 Position 0부터 연속 (RULES.md R9).
        /// </summary>
        public void RunEndPhase()
        {
            SetPhase(GamePhase.End);

            // "턴 종료 시" 캐릭터 효과 (C12) — 사망 처리보다 먼저. 여기서 죽는 유닛도 이번 엔드에 정리된다.
            CharacterEffects?.FireTiming(this, EffectTiming.TurnEnd);

            foreach (var player in Players)
            {
                var dead = player.CharacterZone.Where(u => u.IsDead).ToList();
                foreach (var unit in dead)
                {
                    player.CharacterZone.Remove(unit);
                    player.PlaceCharacterInCostZone(unit.Data.Id);
                    Log?.Invoke($"[P{player.PlayerId}] {unit.Data.Name} 사망 → 코스트존");
                    OnCharacterDied?.Invoke(unit);   // 연출 훅 (모듈 D)
                }

                if (dead.Count > 0)
                    CompactFormation(player);
            }

            if (CheckGameOver()) return;

            foreach (var player in Players)
                foreach (var unit in player.CharacterZone)
                    unit.ClearDefense();
        }

        /// <summary>
        /// 앞당김 (RULES.md R9) — 사망으로 생긴 빈자리를 메워 남은 캐릭터를 Position 0부터 연속으로 재배치한다.
        /// 기존 배치 순서(앞에 있던 캐릭터가 계속 앞)는 유지한 채 번호만 당긴다.
        /// </summary>
        private static void CompactFormation(PlayerState player)
        {
            var ordered = player.CharacterZone.OrderBy(u => u.Position).ToList();

            player.CharacterZone.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].Position = i;
                player.CharacterZone.Add(ordered[i]);
            }
        }

        /// <summary>승패 판정 (RULES.md 1번/5-4). 양측 동시 전멸은 승자 없음으로 본다 (RULES.md 11번 가정).</summary>
        private bool CheckGameOver()
        {
            bool p0Lost = Players[0].HasLost;
            bool p1Lost = Players[1].HasLost;
            if (!p0Lost && !p1Lost) return false;

            Winner = p0Lost && p1Lost ? NoWinner : (p0Lost ? 1 : 0);
            SetPhase(GamePhase.GameOver);
            OnGameOver?.Invoke(Winner.Value);
            return true;
        }

        // ===================== 공용 =====================

        /// <summary>상대 플레이어 id.</summary>
        public static int Opponent(int playerId) => 1 - playerId;

        private void SetPhase(GamePhase phase)
        {
            Phase = phase;
            Log?.Invoke($"===== Phase: {phase} (Turn {TurnNumber}) =====");
            OnPhaseChanged?.Invoke(phase);
        }
    }

    /// <summary>챌린지 한 건의 결과 기록 (테스트·UI 관찰용).</summary>
    public class ChallengeRecord
    {
        public ActiveSlot Slot0 { get; }
        public ActiveSlot Slot1 { get; }

        /// <summary>승자 playerId, 또는 양측 무효면 <see cref="GameManager.NoWinner"/>.</summary>
        public int Winner { get; }

        /// <summary>
        /// [무엇] 덱탑 공개 라운드 기록. 재대결(양쪽 다 성공)이 일어난 만큼 여러 건이 쌓이고,
        ///        <b>마지막 원소가 승부를 가른 라운드</b>다. 최소 1건 (챌린지는 무조건 한 번은 공개한다).
        /// [왜] 연출(모듈 D)이 "A 공개 → 일치 표시 → B 공개 → 판정 → 재대결 N라운드"를 그대로
        ///      재생하려면 라운드별 공개 카드가 필요하다. 이 기록이 없던 동안 UI는 트래쉬 증가분으로
        ///      역산을 시도했는데, 스킬 부수효과가 트래쉬에 카드를 더 버리면 어긋나서 재대결을 못
        ///      보여줬다 (자세한 사연은 <see cref="GameManager.ResolveChallenge"/>의 roundLog 참고).
        /// [주의] <see cref="Winner"/>가 진실이다. 이 기록은 그 결과에 이르는 과정을 보여주기 위한
        ///        것이고, 마지막 라운드의 성공 플래그는 항상 Winner와 일치한다 (양측 무효면 둘 다 false).
        ///        UI가 이걸로 승패를 다시 판정해서는 안 된다.
        /// </summary>
        public IReadOnlyList<ChallengeRound> Rounds { get; }

        public ChallengeRecord(ActiveSlot slot0, ActiveSlot slot1, int winner,
                               IReadOnlyList<ChallengeRound> rounds = null)
        {
            Slot0 = slot0;
            Slot1 = slot1;
            Winner = winner;
            Rounds = rounds ?? Array.Empty<ChallengeRound>();
        }
    }

    /// <summary>
    /// [무엇] 챌린지 덱탑 공개 1회분 — 양쪽이 무엇을 공개했고 각자 성공했는지 (RULES.md 8번).
    /// [왜] 재대결 연출(모듈 D)이 라운드를 하나씩 느리게 보여주기 위한 기록. 순수 관찰용이며
    ///      엔진 판정은 이 구조체를 읽지 않는다.
    /// [주의] Revealed*가 null이면 <b>그 쪽 덱이 말라 공개조차 못 한 것</b>이다 (RULES.md 8-5/8-6).
    ///        그 경우 상대는 무기 매칭과 무관하게 자동 성공으로 기록된다 — 즉 Success=true인데
    ///        Revealed 카드의 무기가 사용 캐릭터와 다를 수 있다. 화면에 "일치!"로 쓰면 오해를 주므로,
    ///        상대편 Revealed가 null인지 함께 보고 "자동 성공"으로 표기할 것.
    /// </summary>
    public readonly struct ChallengeRound
    {
        /// <summary>Slot0 소유자가 공개한 카드. null = 덱 소진으로 공개 못 함.</summary>
        public SkillData RevealedA { get; }

        /// <summary>Slot1 소유자가 공개한 카드. null = 덱 소진으로 공개 못 함.</summary>
        public SkillData RevealedB { get; }

        /// <summary>Slot0 쪽이 이 라운드에서 성공했는지 (덱 소진 상대의 자동 성공 포함).</summary>
        public bool SuccessA { get; }

        /// <summary>Slot1 쪽이 이 라운드에서 성공했는지 (덱 소진 상대의 자동 성공 포함).</summary>
        public bool SuccessB { get; }

        /// <summary>공개된 카드의 무기 타입 (표시용). 덱 소진이면 null.</summary>
        public string WeaponA => RevealedA?.WeaponType;

        /// <summary>공개된 카드의 무기 타입 (표시용). 덱 소진이면 null.</summary>
        public string WeaponB => RevealedB?.WeaponType;

        public ChallengeRound(SkillData revealedA, SkillData revealedB, bool successA, bool successB)
        {
            RevealedA = revealedA;
            RevealedB = revealedB;
            SuccessA = successA;
            SuccessB = successB;
        }
    }
}
