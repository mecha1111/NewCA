using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CrossAccel.UI
{
    /// <summary>
    /// 카드 아트 참조 모음. 런타임에 카드 ID로 일러스트를 찾기 위한 것이다.
    ///
    /// Resources 폴더에 이미지를 통째로 넣는 대신 이 작은 애셋 하나만 Resources에 두고
    /// 이미지들은 원래 자리(Assets/Art/…)에 남긴다 — Resources에 있는 것은 실제 사용 여부와
    /// 무관하게 전부 빌드에 포함되므로, 참조로 끌어오는 편이 낫다.
    ///
    /// 내용은 에디터 도구(CardArtTool)가 Assets/Art를 스캔해 채운다.
    /// </summary>
    public class CardArtLibrary : ScriptableObject
    {
        public const string ResourcePath = "CardArtLibrary";

        [Serializable]
        public class Entry
        {
            public string CardId;
            public Sprite Illustration;
        }

        [SerializeField] private Sprite characterFrame;
        [SerializeField] private Sprite bottomScrim;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private List<Entry> illustrations = new List<Entry>();

        private Dictionary<string, Sprite> _lookup;

        public Sprite CharacterFrame => characterFrame;

        /// <summary>일러스트와 텍스트 사이에 까는 세로 그라데이션 (위 투명 → 아래 어두움).</summary>
        public Sprite BottomScrim => bottomScrim;

        public TMP_FontAsset Font => font;

        private static CardArtLibrary _instance;

        /// <summary>없으면 null — 아트 없이도 게임은 돌아야 하므로 예외를 던지지 않는다.</summary>
        public static CardArtLibrary Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<CardArtLibrary>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>카드 ID에 해당하는 일러스트. 없으면 null (그 경우 호출자가 레이어를 끈다).</summary>
        public Sprite GetIllustration(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;

            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Sprite>();
                foreach (var entry in illustrations)
                    if (!string.IsNullOrEmpty(entry.CardId) && entry.Illustration != null)
                        _lookup[entry.CardId] = entry.Illustration;
            }

            return _lookup.TryGetValue(cardId, out var sprite) ? sprite : null;
        }

#if UNITY_EDITOR
        /// <summary>에디터 도구가 내용을 채울 때 쓴다.</summary>
        public void EditorPopulate(Sprite frame, Sprite scrim, TMP_FontAsset fontAsset, List<Entry> entries)
        {
            characterFrame = frame;
            bottomScrim = scrim;
            font = fontAsset;
            illustrations = entries;
            _lookup = null;
        }
#endif
    }
}
