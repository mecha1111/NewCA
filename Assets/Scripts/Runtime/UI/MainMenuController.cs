using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrossAccel.UI
{
    /// <summary>
    /// 메인 메뉴(임시 로비) 진입점. 게임 로직이 없는 얇은 화면 전환 트리거일 뿐이라
    /// Runtime/Battle이 아니라 Runtime/UI에 둔다.
    /// "게임 시작" 버튼의 OnClick은 SceneSetupBuilder가 씬 저장 시 <see cref="StartGame"/>을
    /// 퍼시스턴트 리스너로 연결해 둔다.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public void StartGame()
        {
            // 이전 판의 정적 상태(밴픽 진행도·GameManager)를 반드시 비운다.
            // 안 그러면 StepIndex가 완료 상태로 남아 BanPickFlow가 곧바로 배틀로 보내
            // "밴픽이 통째로 건너뛰어지는" 증상이 난다.
            BattleSession.Reset();

            // 게임은 밴픽부터 시작한다 (예전엔 BattleScene을 직접 열어 밴픽을 건너뛰었다).
            SceneManager.LoadScene("SelectDeck");
        }
    }
}
