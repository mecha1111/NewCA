// 담당 모듈: A (밴픽 공개화 + PlaceScene) — UNITY_PORTING_SPEC.md 4-2 / 5절
// 의존: BattleUIBuilder의 internal 헬퍼(CreateRect/CreatePanel/CreateLabel/Hex/BuildCanvas 등) 재사용
//       PlaceSceneController / PlaceSlotView (런타임 동작)

using CrossAccel.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.EditorTools
{
    /// <summary>
    /// [무엇] 배치 화면(PlaceScene)을 만들고 저장하는 에디터 도구. 씬 등록 순서의 단일 근거이기도 하다.
    /// [왜] 모듈 A가 새로 추가하는 화면이라 자체 빌더가 필요하다. 기존 빌더(BanPickUIBuilder /
    ///      BattleUIBuilder)는 모듈 B·D 담당이라 건드리지 않는다.
    /// [주의] 씬 인덱스는 여기 <see cref="RegisterAllScenes"/> 하나로만 정한다. 예전에 씬 이름을
    ///        여기저기 하드코딩해 밴픽을 통째로 건너뛰는 버그가 났었다.
    /// </summary>
    public static class PlaceSceneBuilder
    {
        // ── 좌표 상수 (캔버스 1920×1080, 좌상단 기준) ──────────────────────────
        // [출처] 이 파일 상단 설계 검산. 카드 크기는 CardArt(비율 0.6545 고정, SPEC 1절 8번)에서 계산.
        // [검산] 안내문 120~160 / 슬롯라벨 264~292 / 슬롯 300~529.2 / 후보 620~849.2 / 확정 960~1024
        //        → 세로로 어느 것도 겹치지 않는다. 라벨은 슬롯 위 8px 바깥에 있다.
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;

        private const float CardWidth = 150f;
        private static readonly float CardHeight = CardArt.HeightFor(CardWidth); // 229.2

        private const int SlotCount = 4;
        private const float ColumnGap = 40f;

        private const float SlotTop = 300f;
        private const float CandidateTop = 620f;

        private const float SlotLabelHeight = 28f;
        private const float SlotLabelGap = 8f;              // 라벨 하단 ↔ 슬롯 상단 여백

        private const float TitleTop = 60f;
        private const float InstructionTop = 120f;
        private const float HeaderHeight = 40f;

        private const float ConfirmWidth = 200f;
        private const float ConfirmHeight = 64f;
        private const float ConfirmMargin = 56f;            // 화면 우/하단에서 띄우는 여백

        private const float RevealTitleHeight = 40f;
        private const float RevealTitleGap = 8f;

        private const string ScenesFolder = "Assets/Scenes";
        private const string PlaceScenePath = ScenesFolder + "/PlaceScene.unity";
        private const string MainMenuScenePath = ScenesFolder + "/MainMenuScene.unity";
        private const string BanScenePath = ScenesFolder + "/BanScene.unity";
        private const string PickScenePath = ScenesFolder + "/PickScene.unity";
        private const string BattleScenePath = ScenesFolder + "/BattleScene.unity";

        private const string RootName = "PlaceUI";

        /// <summary>
        /// [무엇] 전체 씬 순서를 Build Settings에 등록한다.
        /// [왜] 씬 인덱스를 한 곳에서만 정하기 위해서다. 다른 빌더도 이 메서드를 부른다.
        /// [주의] 순서 = MainMenu(0) → Ban(1) → Pick(2) → <b>Place(3)</b> → Battle(4).
        ///        BanPickFlow의 씬 이름 상수와 짝이 맞아야 LoadScene(string)이 동작한다.
        /// </summary>
        public static void RegisterAllScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true), // 0
                new EditorBuildSettingsScene(BanScenePath, true),      // 1
                new EditorBuildSettingsScene(PickScenePath, true),     // 2
                new EditorBuildSettingsScene(PlaceScenePath, true),    // 3 — 신규
                new EditorBuildSettingsScene(BattleScenePath, true)    // 4 — 3에서 밀림
            };
        }

        /// <summary>[무엇] PlaceScene을 새로 만들어 저장하고 씬 순서를 재등록한다.</summary>
        [MenuItem("CrossAccel/BanPick/Setup Place Scene")]
        public static void SetupScene()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildContent();
            EditorSceneManager.SaveScene(scene, PlaceScenePath);

            RegisterAllScenes();
            Debug.Log($"[PlaceSceneBuilder] {PlaceScenePath} 생성 + 씬 순서 재등록 (Place=3, Battle=4)");
        }

        private static void BuildContent()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var canvasGO = BattleUIBuilder.BuildCanvas(RootName);
            var root = canvasGO.transform;

            BattleUIBuilder.CreatePanel("Background", root, 0, 0, CanvasWidth, CanvasHeight,
                BattleUIBuilder.Hex("#0A0E18")).raycastTarget = false;

            BattleUIBuilder.CreateLabel("TitleText", root, 0, TitleTop, CanvasWidth, HeaderHeight,
                "출전 순서 배치", 24, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#EEF3FA"));
            BattleUIBuilder.CreateLabel("InstructionText", root, 0, InstructionTop, CanvasWidth, HeaderHeight,
                "", 18, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#9AA6B8"));

            BuildSlots(root);
            BuildCandidates(root);
            BuildConfirmButton(root);
            BuildRevealOverlay(root);

            BattleUIBuilder.EnsureEventSystem();
            BattleUIBuilder.EnsureMainCamera();

            canvasGO.AddComponent<PlaceSceneController>();

            Selection.activeGameObject = canvasGO;
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        }

        /// <summary>[무엇] i번째 칸의 좌측 x. [왜] 슬롯·후보·공개연출이 같은 열 좌표를 쓰도록 한 곳에서 계산한다.</summary>
        private static float ColumnLeft(int index)
        {
            float total = SlotCount * CardWidth + (SlotCount - 1) * ColumnGap;   // 720
            float start = (CanvasWidth - total) * 0.5f;                          // 600
            return start + index * (CardWidth + ColumnGap);                      // 600/790/980/1170
        }

        /// <summary>
        /// [무엇] P1~P4 슬롯 4칸 + 각 슬롯 바깥 위 라벨.
        /// [왜] 명세 4-2: 라벨은 슬롯 바깥 위에 두고 카드와 겹치면 안 된다.
        /// [주의] <b>화면 오른쪽이 P1(전방)</b>이다. 슬롯 배열 인덱스 0=P1이므로 화면 열은 역순으로 잡는다.
        ///        (열 0=가장 왼쪽=P4 … 열 3=가장 오른쪽=P1)
        /// </summary>
        private static void BuildSlots(Transform parent)
        {
            var slotRoot = BattleUIBuilder.CreateRect("SlotRoot", parent, 0, 0, CanvasWidth, CanvasHeight);

            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                // slotIndex 0(P1)을 가장 오른쪽 열에 놓는다.
                int column = SlotCount - 1 - slotIndex;
                float left = ColumnLeft(column);

                string label = slotIndex == 0 ? "P1 · 전방"
                    : slotIndex == SlotCount - 1 ? $"P{SlotCount} · 후방"
                    : $"P{slotIndex + 1}";

                // 라벨: 슬롯 상단보다 (라벨높이+간격) 만큼 위 → 슬롯과 겹치지 않는다.
                BattleUIBuilder.CreateLabel($"SlotLabel{slotIndex}", slotRoot,
                    left, SlotTop - SlotLabelGap - SlotLabelHeight, CardWidth, SlotLabelHeight,
                    label, 15, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#31C5E0"));

                BuildSlotBox(slotRoot, $"Slot{slotIndex}", left, SlotTop, emptyText: "비어 있음");
            }
        }

        /// <summary>[무엇] 픽한 4장을 놓아두는 후보 줄.</summary>
        private static void BuildCandidates(Transform parent)
        {
            var candidateRoot = BattleUIBuilder.CreateRect("CandidateRoot", parent, 0, 0, CanvasWidth, CanvasHeight);

            for (int i = 0; i < SlotCount; i++)
                BuildSlotBox(candidateRoot, $"Candidate{i}", ColumnLeft(i), CandidateTop, emptyText: "");
        }

        /// <summary>
        /// [무엇] 칸 하나(빈칸/채움 두 상태 + 클릭)를 만든다. 슬롯과 후보가 같은 구조를 쓴다.
        /// [주의] 자식 이름(EmptyState/FilledState/NameText/InfoText)은 PlaceSlotView가 찾는 이름이다.
        ///        <b>PlaceSlotView.cs는 건드리지 않는다</b> — 이 이름·타입(GameObject/Text)만 지키면
        ///        내부 구성은 자유롭다. PlaceSlotView는 "FilledState/Portrait"를 직접 참조하지 않는다
        ///        (읽어보면 EmptyState/FilledState/FilledState·NameText/FilledState·InfoText 4개만 Find한다) —
        ///        그래서 Portrait 내부를 카드 아트로 바꿔도 로직 변경이 필요 없다.
        /// [주의] NameText/InfoText는 반드시 UnityEngine.UI.Text여야 한다 — PlaceSlotView.Awake가
        ///        GetComponent&lt;Text&gt;()로 찾는다(TMP로 바꾸면 null이 되어 배치해도 이름이 안 보인다).
        /// </summary>
        private static void BuildSlotBox(Transform parent, string name, float left, float top, string emptyText)
        {
            var box = BattleUIBuilder.CreatePanel(name, parent, left, top, CardWidth, CardHeight,
                new Color(0.09f, 0.11f, 0.19f, 1f));
            box.gameObject.AddComponent<Button>();
            box.gameObject.AddComponent<CanvasGroup>();   // 공개 연출에서 알파를 다루기 위해

            var empty = BattleUIBuilder.CreateRect("EmptyState", box.transform, 0, 0, CardWidth, CardHeight);
            empty.gameObject.AddComponent<Image>().color = BattleUIBuilder.Hex("#FFFFFF", 0.06f);
            BattleUIBuilder.CreateLabel("Label", empty, 0, CardHeight * 0.5f - 12f, CardWidth, 24,
                emptyText, 13, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#9AA6B8"));

            var filled = BattleUIBuilder.CreateRect("FilledState", box.transform, 0, 0, CardWidth, CardHeight);
            // 카드 배경(--card-mid 톤) — 정식 카드들과 같은 어두운 표면.
            filled.gameObject.AddComponent<Image>().color = BattleUIBuilder.Hex("#161D30");

            BuildPortrait(filled);
            BuildFilledTexts(filled);

            filled.gameObject.SetActive(false);

            // [버그 수정] PlaceSlotView는 원래 자식(EmptyState/FilledState 등)보다 먼저 AddComponent됐다.
            // Unity 에디터에서 AddComponent는 그 GameObject가 활성 상태면 Awake()를 그 자리에서 즉시 부른다 —
            // 그래서 Awake의 transform.Find("EmptyState") 등이 전부 null을 봤다(자식이 아직 없었으므로).
            // SetCharacter/SetEmpty/SetHidden이 전부 null 체크로 조용히 무시돼 "배치해도 화면이 안 바뀌는"
            // 버그였다 — 이번 작업 검증 렌더에서 발견했다. PlaceSlotView.cs는 그대로 두고, 컴포넌트를
            // 자식이 다 만들어진 뒤에 붙이도록 순서만 바꿨다.
            box.gameObject.AddComponent<PlaceSlotView>();
        }

        /// <summary>
        /// [무엇] "Portrait" — 배치 화면엔 어떤 캐릭터가 놓일지 빌드 시점엔 알 수 없어(로직을 안 건드리는 한
        /// PlaceSlotView가 넘겨주지 않는다) 캐릭터별 일러스트는 못 넣는다. 대신 CardArtLibrary의 실제
        /// 하단 스크림 에셋(작업 1에서 만든 그 PNG)을 그대로 재사용해 다른 정식 카드들과 같은
        /// "아래로 갈수록 어두워지는" 표면을 만든다 — 밋밋한 사각 패널보다 정식 카드에 가깝다.
        /// [검산] scale=폭150/1000=0.15. 스크림 y[132,229.2](카드 하단 42.4%) — NameText(136.5~160.5)·
        ///        InfoText(164.5~204.5) 전부 스크림 위에 있어 배경이 어두운 채로 읽힌다.
        /// </summary>
        private static void BuildPortrait(Transform filledState)
        {
            float scale = CardArt.Scale(CardWidth);
            var scrim = BattleUIBuilder.CreateRect("Portrait", filledState,
                CardArt.BottomScrim.Left * scale, CardArt.BottomScrim.Top * scale,
                CardArt.BottomScrim.Width * scale, CardArt.BottomScrim.Height * scale);

            var image = scrim.gameObject.AddComponent<Image>();
            var library = CardArtLibrary.Instance;
            if (library != null && library.BottomScrim != null) image.sprite = library.BottomScrim;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        /// <summary>
        /// NameText/InfoText — 위치를 CardArt.Name(캐릭터 카드·밴픽 픽카드와 같은 좌표계, 폭 150 기준
        /// 스케일)에 맞춰 스크림 위로 옮겼다. 내용(텍스트)은 PlaceSlotView.SetCharacter가 그대로 채운다.
        /// </summary>
        private static void BuildFilledTexts(Transform filledState)
        {
            float scale = CardArt.Scale(CardWidth);
            float nameTop = CardArt.Name.Top * scale;      // 136.5
            const float nameHeight = 24f;
            const float infoGap = 4f;
            const float infoHeight = 40f;

            BattleUIBuilder.CreateLabel("NameText", filledState, 0, nameTop, CardWidth, nameHeight,
                "", 13, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#EEF3FA"));
            BattleUIBuilder.CreateLabel("InfoText", filledState, 4, nameTop + nameHeight + infoGap, CardWidth - 8, infoHeight,
                "", 11, TextAnchor.UpperCenter, BattleUIBuilder.Hex("#9AA6B8"));
        }

        private static void BuildConfirmButton(Transform parent)
        {
            float left = CanvasWidth - ConfirmMargin - ConfirmWidth;    // 1664
            float top = CanvasHeight - ConfirmMargin - ConfirmHeight;   // 960

            var button = BattleUIBuilder.CreatePanel("ConfirmButton", parent, left, top, ConfirmWidth, ConfirmHeight,
                BattleUIBuilder.Hex("#31C5E0"));
            button.gameObject.AddComponent<Button>();
            BattleUIBuilder.CreateLabel("Label", button.transform, 0, 0, ConfirmWidth, ConfirmHeight,
                "배치 확정 ▶", 18, TextAnchor.MiddleCenter, Color.black);
        }

        /// <summary>
        /// [무엇] 상대 파티 공개 연출용 전체 오버레이 (기본 비활성).
        /// [주의] 카드 세로 위치는 화면 정중앙 기준으로 계산해 제목과 겹치지 않게 둔다.
        ///        제목 하단 = 카드 상단 - 8.
        /// </summary>
        private static void BuildRevealOverlay(Transform parent)
        {
            var overlay = BattleUIBuilder.CreatePanel("RevealOverlay", parent, 0, 0, CanvasWidth, CanvasHeight,
                new Color(0.02f, 0.03f, 0.06f, 0.95f));

            float cardTop = (CanvasHeight - CardHeight) * 0.5f;   // 425.4
            BattleUIBuilder.CreateLabel("Title", overlay.transform, 0,
                cardTop - RevealTitleGap - RevealTitleHeight, CanvasWidth, RevealTitleHeight,
                "상대 파티", 22, TextAnchor.MiddleCenter, BattleUIBuilder.Hex("#E0512F"));

            var revealRoot = BattleUIBuilder.CreateRect("RevealRoot", overlay.transform, 0, 0, CanvasWidth, CanvasHeight);
            for (int i = 0; i < SlotCount; i++)
                BuildSlotBox(revealRoot, $"Reveal{i}", ColumnLeft(i), cardTop, emptyText: "");

            overlay.gameObject.SetActive(false);
        }
    }
}
