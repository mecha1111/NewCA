using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] CardArt 좌표표(원본 1000×1528 px)를 실제 Image/TMP 오브젝트로 조립하는 공용 헬퍼.
    /// [왜] CharacterCardView(배틀 카드)와 BanPickCardView(밴픽 카드)가 "프레임/일러스트/스크림+텍스트"
    ///      3레이어를 같은 방식으로 조립해야 한다(밴픽 카드 3레이어화 지시, UNITY_PORTING_SPEC 5절).
    ///      원래 CharacterCardView 안에 있던 private 헬퍼를 꺼내 공용화한 것 — 로직 변경 없음.
    /// [주의] scale은 항상 <see cref="CardArt.Scale"/>(폭 기준 단일 배율)을 써야 한다. 폭·높이를
    ///        따로 배율화하면 카드가 왜곡된다(CARD_UI_FIX_SPEC.md "비율 0.6545 고정, 왜곡 금지").
    /// </summary>
    internal static class CardArtBuilder
    {
        /// <summary>프레임/일러스트처럼 카드 전체(부모 사각형과 1:1)를 덮는 레이어.</summary>
        public static Image CreateLayer(string name, RectTransform parent, bool raycastTarget = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = raycastTarget;
            image.preserveAspect = false;
            return image;
        }

        /// <summary>CardArt.Box(원본 px) 위치·크기를 갖는 Image — 스크림처럼 카드 일부만 덮는 것용.</summary>
        public static Image CreateBox(string name, RectTransform parent, CardArt.Box box, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(box.Left * scale, -box.Top * scale);
            rt.sizeDelta = new Vector2(box.Width * scale, box.Height * scale);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>CardArt.Box(원본 px) → 배율 적용한 TMP 텍스트. 앵커는 좌상단 기준.</summary>
        public static TextMeshProUGUI CreateText(string name, RectTransform parent, CardArt.Box box, float scale,
            float fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(box.Left * scale, -box.Top * scale);
            rt.sizeDelta = new Vector2(box.Width * scale, box.Height * scale);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize * scale;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }
    }
}
