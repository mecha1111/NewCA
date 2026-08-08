using UnityEngine.SceneManagement;

namespace CrossAccel.UI
{
    /// <summary>
    /// SPEC 6번 확정 시 흐름 — 현재 스텝의 결과는 이미 BanPickState가 기록하고 StepIndex도 올려둔
    /// 상태이므로, 여기서는 "다음에 어느 씬을 열지"만 판단한다.
    /// 씬 이름은 Build Settings에 등록돼 있어야 LoadScene(string)이 동작한다
    /// (BanPickUIBuilder.SetupScenes가 등록).
    /// </summary>
    public static class BanPickFlow
    {
        // ── 씬 이름 상수 ──────────────────────────────────────────────────────
        // [왜 상수인가] 예전에 MainMenuController가 "BattleScene"을 하드코딩해 밴픽을 통째로
        //   건너뛰는 버그가 있었다. 씬 이름을 문자열로 흩뿌리지 말고 반드시 여기를 참조한다.
        // [주의] Build Settings 등록 순서와 이름이 일치해야 LoadScene(string)이 동작한다.
        //   현재 순서: MainMenu(0) → Ban(1) → Pick(2) → Place(3) → Battle(4)
        public const string MainMenuSceneName = "MainMenuScene";
        public const string BanSceneName = "BanScene";
        public const string PickSceneName = "PickScene";
        public const string PlaceSceneName = "PlaceScene";
        public const string BattleSceneName = "BattleScene";

        /// <summary>
        /// [무엇] 현재 스텝이 끝난 뒤 다음 화면으로 넘어간다.
        /// [왜] 진행도(StepIndex)는 BanPickState가 올리므로, 여기서는 "어디로 갈지"만 판단한다.
        /// [주의] 밴픽이 다 끝나면 배틀이 아니라 <b>배치(PlaceScene)</b>로 간다.
        ///        파티 Position은 배치에서 정해지기 때문 (UNITY_PORTING_SPEC 3절 화면 흐름).
        /// </summary>
        public static void AdvanceToNextScene()
        {
            if (BanPickState.IsFinished)
            {
                SceneManager.LoadScene(PlaceSceneName);
                return;
            }

            string next = BanPickState.CurrentStep.Kind == BanPickStepKind.Ban ? BanSceneName : PickSceneName;
            SceneManager.LoadScene(next);
        }

        /// <summary>
        /// [무엇] 배치까지 끝났을 때 전투로 넘어간다.
        /// [왜] 배치 확정과 씬 전환을 PlaceSceneController가 직접 LoadScene 하지 않고 여기로 모아,
        ///      씬 이름이 한 곳에서만 관리되게 한다.
        /// </summary>
        public static void GoToBattle() => SceneManager.LoadScene(BattleSceneName);
    }
}
