using APC.Data;
using R3;
using UnityEngine;

namespace APC.Core
{
    /// <summary>
    /// 게임 상태와 Time.timeScale을 한곳에서 관리한다 (TechStack 5.3).
    /// timeScale은 이 클래스만 바꾼다. 다른 곳은 OpenPopup / SetPaused / SetGameSpeed 등을 호출한다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] GameDatabase database;

        readonly ReactiveProperty<GameState> state = new(GameState.Playing);
        readonly ReactiveProperty<float> gameSpeed = new(1f);

        int popupCount;
        int cutsceneCount;
        bool paused;

        public GameDatabase Database => database;
        public ReadOnlyReactiveProperty<GameState> State => state;

        /// <summary>배속 (디버그 ×1/×2/×4). 일시정지·팝업 중에는 적용되지 않는다.</summary>
        public ReadOnlyReactiveProperty<float> GameSpeed => gameSpeed;

        public bool IsPaused => paused;

        /// <summary>조이스틱 · 존 상호작용을 막아야 하는 상태인가.</summary>
        public bool IsGameplayInputBlocked => state.Value != GameState.Playing;

        public Wallet Wallet { get; private set; }
        public LevelProgress Level { get; private set; }
        public PlayerUpgrades Upgrades { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[GameManager] 씬에 GameManager가 두 개 있습니다. 나중 것을 삭제합니다.", this);
                Destroy(gameObject);
                return;
            }

            var missing = "database";
            if (database == null || !database.Validate(out missing))
            {
                Debug.LogError($"[GameManager] GameDatabase 설정이 비어 있습니다: {missing}", this);
                enabled = false;
                return;
            }

            Instance = this;
            Wallet = new Wallet(database.Economy.StartingCash);
            Level = new LevelProgress(database.Levels);
            Upgrades = new PlayerUpgrades(database.Player, Wallet, Level, database.Economy.XpPurchase);
            ApplyState();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }

            Wallet?.Dispose();
            Level?.Dispose();
            Upgrades?.Dispose();
            state.Dispose();
            gameSpeed.Dispose();
        }

        /// <summary>팝업을 열 때 호출. 열린 수를 세므로 열 때마다 ClosePopup을 한 번씩 호출해야 한다.</summary>
        public void OpenPopup()
        {
            popupCount++;
            ApplyState();
        }

        public void ClosePopup()
        {
            if (popupCount == 0)
            {
                Debug.LogWarning("[GameManager] 열린 팝업이 없는데 ClosePopup이 호출됐습니다.", this);
                return;
            }
            popupCount--;
            ApplyState();
        }

        /// <summary>연출 시작. 시간은 흐르고 입력만 막힌다. EndCutscene과 짝을 맞춘다.</summary>
        public void BeginCutscene()
        {
            cutsceneCount++;
            ApplyState();
        }

        public void EndCutscene()
        {
            if (cutsceneCount == 0)
            {
                Debug.LogWarning("[GameManager] 진행 중인 연출이 없는데 EndCutscene이 호출됐습니다.", this);
                return;
            }
            cutsceneCount--;
            ApplyState();
        }

        public void SetPaused(bool value)
        {
            paused = value;
            ApplyState();
        }

        public void SetGameSpeed(float speed)
        {
            gameSpeed.Value = Mathf.Max(0.1f, speed);
            ApplyState();
        }

        void ApplyState()
        {
            state.Value = popupCount > 0 ? GameState.Popup
                : paused ? GameState.Paused
                : cutsceneCount > 0 ? GameState.Cutscene
                : GameState.Playing;

            Time.timeScale = state.Value is GameState.Popup or GameState.Paused ? 0f : gameSpeed.Value;
        }
    }
}
