using CrossAccel.Data;
using System;
using System.Collections;
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

        private SkillData _boundSkill;

        /// <summary>
        /// [무엇] 마우스를 올렸을 때 이 스킬의 데이터를 띄워줄 프리뷰. 없으면 호버해도 패널이 안 뜬다.
        /// [왜] 손패 카드는 작아서 효과 전문이 잘린다. 좌측 CardHoverPreview에 전문을 넘긴다.
        /// [주의] 직렬화 필드로 둔다. auto-property는 씬에 저장되지 않는다.
        ///        손패는 빌더가 미리 만들어 두지만, BattleUIController가 런타임에도 다시 꽂아 준다.
        /// </summary>
        [SerializeField] private CardHoverPreview _hoverPreview;
        public CardHoverPreview HoverPreview
        {
            get => _hoverPreview;
            set => _hoverPreview = value;
        }

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
            _boundSkill = skill;
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

        // [주의] 제자리는 "지금 화면 좌표"를 CaptureRest로 찍지 않는다.
        //        복귀 트윈 중 재호버하면 떠 있는 높이가 제자리로 굳어 계단처럼 올라간다.
        //        HandFanLayout이 넣어 준 좌표(_layout*)만 진실로 쓴다.
        private Vector2 _layoutPosition;
        private Vector3 _layoutRotation;
        private int _layoutSiblingIndex;
        private bool _hasLayout;
        private bool _hovering;
        private Coroutine _hoverTween;

        /// <summary>
        /// [무엇] 손패 카드에 마우스가 올라오면 위로 떠오르며 커지고, 부채꼴 기울기를 편다.
        /// [왜] 부채꼴로 겹쳐 있어 카드 내용이 서로 가려진다 — 프로토타입도 호버 시 들어올려
        ///      그 한 장만 온전히 보이게 한다.
        /// [주의] 목표 높이는 항상 layout + HoverLift. 현재 시각 좌표에 Lift를 더하지 않는다.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            // [주의] 사용 불가(흐림) 카드도 효과 전문은 볼 수 있어야 한다 — 프리뷰는 playable과 분리한다.
            HoverPreview?.Show(_boundSkill);

            if (!_playable) return;
            if (!_hasLayout) return;

            _hovering = true;
            transform.SetAsLastSibling();
            StartHoverTween(_layoutPosition + new Vector2(0f, HoverLift), Vector3.zero, HoverScale);
        }

        /// <summary>
        /// [무엇] 마우스가 벗어나면 레이아웃 제자리·원래 기울기로 돌아간다.
        /// [주의] 복귀 목표는 layout 고정값이라, 복귀 트윈 중 다시 Enter해도 더 위로 쌓이지 않는다.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            HoverPreview?.Hide();

            if (!_hovering) return;

            _hovering = false;
            if (_hasLayout)
            {
                transform.SetSiblingIndex(_layoutSiblingIndex);
                StartHoverTween(_layoutPosition, _layoutRotation, 1f);
            }
            else
            {
                SnapToLayoutOrIdentity();
            }
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
        /// [무엇] HandFanLayout이 카드를 다시 배치한 직후 호출 — 레이아웃 좌표를 제자리로 확정한다.
        /// [왜] 손패가 바뀌면 부채꼴이 다시 깔린다. 호버 목표/복귀 목표는 이 값만 쓴다.
        /// [주의] 진행 중 트윈을 끊는다. 호버 중이면 새 레이아웃 기준으로 다시 띄운다.
        ///        위치·회전은 HandFanLayout이 이미 써 둔 뒤라 여기서 읽기만 한다.
        /// </summary>
        public void OnLayoutMoved()
        {
            var rt = (RectTransform)transform;
            _layoutPosition = rt.anchoredPosition;
            _layoutRotation = rt.localEulerAngles;
            _layoutSiblingIndex = rt.GetSiblingIndex();
            _hasLayout = true;

            if (_hoverTween != null)
            {
                StopCoroutine(_hoverTween);
                _hoverTween = null;
            }

            if (_hovering && _playable)
            {
                rt.anchoredPosition = _layoutPosition + new Vector2(0f, HoverLift);
                rt.localEulerAngles = Vector3.zero;
                rt.localScale = Vector3.one * HoverScale;
                transform.SetAsLastSibling();
            }
            else
            {
                _hovering = false;
                rt.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// [무엇] 비활성화 시 호버 잔상(떠 있는 좌표·스케일)을 남기지 않는다.
        /// [왜] 카드가 꺼졌다가 다시 켜질 때 이전 높이가 남으면 같은 버그가 재발한다.
        /// [주의] 여기에서는 SetSiblingIndex를 호출하지 않는다.
        ///        부모가 활성/비활성 전환 중일 때 sibling을 바꾸면 Unity가 예외를 던진다
        ///        (HandContainer가 카드를 끄며 Layout 할 때 재현).
        /// </summary>
        private void OnDisable()
        {
            if (_hoverTween != null)
            {
                StopCoroutine(_hoverTween);
                _hoverTween = null;
            }

            _hovering = false;

            var rt = (RectTransform)transform;
            if (_hasLayout)
            {
                rt.anchoredPosition = _layoutPosition;
                rt.localEulerAngles = _layoutRotation;
            }
            rt.localScale = Vector3.one;
        }

        private void SnapToLayoutOrIdentity()
        {
            var rt = (RectTransform)transform;
            if (_hasLayout)
            {
                rt.anchoredPosition = _layoutPosition;
                rt.localEulerAngles = _layoutRotation;
                // active일 때만 sibling 복구 — 비활성 전환 중 호출을 막는다.
                if (gameObject.activeInHierarchy)
                    rt.SetSiblingIndex(_layoutSiblingIndex);
            }
            rt.localScale = Vector3.one;
        }
    }
}