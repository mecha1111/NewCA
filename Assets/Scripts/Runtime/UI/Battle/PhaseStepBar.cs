// 담당 모듈: D (전투 비주얼/연출) — UNITY_PORTING_SPEC.md 4-8 Phase 패널 / docs/full_prototype.html .pstep2
// 의존: BattleUIBuilder가 만든 자식 Step0~Step3 (각각 Image + Label)
// 경계: 페이즈 값은 BattleUIController(모듈 C)가 넘겨준다. 여기는 그 값을 보여주는 연출만 한다.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] Phase 패널의 진행바 — 드로우 → 레디 → 액션 → 엔드 중 지금 단계를 발광으로 보여준다.
    /// [왜] 프로토타입(.pstep2)은 현재 단계에 테두리 발광 + 배경 강조를 주고 지나간 단계는 흐리게
    ///      표시해, 지금 어디쯤 와 있는지 한눈에 보이게 한다. 텍스트 한 줄("레디")만으로는 진행감이
    ///      없어서 이 바가 필요하다.
    /// [주의] 단계 판정을 여기서 하지 않는다. 어느 단계인지는 컨트롤러가 <see cref="SetStep"/>으로
    ///        알려주고, 이 컴포넌트는 색·발광만 바꾼다.
    /// </summary>
    public class PhaseStepBar : MonoBehaviour
    {
        /// <summary>
        /// [무엇] 진행바에 표시되는 단계. [출처] full_prototype.html setBattlePhase의
        /// order = ['draw','ready','action','end'] 순서 그대로.
        /// </summary>
        public enum Step
        {
            Draw = 0,
            Ready = 1,
            Action = 2,
            End = 3
        }

        /// <summary>단계 칸 수 — 위 enum과 항상 같아야 한다.</summary>
        public const int StepCount = 4;

        /// <summary>[출처] full_prototype.html .pstep2 { transition: all .25s }</summary>
        private const float TransitionMs = 250f;

        // 색 — 출처: full_prototype.html .pstep2 / .pstep2.active / .pstep2.done
        private static readonly Color IdleBg = new Color(10 / 255f, 14 / 255f, 24 / 255f, 0.5f);
        private static readonly Color ActiveBg = new Color(49 / 255f, 208 / 255f, 240 / 255f, 0.16f);
        private static readonly Color IdleText = new Color32(0x5B, 0x6B, 0x8C, 0xFF);
        private static readonly Color ActiveText = new Color32(0xEA, 0xFA, 0xFF, 0xFF);
        private static readonly Color DoneText = new Color32(0x4A, 0x6A, 0x5A, 0xFF);
        private static readonly Color ActiveBorder = new Color32(0x31, 0xD0, 0xF0, 0xFF); // --accel

        private readonly Image[] _boxes = new Image[StepCount];
        private readonly Outline[] _borders = new Outline[StepCount];
        private readonly Text[] _labels = new Text[StepCount];

        private Coroutine _tween;
        private bool _wired;

        private void Awake() => Wire();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < StepCount; i++)
            {
                var step = transform.Find($"Step{i}");
                if (step == null) continue;
                _boxes[i] = step.GetComponent<Image>();
                _labels[i] = step.Find("Label")?.GetComponent<Text>();

                // 발광 테두리는 빌더가 안 만들어도 되도록 여기서 붙인다(ActSlotView.SetFiring과 같은 패턴).
                _borders[i] = step.GetComponent<Outline>();
                if (_borders[i] == null)
                {
                    _borders[i] = step.gameObject.AddComponent<Outline>();
                    _borders[i].effectDistance = new Vector2(1.5f, 1.5f);
                }
                _borders[i].effectColor = Color.clear;
            }
        }

        /// <summary>
        /// [무엇] 현재 단계를 설정하고, 이전 표시에서 새 표시로 부드럽게 넘어간다.
        /// [왜] 즉시 바뀌면 "언제 넘어갔는지"가 눈에 안 들어온다 — 프로토타입도 0.25초 트랜지션을 준다.
        /// [주의] 같은 단계를 다시 넣으면 트윈을 새로 시작하지 않는다(깜빡임 방지).
        /// </summary>
        public void SetStep(Step step)
        {
            Wire();
            if (_current == step && _initialized) return;

            _current = step;
            _initialized = true;

            // [주의] 코루틴은 플레이 중에만 돈다 — 에디터 미리보기나 비활성 상태에선 즉시 반영한다
            //        (ResourcePips와 같은 이유).
            if (!Application.isPlaying || !gameObject.activeInHierarchy)
            {
                ApplyImmediate(step);
                return;
            }

            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(TweenTo(step));
        }

        private Step _current;
        private bool _initialized;

        private IEnumerator TweenTo(Step step)
        {
            var fromBg = new Color[StepCount];
            var fromText = new Color[StepCount];
            var fromBorder = new Color[StepCount];
            for (int i = 0; i < StepCount; i++)
            {
                fromBg[i] = _boxes[i] != null ? _boxes[i].color : IdleBg;
                fromText[i] = _labels[i] != null ? _labels[i].color : IdleText;
                fromBorder[i] = _borders[i] != null ? _borders[i].effectColor : Color.clear;
            }

            float duration = TransitionMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < StepCount; i++)
                    ApplyLerp(i, step, fromBg[i], fromText[i], fromBorder[i], t);
                yield return null;
            }

            ApplyImmediate(step);
            _tween = null;
        }

        private void ApplyLerp(int index, Step step, Color fromBg, Color fromText, Color fromBorder, float t)
        {
            var (bg, text, border) = TargetOf(index, step);
            if (_boxes[index] != null) _boxes[index].color = Color.Lerp(fromBg, bg, t);
            if (_labels[index] != null) _labels[index].color = Color.Lerp(fromText, text, t);
            if (_borders[index] != null) _borders[index].effectColor = Color.Lerp(fromBorder, border, t);
        }

        private void ApplyImmediate(Step step)
        {
            Wire();
            for (int i = 0; i < StepCount; i++)
            {
                var (bg, text, border) = TargetOf(i, step);
                if (_boxes[i] != null) _boxes[i].color = bg;
                if (_labels[i] != null) _labels[i].color = text;
                if (_borders[i] != null) _borders[i].effectColor = border;
            }
        }

        /// <summary>칸 하나의 목표 색 — active(현재) / done(지나감) / idle(아직) 세 상태.</summary>
        private static (Color bg, Color text, Color border) TargetOf(int index, Step step)
        {
            int current = (int)step;
            if (index == current) return (ActiveBg, ActiveText, ActiveBorder);
            if (index < current) return (IdleBg, DoneText, Color.clear);
            return (IdleBg, IdleText, Color.clear);
        }
    }
}
