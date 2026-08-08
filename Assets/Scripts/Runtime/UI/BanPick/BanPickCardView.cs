// 담당 모듈: A (밴픽 공개화 + PlaceScene) — UNITY_PORTING_SPEC.md 5절
using System;
using CrossAccel.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 밴픽 화면의 카드 한 장. 
    /// 지정된 카드 프리팹 계층 구조에 맞춰 데이터 바인딩 및 오버레이/클릭을 처리한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class BanPickCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // ── 상태 라벨(상대 픽/상대 밴) 배치 상수 ──────────────────────────────────
        private const float StatusLabelMarginBelowTags = 6f;
        private const float StatusLabelHeight = 18f;
        private const float StatusLabelSideMargin = 4f;
        private const int StatusLabelFontSize = 10;

        // ── PickMark("✓ PICK") 배치 상수 ──────────────────────────────────────
        private const float HexBottomSource = 275f;
        private const float PickMarkTopMargin = 6f;
        private const float PickMarkWidthRatio = 0.7f;
        private const float PickMarkHeight = 20f;

        private const string StatusLabelName = "StatusLabel";

        // =========================================================================
        // [프리팹 구조 대응 직렬화 필드]
        // =========================================================================
        [Header("Prefab Visual Layer References")]
        [SerializeField] private Image _frame;               // Frame_1 혹은 Background
        [SerializeField] private Image _illustration;        // Canvas/Image
        [SerializeField] private Image _bottomScrim;         // 필요 시 사용
        [SerializeField] private Image _highlight;           // Fade 또는 선택 강조용
        [SerializeField] private Image _clickCatcher;         // 클릭 감지 레이어

        [Header("Prefab Text Layer References")]
        [SerializeField] private TextMeshProUGUI _nameText;      // Canvas/CharacterName
        [SerializeField] private TextMeshProUGUI _hpValueText;   // Canvas/Hp_Text
        [SerializeField] private TextMeshProUGUI _reachValueText;// Canvas/Reach_Text
        [SerializeField] private TextMeshProUGUI _weaponTagText; // Canvas/Word_1 (또는 Weapon)
        [SerializeField] private TextMeshProUGUI _raceTagText;   // Canvas/Word_2 (또는 Race)

        [Header("Overlay References")]
        [SerializeField] private GameObject _banOverlay;
        [SerializeField] private Text _banLabelText;
        [SerializeField] private GameObject _pickMark;
        [SerializeField] private Image _statusLabelBox;
        [SerializeField] private Text _statusLabelText;

        private Button _button;
        private bool _built;
        private CharacterData _boundData;

        public int Index { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsBanned { get; private set; }
        public bool IsPicked { get; private set; }

        public event Action<int> Clicked;

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

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => Clicked?.Invoke(Index));
            }
            EnsureStatusLabel();
        }

#if UNITY_EDITOR
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

            // 1. 프리팹 계층 구조 탐색 및 와이어링 시도
            if (TryWireExistingChildren()) return;

            // 2. 만약 자식 탐색에 완전히 실패했을 경우 기존 동적 생성 백업 로직
            var rect = (RectTransform)transform;
            float scale = CardArt.Scale(rect.rect.width);
            var library = CardArtLibrary.Instance;

            if (_frame == null)
            {
                _frame = CardArtBuilder.CreateLayer("Frame", rect);
                if (library != null && library.CharacterFrame != null) _frame.sprite = library.CharacterFrame;
            }

            if (_illustration == null)
            {
                _illustration = CardArtBuilder.CreateLayer("Illustration", rect);
                _illustration.gameObject.SetActive(false);
            }

            var font = library != null ? library.Font : null;

            if (_hpValueText == null)
                _hpValueText = CardArtBuilder.CreateText("HpValue", rect, CardArt.Hp, scale, CardArt.StatFontSize, CardArt.StatColor, font, TextAlignmentOptions.Center);

            if (_reachValueText == null)
                _reachValueText = CardArtBuilder.CreateText("ReachValue", rect, CardArt.Reach, scale, CardArt.StatFontSize, CardArt.StatColor, font, TextAlignmentOptions.Center);

            if (_weaponTagText == null)
                _weaponTagText = CardArtBuilder.CreateText("WeaponTag", rect, CardArt.WeaponTag, scale, CardArt.TagFontSize, CardArt.TagColor, font, TextAlignmentOptions.Center);

            if (_raceTagText == null)
                _raceTagText = CardArtBuilder.CreateText("RaceTag", rect, CardArt.RaceTag, scale, CardArt.TagFontSize, CardArt.TagColor, font, TextAlignmentOptions.Center);

            if (_nameText == null)
                _nameText = CardArtBuilder.CreateText("NameText", rect, CardArt.Name, scale, CardArt.NameFontSize, CardArt.NameColor, font, TextAlignmentOptions.Center);

            if (_banOverlay == null) BuildBanOverlay(rect);
            if (_pickMark == null) BuildPickMark(rect, scale);
        }

        /// <summary>
        /// 프리팹 구조(Canvas 내부 자식 오브젝트 등)를 반영하여 자동 바인딩합니다.
        /// </summary>
        private bool TryWireExistingChildren()
        {
            // 프레임 / 하이라이트
            _frame = _frame != null ? _frame : transform.Find("Frame_1")?.GetComponent<Image>();
            if (_frame == null) _frame = GetComponent<Image>();

            _highlight = _highlight != null ? _highlight : transform.Find("Fade")?.GetComponent<Image>();

            // 일러스트 (Canvas/Image)
            _illustration = _illustration != null ? _illustration : transform.Find("Canvas/Image")?.GetComponent<Image>();

            // 텍스트 요소 (Canvas 내부 탐색)
            _hpValueText = _hpValueText != null ? _hpValueText : transform.Find("Canvas/Hp_Text")?.GetComponent<TextMeshProUGUI>();
            _reachValueText = _reachValueText != null ? _reachValueText : transform.Find("Canvas/Reach_Text")?.GetComponent<TextMeshProUGUI>();
            _nameText = _nameText != null ? _nameText : transform.Find("Canvas/CharacterName")?.GetComponent<TextMeshProUGUI>();

            // 태그 텍스트 (Word_1/Word_2 또는 Weapon/Race 대응)
            _weaponTagText = _weaponTagText != null ? _weaponTagText : transform.Find("Canvas/Word_1")?.GetComponent<TextMeshProUGUI>();
            if (_weaponTagText == null) _weaponTagText = transform.Find("Canvas/Weapon")?.GetComponent<TextMeshProUGUI>();

            _raceTagText = _raceTagText != null ? _raceTagText : transform.Find("Canvas/Word_2")?.GetComponent<TextMeshProUGUI>();
            if (_raceTagText == null) _raceTagText = transform.Find("Canvas/Race")?.GetComponent<TextMeshProUGUI>();

            // 오버레이 및 기타 요소
            _banOverlay = _banOverlay != null ? _banOverlay : transform.Find("BanOverlay")?.gameObject;
            _banLabelText = _banLabelText != null ? _banLabelText : transform.Find("BanOverlay/Label")?.GetComponent<Text>();
            _pickMark = _pickMark != null ? _pickMark : transform.Find("PickMark")?.gameObject;

            return _nameText != null && _hpValueText != null && _reachValueText != null && _illustration != null;
        }

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
            label.text = "PICK";
            label.fontSize = 12;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.black;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            _pickMark = panel;
            _pickMark.SetActive(false);
        }

        private void EnsureStatusLabel()
        {
            if (_statusLabelBox != null)
            {
                if (_statusLabelText == null)
                    _statusLabelText = _statusLabelBox.transform.Find("Text")?.GetComponent<Text>();
                return;
            }

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

        private static float StatusLabelTop(float width, float height)
        {
            float scale = CardArt.Scale(width);
            float tagsBottom = (CardArt.WeaponTag.Top + CardArt.WeaponTag.Height) * scale;
            return tagsBottom + StatusLabelMarginBelowTags;
        }

        public void Bind(CharacterData data, int index, bool clickable)
        {
            EnsureBuilt();

            Index = index;
            _boundData = data;

            var library = CardArtLibrary.Instance;
            var illustration = library != null ? library.GetIllustration(data.Id) : null;

            if (_illustration != null)
            {
                _illustration.gameObject.SetActive(illustration != null);
                if (illustration != null) _illustration.sprite = illustration;
            }

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

            if (_button != null) _button.interactable = clickable;
        }

        public void SetBanned(bool banned)
        {
            IsBanned = banned;
            if (_banOverlay != null) _banOverlay.SetActive(banned);
            if (banned && _button != null) _button.interactable = false;
        }

        public void SetPicked(bool picked)
        {
            IsPicked = picked;
            if (_pickMark != null) _pickMark.SetActive(picked);
            if (picked && _button != null) _button.interactable = false;
        }

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

            if (blocksClick && _button != null) _button.interactable = false;
        }

        public void ClearStatusLabel()
        {
            if (_statusLabelBox != null) _statusLabelBox.gameObject.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (_highlight != null) _highlight.gameObject.SetActive(selected);
        }

        private static readonly Color HighlightColor = new Color32(0x31, 0xD0, 0xF0, 0x66);
    }
}