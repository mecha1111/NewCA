// 담당 모듈: A (밴픽 공개화 + PlaceScene) — UNITY_PORTING_SPEC.md 4-2 / 5절
// 의존: PlaceSceneBuilder가 만든 자식 구조(EmptyState / FilledState/Portrait,NameText,InfoText)
//       PlaceSceneController가 이 뷰를 조작한다.

using System;
using CrossAccel.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 배치 화면에서 캐릭터 한 명을 담는 칸. 클릭을 상위로 알린다.
    /// [왜] "카드 클릭 → 슬롯 클릭"으로 배치하고, 채워진 슬롯을 다시 클릭하면 캔슬해야 해서
    ///      칸 자체가 클릭 대상이자 상태(빈칸/채움) 표시자여야 한다.
    /// [주의] <b>배치 슬롯(P1~P4)과 아래쪽 후보 카드 양쪽에 같은 컴포넌트를 쓴다.</b>
    ///        둘 다 "캐릭터를 보여주고 클릭을 받는 칸"이라 동작이 같기 때문이다.
    ///        후보 카드로 쓸 때는 빈칸 상태를 쓰지 않고 <see cref="SetHidden"/>으로 숨긴다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PlaceSlotView : MonoBehaviour
    {
        private GameObject _emptyState;
        private GameObject _filledState;
        private Text _nameText;
        private Text _infoText;
        private Button _button;

        /// <summary>[무엇] 이 칸의 번호. 슬롯이면 0=P1..3=P4, 후보 카드면 후보 목록 인덱스.</summary>
        public int Index { get; private set; }

        /// <summary>[무엇] 지금 담고 있는 캐릭터. 비었으면 null.</summary>
        public CharacterData Character { get; private set; }

        /// <summary>[무엇] 칸 클릭 (인자=Index). [주의] 숨겨진 칸은 클릭되지 않는다.</summary>
        public event Action<int> Clicked;

        private void Awake()
        {
            _emptyState = transform.Find("EmptyState")?.gameObject;
            _filledState = transform.Find("FilledState")?.gameObject;
            _nameText = transform.Find("FilledState/NameText")?.GetComponent<Text>();
            _infoText = transform.Find("FilledState/InfoText")?.GetComponent<Text>();

            _button = GetComponent<Button>();
            _button.onClick.AddListener(() => Clicked?.Invoke(Index));
        }

        /// <summary>[무엇] 칸 번호를 정한다. [왜] 클릭 이벤트가 어느 칸인지 알려주려면 필요하다.</summary>
        public void SetIndex(int index) => Index = index;

        /// <summary>
        /// [무엇] 칸을 비운다 (빈칸 표시).
        /// [왜] 배치 취소 시 슬롯이 다시 비어야 하고, 그때 클릭도 막아야 한다
        ///      (빈 슬롯을 눌러도 캔슬할 게 없으므로 배치 대기 상태에서만 의미가 있다).
        /// </summary>
        public void SetEmpty()
        {
            Character = null;
            gameObject.SetActive(true);
            if (_emptyState != null) _emptyState.SetActive(true);
            if (_filledState != null) _filledState.SetActive(false);
        }

        /// <summary>
        /// [무엇] 칸에 캐릭터를 표시한다.
        /// [주의] HP/리치는 원본 데이터(CharacterData) 값이다. 전투 중 변하는 값이 아니므로
        ///        배치 화면에서는 이것으로 충분하다 (엔진 상태를 만들기 전 단계라 CharacterUnit이 없다).
        /// </summary>
        public void SetCharacter(CharacterData data)
        {
            Character = data;
            gameObject.SetActive(true);
            if (_emptyState != null) _emptyState.SetActive(false);
            if (_filledState != null) _filledState.SetActive(true);
            if (_nameText != null) _nameText.text = data.Name;
            if (_infoText != null) _infoText.text = $"HP {data.MaxHp} · 리치 {data.Reach}\n{data.WeaponType} · {data.Race}";
        }

        /// <summary>
        /// [무엇] 칸을 통째로 숨긴다.
        /// [왜] 후보 카드가 슬롯으로 옮겨가면 후보 자리에는 아무것도 남지 않아야 한다.
        ///      빈칸 표시(SetEmpty)는 슬롯 전용이라 후보 쪽에서는 이걸 쓴다.
        /// </summary>
        public void SetHidden()
        {
            Character = null;
            gameObject.SetActive(false);
        }

        /// <summary>[무엇] 선택 강조 (후보 카드에서 지금 고른 것 표시).</summary>
        public void SetSelected(bool selected)
        {
            var image = GetComponent<Image>();
            if (image == null) return;
            image.color = selected ? SelectedColor : NormalColor;
        }

        // 선택 강조 색 — 출처: full_prototype.html --accel(#31D0F0) 계열을 어둡게 깐 값.
        private static readonly Color NormalColor = new Color(0.09f, 0.11f, 0.19f, 1f);
        private static readonly Color SelectedColor = new Color(0.12f, 0.35f, 0.45f, 1f);
    }
}
