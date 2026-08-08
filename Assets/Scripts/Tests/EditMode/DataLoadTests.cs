using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrossAccel.Data;
using NUnit.Framework;
using UnityEngine;

namespace CrossAccel.Tests
{
    /// <summary>
    /// Phase 1 데이터 층 검증. docs/DATA_SCHEMA.md 검증 체크리스트를 그대로 테스트로 옮긴 것.
    /// 실제 Assets/StreamingAssets/DataJsons/*.json 파일을 읽어서 CardDatabase에 주입한다.
    /// </summary>
    public class DataLoadTests
    {
        private static readonly string[] SkillIdsWithSkill2 = { "S20", "S52", "S61", "S64", "S84" };

        private static string DataJsonsPath =>
            Path.Combine(Application.dataPath, "StreamingAssets", "DataJsons");

        private static string ReadJson(string fileName) =>
            File.ReadAllText(Path.Combine(DataJsonsPath, fileName));

        // ---------------- 캐릭터 ----------------

        [Test]
        public void LoadCharacters_RealData_Loads62WithNoDuplicateIds()
        {
            var db = new CardDatabase();
            db.LoadCharacters(ReadJson("CharacterCardData.json"));

            Assert.AreEqual(62, db.Characters.Count);
        }

        [Test]
        public void LoadCharacters_DuplicateId_Throws()
        {
            const string json = @"{ ""characters"": [
                { ""id"": ""C01"", ""name"": ""a"", ""race"": ""인간"", ""weaponType"": ""활"", ""maxHp"": 1, ""reach"": 1, ""effectTiming"": """", ""effectText"": """" },
                { ""id"": ""C01"", ""name"": ""b"", ""race"": ""인간"", ""weaponType"": ""활"", ""maxHp"": 1, ""reach"": 1, ""effectTiming"": """", ""effectText"": """" }
            ] }";

            var db = new CardDatabase();
            Assert.Throws<ArgumentException>(() => db.LoadCharacters(json));
        }

        // ---------------- 스킬 ----------------

        [Test]
        public void LoadSkills_RealData_Loads84WithNoDuplicateIds()
        {
            var db = new CardDatabase();
            db.LoadSkills(ReadJson("SkillData.json"));

            Assert.AreEqual(84, db.Skills.Count);
        }

        [Test]
        public void LoadSkills_CardsWithSkill2_HasSkill2AndEffectNotEmpty()
        {
            var db = new CardDatabase();
            db.LoadSkills(ReadJson("SkillData.json"));

            foreach (var id in SkillIdsWithSkill2)
            {
                var skill = db.Skills[id];
                Assert.IsTrue(skill.HasSkill2, $"{id}는 skill2가 있어야 함");
                Assert.IsFalse(string.IsNullOrEmpty(skill.Skill2Effect), $"{id}의 skill2Effect가 비어있음");
            }
        }

        [Test]
        public void LoadSkills_CardsWithoutSkill2_Skill2CostIsNull()
        {
            var db = new CardDatabase();
            db.LoadSkills(ReadJson("SkillData.json"));

            var withSkill2 = new HashSet<string>(SkillIdsWithSkill2);
            var withoutSkill2 = db.Skills.Values.Where(s => !withSkill2.Contains(s.Id));

            foreach (var skill in withoutSkill2)
            {
                Assert.IsFalse(skill.HasSkill2, $"{skill.Id}는 skill2가 없어야 함");
                Assert.IsNull(skill.Skill2Cost, $"{skill.Id}의 skill2Cost는 null이어야 함 (필드 자체 생략 방식)");
            }
        }

        // ---------------- 무기 타입 파싱 ----------------

        [Test]
        public void WeaponTypeParser_CommaSeparated_SplitsIntoTwoTokens()
        {
            var result = WeaponTypeParser.Parse("스태프, 활");
            CollectionAssert.AreEqual(new[] { "스태프", "활" }, result);
        }

        [Test]
        public void WeaponTypeParser_SpaceVariant_NormalizesToCanonicalForm()
        {
            var result = WeaponTypeParser.Parse("한손 검");
            CollectionAssert.AreEqual(new[] { "한손검" }, result);
        }

        [Test]
        public void WeaponTypeParser_RealSkillData_AllTokensHaveNoInternalWhitespace()
        {
            var db = new CardDatabase();
            db.LoadSkills(ReadJson("SkillData.json"));

            foreach (var skill in db.Skills.Values)
            {
                var tokens = WeaponTypeParser.Parse(skill.WeaponType);
                foreach (var token in tokens)
                    Assert.IsFalse(token.Any(char.IsWhiteSpace), $"{skill.Id}: 정규화 후에도 공백 잔존 '{token}'");
            }
        }

        // ---------------- 스타터 덱 ----------------

        [Test]
        public void LoadDecks_RealData_TwoDecksLoaded()
        {
            var db = new CardDatabase();
            db.LoadDecks(ReadJson("StarterDecks.json"));

            Assert.AreEqual(2, db.Decks.Count);
            CollectionAssert.AreEquivalent(new[] { "Aggro", "MidRange_Blood" }, db.Decks.Select(d => d.DeckName));
        }

        [Test]
        public void LoadDecks_RealData_CharacterAndSkillCardsSeparatedPerDeck()
        {
            var db = new CardDatabase();
            db.LoadDecks(ReadJson("StarterDecks.json"));

            foreach (var deck in db.Decks)
            {
                Assert.IsTrue(deck.Cards.Any(c => c.CardType == "Character"), $"{deck.DeckName}에 Character 카드가 있어야 함");
                Assert.IsTrue(deck.Cards.Any(c => c.CardType == "Skill"), $"{deck.DeckName}에 Skill 카드가 있어야 함");
            }
        }

        [Test]
        public void LoadDecks_RealData_CharacterCountIs10ForBothDecks()
        {
            var db = new CardDatabase();
            db.LoadDecks(ReadJson("StarterDecks.json"));

            foreach (var deck in db.Decks)
            {
                int characterCount = deck.Cards.Where(c => c.CardType == "Character").Sum(c => c.Count);
                Assert.AreEqual(10, characterCount, $"{deck.DeckName} 캐릭터 카드 수");
            }
        }

        [Test]
        public void LoadDecks_RealData_SkillCountBelow24RaisesWarningNotFailure()
        {
            // DATA_SCHEMA.md 실측: Aggro 스킬 18장, MidRange_Blood 스킬 20장 — 24장 미달(데이터 미완성).
            // 로더는 예외를 던지지 않고 경고만 남겨야 한다 (강제 실패 금지).
            var db = new CardDatabase();
            db.LoadDecks(ReadJson("StarterDecks.json"));

            Assert.IsTrue(db.Warnings.Any(w => w.Contains("Aggro") && w.Contains("18")), "Aggro 18장 경고 없음");
            Assert.IsTrue(db.Warnings.Any(w => w.Contains("MidRange_Blood") && w.Contains("20")), "MidRange_Blood 20장 경고 없음");
        }

        // ---------------- 통합 ----------------

        [Test]
        public void LoadAll_RealData_NoExceptions()
        {
            Assert.DoesNotThrow(() =>
            {
                var db = new CardDatabase();
                db.LoadCharacters(ReadJson("CharacterCardData.json"));
                db.LoadSkills(ReadJson("SkillData.json"));
                db.LoadDecks(ReadJson("StarterDecks.json"));
            });
        }
    }
}
