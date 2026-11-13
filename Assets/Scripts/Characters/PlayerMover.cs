using APC.Cameras;
using APC.Controls;
using APC.Core;
using APC.Data;
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
        PlayerConfig config;
        PlayerUpgrades upgrades;
        float verticalVelocity;

        /// <summary>현재 입력 크기 0~1 (애니메이션 블렌드용).</summary>
        public float MoveAmount { get; private set; }

        /// <summary>뛰기 애니메이션 여부 (입력 크기 ≥ runThreshold).</summary>
        public bool IsRunning => config != null && MoveAmount >= config.RunThreshold;

        void Awake() => controller = GetComponent<CharacterController>();

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

            controller.Move(horizontal + Vector3.up * (verticalVelocity * deltaTime));

            if (direction.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, config.TurnSpeed * deltaTime);
            }
        }

        /// <summary>
        /// 해금된 칸 밖으로 나가지 못하게 막는다 (GDD 4.2). 막히면 축 하나씩 시도해 벽을 따라 미끄러진다.
        /// 이미 칸 밖에 있으면(순간이동 등) 돌아올 수 있게 막지 않는다.
        /// </summary>
        Vector3 ClampToSite(Vector3 move)
        {
            var grid = SiteGrid.Instance;
            if (grid == null || move.sqrMagnitude <= 0f) return move;

            var position = transform.position;
            var radius = controller.radius;
            if (!grid.IsAreaWalkable(position, radius) || grid.IsAreaWalkable(position + move, radius)) return move;

            var alongX = new Vector3(move.x, 0f, 0f);
            if (grid.IsAreaWalkable(position + alongX, radius)) return alongX;
            var alongZ = new Vector3(0f, 0f, move.z);
            return grid.IsAreaWalkable(position + alongZ, radius) ? alongZ : Vector3.zero;
        }
    }
}
