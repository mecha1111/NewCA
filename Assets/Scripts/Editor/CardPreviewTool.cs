using System.IO;
using System.Linq;
using CrossAccel.Data;
using CrossAccel.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.EditorTools
{
    /// <summary>
    /// 조립된 카드를 PNG로 렌더해서 눈으로 확인하기 위한 도구.
    /// 배치모드에서 돌리려면 -nographics 없이 실행해야 한다 (렌더에 그래픽 디바이스가 필요).
    /// </summary>
    public static class CardPreviewTool
    {
        private static string OutputPath = "card_preview.png";

        [MenuItem("CrossAccel/Art/4. Render Card Preview (C14)")]
        public static void RenderC14()
        {
            OutputPath = "card_preview.png";
            Render("C14", 500);
        }

        /// <summary>일러스트가 없는 카드 — 프레임+텍스트만 나오는지, 텍스트가 프레임 라벨과 맞는지 확인용.</summary>
        [MenuItem("CrossAccel/Art/4b. Render Card Preview (C02, 일러스트 없음)")]
        public static void RenderC02()
        {
            OutputPath = "card_preview_noart.png";
            Render("C02", 500);
        }

        private static void Render(string cardId, int cardWidth)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var db = CardDatabaseProvider.Instance;
            if (!db.Characters.TryGetValue(cardId, out var data))
            {
                Debug.LogError($"[CardPreview] 카드 데이터 없음: {cardId}");
                return;
            }

            int cardHeight = Mathf.RoundToInt(CardArt.HeightFor(cardWidth));
            const int pad = 20;
            int texW = cardWidth + pad * 2;
            int texH = cardHeight + pad * 2;

            // 렌더용 카메라 + 캔버스
            var camGO = new GameObject("PreviewCamera", typeof(Camera));
            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            cam.cullingMask = ~0;

            var canvasGO = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var card = new GameObject($"Card_{cardId}", typeof(RectTransform));
            var rt = card.GetComponent<RectTransform>();
            rt.SetParent(canvasGO.transform, false);
            rt.sizeDelta = new Vector2(cardWidth, cardHeight);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var view = card.AddComponent<CharacterCardView>();
            view.EditorBuild();
            view.Bind(data);

            Canvas.ForceUpdateCanvases();

            // 렌더
            var rtex = new RenderTexture(texW, texH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rtex;
            cam.orthographicSize = texH / 2f;
            cam.Render();

            RenderTexture.active = rtex;
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, texW, texH), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(rtex);
            Object.DestroyImmediate(tex);

            var lib = CardArtLibrary.Instance;
            Debug.Log($"[CardPreview] {cardId} '{data.Name}' → {OutputPath} ({texW}x{texH}), " +
                      $"카드 {cardWidth}x{cardHeight}, 일러스트={(lib != null && lib.GetIllustration(cardId) != null ? "O" : "X")}");
            Debug.Log($"[CardPreview] 바인딩 값 — HP={data.MaxHp} 리치={data.Reach} 종족={data.Race} 무기={data.WeaponType}");
            Debug.Log($"[CardPreview] 조건='{data.EffectTiming}'  효과='{data.EffectText}'");
        }
    }
}
