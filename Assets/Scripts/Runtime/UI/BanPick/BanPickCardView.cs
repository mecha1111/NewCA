// 담당 모듈: A (밴픽 공개화 + PlaceScene) — UNITY_PORTING_SPEC.md 5절
// 3레이어(프레임/일러스트/스크림+텍스트) 조립은 모듈 B가 작업 3에서 CharacterCardView와 같은
// 문법으로 다시 짰다(UNITY_PORTING_SPEC 5절 모듈 B). public API(Bind/SetBanned/SetPicked/SetSelected/
// SetStatusLabel/ClearStatusLabel/Index/Clicked)는 모듈 A·씬 컨트롤러가 그대로 쓰므로 바뀌지 않았다.

using CrossAccel.Data;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 밴픽 화면의 카드 한 장. 프레임+일러스트+스크림+텍스트 3레이어로 데이터를 표시하고,
    ///        밴/픽/상태라벨 오버레이와 클릭을 담당한다.
    /// [왜] 밴픽 카드도 배틀 카드(CharacterCardView)와 같은 아트 언어를 쓰도록 통일했다
    ///      (작업 3 지시). 좌표는 CardArt(1000×1528 원본)를 카드 실제 크기로 배율 환산해 재사용한다 —
    ///      배율은 항상 <see cref="CardArt.Scale"/>(폭 기준 단일값)만 쓴다. 폭/높이를 따로 배율화하면
    ///      카드가 왜곡되기 때문이다(CARD_UI_FIX_SPEC.md "왜곡 금지"). 그래서 카드 높이 자체도
    ///      <see cref="CardArt.HeightFor"/>로 폭에서 역산한 값을 쓴다(BanPickUIBuilder 참고).
    /// [주의] 조건/효과/하단 종족·직업 표기는 넣지 않는다 — 밴픽 카드는 원래도 이름/체력/리치/태그만
    ///        보여줬고(BanPickCardView 기존 Bind), full_prototype.html의 .pcard도 동일하게 간단하다.
    ///        효과 전문은 작업 4의 CardHoverPreview가 보여준다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class BanPickCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // ── 상태 라벨(상대 픽/상대 밴) 배치 상수 ──────────────────────────────────
        // [왜 이 값들인가] 라벨이 카드 그림·글자와 겹치면 안 된다(HANDOFF 협업규칙 2).
        //   작업 1로 이름이 태그보다 위로 올라오면서(CardArt.Name이 CardArt.WeaponTag/RaceTag보다 위)
        //   "태그 아래 ~ 카드 하단"이 완전히 빈 구간이 됐다(조건/효과/하단표기는 밴픽 카드에 없음).
        //   그 구간 맨 위, 태그 바로 아래에 라벨을 둔다. 검산은 StatusLabelTop 참고.
        private const float StatusLabelMarginBelowTags = 6f;  // 태그 하단에서 띄우는 여백(고정 px, 임의)
        private const float StatusLabelHeight = 18f;          // 고정 px (기존 값 유지)
        private const float StatusLabelSideMargin = 4f;       // 고정 px (기존 값 유지)
        private const int StatusLabelFontSize = 10;

        // ── PickMark("✓ PICK") 배치 상수 ──────────────────────────────────────
        // [출처] docs/CARD_ASSET_SPEC.md §3 실측: 체력/리치 육각형 전체가 y 83~275(원본 1000×1528 기준)를
        //        차지한다(라벨 "체력"/"리치" 포함). 숫자만 담는 CardArt.Hp/Reach 박스(y95~205)보다
        //        아래까지 육각 아이콘이 이어지므로, 겹침 없이 배치하려면 275를 기준으로 삼아야 한다.
        // [왜] 작업 2에서 PickMark를 육각 아래로 옮겨 HpHex와의 겹침을 없앴다. 작업 3에서 육각을
        //      근사 컬러 패널 대신 실제 프레임 아트로 바꾸면서 "육각 하단" 기준값도 근사치(HexHeightRatio)
        //      대신 이 실측값(275)으로 다시 계산한다 — 위치를 "유지"하되(안 겹침) 새 레이아웃에 맞춘다.
        private const float HexBottomSource = 275f;
        private const float PickMarkTopMargin = 6f;           // 육각 하단에서 띄우는 여백(고정 px, 임의)
        private const float PickMarkWidthRatio = 0.7f;        // 카드 폭 대비(기존 값 유지)
        private const float PickMarkHeight = 20f;              // 고정 px (기존 값 유지)

        private const string StatusLabelName = "StatusLabel";

        private Image _frame;
        private Image _illustration;
        private Image _bottomScrim;
        private Image _highlight;
        private Image _clickCatcher;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _hpValueText;
        private TextMeshProUGUI _reachValueText;
        private TextMeshProUGUI _weaponTagText;
        private TextMeshProUGUI _raceTagText;
        private GameObject _banOverlay;
        private Text _banLabelText;      // 게임 UI 오버레이 텍스트 — 카드 폰트(KOHINanum) 아님, GameUIFont 사용
        private GameObject _pickMark;
        private Button _button;
        private Image _statusLabelBox;
        private Text _statusLabelText;

        private bool _built;
        private CharacterData _boundData;

        /// <summary>[무엇] 풀(MyPool/EnemyPool) 안에서의 위치. [주의] 밴/픽 기록은 이 인덱스로 오간다.</summary>
        public int Index { get; private set; }

        public bool IsSelected { get; private set; }
        public bool IsBanned { get; private set; }
        public bool IsPicked { get; private set; }

        /// <summary>[무엇] 카드 클릭 (인자=Index). [주의] 클릭 불가 상태면 발생하지 않는다.</summary>
        public event Action<int> Clicked;

        /// <summary>
        /// [무엇] 마우스를 올렸을 때 이 카드의 데이터를 띄워줄 프리뷰(작업 4). 없으면 아무 일도 없다.
        /// [주의] 반드시 직렬화 필드로 둔다. auto-property는 Unity가 씬에 저장하지 않아
        ///        빌더가 꽂아도 Play 시 null이 된다.
        /// </summary>
        [SerializeField] private CardHoverPreview _hoverPreview;
        public CardHoverPreview HoverPreview
        {
            get => _hoverPreview;
            set => _hoverPreview = value;
        }

        public void OnPointerEnter(PointerEventData eventData) => HoverPreview?.Show(_boundData);
        public void OnPointerExit(PointerEventData eventData) => HoverPreview?.Hide();

        private void Awake()
        {
            EnsureBuilt();
            _button.onClick.AddListener(() => Clicked?.Invoke(Index));
            EnsureStatusLabel();
        }

        // ===================== 구성 (CharacterCardView와 같은 3레이어 문법, CardArtBuilder 공용) =====================

#if UNITY_EDITOR
        /// <summary>에디터 도구가 씬에 카드 구조를 미리 구울 때 쓴다.</summary>
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

            _button = GetComponent<Button>();

            if (TryWireExistingChildren()) return;

            var rect = (RectTransform)transform;
            // [주의] 폭 기준 단일 배율만 쓴다 — 밴/픽 카드 높이(BanPickUIBuilder.BanCardHeight 등)는
            // CardArt.HeightFor(폭)로 이미 비율이 맞게 계산돼 있으므로 별도 세로 배율이 필요 없다.
            float scale = CardArt.Scale(rect.rect.width);
            var library = CardArtLibrary.Instance;

            _frame = CardArtBuilder.CreateLayer("Frame", rect);
            if (library != null && library.CharacterFrame != null) _frame.sprite = library.CharacterFrame;
            _frame.color = Color.white;

            _illustration = CardArtBuilder.CreateLayer("Illustration", rect);
            _illustration.gameObject.SetActive(false);

            _bottomScrim = CardArtBuilder.CreateBox("BottomScrim", rect, CardArt.BottomScrim, scale);
            if (library != null && library.BottomScrim != null) _bottomScrim.sprite = library.BottomScrim;
            _bottomScrim.color = Color.white;

            // 선택 강조 — CharacterCardView.SetHighlighted와 같은 색/방식(HighlightColor 참고).
            _highlight = CardArtBuilder.CreateLayer("Highlight", rect);
            _highlight.color = HighlightColor;
            _highlight.gameObject.SetActive(false);

            var font = library != null ? library.Font : null;

            _hpValueText = CardArtBuilder.CreateText("HpValue", rect, CardArt.Hp, scale, CardArt.StatFontSize,
                CardArt.StatColor, font, TextAlignmentOptions.Center);
            _reachValueText = CardArtBuilder.CreateText("ReachValue", rect, CardArt.Reach, scale, CardArt.StatFontSize,
                CardArt.StatColor, font, TextAlignmentOptions.Center);
            _weaponTagText = CardArtBuilder.CreateText("WeaponTag", rect, CardArt.WeaponTag, scale, CardArt.TagFontSize,
                CardArt.TagColor, font, TextAlignmentOptions.Center);
            _raceTagText = CardArtBuilder.CreateText("RaceTag", rect, CardArt.RaceTag, scale, CardArt.TagFontSize,
                CardArt.TagColor, font, TextAlignmentOptions.Center);
            _nameText = CardArtBuilder.CreateText("NameText", rect, CardArt.Name, scale, CardArt.NameFontSize,
                CardArt.NameColor, font, TextAlignmentOptions.Center);
            _nameText.enableAutoSizing = true;
            _nameText.fontSizeMin = CardArt.NameFontSize * scale * 0.5f;
            _nameText.fontSizeMax = CardArt.NameFontSize * scale;

            // 클릭 히트테스트 전용 — 카드 전체를 덮는 투명 레이어(Frame/Illustration은 raycastTarget=false).
            // [왜] CharacterCardView에 모듈 C가 얹은 "클릭용 투명 Image+Button"과 같은 패턴.
            _clickCatcher = CardArtBuilder.CreateLayer("ClickCatcher", rect, raycastTarget: true);
            _clickCatcher.color = Color.clear;

            BuildBanOverlay(rect);
            BuildPickMark(rect, scale);
        }

        /// <summary>
        /// 밴 오버레이 — 카드 전체를 덮는 반투명 막 + 중앙 "BAN". [주의] 기본은 꺼둔다.
        /// [왜 레거시 Text인가] "BAN"은 카드 내용이 아니라 게임 UI 상태 표시라 GameUIFont를 쓴다
        /// (카드용 KOHINanum과 분리 지시).
        /// </summary>
        private void BuildBanOverlay(RectTransform rect)
        {
            var overlay = CardArtBuilder.CreateLayer("BanOverlay", rect, raycastTarget: false);
            overlay.color = new Color(0f, 0f, 0f, 0.72f);
            _banOverlay = overlay.gameObject;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(overlay.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            _banLabelText = labelGo.AddComponent<Text>();
            _banLabelText.font = GameUIFont.Legacy;
            _banLabelText.text = "BAN";
            _banLabelText.fontSize = 20;
            _banLabelText.fontStyle = FontStyle.Bold;
            _banLabelText.color = new Color32(0xE0, 0x51, 0x2F, 0xFF);
            _banLabelText.alignment = TextAnchor.MiddleCenter;
            _banLabelText.raycastTarget = false;

            _banOverlay.SetActive(false);
        }

        /// <summary>
        /// PickMark("✓ PICK") — 육각(체력/리치) 아래, 카드 폭의 70%인 가로 막대. [검산] 클래스 상단 상수 주석.
        /// [왜 레거시 Text인가] "✓ PICK"은 카드 내용이 아니라 게임 UI 상태 표시라 GameUIFont를 쓴다.
        /// 원래 카드 폰트(KOHINanum, 이름·효과 전용 장식 폰트)를 그대로 물려써서 "✓" 글자가 없어
        /// 빈 네모로 나왔다 — 게임 UI 텍스트를 카드 폰트와 분리하며 같이 고친다.
        /// </summary>
        private void BuildPickMark(RectTransform rect, float scale)
        {
            float width = rect.rect.width;
            float markW = width * PickMarkWidthRatio;
            float top = HexBottomSource * scale + PickMarkTopMargin;

            var panel = new GameObject("PickMark", typeof(RectTransform), typeof(Image));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.SetParent(rect, false);
            panelRt.anchorMin = new Vector2(0, 1);
            panelRt.anchorMax = new Vector2(0, 1);
            panelRt.pivot = new Vector2(0, 1);
            panelRt.anchoredPosition = new Vector2((width - markW) / 2f, -top);
            panelRt.sizeDelta = new Vector2(markW, PickMarkHeight);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color32(0x31, 0xD0, 0xF0, 0xE6);
            panelImage.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(panelRt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<Text>();
            label.font = GameUIFont.Legacy;
            // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: "✓ PICK")
            // [주의] ✓(U+2713)는 현재 폰트에서도 정상 렌더된다(픽셀 측정 확인). 지시에 따라 뺐을 뿐
            //        글리프 문제는 아니므로, 되돌려도 지금 폰트에서 바로 보인다.
            label.text = "PICK";
            label.fontSize = 12;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.black;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            _pickMark = panel;
            _pickMark.SetActive(false);
        }

        /// <summary>이미 만들어진 자식이 있으면 참조만 연결한다. 하나라도 없으면 false(새로 만들어야 함).</summary>
        private bool TryWireExistingChildren()
        {
            _frame = transform.Find("Frame")?.GetComponent<Image>();
            _illustration = transform.Find("Illustration")?.GetComponent<Image>();
            _bottomScrim = transform.Find("BottomScrim")?.GetComponent<Image>();
            _highlight = transform.Find("Highlight")?.GetComponent<Image>();
            _hpValueText = transform.Find("HpValue")?.GetComponent<TextMeshProUGUI>();
            _reachValueText = transform.Find("ReachValue")?.GetComponent<TextMeshProUGUI>();
            _weaponTagText = transform.Find("WeaponTag")?.GetComponent<TextMeshProUGUI>();
            _raceTagText = transform.Find("RaceTag")?.GetComponent<TextMeshProUGUI>();
            _nameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            _clickCatcher = transform.Find("ClickCatcher")?.GetComponent<Image>();
            _banOverlay = transform.Find("BanOverlay")?.gameObject;
            _banLabelText = transform.Find("BanOverlay/Label")?.GetComponent<Text>();
            _pickMark = transform.Find("PickMark")?.gameObject;

            return _frame != null && _illustration != null && _bottomScrim != null && _highlight != null
                   && _hpValueText != null && _reachValueText != null && _weaponTagText != null
                   && _raceTagText != null && _nameText != null && _clickCatcher != null
                   && _banOverlay != null && _pickMark != null;
        }

        /// <summary>
        /// [무엇] 상태 라벨("상대 밴"/"상대 픽") 오브젝트를 없으면 만들고 참조를 잡는다.
        /// [왜] 씬을 다시 들어올 때마다 자식을 다시 찾을 수 있게, 그리고 EnsureBuilt가 만든 자식들
        ///      뒤에 마지막으로 붙여야(BanOverlay 위에 그려지도록) 별도 시점에 호출한다.
        /// </summary>
        private void EnsureStatusLabel()
        {
            var existing = transform.Find(StatusLabelName);
            if (existing != null)
            {
                _statusLabelBox = existing.GetComponent<Image>();
                _statusLabelText = existing.Find("Text")?.GetComponent<Text>();
                return;
            }

            var rect = (RectTransform)transform;
            float width = rect.rect.width;
            float height = rect.rect.height;

            var box = new GameObject(StatusLabelName, typeof(RectTransform), typeof(Image));
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.SetParent(rect, false);
            boxRect.anchorMin = new Vector2(0, 1);
            boxRect.anchorMax = new Vector2(0, 1);
            boxRect.pivot = new Vector2(0, 1);
            boxRect.anchoredPosition = new Vector2(StatusLabelSideMargin, -StatusLabelTop(width, height));
            boxRect.sizeDelta = new Vector2(width - StatusLabelSideMargin * 2f, StatusLabelHeight);
            boxRect.SetAsLastSibling();

            _statusLabelBox = box.GetComponent<Image>();
            _statusLabelBox.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(boxRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _statusLabelText = textGo.AddComponent<Text>();
            _statusLabelText.font = GameUIFont.Legacy;
            _statusLabelText.fontSize = StatusLabelFontSize;
            _statusLabelText.alignment = TextAnchor.MiddleCenter;
            _statusLabelText.raycastTarget = false;

            box.SetActive(false);
        }

        /// <summary>
        /// [무엇] 상태 라벨의 세로 위치 — 태그(직업/종족) 하단에서 여백만큼 띄운 지점.
        /// [왜] 작업 1로 이름(CardArt.Name, y910~1000)이 태그(CardArt.WeaponTag/RaceTag, y1013~1067)보다
        ///      위로 올라오면서 "태그 아래 ~ 카드 하단"이 밴픽 카드에서 통째로 빈 구간이 됐다
        ///      (조건/효과/하단표기는 밴픽 카드에 없음). 그 구간 맨 위, 태그와 여백만큼 띄워 넣는다.
        /// [검산] 밴카드(118×182.9)/픽카드(150×229.2), scale=폭/1000:
        ///   태그 하단(원본 y1067) → 스케일 후 125.9 / 160.05
        ///   +여백(6) → 라벨 top = 131.9 / 166.05, 라벨 하단 = 149.9 / 184.05
        ///   카드 하단(180.3 / 229.2)까지 각각 30.4 / 45.15 남아 안 넘친다.
        ///   이름 하단(원본 y1000 → 118.0 / 150.0)보다 아래라 이름과도 안 겹친다.
        /// </summary>
        private static float StatusLabelTop(float width, float height)
        {
            float scale = CardArt.Scale(width);
            float tagsBottom = (CardArt.WeaponTag.Top + CardArt.WeaponTag.Height) * scale;
            return tagsBottom + StatusLabelMarginBelowTags;
        }

        /// <summary>
        /// [무엇] 카드에 데이터를 채우고 표시 상태를 초기화한다.
        /// [왜] 씬을 다시 들어올 때마다 이전 상태(선택/밴/픽/라벨)가 남지 않게 하려고 매번 초기화한다.
        /// [주의] index는 풀 안에서의 위치다. 카드 id가 아니다.
        /// </summary>
        public void Bind(CharacterData data, int index, bool clickable)
        {
            EnsureBuilt();
            Index = index;
            _boundData = data;

            var library = CardArtLibrary.Instance;
            var illustration = library != null ? library.GetIllustration(data.Id) : null;
            _illustration.gameObject.SetActive(illustration != null);
            if (illustration != null) _illustration.sprite = illustration;

            if (_nameText != null) _nameText.text = data.Name;
            if (_hpValueText != null) _hpValueText.text = data.MaxHp.ToString();
            if (_reachValueText != null) _reachValueText.text = data.Reach.ToString();
            if (_weaponTagText != null) _weaponTagText.text = data.WeaponType;
            if (_raceTagText != null) _raceTagText.text = data.Race;

            IsSelected = false;
            IsBanned = false;
            IsPicked = false;
            if (_banOverlay != null) _banOverlay.SetActive(false);
            if (_pickMark != null) _pickMark.SetActive(false);
            ClearStatusLabel();
            SetSelected(false);

            _button.interactable = clickable;
        }

        /// <summary>
        /// [무엇] 밴된 카드 표시 (카드 전체를 덮는 반투명막 + "BAN").
        /// [왜] 밴은 "이 카드는 이번 판에서 사라졌다"는 뜻이라 카드 전체를 죽여 보여준다.
        /// [주의] 밴되면 클릭이 영구히 막힌다.
        /// </summary>
        public void SetBanned(bool banned)
        {
            IsBanned = banned;
            if (_banOverlay != null) _banOverlay.SetActive(banned);
            if (banned) _button.interactable = false;
        }

        /// <summary>[무엇] 내가 픽한 카드 표시 ("✓ PICK"). [주의] 픽하면 클릭이 막힌다.</summary>
        public void SetPicked(bool picked)
        {
            IsPicked = picked;
            if (_pickMark != null) _pickMark.SetActive(picked);
            if (picked) _button.interactable = false;
        }

        /// <summary>
        /// [무엇] 카드에 상태 라벨을 띄운다 ("상대 밴" / "상대 픽").
        /// [왜] 밴픽 전체 공개(UNITY_PORTING_SPEC 4-1)에서 상대가 무엇을 했는지 카드에 직접 보여야 한다.
        /// [주의] 배경색과 글자색을 <b>호출자가 각각 준다</b>.
        /// [주의] blocksClick=true면 클릭을 막는다 — 상대가 가져간 카드를 다시 고를 수 없어야 하기 때문.
        /// </summary>
        public void SetStatusLabel(string text, Color backgroundColor, Color textColor, bool blocksClick)
        {
            EnsureStatusLabel();
            if (_statusLabelBox == null) return;

            _statusLabelBox.gameObject.SetActive(true);
            _statusLabelBox.color = backgroundColor;
            if (_statusLabelText != null)
            {
                _statusLabelText.text = text;
                _statusLabelText.color = textColor;
            }

            if (blocksClick) _button.interactable = false;
        }

        /// <summary>[무엇] 상태 라벨을 숨긴다. [왜] Bind에서 매번 초기화해야 이전 판 표시가 남지 않는다.</summary>
        public void ClearStatusLabel()
        {
            if (_statusLabelBox != null) _statusLabelBox.gameObject.SetActive(false);
        }

        /// <summary>[무엇] 확정 전 임시 선택 표시. [주의] 확정(Submit)과는 무관한 화면 상태일 뿐이다.</summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_highlight != null) _highlight.gameObject.SetActive(selected);
        }

        /// <summary>강조 테두리 색 — CharacterCardView.HighlightColor와 동일(카드 종류 무관 같은 강조 언어).</summary>
        private static readonly Color HighlightColor = new Color32(0x31, 0xD0, 0xF0, 0x66);
    }
}