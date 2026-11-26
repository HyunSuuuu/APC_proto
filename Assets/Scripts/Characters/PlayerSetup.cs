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

        /// <summary>
        /// 도구(구루마 · 롤테이너) 발밑 타원 그림자 (2026-11-27). 도구는 플레이어와 같이 움직이므로 구운 그림자(BakedShadows) 대신 블롭.
        /// 플레이어 자식으로 둔다 — 도구 아래에 두면 ToolRenderers(벽 거리 계산)에 섞인다.
        /// </summary>
        void ToolShadow()
        {
            var shadow = transform.Find(ToolShadowName);
            if (ToolRenderers.Length == 0)
            {
                if (shadow != null) shadow.gameObject.SetActive(false);
                return;
            }
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var r in ToolRenderers)
            {
                var b = r.localBounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector2.Min(min, new Vector2(p.x, p.z));
                    max = Vector2.Max(max, new Vector2(p.x, p.z));
                }
            }
            var center = (min + max) * 0.5f;
            shadow = Visuals.BlobShadow.Attach(gameObject, ToolShadowName, (max - min) * 1.5f, new Vector3(center.x, 0f, center.y)); // 도구 바닥판이 덮으므로 크게 — 가장자리가 밖으로 보이게
            if (shadow != null) shadow.gameObject.SetActive(true);
        }

        const string ToolShadowName = "ToolShadow";

        void Start()
        {
            Visuals.BlobShadow.Attach(gameObject, 1.1f); // 메인 라이트 그림자 대신 (2026-11-26)
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
                ToolShadow();
            }).AddTo(this);
            var interactor = GetComponent<Interactor>();
            interactor.Configure(config.PickInterval, config.InteractionDelay);
            interactor.ConfigureRules(taskLock: true, config.TransferSwitchCooldown); // GDD 5.3 — 플레이어만
        }
    }
}
