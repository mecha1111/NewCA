using System.Collections.Generic;
using Newtonsoft.Json;

namespace CrossAccel.Data
{
    /// <summary>덱 안의 카드 한 항목 (StarterDecks.json의 cards[] 원소). docs/DATA_SCHEMA.md 참고.</summary>
    public class DeckCardEntry
    {
        [JsonProperty("cardId")] public string CardId { get; set; }

        /// <summary>"Character" / "Skill" / "Item" 중 하나.</summary>
        [JsonProperty("cardType")] public string CardType { get; set; }

        [JsonProperty("count")] public int Count { get; set; }
    }

    /// <summary>
    /// 스타터 덱 하나의 원본 데이터 (읽기 전용). Assets/StreamingAssets/DataJsons/StarterDecks.json에서 로드.
    /// Character 카드는 캐릭터 덱(10장), Skill 카드는 메인 덱(24장)으로 분리해서 사용 (DATA_SCHEMA.md).
    /// </summary>
    public class DeckData
    {
        [JsonProperty("deckName")] public string DeckName { get; set; }
        [JsonProperty("cards")] public List<DeckCardEntry> Cards { get; set; } = new List<DeckCardEntry>();
    }

    /// <summary>StarterDecks.json의 최상위 래퍼 ({ "decks": [...] }). 로더 내부 전용.</summary>
    internal class DeckDataFile
    {
        [JsonProperty("decks")] public List<DeckData> Decks { get; set; }
    }
}
