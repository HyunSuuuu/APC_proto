using System.Collections.Generic;
using UnityEngine;

namespace APC.Site
{
    /// <summary>
    /// 벽처럼 플레이어를 가리면 반투명해질 오브젝트 표시 (2026-11-19, 하차 · 상차 모듈의 `Parking` 등). 자식 렌더러 전부가 대상.
    /// 반투명 처리는 SiteWalls가 한다 (WallFadeConfig).
    /// </summary>
    public sealed class WallFadeTarget : MonoBehaviour
    {
        static readonly List<WallFadeTarget> Registered = new();

        Renderer[] renderers;

        public static IReadOnlyList<WallFadeTarget> All => Registered;

        public Renderer[] Renderers => renderers ??= GetComponentsInChildren<Renderer>(true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();

        void OnEnable() => Registered.Add(this);

        void OnDisable() => Registered.Remove(this);
    }
}
