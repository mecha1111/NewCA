using CrossAccel.Battle;
using CrossAccel.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 덱 빌드 화면. 덱 선택에서 고른 스타터를 읽어 수정한다.
    /// [왜] DeckSelectController.SelectedDeckName / SelectedDeck 이 static으로 넘어온다.
    /// [주의] 저장 시 SelectedDeck을 다시 쓰고, BanPickState는 이 SelectedDeck을 우선 사용해야
    ///        수정한 구성이 게임에 반영된다.
    /// </summary>
    public class DeckBuildController : MonoBehaviour
    {
        private const int MaxCharacters = 10;
        private const int MaxSkills = 24;
        private const int MaxCopiesPerSkill = 2;

        [Header("토글")]
        [SerializeField] private Button _characterTabButton;
        [SerializeField] private Button _mainTabButton;

        [Header("전체 카드 (풀)")]
        [SerializeField] private Transform _poolContent;
        [SerializeField] private DeckBuildCardView _cardPrefab;
        [SerializeField] private int _poolPrewarm = 84;

        [Header("내 카드")]
        [SerializeField] private Transform _myDeckContent;
        [SerializeField] private int _myDeckPrewarm = 24;

        [Header("카운터 / 버튼")]
        [SerializeField] private TextMeshProUGUI _characterCountText;
        [SerializeField] private TextMeshProUGUI _mainCountText;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _backButton;

        [Header("프리뷰")]
        [SerializeField] private CardHoverPreview _hoverPreview;

        [Header("씬")]
        [SerializeField] private string _deckSelectSceneName = "DeckSelect";

        private readonly List<DeckBuildCardView> _poolCards = new List<DeckBuildCardView>();
        private readonly List<DeckBuildCardView> _myCards = new List<DeckBuildCardView>();

        /// <summary>작업 중 캐릭터 id 목록 (최대 10, 순서 유지).</summary>
        private readonly List<string> _workCharacters = new List<string>();

        /// <summary>작업 중 스킬 id → 매수.</summary>
        private readonly Dictionary<string, int> _workSkills = new Dictionary<string, int>();

        private string _deckName;
        private bool _characterMode = true;

        private void Awake()
        {
            if (_characterTabButton != null)
                _characterTabButton.onClick.AddListener(() => SetMode(character: true));
            if (_mainTabButton != null)
                _mainTabButton.onClick.AddListener(() => SetMode(character: false));
            if (_saveButton != null) _saveButton.onClick.AddListener(OnSave);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnReset);
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
        }

        private void Start()
        {
            EnsureCardPool();
            LoadFromSelection();
            SetMode(character: true);
        }

        /// <summary>
        /// [무엇] DeckSelect에서 넘긴 덱을 작업 버퍼로 복사한다.
        /// [주의] SelectedDeck이 없으면 이름만으로 CardDatabase에서 다시 만든다.
        /// </summary>
        private void LoadFromSelection()
        {
            _deckName = DeckSelectController.SelectedDeckName;
            if (string.IsNullOrEmpty(_deckName))
                _deckName = "Aggro";

            DeckSelection selection = DeckSelectController.SelectedDeck;
            if (selection == null)
            {
                var db = CardDatabaseProvider.Instance;
                var data = db.Decks.FirstOrDefault(d => d.DeckName == _deckName);
                if (data != null)
                    selection = DeckSelection.FromDeckData(data);
            }

            _workCharacters.Clear();
            _workSkills.Clear();

            if (selection != null)
            {
                foreach (var id in selection.CharacterDeck)
                    _workCharacters.Add(id);

                foreach (var entry in selection.MainDeck)
                    _workSkills[entry.CardId] = entry.Count;
            }

            Debug.Log($"[DeckBuild] 로드 '{_deckName}' 캐릭터 {_workCharacters.Count} / 스킬 {SkillTotalCount()}");
            RefreshCounters();
        }

        private void EnsureCardPool()
        {
            if (_cardPrefab == null || _poolContent == null || _myDeckContent == null)
            {
                Debug.LogError("[DeckBuild] Prefab 또는 Content 참조가 비어 있다.");
                return;
            }

            Prewarm(_poolContent, _poolCards, _poolPrewarm);
            Prewarm(_myDeckContent, _myCards, _myDeckPrewarm);
        }

        private void Prewarm(Transform parent, List<DeckBuildCardView> list, int count)
        {
            while (list.Count < count)
            {
                var view = Instantiate(_cardPrefab, parent);
                view.HoverPreview = _hoverPreview;
                view.gameObject.SetActive(false);
                list.Add(view);
            }
        }

        private void SetMode(bool character)
        {
            _characterMode = character;
            RefreshPool();
            RefreshMyDeck();
            RefreshCounters();
        }

        private void RefreshPool()
        {
            var db = CardDatabaseProvider.Instance;
            int i = 0;

            if (_characterMode)
            {
                foreach (var kv in db.Characters.OrderBy(k => k.Key))
                {
                    if (i >= _poolCards.Count) break;
                    var view = _poolCards[i++];
                    view.Clicked -= OnPoolClicked;
                    view.BindCharacter(kv.Value);
                    view.Clicked += OnPoolClicked;
                }
            }
            else
            {
                foreach (var kv in db.Skills.OrderBy(k => k.Key))
                {
                    if (i >= _poolCards.Count) break;
                    var view = _poolCards[i++];
                    view.Clicked -= OnPoolClicked;
                    view.BindSkill(kv.Value);
                    view.Clicked += OnPoolClicked;
                }
            }

            for (; i < _poolCards.Count; i++)
            {
                _poolCards[i].Clicked -= OnPoolClicked;
                _poolCards[i].SetEmpty();
            }
        }

        private void RefreshMyDeck()
        {
            var db = CardDatabaseProvider.Instance;
            int i = 0;

            if (_characterMode)
            {
                foreach (var id in _workCharacters)
                {
                    if (i >= _myCards.Count) break;
                    if (!db.Characters.TryGetValue(id, out var data)) continue;
                    var view = _myCards[i++];
                    view.Clicked -= OnMyDeckClicked;
                    view.BindCharacter(data);
                    view.Clicked += OnMyDeckClicked;
                }
            }
            else
            {
                foreach (var kv in _workSkills.OrderBy(k => k.Key))
                {
                    if (i >= _myCards.Count) break;
                    if (!db.Skills.TryGetValue(kv.Key, out var data)) continue;
                    var view = _myCards[i++];
                    view.Clicked -= OnMyDeckClicked;
                    view.BindSkill(data, kv.Value);
                    view.Clicked += OnMyDeckClicked;
                }
            }

            for (; i < _myCards.Count; i++)
            {
                _myCards[i].Clicked -= OnMyDeckClicked;
                _myCards[i].SetEmpty();
            }
        }

        private void OnPoolClicked(DeckBuildCardView view)
        {
            if (string.IsNullOrEmpty(view.CardId)) return;

            if (view.IsCharacter)
            {
                if (_workCharacters.Count >= MaxCharacters)
                {
                    Debug.Log("[DeckBuild] 캐릭터 덱이 가득 찼다 (10/10)");
                    return;
                }
                if (_workCharacters.Contains(view.CardId))
                {
                    Debug.Log("[DeckBuild] 이미 넣은 캐릭터다");
                    return;
                }
                _workCharacters.Add(view.CardId);
            }
            else
            {
                int total = SkillTotalCount();
                _workSkills.TryGetValue(view.CardId, out int have);
                if (have >= MaxCopiesPerSkill)
                {
                    Debug.Log("[DeckBuild] 동일 스킬은 최대 2장");
                    return;
                }
                if (total >= MaxSkills)
                {
                    Debug.Log("[DeckBuild] 메인 덱이 가득 찼다 (24/24)");
                    return;
                }
                _workSkills[view.CardId] = have + 1;
            }

            RefreshMyDeck();
            RefreshCounters();
        }

        private void OnMyDeckClicked(DeckBuildCardView view)
        {
            if (string.IsNullOrEmpty(view.CardId)) return;

            if (view.IsCharacter)
            {
                _workCharacters.Remove(view.CardId);
            }
            else
            {
                if (!_workSkills.TryGetValue(view.CardId, out int have)) return;
                have--;
                if (have <= 0) _workSkills.Remove(view.CardId);
                else _workSkills[view.CardId] = have;
            }

            RefreshMyDeck();
            RefreshCounters();
        }

        private int SkillTotalCount()
        {
            int n = 0;
            foreach (var kv in _workSkills) n += kv.Value;
            return n;
        }

        private void RefreshCounters()
        {
            if (_characterCountText != null)
                _characterCountText.text = $"캐릭터 {_workCharacters.Count}/{MaxCharacters}";
            if (_mainCountText != null)
                _mainCountText.text = $"메인 {SkillTotalCount()}/{MaxSkills}";

            bool complete = _workCharacters.Count == MaxCharacters && SkillTotalCount() == MaxSkills;
            // 스타터 데이터가 24장 미달일 수 있으므로, 저장은 일단 항상 허용.
            // 엄격히 막을 거면 complete일 때만 interactable = true.
            if (_saveButton != null) _saveButton.interactable = true;
        }

        /// <summary>
        /// [무엇] 작업 버퍼를 DeckSelection으로 만들어 SelectedDeck에 덮어쓴다.
        /// [왜] 밴픽/게임 시작이 이 static을 읽어 수정된 덱을 쓰게 한다.
        /// </summary>
        private void OnSave()
        {
            var selection = new DeckSelection();
            foreach (var id in _workCharacters)
                selection.CharacterDeck.Add(id);
            foreach (var kv in _workSkills)
                selection.MainDeck.Add(new MainDeckEntry(kv.Key, kv.Value));

            DeckSelectController.ApplyEditedDeck(_deckName, selection);
            Debug.Log($"[DeckBuild] 저장 '{_deckName}' 캐릭터 {selection.CharacterDeck.Count} / 스킬 {SkillTotalCount()}");
        }

        private void OnReset()
        {
            // JSON 원본으로 되돌림 (편집 세션 제거)
            DeckSelectController.ClearEditedDeck(_deckName);

            var db = CardDatabaseProvider.Instance;
            var data = db.Decks.FirstOrDefault(d => d.DeckName == _deckName);
            if (data != null)
            {
                var selection = DeckSelection.FromDeckData(data);
                DeckSelectController.SetSelection(_deckName, selection);
            }

            LoadFromSelection();
            RefreshPool();
            RefreshMyDeck();
            RefreshCounters();
        }

        private void OnBack()
        {
            if (!string.IsNullOrEmpty(_deckSelectSceneName))
                SceneManager.LoadScene(_deckSelectSceneName);
        }
    }
}