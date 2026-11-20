using System.Collections.Generic;
using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 벽 컷아웃 대상 표시 (2026-11-20, 하차 · 상차 트럭). 켜져 있는 동안 이 오브젝트를 가리는 구조물(주차장 지붕 · 벽 등)에 구멍이 난다.
    /// 구멍 처리는 플레이어의 WallCutout이 한다 (플레이어 포함 최대 8개).
    /// </summary>
    public sealed class WallCutoutTarget : MonoBehaviour
    {
        static readonly List<WallCutoutTarget> Registered = new();

        [SerializeField, Min(0f), Tooltip("구멍 반지름 (m)")] float radius = 3f;
        [SerializeField, Tooltip("구멍 중심 (이 오브젝트 로컬, m)")] Vector3 offset = new(0f, 1.5f, 0f);

        public static IReadOnlyList<WallCutoutTarget> All => Registered;
        public float Radius => radius;
        public Vector3 Center => transform.TransformPoint(offset);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);
    }
}
