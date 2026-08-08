using System.IO;
using UnityEngine;

namespace CrossAccel.Data
{
    /// <summary>
    /// StreamingAssets/DataJsons에서 카드 데이터를 읽어 CardDatabase를 구성하는 런타임 로더.
    /// CardDatabase 자체는 파일 I/O를 모르게 유지한다는 ARCHITECTURE.md 원칙을 지키면서, 실제 파일
    /// 읽기는 여기(호출자)가 담당한다. BanPick 런타임 코드와 Editor 툴(BattleUIBuilder 등)이
    /// 이 하나의 로더를 공유해서 "같은 CardDatabase"를 쓴다 (UNITY_BANPICK_UI_SPEC.md 8번).
    ///
    /// Application.streamingAssetsPath는 에디터/스탠드얼론 빌드 양쪽에서 File.ReadAllText로
    /// 바로 읽을 수 있다 (WebGL 등은 UnityWebRequest가 필요하지만 현재 프로젝트 범위 밖).
    /// </summary>
    public static class CardDatabaseProvider
    {
        private static CardDatabase _instance;

        /// <summary>한 번 로드하면 재사용한다 — 씬을 오가도(BanScene↔PickScene) 다시 읽지 않는다.</summary>
        public static CardDatabase Instance
        {
            get
            {
                if (_instance == null) _instance = Load();
                return _instance;
            }
        }

        private static CardDatabase Load()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "DataJsons");
            var db = new CardDatabase();
            db.LoadCharacters(File.ReadAllText(Path.Combine(dir, "CharacterCardData.json")));
            // 스킬도 반드시 로드해야 한다 — GameManager의 세팅 단계(BuildMainDeck)가 스킬 id로
            // 메인덱을 구성하므로, 없으면 전부 "스킬 데이터 없음 — 스킵"이 되어 덱이 통째로 빈다.
            db.LoadSkills(File.ReadAllText(Path.Combine(dir, "SkillData.json")));
            db.LoadDecks(File.ReadAllText(Path.Combine(dir, "StarterDecks.json")));
            return db;
        }
    }
}
