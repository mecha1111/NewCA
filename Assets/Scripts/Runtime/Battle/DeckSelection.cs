using System.Collections.Generic;
using CrossAccel.Data;

namespace CrossAccel.Battle
{
    /// <summary>
    /// 플레이어가 게임에 들고 오는 덱 (밴픽 전 상태).
    /// 캐릭터 덱 10장과 메인 덱 24장을 분리해서 담는다 (RULES.md 3번, DATA_SCHEMA.md).
    /// </summary>
    public class DeckSelection
    {
        /// <summary>캐릭터 덱 — 캐릭터 카드 id 10장 (RULES.md 3번).</summary>
        public List<string> CharacterDeck { get; } = new List<string>();

        /// <summary>메인 덱 구성 — (스킬 카드 id, 매수). 합계 24장이 규칙이나 스타터 데이터는 미달 (DATA_SCHEMA.md).</summary>
        public List<MainDeckEntry> MainDeck { get; } = new List<MainDeckEntry>();

        /// <summary>
        /// StarterDecks.json에서 읽은 DeckData를 캐릭터 덱/메인 덱으로 나눈다.
        /// Item 카드는 원본 데이터가 미완이라 건너뛴다 (DATA_SCHEMA.md: I13/I14는 현재 범위 밖).
        /// </summary>
        public static DeckSelection FromDeckData(DeckData deck)
        {
            var selection = new DeckSelection();
            if (deck?.Cards == null) return selection;

            foreach (var entry in deck.Cards)
            {
                if (entry.CardType == "Character")
                {
                    for (int i = 0; i < entry.Count; i++)
                        selection.CharacterDeck.Add(entry.CardId);
                }
                else if (entry.CardType == "Skill")
                {
                    selection.MainDeck.Add(new MainDeckEntry(entry.CardId, entry.Count));
                }
            }
            return selection;
        }
    }

    /// <summary>메인 덱 구성 한 줄 — 카드 id와 매수 (중복 최대 2장, RULES.md 3번).</summary>
    public class MainDeckEntry
    {
        public string CardId { get; }
        public int Count { get; }

        public MainDeckEntry(string cardId, int count)
        {
            CardId = cardId;
            Count = count;
        }
    }
}
