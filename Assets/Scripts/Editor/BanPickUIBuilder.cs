using CrossAccel.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.EditorTools
{
    /// <summary>
    /// docs/UNITY_BANPICK_UI_SPEC.md 1~4단계 — BanScene + PickScene 뼈대와 씬 전환 흐름.
    /// 좌표 변환·Canvas 세팅은 UNITY_BATTLE_UI_SPEC.md와 동일해서 BattleUIBuilder의 internal
    /// 헬퍼(CreateRect/CreatePanel/CreateLabel/Hex/BuildCanvas/EnsureEventSystem/EnsureMainCamera)를
    /// 그대로 재사용한다.
    ///
    /// ⚠️ 이번 단계 범위:
    /// - BanPickCard 시각 구성(프레임/일러스트/스크림/텍스트/오버레이)은 BanPickCardView가 스스로
    ///   만든다(CharacterCardView와 같은 3레이어 문법, 작업 3). 빌더는 위치와 크기만 정한다.
    /// - "확정 버튼" 크기(right/bottom만 있고 width/height 없음), "픽 슬롯 4칸 간격"(중앙 정렬인데
    ///   슬롯 사이 간격 수치 없음)은 임의 값을 넣고 주석에 남겼다 — 실제 값 나오면 교체.
    /// - CardDatabase 연결(5단계), GameManager 연결(6단계)은 다음 작업 — 지금은 전부 더미 데이터.
    /// - 회전 요소는 없다(밴픽 화면엔 회전 카드가 없음) — 있었다면 배틀 덱 피벗 버그와 같은 이유로
    ///   반드시 중심(0.5,0.5) 피벗을 썼을 것.
    /// </summary>
    public static class BanPickUIBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string MainMenuScenePath = ScenesFolder + "/MainMenuScene.unity";
        private const string BanScenePath = ScenesFolder + "/BanScene.unity";
        private const string PickScenePath = ScenesFolder + "/PickScene.unity";
        private const string BattleScenePath = ScenesFolder + "/BattleScene.unity";

        private const string BanRootName = "BanUI";
        private const string PickRootName = "PickUI";

        private const float BanCardWidth = 118f;
        private const float PickCardWidth = 150f;

        /// <summary>
        /// 밴/픽 카드 높이 — 폭에서 CardArt.HeightFor로 역산한다(0.6545 고정, 왜곡 금지).
        /// [왜] 예전엔 182/231을 손으로 정했는데(비율 0.6484/0.6494) CARD_UI_FIX_SPEC.md가 확정한
        ///      실제 에셋 비율(0.6545)과 어긋나 카드가 미세하게 눌려 보였다. 이제 카드가 프레임
        ///      아트를 실제로 쓰므로(작업 3) 왜곡이 눈에 보인다 — 폭만 정하고 높이는 역산한다.
        /// [주의] 182→180.29 / 231→229.17로 살짝 줄어든다. 풀 그리드 행 간격은 그대로 두었고
        ///        카드가 더 작아지는 방향이라 겹침 위험은 오히려 줄어든다(완료 보고 표 참고).
        /// </summary>
        private static readonly float BanCardHeight = CardArt.HeightFor(BanCardWidth);
        private static readonly float PickCardHeight = CardArt.HeightFor(PickCardWidth);

        // ===================== 메뉴 =====================

        [MenuItem("CrossAccel/BanPick/Build Ban Scene UI")]
        public static void BuildBanSceneUI() => BuildBanSceneContent();

        [MenuItem("CrossAccel/BanPick/Build Pick Scene UI")]
        public static void BuildPickSceneUI() => BuildPickSceneContent();

        /// <summary>BanScene/PickScene을 새로 만들어 저장하고, MainMenu→Ban→Pick→Battle 순으로 Build Settings에 등록.</summary>
        [MenuItem("CrossAccel/BanPick/Setup Scenes (Ban + Pick)")]
        public static void SetupScenes()
        {
            EnsureScenesFolder();
            SaveBanScene();
            SavePickScene();
            RegisterBuildSettings();

            Debug.Log($"[BanPickUIBuilder] {BanScenePath}, {PickScenePath} 생성 + Build Settings 등록 완료.");
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        private static void SaveBanScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildBanSceneContent();
            EditorSceneManager.SaveScene(scene, BanScenePath);
        }

        private static void SavePickScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildPickSceneContent();
            EditorSceneManager.SaveScene(scene, PickScenePath);
        }

        /// <summary>
        /// 씬 순서 등록은 PlaceSceneBuilder.RegisterAllScenes 하나에만 둔다 (모듈 A가 관리).
        /// [왜] 예전에 빌더마다 제각기 Build Settings를 덮어써서, 어떤 빌더를 마지막에 돌렸느냐에 따라
        ///      씬 목록이 달라지는 문제가 있었다. PlaceScene(3) 추가 이후로는 특히 위험하다.
        /// </summary>
        private static void RegisterBuildSettings() => PlaceSceneBuilder.RegisterAllScenes();

        // ===================== BanScene (SPEC 3번) =====================

        private static void BuildBanSceneContent()
        {
            var existing = GameObject.Find(BanRootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var canvasGO = BattleUIBuilder.BuildCanvas(BanRootName);
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build Ban Scene UI");
            var canvasTf = canvasGO.transform;

            BattleUIBuilder.CreatePanel("Background", canvasTf, 0, 0, 1920, 1080, BattleUIBuilder.Hex("#0A0E18")).raycastTarget = false;

            BuildProgressBar(canvasTf);
            BuildInstructionText(canvasTf);

            BuildSideLabel(canvasTf, "OpponentLabel", 600, 70, "상대 진영 (밴 대상)", BattleUIBuilder.Hex("#E0512F"));
            BuildPoolGrid(canvasTf, "EnemyGrid", new[] { 637f, 769f, 901f, 1033f, 1165f }, new[] { 100f, 296f },
                BanCardWidth, BanCardHeight);

            BuildSideLabel(canvasTf, "MyLabel", 600, 502, "내 진영 (상대가 밴)", BattleUIBuilder.Hex("#31C5E0"));
            BuildPoolGrid(canvasTf, "MyGrid", new[] { 637f, 769f, 901f, 1033f, 1165f }, new[] { 532f, 728f },
                BanCardWidth, BanCardHeight);

            BuildConfirmButton(canvasTf, "밴 확정", BattleUIBuilder.Hex("#E0512F"));
            var hoverPreview = BuildPreviewPanel(canvasTf);
            WireHoverPreview(canvasTf, hoverPreview);

            BattleUIBuilder.EnsureEventSystem();
            BattleUIBuilder.EnsureMainCamera();

            canvasGO.AddComponent<BanSceneController>();

            Selection.activeGameObject = canvasGO;
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            Debug.Log("[BanPickUIBuilder] BanScene UI 생성 완료.");
        }

        // ===================== PickScene (SPEC 4번) =====================

        private static void BuildPickSceneContent()
        {
            var existing = GameObject.Find(PickRootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var canvasGO = BattleUIBuilder.BuildCanvas(PickRootName);
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build Pick Scene UI");
            var canvasTf = canvasGO.transform;

            BattleUIBuilder.CreatePanel("Background", canvasTf, 0, 0, 1920, 1080, BattleUIBuilder.Hex("#0A0E18")).raycastTarget = false;

            BuildProgressBar(canvasTf);
            BuildInstructionText(canvasTf);

            BuildSideLabel(canvasTf, "MyLabel", 635, 150, "내 진영 (픽)", BattleUIBuilder.Hex("#31C5E0"));
            BuildPoolGrid(canvasTf, "MyGrid", new[] { 557f, 721f, 885f, 1049f, 1213f }, new[] { 180f, 425f },
                PickCardWidth, PickCardHeight);

            BuildSlotContainer(canvasTf);

            BuildConfirmButton(canvasTf, "픽 확정", BattleUIBuilder.Hex("#31C5E0"));
            var hoverPreview = BuildPreviewPanel(canvasTf);
            WireHoverPreview(canvasTf, hoverPreview);

            BattleUIBuilder.EnsureEventSystem();
            BattleUIBuilder.EnsureMainCamera();

            canvasGO.AddComponent<PickSceneController>();

            Selection.activeGameObject = canvasGO;
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            Debug.Log("[BanPickUIBuilder] PickScene UI 생성 완료.");
        }

        // ===================== 공용 조각 (진행바/안내문/라벨/버튼/프리뷰) =====================

        /// <summary>SPEC 3/4번: 진행바 "중앙 | top 28". 실제 강조는 컨트롤러가 런타임에 BanPickProgressText로 채움.</summary>
        private static void BuildProgressBar(Transform parent) =>
            BattleUIBuilder.CreateLabel("ProgressBar", parent, 0, 28, 1920, 30, "", 18, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#EEF3FA"));

        /// <summary>SPEC 3/4번: 안내문 "중앙 | top 96". 텍스트는 컨트롤러가 런타임에 채움.</summary>
        private static void BuildInstructionText(Transform parent) =>
            BattleUIBuilder.CreateLabel("InstructionText", parent, 0, 96, 1920, 30, "", 20, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#EEF3FA"));

        /// <summary>"상대 진영"/"내 진영" 라벨. width/height가 SPEC에 없어 임의 400×28로 근사.</summary>
        private static void BuildSideLabel(Transform parent, string name, float left, float top, string text, Color color) =>
            BattleUIBuilder.CreateLabel(name, parent, left, top, 400, 28, text, 18, TextAnchor.MiddleLeft, color);

        /// <summary>SPEC 3/4번: 5열×2줄 카드 10장을 담는 컨테이너. 컨테이너 자체는 캔버스 원점에 고정해 자식 left/top이 SPEC 표 값 그대로 절대좌표가 되게 한다.</summary>
        private static void BuildPoolGrid(Transform parent, string containerName, float[] columnLefts, float[] rowTops, float cardWidth, float cardHeight)
        {
            var container = BattleUIBuilder.CreateRect(containerName, parent, 0, 0, 1920, 1080);
            for (int i = 0; i < 10; i++)
            {
                int col = i % columnLefts.Length;
                int row = i / columnLefts.Length;
                BuildBanPickCard(container, $"Card{i}", columnLefts[col], rowTops[row], cardWidth, cardHeight);
            }
        }

        /// <summary>
        /// SPEC 3/4번: "확정 버튼 right 56 bottom 56". 너비/높이가 SPEC에 없어 200×64로 임의 지정.
        /// interactable/onClick 연결은 BanSceneController/PickSceneController가 런타임에 담당.
        /// </summary>
        private static void BuildConfirmButton(Transform parent, string label, Color tint)
        {
            const float width = 200f;
            const float height = 64f;
            float left = 1920f - 56f - width;
            float top = 1080f - 56f - height;

            var panel = BattleUIBuilder.CreatePanel("ConfirmButton", parent, left, top, width, height, tint);
            panel.gameObject.AddComponent<Button>();
            BattleUIBuilder.CreateLabel("Label", panel.transform, 0, 0, width, height, label, 18, TextAnchor.MiddleCenter, Color.white);
        }

        /// <summary>
        /// 카드 호버 프리뷰(작업 4). 좌표는 CardHoverPreview.PanelXxx 상수(= full_prototype.html .pv-panel
        /// 그대로) — 배틀 씬(BattleUIBuilder)과 같은 컴포넌트를 그대로 재사용한다.
        /// </summary>
        private static CardHoverPreview BuildPreviewPanel(Transform parent)
        {
            var rect = BattleUIBuilder.CreateRect("PreviewPanel", parent,
                CardHoverPreview.PanelLeft, CardHoverPreview.PanelTop, CardHoverPreview.PanelWidth, CardHoverPreview.PanelHeight);
            var preview = rect.gameObject.AddComponent<CardHoverPreview>();
            preview.EditorBuild();
            return preview;
        }

        /// <summary>풀에 있는 모든 밴픽 카드에 프리뷰를 꽂는다.</summary>
        private static void WireHoverPreview(Transform canvasTf, CardHoverPreview preview)
        {
            foreach (var card in canvasTf.GetComponentsInChildren<BanPickCardView>(includeInactive: true))
                card.HoverPreview = preview;
        }

        // ===================== 밴픽 카드 (작업 3 — CharacterCardView와 같은 3레이어) =====================

        /// <summary>
        /// BanPickCard 더미 한 장. 프레임/일러스트/스크림/텍스트/오버레이는 전부
        /// <see cref="BanPickCardView.EditorBuild"/>가 스스로 만든다(CharacterCardView와 같은 자기조립 패턴) —
        /// 빌더는 위치만 잡고 컴포넌트를 얹는다. 예전엔 빌더가 자식을 직접 만들어서 뷰의 기대와
        /// 어긋나는 버그(PickMark/HpHex 겹침)가 났었는데, 이 구조에서는 애초에 그럴 일이 없다.
        /// </summary>
        private static void BuildBanPickCard(Transform parent, string name, float left, float top, float width, float height)
        {
            var root = BattleUIBuilder.CreateRect(name, parent, left, top, width, height);
            var view = root.gameObject.AddComponent<BanPickCardView>();
#if UNITY_EDITOR
            view.EditorBuild();
#endif
        }

        // ===================== 픽 슬롯 4칸 (SPEC 4번) =====================

        /// <summary>
        /// SPEC 4번: "하단 픽 슬롯 4칸 중앙 | bottom 30 | 각 130×200". 슬롯 사이 간격이 SPEC에 없어
        /// 20px로 임의 지정하고 그 기준으로 가로 중앙 정렬했다.
        /// </summary>
        private static void BuildSlotContainer(Transform parent)
        {
            const float slotWidth = 130f;
            const float slotHeight = 200f;
            const float gap = 20f; // SPEC에 명시 없음 — 임의 값, 실제 목업 확인되면 교체
            const int count = 4;

            float totalWidth = count * slotWidth + (count - 1) * gap;
            float startLeft = (1920f - totalWidth) / 2f;
            float top = 1080f - 30f - slotHeight; // bottom 30

            var container = BattleUIBuilder.CreateRect("SlotContainer", parent, 0, 0, 1920, 1080);
            for (int i = 0; i < count; i++)
            {
                float left = startLeft + i * (slotWidth + gap);
                BuildPickSlot(container, $"Slot{i}", left, top, slotWidth, slotHeight, i);
            }
        }

        private static void BuildPickSlot(Transform parent, string name, float left, float top, float width, float height, int position)
        {
            var root = BattleUIBuilder.CreateRect(name, parent, left, top, width, height);
            root.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.11f, 0.19f, 0.6f);
            root.gameObject.AddComponent<BanPickSlotView>();

            // EmptyState — 점선 테두리는 아트 없이 반투명 채움으로 근사 + P0~P3 라벨
            var emptyState = BattleUIBuilder.CreateRect("EmptyState", root, 0, 0, width, height);
            emptyState.gameObject.AddComponent<Image>().color = BattleUIBuilder.Hex("#FFFFFF", 0.06f);
            BattleUIBuilder.CreateLabel("Label", emptyState, 0, height / 2f - 10, width, 20, $"P{position}", 14, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#9AA6B8"));

            // FilledState — 픽 확정되면 초상화+이름 (BanPickSlotView가 토글)
            var filledState = BattleUIBuilder.CreateRect("FilledState", root, 0, 0, width, height);
            filledState.gameObject.AddComponent<Image>().color = BattleUIBuilder.Hex("#161D30");
            BattleUIBuilder.CreatePanel("Portrait", filledState, 0, 0, width, height * 0.7f, BattleUIBuilder.Hex("#28324D"));
            BattleUIBuilder.CreateLabel("NameText", filledState, 0, height - 26, width, 20, "", 12, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#EEF3FA"));
            filledState.gameObject.SetActive(false);
        }
    }
}
