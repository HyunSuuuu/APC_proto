using System.Collections.Generic;
using UnityEngine;

namespace APC.Visuals
{
    /// <summary>
    /// 캐릭터 스킨 메시 합치기 (2026-11-27). 알바 한 명 = 스킨 메시 6개 × (툰 + 외곽선 2패스 + 깊이) 드로라
    /// 알바 100명이면 배치 2000 가까이 됐다 → 켜진 SkinnedMeshRenderer들을 렌더러 하나로 합치고, 재질이 텍스처만 다르면 아틀라스로 서브메시 하나 (유니폼 6 → 1).
    /// 합친 메시는 같은 코스튬(같은 메시 · 재질 묶음)끼리 한 번 만들어 같이 쓴다. 원래 렌더러는 끈다(남겨 둠 — CharacterToon · 관찰 외곽선이 찾음).
    /// 정점은 원래 메시 공간 그대로 두고 bindpose도 그대로 이어 붙인다 — 스킨 결과(뼈 × bindpose × 정점)는 렌더러 위치와 상관없다.
    /// 메시 fbx는 Read/Write가 켜져 있어야 한다 (꺼져 있으면 합치지 않고 그대로 둔다).
    /// </summary>
    public static class SkinnedCombine
    {
        const string ChildName = "CombinedSkin";

        sealed class Baked
        {
            public Mesh mesh;
            public Vector2Int[] materialSources; // 서브메시마다 (부품 번호, 그 부품의 서브메시) — 재질을 그 부품에서 가져온다
            public Material atlasMaterial;        // 재질들을 텍스처 아틀라스 하나로 묶었으면 그 재질 (서브메시 1개), 아니면 null
        }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        static readonly Dictionary<string, Baked> Cache = new();
        static readonly List<SkinnedMeshRenderer> Parts = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Cache.Clear(); // 도메인 리로드 없는 Play에서도 지난 Play의 메시를 다시 쓰지 않게

        /// <summary>root 아래 켜진 스킨 메시를 하나로 합친다. 합친 렌더러를 돌려준다 (부품이 2개 미만이거나 읽을 수 없으면 null).</summary>
        public static SkinnedMeshRenderer Combine(GameObject root)
        {
            Parts.Clear();
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                if (r.enabled && r.sharedMesh != null && r.name != ChildName) Parts.Add(r);
            if (Parts.Count < 2) return null;
            foreach (var part in Parts)
                if (!part.sharedMesh.isReadable) return null;

            var key = Key(root);
            if (!Cache.TryGetValue(key, out var baked))
            {
                baked = Bake();
                if (baked == null) return null;
                Cache[key] = baked;
            }

            // 뼈는 부품 순서대로 이어 붙인다 (같은 뼈가 여러 번 나와도 된다 — bindpose가 부품마다 다를 수 있다)
            var bones = new List<Transform>();
            foreach (var part in Parts) bones.AddRange(part.bones);

            var first = Parts[0];
            var go = new GameObject(ChildName) { layer = first.gameObject.layer };
            go.transform.SetParent(first.transform.parent, false);
            go.transform.SetLocalPositionAndRotation(first.transform.localPosition, first.transform.localRotation);
            go.transform.localScale = first.transform.localScale;
            var combined = go.AddComponent<SkinnedMeshRenderer>();
            combined.sharedMesh = baked.mesh;
            combined.bones = bones.ToArray();
            combined.rootBone = first.rootBone;
            combined.quality = first.quality;
            combined.updateWhenOffscreen = false;
            combined.shadowCastingMode = first.shadowCastingMode;
            combined.receiveShadows = first.receiveShadows;
            combined.localBounds = Bounds(combined.rootBone != null ? combined.rootBone : go.transform);

            if (baked.atlasMaterial != null) combined.sharedMaterials = new[] { baked.atlasMaterial };
            else
            {
                var materials = new Material[baked.materialSources.Length];
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = baked.materialSources[i];
                    materials[i] = Parts[source.x].sharedMaterials[source.y];
                }
                combined.sharedMaterials = materials;
            }

            foreach (var part in Parts) part.enabled = false;
            Parts.Clear();
            return combined;
        }

        static string Key(GameObject root)
        {
            var key = new System.Text.StringBuilder(root.name);
            foreach (var part in Parts)
            {
                key.Append('|').Append(part.sharedMesh.GetInstanceID());
                foreach (var m in part.sharedMaterials) key.Append(':').Append(m != null ? m.name : "-");
            }
            return key.ToString();
        }

        static Baked Bake()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var weights = new List<BoneWeight>();
            var bindposes = new List<Matrix4x4>();
            var triangles = new List<List<int>>();
            var sources = new List<Vector2Int>();
            var materialIndex = new Dictionary<Material, int>();
            var anyColors = false;
            foreach (var part in Parts) anyColors |= part.sharedMesh.colors.Length > 0;

            var boneOffset = 0;
            for (var p = 0; p < Parts.Count; p++)
            {
                var part = Parts[p];
                var mesh = part.sharedMesh;
                var vertexOffset = vertices.Count;
                var count = mesh.vertexCount;
                vertices.AddRange(mesh.vertices);
                var n = mesh.normals;
                if (n.Length == count) normals.AddRange(n); else normals.AddRange(new Vector3[count]);
                var t = mesh.tangents;
                if (t.Length == count) tangents.AddRange(t); else tangents.AddRange(new Vector4[count]);
                var uv = mesh.uv;
                if (uv.Length == count) uvs.AddRange(uv); else uvs.AddRange(new Vector2[count]);
                if (anyColors)
                {
                    var c = mesh.colors;
                    if (c.Length == count) colors.AddRange(c);
                    else for (var i = 0; i < count; i++) colors.Add(Color.white);
                }
                foreach (var w in mesh.boneWeights)
                {
                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = w.boneIndex0 + boneOffset, weight0 = w.weight0,
                        boneIndex1 = w.boneIndex1 + boneOffset, weight1 = w.weight1,
                        boneIndex2 = w.boneIndex2 + boneOffset, weight2 = w.weight2,
                        boneIndex3 = w.boneIndex3 + boneOffset, weight3 = w.weight3,
                    });
                }
                bindposes.AddRange(mesh.bindposes);
                boneOffset += part.bones.Length;
                if (mesh.bindposes.Length != part.bones.Length) return null; // 뼈 수와 bindpose 수가 다르면 합치지 않는다

                var materials = part.sharedMaterials;
                for (var s = 0; s < mesh.subMeshCount; s++)
                {
                    var material = s < materials.Length ? materials[s] : null;
                    if (material == null) continue;
                    if (!materialIndex.TryGetValue(material, out var index))
                    {
                        index = triangles.Count;
                        materialIndex[material] = index;
                        triangles.Add(new List<int>());
                        sources.Add(new Vector2Int(p, s));
                    }
                    foreach (var i in mesh.GetTriangles(s)) triangles[index].Add(i + vertexOffset);
                }
            }

            // 재질 2~4개가 텍스처만 다르면 2×2 아틀라스 하나로 묶는다 → 서브메시 1개 = 패스마다 드로 1 (유니폼 피부 · 옷 · 머리, 2026-11-27)
            var sourceMaterials = new List<Material>();
            foreach (var source in sources) sourceMaterials.Add(Parts[source.x].sharedMaterials[source.y]);
            Material atlasMaterial = null;
            if (sourceMaterials.Count >= 2 && sourceMaterials.Count <= 4 && TryAtlas(sourceMaterials, out atlasMaterial))
            {
                // 서브메시 i의 정점 UV를 칸 i로 옮기고 삼각형을 하나로 합친다. 한 정점을 두 재질이 같이 쓰면 옮길 수 없으므로 그때는 아틀라스를 버린다
                var cellOf = new int[vertices.Count];
                for (var i = 0; i < cellOf.Length; i++) cellOf[i] = -1;
                var conflict = false;
                for (var sub = 0; sub < triangles.Count && !conflict; sub++)
                    foreach (var index in triangles[sub])
                    {
                        if (cellOf[index] >= 0 && cellOf[index] != sub) { conflict = true; break; }
                        cellOf[index] = sub;
                    }
                if (!conflict)
                {
                    for (var i = 0; i < uvs.Count; i++)
                        if (cellOf[i] >= 0) uvs[i] = uvs[i] * 0.5f + new Vector2(cellOf[i] % 2, cellOf[i] / 2) * 0.5f;
                    var all = new List<int>();
                    foreach (var list in triangles) all.AddRange(list);
                    triangles.Clear();
                    triangles.Add(all);
                }
                else
                {
                    Object.Destroy(atlasMaterial.GetTexture(BaseMapId));
                    Object.Destroy(atlasMaterial);
                    atlasMaterial = null;
                }
            }

            var combined = new Mesh { name = "CombinedSkin" };
            if (vertices.Count > 65000) combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.SetVertices(vertices);
            combined.SetNormals(normals);
            combined.SetTangents(tangents);
            combined.SetUVs(0, uvs);
            if (anyColors) combined.SetColors(colors);
            combined.boneWeights = weights.ToArray();
            combined.bindposes = bindposes.ToArray();
            combined.subMeshCount = triangles.Count;
            for (var i = 0; i < triangles.Count; i++) combined.SetTriangles(triangles[i], i);
            combined.RecalculateBounds();
            combined.UploadMeshData(true); // CPU 사본은 버린다 (다음 알바는 캐시를 쓴다)
            return new Baked { mesh = combined, materialSources = sources.ToArray(), atlasMaterial = atlasMaterial };
        }

        /// <summary>
        /// 같은 셰이더 · 같은 값(텍스처 말고)이고 _BaseMap이 같은 크기 · 형식 · 밉 수면, GPU 복사(Graphics.CopyTexture)로 2×2 아틀라스를 만들어
        /// 첫 재질 사본에 넣는다. CPU 읽기 없음 — 기기 압축 형식(ASTC 등) 그대로. 밉은 압축 블록보다 작아지기 전까지만.
        /// </summary>
        static bool TryAtlas(List<Material> materials, out Material atlasMaterial)
        {
            atlasMaterial = null;
            if ((SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.Basic) == 0) return false;
            var first = materials[0];
            if (first == null || !first.HasProperty(BaseMapId) || first.GetTexture(BaseMapId) is not Texture2D firstMap) return false;
            foreach (var m in materials)
            {
                if (m == null || m.shader != first.shader || m.GetTexture(BaseMapId) is not Texture2D map) return false;
                if (map.width != firstMap.width || map.height != firstMap.height || map.graphicsFormat != firstMap.graphicsFormat || map.mipmapCount != firstMap.mipmapCount) return false;
                if (!SameValues(first, m)) return false;
            }

            var size = firstMap.width;
            if (firstMap.height != size) return false;
            var block = (int)Mathf.Max(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetBlockWidth(firstMap.graphicsFormat), UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetBlockHeight(firstMap.graphicsFormat));
            var mips = 1;
            while (mips < firstMap.mipmapCount && (size >> mips) >= block) mips++;

            var atlas = new Texture2D(size * 2, size * 2, firstMap.graphicsFormat, mips, UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain)
            {
                name = first.name + " (Atlas)", wrapMode = TextureWrapMode.Clamp, filterMode = firstMap.filterMode, anisoLevel = firstMap.anisoLevel,
            };
            atlas.Apply(false, true); // GPU에만 둔다 (CopyTexture 결과를 CPU 사본이 덮지 않게)
            for (var i = 0; i < materials.Count; i++)
            {
                var source = (Texture2D)materials[i].GetTexture(BaseMapId);
                var x = (i % 2) * size;
                var y = (i / 2) * size;
                for (var mip = 0; mip < mips; mip++)
                    Graphics.CopyTexture(source, 0, mip, 0, 0, size >> mip, size >> mip, atlas, 0, mip, x >> mip, y >> mip);
            }
            atlasMaterial = new Material(first) { name = first.name + " (Atlas)" };
            atlasMaterial.SetTexture(BaseMapId, atlas);
            if (atlasMaterial.HasProperty(MainTexId)) atlasMaterial.SetTexture(MainTexId, atlas);
            return true;
        }

        /// <summary>텍스처를 뺀 셰이더 값(색 · 수 · 벡터)과 켜진 패스가 모두 같은가.</summary>
        static bool SameValues(Material a, Material b)
        {
            var shader = a.shader;
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                var id = shader.GetPropertyNameId(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        if (a.GetColor(id) != b.GetColor(id)) return false;
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        if (!Mathf.Approximately(a.GetFloat(id), b.GetFloat(id))) return false;
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        if (a.GetVector(id) != b.GetVector(id)) return false;
                        break;
                }
            }
            for (var p = 0; p < a.passCount; p++)
                if (a.GetShaderPassEnabled(a.GetPassName(p)) != b.GetShaderPassEnabled(a.GetPassName(p))) return false;
            return true;
        }

        /// <summary>부품 범위를 합친 범위 (rootBone 공간).</summary>
        static Bounds Bounds(Transform space)
        {
            var bounds = new Bounds();
            var any = false;
            foreach (var part in Parts)
            {
                var from = part.rootBone != null ? part.rootBone : part.transform;
                var b = part.localBounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = space.InverseTransformPoint(from.TransformPoint(corner));
                    if (any) bounds.Encapsulate(p);
                    else bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
            }
            return bounds;
        }
    }
}
