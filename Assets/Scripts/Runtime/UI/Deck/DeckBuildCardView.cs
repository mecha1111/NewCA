using CrossAccel.Data;
using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;



namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 덱 빌드 화면의 카드 한 장 (전체 카드·내 카드 공용).
    /// [왜] 캐릭터는 프리뷰와 같은 풀카드 이미지, 스킬은 코스트/속도 조립 UI를 쓴다.
    /// [주의] 텍스트는 전부 TextMeshProUGUI. 루트에 Button 필요.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DeckBuildCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("캐릭터 — 풀카드 이미지 (Art/Cards/{id}.png)")]
        [SerializeField] private GameObject _characterRoot;
        [SerializeField] private Image _fullCardImage;

        [Header("스킬 — 조립 UI (TMP)")]
        [SerializeField] private GameObject _skillRoot;
        [SerializeField] private Text _skillNameText;
        [SerializeField] private Text _skillCostText;
        [SerializeField] private Text _skillSpeedText;
        [SerializeField] private Text _skillEffectText;
        [SerializeField] private Text _skillCountText;

        [Header("공용")]
        [SerializeField] private CardHoverPreview _hoverPreview;

        public CardHoverPreview HoverPreview
        {
            get => _hoverPreview;
            set => _hoverPreview = value;
        }

        public string CardId { get; private set; }
        public bool IsCharacter { get; private set; }
        public int Count { get; private set; } = 1;

        public event Action<DeckBuildCardView> Clicked;

        private Button _button;
        private CharacterData _character;
        private SkillData _skill;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        /// <summary>
        /// [무엇] 캐릭터 카드 표시 — CardArtLibrary 풀카드 이미지 한 장.
        /// </summary>
        public void BindCharacter(CharacterData data, int count = 1)
        {
            _character = data;
            _skill = null;
            IsCharacter = true;
            CardId = data != null ? data.Id : null;
            Count = count;

            if (_skillRoot != null) _skillRoot.SetActive(false);
            if (_characterRoot != null) _characterRoot.SetActive(data != null);

            if (data == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            Sprite sprite = null;
            var library = CardArtLibrary.Instance;
            if (library != null)
                sprite = library.GetCardImage(data.Id);

            if (_fullCardImage != null)
            {
                _fullCardImage.gameObject.SetActive(sprite != null);
                if (sprite != null)
                {
                    _fullCardImage.sprite = sprite;
                    _fullCardImage.preserveAspect = true;
                }
            }
        }

        /// <summary>
        /// [무엇] 스킬 카드 표시 — 이름/코스트/속도/효과 TMP UI.
        /// </summary>
        public void BindSkill(SkillData data, int count = 1)
        {
            _skill = data;
            _character = null;
            IsCharacter = false;
            CardId = data != null ? data.Id : null;
            Count = count;

            if (_characterRoot != null) _characterRoot.SetActive(false);
            if (_skillRoot != null) _skillRoot.SetActive(data != null);

            if (data == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (_skillNameText != null)
                _skillNameText.text = data.Name ?? "";

            if (_skillCostText != null)
                _skillCostText.text = data.Skill1Cost < 0 ? "X" : data.Skill1Cost.ToString();

            if (_skillSpeedText != null)
                _skillSpeedText.text = data.Speed.ToString();

            if (_skillEffectText != null)
                _skillEffectText.text = data.Skill1Effect ?? "";

            if (_skillCountText != null)
            {
                bool show = count > 1;
                _skillCountText.gameObject.SetActive(show);
                _skillCountText.text = show ? $"×{count}" : "";
            }
        }

        public void SetEmpty()
        {
            CardId = null;
            _character = null;
            _skill = null;
            if (_characterRoot != null) _characterRoot.SetActive(false);
            if (_skillRoot != null) _skillRoot.SetActive(false);
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_character != null) HoverPreview?.Show(_character);
            else if (_skill != null) HoverPreview?.Show(_skill);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverPreview?.Hide();
        }
    }
}