using CrossAccel.Battle;
using CrossAccel.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 덱 선택 화면. 스타터 2칸(Aggro / MidRange_Blood)을 보여 주고 수정·선택·뒤로를 처리한다.
    /// [왜] BanPickState가 내 덱 이름을 여기서 고른 SelectedDeckName으로 읽는다.
    ///      덱 빌드에서 저장한 구성은 _editedDecks에 남겨, 다시 골라도 JSON 원본으로 덮지 않는다.
    /// [주의] 네임스페이스는 BanPickState와 같은 CrossAccel.UI 여야 한다.
    /// </summary>
    public class DeckSelectController : MonoBehaviour
    {
        /// <summary>1번 슬롯 = Aggro</summary>
        private const string Slot0DeckName = "Aggro";

        /// <summary>2번 슬롯 = MidRange_Blood</summary>
        private const string Slot1DeckName = "MidRange_Blood";

        [Header("슬롯 (0=Aggro, 1=MidRange_Blood)")]
        [SerializeField] private DeckSelectSlotView[] _slots;

        [Header("버튼")]
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _editButton;
        [SerializeField] private Button _selectButton;

        [Header("씬 이름")]
        [SerializeField] private string _mainMenuSceneName = "MainMenuScene";
        [SerializeField] private string _deckBuildSceneName = "DeckBuildScene";
        [SerializeField] private string _banSceneName = "BanScene";

        private int _selectedIndex = -1;

        /// <summary>
        /// [무엇] 마지막으로 고른 덱 이름. BanPickState.ResolveMyDeckName이 읽는다.
        /// [주의] static — 씬이 바뀌어도 유지된다. BanPickState.Reset과는 별개다.
        /// </summary>
        public static string SelectedDeckName { get; private set; }

        /// <summary>
        /// [무엇] 마지막으로 고른(또는 빌드에서 수정한) 덱 구성.
        /// [주의] 편집본이 있으면 JSON 원본 대신 이 값을 BanPick이 쓴다.
        /// </summary>
        public static DeckSelection SelectedDeck { get; private set; }

        /// <summary>
        /// [무엇] 덱 이름 → 빌드에서 저장한 구성.
        /// [왜] 선택 화면으로 돌아와 같은 슬롯을 다시 골라도 수정본을 유지한다.
        /// </summary>
        private static readonly Dictionary<string, DeckSelection> _editedDecks =
            new Dictionary<string, DeckSelection>();

        private void Awake()
        {
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
            if (_editButton != null) _editButton.onClick.AddListener(OnEdit);
            if (_selectButton != null) _selectButton.onClick.AddListener(OnSelect);
        }

        private void Start()
        {
            BindStarterDecks();
            RefreshButtons();
        }

        private void BindStarterDecks()
        {
            if (_slots == null || _slots.Length == 0) return;

            var db = CardDatabaseProvider.Instance;
            string[] names = { Slot0DeckName, Slot1DeckName };

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;

                DeckData deck = null;
                if (i < names.Length)
                    deck = FindDeckByName(db, names[i]);

                _slots[i].Bind(deck, i);
                _slots[i].Clicked -= OnSlotClicked;
                _slots[i].Clicked += OnSlotClicked;
            }

            if (!string.IsNullOrEmpty(SelectedDeckName))
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null && _slots[i].BoundDeck != null
                        && _slots[i].BoundDeck.DeckName == SelectedDeckName)
                    {
                        SelectIndex(i);
                        return;
                    }
                }
            }

            if (_slots.Length > 0 && _slots[0] != null && _slots[0].BoundDeck != null)
                SelectIndex(0);
            else
            {
                _selectedIndex = -1;
                RefreshButtons();
            }
        }

        private static DeckData FindDeckByName(CardDatabase db, string deckName)
        {
            for (int i = 0; i < db.Decks.Count; i++)
            {
                if (db.Decks[i].DeckName == deckName)
                    return db.Decks[i];
            }

            Debug.LogWarning($"[DeckSelect] 스타터 덱 '{deckName}'을(를) 찾지 못했다. StarterDecks.json 이름을 확인하라.");
            return null;
        }

        private void OnSlotClicked(int index) => SelectIndex(index);

        private void SelectIndex(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length) return;
            if (_slots[index] == null || _slots[index].BoundDeck == null) return;

            _selectedIndex = index;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                    _slots[i].SetSelected(i == index);
            }

            DeckData deck = _slots[index].BoundDeck;
            SelectedDeckName = deck.DeckName;

            // 빌드에서 저장한 수정본이 있으면 그걸 쓰고, 없으면 JSON 원본
            if (_editedDecks.TryGetValue(deck.DeckName, out var edited))
                SelectedDeck = edited;
            else
                SelectedDeck = DeckSelection.FromDeckData(deck);

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool hasSelection = _selectedIndex >= 0
                                && _slots != null
                                && _selectedIndex < _slots.Length
                                && _slots[_selectedIndex] != null
                                && _slots[_selectedIndex].BoundDeck != null;

            if (_editButton != null) _editButton.interactable = hasSelection;
            if (_selectButton != null) _selectButton.interactable = hasSelection;
        }

        private void OnBack()
        {
            if (!string.IsNullOrEmpty(_mainMenuSceneName))
                SceneManager.LoadScene(_mainMenuSceneName);
        }

        /// <summary>
        /// [무엇] 고른 덱을 들고 덱 빌드 씬으로 이동한다.
        /// [주의] SelectedDeck / SelectedDeckName은 static이라 빌드 씬에서 그대로 읽는다.
        /// </summary>
        private void OnEdit()
        {
            if (_selectedIndex < 0) return;
            if (string.IsNullOrEmpty(_deckBuildSceneName)) return;
            SceneManager.LoadScene(_deckBuildSceneName);
        }

        /// <summary>
        /// [무엇] 고른 덱으로 밴 씬에 진입한다.
        /// [주의] 이전 밴픽 세션을 Reset해야 EnsureStarted가 새 SelectedDeck으로 다시 시작한다.
        /// </summary>
        private void OnSelect()
        {
            if (_selectedIndex < 0) return;
            if (string.IsNullOrEmpty(_banSceneName)) return;

            BanPickState.Reset();
            SceneManager.LoadScene(_banSceneName);
        }

        // ===================== 덱 빌드 연동 =====================

        /// <summary>
        /// [무엇] 덱 빌드에서 저장한 구성을 선택 결과에 반영한다.
        /// [왜] 이후 덱 선택·밴픽이 수정된 CharacterDeck/MainDeck을 쓰게 한다.
        /// </summary>
        public static void ApplyEditedDeck(string deckName, DeckSelection selection)
        {
            if (string.IsNullOrEmpty(deckName) || selection == null) return;

            SelectedDeckName = deckName;
            SelectedDeck = selection;
            _editedDecks[deckName] = selection;
            Debug.Log($"[DeckSelect] 편집 덱 저장 '{deckName}' " +
                      $"캐릭터 {selection.CharacterDeck.Count} / 메인 엔트리 {selection.MainDeck.Count}");
        }

        /// <summary>
        /// [무엇] 특정 덱의 편집본을 지운다. null이면 전부 지운다.
        /// [왜] 덱 빌드 초기화 시 JSON 원본으로 되돌릴 때 쓴다.
        /// </summary>
        public static void ClearEditedDeck(string deckName = null)
        {
            if (string.IsNullOrEmpty(deckName))
            {
                _editedDecks.Clear();
                SelectedDeck = null;
            }
            else
            {
                _editedDecks.Remove(deckName);
                if (SelectedDeckName == deckName)
                    SelectedDeck = null;
            }
        }

        /// <summary>
        /// [무엇] 이름과 구성을 강제로 지정한다 (빌드 초기화 후 원본 재로드 등).
        /// </summary>
        public static void SetSelection(string deckName, DeckSelection selection)
        {
            SelectedDeckName = deckName;
            SelectedDeck = selection;
        }
    }
}