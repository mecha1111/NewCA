using UnityEngine;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] "카드가 아닌" 게임 UI 텍스트(버튼·상태 라벨·로그·Phase 패널·코스트/신속 pip 등)가
    ///        공통으로 쓰는 폰트를 한 곳에서 참조한다.
    /// [왜] 카드 내부 텍스트(이름·효과·조건·종족/무기)는 CardArtLibrary.Font(KOHINanum, 카드 전용
    ///      장식 폰트)를 쓰고, 그 외 게임 UI는 이 클래스를 쓰도록 역할을 분리했다. 분리 전에는
    ///      밴픽 카드의 PickMark("✓ PICK")·BanOverlay("BAN")가 실수로 KOHINanum을 그대로 물려받아
    ///      써서, 그 폰트에 없는 글자(체크마크 "✓")가 빈 네모로 나왔다.
    /// [주의] 카드 폰트(KOHINanum)와 섞어 쓰지 않는다 — 카드 텍스트는 항상 CardArtLibrary.Font를
    ///        직접 참조한다(카드는 "정식 카드 아트"이므로 이 클래스의 대상이 아니다).
    /// </summary>
    public static class GameUIFont
    {
        /// <summary>
        /// UnityEngine.UI.Text(레거시)용 게임 UI 폰트.
        /// [주의] 지금은 적당한 게임 UI 전용 폰트가 프로젝트에 없어 Unity 내장 폰트를 쓴다
        /// (한국어 게임 UI 전용 폰트가 들어오면 여기 한 줄만 바꾸면 전체 게임 UI에 반영된다).
        /// </summary>
        public static Font Legacy => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
