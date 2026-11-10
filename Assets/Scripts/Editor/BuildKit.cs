using System.Collections.Generic;
using APC.Data;
using APC.Interaction;
using APC.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace APC.EditorTools
{
    /// <summary>
    /// 임시 프리미티브로 씬 · 프리팹을 만드는 에디터 공용 도구 (재질, 큐브, 글자, 존, UI, 직렬화 필드 설정).
    /// 모든 위치 인자는 월드 좌표다. 프리팹은 원점에서 만들므로 프리팹 안에서는 월드 = 로컬이다.
    /// </summary>
    internal static class BuildKit
    {
        const string MaterialRoot = "Assets/Art/Materials";

        static TMP_FontAsset titleFont;
        static TMP_FontAsset bodyFont;

        public static TMP_FontAsset TitleFont => titleFont != null ? titleFont
            : titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/TmoneyRoundWindExtraBold SDF.asset");

        public static TMP_FontAsset BodyFont => bodyFont != null ? bodyFont
            : bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/SUITE-Medium SDF.asset");

        public static GameDatabase Database => AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");

        /// <summary>카메라를 향한 월드 글자의 처음 회전 (실행 중에는 Billboard가 맞춘다).</summary>
        public static Quaternion CameraFacing
        {
            get
            {
                var controls = Database.Controls;
                return Quaternion.Euler(controls.Pitch, controls.Yaw, 0f);
            }
        }

        // ── 3D ──────────────────────────────────────────────────────────

        public static Material Mat(string name, Color color)
        {
            var path = $"{MaterialRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Material FloorOpen => Mat("M_FloorOpen", new Color(0.8f, 0.8f, 0.78f));
        public static Material FloorLocked => Mat("M_FloorLocked", new Color(0.5f, 0.5f, 0.52f));
        public static Material Asphalt => Mat("M_Road", new Color(0.24f, 0.24f, 0.26f));

        public static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            if (!keepCollider) Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        // ── 글자 ────────────────────────────────────────────────────────

        /// <summary>카메라를 향하는 숫자 · 이름 (외곽선 공유 재질).</summary>
        public static TMP_Text BillboardLabel(Transform parent, string text, Vector3 position, float fontSize)
        {
            var go = new GameObject("CountLabel", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, CameraFacing);
            go.AddComponent<Billboard>();
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = TitleFont;
            tmp.fontSharedMaterial = OutlineMaterial(TitleFont);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            return tmp;
        }

        /// <summary>바닥에 눕힌 글자. 모듈이 회전해도 카메라 쪽으로 읽히게 FlatLabelAligner를 붙인다.</summary>
        public static TMP_Text FlatLabel(Transform parent, string text, Vector3 position, float fontSize, Color color)
        {
            var go = new GameObject("FloorLabel", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<FlatLabelAligner>();
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.font = BodyFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 3f);
            return tmp;
        }

        /// <summary>반투명 재질 (URP Lit, Transparent · Alpha). 유리 저금통 등.</summary>
        public static Material TransparentMat(string name, Color color)
        {
            var path = $"{MaterialRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f);   // Alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.85f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>파티클용 재질 (URP Particles/Unlit, 텍스처 없음 = 네모 조각). 색은 파티클 색을 따른다.</summary>
        public static Material ParticleMat(string name)
        {
            var path = $"{MaterialRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 외곽선이 켜진 공유 재질 에셋. tmp.outlineWidth를 코드에서 바꾸면 재질 복사본이 생겨
        /// 프리팹에 저장되지 않으므로, 재질 에셋을 하나 만들어 같이 쓴다.
        /// </summary>
        static Material OutlineMaterial(TMP_FontAsset font)
        {
            var path = $"Assets/Fonts/{font.name} Outline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(font.material) { name = $"{font.name} Outline" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(40, 40, 40, 255));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static string ShortName(string content)
        {
            var paren = content.IndexOf('(');
            return paren > 0 ? content[..paren].Trim() : content;
        }

        // ── 존 ──────────────────────────────────────────────────────────

        public static T Zone<T>(Transform parent, string name, Vector3 position, Vector2 zoneSize, Color idle, Color active, string label) where T : InteractionZone
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var floor = Cube("Floor", go.transform, position + new Vector3(0f, 0.01f, 0f), new Vector3(zoneSize.x, 0.02f, zoneSize.y), Mat("M_ZoneFloor", Color.white));
            var zone = go.AddComponent<T>();
            var so = new SerializedObject(zone);
            so.FindProperty("size").vector2Value = zoneSize;
            so.FindProperty("floor").objectReferenceValue = floor.GetComponent<Renderer>();
            so.FindProperty("idleColor").colorValue = idle;
            so.FindProperty("activeColor").colorValue = active;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!string.IsNullOrEmpty(label))
                FlatLabel(go.transform, label, position + new Vector3(0f, 0.03f, -zoneSize.y * 0.5f - 0.7f), 3.6f, Color.white);
            return zone;
        }

        /// <summary>바닥 원형 게이지 (월드 캔버스).</summary>
        public static void GaugeRing(GaugeZone zone, Transform parent)
        {
            var canvasGo = new GameObject("GaugeRing", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(180f, 180f);
            rect.position = parent.position + new Vector3(0f, 0.04f, 0f);
            rect.rotation = Quaternion.Euler(90f, 0f, 0f);
            rect.localScale = Vector3.one * 0.01f;
            UIImage("Back", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.25f), BuiltinSprite("UI/Skin/Knob.psd"));
            var fill = UIImage("Fill", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.95f, 0.4f, 0.9f), BuiltinSprite("UI/Skin/Knob.psd"));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillAmount = 0f;
            var view = parent.gameObject.AddComponent<GaugeRingView>();
            SetRef(view, "zone", zone);
            SetRef(view, "fill", fill);
        }

        // ── UI ──────────────────────────────────────────────────────────

        public static Sprite BuiltinSprite(string builtinPath) => AssetDatabase.GetBuiltinExtraResource<Sprite>(builtinPath);

        public static RectTransform UIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>장식용 기본값은 raycastTarget = false. 버튼 · 입력 차단용은 만든 뒤 true로 바꾼다.</summary>
        public static Image UIImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            var rect = UIRect(name, parent, anchorMin, anchorMax, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text UIText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, float fontSize, TMP_FontAsset font, Color color)
        {
            var rect = UIRect(name, parent, anchor, anchor, position, size);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        // ── 직렬화 필드 ─────────────────────────────────────────────────

        public static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetList(Object target, string property, IReadOnlyList<Object> values)
        {
            var so = new SerializedObject(target);
            var list = so.FindProperty(property);
            list.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void DestroyIfExists(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
