using UnityEngine;

namespace CrossAccel.UI
{
    /// <summary>
    /// 손패 부채꼴 배치 (UNITY_BATTLE_UI_SPEC.md 3번 손패 절).
    ///   t = (i - (n-1)/2) / ((n-1)/2),  rot = t * 12,  lift = -(1 - t²) * 18
    /// 카드 Pivot은 (0.5, 0) — 하단 중심에서 회전한다.
    /// 카드 수가 바뀔 때마다 <see cref="Layout"/>을 다시 부르면 된다.
    /// </summary>
    public class HandFanLayout : MonoBehaviour
    {
        public const float CardWidth = 132f;
        public const float CardHeight = 203f;
        public const int MaxCards = 8;

        private const float ContainerTop = 876f;
        private const float SideMargin = -6f;    // 좌우 마진 -6 → 이웃 카드와 12px 겹침
        private const float MaxRotation = 12f;
        private const float MaxLift = 18f;
        private const float CanvasWidth = 1920f;

        /// <summary>
        /// 명세서의 Z회전 표는 목업(HTML)의 CSS transform 값이다. CSS rotate()는 시계방향이 양수인데
        /// Unity의 Z 회전은 반시계방향이 양수라 부호가 반대다. 그대로 넣으면 카드 윗변이 안쪽으로
        /// 모여 부채가 오므라들어 보이므로 뒤집는다.
        /// (혹시 목업과 반대로 보이면 이 값만 +1f로 바꾸면 된다.)
        /// </summary>
        private const float CssToUnitySign = -1f;

        /// <summary>카드 사이 중심 간격 — 카드 폭에서 겹침(양쪽 마진)을 뺀 값.</summary>
        private static float Step => CardWidth + SideMargin * 2f;

        /// <summary>
        /// 자식 카드들을 부채꼴로 배치한다. visibleCount 개만 켜고 나머지는 끈다.
        /// </summary>
        public void Layout(RectTransform[] cards, int visibleCount)
        {
            if (cards == null) return;

            int n = Mathf.Clamp(visibleCount, 0, cards.Length);
            float totalWidth = n <= 0 ? 0f : n * CardWidth + (n - 1) * SideMargin * 2f;
            float startLeft = (CanvasWidth - totalWidth) * 0.5f;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                if (i >= n)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                cards[i].gameObject.SetActive(true);

                // n == 1이면 (n-1)/2 == 0이라 나눗셈이 성립하지 않는다 — 가운데 한 장으로 본다.
                float half = (n - 1) * 0.5f;
                float t = half <= 0f ? 0f : (i - half) / half;

                float rotation = t * MaxRotation * CssToUnitySign;
                float lift = -(1f - t * t) * MaxLift; // 음수 = 위로 (가운데가 가장 높다)

                float centerX = startLeft + i * Step + CardWidth * 0.5f;

                // Pivot이 하단 중심이므로 y는 카드 '아랫변'의 위치. 화면 좌표계가 아래로 음수라
                // lift(음수)를 빼면 위로 올라간다.
                cards[i].anchoredPosition = new Vector2(centerX, -(ContainerTop + CardHeight) - lift);
                cards[i].localEulerAngles = new Vector3(0f, 0f, rotation);
                cards[i].SetSiblingIndex(i); // 왼쪽 카드가 아래로 깔리도록 순서 유지

                // 호버 확대(모듈 D)가 기억해둔 "제자리"를 방금 바뀐 좌표로 무효화한다 —
                // 안 하면 손패가 바뀐 뒤 호버를 벗어날 때 예전 자리로 돌아가 버린다.
                cards[i].GetComponent<SkillCardView>()?.OnLayoutMoved();
            }
        }
    }
}