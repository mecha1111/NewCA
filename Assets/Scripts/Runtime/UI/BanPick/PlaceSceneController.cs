// 담당 모듈: A (밴픽 공개화 + PlaceScene) — UNITY_PORTING_SPEC.md 4-2 / 5절
// 의존: BanPickState(픽 결과·PlacedOrder·GameManager 인계), PlaceSlotView(칸 표시), BanPickFlow(씬 전환)
//       PlaceSceneBuilder가 만든 오브젝트 이름(SlotRoot/CandidateRoot/ConfirmButton/RevealOverlay 등)

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Core;
using CrossAccel.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 배치 화면 — 픽한 4장을 P1~P4 자리에 놓고 확정한다.
    /// [왜] 엔진은 파티 배열 순서를 그대로 Position으로 쓰므로(RunSetup), 전투 전에 출전 순서를
    ///      정하는 단계가 필요하다. 이 화면이 정한 순서가 BanPickState.PlacedOrder가 된다.
    /// [주의] <b>화면 오른쪽이 P1 = 전방</b>이다 (전투 화면과 같은 방향).
    ///        P1이 Position 0(최전선, RULES.md R4)이 된다. 좌우를 뒤집으면 리치 계산이 전부 틀어진다.
    /// </summary>
    public class PlaceSceneController : MonoBehaviour
    {
        /// <summary>[무엇] 상대 파티 공개 연출에서 카드 한 장씩 나타나는 간격(초). [왜] 명세 "약 0.25초 간격".</summary>
        private const float RevealIntervalSeconds = 0.25f;

        /// <summary>[무엇] 카드 한 장이 서서히 나타나는 데 걸리는 시간(초). [주의] 명세에 없어 임의로 정함.</summary>
        private const float RevealFadeSeconds = 0.35f;

        /// <summary>[무엇] 연출이 끝나고 전투로 넘어가기 전 여운(초). [주의] 명세에 없어 임의로 정함.</summary>
        private const float RevealHoldSeconds = 0.6f;

        private PlaceSlotView[] _slots;        // 인덱스 0 = P1(전방) … 3 = P4(후방)
        private PlaceSlotView[] _candidates;   // 픽한 4장 후보
        private Button _confirmButton;
        private Text _instructionText;
        private GameObject _revealOverlay;
        private PlaceSlotView[] _revealCards;

        private IReadOnlyList<CharacterData> _picked;
        private int _selectedCandidate = -1;

        private void Awake()
        {
            _slots = FindViews("SlotRoot");
            _candidates = FindViews("CandidateRoot");
            _revealCards = FindViews("RevealOverlay/RevealRoot");

            _confirmButton = transform.Find("ConfirmButton")?.GetComponent<Button>();
            _instructionText = transform.Find("InstructionText")?.GetComponent<Text>();
            _revealOverlay = transform.Find("RevealOverlay")?.gameObject;
        }

        private PlaceSlotView[] FindViews(string path)
        {
            var root = transform.Find(path);
            return root == null ? new PlaceSlotView[0] : root.GetComponentsInChildren<PlaceSlotView>(true);
        }

        // [흐름] 픽 결과 4장 읽기 → 후보 칸에 배치 → 슬롯 전부 비우기 → 확정 버튼 잠금
        //        → (사용자) 후보 클릭 → 슬롯 클릭 → 배치
        //        → 채워진 슬롯 클릭 → 캔슬(후보로 복귀)
        //        → 4칸 다 차면 확정 활성 → 확정 → 상대 파티 페이드인 → 전투
        private void Start()
        {
            BanPickState.EnsureStarted();
            _picked = BanPickState.MyPickedCharacters;

            for (int i = 0; i < _candidates.Length; i++)
            {
                _candidates[i].SetIndex(i);
                if (i < _picked.Count) _candidates[i].SetCharacter(_picked[i]);
                else _candidates[i].SetHidden();
                _candidates[i].Clicked += OnCandidateClicked;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].SetIndex(i);
                _slots[i].SetEmpty();
                _slots[i].Clicked += OnSlotClicked;
            }

            if (_revealOverlay != null) _revealOverlay.SetActive(false);

            if (_confirmButton != null)
            {
                _confirmButton.interactable = false;
                _confirmButton.onClick.AddListener(OnConfirm);
            }

            UpdateInstruction();
        }

        /// <summary>[무엇] 후보 카드 선택. [주의] 이미 슬롯에 들어간 카드는 후보에서 숨겨져 클릭되지 않는다.</summary>
        private void OnCandidateClicked(int candidateIndex)
        {
            if (_selectedCandidate >= 0 && _selectedCandidate < _candidates.Length)
                _candidates[_selectedCandidate].SetSelected(false);

            _selectedCandidate = candidateIndex;
            _candidates[candidateIndex].SetSelected(true);
            UpdateInstruction();
        }

        /// <summary>
        /// [무엇] 슬롯 클릭 — 비어 있으면 선택한 후보를 배치하고, 차 있으면 캔슬한다.
        /// [왜] 명세 4-2: "채워진 슬롯 클릭 → 비워짐". 별도 X 버튼 없이 같은 클릭으로 처리한다.
        /// [주의] 캔슬하면 카드가 후보로 돌아가야 한다. 안 돌려보내면 그 카드를 영영 못 쓴다.
        /// </summary>
        private void OnSlotClicked(int slotIndex)
        {
            var slot = _slots[slotIndex];

            if (slot.Character != null)
            {
                ReturnToCandidates(slot.Character);
                slot.SetEmpty();
            }
            else
            {
                if (_selectedCandidate < 0) return;

                var card = _candidates[_selectedCandidate].Character;
                if (card == null) return;

                slot.SetCharacter(card);
                _candidates[_selectedCandidate].SetHidden();
                _candidates[_selectedCandidate].SetSelected(false);
                _selectedCandidate = -1;
            }

            RefreshConfirm();
            UpdateInstruction();
        }

        /// <summary>[무엇] 캔슬된 카드를 원래 후보 자리로 되돌린다. [왜] 후보 칸은 픽 순서 고정이라 자리를 찾아 넣는다.</summary>
        private void ReturnToCandidates(CharacterData card)
        {
            for (int i = 0; i < _candidates.Length && i < _picked.Count; i++)
            {
                if (_picked[i] != card) continue;
                _candidates[i].SetCharacter(card);
                _candidates[i].SetSelected(false);
                return;
            }
        }

        private void RefreshConfirm()
        {
            if (_confirmButton == null) return;
            _confirmButton.interactable = _slots.All(s => s.Character != null);
        }

        private void UpdateInstruction()
        {
            if (_instructionText == null) return;

            int placed = _slots.Count(s => s.Character != null);
            _instructionText.text = placed >= GameRules.PartySize
                ? "배치 완료 — 확정을 누르세요 (슬롯을 다시 누르면 취소)"
                : _selectedCandidate >= 0
                    ? $"놓을 자리를 고르세요 ({placed}/{GameRules.PartySize})"
                    : $"배치할 캐릭터를 고르세요 ({placed}/{GameRules.PartySize})";
        }

        /// <summary>
        /// [무엇] 배치 확정 — PlacedOrder 저장 → 엔진 인계 → 상대 파티 공개 연출 → 전투.
        /// [주의] 인계(CompleteAndSetup)가 실패하면 연출도 전투 이동도 하지 않는다.
        ///        잘못된 파티로 전투에 들어가면 리치 계산이 전부 틀어지는데 화면만 봐선 알기 어렵다.
        /// </summary>
        private void OnConfirm()
        {
            if (_slots.Any(s => s.Character == null)) return;

            // 슬롯 인덱스 0이 P1(전방)이므로 그대로가 곧 Position 0..3 순서다.
            BanPickState.SetPlacedOrder(_slots.Select(s => s.Character.Id));

            if (!BanPickState.CompleteAndSetup())
            {
                Debug.LogError("[Place] 세팅 실패 — 전투로 넘어가지 않는다.");
                return;
            }

            if (_confirmButton != null) _confirmButton.interactable = false;
            StartCoroutine(RevealEnemyPartyThenBattle());
        }

        /// <summary>
        /// [무엇] 상대 파티 4장을 순차 페이드인으로 공개한 뒤 전투로 넘어간다.
        /// [왜] 명세 4-2 "확정 → 상대 파티 공개 연출 → Battle".
        /// [주의] 상대 파티는 엔진이 이미 세팅을 끝낸 뒤라 <b>GameManager의 캐릭터 존</b>에서 읽는다.
        ///        UI가 따로 계산하면 엔진과 어긋날 수 있다 (UNITY_PORTING_SPEC 1절 4번).
        /// </summary>
        private IEnumerator RevealEnemyPartyThenBattle()
        {
            var enemyZone = BanPickState.Game.Players[BanPickState.EnemyPlayerId].CharacterZone;

            if (_revealOverlay != null) _revealOverlay.SetActive(true);

            var groups = new List<CanvasGroup>();
            for (int i = 0; i < _revealCards.Length; i++)
            {
                if (i < enemyZone.Count) _revealCards[i].SetCharacter(enemyZone[i].Data);
                else _revealCards[i].SetHidden();

                var group = _revealCards[i].GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 0f;
                groups.Add(group);
            }

            for (int i = 0; i < groups.Count && i < enemyZone.Count; i++)
            {
                yield return FadeIn(groups[i]);
                yield return new WaitForSeconds(RevealIntervalSeconds);
            }

            yield return new WaitForSeconds(RevealHoldSeconds);
            BanPickFlow.GoToBattle();
        }

        private IEnumerator FadeIn(CanvasGroup group)
        {
            if (group == null) yield break;

            for (float t = 0f; t < RevealFadeSeconds; t += Time.deltaTime)
            {
                group.alpha = Mathf.Clamp01(t / RevealFadeSeconds);
                yield return null;
            }
            group.alpha = 1f;
        }
    }
}
