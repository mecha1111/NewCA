using CrossAccel.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 카드 호버 시 화면 좌측 확대 프리뷰. 밴픽·배틀 공용.
    /// [왜] 작은 카드에는 효과 전문이 안 들어간다. full_prototype.html .pv-panel 역할.
    /// [주의] 위치·크기는 고정 px(300×460).
    /// [주의] 캐릭터는 Assets/Art/Cards/{id}.png 풀카드 이미지가 있으면 그 한 장만 보여 준다.
    ///        스킬은 조립 틀(코스트/속도/효과)을 쓴다.
    /// </summary>
    public class CardHoverPreview : MonoBehaviour
    {
        public const float PanelLeft = 26f;
        public const float PanelTop = 420f;
        public const float PanelWidth = 300f;
        public const float PanelHeight = 460f;

        private const float ArtInset = 14f;
        private const float ArtHeightRatio = 0.46f;

        private const float HexTop = 20f;
        private const float HexWidth = 64f;
        private const float HexHeight = 74f;
        private const float HexSideMargin = 16f;

        private const float NameTopRatio = 0.50f;
        private const float NameHeight = 30f;
        private const float TagsTopRatio = 0.57f;
        private const float TagsHeight = 24f;

        private const float EffectSideInset = 14f;
        private const float EffectBottomInset = 14f;
        private const float EffectTopMargin = 10f;

        private const float PanelBorderThickness = 3f;
        private const float FullCardInset = 8f;

        private static readonly Color AccelBorderColor = new Color32(0x31, 0xD0, 0xF0, 0xFF);
        private static readonly Color PanelColor = new Color32(0x16, 0x1D, 0x30, 0xFF);
        private static readonly Color HpColor = new Color32(0xE0, 0x51, 0x2F, 0xFF);
        private static readonly Color ReachColor = new Color32(0x3A, 0x90, 0xC8, 0xFF);
        private static readonly Color CostColor = new Color32(0xD4, 0x8A, 0x2A, 0xFF);
        private static readonly Color SpeedColor = new Color32(0x31, 0xD0, 0xF0, 0xFF);
        private static readonly Color NameColor = Color.white;
        private static readonly Color TagsColor = new Color32(0xD9, 0xD0, 0xF8, 0xFF);
        private static readonly Color EffectBg = new Color(8 / 255f, 12 / 255f, 22 / 255f, 0.8f);
        private static readonly Color EffectColor = new Color32(0xDF, 0xE7, 0xF2, 0xFF);

        // 풀카드 (캐릭터 프리뷰 우선)
        private Image _fullCardImage;

        // 캐릭터 조립 폴백
        private GameObject _characterRoot;
        private Image _illustration;
        private TextMeshProUGUI _hpValueText;
        private TextMeshProUGUI _reachValueText;
        private TextMeshProUGUI _charNameText;
        private TextMeshProUGUI _charTagsText;
        private TextMeshProUGUI _charEffectText;

        // 스킬 틀
        private GameObject _skillRoot;
        private TextMeshProUGUI _costValueText;
        private TextMeshProUGUI _speedValueText;
        private TextMeshProUGUI _skillNameText;
        private TextMeshProUGUI _skillTagsText;
        private TextMeshProUGUI _skillEffectText;

        private bool _built;

        private void Awake() => EnsureBuilt();

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

            EnsurePanelShell();

            if (_fullCardImage == null)
            {
                var existing = transform.Find("FullCard");
                if (existing != null) _fullCardImage = existing.GetComponent<Image>();
            }

            if (_fullCardImage == null)
            {
                _fullCardImage = CreateBox("FullCard", (RectTransform)transform,
                    FullCardInset, FullCardInset,
                    PanelWidth - FullCardInset * 2f, PanelHeight - FullCardInset * 2f);
                _fullCardImage.color = Color.white;
                _fullCardImage.preserveAspect = true;
            }
            _fullCardImage.gameObject.SetActive(false);

            if (!TryWireCharacterChildren())
                BuildCharacterRoot();

            if (!TryWireSkillChildren())
                BuildSkillRoot();

            if (_characterRoot != null) _characterRoot.SetActive(false);
            if (_skillRoot != null) _skillRoot.SetActive(false);
            gameObject.SetActive(false);
        }

        private void EnsurePanelShell()
        {
            var background = GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            var outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = AccelBorderColor;
            outline.effectDistance = new Vector2(PanelBorderThickness, PanelBorderThickness);
        }

        // ===================== 캐릭터 조립 폴백 =====================

        private void BuildCharacterRoot()
        {
            var rect = (RectTransform)transform;
            var library = CardArtLibrary.Instance;
            var font = library != null ? library.Font : null;

            _characterRoot = new GameObject("CharacterRoot", typeof(RectTransform));
            var rootRt = (RectTransform)_characterRoot.transform;
            rootRt.SetParent(rect, false);
            StretchFull(rootRt);

            float artHeight = PanelHeight * ArtHeightRatio;
            _illustration = CreateBox("Illustration", rootRt, ArtInset, ArtInset, PanelWidth - ArtInset * 2f, artHeight);
            _illustration.gameObject.SetActive(false);

            _hpValueText = CreateHex("HpHex", rootRt, HexSideMargin, HpColor, "체력", font, out _);
            _reachValueText = CreateHex("ReachHex", rootRt, PanelWidth - HexSideMargin - HexWidth, ReachColor, "리치", font, out _);

            float nameTop = PanelHeight * NameTopRatio;
            _charNameText = CreateLabel("NameText", rootRt, 0, nameTop, PanelWidth, NameHeight, 21, NameColor, font, FontStyles.Bold);

            float tagsTop = PanelHeight * TagsTopRatio;
            _charTagsText = CreateLabel("TagsText", rootRt, 0, tagsTop, PanelWidth, TagsHeight, 12, TagsColor, font, FontStyles.Normal);

            float effectTop = tagsTop + TagsHeight + EffectTopMargin;
            float effectBottom = PanelHeight - EffectBottomInset;
            var effectBox = CreateBox("EffectBox", rootRt, EffectSideInset, effectTop,
                PanelWidth - EffectSideInset * 2f, effectBottom - effectTop);
            effectBox.color = EffectBg;

            _charEffectText = CreateLabel("Value", (RectTransform)effectBox.transform, 10, 10,
                PanelWidth - EffectSideInset * 2f - 20, effectBottom - effectTop - 20, 13, EffectColor, font, FontStyles.Normal);
            _charEffectText.alignment = TextAlignmentOptions.TopLeft;
            _charEffectText.enableAutoSizing = true;
            _charEffectText.fontSizeMin = 9f;
            _charEffectText.fontSizeMax = 13f;
        }

        private bool TryWireCharacterChildren()
        {
            var root = transform.Find("CharacterRoot");
            if (root == null) return false;

            _characterRoot = root.gameObject;
            _illustration = root.Find("Illustration")?.GetComponent<Image>();
            _hpValueText = root.Find("HpHex/Value")?.GetComponent<TextMeshProUGUI>();
            _reachValueText = root.Find("ReachHex/Value")?.GetComponent<TextMeshProUGUI>();
            _charNameText = root.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            _charTagsText = root.Find("TagsText")?.GetComponent<TextMeshProUGUI>();
            _charEffectText = root.Find("EffectBox/Value")?.GetComponent<TextMeshProUGUI>();

            return _illustration != null && _hpValueText != null && _reachValueText != null
                   && _charNameText != null && _charTagsText != null && _charEffectText != null;
        }

        // ===================== 스킬 틀 =====================

        private void BuildSkillRoot()
        {
            var rect = (RectTransform)transform;
            var library = CardArtLibrary.Instance;
            var font = library != null ? library.Font : null;

            _skillRoot = new GameObject("SkillRoot", typeof(RectTransform));
            var rootRt = (RectTransform)_skillRoot.transform;
            rootRt.SetParent(rect, false);
            StretchFull(rootRt);

            _costValueText = CreateHex("CostHex", rootRt, HexSideMargin, CostColor, "코스트", font, out _);
            _speedValueText = CreateHex("SpeedHex", rootRt, PanelWidth - HexSideMargin - HexWidth, SpeedColor, "속도", font, out _);

            float nameTop = PanelHeight * NameTopRatio;
            _skillNameText = CreateLabel("NameText", rootRt, 0, nameTop, PanelWidth, NameHeight, 21, NameColor, font, FontStyles.Bold);

            float tagsTop = PanelHeight * TagsTopRatio;
            _skillTagsText = CreateLabel("TagsText", rootRt, 0, tagsTop, PanelWidth, TagsHeight, 12, TagsColor, font, FontStyles.Normal);

            float effectTop = tagsTop + TagsHeight + EffectTopMargin;
            float effectBottom = PanelHeight - EffectBottomInset;
            var effectBox = CreateBox("EffectBox", rootRt, EffectSideInset, effectTop,
                PanelWidth - EffectSideInset * 2f, effectBottom - effectTop);
            effectBox.color = EffectBg;

            _skillEffectText = CreateLabel("Value", (RectTransform)effectBox.transform, 10, 10,
                PanelWidth - EffectSideInset * 2f - 20, effectBottom - effectTop - 20, 13, EffectColor, font, FontStyles.Normal);
            _skillEffectText.alignment = TextAlignmentOptions.TopLeft;
            _skillEffectText.enableAutoSizing = true;
            _skillEffectText.fontSizeMin = 9f;
            _skillEffectText.fontSizeMax = 13f;
        }

        private bool TryWireSkillChildren()
        {
            var root = transform.Find("SkillRoot");
            if (root == null) return false;

            _skillRoot = root.gameObject;
            _costValueText = root.Find("CostHex/Value")?.GetComponent<TextMeshProUGUI>();
            _speedValueText = root.Find("SpeedHex/Value")?.GetComponent<TextMeshProUGUI>();
            _skillNameText = root.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            _skillTagsText = root.Find("TagsText")?.GetComponent<TextMeshProUGUI>();
            _skillEffectText = root.Find("EffectBox/Value")?.GetComponent<TextMeshProUGUI>();

            return _costValueText != null && _speedValueText != null
                   && _skillNameText != null && _skillTagsText != null && _skillEffectText != null;
        }

        // ===================== Show / Hide =====================

        /// <summary>
        /// [무엇] 캐릭터 호버 — 풀카드 이미지가 있으면 그 한 장만, 없으면 조립 UI.
        /// </summary>
        public void Show(CharacterData data)
        {
            if (data == null) { Hide(); return; }
            EnsureBuilt();
            gameObject.SetActive(true);

            if (_skillRoot != null) _skillRoot.SetActive(false);

            var library = CardArtLibrary.Instance;
            Sprite cardSprite = library != null ? library.GetCardImage(data.Id) : null;

            if (cardSprite != null)
            {
                if (_characterRoot != null) _characterRoot.SetActive(false);
                _fullCardImage.gameObject.SetActive(true);
                _fullCardImage.sprite = cardSprite;
                _fullCardImage.preserveAspect = true;
                return;
            }

            // 폴백: 조립 UI
            _fullCardImage.gameObject.SetActive(false);
            if (_characterRoot != null) _characterRoot.SetActive(true);

            var illustration = library != null ? library.GetIllustration(data.Id) : null;
            _illustration.gameObject.SetActive(illustration != null);
            if (illustration != null) _illustration.sprite = illustration;

            _hpValueText.text = data.MaxHp.ToString();
            _reachValueText.text = data.Reach.ToString();
            _charNameText.text = data.Name;
            _charTagsText.text = $"{data.Race} · {data.WeaponType}";
            _charEffectText.text = data.HasEffect ? data.EffectText : "";
        }

        /// <summary>
        /// [무엇] 스킬 호버 — 스킬 전용 틀(코스트/속도/효과).
        /// </summary>
        public void Show(SkillData skill)
        {
            if (skill == null) { Hide(); return; }
            EnsureBuilt();
            gameObject.SetActive(true);

            _fullCardImage.gameObject.SetActive(false);
            if (_characterRoot != null) _characterRoot.SetActive(false);
            if (_skillRoot != null) _skillRoot.SetActive(true);

            _costValueText.text = skill.Skill1Cost < 0 ? "X" : skill.Skill1Cost.ToString();
            _speedValueText.text = skill.Speed.ToString();
            _skillNameText.text = skill.Name;
            _skillTagsText.text = string.IsNullOrEmpty(skill.WeaponType) ? "" : skill.WeaponType;

            string effect = skill.Skill1Effect ?? "";
            if (skill.HasSkill2 && !string.IsNullOrEmpty(skill.Skill2Effect))
            {
                string cost2 = skill.Skill2Cost.HasValue && skill.Skill2Cost.Value < 0
                    ? "X"
                    : (skill.Skill2Cost?.ToString() ?? "");
                effect += $"\n\n[스킬2 · 코스트 {cost2}]\n{skill.Skill2Effect}";
            }
            _skillEffectText.text = effect;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ===================== 헬퍼 =====================

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static TextMeshProUGUI CreateHex(string name, RectTransform parent, float left, Color color, string label,
            TMP_FontAsset font, out TextMeshProUGUI labelText)
        {
            var box = CreateBox(name, parent, left, HexTop, HexWidth, HexHeight);
            box.color = color;

            var value = CreateLabel("Value", (RectTransform)box.transform, 0, 8, HexWidth, 32, 28, Color.white, font, FontStyles.Bold);
            labelText = CreateLabel("Label", (RectTransform)box.transform, 0, 44, HexWidth, 20, 10, Color.white, font, FontStyles.Normal);
            labelText.text = label;
            return value;
        }

        private static Image CreateBox(string name, RectTransform parent, float left, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateLabel(string name, RectTransform parent, float left, float top,
            float width, float height, float fontSize, Color color, TMP_FontAsset font, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }
}