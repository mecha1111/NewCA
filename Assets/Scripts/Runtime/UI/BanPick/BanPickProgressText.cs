using System.Linq;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// 진행바 텍스트 조립 (SPEC 3/4번 "진행바 ... 현재 강조"). BanScene/PickScene 양쪽에서 같이 쓴다.
    /// 실제 순서는 항상 BanPickState.Sequence에서 읽으므로, 시퀀스가 바뀌어도(SPEC 0번: "추후 변경 가능성
    /// 있음") 여기 손댈 필요 없다.
    /// </summary>
    internal static class BanPickProgressText
    {
        public static void Update(Text label)
        {
            if (label == null) return;

            var parts = BanPickState.Sequence.Select((step, i) =>
            {
                string tag = step.Kind == BanPickStepKind.Ban ? $"밴{step.Count}" : $"픽{step.Count}";
                return i == BanPickState.StepIndex ? $"[{tag}]" : tag;
            });

            label.text = string.Join(" ▶ ", parts);
        }
    }
}
