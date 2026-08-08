using CrossAccel.Battle;
using CrossAccel.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// 캐릭터 카드 1장 — 프레임 + 일러스트 + 텍스트 3레이어 조립 (docs/CARD_UI_FIX_SPEC.md).
    ///
    /// 자식 오브젝트는 <see cref="Build"/>가 코드로 만든다. 좌표가 전부 CardArt(실측 좌표표)에서
    /// 나오므로 에디터에서 손으로 배치할 필요가 없고, 카드 표시 크기가 달라도 배율만 바뀐다.
    ///
    /// 배틀에서는 CharacterUnit(현재 HP 등 런타임 상태)을, 밴픽에서는 CharacterData(원본)를 표시한다.
    /// </summary>
    public class CharacterCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image _frame;
        private Image _illustration;
        private Image _bottomScrim;
        private TextMeshProUGUI _hpText;
        private TextMeshProUGUI _reachText;
        private TextMeshProUGUI _raceText;
        private TextMeshProUGUI _weaponText;
        private TextMeshProUGUI _conditionText;
        private TextMeshProUGUI _effectText;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _footerText;
        [SerializeField] private CardHoverPreview _hoverPreview;


        private bool _built;
        private CharacterData _boundData;

        /// <summary>배틀에서 표시 중인 유닛 (밴픽에서는 null).</summary>
        public CharacterUnit Unit { get; private set; }

        /// <summary>
        /// [무엇] 마우스를 올렸을 때 이 카드의 데이터를 띄워줄 프리뷰. 없으면(null) 호버해도 아무 일도 없다.
        /// [왜] 씬 빌더가 CardHoverPreview 하나를 만들고 모든 카드에 나눠 꽂아준다 — 카드 자신은
        ///      프리뷰를 어디에 만들지 몰라도 되게 하려고 참조를 주입받는 방식으로 뒀다.
        /// [주의] 반드시 직렬화 필드로 둔다. auto-property는 Unity가 씬에 저장하지 않아
        ///        빌더가 꽂아도 Play 시 null이 된다.
        /// </summary>
      
        public CardHoverPreview HoverPreview
        {
            get => _hoverPreview;
            set => _hoverPreview = value;
        }

        /// <summary>
        /// [무엇] 이 카드에 마우스가 올라왔다/벗어났다.
        /// [왜] 사거리 하이라이트(리치 내 적 발광 + 거리 칩)는 "누가 누구를 얼마나 멀리 볼 수 있는가"라
        ///      게임 상태를 알아야 정할 수 있다. 카드 자신은 그걸 모르므로 이벤트만 쏘고,
        ///      BattleUIController가 엔진 값을 읽어 어느 카드를 밝힐지 정한다.
        /// [주의] 프리뷰 표시(HoverPreview)와는 별개다 — 둘 다 같은 호버에서 함께 일어난다.
        /// </summary>
        public event System.Action<CharacterCardView> PointerEntered;

        /// <summary>[무엇] 마우스가 이 카드를 벗어났다. [왜] 위 <see cref="PointerEntered"/> 참고.</summary>
        public event System.Action<CharacterCardView> PointerExited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverPreview?.Show(_boundData);
            PointerEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverPreview?.Hide();
            PointerExited?.Invoke(this);
        }

        private void Awake() => EnsureBuilt();

        // ===================== 구성 =====================

#if UNITY_EDITOR
        /// <summary>에디터 도구가 씬에 카드 구조를 미리 구울 때 쓴다 (Play 전에도 카드가 보이도록).</summary>
        public void EditorBuild()
        {
            _built = false;
            EnsureBuilt();
        }
#endif

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            // 씬에 이미 구워져 있으면 다시 만들지 않고 참조만 연결한다 (중복 생성 방지).
            if (TryWireExistingChildren()) return;

            var rect = (RectTransform)transform;
            float scale = CardArt.Scale(rect.rect.width);
            var library = CardArtLibrary.Instance;

            _frame = CardArtBuilder.CreateLayer("Frame", rect);
            if (library != null && library.CharacterFrame != null) _frame.sprite = library.CharacterFrame;
            _frame.color = Color.white;

            _illustration = CardArtBuilder.CreateLayer("Illustration", rect);
            _illustration.gameObject.SetActive(false); // 카드가 바인딩될 때 켠다

            // 하단 스크림 — 일러스트 위, 텍스트 아래. CardArt.ScrimTop부터 카드 하단까지 아래로 갈수록
            // 어두워져 일러스트가 정보 영역을 덮어도 글자가 읽힌다 (목업의 프레임 비네팅 재현).
            _bottomScrim = CardArtBuilder.CreateBox("BottomScrim", rect, CardArt.BottomScrim, scale);
            if (library != null && library.BottomScrim != null) _bottomScrim.sprite = library.BottomScrim;
            _bottomScrim.color = Color.white;
            _bottomScrim.raycastTarget = false;

            // 강조 테두리 — 일러스트/스크림 위, 텍스트 아래. 기본은 꺼둔다.
            _highlight = CardArtBuilder.CreateLayer("Highlight", rect);
            _highlight.color = HighlightColor;
            _highlight.gameObject.SetActive(false);

            var font = library != null ? library.Font : null;

            _hpText = CardArtBuilder.CreateText("HpValue", rect, CardArt.Hp, scale, CardArt.StatFontSize,
                CardArt.StatColor, font, TextAlignmentOptions.Center);
            _reachText = CardArtBuilder.CreateText("ReachValue", rect, CardArt.Reach, scale, CardArt.StatFontSize,
                CardArt.StatColor, font, TextAlignmentOptions.Center);
            _raceText = CardArtBuilder.CreateText("RaceTag", rect, CardArt.RaceTag, scale, CardArt.TagFontSize,
                CardArt.TagColor, font, TextAlignmentOptions.Center);
            _weaponText = CardArtBuilder.CreateText("WeaponTag", rect, CardArt.WeaponTag, scale, CardArt.TagFontSize,
                CardArt.TagColor, font, TextAlignmentOptions.Center);
            // 조건/효과 좌표는 CARD_UI_FIX_SPEC.md에서 "시작점"(좌상단)으로 명시되어 TopLeft로 정렬한다.
            _conditionText = CardArtBuilder.CreateText("ConditionText", rect, CardArt.Condition, scale, CardArt.ConditionFontSize,
                CardArt.ConditionColor, font, TextAlignmentOptions.TopLeft);
            _effectText = CardArtBuilder.CreateText("EffectText", rect, CardArt.Effect, scale, CardArt.EffectFontSize,
                CardArt.EffectColor, font, TextAlignmentOptions.TopLeft);
            _nameText = CardArtBuilder.CreateText("NameText", rect, CardArt.Name, scale, CardArt.NameFontSize,
                CardArt.NameColor, font, TextAlignmentOptions.Center);
            // 하단 종족/직업 — 이름이 y955로 올라오며 빈 하단(y1300~)을 채우는 보조 표기.
            _footerText = CardArtBuilder.CreateText("FooterText", rect, CardArt.Footer, scale, CardArt.FooterFontSize,
                CardArt.FooterColor, font, TextAlignmentOptions.Center);

            // 효과 텍스트는 카드마다 길이가 제각각이라 넘치지 않게 자동 축소한다.
            _effectText.enableAutoSizing = true;
            _effectText.fontSizeMin = CardArt.EffectFontSize * scale * 0.6f;
            _effectText.fontSizeMax = CardArt.EffectFontSize * scale;
            _conditionText.enableAutoSizing = true;
            _conditionText.fontSizeMin = CardArt.ConditionFontSize * scale * 0.6f;
            _conditionText.fontSizeMax = CardArt.ConditionFontSize * scale;
            _nameText.enableAutoSizing = true;
            _nameText.fontSizeMin = CardArt.NameFontSize * scale * 0.5f;
            _nameText.fontSizeMax = CardArt.NameFontSize * scale;
            _footerText.enableAutoSizing = true;
            _footerText.fontSizeMin = CardArt.FooterFontSize * scale * 0.5f;
            _footerText.fontSizeMax = CardArt.FooterFontSize * scale;
        }

        /// <summary>이미 만들어진 자식이 있으면 참조만 연결한다. 하나라도 없으면 false(새로 만들어야 함).</summary>
        private bool TryWireExistingChildren()
        {
            _frame = transform.Find("Frame")?.GetComponent<Image>();
            _illustration = transform.Find("Illustration")?.GetComponent<Image>();
            _bottomScrim = transform.Find("BottomScrim")?.GetComponent<Image>();
            _highlight = transform.Find("Highlight")?.GetComponent<Image>();
            _hpText = transform.Find("HpValue")?.GetComponent<TextMeshProUGUI>();
            _reachText = transform.Find("ReachValue")?.GetComponent<TextMeshProUGUI>();
            _raceText = transform.Find("RaceTag")?.GetComponent<TextMeshProUGUI>();
            _weaponText = transform.Find("WeaponTag")?.GetComponent<TextMeshProUGUI>();
            _conditionText = transform.Find("ConditionText")?.GetComponent<TextMeshProUGUI>();
            _effectText = transform.Find("EffectText")?.GetComponent<TextMeshProUGUI>();
            _nameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            _footerText = transform.Find("FooterText")?.GetComponent<TextMeshProUGUI>();

            return _frame != null && _illustration != null && _bottomScrim != null && _highlight != null
                   && _hpText != null && _reachText != null
                   && _raceText != null && _weaponText != null && _conditionText != null
                   && _effectText != null && _nameText != null && _footerText != null;
        }

        // ===================== 바인딩 =====================

        /// <summary>밴픽 등 원본 데이터만 있을 때 (HP는 최대치를 보여준다).</summary>
        public void Bind(CharacterData data)
        {
            EnsureBuilt();
            Unit = null;
            if (data == null) { SetEmpty(); return; }

            gameObject.SetActive(true);
            ApplyStatic(data);
            _hpText.text = data.MaxHp.ToString();
            _reachText.text = data.Reach.ToString();
        }

        /// <summary>배틀 — 현재 HP·방어도·리치 보정이 반영된 런타임 상태를 보여준다.</summary>
        public void Bind(CharacterUnit unit)
        {
            EnsureBuilt();
            Unit = unit;
            if (unit == null) { SetEmpty(); return; }

            gameObject.SetActive(true);
            ApplyStatic(unit.Data);
            Refresh();
        }

        private void ApplyStatic(CharacterData data)
        {
            _boundData = data;
            var library = CardArtLibrary.Instance;
            var illustration = library != null ? library.GetIllustration(data.Id) : null;

            // 일러스트가 없는 카드(MidRange_Blood 등)는 레이어를 꺼서 프레임+텍스트만 보이게 한다.
            _illustration.gameObject.SetActive(illustration != null);
            if (illustration != null) _illustration.sprite = illustration;

            _nameText.text = data.Name;
            _raceText.text = data.Race;
            _weaponText.text = data.WeaponType;
            _conditionText.text = data.EffectTiming;
            _effectText.text = data.HasEffect ? data.EffectText : "";
            // "·" 구분자는 프로젝트 전역에서 두 항목을 한 줄에 묶을 때 쓰는 관례
            // (예: PlaceSlotView의 "HP n · 리치 n")를 따른 것 — 문서엔 표기법이 없어 임의로 골랐다.
            _footerText.text = $"{data.Race} · {data.WeaponType}";
        }

        /// <summary>HP·방어도·리치처럼 전투 중 변하는 값만 다시 읽는다.</summary>
        public void Refresh()
        {
            if (Unit == null) return;

            _hpText.text = Unit.Defense > 0 ? $"{Unit.CurrentHp}+{Unit.Defense}" : Unit.CurrentHp.ToString();
            _hpText.color = Unit.CurrentHp <= 3 ? new Color(1f, 0.75f, 0.2f) : CardArt.StatColor;
            _reachText.text = Unit.EffectiveReach.ToString();
        }

        /// <summary>사망 등으로 캐릭터 존에서 빠진 칸 (RULES.md R9: 앞당김이라 뒤쪽 칸이 빈다).</summary>
        public void SetEmpty()
        {
            Unit = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// [무엇] 선택 가능/타겟 후보 강조 on/off (레디 페이즈 입력용).
        /// [왜] 손패 카드를 고르면 "쓸 수 있는 캐릭터"를, 타겟 지정 중이면 "리치 내 적"을 밝혀야 한다
        ///      (모듈 C 전투 흐름). 프레임 위에 얇은 테두리를 덧그리는 방식이라 카드 아트를 가리지 않는다.
        /// [주의] 색·발광 연출은 모듈 D 영역이다. 여기서는 스위치와 최소 표현만 둔다.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            EnsureBuilt();
            if (_highlight == null) return;
            _highlight.gameObject.SetActive(highlighted);
        }

        private Image _highlight;

        /// <summary>강조 테두리 색 — 출처: full_prototype.html --accel(#31D0F0).</summary>
        private static readonly Color HighlightColor = new Color32(0x31, 0xD0, 0xF0, 0x66);

        // ===================== 사거리 하이라이트 (모듈 D 3단계) =====================
        // [출처] full_prototype.html
        //   .cchar.dim { filter: brightness(.45) saturate(.5) }        → 리치 밖은 어둡게
        //   .cchar .dchip { top:6px; left:50%; background:var(--cost) } → 거리 칩(금색), 상단 중앙
        //   .cchar.in-reach .dchip { display:block }                    → 리치 내에서만 보임
        // [검산] 카드 126×192.5(scale 0.126) 기준, 칩 56×18 @ top6 → x[35.0,91.0] y[6.0,24.0].
        //   HP육각 x[7.1,30.0] / 리치육각 x[96.0,118.7]와 좌우 5.0px씩 여백 — 겹치지 않는다.
        //   이름 y[114.7,126.0]과는 세로로 완전히 분리.

        /// <summary>거리 칩 크기·위치 (표시 좌표, 카드 폭 126 기준). 위 검산 참고.</summary>
        private const float ChipWidth = 56f;
        private const float ChipHeight = 18f;
        private const float ChipTop = 6f;

        /// <summary>리치 밖 어둡게 덮는 막의 농도. [주의] 프로토타입은 CSS filter(brightness .45)인데
        /// Unity UI엔 filter가 없어 검은 반투명 오버레이로 근사했다(임의값 0.55).</summary>
        private const float DimAlpha = 0.55f;

        private static readonly Color ChipColor = new Color32(0xF2, 0xC9, 0x34, 0xFF);      // --cost
        private static readonly Color ChipTextColor = new Color32(0x4A, 0x3A, 0x05, 0xFF);  // .dchip color

        private Image _dim;
        private Image _distanceChip;
        private Text _distanceChipText;

        /// <summary>
        /// [무엇] 리치 밖이라 어둡게 처리할지.
        /// [왜] 어느 적이 사거리 안인지 밝히는 것만으론 약하고, 밖을 죽여야 대비가 살아난다
        ///      (프로토타입 .cchar.dim).
        /// [주의] 카드 내용을 지우지 않고 위에 막만 덮는다 — 정보는 계속 읽히되 눈이 안 가게.
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            EnsureBuilt();
            EnsureReachOverlays();
            if (_dim != null) _dim.gameObject.SetActive(dimmed);
        }

        /// <summary>
        /// [무엇] "거리 N" 칩을 띄운다. null이면 숨긴다.
        /// [왜] 리치 판정이 "내 위치 + 상대 위치 + 1"이라 눈으로 세기 어렵다 — 실제 거리를 숫자로
        ///      보여줘야 왜 닿고 안 닿는지 납득이 된다(프로토타입 .dchip).
        /// [주의] <b>거리를 여기서 계산하지 않는다.</b> 호출자가 엔진의
        ///        <see cref="CharacterUnit.CalculateDistance"/> 값을 넘겨준다.
        /// </summary>
        public void SetDistanceChip(int? distance)
        {
            EnsureBuilt();
            EnsureReachOverlays();
            if (_distanceChip == null) return;

            bool show = distance.HasValue;
            _distanceChip.gameObject.SetActive(show);
            if (show && _distanceChipText != null) _distanceChipText.text = $"거리 {distance.Value}";
        }

        /// <summary>
        /// 사거리용 오버레이(dim/거리칩)를 처음 쓸 때 만든다.
        /// [왜] EnsureBuilt의 자식 목록에 넣으면, 이미 구워진 씬은 자식이 모자라
        ///      TryWireExistingChildren이 실패하고 전체를 <b>다시</b> 만들어 중복이 생긴다.
        ///      ActSlotView.SetFiring과 같은 지연 생성 방식으로 그 위험을 피한다.
        /// </summary>
        private void EnsureReachOverlays()
        {
            if (_dim == null)
            {
                var found = transform.Find("Dim");
                if (found != null) _dim = found.GetComponent<Image>();
                else
                {
                    _dim = CardArtBuilder.CreateLayer("Dim", (RectTransform)transform);
                    _dim.color = new Color(0f, 0f, 0f, DimAlpha);
                    _dim.transform.SetAsLastSibling();
                    _dim.gameObject.SetActive(false);
                }
            }

            if (_distanceChip != null) return;

            var existing = transform.Find("DistanceChip");
            if (existing != null)
            {
                _distanceChip = existing.GetComponent<Image>();
                _distanceChipText = existing.Find("Text")?.GetComponent<Text>();
                return;
            }

            var go = new GameObject("DistanceChip", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2((((RectTransform)transform).rect.width - ChipWidth) / 2f, -ChipTop);
            rt.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            rt.SetAsLastSibling(); // Dim보다 위 — 리치 안 카드에서 칩이 가려지면 안 된다

            _distanceChip = go.GetComponent<Image>();
            _distanceChip.color = ChipColor;
            _distanceChip.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _distanceChipText = textGo.AddComponent<Text>();
            _distanceChipText.font = GameUIFont.Legacy; // 카드 내용이 아니라 게임 UI 표시
            _distanceChipText.fontSize = 11;
            _distanceChipText.fontStyle = FontStyle.Bold;
            _distanceChipText.color = ChipTextColor;
            _distanceChipText.alignment = TextAnchor.MiddleCenter;
            _distanceChipText.raycastTarget = false;

            go.SetActive(false);
        }
    }
}
