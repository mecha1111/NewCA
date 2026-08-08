using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CrossAccel.Data
{
    /// <summary>
    /// 카드 데이터 저장소. id → 데이터 딕셔너리를 제공한다.
    /// ARCHITECTURE.md 원칙: 파일 I/O는 이 클래스 밖(호출자)에서 하고, 여기는 JSON 문자열을 주입받는다.
    /// 그래야 EditMode 테스트가 StreamingAssets 경로 없이 문자열만으로 로드를 검증할 수 있다.
    /// </summary>
    public class CardDatabase
    {
        private readonly Dictionary<string, CharacterData> _characters = new Dictionary<string, CharacterData>();
        private readonly Dictionary<string, SkillData> _skills = new Dictionary<string, SkillData>();
        private readonly List<DeckData> _decks = new List<DeckData>();
        private readonly List<string> _warnings = new List<string>();

        public IReadOnlyDictionary<string, CharacterData> Characters => _characters;
        public IReadOnlyDictionary<string, SkillData> Skills => _skills;
        public IReadOnlyList<DeckData> Decks => _decks;

        /// <summary>로드 중 발생한 비치명적 이슈 (예: 스타터 덱 스킬 24장 미달). 강제 실패 아님 — 그대로 로드하고 경고만 남긴다.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>CharacterCardData.json 내용을 로드한다. id 중복 시 예외 발생(fail-fast).</summary>
        public void LoadCharacters(string json)
        {
            var file = JsonConvert.DeserializeObject<CharacterDataFile>(json);
            if (file?.Characters == null) return;

            foreach (var character in file.Characters)
                _characters.Add(character.Id, character);
        }

        /// <summary>SkillData.json 내용을 로드한다. id 중복 시 예외 발생(fail-fast).</summary>
        public void LoadSkills(string json)
        {
            var file = JsonConvert.DeserializeObject<SkillDataFile>(json);
            if (file?.Skills == null) return;

            foreach (var skill in file.Skills)
                _skills.Add(skill.Id, skill);
        }

        /// <summary>
        /// StarterDecks.json 내용을 로드한다. 덱마다 캐릭터 10장/스킬 24장 규칙과 대조해서
        /// 어긋나면 경고만 쌓는다 (DATA_SCHEMA.md: 스타터 덱 스킬 수는 현재 24장 미달, 데이터 미완성이므로 강제 실패 금지).
        /// </summary>
        public void LoadDecks(string json)
        {
            var file = JsonConvert.DeserializeObject<DeckDataFile>(json);
            if (file?.Decks == null) return;

            foreach (var deck in file.Decks)
            {
                _decks.Add(deck);
                ValidateDeckCount(deck);
            }
        }

        private void ValidateDeckCount(DeckData deck)
        {
            int skillCount = deck.Cards.Where(c => c.CardType == "Skill").Sum(c => c.Count);
            int characterCount = deck.Cards.Where(c => c.CardType == "Character").Sum(c => c.Count);

            if (skillCount != 24)
                _warnings.Add($"[{deck.DeckName}] 스킬 카드 {skillCount}장 (규칙: 24장) — 데이터 미완성, DATA_SCHEMA.md 참고");
            if (characterCount != 10)
                _warnings.Add($"[{deck.DeckName}] 캐릭터 카드 {characterCount}장 (규칙: 10장)");
        }
    }
}
