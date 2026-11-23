using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace APC.UI
{
    /// <summary>
    /// (UI 샘플 SettlementReceipt에서 가져옴, 2026-11-20 — 일일 정산 영수증 말림)
    /// 영수증 위의 개별 Graphic(텍스트/아이콘/선)의 정점을 <see cref="ReceiptCurl"/>에 맞춰 휘게 한다.
    ///  - 일반 Graphic(Image 등): BaseMeshEffect.ModifyMesh 로 변형
    ///  - TextMeshPro: TMP는 IMeshModifier를 거치지 않으므로, 메시 생성 직후(TEXT_CHANGED) textInfo 정점을 직접 변형
    /// 말린 면이 옆으로 누우면 글씨는 흐려지고, 뒷면으로 넘어가면 보이지 않는다(감열지는 단면 인쇄).
    /// </summary>
    [DisallowMultipleComponent]
    public class ReceiptCurlVertex : BaseMeshEffect
    {
        public ReceiptCurl curl;
        [Tooltip("말린 면이 기울수록 알파를 줄인다")] public bool fadeOnTilt = true;

        private TMP_Text tmp;
        private bool applyingTmp;

        protected override void OnEnable()
        {
            base.OnEnable();
            tmp = GetComponent<TMP_Text>();
            if (tmp != null) TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTmpTextChanged);
        }

        protected override void OnDisable()
        {
            if (tmp != null) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTmpTextChanged);
            base.OnDisable();
        }

        /// <summary>말림 각도가 바뀌었을 때 호출: 메시를 다시 만들고 변형을 재적용한다.</summary>
        public void Refresh()
        {
            if (tmp != null)
            {
                if (tmp.isActiveAndEnabled) tmp.ForceMeshUpdate(); // → TEXT_CHANGED → ApplyToTmp
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
            if (obj == tmp && !applyingTmp) ApplyToTmp();
        }

        private void ApplyToTmp()
        {
            if (!isActiveAndEnabled || !Deforming) return;
            TMP_TextInfo info = tmp.textInfo;
            if (info == null || info.characterCount == 0) return;
            if (!TryGetMatrices(out Matrix4x4 toPaper, out Matrix4x4 fromPaper)) return;

            bool changed = false;
            for (int m = 0; m < info.materialCount; m++)
            {
                TMP_MeshInfo mesh = info.meshInfo[m];
                if (mesh.vertices == null) continue;
                int n = Mathf.Min(mesh.vertexCount, mesh.vertices.Length);
                for (int i = 0; i < n; i++)
                {
                    Vector3 pos = mesh.vertices[i];
                    Color32 c = mesh.colors32[i];
                    if (!DeformVertex(ref pos, ref c, toPaper, fromPaper)) continue;
                    mesh.vertices[i] = pos;
                    mesh.colors32[i] = c;
                    changed = true;
                }
            }

            if (!changed) return;
            applyingTmp = true;
            tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            applyingTmp = false;
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
