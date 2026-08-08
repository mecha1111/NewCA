// 담당 모듈: D (전투 비주얼/연출) — UNITY_PORTING_SPEC.md 4-3 / docs/full_prototype.html .pcpip / .swpip
// 경계: 총량·사용량은 엔진(코스트존 IsRested / SwiftResource)에서 읽은 값을 컨트롤러가 넘겨준다.
//       여기는 그 숫자를 pip으로 그리고 트랜지션만 준다. 자원 계산은 하지 않는다.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 코스트/신속 자원을 작은 칩(pip) 줄로 보여준다. 쓴 것은 꺾여서(회전) 흐려진다.
    /// [왜] RULES.md의 코스트는 "소멸"이 아니라 "레스트(꺾임)"이고 매 턴 언탭된다(UNITY_PORTING_SPEC
    ///      4-3). 숫자 "3/5"로는 그 감각이 안 살아서, 프로토타입처럼 카드를 눕히는 표현을 쓴다.
    /// [주의] pip 개수는 매 갱신마다 총량에 맞춰 늘리기만 하고 줄일 땐 비활성화한다 — 매번 파괴/생성하면
    ///        진행 중인 트윈이 끊긴다.
    /// </summary>
    public class ResourcePips : MonoBehaviour
    {
        // 크기 — 출처: full_prototype.html .pcpip i / .swpip i { width:13px; height:18px; gap:3px }
        private const float PipWidth = 13f;
        private const float PipHeight = 18f;
        private const float PipGap = 3f;

        /// <summary>한 줄에 넣을 최대 pip 수. [주의] 프로토타입은 flex-wrap이라 폭에 따라 자동인데,
        /// 여기 Phase 패널 폭(128)에 13+3=16px씩 넣으면 8개가 한계다 → 8개마다 줄바꿈.</summary>
        private const int PipsPerRow = 8;

        /// <summary>줄 간격 (pip 높이 + 여백). 프로토타입엔 값이 없어 4px로 정함(임의).</summary>
        private const float RowGap = 4f;

        /// <summary>
        /// 꺾임(레스트) 회전각. [출처] .pcpip i.spent { transform: rotate(70deg) }
        /// [주의] CSS 회전은 시계방향이 양수, Unity Z 회전은 반시계가 양수라 부호를 뒤집는다
        ///        (HandFanLayout.CssToUnitySign과 같은 이유 — 실제로 겪은 버그).
        /// </summary>
        private const float SpentRotationDegrees = -70f;

        /// <summary>pip 상태 전환 시간. [주의] 프로토타입 CSS엔 이 요소 transition이 없다 —
        /// "채워짐·꺾임 트랜지션" 지시에 맞춰 넣은 값이며, 옆 요소(.pstep2 .25s)보다 짧게 잡았다.</summary>
        private const float TransitionMs = 180f;

        // 색 — 출처: .pcpip i(금색 그라데이션 중간값) / .swpip i(보라) / i.spent(회색)
        private static readonly Color CostColor = new Color32(0xF2, 0xC9, 0x34, 0xFF);
        private static readonly Color SwiftColor = new Color32(0xA9, 0x78, 0xFF, 0xFF);
        private static readonly Color SpentColor = new Color32(0x2A, 0x31, 0x45, 0xFF);

        private readonly List<Image> _pips = new List<Image>();
        private readonly List<Coroutine> _tweens = new List<Coroutine>();
        private Color _fullColor = CostColor;

        /// <summary>
        /// [무엇] pip 색 계열을 정한다 (코스트=금색 / 신속=보라).
        /// [왜] 같은 컴포넌트를 두 자원에 재사용하되 색으로 구분한다 — 프로토타입도 마크업은 같고 색만 다르다.
        /// </summary>
        public void SetKind(bool swift) => _fullColor = swift ? SwiftColor : CostColor;

        /// <summary>
        /// [무엇] 총량 중 몇 개가 쓰였는지 반영한다 (가용 pip은 채워진 색, 쓴 pip은 꺾여서 회색).
        /// [왜] 엔진의 코스트존 총량·레스트 수를 그대로 그림으로 옮기는 것이 이 컴포넌트의 전부다.
        /// [주의] total/spent를 여기서 계산하거나 보정하지 않는다 — 넘어온 값이 곧 엔진 값이다.
        /// </summary>
        public void SetValues(int total, int spent)
        {
            EnsureCapacity(total);

            for (int i = 0; i < _pips.Count; i++)
            {
                bool used = i < total;
                _pips[i].gameObject.SetActive(used);
                if (!used) continue;

                bool isSpent = i < spent;
                TweenPip(i, isSpent ? SpentColor : _fullColor, isSpent ? SpentRotationDegrees : 0f);
            }
        }

        private void EnsureCapacity(int total)
        {
            while (_pips.Count < total)
            {
                int index = _pips.Count;
                int row = index / PipsPerRow;
                int col = index % PipsPerRow;

                var go = new GameObject($"Pip{index}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                // 회전이 있는 요소이므로 중심 피벗을 쓴다 (UNITY_PORTING_SPEC 1절 7번 — 모서리 피벗으로
                // 회전하면 CSS transform-origin 기본값과 달라져 pip이 제자리에서 안 눕는다).
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(
                    col * (PipWidth + PipGap) + PipWidth / 2f,
                    -(row * (PipHeight + RowGap) + PipHeight / 2f));
                rt.sizeDelta = new Vector2(PipWidth, PipHeight);

                var image = go.GetComponent<Image>();
                image.color = _fullColor;
                image.raycastTarget = false;

                _pips.Add(image);
                _tweens.Add(null);
            }
        }

        private void TweenPip(int index, Color targetColor, float targetRotation)
        {
            var image = _pips[index];
            var rt = (RectTransform)image.transform;

            // 이미 목표 상태면 트윈을 새로 걸지 않는다(매 갱신마다 재시작되면 영영 안 끝난다).
            if (Mathf.Approximately(rt.localEulerAngles.z, Mathf.Repeat(targetRotation, 360f))
                && image.color == targetColor)
                return;

            // [주의] 코루틴은 플레이 중에만 돈다. 에디터에서 씬을 구워 미리 볼 때(EditorBuild 경로)나
            //        오브젝트가 꺼져 있을 때 트윈을 걸면 영영 반영되지 않아 pip이 초기색으로 남는다
            //        — 실제로 검증 렌더에서 레스트 표시가 안 나와 발견했다. 그런 경우엔 즉시 적용한다.
            if (!Application.isPlaying || !gameObject.activeInHierarchy)
            {
                image.color = targetColor;
                rt.localEulerAngles = new Vector3(0, 0, targetRotation);
                return;
            }

            if (_tweens[index] != null) StopCoroutine(_tweens[index]);
            _tweens[index] = StartCoroutine(TweenRoutine(index, image, rt, targetColor, targetRotation));
        }

        private IEnumerator TweenRoutine(int index, Image image, RectTransform rt, Color targetColor, float targetRotation)
        {
            Color fromColor = image.color;
            float fromRotation = rt.localEulerAngles.z;
            if (fromRotation > 180f) fromRotation -= 360f; // -70° 가 290° 로 읽히는 것 보정

            float duration = TransitionMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                image.color = Color.Lerp(fromColor, targetColor, t);
                rt.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(fromRotation, targetRotation, t));
                yield return null;
            }

            image.color = targetColor;
            rt.localEulerAngles = new Vector3(0, 0, targetRotation);
            _tweens[index] = null;
        }
    }
}
