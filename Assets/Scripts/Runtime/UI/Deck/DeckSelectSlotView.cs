using CrossAccel.Data;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 덱 선택 화면의 슬롯 한 칸. 루트에 Button이 붙어 있는 전제.
    /// [왜] 현재 씬 슬롯이 버튼으로 만들어져 있어, 별도 Button 참조 없이 자기 컴포넌트를 쓴다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DeckSelectSlotView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Image _background;
        [SerializeField] private GameObject _selectedMark;

        [Header("색")]
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.19f, 0.66f, 0.81f, 1f);

        public DeckData BoundDeck { get; private set; }
        public int Index { get; private set; } = -1;
        public event Action<int> Clicked;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(() => Clicked?.Invoke(Index));
        }

        public void Bind(DeckData deck, int index)
        {
            BoundDeck = deck;
            Index = index;

            if (_nameText != null)
                _nameText.text = deck != null ? deck.DeckName : "";

            if (_button == null)
                _button = GetComponent<Button>();

            if (_button != null)
                _button.interactable = deck != null;

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_background != null)
                _background.color = selected ? _selectedColor : _normalColor;

            if (_selectedMark != null)
                _selectedMark.SetActive(selected);
        }
    }
}