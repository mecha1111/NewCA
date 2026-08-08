// 담당 모듈: C (전투 흐름 + 엔진 브릿지) — UNITY_PORTING_SPEC.md 4-4 / 4-5
// 의존: BattleUIBuilder가 만든 자식 구조(EmptyState / FilledState/{CostCircle,SpeedCircle,NameText,AccelBadge,CancelButton,SwiftButton})

using System;
using CrossAccel.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 액트존 슬롯 1칸 — 배치된 스킬을 보여주고 취소·신속 토글 입력을 받는다.
    /// [왜] 레디 페이즈는 "배치 → 취소 → 신속 지정"이 자유로워야 해서(프로토타입 renderSlot),
    ///      칸마다 취소 버튼과 신속 토글이 필요하다.
    /// [주의] 이 뷰는 상태를 저장하지 않는다. 무엇이 배치됐는지는 엔진의 ActiveZone이 진실이고
    ///        여기는 <see cref="Slot"/>으로 그 참조만 들고 있다가 이벤트에 실어 보낸다.
    /// </summary>
    public class ActSlotView : MonoBehaviour
    {
        private GameObject _emptyState;
        private GameObject _filledState;
        private Text _nameText;
        private Text _costText;
        private Text _speedText;
        private GameObject _accelBadge;
        private Button _cancelButton;
        private Button _swiftButton;
        private Text _swiftLabel;
        private Image _highlight; // 담당 모듈: D — 발동 발광(SetFiring). 자식이 없으면 처음 켤 때 만든다.

        /// <summary>[무엇] 이 칸이 지금 보여주는 엔진 슬롯. 비었으면 null.</summary>
        public ActiveSlot Slot { get; private set; }

        /// <summary>[무엇] 취소(X) 클릭. [주의] 실제 취소는 컨트롤러가 엔진 API로 한다.</summary>
        public event Action<ActSlotView> CancelClicked;

        /// <summary>[무엇] 신속 토글 클릭.</summary>
        public event Action<ActSlotView> SwiftToggled;

        private void Awake()
        {
            _emptyState = transform.Find("EmptyState")?.gameObject;
            _filledState = transform.Find("FilledState")?.gameObject;
            _nameText = transform.Find("FilledState/NameText")?.GetComponent<Text>();
            _costText = transform.Find("FilledState/CostCircle/Value")?.GetComponent<Text>();
            _speedText = transform.Find("FilledState/SpeedCircle/Value")?.GetComponent<Text>();
            _accelBadge = transform.Find("FilledState/AccelBadge")?.gameObject;

            _cancelButton = transform.Find("FilledState/CancelButton")?.GetComponent<Button>();
            _swiftButton = transform.Find("FilledState/SwiftButton")?.GetComponent<Button>();
            _swiftLabel = transform.Find("FilledState/SwiftButton/Label")?.GetComponent<Text>();

            if (_cancelButton != null) _cancelButton.onClick.AddListener(() => CancelClicked?.Invoke(this));
            if (_swiftButton != null) _swiftButton.onClick.AddListener(() => SwiftToggled?.Invoke(this));
        }

        /// <summary>[무엇] 빈 칸으로 만든다.</summary>
        public void SetEmpty()
        {
            Slot = null;
            if (_emptyState != null) _emptyState.SetActive(true);
            if (_filledState != null) _filledState.SetActive(false);
        }

        /// <summary>
        /// [무엇] 카드가 놓였지만 아직 뒷면인 상태.
        /// [왜] RULES.md 5-2 — 레디 페이즈에는 서로 뒷면으로 놓고, 액션 페이즈에 뒤집는다.
        /// </summary>
        public void SetFaceDown(ActiveSlot slot)
        {
            Slot = slot;
            if (_emptyState != null) _emptyState.SetActive(false);
            if (_filledState == null) return;

            _filledState.SetActive(true);
            if (_nameText != null) _nameText.text = "?";
            if (_costText != null) _costText.text = "";
            if (_speedText != null) _speedText.text = "";
            if (_accelBadge != null) _accelBadge.SetActive(false);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
            if (_swiftButton != null) _swiftButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// [무엇] 앞면 공개 — 스킬 내용과 조작 버튼을 보여준다.
        /// [주의] interactive=false면 취소·신속 버튼을 숨긴다 (레디가 아닌 단계, 또는 상대 슬롯).
        /// [주의] swiftAvailable은 "신속 자원이 남았는가"다. 이미 신속으로 지정된 슬롯은 해제가 가능해야
        ///        하므로 자원이 0이어도 버튼을 살려 둔다.
        /// </summary>
        public void SetFilled(ActiveSlot slot, bool interactive, bool swiftAvailable)
        {
            Slot = slot;
            if (_emptyState != null) _emptyState.SetActive(false);
            if (_filledState == null) return;

            _filledState.SetActive(true);
            if (_nameText != null) _nameText.text = slot.Skill.Name;
            if (_speedText != null) _speedText.text = slot.Skill.Speed.ToString();
            if (_costText != null) _costText.text = slot.Cost.ToString();
            if (_accelBadge != null) _accelBadge.SetActive(slot.IsSwift);   // ⚡ 신속 표시

            if (_cancelButton != null) _cancelButton.gameObject.SetActive(interactive);

            if (_swiftButton != null)
            {
                _swiftButton.gameObject.SetActive(interactive);
                _swiftButton.interactable = slot.IsSwift || swiftAvailable;
                // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: "⚡ ON" / "⚡")
                // [왜] 현재 게임 UI 폰트(LegacyRuntime)는 ⚡(U+26A1)를 아예 안 그린다(컬러 이모지라
                //      폰트 아틀라스에 못 굽는다 — 픽셀 측정으로 확인). 빈칸으로 보이므로 글자로 대체.
                if (_swiftLabel != null) _swiftLabel.text = slot.IsSwift ? "신속 ON" : "신속";
            }
        }

        /// <summary>
        /// [무엇] 담당 모듈: D — 이 슬롯이 "지금 발동 중"임을 보여주는 발광 테두리 on/off.
        /// [왜] OnSlotActivating 훅이 뜬 순간부터 효과가 화면에 반영되는 순간까지, "지금 이 카드가
        ///      처리되고 있다"를 보여줘야 눈으로 순서를 따라갈 수 있다(full_prototype .aslot.firing).
        /// [주의] 이 뷰는 BattleUIBuilder(모듈 C)가 자식을 미리 굽는 구조라, 모듈 D가 빌더를 고치지
        ///        않고도 새 오버레이를 붙일 수 있게 처음 호출될 때 스스로 만든다(지연 생성).
        /// </summary>
        public void SetFiring(bool firing)
        {
            EnsureHighlight();
            if (_highlight != null) _highlight.gameObject.SetActive(firing);
        }

        private void EnsureHighlight()
        {
            if (_highlight != null) return;

            var existing = transform.Find("Highlight");
            if (existing != null)
            {
                _highlight = existing.GetComponent<Image>();
                return;
            }

            var go = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _highlight = go.GetComponent<Image>();
            _highlight.color = FiringColor;
            _highlight.raycastTarget = false;
            _highlight.gameObject.SetActive(false);
        }

        // 발광 색 — 출처: full_prototype.html .aslot.firing box-shadow의 --accel(#31D0F0),
        // CharacterCardView.HighlightColor와 같은 값을 써서 카드/슬롯 강조 언어를 통일했다.
        private static readonly Color FiringColor = new Color32(0x31, 0xD0, 0xF0, 0x66);
    }
}
