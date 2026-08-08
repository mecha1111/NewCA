// 담당 모듈: A (밴픽 공개화 + PlaceScene) — UNITY_PORTING_SPEC.md 4-1 / 5절
// 의존: BanPickState(진행 상태·상대 AI), BanPickCardView(카드 표시), BanPickFlow(씬 전환)

using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 밴 화면 — 상대 풀 10장 중 1장을 골라 밴을 확정한다.
    /// [왜] 밴픽이 "전체 공개"로 바뀌면서(UNITY_PORTING_SPEC 4-1) 밴 화면은 <b>상대 풀만</b> 보여주고,
    ///      그 위에 내가 밴한 카드와 상대가 이미 픽해 간 카드를 각각 표시한다.
    /// [주의] 실제 밴 처리와 상대 AI의 밴은 BanPickState가 한다. 여기는 표시와 입력만 담당한다
    ///        (계산을 UI와 엔진이 중복하면 어긋나기 때문 — UNITY_PORTING_SPEC 1절 3번).
    /// </summary>
    public class BanSceneController : MonoBehaviour
    {
        // "상대 픽" 라벨 — 출처: full_prototype.html (UI/UX 정본) --enemy 토큰 #E0512F.
        // [왜 금색이 아닌가] 금색(#F2C934)은 코스트 pip·사거리 하이라이트에 이미 쓰는 토큰이라
        //   "상대가 가져간 카드"에 같은 색을 쓰면 의미가 겹친다. 상대 관련은 빨강으로 통일한다.
        private static readonly Color EnemyPickLabelBackground = new Color32(0xE0, 0x51, 0x2F, 0xFF);
        private static readonly Color EnemyPickLabelText_Color = Color.white;
        private const string EnemyPickLabelText = "상대 픽";

        private Transform _enemyGrid;
        private Transform _myGrid;
        private Transform _myLabel;
        private Button _confirmButton;
        private Text _instructionText;
        private Text _progressText;

        private BanPickCardView[] _enemyCards;
        private int _selectedEnemyIndex = -1;

        private void Awake()
        {
            _enemyGrid = transform.Find("EnemyGrid");
            _myGrid = transform.Find("MyGrid");
            _myLabel = transform.Find("MyLabel");
            _confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();
            _instructionText = transform.Find("InstructionText")?.GetComponent<Text>();
            _progressText = transform.Find("ProgressBar")?.GetComponent<Text>();
        }

        // [흐름] 세션 보장 → 내 풀 숨김(밴 화면은 상대 풀만) → 상대 카드 10장 바인딩
        //        → 카드 상태 표시(내 밴=BAN회색 / 상대 픽=라벨+클릭불가) → 확정 버튼 비활성으로 시작
        private void Start()
        {
            BanPickState.EnsureStarted();

            // [왜] 밴 화면은 상대 풀만 보여준다(UNITY_PORTING_SPEC 4-1). 내 풀은 픽 화면에서 본다.
            //      빌더(모듈 B 담당)를 고치지 않으려고 여기서 끄는 방식을 쓴다.
            if (_myGrid != null) _myGrid.gameObject.SetActive(false);
            if (_myLabel != null) _myLabel.gameObject.SetActive(false);

            _enemyCards = _enemyGrid.GetComponentsInChildren<BanPickCardView>(true);
            for (int i = 0; i < _enemyCards.Length; i++)
            {
                bool bannedByMe = BanPickState.IsEnemyCardBanned(i);
                bool pickedByEnemy = BanPickState.IsEnemyCardPicked(i);

                _enemyCards[i].Bind(BanPickState.EnemyPool[i], i, clickable: !bannedByMe && !pickedByEnemy);
                _enemyCards[i].SetBanned(bannedByMe);

                // [왜] 상대가 이미 픽해 간 카드는 밴 대상이 아니다 (상호 배제).
                //      Bind에서 clickable=false로 막고, 이유를 라벨로 보여준다.
                if (pickedByEnemy)
                    _enemyCards[i].SetStatusLabel(EnemyPickLabelText, EnemyPickLabelBackground,
                        EnemyPickLabelText_Color, blocksClick: true);

                _enemyCards[i].Clicked += OnEnemyCardClicked;
            }

            if (_instructionText != null)
                _instructionText.text = "상대 카드 1장을 밴하세요";
            BanPickProgressText.Update(_progressText);

            if (_confirmButton != null)
            {
                _confirmButton.interactable = false;
                _confirmButton.onClick.AddListener(OnConfirm);
            }
        }

        /// <summary>[무엇] 밴 후보 선택(확정 전). [주의] 한 장만 고를 수 있어 이전 선택은 해제한다.</summary>
        private void OnEnemyCardClicked(int index)
        {
            if (_selectedEnemyIndex >= 0)
                _enemyCards[_selectedEnemyIndex].SetSelected(false);

            _selectedEnemyIndex = index;
            _enemyCards[index].SetSelected(true);

            if (_confirmButton != null)
                _confirmButton.interactable = true;
        }

        /// <summary>[무엇] 밴 확정 → 다음 씬. [주의] 상대 AI의 밴도 BanPickState가 같은 시점에 처리한다.</summary>
        private void OnConfirm()
        {
            if (_selectedEnemyIndex < 0) return;

            BanPickState.SubmitMyBan(_selectedEnemyIndex);
            BanPickFlow.AdvanceToNextScene();
        }
    }
}
