// 담당 모듈: C (전투 흐름 + 엔진 브릿지) — UNITY_PORTING_SPEC.md 4-3~4-7 / docs/full_prototype.html
// 의존: BattleSession(엔진 인계), GameManager(엔진), SwiftResource·TargetLookupBridge(브릿지),
//       CharacterCardView(모듈 B) / ActSlotView·SkillCardView·HandFanLayout(뷰)
// 경계: 카드 비주얼=모듈 B, 연출(애니/오버레이/당김)=모듈 D. 여기는 흐름과 엔진 연결만 한다.

using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 전투 화면 총괄. 엔진 상태를 화면에 그리고, 유저 입력을 엔진 API로 넘긴다.
    /// [왜] full_prototype.html의 전투 흐름(초기코스트 → 드로우 → 코스트배치 → 레디 → 액션 → 엔드)을
    ///      유니티에서 그대로 돌리기 위한 진입점이다.
    /// [주의] <b>UI는 규칙을 재계산하지 않는다.</b> HP·코스트 잔량·발동 순서·데미지는 전부 엔진 값을 읽어
    ///        표시만 한다. UI가 따로 계산하면 엔진과 어긋난다(desync) — 실제로 겪은 문제라 원칙으로 굳혔다.
    /// [주의] 연출은 넣지 않는다. 엔진 훅(OnSlotActivating/OnChallengeResolved/OnCharacterDied)을
    ///        구독해 로그만 남겨두었고, 모듈 D가 그 자리에 애니메이션을 붙인다.
    /// </summary>
    public class BattleUIController : MonoBehaviour
    {
        /// <summary>[무엇] 초기 코스트로 버릴 장수. [출처] RULES.md 4번 준비 = GameRules.MulliganCostCount(2).</summary>
        private const int InitialCostCount = GameRules.MulliganCostCount;

        private GameManager _game;
        private readonly SwiftResource _swift = new SwiftResource();
        private readonly TargetLookupBridge _targets = new TargetLookupBridge();

        // 담당 모듈: D — 액션 페이즈 연출 재생기. RunAction()/훅 3개 핸들러 참고.
        private BattleActionPlayer _actionPlayer;

        /// <summary>[무엇] 액션 페이즈 연출이 재생 중인지. [왜] 재생 중 입력이 들어오면 엔진 상태와
        /// 화면이 어긋난다 — 재생이 끝나 RefreshAll()이 돌 때까지 모든 입력을 막는다.</summary>
        private bool _actionPlaying;

        // 엔진이 원래 쓰던 타겟 선택기(=AI용). 룩업 브릿지가 내 카드가 아닐 때 여기로 넘긴다.
        private System.Func<CharacterUnit, PlayerState, SkillData, CharacterUnit> _aiTargetSelector;

        // ── 뷰 참조 ──────────────────────────────────────────────────────────
        private readonly CharacterCardView[] _myCards = new CharacterCardView[GameRules.PartySize];
        private readonly CharacterCardView[] _opponentCards = new CharacterCardView[GameRules.PartySize];
        private readonly ActSlotView[] _myActSlots = new ActSlotView[GameRules.PartySize];
        private readonly ActSlotView[] _opponentActSlots = new ActSlotView[GameRules.PartySize];
        private readonly Button[] _myCardButtons = new Button[GameRules.PartySize];
        private readonly Button[] _opponentCardButtons = new Button[GameRules.PartySize];

        private HandFanLayout _handLayout;
        private SkillCardView[] _handCards;
        private RectTransform[] _handRects;

        private Text _phaseText;
        private Text _turnText;
        private Text _resultText;

        // 담당 모듈: D — 진행바/pip 연출. 값은 전부 엔진에서 읽어 넘기기만 한다.
        private PhaseStepBar _stepBar;
        private ResourcePips _costPips;
        private ResourcePips _swiftPips;
        private Text _myProfileText;
        private Text _opponentProfileText;
        private Text _logText;
        private Button _nextButton;
        private Text _nextButtonLabel;

        // ── 레디 페이즈 입력 상태 ────────────────────────────────────────────
        /// <summary>지금 손패에서 고른 카드 인덱스. -1이면 선택 없음.</summary>
        private int _selectedHandIndex = -1;

        /// <summary>타겟 지정 대기 중인 배치 (리치 내 적이 2명 이상일 때만 생긴다).</summary>
        private PendingPlacement _pending;

        /// <summary>초기 코스트 단계에서 고른 손패 인덱스들.</summary>
        private readonly List<int> _initialCostSelection = new List<int>();

        /// <summary>드로우 코스트 단계에서 고른 손패 인덱스들 (LastDrawn 안의 카드만).</summary>
        private readonly List<int> _drawCostSelection = new List<int>();

        /// <summary>화면 로그 (프로토타입의 8줄 로그와 동일한 역할).</summary>
        private readonly Queue<string> _logLines = new Queue<string>();
        private const int MaxLogLines = 8;

        /// <summary>[무엇] 타겟 클릭을 기다리는 배치 정보.</summary>
        private struct PendingPlacement
        {
            public SkillData Skill;
            public CharacterUnit User;
        }

        /// <summary>
        /// [무엇] 화면이 지금 어느 입력 단계인지. 엔진의 GamePhase와 별개로 UI 안에서만 쓰는 세부 단계다.
        /// [왜] 엔진의 Draw 페이즈 하나가 UI에서는 "뽑기 → 방금 뽑은 카드 중 코스트 고르기" 두 단계로 나뉜다.
        /// </summary>
        private enum UiStep
        {
            InitialCost,   // 손패 6장 중 2장 버리기 (엔진 Phase = Mulligan)
            DrawCost,      // 방금 뽑은 카드 중 0~2장 코스트로 (엔진 Phase = Draw)
            Ready,         // 스킬 배치 (엔진 Phase = Ready)
            Resolved,      // 액션+엔드 끝남 (엔진 Phase = End)
            GameOver
        }

        private UiStep _step;

        // ===================== 초기화 =====================

        private void Awake()
        {
            for (int i = 0; i < GameRules.PartySize; i++)
            {
                var my = transform.Find($"MyCharacter{i}");
                var op = transform.Find($"OpponentCharacter{i}");
                _myCards[i] = my?.GetComponent<CharacterCardView>();
                _opponentCards[i] = op?.GetComponent<CharacterCardView>();
                _myCardButtons[i] = my?.GetComponent<Button>();
                _opponentCardButtons[i] = op?.GetComponent<Button>();

                _myActSlots[i] = transform.Find($"MyAct{i}")?.GetComponent<ActSlotView>();
                _opponentActSlots[i] = transform.Find($"OpponentAct{i}")?.GetComponent<ActSlotView>();
            }

            _handLayout = transform.Find("HandContainer")?.GetComponent<HandFanLayout>();
            if (_handLayout != null)
            {
                _handCards = _handLayout.GetComponentsInChildren<SkillCardView>(true);
                _handRects = _handCards.Select(c => (RectTransform)c.transform).ToArray();
            }

            _phaseText = transform.Find("PhasePanel/PhaseText")?.GetComponent<Text>();
            _turnText = transform.Find("PhasePanel/TurnText")?.GetComponent<Text>();
            _resultText = transform.Find("PhasePanel/ResultText")?.GetComponent<Text>();
            _logText = transform.Find("PhasePanel/LogText")?.GetComponent<Text>();
            _myProfileText = transform.Find("MyProfile/Label")?.GetComponent<Text>();
            _opponentProfileText = transform.Find("OpponentProfile/Label")?.GetComponent<Text>();
            _nextButton = transform.Find("PhasePanel/NextButton")?.GetComponent<Button>();
            _nextButtonLabel = transform.Find("PhasePanel/NextButton/Label")?.GetComponent<Text>();

            _stepBar = transform.Find("PhasePanel/PhaseStepBar")?.GetComponent<PhaseStepBar>();
            _costPips = transform.Find("PhasePanel/CostPips")?.GetComponent<ResourcePips>();
            _swiftPips = transform.Find("PhasePanel/SwiftPips")?.GetComponent<ResourcePips>();
            if (_swiftPips != null) _swiftPips.SetKind(swift: true);
        }

        // [흐름] 엔진 인계 → 훅 구독 → 타겟 브릿지 장착 → 신속 자원 계산
        //        → 초기 손패 6장 뽑기(RunMulligan) → 초기 코스트 단계 진입
        private void Start()
        {
            _game = BattleSession.Acquire();

            _game.OnPhaseChanged += OnPhaseChanged;
            _game.OnGameOver += OnGameOver;
            _game.OnSlotActivating += OnSlotActivating;
            _game.OnChallengeResolved += OnChallengeResolved;
            _game.OnCharacterDied += OnCharacterDied;

            InstallTargetBridge();

            // 담당 모듈: D — 큐 재생기 장착. 뷰 배열은 Awake에서 이미 찾아뒀으니 그대로 공유한다.
            _actionPlayer = gameObject.AddComponent<BattleActionPlayer>();
            _actionPlayer.Initialize(_myCards, _opponentCards, _myActSlots, _opponentActSlots,
                _game, BattleSession.MyPlayerId, BattleSession.EnemyPlayerId);

            _swift.InitializeFrom(_game.Players[BattleSession.MyPlayerId].CharacterZone);
            if (_swift.Max > 0) AddLog($"신속 자원 {_swift.Max} 보유");

            WireInput();

            // 초기 코스트: 손패 6장을 뽑고 그중 2장을 골라 코스트로 버린다 (프로토타입 startCost).
            _game.RunMulligan();
            EnemyPlaceInitialCost();
            EnterInitialCost();
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            _game.OnPhaseChanged -= OnPhaseChanged;
            _game.OnGameOver -= OnGameOver;
            _game.OnSlotActivating -= OnSlotActivating;
            _game.OnChallengeResolved -= OnChallengeResolved;
            _game.OnCharacterDied -= OnCharacterDied;
        }

        /// <summary>
        /// [무엇] 엔진의 TargetSelector를 룩업 브릿지로 교체한다.
        /// [왜] BattleSession이 AIController.Attach로 양쪽 다 AI 타겟팅을 걸어놨다. 내 카드만
        ///      유저 지정값을 쓰고 상대는 그대로 AI에 맡기려면 한 겹 감싸야 한다.
        /// [주의] 기존 선택기를 버리지 말고 폴백으로 보관한다 — 상대 타겟팅이 죽으면 안 된다.
        /// </summary>
        private void InstallTargetBridge()
        {
            _aiTargetSelector = _game.TargetSelector;
            _game.TargetSelector = (user, opponent, skill) =>
            {
                bool mine = user != null && user.OwnerId == BattleSession.MyPlayerId;
                return mine
                    ? _targets.Resolve(user, opponent, skill, _aiTargetSelector)
                    : _aiTargetSelector?.Invoke(user, opponent, skill);
            };
        }

        private void WireInput()
        {
            if (_nextButton != null) _nextButton.onClick.AddListener(OnNextButton);

            if (_handCards != null)
                foreach (var card in _handCards)
                    card.Clicked += OnHandCardClicked;

            for (int i = 0; i < GameRules.PartySize; i++)
            {
                int index = i;
                if (_myCardButtons[i] != null) _myCardButtons[i].onClick.AddListener(() => OnMyCharacterClicked(index));
                if (_opponentCardButtons[i] != null) _opponentCardButtons[i].onClick.AddListener(() => OnEnemyCharacterClicked(index));

                // 모듈 D: 사거리 하이라이트 — 내 캐릭터에 마우스를 올리면 리치 내 적을 밝힌다.
                if (_myCards[i] != null)
                {
                    _myCards[i].PointerEntered += OnCharacterHoverEnter;
                    _myCards[i].PointerExited += OnCharacterHoverExit;
                }
                if (_myActSlots[i] != null)
                {
                    _myActSlots[i].CancelClicked += OnCancelSlot;
                    _myActSlots[i].SwiftToggled += OnToggleSwift;
                }
            }
        }

        // ===================== 단계 전환 =====================

        /// <summary>[무엇] 초기 코스트 단계 진입 — 손패에서 2장을 골라 버린다.</summary>
        private void EnterInitialCost()
        {
            _step = UiStep.InitialCost;
            _initialCostSelection.Clear();
            AddLog($"초기 코스트 — 손패에서 {InitialCostCount}장을 골라 코스트로 버리세요");
            RefreshAll();
        }

        /// <summary>
        /// [무엇] 드로우 페이즈 — 2장 뽑고, 그 2장 중에서만 코스트를 고르게 한다.
        /// [주의] "방금 뽑은 카드만" 제한은 엔진(PlaceCostFromDraw)이 강제한다. UI는 흐리게 그릴 뿐이다.
        /// </summary>
        private void EnterDrawCost()
        {
            _game.BeginTurn();              // 엔진: 언탭 + 2장 드로우 (첫 턴도 여기서 시작)
            _swift.ResetForTurn();          // 신속 자원도 매 턴 회복 (RULES.md 7번)
            _drawCostSelection.Clear();
            _step = UiStep.DrawCost;

            EnemyPlaceDrawCost();           // 상대는 AI가 즉시 코스트를 정한다

            AddLog($"턴 {_game.TurnNumber} 드로우 — 방금 뽑은 카드 중 최대 {GameRules.DrawPhaseMaxToCost}장 코스트로 (0장도 가능)");
            RefreshAll();
        }

        /// <summary>[무엇] 레디 페이즈 진입 — 내 배치를 기다리고, 상대는 AI가 즉시 낸다.</summary>
        private void EnterReady()
        {
            _game.BeginReadyPhase();
            _targets.Clear();
            _selectedHandIndex = -1;
            _pending = default;
            _step = UiStep.Ready;

            EnemyReady();

            AddLog("레디 — 손패 카드를 고른 뒤 캐릭터를 클릭하세요");
            RefreshAll();
        }

        /// <summary>
        /// [무엇] 액션+엔드 실행.
        /// [왜] 엔진(RunActionPhase)이 액션+엔드를 한 호출로 끝내버리므로, 예전엔 여기서 바로
        ///      RefreshAll()을 불러 "결과부터 튀는" 화면이 됐다(모듈 D 인계 사항). 이제는 엔진 호출이
        ///      끝나자마자 곧바로 그리지 않고, 그동안 <see cref="_actionPlayer"/> 큐에 쌓인 이벤트를
        ///      코루틴으로 재생한 뒤(<see cref="OnActionPlaybackComplete"/>) 최종 상태를 그린다.
        /// [주의] 액트존(스킬 카드)은 예외적으로 여기서 바로 공개한다 — 프로토타입도 액션 시작 시점에
        ///        layoutBattle()로 전부 뒤집어 보여주고 나서 슬롯을 하나씩 발광시킨다(원문 runAction).
        ///        상대가 뭘 냈는지 자체는 "결과"가 아니라 "이미 정해진 패"라 미리 보여줘도 스포일러가
        ///        아니다 — 스포일러가 되는 건 데미지/사망(캐릭터 카드) 쪽이고, 그건 큐 재생이 맡는다.
        /// </summary>
        private void RunAction()
        {
            _actionPlaying = true;

            _actionPlayer.BeginActionPhase();
            _game.RunActionPhase();   // 내부에서 엔드 페이즈까지
            _actionPlayer.EndActionPhase();

            _step = _game.Winner.HasValue ? UiStep.GameOver : UiStep.Resolved;
            RefreshActSlots();
            RefreshPhasePanel();
            if (_nextButton != null) _nextButton.interactable = false; // 재생 끝날 때까지 다시 잠근다

            _actionPlayer.Play(OnActionPlaybackComplete);
        }

        /// <summary>[무엇] 큐 재생이 끝난 뒤 호출된다. [왜] 여기서야 비로소 입력을 풀고 최종 상태로 동기화한다.</summary>
        private void OnActionPlaybackComplete()
        {
            _actionPlaying = false;
            RefreshAll();
        }

        /// <summary>
        /// [무엇] "다음" 버튼 — 지금 단계에 맞는 다음 동작을 한다.
        /// [흐름] 초기코스트 확정 → 드로우 → 코스트확정 → 레디 → 액션+엔드 → 다음 턴 드로우 → …
        /// </summary>
        private void OnNextButton()
        {
            if (_actionPlaying) return; // 모듈 D: 연출 재생 중엔 다음 단계로 못 넘어간다

            switch (_step)
            {
                case UiStep.InitialCost:
                    ConfirmInitialCost();
                    break;
                case UiStep.DrawCost:
                    ConfirmDrawCost();
                    break;
                case UiStep.Ready:
                    RunAction();
                    break;
                case UiStep.Resolved:
                    EnterDrawCost();
                    break;
                case UiStep.GameOver:
                    return;
            }
        }

        /// <summary>[무엇] 초기 코스트 확정 → 첫 턴 드로우로. [주의] 정확히 2장을 골라야 넘어간다.</summary>
        private void ConfirmInitialCost()
        {
            if (_initialCostSelection.Count != InitialCostCount) return;

            var hand = _game.Players[BattleSession.MyPlayerId].Hand;
            var chosen = _initialCostSelection.Select(i => hand[i]).ToList();
            _game.PlaceCostFromHand(BattleSession.MyPlayerId, chosen, InitialCostCount);

            AddLog($"초기 코스트 {chosen.Count}장 배치 — 듀얼 개시");
            EnterDrawCost();
        }

        /// <summary>[무엇] 드로우 코스트 확정(0~2장) → 레디로.</summary>
        private void ConfirmDrawCost()
        {
            if (_drawCostSelection.Count > 0)
            {
                var hand = _game.Players[BattleSession.MyPlayerId].Hand;
                var chosen = _drawCostSelection.Select(i => hand[i]).ToList();
                int placed = _game.PlaceCostFromDraw(BattleSession.MyPlayerId, chosen);
                AddLog($"코스트 {placed}장 추가");
            }
            EnterReady();
        }

        // ===================== 상대(AI) =====================

        /// <summary>[무엇] 상대의 초기 코스트 배치. [왜] 양쪽이 같은 준비 단계를 거쳐야 코스트 총량이 맞는다.</summary>
        private void EnemyPlaceInitialCost()
        {
            const int enemy = BattleSession.EnemyPlayerId;
            var cost = BattleSession.Ai[enemy].ChooseMulliganCost(_game.Players[enemy]);
            _game.PlaceCostFromHand(enemy, cost, InitialCostCount);
        }

        /// <summary>[무엇] 상대의 드로우 코스트 + 카드 배치. [주의] 내 카드는 유저가 내므로 상대만 처리한다.</summary>
        private void EnemyReady()
        {
            const int enemy = BattleSession.EnemyPlayerId;
            foreach (var play in BattleSession.Ai[enemy].ChoosePlays(_game.Players[enemy], _game.Players[BattleSession.MyPlayerId]))
                _game.SubmitActiveCard(enemy, play.Skill, play.User, play.AsAccel, false, play.SkillOption);
        }

        private void EnemyPlaceDrawCost()
        {
            const int enemy = BattleSession.EnemyPlayerId;
            var cost = BattleSession.Ai[enemy].ChooseDrawCost(_game.Players[enemy], _game.LastDrawn[enemy]);
            _game.PlaceCostFromDraw(enemy, cost);
        }

        // ===================== 레디: 손패 → 캐릭터 → 타겟 =====================

        /// <summary>
        /// [무엇] 손패 카드 클릭.
        /// [흐름] 코스트 단계면 코스트 선택 토글 / 레디면 스킬 선택(사용 가능 캐릭터 하이라이트).
        /// [주의] 불가 사유는 반드시 로그로 알려준다 — 프로토타입에서 "왜 안 되는지 모르겠다"는
        ///        피드백이 나왔던 부분이다(HANDOFF 미해결 2번).
        /// </summary>
        private void OnHandCardClicked(int handIndex)
        {
            if (_actionPlaying) return; // 모듈 D: 연출 재생 중 입력 잠금

            var hand = _game.Players[BattleSession.MyPlayerId].Hand;
            if (handIndex < 0 || handIndex >= hand.Count) return;

            switch (_step)
            {
                case UiStep.InitialCost:
                    ToggleSelection(_initialCostSelection, handIndex, InitialCostCount);
                    RefreshAll();
                    return;

                case UiStep.DrawCost:
                    if (!IsNewlyDrawn(hand[handIndex]))
                    {
                        AddLog("방금 뽑은 카드만 코스트로 낼 수 있습니다");
                        return;
                    }
                    ToggleSelection(_drawCostSelection, handIndex, GameRules.DrawPhaseMaxToCost);
                    RefreshAll();
                    return;

                case UiStep.Ready:
                    SelectSkill(handIndex, hand[handIndex]);
                    return;
            }
        }

        /// <summary>[무엇] 레디에서 스킬 선택. [주의] 코스트 부족·사용 캐릭터 없음이면 사유를 남기고 선택하지 않는다.</summary>
        private void SelectSkill(int handIndex, SkillData skill)
        {
            var me = _game.Players[BattleSession.MyPlayerId];

            int reserved = me.ActiveZone.Sum(s => s.Cost);      // 이번 레디에 이미 예약한 코스트(엔진 슬롯에서 읽음)
            int cost = Mathf.Max(0, skill.Skill1Cost);
            if (cost > me.AvailableCost - reserved)
            {
                AddLog($"「{skill.Name}」 코스트 부족 (필요 {cost})");
                return;
            }

            var users = UsableCharacters(skill).ToList();
            if (users.Count == 0)
            {
                AddLog($"「{skill.Name}」({skill.WeaponType}) — 쓸 수 있는 캐릭터가 없음");
                return;
            }

            _selectedHandIndex = _selectedHandIndex == handIndex ? -1 : handIndex;
            _pending = default;

            if (_selectedHandIndex >= 0)
                AddLog($"「{skill.Name}」 선택 — 사용할 캐릭터를 클릭하세요");

            RefreshAll();
        }

        /// <summary>
        /// [무엇] 이 스킬을 쓸 수 있는 내 캐릭터들 (생존 + 무기 적합 + 아직 배치 안 함).
        /// [왜] SubmitActiveCard는 무기 적합성을 검사하지 않는다 — AI가 CanUse로 스스로 지키던 규칙이라
        ///      UI도 같은 규칙을 지켜야 못 쓰는 조합이 들어가지 않는다.
        /// </summary>
        private IEnumerable<CharacterUnit> UsableCharacters(SkillData skill)
        {
            var me = _game.Players[BattleSession.MyPlayerId];
            return me.CharacterZone.Where(u =>
                !u.IsDead
                && AIController.CanUse(skill, u)
                && me.ActiveZone.All(s => s.User != u));   // 한 캐릭터당 한 장 (프로토타입 myActs[idx])
        }

        /// <summary>
        /// [무엇] 내 캐릭터 클릭 — 선택한 스킬을 이 캐릭터에게 배치한다.
        /// [흐름] 회복 스킬 → 타겟 지정 없이 즉시 배치(엔진이 최저 HP 아군 선택)
        ///        공격 스킬 → 리치 내 적 0명: 그대로 배치(불발 예고) / 1명: 자동 지정 / 2명 이상: 타겟 클릭 대기
        /// </summary>
        private void OnMyCharacterClicked(int cardIndex)
        {
            if (_actionPlaying || _step != UiStep.Ready || _selectedHandIndex < 0) return;

            var me = _game.Players[BattleSession.MyPlayerId];
            if (cardIndex >= me.CharacterZone.Count) return;

            var user = me.CharacterZone[cardIndex];
            var skill = me.Hand[_selectedHandIndex];

            if (!UsableCharacters(skill).Contains(user))
            {
                AddLog($"{user.Data.Name}: 「{skill.Name}」 사용 불가");
                return;
            }

            // 회복 스킬은 대상이 아군이라 엔진이 최저 HP를 알아서 고른다 (EffectSystem 참고).
            if (!IsAttackSkill(skill))
            {
                Commit(skill, user, target: null);
                return;
            }

            var opponent = _game.Players[BattleSession.EnemyPlayerId];
            var inReach = opponent.CharacterZone.Where(t => !t.IsDead && user.CanReach(t.Position)).ToList();

            if (inReach.Count == 0)
            {
                AddLog($"{user.Data.Name}: 리치 내 대상 없음 (배치는 되지만 불발 예정)");
                Commit(skill, user, target: null);
            }
            else if (inReach.Count == 1)
            {
                Commit(skill, user, inReach[0]);
            }
            else
            {
                _pending = new PendingPlacement { Skill = skill, User = user };
                AddLog($"{user.Data.Name} 「{skill.Name}」 — 대상을 클릭하세요 ({inReach.Count}명)");
                RefreshAll();
            }
        }

        /// <summary>[무엇] 적 캐릭터 클릭 — 타겟 지정 대기 중일 때만 유효하다.</summary>
        private void OnEnemyCharacterClicked(int cardIndex)
        {
            if (_actionPlaying || _step != UiStep.Ready || _pending.User == null) return;

            var opponent = _game.Players[BattleSession.EnemyPlayerId];
            if (cardIndex >= opponent.CharacterZone.Count) return;

            var target = opponent.CharacterZone[cardIndex];
            if (target.IsDead || !_pending.User.CanReach(target.Position))
            {
                AddLog("리치 밖입니다");
                return;
            }

            Commit(_pending.Skill, _pending.User, target);
        }

        /// <summary>
        /// [무엇] 배치 확정 — 엔진에 제출하고 타겟을 룩업 표에 기억시킨다.
        /// [주의] 코스트는 여기서 빠지지 않는다. 엔진은 발동 시점에 지불한다.
        /// </summary>
        private void Commit(SkillData skill, CharacterUnit user, CharacterUnit target)
        {
            if (!_game.SubmitActiveCard(BattleSession.MyPlayerId, skill, user, asAccel: false, asSwift: false, skillOption: 0))
                return;

            _targets.Remember(user, skill, target);

            string targetName = target != null ? $" → {target.Data.Name}" : "";
            AddLog($"{user.Data.Name} ← 「{skill.Name}」{targetName}");

            _selectedHandIndex = -1;
            _pending = default;
            RefreshAll();
        }

        /// <summary>
        /// [무엇] 배치 취소 — 카드를 손패로 되돌리고 신속·타겟 지정을 환급한다.
        /// [주의] 코스트 환급은 없다 — 배치 시점에 지불한 적이 없기 때문(엔진은 발동 시 지불).
        /// </summary>
        private void OnCancelSlot(ActSlotView view)
        {
            if (_actionPlaying || _step != UiStep.Ready) return;

            var slot = view.Slot;
            if (slot == null) return;

            if (slot.IsSwift) _swift.Refund();
            _targets.Forget(slot.User, slot.Skill);

            if (_game.CancelActiveCard(BattleSession.MyPlayerId, slot))
            {
                AddLog($"「{slot.Skill.Name}」 취소 — 손패 복귀");
                RefreshAll();
            }
        }

        /// <summary>
        /// [무엇] 신속 토글 — 이 슬롯을 신속 그룹으로 지정하거나 해제한다.
        /// [주의] ActiveSlot.IsSwift는 읽기 전용이라, 토글은 <b>취소 후 재제출</b>로 구현한다.
        ///        엔진에 신속 변경 API를 새로 만들지 않기 위해서다(승인 범위 밖).
        /// </summary>
        private void OnToggleSwift(ActSlotView view)
        {
            if (_actionPlaying || _step != UiStep.Ready) return;

            var slot = view.Slot;
            if (slot == null) return;

            bool turningOn = !slot.IsSwift;
            if (turningOn && _swift.Remaining <= 0)
            {
                AddLog("신속 자원 부족");
                return;
            }

            // 같은 카드/캐릭터로 다시 제출하되 신속 플래그만 바꾼다.
            var skill = slot.Skill;
            var user = slot.User;
            int option = slot.SkillOption;
            if (!_game.CancelActiveCard(BattleSession.MyPlayerId, slot)) return;

            if (!_game.SubmitActiveCard(BattleSession.MyPlayerId, skill, user, asAccel: false, asSwift: turningOn, skillOption: option))
                return;

            if (turningOn) _swift.TrySpend();
            else _swift.Refund();

            // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: 신속 지정 문구 끝에 " ⚡")
            AddLog(turningOn ? $"「{skill.Name}」 신속 지정" : $"「{skill.Name}」 신속 해제");
            RefreshAll();
        }

        // ===================== 사거리 하이라이트 (모듈 D 3단계) =====================

        /// <summary>
        /// [무엇] 내 캐릭터에 마우스를 올리면, 그 캐릭터의 리치 안에 있는 적을 밝히고 "거리 N"을 띄운다.
        ///        리치 밖 적은 어둡게 죽인다.
        /// [왜] 리치 판정(거리 = 내 위치 + 상대 위치 + 1, RULES.md R4)이 눈으로 세기 어렵다.
        ///      스킬을 내기 전에 누구를 때릴 수 있는지 보여줘야 배치를 결정할 수 있다.
        /// [주의] <b>거리·도달 여부를 여기서 계산하지 않는다.</b> 전부 엔진 API
        ///        (<see cref="CharacterUnit.CalculateDistance"/>, <see cref="CharacterUnit.CanReach(int)"/>)를
        ///        호출해 얻는다 — UI가 같은 공식을 따로 구현하면 엔진과 어긋난다(desync).
        /// [주의] 타겟 지정 대기 중(_pending)에는 건드리지 않는다. 그 상태에서는 RefreshHighlights가
        ///        "지정 가능한 적"을 이미 밝히고 있어, 호버가 그 위에 덮어쓰면 헷갈린다.
        /// </summary>
        private void OnCharacterHoverEnter(CharacterCardView card)
        {
            if (_actionPlaying || _pending.User != null) return;

            var user = card.Unit;
            if (user == null || user.IsDead) return;

            var opponent = _game.Players[BattleSession.EnemyPlayerId];
            for (int i = 0; i < _opponentCards.Length; i++)
            {
                if (_opponentCards[i] == null) continue;
                if (i >= opponent.CharacterZone.Count) continue;

                var target = opponent.CharacterZone[i];
                if (target.IsDead) continue;

                // 거리·도달 판정 전부 엔진 값
                int distance = CharacterUnit.CalculateDistance(user.Position, target.Position);
                bool inReach = user.CanReach(target.Position);

                _opponentCards[i].SetDistanceChip(inReach ? distance : (int?)null);
                _opponentCards[i].SetDimmed(!inReach);
            }
        }

        /// <summary>[무엇] 호버가 끝나면 사거리 표시를 지운다.</summary>
        private void OnCharacterHoverExit(CharacterCardView card) => ClearReachHighlight();

        /// <summary>[무엇] 모든 적 카드의 거리 칩·어둡게 처리를 초기화한다.</summary>
        private void ClearReachHighlight()
        {
            foreach (var card in _opponentCards)
            {
                if (card == null) continue;
                card.SetDistanceChip(null);
                card.SetDimmed(false);
            }
        }

        // ===================== 판정 헬퍼 =====================

        /// <summary>
        /// [무엇] 공격 스킬인지 (타겟 지정이 필요한지).
        /// [주의] 엔진에 "공격/회복" 분류가 없어 효과 원문으로 판단한다. 규칙 재계산이 아니라
        ///        <b>타겟 UI를 띄울지 말지</b>를 정하는 화면 판단이며, 실제 효과는 엔진이 처리한다.
        ///        오판해도 배치는 되고 엔진이 알아서 처리한다(회복 스킬에 타겟을 넘겨도 무시됨).
        /// </summary>
        private static bool IsAttackSkill(SkillData skill) =>
            !string.IsNullOrEmpty(skill.Skill1Effect) && skill.Skill1Effect.Contains("데미지");

        /// <summary>[무엇] 이번 턴에 뽑은 카드인지. [주의] 판정 근거는 엔진의 LastDrawn이다.</summary>
        private bool IsNewlyDrawn(SkillData card)
        {
            var drawn = _game.LastDrawn?[BattleSession.MyPlayerId];
            return drawn != null && drawn.Contains(card);
        }

        private static void ToggleSelection(List<int> selection, int index, int max)
        {
            if (selection.Contains(index)) { selection.Remove(index); return; }
            if (selection.Count >= max) selection.RemoveAt(0);
            selection.Add(index);
        }

        // ===================== 갱신 =====================

        private void RefreshAll()
        {
            BindParties();
            RefreshHand();
            RefreshActSlots();
            RefreshHighlights();
            RefreshPhasePanel();
            RefreshProfiles();
            RefreshResourcePips();   // 모듈 D: 코스트/신속 pip
            ClearReachHighlight();   // 상태가 바뀌면 사거리 표시는 일단 지운다(호버하면 다시 뜬다)
        }

        /// <summary>
        /// [무엇] 코스트·신속 자원을 pip으로 반영한다 (모듈 D 3단계).
        /// [주의] 총량·레스트 수를 여기서 계산하지 않는다 — 엔진의 CostZone(IsRested)과
        ///        SwiftResource가 이미 들고 있는 값을 그대로 넘긴다.
        /// </summary>
        private void RefreshResourcePips()
        {
            var me = _game.Players[BattleSession.MyPlayerId];
            if (_costPips != null)
                _costPips.SetValues(me.CostZone.Count, me.CostZone.Count(c => c.IsRested));
            if (_swiftPips != null)
                _swiftPips.SetValues(_swift.Max, _swift.Max - _swift.Remaining);
        }

        /// <summary>[무엇] 캐릭터 존을 카드 칸에 연결. [주의] R9 앞당김으로 순서가 바뀌므로 매번 다시 연결한다.</summary>
        private void BindParties()
        {
            BindParty(_myCards, _game.Players[BattleSession.MyPlayerId]);
            BindParty(_opponentCards, _game.Players[BattleSession.EnemyPlayerId]);
        }

        private static void BindParty(CharacterCardView[] cards, PlayerState player)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                if (i < player.CharacterZone.Count) cards[i].Bind(player.CharacterZone[i]);
                else cards[i].SetEmpty();
            }
        }

        /// <summary>[무엇] 손패를 통째로 다시 그린다. [왜] Hand가 진실이라 증분 갱신하지 않는다.</summary>
        private void RefreshHand()
        {
            if (_handCards == null) return;

            var me = _game.Players[BattleSession.MyPlayerId];
            var hand = me.Hand;
            int shown = Mathf.Min(hand.Count, _handCards.Length);
            int reserved = me.ActiveZone.Sum(s => s.Cost);

            for (int i = 0; i < shown; i++)
            {
                bool playable;
                bool highlighted = false;

                switch (_step)
                {
                    case UiStep.InitialCost:
                        playable = true;
                        highlighted = _initialCostSelection.Contains(i);
                        break;
                    case UiStep.DrawCost:
                        playable = IsNewlyDrawn(hand[i]);              // 방금 뽑은 것만
                        highlighted = _drawCostSelection.Contains(i);
                        break;
                    case UiStep.Ready:
                        playable = Mathf.Max(0, hand[i].Skill1Cost) <= me.AvailableCost - reserved
                                   && UsableCharacters(hand[i]).Any();
                        highlighted = i == _selectedHandIndex;
                        break;
                    default:
                        playable = false;
                        break;
                }

                _handCards[i].Bind(hand[i], i, playable);
                _handCards[i].SetHighlighted(highlighted);
            }

            _handLayout.Layout(_handRects, shown);
        }

        /// <summary>
        /// [무엇] 액트존 표시. 상대 카드는 레디 동안 뒷면(RULES.md 5-2), 액션 뒤 공개(5-3).
        /// [주의] 내 슬롯은 레디 동안에만 취소·신속 버튼을 보여준다.
        /// </summary>
        private void RefreshActSlots()
        {
            var me = _game.Players[BattleSession.MyPlayerId];
            var op = _game.Players[BattleSession.EnemyPlayerId];

            BindActZone(_myActSlots, me, faceDown: false, interactive: _step == UiStep.Ready, swiftAvailable: _swift.Remaining > 0);
            BindActZone(_opponentActSlots, op, faceDown: _step == UiStep.Ready, interactive: false, swiftAvailable: false);
        }

        private static void BindActZone(ActSlotView[] slots, PlayerState player, bool faceDown, bool interactive, bool swiftAvailable)
        {
            var zone = player.ActiveZone;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;

                if (i >= zone.Count) slots[i].SetEmpty();
                else if (faceDown) slots[i].SetFaceDown(zone[i]);
                else slots[i].SetFilled(zone[i], interactive, swiftAvailable);
            }
        }

        /// <summary>
        /// [무엇] 레디 입력 상태에 따른 하이라이트 — 사용 가능 캐릭터 / 타겟 후보.
        /// [주의] 색·발광은 모듈 D 영역이라 여기서는 뷰의 하이라이트 스위치만 켠다.
        /// </summary>
        private void RefreshHighlights()
        {
            var me = _game.Players[BattleSession.MyPlayerId];
            var op = _game.Players[BattleSession.EnemyPlayerId];

            IEnumerable<CharacterUnit> usable = _step == UiStep.Ready && _selectedHandIndex >= 0 && _pending.User == null
                ? UsableCharacters(me.Hand[_selectedHandIndex])
                : System.Linq.Enumerable.Empty<CharacterUnit>();
            var usableSet = new HashSet<CharacterUnit>(usable);

            for (int i = 0; i < _myCards.Length; i++)
            {
                if (_myCards[i] == null) continue;
                bool on = i < me.CharacterZone.Count && usableSet.Contains(me.CharacterZone[i]);
                _myCards[i].SetHighlighted(on);
            }

            for (int i = 0; i < _opponentCards.Length; i++)
            {
                if (_opponentCards[i] == null) continue;
                bool on = _pending.User != null
                          && i < op.CharacterZone.Count
                          && !op.CharacterZone[i].IsDead
                          && _pending.User.CanReach(op.CharacterZone[i].Position);
                _opponentCards[i].SetHighlighted(on);
            }
        }

        private void RefreshPhasePanel()
        {
            // 모듈 D: 진행바 — UiStep(화면 단계)을 프로토타입의 4단계로 접어서 넘긴다.
            // [주의] 판정이 아니라 표시용 매핑이다. 초기 코스트/드로우 코스트는 프로토타입에서
            //   모두 '드로우' 단계에 해당한다(코스트 배치가 드로우 페이즈의 일부라서).
            if (_stepBar != null) _stepBar.SetStep(_step switch
            {
                UiStep.InitialCost => PhaseStepBar.Step.Draw,
                UiStep.DrawCost => PhaseStepBar.Step.Draw,
                UiStep.Ready => PhaseStepBar.Step.Ready,
                UiStep.Resolved => PhaseStepBar.Step.End,
                _ => PhaseStepBar.Step.End
            });

            if (_phaseText != null) _phaseText.text = StepLabel();
            if (_turnText != null) _turnText.text = _game.TurnNumber > 0 ? $"턴 {_game.TurnNumber}" : "";
            if (_nextButtonLabel != null) _nextButtonLabel.text = NextButtonLabel();
            if (_nextButton != null) _nextButton.interactable = CanAdvance();
            if (_logText != null) _logText.text = string.Join("\n", _logLines);
        }

        private string StepLabel() => _step switch
        {
            UiStep.InitialCost => "초기 코스트",
            UiStep.DrawCost => "드로우",
            UiStep.Ready => "레디",
            UiStep.Resolved => "엔드",
            _ => "종료"
        };

        private string NextButtonLabel() => _step switch
        {
            UiStep.InitialCost => $"듀얼 개시 ({_initialCostSelection.Count}/{InitialCostCount}) ▶",
            UiStep.DrawCost => $"코스트 확정 ({_drawCostSelection.Count}/{GameRules.DrawPhaseMaxToCost}) ▶",
            UiStep.Ready => "액션 개시 ▶",
            UiStep.Resolved => "다음 턴 ▶",
            _ => "종료"
        };

        /// <summary>[주의] 초기 코스트만 2장 강제다. 드로우 코스트는 0장도 허용(프로토타입 updateDrawCostBtn).</summary>
        private bool CanAdvance() => _step switch
        {
            UiStep.InitialCost => _initialCostSelection.Count == InitialCostCount,
            UiStep.GameOver => false,
            _ => true
        };

        /// <summary>[무엇] 코스트 pip 표시용 텍스트 — 총량과 꺾인(레스트) 수를 엔진에서 읽는다.</summary>
        private void RefreshProfiles()
        {
            SetProfile(_myProfileText, "나", _game.Players[BattleSession.MyPlayerId], _swift);
            SetProfile(_opponentProfileText, "상대", _game.Players[BattleSession.EnemyPlayerId], null);
        }

        private static void SetProfile(Text label, string who, PlayerState player, SwiftResource swift)
        {
            if (label == null) return;

            int pool = player.CostZone.Count;
            int rested = player.CostZone.Count(c => c.IsRested);
            string swiftPart = swift != null ? $"  신속 {swift.Remaining}/{swift.Max}" : "";

            label.text = $"{who}  덱{player.Deck.Count} 패{player.Hand.Count}\n" +
                         $"코스트 {pool - rested}/{pool}{swiftPart}  트래쉬{player.Trash.Count}";
        }

        private void AddLog(string message)
        {
            _logLines.Enqueue(message);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();
            Debug.Log($"[Battle] {message}");
        }

        // ===================== 엔진 이벤트 =====================

        private void OnPhaseChanged(GamePhase phase)
        {
            // 엔진 페이즈는 참고용. 화면 단계(UiStep)는 이 컨트롤러가 따로 관리한다.
        }

        private void OnGameOver(int winner)
        {
            _step = UiStep.GameOver;
            if (_resultText != null)
            {
                _resultText.text = winner == GameManager.NoWinner ? "무승부\n(양측 전멸)"
                    : winner == BattleSession.MyPlayerId ? "승리!" : "패배...";
            }
            AddLog(winner == GameManager.NoWinner ? "무승부" : winner == BattleSession.MyPlayerId ? "승리!" : "패배...");
        }

        // ── 연출 훅 (모듈 D 1단계에서 큐 적재로 연결) ────────────────────────
        // [왜] 세 훅 다 RunActionPhase 안에서 동기 호출된다. 여기서 바로 애니메이션을 돌리면
        //      엔진이 이미 상태를 다 바꿔놓은 뒤라 화면이 결과부터 튀어버린다(모듈 D 인계 사항).
        //      그래서 로그만 남기던 자리에 "큐에 적재"만 추가했다 — 실제 재생은 RunAction()이
        //      RunActionPhase()를 다 부른 뒤 BattleActionPlayer.Play()가 코루틴으로 한다.

        /// <summary>[무엇] 카드 발동 직전 훅.</summary>
        private void OnSlotActivating(ActiveSlot slot)
        {
            // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: swift = "⚡")
            string swift = slot.IsSwift ? "[신속] " : "";
            AddLog($"{swift}{slot.User?.Data.Name} 「{slot.Skill.Name}」 (속도 {slot.Skill.Speed})");
            _actionPlayer.EnqueueSlotActivating(slot);
        }

        /// <summary>[무엇] 액셀 판정 종료 훅. [주의] 재생은 모듈 D 2단계(액셀 오버레이)에서 붙인다.</summary>
        private void OnChallengeResolved(ChallengeRecord record)
        {
            string winner = record.Winner == GameManager.NoWinner ? "양측 무효"
                : record.Winner == BattleSession.MyPlayerId ? "내 승" : "상대 승";
            // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: 문구 앞에 "⚡ ")
            AddLog($"액셀 (속도 {record.Slot0.Skill.Speed} 동점) — {winner}");
            _actionPlayer.EnqueueChallengeResolved(record);
        }

        /// <summary>[무엇] 사망 훅.</summary>
        private void OnCharacterDied(CharacterUnit unit)
        {
            AddLog($"☠ {unit.Data.Name} 전투불능 → 코스트 존");
            _actionPlayer.EnqueueCharacterDied(unit);
        }
    }
}
