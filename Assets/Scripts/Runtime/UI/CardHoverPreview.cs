using CrossAccel.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 카드에 마우스를 올리면 화면 좌측에 뜨는 확대 프리뷰(큰 카드 + 효과 전문). 밴픽·배틀 공용.
    /// [왜] 밴/픽/배틀 카드는 작아서 효과 전문이 안 들어간다(작업 3에서 밴픽 카드가 조건/효과를 아예
    ///      뺀 이유이기도 하다). full_prototype.html의 좌측 프리뷰 패널(#pvp, function preview(c))이
    ///      이 역할을 한다 — 좌표·구성은 그 CSS를 그대로 옮겼다.
    /// [주의] 위치·크기는 전부 고정 px(300×460, 화면 좌측)다. CardArt처럼 카드 폭에 비례해 배율을
    ///        먹이지 않는다 — 프리뷰 패널 자체가 원본에서도 고정 크기이기 때문(스케일 대상이 아님).
    /// </summary>
    public class CardHoverPreview : MonoBehaviour
    {
        // ── 패널 좌표 (출처: full_prototype.html .pv-panel, 그대로) ──────────────────
        /// <summary>화면 좌측 고정 위치. 다른 UI와 안 겹치는지는 완료 보고서 좌표 검산표 참고.</summary>
        public const float PanelLeft = 26f;
        public const float PanelTop = 420f;
        public const float PanelWidth = 300f;
        public const float PanelHeight = 460f;

        // ── 내부 요소 좌표 (출처: full_prototype.html .pv-panel .pa/.ph/.pn/.pw/.pb) ──
        private const float ArtInset = 14f;                 // .pa: top/left/right 14
        private const float ArtHeightRatio = 0.46f;          // .pa: height 46%

        private const float HexTop = 20f;                    // .ph: top 20
        private const float HexWidth = 64f;                  // .ph: width 64
        private const float HexHeight = 74f;                 // .ph: height 74
        private const float HexSideMargin = 16f;              // .ph.h left16 / .ph.r right16

        private const float NameTopRatio = 0.50f;             // .pn: top 50%
        private const float NameHeight = 30f;                 // SPEC에 없음(퍼센트 기준점일 뿐) — 한 줄 높이로 임의 지정

        private const float TagsTopRatio = 0.57f;             // .pw: top 57%
        private const float TagsHeight = 24f;                 // SPEC에 없음 — 한 줄 높이로 임의 지정

        private const float EffectSideInset = 14f;            // .pb: left/right 14
        private const float EffectBottomInset = 14f;          // .pb: bottom 14
        // .pb의 top은 SPEC에 없다(bottom+min-height로만 정의된 가변 박스) — 태그 줄 아래로 여백을 두고
        // 계산한다: 태그 top(57%=262.2) + 태그 한 줄 높이(24) + 여백(10) ≈ 296.
        private const float EffectTopMargin = 10f;

        /// <summary>패널 발광 테두리 두께. [출처] .pv-panel box-shadow "0 0 0 3px var(--accel)".</summary>
        private const float PanelBorderThickness = 3f;

        private static readonly Color AccelBorderColor = new Color32(0x31, 0xD0, 0xF0, 0xFF); // --accel
        private static readonly Color PanelColor = new Color32(0x16, 0x1D, 0x30, 0xFF);       // --card-mid 근사(그라데이션 단색화)
        private static readonly Color HpColor = new Color32(0xE0, 0x51, 0x2F, 0xFF);           // --hp
        private static readonly Color ReachColor = new Color32(0x3A, 0x90, 0xC8, 0xFF);        // --reach
        private static readonly Color NameColor = Color.white;
        private static readonly Color TagsColor = new Color32(0xD9, 0xD0, 0xF8, 0xFF);         // .pw span color
        private static readonly Color EffectBg = new Color(8 / 255f, 12 / 255f, 22 / 255f, 0.8f); // .pb background
        private static readonly Color EffectColor = new Color32(0xDF, 0xE7, 0xF2, 0xFF);       // .pb color

        private Image _illustration;
        private TextMeshProUGUI _hpValueText;
        private TextMeshProUGUI _reachValueText;
        private TextMeshProUGUI _hpLabelText;
        private TextMeshProUGUI _reachLabelText;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _tagsText;
        private TextMeshProUGUI _effectText;

        private bool _built;

        private void Awake() => EnsureBuilt();

#if UNITY_EDITOR
        /// <summary>에디터 도구가 씬에 미리 구울 때 쓴다.</summary>
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

            if (TryWireExistingChildren())
            {
                gameObject.SetActive(false);
                return;
            }

            var rect = (RectTransform)transform;
            var library = CardArtLibrary.Instance;
            var font = library != null ? library.Font : null;

            // 배경 — 루트에 Image가 없으면 스스로 붙인다(빌더가 CreateRect만 해도 동작하도록).
            var background = GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            // 발광 테두리 (모듈 D 3단계 마감) — [출처] .pv-panel box-shadow "0 0 0 3px var(--accel)".
            // [왜] 모듈 B는 패널 자리만 잡고 테두리를 생략했다. 테두리가 없으면 어두운 배경 위에서
            //      패널 경계가 흐려 "떠 있는 카드"로 안 보인다.
            var outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = AccelBorderColor;
            outline.effectDistance = new Vector2(PanelBorderThickness, PanelBorderThickness);

            float artHeight = PanelHeight * ArtHeightRatio;
            _illustration = CreateBox("Illustration", rect, ArtInset, ArtInset, PanelWidth - ArtInset * 2f, artHeight);
            _illustration.gameObject.SetActive(false);

            _hpValueText = CreateHex("HpHex", rect, HexSideMargin, HpColor, "체력", font, out _hpLabelText);
            _reachValueText = CreateHex("ReachHex", rect, PanelWidth - HexSideMargin - HexWidth, ReachColor, "리치", font, out _reachLabelText);

            float nameTop = PanelHeight * NameTopRatio;
            _nameText = CreateLabel("NameText", rect, 0, nameTop, PanelWidth, NameHeight, 21, NameColor, font, FontStyles.Bold);

            float tagsTop = PanelHeight * TagsTopRatio;
            _tagsText = CreateLabel("TagsText", rect, 0, tagsTop, PanelWidth, TagsHeight, 12, TagsColor, font, FontStyles.Normal);

            float effectTop = tagsTop + TagsHeight + EffectTopMargin;
            float effectBottom = PanelHeight - EffectBottomInset;
            var effectBoxImage = CreateBox("EffectBox", rect, EffectSideInset, effectTop,
                PanelWidth - EffectSideInset * 2f, effectBottom - effectTop);
            effectBoxImage.color = EffectBg;

            _effectText = CreateLabel("Value", (RectTransform)effectBoxImage.transform, 10, 10,
                PanelWidth - EffectSideInset * 2f - 20, effectBottom - effectTop - 20, 13, EffectColor, font, FontStyles.Normal);
            _effectText.alignment = TextAlignmentOptions.TopLeft;
            _effectText.enableAutoSizing = true;
            _effectText.fontSizeMin = 9f;
            _effectText.fontSizeMax = 13f;

            gameObject.SetActive(false);
        }

        /// <summary>육각 배지(체력/리치) — 값 텍스트 위에 라벨을 작게 둔다. 프레임 아트가 없어 색 패널로 근사.</summary>
        private TextMeshProUGUI CreateHex(string name, RectTransform parent, float left, Color color, string label,
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

        private bool TryWireExistingChildren()
        {
            _illustration = transform.Find("Illustration")?.GetComponent<Image>();
            _hpValueText = transform.Find("HpHex/Value")?.GetComponent<TextMeshProUGUI>();
            _hpLabelText = transform.Find("HpHex/Label")?.GetComponent<TextMeshProUGUI>();
            _reachValueText = transform.Find("ReachHex/Value")?.GetComponent<TextMeshProUGUI>();
            _reachLabelText = transform.Find("ReachHex/Label")?.GetComponent<TextMeshProUGUI>();
            _nameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            _tagsText = transform.Find("TagsText")?.GetComponent<TextMeshProUGUI>();
            _effectText = transform.Find("EffectBox/Value")?.GetComponent<TextMeshProUGUI>();

            return _illustration != null && _hpValueText != null && _reachValueText != null
                   && _nameText != null && _tagsText != null && _effectText != null;
        }

        /// <summary>
        /// [무엇] 카드 위에 마우스를 올렸을 때 호출 — 패널을 채우고 보여준다.
        /// [왜] 밴픽 카드(작업 3에서 조건/효과를 뺐다)와 배틀 카드 둘 다 이 패널로 전문을 보여준다.
        /// </summary>
        public void Show(CharacterData data)
        {
            if (data == null) { Hide(); return; }
            EnsureBuilt();
            gameObject.SetActive(true);

            var library = CardArtLibrary.Instance;
            var illustration = library != null ? library.GetIllustration(data.Id) : null;
            _illustration.gameObject.SetActive(illustration != null);
            if (illustration != null) _illustration.sprite = illustration;

            // 캐릭터 모드: 체력 / 리치
            if (_hpLabelText != null) _hpLabelText.text = "체력";
            if (_reachLabelText != null) _reachLabelText.text = "리치";
            _hpValueText.text = data.MaxHp.ToString();
            _reachValueText.text = data.Reach.ToString();
            _nameText.text = data.Name;
            _tagsText.text = $"{data.Race} · {data.WeaponType}";
            _effectText.text = data.HasEffect ? data.EffectText : "";
        }

        /// <summary>
        /// [무엇] 손패 스킬 카드 호버 시 호출 — 같은 좌측 패널에 코스트·속도·효과 전문을 띄운다.
        /// [왜] 손패 카드(132×203)는 효과 전문이 잘린다. 캐릭터 프리뷰와 같은 패널을 재사용해
        ///      위치·크기를 하나로 유지한다(full_prototype.html .pv-panel 공용).
        /// [주의] 스킬은 HP/리치가 없다. 왼쪽 육각=코스트, 오른쪽 육각=속도로 라벨만 바꿔 쓴다.
        ///        일러스트 라이브러리에 스킬 아트가 없으므로 Illustration은 끈다.
        ///        CostCircle/SpeedCircle은 손패 카드용이라 프리뷰에 없다 — HpHex/ReachHex를 재사용한다.
        /// </summary>
        public void Show(SkillData skill)
        {
            if (skill == null) { Hide(); return; }
            EnsureBuilt();
            gameObject.SetActive(true);

            _illustration.gameObject.SetActive(false);

            // 스킬 모드: 기존 육각(체력/리치)을 코스트/속도로 라벨만 바꿔 재사용
            if (_hpLabelText != null) _hpLabelText.text = "코스트";
            if (_reachLabelText != null) _reachLabelText.text = "속도";
            _hpValueText.text = skill.Skill1Cost < 0 ? "X" : skill.Skill1Cost.ToString();
            _reachValueText.text = skill.Speed.ToString();
            _nameText.text = skill.Name;
            _tagsText.text = string.IsNullOrEmpty(skill.WeaponType) ? "" : skill.WeaponType;

            string effect = skill.Skill1Effect ?? "";
            if (skill.HasSkill2 && !string.IsNullOrEmpty(skill.Skill2Effect))
            {
                string cost2 = skill.Skill2Cost.HasValue && skill.Skill2Cost.Value < 0
                    ? "X"
                    : (skill.Skill2Cost?.ToString() ?? "");
                effect += $"\n\n[스킬2 · 코스트 {cost2}]\n{skill.Skill2Effect}";
            }
            _effectText.text = effect;
        }

        /// <summary>[무엇] 마우스가 카드를 벗어나면 숨긴다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}