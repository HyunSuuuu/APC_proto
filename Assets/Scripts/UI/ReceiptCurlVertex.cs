using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// (UI 샘플 SettlementReceipt에서 가져옴, 2026-11-20 — 일일 정산 영수증 말림)
    /// 영수증 위의 개별 Graphic(텍스트/아이콘/선)의 정점을 <see cref="ReceiptCurl"/>에 맞춰 휘게 한다.
    ///  - 일반 Graphic(Image 등): BaseMeshEffect.ModifyMesh 로 변형
    ///  - TextMeshPro: TMP는 IMeshModifier를 거치지 않으므로, 메시 생성 직후(TEXT_CHANGED) 원래 정점을 캐시해 두고
    ///    매 프레임 캐시 → 변형 → UpdateVertexData만 한다 (글자를 다시 조판하지 않음, 2026-11-25 최적화).
    /// 말린 면이 옆으로 누우면 글씨는 흐려지고, 뒷면으로 넘어가면 보이지 않는다(감열지는 단면 인쇄).
    /// </summary>
    [DisallowMultipleComponent]
    public class ReceiptCurlVertex : BaseMeshEffect
    {
        public ReceiptCurl curl;
        [Tooltip("말린 면이 기울수록 알파를 줄인다")] public bool fadeOnTilt = true;

        private TMP_Text tmp;
        private bool applyingTmp;

        // TMP가 만든 변형 전 정점 · 색 (머티리얼(서브메시)별). TMP가 메시를 새로 만들 때마다(TEXT_CHANGED) 다시 담는다.
        private Vector3[][] baseVerts = new Vector3[0][];
        private Color32[][] baseColors = new Color32[0][];
        private int[] baseCounts = new int[0];
        private bool baseValid;
        private bool meshDeformed; // textInfo에 지금 변형된 정점이 들어 있는지

        protected override void OnEnable()
        {
            base.OnEnable();
            tmp = GetComponent<TMP_Text>();
            baseValid = false;
            if (tmp != null) TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTmpTextChanged);
        }

        protected override void OnDisable()
        {
            if (tmp != null) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTmpTextChanged);
            baseValid = false;
            base.OnDisable();
        }

        /// <summary>말림 · 뜯기 값이 바뀌었을 때 호출: 원래 정점에서 변형을 다시 적용한다.</summary>
        public void Refresh()
        {
            if (tmp != null)
            {
                if (!tmp.isActiveAndEnabled) return;
                if (BaseMatchesMesh()) ApplyToTmp();
                else tmp.ForceMeshUpdate(); // 캐시가 없거나 메시 크기가 바뀜 → TEXT_CHANGED → CaptureBase + ApplyToTmp
            }
            else if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        // ------------------------------------------------------------------ uGUI Graphic

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || tmp != null || !Deforming || vh.currentVertCount == 0) return;
            if (!TryGetMatrices(out Matrix4x4 toPaper, out Matrix4x4 fromPaper)) return;

            UIVertex v = default;
            int count = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Vector3 pos = v.position;
                Color32 c = v.color;
                if (!DeformVertex(ref pos, ref c, toPaper, fromPaper)) continue;
                v.position = pos;
                v.color = c;
                vh.SetUIVertex(v, i);
            }
        }

        // ------------------------------------------------------------------ TextMeshPro

        private void OnTmpTextChanged(Object obj)
        {
            if (obj != tmp || applyingTmp) return;
            CaptureBase(); // TMP가 방금 만든 정점 = 변형 전 원본
            ApplyToTmp();
        }

        /// <summary>textInfo의 변형 전 정점 · 색을 캐시에 복사한다 (배열은 커질 때만 새로 만든다).</summary>
        private void CaptureBase()
        {
            meshDeformed = false;
            TMP_TextInfo info = tmp.textInfo;
            if (info == null || info.meshInfo == null) { baseValid = false; return; }

            int materials = info.meshInfo.Length;
            if (baseVerts.Length != materials)
            {
                System.Array.Resize(ref baseVerts, materials);
                System.Array.Resize(ref baseColors, materials);
                System.Array.Resize(ref baseCounts, materials);
            }
            for (int m = 0; m < materials; m++)
            {
                int n = UsableCount(info.meshInfo[m]);
                TMP_MeshInfo mesh = info.meshInfo[m];
                if (baseVerts[m] == null || baseVerts[m].Length < n)
                {
                    baseVerts[m] = new Vector3[n];
                    baseColors[m] = new Color32[n];
                }
                if (n > 0)
                {
                    System.Array.Copy(mesh.vertices, baseVerts[m], n);
                    System.Array.Copy(mesh.colors32, baseColors[m], n);
                }
                baseCounts[m] = n;
            }
            baseValid = true;
        }

        /// <summary>캐시가 지금 TMP 메시와 같은 모양(서브메시 수 · 정점 수)인지.</summary>
        private bool BaseMatchesMesh()
        {
            if (!baseValid) return false;
            TMP_TextInfo info = tmp.textInfo;
            if (info == null || info.meshInfo == null || info.meshInfo.Length != baseCounts.Length) return false;
            for (int m = 0; m < baseCounts.Length; m++)
                if (UsableCount(info.meshInfo[m]) != baseCounts[m]) return false;
            return true;
        }

        private static int UsableCount(TMP_MeshInfo mesh)
        {
            if (mesh.vertices == null || mesh.colors32 == null) return 0;
            return Mathf.Min(mesh.vertexCount, Mathf.Min(mesh.vertices.Length, mesh.colors32.Length));
        }

        /// <summary>캐시한 원래 정점에서 변형을 다시 적용해 올린다. 변형이 꺼졌으면 원래대로 되돌린다.</summary>
        private void ApplyToTmp()
        {
            if (!isActiveAndEnabled || !baseValid) return;
            TMP_TextInfo info = tmp.textInfo;
            if (info == null) return;

            Matrix4x4 toPaper = default, fromPaper = default;
            bool deform = Deforming && info.characterCount > 0 && TryGetMatrices(out toPaper, out fromPaper);
            if (!deform && !meshDeformed) return; // 이미 원래 모양

            bool changed = false;
            int materials = Mathf.Min(info.meshInfo.Length, baseCounts.Length);
            for (int m = 0; m < materials; m++)
            {
                TMP_MeshInfo mesh = info.meshInfo[m];
                int n = baseCounts[m];
                if (n == 0) continue;
                Vector3[] srcV = baseVerts[m];
                Color32[] srcC = baseColors[m];
                System.Array.Copy(srcV, mesh.vertices, n);
                System.Array.Copy(srcC, mesh.colors32, n);
                if (!deform) continue;
                for (int i = 0; i < n; i++)
                {
                    Vector3 pos = srcV[i];
                    Color32 c = srcC[i];
                    if (!DeformVertex(ref pos, ref c, toPaper, fromPaper)) continue;
                    mesh.vertices[i] = pos;
                    mesh.colors32[i] = c;
                    changed = true;
                }
            }

            if (!changed && !meshDeformed) return; // 원래 모양 그대로 → 올릴 필요 없음
            applyingTmp = true;
            tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            applyingTmp = false;
            meshDeformed = changed;
        }

        // ------------------------------------------------------------------ shared

        private bool TryGetMatrices(out Matrix4x4 toPaper, out Matrix4x4 fromPaper)
        {
            toPaper = curl.Rect.worldToLocalMatrix * transform.localToWorldMatrix;
            fromPaper = toPaper.inverse;
            float f = fromPaper.m00;
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        /// <summary>말림 또는 절취선 뜯기가 동작 중인지.</summary>
        private bool Deforming => curl != null && (curl.IsCurling || curl.TearDeforming);

        private bool DeformVertex(ref Vector3 pos, ref Color32 c, Matrix4x4 toPaper, Matrix4x4 fromPaper)
        {
            Vector3 pp = toPaper.MultiplyPoint3x4(pos);
            bool inCurl = curl.IsCurling && curl.InZone(pp.y);
            ReceiptTear tear = curl.TearDeforming ? curl.Tear : null;
            if (!inCurl && tear == null) return false;

            float facing = 1f, shade = 1f, alpha = 1f;
            Vector3 d = pp;
            if (inCurl) d = curl.Deform(pp, out facing, out shade);
            // 말림 다음에 뜯기 (남는 조각 여부는 변형 전 높이로)
            if (tear != null)
            {
                d = tear.Deform(d, tear.IsStubY(pp.y), out float tearShade, out alpha);
                shade *= tearShade;
            }
            pos = fromPaper.MultiplyPoint3x4(d);

            c.r = (byte)Mathf.Min(255f, c.r * shade);
            c.g = (byte)Mathf.Min(255f, c.g * shade);
            c.b = (byte)Mathf.Min(255f, c.b * shade);
            c.a = (byte)(c.a * alpha);
            // 글씨는 면이 ~65° 이상 기울면 사라짐 → 뒤로 넘어온 종이 뒷면과 겹쳐 비치지 않게
            if (fadeOnTilt && inCurl) c.a = (byte)(c.a * Mathf.Clamp01((facing - 0.42f) * 4f));
            return true;
        }
    }
}
