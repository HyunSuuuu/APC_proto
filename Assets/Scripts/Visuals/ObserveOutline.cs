using System.Collections.Generic;
using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 관찰 중인 알바의 외곽선을 흰색 + 바깥 얇은 검정으로 바꾼다. 알바 루트에 붙인다 (플레이어에는 붙이지 않는다).
    /// 외곽선 슬롯(셰이더가 observedMaterial과 같은 재질)만 골라 바꾸고, 원래 재질은 (렌더러, 슬롯) 단위로 기억했다 되돌린다.
    /// 스텐실 Ref는 렌더 상태라 MaterialPropertyBlock으로 못 바꾸므로 재질 슬롯을 바꾼다. 레이어는 바꾸지 않는다 (클릭 · 물리 판정 보호).
    /// 호출 예 (스테이터스 창, UI 연결은 아직 없음):
    /// <code>statusPanel.onOpen += () => target.GetComponent&lt;ObserveOutline&gt;().SetObserved(true);</code>
    /// </summary>
    public sealed class ObserveOutline : MonoBehaviour
    {
        [SerializeField, Tooltip("M_Outline_Observed (흰색, 그룹 255, OutlineOuter 켜짐)")] Material observedMaterial;

        static readonly List<ObserveOutline> all = new();
        internal static IReadOnlyList<ObserveOutline> All => all; // ObserveOutlinePicker가 탭 대상을 찾는다

        readonly List<(Renderer renderer, int slot, Material original)> swapped = new();
        bool observed;

        public bool IsObserved => observed;

        void OnEnable() => all.Add(this);

        /// <summary>켜진 SkinnedMeshRenderer 범위(AABB) 중 ray에 가장 가까이 닿는 거리.</summary>
        internal bool TryHit(Ray ray, out float distance)
        {
            // shortcut: 몸 모양이 아니라 AABB로 고른다, 알바끼리 붙어 서서 잘못 골리면 콜라이더(별도 레이어)로 바꾼다
            distance = float.MaxValue;
            var hit = false;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
                if (r.enabled && r.bounds.IntersectRay(ray, out var d) && d < distance)
                {
                    distance = d;
                    hit = true;
                }
            return hit;
        }

        public void SetObserved(bool on)
        {
            if (on == observed || observedMaterial == null) return;
            if (!on)
            {
                Restore();
                return;
            }
            observed = true;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) // 꺼진 코스튬도
            {
                var materials = r.sharedMaterials; // renderer.materials는 인스턴스를 만들므로 쓰지 않는다
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (m == null || m == observedMaterial || m.shader != observedMaterial.shader) continue;
                    swapped.Add((r, i, m));
                    materials[i] = observedMaterial;
                    changed = true;
                }
                if (changed) r.sharedMaterials = materials;
            }
        }

        void Restore()
        {
            observed = false;
            foreach (var (r, slot, original) in swapped)
            {
                if (r == null) continue;
                var materials = r.sharedMaterials;
                // 그사이 다른 코드(CharacterToon 끄기 등)가 배열을 바꿨으면 그 슬롯은 건드리지 않는다
                if (slot >= materials.Length || materials[slot] != observedMaterial) continue;
                materials[slot] = original;
                r.sharedMaterials = materials;
            }
            swapped.Clear();
        }

        [ContextMenu("Toggle Observed")]
        void Toggle() => SetObserved(!observed);

        void OnDisable()
        {
            all.Remove(this);
            SetObserved(false);
        }
    }
}
