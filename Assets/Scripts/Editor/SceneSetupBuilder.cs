using CrossAccel.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CrossAccel.EditorTools
{
    /// <summary>
    /// 씬 구조 세팅. BattleUIBuilder의 결과물을 Assets/Scenes/BattleScene.unity로 저장하고,
    /// 임시 로비 Assets/Scenes/MainMenuScene.unity를 새로 만든 뒤, 둘 다 Build Settings의
    /// Scenes In Build에 등록한다 (MainMenuScene = index 0, 빌드 시작 씬).
    ///
    /// 재실행하면 두 씬 파일을 덮어쓰고 Build Settings 목록도 다시 세팅한다 (멱등).
    /// </summary>
    public static class SceneSetupBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string BattleScenePath = ScenesFolder + "/BattleScene.unity";
        private const string MainMenuScenePath = ScenesFolder + "/MainMenuScene.unity";

        [MenuItem("CrossAccel/Setup Scenes (Battle + MainMenu)")]
        public static void Setup()
        {
            EnsureScenesFolder();
            BuildAndSaveBattleScene();
            BuildAndSaveMainMenuScene();
            RegisterBuildSettings();

            Debug.Log($"[SceneSetupBuilder] {MainMenuScenePath}(0), {BattleScenePath}(1) 생성 + Build Settings 등록 완료.");
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        /// <summary>새 빈 씬에 BattleUIBuilder로 배틀 UI 뼈대를 생성하고 저장한다.</summary>
        private static void BuildAndSaveBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BattleUIBuilder.Build();
            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        /// <summary>화면 중앙에 "게임 시작" 버튼 하나만 있는 임시 로비를 만들고 저장한다.</summary>
        private static void BuildAndSaveMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGO = BattleUIBuilder.BuildCanvas("MainMenuCanvas");
            BattleUIBuilder.EnsureEventSystem();
            BattleUIBuilder.EnsureMainCamera();

            var controller = new GameObject("MainMenuController", typeof(MainMenuController))
                .GetComponent<MainMenuController>();

            BuildStartButton(canvasGO.transform, controller);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void BuildStartButton(Transform canvasTf, MainMenuController controller)
        {
            const float width = 240f;
            const float height = 72f;

            var buttonGO = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(canvasTf, false);

            var rt = buttonGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; // 화면 정중앙
            rt.sizeDelta = new Vector2(width, height);

            buttonGO.GetComponent<Image>().color = BattleUIBuilder.Hex("#28324D"); // 카드 상단 색 재사용(임시)

            var button = buttonGO.GetComponent<Button>();
            // 런타임 AddListener는 씬에 저장되지 않으므로 퍼시스턴트 리스너로 연결해야 저장/빌드 후에도 동작한다.
            UnityEventTools.AddPersistentListener(button.onClick, controller.StartGame);

            var label = BattleUIBuilder.CreateLabel("Label", buttonGO.transform, 0, 0, width, height,
                "게임 시작", 28, TextAnchor.MiddleCenter, Color.white);
            // Label은 버튼 자식으로 붙었지만 CreateRect가 top-left 앵커를 강제하므로 버튼 중심에 맞춰 재배치.
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.anchoredPosition = Vector2.zero;
            labelRt.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// 씬 순서 등록은 PlaceSceneBuilder.RegisterAllScenes 하나에만 둔다 (모듈 A가 관리).
        /// [왜] 여기서 MainMenu+Battle 2개만 등록하면 Ban/Pick/Place가 목록에서 사라져
        ///      LoadScene(string)이 실패한다. 실제로 빌더 실행 순서에 따라 씬이 날아가는 문제가 있었다.
        /// </summary>
        private static void RegisterBuildSettings() => PlaceSceneBuilder.RegisterAllScenes();
    }
}
