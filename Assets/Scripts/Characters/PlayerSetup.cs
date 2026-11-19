using APC.Core;
using APC.Interaction;
using APC.Logistics;
using R3;
using UnityEngine;

namespace APC.Characters
{
    /// <summary>플레이어의 운반 · 상호작용 수치를 PlayerConfig에서 적용한다 (GDD 5.1). 적재 수는 업그레이드 단계(`PlayerUpgrades`)를 따른다.</summary>
    [RequireComponent(typeof(CarryStack), typeof(Interactor))]
    public sealed class PlayerSetup : MonoBehaviour
    {
        [SerializeField, Tooltip("적재 도구 단계별 모델 ([0] = 구루마, [1] = 롤테이너). 자식 StackAnchor부터 상자를 쌓는다 (GDD 5.3)")]
        GameObject[] tools = new GameObject[0];

        /// <summary>지금 켜진 적재 도구의 렌더러 (맨손이면 빈 배열). PlayerMover가 벽에 닿지 않게 쓴다.</summary>
        public Renderer[] ToolRenderers { get; private set; } = new Renderer[0];

        void Start()
        {
            var config = GameManager.Instance.Database.Player;
            var stack = GetComponent<CarryStack>();
            var handAnchor = stack.Anchor;
            var upgrades = GameManager.Instance.Upgrades;
            var animator = GetComponent<CharacterAnimator>();
            upgrades.CarryStep.Subscribe(step =>
            {
                stack.SetCapacity(upgrades.Capacity); // 업그레이드 탭 (GDD 5.1)
                if (animator != null) animator.PushTool = step; // 1 구루마 · 2 롤테이너 — 앞에 두고 민다
                Transform toolAnchor = null;
                ToolRenderers = new Renderer[0];
                for (var i = 0; i < tools.Length; i++)
                {
                    if (tools[i] == null) continue;
                    tools[i].SetActive(i == step - 1);
                    if (i != step - 1) continue;
                    toolAnchor = tools[i].transform.Find("StackAnchor");
                    ToolRenderers = tools[i].GetComponentsInChildren<Renderer>();
                }
                stack.UseAnchor(toolAnchor != null ? toolAnchor : handAnchor);
            }).AddTo(this);
            var interactor = GetComponent<Interactor>();
            interactor.Configure(config.PickInterval, config.InteractionDelay);
            interactor.ConfigureRules(taskLock: true, config.TransferSwitchCooldown); // GDD 5.3 — 플레이어만
        }
    }
}
