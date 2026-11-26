using System.Collections.Generic;
using APC.Cameras;
using APC.Controls;
using APC.Core;
using APC.Data;
using APC.Logistics;
using APC.Site;
using UnityEngine;

namespace APC.Characters
{
    /// <summary>
    /// 조이스틱 입력으로 플레이어를 움직인다 (GDD 3.1, 5.1).
    /// 속도는 입력 크기에 비례하고, 방향은 카메라 Yaw 기준(화면 위 = 카메라가 보는 방향)이다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMover : MonoBehaviour
    {
        const float GroundedVelocity = -2f;

        [SerializeField] FloatingJoystick joystick;
        [SerializeField] CameraRig cameraRig;

        CharacterController controller;
        CarryStack stack;
        PlayerSetup setup;
        PlayerConfig config;
        PlayerUpgrades upgrades;
        float verticalVelocity;
        float wallRadius; // 이번 프레임 벽까지 지킬 거리 (ClampToSite가 정함, 이동 뒤 확인에도 씀)
        readonly List<Renderer> cargoRenderers = new();

        /// <summary>현재 입력 크기 0~1 (애니메이션 블렌드용).</summary>
        public float MoveAmount { get; private set; }

        /// <summary>뛰기 애니메이션 여부 (입력 크기 ≥ runThreshold).</summary>
        public bool IsRunning => config != null && MoveAmount >= config.RunThreshold;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            stack = GetComponent<CarryStack>();
            setup = GetComponent<PlayerSetup>();
        }

        void Start()
        {
            config = GameManager.Instance.Database.Player;
            upgrades = GameManager.Instance.Upgrades;
        }

        void Update()
        {
            var deltaTime = Time.deltaTime;
            if (config == null || deltaTime <= 0f) return;

            var input = joystick != null ? joystick.Value : Vector2.zero;
            MoveAmount = input.magnitude;

            var yaw = cameraRig != null ? cameraRig.Yaw : 0f;
            var direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            var horizontal = ClampToSite(direction * (upgrades.MoveSpeed * deltaTime));

            verticalVelocity = controller.isGrounded
                ? GroundedVelocity
                : verticalVelocity + Physics.gravity.y * deltaTime;

            var before = transform.position;
            controller.Move(horizontal + Vector3.up * (verticalVelocity * deltaTime));
            KeepOffWalls(before);

            if (direction.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, config.TurnSpeed * deltaTime);
            }
        }

        /// <summary>
        /// 해금된 칸 밖으로 나가지 못하게 막는다 (GDD 4.2). 벽은 콜라이더 없이 칸 경계까지 거리로 막는다. 막히면 축 하나씩 시도해 벽을 따라 미끄러진다.
        /// 반지름 = (몸 반지름, 든 상자 · 도구 끝 중 큰 것) + 벽 두께 — 방향과 상관없는 원이라 벽 앞에서 돌아도 안 뚫린다 (2026-11-19, 맨손도 벽 두께 2026-11-27 — 몸이 벽에 파묻히던 것).
        /// 이미 그 안쪽이면(벽 옆에서 집음 · 도구 업그레이드) 지금 남은 여유보다 더 다가가지 못하게만 막고, 몸이 칸 밖이면(순간이동 등) 막지 않는다.
        /// </summary>
        Vector3 ClampToSite(Vector3 move)
        {
            var grid = SiteGrid.Instance;
            wallRadius = 0f;
            if (grid == null) return move;

            var position = transform.position;
            var radius = Mathf.Max(controller.radius, CarryReach()) + config.WallClearance;
            while (radius > controller.radius && !grid.IsAreaWalkable(position, radius)) radius -= 0.05f;
            wallRadius = Mathf.Max(radius, controller.radius);
            radius = wallRadius + 0.01f; // 지금 여유보다 살짝 크게 재야 프레임마다 0.05씩 벽 쪽으로 밀려들지 않는다
            if (move.sqrMagnitude <= 0f) return move;
            if (!grid.IsAreaWalkable(position, controller.radius) || grid.IsAreaWalkable(position + move, radius)) return move;

            var alongX = new Vector3(move.x, 0f, 0f);
            if (grid.IsAreaWalkable(position + alongX, radius)) return alongX;
            var alongZ = new Vector3(0f, 0f, move.z);
            return grid.IsAreaWalkable(position + alongZ, radius) ? alongZ : Vector3.zero;
        }

        /// <summary>
        /// 이동 뒤 확인 (2026-11-27): 소품 콜라이더(책상 · 소파 등)에 비비면 CharacterController가 옆으로 밀어 ClampToSite를 거치지 않고 벽을 넘었다.
        /// 이동 전엔 벽 거리를 지켰는데 이동 뒤에 어기면 수평 위치만 이동 전으로 되돌린다.
        /// </summary>
        void KeepOffWalls(Vector3 before)
        {
            var grid = SiteGrid.Instance;
            if (grid == null || wallRadius <= 0f) return;
            var after = transform.position;
            if (grid.IsAreaWalkable(after, wallRadius) || !grid.IsAreaWalkable(before, wallRadius)) return;
            controller.enabled = false; // CharacterController는 꺼야 위치를 직접 바꿀 수 있다
            transform.position = new Vector3(before.x, after.y, before.z);
            controller.enabled = true;
        }

        /// <summary>든 상자 · 적재 도구가 몸 중심에서 수평으로 가장 멀리 닿는 거리 (m). 없으면 0. 렌더러 로컬 경계 꼭짓점으로 잰다.</summary>
        float CarryReach()
        {
            var reach = 0f;
            if (setup != null) foreach (var r in setup.ToolRenderers) reach = Mathf.Max(reach, Reach(r));
            if (stack == null) return reach;
            foreach (var cargo in stack.Items)
            {
                cargo.GetComponentsInChildren(cargoRenderers);
                foreach (var r in cargoRenderers) reach = Mathf.Max(reach, Reach(r));
            }
            return reach;
        }

        float Reach(Renderer r)
        {
            var bounds = r.localBounds;
            var max = 0f;
            for (var i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                var offset = r.transform.TransformPoint(corner) - transform.position;
                max = Mathf.Max(max, new Vector2(offset.x, offset.z).magnitude);
            }
            return max;
        }
    }
}
