// 담당 모듈: A (밴픽 공개화 + PlaceScene) — UNITY_PORTING_SPEC.md 4-1 / 5절
// 의존: BanPickState(진행 상태·상대 AI), BanPickCardView(카드 표시), BanPickSlotView(픽 슬롯), BanPickFlow(씬 전환)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 픽 화면 — 내 풀에서 이번 스텝 매수(2장)만큼 골라 확정한다.
    /// [왜] 밴픽 전체 공개(UNITY_PORTING_SPEC 4-1)에서 내 풀 위에 "상대가 밴한 내 카드"를 즉시
    ///      보여줘야 하므로, 픽 화면은 내 풀만 띄우고 상대 밴 결과를 라벨로 표시한다.
    /// [주의] 픽 매수는 GameRules.PicksPerRound(=2)에서 오는 값이다. 화면에 숫자를 박지 않는다.
    /// </summary>
    public class PickSceneController : MonoBehaviour
    {
        // "상대 밴" 라벨 — 출처: full_prototype.html (UI/UX 정본) rgba(0,0,0,0.7).
        // [왜 어두운 반투명인가] 밴은 "이 카드는 죽었다"는 표시라 카드를 눌러 죽이는 어두운 막을 쓴다.
        //   빨강(#E0512F)은 "상대 픽"이 쓰므로 두 상태를 색으로 구분한다.
        private static readonly Color EnemyBanLabelBackground = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color EnemyBanLabelTextColor = Color.white;
        private const string EnemyBanLabelText = "상대 밴";

        private Transform _myGrid;
        private Transform _slotContainer;
        private Button _confirmButton;
        private Text _instructionText;
        private Text _progressText;

        private BanPickCardView[] _myCards;
        private BanPickSlotView[] _slots;
        private readonly List<int> _selected = new List<int>();
        private int _requiredCount;

        private void Awake()
        {
            _myGrid = transform.Find("MyGrid");
            _slotContainer = transform.Find("SlotContainer");
            _confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();
            _instructionText = transform.Find("InstructionText")?.GetComponent<Text>();
            _progressText = transform.Find("ProgressBar")?.GetComponent<Text>();
        }

        // [흐름] 세션 보장 → 이번 스텝 필요 매수 확인 → 내 카드 10장 바인딩
        //        → 상태 표시(상대 밴=회색+라벨+픽불가 / 내 픽=✓) → 슬롯 갱신 → 확정 버튼 비활성으로 시작
        private void Start()
        {
            BanPickState.EnsureStarted();

            _requiredCount = BanPickState.CurrentStep.Count;
            _myCards = _myGrid.GetComponentsInChildren<BanPickCardView>(true);
            _slots = _slotContainer.GetComponentsInChildren<BanPickSlotView>(true);

            for (int i = 0; i < _myCards.Length; i++)
            {
                bool bannedByEnemy = BanPickState.IsMyCardBanned(i);
                bool pickedByMe = BanPickState.IsMyCardPicked(i);

                _myCards[i].Bind(BanPickState.MyPool[i], i, clickable: !bannedByEnemy && !pickedByMe);
                _myCards[i].SetBanned(bannedByEnemy);
                _myCards[i].SetPicked(pickedByMe);

                // [왜] 상대가 밴한 내 카드는 픽할 수 없다. 회색막(SetBanned)만으로는 "누가 밴했는지"가
                //      안 보이므로, 전체 공개 규칙에 맞게 "상대 밴" 라벨을 함께 띄운다.
                if (bannedByEnemy)
                    _myCards[i].SetStatusLabel(EnemyBanLabelText, EnemyBanLabelBackground,
                        EnemyBanLabelTextColor, blocksClick: true);

                _myCards[i].Clicked += OnCardClicked;
            }

            RefreshSlots();
            UpdateInstruction();
            BanPickProgressText.Update(_progressText);

            if (_confirmButton != null)
            {
                _confirmButton.interactable = false;
                _confirmButton.onClick.AddListener(OnConfirm);
            }
        }

        /// <summary>[무엇] 픽 후보 토글. [주의] 이번 스텝 매수를 넘겨 고를 수 없다.</summary>
        private void OnCardClicked(int index)
        {
            if (_selected.Contains(index))
            {
                _selected.Remove(index);
                _myCards[index].SetSelected(false);
            }
            else
            {
                if (_selected.Count >= _requiredCount) return;
                _selected.Add(index);
                _myCards[index].SetSelected(true);
            }

            UpdateInstruction();
            if (_confirmButton != null)
                _confirmButton.interactable = _selected.Count == _requiredCount;
        }

        private void UpdateInstruction()
        {
            if (_instructionText != null)
                _instructionText.text = $"내 카드 {_requiredCount}장을 픽하세요 ({_selected.Count}/{_requiredCount})";
        }

        /// <summary>
        /// [무엇] 픽 확정 → 다음 화면(밴 or 배치).
        /// [왜] 여기서 GameManager 인계(CompleteAndSetup)를 <b>하지 않는다</b>. 엔진의 RunSetup은
        ///      picks[p][i]를 그대로 Position i로 만드는데, 이 시점엔 출전 순서가 아직 안 정해졌다.
        ///      픽한 순서로 넘기면 파티 배치가 픽 순서로 굳어버리므로, 인계는 PlaceScene 확정 후에 한다.
        /// [주의] 마지막 픽이 끝나면 BanPickFlow가 배틀이 아니라 PlaceScene으로 보낸다.
        /// </summary>
        private void OnConfirm()
        {
            if (_selected.Count != _requiredCount) return;

            BanPickState.SubmitMyPicks(_selected);
            BanPickFlow.AdvanceToNextScene();
        }

        /// <summary>[무엇] 하단 픽 슬롯 — 지금까지 확정된 내 파티를 픽한 순서대로 보여준다.</summary>
        private void RefreshSlots()
        {
            var picked = BanPickState.MyPickedCharacters;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < picked.Count) _slots[i].SetFilled(picked[i]);
                else _slots[i].SetEmpty();
            }
        }
    }
}
