using System;
using System.Collections;
using CrossAccel.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// 손패 스킬 카드 1장 (UNITY_BATTLE_UI_SPEC.md 4번 SkillCard 구조).
    /// 자식을 이름으로 스스로 찾으므로 BattleUIBuilder가 정해진 이름만 만들어주면 된다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Text _nameText;
        private Text _effectText;
        private Text _costText;
        private Text _speedText;
        private Image _background;
        private Button _button;

        /// <summary>손패(PlayerState.Hand) 안에서의 인덱스.</summary>
        public int HandIndex { get; private set; }

        /// <summary>클릭됨 (인자 = HandIndex). 사용 불가 상태면 발생하지 않는다.</summary>
        public event Action<int> Clicked;

        private void Awake()
        {
            _nameText = transform.Find("NameText")?.GetComponent<Text>();
            _effectText = transform.Find("EffectText/Value")?.GetComponent<Text>();
            _costText = transform.Find("CostCircle/Value")?.GetComponent<Text>();
            _speedText = transform.Find("SpeedCircle/Value")?.GetComponent<Text>();
            _background = GetComponent<Image>();

            _button = GetComponent<Button>();
            _button.onClick.AddListener(() => Clicked?.Invoke(HandIndex));
        }

        /// <summary>
        /// 카드를 표시한다. playable=false면 클릭이 막히고 흐리게 보인다
        /// (이 스킬을 쓸 수 있는 캐릭터가 파티에 없거나, 지금이 레디 페이즈가 아닐 때).
        /// </summary>
        public void Bind(SkillData skill, int handIndex, bool playable)
        {
            HandIndex = handIndex;

            if (_nameText != null) _nameText.text = skill.Name;
            if (_effectText != null) _effectText.text = skill.Skill1Effect;
            if (_speedText != null) _speedText.text = skill.Speed.ToString();
            if (_costText != null)
            {
                // DATA_SCHEMA.md: skill1Cost -1 = 데이터 미기재(X코스트) → 화면엔 "X"로 보여준다.
                _costText.text = skill.Skill1Cost < 0 ? "X" : skill.Skill1Cost.ToString();
            }

            _playable = playable;
            _button.interactable = playable;
            ApplyColor();
        }

        private bool _playable = true;
        private bool _highlighted;

        // 색 — 출처: full_prototype.html의 .hcard / .sel / .unusable 상태.
        private static readonly Color NormalColor = new Color(0.09f, 0.11f, 0.19f, 1f);
        private static readonly Color UnusableColor = new Color(0.09f, 0.11f, 0.19f, 0.45f);
        private static readonly Color SelectedColor = new Color(0.12f, 0.42f, 0.52f, 1f);

        /// <summary>
        /// [무엇] 지금 고른 카드(또는 코스트로 찍은 카드) 강조.
        /// [왜] 레디에서 "무엇을 고른 상태인지", 코스트 단계에서 "무엇을 버릴지"가 보여야 한다.
        /// [주의] 강조는 선택 상태 표시일 뿐 게임 상태가 아니다. 확정은 컨트롤러가 엔진에 넘긴다.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (_background == null) return;
            _background.color = _highlighted ? SelectedColor : (_playable ? NormalColor : UnusableColor);
        }

        // ===================== 호버 확대 (모듈 D 3단계) =====================
        // [출처] full_prototype.html
        //   .hcard { transition: transform .18s, box-shadow .18s }
        //   .hcard:hover { z-index:50; transform: translateY(-62px) rotate(0deg) scale(1.12) }

        /// <summary>호버 시 위로 띄우는 거리(px). [출처] .hcard:hover translateY(-62px)</summary>
        private const float HoverLift = 62f;

        /// <summary>호버 시 확대 배율. [출처] .hcard:hover scale(1.12)</summary>
        private const float HoverScale = 1.12f;

        /// <summary>확대/복귀 트윈 시간. [출처] .hcard transition .18s</summary>
        private const float HoverTweenMs = 180f;

        private Vector2 _restPosition;
        private Vector3 _restRotation;
        private int _restSiblingIndex;
        private bool _restCaptured;
        private bool _hovering;
        private Coroutine _hoverTween;

        /// <summary>
        /// [무엇] 손패 카드에 마우스가 올라오면 위로 떠오르며 커지고, 부채꼴 기울기를 편다.
        /// [왜] 부채꼴로 겹쳐 있어 카드 내용이 서로 가려진다 — 프로토타입도 호버 시 들어올려
        ///      그 한 장만 온전히 보이게 한다.
        /// [주의] 제자리 좌표는 HandFanLayout이 정한다. 여기서는 "지금 위치"를 복귀 지점으로 기억해
        ///        두고 벗어날 때 되돌린다 — 레이아웃 계산을 다시 하지 않는다.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_playable) return; // 못 쓰는 카드는 들어올리지 않는다(흐림 상태 유지)

            CaptureRest();
            _hovering = true;
            transform.SetAsLastSibling(); // 프로토타입 z-index:50 — 이웃 카드 위로 올라온다
            StartHoverTween(_restPosition + new Vector2(0f, HoverLift), Vector3.zero, HoverScale);
        }

        /// <summary>[무엇] 마우스가 벗어나면 제자리·원래 기울기로 돌아간다.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_restCaptured || !_hovering) return;

            _hovering = false;
            transform.SetSiblingIndex(_restSiblingIndex);
            StartHoverTween(_restPosition, _restRotation, 1f);
        }

        /// <summary>
        /// 제자리 좌표를 기억한다.
        /// [주의] 호버 중이 아닐 때만 갱신한다 — 떠 있는 상태를 "제자리"로 잘못 기억하면
        ///        카드가 점점 위로 올라가 버린다.
        /// </summary>
        private void CaptureRest()
        {
            if (_hovering) return;
            var rt = (RectTransform)transform;
            _restPosition = rt.anchoredPosition;
            _restRotation = rt.localEulerAngles;
            _restSiblingIndex = rt.GetSiblingIndex();
            _restCaptured = true;
        }

        private void StartHoverTween(Vector2 targetPosition, Vector3 targetRotation, float targetScale)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_hoverTween != null) StopCoroutine(_hoverTween);
            _hoverTween = StartCoroutine(HoverRoutine(targetPosition, targetRotation, targetScale));
        }

        private IEnumerator HoverRoutine(Vector2 targetPosition, Vector3 targetRotation, float targetScale)
        {
            var rt = (RectTransform)transform;
            Vector2 fromPosition = rt.anchoredPosition;
            Quaternion fromRotation = rt.localRotation;
            Vector3 fromScale = rt.localScale;
            Quaternion toRotation = Quaternion.Euler(targetRotation);
            Vector3 toScale = Vector3.one * targetScale;

            float duration = HoverTweenMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(fromPosition, targetPosition, t);
                rt.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
                rt.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }

            rt.anchoredPosition = targetPosition;
            rt.localRotation = toRotation;
            rt.localScale = toScale;
            _hoverTween = null;
        }

        /// <summary>
        /// [무엇] HandFanLayout이 카드를 다시 배치했을 때 호출 — 기억해둔 제자리를 새 좌표로 갱신한다.
        /// [왜] 손패가 바뀌면(카드를 내거나 뽑으면) 부채꼴이 다시 깔린다. 그때 예전 제자리로
        ///      되돌아가면 카드가 엉뚱한 곳에 놓인다.
        /// </summary>
        public void OnLayoutMoved()
        {
            _hovering = false;
            _restCaptured = false;
            var rt = (RectTransform)transform;
            rt.localScale = Vector3.one;
        }
    }
}
