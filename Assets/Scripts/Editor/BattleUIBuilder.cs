using System.Collections.Generic;
using System.Linq;
using CrossAccel.Data;
using CrossAccel.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrossAccel.EditorTools
{
    /// <summary>
    /// docs/UNITY_BATTLE_UI_SPEC.md 1단계 — 배틀 UI 뼈대.
    /// 메뉴 CrossAccel/Build Battle UI 로 실행하면 명세서 3번 좌표표 그대로
    /// Canvas + 정적 존(매트/중앙선/덱/트래쉬/코스트/프로필/Phase패널/프리뷰패널) +
    /// 캐릭터 카드 8칸 + 액트 슬롯 8칸을 현재 열린 씬에 생성한다.
    ///
    /// ⚠️ 이번 단계 범위 (다음 단계에서 채울 것):
    /// - 프로젝트에 TextMeshPro 패키지가 없어 텍스트는 uGUI 레거시 Text로 대체했다.
    ///   (Window ▸ TextMeshPro ▸ Import TMP Essential Resources 후 SkillCardView 등에서 TMP로 교체 예정)
    /// - CharacterCard/ActSlot은 명세서 4번의 자식 구조(Portrait/HpHex/ReachHex 등)를 이름은
    ///   그대로 따르되, 실제 육각형·아이콘 아트 에셋이 없어 사각 패널로 근사했다.
    /// - 손패(SkillCard + HandFanLayout)와 CardHoverPreview는 3단계 이후 작업 — 여기서는
    ///   프리뷰 패널만 자리를 잡아두고 비활성 상태로 둔다.
    /// - 캐릭터 카드에 에디터 시점에 굽는 값(스타터 덱 앞 4장)은 씬을 열었을 때 빈 카드가 아니게 하려는
    ///   프리뷰일 뿐이다. 실행하면 BattleUIController가 실제 밴픽 결과(GameManager의 CharacterZone)로
    ///   전부 덮어쓴다.
    /// - .prefab 에셋으로 저장하지 않고 씬에 직접 생성한다 — 아트가 들어오면 프리팹화.
    /// </summary>
    public static class BattleUIBuilder
    {
        private const string RootName = "BattleUI";
        private const float CardWidth = 126f;

        /// <summary>액트 슬롯 높이 (UNITY_BATTLE_UI_SPEC.md 3번: 126×194).</summary>
        private const float CardHeight = 194f;

        /// <summary>액트 슬롯 취소(X) 버튼 한 변. [주의] 명세에 크기 없음 — 카드 폭 대비 눈에 띄되 이름을 안 가리는 값.</summary>
        private const float CancelSize = 20f;

        /// <summary>액트 슬롯 신속 토글 높이. [주의] 명세에 크기 없음.</summary>
        private const float SwiftHeight = 18f;

        /// <summary>
        /// 캐릭터 카드 높이 — 실제 아트 비율(1000×1528 = 0.6545)을 따른다.
        /// 194로 두면 일러스트가 세로로 약 0.8% 늘어난다 (docs/CARD_ASSET_SPEC.md 4번).
        /// </summary>
        private static readonly float CharacterCardHeight = CardArt.HeightFor(CardWidth);

        [MenuItem("CrossAccel/Build Battle UI")]
        public static void Build()
        {
            // 다시 실행해도 깨끗하게 재생성되도록 이전 결과물을 지운다.
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Debug.Log($"[BattleUIBuilder] 기존 '{RootName}' 제거 후 재생성");
                Undo.DestroyObjectImmediate(existing);
            }

            var canvasGO = BuildCanvas();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build Battle UI");

            var canvasTf = canvasGO.transform;
            BuildBackground(canvasTf);
            BuildMats(canvasTf);
            BuildDecks(canvasTf);
            BuildTrashAndCostZones(canvasTf);
            BuildProfiles(canvasTf);
            BuildPhasePanel(canvasTf);
            var hoverPreview = BuildPreviewPanel(canvasTf);
            BuildCharacterAndActZones(canvasTf);
            BuildHand(canvasTf);
            WireHoverPreview(canvasTf, hoverPreview);

            EnsureEventSystem();
            EnsureMainCamera();

            // 런타임 총괄 — GameManager 상태를 카드/패널에 반영하고 "다음 ▶"으로 페이즈를 진행한다.
            canvasGO.AddComponent<BattleUIController>();

            Selection.activeGameObject = canvasGO;
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);

            Debug.Log("[BattleUIBuilder] 배틀 UI 뼈대 생성 완료 (1단계).");
        }

        // ===================== Canvas =====================
        // BuildCanvas/EnsureEventSystem/CreateRect/CreatePanel/CreateLabel/Hex는 internal —
        // SceneSetupBuilder(같은 폴더의 다른 Editor 도구)도 MainMenuScene을 만들 때 재사용한다.

        internal static GameObject BuildCanvas(string name = RootName)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // SPEC 1번: Scale With Screen Size, 1920x1080, Match Width Or Height = 0.5
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return go;
        }

        internal static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Build Battle UI");
        }

        /// <summary>
        /// Screen Space Overlay 캔버스는 카메라 없이도 그려지지만, 씬에 카메라가 하나도 없으면
        /// Game 뷰가 "No cameras rendering"만 띄우고 아무것도 그리지 않는다. UI만 비추면 되므로
        /// Orthographic + 아무것도 안 그리는 최소 카메라 하나만 둔다.
        /// </summary>
        internal static void EnsureMainCamera()
        {
            if (UnityEngine.Object.FindAnyObjectByType<Camera>() != null) return;

            var camGO = new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";

            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#0A0E18"); // 배경 패널과 동일 톤 — UI 밖 여백 대비용
            cam.cullingMask = 0; // UI(Overlay)는 카메라 없이 그려지므로 3D 컬링은 아무것도 안 비춤

            Undo.RegisterCreatedObjectUndo(camGO, "Build Battle UI");
        }

        // ===================== 배경 / 매트 / 중앙선 (SPEC 3번) =====================

        private static void BuildBackground(Transform parent)
        {
            var img = CreatePanel("Background", parent, 0, 0, 1920, 1080, Hex("#0A0E18"));
            img.raycastTarget = false;
        }

        private static void BuildMats(Transform parent)
        {
            // 상대 매트 946,2,580,430 — 빨강 그라데이션 근사(단색), 위쪽 테두리는 다음 단계에서 스프라이트로.
            var opponentMat = CreatePanel("OpponentMat", parent, 946, 2, 580, 430, Hex("#E0512F", 0.18f));
            opponentMat.raycastTarget = false;

            // 내 매트 394,444,580,430 — 청록 그라데이션 근사.
            var myMat = CreatePanel("MyMat", parent, 394, 444, 580, 430, Hex("#31C5E0", 0.18f));
            myMat.raycastTarget = false;

            // 중앙선 394,437,1132,2
            var centerLine = CreatePanel("CenterLine", parent, 394, 437, 1132, 2, Hex("#EEF3FA", 0.4f));
            centerLine.raycastTarget = false;
        }

        // ===================== 덱 (SPEC 3번, Z회전 포함) =====================

        private static void BuildDecks(Transform parent)
        {
            // 상대 덱 -30,-175,250,385, Z회전 162°
            var opponentDeck = CreateRotatedPanel("OpponentDeck", parent, -30, -175, 250, 385, 162f, Hex("#161D30"));
            CreateLabel("Label", opponentDeck.transform, 0, 0, 250, 385, "상대 덱", 20, TextAnchor.MiddleCenter, Hex("#9AA6B8"));

            // 내 덱 1700,870,250,385, Z회전 -18°
            var myDeck = CreateRotatedPanel("MyDeck", parent, 1700, 870, 250, 385, -18f, Hex("#161D30"));
            CreateLabel("Label", myDeck.transform, 0, 0, 250, 385, "내 덱", 20, TextAnchor.MiddleCenter, Hex("#9AA6B8"));
        }

        /// <summary>
        /// SPEC 2번의 Pivot=(0,1) 일반 규칙은 "위치 변환"용이다. 회전까지 모서리 기준으로 하면
        /// CSS `transform: rotate()`(transform-origin 기본값 = 중심)와 달라져 덱이 화면 밖으로
        /// 튀어나가 보인다 — 손패 섹션이 "Pivot=(0.5,0)"을 명시적으로 예외 표기한 것도 같은 이유.
        /// 그래서 회전이 있는 요소는 (left,top,width,height)로 정한 위치는 그대로 두되, 피벗과
        /// anchoredPosition만 중심으로 바꿔서 CSS 기본 동작(제자리에서 회전)과 맞춘다.
        /// </summary>
        private static Image CreateRotatedPanel(string name, Transform parent, float left, float top, float width, float height,
            float zRotationDegrees, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(left + width / 2f, -top - height / 2f);
            rt.sizeDelta = new Vector2(width, height);
            rt.localEulerAngles = new Vector3(0, 0, zRotationDegrees);

            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        // ===================== 트래쉬 / 코스트 (SPEC 3번) =====================

        private static void BuildTrashAndCostZones(Transform parent)
        {
            BuildLabeledPanel(parent, "OpponentTrash", 24, 250, 140, 95, "상대 트래쉬");
            BuildLabeledPanel(parent, "MyTrash", 1756, 735, 140, 95, "내 트래쉬");
            BuildLabeledPanel(parent, "OpponentCost", 204, 250, 140, 95, "상대 코스트");
            BuildLabeledPanel(parent, "MyCost", 1576, 735, 140, 95, "내 코스트");
        }

        // ===================== 프로필 (SPEC 3번) =====================

        private static void BuildProfiles(Transform parent)
        {
            BuildLabeledPanel(parent, "OpponentProfile", 1656, 20, 240, 80, "상대 프로필");
            BuildLabeledPanel(parent, "MyProfile", 24, 980, 240, 80, "내 프로필");
        }

        // ===================== Phase 패널 (SPEC 3번, 5번) =====================

        /// <summary>
        /// Phase 패널 — 헤더 + 페이즈 진행바 + 코스트/신속 pip + 로그 + 버튼 (UNITY_PORTING_SPEC 4-8).
        ///
        /// [왜 패널이 커졌나] 예전엔 (1756,355,140×340)에 텍스트만 있었다. 4-8이 요구하는 진행바와
        /// pip을 넣으려면 세로가 모자라 위로 늘렸다 — 위쪽은 상대 프로필(y20~100), 아래쪽은
        /// 내 트래쉬(y735~)가 한계라 그 사이(110~715)를 꽉 쓴다.
        ///
        /// [검산] 패널 x[1756,1896] y[110,715]
        ///   내부: Title[10,30] Phase[34,60] Turn[62,80] Step0~3[90,214] CostLabel[222,238]
        ///         CostPips[240,280] SwiftLabel[286,302] SwiftPips[304,322] Result[330,366]
        ///         Log[374,530] NextButton[545,589] — 전부 세로로 분리, 하단 여백 16px
        ///   화면: 상대프로필(y~100)과 10px, 내트래쉬(y735~)와 20px, 내코스트(x~1716)와 40px 간격.
        ///         상대 액트/캐릭터 3열(x~1512)과는 244px — 어느 것과도 겹치지 않는다.
        /// </summary>
        private static void BuildPhasePanel(Transform parent)
        {
            const float width = 140f;
            const float height = 605f;
            const float inset = 6f;
            const float innerWidth = width - inset * 2f;   // 128

            var panel = CreatePanel("PhasePanel", parent, 1756, 110, width, height,
                new Color(22 / 255f, 28 / 255f, 42 / 255f, 0.85f));

            CreateLabel("Title", panel.transform, 0, 10, width, 20, "PHASE", 11, TextAnchor.MiddleCenter, Hex("#9AA6B8"));
            CreateLabel("PhaseText", panel.transform, 0, 34, width, 26, "-", 16, TextAnchor.MiddleCenter, Hex("#31D0F0"));
            CreateLabel("TurnText", panel.transform, 0, 62, width, 18, "", 12, TextAnchor.MiddleCenter, Hex("#9AA6B8"));

            BuildPhaseStepBar(panel.transform, inset, innerWidth);
            BuildResourceRows(panel.transform, inset, innerWidth);

            CreateLabel("ResultText", panel.transform, 0, 330, width, 36, "", 14, TextAnchor.MiddleCenter, Hex("#F2C934"));

            // 진행 로그 — 프로토타입의 8줄 로그와 같은 역할 (불가 사유·발동 순서를 유저에게 알린다).
            CreateLabel("LogText", panel.transform, inset, 374, innerWidth, 156, "", 9,
                TextAnchor.UpperLeft, Hex("#9AA6B8"));

            // "다음 ▶" — 페이즈를 한 단계씩 진행. 실제 동작은 BattleUIController가 런타임에 연결한다.
            const float buttonWidth = 116f;
            const float buttonHeight = 44f;
            var button = CreatePanel("NextButton", panel.transform,
                (width - buttonWidth) / 2f, 545, buttonWidth, buttonHeight, Hex("#31D0F0"));
            button.gameObject.AddComponent<Button>();
            CreateLabel("Label", button.transform, 0, 0, buttonWidth, buttonHeight, "다음 ▶", 15, TextAnchor.MiddleCenter, Color.black);
        }

        /// <summary>
        /// 페이즈 진행바 — 드로우/레디/액션/엔드 4칸. 발광 전환은 PhaseStepBar(모듈 D)가 런타임에 한다.
        /// [주의] 프로토타입은 가로 4칸인데 이 패널은 폭이 140뿐이라 한 칸이 35px밖에 안 된다
        ///        ("드로우" 세 글자가 안 들어간다) — 세로로 쌓았다. 순서·의미는 그대로다.
        /// </summary>
        private static void BuildPhaseStepBar(Transform panel, float inset, float innerWidth)
        {
            // [출처] full_prototype.html setBattlePhase order = ['draw','ready','action','end']
            string[] labels = { "드로우", "레디", "액션", "엔드" };
            const float stepTop = 90f;
            const float stepHeight = 28f;
            const float stepGap = 4f;

            var bar = CreateRect("PhaseStepBar", panel, 0, 0, 140, 605);
            bar.gameObject.AddComponent<PhaseStepBar>();

            for (int i = 0; i < labels.Length; i++)
            {
                float top = stepTop + i * (stepHeight + stepGap);
                var step = CreatePanel($"Step{i}", bar, inset, top, innerWidth, stepHeight,
                    new Color(10 / 255f, 14 / 255f, 24 / 255f, 0.5f));   // .pstep2 기본 배경
                step.raycastTarget = false;
                CreateLabel("Label", step.transform, 0, 0, innerWidth, stepHeight, labels[i], 11,
                    TextAnchor.MiddleCenter, Hex("#5B6B8C"));
            }
        }

        /// <summary>
        /// 코스트/신속 pip 줄. 값 반영은 ResourcePips(모듈 D)가 런타임에 한다.
        /// [왜] 코스트는 소멸이 아니라 레스트(꺾임)라서(UNITY_PORTING_SPEC 4-3) 숫자보다 pip이 맞다.
        /// </summary>
        private static void BuildResourceRows(Transform panel, float inset, float innerWidth)
        {
            CreateLabel("CostLabel", panel, inset, 222, innerWidth, 16, "코스트", 10, TextAnchor.MiddleLeft, Hex("#9AA6B8"));
            var costPips = CreateRect("CostPips", panel, inset, 240, innerWidth, 40);
            costPips.gameObject.AddComponent<ResourcePips>();

            CreateLabel("SwiftLabel", panel, inset, 286, innerWidth, 16, "신속", 10, TextAnchor.MiddleLeft, Hex("#9AA6B8"));
            var swiftPips = CreateRect("SwiftPips", panel, inset, 304, innerWidth, 18);
            swiftPips.gameObject.AddComponent<ResourcePips>();
        }

        // ===================== 프리뷰 패널 (작업 4 — CardHoverPreview, 좌표는 full_prototype.html .pv-panel) ====

        /// <summary>
        /// 카드 호버 프리뷰. 좌표는 CardHoverPreview.PanelXxx 상수(= full_prototype.html 그대로) — 여기서
        /// 다시 적지 않는다. 기본 비활성(CardHoverPreview.Hide가 초기 상태).
        /// </summary>
        private static CardHoverPreview BuildPreviewPanel(Transform parent)
        {
            var rect = CreateRect("PreviewPanel", parent,
                CardHoverPreview.PanelLeft, CardHoverPreview.PanelTop, CardHoverPreview.PanelWidth, CardHoverPreview.PanelHeight);
            var preview = rect.gameObject.AddComponent<CardHoverPreview>();
            preview.EditorBuild();
            return preview;
        }

        /// <summary>모든 캐릭터 카드에 프리뷰를 꽂는다 — 카드는 어디에 프리뷰가 있는지 몰라도 된다.</summary>
        private static void WireHoverPreview(Transform canvasTf, CardHoverPreview preview)
        {
            foreach (var card in canvasTf.GetComponentsInChildren<CharacterCardView>(includeInactive: true))
                card.HoverPreview = preview;
            foreach (var card in canvasTf.GetComponentsInChildren<SkillCardView>(includeInactive: true))
                card.HoverPreview = preview;
        }

        // ===================== 캐릭터 존 / 액트 존 (SPEC 3번 표 2) =====================

        private static void BuildCharacterAndActZones(Transform parent)
        {
            // ── 캐릭터/액트 열 좌표 (index = CharacterZone 인덱스 = Position) ──────────────
            // [출처] full_prototype.html — 내 열 [810,650,490,330](우→좌), 상대 열 [960,1120,1280,1440](좌→우).
            //        실제 픽셀은 카드 폭이 다르므로(프로토 150 / 여기 126) 이 캔버스 기준으로 환산했다.
            // [왜] 확정 규칙(UNITY_PORTING_SPEC 4-2/4-8): <b>화면상 오른쪽 = P1 = 전방 = index 0</b>.
            //      배치 화면(PlaceSceneBuilder)이 P1을 가장 오른쪽 열에 놓으므로 전투도 같아야 한다.
            //      예전엔 내 열이 {408,550,692,834}로 index 0(최전선)을 <b>왼쪽</b>에 놓아, 배치에서 오른쪽에
            //      세운 P1이 전투에 들어가면 왼쪽 끝에 서 있는 것처럼 보였다(전후방이 뒤집힘).
            // [검산] 카드 폭 126, 중앙 접점 960 기준:
            //        내   index0 = [834,960] → 오른쪽 끝이 접점 960에 정확히 맞닿는다.
            //        상대 index0 = [960,1086] → 왼쪽 끝이 접점 960에 정확히 맞닿는다.
            //        → 양쪽 최전선이 접점에서 맞붙어 리치 거리 1(= pos0+pos0+1)이 화면상으로도 인접이다.
            //        점대칭: 접점까지의 거리가 idx별로 내/상대 동일 (63/205/347/489).
            //        칸 간격 142 − 카드폭 126 = 여백 16px, 겹침 없음.
            // [주의] 상대 열은 원래부터 index 0 = 960(접점)이라 방향이 맞았다 — 내 열만 뒤집었다.
            //        이 배열은 액트 슬롯도 같이 쓰므로(아래 루프) 캐릭터/액트 열 방향이 자동으로 일치한다.
            float[] opponentCols = { 960, 1102, 1244, 1386 };
            float[] myCols = { 834, 692, 550, 408 };

            // 실제 스타터 덱에서 미리보기 파티를 읽어온다 (UNITY_BANPICK_UI_SPEC.md 8번: 같은 CardDatabase 공유).
            // 아직 밴픽 결과가 없는 스켈레톤 단계라, 각 덱의 앞쪽 4장을 그대로 보여준다 —
            // 실제 확정 파티 바인딩은 BattleUIController(7단계)가 GameManager 상태로 대체.
            var myParty = LoadPreviewParty("Aggro");
            var opponentParty = LoadPreviewParty("MidRange_Blood");

            for (int i = 0; i < 4; i++)
            {
                BuildActSlot(parent, $"OpponentAct{i}", opponentCols[i], 16, i, opponent: true);
                BuildCharacterCard(parent, $"OpponentCharacter{i}", opponentCols[i], 224, i, opponentParty.ElementAtOrDefault(i));
                BuildCharacterCard(parent, $"MyCharacter{i}", myCols[i], 458, i, myParty.ElementAtOrDefault(i));
                BuildActSlot(parent, $"MyAct{i}", myCols[i], 666, i, opponent: false);
            }
        }

        /// <summary>스타터 덱의 캐릭터 카드만 최대 4장 — CardDatabaseProvider가 로드한 실데이터.</summary>
        private static List<CharacterData> LoadPreviewParty(string deckName)
        {
            var db = CardDatabaseProvider.Instance;
            var deck = db.Decks.FirstOrDefault(d => d.DeckName == deckName);
            if (deck == null) return new List<CharacterData>();

            return deck.Cards
                .Where(c => c.CardType == "Character")
                .Select(c => db.Characters.TryGetValue(c.CardId, out var data) ? data : null)
                .Where(d => d != null)
                .Take(4)
                .ToList();
        }

        /// <summary>
        /// CharacterCard — 실제 아트로 조립한다 (docs/CARD_ASSET_SPEC.md).
        /// 자식(프레임/일러스트/스크림/텍스트)은 CharacterCardView가 직접 만들고, 여기서는 크기만 잡은 뒤
        /// EditorBuild로 씬에 미리 구워 Play 전에도 카드가 보이게 한다.
        /// 카드 높이는 에셋 비율(0.6545)을 따른다 — 194로 두면 일러스트가 세로로 늘어난다.
        /// </summary>
        private static void BuildCharacterCard(Transform parent, string name, float left, float top, int position, CharacterData data)
        {
            var card = CreateRect(name, parent, left, top, CardWidth, CharacterCardHeight);

            // 클릭 대상이 되려면 Raycast를 받는 Graphic이 필요하다. 카드 아트는 전부 raycastTarget=false라
            // 투명 Image를 배경으로 깔아 클릭만 받게 한다 (모듈 C 레디 입력: 캐릭터 선택 / 타겟 지정).
            var hit = card.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            card.gameObject.AddComponent<Button>();

            var view = card.gameObject.AddComponent<CharacterCardView>();

            view.EditorBuild();
            if (data != null) view.Bind(data); // 에디터 프리뷰. 실행하면 실제 밴픽 결과로 덮인다.
        }

        /// <summary>ActSlot — 비었을 때 EmptyState, 카드가 놓이면 FilledState. 런타임 갱신은 ActSlotView가 담당.</summary>
        private static void BuildActSlot(Transform parent, string name, float left, float top, int position, bool opponent)
        {
            var slot = CreateRect(name, parent, left, top, CardWidth, CardHeight);
            slot.gameObject.AddComponent<ActSlotView>();

            // EmptyState — 점선 테두리는 아트 없이 반투명 테두리색 채움으로 근사.
            var emptyState = CreateRect("EmptyState", slot, 0, 0, CardWidth, CardHeight);
            var border = emptyState.gameObject.AddComponent<Image>();
            border.color = Hex("#FFFFFF", 0.06f);
            CreateLabel("Label", emptyState, 0, CardHeight / 2f - 10, CardWidth, 20, "스킬 대기", 12, TextAnchor.MiddleCenter, Hex("#9AA6B8"));

            // FilledState — 다음 단계(ActSlotView)에서 채움. 구조만 미리 잡아두고 비활성.
            var filledState = CreateRect("FilledState", slot, 0, 0, CardWidth, CardHeight);
            var filledBg = filledState.gameObject.AddComponent<Image>();
            filledBg.color = Hex("#161D30");
            var actCost = CreatePanel("CostCircle", filledState, 0, 0, 30, 30, Hex("#F2C934"));
            CreateLabel("Value", actCost.transform, 0, 0, 30, 30, "", 13, TextAnchor.MiddleCenter, Color.black);
            var actSpeed = CreatePanel("SpeedCircle", filledState, CardWidth - 30, 0, 30, 30, Hex("#31D0F0"));
            CreateLabel("Value", actSpeed.transform, 0, 0, 30, 30, "", 13, TextAnchor.MiddleCenter, Color.black);
            CreatePanel("ArtIcon", filledState, 0, 56, CardWidth, 80, Hex("#28324D"));
            CreateLabel("NameText", filledState, 0, FromBottom(CardHeight, 30, 20), CardWidth, 20, "", 12, TextAnchor.MiddleCenter, Hex("#EEF3FA"));
            var accelBadge = CreatePanel("AccelBadge", filledState, CardWidth / 2f - 20, 2, 40, 16, Hex("#31D0F0"));
            accelBadge.gameObject.SetActive(false);

            // 취소(X) — 우상단. 배치 취소 시 카드가 손패로 돌아간다 (모듈 C).
            var cancel = CreatePanel("CancelButton", filledState, CardWidth - CancelSize - 2, 2, CancelSize, CancelSize,
                Hex("#E0512F"));
            cancel.gameObject.AddComponent<Button>();
            CreateLabel("Label", cancel.transform, 0, 0, CancelSize, CancelSize, "X", 12, TextAnchor.MiddleCenter, Color.white);

            // 신속 토글(⚡) — 하단. 자원이 없으면 컨트롤러가 interactable=false로 끈다.
            var swift = CreatePanel("SwiftButton", filledState, 2, CardHeight - SwiftHeight - 2,
                CardWidth - 4, SwiftHeight, Hex("#31D0F0", 0.85f));
            swift.gameObject.AddComponent<Button>();
            // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: "⚡")
            CreateLabel("Label", swift.transform, 0, 0, CardWidth - 4, SwiftHeight, "신속", 11, TextAnchor.MiddleCenter, Color.black);

            filledState.gameObject.SetActive(false);
        }

        // ===================== 손패 (SPEC 3번 손패 절 / 4번 SkillCard) =====================

        /// <summary>
        /// 내 손패 — 카드 8장(SPEC의 최대치)을 미리 만들어두고 HandFanLayout이 매번 필요한 만큼만 켠다.
        /// 실제 좌표·회전은 전부 HandFanLayout이 계산하므로 여기서는 자리만 잡는다.
        /// 상대 손패는 좌표가 명세서에 없어 만들지 않는다 (SPEC 4번의 뒷면 프리팹은 액트존에만 적용).
        /// </summary>
        private static void BuildHand(Transform parent)
        {
            var container = CreateRect("HandContainer", parent, 0, 0, 1920, 1080);
            container.gameObject.AddComponent<HandFanLayout>();

            for (int i = 0; i < HandFanLayout.MaxCards; i++)
                BuildSkillCard(container, $"HandCard{i}");
        }

        /// <summary>SkillCard (132×203) — SPEC 4번 자식 구조. Pivot은 하단 중심(부채꼴 회전 기준).</summary>
        private static void BuildSkillCard(Transform parent, string name)
        {
            const float width = HandFanLayout.CardWidth;
            const float height = HandFanLayout.CardHeight;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0f); // SPEC: 카드 하단 중심에서 회전
            rt.sizeDelta = new Vector2(width, height);

            go.AddComponent<Image>().color = new Color(0.09f, 0.11f, 0.19f);
            go.AddComponent<Button>();
            go.AddComponent<SkillCardView>();

            // ArtFrame — 상단 46%
            CreatePanel("ArtFrame", rt, 0, 0, width, height * 0.46f, Hex("#28324D"));

            // CostCircle / SpeedCircle — 35×35, 좌상단 / 우상단
            var cost = CreatePanel("CostCircle", rt, 4, 4, 35, 35, Hex("#F2C934"));
            CreateLabel("Value", cost.transform, 0, 0, 35, 35, "", 16, TextAnchor.MiddleCenter, Color.black);
            var speed = CreatePanel("SpeedCircle", rt, width - 4 - 35, 4, 35, 35, Hex("#31D0F0"));
            CreateLabel("Value", speed.transform, 0, 0, 35, 35, "", 16, TextAnchor.MiddleCenter, Color.black);

            // NameText — bottom 50
            CreateLabel("NameText", rt, 4, FromBottom(height, 50, 22), width - 8, 22, "", 12, TextAnchor.MiddleCenter, Hex("#EEF3FA"));

            // EffectText — bottom 8, 배경 있는 박스
            var effectBox = CreatePanel("EffectText", rt, 4, FromBottom(height, 8, 42), width - 8, 42, new Color(0, 0, 0, 0.45f));
            CreateLabel("Value", effectBox.transform, 3, 2, width - 14, 38, "", 9, TextAnchor.UpperLeft, Hex("#9AA6B8"));
        }

        // ===================== 공용 헬퍼 =====================

        /// <summary>bottom 오프셋을 우리 top-left 기준 top 값으로 환산 (SPEC 2번 좌표 변환 규칙).</summary>
        private static float FromBottom(float containerHeight, float bottom, float elementHeight) =>
            containerHeight - bottom - elementHeight;

        /// <summary>라벨 붙은 단순 패널 — 트래쉬/코스트/프로필/Phase/프리뷰처럼 세부 구조가 다음 단계인 존.</summary>
        private static Image BuildLabeledPanel(Transform parent, string name, float left, float top, float width, float height, string label)
        {
            // 패널 배경 rgba(22,28,42,0.85), 테두리는 아트 없이 생략(다음 단계에서 스프라이트로 추가).
            var panel = CreatePanel(name, parent, left, top, width, height, new Color(22 / 255f, 28 / 255f, 42 / 255f, 0.85f));
            CreateLabel("Label", panel.transform, 0, 0, width, height, label, 14, TextAnchor.MiddleCenter, Hex("#9AA6B8"));
            return panel;
        }

        /// <summary>
        /// SPEC 2번 좌표 변환 규칙 그대로: AnchorMin=Max=Pivot=(0,1)(좌상단),
        /// anchoredPosition=(left,-top), sizeDelta=(width,height).
        /// </summary>
        internal static RectTransform CreateRect(string name, Transform parent, float left, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        internal static Image CreatePanel(string name, Transform parent, float left, float top, float width, float height, Color color)
        {
            var rt = CreateRect(name, parent, left, top, width, height);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        // 게임 UI(카드 아닌 모든 것) 공용 라벨 — 폰트는 GameUIFont(카드용 KOHINanum과 분리) 참조.
        internal static Text CreateLabel(string name, Transform parent, float left, float top, float width, float height,
            string text, int fontSize, TextAnchor alignment, Color color)
        {
            var rt = CreateRect(name, parent, left, top, width, height);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = GameUIFont.Legacy;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        internal static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }
    }
}
