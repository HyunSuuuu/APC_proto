namespace APC.Core
{
    /// <summary>
    /// 게임 진행 상태. 여러 조건이 겹치면 Popup &gt; Paused &gt; Cutscene &gt; Playing 순으로 정한다.
    /// </summary>
    public enum GameState
    {
        /// <summary>정상 진행. timeScale = 배속.</summary>
        Playing,

        /// <summary>연출 중. 시간은 흐르지만 플레이어 입력을 막는다 (레벨업 축하, 부지 확장 팝 등).</summary>
        Cutscene,

        /// <summary>일시정지. timeScale = 0.</summary>
        Paused,

        /// <summary>팝업이 열려 있음. timeScale = 0, 하루 타이머도 멈춘다 (GDD 11.7).</summary>
        Popup,
    }
}
